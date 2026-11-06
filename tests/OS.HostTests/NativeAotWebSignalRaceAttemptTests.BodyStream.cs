using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OS.Tooling;

// Deterministic HTTP-handler controls exercise owned logical requests and
// retained bytes only. They establish no real socket, signal or guest result.
internal static partial class NativeAotWebSignalRaceAttemptTests
{
    private sealed class BodyStream(byte[] prefix, string terminal, CancellationTokenSource? stop = null) : Stream
    {
        private int offset;
        internal bool Closed;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException(); set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int start, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (offset < prefix.Length)
            {
                int count = Math.Min(buffer.Length, prefix.Length - offset);
                prefix.AsMemory(offset, count).CopyTo(buffer);
                offset += count;
                return count;
            }

            if (terminal == "eof")
                return 0;
            if (terminal == "fault")
                throw new IOException("secret-rich I/O message must not enter evidence");
            if (terminal == "cancel")
                stop!.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Unexpected pending stream completion");
        }

        protected override void Dispose(bool disposing)
        {
            Closed = true;
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
