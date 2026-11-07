namespace OS.Tools.QemuRunner;

public sealed partial class ServiceSocketControls
{
    /// <summary>Original guest packet ordinals for the explicit32-byte MSS
    /// flight control. The independent full-PCAP consumer verifies the copies,
    /// partial/duplicate/stale ACK injections and exact retransmitted suffix.
    /// These copied observations grant no guest or process authority alone.</summary>
    public sealed record Flight(long First, long Timeout, long NextFirst, long NextSecond, long Fast, long Suffix);
}
