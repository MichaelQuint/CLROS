using System.Collections.Concurrent;
using System.Diagnostics;

internal static class KestrelRequestChurnTests
{
    // These controls execute the actual managed helper only; they construct no
    // original HTTP, scheduler, native, packet or CLROS guest authority.
    [HostTest("m10-012 literal churn repeats real timer ThreadPool full-GC work with bounded retained roots")]
    private static async Task CompleteAndRepeat()
    {
        var exercise = new KestrelChurnExercise();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        for (int i = 0; i < 2; i++) HostCheck.Equal(KestrelChurnExercise.SuccessBody, await exercise.RunAsync(cancellation.Token));
        var observed = exercise.Inspect();
        HostCheck.True(observed.Started == 2 && observed.Completed == 2 && observed.Canceled == 0 && observed.Failed == 0 &&
            observed.Timers == 16 && observed.Workers == 16 && observed.Collections == 16 && observed.PeakActive == 1 &&
            observed.PeakHeldBytes == 65536);
        Drained(exercise);
        string status = exercise.Read();
        foreach (string field in new[] { "active=0", "waiting=0", "completed=2", "canceled=0", "failed=0", "timers=16", "workers=16", "collections=16" })
            HostCheck.True(status.Split(' ').Contains(field));
        HostCheck.True(status.StartsWith("CLROS_CHURN_STATUS ", StringComparison.Ordinal));
    }

    [HostTest("m10-012 literal churn propagates pending cancellation then drains and accepts a fresh request")]
    private static async Task CancelAndRecover()
    {
        var exercise = new KestrelChurnExercise(); using var cancellation = new CancellationTokenSource();
        var held = new HeldContext(); SynchronizationContext? previous = SynchronizationContext.Current;
        Task<string?>? request = null; KestrelChurnExercise.Snapshot pending = default;
        try
        {
            SynchronizationContext.SetSynchronizationContext(held);
            request = exercise.RunAsync(cancellation.Token);
            pending = exercise.Inspect();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); cancellation.Cancel(); }
        await held.JoinAsync(request!); HostCheck.True(request!.IsCanceled && pending is { Active: 1, Waiting: 1, Completed: 0 });
        HostCheck.True(exercise.Inspect() is { Canceled: 1, Failed: 0, Completed: 0 }); Drained(exercise);
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        HostCheck.Equal(KestrelChurnExercise.SuccessBody, await exercise.RunAsync(recovery.Token));
        HostCheck.True(exercise.Inspect() is { Started: 2, Completed: 1, Canceled: 1, Failed: 0 }); Drained(exercise);
    }

    [HostTest("m10-012 literal churn capacity refuses a third request while two actual timer continuations are held")]
    private static async Task Capacity()
    {
        var exercise = new KestrelChurnExercise(); using var cancellation = new CancellationTokenSource();
        var held = new HeldContext(); SynchronizationContext? previous = SynchronizationContext.Current;
        List<Task<string?>> requests = []; KestrelChurnExercise.Snapshot pending = default; bool busyRefused = false;
        try
        {
            SynchronizationContext.SetSynchronizationContext(held);
            requests.Add(exercise.RunAsync(cancellation.Token)); requests.Add(exercise.RunAsync(cancellation.Token));
            var busy = exercise.RunAsync(cancellation.Token);
            requests.Add(busy); busyRefused = busy.IsCompletedSuccessfully && busy.Result is null;
            pending = exercise.Inspect();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); cancellation.Cancel(); }
        await held.JoinAsync(requests.ToArray()); HostCheck.True(requests.Take(2).All(r => r.IsCanceled) && busyRefused);
        HostCheck.True(pending is { Active: 2, Waiting: 2, PeakActive: 2, Started: 2, Rejected: 1,
            HeldBytes: 131072, PeakHeldBytes: 131072, Completed: 0 });
        HostCheck.True(exercise.Inspect() is { Canceled: 2, Failed: 0, Completed: 0, PeakActive: 2 }); Drained(exercise);
    }

    [HostTest("m10-012 literal churn validates cancellation before admission and every retained payload byte")]
    private static async Task PreflightAndPayload()
    {
        var exercise = new KestrelChurnExercise(); bool rejected = false;
        try { await exercise.RunAsync(CancellationToken.None); } catch (ArgumentException) { rejected = true; }
        HostCheck.True(rejected); using var canceled = new CancellationTokenSource(); canceled.Cancel(); rejected = false;
        try { await exercise.RunAsync(canceled.Token); } catch (OperationCanceledException) { rejected = true; }
        HostCheck.True(rejected && exercise.Inspect() is { Started: 0, Canceled: 0, Failed: 0, Rejected: 0 }); Drained(exercise);
        byte[] independent = new byte[65536];
        for (int i = 0; i < independent.Length; i++) independent[i] = unchecked((byte)(23 + i * 17));
        HostCheck.True(KestrelChurnExercise.PayloadIntact(independent, 23));
        foreach (int at in new[] { 0, 32768, 65535 })
        {
            independent[at] ^= 1; HostCheck.True(!KestrelChurnExercise.PayloadIntact(independent, 23)); independent[at] ^= 1;
        }
        HostCheck.True(!KestrelChurnExercise.PayloadIntact(independent.AsSpan(1), 23) &&
            !KestrelChurnExercise.PayloadIntact(independent, 24) && !KestrelChurnExercise.PayloadIntact([], 23));
    }

    [HostTest("event-owned churn remains active past eight real iterations until request cancellation then finite churn adds exactly eight")]
    private static async Task UntilRequestedCancellation()
    {
        var exercise = new KestrelChurnExercise(); using var request = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task<string?> running = exercise.RunUntilCanceledAsync(request.Token);
        try
        {
            while (exercise.Inspect() is not { Workers: >= 9, Waiting: 1 })
            { request.Token.ThrowIfCancellationRequested(); HostCheck.True(!running.IsCompleted); await Task.Delay(1, request.Token); }
            HostCheck.True(!running.IsCompleted && exercise.Inspect() is { Active: 1, HeldBytes: 65536, Completed: 0 });
            request.Cancel(); bool canceled = false;
            try { await running; } catch (OperationCanceledException) when (request.IsCancellationRequested) { canceled = true; }
            HostCheck.True(canceled && exercise.Inspect() is { Canceled: 1, Failed: 0, Completed: 0 }); Drained(exercise);
            var before = exercise.Inspect(); using var finite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            HostCheck.True(await exercise.RunAsync(finite.Token) == KestrelChurnExercise.SuccessBody);
            var after = exercise.Inspect();
            HostCheck.True(after.Completed == 1 && after.Timers == before.Timers + 8 && after.Workers == before.Workers + 8 &&
                after.Collections == before.Collections + 8 && after.Canceled == 1 && after.Failed == 0); Drained(exercise);
        }
        finally
        {
            request.Cancel(); try { await running; } catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        }
    }

    [HostTest("event-owned churn watchdog expiration fails and drains without counting requested cancellation")]
    private static async Task WatchdogFailure()
    {
        var exercise = new KestrelChurnExercise(); using var request = new CancellationTokenSource();
        var held = new HeldContext(); SynchronizationContext? previous = SynchronizationContext.Current; Task<string?> running;
        try
        {
            SynchronizationContext.SetSynchronizationContext(held);
            running = exercise.RunUntilCanceledAsync(request.Token, TimeSpan.FromMilliseconds(1));
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        await Task.Delay(40); bool failed = false;
        try { await held.JoinAsync(running); } catch (TimeoutException error) { failed = error.Message == "CLROS_CHURN_FAIL deadline"; }
        HostCheck.True(failed && !request.IsCancellationRequested && running.IsFaulted &&
            exercise.Inspect() is { Failed: 1, Canceled: 0, Completed: 0 }); Drained(exercise);
        foreach (TimeSpan budget in new[] { TimeSpan.Zero, TimeSpan.FromSeconds(121) })
        {
            bool rejected = false; try { await exercise.RunUntilCanceledAsync(request.Token, budget); } catch (ArgumentOutOfRangeException) { rejected = true; }
            HostCheck.True(rejected);
        }
        HostCheck.Equal(1L, exercise.Inspect().Started); Drained(exercise);
    }

    [HostTest("churn snapshots conserve admitted requests through completion cancellation and watchdog failure")]
    private static async Task ConcurrentTerminalSnapshots()
    {
        var exercise = new KestrelChurnExercise();
        using var stop = new CancellationTokenSource();
        using var observing = new ManualResetEventSlim();
        long snapshots = 0;
        Task<bool> reader = Task.Factory.StartNew(() =>
        {
            observing.Set();
            while (!stop.IsCancellationRequested)
            {
                var value = exercise.Inspect(); snapshots++;
                if (value.Started != value.Active + value.Completed + value.Canceled + value.Failed ||
                    value.Waiting > value.Active || value.HeldBytes > value.Active * 65536L) return false;
            }
            return true;
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            observing.Wait();
            for (int i = 0; i < 64; i++)
            {
                using var request = new CancellationTokenSource();
                Task<string?> running = exercise.RunUntilCanceledAsync(request.Token);
                request.Cancel();
                try { await running; } catch (OperationCanceledException) when (request.IsCancellationRequested) { }
                HostCheck.True(running.IsCanceled);
            }
            using var finite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            HostCheck.Equal(KestrelChurnExercise.SuccessBody, await exercise.RunAsync(finite.Token));
            using var failure = new CancellationTokenSource(); bool expired = false;
            var held = new HeldContext(); SynchronizationContext? previous = SynchronizationContext.Current; Task<string?> failing;
            try
            {
                SynchronizationContext.SetSynchronizationContext(held);
                failing = exercise.RunUntilCanceledAsync(failure.Token, TimeSpan.FromMilliseconds(1));
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
            await Task.Delay(40);
            try { await held.JoinAsync(failing); }
            catch (TimeoutException error) { expired = error.Message == "CLROS_CHURN_FAIL deadline"; }
            HostCheck.True(expired && !failure.IsCancellationRequested && failing.IsFaulted &&
                exercise.Inspect() is { Started: 66, Completed: 1, Canceled: 64, Failed: 1 });
            Drained(exercise);
        }
        finally { stop.Cancel(); HostCheck.True(await reader && snapshots > 0); }
    }

    private static void Drained(KestrelChurnExercise exercise) =>
        HostCheck.True(exercise.Inspect() is { Active: 0, Waiting: 0, HeldBytes: 0 });

    // The timers and token registrations are real. Holding only their resumed
    // continuations makes admission/cancellation assertions independent of wall
    // scheduling; every queued callback/task is joined before this owner ends.
    private sealed class HeldContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new();
        public override void Post(SendOrPostCallback callback, object? state) => queue.Enqueue((callback, state));
        internal async Task JoinAsync(params Task[] requests)
        {
            Task all = Task.WhenAll(requests); long began = Stopwatch.GetTimestamp();
            while (!all.IsCompleted)
            {
                while (queue.TryDequeue(out var item)) item.Callback(item.State);
                if (Stopwatch.GetElapsedTime(began) > TimeSpan.FromSeconds(10)) throw new TimeoutException("churn continuation drain");
                await Task.Yield();
            }
            while (queue.TryDequeue(out var item)) item.Callback(item.State);
            try { await all; } catch (OperationCanceledException) when (requests.All(t => t.IsCanceled || t.IsCompletedSuccessfully)) { }
        }
    }
}
