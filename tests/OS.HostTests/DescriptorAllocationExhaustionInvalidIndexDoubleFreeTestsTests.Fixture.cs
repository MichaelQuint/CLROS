using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class DescriptorAllocationExhaustionInvalidIndexDoubleFreeTestsTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly VirtqueueDescriptorPoolState* Pool = (VirtqueueDescriptorPoolState*)NativeMemory.AllocZeroed((nuint)sizeof(VirtqueueDescriptorPoolState));
        internal readonly VirtqueueDescriptorSlot* Slots;
        internal readonly uint Count;
        internal Fixture(uint count, ulong epoch, bool initialize = true)
        {
            Count = count;
            Slots = (VirtqueueDescriptorSlot*)NativeMemory.AllocZeroed(count, (nuint)sizeof(VirtqueueDescriptorSlot));
            if (initialize)
                HostCheck.Equal(VirtqueueDescriptorStatus.Success, VirtqueueDescriptors.TryInitialize(Pool, Slots, count, epoch));
        }

        public void Dispose()
        {
            NativeMemory.Free(Slots);
            NativeMemory.Free(Pool);
        }
    }
}
