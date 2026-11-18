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

    /// <summary>Authoritative grenade flight: gravity arc, solid bounce damp, explode on fuse (not first bounce).</summary>
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
            var filter = new QueryFilter(CollisionLayers.MotorMask, default, false);
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
                    var capsule = new Capsule(g.Position, new Float3(g.Position.X, g.Position.Y + 0.001f, g.Position.Z), Radius);
                    var sweep = world.SweepCapsule(capsule, delta, filter);
                    if (sweep.Status == QueryStatus.Hit)
                    {
                        Float3 n = sweep.Hit.Normal;
                        float nd = vel.X * n.X + vel.Y * n.Y + vel.Z * n.Z;
                        if (nd < 0f)
                        {
                            vel = new Float3(
                                (vel.X - 2f * nd * n.X) * BounceRetain,
                                (vel.Y - 2f * nd * n.Y) * BounceRetain,
                                (vel.Z - 2f * nd * n.Z) * BounceRetain);
                        }
                        Float3 hitPoint = sweep.Hit.Point;
                        g.Position = new Float3(
                            hitPoint.X + n.X * 0.02f,
                            hitPoint.Y + n.Y * 0.02f,
                            hitPoint.Z + n.Z * 0.02f);
                        if (n.Y > 0.7f)
                            vel = new Float3(vel.X * GroundFriction, vel.Y, vel.Z * GroundFriction);
                    }
                    else
                    {
                        g.Position = new Float3(g.Position.X + delta.X, g.Position.Y + delta.Y, g.Position.Z + delta.Z);
                    }
                }
                g.Velocity = vel;
            }
        }

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
