#nullable enable

namespace Shooter.Simulation.AI
{
    public enum EngageRange : byte { Close = 0, Mid = 1, Long = 2 }

    /// <summary>
    /// T082 trigger discipline: sustained fire up close, 3–6 round bursts at mid range and taps at long
    /// range, with seeded pauses that let the authoritative recoil recover. A burst only starts when the
    /// aim is aligned; during a burst a looser alignment keeps the trigger down.
    /// </summary>
    public static class FireDiscipline
    {
        public static EngageRange Classify(float distance) =>
            distance < AiTuning.CloseRange ? EngageRange.Close : distance < AiTuning.LongRange ? EngageRange.Mid : EngageRange.Long;

        public static bool InBurst(AiBlackboard board, uint tick) => tick < board.BurstEndTick;

        public static bool InPause(AiBlackboard board, uint tick) => tick >= board.BurstEndTick && tick < board.BurstPauseUntilTick;

        /// <summary>Stop any running burst (target lost, reload, sprint). The pause timer is kept.</summary>
        public static void Cease(AiBlackboard board, uint tick)
        {
            if (tick < board.BurstEndTick) board.BurstEndTick = tick;
        }

        /// <summary>
        /// Returns true when the trigger is down this tick. <paramref name="alignedTight"/> gates starting
        /// a burst; <paramref name="alignedLoose"/> keeps a burst going.
        /// </summary>
        public static bool Update(AiBlackboard board, uint tick, float distance, bool alignedTight, bool alignedLoose)
        {
            if (InBurst(board, tick)) return alignedLoose;
            if (InPause(board, tick) || !alignedTight) return false;

            ulong self = board.Self.Value;
            ulong n = (ulong)board.BurstIndex++;
            uint length, pause;
            switch (Classify(distance))
            {
                case EngageRange.Close:
                    length = AiTuning.CloseBurstMinTicks + AiNoise.Range(self, n, AiNoise.SaltBurst, AiTuning.CloseBurstSpreadTicks);
                    pause = AiTuning.ClosePauseMinTicks + AiNoise.Range(self, n, AiNoise.SaltPause, AiTuning.ClosePauseSpreadTicks);
                    break;
                case EngageRange.Mid:
                    length = (AiTuning.MidBurstMinRounds + AiNoise.Range(self, n, AiNoise.SaltBurst, AiTuning.MidBurstSpreadRounds)) * AiTuning.WeaponIntervalTicks;
                    pause = AiTuning.MidPauseMinTicks + AiNoise.Range(self, n, AiNoise.SaltPause, AiTuning.MidPauseSpreadTicks);
                    break;
                default:
                    length = (AiTuning.LongBurstMinRounds + AiNoise.Range(self, n, AiNoise.SaltBurst, AiTuning.LongBurstSpreadRounds)) * AiTuning.WeaponIntervalTicks;
                    pause = AiTuning.LongPauseMinTicks + AiNoise.Range(self, n, AiNoise.SaltPause, AiTuning.LongPauseSpreadTicks);
                    break;
            }
            board.BurstStartTick = tick;
            board.BurstEndTick = tick + length;
            board.BurstPauseUntilTick = board.BurstEndTick + pause;
            return true;
        }
    }
}
