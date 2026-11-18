#nullable enable

namespace Shooter.Game.Session
{
    /// <summary>
    /// ADR-108 cosmetic bot ranks for the scoreboards. Bots have no career; each one shows a rank drawn from its own
    /// SplitMix64 stream (match seed × participant), within <see cref="Spread"/> of the player's rank and clamped to the
    /// ladder. Nothing in authority reads it, so gameplay draws and replays are unchanged.
    /// </summary>
    public static class BotRanks
    {
        public const int MinRank = 1;
        public const int MaxRank = 20;
        public const int Spread = 3;
        private const ulong StreamSalt = 0xC0A7_5EED_0108_0001UL;

        public static int Resolve(uint participant, ulong seed, int playerRank)
        {
            int center = Clamp(playerRank);
            ulong roll = Mix(seed ^ StreamSalt ^ Mix(participant + 0x9E3779B97F4A7C15UL));
            int offset = (int)(roll % (2 * Spread + 1)) - Spread;
            return Clamp(center + offset);
        }

        public static int Clamp(int rank) => rank < MinRank ? MinRank : rank > MaxRank ? MaxRank : rank;

        private static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
