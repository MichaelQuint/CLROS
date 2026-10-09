using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;

internal static unsafe class PageAlignedDMABuffersLifetimeOwnershipTrackingTests
{
    [HostTest("dma.pool exact capacity preserves addresses and rejects stale reuse", HostLane.Fast, 0)]
    private static bool CapacityAndReuse()
    {
        using var f = new Fixture(64, 1);
        var original = Allocate(f.Pool, 64);
        HostCheck.Equal(ulong.MaxValue, f.Pool->AllocatedMask);
        HostCheck.True(original.Range.CpuVirtualAddress == 0xffff800000100000 && original.Range.PhysicalAddress == 0x200000 &&
            original.Range.DeviceAddress == 0x300000 && original.Range.ByteLength == 64 * 4096 && original.Range.Offset == 0);
        HostCheck.Equal(DmaBufferStatus.Exhausted, PageAlignedDmaBuffers.TryAllocate(f.Pool, 1, out var failed));
        Empty(failed);
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(f.Pool, original.Token));
        HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        var reused = Allocate(f.Pool, 1);
        HostCheck.Equal(DmaBufferStatus.StaleToken, PageAlignedDmaBuffers.TryRelease(f.Pool, original.Token));
        HostCheck.Equal(1UL, f.Pool->AllocatedMask);
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(f.Pool, reused.Token));
        HostCheck.Equal(DmaBufferStatus.StaleToken, PageAlignedDmaBuffers.TryRelease(f.Pool, reused.Token));
        HostCheck.Equal(DmaBufferStatus.AlreadyInitialized, PageAlignedDmaBuffers.TryInitialize(f.Pool, f.Window, f.Slots, 64, 2));
        foreach (uint pages in new uint[] { 0, 65, uint.MaxValue })
        {
            HostCheck.Equal(DmaBufferStatus.InvalidPageCount, PageAlignedDmaBuffers.TryAllocate(f.Pool, pages, out failed));
            Empty(failed);
        }
        return true;
    }

    [HostTest("dma.pool ownership and fragmented contiguous runs are checked", HostLane.Fast, 1)]
    private static bool OwnershipAndFragmentation()
    {
        using var f = new Fixture(4, 3);
        var a = Allocate(f.Pool, 1); var b = Allocate(f.Pool, 1);
        var c = Allocate(f.Pool, 1); var d = Allocate(f.Pool, 1);
        HostCheck.True(b.Range.Offset == 4096 && b.Range.CpuVirtualAddress == 0xffff800000101000 &&
            b.Range.PhysicalAddress == 0x201000 && b.Range.DeviceAddress == 0x301000);
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryGiveToDevice(f.Pool, a.Token));
        HostCheck.Equal(DmaBufferStatus.InvalidOwnership, PageAlignedDmaBuffers.TryGiveToDevice(f.Pool, a.Token));
        HostCheck.Equal(DmaBufferStatus.InvalidOwnership, PageAlignedDmaBuffers.TryRelease(f.Pool, a.Token));
        HostCheck.Equal(15UL, f.Pool->AllocatedMask);
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryReturnToCpu(f.Pool, a.Token));
        HostCheck.Equal(DmaBufferStatus.InvalidOwnership, PageAlignedDmaBuffers.TryReturnToCpu(f.Pool, a.Token));
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(f.Pool, a.Token));
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(f.Pool, c.Token));
        HostCheck.Equal(DmaBufferStatus.Exhausted, PageAlignedDmaBuffers.TryAllocate(f.Pool, 2, out var failed));
        Empty(failed);
        HostCheck.Equal(10UL, f.Pool->AllocatedMask);
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(f.Pool, b.Token));
        var run = Allocate(f.Pool, 3);
        HostCheck.True(run.Range.Offset == 0 && run.Range.ByteLength == 12288 && d.Range.Offset == 12288);
        return true;
    }

    [HostTest("dma.pool rejects wrong owners defaults and generation wrap", HostLane.Fast, 2)]
    private static bool TokenBoundaries()
    {
        using var first = new Fixture(1, 4);
        using var second = new Fixture(1, 5);
        var allocation = Allocate(first.Pool, 1);
        HostCheck.Equal(DmaBufferStatus.InvalidToken, PageAlignedDmaBuffers.TryRelease(second.Pool, allocation.Token));
        HostCheck.Equal(DmaBufferStatus.InvalidToken, PageAlignedDmaBuffers.TryGiveToDevice(first.Pool, default));
        DmaBufferPool copy = *first.Pool, empty = default;
        HostCheck.Equal(DmaBufferStatus.InvalidPool, PageAlignedDmaBuffers.TryRelease(&copy, allocation.Token));
        HostCheck.Equal(DmaBufferStatus.InvalidPool, PageAlignedDmaBuffers.TryAllocate(&empty, 1, out var rejected));
        Empty(rejected);
        HostCheck.Equal(DmaBufferStatus.InvalidPool, PageAlignedDmaBuffers.TryAllocate(null, 1, out rejected));
        Empty(rejected);
        HostCheck.True(first.Pool->AllocatedMask == 1 && second.Pool->AllocatedMask == 0);

        // Host-only boundary setup avoids an impractical 2^64 allocation loop.
        int generationOffset = (int)Marshal.OffsetOf<DmaBufferSlot>("Generation");
        *(ulong*)((byte*)second.Slots + generationOffset) = ulong.MaxValue - 1;
        var terminal = Allocate(second.Pool, 1);
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(second.Pool, terminal.Token));
        HostCheck.Equal(DmaBufferStatus.GenerationExhausted, PageAlignedDmaBuffers.TryAllocate(second.Pool, 1, out rejected));
        Empty(rejected);
        HostCheck.Equal(0UL, second.Pool->AllocatedMask);
        HostCheck.Equal(DmaBufferStatus.StaleToken, PageAlignedDmaBuffers.TryGiveToDevice(second.Pool, terminal.Token));
        return true;
    }

    [HostTest("dma.pool initialization excludes invalid and overlapping metadata", HostLane.Fast, 3)]
    private static bool Initialization()
    {
        using var f = new Fixture(2, 6, false);
        HostCheck.Equal(DmaBufferStatus.InvalidStorage, PageAlignedDmaBuffers.TryInitialize(null, f.Window, f.Slots, 2, 6));
        HostCheck.Equal(DmaBufferStatus.InvalidEpoch, PageAlignedDmaBuffers.TryInitialize(f.Pool, f.Window, f.Slots, 2, 0));
        HostCheck.Equal(DmaBufferStatus.InvalidWindow, PageAlignedDmaBuffers.TryInitialize(f.Pool, default, f.Slots, 2, 6));
        HostCheck.Equal(DmaBufferStatus.InvalidWindow, PageAlignedDmaBuffers.TryInitialize(f.Pool, Window(65), f.Slots, 65, 6));
        HostCheck.Equal(DmaBufferStatus.InvalidWindow, PageAlignedDmaBuffers.TryInitialize(f.Pool, f.Window, f.Slots, 1, 6));
        foreach (nint address in new nint[] { 0, (nint)((byte*)f.Slots + 1), (nint)f.Pool, unchecked((nint)(ulong.MaxValue - 7)) })
            HostCheck.Equal(DmaBufferStatus.InvalidStorage, PageAlignedDmaBuffers.TryInitialize(f.Pool, f.Window, (DmaBufferSlot*)address, 2, 6));
        foreach (ulong address in new[] { (ulong)f.Pool, (ulong)f.Slots })
        {
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate(address & ~4095UL, 0x200000, 0x300000, 4096, 32, out var overlap));
            HostCheck.Equal(DmaBufferStatus.InvalidStorage, PageAlignedDmaBuffers.TryInitialize(f.Pool, overlap, f.Slots, 1, 6));
        }
        *(byte*)f.Slots = 1;
        HostCheck.Equal(DmaBufferStatus.MetadataNotEmpty, PageAlignedDmaBuffers.TryInitialize(f.Pool, f.Window, f.Slots, 2, 6));
        HostCheck.True(!f.Pool->IsInitialized && f.Pool->AllocatedMask == 0 && *(byte*)f.Slots == 1);
        *(byte*)f.Slots = 0;
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(f.Pool, f.Window, f.Slots, 2, 6));
        return true;
    }

    [HostTest("dma.pool allocation and release do not touch backing bytes", HostLane.Fast, 4)]
    private static bool BackingUnchanged()
    {
        byte* backing = (byte*)NativeMemory.AlignedAlloc(8192, 4096);
        try
        {
            new Span<byte>(backing, 8192).Fill(0xa7);
            using var f = new Fixture(2, 7, false);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)backing, 0x400000, 0x500000, 8192, 32, out var window));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(f.Pool, window, f.Slots, 2, 7));
            var allocation = Allocate(f.Pool, 2);
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(f.Pool, allocation.Token));
            return HostCheck.Result(new ReadOnlySpan<byte>(backing, 8192).IndexOfAnyExcept((byte)0xa7) < 0);
        }
        finally { NativeMemory.AlignedFree(backing); }
    }

    private static DmaBufferAllocation Allocate(DmaBufferPool* pool, uint pages)
    {
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryAllocate(pool, pages, out var allocation));
        HostCheck.True(allocation.IsInitialized);
        return allocation;
    }
    private static void Empty(DmaBufferAllocation allocation) => HostCheck.True(!allocation.IsInitialized &&
        !allocation.Token.IsInitialized && !allocation.Range.IsInitialized && allocation.Range.ByteLength == 0);
    private static DmaAddressWindow Window(uint pages)
    {
        HostCheck.Equal(DmaAddressStatus.Success,
            DmaAddressWindow.TryCreate(0xffff800000100000, 0x200000, 0x300000, (ulong)pages * 4096, 32, out var window));
        return window;
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        internal readonly DmaBufferSlot* Slots;
        internal readonly DmaAddressWindow Window;
        internal Fixture(uint pages, ulong epoch, bool initialize = true)
        {
            Slots = (DmaBufferSlot*)NativeMemory.AllocZeroed(pages, (nuint)sizeof(DmaBufferSlot));
            Window = PageAlignedDMABuffersLifetimeOwnershipTrackingTests.Window(pages);
            if (initialize) HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, Window, Slots, pages, epoch));
        }
        public void Dispose() { NativeMemory.Free(Slots); NativeMemory.Free(Pool); }
    }
}
