namespace OS.Drivers;

using OS.Std.Binary;

public readonly struct EnaDataQueueReceiveCompletion
{
    internal EnaDataQueueReceiveCompletion(uint status, ushort length, ushort requestId, uint hash, ushort submissionQueueId, byte offset)
    {
        Status = status;
        Length = length;
        RequestId = requestId;
        Hash = hash;
        SubmissionQueueId = submissionQueueId;
        Offset = offset;
    }

    public uint Status
    {
        get;
    }
    public ushort Length
    {
        get;
    }
    public ushort RequestId
    {
        get;
    }
    public uint Hash
    {
        get;
    }
    public ushort SubmissionQueueId
    {
        get;
    }
    public byte Offset
    {
        get;
    }
    public byte L3ProtocolIndex => (byte)(Status & EnaDataQueueDescriptors.RxCompletionL3ProtocolMask);
    public byte SourceVlanCount => (byte)((Status & EnaDataQueueDescriptors.RxCompletionSourceVlanCountMask) >> EnaDataQueueDescriptors.RxCompletionSourceVlanCountShift);
    public byte L4ProtocolIndex => (byte)((Status & EnaDataQueueDescriptors.RxCompletionL4ProtocolMask) >> EnaDataQueueDescriptors.RxCompletionL4ProtocolShift);
    public bool HasL3ChecksumError => (Status & EnaDataQueueDescriptors.RxCompletionL3ChecksumErrorFlag) != 0;
    public bool HasL4ChecksumError => (Status & EnaDataQueueDescriptors.RxCompletionL4ChecksumErrorFlag) != 0;
    public bool IsIpv4Fragment => (Status & EnaDataQueueDescriptors.RxCompletionIpv4FragmentFlag) != 0;
    public bool L4ChecksumChecked => (Status & EnaDataQueueDescriptors.RxCompletionL4ChecksumCheckedFlag) != 0;
    public bool Phase => (Status & EnaDataQueueDescriptors.RxCompletionPhaseFlag) != 0;
    public bool First => (Status & EnaDataQueueDescriptors.RxCompletionFirstFlag) != 0;
    public bool Last => (Status & EnaDataQueueDescriptors.RxCompletionLastFlag) != 0;
    public bool Buffer => (Status & EnaDataQueueDescriptors.RxCompletionBufferFlag) != 0;
}
