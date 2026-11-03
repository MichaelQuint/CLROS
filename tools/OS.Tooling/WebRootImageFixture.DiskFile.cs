using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;
public static partial class WebRootImageFixture
{
    public sealed record DiskFile(string Path, uint Bytes, string Sha256, byte Attributes, ushort FatDate, ushort FatTime, Extent[] Extents);
}
