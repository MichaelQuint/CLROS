using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
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
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);

    public readonly record struct Extent(uint FirstCluster, uint Clusters, ulong FirstLba, uint Sectors);
    public readonly record struct Sector(ulong FileOffset, ulong Lba, uint ValidBytes);

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
        public string ImageSha256 { get; }
        public ulong PartitionFirstLba { get; }
        public ulong PartitionLastLba { get; }
        public IReadOnlyList<Entry> Entries { get; }
        public bool GrantsNativeDeviceOrProcessAuthority => false;
        internal Inspection(string hash, LiveFat32Volume volume, List<Entry> entries)
        {
            ImageSha256 = hash; PartitionFirstLba = volume.PartitionFirstLba; PartitionLastLba = volume.PartitionLastLba;
            Entries = Array.AsReadOnly(entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray());
            byPath = Entries.ToDictionary(e => e.Path, StringComparer.Ordinal);
        }
        public Entry? Find(string path) => Canonical(path) && byPath.TryGetValue(path, out var entry) ? entry : null;
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
        string hash = Convert.ToHexString(SHA256.HashData(rawImage));
        var independent = WebRootImageFixture.ReadBack(originalImagePath);
        Need(hash == independent.Sha256 && hash == receipt.FinalSha256 && independent.PartitionFirstLba == receipt.PartitionFirstLba &&
            independent.PartitionLastLba == receipt.PartitionLastLba && receipt.Variant is "DiskA" or "DiskB", "identity");
        var expected = receipt.PreservedFiles.Concat(receipt.WebRootFiles).Append(receipt.Sentinel).ToArray();
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
        var app = scanner.Directory(scanner.Volume.RootCluster).SingleOrDefault(e => e.Name == "app");
        Need(app is not null && (app.Attributes & 0x10) != 0, "app_directory");
        var root = scanner.Directory(app!.Cluster).SingleOrDefault(e => e.Name == "wwwroot");
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
        return new(hash, scanner.Volume, scanner.Entries);
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
        internal List<Found> Directory(uint first) => ReadDirectory(Clusters(first));
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
