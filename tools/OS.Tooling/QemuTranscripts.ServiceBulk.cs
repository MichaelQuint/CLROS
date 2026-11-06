namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed class ServiceBulk(Dictionary<string, ulong> identity)
    {
        internal readonly Dictionary<string, ulong> Identity = identity;
        internal ulong Deadline, Duration;
        internal bool Transferred, Retired, LastClosed;
    }
}
