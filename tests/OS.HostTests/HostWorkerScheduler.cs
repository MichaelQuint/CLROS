internal static class HostWorkerScheduler
{
    public static async Task<bool> RunPhasesAsync<T>(IReadOnlyList<T> items, Func<T, bool> native,
        int jobs, Func<T, Task> run, Func<bool> fastPassed, CancellationToken cancellation = default)
    {
        await RunAsync(items.Where(item => !native(item)).ToArray(), _ => false,
            jobs, (_, item) => run(item), cancellation);
        if (cancellation.IsCancellationRequested)
            return false;
        T[] remaining = items.Where(native).ToArray();
        if (remaining.Length != 0 && !fastPassed())
            return false;
        await RunAsync(remaining, _ => true, jobs, (_, item) => run(item), cancellation);
        return !cancellation.IsCancellationRequested;
    }

    // Native fixtures mutate the canonical publish tree. They remain exclusive;
    // fast fixtures run in separate processes and own any scratch directories.
    public static async Task RunAsync<T>(IReadOnlyList<T> items, Func<T, bool> exclusive,
        int jobs, Func<int, T, Task> run, CancellationToken cancellation = default)
    {
        if (jobs is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(jobs));
        List<Task> active = [];
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (cancellation.IsCancellationRequested)
                    break;
                T item = items[i];
                if (exclusive(item))
                {
                    await Task.WhenAll(active);
                    active.Clear();
                    if (!cancellation.IsCancellationRequested)
                        await run(i, item);
                    continue;
                }
                if (active.Count == jobs)
                {
                    Task finished = await Task.WhenAny(active);
                    await finished;
                    active.Remove(finished);
                }
                if (!cancellation.IsCancellationRequested)
                    active.Add(run(i, item));
            }
        }
        finally
        {
            // Receipt/launch failures must not release the native barrier while
            // another worker is still alive. Each worker owns its bounded cleanup.
            await Task.WhenAll(active);
        }
    }
}
