namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed class WebRaceTemporalConnection(ServiceQuotaSocketIdentity identity, int description)
    {
        internal readonly ServiceQuotaSocketIdentity Identity = identity;
        internal readonly int Description = description;
        internal readonly List<WebSignalRaceSocketCall> Reads = [], Writes = [];
        internal ulong Sent, Received;
    }
}
