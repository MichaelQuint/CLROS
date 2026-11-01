using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioPciBarRegisterWindowAccessPlan
{
    internal VirtioPciBarRegisterWindowAccessPlan(VirtioPciBarRegisterWindowBinding binding, uint relativeOffset, VirtioPciRegisterAccessWidth width, uint widthBytes, ulong barRelativeOffset, ulong physicalAddress, ulong virtualAddress)
    {
        Binding = binding;
        RelativeOffset = relativeOffset;
        Width = width;
        WidthBytes = widthBytes;
        BarRelativeOffset = barRelativeOffset;
        PhysicalAddress = physicalAddress;
        VirtualAddress = virtualAddress;
    }

    public VirtioPciBarRegisterWindowBinding Binding { get; }
    public VirtioPciCapabilityType CapabilityType => Binding.CapabilityType;
    public byte BarIndex => Binding.BarIndex;
    public uint RelativeOffset { get; }
    public VirtioPciRegisterAccessWidth Width { get; }
    public uint WidthBytes { get; }
    public ulong BarRelativeOffset { get; }
    public ulong PhysicalAddress { get; }
    public ulong VirtualAddress { get; }
}
