using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Boot;
using OS.Kernel.Memory;

internal static partial class BootPageAllocatorStressTests
{
    private struct DeterministicRandom
    {
        private ulong _state;
        public DeterministicRandom(ulong seed)
        {
            _state = seed == 0 ? 0xD1B5_4A32_D192_ED03 : seed;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                ulong value = _state;
                value ^= value >> 12;
                value ^= value << 25;
                value ^= value >> 27;
                _state = value;
                return value * 0x2545_F491_4F6C_DD1D;
            }
        }

        public int NextBounded(int exclusiveUpperBound, out ulong word)
        {
            if (exclusiveUpperBound <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
            }

            ulong bound = (ulong)exclusiveUpperBound;
            ulong limit = ulong.MaxValue - (ulong.MaxValue % bound);
            do
            {
                word = NextUInt64();
            }
            while (word >= limit);
            return (int)(word % bound);
        }
    }
}
