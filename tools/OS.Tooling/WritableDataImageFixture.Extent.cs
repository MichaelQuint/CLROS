using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Std.Hash;
using OS.Std.Time;
using OS.Storage;

namespace OS.Tooling;

public static partial class WritableDataImageFixture
{
    public sealed record Extent(uint FirstCluster, uint Clusters, ulong FirstLba);
}
