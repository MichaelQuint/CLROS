using System.Collections.Concurrent;
using System.Diagnostics;

internal static partial class KestrelRequestChurnTests
{
    // The timers and token registrations are real. Holding only their resumed
    // continuations makes admission/cancellation assertions independent of wall
    // scheduling; every queued callback/task is joined before this owner ends.
    private sealed class HeldContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new();
        public override void Post(SendOrPostCallback callback, object? state) => queue.Enqueue((callback, state));
        internal async Task JoinAsync(params Task[] requests)
        {
            Task all = Task.WhenAll(requests);
            long began = Stopwatch.GetTimestamp();
            while (!all.IsCompleted)
            {
                while (queue.TryDequeue(out var item))
                    item.Callback(item.State);
                if (Stopwatch.GetElapsedTime(began) > TimeSpan.FromSeconds(10))
                    throw new TimeoutException("churn continuation drain");
                await Task.Yield();
            }

            while (queue.TryDequeue(out var item))
                item.Callback(item.State);
            try
            {
                await all;
            }
            catch (OperationCanceledException)when (requests.All(t => t.IsCanceled || t.IsCompletedSuccessfully))
            {
            }
        }
    }
}
