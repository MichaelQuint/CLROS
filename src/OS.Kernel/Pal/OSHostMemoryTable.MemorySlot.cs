using System.Runtime.InteropServices;

namespace OS.Pal;

public sealed partial class OSHostMemoryTable
{
    private struct MemorySlot
    {
        public bool InUse;
        public uint Generation;
        public OSHostHandle Allocation;
        public OSHostMemoryRange Range;
        public OSHostPageProtection Protection;
        public ulong EndAddress => Range.BaseAddress + Range.ByteLength;

        public static MemorySlot Create(OSHostHandle allocation, uint generation, OSHostMemoryRange range, OSHostPageProtection protection)
        {
            return new MemorySlot
            {
                InUse = true,
                Generation = generation,
                Allocation = allocation,
                Range = range,
                Protection = protection
            };
        }
    }
}
