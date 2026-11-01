using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioPciBarRegisterWindowLiveAccessorResult
{
    private VirtioPciBarRegisterWindowLiveAccessorResult(bool isInitialized, VirtioPciBarRegisterWindowLiveAccessorStatus status, VirtioPciBarRegisterWindowLiveAccessorOperation operation, VirtioPciRegisterReadStatus readStatus, VirtioPciRegisterWriteStatus writeStatus, VirtioPciCapabilityType capabilityType, byte barIndex, PciBarKind barKind, uint relativeOffset, VirtioPciRegisterAccessWidth width, uint widthBytes, ulong barRelativeOffset, ulong physicalAddress, ulong virtualAddress, ulong value)
    {
        IsInitialized = isInitialized;
        Status = status;
        Operation = operation;
        ReadStatus = readStatus;
        WriteStatus = writeStatus;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        BarKind = barKind;
        RelativeOffset = relativeOffset;
        Width = width;
        WidthBytes = widthBytes;
        BarRelativeOffset = barRelativeOffset;
        PhysicalAddress = physicalAddress;
        VirtualAddress = virtualAddress;
        Value = value;
    }

    public bool IsInitialized { get; }
    public VirtioPciBarRegisterWindowLiveAccessorStatus Status { get; }
    public VirtioPciBarRegisterWindowLiveAccessorOperation Operation { get; }
    public VirtioPciRegisterReadStatus ReadStatus { get; }
    public VirtioPciRegisterWriteStatus WriteStatus { get; }
    public VirtioPciCapabilityType CapabilityType { get; }
    public byte BarIndex { get; }
    public PciBarKind BarKind { get; }
    public uint RelativeOffset { get; }
    public VirtioPciRegisterAccessWidth Width { get; }
    public uint WidthBytes { get; }
    public ulong BarRelativeOffset { get; }
    public ulong PhysicalAddress { get; }
    public ulong VirtualAddress { get; }
    public ulong Value { get; }
    public bool Succeeded => IsInitialized && Status == VirtioPciBarRegisterWindowLiveAccessorStatus.Success;

    internal static VirtioPciBarRegisterWindowLiveAccessorResult FromPlan(VirtioPciBarRegisterWindowLiveAccessorStatus status, VirtioPciBarRegisterWindowLiveAccessorOperation operation, VirtioPciBarRegisterWindowAccessPlan plan, VirtioPciRegisterReadStatus readStatus, VirtioPciRegisterWriteStatus writeStatus, ulong value)
    {
        return new VirtioPciBarRegisterWindowLiveAccessorResult(isInitialized: true, status, operation, readStatus, writeStatus, plan.CapabilityType, plan.BarIndex, plan.Binding.BarKind, plan.RelativeOffset, plan.Width, plan.WidthBytes, plan.BarRelativeOffset, plan.PhysicalAddress, plan.VirtualAddress, value);
    }
}
