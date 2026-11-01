namespace OS.Net;
public static unsafe partial class TcpConnectionTable
{
    public enum OwnershipStatus : byte
    {
        Uninitialized,
        Success,
        InvalidStorage,
        InvalidState,
        InvalidEndpoint,
        InvalidSequence,
        DuplicateEndpoint,
        Full,
        NotFound,
        StaleHandle,
        InvalidTransition,
        GenerationExhausted,
        CorruptState,
        ConnectionsRemain,
        AttachmentsRemain
    }
}
