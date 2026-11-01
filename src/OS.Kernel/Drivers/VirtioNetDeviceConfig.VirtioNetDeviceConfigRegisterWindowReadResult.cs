namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public readonly struct VirtioNetDeviceConfigRegisterWindowReadResult
{
    private VirtioNetDeviceConfigRegisterWindowReadResult(VirtioNetDeviceConfigRegisterWindowReadStatus status, VirtioNetDeviceConfigStatus configStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus, VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus, VirtioNetDeviceConfig config, int scratchLength, int bytesRead, int bytesConsumed, bool statusFieldAttempted, bool statusFieldRead, VirtioPciCapabilityType capabilityType, byte barIndex, uint lastRelativeOffset, VirtioPciRegisterAccessWidth lastAccessWidth, uint lastAccessWidthBytes, ulong lastBarRelativeOffset, ulong lastValue, bool isInitialized)
    {
        Status = status;
        ConfigStatus = configStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
        AccessExecutionStatus = accessExecutionStatus;
        Config = config;
        ScratchLength = scratchLength;
        BytesRead = bytesRead;
        BytesConsumed = bytesConsumed;
        StatusFieldAttempted = statusFieldAttempted;
        StatusFieldRead = statusFieldRead;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        LastRelativeOffset = lastRelativeOffset;
        LastAccessWidth = lastAccessWidth;
        LastAccessWidthBytes = lastAccessWidthBytes;
        LastBarRelativeOffset = lastBarRelativeOffset;
        LastValue = lastValue;
        IsInitialized = isInitialized;
    }

    public VirtioNetDeviceConfigRegisterWindowReadStatus Status { get; }
    public VirtioNetDeviceConfigStatus ConfigStatus { get; }
    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus { get; }
    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus { get; }
    public VirtioNetDeviceConfig Config { get; }
    public int ScratchLength { get; }
    public int BytesRead { get; }
    public int BytesConsumed { get; }
    public bool StatusFieldAttempted { get; }
    public bool StatusFieldRead { get; }
    public VirtioPciCapabilityType CapabilityType { get; }
    public byte BarIndex { get; }
    public uint LastRelativeOffset { get; }
    public VirtioPciRegisterAccessWidth LastAccessWidth { get; }
    public uint LastAccessWidthBytes { get; }
    public ulong LastBarRelativeOffset { get; }
    public ulong LastValue { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetDeviceConfigRegisterWindowReadStatus.Success;

    internal static VirtioNetDeviceConfigRegisterWindowReadResult FromAccess(VirtioNetDeviceConfigRegisterWindowReadStatus status, VirtioNetDeviceConfigStatus configStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus, VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus, VirtioNetDeviceConfig config, int scratchLength, int bytesRead, int bytesConsumed, bool statusFieldAttempted, bool statusFieldRead, VirtioPciBarRegisterWindowAccessExecutionResult access)
    {
        return new VirtioNetDeviceConfigRegisterWindowReadResult(status, configStatus, registerWindowPlanStatus, accessExecutionStatus, config, scratchLength, bytesRead, bytesConsumed, statusFieldAttempted, statusFieldRead, access.CapabilityType, access.BarIndex, access.RelativeOffset, access.Width, access.WidthBytes, access.BarRelativeOffset, access.Value, isInitialized: true);
    }

    internal static VirtioNetDeviceConfigRegisterWindowReadResult FromPlan(VirtioNetDeviceConfigRegisterWindowReadStatus status, VirtioNetDeviceConfigStatus configStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus, VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus, VirtioNetDeviceConfig config, int scratchLength, int bytesRead, int bytesConsumed, bool statusFieldAttempted, bool statusFieldRead, VirtioPciBarRegisterWindowAccessPlan plan)
    {
        return new VirtioNetDeviceConfigRegisterWindowReadResult(status, configStatus, registerWindowPlanStatus, accessExecutionStatus, config, scratchLength, bytesRead, bytesConsumed, statusFieldAttempted, statusFieldRead, plan.CapabilityType, plan.BarIndex, plan.RelativeOffset, plan.Width, plan.WidthBytes, plan.BarRelativeOffset, lastValue: 0, isInitialized: true);
    }

    internal static VirtioNetDeviceConfigRegisterWindowReadResult FromLastRead(VirtioNetDeviceConfigRegisterWindowReadStatus status, VirtioNetDeviceConfigStatus configStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus, VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus, VirtioNetDeviceConfig config, int scratchLength, int bytesRead, int bytesConsumed, bool statusFieldAttempted, bool statusFieldRead, VirtioNetDeviceConfigRegisterWindowReadResult lastRead)
    {
        return new VirtioNetDeviceConfigRegisterWindowReadResult(status, configStatus, registerWindowPlanStatus, accessExecutionStatus, config, scratchLength, bytesRead, bytesConsumed, statusFieldAttempted, statusFieldRead, lastRead.CapabilityType, lastRead.BarIndex, lastRead.LastRelativeOffset, lastRead.LastAccessWidth, lastRead.LastAccessWidthBytes, lastRead.LastBarRelativeOffset, lastRead.LastValue, isInitialized: true);
    }
}
