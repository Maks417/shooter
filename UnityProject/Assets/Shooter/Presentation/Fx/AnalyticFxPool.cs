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

        /// <summary>Deterministic world scale for mesh proxies (shrink flash/spark, expand smoke).</summary>
        public static float EvaluateScale(FxKind kind, float age, float lifetime)
        {
            float t = NormalizeAge(age, lifetime);
            return kind switch
            {
                FxKind.MuzzleFlash => 0.28f + 0.38f * (1f - t) * (1f - t),
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
