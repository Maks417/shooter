#nullable enable
using System;
using Shooter.Game.Contracts;

namespace Shooter.Game.Session
{
    /// <summary>
    /// Deterministic short first-name labels for offline TDM bots.
    /// Human participants stay "Player"; bots draw unique names from a fixed pool shuffled by match seed.
    /// </summary>
    public static class BotDisplayNames
    {
        /// <summary>Fixed ASCII first-name pool (≥24). Order is the shuffle base, not match assignment.</summary>
        public static readonly string[] Pool =
        {
            "Alex", "Marco", "Nadia", "Sam", "Elena", "Omar", "Priya", "Luca",
            "Nora", "Kai", "Mira", "Jonas", "Aya", "Reed", "Sofia", "Malik",
            "Ines", "Theo", "Yuki", "Hugo", "Lara", "Devon", "Anika", "Cole"
        };

        /// <summary>
        /// Returns the display name for bot ordinal <paramref name="botIndex"/> in a match with
        /// <paramref name="botCount"/> bots. Same seed and botCount always yield the same ordered set;
        /// names within one resolve pass are unique.
        /// </summary>
        public static string Resolve(int botIndex, int botCount, ulong seed)
        {
            if (botCount < 1 || botCount > Pool.Length)
                throw new ArgumentOutOfRangeException(nameof(botCount));
            if (botIndex < 0 || botIndex >= botCount)
                throw new ArgumentOutOfRangeException(nameof(botIndex));

            var order = new int[Pool.Length];
            for (int i = 0; i < Pool.Length; i++) order[i] = i;
            Shuffle(order, MixSeed(seed, (uint)botCount));

            string name = Pool[order[botIndex]];
            if (name.Length < 1 || name.Length > GameContractVersion.MaxDisplayName)
                throw new InvalidOperationException("bot display name out of bounds");
            return name;
        }

        private static ulong MixSeed(ulong seed, uint botCount)
        {
            ulong x = seed ^ 0x9E3779B97F4A7C15UL;
            x ^= (ulong)botCount * 0xBF58476D1CE4E5B9UL;
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return x == 0 ? 0xA5A5A5A5A5A5A5A5UL : x;
        }

        private static void Shuffle(int[] order, ulong state)
        {
            for (int i = order.Length - 1; i > 0; i--)
            {
                state = Next(state);
                int j = (int)(state % (uint)(i + 1));
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }
        }

        private static ulong Next(ulong state)
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            return state == 0 ? 1UL : state;
        }
    }
}
