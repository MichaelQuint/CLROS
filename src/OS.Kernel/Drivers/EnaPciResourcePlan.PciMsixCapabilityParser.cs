using OS.Std.Binary;

namespace OS.Drivers;
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
    public static PciMsixCapabilityParseResult Find(ReadOnlySpan<byte> configurationSpace, byte capabilityPointer)
    {
        if (configurationSpace.Length < MinimumConfigurationSpaceLength)
        {
            return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.BufferTooSmall);
        }

        if (!IsValidCapabilityPointer(capabilityPointer))
        {
            return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.InvalidCapabilityPointer);
        }

        byte current = capabilityPointer;
        byte walkCount = 0;
        while (current != 0)
        {
            if (walkCount >= MaximumCapabilityWalkCount)
            {
                return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.CapabilityLoopDetected);
            }

            if (!IsValidCapabilityPointer(current))
            {
                return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.InvalidNextCapabilityPointer);
            }

            if (current > configurationSpace.Length - CapabilityHeaderLength)
            {
                return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.TruncatedCapability);
            }

            byte capabilityId = configurationSpace[current];
            byte nextCapabilityPointer = configurationSpace[current + 1];
            if (capabilityId == CapabilityId)
            {
                return ParseMsix(configurationSpace, current);
            }

            if (nextCapabilityPointer == 0)
            {
                return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.CapabilityNotFound);
            }

            if (!IsValidCapabilityPointer(nextCapabilityPointer))
            {
                return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.InvalidNextCapabilityPointer);
            }

            current = nextCapabilityPointer;
            walkCount++;
        }

        return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.CapabilityNotFound);
    }

    private static PciMsixCapabilityParseResult ParseMsix(ReadOnlySpan<byte> configurationSpace, byte capabilityOffset)
    {
        if (capabilityOffset > configurationSpace.Length - MsixCapabilityLength)
        {
            return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.TruncatedCapability);
        }

        ushort messageControl = EndianPrimitives.ReadUInt16LittleEndian(configurationSpace.Slice(capabilityOffset + MessageControlOffset, sizeof(ushort)));
        uint table = EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(capabilityOffset + TableOffset, sizeof(uint)));
        uint pendingBitArray = EndianPrimitives.ReadUInt32LittleEndian(configurationSpace.Slice(capabilityOffset + PendingBitArrayOffset, sizeof(uint)));
        byte tableBarIndicator = (byte)(table & BarIndicatorMask);
        byte pendingBitArrayBarIndicator = (byte)(pendingBitArray & BarIndicatorMask);
        if (tableBarIndicator >= PciBarDecoder.BarCount || pendingBitArrayBarIndicator >= PciBarDecoder.BarCount)
        {
            return PciMsixCapabilityParseResult.Fail(PciMsixCapabilityParseStatus.InvalidBarIndicator);
        }

        ushort tableEntryCount = (ushort)((messageControl & TableSizeMask) + 1);
        PciMsixCapabilitySnapshot capability = new PciMsixCapabilitySnapshot(isPresent: true, tableEntryCount, tableBarIndicator, table & OffsetMask, pendingBitArrayBarIndicator, pendingBitArray & OffsetMask, (messageControl & FunctionMaskBit) != 0, (messageControl & EnableBit) != 0);
        return PciMsixCapabilityParseResult.Success(capabilityOffset, capability);
    }

    private static bool IsValidCapabilityPointer(byte capabilityPointer)
    {
        if ((capabilityPointer & 0x03) != 0)
        {
            return false;
        }

        return capabilityPointer >= PciConfigurationHeader.FirstCapabilityPointer && capabilityPointer <= PciConfigurationHeader.LastCapabilityPointer;
    }
}
