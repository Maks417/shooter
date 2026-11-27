using System;
using Shooter.Bootstrap;
using Xunit;

namespace Shooter.Contracts.Tests
{
    public sealed class FrameTimingWindowTests
    {
        [Fact]
        public void Summary_reports_p50_p95_max_and_slow_frames()
        {
            var window = new FrameTimingWindow(256);
            // 100 frames: 1..100 ms; CPU main = frame - 5, render = 2, GPU = frame - 10.
            for (int i = 1; i <= 100; i++) window.Add(i, i - 5, 2, i - 10);
            Assert.Equal(100, window.Frames);
            Assert.Equal(34, window.SlowFrames); // 67..100 ms (34 frames) exceed the 66.7 ms (4-tick) limit.
            Assert.Equal(50, window.Percentile(0, .5));
            Assert.Equal(95, window.Percentile(0, .95));
            string summary = window.Summary();
            Assert.Contains("frames=100", summary);
            Assert.Contains("seconds=5.1", summary); // 5050 ms of frames.
            Assert.Contains("fps=19.8", summary);
            Assert.Contains("frameMs=50.0/95.0/100.0", summary);
            Assert.Contains("renderMs=2.0/2.0/2.0", summary);
            Assert.Contains("slowFrames=34", summary);
            Assert.DoesNotContain("sampled=", summary);
        }

        [Fact]
        public void Unavailable_timings_are_skipped_and_reported_as_na()
        {
            var window = new FrameTimingWindow(16);
            window.Add(20, -1, double.NaN, -1);
            window.Add(30, 12, double.NaN, -1);
            Assert.Equal(12, window.Percentile(1, .5)); // only the valid main-thread sample counts
            string summary = window.Summary();
            Assert.Contains("renderMs=na", summary);
            Assert.Contains("gpuMs=na", summary);
            Assert.Contains("mainMs=12.0/12.0/12.0", summary);
        }

        [Fact]
        public void Frames_past_capacity_are_counted_and_keep_the_true_maximum()
        {
            var window = new FrameTimingWindow(4);
            for (int i = 0; i < 4; i++) window.Add(10, 5, 5, 5);
            window.Add(200, 5, 5, 5); // over capacity: not sampled, but counted and still the max
            Assert.Equal(5, window.Frames);
            Assert.Equal(1, window.SlowFrames);
            string summary = window.Summary();
            Assert.Contains("frameMs=10.0/10.0/200.0", summary);
            Assert.Contains("sampled=4", summary);
        }

        [Fact]
        public void Reset_starts_an_empty_window_and_invalid_frames_are_ignored()
        {
            var window = new FrameTimingWindow(8);
            window.Add(50, 1, 1, 1);
            window.Add(double.NaN, 1, 1, 1);
            window.Add(-3, 1, 1, 1);
            Assert.Equal(1, window.Frames);
            window.Reset();
            Assert.Equal(0, window.Frames);
            Assert.Equal(0, window.Seconds);
            Assert.True(double.IsNaN(window.Percentile(0, .5)));
            Assert.Contains("frameMs=na", window.Summary());
            Assert.Throws<ArgumentOutOfRangeException>(() => new FrameTimingWindow(0));
        }

        [Fact]
        public void Adding_frames_does_not_allocate()
        {
            var window = new FrameTimingWindow(4096);
            window.Add(16, 8, 4, 10); // warm up
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 5000; i++) window.Add(16 + i % 7, 8, 4, 10);
            Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        }
    }
}
