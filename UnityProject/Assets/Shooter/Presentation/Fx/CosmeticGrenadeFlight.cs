#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.Fx
{
    /// <summary>
    /// Presentation-only thrown grenade arcs. Mirrors GrenadeSimulator gravity; no authority mutation.
    /// Bounce is omitted here (no world queries); despawn on nearby Impact or lifetime.
    /// </summary>
    public sealed class CosmeticGrenadeFlight
    {
        public const int MaxLive = 8;
        public const float GravityY = -20f;
        public const float MaxLifetimeSeconds = 2.5f;
        public const float DespawnImpactRadiusMeters = 1.25f;

        public readonly struct Live
        {
            public EntityId ProjectileId { get; }
            public Float3 Position { get; }
            public Float3 Velocity { get; }
            public float AgeSeconds { get; }
            public bool Active { get; }

            public Live(EntityId projectileId, Float3 position, Float3 velocity, float ageSeconds, bool active)
            {
                ProjectileId = projectileId;
                Position = position;
                Velocity = velocity;
                AgeSeconds = ageSeconds;
                Active = active;
            }
        }

        private readonly Live[] slots = new Live[MaxLive];

        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxLive; i++)
                    if (slots[i].Active) n++;
                return n;
            }
        }

        public Live this[int index] => slots[index];

        public void Clear()
        {
            for (int i = 0; i < MaxLive; i++) slots[i] = default;
        }

        public bool TrySpawn(EntityId projectileId, Float3 origin, Float3 velocity)
        {
            if (!projectileId.IsValid) return false;
            int free = -1;
            for (int i = 0; i < MaxLive; i++)
            {
                if (slots[i].Active && slots[i].ProjectileId.Equals(projectileId))
                {
                    slots[i] = new Live(projectileId, origin, velocity, 0f, true);
                    return true;
                }
                if (!slots[i].Active && free < 0) free = i;
            }
            if (free < 0) return false;
            slots[free] = new Live(projectileId, origin, velocity, 0f, true);
            return true;
        }

        public void ObserveImpact(Float3 position)
        {
            for (int i = 0; i < MaxLive; i++)
            {
                if (!slots[i].Active) continue;
                float dx = slots[i].Position.X - position.X;
                float dy = slots[i].Position.Y - position.Y;
                float dz = slots[i].Position.Z - position.Z;
                if (dx * dx + dy * dy + dz * dz <= DespawnImpactRadiusMeters * DespawnImpactRadiusMeters)
                    slots[i] = default;
            }
        }

        public void Tick(float dtSeconds)
        {
            if (!float.IsFinite(dtSeconds) || dtSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(dtSeconds));
            if (dtSeconds == 0f) return;
            for (int i = 0; i < MaxLive; i++)
            {
                var g = slots[i];
                if (!g.Active) continue;
                float age = g.AgeSeconds + dtSeconds;
                if (age >= MaxLifetimeSeconds)
                {
                    slots[i] = default;
                    continue;
                }
                var vel = new Float3(g.Velocity.X, g.Velocity.Y + GravityY * dtSeconds, g.Velocity.Z);
                var pos = new Float3(
                    g.Position.X + vel.X * dtSeconds,
                    g.Position.Y + vel.Y * dtSeconds,
                    g.Position.Z + vel.Z * dtSeconds);
                slots[i] = new Live(g.ProjectileId, pos, vel, age, true);
            }
        }
    }
}
