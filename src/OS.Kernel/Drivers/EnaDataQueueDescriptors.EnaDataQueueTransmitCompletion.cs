namespace OS.Drivers;
using OS.Std.Binary;

public readonly struct EnaDataQueueTransmitCompletion
{
    internal EnaDataQueueTransmitCompletion(ushort requestId, byte status, byte flags, ushort submissionQueueId, ushort submissionQueueHeadIndex)
    {
        IsInitialized = true;
        RequestId = requestId;
        Status = status;
        Flags = flags;
        SubmissionQueueId = submissionQueueId;
        SubmissionQueueHeadIndex = submissionQueueHeadIndex;
    }

    public bool IsInitialized { get; }
    public ushort RequestId { get; }
    public byte Status { get; }
    public byte Flags { get; }
    public bool Phase => (Flags & EnaDataQueueDescriptors.CompletionPhaseFlag) != 0;
    public ushort SubmissionQueueId { get; }
    public ushort SubmissionQueueHeadIndex { get; }
}
