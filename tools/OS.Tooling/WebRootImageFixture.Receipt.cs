using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;

public static partial class WebRootImageFixture
{
    public sealed record Receipt(string Variant, string OriginalSha256, string FinalSha256, string PublishedInventorySha256, int PublishedFileCount, long PublishedBytes, int MaximumPublishedFileBytes, ulong PartitionFirstLba, ulong PartitionLastLba, string UtcTimestampPolicy, DiskFile[] WebRootFiles, DiskFile[] PreservedFiles, DiskFile Sentinel);
}
