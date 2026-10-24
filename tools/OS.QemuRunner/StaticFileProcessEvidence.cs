using System.Globalization;

namespace OS.Tools.QemuRunner;

/// <summary>Runner-owned lifetime of its configured emulator or capture child.
/// A capture wrapper PID is not relabeled as the underlying QEMU PID. Native
/// capture validation supplies that separate provenance. This receipt proves
/// neither guest process exit nor guest device release.</summary>
public sealed class StaticFileProcessEvidence
{
    public const int SummaryCount = 12;
    internal int? ProcessId, ExitCode;
    internal string? StartUtc, EndUtc, Failure;
    internal long? LifetimeMilliseconds;
    internal bool PreLaunchAvailable, TerminationRequested, DeathConfirmed, SpontaneousExit, CleanupFailed;
    internal bool? StoppedEndpointRefused;

    /// <summary>Copy current child observations into canonical invariant rows.
    /// Missing observations remain explicit; the consumer must reject them.
    /// No process is launched, queried or retained by formatting.</summary>
    public IReadOnlyList<string> Summary()
    {
        string Bit(bool value)=>value?"true":"false";
        string Number(long? value)=>value?.ToString(CultureInfo.InvariantCulture)??"none";
        return Array.AsReadOnly(new[]{
            "STATIC_CHILD_ID "+Number(ProcessId),
            "STATIC_CHILD_START_UTC "+(StartUtc??"none"),
            "STATIC_CHILD_END_UTC "+(EndUtc??"none"),
            "STATIC_CHILD_LIFETIME_MS "+Number(LifetimeMilliseconds),
            "STATIC_ENDPOINT_PRELAUNCH_AVAILABLE "+Bit(PreLaunchAvailable),
            "STATIC_ENDPOINT_STOPPED_REFUSED "+(StoppedEndpointRefused is bool refused?Bit(refused):"not_run"),
            "STATIC_CHILD_TERMINATION_REQUESTED "+Bit(TerminationRequested),
            "STATIC_CHILD_DEATH_CONFIRMED "+Bit(DeathConfirmed),
            "STATIC_CHILD_SPONTANEOUS "+Bit(SpontaneousExit),
            "STATIC_CHILD_CLEANUP_FAILED "+Bit(CleanupFailed),
            "STATIC_CHILD_EXIT_CODE "+Number(ExitCode),
            "STATIC_CHILD_FAILURE "+(Failure??"none")});
    }
}
