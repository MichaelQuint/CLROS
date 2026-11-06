internal static partial class HttpWorkloadBenchmarkTests
{
    private sealed class BodyStream(byte[] body, string mode, Action onDispose) : MemoryStream(body, writable: false)
    {
        private bool disposed;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Position != 0 && mode == "blocked")
                await Task.Delay(Timeout.Infinite, cancellation);
            if (Position != 0 && mode == "interrupted")
                throw new IOException("synthetic interrupted response");
            return Read(buffer.Span[..Math.Min(buffer.Length, mode == "good" ? 17 : 1)]);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
                onDispose();
            }
            base.Dispose(disposing);
        }
    }
}
