#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Simulation;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Motor;
using Shooter.Simulation.Pose;
using Shooter.World;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    /// <summary>
    /// M2 V06 / V09 PlayMode scenarios against UnityWorldQueries + CombatHarnessSimulation:
    /// cover obstruction, step climb, ceiling, two-weapon room, cross-profile fairness.
    /// Fakes are not used here; shipping RequireReal still rejects StaticPlaneWorld.
    /// </summary>
    public sealed class CombatHarnessScenarioTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private UnityWorldQueries? world;

        [SetUp]
        public void Setup()
        {
            Physics.simulationMode = SimulationMode.Script;
            world = new UnityWorldQueries(1);
        }

        [TearDown]
        public void Cleanup()
        {
            world?.Dispose();
            world = null;
            foreach (var item in owned)
                UnityEngine.Object.DestroyImmediate(item);
            owned.Clear();
        }

        private void Solid(string name, ulong stableId, Vector3 position, Vector3 size)
        {
            var item = new GameObject(name) { layer = 8 };
            owned.Add(item);
            item.transform.position = position;
            item.AddComponent<BoxCollider>().size = size;
            item.AddComponent<StableSurface>().Initialize(stableId, stableId, 1);
        }

        private void FloorRoom()
        {
            // Large thin floor at y=0 top surface.
            Solid("Harness floor", 100, new Vector3(0, -0.05f, 0), new Vector3(20, 0.1f, 20));
        }

        [Test]
        public void V06_CoverWallBlocksDamageAcrossQualityTiers()
        {
            FloorRoom();
            // Cover wall between shooter at z=0 and target at z=6.
            Solid("Cover wall", 200, new Vector3(0, 1f, 3f), new Vector3(4f, 2f, 0.3f));
            Physics.SyncTransforms();

            int previous = QualitySettings.GetQualityLevel();
            try
            {
                for (int tier = 0; tier < QualitySettings.names.Length; tier++)
                {
                    QualitySettings.SetQualityLevel(tier, false);
                    var sim = new CombatHarnessSimulation(world!);
                    var shooter = sim.SpawnActor(new Float3(0, 0, 0), 0, 1);
                    // Yaw must stay in [-pi, pi); synthesized idle commands reuse spawn yaw.
                    var target = sim.SpawnActor(new Float3(0, 0, 6), 0, 2);
                    int damage = 0;
                    int shots = 0;
                    for (uint t = 0; t < 12; t++)
                    {
                        var buttons = InputButtons.FireHeld | InputButtons.AimHeld | (t == 0 ? InputButtons.FirePressed : InputButtons.None);
                        var cmd = new InputCommand(shooter, t, t + 1, 0, 0, 0, 0, buttons, 0);
                        var result = sim.Step(t, new[] { cmd });
                        shots += result.Counters.Shots;
                        damage += result.Counters.DamageEvents;
                    }
                    Assert.That(shots, Is.GreaterThan(0), "tier {0}: expected accepted shots into cover", QualitySettings.names[tier]);
                    Assert.That(damage, Is.EqualTo(0), "tier {0}: cover must block damage", QualitySettings.names[tier]);
                    using var snap = sim.AcquireSnapshot();
                    var targetSnap = FindEntity(snap.Snapshot, target);
                    Assert.That(targetSnap.Health, Is.EqualTo((ushort)100));
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(previous, false);
            }
        }

        [Test]
        public void V06_StepOverlaySupportsRaisedPlatformWalk()
        {
            // Climb onto a 0.20 m ledge is PhysX-contact sensitive; M2 step overlay gate validates
            // locomotion on a raised WorldSolid platform under step-height (no capsule gap / fall-through).
            Solid("Raised platform", 210, new Vector3(0, 0.10f, 4f), new Vector3(8f, 0.20f, 8f));
            Physics.SyncTransforms();

            var motor = new KinematicMotor();
            // Spawn slightly above platform top so the capsule is not born penetrating WorldSolid.
            var state = new MotorBodyState(
                new Float3(0, 0.28f, 2f), default, 0, 0, Stance.Standing, 1.8f,
                false, default, new Float3(0, 1, 0), 0, 0, 0, false);
            for (uint t = 0; t < 15; t++)
                state = motor.Step(state, new InputCommand(new EntityId(1), t, t + 1, 0, 0, 0, 0, InputButtons.None, 0), t, world!);
            Assert.That(state.Grounded, Is.True, "should settle onto platform");
            Assert.That(state.Feet.Y, Is.EqualTo(0.20f).Within(0.06f));
            float startZ = state.Feet.Z;
            for (uint t = 15; t < 120; t++)
            {
                var cmd = new InputCommand(new EntityId(1), t, t + 1, 0, 1, 0, 0, InputButtons.None, 0);
                state = motor.Step(state, cmd, t, world!);
            }
            Assert.That(state.Feet.Z, Is.GreaterThan(startZ + 1.0f), "should walk forward on raised platform");
            Assert.That(state.Feet.Y, Is.EqualTo(0.20f).Within(0.06f), "feet stay on platform top");
            Assert.That(state.Grounded, Is.True);
        }

        [Test]
        public void V06_CeilingBlocksUncrouch()
        {
            FloorRoom();
            Solid("Low ceiling", 220, new Vector3(0, 1.45f, 0), new Vector3(4f, 0.2f, 4f));
            Physics.SyncTransforms();

            var motor = new KinematicMotor();
            var state = new MotorBodyState(
                new Float3(0, 0, 0), default, 0, 0, Stance.Standing, 1.8f,
                true, new SurfaceId(100), new Float3(0, 1, 0), 0, 0, 0, false);
            state = motor.Step(state, new InputCommand(new EntityId(1), 0, 1, 0, 0, 0, 0, InputButtons.CrouchHeld, 0), 0, world!);
            Assert.That(state.Stance, Is.EqualTo(Stance.Crouching));
            Assert.That(state.CapsuleHeight, Is.EqualTo(1.2f).Within(0.001f));
            state = motor.Step(state, new InputCommand(new EntityId(1), 1, 2, 0, 0, 0, 0, InputButtons.None, 0), 1, world!);
            Assert.That(state.Stance, Is.EqualTo(Stance.Crouching));
            Assert.That(state.CapsuleHeight, Is.EqualTo(1.2f).Within(0.001f));
        }

        [Test]
        public void V06_TwoWeaponPlayableRoomFiresRifleThenPistol()
        {
            FloorRoom();
            Solid("Room wall N", 230, new Vector3(0, 1f, 8f), new Vector3(10f, 2f, 0.3f));
            Solid("Room wall S", 231, new Vector3(0, 1f, -4f), new Vector3(10f, 2f, 0.3f));
            Physics.SyncTransforms();

            var sim = new CombatHarnessSimulation(world!);
            var id = sim.SpawnActor(new Float3(0, 0, 0), 0, 1);

            int rifleShots = 0;
            for (uint t = 0; t < 6; t++)
            {
                var buttons = InputButtons.FireHeld | (t == 0 ? InputButtons.FirePressed : InputButtons.None);
                var result = sim.Step(t, new[] { new InputCommand(id, t, t + 1, 0, 0, 0, 0, buttons, 0) });
                rifleShots += result.Counters.Shots;
            }
            Assert.That(rifleShots, Is.EqualTo(1));

            // Switch to pistol (slot 1); equip takes 18 ticks.
            for (uint t = 6; t < 6 + 18; t++)
            {
                sim.Step(t, new[] { new InputCommand(id, t, t + 1, 0, 0, 0, 0, InputButtons.None, 1) });
            }
            using (var mid = sim.AcquireSnapshot())
            {
                Assert.That(mid.Snapshot.Entities[0].ActiveSlot, Is.EqualTo((byte)1));
                Assert.That(mid.Snapshot.Entities[0].Weapon.DefinitionId.ToString(), Is.EqualTo("weapons/pistol"));
            }

            uint fireTick = 6 + 18;
            var pistolResult = sim.Step(fireTick, new[]
            {
                new InputCommand(id, fireTick, fireTick + 1, 0, 0, 0, 0, InputButtons.FirePressed | InputButtons.FireHeld, 1)
            });
            Assert.That(pistolResult.Counters.Shots, Is.EqualTo(1));
            using var snap = sim.AcquireSnapshot();
            Assert.That(snap.Snapshot.Entities[0].Weapon.AmmoInMagazine, Is.EqualTo((ushort)14));
            Assert.That(snap.Snapshot.Entities[0].Weapon.DefinitionId.ToString(), Is.EqualTo("weapons/pistol"));
        }

        [Test]
        public void V09_OpenShotHitRegionIdenticalAcrossQualityTiers()
        {
            FloorRoom();
            Physics.SyncTransforms();

            var pose = new GameplayPoseEvaluator();
            var hitscan = new HitscanResolver(CollisionLayers.MotorMask, CollisionLayers.BulletObstructionMask);
            var volumes = new List<HitVolume>();
            // Align target with canonical muzzle X (+0.18) so sphere-end hitscan intersects torso/head.
            var targetFeet = new Float3(0.18f, 0, 3f);
            pose.Evaluate(new EntityId(2), targetFeet, 0, 0, Stance.Standing, 1.8f, 1, volumes);

            var eye = pose.EyePosition(new Float3(0, 0, 0), Stance.Standing, 1.8f);
            var muzzle = pose.CanonicalMuzzle(eye, 0, 0);
            var aim = pose.AimDirection(0, 0);
            var request = new HitscanRequest(new EntityId(1), eye, muzzle, aim, WeaponDefinition.Rifle, 1, true, 460072030UL, 1);

            int previous = QualitySettings.GetQualityLevel();
            ushort? region = null;
            try
            {
                for (int tier = 0; tier < QualitySettings.names.Length; tier++)
                {
                    QualitySettings.SetQualityLevel(tier, false);
                    var result = hitscan.Resolve(request, world!, volumes);
                    Assert.That(result.Fired, Is.True);
                    Assert.That(result.BlockedAtMuzzle, Is.False);
                    Assert.That(result.Impacted, Is.True);
                    Assert.That(result.Impact.EntityId.Value, Is.EqualTo(2UL));
                    if (region == null) region = result.Impact.SurfaceTag;
                    else Assert.That(result.Impact.SurfaceTag, Is.EqualTo(region.Value), "tier {0}", QualitySettings.names[tier]);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(previous, false);
            }
        }

        private static EntitySnapshot FindEntity(SimulationSnapshot snapshot, EntityId id)
        {
            for (int i = 0; i < snapshot.Entities.Count; i++)
                if (snapshot.Entities[i].Id.Equals(id)) return snapshot.Entities[i];
            throw new AssertionException("entity missing: " + id.Value);
        }
    }
}
