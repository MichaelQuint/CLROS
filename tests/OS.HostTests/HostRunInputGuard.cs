// One run owns this guard. Checks are serialized with the failure latch so all
// workers observe the first invalidation. Cancellation uses the existing bounded
// process cleanup and scheduler drain; this class never terminates a process.
internal sealed class HostRunInputGuard(Func<bool> unchanged, CancellationTokenSource cancellation, Action<string> invalidate)
{
    private readonly object sync = new();
    internal string? Failure { get; private set; }

    internal bool Check(string context, Func<bool>? exact = null)
    {
        lock (sync)
        {
            if (Failure is not null) return false;
            string? failure = null;
            try
            {
                if (!unchanged() || exact is not null && !exact()) failure = "inputs_changed " + context;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                failure = "input_validation_failed " + context + " " + error.GetType().Name;
            }
            if (failure is null) return true;
            Failure = failure;
            cancellation.Cancel();
            invalidate(failure);
            return false;
        }
    }
}
