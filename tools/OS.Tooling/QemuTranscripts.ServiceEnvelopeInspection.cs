using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Value-only outer wire, lifetime and cleanup accounting. Does not
    /// establish independent packets, runner ownership or guest qualification.</summary>

    public readonly record struct ServiceEnvelopeInspection(ulong Received, ulong Arp, ulong Accepted, ulong Ignored, ulong Capacity, ulong ArpCopied, ulong TcpCopied, ulong Polls, ulong Fresh, ulong Retired, ulong Started, ulong Ended, ulong MaximumDeadline);
}
