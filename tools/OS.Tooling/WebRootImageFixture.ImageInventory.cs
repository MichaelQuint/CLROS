using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;

public static partial class WebRootImageFixture
{
    public sealed record ImageInventory(string Sha256, ulong PartitionFirstLba, ulong PartitionLastLba, DiskFile[] Files);
}
