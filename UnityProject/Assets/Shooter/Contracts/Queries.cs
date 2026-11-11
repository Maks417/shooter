#nullable enable
using System;

namespace Shooter.Contracts
{
    public readonly struct Capsule
    {
        public Float3 BottomSphereCenter { get; }
        public Float3 TopSphereCenter { get; }
        public float Radius { get; }
        public bool IsValid => Radius > 0;
        public Capsule(Float3 bottomSphereCenter, Float3 topSphereCenter, float radius)
        {
            Require.That(Require.Finite(radius, nameof(radius)) > 0, nameof(radius));
            BottomSphereCenter = bottomSphereCenter; TopSphereCenter = topSphereCenter; Radius = radius;
        }
    }
    public readonly struct QueryFilter
    {
        public uint LayerMask { get; }
        public EntityId IgnoredEntity { get; }
        public bool IncludeTriggers { get; }
        public QueryFilter(uint layerMask, EntityId ignoredEntity, bool includeTriggers)
        { LayerMask = layerMask; IgnoredEntity = ignoredEntity; IncludeTriggers = includeTriggers; }
    }
    public readonly struct QueryHit
    {
        public float DistanceMeters { get; }
        public Float3 Point { get; }
        public Float3 Normal { get; }
        public EntityId EntityId { get; }
        public SurfaceId StaticSurfaceId { get; }
        public ulong ColliderStableId { get; }
        public ushort SurfaceTag { get; }
        public uint CollisionRevision { get; }
        public bool IsValid => ColliderStableId != 0 && EntityId.IsValid != StaticSurfaceId.IsValid && Normal.IsUnit;
        public QueryHit(float distanceMeters, Float3 point, Float3 normal, EntityId entityId, SurfaceId staticSurfaceId, ulong colliderStableId, ushort surfaceTag, uint collisionRevision)
        {
            Require.That(Require.Finite(distanceMeters, nameof(distanceMeters)) >= 0, nameof(distanceMeters));
            Require.That(entityId.IsValid != staticSurfaceId.IsValid && colliderStableId != 0 && normal.IsUnit, "hit identity/normal");
            DistanceMeters = distanceMeters; Point = point; Normal = normal; EntityId = entityId; StaticSurfaceId = staticSurfaceId;
            ColliderStableId = colliderStableId; SurfaceTag = surfaceTag; CollisionRevision = collisionRevision;
        }
    }
    public readonly struct RayHit
    {
        private readonly QueryHit hit;
        public QueryStatus Status { get; }
        public QueryHit Hit => Status == QueryStatus.Hit ? hit : throw new InvalidOperationException("No usable ray hit");
        public RayHit(QueryStatus status, QueryHit hit = default)
        {
            Require.Enum(status, nameof(status));
            Require.That(status != QueryStatus.BufferOverflow && (status != QueryStatus.Hit || hit.IsValid), nameof(status));
            Status = status; this.hit = hit;
        }
    }
    public readonly struct SweepHit
    {
        private readonly QueryHit hit;
        public QueryStatus Status { get; }
        public QueryHit Hit => Status == QueryStatus.Hit ? hit : throw new InvalidOperationException("No usable sweep hit");
        public SweepHit(QueryStatus status, QueryHit hit = default)
        {
            var validated = new RayHit(status, hit); Status = validated.Status; this.hit = hit;
        }
    }
    public readonly struct OverlapResult
    {
        public QueryStatus Status { get; }
        public int WrittenCount { get; }
        public int RequiredCount { get; }
        public bool IsComplete => Status == QueryStatus.Hit || Status == QueryStatus.NoHit;
        public OverlapResult(QueryStatus status, int writtenCount, int requiredCount)
        {
            Require.Enum(status, nameof(status));
            Require.That(writtenCount >= 0 && writtenCount <= ContractVersion.MaxQueryHits && requiredCount >= writtenCount && requiredCount <= 257, "overlap counts");
            Require.That(status == QueryStatus.NoHit || status == QueryStatus.InvalidInput ? writtenCount == 0 && requiredCount == 0
                : status == QueryStatus.Hit ? writtenCount > 0 && writtenCount == requiredCount && requiredCount <= 256
                : requiredCount > writtenCount, "overlap status/counts");
            Status = status; WrittenCount = writtenCount; RequiredCount = requiredCount;
        }
    }

    /// <summary>Caller-owned fixed storage. Only counts from the returned result are usable.</summary>
    public sealed class HitBuffer
    {
        private readonly QueryHit[] values;
        public int Capacity => values.Length;
        public HitBuffer(int capacity = 128)
        { Require.That(capacity > 0 && capacity <= ContractVersion.MaxQueryHits, nameof(capacity)); values = new QueryHit[capacity]; }
        public QueryHit this[int index] { get => values[index]; set { Require.That(value.IsValid, nameof(value)); values[index] = value; } }
        public void SortComplete(OverlapResult result)
        {
            Require.That(result.IsComplete && result.WrittenCount <= Capacity, nameof(result));
            Array.Sort(values, 0, result.WrittenCount, System.Collections.Generic.Comparer<QueryHit>.Create((a, b) => a.ColliderStableId.CompareTo(b.ColliderStableId)));
        }
    }
    public static class QueryOrder
    {
        public static long DistanceBucket(float meters)
        {
            Require.That(Require.Finite(meters, nameof(meters)) >= 0 && meters <= 1e9f, nameof(meters));
            return checked((long)Math.Round((double)meters * 10000, MidpointRounding.AwayFromZero));
        }
        public static int CompareClosest(QueryHit a, QueryHit b)
        {
            Require.That(a.IsValid && b.IsValid, "hits");
            int distance = DistanceBucket(a.DistanceMeters).CompareTo(DistanceBucket(b.DistanceMeters));
            return distance != 0 ? distance : a.ColliderStableId.CompareTo(b.ColliderStableId);
        }
    }

    public readonly struct PathTicket
    {
        public NavTicketId Id { get; }
        public EntityId Actor { get; }
        public uint NavRevision { get; }
        public PathTicket(NavTicketId id, EntityId actor, uint navRevision)
        { Require.That(id.IsValid && actor.IsValid, "path ticket"); Id = id; Actor = actor; NavRevision = navRevision; }
    }
    public sealed class PathBuffer
    {
        private readonly Float3[] corners;
        public int Capacity => corners.Length;
        public PathBuffer(int capacity)
        { Require.That(capacity > 0 && capacity <= ContractVersion.MaxPathCorners, nameof(capacity)); corners = new Float3[capacity]; }
        public Float3 this[int index] { get => corners[index]; set => corners[index] = value; }
    }
    public readonly struct PathResult
    {
        public PathStatus Status { get; }
        public int WrittenCount { get; }
        public int RequiredCount { get; }
        public ErrorCode Error { get; }
        public PathResult(PathStatus status, int writtenCount, int requiredCount, ErrorCode error)
        {
            Require.Enum(status, nameof(status)); Require.Enum(error, nameof(error));
            Require.That(writtenCount >= 0 && writtenCount <= 128 && requiredCount >= writtenCount && requiredCount <= 128, "path counts");
            bool usable = status == PathStatus.Complete || status == PathStatus.Partial;
            Require.That(usable ? writtenCount > 0 && writtenCount == requiredCount && error == ErrorCode.None
                : writtenCount == 0 && (status != PathStatus.Pending || requiredCount == 0 && error == ErrorCode.None), "path status/counts");
            Require.That(status != PathStatus.Failed || error != ErrorCode.None, nameof(error));
            Require.That(status != PathStatus.Stale || error == ErrorCode.StaleRevision, nameof(error));
            Require.That(status != PathStatus.Cancelled || error == ErrorCode.Cancelled, nameof(error));
            Status = status; WrittenCount = writtenCount; RequiredCount = requiredCount; Error = error;
        }
    }
}
