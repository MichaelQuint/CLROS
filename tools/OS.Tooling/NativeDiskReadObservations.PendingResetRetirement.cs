using System.Globalization;

namespace OS.Tooling;
public static partial class NativeDiskReadObservations
{
    /// <summary>Copied values from a separately authenticated actual Stop
    /// receipt. Only one last submitted, uncompleted request may be retired by
    /// reset. These values confer no device, source, process or guest authority.</summary>

    public readonly record struct PendingResetRetirement(ulong Owner, ulong Epoch, ulong Generation, ulong Sector, ulong SubmittedBeforeStop, ulong CompletedBeforeStop, ulong CopiedBeforeStop, ulong CancelledBeforeStop, OS.Storage.ContinuingReadOnlyBlockService.Status StopStatus, uint ResetReads, bool ResetComplete, bool BusMasterDisabled, bool DmaReleased, ulong PoolMask);
}
