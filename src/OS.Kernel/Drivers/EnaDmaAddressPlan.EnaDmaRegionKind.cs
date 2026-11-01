namespace OS.Drivers;
public enum EnaDmaRegionKind
{
    None = 0,
    Generic = 1,
    ContiguousAdminQueueMemory = 2,
    AdminSubmissionQueue = 3,
    AdminCompletionQueue = 4,
    TxRing = 5,
    RxRing = 6,
    PacketBuffer = 7
}
