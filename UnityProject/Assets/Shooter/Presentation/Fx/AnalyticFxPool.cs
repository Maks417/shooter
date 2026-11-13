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
        private readonly FxInstance[] items = new FxInstance[MaxInstances];
        public int ActiveCount { get; private set; }
        public int SmokeCount { get; private set; }
        public int Spawns { get; private set; }
        public int Rejected { get; private set; }

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
                    Lifetime = kind == FxKind.SmokeProxy ? 1.5f : 0.2f,
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

        public FxInstance Get(int index) => items[index];
    }
}
