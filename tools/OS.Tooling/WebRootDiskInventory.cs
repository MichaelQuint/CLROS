using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Std.Time;
using OS.Storage;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Tooling;
/// <summary>Host-only copied metadata for the confined, immutable webroot in a
/// generated GPT/FAT32 fixture. Reuses the independent image reader and numeric
/// BPB/FAT/VFAT validators. No runtime filesystem, writer object, native/process
/// owner or device receipt is constructed. Original held inputs and complete
/// guest/HTTP/sector joins remain the enclosing consumer's responsibility.</summary>

public static partial class WebRootDiskInventory
{
    public const string Root = WebRootImageFixture.GuestRoot;
    // This is a host evidence bound, not a larger runtime namespace allowance.
    public const int MaximumEntries = 4096;
    public const int MaximumMetadataObservations = 65536;
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);
    /// <summary>Synchronously borrow a stable held raw image and at most65536
    /// residual copied-sector LBAs. Require exact whole-image SHA equality with
    /// the independent inventory; return immutable classifications in input
    /// order, retaining repeated reads. Allow only the two already verified FAT
    /// ranges or exact root/app/confined directory-chain sectors. Asset sectors,
    /// including tail padding, and all reserved, preserved-file, sentinel or
    /// unallocated sectors reject. This recognizes physical metadata ownership,
    /// not whether an operation needed a lookup, its timing, bytes, cache state,
    /// original observation, device or execution authority. The caller holds the
    /// original input unchanged through final acceptance. No input alias, owner,
    /// path route, I/O or callback is retained. Invalid inputs throw ToolFailure.</summary>

    public static IReadOnlyList<MetadataSector> ClassifyMetadata(Inspection inventory, ReadOnlySpan<byte> heldImage, IReadOnlyList<ulong> residualLbas, VerifiedTruncated? truncated = null)
    {
        int count = residualLbas?.Count ?? -1;
        Need(inventory is not null && count is >= 0 and <= MaximumMetadataObservations && heldImage.Length is >= 34 * 1024 * 1024 and <= WebRootImageFixture.MaximumImageBytes && heldImage.Length % 512 == 0, "metadata_input");
        Inspection actual = inventory!;
        if (truncated is null)
            Need(Convert.ToHexString(SHA256.HashData(heldImage)) == actual.ImageSha256, "metadata_image");
        else
            truncated.RequireValues(actual, heldImage);
        MetadataSector[] result = new MetadataSector[count];
        for (int i = 0; i < count; i++)
            result[i] = actual.Classify(residualLbas![i]);
        return Array.AsReadOnly(result);
    }

    /// <summary>Synchronously borrow exclusive stable raw bytes and receipt,
    /// and independently call ReadBack on the original image path. The caller
    /// must hold that original file unchanged through its final acceptance
    /// checks; this helper adds no lease owner. Image/hash/partition and the
    /// complete readback/receipt file inventories must agree. Neither input is
    /// mutated or retained in the copied result. Throws on malformed geometry,
    /// names, mirrors, timestamp, chains, aliases, scope or incomplete inventory.
    /// Current fixture policy is512-byte sectors/clusters and <=4096 clusters
    /// per served file/directory, depth8 and path255, matching the live slice.</summary>

    public static Inspection Inspect(string originalImagePath, byte[] rawImage, WebRootImageFixture.Receipt receipt)
    {
        Need(rawImage is { Length: >= 34 * 1024 * 1024 and <= WebRootImageFixture.MaximumImageBytes } && rawImage.Length % 512 == 0 && receipt is not null && receipt.WebRootFiles is { Length: > 0 and < MaximumEntries } && receipt.PreservedFiles is not null && receipt.Sentinel is not null, "input");
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(receipt.PreservedFiles);
        ArgumentNullException.ThrowIfNull(receipt.WebRootFiles);
        string hash = Convert.ToHexString(SHA256.HashData(rawImage));
        var independent = WebRootImageFixture.ReadBack(originalImagePath);
        Need(hash == independent.Sha256 && hash == receipt.FinalSha256 && independent.PartitionFirstLba == receipt.PartitionFirstLba && independent.PartitionLastLba == receipt.PartitionLastLba && receipt.Variant is "DiskA" or "DiskB", "identity");
        var expected = receipt.PreservedFiles.Concat(receipt.WebRootFiles).Append(receipt.Sentinel!).ToArray();
        Need(expected.Length == independent.Files.Length && expected.Select(e => e.Path).Distinct(StringComparer.Ordinal).Count() == expected.Length && expected.All(e => independent.Files.Any(a => Same(e, a))), "receipt");
        Need(receipt.WebRootFiles.All(f => Canonical(f.Path) && f.Extents is not null), "receipt_scope");
        HashSet<string> admitted = new(StringComparer.Ordinal)
        {
            Root
        };
        foreach (var file in receipt.WebRootFiles)
        {
            admitted.Add(file.Path);
            string parent = file.Path;
            while ((parent = parent[..parent.LastIndexOf('/')]) != "/app")
                admitted.Add(parent);
        }

        Need(admitted.Count <= MaximumEntries, "entry_bound");
        Scanner scanner = new(rawImage, independent.PartitionFirstLba, independent.PartitionLastLba, admitted.Count);
        var app = scanner.Directory(scanner.Volume.RootCluster, "/").SingleOrDefault(e => e.Name == "app");
        Need(app is not null && (app.Attributes & 0x10) != 0, "app_directory");
        var root = scanner.Directory(app!.Cluster, "/app").SingleOrDefault(e => e.Name == "wwwroot");
        Need(root is not null && (root.Attributes & 0x10) != 0, "root_directory");
        scanner.Walk(Root, root!, 0);
        Need(scanner.Entries.Count == admitted.Count && scanner.Entries.All(e => admitted.Contains(e.Path)) && scanner.Entries.Where(e => !e.IsDirectory).Count() == receipt.WebRootFiles.Length, "inventory");
        foreach (var entry in scanner.Entries.Where(e => !e.IsDirectory))
        {
            var file = receipt.WebRootFiles.Single(e => e.Path == entry.Path);
            Need(entry.Bytes == file.Bytes && entry.Sha256 == file.Sha256 && entry.Attributes == file.Attributes && entry.FatDate == file.FatDate && entry.FatTime == file.FatTime && entry.Extents.Count == file.Extents.Length && entry.Extents.Select(e => new WebRootImageFixture.Extent(e.FirstCluster, e.Clusters, e.FirstLba)).SequenceEqual(file.Extents), "file_join");
        }

        string[] fixtures = ["fixture-nonce.txt", "fixture/empty.bin", "fixture/large.bin", "fixture/fragmented.bin", "fixture/nested/info.txt"];
        var published = scanner.Entries.Where(e => !e.IsDirectory && !fixtures.Contains(e.Path[(Root.Length + 1)..], StringComparer.Ordinal)).OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
        Need(published.Length > 0 && published.Length == receipt.PublishedFileCount && published.Sum(e => (long)e.Bytes) == receipt.PublishedBytes && published.Max(e => e.Bytes) == receipt.MaximumPublishedFileBytes, "published_shape");
        string text = string.Concat(published.Select(e => e.Path[(Root.Length + 1)..] + "\t" + e.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\t" + e.Sha256 + "\n"));
        Need(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))) == receipt.PublishedInventorySha256 && fixtures.All(f => scanner.Entries.Any(e => !e.IsDirectory && e.Path == Root + "/" + f)), "published_identity");
        return new(hash, scanner.Volume, scanner.Entries, scanner.NavigationSectors);
    }

    private static bool Same(WebRootImageFixture.DiskFile a, WebRootImageFixture.DiskFile b) => a.Path == b.Path && a.Bytes == b.Bytes && a.Sha256 == b.Sha256 && a.Attributes == b.Attributes && a.FatDate == b.FatDate && a.FatTime == b.FatTime && a.Extents is not null && b.Extents is not null && a.Extents.SequenceEqual(b.Extents);
    private static bool Canonical(string? path)
    {
        if (path is null || path.Length > WebRootImageFixture.MaximumGuestPathBytes || !(path == Root || path.StartsWith(Root + "/", StringComparison.Ordinal)))
            return false;
        return path.All(c => c is >= ' ' and <= '~' && c is not ('\\' or ':' or '"' or '|' or '<' or '>' or '*' or '?')) && path[1..].Split('/').All(s => s.Length > 0 && s is not ("." or "..") && !s.EndsWith('.') && !s.EndsWith(' ')) && path[1..].Split('/').Length <= WebRootImageFixture.MaximumDirectoryDepth + 3;
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("webroot_disk_inventory_" + reason);
    }
}
