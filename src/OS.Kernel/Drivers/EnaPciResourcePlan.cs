using OS.Std.Binary;

namespace OS.Drivers;

public enum PciMsixCapabilityParseStatus
{
    Success = 0,
    BufferTooSmall = 1,
    InvalidCapabilityPointer = 2,
    TruncatedCapability = 3,
    InvalidNextCapabilityPointer = 4,
    CapabilityLoopDetected = 5,
    CapabilityNotFound = 6,
    InvalidBarIndicator = 7
}

public readonly struct PciMsixCapabilitySnapshot
{
    public PciMsixCapabilitySnapshot(
        bool isPresent,
        ushort tableEntryCount,
        byte tableBarIndicator,
        uint tableOffset,
        byte pendingBitArrayBarIndicator,
        uint pendingBitArrayOffset,
        bool functionMasked,
        bool enabled)
    {
        IsPresent = isPresent;
        TableEntryCount = tableEntryCount;
        TableBarIndicator = tableBarIndicator;
        TableOffset = tableOffset;
        PendingBitArrayBarIndicator = pendingBitArrayBarIndicator;
        PendingBitArrayOffset = pendingBitArrayOffset;
        FunctionMasked = functionMasked;
        Enabled = enabled;
    }

    public bool IsPresent { get; }

    public ushort TableEntryCount { get; }

    public byte TableBarIndicator { get; }

    public uint TableOffset { get; }

    public byte PendingBitArrayBarIndicator { get; }

    public uint PendingBitArrayOffset { get; }

    public bool FunctionMasked { get; }

    public bool Enabled { get; }

    public ulong TableLengthBytes => (ulong)TableEntryCount * PciMsixCapabilityParser.TableEntrySize;

    public ulong PendingBitArrayLengthBytes =>
        ((ulong)TableEntryCount + PciMsixCapabilityParser.PendingBitArrayEntriesPerWord - 1) /
        PciMsixCapabilityParser.PendingBitArrayEntriesPerWord *
        PciMsixCapabilityParser.PendingBitArrayWordSize;

    public static PciMsixCapabilitySnapshot Missing =>
        new PciMsixCapabilitySnapshot(
            isPresent: false,
            tableEntryCount: 0,
            tableBarIndicator: PciBarDecoder.NoBarIndex,
            tableOffset: 0,
            pendingBitArrayBarIndicator: PciBarDecoder.NoBarIndex,
            pendingBitArrayOffset: 0,
            functionMasked: false,
            enabled: false);
}

public readonly struct PciMsixCapabilityParseResult
{
    private PciMsixCapabilityParseResult(
        bool isInitialized,
        PciMsixCapabilityParseStatus status,
        byte capabilityOffset,
        PciMsixCapabilitySnapshot capability)
    {
        IsInitialized = isInitialized;
        Status = status;
        CapabilityOffset = capabilityOffset;
        Capability = capability;
    }

    public PciMsixCapabilityParseStatus Status { get; }

    public byte CapabilityOffset { get; }

    public PciMsixCapabilitySnapshot Capability { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == PciMsixCapabilityParseStatus.Success;

    public static PciMsixCapabilityParseResult Success(
        byte capabilityOffset,
        PciMsixCapabilitySnapshot capability)
    {
        return new PciMsixCapabilityParseResult(
            isInitialized: true,
            PciMsixCapabilityParseStatus.Success,
            capabilityOffset,
            capability);
    }

    public static PciMsixCapabilityParseResult Fail(PciMsixCapabilityParseStatus status)
    {
        return new PciMsixCapabilityParseResult(
            isInitialized: true,
            status,
            capabilityOffset: 0,
            PciMsixCapabilitySnapshot.Missing);
    }
}

public static class PciMsixCapabilityParser
{
    public const byte CapabilityId = 0x11;
    public const int MinimumConfigurationSpaceLength = PciConfigurationHeader.Type0HeaderLength;
    public const byte MaximumCapabilityWalkCount = 48;
    public const ushort TableSizeMask = 0x07FF;
    public const ushort FunctionMaskBit = 0x4000;
    public const ushort EnableBit = 0x8000;
    public const uint BarIndicatorMask = 0x00000007;
    public const uint OffsetMask = 0xFFFFFFF8;
    public const uint TableEntrySize = 16;
    public const uint PendingBitArrayWordSize = 8;
    public const uint PendingBitArrayEntriesPerWord = 64;

    private const int CapabilityHeaderLength = 2;
    private const int MsixCapabilityLength = 12;
    private const int MessageControlOffset = 2;
    private const int TableOffset = 4;
    private const int PendingBitArrayOffset = 8;

    public static PciMsixCapabilityParseResult Find(
        ReadOnlySpan<byte> configurationSpace,
        byte capabilityPointer)
    {
        if (configurationSpace.Length < MinimumConfigurationSpaceLength)
        {
            return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.BufferTooSmall);
        }

        if (!IsValidCapabilityPointer(capabilityPointer))
        {
            return PciMsixCapabilityParseResult.Fail(
                PciMsixCapabilityParseStatus.InvalidCapabilityPointer);
        }

        byte current = capabilityPointer;
        byte walkCount = 0;
        while (current != 0)
        {
            if (walkCount >= MaximumCapabilityWalkCount)
            {
                return PciMsixCapabilityParseResult.Fail(
                    PciMsixCapabilityParseStatus.CapabilityLoopDetected);
            }

            if (!IsValidCapabilityPointer(current))
            {
                return PciMsixCapabilityParseResult.Fail(
                    PciMsixCapabilityParseStatus.InvalidNextCapabilityPointer);
            }

            if (current > configurationSpace.Length - CapabilityHeaderLength)
            {
                return PciMsixCapabilityParseResult.Fail(
                    PciMsixCapabilityParseStatus.TruncatedCapability);
            }

            byte capabilityId = configurationSpace[current];
            byte nextCapabilityPointer = configurationSpace[current + 1];
            if (capabilityId == CapabilityId)
            {
                return ParseMsix(configurationSpace, current);
            }

            if (nextCapabilityPointer == 0)
            {
                return PciMsixCapabilityParseResult.Fail(
                    PciMsixCapabilityParseStatus.CapabilityNotFound);
            }

            if (!IsValidCapabilityPointer(nextCapabilityPointer))
            {
                return PciMsixCapabilityParseResult.Fail(
                    PciMsixCapabilityParseStatus.InvalidNextCapabilityPointer);
            }

            current = nextCapabilityPointer;
            walkCount++;
        }

        return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.CapabilityNotFound);
    }

    private static PciMsixCapabilityParseResult ParseMsix(
        ReadOnlySpan<byte> configurationSpace,
        byte capabilityOffset)
    {
        if (capabilityOffset > configurationSpace.Length - MsixCapabilityLength)
        {
            return PciMsixCapabilityParseResult.Fail(
                PciMsixCapabilityParseStatus.TruncatedCapability);
        }

        ushort messageControl = EndianPrimitives.ReadUInt16LittleEndian(
            configurationSpace.Slice(capabilityOffset + MessageControlOffset, sizeof(ushort)));
        uint table = EndianPrimitives.ReadUInt32LittleEndian(
            configurationSpace.Slice(capabilityOffset + TableOffset, sizeof(uint)));
        uint pendingBitArray = EndianPrimitives.ReadUInt32LittleEndian(
            configurationSpace.Slice(capabilityOffset + PendingBitArrayOffset, sizeof(uint)));

        byte tableBarIndicator = (byte)(table & BarIndicatorMask);
        byte pendingBitArrayBarIndicator = (byte)(pendingBitArray & BarIndicatorMask);
        if (tableBarIndicator >= PciBarDecoder.BarCount ||
            pendingBitArrayBarIndicator >= PciBarDecoder.BarCount)
        {
            return PciMsixCapabilityParseResult.Fail(
                PciMsixCapabilityParseStatus.InvalidBarIndicator);
        }

        ushort tableEntryCount = (ushort)((messageControl & TableSizeMask) + 1);
        PciMsixCapabilitySnapshot capability = new PciMsixCapabilitySnapshot(
            isPresent: true,
            tableEntryCount,
            tableBarIndicator,
            table & OffsetMask,
            pendingBitArrayBarIndicator,
            pendingBitArray & OffsetMask,
            (messageControl & FunctionMaskBit) != 0,
            (messageControl & EnableBit) != 0);

        return PciMsixCapabilityParseResult.Success(capabilityOffset, capability);
    }

    private static bool IsValidCapabilityPointer(byte capabilityPointer)
    {
        if ((capabilityPointer & 0x03) != 0)
        {
            return false;
        }

        return capabilityPointer >= PciConfigurationHeader.FirstCapabilityPointer &&
            capabilityPointer <= PciConfigurationHeader.LastCapabilityPointer;
    }
}

public readonly struct PciBarApertureSet
{
    public PciBarApertureSet(
        ulong bar0Length,
        ulong bar1Length,
        ulong bar2Length,
        ulong bar3Length,
        ulong bar4Length,
        ulong bar5Length)
    {
        Bar0Length = bar0Length;
        Bar1Length = bar1Length;
        Bar2Length = bar2Length;
        Bar3Length = bar3Length;
        Bar4Length = bar4Length;
        Bar5Length = bar5Length;
    }

    public ulong Bar0Length { get; }

    public ulong Bar1Length { get; }

    public ulong Bar2Length { get; }

    public ulong Bar3Length { get; }

    public ulong Bar4Length { get; }

    public ulong Bar5Length { get; }

    public bool TryGet(byte index, out ulong length)
    {
        switch (index)
        {
            case 0:
                length = Bar0Length;
                return true;
            case 1:
                length = Bar1Length;
                return true;
            case 2:
                length = Bar2Length;
                return true;
            case 3:
                length = Bar3Length;
                return true;
            case 4:
                length = Bar4Length;
                return true;
            case 5:
                length = Bar5Length;
                return true;
            default:
                length = 0;
                return false;
        }
    }
}

public readonly struct EnaPciResourceSnapshot
{
    public EnaPciResourceSnapshot(
        int sourceIndex,
        PciEcamScannedDevice device,
        PciBarDescriptorSet bars,
        PciBarApertureSet apertures,
        PciMsixCapabilitySnapshot msix,
        byte registerBarIndex)
    {
        SourceIndex = sourceIndex;
        Device = device;
        Bars = bars;
        Apertures = apertures;
        Msix = msix;
        RegisterBarIndex = registerBarIndex;
    }

    public int SourceIndex { get; }

    public PciEcamScannedDevice Device { get; }

    public PciBarDescriptorSet Bars { get; }

    public PciBarApertureSet Apertures { get; }

    public PciMsixCapabilitySnapshot Msix { get; }

    public byte RegisterBarIndex { get; }
}

public enum EnaPciResourcePlanStatus
{
    Success = 0,
    CandidateValidationFailed = 1,
    InvalidRegisterBarIndex = 2,
    RegisterBarUnavailable = 3,
    RegisterBarIsIo = 4,
    RegisterBarIsMalformed = 5,
    RegisterBarBaseAddressZero = 6,
    RegisterBarApertureMissing = 7,
    RegisterBarApertureSizeNotPowerOfTwo = 8,
    RegisterBarApertureMisaligned = 9,
    RegisterBarApertureTooSmall = 10,
    MissingMsixCapability = 11,
    MsixTableBarInvalid = 12,
    MsixPbaBarInvalid = 13,
    MsixTableBarNotMemory = 14,
    MsixPbaBarNotMemory = 15,
    MsixTableApertureMissing = 16,
    MsixPbaApertureMissing = 17,
    MsixOffsetMisaligned = 18,
    MsixRegionOutOfRange = 19,
    MsixRegionsOverlap = 20
}

public readonly struct PciBarResourcePlan
{
    internal PciBarResourcePlan(
        byte barIndex,
        PciBarKind kind,
        ulong baseAddress,
        ulong apertureLength,
        bool isPrefetchable)
    {
        BarIndex = barIndex;
        Kind = kind;
        BaseAddress = baseAddress;
        ApertureLength = apertureLength;
        IsPrefetchable = isPrefetchable;
        EndAddressExclusive = baseAddress + apertureLength;
    }

    public byte BarIndex { get; }

    public PciBarKind Kind { get; }

    public ulong BaseAddress { get; }

    public ulong ApertureLength { get; }

    public bool IsPrefetchable { get; }

    public ulong EndAddressExclusive { get; }
}

public readonly struct PciMsixRegionPlan
{
    internal PciMsixRegionPlan(
        byte barIndex,
        uint offset,
        ulong length)
    {
        BarIndex = barIndex;
        Offset = offset;
        Length = length;
        EndOffsetExclusive = (ulong)offset + length;
    }

    public byte BarIndex { get; }

    public uint Offset { get; }

    public ulong Length { get; }

    public ulong EndOffsetExclusive { get; }
}

public readonly struct EnaPciResourcePlan
{
    internal EnaPciResourcePlan(
        int sourceIndex,
        EnaPciDeviceKind deviceKind,
        PciEcamScannedDevice device,
        PciBarResourcePlan registerBar,
        PciMsixCapabilitySnapshot msix,
        PciMsixRegionPlan msixTable,
        PciMsixRegionPlan msixPendingBitArray)
    {
        SourceIndex = sourceIndex;
        DeviceKind = deviceKind;
        Device = device;
        RegisterBar = registerBar;
        Msix = msix;
        MsixTable = msixTable;
        MsixPendingBitArray = msixPendingBitArray;
    }

    public int SourceIndex { get; }

    public EnaPciDeviceKind DeviceKind { get; }

    public PciEcamScannedDevice Device { get; }

    public PciBarResourcePlan RegisterBar { get; }

    public PciMsixCapabilitySnapshot Msix { get; }

    public PciMsixRegionPlan MsixTable { get; }

    public PciMsixRegionPlan MsixPendingBitArray { get; }
}

public readonly struct EnaPciResourcePlanResult
{
    private EnaPciResourcePlanResult(
        bool isInitialized,
        EnaPciResourcePlanStatus status,
        EnaPciCandidateValidationStatus candidateValidationStatus,
        byte failingBarIndex,
        EnaPciResourcePlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        CandidateValidationStatus = candidateValidationStatus;
        FailingBarIndex = failingBarIndex;
        Plan = plan;
    }

    public EnaPciResourcePlanStatus Status { get; }

    public EnaPciCandidateValidationStatus CandidateValidationStatus { get; }

    public byte FailingBarIndex { get; }

    public EnaPciResourcePlan Plan { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == EnaPciResourcePlanStatus.Success;

    internal static EnaPciResourcePlanResult Success(EnaPciResourcePlan plan)
    {
        return new EnaPciResourcePlanResult(
            isInitialized: true,
            EnaPciResourcePlanStatus.Success,
            EnaPciCandidateValidationStatus.Success,
            PciBarDecoder.NoBarIndex,
            plan);
    }

    internal static EnaPciResourcePlanResult Fail(
        EnaPciResourcePlanStatus status,
        EnaPciCandidateValidationStatus candidateValidationStatus,
        byte failingBarIndex)
    {
        return new EnaPciResourcePlanResult(
            isInitialized: true,
            status,
            candidateValidationStatus,
            failingBarIndex,
            default);
    }
}

public static class EnaPciResourcePlanner
{
    public const byte DefaultRegisterBarIndex = 0;
    public const ulong MinimumRegisterBarApertureLength =
        EnaRegisterMap.RequiredAdminReadinessRegisterApertureLength;

    public static EnaPciResourcePlanResult Plan(EnaPciResourceSnapshot snapshot)
    {
        EnaPciCandidateValidationResult candidateValidation =
            EnaPciCandidateSelector.Validate(snapshot.Device);
        if (!candidateValidation.Succeeded)
        {
            return EnaPciResourcePlanResult.Fail(
                EnaPciResourcePlanStatus.CandidateValidationFailed,
                candidateValidation.Status,
                PciBarDecoder.NoBarIndex);
        }

        EnaPciResourcePlanStatus registerStatus = ValidateRegisterBar(
            snapshot,
            out PciBarResourcePlan registerBar);
        if (registerStatus != EnaPciResourcePlanStatus.Success)
        {
            return EnaPciResourcePlanResult.Fail(
                registerStatus,
                EnaPciCandidateValidationStatus.Success,
                snapshot.RegisterBarIndex);
        }

        if (!snapshot.Msix.IsPresent)
        {
            return EnaPciResourcePlanResult.Fail(
                EnaPciResourcePlanStatus.MissingMsixCapability,
                EnaPciCandidateValidationStatus.Success,
                PciBarDecoder.NoBarIndex);
        }

        if (!TryCreateMsixRegion(
            snapshot,
            isTable: true,
            snapshot.Msix.TableBarIndicator,
            snapshot.Msix.TableOffset,
            snapshot.Msix.TableLengthBytes,
            out PciMsixRegionPlan msixTable,
            out EnaPciResourcePlanStatus msixStatus,
            out byte failingBarIndex))
        {
            return EnaPciResourcePlanResult.Fail(
                msixStatus,
                EnaPciCandidateValidationStatus.Success,
                failingBarIndex);
        }

        if (!TryCreateMsixRegion(
            snapshot,
            isTable: false,
            snapshot.Msix.PendingBitArrayBarIndicator,
            snapshot.Msix.PendingBitArrayOffset,
            snapshot.Msix.PendingBitArrayLengthBytes,
            out PciMsixRegionPlan msixPendingBitArray,
            out msixStatus,
            out failingBarIndex))
        {
            return EnaPciResourcePlanResult.Fail(
                msixStatus,
                EnaPciCandidateValidationStatus.Success,
                failingBarIndex);
        }

        if (msixTable.BarIndex == msixPendingBitArray.BarIndex &&
            RegionsOverlap(msixTable, msixPendingBitArray))
        {
            return EnaPciResourcePlanResult.Fail(
                EnaPciResourcePlanStatus.MsixRegionsOverlap,
                EnaPciCandidateValidationStatus.Success,
                msixTable.BarIndex);
        }

        EnaPciResourcePlan plan = new EnaPciResourcePlan(
            snapshot.SourceIndex,
            candidateValidation.DeviceKind,
            snapshot.Device,
            registerBar,
            snapshot.Msix,
            msixTable,
            msixPendingBitArray);

        return EnaPciResourcePlanResult.Success(plan);
    }

    private static EnaPciResourcePlanStatus ValidateRegisterBar(
        EnaPciResourceSnapshot snapshot,
        out PciBarResourcePlan registerBar)
    {
        registerBar = default;
        if (!snapshot.Bars.TryGet(snapshot.RegisterBarIndex, out PciBarDescriptor descriptor))
        {
            return EnaPciResourcePlanStatus.InvalidRegisterBarIndex;
        }

        if (descriptor.IsMalformed)
        {
            return EnaPciResourcePlanStatus.RegisterBarIsMalformed;
        }

        if (descriptor.IsIo)
        {
            return EnaPciResourcePlanStatus.RegisterBarIsIo;
        }

        if (!descriptor.IsMemory)
        {
            return EnaPciResourcePlanStatus.RegisterBarUnavailable;
        }

        if (descriptor.BaseAddressBits == 0)
        {
            return EnaPciResourcePlanStatus.RegisterBarBaseAddressZero;
        }

        if (!snapshot.Apertures.TryGet(snapshot.RegisterBarIndex, out ulong apertureLength) ||
            apertureLength == 0)
        {
            return EnaPciResourcePlanStatus.RegisterBarApertureMissing;
        }

        if (!IsPowerOfTwo(apertureLength))
        {
            return EnaPciResourcePlanStatus.RegisterBarApertureSizeNotPowerOfTwo;
        }

        if ((descriptor.BaseAddressBits % apertureLength) != 0)
        {
            return EnaPciResourcePlanStatus.RegisterBarApertureMisaligned;
        }

        if (apertureLength < MinimumRegisterBarApertureLength)
        {
            return EnaPciResourcePlanStatus.RegisterBarApertureTooSmall;
        }

        registerBar = new PciBarResourcePlan(
            descriptor.Index,
            descriptor.Kind,
            descriptor.BaseAddressBits,
            apertureLength,
            descriptor.IsPrefetchable);
        return EnaPciResourcePlanStatus.Success;
    }

    private static bool TryCreateMsixRegion(
        EnaPciResourceSnapshot snapshot,
        bool isTable,
        byte barIndex,
        uint offset,
        ulong length,
        out PciMsixRegionPlan region,
        out EnaPciResourcePlanStatus status,
        out byte failingBarIndex)
    {
        region = default;
        failingBarIndex = barIndex;

        if (!snapshot.Bars.TryGet(barIndex, out PciBarDescriptor descriptor))
        {
            status = isTable
                ? EnaPciResourcePlanStatus.MsixTableBarInvalid
                : EnaPciResourcePlanStatus.MsixPbaBarInvalid;
            return false;
        }

        if (!descriptor.IsMemory)
        {
            status = isTable
                ? EnaPciResourcePlanStatus.MsixTableBarNotMemory
                : EnaPciResourcePlanStatus.MsixPbaBarNotMemory;
            return false;
        }

        if (!snapshot.Apertures.TryGet(barIndex, out ulong apertureLength) ||
            apertureLength == 0)
        {
            status = isTable
                ? EnaPciResourcePlanStatus.MsixTableApertureMissing
                : EnaPciResourcePlanStatus.MsixPbaApertureMissing;
            return false;
        }

        if ((offset & (PciMsixCapabilityParser.PendingBitArrayWordSize - 1)) != 0)
        {
            status = EnaPciResourcePlanStatus.MsixOffsetMisaligned;
            return false;
        }

        if (!TryAdd(offset, length, out ulong endOffset) || endOffset > apertureLength)
        {
            status = EnaPciResourcePlanStatus.MsixRegionOutOfRange;
            return false;
        }

        region = new PciMsixRegionPlan(barIndex, offset, length);
        status = EnaPciResourcePlanStatus.Success;
        failingBarIndex = PciBarDecoder.NoBarIndex;
        return true;
    }

    private static bool RegionsOverlap(PciMsixRegionPlan left, PciMsixRegionPlan right)
    {
        return left.Offset < right.EndOffsetExclusive &&
            right.Offset < left.EndOffsetExclusive;
    }

    private static bool IsPowerOfTwo(ulong value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool TryAdd(
        ulong left,
        ulong right,
        out ulong result)
    {
        if (left > ulong.MaxValue - right)
        {
            result = 0;
            return false;
        }

        result = left + right;
        return true;
    }
}
