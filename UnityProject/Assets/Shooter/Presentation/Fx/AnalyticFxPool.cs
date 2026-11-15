#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.Fx
{
    public enum FxKind : byte { MuzzleFlash = 1, ImpactSpark = 2, SmokeProxy = 3 }

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
        // Brief but denser combat proxies (Warface-scale feedback without particle packs).
        public const float MuzzleFlashLifetime = 0.13f;
        public const float ImpactSparkLifetime = 0.26f;
        public const float SmokeProxyLifetime = 1.45f;

        private readonly FxInstance[] items = new FxInstance[MaxInstances];
        public int ActiveCount { get; private set; }
        public int SmokeCount { get; private set; }
        public int Spawns { get; private set; }
        public int Rejected { get; private set; }

        public static float LifetimeFor(FxKind kind) => kind switch
        {
            FxKind.MuzzleFlash => MuzzleFlashLifetime,
            FxKind.ImpactSpark => ImpactSparkLifetime,
            FxKind.SmokeProxy => SmokeProxyLifetime,
            _ => ImpactSparkLifetime
        };

        /// <summary>Deterministic world scale for mesh proxies (shrink flash/spark, expand smoke).</summary>
        public static float EvaluateScale(FxKind kind, float age, float lifetime)
        {
            float t = NormalizeAge(age, lifetime);
            return kind switch
            {
                FxKind.MuzzleFlash => 0.20f + 0.32f * (1f - t) * (1f - t),
                FxKind.ImpactSpark => 0.10f + 0.26f * (1f - t),
                FxKind.SmokeProxy => 0.42f + 1.12f * t,
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
                _ => 1f - t
            };
        }

        public bool TrySpawn(FxKind kind, Float3 position, Float3 normal)
        {
            if (kind == FxKind.SmokeProxy && SmokeCount >= MaxSmokeProxies) { Rejected++; return false; }
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
                Spawns++;
                return true;
            }
            Rejected++;
            return false;
        }

        public void Tick(float dt)
        {
            if (dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
            int active = 0, smoke = 0;
            for (int i = 0; i < MaxInstances; i++)
            {
                if (!items[i].Active) continue;
                items[i].Age += dt;
                if (items[i].Age >= items[i].Lifetime) { items[i].Active = false; continue; }
                active++;
                if (items[i].Kind == FxKind.SmokeProxy) smoke++;
            }
            ActiveCount = active;
            SmokeCount = smoke;
        }

        public void Clear()
        {
            for (int i = 0; i < MaxInstances; i++)
                items[i].Active = false;
            ActiveCount = 0;
            SmokeCount = 0;
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
