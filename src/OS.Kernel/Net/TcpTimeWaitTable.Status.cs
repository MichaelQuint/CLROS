namespace OS.Net;

public static unsafe partial class TcpTimeWaitTable
{
    public enum Status : byte
    {
        Success,
        InvalidStorage,
        InvalidState,
        InvalidRecord,
        InvalidClock,
        DuplicateEndpoint,
        Full,
        NotFound,
        StaleHandle,
        GenerationExhausted,
        Expired,
        RecordsRemain
    }
}
