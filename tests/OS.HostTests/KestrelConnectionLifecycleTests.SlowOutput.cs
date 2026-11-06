using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Tooling;

internal static partial class KestrelConnectionLifecycleTests
{
    private sealed class SlowOutput : MemoryStream
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Writes, LargestWrite;
        internal bool Closed;
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellation = default)
        {
            Writes++;
            LargestWrite = Math.Max(LargestWrite, buffer.Length);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellation);
            await base.WriteAsync(buffer, cancellation);
        }

        protected override void Dispose(bool disposing)
        {
            Closed = true;
            base.Dispose(disposing);
        }
    }
}
