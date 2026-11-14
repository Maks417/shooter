#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Bootstrap;
using Shooter.Contracts;
using Shooter.Simulation.Motor;

namespace Shooter.Simulation.Tests.Motor
{
    /// <summary>Analytic infinite Y=0 ground plus optional axis-aligned solid boxes. Test-only fake.</summary>
    internal sealed class StaticPlaneWorld : IWorldQueries, IFakeAdapter
    {
        private readonly List<(Float3 Min, Float3 Max, ulong StableId, ushort Tag)> boxes = new List<(Float3, Float3, ulong, ushort)>();
        private readonly int owner = Environment.CurrentManagedThreadId;
        private readonly uint revision;

        public StaticPlaneWorld(uint revision = 1) { this.revision = revision; }

        public void AddBox(Float3 min, Float3 max, ulong stableId, ushort tag = 1)
        {
            boxes.Add((min, max, stableId, tag));
        }

        private void MainThread()
        {
            if (Environment.CurrentManagedThreadId != owner) throw new InvalidOperationException("Main-thread-only query");
        }

        public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
        {
            MainThread();
            if (!unitDirection.IsUnit || !float.IsFinite(maxMeters) || maxMeters <= 0) return new RayHit(QueryStatus.InvalidInput);
            QueryHit? best = null;
            if ((filter.LayerMask & CollisionLayers.MotorMask) != 0 && unitDirection.Y < -1e-6f)
            {
                float t = -origin.Y / unitDirection.Y;
                if (t >= 0 && t <= maxMeters)
                {
                    var point = new Float3(origin.X + unitDirection.X * t, 0, origin.Z + unitDirection.Z * t);
                    best = new QueryHit(t, point, new Float3(0, 1, 0), default, new SurfaceId(100), 1, 1, revision);
                }
            }
            foreach (var box in boxes)
            {
                if (!RayAabb(origin, unitDirection, maxMeters, box.Min, box.Max, out float t, out Float3 point, out Float3 normal)) continue;
                var hit = new QueryHit(t, point, normal, default, new SurfaceId(box.StableId + 1000), box.StableId, box.Tag, revision);
                if (best == null || QueryOrder.CompareClosest(hit, best.Value) < 0) best = hit;
            }
            return best == null ? new RayHit(QueryStatus.NoHit) : new RayHit(QueryStatus.Hit, best.Value);
        }

        public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter)
        {
            MainThread();
            if (!capsule.IsValid) return new SweepHit(QueryStatus.InvalidInput);
            if (displacement.LengthSquared == 0) return new SweepHit(QueryStatus.NoHit);
            float length = (float)Math.Sqrt(displacement.LengthSquared);
            var dir = new Float3(displacement.X / length, displacement.Y / length, displacement.Z / length);
            if (Math.Abs(dir.X) < 1e-6f && Math.Abs(dir.Z) < 1e-6f && dir.Y > 0f)
            {
                // Upward sweep: the capsule footprint (square approximation) against box undersides.
                float cx = capsule.TopSphereCenter.X, cz = capsule.TopSphereCenter.Z, r = capsule.Radius;
                float top = Math.Max(capsule.BottomSphereCenter.Y, capsule.TopSphereCenter.Y) + r;
                SweepHit? bestUp = null;
                foreach (var box in boxes)
                {
                    if (!(cx + r > box.Min.X && cx - r < box.Max.X && cz + r > box.Min.Z && cz - r < box.Max.Z)) continue;
                    float d = Math.Max(0f, box.Min.Y - top);
                    if (box.Max.Y < top || d > length) continue;
                    var upHit = new QueryHit(d, new Float3(cx, box.Min.Y, cz), new Float3(0, -1, 0), default, new SurfaceId(box.StableId + 1000), box.StableId, box.Tag, revision);
                    if (bestUp == null || d < bestUp.Value.Hit.DistanceMeters) bestUp = new SweepHit(QueryStatus.Hit, upHit);
                }
                return bestUp ?? new SweepHit(QueryStatus.NoHit);
            }
            // Approximate capsule sweep by ray from bottom sphere (sufficient for flat ground + box harness).
            var ray = Raycast(capsule.BottomSphereCenter, dir, length + capsule.Radius, filter);
            if (ray.Status != QueryStatus.Hit) return new SweepHit(QueryStatus.NoHit);
            float adjusted = Math.Max(0f, ray.Hit.DistanceMeters - capsule.Radius);
            if (adjusted > length) return new SweepHit(QueryStatus.NoHit);
            var hit = new QueryHit(adjusted, ray.Hit.Point, ray.Hit.Normal, ray.Hit.EntityId, ray.Hit.StaticSurfaceId, ray.Hit.ColliderStableId, ray.Hit.SurfaceTag, ray.Hit.CollisionRevision);
            return new SweepHit(QueryStatus.Hit, hit);
        }

        public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned)
        {
            MainThread();
            if (!capsule.IsValid) return new OverlapResult(QueryStatus.InvalidInput, 0, 0);
            var hits = new List<QueryHit>();
            float minY = Math.Min(capsule.BottomSphereCenter.Y, capsule.TopSphereCenter.Y) - capsule.Radius;
            float maxY = Math.Max(capsule.BottomSphereCenter.Y, capsule.TopSphereCenter.Y) + capsule.Radius;
            if (minY < 0f && maxY > 0f)
                hits.Add(new QueryHit(-minY, new Float3(capsule.BottomSphereCenter.X, 0, capsule.BottomSphereCenter.Z), new Float3(0, 1, 0), default, new SurfaceId(100), 1, 1, revision));
            foreach (var box in boxes)
            {
                if (!CapsuleAabbOverlap(capsule, box.Min, box.Max)) continue;
                BoxPushOut(capsule, box.Min, box.Max, out float depth, out Float3 pushNormal);
                hits.Add(new QueryHit(depth, box.Min, pushNormal, default, new SurfaceId(box.StableId + 1000), box.StableId, box.Tag, revision));
            }
            int written = Math.Min(Math.Min(hits.Count, callerOwned.Capacity), 256);
            for (int i = 0; i < written; i++) callerOwned[i] = hits[i];
            if (hits.Count == 0) return new OverlapResult(QueryStatus.NoHit, 0, 0);
            if (hits.Count > callerOwned.Capacity || hits.Count > 256) return new OverlapResult(QueryStatus.BufferOverflow, written, Math.Min(hits.Count, 257));
            var result = new OverlapResult(QueryStatus.Hit, written, written);
            callerOwned.SortComplete(result);
            return result;
        }

        /// <summary>Minimum-axis push-out of the capsule's bounding box from a solid box (test approximation).</summary>
        private static void BoxPushOut(Capsule capsule, Float3 min, Float3 max, out float depth, out Float3 normal)
        {
            float cx = capsule.BottomSphereCenter.X, cz = capsule.BottomSphereCenter.Z, r = capsule.Radius;
            float y0 = Math.Min(capsule.BottomSphereCenter.Y, capsule.TopSphereCenter.Y) - r;
            float y1 = Math.Max(capsule.BottomSphereCenter.Y, capsule.TopSphereCenter.Y) + r;
            float bestD = float.MaxValue; Float3 bestN = new Float3(0, 1, 0);
            void Try(float d, Float3 n) { if (d < bestD) { bestD = d; bestN = n; } }
            Try((cx + r) - min.X, new Float3(-1, 0, 0)); Try(max.X - (cx - r), new Float3(1, 0, 0));
            Try((y1) - min.Y, new Float3(0, -1, 0)); Try(max.Y - y0, new Float3(0, 1, 0));
            Try((cz + r) - min.Z, new Float3(0, 0, -1)); Try(max.Z - (cz - r), new Float3(0, 0, 1));
            depth = Math.Max(0f, bestD); normal = bestN;
        }

        private static bool CapsuleAabbOverlap(Capsule capsule, Float3 min, Float3 max)
        {
            float cx = capsule.BottomSphereCenter.X, cz = capsule.BottomSphereCenter.Z, r = capsule.Radius;
            float y0 = Math.Min(capsule.BottomSphereCenter.Y, capsule.TopSphereCenter.Y) - r;
            float y1 = Math.Max(capsule.BottomSphereCenter.Y, capsule.TopSphereCenter.Y) + r;
            return cx + r >= min.X && cx - r <= max.X && cz + r >= min.Z && cz - r <= max.Z && y1 >= min.Y && y0 <= max.Y;
        }

        private static bool RayAabb(Float3 origin, Float3 dir, float max, Float3 min, Float3 maxB, out float t, out Float3 point, out Float3 normal)
        {
            t = 0; point = default; normal = new Float3(0, 1, 0);
            float tmin = 0f, tmax = max;
            Float3 n = new Float3(0, 1, 0);
            if (!ClipAxis(origin.X, dir.X, min.X, maxB.X, ref tmin, ref tmax, ref n, new Float3(-1, 0, 0), new Float3(1, 0, 0))) return false;
            if (!ClipAxis(origin.Y, dir.Y, min.Y, maxB.Y, ref tmin, ref tmax, ref n, new Float3(0, -1, 0), new Float3(0, 1, 0))) return false;
            if (!ClipAxis(origin.Z, dir.Z, min.Z, maxB.Z, ref tmin, ref tmax, ref n, new Float3(0, 0, -1), new Float3(0, 0, 1))) return false;
            if (tmin > max || tmax < 0) return false;
            t = tmin >= 0 ? tmin : tmax;
            if (t < 0 || t > max) return false;
            point = new Float3(origin.X + dir.X * t, origin.Y + dir.Y * t, origin.Z + dir.Z * t);
            normal = n;
            return true;
        }

        private static bool ClipAxis(float o, float d, float min, float max, ref float tmin, ref float tmax, ref Float3 normal, Float3 nMin, Float3 nMax)
        {
            if (Math.Abs(d) < 1e-8f) return o >= min && o <= max;
            float inv = 1f / d;
            float t1 = (min - o) * inv; float t2 = (max - o) * inv;
            Float3 n1 = nMin, n2 = nMax;
            if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; var nt = n1; n1 = n2; n2 = nt; }
            if (t1 > tmin) { tmin = t1; normal = n1; }
            if (t2 < tmax) tmax = t2;
            return tmin <= tmax;
        }
    }
}
