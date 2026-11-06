using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;

internal static unsafe partial class PageAlignedDMABuffersLifetimeOwnershipTrackingTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        internal readonly DmaBufferSlot* Slots;
        internal readonly DmaAddressWindow Window;
        internal Fixture(uint pages, ulong epoch, bool initialize = true)
        {
            Slots = (DmaBufferSlot*)NativeMemory.AllocZeroed(pages, (nuint)sizeof(DmaBufferSlot));
            Window = PageAlignedDMABuffersLifetimeOwnershipTrackingTests.Window(pages);
            if (initialize)
                HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, Window, Slots, pages, epoch));
        }

        public void Dispose()
        {
            NativeMemory.Free(Slots);
            NativeMemory.Free(Pool);
        }
    }
}
