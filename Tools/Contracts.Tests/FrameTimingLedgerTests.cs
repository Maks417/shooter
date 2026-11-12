using System;
using Shooter.Bootstrap;
using Xunit;

namespace Shooter.Contracts.Tests
{
    public sealed class FrameTimingLedgerTests
    {
        [Fact] public void DelayedOutOfOrderUpdatesJoinOnlyTheSameNativeFrame()
        {
            var ledger = new FrameTimingLedger(3); ledger.Add(10, 20); ledger.Add(20, 30); ledger.Add(30, 40);
            Assert.True(ledger.Observe(35, 2, 1, -1)); Assert.True(ledger.Observe(15, 3, 1, 4));
            Assert.Equal(-1, ledger[1].Gpu); Assert.Equal(-1, ledger[2].Gpu);
            Assert.True(ledger.Observe(35, 2, 1, 5)); Assert.Equal(5, ledger[2].Gpu);
            Assert.False(ledger.Observe(9, 1, 1, 1)); Assert.False(ledger.Observe(41, 1, 1, 1));
            Assert.False(ledger.Observe(ulong.MaxValue, 1, 1, 1)); Assert.Equal(-1, ledger[1].Gpu);
        }
        [Fact] public void AmbiguousAndMissingTimingsCannotBeTurnedIntoValidSamples()
        {
            var ledger = new FrameTimingLedger(2); ledger.Add(10, 20); ledger.Add(20, 30);
            ledger.Observe(15, double.NaN, 0, double.PositiveInfinity); Assert.Equal(-1, ledger[0].Gpu);
            ledger.Observe(15, 1, 1, 2); ledger.Observe(16, 1, 1, 2);
            Assert.True(ledger[0].Ambiguous); Assert.Equal(-1, ledger[0].Gpu);
            ledger.Observe(15, 1, 1, 2); Assert.Equal(-1, ledger[0].Gpu); Assert.Equal(-1, ledger[1].Gpu);
        }
        [Fact] public void BoundsAndCapacityAreHardLimits()
        {
            var ledger = new FrameTimingLedger(2); ledger.Add(10, 20);
            Assert.Throws<ArgumentException>(() => ledger.Add(19, 30)); ledger.Add(20, 30);
            Assert.Throws<InvalidOperationException>(() => ledger.Add(30, 40));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger[-1]);
        }
    }
}
