internal static class KestrelSignalRaceLifetimeTests
{
    // Real managed gates/threads only. These controls establish no native wait,
    // signal, descriptor, original syscall order or guest authority.
    [HostTest("m10-012 event lifetime caller cancellation releases an actual managed gate wait")]
    private static void CallerCancellation()
    {
        using var request = new CancellationTokenSource();
        WaitCanceled(request, TimeSpan.FromSeconds(10), callerCancels: true);
    }

    [HostTest("m10-012 event lifetime owned deadline cancels without canceling its caller")]
    private static void DeadlineCancellation()
    {
        using var request = new CancellationTokenSource();
        WaitCanceled(request, TimeSpan.FromMilliseconds(50), callerCancels: false);
        HostCheck.True(!request.IsCancellationRequested);
    }

    private static void WaitCanceled(CancellationTokenSource request, TimeSpan budget, bool callerCancels)
    {
        using var lifetime = new KestrelSignalProbe.EventLifetime(request.Token, budget);
        lifetime.Check();
        var transactionStop = lifetime.Token;
        using var gate = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim();
        Exception? failure = null;
        int canceled = 0;
        var thread = new Thread(() =>
        {
            try
            {
                entered.Set();
                lifetime.Wait(gate);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Exchange(ref canceled, 1);
            }
            catch (Exception error)
            {
                failure = error;
            }
        })
        {
            IsBackground = true
        };
        thread.Start();
        try
        {
            HostCheck.True(entered.Wait(TimeSpan.FromSeconds(5)));
            if (callerCancels)
                request.Cancel();
            HostCheck.True(thread.Join(TimeSpan.FromSeconds(5)), "managed canceled waiter must terminate");
            HostCheck.True(failure is null && canceled == 1 && transactionStop.IsCancellationRequested);
            bool entryRefused = false, completionRefused = false;
            try
            {
                transactionStop.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                entryRefused = true;
            }
            try
            {
                lifetime.Check();
            }
            catch (OperationCanceledException)
            {
                completionRefused = true;
            }
            HostCheck.True(entryRefused && completionRefused, "cached entry cancellation and absolute completion fence retain the original lifetime");
        }
        finally
        {
            gate.Set();
            if (callerCancels)
                request.Cancel();
            HostCheck.True(thread.Join(TimeSpan.FromSeconds(5)));
        }
    }

    [HostTest("m10-012 event lifetime released gate completes and Join confirms the actual thread")]
    private static void ReleasedGate()
    {
        using var lifetime = new KestrelSignalProbe.EventLifetime(CancellationToken.None, TimeSpan.FromSeconds(10));
        using var gate = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim();
        Exception? failure = null;
        int completed = 0;
        var thread = new Thread(() =>
        {
            try
            {
                entered.Set();
                lifetime.Wait(gate);
                Interlocked.Exchange(ref completed, 1);
            }
            catch (Exception error)
            {
                failure = error;
            }
        })
        {
            IsBackground = true
        };
        thread.Start();
        try
        {
            HostCheck.True(entered.Wait(TimeSpan.FromSeconds(5)));
            gate.Set();
            HostCheck.True(lifetime.Join(thread));
            HostCheck.True(!thread.IsAlive && failure is null && completed == 1 && !lifetime.Token.IsCancellationRequested);
        }
        finally
        {
            gate.Set();
            HostCheck.True(thread.Join(TimeSpan.FromSeconds(5)));
        }
    }

    [HostTest("m10-012 event lifetime expired or caller-canceled Join refuses without claiming thread completion")]
    private static void JoinRefusal()
    {
        foreach (bool deadline in new[] { false, true })
        {
            using var request = new CancellationTokenSource();
            using var lifetime = new KestrelSignalProbe.EventLifetime(request.Token,
                deadline ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromSeconds(10));
            using var release = new ManualResetEventSlim();
            using var entered = new ManualResetEventSlim();
            var thread = new Thread(() =>
{
    entered.Set();
    release.Wait();
})
            {
                IsBackground = true
            };
            thread.Start();
            try
            {
                HostCheck.True(entered.Wait(TimeSpan.FromSeconds(5)));
                if (deadline)
                    HostCheck.True(lifetime.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)));
                else
                    request.Cancel();
                HostCheck.True(!lifetime.Join(thread) && thread.IsAlive);
            }
            finally
            {
                release.Set();
                HostCheck.True(thread.Join(TimeSpan.FromSeconds(5)));
            }
        }
    }

    [HostTest("m10-012 event lifetime rejects nonpositive or over-120-second budgets and retains pre-cancellation")]
    private static void Bounds()
    {
        foreach (TimeSpan budget in new[] { TimeSpan.Zero, TimeSpan.FromTicks(-1), TimeSpan.FromSeconds(120) + TimeSpan.FromTicks(1) })
        {
            bool refused = false;
            try
            {
                using var invalid = new KestrelSignalProbe.EventLifetime(CancellationToken.None, budget);
            }
            catch (ArgumentOutOfRangeException)
            {
                refused = true;
            }
            HostCheck.True(refused);
        }
        using (var maximum = new KestrelSignalProbe.EventLifetime(CancellationToken.None, TimeSpan.FromSeconds(120)))
            HostCheck.True(maximum.Token.CanBeCanceled && !maximum.Token.IsCancellationRequested);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        using var lifetime = new KestrelSignalProbe.EventLifetime(canceled.Token);
        using var gate = new ManualResetEventSlim();
        bool observed = false;
        try
        {
            lifetime.Wait(gate);
        }
        catch (OperationCanceledException)
        {
            observed = true;
        }
        HostCheck.True(observed && lifetime.Token.IsCancellationRequested);
    }
}
