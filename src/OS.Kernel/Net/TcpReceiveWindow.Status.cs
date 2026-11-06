namespace OS.Net;

public static unsafe partial class TcpReceiveWindow
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
        Duplicate,
        OutOfOrder,
        WindowFull,
        NoPayload,
        NoData,
        OutputTooSmall,
        NothingToAck,
        WouldBlock,
        SendFailed,
        PeerClosed,
        EndOfStream
    }
}
