using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct PciMsixCapabilitySnapshot
{
    public PciMsixCapabilitySnapshot(bool isPresent, ushort tableEntryCount, byte tableBarIndicator, uint tableOffset, byte pendingBitArrayBarIndicator, uint pendingBitArrayOffset, bool functionMasked, bool enabled)
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

    public bool IsPresent
    {
        get;
    }
    public ushort TableEntryCount
    {
        get;
    }
    public byte TableBarIndicator
    {
        get;
    }
    public uint TableOffset
    {
        get;
    }
    public byte PendingBitArrayBarIndicator
    {
        get;
    }
    public uint PendingBitArrayOffset
    {
        get;
    }
    public bool FunctionMasked
    {
        get;
    }
    public bool Enabled
    {
        get;
    }
    public ulong TableLengthBytes => (ulong)TableEntryCount * PciMsixCapabilityParser.TableEntrySize;
    public ulong PendingBitArrayLengthBytes => ((ulong)TableEntryCount + PciMsixCapabilityParser.PendingBitArrayEntriesPerWord - 1) / PciMsixCapabilityParser.PendingBitArrayEntriesPerWord * PciMsixCapabilityParser.PendingBitArrayWordSize;
    public static PciMsixCapabilitySnapshot Missing => new PciMsixCapabilitySnapshot(isPresent: false, tableEntryCount: 0, tableBarIndicator: PciBarDecoder.NoBarIndex, tableOffset: 0, pendingBitArrayBarIndicator: PciBarDecoder.NoBarIndex, pendingBitArrayOffset: 0, functionMasked: false, enabled: false);
}
