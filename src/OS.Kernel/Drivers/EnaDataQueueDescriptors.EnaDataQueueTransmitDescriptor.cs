namespace OS.Drivers;

using OS.Std.Binary;

public readonly struct EnaDataQueueTransmitDescriptor
{
    public EnaDataQueueTransmitDescriptor(ushort requestId, uint length, ulong physicalAddress, bool phase, bool first, bool last, bool completionRequested, byte headerLength)
    {
        RequestId = requestId;
        Length = length;
        PhysicalAddress = physicalAddress;
        Phase = phase;
        First = first;
        Last = last;
        CompletionRequested = completionRequested;
        HeaderLength = headerLength;
    }

    public ushort RequestId
    {
        get;
    }
    public uint Length
    {
        get;
    }
    public ulong PhysicalAddress
    {
        get;
    }
    public bool Phase
    {
        get;
    }
    public bool First
    {
        get;
    }
    public bool Last
    {
        get;
    }
    public bool CompletionRequested
    {
        get;
    }
    public byte HeaderLength
    {
        get;
    }
    public ushort RequestIdLow => (ushort)(RequestId & EnaDataQueueDescriptors.TxRequestIdLowMask);
    public byte RequestIdHigh => (byte)(RequestId >> EnaDataQueueDescriptors.TxRequestIdLowBitCount);
    public uint AddressLow => (uint)PhysicalAddress;
    public ushort AddressHigh => (ushort)(PhysicalAddress >> 32);
}
