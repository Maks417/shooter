using System;
using Shooter.Contracts;
using UnityEngine;

namespace Shooter.World
{
    public sealed class UnityWorldQueries : IWorldQueries, IDisposable
    {
        // One extra internal probe hit can be filtered before the independent 257 sentinel.
        private readonly RaycastHit[] castHits = new RaycastHit[258];
        private readonly Collider[] overlaps = new Collider[258];
        private readonly int owner = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly CapsuleCollider probe;
        public uint Revision { get; }
        public UnityWorldQueries(uint revision)
        {
            Revision = revision;
            var queryObject = new GameObject("Internal overlap probe") { hideFlags = HideFlags.HideInHierarchy, layer = 2 };
            queryObject.transform.position = new Vector3(100000, 100000, 100000);
            probe = queryObject.AddComponent<CapsuleCollider>(); probe.isTrigger = true;
        }
        public void Dispose() { MainThread(); if (probe != null) UnityEngine.Object.Destroy(probe.gameObject); }
        private void MainThread()
        { if (System.Threading.Thread.CurrentThread.ManagedThreadId != owner) throw new InvalidOperationException("World queries require main thread"); }
        public static Vector3 V(Float3 v) => new Vector3(v.X, v.Y, v.Z);
        public static Float3 F(Vector3 v) => new Float3(v.x, v.y, v.z);
        private static QueryTriggerInteraction Triggers(QueryFilter filter) => filter.IncludeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        private QueryHit ConvertHit(RaycastHit hit)
        {
            var surface = hit.collider.GetComponent<StableSurface>();
            if (surface == null || surface.StableId == 0) throw new InvalidOperationException("Collision has no stable static identity");
            return new QueryHit(hit.distance, F(hit.point), F(hit.normal.normalized), default, new SurfaceId(surface.SurfaceId), surface.StableId, surface.SurfaceTag, Revision);
        }
        private bool Closest(int count, out QueryHit closest)
        {
            closest = default;
            int accepted = 0;
            for (int i = 0; i < count; i++)
            {
                if (castHits[i].collider == probe) continue;
                if (++accepted > 256) throw new InvalidOperationException("Cast hard cap exceeded; no authoritative partial hit");
                var hit = ConvertHit(castHits[i]);
                if (accepted == 1 || QueryOrder.CompareClosest(hit, closest) < 0) closest = hit;
            }
            return accepted != 0;
        }
        public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
        {
            MainThread();
            if (!unitDirection.IsUnit || float.IsNaN(maxMeters) || float.IsInfinity(maxMeters) || maxMeters <= 0) return new RayHit(QueryStatus.InvalidInput);
            int count = Physics.RaycastNonAlloc(V(origin), V(unitDirection), castHits, maxMeters, unchecked((int)filter.LayerMask), Triggers(filter));
            return Closest(count, out var hit) ? new RayHit(QueryStatus.Hit, hit) : new RayHit(QueryStatus.NoHit);
        }
        public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter)
        {
            MainThread(); if (!capsule.IsValid) return new SweepHit(QueryStatus.InvalidInput);
            if (displacement.LengthSquared == 0) return new SweepHit(QueryStatus.NoHit);
            Vector3 delta = V(displacement); float distance = delta.magnitude;
            if (float.IsInfinity(distance) || distance <= 0) return new SweepHit(QueryStatus.InvalidInput);
            int count = Physics.CapsuleCastNonAlloc(V(capsule.BottomSphereCenter), V(capsule.TopSphereCenter), capsule.Radius, delta / distance, castHits, distance, unchecked((int)filter.LayerMask), Triggers(filter));
            return Closest(count, out var hit) ? new SweepHit(QueryStatus.Hit, hit) : new SweepHit(QueryStatus.NoHit);
        }
        public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned)
        {
            MainThread(); if (!capsule.IsValid) return new OverlapResult(QueryStatus.InvalidInput, 0, 0);
            if (callerOwned == null) throw new ArgumentNullException(nameof(callerOwned));
            int count = Physics.OverlapCapsuleNonAlloc(V(capsule.BottomSphereCenter), V(capsule.TopSphereCenter), capsule.Radius, overlaps, unchecked((int)filter.LayerMask), Triggers(filter));
            int filtered = 0;
            for (int i = 0; i < count; i++) if (overlaps[i] != probe) overlaps[filtered++] = overlaps[i];
            count = Math.Min(filtered, 257);
            int written = Math.Min(Math.Min(count, 256), callerOwned.Capacity);
            Vector3 bottom = V(capsule.BottomSphereCenter), top = V(capsule.TopSphereCenter);
            Vector3 center = (bottom + top) * .5f;
            Quaternion rotation = top == bottom ? Quaternion.identity : Quaternion.FromToRotation(Vector3.up, (top - bottom).normalized);
            probe.radius = capsule.Radius; probe.height = (top - bottom).magnitude + 2 * capsule.Radius;
            for (int i = 0; i < written; i++)
            {
                var surface = overlaps[i].GetComponent<StableSurface>();
                if (surface == null || surface.StableId == 0) throw new InvalidOperationException("Overlap has no stable static identity");
                if (!Physics.ComputePenetration(probe, center, rotation, overlaps[i], overlaps[i].transform.position, overlaps[i].transform.rotation, out var normal, out float depth))
                    return new OverlapResult(QueryStatus.InvalidInput, 0, 0); // Never invent a recovery normal.
                var point = overlaps[i].ClosestPoint(center + normal * (probe.height + capsule.Radius));
                callerOwned[i] = new QueryHit(depth, F(point), F(normal.normalized), default, new SurfaceId(surface.SurfaceId), surface.StableId, surface.SurfaceTag, Revision);
            }
            if (count > callerOwned.Capacity || count > 256) return new OverlapResult(QueryStatus.BufferOverflow, written, count);
            var result = new OverlapResult(count == 0 ? QueryStatus.NoHit : QueryStatus.Hit, written, count);
            callerOwned.SortComplete(result); return result;
        }
    }
}
