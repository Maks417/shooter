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
        public void EvaluatorEmitsSixOrderedRegionsIndependentOfPitchLodProxy()
        {
            var eval = new GameplayPoseEvaluator();
            var a = new List<HitVolume>();
            var b = new List<HitVolume>();
            eval.Evaluate(new EntityId(1), default, 0, 0, Stance.Standing, 1.8f, 7, a);
            eval.Evaluate(new EntityId(1), default, 0, 0.5f, Stance.Standing, 1.8f, 7, b);
            Assert.Equal(6, a.Count);
            Assert.Equal(GameplayPoseEvaluator.Version, eval.PoseVersion);
            for (int i = 0; i < 6; i++)
            {
                Assert.Equal((ushort)(i + 1), a[i].RegionId);
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
            // Reference markers for standing bind-like sample (not skinned mesh).
            AssertCenter(volumes, 1, new Float3(0, 1.65f, 0), 0.05f); // head ≤5cm
            AssertCenter(volumes, 2, new Float3(0, 1.15f, 0), 0.05f); // torso ≤5cm (hip..shoulder)
            AssertCenter(volumes, 3, new Float3(-0.22f, 1.225f, 0), 0.10f); // limb ≤10cm
            AssertCenter(volumes, 4, new Float3(0.22f, 1.225f, 0), 0.10f);
            AssertCenter(volumes, 5, new Float3(0, 0.49f, 0), 0.10f);
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
