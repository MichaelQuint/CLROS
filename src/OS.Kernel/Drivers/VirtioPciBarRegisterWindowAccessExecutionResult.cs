using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioPciBarRegisterWindowAccessExecutionResult
{
    private VirtioPciBarRegisterWindowAccessExecutionResult(VirtioPciBarRegisterWindowAccessExecutionStatus status, VirtioPciCapabilityType capabilityType, byte barIndex, uint relativeOffset, VirtioPciRegisterAccessWidth width, uint widthBytes, ulong barRelativeOffset, ulong value, bool isInitialized)
    {
        Status = status;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        RelativeOffset = relativeOffset;
        Width = width;
        WidthBytes = widthBytes;
        BarRelativeOffset = barRelativeOffset;
        Value = value;
        IsInitialized = isInitialized;
    }

    public VirtioPciBarRegisterWindowAccessExecutionStatus Status { get; }
    public bool IsInitialized { get; }
    public VirtioPciCapabilityType CapabilityType { get; }
    public byte BarIndex { get; }
    public uint RelativeOffset { get; }
    public VirtioPciRegisterAccessWidth Width { get; }
    public uint WidthBytes { get; }
    public ulong BarRelativeOffset { get; }
    public ulong Value { get; }
    public bool Succeeded => IsInitialized && Status == VirtioPciBarRegisterWindowAccessExecutionStatus.Success;

    internal static VirtioPciBarRegisterWindowAccessExecutionResult FromPlan(VirtioPciBarRegisterWindowAccessExecutionStatus status, VirtioPciBarRegisterWindowAccessPlan plan, ulong value)
    {
        return new VirtioPciBarRegisterWindowAccessExecutionResult(status, plan.CapabilityType, plan.BarIndex, plan.RelativeOffset, plan.Width, plan.WidthBytes, plan.BarRelativeOffset, value, isInitialized: true);
    }
}
