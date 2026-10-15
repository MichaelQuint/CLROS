using System.Diagnostics;

// This is an upstream managed workload, never freestanding kernel source.
// Output proves only what the executing runtime observed. Activation/suspension
// additionally requires the pinned native trace and CLROS thread/signal receipts.
internal static class Program
{
    [ThreadStatic] private static int s_threadValue;
    private const int Collections = 2, TimeoutSeconds = 20;

    private sealed class WorkerState
    {
        internal int Ready, Stop, Roots, Interior, Tls;
        internal long Progress;
        internal Exception? Failure;
    }

    private static int Main(string[] args)
    {
        Console.WriteLine("TEST_START nativeaot.suspension");
        if (args.Length != 0)
        {
            Console.WriteLine("TEST_FAIL nativeaot.suspension invalid_arguments");
            return 1;
        }

        try
        {
            WorkerState state = new();
            Thread worker = new(() => Work(state)) { IsBackground = true };
            bool started = false, joined = false;
            s_threadValue = 17;
            byte[] mainRoots = new byte[4096]; mainRoots[0] = 53; mainRoots[^1] = 59;
            try
            {
                worker.Start(); started = true;
                Require(WaitFor(state, 0, ready: true) && WaitFor(state, 0), "worker_start");
                for (int epoch = 1; epoch <= Collections; epoch++)
                {
                    long progressBefore = Volatile.Read(ref state.Progress);
                    int before = GC.CollectionCount(GC.MaxGeneration);
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                    int after = GC.CollectionCount(GC.MaxGeneration);
                    long progressReturned = Volatile.Read(ref state.Progress);
                    Require(after > before && WaitFor(state, progressReturned), "collection_progress");
                    Require(mainRoots[0] == 53 && mainRoots[^1] == 59 && s_threadValue == 17, "main_roots_tls");
                    long progressAfter = Volatile.Read(ref state.Progress);
                    Console.WriteLine($"NATIVEAOT_SUSPENSION_GC epoch={epoch} before={before} after={after} progressBefore={progressBefore} progressReturned={progressReturned} progressAfter={progressAfter}");
                }
                GC.KeepAlive(mainRoots);
            }
            finally
            {
                Volatile.Write(ref state.Stop, 1);
                if (started) joined = worker.Join(TimeSpan.FromSeconds(TimeoutSeconds));
            }
            Require(joined && Volatile.Read(ref state.Failure) is null, "worker_join");
            Require(state.Roots == 1 && state.Interior == 1 && state.Tls == 1 && s_threadValue == 17, "joined_witness");
            Console.WriteLine("NATIVEAOT_SUSPENSION_JOINED workers=1 collections=2 roots=true interior=true tls=true");
            Console.WriteLine("TEST_PASS nativeaot.suspension");
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL nativeaot.suspension " + error.GetType().Name + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    private static void Work(WorkerState state)
    {
        try
        {
            s_threadValue = 29;
            byte[] rooted = new byte[4096]; rooted[0] = 37; rooted[^1] = 41;
            ref byte interior = ref rooted[2048]; interior = 43;
            Volatile.Write(ref state.Ready, 1);
            // The worker allocates nothing and invokes no waits or callbacks in
            // its successful loop. It retains both an object and an interior ref
            // across the main thread's blocking full collections. Whether the
            // compiler/runtime uses activation must be measured, never inferred.
            while (Volatile.Read(ref state.Stop) == 0)
            {
                if (rooted[0] != 37 || rooted[^1] != 41 || interior != 43 || s_threadValue != 29)
                    throw new InvalidOperationException("worker_roots_tls");
                Interlocked.Increment(ref state.Progress);
            }
            state.Roots = rooted[0] == 37 && rooted[^1] == 41 ? 1 : 0;
            state.Interior = interior == 43 ? 1 : 0; state.Tls = s_threadValue == 29 ? 1 : 0;
            GC.KeepAlive(rooted);
        }
        catch (Exception error)
        {
            Volatile.Write(ref state.Failure, error);
            Volatile.Write(ref state.Ready, 1);
        }
    }

    // The host/guest process owner must also bound the whole run: GC.Collect
    // itself cannot safely be cancelled if the runtime fails to suspend a worker.
    private static bool WaitFor(WorkerState state, long progress, bool ready = false)
    {
        long start = Stopwatch.GetTimestamp();
        while (ready ? Volatile.Read(ref state.Ready) == 0 : Volatile.Read(ref state.Progress) <= progress)
        {
            if (Volatile.Read(ref state.Failure) is not null || Stopwatch.GetElapsedTime(start).TotalSeconds >= TimeoutSeconds) return false;
            Thread.Yield();
        }
        return Volatile.Read(ref state.Failure) is null;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
