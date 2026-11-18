#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Simulation.Pose;
using Xunit;

namespace Shooter.Simulation.Tests.Pose
{
    public sealed class PoseTests
    {
        [Fact]
        public void EvaluatorEmitsSixOrderedRegionsAndOnlyTheHeadFollowsPitch()
        {
            var eval = new GameplayPoseEvaluator();
            var a = new List<HitVolume>();
            var b = new List<HitVolume>();
            eval.Evaluate(new EntityId(1), default, 0, 0, Stance.Standing, 1.8f, 7, a);
            eval.Evaluate(new EntityId(1), default, 0, 0.5f, Stance.Standing, 1.8f, 7, b);
            Assert.Equal(6, a.Count);
            Assert.Equal(GameplayPoseEvaluator.Version, eval.PoseVersion);
            Assert.NotEqual(a[0].Capsule.BottomSphereCenter.Z, b[0].Capsule.BottomSphereCenter.Z);
            for (int i = 0; i < 6; i++)
            {
                Assert.Equal((ushort)(i + 1), a[i].RegionId);
                if (i == 0) continue;
                Assert.Equal(a[i].Capsule.BottomSphereCenter.X, b[i].Capsule.BottomSphereCenter.X);
                Assert.Equal(a[i].Capsule.BottomSphereCenter.Y, b[i].Capsule.BottomSphereCenter.Y);
                Assert.Equal(a[i].Capsule.BottomSphereCenter.Z, b[i].Capsule.BottomSphereCenter.Z);
            }
        }

        [Fact]
        public void SamplePoseCentersStayWithinDeclaredTolerancesOfReferenceMarkers()
        {
            var eval = new GameplayPoseEvaluator();
            var volumes = new List<HitVolume>();
            var feet = new Float3(0, 0, 0);
            eval.Evaluate(new EntityId(2), feet, 0, 0, Stance.Standing, 1.8f, 1, volumes);
            // ADR-112 reference markers: the drawn soldier standing still at level aim (SoldierMotion head joint
            // 1.572 m, 0.094 m to the shooting side, 0.101 m back; pelvis 0.943 m).
            AssertCenter(volumes, 1, new Float3(0.094f, 1.592f, -0.101f), 0.05f); // head ≤5cm
            AssertCenter(volumes, 2, new Float3(0, 1.11f, 0), 0.05f); // torso ≤5cm (pelvis..shoulder)
            AssertCenter(volumes, 3, new Float3(-0.22f, 1.26f, 0), 0.10f); // limb ≤10cm
            AssertCenter(volumes, 4, new Float3(0.22f, 1.26f, 0), 0.10f);
            AssertCenter(volumes, 5, new Float3(-0.1f, 0.47f, 0), 0.10f);
            AssertCenter(volumes, 6, new Float3(0.1f, 0.47f, 0), 0.10f);
        }

        [Theory]
        [InlineData(Stance.Standing, 1.8f, 0f, 1.592f)]   // still: drawn head joint 1.572 + 0.02
        [InlineData(Stance.Standing, 1.8f, 3.5f, 1.429f)] // moving: bent-knee pelvis 0.78
        [InlineData(Stance.Crouching, 1.2f, 0f, 1.309f)]  // crouched: pelvis 0.66
        public void HeadSphereFollowsTheDrawnHeadForEachStance(Stance stance, float height, float speed, float headY)
        {
            var eval = new GameplayPoseEvaluator();
            var volumes = new List<HitVolume>();
            eval.Evaluate(new EntityId(2), default, 0, 0, stance, height, speed, 1, volumes);
            AssertCenter(volumes, 1, new Float3(0.094f, headY, -0.101f), 0.01f);
            Assert.Equal(GameplayPoseEvaluator.HeadRadius, volumes[0].Capsule.Radius, 4);
        }

        [Fact]
        public void CanonicalMuzzleIsEyeRelativeIndependentOfBodyLodProxy()
        {
            var eval = new GameplayPoseEvaluator();
            var eye = eval.EyePosition(default, Stance.Standing, 1.8f);
            var muzzle = eval.CanonicalMuzzle(eye, 0, 0);
            Assert.InRange(muzzle.X - eye.X, 0.17f, 0.19f);
            Assert.InRange(muzzle.Y - eye.Y, -0.16f, -0.14f);
            Assert.InRange(muzzle.Z - eye.Z, 0.44f, 0.46f);
        }

        private static void AssertCenter(List<HitVolume> volumes, ushort region, Float3 expected, float tol)
        {
            var v = volumes.Find(h => h.RegionId == region);
            var c = new Float3(
                (v.Capsule.BottomSphereCenter.X + v.Capsule.TopSphereCenter.X) * 0.5f,
                (v.Capsule.BottomSphereCenter.Y + v.Capsule.TopSphereCenter.Y) * 0.5f,
                (v.Capsule.BottomSphereCenter.Z + v.Capsule.TopSphereCenter.Z) * 0.5f);
            float d = Distance(c, expected);
            Assert.True(d <= tol, $"region {region} center distance {d} > {tol}");
        }

        private static float Distance(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
