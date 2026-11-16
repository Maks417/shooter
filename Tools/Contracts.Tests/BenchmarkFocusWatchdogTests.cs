using System;
using Shooter.Bootstrap;
using Xunit;

namespace Shooter.Contracts.Tests
{
    public sealed class BenchmarkFocusWatchdogTests
    {
        [Fact]
        public void FiresOnlyAfterTheWholeGraceWithoutFocus()
        {
            var watchdog = new BenchmarkFocusWatchdog(5);
            Assert.False(watchdog.Observe(false, 10.0));
            Assert.False(watchdog.Observe(false, 14.9));
            Assert.True(watchdog.Observe(false, 15.0));
        }

        [Fact]
        public void FocusResetsTheGrace()
        {
            var watchdog = new BenchmarkFocusWatchdog(5);
            Assert.False(watchdog.Observe(false, 0));
            Assert.False(watchdog.Observe(false, 4));
            Assert.False(watchdog.Observe(true, 4.5));
            Assert.False(watchdog.Observe(false, 6));
            Assert.False(watchdog.Observe(false, 10.9));
            Assert.True(watchdog.Observe(false, 11));
        }

        [Fact]
        public void ReasonsNameTheCauseAndRejectBadInput()
        {
            var watchdog = new BenchmarkFocusWatchdog();
            Assert.Contains("never received focus", watchdog.Reason(everFocused: false));
            Assert.Contains("lost focus", watchdog.Reason(everFocused: true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BenchmarkFocusWatchdog(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => watchdog.Observe(false, double.NaN));
        }
    }
}
