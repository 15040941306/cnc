using System;

namespace Rts.Core.Math
{
    /// <summary>
    /// The only random source the simulation may use. SplitMix64; the whole state is one ulong,
    /// so saving and restoring <see cref="State"/> resumes the exact sequence.
    /// </summary>
    public sealed class SimRandom
    {
        public ulong State { get; private set; }

        public SimRandom(ulong state)
        {
            State = state;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                State += 0x9E3779B97F4A7C15UL;
                ulong z = State;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Returns a value in [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(((NextUInt64() >> 32) * (ulong)maxExclusive) >> 32);
        }

        /// <summary>Returns a value in [min, maxExclusive).</summary>
        public int Next(int min, int maxExclusive)
        {
            if (maxExclusive <= min)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            long span = (long)maxExclusive - min;
            return (int)(min + (long)(((NextUInt64() >> 32) * (ulong)span) >> 32));
        }
    }
}
