using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;

public static partial class WebRootImageFixture
{
    public sealed record Snapshot(Asset[] Files, long TotalBytes, int MaximumFileBytes, string Sha256);
}
