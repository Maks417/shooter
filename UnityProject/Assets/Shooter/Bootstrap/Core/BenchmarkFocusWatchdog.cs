#nullable enable
using System;

namespace Shooter.Bootstrap
{
    /// <summary>
    /// Product matches hold their simulation clock while the window is unfocused (player-facing pause rule). A benchmark
    /// whose window never receives focus, for example because it launched behind another application, would otherwise
    /// stall silently until the wrapper's timeout. This watchdog reports the stall after a short continuous grace so
    /// the run fails fast with an explicit reason. Pure: the caller supplies focus state and a monotonic clock.
    /// </summary>
    public sealed class BenchmarkFocusWatchdog
    {
        public const double DefaultGraceSeconds = 5.0;
        private readonly double graceSeconds;
        private double unfocusedSince = double.NaN;

        public BenchmarkFocusWatchdog(double graceSeconds = DefaultGraceSeconds)
        {
            if (!(graceSeconds > 0) || double.IsInfinity(graceSeconds)) throw new ArgumentOutOfRangeException(nameof(graceSeconds));
            this.graceSeconds = graceSeconds;
        }

        public double GraceSeconds => graceSeconds;

        /// <summary>Feed one observation; returns true once the window has been unfocused for the whole grace period.</summary>
        public bool Observe(bool focused, double nowSeconds)
        {
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds)) throw new ArgumentOutOfRangeException(nameof(nowSeconds));
            if (focused)
            {
                unfocusedSince = double.NaN;
                return false;
            }
            if (double.IsNaN(unfocusedSince)) unfocusedSince = nowSeconds;
            return nowSeconds - unfocusedSince >= graceSeconds;
        }

        public string Reason(bool everFocused) => everFocused
            ? "Benchmark window lost focus for " + graceSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s; the product match pauses without focus, so the capture cannot complete."
            : "Benchmark window never received focus within " + graceSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s (it may have opened behind another application); the product match holds its clock without focus. Rerun with the machine idle or bring the window to the front.";
    }
}
