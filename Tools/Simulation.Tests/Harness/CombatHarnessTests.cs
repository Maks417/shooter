#nullable enable
using Shooter.Contracts;
using Shooter.Simulation;
using Shooter.Simulation.Tests.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Harness
{
    public sealed class CombatHarnessTests
    {
        [Fact]
        public void EmptyHarnessServicesTicksLikeFoundationSmoke()
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld());
            for (uint t = 0; t < 60; t++)
            {
                var result = sim.Step(t, System.ReadOnlyMemory<InputCommand>.Empty);
                Assert.Equal(t, result.Tick);
            }
            using var snap = sim.AcquireSnapshot();
            Assert.Equal(59u, snap.Snapshot.Tick);
            Assert.Empty(snap.Snapshot.Entities);
        }

        [Fact]
        public void SpawnedActorAcceptsRifleCadenceWithoutNegativeAmmo()
        {
            var world = new StaticPlaneWorld();
            var sim = new CombatHarnessSimulation(world);
            var id = sim.SpawnActor(new Float3(0, 0, 0), 0, 1);
            int shots = 0;
            for (uint t = 0; t < 60; t++)
            {
                var buttons = InputButtons.FireHeld | (t == 0 ? InputButtons.FirePressed : InputButtons.None);
                var cmd = new InputCommand(id, t, t + 1, 0, 0, 0, 0, buttons, 0);
                var result = sim.Step(t, new[] { cmd });
                shots += result.Counters.Shots;
            }
            Assert.Equal(10, shots);
            using var snap = sim.AcquireSnapshot();
            Assert.Equal((ushort)20, snap.Snapshot.Entities[0].Weapon.AmmoInMagazine);
        }

        [Fact]
        public void RetiredActorRejectsCommandsAndFreshIdsAreMonotonic()
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld());
            var oldLife = sim.SpawnActor(new Float3(0, 0, 0), 0, 1);
            sim.Step(0, new[] { new InputCommand(oldLife, 0, 1, 0, 0, 0, 0, InputButtons.None, 0) });
            Assert.True(sim.RetireActor(oldLife));

            var replacement = sim.SpawnActor(new Float3(1, 0, 0), 0, 1);
            Assert.True(replacement.Value > oldLife.Value);
            var result = sim.Step(1, new[]
            {
                new InputCommand(oldLife, 1, 2, 0, 0, 0, 0, InputButtons.FirePressed, 0),
                new InputCommand(replacement, 1, 1, 0, 0, 0, 0, InputButtons.None, 0)
            });

            Assert.Equal(1, result.Counters.CommandsAccepted);
            Assert.Equal(1, result.Counters.CommandsRejected);
            Assert.Equal(1, sim.ActiveActorCount);
            using var snap = sim.AcquireSnapshot();
            Assert.Single(snap.Snapshot.Entities);
            Assert.Equal(replacement, snap.Snapshot.Entities[0].Id);
            Assert.Equal(6, snap.Snapshot.HitVolumes.Count);
        }

        [Fact]
        public void ResetActorRestoresAuthoritativeLifeState()
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld());
            var actor = sim.SpawnActor(default, 0, 1);
            sim.Step(0, new[]
            {
                new InputCommand(actor, 0, 1, 0, 0, 0, 0, InputButtons.FireHeld | InputButtons.FirePressed, 0)
            });

            Assert.True(sim.ResetActor(actor, new Float3(7, 0, 9), 1f));
            sim.Step(1, new[] { new InputCommand(actor, 1, 1, 0, 0, 1f, 0, InputButtons.None, 0) });
            using var snap = sim.AcquireSnapshot();
            var state = Assert.Single(snap.Snapshot.Entities);
            Assert.Equal(new Float3(7, 0, 9), state.Position);
            Assert.Equal((ushort)100, state.Health);
            Assert.Equal((ushort)30, state.Weapon.AmmoInMagazine);
        }
    }
}
