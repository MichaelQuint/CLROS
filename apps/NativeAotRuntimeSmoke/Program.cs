Console.WriteLine("TEST_START nativeaot.runtime");
try
{
    int cleanups = 0;
    var original = new InvalidOperationException("nativeaot.runtime.original");
    try
    {
        try { throw original; }
        catch (InvalidOperationException) { throw; }
        finally { cleanups++; }
    }
    catch (InvalidOperationException caught)
    {
        Require(ReferenceEquals(original, caught) && cleanups == 1, "exception_identity");
    }
    Console.WriteLine("NATIVEAOT_RUNTIME_EH_OK");

    using var started = new ManualResetEventSlim();
    using var release = new ManualResetEventSlim();
    Exception? threadFailure = null;
    ThreadLocalState.Value = 17;
    var thread = new Thread(() =>
    {
        try
        {
            Require(ThreadLocalState.Value == 0, "tls_initial_value");
            ThreadLocalState.Value = 29;
            byte[] rooted = new byte[4 * 1024 * 1024];
            rooted[0] = 37; rooted[^1] = 41;
            started.Set();
            Require(release.Wait(TimeSpan.FromSeconds(60)), "thread_release_timeout");
            Require(ThreadLocalState.Value == 29 && rooted[0] == 37 && rooted[^1] == 41, "thread_gc_roots");
            GC.KeepAlive(rooted);
        }
        catch (Exception error) { threadFailure = error; started.Set(); }
    }) { IsBackground = true };
    thread.Start();
    try
    {
        Require(started.Wait(TimeSpan.FromSeconds(60)), "thread_start_timeout");
        for (int i = 0; i < 8; i++)
        {
            byte[] bytes = new byte[4 * 1024 * 1024];
            bytes[i] = (byte)(i + 1);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Require(bytes[i] == i + 1, "main_gc_roots");
            GC.KeepAlive(bytes);
        }
        Require(ThreadLocalState.Value == 17, "tls_separation");
    }
    finally { release.Set(); }
    Require(thread.Join(TimeSpan.FromSeconds(60)) && threadFailure is null, "thread_completion");
    Console.WriteLine("NATIVEAOT_RUNTIME_GC_THREADS_TLS_OK");

    int result = await Task.Run(() => 43);
    Require(result == 43, "threadpool_task");
    var tick = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    using (var timer = new Timer(_ => tick.TrySetResult(true), null, TimeSpan.FromMilliseconds(10), Timeout.InfiniteTimeSpan))
        Require(await tick.Task.WaitAsync(TimeSpan.FromSeconds(60)), "timer");
    Console.WriteLine("NATIVEAOT_RUNTIME_TASK_TIMER_OK");
    Console.WriteLine("TEST_PASS nativeaot.runtime");
    return 0;
}
catch (Exception error)
{
    Console.WriteLine("TEST_FAIL nativeaot.runtime " + error.GetType().Name + " " + error.Message.ReplaceLineEndings(" "));
    return 1;
}

static void Require(bool condition, string reason)
{
    if (!condition) throw new InvalidOperationException(reason);
}

internal static class ThreadLocalState
{
    [ThreadStatic] internal static int Value;
}
