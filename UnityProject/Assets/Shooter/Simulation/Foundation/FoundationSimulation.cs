using System;
using System.Security.Cryptography;
using System.Text;
using Shooter.Contracts;

namespace Shooter.Simulation
{
    public sealed class StepUnavailableException : InvalidOperationException
    {
        public ErrorCode Error { get; }
        public StepUnavailableException(ErrorCode error) : base("Foundation step unavailable: " + error) { Error = error; }
    }
    /// <summary>M0 empty authority: no actors/combat. Tick publication and leases are real; commands target no existing actor.</summary>
    public sealed class FoundationSimulation : ISimulation
    {
        private sealed class Slot { public uint Tick; public int Readers; }
        private readonly Slot[] snapshots = new Slot[8];
        private readonly Slot[] events = new Slot[8];
        private readonly FrozenList<CombatEvent> emptyEvents = new FrozenList<CombatEvent>(Array.Empty<CombatEvent>(), 4096);
        private readonly ContentId emptyStateDigest;
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private int publishedSnapshot = -1, publishedEvents = -1;
        private uint tick;
        public FoundationSimulation()
        {
            for (int i = 0; i < 8; i++) { snapshots[i] = new Slot(); events[i] = new Slot(); }
            using var hash = SHA256.Create();
            // State is empty/Ready/world revision 1. Tick is separately carried in StepResult.
            emptyStateDigest = new ContentId(hash.ComputeHash(Encoding.ASCII.GetBytes("shooter.foundation.empty-world.v1")));
        }
        private void MainThread()
        { if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Simulation is main-thread-only"); }
        private static int Free(Slot[] slots)
        { for (int i = 0; i < slots.Length; i++) if (slots[i].Readers == 0) return i; return -1; }
        public StepResult Step(uint requestedTick, ReadOnlyMemory<InputCommand> commands)
        {
            MainThread();
            if (commands.Length > 256) throw new ArgumentException("Command cap exceeded", nameof(commands));
            if (publishedSnapshot >= 0 && requestedTick != unchecked(tick + 1) || publishedSnapshot < 0 && requestedTick != 0) throw new ArgumentException("Nonconsecutive simulation tick", nameof(requestedTick));
            int snapshot = Free(snapshots), eventBuffer = Free(events);
            if (snapshot < 0 || eventBuffer < 0) throw new StepUnavailableException(ErrorCode.Busy);
            snapshots[snapshot].Tick = requestedTick; events[eventBuffer].Tick = requestedTick;
            tick = requestedTick; publishedSnapshot = snapshot; publishedEvents = eventBuffer;
            return new StepResult(tick, emptyStateDigest, 0, new StepCounters(0, commands.Length, 0, 0, 0, 0, false));
        }
        public SnapshotLease AcquireSnapshot()
        {
            MainThread(); if (publishedSnapshot < 0) throw new InvalidOperationException("No completed tick");
            var slot = snapshots[publishedSnapshot];
            var snapshot = new SimulationSnapshot(slot.Tick, 1, MatchState.Ready, default, Array.Empty<EntitySnapshot>(), Array.Empty<HitVolume>());
            slot.Readers++;
            return new SnapshotLease(snapshot, () => { MainThread(); slot.Readers--; });
        }
        public EventLease AcquireEvents()
        {
            MainThread(); if (publishedEvents < 0) throw new InvalidOperationException("No completed tick");
            var slot = events[publishedEvents]; slot.Readers++;
            return new EventLease(emptyEvents, () => { MainThread(); slot.Readers--; });
        }
    }
}
