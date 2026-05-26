namespace OS.Drivers;

public enum PciBarSizingPlanStatus
{
    Success = 0,
    SourceTooShort = 1,
    DescriptorMalformed = 2,
    MalformedReadback = 3,
    BaseAlignmentMismatch = 4
}

public enum PciBarSizeDescriptorStatus
{
    Success = 0,
    Absent = 1,
    IoBarNotSized = 2,
    ConsumedByPrevious64BitBar = 3,
    DescriptorMalformed = 4,
    SourceTooShort = 5,
    MalformedReadback = 6,
    BaseAlignmentMismatch = 7
}

public readonly struct PciBarSizeDescriptor
{
    public PciBarSizeDescriptor(
        byte index,
        PciBarKind kind,
        PciBarSizeDescriptorStatus status,
        uint rawReadback,
        uint rawUpperReadback,
        ulong baseAddressBits,
        ulong sizeBytes,
        bool isPrefetchable,
        bool consumesNextSlot,
        byte pairBarIndex)
        : this(
            index,
            kind,
            status,
            rawReadback,
            rawUpperReadback,
            baseAddressBits,
            sizeBytes,
            isPrefetchable,
            consumesNextSlot,
            pairBarIndex,
            isInitialized: true)
    {
    }

    private PciBarSizeDescriptor(
        byte index,
        PciBarKind kind,
        PciBarSizeDescriptorStatus status,
        uint rawReadback,
        uint rawUpperReadback,
        ulong baseAddressBits,
        ulong sizeBytes,
        bool isPrefetchable,
        bool consumesNextSlot,
        byte pairBarIndex,
        bool isInitialized)
    {
        Index = index;
        Kind = kind;
        Status = status;
        RawReadback = rawReadback;
        RawUpperReadback = rawUpperReadback;
        BaseAddressBits = baseAddressBits;
        SizeBytes = sizeBytes;
        IsPrefetchable = isPrefetchable;
        ConsumesNextSlot = consumesNextSlot;
        PairBarIndex = pairBarIndex;
        IsInitialized = isInitialized;
    }

    public byte Index { get; }

    public PciBarKind Kind { get; }

    public PciBarSizeDescriptorStatus Status { get; }

    public uint RawReadback { get; }

    public uint RawUpperReadback { get; }

    public ulong BaseAddressBits { get; }

    public ulong SizeBytes { get; }

    public bool IsPrefetchable { get; }

    public bool ConsumesNextSlot { get; }

    public byte PairBarIndex { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == PciBarSizeDescriptorStatus.Success;
}

public readonly struct PciBarSizeDescriptorSet
{
    public PciBarSizeDescriptorSet(
        PciBarSizeDescriptor bar0,
        PciBarSizeDescriptor bar1,
        PciBarSizeDescriptor bar2,
        PciBarSizeDescriptor bar3,
        PciBarSizeDescriptor bar4,
        PciBarSizeDescriptor bar5)
    {
        Bar0 = bar0;
        Bar1 = bar1;
        Bar2 = bar2;
        Bar3 = bar3;
        Bar4 = bar4;
        Bar5 = bar5;
    }

    public PciBarSizeDescriptor Bar0 { get; }

    public PciBarSizeDescriptor Bar1 { get; }

    public PciBarSizeDescriptor Bar2 { get; }

    public PciBarSizeDescriptor Bar3 { get; }

    public PciBarSizeDescriptor Bar4 { get; }

    public PciBarSizeDescriptor Bar5 { get; }

    public bool TryGet(byte index, out PciBarSizeDescriptor descriptor)
    {
        switch (index)
        {
            case 0:
                descriptor = Bar0;
                return true;
            case 1:
                descriptor = Bar1;
                return true;
            case 2:
                descriptor = Bar2;
                return true;
            case 3:
                descriptor = Bar3;
                return true;
            case 4:
                descriptor = Bar4;
                return true;
            case 5:
                descriptor = Bar5;
                return true;
            default:
                descriptor = default;
                return false;
        }
    }

    public bool TryCopySizesTo(Span<ulong> destination)
    {
        if (destination.Length < PciBarDecoder.BarCount)
        {
            return false;
        }

        destination[0] = Bar0.SizeBytes;
        destination[1] = Bar1.SizeBytes;
        destination[2] = Bar2.SizeBytes;
        destination[3] = Bar3.SizeBytes;
        destination[4] = Bar4.SizeBytes;
        destination[5] = Bar5.SizeBytes;
        return true;
    }
}

public readonly struct PciBarSizingPlanResult
{
    private PciBarSizingPlanResult(
        PciBarSizingPlanStatus status,
        byte failingBarIndex,
        PciBarSizeDescriptorStatus failingDescriptorStatus,
        PciBarSizeDescriptorSet bars,
        bool isInitialized)
    {
        Status = status;
        FailingBarIndex = failingBarIndex;
        FailingDescriptorStatus = failingDescriptorStatus;
        Bars = bars;
        IsInitialized = isInitialized;
    }

    public PciBarSizingPlanStatus Status { get; }

    public byte FailingBarIndex { get; }

    public PciBarSizeDescriptorStatus FailingDescriptorStatus { get; }

    public PciBarSizeDescriptorSet Bars { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == PciBarSizingPlanStatus.Success;

    public bool TryCopySizesTo(Span<ulong> destination)
    {
        return Succeeded && Bars.TryCopySizesTo(destination);
    }

    public static PciBarSizingPlanResult Create(
        PciBarSizingPlanStatus status,
        byte failingBarIndex,
        PciBarSizeDescriptorStatus failingDescriptorStatus,
        PciBarSizeDescriptorSet bars)
    {
        return new PciBarSizingPlanResult(
            status,
            failingBarIndex,
            failingDescriptorStatus,
            bars,
            isInitialized: true);
    }
}

public static class PciBarSizingPlanner
{
    public const byte RequiredReadbackCount = PciBarDecoder.BarCount;
    public const bool RequiresCallerOwnedOriginalBarSnapshot = true;
    public const bool RequiresCallerOwnedSizingReadbacks = true;
    public const bool ProducesVirtioApertureValidatorInput = true;
    public const bool PerformsLiveConfigWrites = false;
    public const bool PerformsLiveConfigReads = false;
    public const bool RestoresBars = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsBlockIo = false;

    public static PciBarSizingPlanResult Plan(
        PciBarDescriptorSet bars,
        ReadOnlySpan<uint> sizingReadbacks)
    {
        if (sizingReadbacks.Length < RequiredReadbackCount)
        {
            PciBarSizeDescriptorSet shortResult = CreateSet(
                CreateFailureDescriptor(0, default, PciBarSizeDescriptorStatus.SourceTooShort, 0, 0),
                default,
                default,
                default,
                default,
                default);

            return PciBarSizingPlanResult.Create(
                PciBarSizingPlanStatus.SourceTooShort,
                failingBarIndex: 0,
                PciBarSizeDescriptorStatus.SourceTooShort,
                shortResult);
        }

        PciBarSizeDescriptor bar0 = PlanOne(bars.Bar0, sizingReadbacks, out PciBarSizingPlanStatus status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar0);
        }

        PciBarSizeDescriptor bar1 = PlanOne(bars.Bar1, sizingReadbacks, out status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar1, bar0);
        }

        PciBarSizeDescriptor bar2 = PlanOne(bars.Bar2, sizingReadbacks, out status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar2, bar0, bar1);
        }

        PciBarSizeDescriptor bar3 = PlanOne(bars.Bar3, sizingReadbacks, out status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar3, bar0, bar1, bar2);
        }

        PciBarSizeDescriptor bar4 = PlanOne(bars.Bar4, sizingReadbacks, out status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar4, bar0, bar1, bar2, bar3);
        }

        PciBarSizeDescriptor bar5 = PlanOne(bars.Bar5, sizingReadbacks, out status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar5, bar0, bar1, bar2, bar3, bar4);
        }

        return PciBarSizingPlanResult.Create(
            PciBarSizingPlanStatus.Success,
            PciBarDecoder.NoBarIndex,
            PciBarSizeDescriptorStatus.Success,
            CreateSet(bar0, bar1, bar2, bar3, bar4, bar5));
    }

    private static PciBarSizeDescriptor PlanOne(
        PciBarDescriptor descriptor,
        ReadOnlySpan<uint> sizingReadbacks,
        out PciBarSizingPlanStatus status)
    {
        status = PciBarSizingPlanStatus.Success;
        uint rawReadback = sizingReadbacks[descriptor.Index];

        switch (descriptor.Kind)
        {
            case PciBarKind.Absent:
                return CreateDescriptor(
                    descriptor,
                    PciBarSizeDescriptorStatus.Absent,
                    rawReadback,
                    rawUpperReadback: 0,
                    sizeBytes: 0);

            case PciBarKind.Io:
                return CreateDescriptor(
                    descriptor,
                    PciBarSizeDescriptorStatus.IoBarNotSized,
                    rawReadback,
                    rawUpperReadback: 0,
                    sizeBytes: 0);

            case PciBarKind.ConsumedByPrevious64BitBar:
                return CreateDescriptor(
                    descriptor,
                    PciBarSizeDescriptorStatus.ConsumedByPrevious64BitBar,
                    rawReadback,
                    rawUpperReadback: 0,
                    sizeBytes: 0);

            case PciBarKind.Memory32:
                return PlanMemory32(descriptor, rawReadback, out status);

            case PciBarKind.Memory64:
                return PlanMemory64(descriptor, sizingReadbacks, rawReadback, out status);

            default:
                status = PciBarSizingPlanStatus.DescriptorMalformed;
                return CreateDescriptor(
                    descriptor,
                    PciBarSizeDescriptorStatus.DescriptorMalformed,
                    rawReadback,
                    rawUpperReadback: 0,
                    sizeBytes: 0);
        }
    }

    private static PciBarSizeDescriptor PlanMemory32(
        PciBarDescriptor descriptor,
        uint rawReadback,
        out PciBarSizingPlanStatus status)
    {
        uint readbackFlags = rawReadback & ~PciBarDecoder.MemoryBaseAddressMask;
        uint descriptorFlags = descriptor.RawValue & ~PciBarDecoder.MemoryBaseAddressMask;
        uint addressMask = rawReadback & PciBarDecoder.MemoryBaseAddressMask;
        ulong size = unchecked((uint)(~addressMask + 1U));
        if (readbackFlags != descriptorFlags ||
            addressMask == 0 ||
            !IsPowerOfTwo(size))
        {
            status = PciBarSizingPlanStatus.MalformedReadback;
            return CreateDescriptor(
                descriptor,
                PciBarSizeDescriptorStatus.MalformedReadback,
                rawReadback,
                rawUpperReadback: 0,
                sizeBytes: 0);
        }

        if (!IsAligned(descriptor.BaseAddressBits, size))
        {
            status = PciBarSizingPlanStatus.BaseAlignmentMismatch;
            return CreateDescriptor(
                descriptor,
                PciBarSizeDescriptorStatus.BaseAlignmentMismatch,
                rawReadback,
                rawUpperReadback: 0,
                sizeBytes: size);
        }

        status = PciBarSizingPlanStatus.Success;
        return CreateDescriptor(
            descriptor,
            PciBarSizeDescriptorStatus.Success,
            rawReadback,
            rawUpperReadback: 0,
            size);
    }

    private static PciBarSizeDescriptor PlanMemory64(
        PciBarDescriptor descriptor,
        ReadOnlySpan<uint> sizingReadbacks,
        uint rawReadback,
        out PciBarSizingPlanStatus status)
    {
        uint rawUpperReadback = sizingReadbacks[descriptor.PairBarIndex];
        uint readbackFlags = rawReadback & ~PciBarDecoder.MemoryBaseAddressMask;
        uint descriptorFlags = descriptor.RawValue & ~PciBarDecoder.MemoryBaseAddressMask;
        ulong addressMask = ((ulong)rawUpperReadback << 32) |
            (rawReadback & PciBarDecoder.MemoryBaseAddressMask);
        ulong size = unchecked(~addressMask + 1UL);
        if (readbackFlags != descriptorFlags ||
            addressMask == 0 ||
            size == 0 ||
            !IsPowerOfTwo(size))
        {
            status = PciBarSizingPlanStatus.MalformedReadback;
            return CreateDescriptor(
                descriptor,
                PciBarSizeDescriptorStatus.MalformedReadback,
                rawReadback,
                rawUpperReadback,
                sizeBytes: 0);
        }

        if (!IsAligned(descriptor.BaseAddressBits, size))
        {
            status = PciBarSizingPlanStatus.BaseAlignmentMismatch;
            return CreateDescriptor(
                descriptor,
                PciBarSizeDescriptorStatus.BaseAlignmentMismatch,
                rawReadback,
                rawUpperReadback,
                size);
        }

        status = PciBarSizingPlanStatus.Success;
        return CreateDescriptor(
            descriptor,
            PciBarSizeDescriptorStatus.Success,
            rawReadback,
            rawUpperReadback,
            size);
    }

    private static bool IsPowerOfTwo(ulong value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool IsAligned(ulong value, ulong alignment)
    {
        return alignment != 0 && (value & (alignment - 1)) == 0;
    }

    private static PciBarSizeDescriptor CreateDescriptor(
        PciBarDescriptor descriptor,
        PciBarSizeDescriptorStatus status,
        uint rawReadback,
        uint rawUpperReadback,
        ulong sizeBytes)
    {
        return new PciBarSizeDescriptor(
            descriptor.Index,
            descriptor.Kind,
            status,
            rawReadback,
            rawUpperReadback,
            descriptor.BaseAddressBits,
            sizeBytes,
            descriptor.IsPrefetchable,
            descriptor.ConsumesNextSlot,
            descriptor.PairBarIndex);
    }

    private static PciBarSizeDescriptor CreateFailureDescriptor(
        byte index,
        PciBarKind kind,
        PciBarSizeDescriptorStatus status,
        uint rawReadback,
        uint rawUpperReadback)
    {
        return new PciBarSizeDescriptor(
            index,
            kind,
            status,
            rawReadback,
            rawUpperReadback,
            baseAddressBits: 0,
            sizeBytes: 0,
            isPrefetchable: false,
            consumesNextSlot: false,
            PciBarDecoder.NoBarIndex);
    }

    private static PciBarSizingPlanResult CreateFailure(
        PciBarSizingPlanStatus status,
        PciBarSizeDescriptor failing,
        PciBarSizeDescriptor bar0 = default,
        PciBarSizeDescriptor bar1 = default,
        PciBarSizeDescriptor bar2 = default,
        PciBarSizeDescriptor bar3 = default,
        PciBarSizeDescriptor bar4 = default)
    {
        PciBarSizeDescriptorSet set = CreateSet(
            failing.Index == 0 ? failing : bar0,
            failing.Index == 1 ? failing : bar1,
            failing.Index == 2 ? failing : bar2,
            failing.Index == 3 ? failing : bar3,
            failing.Index == 4 ? failing : bar4,
            failing.Index == 5 ? failing : default);

        return PciBarSizingPlanResult.Create(
            status,
            failing.Index,
            failing.Status,
            set);
    }

    private static PciBarSizeDescriptorSet CreateSet(
        PciBarSizeDescriptor bar0,
        PciBarSizeDescriptor bar1,
        PciBarSizeDescriptor bar2,
        PciBarSizeDescriptor bar3,
        PciBarSizeDescriptor bar4,
        PciBarSizeDescriptor bar5)
    {
        return new PciBarSizeDescriptorSet(
            bar0,
            bar1,
            bar2,
            bar3,
            bar4,
            bar5);
    }
}
