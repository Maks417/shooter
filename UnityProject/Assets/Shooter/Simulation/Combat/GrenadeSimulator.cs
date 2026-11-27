#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Motor;

namespace Shooter.Simulation.Combat
{
    public sealed class LiveGrenade
    {
        public EntityId Id { get; set; }
        public EntityId ThrowerId { get; set; }
        public byte ThrowerTeam { get; set; }
        public DefinitionId DefinitionId { get; set; }
        public Float3 Position { get; set; }
        public Float3 Velocity { get; set; }
        public uint ExplodeTick { get; set; }
        public uint ShotIndex { get; set; }
        public bool Active { get; set; }
    }

    public readonly struct GrenadeExplosion
    {
        public EntityId ProjectileId { get; }
        public EntityId ThrowerId { get; }
        public byte ThrowerTeam { get; }
        public Float3 Position { get; }
        public WeaponDefinition Definition { get; }
        public uint ShotIndex { get; }
        public GrenadeExplosion(EntityId projectileId, EntityId throwerId, byte throwerTeam, Float3 position, WeaponDefinition definition, uint shotIndex)
        {
            ProjectileId = projectileId; ThrowerId = throwerId; ThrowerTeam = throwerTeam;
            Position = position; Definition = definition; ShotIndex = shotIndex;
        }
    }

    /// <summary>
    /// Authoritative grenade flight: gravity arc, solid bounce damp, explode on fuse (not first bounce). The centre is
    /// placed from the sweep distance with start contacts resolved, never from the reported hit point (ADR-129).
    /// </summary>
    public sealed class GrenadeSimulator
    {
        public const int MaxLive = 16;
        public const float GravityY = -24f;
        /// <summary>Extra launch elevation added to the aim pitch so a level aim still arcs (ADR-111).</summary>
        public const float LoftDegrees = 8f;
        public const float MaxLaunchPitchDegrees = 80f;
        public const float BounceRetain = 0.45f;
        public const float Radius = 0.08f;
        public const float GroundFriction = 0.85f;
        /// <summary>Gap kept between the grenade sphere and a surface it stops at; outside the PhysX contact offset.</summary>
        public const float ContactSkin = 0.02f;
        /// <summary>A sweep this short is a start contact whose point and normal carry no information (ADR-114).</summary>
        private const float ContactDistance = 1e-3f;
        private const float InsetMeters = 0.03f;
        private static readonly QueryFilter Filter = new QueryFilter(CollisionLayers.MotorMask, default, false);
        private readonly HitBuffer overlapBuffer = new HitBuffer(16);
        private readonly LiveGrenade[] live = new LiveGrenade[MaxLive];
        private ulong nextProjectileId = 1;

        public GrenadeSimulator()
        {
            for (int i = 0; i < MaxLive; i++) live[i] = new LiveGrenade();
        }

        public void Clear()
        {
            for (int i = 0; i < MaxLive; i++) live[i].Active = false;
            nextProjectileId = 1;
        }

        public bool TrySpawn(
            EntityId throwerId,
            byte throwerTeam,
            Float3 origin,
            Float3 unitAim,
            WeaponDefinition definition,
            uint fuseTicksRemaining,
            uint tick,
            uint shotIndex,
            out LiveGrenade spawned)
        {
            spawned = live[0];
            int slot = -1;
            for (int i = 0; i < MaxLive; i++)
            {
                if (!live[i].Active) { slot = i; break; }
            }
            if (slot < 0) return false;
            float speed = definition.ThrowSpeedMetersPerSecond;
            var g = live[slot];
            g.Id = new EntityId(unchecked(nextProjectileId++));
            if (nextProjectileId == 0) nextProjectileId = 1;
            g.ThrowerId = throwerId;
            g.ThrowerTeam = throwerTeam;
            g.DefinitionId = definition.Id;
            g.Position = origin;
            Float3 launch = Loft(unitAim);
            g.Velocity = new Float3(launch.X * speed, launch.Y * speed, launch.Z * speed);
            g.ExplodeTick = unchecked(tick + Math.Max(1u, fuseTicksRemaining));
            g.ShotIndex = shotIndex;
            g.Active = true;
            spawned = g;
            return true;
        }

        public void Step(uint tick, float dt, IWorldQueries world, List<GrenadeExplosion> explosions)
        {
            explosions.Clear();
            for (int i = 0; i < MaxLive; i++)
            {
                var g = live[i];
                if (!g.Active) continue;
                if (tick >= g.ExplodeTick)
                {
                    g.Active = false;
                    explosions.Add(new GrenadeExplosion(g.Id, g.ThrowerId, g.ThrowerTeam, g.Position, WeaponDefinition.FromId(g.DefinitionId), g.ShotIndex));
                    continue;
                }

                Float3 vel = g.Velocity;
                vel = new Float3(vel.X, vel.Y + GravityY * dt, vel.Z);
                Float3 delta = new Float3(vel.X * dt, vel.Y * dt, vel.Z * dt);
                float dist = (float)Math.Sqrt(delta.LengthSquared);
                if (dist > 1e-6f)
                {
                    var sweep = SweepSphere(g.Position, delta, world);
                    if (sweep.Status == QueryStatus.Hit && sweep.Hit.DistanceMeters > ContactDistance)
                    {
                        // The centre travels the swept distance to the contact, then stands off the surface by the skin
                        // along its normal (so a grazing roll keeps moving). Hit.Point is a surface point, and (0,0,0) for
                        // a start contact, so it never places the grenade.
                        float travel = Math.Min(dist, sweep.Hit.DistanceMeters);
                        var n = sweep.Hit.Normal;
                        var reached = Advance(g.Position, delta, travel / dist);
                        g.Position = new Float3(reached.X + n.X * ContactSkin, reached.Y + n.Y * ContactSkin, reached.Z + n.Z * ContactSkin);
                        vel = Bounce(vel, n);
                    }
                    else if (sweep.Status == QueryStatus.Hit)
                    {
                        // Genuine penetration: push out along the measured overlap normal, or hold still.
                        if (TryPushOut(g.Position, world, out var pushed, out var normal))
                        {
                            g.Position = pushed;
                            vel = Bounce(vel, normal);
                        }
                        else
                        {
                            vel = default;
                        }
                    }
                    else if (sweep.Status == QueryStatus.NoHit)
                    {
                        g.Position = new Float3(g.Position.X + delta.X, g.Position.Y + delta.Y, g.Position.Z + delta.Z);
                    }
                    else
                    {
                        vel = default; // Unusable query: never move on a guess.
                    }
                }
                g.Velocity = vel;
            }
        }

        /// <summary>
        /// Launch point for a throw: the canonical muzzle, pulled back toward the eye when a wall stands between them so
        /// the grenade never starts inside geometry.
        /// </summary>
        public Float3 SafeLaunchOrigin(Float3 eye, Float3 muzzle, IWorldQueries world)
        {
            var delta = new Float3(muzzle.X - eye.X, muzzle.Y - eye.Y, muzzle.Z - eye.Z);
            float dist = (float)Math.Sqrt(delta.LengthSquared);
            if (dist <= 1e-6f) return eye;
            var sweep = SweepSphere(eye, delta, world);
            if (sweep.Status == QueryStatus.NoHit) return muzzle;
            if (sweep.Status != QueryStatus.Hit || sweep.Hit.DistanceMeters <= ContactDistance) return eye;
            float travel = Math.Min(dist, Math.Max(0f, sweep.Hit.DistanceMeters - ContactSkin));
            return Advance(eye, delta, travel / dist);
        }

        /// <summary>
        /// Sphere sweep with the PhysX start contact resolved (ADR-114, as KinematicMotor.Sweep). A sphere already touching
        /// a collider reports distance 0, point zero and normal = -direction. Re-sweep an inset sphere: no hit means the
        /// motion leaves the touch; a real hit gives the true normal and, minus the inset, the true distance. If the inset
        /// sphere also starts in contact the raw start-contact hit is returned (genuine penetration).
        /// </summary>
        private static SweepHit SweepSphere(Float3 position, Float3 delta, IWorldQueries world)
        {
            var sweep = world.SweepCapsule(Sphere(position, Radius), delta, Filter);
            if (sweep.Status != QueryStatus.Hit || sweep.Hit.DistanceMeters > ContactDistance) return sweep;
            var retry = world.SweepCapsule(Sphere(position, Radius - InsetMeters), delta, Filter);
            if (retry.Status == QueryStatus.NoHit) return retry;
            if (retry.Status != QueryStatus.Hit || retry.Hit.DistanceMeters <= ContactDistance) return sweep;
            var h = retry.Hit;
            // The outer sphere was only touching, so it may still travel up to the inset before contact.
            float distance = Math.Max(2f * ContactDistance, h.DistanceMeters - InsetMeters);
            return new SweepHit(QueryStatus.Hit, new QueryHit(distance, h.Point, h.Normal, h.EntityId, h.StaticSurfaceId,
                h.ColliderStableId, h.SurfaceTag, h.CollisionRevision));
        }

        private bool TryPushOut(Float3 position, IWorldQueries world, out Float3 pushed, out Float3 normal)
        {
            pushed = position; normal = default;
            var result = world.Overlap(Sphere(position, Radius), Filter, overlapBuffer);
            if (result.Status != QueryStatus.Hit) return false;
            float bestDepth = 1e-4f;
            bool found = false;
            for (int i = 0; i < result.WrittenCount; i++)
            {
                var hit = overlapBuffer[i];
                if (hit.DistanceMeters > bestDepth && hit.Normal.LengthSquared > 0.5f)
                {
                    bestDepth = hit.DistanceMeters; normal = hit.Normal; found = true;
                }
            }
            if (!found) return false;
            float push = Math.Min(bestDepth + ContactSkin, 2f * Radius + ContactSkin);
            pushed = new Float3(position.X + normal.X * push, position.Y + normal.Y * push, position.Z + normal.Z * push);
            return true;
        }

        private static Float3 Bounce(Float3 vel, Float3 n)
        {
            float nd = vel.X * n.X + vel.Y * n.Y + vel.Z * n.Z;
            if (nd < 0f)
            {
                vel = new Float3(
                    (vel.X - 2f * nd * n.X) * BounceRetain,
                    (vel.Y - 2f * nd * n.Y) * BounceRetain,
                    (vel.Z - 2f * nd * n.Z) * BounceRetain);
            }
            if (n.Y > 0.7f)
                vel = new Float3(vel.X * GroundFriction, vel.Y, vel.Z * GroundFriction);
            return vel;
        }

        private static Float3 Advance(Float3 from, Float3 delta, float fraction) =>
            new Float3(from.X + delta.X * fraction, from.Y + delta.Y * fraction, from.Z + delta.Z * fraction);

        private static Capsule Sphere(Float3 centre, float radius) =>
            new Capsule(centre, new Float3(centre.X, centre.Y + 0.001f, centre.Z), radius);

        /// <summary>Raises a unit aim vector by <see cref="LoftDegrees"/>, capped at <see cref="MaxLaunchPitchDegrees"/>.</summary>
        public static Float3 Loft(Float3 unitAim)
        {
            double h = Math.Sqrt(unitAim.X * (double)unitAim.X + unitAim.Z * (double)unitAim.Z);
            if (h < 1e-6) return unitAim;
            double pitch = Math.Atan2(unitAim.Y, h);
            double lofted = Math.Min(pitch + LoftDegrees * Math.PI / 180.0, MaxLaunchPitchDegrees * Math.PI / 180.0);
            if (lofted <= pitch) return unitAim;
            double c = Math.Cos(lofted) / h;
            return new Float3((float)(unitAim.X * c), (float)Math.Sin(lofted), (float)(unitAim.Z * c));
        }

        public static ushort BlastDamage(WeaponDefinition def, float distanceMeters)
        {
            float r = def.BlastRadiusMeters;
            if (r <= 0f || distanceMeters >= r) return 0;
            float t = 1f - distanceMeters / r;
            return (ushort)Math.Max(1, (int)Math.Floor(def.BaseDamage * t + 0.5f));
        }
    }
}
