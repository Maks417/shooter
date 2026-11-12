using System;

namespace Shooter.Bootstrap
{
    /// <summary>Bounded join of delayed native frame-start timestamps to measured frame intervals.</summary>
    public sealed class FrameTimingLedger
    {
        public struct Sample
        {
            public long LowerExclusive, UpperInclusive;
            public ulong NativeTimestamp;
            public double Main, Render, Gpu;
            public bool Ambiguous;
        }
        private readonly Sample[] samples;
        public int Count { get; private set; }
        public FrameTimingLedger(int capacity)
        { if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity)); samples = new Sample[capacity]; }
        public Sample this[int index] => index >= 0 && index < Count ? samples[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public void Add(long lowerExclusive, long upperInclusive)
        {
            if (Count == samples.Length) throw new InvalidOperationException("Timing capacity exceeded");
            if (lowerExclusive < 0 || upperInclusive <= lowerExclusive || Count > 0 && lowerExclusive < samples[Count - 1].UpperInclusive)
                throw new ArgumentException("Non-monotonic frame bounds");
            samples[Count++] = new Sample { LowerExclusive = lowerExclusive, UpperInclusive = upperInclusive, Main = -1, Render = -1, Gpu = -1 };
        }
        public bool Observe(ulong timestamp, double main, double render, double gpu)
        {
            if (timestamp == 0 || timestamp > long.MaxValue || Count == 0) return false;
            int low = 0, high = Count - 1;
            while (low < high) { int mid = low + (high - low) / 2; if ((ulong)samples[mid].UpperInclusive < timestamp) low = mid + 1; else high = mid; }
            ref Sample sample = ref samples[low];
            if (timestamp <= (ulong)sample.LowerExclusive || timestamp > (ulong)sample.UpperInclusive) return false;
            if (sample.NativeTimestamp != 0 && sample.NativeTimestamp != timestamp) { sample.Ambiguous = true; sample.Main = sample.Render = sample.Gpu = -1; }
            if (sample.Ambiguous) return false;
            sample.NativeTimestamp = timestamp;
            // Re-reading the same native frame may complete previously unavailable GPU results.
            // Never use another frame's timing to fill a gap.
            if (Valid(main)) sample.Main = main;
            if (Valid(render)) sample.Render = render;
            if (Valid(gpu)) sample.Gpu = gpu;
            return true;
        }
        private static bool Valid(double value) => value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
