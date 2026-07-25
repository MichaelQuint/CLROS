namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public enum VirtioNetDeviceConfigStatus
{
    Success = 0,
    BufferTooSmall = 1,
    ZeroMacAddress = 2,
    BroadcastMacAddress = 3
}

public enum VirtioNetDeviceConfigRegisterWindowReadStatus
{
    Success = 0,
    ScratchBufferTooSmall = 1,
    RegisterWindowPlanFailed = 2,
    AccessExecutionFailed = 3,
    ConfigParseFailed = 4
}

public readonly struct VirtioNetDeviceConfig
{
    public const int MacAddressOffset = 0;
    public const int MacAddressLength = MacAddress.Length;
    public const int MinimumLength = MacAddressOffset + MacAddressLength;
    public const int StatusOffset = MinimumLength;
    public const int StatusLength = sizeof(ushort);
    public const int StatusConfigLength = StatusOffset + StatusLength;

    public const ushort StatusLinkUp = 0x0001;
    public const ushort StatusAnnounce = 0x0002;

    public VirtioNetDeviceConfig(
        MacAddress macAddress,
        bool hasStatus,
        ushort statusBits)
    {
        MacAddress = macAddress;
        HasStatus = hasStatus;
        StatusBits = statusBits;
    }

    public MacAddress MacAddress { get; }

    public bool HasStatus { get; }

    public ushort StatusBits { get; }

    public bool IsLinkUp => HasStatus && (StatusBits & StatusLinkUp) != 0;

    public bool HasAnnounce => HasStatus && (StatusBits & StatusAnnounce) != 0;

    public static VirtioNetDeviceConfigParseResult TryParse(
        ReadOnlySpan<byte> source)
    {
        if (source.Length < MinimumLength ||
            source.Length == StatusConfigLength - 1)
        {
            return VirtioNetDeviceConfigParseResult.Fail(
                VirtioNetDeviceConfigStatus.BufferTooSmall,
                source.Length);
        }

        MacAddress macAddress = new MacAddress(
            source[0],
            source[1],
            source[2],
            source[3],
            source[4],
            source[5]);

        if (macAddress.IsZero)
        {
            return VirtioNetDeviceConfigParseResult.Fail(
                VirtioNetDeviceConfigStatus.ZeroMacAddress,
                source.Length);
        }

        if (macAddress.IsBroadcast)
        {
            return VirtioNetDeviceConfigParseResult.Fail(
                VirtioNetDeviceConfigStatus.BroadcastMacAddress,
                source.Length);
        }

        bool hasStatus = source.Length >= StatusConfigLength;
        ushort statusBits = hasStatus
            ? EndianPrimitives.ReadUInt16LittleEndian(
                source.Slice(StatusOffset, StatusLength))
            : (ushort)0;

        VirtioNetDeviceConfig config = new VirtioNetDeviceConfig(
            macAddress,
            hasStatus,
            statusBits);

        int bytesConsumed = hasStatus ? StatusConfigLength : MinimumLength;
        return VirtioNetDeviceConfigParseResult.Success(
            config,
            source.Length,
            bytesConsumed);
    }

}

public readonly struct VirtioNetDeviceConfigRegisterWindowReadResult
{
    private VirtioNetDeviceConfigRegisterWindowReadResult(
        VirtioNetDeviceConfigRegisterWindowReadStatus status,
        VirtioNetDeviceConfigStatus configStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioNetDeviceConfig config,
        int scratchLength,
        int bytesRead,
        int bytesConsumed,
        bool statusFieldAttempted,
        bool statusFieldRead,
        VirtioPciCapabilityType capabilityType,
        byte barIndex,
        uint lastRelativeOffset,
        VirtioPciRegisterAccessWidth lastAccessWidth,
        uint lastAccessWidthBytes,
        ulong lastBarRelativeOffset,
        ulong lastValue,
        bool isInitialized)
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

    public bool Succeeded =>
        IsInitialized && Status == VirtioNetDeviceConfigRegisterWindowReadStatus.Success;

    internal static VirtioNetDeviceConfigRegisterWindowReadResult FromAccess(
        VirtioNetDeviceConfigRegisterWindowReadStatus status,
        VirtioNetDeviceConfigStatus configStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioNetDeviceConfig config,
        int scratchLength,
        int bytesRead,
        int bytesConsumed,
        bool statusFieldAttempted,
        bool statusFieldRead,
        VirtioPciBarRegisterWindowAccessExecutionResult access)
    {
        return new VirtioNetDeviceConfigRegisterWindowReadResult(
            status,
            configStatus,
            registerWindowPlanStatus,
            accessExecutionStatus,
            config,
            scratchLength,
            bytesRead,
            bytesConsumed,
            statusFieldAttempted,
            statusFieldRead,
            access.CapabilityType,
            access.BarIndex,
            access.RelativeOffset,
            access.Width,
            access.WidthBytes,
            access.BarRelativeOffset,
            access.Value,
            isInitialized: true);
    }

    internal static VirtioNetDeviceConfigRegisterWindowReadResult FromPlan(
        VirtioNetDeviceConfigRegisterWindowReadStatus status,
        VirtioNetDeviceConfigStatus configStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioNetDeviceConfig config,
        int scratchLength,
        int bytesRead,
        int bytesConsumed,
        bool statusFieldAttempted,
        bool statusFieldRead,
        VirtioPciBarRegisterWindowAccessPlan plan)
    {
        return new VirtioNetDeviceConfigRegisterWindowReadResult(
            status,
            configStatus,
            registerWindowPlanStatus,
            accessExecutionStatus,
            config,
            scratchLength,
            bytesRead,
            bytesConsumed,
            statusFieldAttempted,
            statusFieldRead,
            plan.CapabilityType,
            plan.BarIndex,
            plan.RelativeOffset,
            plan.Width,
            plan.WidthBytes,
            plan.BarRelativeOffset,
            lastValue: 0,
            isInitialized: true);
    }

    internal static VirtioNetDeviceConfigRegisterWindowReadResult FromLastRead(
        VirtioNetDeviceConfigRegisterWindowReadStatus status,
        VirtioNetDeviceConfigStatus configStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioNetDeviceConfig config,
        int scratchLength,
        int bytesRead,
        int bytesConsumed,
        bool statusFieldAttempted,
        bool statusFieldRead,
        VirtioNetDeviceConfigRegisterWindowReadResult lastRead)
    {
        return new VirtioNetDeviceConfigRegisterWindowReadResult(
            status,
            configStatus,
            registerWindowPlanStatus,
            accessExecutionStatus,
            config,
            scratchLength,
            bytesRead,
            bytesConsumed,
            statusFieldAttempted,
            statusFieldRead,
            lastRead.CapabilityType,
            lastRead.BarIndex,
            lastRead.LastRelativeOffset,
            lastRead.LastAccessWidth,
            lastRead.LastAccessWidthBytes,
            lastRead.LastBarRelativeOffset,
            lastRead.LastValue,
            isInitialized: true);
    }
}

public static class VirtioNetDeviceConfigRegisterWindowReader
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
    public const bool PerformsPacketIo = false;

    public static VirtioNetDeviceConfigRegisterWindowReadResult Read(
        VirtioPciBarRegisterWindowBinding deviceConfiguration,
        ReadOnlySpan<byte> deviceWindowBytes,
        Span<byte> scratch)
    {
        if (scratch.Length < VirtioNetDeviceConfig.MinimumLength)
        {
            return VirtioNetDeviceConfigRegisterWindowReadResult.FromPlan(
                VirtioNetDeviceConfigRegisterWindowReadStatus.ScratchBufferTooSmall,
                VirtioNetDeviceConfigStatus.BufferTooSmall,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                scratch.Length,
                bytesRead: 0,
                bytesConsumed: 0,
                statusFieldAttempted: false,
                statusFieldRead: false,
                default);
        }

        VirtioNetDeviceConfigRegisterWindowReadResult macPrefix =
            ReadDWord(
                deviceConfiguration,
                relativeOffset: VirtioNetDeviceConfig.MacAddressOffset,
                deviceWindowBytes,
                scratch,
                scratchOffset: VirtioNetDeviceConfig.MacAddressOffset,
                bytesRead: 0,
                statusFieldAttempted: false,
                statusFieldRead: false);
        if (!macPrefix.Succeeded)
        {
            return macPrefix;
        }

        VirtioNetDeviceConfigRegisterWindowReadResult macSuffix =
            ReadWord(
                deviceConfiguration,
                relativeOffset: VirtioNetDeviceConfig.MacAddressOffset + sizeof(uint),
                deviceWindowBytes,
                scratch,
                scratchOffset: VirtioNetDeviceConfig.MacAddressOffset + sizeof(uint),
                bytesRead: sizeof(uint),
                statusFieldAttempted: false,
                statusFieldRead: false);
        if (!macSuffix.Succeeded)
        {
            return macSuffix;
        }

        bool readStatus =
            deviceConfiguration.ApertureLength >= VirtioNetDeviceConfig.StatusConfigLength &&
            scratch.Length >= VirtioNetDeviceConfig.StatusConfigLength;
        VirtioNetDeviceConfigRegisterWindowReadResult lastRead = macSuffix;
        int bytesRead = VirtioNetDeviceConfig.MinimumLength;

        if (readStatus)
        {
            lastRead = ReadWord(
                deviceConfiguration,
                relativeOffset: VirtioNetDeviceConfig.StatusOffset,
                deviceWindowBytes,
                scratch,
                scratchOffset: VirtioNetDeviceConfig.StatusOffset,
                bytesRead,
                statusFieldAttempted: true,
                statusFieldRead: false);
            if (!lastRead.Succeeded)
            {
                return lastRead;
            }

            bytesRead = VirtioNetDeviceConfig.StatusConfigLength;
        }

        VirtioNetDeviceConfigParseResult parse =
            VirtioNetDeviceConfig.TryParse(scratch.Slice(0, bytesRead));
        if (!parse.Succeeded)
        {
            return VirtioNetDeviceConfigRegisterWindowReadResult.FromLastRead(
                VirtioNetDeviceConfigRegisterWindowReadStatus.ConfigParseFailed,
                parse.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                scratch.Length,
                bytesRead,
                parse.BytesConsumed,
                statusFieldAttempted: readStatus,
                statusFieldRead: readStatus,
                lastRead);
        }

        return VirtioNetDeviceConfigRegisterWindowReadResult.FromLastRead(
            VirtioNetDeviceConfigRegisterWindowReadStatus.Success,
            parse.Status,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
            parse.Config,
            scratch.Length,
            bytesRead,
            parse.BytesConsumed,
            statusFieldAttempted: readStatus,
            statusFieldRead: readStatus,
            lastRead);
    }

    private static VirtioNetDeviceConfigRegisterWindowReadResult ReadDWord(
        VirtioPciBarRegisterWindowBinding deviceConfiguration,
        uint relativeOffset,
        ReadOnlySpan<byte> deviceWindowBytes,
        Span<byte> scratch,
        int scratchOffset,
        int bytesRead,
        bool statusFieldAttempted,
        bool statusFieldRead)
    {
        VirtioPciBarRegisterWindowAccessExecutionResult read =
            ReadAccess(
                deviceConfiguration,
                relativeOffset,
                VirtioPciRegisterAccessWidth.DWord,
                deviceWindowBytes,
                out VirtioPciBarRegisterWindowPlanStatus planStatus);
        if (planStatus != VirtioPciBarRegisterWindowPlanStatus.Success ||
            !read.Succeeded)
        {
            return FailRead(read, planStatus, scratch.Length, bytesRead, statusFieldAttempted, statusFieldRead);
        }

        EndianPrimitives.WriteUInt32LittleEndian(
            scratch.Slice(scratchOffset, sizeof(uint)),
            (uint)read.Value);

        return VirtioNetDeviceConfigRegisterWindowReadResult.FromAccess(
            VirtioNetDeviceConfigRegisterWindowReadStatus.Success,
            VirtioNetDeviceConfigStatus.Success,
            planStatus,
            read.Status,
            default,
            scratch.Length,
            bytesRead + sizeof(uint),
            bytesConsumed: 0,
            statusFieldAttempted,
            statusFieldRead,
            read);
    }

    private static VirtioNetDeviceConfigRegisterWindowReadResult ReadWord(
        VirtioPciBarRegisterWindowBinding deviceConfiguration,
        uint relativeOffset,
        ReadOnlySpan<byte> deviceWindowBytes,
        Span<byte> scratch,
        int scratchOffset,
        int bytesRead,
        bool statusFieldAttempted,
        bool statusFieldRead)
    {
        VirtioPciBarRegisterWindowAccessExecutionResult read =
            ReadAccess(
                deviceConfiguration,
                relativeOffset,
                VirtioPciRegisterAccessWidth.Word,
                deviceWindowBytes,
                out VirtioPciBarRegisterWindowPlanStatus planStatus);
        if (planStatus != VirtioPciBarRegisterWindowPlanStatus.Success ||
            !read.Succeeded)
        {
            return FailRead(read, planStatus, scratch.Length, bytesRead, statusFieldAttempted, statusFieldRead);
        }

        EndianPrimitives.WriteUInt16LittleEndian(
            scratch.Slice(scratchOffset, sizeof(ushort)),
            (ushort)read.Value);

        return VirtioNetDeviceConfigRegisterWindowReadResult.FromAccess(
            VirtioNetDeviceConfigRegisterWindowReadStatus.Success,
            VirtioNetDeviceConfigStatus.Success,
            planStatus,
            read.Status,
            default,
            scratch.Length,
            bytesRead + sizeof(ushort),
            bytesConsumed: 0,
            statusFieldAttempted,
            statusFieldRead: statusFieldAttempted || statusFieldRead,
            read);
    }

    private static VirtioPciBarRegisterWindowAccessExecutionResult ReadAccess(
        VirtioPciBarRegisterWindowBinding deviceConfiguration,
        uint relativeOffset,
        VirtioPciRegisterAccessWidth width,
        ReadOnlySpan<byte> deviceWindowBytes,
        out VirtioPciBarRegisterWindowPlanStatus planStatus)
    {
        VirtioPciBarRegisterWindowAccessPlanResult plan =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                deviceConfiguration,
                relativeOffset,
                width);
        planStatus = plan.Status;

        if (!plan.Succeeded)
        {
            return VirtioPciBarRegisterWindowAccessExecutionResult.FromPlan(
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                plan.Plan,
                value: 0);
        }

        return VirtioPciBarRegisterWindowAccessExecutor.Read(
            plan.Plan,
            deviceWindowBytes);
    }

    private static VirtioNetDeviceConfigRegisterWindowReadResult FailRead(
        VirtioPciBarRegisterWindowAccessExecutionResult read,
        VirtioPciBarRegisterWindowPlanStatus planStatus,
        int scratchLength,
        int bytesRead,
        bool statusFieldAttempted,
        bool statusFieldRead)
    {
        VirtioNetDeviceConfigRegisterWindowReadStatus status =
            planStatus == VirtioPciBarRegisterWindowPlanStatus.Success
                ? VirtioNetDeviceConfigRegisterWindowReadStatus.AccessExecutionFailed
                : VirtioNetDeviceConfigRegisterWindowReadStatus.RegisterWindowPlanFailed;
        return VirtioNetDeviceConfigRegisterWindowReadResult.FromAccess(
            status,
            VirtioNetDeviceConfigStatus.Success,
            planStatus,
            read.Status,
            default,
            scratchLength,
            bytesRead,
            bytesConsumed: 0,
            statusFieldAttempted,
            statusFieldRead,
            read);
    }
}

public readonly struct VirtioNetDeviceConfigParseResult
{
    private VirtioNetDeviceConfigParseResult(
        VirtioNetDeviceConfigStatus status,
        VirtioNetDeviceConfig config,
        int inputLength,
        int bytesConsumed,
        bool isInitialized)
    {
        Status = status;
        Config = config;
        InputLength = inputLength;
        BytesConsumed = bytesConsumed;
        IsInitialized = isInitialized;
    }

    public VirtioNetDeviceConfigStatus Status { get; }

    public VirtioNetDeviceConfig Config { get; }

    public int InputLength { get; }

    public int BytesConsumed { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioNetDeviceConfigStatus.Success;

    public static VirtioNetDeviceConfigParseResult Success(
        VirtioNetDeviceConfig config,
        int inputLength,
        int bytesConsumed)
    {
        return new VirtioNetDeviceConfigParseResult(
            VirtioNetDeviceConfigStatus.Success,
            config,
            inputLength,
            bytesConsumed,
            isInitialized: true);
    }

    public static VirtioNetDeviceConfigParseResult Fail(
        VirtioNetDeviceConfigStatus status,
        int inputLength)
    {
        return new VirtioNetDeviceConfigParseResult(
            status,
            default,
            inputLength,
            0,
            isInitialized: true);
    }
}
