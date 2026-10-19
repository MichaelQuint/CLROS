using System.Diagnostics;
using System.Runtime.InteropServices;

// This workload witness owns one actual OS thread. Counts describe successful
// direct libc calls; the guest qualifier must independently join actual syscall
// instruction/identity/clock/worker and external packet evidence. No fake clock
// or getrandom substitute belongs in this fixture.
internal sealed class ServiceExercise(Action failed) : IDisposable
{
    private readonly object gate = new();
    private readonly ManualResetEventSlim stop = new(false);
    private Thread? worker;
    private long started, ended, calls, randomRequests, randomBytes, iterations;
    private int phase, failure, completed, managedWorkerId, disposed;

    internal bool Completed => Volatile.Read(ref completed) == 1 && Volatile.Read(ref phase) == 4 && Volatile.Read(ref failure) == 0;

    internal bool Start()
    {
        lock (gate)
        {
            if (phase != 0 || disposed != 0) return false;
            started = Stopwatch.GetTimestamp(); phase = 1;
            try
            {
                worker = new Thread(Run, 256 * 1024) { IsBackground = false };
                worker.Start(); return true;
            }
            catch { worker = null; phase = 3; failure = 70; failed(); throw; }
        }
    }

    internal ServiceSnapshot Read()
    {
        long begin = Interlocked.Read(ref started), end = Interlocked.Read(ref ended);
        long elapsed = begin == 0 ? 0 : (long)Stopwatch.GetElapsedTime(begin, end == 0 ? Stopwatch.GetTimestamp() : end).TotalMilliseconds;
        return new("clros.service.v1", Volatile.Read(ref phase), Volatile.Read(ref failure),
            Volatile.Read(ref managedWorkerId), Interlocked.Read(ref calls), Interlocked.Read(ref randomRequests),
            Interlocked.Read(ref randomBytes), Interlocked.Read(ref iterations), elapsed);
    }

    internal bool TryComplete()
    {
        lock (gate)
        {
            var value = Read();
            if (value.Phase != 1 || value.Failure != 0 || value.ElapsedMilliseconds <= 300_000 ||
                value.Calls <= 4096 || value.RandomBytes <= 4096 || value.Iterations == 0 || worker == null || !worker.IsAlive) return false;
            phase = 2; stop.Set();
            if (!worker.Join(TimeSpan.FromSeconds(30))) Environment.Exit(71);
            if (phase != 4 || failure != 0) return false;
            completed = 1; return true;
        }
    }

    internal void RequestStop() => stop.Set();

    private unsafe void Run()
    {
        byte[]? random = null;
        try
        {
            random = new byte[16384];
            Volatile.Write(ref managedWorkerId, Environment.CurrentManagedThreadId);
            random.AsSpan().Fill(0xa7);
            fixed (byte* output = random)
            {
                if (GetRandom(output, 0, 1) != 0 || random[0] != 0xa7) { Reject(320); return; }
                if (GetRandom(output, 1, 8) != -1 || Marshal.GetLastPInvokeError() != 22 || random[0] != 0xa7) { Reject(321); return; }
                if (GetRandom(null, 32, 1) != -1 || Marshal.GetLastPInvokeError() != 14) { Reject(322); return; }
            }
            Console.Error.WriteLine("CLROS_SERVICE_RANDOM_CONTROLS zero=0 invalid=-22 fault=-14");
            while (!stop.IsSet)
            {
                // Reach the direct-call threshold in bounded batches instead
                // of assuming a scheduler or serial-trace rate. Continue one
                // real call per iteration throughout the remaining >300s run.
                int batch = Interlocked.Read(ref calls) <= 4096 ? 32 : 1;
                for (int i = 0; i < batch; i++)
                {
                    if (GetPid() <= 0) { Reject(39); return; }
                    Interlocked.Increment(ref calls);
                }
                long iteration = Interlocked.Read(ref iterations);
                if (iteration < 2 || Interlocked.Read(ref randomRequests) <= 32 || (iteration & 15) == 0)
                {
                    nuint wanted = iteration < 2 ? (nuint)random.Length : 32;
                    nint received;
                    random.AsSpan().Fill(0xa7);
                    fixed (byte* output = random)
                        received = GetRandom(output, wanted, 1);
                    // The same original fixture permits Linux's full result and
                    // the CLROS adapter's bounded8192-byte prefix. Native syscall
                    // replay must independently require the actual guest policy.
                    if (received != (nint)wanted && !(wanted == 16384 && received == 8192)) { Reject(318); return; }
                    for (int i = (int)received; i < random.Length; i++)
                        if (random[i] != 0xa7) { Reject(319); return; }
                    Interlocked.Increment(ref randomRequests); Interlocked.Add(ref randomBytes, (long)received);
                    random.AsSpan().Clear(); // Never return random output through HTTP/serial.
                }
                Interlocked.Increment(ref iterations);
                if (Stopwatch.GetElapsedTime(Interlocked.Read(ref started)) >= TimeSpan.FromSeconds(600)) { Reject(600); return; }
                stop.Wait(100);
            }
        }
        catch { Reject(70); }
        finally
        {
            if (random != null) random.AsSpan().Clear();
            Interlocked.Exchange(ref ended, Stopwatch.GetTimestamp());
            Volatile.Write(ref phase, Volatile.Read(ref failure) == 0 ? 4 : 3);
        }
    }

    private void Reject(int reason)
    {
        Volatile.Write(ref failure, reason); stop.Set();
        Console.Error.WriteLine($"CLROS_SERVICE_FAIL reason={reason}"); failed();
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed != 0) return;
            stop.Set();
            if (worker != null && !worker.Join(TimeSpan.FromSeconds(30))) Environment.Exit(71);
            disposed = 1; stop.Dispose();
        }
    }

    [DllImport("libc", EntryPoint = "getpid")]
    private static extern int GetPid();
    [DllImport("libc", EntryPoint = "getrandom", SetLastError = true)]
    private static extern unsafe nint GetRandom(byte* output, nuint bytes, uint flags);
}

// A detached numeric observation, with no output bytes, keys, pointers or
// authority to acknowledge kernel exit/reap/resource cleanup.
// Phase:0 idle,1 running,2 stopping,3 failed,4 stopped.
internal sealed record ServiceSnapshot(string Contract, int Phase, int Failure, int ManagedWorkerId,
    long Calls, long RandomRequests, long RandomBytes, long Iterations, long ElapsedMilliseconds);
