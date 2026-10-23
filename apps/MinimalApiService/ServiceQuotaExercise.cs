using System.Globalization;
using System.Runtime.InteropServices;

/// <summary>Explicit quota-negative fixture only. Retains at most 64MiB while
/// real async requests and the service worker continue. The kernel must end
/// the process on proven backing exhaustion; returning ENOMEM or completing
/// this bound fails the fixture. If the process remains alive, finally releases
/// every retained mapping. These user observations grant no kernel policy.</summary>
internal sealed class ServiceQuotaExercise
{
    internal const int ChunkBytes = 1024 * 1024, MaximumChunks = 64;
    private readonly nint[] held = new nint[MaximumChunks];
    private int started, attempts, gcCollections;
    private long retainedBytes;

    internal bool TryStart() => Interlocked.CompareExchange(ref started, 1, 0) == 0;

    // Copied monotonic progress observations, not an atomic resource snapshot
    // or kernel quota/cleanup authority. Phase2 always rejects the fixture.
    internal string Read()
    {
        long retained = Interlocked.Read(ref retainedBytes);
        return string.Create(CultureInfo.InvariantCulture,
            $"CLROS_QUOTA_STATUS phase={Volatile.Read(ref started)} attempts={Volatile.Read(ref attempts)} retainedBytes={retained} gcCollections={Volatile.Read(ref gcCollections)}");
    }

    internal async Task ExhaustAsync(CancellationToken cancellation)
    {
        if (Volatile.Read(ref started) != 1 || !OperatingSystem.IsLinux())
            throw new InvalidOperationException("CLROS_QUOTA_REJECT invocation");
        int retained = 0;
        bool released = true;
        try
        {
            Console.Error.WriteLine("CLROS_SERVICE_QUOTA_BEGIN chunkBytes=1048576 maximumChunks=64");
            while (retained < MaximumChunks)
            {
                cancellation.ThrowIfCancellationRequested();
                Volatile.Write(ref attempts, retained + 1);
                Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"CLROS_SERVICE_QUOTA_ATTEMPT index={retained} retainedBytes={retained * (long)ChunkBytes} gcCommitted={GC.GetGCMemoryInfo().TotalCommittedBytes}"));
                nint mapping = MapNative(0, ChunkBytes, 3, 0x22, -1, 0);
                if (mapping == -1)
                {
                    int error = Marshal.GetLastPInvokeError();
                    throw new InvalidOperationException("CLROS_QUOTA_RETURNED errno=" + error.ToString(CultureInfo.InvariantCulture));
                }
                held[retained++] = mapping;
                Fill(mapping, (byte)retained);
                int collectionsBefore = GC.CollectionCount(GC.MaxGeneration);
                if (retained <= 4)
                {
                    // Force bounded real managed pressure while the peer keeps
                    // asynchronous requests open and native mappings retained.
                    byte[] pressure = new byte[65536];
                    pressure.AsSpan().Fill(0x6d);
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                    if (pressure.AsSpan().IndexOfAnyExcept((byte)0x6d) >= 0)
                        throw new InvalidOperationException("CLROS_QUOTA_GC_CONTENT");
                    GC.KeepAlive(pressure);
                }
                int collectionsAfter = GC.CollectionCount(GC.MaxGeneration);
                if (retained <= 4 && collectionsAfter <= collectionsBefore)
                    throw new InvalidOperationException("CLROS_QUOTA_GC_PROGRESS");
                if (retained <= 4) Interlocked.Add(ref gcCollections, collectionsAfter - collectionsBefore);
                Interlocked.Exchange(ref retainedBytes, retained * (long)ChunkBytes);
                Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"CLROS_SERVICE_QUOTA_RETAINED index={retained - 1} retainedBytes={retained * (long)ChunkBytes} collectionsBefore={collectionsBefore} collectionsAfter={collectionsAfter} gcCommitted={GC.GetGCMemoryInfo().TotalCommittedBytes}"));
                // A real timer/ThreadPool wait lets held async requests and
                // independently checked healthy peer traffic advance.
                await Task.Delay(250, cancellation);
            }
        }
        finally
        {
            long remainingBytes = 0;
            for (int i = 0; i < retained; i++)
            {
                if (UnmapNative(held[i], ChunkBytes) != 0) { released = false; remainingBytes += ChunkBytes; }
                else held[i] = 0;
            }
            Interlocked.Exchange(ref retainedBytes, remainingBytes);
            Volatile.Write(ref started, 2);
            Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"CLROS_SERVICE_QUOTA_SURVIVED retainedBytes={retained * (long)ChunkBytes} released={released}"));
        }
        throw new InvalidOperationException("CLROS_QUOTA_NOT_ENFORCED");
    }

    private static unsafe void Fill(nint address, byte value) => new Span<byte>((void*)address, ChunkBytes).Fill(value);

    [DllImport("libc", EntryPoint = "mmap", SetLastError = true)]
    private static extern nint MapNative(nint address, nuint bytes, int protection, int flags, int descriptor, long offset);
    [DllImport("libc", EntryPoint = "munmap", SetLastError = true)]
    private static extern int UnmapNative(nint address, nuint bytes);
}
