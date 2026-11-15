#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.Fx
{
    public enum FxKind : byte { MuzzleFlash = 1, ImpactSpark = 2, SmokeProxy = 3, ShellCasing = 4, Tracer = 5 }

    public struct FxInstance
    {
        public FxKind Kind;
        public Float3 Position;
        public Float3 Normal;
        public float Age;
        public float Lifetime;
        public bool Active;
    }

    /// <summary>Capped analytic mesh FX proxies. No sprites/images; bounded pool.</summary>
    public sealed class AnalyticFxPool
    {
        public const int MaxInstances = 64;
        public const int MaxSmokeProxies = 16;
        public const int MaxShellCasings = 24;
        public const int MaxTracers = 16;
        // Brief but denser combat proxies (Warface-scale feedback without particle packs).
        public const float MuzzleFlashLifetime = 0.13f;
        public const float ImpactSparkLifetime = 0.26f;
        public const float SmokeProxyLifetime = 1.45f;
        public const float ShellCasingLifetime = 0.85f;
        public const float TracerLifetime = 0.09f;
        public const float TracerLengthMeters = 9.5f;

        private readonly FxInstance[] items = new FxInstance[MaxInstances];
        public int ActiveCount { get; private set; }
        public int SmokeCount { get; private set; }
        public int ShellCount { get; private set; }
        public int TracerCount { get; private set; }
        public int Spawns { get; private set; }
        public int Rejected { get; private set; }

        public static float LifetimeFor(FxKind kind) => kind switch
        {
            FxKind.MuzzleFlash => MuzzleFlashLifetime,
            FxKind.ImpactSpark => ImpactSparkLifetime,
            FxKind.SmokeProxy => SmokeProxyLifetime,
            FxKind.ShellCasing => ShellCasingLifetime,
            FxKind.Tracer => TracerLifetime,
            _ => ImpactSparkLifetime
        };

        /// <summary>
        /// Peak primary-lobe scale at spawn (meters). Presenter stretches this into a thin flame cone.
        /// </summary>
        public const float MuzzleFlashPeakScale = 0.62f;
        /// <summary>Secondary cross-lobe scale factor relative to primary (classic star flash).</summary>
        public const float MuzzleFlashSecondaryScale = 0.72f;
        /// <summary>Meters past the shot origin so the flame clears the viewmodel tip.</summary>
        public const float MuzzleFlashForwardPushMeters = 0.18f;
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

        public bool TrySpawn(FxKind kind, Float3 position, Float3 normal)
        {
            if (kind == FxKind.SmokeProxy && SmokeCount >= MaxSmokeProxies) { Rejected++; return false; }
            if (kind == FxKind.ShellCasing && ShellCount >= MaxShellCasings) { Rejected++; return false; }
            if (kind == FxKind.Tracer && TracerCount >= MaxTracers) { Rejected++; return false; }
            for (int i = 0; i < MaxInstances; i++)
            {
                if (items[i].Active) continue;
                items[i] = new FxInstance
                {
                    Kind = kind,
                    Position = position,
                    Normal = normal.IsUnit ? normal : new Float3(0, 1, 0),
                    Age = 0,
                    Lifetime = LifetimeFor(kind),
                    Active = true
                };
                ActiveCount++;
                if (kind == FxKind.SmokeProxy) SmokeCount++;
                else if (kind == FxKind.ShellCasing) ShellCount++;
                else if (kind == FxKind.Tracer) TracerCount++;
                Spawns++;
                return true;
            }
            Rejected++;
            return false;
        }

        /// <summary>
        /// Product-path gunshot FX (ADR-061): dual muzzle lobes + tracer + ejected casing.
        /// <paramref name="suppressMuzzleAndTracer"/> mirrors ReducedFlash — casings still spawn.
        /// </summary>
        public int SpawnGunshotFeedback(Float3 origin, Float3 unitDirection, bool suppressMuzzleAndTracer)
        {
            Float3 dir = unitDirection.IsUnit ? unitDirection : new Float3(0f, 0f, 1f);
            Float3 eject = EjectionAxis(dir);
            int spawned = 0;

            if (!suppressMuzzleAndTracer)
            {
                var primary = new Float3(
                    origin.X + dir.X * MuzzleFlashForwardPushMeters,
                    origin.Y + dir.Y * MuzzleFlashForwardPushMeters,
                    origin.Z + dir.Z * MuzzleFlashForwardPushMeters);
                if (TrySpawn(FxKind.MuzzleFlash, primary, dir)) spawned++;

                // Cross lobe: vertical relative to the shot so the flash reads as a star, not a marble.
                Float3 cross = CrossLobeAxis(dir);
                var secondary = new Float3(
                    primary.X + dir.X * MuzzleFlashSecondaryPushMeters,
                    primary.Y + dir.Y * MuzzleFlashSecondaryPushMeters,
                    primary.Z + dir.Z * MuzzleFlashSecondaryPushMeters);
                if (TrySpawn(FxKind.MuzzleFlash, secondary, cross)) spawned++;

                if (TrySpawn(FxKind.Tracer, primary, dir)) spawned++;
            }

            // Casings remain under ReducedFlash (ADR-061).
            if (TrySpawn(FxKind.ShellCasing, origin, eject)) spawned++;
            return spawned;
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

        private static Float3 Cross(Float3 a, Float3 b) =>
            new Float3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public void Tick(float dt)
        {
            if (dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
            int active = 0, smoke = 0, shells = 0, tracers = 0;
            for (int i = 0; i < MaxInstances; i++)
            {
                if (!items[i].Active) continue;
                items[i].Age += dt;
                if (items[i].Age >= items[i].Lifetime) { items[i].Active = false; continue; }
                active++;
                if (items[i].Kind == FxKind.SmokeProxy) smoke++;
                else if (items[i].Kind == FxKind.ShellCasing) shells++;
                else if (items[i].Kind == FxKind.Tracer) tracers++;
            }
            ActiveCount = active;
            SmokeCount = smoke;
            ShellCount = shells;
            TracerCount = tracers;
        }

        public void Clear()
        {
            for (int i = 0; i < MaxInstances; i++)
                items[i].Active = false;
            ActiveCount = 0;
            SmokeCount = 0;
            ShellCount = 0;
            TracerCount = 0;
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
