using OS.Kernel.Platform;

internal static class ExplicitCPUVirtualPhysicalDeviceAddressDMAMappingsTests
{
    [HostTest("dma.address_window preserves distinct address domains and slice offsets", HostLane.Fast, 0)]
    private static bool DistinctDomains()
    {
        HostCheck.Equal(DmaAddressStatus.Success,
            DmaAddressWindow.TryCreate(0xffff800000100000, 0x100000000, 0x200000, 0x4000, 32, out var window));
        HostCheck.True(window.IsInitialized && window.ByteLength == 0x4000 && window.DeviceAddressWidth == 32);
        HostCheck.Equal(DmaAddressStatus.Success, window.TrySlice(0, 0x1000, out var first));
        HostCheck.True(first.IsInitialized && first.CpuVirtualAddress == 0xffff800000100000 &&
            first.PhysicalAddress == 0x100000000 && first.DeviceAddress == 0x200000 && first.Offset == 0 &&
            first.ByteLength == 0x1000 && first.DeviceAddressWidth == 32);
        HostCheck.Equal(DmaAddressStatus.Success, window.TrySlice(0x2000, 0x2000, out var tail));
        HostCheck.True(tail.IsInitialized && tail.CpuVirtualAddress == 0xffff800000102000 &&
            tail.PhysicalAddress == 0x100002000 && tail.DeviceAddress == 0x202000 && tail.Offset == 0x2000 &&
            tail.ByteLength == 0x2000 && tail.DeviceAddressWidth == 32);
        HostCheck.True(window.CpuVirtualBase == 0xffff800000100000 && window.PhysicalBase == 0x100000000 &&
            window.DeviceBase == 0x200000 && window.ByteLength == 0x4000);

        HostCheck.Equal(DmaAddressStatus.Success,
            DmaAddressWindow.TryCreate(0x100000, 0x100000, 0x100000, 0x2000, 32, out var identity));
        HostCheck.Equal(DmaAddressStatus.Success, identity.TrySlice(0, 0x2000, out var whole));
        return HostCheck.Result(whole.CpuVirtualAddress == 0x100000 && whole.PhysicalAddress == 0x100000 &&
            whole.DeviceAddress == 0x100000 && whole.ByteLength == 0x2000);
    }

    [HostTest("dma.address_window enforces explicit device width and end bounds", HostLane.Fast, 1)]
    private static bool DeviceWidths()
    {
        HostCheck.Equal(DmaAddressStatus.Success,
            DmaAddressWindow.TryCreate(0x400000, 0x100000000, 0xfffff000, 0x1000, 32, out var edge32));
        HostCheck.Equal(DmaAddressStatus.Success, edge32.TrySlice(0, 0x1000, out var range));
        HostCheck.Equal(0xfffff000UL, range.DeviceAddress);
        RejectCreate(0x400000, 0x100000000, 0xfffff000, 0x2000, 32, DmaAddressStatus.DeviceAddressLimit);
        RejectCreate(0x400000, 0x100000000, 0x100000000, 0x1000, 32, DmaAddressStatus.DeviceAddressLimit);
        HostCheck.Equal(DmaAddressStatus.Success,
            DmaAddressWindow.TryCreate(0x400000, 0x100000000, 0x100000000, 0x1000, 64, out var above32));
        HostCheck.True(above32.IsInitialized && above32.DeviceAddressWidth == 64);
        HostCheck.Equal(DmaAddressStatus.Success,
            DmaAddressWindow.TryCreate(0x400000, 0x100000000, 0xffffffffffffe000, 0x1000, 64, out var edge64));
        HostCheck.Equal(DmaAddressStatus.Success, edge64.TrySlice(0, 0x1000, out range));
        HostCheck.Equal(0xffffffffffffe000UL, range.DeviceAddress);
        RejectCreate(0x400000, 0x100000000, 0xfffffffffffff000, 0x1000, 64, DmaAddressStatus.AddressOverflow);
        foreach (uint width in new uint[] { 0, 65, uint.MaxValue })
            RejectCreate(0x1000, 0x2000, 0x3000, 0x1000, width, DmaAddressStatus.InvalidDeviceWidth);
        HostCheck.Equal(DmaAddressStatus.Success,
            DmaAddressWindow.TryCreate(0x1000, 0x2000, 0x1000, 0x1000, 13, out _));
        RejectCreate(0x1000, 0x2000, 0x1000, 0x1000, 12, DmaAddressStatus.DeviceAddressLimit);
        return true;
    }

    [HostTest("dma.address_window rejects zero alignment overflow and invalid slices with empty outputs", HostLane.Fast, 2)]
    private static bool InvalidInputs()
    {
        foreach (int field in new[] { 0, 1, 2 })
        {
            ulong[] bases = [0x100000, 0x200000, 0x300000];
            bases[field] = 0;
            RejectCreate(bases[0], bases[1], bases[2], 0x4000, 64, DmaAddressStatus.InvalidAddress);
            bases[field] = 0x123001;
            RejectCreate(bases[0], bases[1], bases[2], 0x4000, 64, DmaAddressStatus.InvalidAlignment);
            bases[field] = 0xfffffffffffff000;
            RejectCreate(bases[0], bases[1], bases[2], 0x4000, 64, DmaAddressStatus.AddressOverflow);
        }
        RejectCreate(0x100000, 0x200000, 0x300000, 0, 64, DmaAddressStatus.InvalidLength);
        RejectCreate(0x100000, 0x200000, 0x300000, 0x1001, 64, DmaAddressStatus.InvalidAlignment);
        RejectCreate(0x100000, 0x200000, 0x300000, 0xfffffffffffff000, 64, DmaAddressStatus.AddressOverflow);

        DmaAddressWindow empty = default;
        HostCheck.True(!empty.IsInitialized);
        HostCheck.Equal(DmaAddressStatus.Uninitialized, empty.TrySlice(0, 0x1000, out var rejected));
        Empty(rejected);
        HostCheck.Equal(DmaAddressStatus.Success,
            DmaAddressWindow.TryCreate(0x100000, 0x200000, 0x300000, 0x4000, 32, out var window));
        foreach ((ulong offset, ulong length, DmaAddressStatus status) in new[]
        {
            (0UL, 0UL, DmaAddressStatus.InvalidLength),
            (1UL, 0x1000UL, DmaAddressStatus.InvalidAlignment),
            (0UL, 1UL, DmaAddressStatus.InvalidAlignment),
            (0x4000UL, 0x1000UL, DmaAddressStatus.OutOfRange),
            (0x3000UL, 0x2000UL, DmaAddressStatus.OutOfRange),
            (0xfffffffffffff000UL, 0x1000UL, DmaAddressStatus.OutOfRange),
            (0x1000UL, 0xfffffffffffff000UL, DmaAddressStatus.OutOfRange)
        })
        {
            HostCheck.Equal(DmaAddressStatus.Success, window.TrySlice(0, 0x1000, out rejected));
            HostCheck.Equal(status, window.TrySlice(offset, length, out rejected));
            Empty(rejected);
        }
        return true;
    }

    private static void RejectCreate(ulong cpu, ulong physical, ulong device, ulong bytes, uint width, DmaAddressStatus status)
    {
        HostCheck.Equal(DmaAddressStatus.Success,
            DmaAddressWindow.TryCreate(0x1000, 0x2000, 0x3000, 0x1000, 64, out var window));
        HostCheck.Equal(status, DmaAddressWindow.TryCreate(cpu, physical, device, bytes, width, out window));
        HostCheck.True(!window.IsInitialized && window.CpuVirtualBase == 0 && window.PhysicalBase == 0 &&
            window.DeviceBase == 0 && window.ByteLength == 0 && window.DeviceAddressWidth == 0);
    }

    private static void Empty(DmaAddressRange range) => HostCheck.True(!range.IsInitialized &&
        range.CpuVirtualAddress == 0 && range.PhysicalAddress == 0 && range.DeviceAddress == 0 &&
        range.ByteLength == 0 && range.Offset == 0 && range.DeviceAddressWidth == 0);
}
