using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    private sealed class WireStream(WireCapture owner, Stream inner, int id) : Stream
    {
        private int disposed;
        internal int Id => id;
        public override bool CanRead => inner.CanRead;
        public override bool CanWrite => inner.CanWrite;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException(); set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellation) => inner.FlushAsync(cancellation);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            object op = owner.Begin(id, false, buffer.Length);
            int count;
            try
            {
                count = inner.Read(buffer);
            }
            catch (Exception error)
            {
                owner.Finish(op, -1, [], error);
                throw;
            }

            owner.Finish(op, count, buffer[..count], null);
            return count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            object op = owner.Begin(id, true, buffer.Length, buffer);
            try
            {
                inner.Write(buffer);
            }
            catch (Exception error)
            {
                owner.Finish(op, -1, [], error);
                throw;
            }

            owner.Finish(op, buffer.Length, [], null);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellation) => ReadAsync(buffer.AsMemory(offset, count), cancellation).AsTask();
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellation) => WriteAsync(buffer.AsMemory(offset, count), cancellation).AsTask();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
        {
            object op = owner.Begin(id, false, buffer.Length);
            int count;
            try
            {
                count = await inner.ReadAsync(buffer, cancellation);
            }
            catch (Exception error)
            {
                owner.Finish(op, -1, [], error);
                throw;
            }

            owner.Finish(op, count, buffer.Span[..count], null);
            return count;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellation = default)
        {
            object op = owner.Begin(id, true, buffer.Length, buffer.Span);
            try
            {
                await inner.WriteAsync(buffer, cancellation);
            }
            catch (Exception error)
            {
                owner.Finish(op, -1, [], error);
                throw;
            }

            owner.Finish(op, buffer.Length, [], null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref disposed, 1) == 0)
            {
                try
                {
                    inner.Dispose();
                }
                finally
                {
                    owner.Closed(id);
                }
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
