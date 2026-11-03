using System.Diagnostics;

// Separate upstream managed workload; producer and platform evidence are still
// required. Draining app-owned operations never means runtime workers shut down.
internal static partial class Program
{
    private const int Lifetimes = 3, WorkItems = 8, TimeoutSeconds = 20;
    [ThreadStatic]
    private static bool s_publishing;
    private static int Main(string[] args)
    {
        Console.WriteLine("TEST_START nativeaot.tp-stress");
        if (args.Length != 0)
        {
            Console.WriteLine("TEST_FAIL nativeaot.tp-stress invalid_arguments");
            return 1;
        }

        try
        {
            RunAsync().GetAwaiter().GetResult();
            Console.WriteLine("TEST_PASS nativeaot.tp-stress");
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL nativeaot.tp-stress " + error.GetType().Name + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    private static async Task RunAsync()
    {
        for (int lifetime = 1; lifetime <= Lifetimes; lifetime++)
        {
            await WorkAsync(lifetime).ConfigureAwait(false);
            await CancellationAsync(lifetime).ConfigureAwait(false);
            await PublicationAsync(lifetime).ConfigureAwait(false);
            await TimerAsync(lifetime).ConfigureAwait(false);
        }
    }

    private static async Task WorkAsync(int lifetime)
    {
        long stage = Stopwatch.GetTimestamp();
        WorkState state = new();
        using SemaphoreSlim slots = new(2, 2);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<(int Square, int Worker)>[] tasks = new Task<(int, int)>[WorkItems];
        for (int i = 0; i < tasks.Length; i++)
        {
            int index = i;
            tasks[i] = Task.Run(async () =>
            {
                await slots.WaitAsync().ConfigureAwait(false);
                int active = Interlocked.Increment(ref state.Active);
                try
                {
                    Require(Thread.CurrentThread.IsThreadPoolThread && active is >= 1 and <= 2, "work_entry");
                    int previous;
                    do
                    {
                        previous = Volatile.Read(ref state.Maximum);
                        if (previous >= active)
                            break;
                    }
                    while (Interlocked.CompareExchange(ref state.Maximum, active, previous) != previous);
                    if (active == 2)
                        state.Staged.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    Require(Thread.CurrentThread.IsThreadPoolThread, "work_resume");
                    int bit = 1 << index;
                    Require((Interlocked.Or(ref state.Mask, bit) & bit) == 0, "work_duplicate");
                    Interlocked.Increment(ref state.Completed);
                    return (Square: (index + 1) * (index + 1), Worker: Environment.CurrentManagedThreadId);
                }
                finally
                {
                    Interlocked.Decrement(ref state.Active);
                    slots.Release();
                }
            });
        }

        var all = Task.WhenAll(tasks);
        (int Square, int Worker)[] results = [];
        try
        {
            await state.Staged.Task.WaitAsync(Remaining(stage)).ConfigureAwait(false);
            Require(Volatile.Read(ref state.Active) == 2, "work_overlap");
            release.SetResult(true);
            results = await all.WaitAsync(Remaining(stage)).ConfigureAwait(false);
        }
        finally
        {
            release.TrySetResult(true);
            if (!all.IsCompletedSuccessfully)
                await all.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds)).ConfigureAwait(false);
        }

        int checksum = 0;
        HashSet<int> workers = [];
        for (int i = 0; i < results.Length; i++)
        {
            Require(results[i].Square == (i + 1) * (i + 1) && results[i].Worker > 0, "work_result");
            checksum += results[i].Square;
            workers.Add(results[i].Worker);
        }

        Require(state.Completed == 8 && state.Mask == 255 && state.Maximum == 2 && state.Active == 0 && slots.CurrentCount == 2 && checksum == 204, "work_drained");
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_WORK lifetime={lifetime} tasks=8 completed={state.Completed} checksum={checksum} activeMax={state.Maximum} inFlight={state.Active} mask={state.Mask} workers={workers.Count} threadPool=true"));
    }

    private static async Task CancellationAsync(int lifetime)
    {
        long stage = Stopwatch.GetTimestamp();
        int before = 0, during = 0, callbacks = 0;
        using SemaphoreSlim semaphore = new(0, 1);
        using (CancellationTokenSource canceled = new())
        {
            canceled.Cancel();
            Task work = Task.Run(() => Interlocked.Increment(ref callbacks), canceled.Token);
            Task wait = semaphore.WaitAsync(canceled.Token);
            await Canceled(work, canceled.Token, stage).ConfigureAwait(false);
            before++;
            await Canceled(wait, canceled.Token, stage).ConfigureAwait(false);
            before++;
        }

        using (CancellationTokenSource live = new())
        {
            Task wait = semaphore.WaitAsync(live.Token);
            Require(!wait.IsCompleted, "live_wait");
            live.Cancel();
            await Canceled(wait, live.Token, stage).ConfigureAwait(false);
            during++;
        }

        using (CancellationTokenSource live = new())
        {
            Task delay = Task.Delay(TimeSpan.FromSeconds(10), live.Token);
            Require(!delay.IsCompleted, "live_delay");
            live.Cancel();
            await Canceled(delay, live.Token, stage).ConfigureAwait(false);
            during++;
        }

        semaphore.Release();
        Require(await semaphore.WaitAsync(Remaining(stage)).ConfigureAwait(false), "wait_recovered");
        Require(semaphore.CurrentCount == 0 && before == 2 && during == 2 && callbacks == 0, "cancellation_inventory");
        semaphore.Release();
        Require(semaphore.CurrentCount == 1, "wait_released");
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_CANCEL lifetime={lifetime} before={before} during={during} workCallbacks={callbacks} recovered=1"));
    }

    private static async Task Canceled(Task task, CancellationToken token, long stage)
    {
        bool canceled = false;
        try
        {
            await task.WaitAsync(Remaining(stage)).ConfigureAwait(false);
        }
        catch (OperationCanceledException error)
        {
            Require(error.CancellationToken == token, "cancellation_token");
            canceled = true;
        }

        Require(canceled && task.IsCanceled, "cancellation_state");
    }

    private static async Task PublicationAsync(int lifetime)
    {
        long stage = Stopwatch.GetTimestamp();
        PublicationState state = new();
        CancellationToken token = new(true);
        TaskCompletionSource<int> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task before = Attach(source.Task, state, 0);
        Task racing = gate.Task.ContinueWith(_ => Attach(source.Task, state, 1), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
        Task result = gate.Task.ContinueWith(_ => Publish(false), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        Task cancel = gate.Task.ContinueWith(_ => Publish(true), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        void Publish(bool canceled)
        {
            Require(Thread.CurrentThread.IsThreadPoolThread, "publication_worker");
            s_publishing = true;
            try
            {
                if (canceled ? source.TrySetCanceled(token) : source.TrySetResult(43))
                    Interlocked.Increment(ref state.Winners);
            }
            finally
            {
                s_publishing = false;
            }
        }

        Require(!source.Task.IsCompleted && !before.IsCompleted && !racing.IsCompleted, "publication_staged");
        gate.SetResult(true);
        await Task.WhenAll(result, cancel, racing, before).WaitAsync(Remaining(stage)).ConfigureAwait(false);
        int value = 0;
        string outcome;
        if (source.Task.IsCanceled)
        {
            await Canceled(source.Task, token, stage).ConfigureAwait(false);
            outcome = "cancel";
        }
        else
        {
            value = await source.Task.WaitAsync(Remaining(stage)).ConfigureAwait(false);
            Require(value == 43, "publication_result");
            outcome = "result";
        }

        int duplicates = 0;
        if (!source.TrySetResult(47))
            duplicates++;
        if (!source.TrySetCanceled(token))
            duplicates++;
        await Task.Run(() => Attach(source.Task, state, 2)).WaitAsync(Remaining(stage)).ConfigureAwait(false);
        Require(state.Winners == 1 && duplicates == 2 && state.Count == 3 && state.Inline == 0 && state.Positions.All(n => n == 1), "publication_inventory");
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_RACE lifetime={lifetime} outcome={outcome} winners={state.Winners} duplicates={duplicates} continuations={state.Count} inline={state.Inline} value={value}"));
    }

    private static Task Attach(Task<int> source, PublicationState state, int position) => source.ContinueWith(task =>
    {
        if (s_publishing)
            Interlocked.Increment(ref state.Inline);
        Require(!s_publishing && Thread.CurrentThread.IsThreadPoolThread && (task.IsCompletedSuccessfully || task.IsCanceled), "continuation_context");
        Require(Interlocked.Increment(ref state.Positions[position]) == 1, "continuation_duplicate");
        Interlocked.Increment(ref state.Count);
    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    private static async Task TimerAsync(int lifetime)
    {
        long stage = Stopwatch.GetTimestamp();
        int never = 0;
        Timer inactive = new(_ => Interlocked.Increment(ref never), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        await inactive.DisposeAsync().AsTask().WaitAsync(Remaining(stage)).ConfigureAwait(false);
        TimerState state = new(Stopwatch.GetTimestamp());
        Timer timer = new(Callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Task? drain = null;
        try
        {
            Require(timer.Change(TimeSpan.FromMilliseconds(50), Timeout.InfiniteTimeSpan), "timer_change");
            long fired = await state.Entered.Task.WaitAsync(Remaining(stage)).ConfigureAwait(false);
            drain = timer.DisposeAsync().AsTask();
            Require(!drain.IsCompleted, "timer_pending_drain");
            state.Release.Set();
            await drain.WaitAsync(Remaining(stage)).ConfigureAwait(false);
            Volatile.Write(ref state.Disposed, 1);
            long drained = Stopwatch.GetTimestamp();
            await Task.Delay(TimeSpan.FromMilliseconds(50)).WaitAsync(Remaining(stage)).ConfigureAwait(false);
            long after = Stopwatch.GetTimestamp();
            Elapsed(state.Before, fired, "timer_due");
            Elapsed(drained, after, "timer_observation");
            Require(Volatile.Read(ref state.Failure) is null && state.Callbacks == 1 && state.Completed == 1 && state.Stale == 0 && Volatile.Read(ref never) == 0, "timer_drained");
            Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_TIMER lifetime={lifetime} before={state.Before} fired={fired} drained={drained} after={after} frequency={Stopwatch.Frequency} callbacks={state.Callbacks} completed={state.Completed} stale={state.Stale} never={never} pendingDrain=true"));
        }
        finally
        {
            state.Release.Set();
            drain ??= timer.DisposeAsync().AsTask();
            await drain.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds)).ConfigureAwait(false);
            state.Release.Dispose();
        }
    }

    private static void Callback(object? value)
    {
        TimerState state = (TimerState)value!;
        try
        {
            if (Volatile.Read(ref state.Disposed) != 0)
                Interlocked.Increment(ref state.Stale);
            Require(Interlocked.Increment(ref state.Callbacks) == 1 && Thread.CurrentThread.IsThreadPoolThread, "timer_callback");
            long fired = Stopwatch.GetTimestamp();
            Elapsed(state.Before, fired, "timer_callback_due");
            Require(state.Entered.TrySetResult(fired), "timer_entered");
            Require(state.Release.Wait(TimeSpan.FromSeconds(TimeoutSeconds)), "timer_release_timeout");
            Interlocked.Increment(ref state.Completed);
        }
        catch (Exception error)
        {
            Interlocked.CompareExchange(ref state.Failure, error, null);
            state.Entered.TrySetException(error);
        }
    }

    private static TimeSpan Remaining(long stage)
    {
        TimeSpan remaining = TimeSpan.FromSeconds(TimeoutSeconds) - Stopwatch.GetElapsedTime(stage);
        Require(remaining > TimeSpan.Zero, "stage_timeout");
        return remaining;
    }

    private static void Elapsed(long before, long after, string reason) => Require(Stopwatch.Frequency > 0 && before >= 0 && after > before && Stopwatch.GetElapsedTime(before, after) >= TimeSpan.FromMilliseconds(25), reason);
    private static void Require(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }
}
