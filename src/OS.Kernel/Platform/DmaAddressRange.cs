namespace OS.Kernel.Platform;
/// <summary>
/// Numeric slice of an admitted window. It does not establish hardware mapping,
/// coherence or storage lifetime, and never substitutes one address domain for another.
/// </summary>

public readonly struct DmaAddressRange
{
    private readonly byte _initialized;
    public readonly ulong CpuVirtualAddress, PhysicalAddress, DeviceAddress, ByteLength, Offset;
    public readonly uint DeviceAddressWidth;
    public bool IsInitialized => _initialized == 1;

    internal DmaAddressRange(ulong cpuVirtualAddress, ulong physicalAddress, ulong deviceAddress, ulong byteLength, ulong offset, uint width)
    {
        _initialized = 1;
        CpuVirtualAddress = cpuVirtualAddress;
        PhysicalAddress = physicalAddress;
        DeviceAddress = deviceAddress;
        ByteLength = byteLength;
        Offset = offset;
        DeviceAddressWidth = width;
    }
}
