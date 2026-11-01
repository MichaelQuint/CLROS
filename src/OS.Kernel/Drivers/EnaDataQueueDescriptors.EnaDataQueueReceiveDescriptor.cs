namespace OS.Drivers;
using OS.Std.Binary;

public readonly struct EnaDataQueueReceiveDescriptor
{
    public EnaDataQueueReceiveDescriptor(ushort requestId, uint bufferLength, ulong physicalAddress, bool phase, bool first, bool last, bool completionRequested)
    {
        RequestId = requestId;
        BufferLength = bufferLength;
        PhysicalAddress = physicalAddress;
        Phase = phase;
        First = first;
        Last = last;
        CompletionRequested = completionRequested;
    }

    public ushort RequestId { get; }
    public uint BufferLength { get; }
    public ushort EncodedLength => BufferLength == EnaDataQueueDescriptors.RxEncodedZeroLengthBytes ? (ushort)0 : (ushort)BufferLength;
    public ulong PhysicalAddress { get; }
    public bool Phase { get; }
    public bool First { get; }
    public bool Last { get; }
    public bool CompletionRequested { get; }
    public uint AddressLow => (uint)PhysicalAddress;
    public ushort AddressHigh => (ushort)(PhysicalAddress >> 32);
}
