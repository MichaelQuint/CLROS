namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied native-CALL coverage only. Counts measure application
    /// acceptance/consumption, not packet delivery, memory use or cleanup.</summary>

    public sealed record WebSignalRaceTemporalInspection(int SocketCalls, ulong RequestBytesConsumed, ulong ResponseBytesAccepted, IReadOnlyList<WebSignalRaceHttpSyscallInterval> Intervals);
}
