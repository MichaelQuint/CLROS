namespace OS.Net;
public static unsafe partial class TcpPassiveConnection
{
    public enum Status : byte
    {
        Uninitialized,
        Success,
        InvalidStorage,
        InvalidState,
        InvalidOptions,
        InvalidClock,
        InvalidPacket,
        OutputTooSmall,
        WouldBlock,
        SendFailed,
        TableRejected,
        BacklogRejected,
        NotDue,
        RetryLimit,
        NotSent,
        Timeout
    }
}
