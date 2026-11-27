#nullable enable
using System;
using Shooter.Contracts;
using static Shooter.VectorMath.VecMath;

namespace Shooter.Presentation.Fx
{
    public enum FxKind : byte { MuzzleFlash = 1, ImpactSpark = 2, SmokeProxy = 3, ShellCasing = 4, Tracer = 5, Dust = 6, Fireball = 7, Shockwave = 8, Debris = 9,
        // ADR-111 grenade blast layers.
        BlastFlash = 10, FirePuff = 11, Sparks = 12, Embers = 13, DustRing = 14,
        // ADR-126: licensed fire ball drawn over the generated muzzle star.
        MuzzleFire = 15 }

    public struct FxInstance
    {
        public FxKind Kind;
        public Float3 Position;
        public Float3 Normal;
        public float Age;
        public float Lifetime;
        public bool Active;
        public byte Variant;
    }

    /// <summary>Capped FX proxies (bounded pool). Presenters draw them with generated or ADR-126 licensed flipbooks.</summary>
    public sealed class AnalyticFxPool
    {
        public const int MaxInstances = 96;
        /// <summary>ADR-111: grenade blast layers share one cap so two overlapping blasts fit without starving gunfire.</summary>
        public const int MaxBlastProxies = 40;
        public const int MaxSmokeProxies = 16;
        public const int MaxShellCasings = 24;
        public const int MaxTracers = 16;
        /// <summary>Impact/footstep dust and debris share one cap so they cannot starve muzzle flashes.</summary>
        public const int MaxDustAndDebris = 14;
        // Brief but denser combat proxies (Warface-scale feedback without particle packs).
        public const float MuzzleFlashLifetime = 0.13f;
        /// <summary>ADR-126 muzzle fire ball: a touch shorter than the star so the star's tips read last.</summary>
        public const float MuzzleFireLifetime = 0.1f;
        public const float ImpactSparkLifetime = 0.26f;
        public const float SmokeProxyLifetime = 1.45f;
        public const float ShellCasingLifetime = 0.85f;
        public const float TracerLifetime = 0.09f;
        public const float TracerLengthMeters = 9.5f;
        // ADR-111 grenade blast timings (seconds).
        public const float BlastFlashLifetime = 0.09f;
        /// <summary>ADR-126: the licensed explosion sheet burns from fire into dark smoke over this time.</summary>
        public const float FireballLifetime = 1.6f;
        public const float FirePuffLifetime = 0.75f;
        public const float SparksLifetime = 1.2f;
        public const float SparkMinLifetime = 0.5f;
        public const float EmbersLifetime = 2.5f;
        public const float ShockwaveLifetime = 0.35f;
        public const float DustRingLifetime = 1.6f;
        public const float DebrisLifetime = 1.3f;
        public const float BlastSmokeLifetime = 5.5f;
        public const float FirePuffMaxDelay = 0.12f;
        /// <summary>Variant on SmokeProxy/Debris marking the grenade blast (not gunfire smoke or surface impacts).</summary>
        public const byte BlastVariant = 0xB1;
        public const float BlastLightIntensity = 16f;
        public const float BlastLightRange = 22f;
        public const float BlastLightSeconds = 0.45f;
        public const float BlastShakeRangeMeters = 25f;
        public const float BlastShakeSeconds = 0.6f;

        private readonly FxInstance[] items = new FxInstance[MaxInstances];
        public int ActiveCount { get; private set; }
        public int SmokeCount { get; private set; }
        public int ShellCount { get; private set; }
        public int TracerCount { get; private set; }
        public int DustCount { get; private set; }
        public int BlastCount { get; private set; }
        public int Evicted { get; private set; }
        public int Spawns { get; private set; }
        public int Rejected { get; private set; }

        public static float LifetimeFor(FxKind kind) => kind switch
        {
            FxKind.MuzzleFlash => MuzzleFlashLifetime,
            FxKind.MuzzleFire => MuzzleFireLifetime,
            FxKind.ImpactSpark => ImpactSparkLifetime,
            FxKind.SmokeProxy => SmokeProxyLifetime,
            FxKind.ShellCasing => ShellCasingLifetime,
            FxKind.Tracer => TracerLifetime,
            FxKind.Dust => .9f, FxKind.Fireball => FireballLifetime, FxKind.Shockwave => ShockwaveLifetime, FxKind.Debris => DebrisLifetime,
            FxKind.BlastFlash => BlastFlashLifetime, FxKind.FirePuff => FirePuffLifetime, FxKind.Sparks => SparksLifetime,
            FxKind.Embers => EmbersLifetime, FxKind.DustRing => DustRingLifetime,
            _ => ImpactSparkLifetime
        };

        /// <summary>Variant-aware lifetime: the blast smoke column outlives gunfire smoke.</summary>
        public static float LifetimeFor(FxKind kind, byte variant) =>
            kind == FxKind.SmokeProxy && variant == BlastVariant ? BlastSmokeLifetime : LifetimeFor(kind);

        /// <summary>Blast layers share <see cref="MaxBlastProxies"/> and may evict ambient proxies.</summary>
        public static bool IsBlast(FxKind kind) =>
            kind == FxKind.Fireball || kind == FxKind.Shockwave || kind == FxKind.BlastFlash || kind == FxKind.FirePuff
            || kind == FxKind.Sparks || kind == FxKind.Embers || kind == FxKind.DustRing;

        /// <summary>Bright additive kinds hidden under ReducedFlash.</summary>
        public static bool IsFlashing(FxKind kind) =>
            kind == FxKind.MuzzleFlash || kind == FxKind.MuzzleFire || kind == FxKind.Tracer || kind == FxKind.Fireball || kind == FxKind.BlastFlash
            || kind == FxKind.FirePuff || kind == FxKind.Sparks || kind == FxKind.Shockwave;

        /// <summary>Spark streaks per quality tier (Low 40, Balanced 80, High 120).</summary>
        public static int SparkCount(int qualityTier) => qualityTier <= 0 ? 40 : qualityTier == 1 ? 80 : 120;
        /// <summary>Radial fire puffs per quality tier (6..10).</summary>
        public static int FirePuffCount(int qualityTier) => qualityTier <= 0 ? 6 : qualityTier == 1 ? 8 : 10;
        /// <summary>Blast smoke particles per tier (Low 8, otherwise 14).</summary>
        public static int BlastSmokeParticles(int qualityTier) => qualityTier <= 0 ? 8 : 14;
        public static int EmberCount(int qualityTier) => qualityTier <= 0 ? 8 : qualityTier == 1 ? 16 : 24;
        public static int DustRingParticles(int qualityTier) => qualityTier <= 0 ? 10 : qualityTier == 1 ? 16 : 22;

        /// <summary>Camera shake scale: 1 at the blast, quadratic falloff to 0 at 25 m.</summary>
        public static float BlastShakeScale(float distanceMeters)
        {
            if (float.IsNaN(distanceMeters) || distanceMeters >= BlastShakeRangeMeters) return 0f;
            if (distanceMeters <= 0f) return 1f;
            float k = 1f - distanceMeters / BlastShakeRangeMeters;
            return k * k;
        }

        /// <summary>Blast light: 16 at detonation, quadratic decay with a hot flicker, dark after 0.45 s.</summary>
        public static float BlastLightAt(float age)
        {
            if (age < 0f || age >= BlastLightSeconds) return 0f;
            float t = age / BlastLightSeconds, fade = (1f - t) * (1f - t);
            float flicker = 0.78f + 0.22f * (0.5f + 0.5f * (float)Math.Cos(age * 71f) * (float)Math.Cos(age * 43f));
            return BlastLightIntensity * fade * flicker;
        }

        /// <summary>
        /// Peak primary-lobe scale at spawn (meters). Presenter stretches this into a thin flame cone.
        /// </summary>
        public const float MuzzleFlashPeakScale = 0.62f;
        /// <summary>Secondary cross-lobe scale factor relative to primary (classic star flash).</summary>
        public const float MuzzleFlashSecondaryScale = 0.72f;
        /// <summary>Meters past the shot origin so the flame clears the viewmodel tip.</summary>
        public const float MuzzleFlashForwardPushMeters = 0.18f;
        /// <summary>Push used when the flash is anchored to the visible weapon's muzzle socket.</summary>
        public const float MuzzleSocketPushMeters = 0.05f;
        /// <summary>Extra forward offset for the secondary cross lobe.</summary>
        public const float MuzzleFlashSecondaryPushMeters = 0.025f;

        /// <summary>Deterministic world scale for mesh proxies (shrink flash/spark, expand smoke).</summary>
        public static float EvaluateScale(FxKind kind, float age, float lifetime)
        {
            float t = NormalizeAge(age, lifetime);
            return kind switch
            {
                // Hot punch then rapid shrink — elongated into a flame cone by the presenter.
                FxKind.MuzzleFlash => 0.22f + (MuzzleFlashPeakScale - 0.22f) * (1f - t) * (1f - t),
                FxKind.ImpactSpark => 0.10f + 0.26f * (1f - t),
                FxKind.SmokeProxy => 0.42f + 1.12f * t,
                FxKind.ShellCasing => 0.045f + 0.01f * (1f - t),
                FxKind.Tracer => 0.035f + 0.02f * (1f - t),
                _ => 0.1f
            };
        }

        /// <summary>Opacity cue for presenters (smoke stays translucent).</summary>
        public static float EvaluateOpacity(FxKind kind, float age, float lifetime)
        {
            float t = NormalizeAge(age, lifetime);
            return kind switch
            {
                // Hot punch then rapid falloff so flashes stay readable at 720p Low.
                FxKind.MuzzleFlash => (1f - t) * (1f - t * 0.25f),
                FxKind.ImpactSpark => (1f - t) * (1f - t * 0.12f),
                FxKind.SmokeProxy => (1f - t) * 0.50f,
                FxKind.ShellCasing => (1f - t) * 0.92f,
                FxKind.Tracer => (1f - t) * (1f - t * 0.35f),
                _ => 1f - t
            };
        }

        /// <summary>
        /// World position after age motion. Casings arc from the ejection socket; tracers stay on the shot ray.
        /// </summary>
        public static Float3 EvaluateWorldPosition(FxKind kind, Float3 spawn, Float3 normal, float age, float lifetime)
        {
            if (kind == FxKind.ShellCasing)
            {
                float t = NormalizeAge(age, lifetime);
                // Ejection: Normal is rightward ejection axis; add upward then gravity.
                float lateral = 0.55f * t + 0.85f * t * t;
                float up = 0.95f * t - 1.65f * t * t;
                float forward = 0.12f * t;
                return new Float3(
                    spawn.X + normal.X * lateral + forward * (1f - Math.Abs(normal.X)),
                    spawn.Y + up + Math.Abs(normal.Y) * lateral * 0.15f,
                    spawn.Z + normal.Z * lateral + forward * (1f - Math.Abs(normal.Z)));
            }
            if (kind == FxKind.Tracer)
            {
                // Midpoint along a short analytic streak from the muzzle.
                float half = TracerLengthMeters * 0.5f;
                return new Float3(
                    spawn.X + normal.X * half,
                    spawn.Y + normal.Y * half,
                    spawn.Z + normal.Z * half);
            }
            return spawn;
        }

        public bool TrySpawn(FxKind kind, Float3 position, Float3 normal, byte variant = 0) => TrySpawn(kind, position, normal, variant, 0f);

        /// <summary>
        /// <paramref name="delaySeconds"/> staggers a layer: the instance starts at negative age and presenters show it
        /// only once age reaches zero, so its visible lifetime is unchanged.
        /// </summary>
        public bool TrySpawn(FxKind kind, Float3 position, Float3 normal, byte variant, float delaySeconds)
        {
            bool blast = IsBlast(kind);
            // A new blast outranks the most-faded layer of an older one.
            if (blast && BlastCount >= MaxBlastProxies && EvictWhere(IsBlast) < 0) { Rejected++; return false; }
            if (kind == FxKind.SmokeProxy && SmokeCount >= MaxSmokeProxies) { Rejected++; return false; }
            if (kind == FxKind.ShellCasing && ShellCount >= MaxShellCasings) { Rejected++; return false; }
            if (kind == FxKind.Tracer && TracerCount >= MaxTracers) { Rejected++; return false; }
            if (IsDust(kind) && DustCount >= MaxDustAndDebris) { Rejected++; return false; }
            int slot = -1;
            for (int i = 0; i < MaxInstances; i++)
                if (!items[i].Active) { slot = i; break; }
            // A saturated pool must still show the shot: flashes and blasts evict the most-faded ambient proxy.
            if (slot < 0 && (kind == FxKind.MuzzleFlash || kind == FxKind.MuzzleFire || blast))
                slot = EvictWhere(IsAmbient);
            for (int i = slot < 0 ? MaxInstances : slot; i < MaxInstances; i++)
            {
                if (items[i].Active) continue;
                items[i] = new FxInstance
                {
                    Kind = kind,
                    Position = position,
                    Normal = normal.IsUnit ? normal : new Float3(0, 1, 0),
                    Age = delaySeconds > 0f ? -delaySeconds : 0f,
                    Lifetime = LifetimeFor(kind, variant),
                    Active = true, Variant=variant
                };
                ActiveCount++;
                if (kind == FxKind.SmokeProxy) SmokeCount++;
                else if (kind == FxKind.ShellCasing) ShellCount++;
                else if (kind == FxKind.Tracer) TracerCount++;
                else if (IsDust(kind)) DustCount++;
                else if (blast) BlastCount++;
                Spawns++;
                return true;
            }
            Rejected++;
            return false;
        }

        /// <summary>
        /// ADR-111 grenade blast: flash core, fireball, staggered radial fire puffs, spark streaks, embers, shockwave,
        /// ground dust ring, two debris sets and a rising smoke column. ReducedFlash drops every bright additive layer
        /// (flash, fireball, puffs, sparks, shockwave) and keeps embers, dust, debris and smoke.
        /// </summary>
        public int SpawnGrenadeBlast(Float3 position, Float3 normal, bool reducedFlash, int qualityTier)
        {
            Float3 n = normal.IsUnit ? normal : new Float3(0f, 1f, 0f);
            int spawned = 0;
            if (!reducedFlash)
            {
                if (TrySpawn(FxKind.BlastFlash, position, n)) spawned++;
                if (TrySpawn(FxKind.Fireball, position, n)) spawned++;
                int puffs = FirePuffCount(qualityTier);
                Float3 side = EjectionAxis(n), across = Cross(side, n);
                for (int p = 0; p < puffs; p++)
                {
                    float turn = p * (float)(2 * Math.PI) / puffs + 0.37f * (p & 1);
                    float c = (float)Math.Cos(turn), s = (float)Math.Sin(turn), lift = 0.35f + 0.25f * (p % 3);
                    float x = side.X * c + across.X * s + n.X * lift, y = side.Y * c + across.Y * s + n.Y * lift, z = side.Z * c + across.Z * s + n.Z * lift;
                    float inv = 1f / (float)Math.Sqrt(x * x + y * y + z * z);
                    var dir = new Float3(x * inv, y * inv, z * inv);
                    var at = new Float3(position.X + dir.X * 0.5f, position.Y + dir.Y * 0.5f, position.Z + dir.Z * 0.5f);
                    // 7 is coprime to 6, 8 and 10: every puff gets a distinct slot in the 0..120 ms stagger.
                    float delay = FirePuffMaxDelay * ((p * 7) % puffs) / (puffs - 1);
                    if (TrySpawn(FxKind.FirePuff, at, dir, (byte)p, delay)) spawned++;
                }
                if (TrySpawn(FxKind.Sparks, position, n)) spawned++;
                if (TrySpawn(FxKind.Shockwave, position, n)) spawned++;
            }
            if (TrySpawn(FxKind.Embers, position, n)) spawned++;
            if (TrySpawn(FxKind.DustRing, position, n)) spawned++;
            if (TrySpawn(FxKind.Debris, position, n, BlastVariant)) spawned++;
            if (TrySpawn(FxKind.Debris, position, n, BlastVariant, 0.04f)) spawned++;
            if (TrySpawn(FxKind.SmokeProxy, position, new Float3(0f, 1f, 0f), BlastVariant, 0.05f)) spawned++;
            return spawned;
        }

        /// <summary>
        /// Product-path gunshot FX (ADR-061): dual muzzle lobes + muzzle fire ball (ADR-126) + tracer + ejected casing.
        /// <paramref name="suppressMuzzleAndTracer"/> mirrors ReducedFlash — casings still spawn.
        /// </summary>
        public int SpawnGunshotFeedback(Float3 origin, Float3 unitDirection, bool suppressMuzzleAndTracer, byte muzzleProfile = 0,
            float forwardPushMeters = MuzzleFlashForwardPushMeters, bool sideJets = false)
        {
            Float3 dir = unitDirection.IsUnit ? unitDirection : new Float3(0f, 0f, 1f);
            Float3 eject = EjectionAxis(dir);
            int spawned = 0;

            if (!suppressMuzzleAndTracer)
            {
                var primary = new Float3(
                    origin.X + dir.X * forwardPushMeters,
                    origin.Y + dir.Y * forwardPushMeters,
                    origin.Z + dir.Z * forwardPushMeters);
                if (TrySpawn(FxKind.MuzzleFlash, primary, dir, muzzleProfile)) spawned++;
                // ADR-126: hot licensed fire ball at the bore, under the crisp star lobes.
                if (TrySpawn(FxKind.MuzzleFire, primary, dir, muzzleProfile)) spawned++;

                // Cross lobe: vertical relative to the shot so the flash reads as a star, not a marble.
                Float3 cross = CrossLobeAxis(dir);
                var secondary = new Float3(
                    primary.X + dir.X * MuzzleFlashSecondaryPushMeters,
                    primary.Y + dir.Y * MuzzleFlashSecondaryPushMeters,
                    primary.Z + dir.Z * MuzzleFlashSecondaryPushMeters);
                if (TrySpawn(FxKind.MuzzleFlash, secondary, cross, muzzleProfile)) spawned++;

                // ADR-102 muzzle brake: gas vents left and right of the bore as two smaller jets.
                if (sideJets)
                {
                    Float3 side = EjectionAxis(dir);
                    byte jet = (byte)Math.Max(10, muzzleProfile * 3 / 5);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var at = new Float3(primary.X + side.X * s * .09f, primary.Y + side.Y * s * .09f, primary.Z + side.Z * s * .09f);
                        if (TrySpawn(FxKind.MuzzleFlash, at, new Float3(side.X * s, side.Y * s, side.Z * s), jet)) spawned++;
                    }
                }

                if (TrySpawn(FxKind.Tracer, primary, dir)) spawned++;
            }

            // Casings remain under ReducedFlash (ADR-061).
            if (TrySpawn(FxKind.ShellCasing, origin, eject)) spawned++;
            return spawned;
        }

        /// <summary>
        /// The flipbook bake writes frame 0 at byte row 0, but raw texture rows start at the bottom while the
        /// texture-sheet animation numbers tiles from the top-left. Without this flip every flipbook played
        /// last frame first, so the muzzle flash only peaked after it had faded out.
        /// </summary>
        public static byte[] FlipTextureRows(System.Collections.Generic.IReadOnlyList<byte> source, int width, int height)
        {
            int stride = width * 4;
            if (source.Count != stride * height) throw new ArgumentException("RGBA32 size mismatch", nameof(source));
            var bytes = new byte[source.Count];
            for (int row = 0; row < height; row++)
            {
                int from = row * stride, to = (height - 1 - row) * stride;
                for (int x = 0; x < stride; x++) bytes[to + x] = source[from + x];
            }
            return bytes;
        }

        /// <summary>Rightward ejection axis from the shot direction (weapon-local +X approximation).</summary>
        public static Float3 EjectionAxis(Float3 unitDirection)
        {
            Float3 forward = unitDirection.IsUnit ? unitDirection : new Float3(0f, 0f, 1f);
            // Prefer world-up cross (right-handed); fall back if looking nearly vertical.
            Float3 up = new Float3(0f, 1f, 0f);
            Float3 right = Cross(up, forward);
            float lenSq = right.X * right.X + right.Y * right.Y + right.Z * right.Z;
            if (lenSq < 1e-8f)
            {
                right = Cross(new Float3(1f, 0f, 0f), forward);
                lenSq = right.X * right.X + right.Y * right.Y + right.Z * right.Z;
            }
            if (lenSq < 1e-8f) return new Float3(1f, 0f, 0f);
            float inv = 1f / (float)Math.Sqrt(lenSq);
            return new Float3(right.X * inv, right.Y * inv, right.Z * inv);
        }

        /// <summary>Perpendicular flame lobe axis for the secondary muzzle flash.</summary>
        public static Float3 CrossLobeAxis(Float3 unitDirection)
        {
            Float3 forward = unitDirection.IsUnit ? unitDirection : new Float3(0f, 0f, 1f);
            Float3 right = EjectionAxis(forward);
            Float3 up = Cross(right, forward);
            float lenSq = up.X * up.X + up.Y * up.Y + up.Z * up.Z;
            if (lenSq < 1e-8f) return new Float3(0f, 1f, 0f);
            float inv = 1f / (float)Math.Sqrt(lenSq);
            return new Float3(up.X * inv, up.Y * inv, up.Z * inv);
        }

        public void Tick(float dt)
        {
            if (dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
            int active = 0, smoke = 0, shells = 0, tracers = 0, dust = 0, blast = 0;
            for (int i = 0; i < MaxInstances; i++)
            {
                if (!items[i].Active) continue;
                items[i].Age += dt;
                if (items[i].Age >= items[i].Lifetime) { items[i].Active = false; continue; }
                active++;
                if (items[i].Kind == FxKind.SmokeProxy) smoke++;
                else if (items[i].Kind == FxKind.ShellCasing) shells++;
                else if (items[i].Kind == FxKind.Tracer) tracers++;
                else if (IsDust(items[i].Kind)) dust++;
                else if (IsBlast(items[i].Kind)) blast++;
            }
            ActiveCount = active;
            SmokeCount = smoke;
            ShellCount = shells;
            TracerCount = tracers;
            DustCount = dust;
            BlastCount = blast;
        }

        private static bool IsDust(FxKind kind) => kind == FxKind.Dust || kind == FxKind.Debris;

        private static bool IsAmbient(FxKind kind) =>
            kind == FxKind.SmokeProxy || kind == FxKind.Dust || kind == FxKind.Debris || kind == FxKind.ShellCasing;

        /// <summary>Free the matching proxy closest to the end of its life (ambient: smoke, dust, debris, casing).</summary>
        private int EvictWhere(Func<FxKind, bool> match)
        {
            int best = -1;
            float bestT = -1f;
            for (int i = 0; i < MaxInstances; i++)
            {
                if (!items[i].Active || !match(items[i].Kind)) continue;
                float t = NormalizeAge(items[i].Age, items[i].Lifetime);
                if (t > bestT) { bestT = t; best = i; }
            }
            if (best < 0) return -1;
            var kind = items[best].Kind;
            items[best].Active = false;
            ActiveCount--;
            if (kind == FxKind.SmokeProxy) SmokeCount--;
            else if (kind == FxKind.ShellCasing) ShellCount--;
            else if (IsDust(kind)) DustCount--;
            else if (IsBlast(kind)) BlastCount--;
            Evicted++;
            return best;
        }

        public void Clear()
        {
            for (int i = 0; i < MaxInstances; i++)
                items[i].Active = false;
            ActiveCount = 0;
            SmokeCount = 0;
            ShellCount = 0;
            TracerCount = 0;
            DustCount = 0;
            BlastCount = 0;
        }

        public FxInstance Get(int index) => items[index];

        private static float NormalizeAge(float age, float lifetime)
        {
            if (lifetime <= 0f) return 1f;
            float t = age / lifetime;
            if (t < 0f) return 0f;
            if (t > 1f) return 1f;
            return t;
        }
    }
}
