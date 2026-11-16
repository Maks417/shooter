using System;
using Shooter.Bootstrap;
using Xunit;

namespace Shooter.Contracts.Tests
{
    public sealed class BenchmarkProgressWatchdogTests
    {
        [Fact]
        public void FiresOnlyAfterTheWholeGraceWithoutTickProgress()
        {
            var watchdog = new BenchmarkProgressWatchdog(10);
            Assert.False(watchdog.Observe(120, 0.0));
            Assert.False(watchdog.Observe(120, 9.9));
            Assert.Equal(9.9, watchdog.StalledSeconds, 6);
            Assert.True(watchdog.Observe(120, 10.0));
        }

        [Fact]
        public void AnyTickProgressRestartsTheGrace()
        {
            var watchdog = new BenchmarkProgressWatchdog(10);
            Assert.False(watchdog.Observe(120, 0));
            Assert.False(watchdog.Observe(120, 9));
            Assert.False(watchdog.Observe(121, 9.5));
            Assert.Equal(0, watchdog.StalledSeconds);
            Assert.False(watchdog.Observe(121, 19.4));
            Assert.True(watchdog.Observe(121, 19.5));
        }

        [Fact]
        public void RejectsBadInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BenchmarkProgressWatchdog(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BenchmarkProgressWatchdog().Observe(1, double.PositiveInfinity));
        }
    }
}
