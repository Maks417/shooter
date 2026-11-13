#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Combat;
using Shooter.Simulation.Motor;
using Shooter.Simulation.Pose;
using Shooter.Simulation.Tests.Motor;
using Xunit;

namespace Shooter.Simulation.Tests.Combat
{
    public sealed class CoverHitscanTests
    {
        [Fact]
        public void CoverBetweenMuzzleAndTargetBlocksActorDamageRegion()
        {
            var world = new StaticPlaneWorld();
            world.AddBox(new Float3(-2, 0, 2.8f), new Float3(2, 2.2f, 3.2f), 50);
            var pose = new GameplayPoseEvaluator();
            var volumes = new List<HitVolume>();
            pose.Evaluate(new EntityId(2), new Float3(0, 0, 6), 0, 0, Stance.Standing, 1.8f, 1, volumes);
            var eye = pose.EyePosition(default, Stance.Standing, 1.8f);
            var muzzle = pose.CanonicalMuzzle(eye, 0, 0);
            var aim = pose.AimDirection(0, 0);
            var hitscan = new HitscanResolver(CollisionLayers.MotorMask, CollisionLayers.BulletObstructionMask);
            var result = hitscan.Resolve(
                new HitscanRequest(new EntityId(1), eye, muzzle, aim, WeaponDefinition.Rifle, 1, true, 460072030UL, 1),
                world, volumes);
            Assert.True(result.Fired);
            Assert.False(result.BlockedAtMuzzle);
            Assert.True(result.Impacted);
            Assert.False(result.Impact.EntityId.IsValid);
            Assert.Equal(50UL, result.Impact.ColliderStableId);
        }

        [Fact]
        public void MuzzleFlushAgainstCoverRejectsShot()
        {
            var world = new StaticPlaneWorld();
            // Thin wall whose near face coincides with canonical muzzle (short-ray inside approximation).
            world.AddBox(new Float3(-1, 1.2f, 0.45f), new Float3(1, 1.8f, 0.55f), 77);
            var pose = new GameplayPoseEvaluator();
            var eye = pose.EyePosition(default, Stance.Standing, 1.8f);
            var muzzle = pose.CanonicalMuzzle(eye, 0, 0);
            var aim = pose.AimDirection(0, 0);
            var hitscan = new HitscanResolver(CollisionLayers.MotorMask, CollisionLayers.BulletObstructionMask);
            var result = hitscan.Resolve(
                new HitscanRequest(new EntityId(1), eye, muzzle, aim, WeaponDefinition.Rifle, 1, true, 460072030UL, 1),
                world, null);
            Assert.True(result.BlockedAtMuzzle);
            Assert.False(result.Fired);
        }
    }
}
