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
        public void RelocateActorMovesBodyButKeepsHealthAmmoAndIdentity()
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld());
            var id = sim.SpawnActor(new Float3(0, 0, 0), 0, 1);
            // Fire once so ammo differs from a fresh loadout.
            sim.Step(0, new[] { new InputCommand(id, 0, 1, 0, 0, 0, 0, InputButtons.FireHeld | InputButtons.FirePressed, 0) });
            ushort ammo;
            using (var before = sim.AcquireSnapshot()) ammo = before.Snapshot.Entities[0].Weapon.AmmoInMagazine;
            Assert.True(ammo < 30);

            Assert.True(sim.RelocateActor(id, new Float3(10, 0, 20), 1.5f));

            Assert.True(sim.TryGetMotorState(id, out var motor));
            Assert.Equal(10f, motor.Feet.X);
            Assert.Equal(20f, motor.Feet.Z);
            Assert.Equal(1.5f, motor.AimYaw);
            Assert.False(motor.OverlapUnresolved);
            Assert.Equal(0f, motor.Velocity.X);
            sim.Step(1, new[] { new InputCommand(id, 1, 2, 0, 0, 1.5f, 0, InputButtons.None, 0) });
            using var after = sim.AcquireSnapshot();
            Assert.Equal(id, after.Snapshot.Entities[0].Id);
            Assert.Equal(ammo, after.Snapshot.Entities[0].Weapon.AmmoInMagazine);
            Assert.Equal((ushort)100, after.Snapshot.Entities[0].Health);
            Assert.False(sim.RelocateActor(new EntityId(9999), default, 0));
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
        public void LocalSlideRequiresAcceptedFreshLifeCommandAndUsesCrouchHitPose()
        {
            var sim = new CombatHarnessSimulation(new StaticPlaneWorld());
            var actor = sim.SpawnActor(default, 0, 1);
            for (uint t = 0; t < 20; t++) sim.Step(t, new[] { new InputCommand(actor, t, t + 1, 0, 1, 0, 0, InputButtons.SprintHeld, 0) });
            // Duplicate sequence cannot smuggle a separate slide intent into authority.
            sim.Step(20, new[] { new InputCommand(actor, 20, 20, 0, 1, 0, 0, InputButtons.SprintHeld, 0) }, actor);
            using (var snap = sim.AcquireSnapshot()) Assert.Equal(Stance.Standing, snap.Snapshot.Entities[0].Stance);
            sim.Step(21, new[] { new InputCommand(actor, 21, 22, 0, 1, 0, 0, InputButtons.SprintHeld, 0) }, actor);
            using (var snap = sim.AcquireSnapshot())
            {
                var state = Assert.Single(snap.Snapshot.Entities);
                Assert.Equal(Stance.Crouching, state.Stance);
                Assert.Equal(6.5f, state.Velocity.Z, 3);
                Assert.Equal(6, snap.Snapshot.HitVolumes.Count);
            }
            Assert.True(sim.ResetActor(actor, default, 0));
            sim.Step(22, new[] { new InputCommand(actor, 22, 1, 0, 0, 0, 0, InputButtons.None, 0) });
            using (var snap = sim.AcquireSnapshot())
            {
                Assert.Equal(Stance.Standing, snap.Snapshot.Entities[0].Stance);
                Assert.Equal(0f, snap.Snapshot.Entities[0].Velocity.Z);
            }
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
            Assert.Equal(7f, state.Position.X);
            Assert.Equal(9f, state.Position.Z);
            Assert.InRange(state.Position.Y, 0f, 0.025f); // spawned in ground contact: lifted by Skin
            Assert.Equal((ushort)100, state.Health);
            Assert.Equal((ushort)30, state.Weapon.AmmoInMagazine);
        }
    }
}
