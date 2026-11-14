#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Simulation;
using Xunit;

namespace Shooter.Simulation.Tests.Harness
{
    public sealed class WorldKillVolumeTests
    {
        private static readonly Aabb Bounds = new Aabb(new Float3(-50, 0, -50), new Float3(50, 20, 50));

        [Theory]
        [InlineData(0f, -10f, 0f, false)]     // exactly at threshold: strict
        [InlineData(0f, -10.01f, 0f, true)]
        [InlineData(60f, 0f, 0f, false)]      // exactly 10 m outside X: strict
        [InlineData(60.01f, 0f, 0f, true)]
        [InlineData(-60.01f, 0f, 0f, true)]
        [InlineData(0f, 0f, 60.01f, true)]
        [InlineData(0f, 0f, -60.01f, true)]
        [InlineData(0f, 5f, 0f, false)]
        public void Rule_uses_strict_comparisons(float x, float y, float z, bool expected) =>
            Assert.Equal(expected, WorldKillVolume.IsOutOfWorld(new Float3(x, y, z), Bounds));

        [Fact]
        public void Actor_without_floor_dies_environmentally_once()
        {
            var sim = new CombatHarnessSimulation(new NoFloorWorld());
            sim.SetWorldKillBounds(Bounds);
            var id = sim.SpawnActor(new Float3(0, -9.95f, 0), 0, 1);
            uint deathTick = uint.MaxValue;
            int deaths = 0;
            for (uint t = 0; t < 60; t++)
            {
                sim.Step(t, ReadOnlyMemory<InputCommand>.Empty);
                using var ev = sim.AcquireEvents();
                for (int i = 0; i < ev.Events.Count; i++)
                {
                    var e = ev.Events[i];
                    if (e.Kind != CombatEventKind.Death) continue;
                    deaths++;
                    deathTick = t;
                    Assert.Equal(id, e.Target);
                    Assert.False(e.Instigator.IsValid);
                    Assert.False(Assert.IsType<DeathPayload>(e.Payload).Killer.IsValid);
                }
            }
            Assert.Equal(1, deaths);
            Assert.True(deathTick > 0 && deathTick < 30);
            using var snap = sim.AcquireSnapshot();
            Assert.Equal(Stance.Dead, snap.Snapshot.Entities[0].Stance);
            Assert.Equal((ushort)0, snap.Snapshot.Entities[0].Health);
        }

        [Fact]
        public void Unset_bounds_disable_the_kill_volume()
        {
            var sim = new CombatHarnessSimulation(new NoFloorWorld());
            var id = sim.SpawnActor(new Float3(0, -500f, 0), 0, 1);
            for (uint t = 0; t < 5; t++)
            {
                sim.Step(t, ReadOnlyMemory<InputCommand>.Empty);
                using var ev = sim.AcquireEvents();
                for (int i = 0; i < ev.Events.Count; i++) Assert.NotEqual(CombatEventKind.Death, ev.Events[i].Kind);
            }
            using var snap = sim.AcquireSnapshot();
            Assert.NotEqual(Stance.Dead, snap.Snapshot.Entities[0].Stance);
            Assert.Equal(id, snap.Snapshot.Entities[0].Id);
        }

        [Fact]
        public void Actor_outside_xz_bounds_dies_and_dead_actor_is_not_killed_again()
        {
            var sim = new CombatHarnessSimulation(new NoFloorWorld());
            sim.SetWorldKillBounds(Bounds);
            sim.SpawnActor(new Float3(61, 5, 0), 0, 1);
            int deaths = 0;
            for (uint t = 0; t < 4; t++)
            {
                sim.Step(t, ReadOnlyMemory<InputCommand>.Empty);
                using var ev = sim.AcquireEvents();
                for (int i = 0; i < ev.Events.Count; i++) if (ev.Events[i].Kind == CombatEventKind.Death) deaths++;
            }
            Assert.Equal(1, deaths);
        }

        private sealed class NoFloorWorld : IWorldQueries
        {
            public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);
            public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter) => new RayHit(QueryStatus.NoHit);
            public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);
        }
    }
}
