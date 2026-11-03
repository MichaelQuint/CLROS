using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public readonly struct VirtioBlockDeviceConfigRegisterWindowReadResult
{
    private VirtioBlockDeviceConfigRegisterWindowReadResult(
        VirtioBlockDeviceConfigRegisterWindowReadStatus status,
        VirtioBlockDeviceConfigStatus configStatus,
        BlockDeviceStatus blockDeviceStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        ulong rawCapacitySectors,
        uint sectorSizeBytes,
        BlockDeviceInfo blockDeviceInfo,
        int scratchLength,
        int bytesRead,
        VirtioPciCapabilityType capabilityType,
        byte barIndex,
        uint relativeOffset,
        VirtioPciRegisterAccessWidth accessWidth,
        uint accessWidthBytes,
        ulong barRelativeOffset,
        ulong value)
    {
        IsInitialized = true;
        Status = status;
        ConfigStatus = configStatus;
        BlockDeviceStatus = blockDeviceStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
        AccessExecutionStatus = accessExecutionStatus;
        RawCapacitySectors = rawCapacitySectors;
        SectorSizeBytes = sectorSizeBytes;
        BlockDeviceInfo = blockDeviceInfo;
        ScratchLength = scratchLength;
        BytesRead = bytesRead;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        RelativeOffset = relativeOffset;
        AccessWidth = accessWidth;
        AccessWidthBytes = accessWidthBytes;
        BarRelativeOffset = barRelativeOffset;
        Value = value;
    }

    public bool IsInitialized
    {
        get;
    }

    public VirtioBlockDeviceConfigRegisterWindowReadStatus Status
    {
        get;
    }

    public VirtioBlockDeviceConfigStatus ConfigStatus
    {
        get;
    }

    public BlockDeviceStatus BlockDeviceStatus
    {
        get;
    }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus
    {
        get;
    }

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus
    {
        get;
    }

    public ulong RawCapacitySectors
    {
        get;
    }

    public uint SectorSizeBytes
    {
        get;
    }

    public BlockDeviceInfo BlockDeviceInfo
    {
        get;
    }

    public int ScratchLength
    {
        get;
    }

    public int BytesRead
    {
        get;
    }

    public VirtioPciCapabilityType CapabilityType
    {
        get;
    }

    public byte BarIndex
    {
        get;
    }

    public uint RelativeOffset
    {
        get;
    }

    public VirtioPciRegisterAccessWidth AccessWidth
    {
        get;
    }

    public uint AccessWidthBytes
    {
        get;
    }

    public ulong BarRelativeOffset
    {
        get;
    }

    public ulong Value
    {
        get;
    }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockDeviceConfigRegisterWindowReadStatus.Success;

    internal static VirtioBlockDeviceConfigRegisterWindowReadResult FromPlan(
        VirtioBlockDeviceConfigRegisterWindowReadStatus status,
        VirtioBlockDeviceConfigStatus configStatus,
        BlockDeviceStatus blockDeviceStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        ulong rawCapacitySectors,
        uint sectorSizeBytes,
        BlockDeviceInfo blockDeviceInfo,
        int scratchLength,
        int bytesRead,
        VirtioPciBarRegisterWindowAccessPlan plan,
        ulong value)
    {
        return new VirtioBlockDeviceConfigRegisterWindowReadResult(
            status,
            configStatus,
            blockDeviceStatus,
            registerWindowPlanStatus,
            accessExecutionStatus,
            rawCapacitySectors,
            sectorSizeBytes,
            blockDeviceInfo,
            scratchLength,
            bytesRead,
            plan.CapabilityType,
            plan.BarIndex,
            plan.RelativeOffset,
            plan.Width,
            plan.WidthBytes,
            plan.BarRelativeOffset,
            value);
    }

    internal static VirtioBlockDeviceConfigRegisterWindowReadResult FromAccess(
        VirtioBlockDeviceConfigRegisterWindowReadStatus status,
        VirtioBlockDeviceConfigStatus configStatus,
        BlockDeviceStatus blockDeviceStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        ulong rawCapacitySectors,
        uint sectorSizeBytes,
        BlockDeviceInfo blockDeviceInfo,
        int scratchLength,
        int bytesRead,
        VirtioPciBarRegisterWindowAccessExecutionResult access)
    {
        return new VirtioBlockDeviceConfigRegisterWindowReadResult(
            status,
            configStatus,
            blockDeviceStatus,
            registerWindowPlanStatus,
            accessExecutionStatus,
            rawCapacitySectors,
            sectorSizeBytes,
            blockDeviceInfo,
            scratchLength,
            bytesRead,
            access.CapabilityType,
            access.BarIndex,
            access.RelativeOffset,
            access.Width,
            access.WidthBytes,
            access.BarRelativeOffset,
            access.Value);
    }
}
