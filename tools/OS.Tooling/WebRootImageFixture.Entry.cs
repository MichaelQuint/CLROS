using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;
public static partial class WebRootImageFixture
{
    private sealed record Entry(string Name, string Alias, uint Cluster, uint Bytes, byte Attributes, ushort Date, ushort Time, int[] SlotOffsets);
}
