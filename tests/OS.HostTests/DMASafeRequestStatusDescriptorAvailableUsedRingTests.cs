using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;

internal static unsafe class DMASafeRequestStatusDescriptorAvailableUsedRingTests
{
    [HostTest("virtio.block.dma explicit four-sector data fits unchanged page and legacy wire offsets")]
    private static void BoundedReadArea()
    {
        HostCheck.Equal(512U, VirtioBlkDmaLayout.DataBytes);
        HostCheck.Equal(4U, VirtioBlkDmaLayout.MaximumReadSectors);
        HostCheck.Equal(2048U, VirtioBlkDmaLayout.MaximumReadBytes);
        HostCheck.Equal(4096U, VirtioBlkDmaLayout.PageBytes);
        HostCheck.True(VirtioBlkDmaLayout.DataOffset + VirtioBlkDmaLayout.MaximumReadBytes <= VirtioBlkDmaLayout.PageBytes);
        HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate(0x100000, 0x200000, 0x300000, 4096, 32, out var window));
        HostCheck.Equal(DmaAddressStatus.Success, window.TrySlice(0, 4096, out var range));
        HostCheck.Equal(VirtioBlkDmaStatus.Success, DMASafeRequestStatusDescriptorAvailableUsedRing.TryLayout(range, out var layout));
        HostCheck.Equal(216U, layout.Rings.EndOffset);
        HostCheck.Equal(256U, VirtioBlkDmaLayout.RequestOffset);
        HostCheck.Equal(272U, VirtioBlkDmaLayout.StatusOffset);
        HostCheck.Equal(512U, VirtioBlkDmaLayout.DataOffset);
        HostCheck.Equal(4096UL, layout.Range.ByteLength);
    }

    [HostTest("virtio.block.dma preserves three address domains and independent size-eight wire layout")]
    private static void Layout()
    {
        HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate(0x100000, 0x200000, 0x123400000000,
            4096, 64, out var window));
        HostCheck.Equal(DmaAddressStatus.Success, window.TrySlice(0, 4096, out var range));
        HostCheck.Equal(VirtioBlkDmaStatus.Success,
            DMASafeRequestStatusDescriptorAvailableUsedRing.TryLayout(range, out var layout));
        HostCheck.True(layout.IsInitialized);
        HostCheck.Equal(0x100000UL, layout.Range.CpuVirtualAddress);
        HostCheck.Equal(0x200000UL, layout.Range.PhysicalAddress);
        HostCheck.Equal(0x123400000000UL, layout.Range.DeviceAddress);
        HostCheck.Equal((ushort)8, layout.Rings.QueueSize);
        HostCheck.Equal(0U, layout.Rings.DescriptorTable.Offset);
        HostCheck.Equal(128U, layout.Rings.DescriptorTable.Length);
        HostCheck.Equal(128U, layout.Rings.AvailableRing.Offset);
        HostCheck.Equal(20U, layout.Rings.AvailableRing.Length);
        HostCheck.Equal(148U, layout.Rings.UsedRing.Offset);
        HostCheck.Equal(68U, layout.Rings.UsedRing.Length);
        HostCheck.Equal(216U, layout.Rings.EndOffset);
        HostCheck.Equal(256U, VirtioBlkDmaLayout.RequestOffset);
        HostCheck.Equal(16U, VirtioBlkDmaLayout.RequestBytes);
        HostCheck.Equal(272U, VirtioBlkDmaLayout.StatusOffset);
        HostCheck.Equal(1U, VirtioBlkDmaLayout.StatusBytes);
        HostCheck.Equal(512U, VirtioBlkDmaLayout.DataOffset);
        HostCheck.Equal(512U, VirtioBlkDmaLayout.DataBytes);
        HostCheck.Equal(VirtioBlkDmaStatus.InvalidRange,
            DMASafeRequestStatusDescriptorAvailableUsedRing.TryLayout(default, out layout));
        HostCheck.True(!layout.IsInitialized);
        foreach (ulong address in new ulong[] { 0x800000000000, 0xffffffffffffe000 })
        {
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate(address, 0x200000, 0x300000, 4096, 32, out window));
            HostCheck.Equal(DmaAddressStatus.Success, window.TrySlice(0, 4096, out range));
            HostCheck.Equal(VirtioBlkDmaStatus.InvalidRange,
                DMASafeRequestStatusDescriptorAvailableUsedRing.TryLayout(range, out layout));
            HostCheck.True(!layout.IsInitialized);
        }
        HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate(0x100000, 0x200000, 0x300000, 8192, 32, out window));
        HostCheck.Equal(DmaAddressStatus.Success, window.TrySlice(0, 8192, out range));
        HostCheck.Equal(VirtioBlkDmaStatus.InvalidRange,
            DMASafeRequestStatusDescriptorAvailableUsedRing.TryLayout(range, out layout));
    }

    [HostTest("virtio.block.dma clears exact owned page keeps indices empty and rejects exhausted stale ownership")]
    private static void StorageLifetime()
    {
        byte* bytes = (byte*)NativeMemory.AlignedAlloc(8192, 4096);
        try
        {
            new Span<byte>(bytes, 8192).Fill(0xa5);
            HostCheck.Equal(DmaAddressStatus.Success,
                DmaAddressWindow.TryCreate((ulong)bytes, 0x220000, 0x123400000000, 4096, 64, out var window));
            DmaBufferPool pool = default;
            DmaBufferSlot slot = default;
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(&pool, window, &slot, 1, 1));
            HostCheck.Equal(VirtioBlkDmaStatus.Success,
                DMASafeRequestStatusDescriptorAvailableUsedRing.TryAllocate(&pool, out var allocation, out var layout));
            HostCheck.True(allocation.IsInitialized && layout.IsInitialized);
            DMASafeRequestStatusDescriptorAvailableUsedRing.Initialize(layout);
            for (int i = 0; i < 8192; i++)
                HostCheck.Equal(i >= 4096 ? (byte)0xa5 : i == 128 ? (byte)1 : i == 272 ? (byte)0xff : (byte)0, bytes[i]);
            HostCheck.Equal(VirtioBlkDmaStatus.AllocationFailed,
                DMASafeRequestStatusDescriptorAvailableUsedRing.TryAllocate(&pool, out var extra, out var empty));
            HostCheck.True(!extra.IsInitialized && !empty.IsInitialized);
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryGiveToDevice(&pool, allocation.Token));
            HostCheck.Equal(DmaBufferStatus.InvalidOwnership, PageAlignedDmaBuffers.TryRelease(&pool, allocation.Token));
            HostCheck.Equal(DmaBufferStatus.InvalidOwnership, PageAlignedDmaBuffers.TryGiveToDevice(&pool, allocation.Token));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryReturnToCpu(&pool, allocation.Token));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(&pool, allocation.Token));
            HostCheck.Equal(DmaBufferStatus.StaleToken, PageAlignedDmaBuffers.TryRelease(&pool, allocation.Token));
            HostCheck.Equal(VirtioBlkDmaStatus.Success,
                DMASafeRequestStatusDescriptorAvailableUsedRing.TryAllocate(&pool, out extra, out empty));
            HostCheck.Equal(DmaBufferStatus.StaleToken, PageAlignedDmaBuffers.TryGiveToDevice(&pool, allocation.Token));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(&pool, extra.Token));
            HostCheck.Equal(0UL, pool.AllocatedMask);
        }
        finally { NativeMemory.AlignedFree(bytes); }
        HostCheck.Equal(VirtioBlkDmaStatus.AllocationFailed,
            DMASafeRequestStatusDescriptorAvailableUsedRing.TryAllocate(null, out var none, out var noLayout));
        HostCheck.True(!none.IsInitialized && !noLayout.IsInitialized);
    }
}
