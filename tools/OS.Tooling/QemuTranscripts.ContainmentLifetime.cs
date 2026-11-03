namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed class ContainmentLifetime(Dictionary<string, ulong> first)
    {
        internal readonly Dictionary<string, ulong> First = first;
        internal ulong Retired, Timeout;
    }
}
