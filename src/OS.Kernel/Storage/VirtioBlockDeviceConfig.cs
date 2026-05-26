using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockDeviceConfigStatus
{
    Success = 0,
    ConfigTooSmall = 1,
    InvalidCapacity = 2,
    CapacityOverflow = 3,
    BlockDeviceInfoRejected = 4
}

public enum VirtioBlockDeviceConfigRegisterWindowReadStatus
{
    Success = 0,
    ScratchBufferTooSmall = 1,
    RegisterWindowPlanFailed = 2,
    AccessExecutionFailed = 3,
    ConfigParseFailed = 4
}

public readonly struct VirtioBlockDeviceConfigResult
{
    private VirtioBlockDeviceConfigResult(
        VirtioBlockDeviceConfigStatus status,
        BlockDeviceStatus blockDeviceStatus,
        ulong rawCapacitySectors,
        uint sectorSizeBytes,
        BlockDeviceInfo blockDeviceInfo)
    {
        IsInitialized = true;
        Status = status;
        BlockDeviceStatus = blockDeviceStatus;
        RawCapacitySectors = rawCapacitySectors;
        SectorSizeBytes = sectorSizeBytes;
        BlockDeviceInfo = blockDeviceInfo;
    }

    public bool IsInitialized { get; }

    public VirtioBlockDeviceConfigStatus Status { get; }

    public BlockDeviceStatus BlockDeviceStatus { get; }

    public ulong RawCapacitySectors { get; }

    public uint SectorSizeBytes { get; }

    public BlockDeviceInfo BlockDeviceInfo { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockDeviceConfigStatus.Success;

    public static VirtioBlockDeviceConfigResult Success(
        ulong rawCapacitySectors,
        uint sectorSizeBytes,
        BlockDeviceInfo blockDeviceInfo)
    {
        return new VirtioBlockDeviceConfigResult(
            VirtioBlockDeviceConfigStatus.Success,
            BlockDeviceStatus.Success,
            rawCapacitySectors,
            sectorSizeBytes,
            blockDeviceInfo);
    }

    public static VirtioBlockDeviceConfigResult Fail(
        VirtioBlockDeviceConfigStatus status,
        BlockDeviceStatus blockDeviceStatus,
        ulong rawCapacitySectors,
        uint sectorSizeBytes)
    {
        return new VirtioBlockDeviceConfigResult(
            status,
            blockDeviceStatus,
            rawCapacitySectors,
            sectorSizeBytes,
            default);
    }
}

public static class VirtioBlockDeviceConfig
{
    public const int CapacityOffset = 0;
    public const int CapacityLength = sizeof(ulong);
    public const uint SectorSizeBytes = VirtioBlockRequestPlan.SectorSizeBytes;
    public const bool PerformsMmio = false;
    public const bool ReadsDeviceConfig = false;

    public static VirtioBlockDeviceConfigResult Parse(ReadOnlySpan<byte> config)
    {
        if (!EndianPrimitives.TryReadUInt64LittleEndian(
            config.Slice(CapacityOffset),
            out ulong rawCapacitySectors))
        {
            return VirtioBlockDeviceConfigResult.Fail(
                VirtioBlockDeviceConfigStatus.ConfigTooSmall,
                BlockDeviceStatus.Success,
                rawCapacitySectors: 0,
                SectorSizeBytes);
        }

        BlockDeviceInfoResult blockInfo = BlockDeviceInfo.Create(
            SectorSizeBytes,
            rawCapacitySectors);
        if (blockInfo.Succeeded)
        {
            return VirtioBlockDeviceConfigResult.Success(
                rawCapacitySectors,
                SectorSizeBytes,
                blockInfo.Info);
        }

        VirtioBlockDeviceConfigStatus status = blockInfo.Status switch
        {
            BlockDeviceStatus.InvalidBlockCount => VirtioBlockDeviceConfigStatus.InvalidCapacity,
            BlockDeviceStatus.Overflow => VirtioBlockDeviceConfigStatus.CapacityOverflow,
            _ => VirtioBlockDeviceConfigStatus.BlockDeviceInfoRejected
        };
        return VirtioBlockDeviceConfigResult.Fail(
            status,
            blockInfo.Status,
            rawCapacitySectors,
            SectorSizeBytes);
    }
}

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

    public bool IsInitialized { get; }

    public VirtioBlockDeviceConfigRegisterWindowReadStatus Status { get; }

    public VirtioBlockDeviceConfigStatus ConfigStatus { get; }

    public BlockDeviceStatus BlockDeviceStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus { get; }

    public ulong RawCapacitySectors { get; }

    public uint SectorSizeBytes { get; }

    public BlockDeviceInfo BlockDeviceInfo { get; }

    public int ScratchLength { get; }

    public int BytesRead { get; }

    public VirtioPciCapabilityType CapabilityType { get; }

    public byte BarIndex { get; }

    public uint RelativeOffset { get; }

    public VirtioPciRegisterAccessWidth AccessWidth { get; }

    public uint AccessWidthBytes { get; }

    public ulong BarRelativeOffset { get; }

    public ulong Value { get; }

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

public static class VirtioBlockDeviceConfigRegisterWindowReader
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool UsesCallerOwnedScratchBuffer = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool ReadsCallerOwnedWindowBuffer = true;
    public const bool WritesCallerOwnedScratchBuffer = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ReadsLiveDeviceRegisters = false;
    public const bool WritesLiveDeviceRegisters = false;
    public const bool ConfiguresQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;

    public static VirtioBlockDeviceConfigRegisterWindowReadResult Read(
        VirtioPciBarRegisterWindowBinding deviceConfiguration,
        ReadOnlySpan<byte> deviceWindowBytes,
        Span<byte> scratch)
    {
        if (scratch.Length < VirtioBlockDeviceConfig.CapacityLength)
        {
            return VirtioBlockDeviceConfigRegisterWindowReadResult.FromPlan(
                VirtioBlockDeviceConfigRegisterWindowReadStatus.ScratchBufferTooSmall,
                VirtioBlockDeviceConfigStatus.ConfigTooSmall,
                BlockDeviceStatus.Success,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                rawCapacitySectors: 0,
                VirtioBlockDeviceConfig.SectorSizeBytes,
                default,
                scratch.Length,
                bytesRead: 0,
                default,
                value: 0);
        }

        VirtioPciBarRegisterWindowAccessPlanResult plan =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                deviceConfiguration,
                VirtioBlockDeviceConfig.CapacityOffset,
                VirtioPciRegisterAccessWidth.QWord);
        if (!plan.Succeeded)
        {
            return VirtioBlockDeviceConfigRegisterWindowReadResult.FromPlan(
                VirtioBlockDeviceConfigRegisterWindowReadStatus.RegisterWindowPlanFailed,
                VirtioBlockDeviceConfigStatus.Success,
                BlockDeviceStatus.Success,
                plan.Status,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                rawCapacitySectors: 0,
                VirtioBlockDeviceConfig.SectorSizeBytes,
                default,
                scratch.Length,
                bytesRead: 0,
                plan.Plan,
                value: 0);
        }

        VirtioPciBarRegisterWindowAccessExecutionResult access =
            VirtioPciBarRegisterWindowAccessExecutor.Read(
                plan.Plan,
                deviceWindowBytes);
        if (!access.Succeeded)
        {
            return VirtioBlockDeviceConfigRegisterWindowReadResult.FromAccess(
                VirtioBlockDeviceConfigRegisterWindowReadStatus.AccessExecutionFailed,
                VirtioBlockDeviceConfigStatus.Success,
                BlockDeviceStatus.Success,
                plan.Status,
                access.Status,
                rawCapacitySectors: 0,
                VirtioBlockDeviceConfig.SectorSizeBytes,
                default,
                scratch.Length,
                bytesRead: 0,
                access);
        }

        EndianPrimitives.WriteUInt64LittleEndian(
            scratch.Slice(VirtioBlockDeviceConfig.CapacityOffset, VirtioBlockDeviceConfig.CapacityLength),
            access.Value);

        VirtioBlockDeviceConfigResult config =
            VirtioBlockDeviceConfig.Parse(
                scratch.Slice(VirtioBlockDeviceConfig.CapacityOffset, VirtioBlockDeviceConfig.CapacityLength));
        if (!config.Succeeded)
        {
            return VirtioBlockDeviceConfigRegisterWindowReadResult.FromAccess(
                VirtioBlockDeviceConfigRegisterWindowReadStatus.ConfigParseFailed,
                config.Status,
                config.BlockDeviceStatus,
                plan.Status,
                access.Status,
                config.RawCapacitySectors,
                config.SectorSizeBytes,
                config.BlockDeviceInfo,
                scratch.Length,
                VirtioBlockDeviceConfig.CapacityLength,
                access);
        }

        return VirtioBlockDeviceConfigRegisterWindowReadResult.FromAccess(
            VirtioBlockDeviceConfigRegisterWindowReadStatus.Success,
            config.Status,
            config.BlockDeviceStatus,
            plan.Status,
            access.Status,
            config.RawCapacitySectors,
            config.SectorSizeBytes,
            config.BlockDeviceInfo,
            scratch.Length,
            VirtioBlockDeviceConfig.CapacityLength,
            access);
    }
}
