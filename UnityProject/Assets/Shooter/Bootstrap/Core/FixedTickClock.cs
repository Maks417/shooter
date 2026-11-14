using System;

namespace Shooter.Bootstrap
{
    /// <summary>Caller supplies monotonic elapsed seconds. Rendering never supplies simulation delta time.</summary>
    public sealed class FixedTickClock
    {
        public const double TickSeconds = 1.0 / 60;
        public uint NextTick { get; private set; }
        public double DebtSeconds { get; private set; }
        public bool Overloaded { get; private set; }
        public int LastAdvanceTicks { get; private set; }
        public bool Suspended { get; private set; }
        private readonly bool server;
        private double excessiveDebtDuration;
        public FixedTickClock(bool server) { this.server = server; }
        /// <summary>
        /// Suspended wall time is deliberately discarded. Existing fractional debt and the next
        /// tick identity are retained so resume cannot reorder or catch up simulation ticks.
        /// </summary>
        public void SetSuspended(bool suspended)
        {
            Suspended = suspended;
            LastAdvanceTicks = 0;
            if (suspended)
            {
                Overloaded = false;
                excessiveDebtDuration = 0;
            }
        }
        public void Advance(double elapsedSeconds, Action<uint> step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            LastAdvanceTicks = 0;
            if (Suspended) return;
            if (Overloaded && !server) return;
            DebtSeconds += elapsedSeconds;
            while (DebtSeconds + 1e-12 >= TickSeconds && LastAdvanceTicks < 4)
            {
                step(NextTick); // A failed step leaves its tick/debt unconsumed.
                NextTick = unchecked(NextTick + 1);
                DebtSeconds = Math.Max(0, DebtSeconds - TickSeconds);
                LastAdvanceTicks++;
            }
            Overloaded = DebtSeconds + 1e-12 >= TickSeconds;
            if (server)
            {
                excessiveDebtDuration = DebtSeconds > 1 ? excessiveDebtDuration + elapsedSeconds : 0;
                if (excessiveDebtDuration >= 1) throw new InvalidOperationException("Server sustained more than one second of tick debt; session invalid");
            }
        }
    }
    /// <summary>Test doubles implement this marker; production composition rejects required marked ports.</summary>
    public interface IFakeAdapter { }
    public static class CompositionPolicy
    {
        public static void RequireReal(params object[] requiredAdapters)
        {
            foreach (object adapter in requiredAdapters)
            {
                if (adapter == null) throw new InvalidOperationException("Required adapter is missing");
                if (adapter is IFakeAdapter) throw new InvalidOperationException("Shipping composition rejects a required fake adapter");
            }
        }
    }
}
