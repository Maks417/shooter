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
    }
}
