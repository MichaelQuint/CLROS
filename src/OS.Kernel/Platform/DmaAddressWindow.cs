namespace OS.Kernel.Platform;
/// <summary>
/// Adapter-supplied numeric address correspondence, not hardware/IOMMU mapping proof.
/// Mapping, coherence and lifetime remain adapter-owned. Identity requires explicitly
/// supplying equal bases; CPU virtual, physical and device addresses may all differ.
/// </summary>

public readonly struct DmaAddressWindow
{
    private readonly byte _initialized;
    public readonly ulong CpuVirtualBase, PhysicalBase, DeviceBase, ByteLength;
    public readonly uint DeviceAddressWidth;
    public bool IsInitialized => _initialized == 1;

    private DmaAddressWindow(ulong cpuVirtualBase, ulong physicalBase, ulong deviceBase, ulong byteLength, uint width)
    {
        _initialized = 1;
        CpuVirtualBase = cpuVirtualBase;
        PhysicalBase = physicalBase;
        DeviceBase = deviceBase;
        ByteLength = byteLength;
        DeviceAddressWidth = width;
    }

    public static DmaAddressStatus TryCreate(ulong cpuVirtualBase, ulong physicalBase, ulong deviceBase, ulong byteLength, uint deviceAddressWidth, out DmaAddressWindow window)
    {
        window = default;
        if (deviceAddressWidth == 0 || deviceAddressWidth > 64)
            return DmaAddressStatus.InvalidDeviceWidth;
        if (cpuVirtualBase == 0 || physicalBase == 0 || deviceBase == 0)
            return DmaAddressStatus.InvalidAddress;
        if (byteLength == 0)
            return DmaAddressStatus.InvalidLength;
        if (((cpuVirtualBase | physicalBase | deviceBase | byteLength) & 4095) != 0)
            return DmaAddressStatus.InvalidAlignment;
        if (byteLength > ~0UL - cpuVirtualBase || byteLength > ~0UL - physicalBase || byteLength > ~0UL - deviceBase)
            return DmaAddressStatus.AddressOverflow;
        if (deviceAddressWidth < 64)
        {
            ulong limit = 1UL << (int)deviceAddressWidth;
            if (deviceBase >= limit || byteLength > limit - deviceBase)
                return DmaAddressStatus.DeviceAddressLimit;
        }

        window = new(cpuVirtualBase, physicalBase, deviceBase, byteLength, deviceAddressWidth);
        return DmaAddressStatus.Success;
    }

    /// <summary>Offset zero is valid; the offset and nonzero length must be page-aligned.</summary>

    public DmaAddressStatus TrySlice(ulong offset, ulong byteLength, out DmaAddressRange range)
    {
        range = default;
        if (!IsInitialized)
            return DmaAddressStatus.Uninitialized;
        if (byteLength == 0)
            return DmaAddressStatus.InvalidLength;
        if (((offset | byteLength) & 4095) != 0)
            return DmaAddressStatus.InvalidAlignment;
        if (offset >= ByteLength || byteLength > ByteLength - offset)
            return DmaAddressStatus.OutOfRange;
        range = new(CpuVirtualBase + offset, PhysicalBase + offset, DeviceBase + offset, byteLength, offset, DeviceAddressWidth);
        return DmaAddressStatus.Success;
    }
}
