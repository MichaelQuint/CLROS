using System.Diagnostics;
using System.Runtime.CompilerServices;

// Normal upstream managed runtime behavior. Producer and executing-platform
// qualification remain separate; original pinned workloads are unchanged.
internal static partial class Program
{
    private const int Rounds = 3, TimeoutSeconds = 20;
    [ThreadStatic]
    private static int s_tls;
    private static int s_cctorRuns, s_cctorAttempts, s_finalizedMask, s_duplicateFinalizers;
    private static readonly InvalidOperationException CctorOriginal = new("matrix.cctor.original");
    private static int Main(string[] args)
    {
        Console.WriteLine("TEST_START nativeaot.runtime-matrix");
        if (args.Length != 0)
        {
            Console.WriteLine("TEST_FAIL nativeaot.runtime-matrix invalid_arguments");
            return 1;
        }

        try
        {
            RunAsync().GetAwaiter().GetResult();
            Console.WriteLine("TEST_PASS nativeaot.runtime-matrix");
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL nativeaot.runtime-matrix " + error.GetType().Name + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    private static async Task RunAsync()
    {
        for (int round = 1; round <= Rounds; round++)
        {
            Exceptions(round);
            Constructors(round);
            Delegates(round);
            Collections(round);
            await AsyncWork(round).ConfigureAwait(false);
        }
    }

    private static void Exceptions(int round)
    {
        EhState state = new();
        InvalidOperationException original = new("matrix.eh.original");
        try
        {
            try
            {
                try
                {
                    Throw(original);
                }
                catch (InvalidOperationException caught) when (Filter(caught, original, state))
                {
                    Require(ReferenceEquals(caught, original), "inner_exception_identity");
                    state.Catches++;
                    throw;
                }
                finally
                {
                    state.Cleanups++;
                    state.Order = state.Order * 10 + 1;
                }
            }
            finally
            {
                state.Cleanups++;
                state.Order = state.Order * 10 + 2;
            }
        }
        catch (InvalidOperationException caught)
        {
            Require(ReferenceEquals(caught, original), "outer_exception_identity");
            state.Catches++;
        }

        Require(state.Filters == 1 && state.Catches == 2 && state.Cleanups == 2 && state.Order == 12, "exception_inventory");
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_MATRIX_EH round={round} filters={state.Filters} catches={state.Catches} cleanups={state.Cleanups} order={state.Order} identity=true"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Throw(Exception error) => throw error;
    private static bool Filter(Exception caught, Exception original, EhState state)
    {
        state.Filters++;
        return ReferenceEquals(caught, original);
    }

    private static void Constructors(int round)
    {
        for (int attempt = 0; attempt < 2; attempt++)
            ReadFailure();
        Require(s_cctorRuns == 1 && s_cctorAttempts == round * 2, "cached_constructor_inventory");
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_MATRIX_CCTOR round={round} attempts=2 total={s_cctorAttempts} runs={s_cctorRuns} typed=true innerIdentity=true"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReadFailure()
    {
        try
        {
            _ = Broken.Read();
        }
        catch (TypeInitializationException error)
        {
            Require(ReferenceEquals(error.InnerException, CctorOriginal), "cached_constructor_inner");
            s_cctorAttempts++;
            return;
        }

        throw new InvalidOperationException("constructor_returned");
    }

    private static void Delegates(int round)
    {
        Target target = new(43 + round);
        Func<int, int> closed = target.Add;
        int captured = 47 + round;
        Func<int> closure = () => captured;
        Require(Invoke(closed, 7) == 50 + round, "delegate_before_gc");
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        int value = Invoke(closed, 7), capture = InvokeClosure(closure), actions = 0, order = 0, sum = 0;
        Action<int> first = x =>
        {
            actions++;
            order = order * 10 + 1;
            sum += x;
        };
        Action<int> second = x =>
        {
            actions++;
            order = order * 10 + 2;
            sum += x * 2;
        };
        Action<int> both = first + second;
        both(3);
        Action<int>? remaining = both - first;
        Require(remaining is not null, "delegate_remove");
        remaining!(5);
        Require(value == 50 + round && capture == 47 + round && target.Calls == 2 && actions == 3 && order == 122 && sum == 19, "delegate_inventory");
        GC.KeepAlive(closed);
        GC.KeepAlive(closure);
        GC.KeepAlive(target);
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_MATRIX_DELEGATES round={round} closed={value} closure={capture} calls={target.Calls} actions={actions} order={order} sum={sum}"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Invoke(Func<int, int> operation, int value) => operation(value);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int InvokeClosure(Func<int> operation) => operation();
    private static void Collections(int round)
    {
        ManualResetEventSlim release = new(false, 0);
        WorkerState[] states = [new(), new()];
        Thread[] threads = new Thread[2];
        int stop = 0;
        for (int i = 0; i < 2; i++)
        {
            int role = i;
            threads[i] = new Thread(() =>
            {
                WorkerState state = states[role];
                try
                {
                    Require(s_tls == 0, "worker_tls_initial");
                    state.FreshTls = 1;
                    s_tls = 29 + role;
                    byte[] root = new byte[8192];
                    root[0] = (byte)(37 + role);
                    root[^1] = (byte)(41 + role);
                    ref byte interior = ref root[4096];
                    interior = (byte)(43 + role);
                    Volatile.Write(ref state.Ready, 1);
                    if (role == 0)
                    {
                        while (Volatile.Read(ref stop) == 0)
                        {
                            Require(root[0] == 37 + role && root[^1] == 41 + role && interior == 43 + role && s_tls == 29 + role, "running_roots_tls");
                            Interlocked.Increment(ref state.Progress);
                        }
                    }
                    else
                        Require(release.Wait(TimeSpan.FromSeconds(TimeoutSeconds)), "worker_wait_timeout");
                    state.Roots = root[0] == 37 + role && root[^1] == 41 + role ? 1 : 0;
                    state.Interior = interior == 43 + role ? 1 : 0;
                    state.Tls = s_tls == 29 + role ? 1 : 0;
                    GC.KeepAlive(root);
                }
                catch (Exception error)
                {
                    Volatile.Write(ref state.Failure, error);
                    Volatile.Write(ref state.Ready, 1);
                }
            })
            {
                IsBackground = true
            };
        }

        int started = 0, joined = 0, before = 0, after = 0;
        s_tls = 17 + round;
        long firstProgress = 0, returned = 0, progressAfter = 0;
        byte[] main = new byte[4 * 1024 * 1024];
        main[0] = 53;
        main[^1] = 59;
        try
        {
            foreach (Thread thread in threads)
            {
                thread.Start();
                started++;
            }

            Wait(() => states.All(s => Volatile.Read(ref s.Ready) == 1) && Volatile.Read(ref states[0].Progress) > 0, states, "start");
            Wait(() => (threads[1].ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, states, "park");
            CreateFinalized(1 << (round - 1));
            before = GC.CollectionCount(GC.MaxGeneration);
            firstProgress = Volatile.Read(ref states[0].Progress);
            for (int i = 0; i < 2; i++)
            {
                int prior = GC.CollectionCount(GC.MaxGeneration);
                // The process owner must independently bound a stuck collector.
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                after = GC.CollectionCount(GC.MaxGeneration);
                returned = Volatile.Read(ref states[0].Progress);
                Wait(() => Volatile.Read(ref states[0].Progress) > returned, states, "resume");
                progressAfter = Volatile.Read(ref states[0].Progress);
                Require(after > prior && main[0] == 53 && main[^1] == 59 && s_tls == 17 + round, "main_roots_tls");
            }

            GC.KeepAlive(main);
        }
        finally
        {
            Volatile.Write(ref stop, 1);
            release.Set();
            long cleanup = Stopwatch.GetTimestamp();
            foreach (Thread thread in threads.Take(started))
            {
                TimeSpan remaining = TimeSpan.FromSeconds(TimeoutSeconds) - Stopwatch.GetElapsedTime(cleanup);
                if (remaining > TimeSpan.Zero && thread.Join(remaining))
                    joined++;
            }

            if (joined == started)
                release.Dispose();
        }

        Require(started == 2 && joined == 2 && states.All(s => s.Failure is null && s.FreshTls == 1 && s.Roots == 1 && s.Interior == 1 && s.Tls == 1), "worker_join_roots");
        Require(s_tls == 17 + round && s_finalizedMask == (1 << round) - 1 && s_duplicateFinalizers == 0, "finalizer_inventory");
        Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_MATRIX_GC round={round} collections=2 before={before} after={after} workers={started} joined={joined} freshTls=2 roots=2 interior=2 tls=2 progressBefore={firstProgress} progressReturned={returned} progressAfter={progressAfter} finalizedMask={s_finalizedMask} duplicateFinalizers={s_duplicateFinalizers}"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateFinalized(int bit) => _ = new Finalized(bit);
    private static async Task AsyncWork(int round)
    {
        long stage = Stopwatch.GetTimestamp();
        int completed = 0, mask = 0, callbacks = 0;
        Task<(int Value, int Worker)>[] tasks = new Task<(int, int)>[4];
        for (int i = 0; i < tasks.Length; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                Require(Thread.CurrentThread.IsThreadPoolThread, "task_worker");
                int bit = 1 << index;
                Require((Interlocked.Or(ref mask, bit) & bit) == 0, "task_duplicate");
                Interlocked.Increment(ref completed);
                return (Value: (index + 1) * (index + 1), Worker: Environment.CurrentManagedThreadId);
            });
        }

        var results = await Task.WhenAll(tasks).WaitAsync(Remaining(stage)).ConfigureAwait(false);
        int checksum = 0;
        HashSet<int> workers = [];
        for (int i = 0; i < results.Length; i++)
        {
            Require(results[i].Value == (i + 1) * (i + 1) && results[i].Worker > 0, "task_result");
            checksum += results[i].Value;
            workers.Add(results[i].Worker);
        }

        Require(completed == 4 && mask == 15 && checksum == 30, "task_inventory");
        long delayBefore = Stopwatch.GetTimestamp();
        await Task.Delay(TimeSpan.FromMilliseconds(50)).WaitAsync(Remaining(stage)).ConfigureAwait(false);
        long delayAfter = Stopwatch.GetTimestamp();
        Elapsed(delayBefore, delayAfter, "delay_elapsed");
        using (CancellationTokenSource canceled = new())
        {
            canceled.Cancel();
            Task work = Task.Run(() => Interlocked.Increment(ref callbacks), canceled.Token);
            await Canceled(work, canceled.Token, stage).ConfigureAwait(false);
        }

        using (CancellationTokenSource live = new())
        {
            Task delay = Task.Delay(TimeSpan.FromSeconds(10), live.Token);
            Require(!delay.IsCompleted, "live_delay");
            live.Cancel();
            await Canceled(delay, live.Token, stage).ConfigureAwait(false);
        }

        Require(callbacks == 0, "canceled_work_callback");
        TimerState state = new(Stopwatch.GetTimestamp());
        Timer timer = new(TimerCallback, state, TimeSpan.FromMilliseconds(50), Timeout.InfiniteTimeSpan);
        Task? drain = null;
        try
        {
            long fired = await state.Fired.Task.WaitAsync(Remaining(stage)).ConfigureAwait(false);
            drain = timer.DisposeAsync().AsTask();
            await drain.WaitAsync(Remaining(stage)).ConfigureAwait(false);
            Volatile.Write(ref state.Disposed, 1);
            long drained = Stopwatch.GetTimestamp();
            await Task.Delay(TimeSpan.FromMilliseconds(50)).WaitAsync(Remaining(stage)).ConfigureAwait(false);
            long after = Stopwatch.GetTimestamp();
            Elapsed(state.Before, fired, "timer_due");
            Elapsed(drained, after, "timer_observation");
            Require(state.Failure is null && state.Callbacks == 1 && state.Completed == 1 && state.Stale == 0, "timer_drained");
            Console.WriteLine(FormattableString.Invariant($"NATIVEAOT_MATRIX_ASYNC round={round} tasks=4 completed={completed} checksum={checksum} workers={workers.Count} canceledBefore=1 canceledDuring=1 workCallbacks={callbacks} delayBefore={delayBefore} delayAfter={delayAfter} timerBefore={state.Before} fired={fired} drained={drained} after={after} frequency={Stopwatch.Frequency} callbacks={state.Callbacks} stale={state.Stale} disposed=true"));
        }
        finally
        {
            drain ??= timer.DisposeAsync().AsTask();
            await drain.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds)).ConfigureAwait(false);
        }
    }

    private static void TimerCallback(object? value)
    {
        TimerState state = (TimerState)value!;
        try
        {
            if (Volatile.Read(ref state.Disposed) != 0)
                Interlocked.Increment(ref state.Stale);
            Require(Interlocked.Increment(ref state.Callbacks) == 1 && Thread.CurrentThread.IsThreadPoolThread, "timer_callback");
            long fired = Stopwatch.GetTimestamp();
            Elapsed(state.Before, fired, "callback_due");
            Require(state.Fired.TrySetResult(fired), "timer_publication");
            Interlocked.Increment(ref state.Completed);
        }
        catch (Exception error)
        {
            Interlocked.CompareExchange(ref state.Failure, error, null);
            state.Fired.TrySetException(error);
        }
    }

    private static async Task Canceled(Task task, CancellationToken token, long stage)
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

        Require(observed && task.IsCanceled, "cancellation_state");
    }

    private static void Wait(Func<bool> ready, WorkerState[] states, string reason)
    {
        long stage = Stopwatch.GetTimestamp();
        while (!ready())
        {
            Require(states.All(s => Volatile.Read(ref s.Failure) is null), "worker_failure");
            Require(Stopwatch.GetElapsedTime(stage) < TimeSpan.FromSeconds(TimeoutSeconds), reason + "_timeout");
            Thread.Yield();
        }

        Require(states.All(s => Volatile.Read(ref s.Failure) is null), "worker_failure");
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
