using System.Diagnostics;

// Managed output alone grants no NativeAOT or CLROS execution evidence. The
// process owner must separately qualify the producer and executing platform.
// Drained below means only this app's awaited work and disposed timer; it never
// claims that runtime ThreadPool workers have stopped or released their stacks.
internal static partial class Program
{
    private const int TimeoutSeconds = 20, WorkItems = 4, PublishedValue = 43;
    [ThreadStatic]
    private static bool s_publishing;
    private static int Main(string[] args)
    {
        Console.WriteLine("TEST_START nativeaot.threadpool");
        if (args.Length != 0)
        {
            Console.WriteLine("TEST_FAIL nativeaot.threadpool invalid_arguments");
            return 1;
        }

        try
        {
            RunAsync().GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL nativeaot.threadpool " + error.GetType().Name + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    private static async Task RunAsync()
    {
        int tasks = await WorkAsync().ConfigureAwait(false);
        int continuations = await ContinuationsAsync().ConfigureAwait(false);
        await CancellationAsync().ConfigureAwait(false);
        await DelayAsync().ConfigureAwait(false);
        var timer = await TimerAsync().ConfigureAwait(false);
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_THREADPOOL_DRAINED tasks={tasks} continuations={continuations} callbacks={timer.Callbacks} stale={timer.Stale}"));
        Console.WriteLine("TEST_PASS nativeaot.threadpool");
    }

    private static async Task<int> WorkAsync()
    {
        long stage = Stopwatch.GetTimestamp();
        int completed = 0;
        Task<(int Square, int Worker)>[] tasks = new Task<(int Square, int Worker)>[WorkItems];
        for (int i = 0; i < tasks.Length; i++)
        {
            int value = i + 1;
            tasks[i] = Task.Run(() =>
            {
                Require(Thread.CurrentThread.IsThreadPoolThread, "work_threadpool");
                int square = value * value;
                Interlocked.Increment(ref completed);
                return (Square: square, Worker: Environment.CurrentManagedThreadId);
            });
        }

        var results = await Task.WhenAll(tasks).WaitAsync(Remaining(stage)).ConfigureAwait(false);
        HashSet<int> workers = [];
        int checksum = 0;
        for (int i = 0; i < results.Length; i++)
        {
            Require(results[i].Square == (i + 1) * (i + 1) && results[i].Worker > 0, "work_result");
            checksum += results[i].Square;
            workers.Add(results[i].Worker);
        }

        Require(completed == WorkItems && checksum == 30 && workers.Count is >= 1 and <= WorkItems, "work_inventory");
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_THREADPOOL_WORK tasks={tasks.Length} completed={completed} checksum={checksum} workers={workers.Count} threadPool=true"));
        return completed;
    }

    private static async Task<int> ContinuationsAsync()
    {
        long stage = Stopwatch.GetTimestamp();
        ContinuationState state = new();
        TaskCompletionSource<int> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task before = Attach(source.Task, state, 0);
        // Both contenders are registered on the same incomplete asynchronous
        // gate before release. Registration may win or lose the publication race.
        Task race = gate.Task.ContinueWith(_ =>
        {
            Require(Thread.CurrentThread.IsThreadPoolThread, "registration_threadpool");
            return Attach(source.Task, state, 1);
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
        Task publisher = gate.Task.ContinueWith(_ =>
        {
            Require(Thread.CurrentThread.IsThreadPoolThread, "publication_threadpool");
            s_publishing = true;
            try
            {
                source.SetResult(PublishedValue);
            }
            finally
            {
                s_publishing = false;
            }
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        Require(!source.Task.IsCompleted && !before.IsCompleted && !race.IsCompleted && !publisher.IsCompleted, "continuations_staged");
        gate.SetResult(true);
        await Task.WhenAll(before, race, publisher).WaitAsync(Remaining(stage)).ConfigureAwait(false);
        Require(source.Task.IsCompletedSuccessfully && source.Task.Result == PublishedValue, "publication_value");
        // ExecuteSynchronously is legal after publication; attach on a real
        // worker so that even that permitted inline case has ThreadPool context.
        Task after = Task.Run(async () => await Attach(source.Task, state, 2).ConfigureAwait(false));
        await after.WaitAsync(Remaining(stage)).ConfigureAwait(false);
        Require(state.Before == 1 && state.Race == 1 && state.After == 1 && state.Count == 3 && state.PublicationInline == 0, "continuation_inventory");
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_THREADPOOL_CONTINUATIONS before={state.Before} race={state.Race} after={state.After} count={state.Count} value={source.Task.Result} publicationInline={state.PublicationInline}"));
        return state.Count;
    }

    private static Task Attach(Task<int> source, ContinuationState state, int position) => source.ContinueWith(task =>
    {
        if (s_publishing)
            Interlocked.Increment(ref state.PublicationInline);
        Require(!s_publishing && Thread.CurrentThread.IsThreadPoolThread, "continuation_publication_context");
        Require(task.IsCompletedSuccessfully && task.Result == PublishedValue, "continuation_value");
        int count = position switch
        {
            0 => Interlocked.Increment(ref state.Before),
            1 => Interlocked.Increment(ref state.Race),
            _ => Interlocked.Increment(ref state.After)
        };
        Require(count == 1, "continuation_duplicate");
        Interlocked.Increment(ref state.Count);
    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    private static async Task CancellationAsync()
    {
        long stage = Stopwatch.GetTimestamp();
        int before = 0, during = 0, callbacks = 0;
        using (CancellationTokenSource canceled = new())
        {
            canceled.Cancel();
            Task work = Task.Run(() => Interlocked.Increment(ref callbacks), canceled.Token);
            Task delay = Task.Delay(TimeSpan.FromMilliseconds(50), canceled.Token);
            await ExpectCanceled(work, canceled.Token, stage).ConfigureAwait(false);
            before++;
            await ExpectCanceled(delay, canceled.Token, stage).ConfigureAwait(false);
            before++;
        }

        using (CancellationTokenSource live = new())
        {
            Task delay = Task.Delay(TimeSpan.FromSeconds(10), live.Token);
            Require(!delay.IsCompleted && !live.IsCancellationRequested, "cancellation_live_delay");
            live.Cancel();
            await ExpectCanceled(delay, live.Token, stage).ConfigureAwait(false);
            during++;
        }

        Require(before == 2 && during == 1 && callbacks == 0, "cancellation_inventory");
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_THREADPOOL_CANCELLATION before={before} during={during} callbacks={callbacks}"));
    }

    private static async Task ExpectCanceled(Task task, CancellationToken token, long stage)
    {
        bool observed = false;
        try
        {
            await task.WaitAsync(Remaining(stage)).ConfigureAwait(false);
        }
        catch (OperationCanceledException error)
        {
            Require(error.CancellationToken == token, "cancellation_token");
            observed = true;
        }

        Require(observed && task.IsCanceled, "cancellation_final_state");
    }

    private static async Task DelayAsync()
    {
        long stage = Stopwatch.GetTimestamp(), before = Stopwatch.GetTimestamp();
        await Task.Delay(TimeSpan.FromMilliseconds(50)).WaitAsync(Remaining(stage)).ConfigureAwait(false);
        long after = Stopwatch.GetTimestamp();
        RequireElapsed(before, after, "delay_elapsed");
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_THREADPOOL_DELAY requestedMs=50 before={before} after={after} frequency={Stopwatch.Frequency} minimumMs=25"));
    }

    private static async Task<(int Callbacks, int Stale)> TimerAsync()
    {
        long stage = Stopwatch.GetTimestamp();
        TimerState state = new(Stopwatch.GetTimestamp());
        Timer timer = new(TimerCallback, state, TimeSpan.FromMilliseconds(50), Timeout.InfiniteTimeSpan);
        Task? disposal = null;
        try
        {
            long fired = await state.Fired.Task.WaitAsync(Remaining(stage)).ConfigureAwait(false);
            disposal = timer.DisposeAsync().AsTask();
            await disposal.WaitAsync(Remaining(stage)).ConfigureAwait(false);
            Volatile.Write(ref state.Disposed, 1);
            long observation = Stopwatch.GetTimestamp();
            await Task.Delay(TimeSpan.FromMilliseconds(50)).WaitAsync(Remaining(stage)).ConfigureAwait(false);
            RequireElapsed(observation, Stopwatch.GetTimestamp(), "timer_drain_observation");
            Require(observation >= fired && Volatile.Read(ref state.Failure) is null && Volatile.Read(ref state.Callbacks) == 1 && Volatile.Read(ref state.Stale) == 0, "timer_drained");
            Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_THREADPOOL_TIMER dueMs=50 before={state.Before} fired={fired} frequency={Stopwatch.Frequency} callbacks={state.Callbacks} stale={state.Stale} disposed=true"));
            return (state.Callbacks, state.Stale);
        }
        finally
        {
            // Failure cleanup has its own bounded drain. A broken runtime still
            // requires the enclosing process owner's independent whole-run limit.
            disposal ??= timer.DisposeAsync().AsTask();
            await disposal.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds)).ConfigureAwait(false);
        }
    }

    private static void TimerCallback(object? value)
    {
        TimerState state = (TimerState)value!;
        int count = Interlocked.Increment(ref state.Callbacks);
        try
        {
            if (Volatile.Read(ref state.Disposed) != 0)
                Interlocked.Increment(ref state.Stale);
            Require(count == 1 && Volatile.Read(ref state.Stale) == 0 && Thread.CurrentThread.IsThreadPoolThread, "timer_callback_context");
            long fired = Stopwatch.GetTimestamp();
            RequireElapsed(state.Before, fired, "timer_elapsed");
            Require(state.Fired.TrySetResult(fired), "timer_publication");
        }
        catch (Exception error)
        {
            Interlocked.CompareExchange(ref state.Failure, error, null);
            state.Fired.TrySetException(error);
        }
    }

    private static TimeSpan Remaining(long stage)
    {
        TimeSpan remaining = TimeSpan.FromSeconds(TimeoutSeconds) - Stopwatch.GetElapsedTime(stage);
        Require(remaining > TimeSpan.Zero, "stage_timeout");
        return remaining;
    }

    private static void RequireElapsed(long before, long after, string reason) => Require(Stopwatch.Frequency > 0 && before >= 0 && after >= before && Stopwatch.GetElapsedTime(before, after) >= TimeSpan.FromMilliseconds(25), reason);
    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
