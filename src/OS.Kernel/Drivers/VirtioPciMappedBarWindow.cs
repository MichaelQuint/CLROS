using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioPciMappedBarWindow
{
    public VirtioPciMappedBarWindow(byte barIndex, ulong physicalBaseAddress, ulong virtualBaseAddress, ulong length, bool isMapped, bool isMmio, bool isDeviceMemory, bool isUncached)
    {
        BarIndex = barIndex;
        PhysicalBaseAddress = physicalBaseAddress;
        VirtualBaseAddress = virtualBaseAddress;
        Length = length;
        IsMapped = isMapped;
        IsMmio = isMmio;
        IsDeviceMemory = isDeviceMemory;
        IsUncached = isUncached;
    }

    public byte BarIndex { get; }
    public ulong PhysicalBaseAddress { get; }
    public ulong VirtualBaseAddress { get; }
    public ulong Length { get; }
    public bool IsMapped { get; }
    public bool IsMmio { get; }
    public bool IsDeviceMemory { get; }
    public bool IsUncached { get; }
}
