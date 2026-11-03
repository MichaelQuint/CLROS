using System.Text;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// A one-shot bounded diagnostic, never run by ordinary HTTP requests. Eight
// owned threads run in two groups of four so runtime threads retain capacity;
// each group rendezvous before writing and joins before the next group starts.
// The Linux-only native call observes the real writev result and errno rather
// than translating a managed stream error into an assumed descriptor result.
internal static partial class LinuxLoggingExercise
{
    private static int started;
    internal static IResult Run()
    {
        if (!OperatingSystem.IsLinux())
            return Results.StatusCode(501);
        if (Interlocked.Exchange(ref started, 1) != 0)
            return Results.StatusCode(409);
        int failed = 0;
        const int groupSize = 4;
        for (int first = 0; first < 8; first += groupSize)
        {
            using var ready = new CountdownEvent(groupSize);
            using var release = new ManualResetEventSlim(false);
            var threads = new Thread[groupSize];
            int startedThreads = 0;
            bool allReady;
            try
            {
                for (int worker = 0; worker < threads.Length; worker++)
                {
                    int identity = first + worker;
                    threads[worker] = new Thread(() =>
                    {
                        try
                        {
                            ready.Signal();
                            if (!release.Wait(TimeSpan.FromSeconds(30)))
                            {
                                Interlocked.Exchange(ref failed, 1);
                                return;
                            }

                            for (int record = 0; record < 4; record++)
                            {
                                Console.Out.WriteLine($"CLROS_LOG fd=1 worker={identity} record={record}");
                                if (!WriteRecord(identity, record))
                                    Interlocked.Exchange(ref failed, 1);
                            }
                        }
                        catch
                        {
                            Interlocked.Exchange(ref failed, 1);
                        }
                    }, 256 * 1024)
                    {
                        IsBackground = true
                    };
                    threads[worker].Start();
                    startedThreads++;
                }

                allReady = ready.Wait(TimeSpan.FromSeconds(30));
            }
            finally
            {
                release.Set();
                for (int worker = 0; worker < startedThreads; worker++)
                    // A stuck writer must not outlive this diagnostic or its events.
                    // Terminate the owned fixture process rather than return HTTP.
                    if (!threads[worker].Join(TimeSpan.FromSeconds(30)))
                        Environment.Exit(70);
            }

            if (!allReady || failed != 0)
                return Results.StatusCode(500);
        }

        if (!InvalidDescriptor())
            return Results.StatusCode(500);
        return Results.Text("CLROS_LOG_OK workers=8 records=64 writev=32 invalid_errno=9", "text/plain");
    }

    private static unsafe bool WriteRecord(int worker, int record)
    {
        byte[] bytes = Encoding.ASCII.GetBytes($"CLROS_LOG fd=2 worker={worker} record={record}");
        byte newline = (byte)'\n';
        fixed (byte* body = bytes)
        {
            Vector* vectors = stackalloc Vector[2];
            vectors[0] = new Vector
            {
                Address = body,
                Length = (nuint)bytes.Length
            };
            vectors[1] = new Vector
            {
                Address = &newline,
                Length = 1
            };
            return WriteVector(2, vectors, 2) == bytes.Length + 1;
        }
    }

    private static unsafe bool InvalidDescriptor()
    {
        byte value = (byte)'!';
        var vector = new Vector
        {
            Address = &value,
            Length = 1
        };
        return WriteVector(1234, &vector, 1) == -1 && Marshal.GetLastPInvokeError() == 9;
    }

    [DllImport("libc", EntryPoint = "writev", SetLastError = true)]
    private static extern unsafe nint WriteVector(int descriptor, Vector* vectors, int count);
}
