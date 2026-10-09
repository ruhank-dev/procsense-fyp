using System;
using System.Text;

namespace ProcSense.Core.Random
{
    /// <summary>Seeded xoshiro256** generator, identical on every runtime. Use Stream(name) per subsystem so one subsystem's draws never shift another's.</summary>
    public sealed class SimRandom
    {
        private readonly ulong _seed;
        private ulong _s0, _s1, _s2, _s3;

        public SimRandom(ulong seed)
        {
            _seed = seed;
            ulong sm = seed;
            _s0 = SplitMix64(ref sm);
            _s1 = SplitMix64(ref sm);
            _s2 = SplitMix64(ref sm);
            _s3 = SplitMix64(ref sm);
        }

        /// <summary>Independent generator for a named subsystem ("events", "physio", "iv"...).</summary>
        public SimRandom Stream(string name) => new SimRandom(_seed + Fnv1a(name));

        public ulong NextULong()
        {
            ulong result = Rotl(_s1 * 5, 7) * 9;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = Rotl(_s3, 45);
            return result;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextDouble() * maxExclusive);
        }

        private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        private static ulong SplitMix64(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // Own hash: string.GetHashCode() is randomised per process.
        private static ulong Fnv1a(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(s)) { h ^= b; h *= 1099511628211UL; }
            return h;
        }
    }
}
