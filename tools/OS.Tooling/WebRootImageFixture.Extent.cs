using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;

public static partial class WebRootImageFixture
{
    public sealed record Extent(uint FirstCluster, uint Clusters, ulong FirstLba);
}
