using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Tooling;

internal static partial class KestrelConnectionLifecycleTests
{
    private sealed class ShortInput(byte[] bytes) : MemoryStream(bytes)
    {
        internal int Reads, LargestRequest;
        internal bool Closed;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
        {
            Reads++;
            LargestRequest = Math.Max(LargestRequest, buffer.Length);
            return base.ReadAsync(buffer[..Math.Min(17, buffer.Length)], cancellation);
        }

        protected override void Dispose(bool disposing)
        {
            Closed = true;
            base.Dispose(disposing);
        }
    }
}
