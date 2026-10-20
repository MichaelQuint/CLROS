using System.Globalization;

/// <summary>Explicit request-owned managed churn. At most two calls retain one
/// 64KiB array each; all timer and ThreadPool operations are awaited. The HTTP
/// owner supplies cancellation and retains response ownership. Counters measure
/// completed helper operations and retained roots, not physical/GC backing,
/// unique global collections, native threads or CLROS guest execution.</summary>
internal sealed class KestrelChurnExercise
{
    internal const int MaximumActive = 2, Iterations = 8, PayloadBytes = 65536, DelayMilliseconds = 25;
    internal const string SuccessBody = "CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8";
    private readonly object gate = new();
    private int active, waiting, peakActive;
    private long started, completed, canceled, failed, rejected, timers, workers, collections, heldBytes, peakHeldBytes;

    /// <summary>Atomic copied counters only. Waiting counts active timer awaits;
    /// Collections counts forced calls with a verified positive full-GC delta,
    /// which can overlap other requests' collections. No history is retained.</summary>
    internal readonly record struct Snapshot(int Active, int Waiting, int PeakActive, long Started,
        long Completed, long Canceled, long Failed, long Rejected, long Timers, long Workers, long Collections,
        long HeldBytes, long PeakHeldBytes);

    internal Snapshot Inspect()
    {
        lock (gate) return new(active, waiting, peakActive, started, completed, canceled, failed, rejected,
            timers, workers, collections, heldBytes, peakHeldBytes);
    }

    internal string Read()
    {
        var value = Inspect();
        return string.Create(CultureInfo.InvariantCulture,
            $"CLROS_CHURN_STATUS active={value.Active} waiting={value.Waiting} peakActive={value.PeakActive} started={value.Started} completed={value.Completed} canceled={value.Canceled} failed={value.Failed} rejected={value.Rejected} timers={value.Timers} workers={value.Workers} collections={value.Collections} heldBytes={value.HeldBytes} peakHeldBytes={value.PeakHeldBytes}");
    }

    /// <summary>Run exactly eight successful iterations, or propagate request
    /// cancellation/failure after draining the awaited operation. Null means
    /// capacity was busy before admission. Invalid/already canceled tokens do
    /// not acquire a slot. Fixed counters use checked arithmetic, never wrap.</summary>
    internal async Task<string?> RunAsync(CancellationToken cancellation)
    {
        if (!cancellation.CanBeCanceled) throw new ArgumentException("A request cancellation lifetime is required.", nameof(cancellation));
        cancellation.ThrowIfCancellationRequested();
        byte pattern;
        lock (gate)
        {
            if (active == MaximumActive) { rejected = checked(rejected + 1); return null; }
            long sequence = checked(started + 1);
            pattern = (byte)(sequence % 251); started = sequence; active++;
            peakActive = Math.Max(peakActive, active);
        }
        byte[]? payload = null; bool retained = false;
        try
        {
            payload = new byte[PayloadBytes];
            for (int i = 0; i < payload.Length; i++) payload[i] = unchecked((byte)(pattern + i * 17));
            lock (gate)
            {
                heldBytes += PayloadBytes; retained = true; peakHeldBytes = Math.Max(peakHeldBytes, heldBytes);
            }
            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                cancellation.ThrowIfCancellationRequested();
                lock (gate) waiting++;
                try { await Task.Delay(DelayMilliseconds, cancellation); }
                finally { lock (gate) waiting--; }
                lock (gate) timers = checked(timers + 1);
                // Await even if cancellation arrives while this worker runs;
                // its local root cannot outlive the request's finally block.
                await Task.Run(() => VerifyCollection(payload!, pattern, cancellation), cancellation);
                lock (gate)
                {
                    long nextWorkers = checked(workers + 1), nextCollections = checked(collections + 1);
                    workers = nextWorkers; collections = nextCollections;
                }
            }
            cancellation.ThrowIfCancellationRequested();
            lock (gate) completed = checked(completed + 1);
            return SuccessBody;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { lock (gate) canceled = checked(canceled + 1); throw; }
        catch
        { lock (gate) failed = checked(failed + 1); throw; }
        finally
        {
            GC.KeepAlive(payload); payload = null;
            lock (gate) { if (retained) heldBytes -= PayloadBytes; active--; }
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
        if (payload.Length != PayloadBytes) return false;
        for (int i = 0; i < payload.Length; i++) if (payload[i] != unchecked((byte)(pattern + i * 17))) return false;
        return true;
    }
}
