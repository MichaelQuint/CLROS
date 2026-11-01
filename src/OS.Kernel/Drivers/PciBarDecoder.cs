namespace OS.Drivers;
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

        PciBarDescriptorSet bars = new PciBarDescriptorSet(bar0, bar1, bar2, bar3, bar4, bar5);
        PciBarDecodeStatus status = GetResultStatus(bars, out byte failingBarIndex);
        return PciBarDecodeResult.Create(status, bars, failingBarIndex);
    }

    private static PciBarDescriptor DecodeSlot(PciType0ConfigurationHeader header, byte index, out bool consumesNextSlot)
    {
        consumesNextSlot = false;
        uint rawValue = ReadRawBar(header, index);
        if (rawValue == 0)
        {
            return CreateAbsent(index);
        }

        if ((rawValue & IoSpaceIndicator) != 0)
        {
            return new PciBarDescriptor(index, PciBarKind.Io, PciBarDescriptorStatus.Success, rawValue, rawUpperValue: 0, rawValue & IoBaseAddressMask, isPrefetchable: false, consumesNextSlot: false, NoBarIndex);
        }

        byte memoryType = (byte)((rawValue >> 1) & 0x03);
        if (memoryType == MemoryType32)
        {
            return new PciBarDescriptor(index, PciBarKind.Memory32, PciBarDescriptorStatus.Success, rawValue, rawUpperValue: 0, rawValue & MemoryBaseAddressMask, (rawValue & MemoryPrefetchableBit) != 0, consumesNextSlot: false, NoBarIndex);
        }

        if (memoryType == MemoryType64)
        {
            if (index == BarCount - 1)
            {
                return CreateMalformed(index, rawValue, PciBarDescriptorStatus.Memory64AtLastIndex);
            }

            uint rawUpperValue = ReadRawBar(header, (byte)(index + 1));
            ulong baseAddressBits = ((ulong)rawUpperValue << 32) | (rawValue & MemoryBaseAddressMask);
            consumesNextSlot = true;
            return new PciBarDescriptor(index, PciBarKind.Memory64, PciBarDescriptorStatus.Success, rawValue, rawUpperValue, baseAddressBits, (rawValue & MemoryPrefetchableBit) != 0, consumesNextSlot: true, (byte)(index + 1));
        }

        return CreateMalformed(index, rawValue, PciBarDescriptorStatus.ReservedMemoryType);
    }

    private static PciBarDescriptor DecodeConsumedSlot(PciType0ConfigurationHeader header, byte index, PciBarDescriptor pairBaseDescriptor)
    {
        return new PciBarDescriptor(index, PciBarKind.ConsumedByPrevious64BitBar, PciBarDescriptorStatus.ConsumedByPrevious64BitBar, ReadRawBar(header, index), rawUpperValue: 0, pairBaseDescriptor.BaseAddressBits, pairBaseDescriptor.IsPrefetchable, consumesNextSlot: false, pairBaseDescriptor.Index);
    }

    private static PciBarDescriptor CreateAbsent(byte index)
    {
        return new PciBarDescriptor(index, PciBarKind.Absent, PciBarDescriptorStatus.Absent, rawValue: 0, rawUpperValue: 0, baseAddressBits: 0, isPrefetchable: false, consumesNextSlot: false, NoBarIndex);
    }

    private static PciBarDescriptor CreateMalformed(byte index, uint rawValue, PciBarDescriptorStatus status)
    {
        return new PciBarDescriptor(index, PciBarKind.Malformed, status, rawValue, rawUpperValue: 0, rawValue & MemoryBaseAddressMask, (rawValue & MemoryPrefetchableBit) != 0, consumesNextSlot: false, NoBarIndex);
    }

    private static PciBarDecodeStatus GetResultStatus(PciBarDescriptorSet bars, out byte failingBarIndex)
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

    private static bool TryGetMalformedStatus(PciBarDescriptor descriptor, out PciBarDecodeStatus status)
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
