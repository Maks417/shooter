#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Bootstrap;
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

        [Fact]
        public void WallBetweenEyeAndMuzzleBlocksEvenWhenMuzzleIsInsideWall()
        {
            // Unity-like backend: rays starting inside a box never report that box.
            var world = new UnityLikeBoxWorld();
            world.AddBox(new Float3(-2, 0, 0.2f), new Float3(2, 2.2f, 0.6f), 90);
            var pose = new GameplayPoseEvaluator();
            var volumes = new List<HitVolume>();
            pose.Evaluate(new EntityId(2), new Float3(0, 0, 6), 0, 0, Stance.Standing, 1.8f, 1, volumes);
            var eye = new Float3(0, 1.6f, 0);
            var muzzle = new Float3(0, 1.5f, 0.45f);   // inside the slab, past its near face
            var aim = new Float3(0, 0, 1);
            var hitscan = new HitscanResolver(CollisionLayers.MotorMask, CollisionLayers.BulletObstructionMask);
            var result = hitscan.Resolve(
                new HitscanRequest(new EntityId(1), eye, muzzle, aim, WeaponDefinition.Rifle, 1, true, 460072030UL, 1),
                world, volumes);
            Assert.True(result.BlockedAtMuzzle);
            Assert.False(result.Fired);
            Assert.False(result.Impacted);
        }

        [Fact]
        public void OpenAirShotIsUnchangedByEyeToMuzzleCheck()
        {
            var world = new UnityLikeBoxWorld();
            var pose = new GameplayPoseEvaluator();
            var volumes = new List<HitVolume>();
            pose.Evaluate(new EntityId(2), new Float3(0, 0, 6), 0, 0, Stance.Standing, 1.8f, 1, volumes);
            var eye = new Float3(0, 1.6f, 0);
            var muzzle = new Float3(0, 1.5f, 0.45f);   // clear of any cover, on the centre line of the target
            var aim = new Float3(0, 0, 1);
            var hitscan = new HitscanResolver(CollisionLayers.MotorMask, CollisionLayers.BulletObstructionMask);
            var result = hitscan.Resolve(
                new HitscanRequest(new EntityId(1), eye, muzzle, aim, WeaponDefinition.Rifle, 1, true, 460072030UL, 1),
                world, volumes);
            Assert.False(result.BlockedAtMuzzle);
            Assert.True(result.Fired);
            Assert.True(result.Impacted);
            Assert.Equal(new EntityId(2), result.Impact.EntityId);
        }
    }

    /// <summary>ADR-112: the crosshair (eye ray) decides the hit; the muzzle offset no longer throws head shots off.</summary>
    public sealed class EyeRayHitscanTests
    {
        private static HitscanResult HeadShot(float distance, UnityLikeBoxWorld world, float targetSpeed, IReadOnlyList<EntityId>? ignore = null,
            List<HitVolume>? extra = null)
        {
            var pose = new GameplayPoseEvaluator();
            var volumes = new List<HitVolume>();
            if (extra != null) volumes.AddRange(extra);
            // Target faces the shooter (yaw pi) and is either still or walking.
            pose.Evaluate(new EntityId(2), new Float3(0, 0, distance), (float)Math.PI, 0, Stance.Standing, 1.8f, targetSpeed, 1, volumes);
            Float3 head = default;
            foreach (var v in volumes) if (v.EntityId.Equals(new EntityId(2)) && v.RegionId == 1) head = v.Capsule.BottomSphereCenter;
            var eye = pose.EyePosition(default, Stance.Standing, 1.8f);
            float dx = head.X - eye.X, dy = head.Y - eye.Y, dz = head.Z - eye.Z;
            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            var aim = new Float3(dx / len, dy / len, dz / len);
            float yaw = (float)Math.Atan2(aim.X, aim.Z), pitch = -(float)Math.Asin(aim.Y);
            var muzzle = pose.CanonicalMuzzle(eye, yaw, pitch);
            var hitscan = new HitscanResolver(CollisionLayers.MotorMask, CollisionLayers.BulletObstructionMask);
            return hitscan.Resolve(new HitscanRequest(new EntityId(1), eye, muzzle, aim, WeaponDefinition.Rifle, 1, true, 460072030UL, 1),
                world, volumes, ignore);
        }

        [Theory]
        [InlineData(5f, 0f)]
        [InlineData(20f, 0f)]
        [InlineData(40f, 0f)]
        [InlineData(20f, 3.5f)]
        public void CentredHeadShotAgainstOpenSkyHitsTheHead(float distance, float speed)
        {
            var result = HeadShot(distance, new UnityLikeBoxWorld(), speed);
            Assert.True(result.Fired);
            Assert.True(result.Impacted);
            Assert.Equal(new EntityId(2), result.Impact.EntityId);
            Assert.Equal((ushort)1, result.Impact.SurfaceTag);
        }

        [Fact]
        public void CentredHeadShotWithAWallBehindHitsTheHead()
        {
            var world = new UnityLikeBoxWorld();
            world.AddBox(new Float3(-3, 0, 21), new Float3(3, 3, 21.5f), 5);
            var result = HeadShot(20f, world, 0f);
            Assert.Equal(new EntityId(2), result.Impact.EntityId);
            Assert.Equal((ushort)1, result.Impact.SurfaceTag);
        }

        [Fact]
        public void IgnoredTeammateDoesNotSoakTheShot()
        {
            var pose = new GameplayPoseEvaluator();
            var mate = new List<HitVolume>();
            pose.Evaluate(new EntityId(3), new Float3(0, 0, 10), (float)Math.PI, 0, Stance.Standing, 1.8f, 0f, 1, mate);
            var blocked = HeadShot(20f, new UnityLikeBoxWorld(), 0f, null, mate);
            Assert.Equal(new EntityId(3), blocked.Impact.EntityId);
            var through = HeadShot(20f, new UnityLikeBoxWorld(), 0f, new[] { new EntityId(3) }, mate);
            Assert.Equal(new EntityId(2), through.Impact.EntityId);
        }

        [Fact]
        public void CoverInFrontOfTheHeadStillBlocks()
        {
            var world = new UnityLikeBoxWorld();
            world.AddBox(new Float3(-3, 0, 10), new Float3(3, 3, 10.5f), 9);
            var result = HeadShot(20f, world, 0f);
            Assert.True(result.Impacted);
            Assert.False(result.Impact.EntityId.IsValid);
            Assert.Equal(9UL, result.Impact.ColliderStableId);
        }
    }

    /// <summary>Test fake mimicking Physics.RaycastNonAlloc: boxes are reported only when the ray starts outside them.</summary>
    internal sealed class UnityLikeBoxWorld : IWorldQueries, IFakeAdapter
    {
        private readonly List<(Float3 Min, Float3 Max, ulong StableId)> boxes = new List<(Float3, Float3, ulong)>();

        public void AddBox(Float3 min, Float3 max, ulong stableId) { boxes.Add((min, max, stableId)); }

        public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
        {
            if (!unitDirection.IsUnit || !float.IsFinite(maxMeters) || maxMeters <= 0) return new RayHit(QueryStatus.InvalidInput);
            if ((filter.LayerMask & CollisionLayers.BulletObstructionMask) == 0 && (filter.LayerMask & CollisionLayers.MotorMask) == 0)
                return new RayHit(QueryStatus.NoHit);
            QueryHit? best = null;
            foreach (var box in boxes)
            {
                if (Inside(origin, box.Min, box.Max)) continue;
                if (!RayAabb(origin, unitDirection, maxMeters, box.Min, box.Max, out float t, out Float3 point, out Float3 normal)) continue;
                var hit = new QueryHit(t, point, normal, default, new SurfaceId(box.StableId + 1000), box.StableId, 1, 1);
                if (best == null || QueryOrder.CompareClosest(hit, best.Value) < 0) best = hit;
            }
            return best == null ? new RayHit(QueryStatus.NoHit) : new RayHit(QueryStatus.Hit, best.Value);
        }

        public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter) => new SweepHit(QueryStatus.NoHit);

        public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned) => new OverlapResult(QueryStatus.NoHit, 0, 0);

        private static bool Inside(Float3 p, Float3 min, Float3 max) =>
            p.X > min.X && p.X < max.X && p.Y > min.Y && p.Y < max.Y && p.Z > min.Z && p.Z < max.Z;

        private static bool RayAabb(Float3 o, Float3 d, float max, Float3 min, Float3 maxB, out float t, out Float3 point, out Float3 normal)
        {
            t = 0; point = default; normal = new Float3(0, 1, 0);
            float tmin = 0f, tmax = max;
            Float3 n = new Float3(0, 1, 0);
            if (!Clip(o.X, d.X, min.X, maxB.X, ref tmin, ref tmax, ref n, new Float3(-1, 0, 0), new Float3(1, 0, 0))) return false;
            if (!Clip(o.Y, d.Y, min.Y, maxB.Y, ref tmin, ref tmax, ref n, new Float3(0, -1, 0), new Float3(0, 1, 0))) return false;
            if (!Clip(o.Z, d.Z, min.Z, maxB.Z, ref tmin, ref tmax, ref n, new Float3(0, 0, -1), new Float3(0, 0, 1))) return false;
            if (tmin > max || tmax < 0) return false;
            t = tmin;
            point = new Float3(o.X + d.X * t, o.Y + d.Y * t, o.Z + d.Z * t);
            normal = n;
            return true;
        }

        private static bool Clip(float o, float d, float min, float max, ref float tmin, ref float tmax, ref Float3 normal, Float3 nMin, Float3 nMax)
        {
            if (Math.Abs(d) < 1e-8f) return o >= min && o <= max;
            float inv = 1f / d;
            float t1 = (min - o) * inv, t2 = (max - o) * inv;
            Float3 n1 = nMin, n2 = nMax;
            if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; var nt = n1; n1 = n2; n2 = nt; }
            if (t1 > tmin) { tmin = t1; normal = n1; }
            if (t2 < tmax) tmax = t2;
            return tmin <= tmax;
        }
    }
}
