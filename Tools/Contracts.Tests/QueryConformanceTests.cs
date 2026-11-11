using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Shooter.Contracts;
using Xunit;

namespace Shooter.Contracts.Tests
{
    // Synthetic query data: this fake exercises the port contract, not geometric intersection correctness.
    internal sealed class FixtureWorld : IWorldQueries
    {
        private readonly QueryHit[] hits;
        private readonly int owner = Environment.CurrentManagedThreadId;
        public FixtureWorld(IEnumerable<QueryHit> hits) { this.hits = hits.ToArray(); }
        private void MainThread() { if (Environment.CurrentManagedThreadId != owner) throw new InvalidOperationException("Main-thread-only query"); }
        private IEnumerable<QueryHit> Filter(QueryFilter filter) => hits.Where(h => filter.LayerMask != 0 && (!h.EntityId.IsValid || h.EntityId != filter.IgnoredEntity));
        public RayHit Raycast(Float3 origin, Float3 unitDirection, float maxMeters, QueryFilter filter)
        {
            MainThread(); if (!unitDirection.IsUnit || !float.IsFinite(maxMeters) || maxMeters <= 0) return new RayHit(QueryStatus.InvalidInput);
            var selected = Filter(filter).Where(h => h.DistanceMeters <= maxMeters).OrderBy(h => h, Comparer<QueryHit>.Create(QueryOrder.CompareClosest)).ToArray();
            return selected.Length == 0 ? new RayHit(QueryStatus.NoHit) : new RayHit(QueryStatus.Hit, selected[0]);
        }
        public SweepHit SweepCapsule(Capsule capsule, Float3 displacement, QueryFilter filter)
        {
            MainThread(); if (!capsule.IsValid) return new SweepHit(QueryStatus.InvalidInput);
            if (displacement.LengthSquared == 0) return new SweepHit(QueryStatus.NoHit);
            float length = (float)Math.Sqrt(displacement.LengthSquared);
            var ray = Raycast(capsule.BottomSphereCenter, new Float3(displacement.X / length, displacement.Y / length, displacement.Z / length), length, filter);
            return new SweepHit(ray.Status, ray.Status == QueryStatus.Hit ? ray.Hit : default);
        }
        public OverlapResult Overlap(Capsule capsule, QueryFilter filter, HitBuffer callerOwned)
        {
            MainThread(); if (!capsule.IsValid) return new OverlapResult(QueryStatus.InvalidInput, 0, 0);
            var selected = Filter(filter).Take(257).ToArray();
            int count = Math.Min(Math.Min(selected.Length, 256), callerOwned.Capacity);
            for (int i = 0; i < count; i++) callerOwned[i] = selected[i];
            if (selected.Length > callerOwned.Capacity || selected.Length > 256) return new OverlapResult(QueryStatus.BufferOverflow, count, selected.Length);
            var result = new OverlapResult(count == 0 ? QueryStatus.NoHit : QueryStatus.Hit, count, count); callerOwned.SortComplete(result); return result;
        }
    }
    internal sealed class FixtureNavigation : INavigation
    {
        private readonly Dictionary<NavTicketId, (PathTicket Ticket, bool Cancelled)> tickets = new Dictionary<NavTicketId, (PathTicket, bool)>();
        private ulong next;
        public uint Revision { get; set; } = 1;
        public bool Ready { get; set; }
        public PathTicket RequestPath(EntityId actor, Float3 destination, uint navRevision)
        { var ticket = new PathTicket(new NavTicketId(++next), actor, navRevision); tickets.Add(ticket.Id, (ticket, false)); return ticket; }
        public void Cancel(PathTicket ticket) { if (tickets.TryGetValue(ticket.Id, out var entry)) tickets[ticket.Id] = (entry.Ticket, true); }
        public PathResult Poll(PathTicket ticket, PathBuffer callerOwned)
        {
            if (!tickets.TryGetValue(ticket.Id, out var item) || !item.Ticket.Actor.Equals(ticket.Actor) || ticket.NavRevision != item.Ticket.NavRevision) return new PathResult(PathStatus.Stale, 0, 0, ErrorCode.StaleRevision);
            if (item.Cancelled) { tickets.Remove(ticket.Id); return new PathResult(PathStatus.Cancelled, 0, 0, ErrorCode.Cancelled); }
            if (ticket.NavRevision != Revision) { tickets.Remove(ticket.Id); return new PathResult(PathStatus.Stale, 0, 0, ErrorCode.StaleRevision); }
            if (!Ready) return new PathResult(PathStatus.Pending, 0, 0, ErrorCode.None);
            tickets.Remove(ticket.Id);
            if (callerOwned.Capacity < 3) return new PathResult(PathStatus.Failed, 0, 3, ErrorCode.LimitExceeded);
            for (int i = 0; i < 3; i++) callerOwned[i] = new Float3(i, 0, 0);
            return new PathResult(PathStatus.Complete, 3, 3, ErrorCode.None);
        }
    }
    public sealed class QueryConformanceTests
    {
        internal static QueryHit Hit(ulong stableId, float distance = 1) => new QueryHit(distance, default, new Float3(0, 1, 0), default, new SurfaceId(100), stableId, 1, 1);
        private static Capsule Body => new Capsule(new Float3(0, .32f, 0), new Float3(0, 1.48f, 0), .32f);
        private static QueryFilter Filter => new QueryFilter(uint.MaxValue, default, false);
        [Theory]
        [InlineData(0, 128, QueryStatus.NoHit, 0, 0)]
        [InlineData(3, 128, QueryStatus.Hit, 3, 3)]
        [InlineData(200, 128, QueryStatus.BufferOverflow, 128, 200)]
        [InlineData(200, 256, QueryStatus.Hit, 200, 200)]
        [InlineData(256, 256, QueryStatus.Hit, 256, 256)]
        [InlineData(300, 256, QueryStatus.BufferOverflow, 256, 257)]
        public void OverlapNeverTreatsPartialResultsAsGameplay(int count, int capacity, QueryStatus expected, int written, int required)
        {
            var world = new FixtureWorld(Enumerable.Range(1, count).Reverse().Select(i => Hit((ulong)i)));
            var buffer = new HitBuffer(capacity); var result = world.Overlap(Body, Filter, buffer);
            Assert.Equal(expected, result.Status); Assert.Equal(written, result.WrittenCount); Assert.Equal(required, result.RequiredCount);
            if (result.IsComplete && written > 0) Assert.Equal(1UL, buffer[0].ColliderStableId);
            if (!result.IsComplete) Assert.Throws<ArgumentException>(() => buffer.SortComplete(result));
        }
        [Fact]
        public void ClosestTieUsesQuantizedDistanceThenStableIdAndIsTransitive()
        {
            var near = Hit(9, 1.00001f); var sameBucket = Hit(2, 1.00004f); var nextBucket = Hit(1, 1.00011f);
            var world = new FixtureWorld(new[] { nextBucket, near, sameBucket });
            Assert.Equal(2UL, world.Raycast(default, new Float3(0, 0, 1), 10, Filter).Hit.ColliderStableId);
            Assert.True(QueryOrder.CompareClosest(sameBucket, near) < 0); Assert.True(QueryOrder.CompareClosest(near, nextBucket) < 0);
            Assert.True(QueryOrder.CompareClosest(sameBucket, nextBucket) < 0);
            // This midpoint is exactly representable as float32.
            Assert.Equal(313L, QueryOrder.DistanceBucket(.03125f));
        }
        [Fact]
        public void InvalidQueriesAndZeroSweepHaveExplicitResults()
        {
            var world = new FixtureWorld(new[] { Hit(1) });
            Assert.Equal(QueryStatus.NoHit, world.SweepCapsule(Body, default, Filter).Status);
            Assert.Equal(QueryStatus.InvalidInput, world.SweepCapsule(default, default, Filter).Status);
            Assert.Equal(QueryStatus.InvalidInput, world.Raycast(default, default, 1, Filter).Status);
            Assert.Equal(QueryStatus.InvalidInput, world.Raycast(default, new Float3(0, 0, 1), 0, Filter).Status);
            Assert.Throws<InvalidOperationException>(() => new RayHit(QueryStatus.NoHit).Hit);
            Assert.Throws<ArgumentException>(() => new OverlapResult(QueryStatus.Hit, 128, 200));
            Assert.Throws<ArgumentException>(() => new QueryHit(0, default, new Float3(0, 1, 0), new EntityId(1), new SurfaceId(1), 1, 0, 1));
            Assert.Throws<ArgumentException>(() => new HitBuffer(257)); Assert.Throws<ArgumentException>(() => new PathBuffer(129));
        }
        [Fact]
        public void QueryFakeRejectsWorkerThreadCalls()
        {
            var world = new FixtureWorld(Array.Empty<QueryHit>()); Exception? failure = null;
            var worker = new Thread(() => { try { world.Overlap(Body, Filter, new HitBuffer()); } catch (Exception e) { failure = e; } }); worker.Start(); worker.Join();
            Assert.IsType<InvalidOperationException>(failure);
        }
        [Fact]
        public void NavigationCapacityFailureConsumesTicketInsteadOfReturningTruncatedPath()
        {
            var nav = new FixtureNavigation(); var ticket = nav.RequestPath(new EntityId(1), default, 1);
            Assert.Equal(PathStatus.Pending, nav.Poll(ticket, new PathBuffer(1)).Status);
            nav.Ready = true; var result = nav.Poll(ticket, new PathBuffer(1));
            Assert.Equal(PathStatus.Failed, result.Status); Assert.Equal(ErrorCode.LimitExceeded, result.Error); Assert.Equal(0, result.WrittenCount); Assert.Equal(3, result.RequiredCount);
            Assert.Equal(PathStatus.Stale, nav.Poll(ticket, new PathBuffer(3)).Status);
            var replacement = nav.RequestPath(new EntityId(1), default, 1); Assert.Equal(PathStatus.Complete, nav.Poll(replacement, new PathBuffer(3)).Status);
        }
        [Fact]
        public void NavigationStaleAndCancellationConsumeOnlyTheirOwnTicket()
        {
            var nav = new FixtureNavigation(); var cancelled = nav.RequestPath(new EntityId(1), default, 1); var other = nav.RequestPath(new EntityId(2), default, 1);
            nav.Cancel(cancelled); Assert.Equal(PathStatus.Cancelled, nav.Poll(cancelled, new PathBuffer(3)).Status);
            Assert.Equal(PathStatus.Stale, nav.Poll(cancelled, new PathBuffer(3)).Status); Assert.Equal(PathStatus.Pending, nav.Poll(other, new PathBuffer(3)).Status);
            nav.Revision = 2; Assert.Equal(PathStatus.Stale, nav.Poll(other, new PathBuffer(3)).Status);
            Assert.Throws<ArgumentException>(() => new PathResult(PathStatus.Complete, 0, 3, ErrorCode.None));
        }
    }
}
