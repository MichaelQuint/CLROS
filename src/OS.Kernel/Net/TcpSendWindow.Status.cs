namespace OS.Net;

public static unsafe partial class TcpSendWindow
{
    public enum Status : byte
    {
        Uninitialized,
        Success,
        InvalidStorage,
        InvalidState,
        InvalidOptions,
        TableRejected,
        InvalidPacket,
        InvalidAck,
        OldAck,
        DuplicateAck,
        WindowUpdated,
        NoData,
        NothingToSend,
        WindowClosed,
        OutputTooSmall,
        WouldBlock,
        SendFailed,
        AwaitingAck,
        NotDue,
        Timeout,
        InvalidClock,
        WriteClosed,
        WaitingForDrain,
        PeerClosed,
        ConnectionClosed,
        ConnectionReset
    }
}
