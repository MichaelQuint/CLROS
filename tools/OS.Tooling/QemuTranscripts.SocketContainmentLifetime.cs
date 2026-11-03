namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Original handle/tuple lifetime observed in the containment case.
    /// Times are network-owner monotonic nanoseconds, not host PCAP timestamps.</summary>

    public readonly record struct SocketContainmentLifetime(ulong Slot, ulong Owner, ulong Epoch, ulong Generation, ulong Index, ulong RemotePort, ulong Created, ulong Retired, ulong Timeout);
}
