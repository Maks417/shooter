#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Presentation.Fx;
using Xunit;

namespace Shooter.Simulation.Tests.Fx
{
    /// <summary>ADR-127: every pellet of a shotgun blast after pellet 0 draws its own tracer.</summary>
    public sealed class PelletTracerTrackerTests
    {
        private static readonly EntityId Shooter = new EntityId(7);
        private static readonly EntityId Other = new EntityId(8);
        private static readonly EntityId Victim = new EntityId(9);
        private static readonly Float3 Muzzle = new Float3(0f, 1.5f, 0f);
        private static ushort ordinal;

        private static EventId Id(uint tick, EntityId entity) => new EventId(1, tick, entity, ordinal++);

        private static Float3 Unit(float x, float y, float z)
        {
            float inv = 1f / (float)Math.Sqrt(x * x + y * y + z * z);
            return new Float3(x * inv, y * inv, z * inv);
        }

        private static CombatEvent Shot(uint tick, EntityId shooter, Float3 direction, string weapon = "weapons/m870") =>
            new CombatEvent(Id(tick, shooter), CombatEventKind.ShotAccepted, shooter, default,
                new ShotPayload(new DefinitionId(weapon), 1, Muzzle, direction, 99, 1));

        private static CombatEvent Impact(uint tick, EntityId shooter, Float3 point, EntityId target = default) =>
            new CombatEvent(Id(tick, shooter), CombatEventKind.Impact, shooter, target,
                new ImpactPayload(point, new Float3(0, 0, -1), 50, (ushort)(target.IsValid ? 2 : 0)));

        private static Float3 Along(Float3 dir, float meters) =>
            new Float3(Muzzle.X + dir.X * meters, Muzzle.Y + dir.Y * meters, Muzzle.Z + dir.Z * meters);

        private static int CountPellets(PelletTracerTracker tracker, IEnumerable<CombatEvent> events)
        {
            int pellets = 0;
            foreach (var ev in events)
            {
                if (ev.Kind == CombatEventKind.ShotAccepted) tracker.ObserveShot(ev, ((ShotPayload)ev.Payload).WeaponId.ToString() != "weapons/knife");
                else if (tracker.TryPelletImpact(ev, out _)) pellets++;
            }
            return pellets;
        }

        [Fact]
        public void A_blast_with_eight_impacts_yields_seven_pellet_tracers()
        {
            var primary = Unit(0f, -0.05f, 1f);
            var events = new List<CombatEvent> { Shot(10, Shooter, primary), Impact(10, Shooter, Along(primary, 6f), Victim) };
            for (int p = 1; p < 8; p++)
            {
                var dir = Unit(0.03f * (p - 4), -0.05f + 0.01f * p, 1f);
                events.Add(Impact(10, Shooter, Along(dir, 6f + p), p % 2 == 0 ? Victim : default));
                // Damage between pellet impacts does not disturb the tracker.
                events.Add(new CombatEvent(Id(10, Shooter), CombatEventKind.DamageApplied, Shooter, Victim, new DamagePayload(50, 50, 2)));
            }
            Assert.Equal(7, CountPellets(new PelletTracerTracker(), events));
        }

        [Fact]
        public void A_single_projectile_shot_yields_no_extra_tracer()
        {
            var dir = Unit(0.1f, 0f, 1f);
            var events = new[] { Shot(3, Shooter, dir, "weapons/m16"), Impact(3, Shooter, Along(dir, 23.7f)) };
            Assert.Equal(0, CountPellets(new PelletTracerTracker(), events));
        }

        [Fact]
        public void Impacts_of_another_shooter_tick_or_a_knife_are_not_pellets()
        {
            var primary = Unit(0f, 0f, 1f);
            var side = Unit(0.2f, 0f, 1f);
            var tracker = new PelletTracerTracker();
            tracker.ObserveShot(Shot(5, Shooter, primary));
            Assert.False(tracker.TryPelletImpact(Impact(5, Other, Along(side, 5f)), out _));
            Assert.False(tracker.TryPelletImpact(Impact(6, Shooter, Along(side, 5f)), out _));
            Assert.True(tracker.TryPelletImpact(Impact(5, Shooter, Along(side, 5f)), out var point));
            Assert.Equal(Along(side, 5f), point);
            // A knife strike replaces the shot and draws nothing.
            tracker.ObserveShot(Shot(5, Shooter, primary, "weapons/knife"), drawsTracers: false);
            Assert.False(tracker.TryPelletImpact(Impact(5, Shooter, Along(side, 1f)), out _));
            tracker.ObserveShot(Shot(9, Shooter, primary));
            tracker.Clear();
            Assert.False(tracker.TryPelletImpact(Impact(9, Shooter, Along(side, 5f)), out _));
        }

        [Fact]
        public void A_grenade_blast_from_the_shooter_in_the_same_tick_is_not_a_pellet()
        {
            var tracker = new PelletTracerTracker();
            tracker.ObserveShot(Shot(12, Shooter, Unit(0f, 0f, 1f)));
            var blast = new CombatEvent(Id(12, Shooter), CombatEventKind.Impact, Shooter, default,
                new ImpactPayload(new Float3(4f, 0f, 9f), new Float3(0, 1, 0), 1, 0));
            Assert.False(tracker.TryPelletImpact(blast, out _));
        }

        [Fact]
        public void Pellet_tracer_points_from_the_muzzle_at_the_impact()
        {
            var pool = new AnalyticFxPool();
            var impact = new Float3(3f, 1f, 12f);
            Assert.True(pool.SpawnPelletTracer(Muzzle, impact, 0.05f));
            Assert.False(pool.SpawnPelletTracer(Muzzle, Muzzle, 0.05f));
            Assert.Equal(1, pool.TracerCount);
            FxInstance tracer = default;
            for (int i = 0; i < AnalyticFxPool.MaxInstances; i++)
                if (pool.Get(i).Active && pool.Get(i).Kind == FxKind.Tracer) tracer = pool.Get(i);
            var dir = Unit(impact.X - Muzzle.X, impact.Y - Muzzle.Y, impact.Z - Muzzle.Z);
            Assert.Equal(dir.X, tracer.Normal.X, 4);
            Assert.Equal(dir.Y, tracer.Normal.Y, 4);
            Assert.Equal(dir.Z, tracer.Normal.Z, 4);
            Assert.Equal(Muzzle.X + dir.X * 0.05f, tracer.Position.X, 4);
            Assert.Equal(Muzzle.Z + dir.Z * 0.05f, tracer.Position.Z, 4);
        }

        [Fact]
        public void One_full_blast_fits_beside_ordinary_gunfire()
        {
            Assert.True(AnalyticFxPool.MaxTracers >= 8 + 16);
        }
    }
}
