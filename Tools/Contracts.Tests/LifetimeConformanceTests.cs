using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Contracts;
using Xunit;

namespace Shooter.Contracts.Tests
{
    internal sealed class FixturePool<T> where T : class
    {
        private readonly T?[] values = new T?[ContractVersion.MaxLeasedBuffers];
        private readonly int[] readers = new int[ContractVersion.MaxLeasedBuffers];
        private int latest = -1;
        public ErrorCode Publish(T value)
        {
            for (int i = 0; i < readers.Length; i++)
            {
                if (readers[i] != 0) continue;
                values[i] = value; latest = i; return ErrorCode.None;
            }
            return ErrorCode.Busy;
        }
        public TLease Acquire<TLease>(Func<T, Action, TLease> factory)
        {
            if (latest < 0) throw new InvalidOperationException("No published data");
            int slot = latest; readers[slot]++; return factory(values[slot]!, () => readers[slot]--);
        }
    }
    internal sealed class FixtureContentStore : IContentStore
    {
        private readonly TaskCompletionSource<FrozenList<byte>> sharedRead = new TaskCompletionSource<FrozenList<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ActiveLeases;
        public int PhysicalReads { get; private set; }
        public void CompleteRead(byte[] verifiedBytes) { PhysicalReads++; sharedRead.SetResult(new FrozenList<byte>(verifiedBytes, ContractVersion.MaxArtifactBytes)); }
        public async Task<ContentLeaseResult> AcquireAsync(ContentId id, ContentKind kind, CancellationToken cancel)
        {
            FrozenList<byte> payload;
            try { payload = await sharedRead.Task.WaitAsync(cancel); }
            catch (OperationCanceledException) { return new ContentLeaseResult(ErrorCode.Cancelled, null, "Cancelled"); }
            if (cancel.IsCancellationRequested) return new ContentLeaseResult(ErrorCode.Cancelled, null, "Cancelled");
            Interlocked.Increment(ref ActiveLeases);
            return new ContentLeaseResult(ErrorCode.None, new ContentLease(id, kind, payload, () => Interlocked.Decrement(ref ActiveLeases)), "");
        }
    }
    public sealed class LifetimeConformanceTests
    {
        internal static SimulationSnapshot Snapshot(uint tick = 0) => new SimulationSnapshot(tick, 1, MatchState.Ready, default, Array.Empty<EntitySnapshot>(), Array.Empty<HitVolume>());
        [Fact]
        public void EightHeldSnapshotBuffersBlockPublicationWithoutOverwritingLiveData()
        {
            var pool = new FixturePool<SimulationSnapshot>(); var leases = new List<SnapshotLease>();
            for (uint tick = 0; tick < 8; tick++)
            {
                Assert.Equal(ErrorCode.None, pool.Publish(Snapshot(tick))); leases.Add(pool.Acquire((s, release) => new SnapshotLease(s, release)));
            }
            Assert.Equal(ErrorCode.Busy, pool.Publish(Snapshot(8)));
            for (int i = 0; i < leases.Count; i++) Assert.Equal((uint)i, leases[i].Snapshot.Tick);
            leases[0].Dispose(); leases[0].Dispose(); Assert.Equal(ErrorCode.None, pool.Publish(Snapshot(8)));
            Assert.Throws<ObjectDisposedException>(() => leases[0].Snapshot);
            using var replacement = pool.Acquire((s, release) => new SnapshotLease(s, release)); Assert.Equal(8u, replacement.Snapshot.Tick);
            foreach (var lease in leases) lease.Dispose();
        }
        [Fact]
        public void ConsumersAcquireIndependentLeasesAndBorrowedCallsDoNotStealOwnership()
        {
            var pool = new FixturePool<SimulationSnapshot>(); pool.Publish(Snapshot());
            using var first = pool.Acquire((s, release) => new SnapshotLease(s, release));
            using var second = pool.Acquire((s, release) => new SnapshotLease(s, release));
            first.Dispose(); Assert.Equal(0u, second.Snapshot.Tick);
            int released = 0; using var events = new EventLease(new FrozenList<CombatEvent>(Array.Empty<CombatEvent>(), 4096), () => released++);
            ISnapshotConsumer consumer = new BorrowingConsumer(); consumer.Consume(second, events);
            Assert.False(second.IsDisposed); Assert.Equal(0, released);
        }
        private sealed class BorrowingConsumer : ISnapshotConsumer
        {
            public void Consume(SnapshotLease snapshot, EventLease events) { Assert.Equal(0u, snapshot.Snapshot.Tick); Assert.Empty(events.Events); }
        }
        [Fact]
        public void EventLeasesGuardRetainedViewsAndPoolExhaustion()
        {
            var pool = new FixturePool<FrozenList<CombatEvent>>(); var leases = new List<EventLease>();
            var events = new FrozenList<CombatEvent>(new[] { new CombatEvent(new EventId(1, 0, new EntityId(1), 0), CombatEventKind.Death, default, new EntityId(1), new DeathPayload(default)) }, 4096);
            for (int i = 0; i < 8; i++) { Assert.Equal(ErrorCode.None, pool.Publish(events)); leases.Add(pool.Acquire((e, release) => new EventLease(e, release))); }
            Assert.Equal(ErrorCode.Busy, pool.Publish(events)); var retained = leases[0].Events;
            leases[0].Dispose(); Assert.Throws<ObjectDisposedException>(() => retained.Count); Assert.Throws<ObjectDisposedException>(() => retained[0]);
            Assert.Equal(ErrorCode.None, pool.Publish(events)); foreach (var lease in leases) lease.Dispose();
            Assert.Throws<ArgumentException>(() => new EventLease(new FrozenList<CombatEvent>(Enumerable.Repeat(events[0], 4097).ToArray(), 4097), () => { }));
        }
        [Fact]
        public async Task CancelledContentConsumerDoesNotCancelSharedReadOrOtherLeases()
        {
            var store = new FixtureContentStore(); using var cancelled = new CancellationTokenSource();
            Task<ContentLeaseResult> first = store.AcquireAsync(default, ContentKind.Collision, cancelled.Token);
            Task<ContentLeaseResult> second = store.AcquireAsync(default, ContentKind.Collision, CancellationToken.None);
            cancelled.Cancel(); var failed = await first; Assert.Equal(ErrorCode.Cancelled, failed.Error); Assert.Null(failed.Lease);
            store.CompleteRead(new byte[] { 1, 2, 3 }); var good = await second; using var lease = good.Lease!;
            Assert.Equal(ErrorCode.None, good.Error); Assert.Equal(1, store.PhysicalReads); Assert.Equal(1, store.ActiveLeases);
            Assert.Equal(2, lease[1]); var copy = new byte[3]; lease.CopyTo(copy); Assert.Equal(new byte[] { 1, 2, 3 }, copy);
            var third = await store.AcquireAsync(default, ContentKind.Collision, CancellationToken.None); using var thirdLease = third.Lease!;
            lease.Dispose(); lease.Dispose(); Assert.Equal(1, store.ActiveLeases); Assert.Equal(3, thirdLease.Length);
            Assert.Throws<ObjectDisposedException>(() => lease[0]); thirdLease.Dispose(); Assert.Equal(0, store.ActiveLeases);
        }
        [Fact]
        public void ResultUnionAndDrainNeverExposePartialSuccess()
        {
            Assert.Throws<ArgumentException>(() => new ContentLeaseResult(ErrorCode.None, null, ""));
            using var lease = new ContentLease(default, ContentKind.Mesh, new FrozenList<byte>(new byte[] { 1 }, 1), () => { });
            Assert.Throws<ArgumentException>(() => new ContentLeaseResult(ErrorCode.Cancelled, lease, ""));
            Assert.Throws<ArgumentException>(() => new DrainResult(ErrorCode.LimitExceeded, 2, 3));
            Assert.Throws<ArgumentException>(() => new DrainResult(ErrorCode.None, 2, 3));
            var overflow = new DrainResult(ErrorCode.LimitExceeded, 0, 257); Assert.Equal(257, overflow.RequiredCount);
        }
        [Fact]
        public void SnapshotsDefensivelyCopyAndRejectDuplicateActorsOrForeignVolumes()
        {
            using var fixture = FixtureJson.Load("snapshot.json"); var source = FixtureJson.Snapshot(fixture.RootElement);
            var entities = source.Entities.ToArray(); var snapshot = new SimulationSnapshot(0, 1, MatchState.Ready, default, entities, Array.Empty<HitVolume>());
            entities[0] = null!; Assert.Equal(1UL, snapshot.Entities[0].Id.Value);
            Assert.Throws<ArgumentException>(() => new SimulationSnapshot(0, 1, MatchState.Ready, default, new[] { source.Entities[0], source.Entities[0] }, Array.Empty<HitVolume>()));
            var foreign = new HitVolume(new EntityId(2), 1, new Capsule(default, new Float3(0, 1, 0), .1f), 0);
            Assert.Throws<ArgumentException>(() => new SimulationSnapshot(0, 1, MatchState.Ready, default, source.Entities, new[] { foreign }));
        }
    }
}
