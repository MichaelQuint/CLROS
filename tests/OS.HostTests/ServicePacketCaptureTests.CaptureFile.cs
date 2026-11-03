using System.Buffers.Binary;
using OS.Tools.QemuRunner;

internal static partial class ServicePacketCaptureTests
{
    private sealed class CaptureFile : IDisposable
    {
        internal readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "clros-service-pcap-" + Guid.NewGuid().ToString("N"));
        private readonly FileStream writer;
        internal CaptureFile() => writer = new(Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
        internal void Append(ReadOnlySpan<byte> bytes, bool flush = true)
        {
            writer.Position = writer.Length;
            writer.Write(bytes);
            if (flush)
                writer.Flush();
        }

        internal void Rewrite(long offset, ReadOnlySpan<byte> bytes)
        {
            writer.Position = offset;
            writer.Write(bytes);
            writer.Flush();
        }

        internal void Truncate(long length)
        {
            writer.SetLength(length);
            writer.Flush();
        }

        internal void Flush() => writer.Flush();
        public void Dispose()
        {
            writer.Dispose();
            File.Delete(Path);
        }
    }
}
