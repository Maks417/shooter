using System;
using System.Globalization;
using System.Text;

namespace Shooter.Bootstrap
{
    /// <summary>
    /// Bounded window of per-frame timings for the product player log (SHOOTER_FRAME_SUMMARY): frame interval plus
    /// CPU main thread, CPU render thread and GPU time, reported as p50/p95/max with a count of frames slower than the
    /// fixed-tick catch-up limit. Ordinary play sessions can then attribute stalls without a benchmark run. Adding a
    /// frame does not allocate; a summary allocates its string once per window.
    /// </summary>
    public sealed class FrameTimingWindow
    {
        /// <summary>Frames slower than this drop simulation time (4 ticks at 60 Hz; the SHOOTER_HITCH condition).</summary>
        public const double SlowFrameMs = 4000.0 / 60.0;

        private readonly double[] frame, main, render, gpu, scratch;
        private int stored;
        private double frameMax;

        public FrameTimingWindow(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            frame = new double[capacity]; main = new double[capacity]; render = new double[capacity]; gpu = new double[capacity];
            scratch = new double[capacity];
        }

        /// <summary>Frames seen in this window, including any past capacity.</summary>
        public int Frames { get; private set; }
        /// <summary>Frames slower than <see cref="SlowFrameMs"/> in this window.</summary>
        public int SlowFrames { get; private set; }
        public double Seconds { get; private set; }

        /// <summary>Adds one frame. Unavailable CPU/GPU times are passed as a negative value or NaN.</summary>
        public void Add(double frameMs, double mainMs, double renderMs, double gpuMs)
        {
            if (!(frameMs >= 0) || double.IsInfinity(frameMs)) return;
            Frames++;
            Seconds += frameMs / 1000.0;
            if (frameMs > SlowFrameMs) SlowFrames++;
            if (frameMs > frameMax) frameMax = frameMs;
            if (stored == frame.Length) return;
            frame[stored] = frameMs;
            main[stored] = Valid(mainMs);
            render[stored] = Valid(renderMs);
            gpu[stored] = Valid(gpuMs);
            stored++;
        }

        public void Reset()
        {
            stored = 0; Frames = 0; SlowFrames = 0; Seconds = 0; frameMax = 0;
        }

        /// <summary>One log line for the window, for example
        /// <c>seconds=10.0 frames=212 fps=21.2 frameMs=47.1/81.3/183.5 mainMs=44.7/67.7/121.5 renderMs=... gpuMs=38.0/65.7/167.1 slowFrames=14</c>
        /// (each triple is p50/p95/max; "na" when that timing was unavailable for every frame).</summary>
        public string Summary()
        {
            var text = new StringBuilder(220);
            text.Append("seconds=").Append(Seconds.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" frames=").Append(Frames)
                .Append(" fps=").Append((Seconds > 0 ? Frames / Seconds : 0).ToString("0.0", CultureInfo.InvariantCulture));
            Append(text, "frameMs", frame);
            Append(text, "mainMs", main);
            Append(text, "renderMs", render);
            Append(text, "gpuMs", gpu);
            text.Append(" slowFrames=").Append(SlowFrames);
            if (Frames > stored) text.Append(" sampled=").Append(stored);
            return text.ToString();
        }

        /// <summary>Nearest-rank percentile over the stored values of one series, skipping unavailable entries; NaN when none.</summary>
        public double Percentile(int series, double fraction)
        {
            var source = series switch { 0 => frame, 1 => main, 2 => render, 3 => gpu, _ => throw new ArgumentOutOfRangeException(nameof(series)) };
            int n = Sorted(source);
            if (n == 0) return double.NaN;
            int rank = (int)Math.Ceiling(Math.Max(0, Math.Min(1, fraction)) * n) - 1;
            return scratch[Math.Max(0, Math.Min(n - 1, rank))];
        }

        private void Append(StringBuilder text, string name, double[] source)
        {
            int n = Sorted(source);
            text.Append(' ').Append(name).Append('=');
            if (n == 0) { text.Append("na"); return; }
            double max = source == frame ? Math.Max(frameMax, scratch[n - 1]) : scratch[n - 1];
            text.Append(Ms(scratch[Rank(n, .5)])).Append('/').Append(Ms(scratch[Rank(n, .95)])).Append('/').Append(Ms(max));
        }

        private int Sorted(double[] source)
        {
            int n = 0;
            for (int i = 0; i < stored; i++) if (!double.IsNaN(source[i])) scratch[n++] = source[i];
            Array.Sort(scratch, 0, n);
            return n;
        }

        private static int Rank(int n, double fraction) => Math.Max(0, Math.Min(n - 1, (int)Math.Ceiling(fraction * n) - 1));
        private static double Valid(double ms) => ms >= 0 && !double.IsInfinity(ms) ? ms : double.NaN;
        private static string Ms(double ms) => ms.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
