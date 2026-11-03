namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied required HTTP bounds and their complete original syscall
    /// coverage. ConnectionId remains host-session local; Connection is the
    /// native guest tuple/lifetime. Their association comes from the supplied
    /// strict byte-based stream join, never host/guest port equality or clocks.</summary>

    public sealed record ServiceQuotaHttpSyscallInterval(int ExchangeId, int ConnectionId, ServiceQuotaSocketIdentity Connection, bool Response, string Kind, int Start, int End, IReadOnlyList<ServiceQuotaHttpSyscallSlice> Slices);
}
