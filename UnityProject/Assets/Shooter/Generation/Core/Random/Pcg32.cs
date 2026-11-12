#nullable enable
using System;

namespace Shooter.Generation
{
    /// <summary>PCG-XSH-RR 64/32 set-sequence. Each generator belongs to one request/feature.</summary>
    public sealed class Pcg32
    {
        private ulong state;
        private readonly ulong increment;

        public Pcg32(ulong initState, ulong initSequence)
        {
            unchecked
            {
                state = 0;
                increment = (initSequence << 1) | 1;
                NextUInt();
                state += initState;
                NextUInt();
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                ulong old = state;
                state = old * 6364136223846793005UL + increment;
                uint x = (uint)(((old >> 18) ^ old) >> 27);
                int rotation = (int)(old >> 59);
                return (x >> rotation) | (x << ((-rotation) & 31));
            }
        }

        public uint NextBounded(uint bound)
        {
            if (bound == 0) throw new ArgumentOutOfRangeException(nameof(bound));
            uint threshold = unchecked(0U - bound) % bound;
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold) return value % bound;
            }
        }

        public float NextUnitFloat() => (NextUInt() >> 8) * (1.0f / 16777216.0f);
    }
}
