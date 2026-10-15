namespace OS.Tools.QemuRunner;

/// <summary>
/// Bounds startup separately from the workload. Poll with monotonic milliseconds since child
/// launch and its serial parser. Only a complete firmware-exit marker observed before the deadline
/// disarms it; an expired deadline stays failed even if late output arrives. Access is exclusive.
/// </summary>
public sealed class QemuStartupDeadline
{
    public const string ReadyMarker = "M03_EBS_RETURNED_SUCCESS";
    private readonly long deadline;
    private bool completed, expired;

    public QemuStartupDeadline(int seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seconds);
        deadline = seconds * 1000L;
    }

    public bool HasExpired(long elapsedMilliseconds, SerialMarkerStateMachine markers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMilliseconds);
        ArgumentNullException.ThrowIfNull(markers);
        if (completed) return false;
        if (expired || elapsedMilliseconds >= deadline) return expired = true;
        completed = markers.HasSeenRequiredMarker(ReadyMarker);
        return false;
    }
}
