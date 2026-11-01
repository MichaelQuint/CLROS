namespace OS.Drivers;
public struct EthernetFrameReceiveResult
{
    public VirtioNetQueueResult Queue;
    public VirtioNetReceiveFrameClassificationResult Classification;
    public ushort ReceiveUsedIndex;
    public uint ReceiveUsedId, ReceiveUsedLength, CapturedBytes;
    public readonly bool Succeeded => Queue.Succeeded && Queue.TransmitCompleted && CapturedBytes == ReceiveUsedLength && CapturedBytes != 0 && Classification.Succeeded;
}
