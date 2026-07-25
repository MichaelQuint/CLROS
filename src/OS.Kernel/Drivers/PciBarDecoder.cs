namespace OS.Drivers;

public enum PciBarKind
{
    Absent = 0,
    Io = 1,
    Memory32 = 2,
    Memory64 = 3,
    ConsumedByPrevious64BitBar = 4,
    Malformed = 5
}

public enum PciBarDescriptorStatus
{
    Success = 0,
    Absent = 1,
    ConsumedByPrevious64BitBar = 2,
    ReservedMemoryType = 3,
    Memory64AtLastIndex = 4
}

public enum PciBarDecodeStatus
{
    Success = 0,
    ReservedMemoryType = 1,
    Memory64AtLastIndex = 2
}

public readonly struct PciBarDescriptor
{
    public PciBarDescriptor(
        byte index,
        PciBarKind kind,
        PciBarDescriptorStatus status,
        uint rawValue,
        uint rawUpperValue,
        ulong baseAddressBits,
        bool isPrefetchable,
        bool consumesNextSlot,
        byte pairBarIndex)
    {
        Index = index;
        Kind = kind;
        Status = status;
        RawValue = rawValue;
        RawUpperValue = rawUpperValue;
        BaseAddressBits = baseAddressBits;
        IsPrefetchable = isPrefetchable;
        ConsumesNextSlot = consumesNextSlot;
        PairBarIndex = pairBarIndex;
    }

    public byte Index { get; }

    public PciBarKind Kind { get; }

    public PciBarDescriptorStatus Status { get; }

    public uint RawValue { get; }

    public uint RawUpperValue { get; }

    public ulong BaseAddressBits { get; }

    public bool IsPrefetchable { get; }

    public bool ConsumesNextSlot { get; }

    public byte PairBarIndex { get; }

    public bool IsPresent =>
        Kind == PciBarKind.Io ||
        Kind == PciBarKind.Memory32 ||
        Kind == PciBarKind.Memory64;

    public bool IsMemory =>
        Kind == PciBarKind.Memory32 ||
        Kind == PciBarKind.Memory64;

    public bool IsIo => Kind == PciBarKind.Io;

    public bool IsMalformed => Kind == PciBarKind.Malformed;
}

public readonly struct PciBarDescriptorSet
{
    public PciBarDescriptorSet(
        PciBarDescriptor bar0,
        PciBarDescriptor bar1,
        PciBarDescriptor bar2,
        PciBarDescriptor bar3,
        PciBarDescriptor bar4,
        PciBarDescriptor bar5)
    {
        Bar0 = bar0;
        Bar1 = bar1;
        Bar2 = bar2;
        Bar3 = bar3;
        Bar4 = bar4;
        Bar5 = bar5;
    }

    public PciBarDescriptor Bar0 { get; }

    public PciBarDescriptor Bar1 { get; }

    public PciBarDescriptor Bar2 { get; }

    public PciBarDescriptor Bar3 { get; }

    public PciBarDescriptor Bar4 { get; }

    public PciBarDescriptor Bar5 { get; }

    public bool TryGet(byte index, out PciBarDescriptor descriptor)
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

    public bool TryCopyTo(Span<PciBarDescriptor> destination)
    {
        if (destination.Length < PciBarDecoder.BarCount)
        {
            return false;
        }

        destination[0] = Bar0;
        destination[1] = Bar1;
        destination[2] = Bar2;
        destination[3] = Bar3;
        destination[4] = Bar4;
        destination[5] = Bar5;
        return true;
    }
}

public readonly struct PciBarDecodeResult
{
    private PciBarDecodeResult(
        bool isInitialized,
        PciBarDecodeStatus status,
        PciBarDescriptorSet bars,
        byte failingBarIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        Bars = bars;
        FailingBarIndex = failingBarIndex;
    }

    public bool IsInitialized { get; }

    public PciBarDecodeStatus Status { get; }

    public PciBarDescriptorSet Bars { get; }

    public byte FailingBarIndex { get; }

    public bool Succeeded => IsInitialized && Status == PciBarDecodeStatus.Success;

    public static PciBarDecodeResult Create(
        PciBarDecodeStatus status,
        PciBarDescriptorSet bars,
        byte failingBarIndex)
    {
        return new PciBarDecodeResult(true, status, bars, failingBarIndex);
    }
}

public static class PciBarDecoder
{
    public const byte BarCount = 6;
    public const byte NoBarIndex = 0xFF;
    public const uint IoSpaceIndicator = 0x00000001;
    public const uint MemoryPrefetchableBit = 0x00000008;
    public const uint IoBaseAddressMask = 0xFFFFFFFC;
    public const uint MemoryBaseAddressMask = 0xFFFFFFF0;

    private const byte MemoryType32 = 0;
    private const byte MemoryType64 = 2;

    public static PciBarDecodeResult Decode(PciType0ConfigurationHeader header)
    {
        PciBarDescriptor bar0 = DecodeSlot(header, 0, out bool bar0ConsumesNext);
        PciBarDescriptor bar1;
        PciBarDescriptor bar2;
        PciBarDescriptor bar3;
        PciBarDescriptor bar4;
        PciBarDescriptor bar5;

        bool bar1ConsumesNext = false;
        bool skipNext2 = false;
        bool skipNext3 = false;
        bool skipNext4 = false;

        if (bar0ConsumesNext)
        {
            bar1 = DecodeConsumedSlot(header, 1, bar0);
        }
        else
        {
            bar1 = DecodeSlot(header, 1, out bar1ConsumesNext);
        }

        if (bar0ConsumesNext)
        {
            bar2 = DecodeSlot(header, 2, out skipNext2);
        }
        else if (bar1ConsumesNext)
        {
            bar2 = DecodeConsumedSlot(header, 2, bar1);
        }
        else
        {
            bar2 = DecodeSlot(header, 2, out skipNext2);
        }

        if (skipNext2)
        {
            bar3 = DecodeConsumedSlot(header, 3, bar2);
        }
        else
        {
            bar3 = DecodeSlot(header, 3, out skipNext3);
        }

        if (skipNext3)
        {
            bar4 = DecodeConsumedSlot(header, 4, bar3);
        }
        else
        {
            bar4 = DecodeSlot(header, 4, out skipNext4);
        }

        if (skipNext4)
        {
            bar5 = DecodeConsumedSlot(header, 5, bar4);
        }
        else
        {
            bar5 = DecodeSlot(header, 5, out _);
        }

        PciBarDescriptorSet bars = new PciBarDescriptorSet(
            bar0,
            bar1,
            bar2,
            bar3,
            bar4,
            bar5);

        PciBarDecodeStatus status = GetResultStatus(bars, out byte failingBarIndex);
        return PciBarDecodeResult.Create(status, bars, failingBarIndex);
    }

    private static PciBarDescriptor DecodeSlot(
        PciType0ConfigurationHeader header,
        byte index,
        out bool consumesNextSlot)
    {
        consumesNextSlot = false;
        uint rawValue = ReadRawBar(header, index);
        if (rawValue == 0)
        {
            return CreateAbsent(index);
        }

        if ((rawValue & IoSpaceIndicator) != 0)
        {
            return new PciBarDescriptor(
                index,
                PciBarKind.Io,
                PciBarDescriptorStatus.Success,
                rawValue,
                rawUpperValue: 0,
                rawValue & IoBaseAddressMask,
                isPrefetchable: false,
                consumesNextSlot: false,
                NoBarIndex);
        }

        byte memoryType = (byte)((rawValue >> 1) & 0x03);
        if (memoryType == MemoryType32)
        {
            return new PciBarDescriptor(
                index,
                PciBarKind.Memory32,
                PciBarDescriptorStatus.Success,
                rawValue,
                rawUpperValue: 0,
                rawValue & MemoryBaseAddressMask,
                (rawValue & MemoryPrefetchableBit) != 0,
                consumesNextSlot: false,
                NoBarIndex);
        }

        if (memoryType == MemoryType64)
        {
            if (index == BarCount - 1)
            {
                return CreateMalformed(index, rawValue, PciBarDescriptorStatus.Memory64AtLastIndex);
            }

            uint rawUpperValue = ReadRawBar(header, (byte)(index + 1));
            ulong baseAddressBits =
                ((ulong)rawUpperValue << 32) |
                (rawValue & MemoryBaseAddressMask);
            consumesNextSlot = true;
            return new PciBarDescriptor(
                index,
                PciBarKind.Memory64,
                PciBarDescriptorStatus.Success,
                rawValue,
                rawUpperValue,
                baseAddressBits,
                (rawValue & MemoryPrefetchableBit) != 0,
                consumesNextSlot: true,
                (byte)(index + 1));
        }

        return CreateMalformed(index, rawValue, PciBarDescriptorStatus.ReservedMemoryType);
    }

    private static PciBarDescriptor DecodeConsumedSlot(
        PciType0ConfigurationHeader header,
        byte index,
        PciBarDescriptor pairBaseDescriptor)
    {
        return new PciBarDescriptor(
            index,
            PciBarKind.ConsumedByPrevious64BitBar,
            PciBarDescriptorStatus.ConsumedByPrevious64BitBar,
            ReadRawBar(header, index),
            rawUpperValue: 0,
            pairBaseDescriptor.BaseAddressBits,
            pairBaseDescriptor.IsPrefetchable,
            consumesNextSlot: false,
            pairBaseDescriptor.Index);
    }

    private static PciBarDescriptor CreateAbsent(byte index)
    {
        return new PciBarDescriptor(
            index,
            PciBarKind.Absent,
            PciBarDescriptorStatus.Absent,
            rawValue: 0,
            rawUpperValue: 0,
            baseAddressBits: 0,
            isPrefetchable: false,
            consumesNextSlot: false,
            NoBarIndex);
    }

    private static PciBarDescriptor CreateMalformed(
        byte index,
        uint rawValue,
        PciBarDescriptorStatus status)
    {
        return new PciBarDescriptor(
            index,
            PciBarKind.Malformed,
            status,
            rawValue,
            rawUpperValue: 0,
            rawValue & MemoryBaseAddressMask,
            (rawValue & MemoryPrefetchableBit) != 0,
            consumesNextSlot: false,
            NoBarIndex);
    }

    private static PciBarDecodeStatus GetResultStatus(
        PciBarDescriptorSet bars,
        out byte failingBarIndex)
    {
        if (TryGetMalformedStatus(bars.Bar0, out PciBarDecodeStatus status))
        {
            failingBarIndex = 0;
            return status;
        }

        if (TryGetMalformedStatus(bars.Bar1, out status))
        {
            failingBarIndex = 1;
            return status;
        }

        if (TryGetMalformedStatus(bars.Bar2, out status))
        {
            failingBarIndex = 2;
            return status;
        }

        if (TryGetMalformedStatus(bars.Bar3, out status))
        {
            failingBarIndex = 3;
            return status;
        }

        if (TryGetMalformedStatus(bars.Bar4, out status))
        {
            failingBarIndex = 4;
            return status;
        }

        if (TryGetMalformedStatus(bars.Bar5, out status))
        {
            failingBarIndex = 5;
            return status;
        }

        failingBarIndex = NoBarIndex;
        return PciBarDecodeStatus.Success;
    }

    private static bool TryGetMalformedStatus(
        PciBarDescriptor descriptor,
        out PciBarDecodeStatus status)
    {
        if (descriptor.Status == PciBarDescriptorStatus.ReservedMemoryType)
        {
            status = PciBarDecodeStatus.ReservedMemoryType;
            return true;
        }

        if (descriptor.Status == PciBarDescriptorStatus.Memory64AtLastIndex)
        {
            status = PciBarDecodeStatus.Memory64AtLastIndex;
            return true;
        }

        status = PciBarDecodeStatus.Success;
        return false;
    }

    private static uint ReadRawBar(PciType0ConfigurationHeader header, byte index)
    {
        switch (index)
        {
            case 0:
                return header.Bar0;
            case 1:
                return header.Bar1;
            case 2:
                return header.Bar2;
            case 3:
                return header.Bar3;
            case 4:
                return header.Bar4;
            case 5:
                return header.Bar5;
            default:
                return 0;
        }
    }
}
