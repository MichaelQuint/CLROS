namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied backing of the pinned service runtime's dedicated GC
    /// arena. Counts only still-owned arena pages; bookkeeping elsewhere remains
    /// non-GC conservatively. Supplied rows confer no source, image, allocation
    /// or guest authority. The parent binds the original image and startup.</summary>

    public readonly record struct ServiceGcBackingInspection(bool ArenaIdentified, ulong ArenaStart, ulong ArenaBytes, ulong LastBackedBytes, ulong PeakBackedBytes, int RevokedPages, int ReservationCallPosition);
}
