using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Abi = KestrelSignalProbeAbi;

internal sealed partial class KestrelSignalProbe
{
    /// <summary>One linked absolute operation/cleanup budget. Borrowers may wait
    /// on gates or join within the remaining budget; no call refreshes it.
    /// Cancellation cannot wake native epoll: the request's finally owns that
    /// wake and every delegate join. Dispose only after those borrows retire.</summary>

    internal sealed class EventLifetime : IDisposable
    {
        private readonly CancellationTokenSource stop;
        private readonly long started;
        private readonly TimeSpan budget;
        internal EventLifetime(CancellationToken request, TimeSpan? budget = null)
        {
            this.budget = budget ?? TimeSpan.FromSeconds(120);
            if (this.budget <= TimeSpan.Zero || this.budget > TimeSpan.FromSeconds(120))
                throw new ArgumentOutOfRangeException(nameof(budget));
            started = System.Diagnostics.Stopwatch.GetTimestamp();
            stop = CancellationTokenSource.CreateLinkedTokenSource(request);
            stop.CancelAfter(this.budget);
        }

        internal CancellationToken Token => stop.Token;

        internal void Check()
        {
            if (System.Diagnostics.Stopwatch.GetElapsedTime(started) >= budget)
                stop.Cancel();
            stop.Token.ThrowIfCancellationRequested();
        }

        internal void Wait(ManualResetEventSlim gate)
        {
            ArgumentNullException.ThrowIfNull(gate);
            Check();
            gate.Wait(stop.Token);
            Check();
        }

        internal bool Join(Thread thread)
        {
            ArgumentNullException.ThrowIfNull(thread);
            TimeSpan remaining = budget - System.Diagnostics.Stopwatch.GetElapsedTime(started);
            if (stop.IsCancellationRequested || remaining <= TimeSpan.Zero)
                return false;
            bool joined = thread.Join(remaining < TimeSpan.FromSeconds(5) ? remaining : TimeSpan.FromSeconds(5));
            return joined && !stop.IsCancellationRequested && System.Diagnostics.Stopwatch.GetElapsedTime(started) < budget;
        }

        public void Dispose() => stop.Dispose();
    }
}
