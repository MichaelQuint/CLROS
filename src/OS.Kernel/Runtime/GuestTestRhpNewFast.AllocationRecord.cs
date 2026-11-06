using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;

public static unsafe partial class GuestTestRhpNewFast
{
    private struct AllocationRecord
    {
        public ulong Offset;
        public uint Bytes;
        public uint StaticBase;
        public ulong MethodTable;
        public ulong Sequence;
    }
}
