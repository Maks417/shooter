#nullable enable
using System;

namespace Shooter.Bootstrap
{
    /// <summary>
    /// Fails a product benchmark fast when the match stops advancing. A capture that stalls (paused match, held clock,
    /// unfocused window or anything else) would otherwise run until the wrapper's timeout with no explanation. The
    /// caller feeds the completed tick and a monotonic clock each frame; once the tick has not moved for the whole
    /// grace period, <see cref="Observe"/> returns true and the caller aborts with a state snapshot. Pure.
    /// </summary>
    public sealed class BenchmarkProgressWatchdog
    {
        public const double DefaultGraceSeconds = 10.0;
        private readonly double graceSeconds;
        private uint lastTick;
        private double lastProgressSeconds = double.NaN;

        public BenchmarkProgressWatchdog(double graceSeconds = DefaultGraceSeconds)
        {
            if (!(graceSeconds > 0) || double.IsInfinity(graceSeconds)) throw new ArgumentOutOfRangeException(nameof(graceSeconds));
            this.graceSeconds = graceSeconds;
        }

        public double GraceSeconds => graceSeconds;

        /// <summary>Seconds since the tick last advanced (0 before the first observation).</summary>
        public double StalledSeconds { get; private set; }

        public bool Observe(uint completedTick, double nowSeconds)
        {
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds)) throw new ArgumentOutOfRangeException(nameof(nowSeconds));
            if (double.IsNaN(lastProgressSeconds) || completedTick != lastTick)
            {
                lastTick = completedTick;
                lastProgressSeconds = nowSeconds;
                StalledSeconds = 0;
                return false;
            }
            StalledSeconds = nowSeconds - lastProgressSeconds;
            return StalledSeconds >= graceSeconds;
        }
    }
}
