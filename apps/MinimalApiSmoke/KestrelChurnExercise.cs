using System.Globalization;
using System.Diagnostics;

/// <summary>Explicit request-owned managed churn. At most two calls retain one
/// 64KiB array each; all timer and ThreadPool operations are awaited. The HTTP
/// owner supplies cancellation and retains response ownership. Counters measure
/// completed helper operations and retained roots, not physical/GC backing,
/// unique global collections, native threads or CLROS guest execution.</summary>

internal sealed partial class KestrelChurnExercise
{
    internal const int MaximumActive = 2, Iterations = 8, PayloadBytes = 65536, DelayMilliseconds = 25;
    internal const string SuccessBody = "CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8";
    private readonly object gate = new();
    private int active, waiting, peakActive;
    private long started, completed, canceled, failed, rejected, timers, workers, collections, heldBytes, peakHeldBytes;
    internal Snapshot Inspect()
    {
        lock (gate)
            return new(active, waiting, peakActive, started, completed, canceled, failed, rejected, timers, workers, collections, heldBytes, peakHeldBytes);
    }

    internal string Read()
    {
        var value = Inspect();
        return string.Create(CultureInfo.InvariantCulture, $"CLROS_CHURN_STATUS active={value.Active} waiting={value.Waiting} peakActive={value.PeakActive} started={value.Started} completed={value.Completed} canceled={value.Canceled} failed={value.Failed} rejected={value.Rejected} timers={value.Timers} workers={value.Workers} collections={value.Collections} heldBytes={value.HeldBytes} peakHeldBytes={value.PeakHeldBytes}");
    }

    /// <summary>Run exactly eight successful iterations, or propagate request
    /// cancellation/failure after draining the awaited operation. Null means
    /// capacity was busy before admission. Invalid/already canceled tokens do
    /// not acquire a slot. Fixed counters use checked arithmetic, never wrap.</summary>

    internal Task<string?> RunAsync(CancellationToken cancellation) => RunCoreAsync(cancellation, null);
    /// <summary>Repeat real awaited iterations until the request cancels. One
    /// watchdog begins at admission, is never refreshed, and expires as failure.
    /// The request still joins an in-flight worker before releasing its payload
    /// and slot. This mode never returns successful finite-work evidence.</summary>

    internal Task<string?> RunUntilCanceledAsync(CancellationToken cancellation, TimeSpan? budget = null)
    {
        TimeSpan limit = budget ?? TimeSpan.FromSeconds(120);
        if (limit <= TimeSpan.Zero || limit > TimeSpan.FromSeconds(120))
            throw new ArgumentOutOfRangeException(nameof(budget));
        return RunCoreAsync(cancellation, limit);
    }

    private async Task<string?> RunCoreAsync(CancellationToken cancellation, TimeSpan? budget)
    {
        if (!cancellation.CanBeCanceled)
            throw new ArgumentException("A request cancellation lifetime is required.", nameof(cancellation));
        cancellation.ThrowIfCancellationRequested();
        byte pattern;
        long admitted;
        lock (gate)
        {
            if (active == MaximumActive)
            {
                rejected = checked(rejected + 1);
                return null;
            }

            long sequence = checked(started + 1);
            pattern = (byte)(sequence % 251);
            started = sequence;
            active++;
            peakActive = Math.Max(peakActive, active);
            admitted = Stopwatch.GetTimestamp();
        }

        CancellationTokenSource? watchdog = null;
        CancellationToken work = cancellation;
        bool Expired() => budget is { } limit && Stopwatch.GetElapsedTime(admitted) >= limit;
        void Check()
        {
            if (Expired())
                throw new TimeoutException("CLROS_CHURN_FAIL deadline");
            work.ThrowIfCancellationRequested();
        }

        byte[]? payload = null;
        bool retained = false, succeeded = false, requestCanceled = false;
        try
        {
            if (budget is { } limit)
            {
                watchdog = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                watchdog.CancelAfter(limit);
                work = watchdog.Token;
            }

            payload = new byte[PayloadBytes];
            for (int i = 0; i < payload.Length; i++)
                payload[i] = unchecked((byte)(pattern + i * 17));
            lock (gate)
            {
                heldBytes += PayloadBytes;
                retained = true;
                peakHeldBytes = Math.Max(peakHeldBytes, heldBytes);
            }

            for (int iteration = 0; budget is not null || iteration < Iterations; iteration = checked(iteration + 1))
            {
                Check();
                lock (gate)
                    waiting++;
                try
                {
                    await Task.Delay(DelayMilliseconds, work);
                }
                finally
                {
                    lock (gate)
                        waiting--;
                }

                lock (gate)
                    timers = checked(timers + 1);
                // Await even if cancellation arrives while this worker runs;
                // its local root cannot outlive the request's finally block.
                await Task.Run(() => VerifyCollection(payload!, pattern, work), work);
                lock (gate)
                {
                    long nextWorkers = checked(workers + 1), nextCollections = checked(collections + 1);
                    workers = nextWorkers;
                    collections = nextCollections;
                }

                Check();
            }

            cancellation.ThrowIfCancellationRequested();
            succeeded = true;
            return SuccessBody;
        }
        catch (OperationCanceledException error)when (Expired())
        {
            throw new TimeoutException("CLROS_CHURN_FAIL deadline", error);
        }
        catch (OperationCanceledException)when (cancellation.IsCancellationRequested)
        {
            requestCanceled = true;
            throw;
        }
        finally
        {
            GC.KeepAlive(payload);
            payload = null;
            // Publish the terminal outcome and released slot together so a
            // status snapshot never counts one request as active and finished.
            lock (gate)
            {
                if (succeeded)
                    completed = checked(completed + 1);
                else if (requestCanceled)
                    canceled = checked(canceled + 1);
                else
                    failed = checked(failed + 1);
                if (retained)
                    heldBytes -= PayloadBytes;
                active--;
            }

            watchdog?.Dispose();
        }
    }

    private static void VerifyCollection(byte[] payload, byte pattern, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!Thread.CurrentThread.IsThreadPoolThread || !PayloadIntact(payload, pattern))
            throw new InvalidOperationException("CLROS_CHURN_FAIL worker_or_payload");
        int before = GC.CollectionCount(GC.MaxGeneration);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        if (GC.CollectionCount(GC.MaxGeneration) <= before || !PayloadIntact(payload, pattern))
            throw new InvalidOperationException("CLROS_CHURN_FAIL collection_or_payload");
        GC.KeepAlive(payload);
    }

    internal static bool PayloadIntact(ReadOnlySpan<byte> payload, byte pattern)
    {
        if (payload.Length != PayloadBytes)
            return false;
        for (int i = 0; i < payload.Length; i++)
            if (payload[i] != unchecked((byte)(pattern + i * 17)))
                return false;
        return true;
    }
}
