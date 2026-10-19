using System.Globalization;

/// <summary>Explicit v2 diagnostic routes share these bounded request workers.
/// Kestrel owns request/response streams and cancellation; this object never
/// closes them. Each call retains one small buffer until its awaited operation
/// completes. Counters describe observations, not an atomic resource snapshot.
/// Invalid bodies return rejection; canceled I/O propagates to the server after
/// recording cancellation. No background worker or process lifetime is owned.</summary>
internal sealed class ServiceLifecycleExercise
{
    internal const int EchoBytes = 32768, StreamBytes = 524288, ChunkBytes = 1024;
    private int active, waiting, waits, canceled, echoes, streams, rejected;
    private long read, written;

    internal bool IsDrained => Volatile.Read(ref active) == 0;
    internal readonly record struct EchoResult(bool Accepted, int Bytes, uint Hash)
    {
        internal string Text => Accepted
            ? string.Create(CultureInfo.InvariantCulture, $"CLROS_LIFECYCLE_ECHO_OK bytes={Bytes} hash={Hash:x8}")
            : "CLROS_LIFECYCLE_BAD_BODY";
    }

    internal string Read() => string.Create(CultureInfo.InvariantCulture,
        $"CLROS_LIFECYCLE_STATUS active={Volatile.Read(ref active)} waiting={Volatile.Read(ref waiting)} waits={Volatile.Read(ref waits)} canceled={Volatile.Read(ref canceled)} echoes={Volatile.Read(ref echoes)} streams={Volatile.Read(ref streams)} rejected={Volatile.Read(ref rejected)} read={Interlocked.Read(ref read)} written={Interlocked.Read(ref written)}");

    internal static byte ByteAt(int offset) => unchecked((byte)(offset * 17 + 31));

    /// <summary>Explicit v2-only runtime observation under the canonical16MiB
    /// workstation GC limit. Retain32 live64KiB arrays across a forced collection,
    /// then release them before serving/shutdown continues. Failure rejects this
    /// diagnostic run. Values describe GC only, never native headroom or quota
    /// recovery; host observations grant no guest execution authority.</summary>
    internal static string MeasureGcBudget()
    {
        const long expected = 16 * 1024 * 1024, liveBytes = 2 * 1024 * 1024;
        if (!OperatingSystem.IsLinux() || System.Runtime.GCSettings.IsServerGC)
            throw new InvalidOperationException("CLROS_SERVICE_GC_REJECT runtime");
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        var before = GC.GetGCMemoryInfo();
        if (before.TotalAvailableMemoryBytes != expected)
            throw new InvalidOperationException("CLROS_SERVICE_GC_REJECT limit");
        byte[][] held = new byte[32][];
        for (int i = 0; i < held.Length; i++) { held[i] = new byte[65536]; held[i].AsSpan().Fill((byte)(i + 1)); }
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        var live = GC.GetGCMemoryInfo();
        if (live.TotalAvailableMemoryBytes != expected || live.HeapSizeBytes < liveBytes ||
            live.TotalCommittedBytes < live.HeapSizeBytes || live.TotalCommittedBytes > expected)
            throw new InvalidOperationException("CLROS_SERVICE_GC_REJECT accounting");
        string result = string.Create(CultureInfo.InvariantCulture,
            $"limit={live.TotalAvailableMemoryBytes} baselineHeap={before.HeapSizeBytes} baselineCommitted={before.TotalCommittedBytes} liveBytes={liveBytes} liveHeap={live.HeapSizeBytes} liveCommitted={live.TotalCommittedBytes}");
        GC.KeepAlive(held); return result;
    }

    internal async Task<EchoResult> ReadEchoAsync(Stream input, long? contentLength, CancellationToken cancellation)
    {
        if (contentLength != EchoBytes) { Interlocked.Increment(ref rejected); return default; }
        Interlocked.Increment(ref active);
        try
        {
            byte[] buffer = new byte[ChunkBytes]; int count = 0; uint hash = 2166136261;
            while (count < EchoBytes)
            {
                int length = await input.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, EchoBytes - count)), cancellation);
                if (length == 0) { Interlocked.Increment(ref rejected); return new(false, count, hash); }
                Interlocked.Add(ref read, length);
                for (int i = 0; i < length; i++)
                {
                    if (buffer[i] != ByteAt(count + i)) { Interlocked.Increment(ref rejected); return new(false, count, hash); }
                    hash = unchecked((hash ^ buffer[i]) * 16777619);
                }
                count += length;
            }
            Interlocked.Increment(ref echoes); return new(true, count, hash);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { Interlocked.Increment(ref canceled); throw; }
        catch (IOException) when (cancellation.IsCancellationRequested)
        { Interlocked.Increment(ref canceled); throw; }
        finally { Interlocked.Decrement(ref active); }
    }

    internal async Task WriteStreamAsync(Stream output, CancellationToken cancellation)
    {
        Interlocked.Increment(ref active);
        try
        {
            byte[] buffer = new byte[ChunkBytes];
            for (int i = 0; i < buffer.Length; i++) buffer[i] = ByteAt(i);
            for (int offset = 0; offset < StreamBytes; offset += buffer.Length)
            {
                await output.WriteAsync(buffer, cancellation);
                await output.FlushAsync(cancellation);
                Interlocked.Add(ref written, buffer.Length);
            }
            Interlocked.Increment(ref streams);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { Interlocked.Increment(ref canceled); throw; }
        catch (IOException) when (cancellation.IsCancellationRequested)
        { Interlocked.Increment(ref canceled); throw; }
        finally { Interlocked.Decrement(ref active); }
    }

    internal async Task WaitForCancellationAsync(CancellationToken cancellation)
    {
        if (!cancellation.CanBeCanceled) throw new ArgumentException("A request cancellation lifetime is required.", nameof(cancellation));
        Interlocked.Increment(ref active); Interlocked.Increment(ref waiting); Interlocked.Increment(ref waits);
        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { Interlocked.Increment(ref canceled); }
        finally { Interlocked.Decrement(ref waiting); Interlocked.Decrement(ref active); }
    }
}
