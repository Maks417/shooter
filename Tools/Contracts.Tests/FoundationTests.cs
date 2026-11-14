using System;
using System.Collections.Generic;
using Shooter.Bootstrap;
using Shooter.Contracts;
using Shooter.Simulation;
using Xunit;

namespace Shooter.Contracts.Tests
{
    public sealed class FoundationTests
    {
        [Fact]
        public void TwoFramesPerTickAndFourTicksPerFramePreserveTheSameTickSequence()
        {
            var slowFrames = new FixedTickClock(false); var fastFrames = new FixedTickClock(false);
            var a = new List<uint>(); var b = new List<uint>();
            for (int i = 0; i < 120; i++) slowFrames.Advance(1.0 / 120, a.Add);
            for (int i = 0; i < 15; i++) fastFrames.Advance(4.0 / 60, b.Add);
            Assert.Equal(a, b); Assert.Equal(60, a.Count); Assert.False(slowFrames.Overloaded); Assert.False(fastFrames.Overloaded);
        }
        [Fact]
        public void OfflineOverloadFreezesAndRetainsUnprocessedDebt()
        {
            var clock = new FixedTickClock(false); int steps = 0; clock.Advance(.2, _ => steps++);
            Assert.Equal(4, steps); Assert.True(clock.Overloaded); double debt = clock.DebtSeconds;
            clock.Advance(1, _ => steps++); Assert.Equal(4, steps); Assert.Equal(debt, clock.DebtSeconds);
        }
        [Fact]
        public void DiagnosticSizedHitchRetainsEveryUnservicedTickAndNeverResumesOffline()
        {
            var clock = new FixedTickClock(false); var ticks = new List<uint>();
            for (int i = 0; i < 120; i++) clock.Advance(1.0 / 60, ticks.Add);
            clock.Advance(.2, ticks.Add);
            Assert.Equal(124u, clock.NextTick); Assert.Equal(4, clock.LastAdvanceTicks);
            Assert.True(clock.Overloaded); Assert.Equal(8.0 / 60, clock.DebtSeconds, 10);
            clock.Advance(0, ticks.Add); clock.Advance(10, ticks.Add);
            Assert.Equal(124, ticks.Count); Assert.Equal(124u, clock.NextTick);
            Assert.Equal(8.0 / 60, clock.DebtSeconds, 10);
        }
        [Fact]
        public void ServerCatchesUpButTerminatesSustainedOneSecondDebt()
        {
            var clock = new FixedTickClock(true); int steps = 0; clock.Advance(.1, _ => steps++); clock.Advance(0, _ => steps++);
            Assert.Equal(6, steps); Assert.False(clock.Overloaded);
            Assert.Throws<InvalidOperationException>(() => clock.Advance(2, _ => steps++));
        }
        [Fact]
        public void FailedStepDoesNotConsumeTickOrDebt()
        {
            var clock = new FixedTickClock(false);
            Assert.Throws<InvalidOperationException>(() => clock.Advance(1.0 / 60, _ => throw new InvalidOperationException()));
            Assert.Equal(0u, clock.NextTick); Assert.Equal(1.0 / 60, clock.DebtSeconds);
        }
        [Fact]
        public void SuspensionDiscardsWallTimeWithoutCatchUpOrTickReordering()
        {
            var clock = new FixedTickClock(false); var ticks = new List<uint>();
            clock.Advance(1.5 / 60, ticks.Add);
            Assert.Equal(new uint[] { 0 }, ticks);
            double fractionalDebt = clock.DebtSeconds;

            clock.SetSuspended(true);
            clock.Advance(10, ticks.Add);
            Assert.Equal(new uint[] { 0 }, ticks);
            Assert.Equal(fractionalDebt, clock.DebtSeconds, 12);
            Assert.False(clock.Overloaded);

            clock.SetSuspended(false);
            clock.Advance(.5 / 60, ticks.Add);
            Assert.Equal(new uint[] { 0, 1 }, ticks);
            Assert.Equal(2u, clock.NextTick);
            Assert.False(clock.Overloaded);
        }
        [Fact]
        public void EmptyAuthorityBoundsPublicationAndAllowsRetryWithoutOverwritingHeldLeases()
        {
            var simulation = new FoundationSimulation(); var snapshots = new List<SnapshotLease>(); var events = new List<EventLease>();
            for (uint tick = 0; tick < 8; tick++) { simulation.Step(tick, ReadOnlyMemory<InputCommand>.Empty); snapshots.Add(simulation.AcquireSnapshot()); events.Add(simulation.AcquireEvents()); }
            Assert.Equal(ErrorCode.Busy, Assert.Throws<StepUnavailableException>(() => simulation.Step(8, ReadOnlyMemory<InputCommand>.Empty)).Error);
            for (int i = 0; i < 8; i++) Assert.Equal((uint)i, snapshots[i].Snapshot.Tick);
            snapshots[0].Dispose(); Assert.Throws<StepUnavailableException>(() => simulation.Step(8, ReadOnlyMemory<InputCommand>.Empty));
            events[0].Dispose(); Assert.Equal(8u, simulation.Step(8, ReadOnlyMemory<InputCommand>.Empty).Tick);
            foreach (var s in snapshots) s.Dispose(); foreach (var e in events) e.Dispose();
        }
        [Fact]
        public void SteadyEmptyStepDoesNotAllocateAndRejectsCommandsForAbsentActors()
        {
            var simulation = new FoundationSimulation(); simulation.Step(0, ReadOnlyMemory<InputCommand>.Empty);
            for (uint t = 1; t <= 100; t++) simulation.Step(t, ReadOnlyMemory<InputCommand>.Empty);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (uint t = 101; t <= 1100; t++) simulation.Step(t, ReadOnlyMemory<InputCommand>.Empty);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            var commands = new[] { ValueAndFixtureTests.Command() }; Assert.Equal(1, simulation.Step(1101, commands).Counters.CommandsRejected);
        }
        private sealed class Fake : IFakeAdapter { }
        [Fact]
        public void ShippingCompositionRejectsMissingAndFakeAdapters()
        {
            Assert.Throws<InvalidOperationException>(() => CompositionPolicy.RequireReal(new object(), null!));
            Assert.Throws<InvalidOperationException>(() => CompositionPolicy.RequireReal(new Fake()));
            CompositionPolicy.RequireReal(new object());
        }
    }
}
