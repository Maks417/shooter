#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.Fx
{
    /// <summary>
    /// Presentation-only thrown grenade arcs. Mirrors GrenadeSimulator gravity, bounce damping and ground friction
    /// (ADR-111); bounces need a world <see cref="Probe"/>, otherwise the arc flies on. No authority mutation;
    /// despawn on a nearby Impact or lifetime.
    /// </summary>
    public sealed class CosmeticGrenadeFlight
    {
        public const int MaxLive = 8;
        public const float GravityY = -24f;
        public const float BounceRetain = 0.45f;
        public const float GroundFriction = 0.85f;
        public const float Radius = 0.08f;
        public const float MaxLifetimeSeconds = 2.5f;
        public const float DespawnImpactRadiusMeters = 1.25f;
        /// <summary>Tumble rate per metre travelled (degrees), so the spin slows as the grenade rolls out.</summary>
        public const float TumbleDegreesPerMeter = 140f;

        /// <summary>Sweeps a point from <paramref name="from"/> by <paramref name="delta"/>; true with the first contact.</summary>
        public delegate bool SweepProbe(Float3 from, Float3 delta, out Float3 point, out Float3 normal);

        public SweepProbe? Probe { get; set; }

        public readonly struct Live
        {
            public EntityId ProjectileId { get; }
            public Float3 Position { get; }
            public Float3 Velocity { get; }
            public float AgeSeconds { get; }
            public bool Active { get; }
            /// <summary>Accumulated tumble angle in degrees about the axis across the travel direction.</summary>
            public float TumbleDegrees { get; }
            public int Bounces { get; }

            public Live(EntityId projectileId, Float3 position, Float3 velocity, float ageSeconds, bool active,
                float tumbleDegrees = 0f, int bounces = 0)
            {
                ProjectileId = projectileId;
                Position = position;
                Velocity = velocity;
                AgeSeconds = ageSeconds;
                Active = active;
                TumbleDegrees = tumbleDegrees;
                Bounces = bounces;
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
                var delta = new Float3(vel.X * dtSeconds, vel.Y * dtSeconds, vel.Z * dtSeconds);
                Float3 pos;
                int bounces = g.Bounces;
                if (Probe != null && Probe(g.Position, delta, out var point, out var normal))
                {
                    float nd = vel.X * normal.X + vel.Y * normal.Y + vel.Z * normal.Z;
                    if (nd < 0f)
                    {
                        vel = new Float3(
                            (vel.X - 2f * nd * normal.X) * BounceRetain,
                            (vel.Y - 2f * nd * normal.Y) * BounceRetain,
                            (vel.Z - 2f * nd * normal.Z) * BounceRetain);
                        bounces++;
                    }
                    if (normal.Y > 0.7f) vel = new Float3(vel.X * GroundFriction, vel.Y, vel.Z * GroundFriction);
                    pos = new Float3(point.X + normal.X * 0.02f, point.Y + normal.Y * 0.02f, point.Z + normal.Z * 0.02f);
                }
                else
                {
                    pos = new Float3(g.Position.X + delta.X, g.Position.Y + delta.Y, g.Position.Z + delta.Z);
                }
                float travelled = (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);
                float tumble = (g.TumbleDegrees + travelled * TumbleDegreesPerMeter) % 360f;
                slots[i] = new Live(g.ProjectileId, pos, vel, age, true, tumble, bounces);
            }
        }
    }
}
