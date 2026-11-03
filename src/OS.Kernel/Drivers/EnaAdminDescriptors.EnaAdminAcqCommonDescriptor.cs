namespace OS.Drivers;

using OS.Std.Binary;

public readonly struct EnaAdminAcqCommonDescriptor
{
    public EnaAdminAcqCommonDescriptor(ushort rawCommand, byte status, byte flags, ushort extendedStatus, ushort submissionQueueHeadIndex)
    {
        RawCommand = rawCommand;
        Status = status;
        Flags = flags;
        ExtendedStatus = extendedStatus;
        SubmissionQueueHeadIndex = submissionQueueHeadIndex;
    }

    public ushort RawCommand
    {
        get;
    }
    public ushort CommandId => (ushort)(RawCommand & EnaAdminDescriptors.CommandIdMask);
    public byte Status
    {
        get;
    }
    public EnaAdminCompletionStatus CompletionStatus => (EnaAdminCompletionStatus)Status;
    public byte Flags
    {
        get;
    }
    public bool Phase => (Flags & EnaAdminDescriptors.PhaseFlag) != 0;
    public ushort ExtendedStatus
    {
        get;
    }
    public ushort SubmissionQueueHeadIndex
    {
        get;
    }
}
