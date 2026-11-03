namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied fresh SLOT event0 identity. RowIndex is zero-based in the
    /// supplied normalized row array, not a syscall ordinal or raw serial byte.
    /// Addresses/ports and all counters retain original decimal value units;
    /// Clock/Deadline/Duration are guest nanoseconds. A pending SYN reservation
    /// is not an accepted socket or completed HTTP exchange. Repeated ports in
    /// distinct generations are retained; no host/PCAP/clock mapping is implied.</summary>

    public readonly record struct ServiceQuotaTransportLifetime(int RowIndex, ulong Slot, ulong Owner, ulong Epoch, ulong Generation, ulong Index, ulong Local, ulong LocalPort, ulong Remote, ulong RemotePort, ulong Clock, ulong Deadline, ulong Duration);
}
