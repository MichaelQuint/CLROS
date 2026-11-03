using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>Explicit v2 diagnostic routes share these bounded request workers.
/// Kestrel owns request/response streams and cancellation; this object never
/// closes them. Each call retains one small buffer until its awaited operation
/// completes. Counters describe observations, not an atomic resource snapshot.
/// Invalid bodies return rejection; canceled I/O propagates to the server after
/// recording cancellation. No background worker or process lifetime is owned.</summary>

internal sealed partial class ServiceLifecycleExercise
{
    internal const int EchoBytes = 32768, StreamBytes = 524288, ChunkBytes = 1024;
    private int active, waiting, waits, canceled, echoes, streams, rejected;
    private long read, written;
    internal bool IsDrained => Volatile.Read(ref active) == 0;

    internal string Read() => string.Create(CultureInfo.InvariantCulture, $"CLROS_LIFECYCLE_STATUS active={Volatile.Read(ref active)} waiting={Volatile.Read(ref waiting)} waits={Volatile.Read(ref waits)} canceled={Volatile.Read(ref canceled)} echoes={Volatile.Read(ref echoes)} streams={Volatile.Read(ref streams)} rejected={Volatile.Read(ref rejected)} read={Interlocked.Read(ref read)} written={Interlocked.Read(ref written)}");
    internal static byte ByteAt(int offset) => unchecked((byte)(offset * 17 + 31));
    /// <summary>Explicit v2-only runtime observation under the canonical16MiB
    /// workstation GC limit. Fill bounded one-MiB arrays until managed allocation
    /// fails, verify their data and release every reference. Then retain32 fresh
    /// 64KiB arrays across collection to prove allocation recovery before service
    /// progress continues. Failure rejects the diagnostic run. Values describe
    /// managed GC only; native-pool quotas/headroom need independent evidence.</summary>

    internal static string MeasureGcBudget()
    {
        const long expected = 16 * 1024 * 1024, liveBytes = 2 * 1024 * 1024;
        if (!OperatingSystem.IsLinux() || System.Runtime.GCSettings.IsServerGC)
            throw new InvalidOperationException("CLROS_SERVICE_GC_REJECT runtime");
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        var before = GC.GetGCMemoryInfo();
        if (before.TotalAvailableMemoryBytes != expected)
            throw new InvalidOperationException("CLROS_SERVICE_GC_REJECT limit");
        var pressure = ExhaustGc();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        if (!pressure.Caught || !pressure.DataIntact || pressure.Bytes < 1048576 || pressure.Bytes >= expected || pressure.Heap < pressure.Bytes || pressure.Committed < pressure.Heap || pressure.Committed > expected || pressure.Collections <= 0)
            throw new InvalidOperationException("CLROS_SERVICE_GC_REJECT pressure");
        byte[][] held = new byte[32][];
        for (int i = 0; i < held.Length; i++)
        {
            held[i] = new byte[65536];
            held[i].AsSpan().Fill((byte)(i + 1));
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        var live = GC.GetGCMemoryInfo();
        if (live.TotalAvailableMemoryBytes != expected || live.HeapSizeBytes < liveBytes || live.TotalCommittedBytes < live.HeapSizeBytes || live.TotalCommittedBytes > expected)
            throw new InvalidOperationException("CLROS_SERVICE_GC_REJECT accounting");
        for (int i = 0; i < held.Length; i++)
            if (held[i].AsSpan().IndexOfAnyExcept((byte)(i + 1)) >= 0)
                throw new InvalidOperationException("CLROS_SERVICE_GC_REJECT recovery_data");
        string result = string.Create(CultureInfo.InvariantCulture, $"limit={live.TotalAvailableMemoryBytes} baselineHeap={before.HeapSizeBytes} baselineCommitted={before.TotalCommittedBytes} liveBytes={liveBytes} liveHeap={live.HeapSizeBytes} liveCommitted={live.TotalCommittedBytes} pressureBytes={pressure.Bytes} pressureHeap={pressure.Heap} pressureCommitted={pressure.Committed} pressureCollections={pressure.Collections} oom=1");
        GC.KeepAlive(held);
        return result;
    }

    /// <summary>Explicit v2-only native pressure, outside request workers. Own
    /// up to32 anonymous one-MiB mappings, touch and verify every byte, release
    /// them all, then verify a fresh mapping and release it. Only ENOMEM is an
    /// expected allocation failure. Linux may satisfy the entire bounded probe;
    /// quota=0 records that outcome without claiming quota exhaustion. This
    /// measures these mappings, not all runtime-native memory or its headroom.</summary>

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static unsafe string MeasureNativeBudget()
    {
        if (!OperatingSystem.IsLinux())
            throw new InvalidOperationException("CLROS_SERVICE_NATIVE_REJECT platform");
        const int chunk = 1024 * 1024, capacity = 32;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        nint* held = stackalloc nint[capacity];
        int count = 0, error = 0;
        bool intact = true, released = true;
        long committed = 0;
        try
        {
            while (count < capacity)
            {
                nint mapping = MapNative(0, chunk, 3, 0x22, -1, 0);
                if (mapping == -1)
                {
                    error = Marshal.GetLastPInvokeError();
                    break;
                }

                held[count++] = mapping;
                new Span<byte>((void*)mapping, chunk).Fill((byte)count);
            }

            for (int i = 0; i < count; i++)
                if (new ReadOnlySpan<byte>((void*)held[i], chunk).IndexOfAnyExcept((byte)(i + 1)) >= 0)
                    intact = false;
            committed = GC.GetGCMemoryInfo().TotalCommittedBytes;
        }
        finally
        {
            for (int i = 0; i < count; i++)
                if (UnmapNative(held[i], chunk) != 0)
                    released = false;
        }

        if (!intact || !released || count == 0 || (count < capacity ? error != 12 : error != 0))
            throw new InvalidOperationException("CLROS_SERVICE_NATIVE_REJECT pressure");
        nint recovered = MapNative(0, chunk, 3, 0x22, -1, 0);
        if (recovered == -1)
            throw new InvalidOperationException("CLROS_SERVICE_NATIVE_REJECT recovery_map");
        try
        {
            var bytes = new Span<byte>((void*)recovered, chunk);
            bytes.Fill(0xa7);
            intact = bytes.IndexOfAnyExcept((byte)0xa7) < 0;
        }
        finally
        {
            released = UnmapNative(recovered, chunk) == 0;
        }

        if (!intact || !released)
            throw new InvalidOperationException("CLROS_SERVICE_NATIVE_REJECT recovery");
        return string.Create(CultureInfo.InvariantCulture, $"capacityBytes={capacity * chunk} retainedBytes={count * chunk} gcCommitted={committed} quota={(error == 12 ? 1 : 0)} errno={error} releasedBytes={count * chunk} recoveredBytes={chunk}");
    }

    [DllImport("libc", EntryPoint = "mmap", SetLastError = true)]
    private static extern nint MapNative(nint address, nuint bytes, int protection, int flags, int descriptor, long offset);
    [DllImport("libc", EntryPoint = "munmap", SetLastError = true)]
    private static extern int UnmapNative(nint address, nuint bytes);
    // The separate frame bounds reference lifetime after exhaustion; finally
    // clears all roots before the caller attempts a fresh recovery allocation.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static GcPressure ExhaustGc()
    {
        const int chunk = 1024 * 1024;
        byte[][] held = new byte[32][];
        int count = 0, before = GC.CollectionCount(GC.MaxGeneration);
        bool caught = false, intact = true;
        try
        {
            try
            {
                for (; count < held.Length; count++)
                {
                    held[count] = new byte[chunk];
                    held[count].AsSpan().Fill((byte)(count + 1));
                }
            }
            catch (OutOfMemoryException)
            {
                caught = true;
            }

            for (int i = 0; i < count; i++)
                if (held[i].AsSpan().IndexOfAnyExcept((byte)(i + 1)) >= 0)
                    intact = false;
            var memory = GC.GetGCMemoryInfo();
            return new((long)count * chunk, memory.HeapSizeBytes, memory.TotalCommittedBytes, GC.CollectionCount(GC.MaxGeneration) - before, caught, intact);
        }
        finally
        {
            Array.Clear(held);
        }
    }

    internal async Task<EchoResult> ReadEchoAsync(Stream input, long? contentLength, CancellationToken cancellation)
    {
        if (contentLength != EchoBytes)
        {
            Interlocked.Increment(ref rejected);
            return default;
        }

        Interlocked.Increment(ref active);
        try
        {
            byte[] buffer = new byte[ChunkBytes];
            int count = 0;
            uint hash = 2166136261;
            while (count < EchoBytes)
            {
                int length = await input.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, EchoBytes - count)), cancellation);
                if (length == 0)
                {
                    Interlocked.Increment(ref rejected);
                    return new(false, count, hash);
                }

                Interlocked.Add(ref read, length);
                for (int i = 0; i < length; i++)
                {
                    if (buffer[i] != ByteAt(count + i))
                    {
                        Interlocked.Increment(ref rejected);
                        return new(false, count, hash);
                    }

                    hash = unchecked((hash ^ buffer[i]) * 16777619);
                }

                count += length;
            }

            Interlocked.Increment(ref echoes);
            return new(true, count, hash);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Interlocked.Increment(ref canceled);
            throw;
        }
        catch (IOException) when (cancellation.IsCancellationRequested)
        {
            Interlocked.Increment(ref canceled);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref active);
        }
    }

    internal async Task WriteStreamAsync(Stream output, CancellationToken cancellation)
    {
        Interlocked.Increment(ref active);
        try
        {
            byte[] buffer = new byte[ChunkBytes];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = ByteAt(i);
            for (int offset = 0; offset < StreamBytes; offset += buffer.Length)
            {
                await output.WriteAsync(buffer, cancellation);
                await output.FlushAsync(cancellation);
                Interlocked.Add(ref written, buffer.Length);
            }

            Interlocked.Increment(ref streams);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Interlocked.Increment(ref canceled);
            throw;
        }
        catch (IOException) when (cancellation.IsCancellationRequested)
        {
            Interlocked.Increment(ref canceled);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref active);
        }
    }

    internal async Task WaitForCancellationAsync(CancellationToken cancellation)
    {
        if (!cancellation.CanBeCanceled)
            throw new ArgumentException("A request cancellation lifetime is required.", nameof(cancellation));
        Interlocked.Increment(ref active);
        Interlocked.Increment(ref waiting);
        Interlocked.Increment(ref waits);
        try
        {
            await Task.Delay(Timeout.Infinite, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Interlocked.Increment(ref canceled);
        }
        finally
        {
            Interlocked.Decrement(ref waiting);
            Interlocked.Decrement(ref active);
        }
    }
}
