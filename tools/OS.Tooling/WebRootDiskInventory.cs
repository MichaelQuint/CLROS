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
public static class WebRootDiskInventory
{
    public const string Root = WebRootImageFixture.GuestRoot;
    // This is a host evidence bound, not a larger runtime namespace allowance.
    public const int MaximumEntries = 4096;
    public const int MaximumMetadataObservations = 65536;
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);

    public readonly record struct Extent(uint FirstCluster, uint Clusters, ulong FirstLba, uint Sectors);
    public readonly record struct Sector(ulong FileOffset, ulong Lba, uint ValidBytes);
    public readonly record struct DirectorySector(string Path, ulong Lba);
    public enum MetadataKind : byte { Fat0, Fat1, Directory }
    public readonly record struct MetadataSector(ulong Lba, MetadataKind Kind, string DirectoryPath);

    /// <summary>Copied authority for the fixed truncated-chain control. Creation
    /// independently verifies both distinct images and the complete byte delta;
    /// a caller-supplied receipt alone cannot construct this value. Baseline
    /// remains a healthy inventory with its original hash. Original consumers
    /// must hold both images and receipts unchanged and reverify at acceptance.
    /// This value grants no native, process, HTTP or guest authority.</summary>
    public sealed class VerifiedTruncated
    {
        public const string Target = Root + "/fixture/fragmented.bin";
        private readonly string root, positivePath, faultPath, positiveJson;
        private readonly WebRootImageFixture.FaultInspection inspected;
        public Inspection Baseline { get; }
        public string ActualImageSha256 => inspected.Receipt.FinalSha256;
        public WebRootImageFixture.FaultInspection Fault => inspected;
        private VerifiedTruncated(string root, string positivePath, string faultPath, string positiveJson,
            Inspection baseline, WebRootImageFixture.FaultInspection inspected)
        {
            this.root = root; this.positivePath = positivePath; this.faultPath = faultPath;
            this.positiveJson = positiveJson; Baseline = baseline; this.inspected = inspected;
        }

        /// <summary>Synchronously borrow plain distinct image paths and receipt
        /// values; copy receipt arrays before checking. Throw on any nonfixed
        /// mutation, forged identity, alias, malformed positive or wrong variant.
        /// Files are read only; the result retains no lease or mutable input.</summary>
        public static VerifiedTruncated Verify(string root, string positivePath, WebRootImageFixture.Receipt positive,
            string faultPath, WebRootImageFixture.FaultReceipt fault)
        {
            Need(positive is not null && fault is { Variant: "Truncated", TargetPath: Target }, "truncated_input");
            root = Path.GetFullPath(root);
            positivePath = Path.GetFullPath(positivePath, root);
            faultPath = Path.GetFullPath(faultPath, root);
            string json = JsonSerializer.Serialize(positive);
            var copied = JsonSerializer.Deserialize<WebRootImageFixture.Receipt>(json)!;
            var inspected = WebRootImageFixture.VerifyFaultVariant(root, positivePath, copied, faultPath, fault);
            var baseline = Inspect(positivePath, File.ReadAllBytes(positivePath), copied);
            return new(Path.GetFullPath(root), Path.GetFullPath(positivePath), Path.GetFullPath(faultPath), json, baseline, inspected);
        }

        /// <summary>Require this exact copied baseline and actual held fault
        /// bytes. A healthy inventory's image hash is never rewritten. No file
        /// lease or runtime observation is inferred from this synchronous join.</summary>
        internal void RequireValues(Inspection baseline, ReadOnlySpan<byte> image)
        {
            Need(ReferenceEquals(Baseline, baseline) && Convert.ToHexString(SHA256.HashData(image)) == ActualImageSha256,
                "truncated_values");
        }

        /// <summary>Recheck the complete independent positive/fault delta using
        /// original paths. The enclosing original owner keeps all inputs held
        /// through this check and final acceptance. A changed file throws.</summary>
        public void VerifyUnchanged()
        {
            var positive = JsonSerializer.Deserialize<WebRootImageFixture.Receipt>(positiveJson)!;
            Need(WebRootImageFixture.VerifyFaultVariant(root, positivePath, positive, faultPath, inspected.Receipt) == inspected,
                "truncated_changed");
        }
    }

    /// <summary>All collections and content are owned copies. Inode identifies
    /// the actual physical short directory slot: LBA*16+slot+256. Zero mtime is
    /// the project's unspecified all-zero FAT timestamp sentinel. Allocated
    /// sectors come from the checked chain, including directories; Linux's
    /// current directory stat still reports unknown allocation as zero.</summary>
    public sealed class Entry
    {
        private readonly byte[] content;
        public string Path { get; }
        public string ShortAlias { get; }
        public ulong Inode { get; }
        public byte Attributes { get; }
        public bool IsDirectory => (Attributes & 0x10) != 0;
        public uint Bytes { get; }
        public string Sha256 { get; }
        public long ModifiedSeconds { get; }
        public ushort FatDate { get; }
        public ushort FatTime { get; }
        public ulong ShortEntryLba { get; }
        public uint ShortEntrySlot { get; }
        public ulong AllocatedSectors { get; }
        public IReadOnlyList<Extent> Extents { get; }
        public IReadOnlyList<Sector> Sectors { get; }
        internal Entry(string path, Found raw, uint[] chain, LiveFat32Volume volume, byte[] data)
        {
            Path = path; ShortAlias = raw.Alias; Attributes = raw.Attributes; Bytes = raw.Bytes;
            Inode = checked(raw.Lba * 16 + raw.Slot + 256); ModifiedSeconds = raw.Modified;
            FatDate = raw.Date; FatTime = raw.Time; ShortEntryLba = raw.Lba; ShortEntrySlot = raw.Slot;
            AllocatedSectors = (ulong)chain.Length * volume.SectorsPerCluster;
            content = data; Sha256 = Convert.ToHexString(SHA256.HashData(data));
            List<Extent> extents = []; List<Sector> sectors = [];
            ulong offset = 0;
            foreach (uint cluster in chain)
            {
                ulong lba = volume.FirstDataLba + (ulong)(cluster - 2) * volume.SectorsPerCluster;
                if (extents.Count != 0 && extents[^1].FirstCluster + extents[^1].Clusters == cluster)
                    extents[^1] = extents[^1] with { Clusters = extents[^1].Clusters + 1, Sectors = extents[^1].Sectors + volume.SectorsPerCluster };
                else extents.Add(new(cluster, 1, lba, volume.SectorsPerCluster));
                for (uint i = 0; i < volume.SectorsPerCluster; i++)
                {
                    uint valid = IsDirectory ? 0U : offset >= Bytes ? 0U : (uint)Math.Min(512UL, Bytes - offset);
                    sectors.Add(new(offset, lba + i, valid)); offset += 512;
                }
            }
            Extents = Array.AsReadOnly(extents.ToArray()); Sectors = Array.AsReadOnly(sectors.ToArray());
        }

        /// <summary>Return a fresh copy; no writable alias to retained evidence
        /// is exposed. Directories have no file content. The caller owns and may
        /// modify the result without changing the inventory.</summary>
        public byte[] CopyBytes() => content.ToArray();

        /// <summary>Copy the exact ordered data-sector coverage of a bounded
        /// file range. Offset at/beyond EOF or length0 returns no sectors.
        /// Directories and ranges above the existing64KiB request bound reject.
        /// This is image coverage, not evidence that a guest issued these reads.</summary>
        public IReadOnlyList<Sector> Cover(ulong offset, uint bytes)
        {
            Need(!IsDirectory && bytes <= 65536 && offset <= long.MaxValue, "range");
            if (offset >= Bytes || bytes == 0) return Array.AsReadOnly(Array.Empty<Sector>());
            ulong end = offset + Math.Min((ulong)bytes, Bytes - offset);
            return Array.AsReadOnly(Sectors.Where(s => s.FileOffset < end && offset < s.FileOffset + s.ValidBytes).ToArray());
        }
    }

    /// <summary>Immutable, ordinal/case-sensitive canonical names only. VFAT
    /// short aliases, traversal, alternate roots and normalization variants are
    /// not lookup routes. This copied result carries no external authority.</summary>
    public sealed class Inspection
    {
        private readonly Dictionary<string, Entry> byPath;
        private readonly Dictionary<ulong, string> directoryByLba;
        private readonly HashSet<ulong> assetSectors;
        private readonly ulong firstFatLba;
        private readonly uint fatSectors;
        public string ImageSha256 { get; }
        public ulong PartitionFirstLba { get; }
        public ulong PartitionLastLba { get; }
        public IReadOnlyList<Entry> Entries { get; }
        /// <summary>Exact checked directory-chain sectors, including navigation
        /// metadata for '/' and '/app' outside the served namespace. These paths
        /// do not become Entries/Find routes. No sector is recognized by its byte
        /// pattern; allocation and global chain ownership establish its role.</summary>
        public IReadOnlyList<DirectorySector> DirectorySectors { get; }
        public bool GrantsNativeDeviceOrProcessAuthority => false;
        internal Inspection(string hash, LiveFat32Volume volume, List<Entry> entries, List<DirectorySector> navigation)
        {
            ImageSha256 = hash; PartitionFirstLba = volume.PartitionFirstLba; PartitionLastLba = volume.PartitionLastLba;
            firstFatLba = volume.FirstFatLba; fatSectors = volume.FatSectors;
            Entries = Array.AsReadOnly(entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray());
            byPath = Entries.ToDictionary(e => e.Path, StringComparer.Ordinal);
            DirectorySectors = Array.AsReadOnly(navigation.Concat(Entries.Where(e => e.IsDirectory)
                .SelectMany(e => e.Sectors.Select(s => new DirectorySector(e.Path, s.Lba)))).OrderBy(s => s.Lba).ToArray());
            assetSectors = Entries.Where(e => !e.IsDirectory).SelectMany(e => e.Sectors.Select(s => s.Lba)).ToHashSet();
            directoryByLba = new();
            foreach (var sector in DirectorySectors)
                Need(sector.Lba >= volume.FirstDataLba && sector.Lba <= volume.PartitionLastLba &&
                    !assetSectors.Contains(sector.Lba) && directoryByLba.TryAdd(sector.Lba, sector.Path), "metadata_overlap");
        }
        public Entry? Find(string path) => Canonical(path) && byPath.TryGetValue(path, out var entry) ? entry : null;
        internal MetadataSector Classify(ulong lba)
        {
            // Allocated asset sectors include the complete physical last sector,
            // so bytes after logical EOF never become directory metadata.
            Need(!assetSectors.Contains(lba), "metadata_asset");
            if (lba >= firstFatLba && lba - firstFatLba < fatSectors) return new(lba, MetadataKind.Fat0, "");
            ulong second = firstFatLba + fatSectors;
            if (lba >= second && lba - second < fatSectors) return new(lba, MetadataKind.Fat1, "");
            if (directoryByLba.TryGetValue(lba, out var path)) return new(lba, MetadataKind.Directory, path);
            throw new ToolFailure("webroot_disk_inventory_metadata_sector");
        }
    }

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
    public static IReadOnlyList<MetadataSector> ClassifyMetadata(Inspection inventory,
        ReadOnlySpan<byte> heldImage, IReadOnlyList<ulong> residualLbas, VerifiedTruncated? truncated = null)
    {
        int count = residualLbas?.Count ?? -1;
        Need(inventory is not null && count is >= 0 and <= MaximumMetadataObservations &&
            heldImage.Length is >= 34 * 1024 * 1024 and <= WebRootImageFixture.MaximumImageBytes &&
            heldImage.Length % 512 == 0, "metadata_input");
        Inspection actual = inventory!;
        if (truncated is null) Need(Convert.ToHexString(SHA256.HashData(heldImage)) == actual.ImageSha256, "metadata_image");
        else truncated.RequireValues(actual, heldImage);
        MetadataSector[] result = new MetadataSector[count];
        for (int i = 0; i < count; i++) result[i] = actual.Classify(residualLbas![i]);
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
        Need(rawImage is { Length: >= 34 * 1024 * 1024 and <= WebRootImageFixture.MaximumImageBytes } && rawImage.Length % 512 == 0 &&
            receipt is not null && receipt.WebRootFiles is { Length: > 0 and < MaximumEntries } &&
            receipt.PreservedFiles is not null && receipt.Sentinel is not null, "input");
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(receipt.PreservedFiles);
        ArgumentNullException.ThrowIfNull(receipt.WebRootFiles);
        string hash = Convert.ToHexString(SHA256.HashData(rawImage));
        var independent = WebRootImageFixture.ReadBack(originalImagePath);
        Need(hash == independent.Sha256 && hash == receipt.FinalSha256 && independent.PartitionFirstLba == receipt.PartitionFirstLba &&
            independent.PartitionLastLba == receipt.PartitionLastLba && receipt.Variant is "DiskA" or "DiskB", "identity");
        var expected = receipt.PreservedFiles.Concat(receipt.WebRootFiles).Append(receipt.Sentinel!).ToArray();
        Need(expected.Length == independent.Files.Length && expected.Select(e => e.Path).Distinct(StringComparer.Ordinal).Count() == expected.Length &&
            expected.All(e => independent.Files.Any(a => Same(e, a))), "receipt");
        Need(receipt.WebRootFiles.All(f => Canonical(f.Path) && f.Extents is not null), "receipt_scope");
        HashSet<string> admitted = new(StringComparer.Ordinal) { Root };
        foreach (var file in receipt.WebRootFiles)
        {
            admitted.Add(file.Path); string parent = file.Path;
            while ((parent = parent[..parent.LastIndexOf('/')]) != "/app") admitted.Add(parent);
        }
        Need(admitted.Count <= MaximumEntries, "entry_bound");
        Scanner scanner = new(rawImage, independent.PartitionFirstLba, independent.PartitionLastLba, admitted.Count);
        var app = scanner.Directory(scanner.Volume.RootCluster, "/").SingleOrDefault(e => e.Name == "app");
        Need(app is not null && (app.Attributes & 0x10) != 0, "app_directory");
        var root = scanner.Directory(app!.Cluster, "/app").SingleOrDefault(e => e.Name == "wwwroot");
        Need(root is not null && (root.Attributes & 0x10) != 0, "root_directory");
        scanner.Walk(Root, root!, 0);
        Need(scanner.Entries.Count == admitted.Count && scanner.Entries.All(e => admitted.Contains(e.Path)) &&
            scanner.Entries.Where(e => !e.IsDirectory).Count() == receipt.WebRootFiles.Length, "inventory");
        foreach (var entry in scanner.Entries.Where(e => !e.IsDirectory))
        {
            var file = receipt.WebRootFiles.Single(e => e.Path == entry.Path);
            Need(entry.Bytes == file.Bytes && entry.Sha256 == file.Sha256 && entry.Attributes == file.Attributes &&
                entry.FatDate == file.FatDate && entry.FatTime == file.FatTime && entry.Extents.Count == file.Extents.Length &&
                entry.Extents.Select(e => new WebRootImageFixture.Extent(e.FirstCluster, e.Clusters, e.FirstLba)).SequenceEqual(file.Extents), "file_join");
        }
        string[] fixtures = ["fixture-nonce.txt", "fixture/empty.bin", "fixture/large.bin", "fixture/fragmented.bin", "fixture/nested/info.txt"];
        var published = scanner.Entries.Where(e => !e.IsDirectory && !fixtures.Contains(e.Path[(Root.Length + 1)..], StringComparer.Ordinal))
            .OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
        Need(published.Length > 0 && published.Length == receipt.PublishedFileCount &&
            published.Sum(e => (long)e.Bytes) == receipt.PublishedBytes && published.Max(e => e.Bytes) == receipt.MaximumPublishedFileBytes,
            "published_shape");
        string text = string.Concat(published.Select(e => e.Path[(Root.Length + 1)..] + "\t" +
            e.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\t" + e.Sha256 + "\n"));
        Need(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))) == receipt.PublishedInventorySha256 &&
            fixtures.All(f => scanner.Entries.Any(e => !e.IsDirectory && e.Path == Root + "/" + f)), "published_identity");
        return new(hash, scanner.Volume, scanner.Entries, scanner.NavigationSectors);
    }

    private static bool Same(WebRootImageFixture.DiskFile a, WebRootImageFixture.DiskFile b) =>
        a.Path == b.Path && a.Bytes == b.Bytes && a.Sha256 == b.Sha256 && a.Attributes == b.Attributes &&
        a.FatDate == b.FatDate && a.FatTime == b.FatTime && a.Extents is not null && b.Extents is not null && a.Extents.SequenceEqual(b.Extents);
    private static bool Canonical(string? path)
    {
        if (path is null || path.Length > WebRootImageFixture.MaximumGuestPathBytes ||
            !(path == Root || path.StartsWith(Root + "/", StringComparison.Ordinal))) return false;
        return path.All(c => c is >= ' ' and <= '~' && c is not ('\\' or ':' or '"' or '|' or '<' or '>' or '*' or '?')) &&
            path[1..].Split('/').All(s => s.Length > 0 && s is not ("." or "..") && !s.EndsWith('.') && !s.EndsWith(' ')) &&
            path[1..].Split('/').Length <= WebRootImageFixture.MaximumDirectoryDepth + 3;
    }
    private static void Need(bool condition, string reason) { if (!condition) throw new ToolFailure("webroot_disk_inventory_" + reason); }
    internal sealed record Found(string Name, string Alias, uint Cluster, uint Bytes, byte Attributes,
        ushort Date, ushort Time, long Modified, ulong Lba, uint Slot);

    private sealed class Scanner
    {
        private readonly byte[] raw;
        private readonly HashSet<uint> owned = [];
        private readonly HashSet<string> paths = new(StringComparer.Ordinal);
        private readonly int maximumEntries;
        internal readonly LiveFat32Volume Volume;
        internal readonly List<Entry> Entries = [];
        internal readonly List<DirectorySector> NavigationSectors = [];
        internal Scanner(byte[] raw, ulong first, ulong last, int maximumEntries)
        {
            this.raw = raw; this.maximumEntries = maximumEntries;
            Span<byte> sector = stackalloc byte[512]; SectorBytes(first).CopyTo(sector);
            LiveFat32Volume volume = default;
            Need(Chain.ParseBpb(sector, (ulong)raw.Length / 512, first, last, Complete, ref volume) == LiveFat32Status.Success &&
                volume.SectorsPerCluster == 1 && volume.TotalSectors == last - first + 1, "geometry");
            Volume = volume;
            for (uint i = 0; i < volume.FatSectors; i++)
                Need(SectorBytes(volume.FirstFatLba + i).SequenceEqual(SectorBytes(volume.FirstFatLba + volume.FatSectors + i)), "mirror");
        }
        private ReadOnlySpan<byte> SectorBytes(ulong lba)
        { Need(lba < (ulong)raw.Length / 512, "sector"); return raw.AsSpan(checked((int)lba * 512), 512); }
        private ulong ClusterLba(uint cluster)
        {
            ulong lba = 0;
            Need(Chain.ClusterFirstLba(Volume, cluster, ref lba) == LiveFat32Status.Success, "cluster"); return lba;
        }
        private uint[] Clusters(uint first)
        {
            uint[] history = new uint[ReadOnlyWebRootFileSystem.MaximumClusters]; uint count = 0, cluster = first;
            Span<byte> fat = stackalloc byte[512];
            while (true)
            {
                Need(Chain.Visit(Volume, cluster, history, ref count) == LiveFat32Status.Success && owned.Add(cluster), "chain_ownership");
                ulong lba = Volume.FirstFatLba + cluster / 128; SectorBytes(lba).CopyTo(fat); uint next = 0;
                var status = Chain.ReadFatEntry(Volume, cluster, lba, fat, Complete, ref next);
                if (status == LiveFat32Status.EndOfChain) return history.AsSpan(0, (int)count).ToArray();
                Need(status == LiveFat32Status.Success, "chain_link"); cluster = next;
            }
        }
        internal List<Found> Directory(uint first, string path)
        {
            uint[] clusters = Clusters(first);
            foreach (uint cluster in clusters) NavigationSectors.Add(new(path, ClusterLba(cluster)));
            return ReadDirectory(clusters);
        }
        private List<Found> ReadDirectory(uint[] chain)
        {
            List<Found> entries = []; HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            LiveVfatState state = default; Span<ushort> words = stackalloc ushort[260]; Span<byte> slot = stackalloc byte[32];
            int physical = 0;
            foreach (uint cluster in chain)
            {
                ulong lba = ClusterLba(cluster); ReadOnlySpan<byte> sector = SectorBytes(lba);
                for (uint index = 0; index < 16; index++)
                {
                    Need(++physical <= 4096, "directory_budget"); sector.Slice((int)index * 32, 32).CopyTo(slot);
                    var status = Vfat.Consume(slot, ref state, words, out var entry);
                    if (status == LiveDirectoryStatus.End) return entries;
                    Need(status is LiveDirectoryStatus.Entry or LiveDirectoryStatus.Skipped or LiveDirectoryStatus.LongName, "vfat");
                    if (status != LiveDirectoryStatus.Entry) continue;
                    string alias = Encoding.ASCII.GetString(slot[..8]).TrimEnd(' '), ext = Encoding.ASCII.GetString(slot.Slice(8, 3)).TrimEnd(' ');
                    if (ext.Length != 0) alias += "." + ext;
                    string name = entry.NameLength == 0 ? alias : new string(words[..entry.NameLength].ToArray().Select(c => (char)c).ToArray());
                    Need(name.All(c => c is >= ' ' and <= '~') && name is not ("." or "..") && !name.EndsWith('.') && !name.EndsWith(' ') && names.Add(name) &&
                        (alias.Equals(name, StringComparison.OrdinalIgnoreCase) || names.Add(alias)), "name_alias");
                    ushort date = BinaryPrimitives.ReadUInt16LittleEndian(slot[24..]), time = BinaryPrimitives.ReadUInt16LittleEndian(slot[22..]);
                    long modified = 0;
                    Need(date == 0 && time == 0 || GregorianUtc.TryUnixSeconds((uint)(1980 + (date >> 9)), (uint)((date >> 5) & 15), (uint)(date & 31),
                        (uint)(time >> 11), (uint)((time >> 5) & 63), (uint)((time & 31) * 2), out modified), "timestamp");
                    entries.Add(new(name, alias, entry.FirstCluster, entry.ByteCount, entry.Attributes, date, time, modified, lba, index));
                }
            }
            Need(Vfat.Finish(state) == LiveDirectoryStatus.End, "directory_end"); return entries;
        }
        internal void Walk(string path, Found rawEntry, int depth)
        {
            Need(Canonical(path) && depth <= WebRootImageFixture.MaximumDirectoryDepth && paths.Add(path) && Entries.Count < maximumEntries, "path");
            bool directory = (rawEntry.Attributes & 0x10) != 0;
            uint[] clusters = rawEntry.Bytes == 0 && !directory ? [] : Clusters(rawEntry.Cluster);
            Need(directory || (rawEntry.Bytes == 0 ? rawEntry.Cluster == 0 :
                Chain.CheckLength((uint)clusters.Length, (uint)(((ulong)rawEntry.Bytes + 511) / 512)) == LiveFat32Status.Success), "length");
            byte[] data = directory ? [] : new byte[rawEntry.Bytes]; int at = 0;
            if (!directory)
                foreach (uint cluster in clusters)
                {
                    int bytes = Math.Min(512, data.Length - at); SectorBytes(ClusterLba(cluster))[..bytes].CopyTo(data.AsSpan(at)); at += bytes;
                }
            Entries.Add(new(path, rawEntry, clusters, Volume, data));
            if (directory) foreach (var child in ReadDirectory(clusters)) Walk(path + "/" + child.Name, child, depth + 1);
        }
    }
}
