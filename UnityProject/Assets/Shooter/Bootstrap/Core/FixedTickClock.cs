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
        /// <summary>Unserviced whole-tick debt discarded by the last Advance (product hitch recovery only), else 0.</summary>
        public double LastDroppedDebtSeconds { get; private set; }
        public int HitchRecoveries { get; private set; }
        private readonly bool server;
        private readonly bool dropHitchDebt;
        private bool rebasePending;
        private double excessiveDebtDuration;
        public FixedTickClock(bool server) : this(server, false) { }
        /// <summary>
        /// dropHitchDebt (offline product client only, ADR-046): a frame needing more than four ticks drops the
        /// unserviced whole ticks instead of latching Overloaded. Diagnostic/benchmark clocks and the server keep
        /// the retained-debt behaviour.
        /// </summary>
        public FixedTickClock(bool server, bool dropHitchDebt)
        {
            this.server = server;
            this.dropHitchDebt = dropHitchDebt && !server;
        }
        /// <summary>The interval crossing a suspended/loading boundary is not active play time.</summary>
        public double SampleActiveElapsed(double elapsedSeconds, bool running)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            SetSuspended(!running);
            if (running && rebasePending) { rebasePending = false; return 0; }
            return running ? elapsedSeconds : 0;
        }
        /// <summary>
        /// Suspended wall time is deliberately discarded. Existing fractional debt and the next
        /// tick identity are retained so resume cannot reorder or catch up simulation ticks.
        /// </summary>
        public void SetSuspended(bool suspended)
        {
            if (!suspended && Suspended) rebasePending = true; // first sample after resume is zero
            Suspended = suspended;
            LastAdvanceTicks = 0;
            LastDroppedDebtSeconds = 0;
            if (suspended)
            {
                rebasePending = false;
                Overloaded = false;
                excessiveDebtDuration = 0;
            }
        }
        public void Advance(double elapsedSeconds, Action<uint> step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            LastAdvanceTicks = 0;
            LastDroppedDebtSeconds = 0;
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
            if (Overloaded && dropHitchDebt)
            {
                double remainder = DebtSeconds % TickSeconds;
                LastDroppedDebtSeconds = DebtSeconds - remainder;
                DebtSeconds = remainder;
                Overloaded = false;
                HitchRecoveries++;
            }
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
