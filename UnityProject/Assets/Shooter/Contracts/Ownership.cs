#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Shooter.Contracts
{
    /// <summary>Defensive owned copy; never exposes its backing array or writable memory.</summary>
    public sealed class FrozenList<T> : IReadOnlyList<T>
    {
        private readonly T[] values;
        public int Count => values.Length;
        public T this[int index] => values[index];
        public FrozenList(IReadOnlyList<T> values, int maxCount) { this.values = Require.Copy(values, maxCount, nameof(values)); }
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Adapters supply an idempotent release action; resource thread dispatch belongs to the adapter.</summary>
    public abstract class ReadLease<T> : IDisposable where T : class
    {
        private readonly T value;
        private Action? release;
        protected ReadLease(T value, Action release)
        { this.value = value ?? throw new ArgumentNullException(nameof(value)); this.release = release ?? throw new ArgumentNullException(nameof(release)); }
        protected T Read() => Volatile.Read(ref release) != null ? value : throw new ObjectDisposedException(GetType().Name);
        public bool IsDisposed => Volatile.Read(ref release) == null;
        public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
    }
    public sealed class SnapshotLease : ReadLease<SimulationSnapshot>
    {
        public SnapshotLease(SimulationSnapshot snapshot, Action release) : base(snapshot, release) { }
        public SimulationSnapshot Snapshot => Read();
    }
    public sealed class EventLease : ReadLease<FrozenList<CombatEvent>>
    {
        public EventLease(FrozenList<CombatEvent> events, Action release) : base(events, release)
        {
            Require.That(events.Count <= ContractVersion.MaxEvents, nameof(events));
            foreach (var item in events) if (item == null) throw new ArgumentException("Null event", nameof(events));
        }
        public IReadOnlyList<CombatEvent> Events => new LeaseView(this);
        private sealed class LeaseView : IReadOnlyList<CombatEvent>
        {
            private readonly EventLease owner;
            public LeaseView(EventLease owner) { this.owner = owner; }
            public int Count => owner.Read().Count;
            public CombatEvent this[int index] => owner.Read()[index];
            public IEnumerator<CombatEvent> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
    public sealed class ContentLease : ReadLease<FrozenList<byte>>
    {
        public ContentId ContentId { get; }
        public ContentKind Kind { get; }
        public ContentLease(ContentId contentId, ContentKind kind, FrozenList<byte> verifiedPayload, Action release) : base(verifiedPayload, release)
        { Require.Enum(kind, nameof(kind)); Require.That(verifiedPayload.Count <= ContractVersion.MaxArtifactBytes, nameof(verifiedPayload)); ContentId = contentId; Kind = kind; }
        public int Length => Read().Count;
        public byte this[int index] => Read()[index];
        public void CopyTo(Span<byte> destination)
        {
            var payload = Read(); Require.That(destination.Length >= payload.Count, nameof(destination));
            for (int i = 0; i < payload.Count; i++) destination[i] = payload[i];
        }
    }
    public sealed class ContentLeaseResult
    {
        public ErrorCode Error { get; }
        public ContentLease? Lease { get; }
        public string Diagnostic { get; }
        public ContentLeaseResult(ErrorCode error, ContentLease? lease, string diagnostic)
        {
            Require.Enum(error, nameof(error));
            Require.That(error == ErrorCode.None ? lease != null && !lease.IsDisposed : lease == null, "content result");
            Require.That(diagnostic != null && diagnostic.Length <= 512, nameof(diagnostic));
            Error = error; Lease = lease; Diagnostic = diagnostic;
        }
    }
}
