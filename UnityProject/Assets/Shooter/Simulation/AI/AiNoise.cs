#nullable enable

namespace Shooter.Simulation.AI
{
    /// <summary>
    /// Deterministic per-bot variation (SplitMix64 finaliser). Replaces RNG so identical command and
    /// collision recordings give identical AI decisions on every quality profile (ADR-004, ai.md).
    /// </summary>
    public static class AiNoise
    {
        public const ulong SaltReaction = 0x52454143UL;
        public const ulong SaltErrorAngle = 0x45525241UL;
        public const ulong SaltPersonality = 0x50455253UL;
        public const ulong SaltBurst = 0x42555253UL;
        public const ulong SaltPause = 0x50415553UL;
        public const ulong SaltStrafe = 0x53545246UL;
        public const ulong SaltHearingX = 0x48454158UL;
        public const ulong SaltHearingZ = 0x4845415AUL;

        public static ulong Mix(ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }

        public static ulong Hash(ulong a, ulong b, ulong salt) => Mix(a ^ Mix(b ^ Mix(salt)));

        /// <summary>Uniform in [0, 1).</summary>
        public static float Unit(ulong a, ulong b, ulong salt) =>
            (float)((Hash(a, b, salt) >> 40) / (double)(1UL << 24));

        /// <summary>Uniform in [-1, 1).</summary>
        public static float Signed(ulong a, ulong b, ulong salt) => Unit(a, b, salt) * 2f - 1f;

        /// <summary>Uniform integer in [0, spread).</summary>
        public static uint Range(ulong a, ulong b, ulong salt, uint spread) =>
            spread == 0 ? 0u : (uint)(Hash(a, b, salt) % spread);
    }
}
