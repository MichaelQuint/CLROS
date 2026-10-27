using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;

/// <summary>Host-only import of an immutable published webroot into a fresh
/// copy of a checked GPT/FAT32 image. Images and receipts do not establish guest
/// I/O. The base and source files remain held read-only through publication.</summary>
public static class WebRootImageFixture
{
    public const int MaximumImageBytes = 256 * 1024 * 1024;
    public const int MaximumDirectoryDepth = 8;
    public const int MaximumGuestPathBytes = 255;
    public const int LargeFixtureBytes = 65536 + 17;
    public const int FragmentedFixtureBytes = 2 * 65536 + 37;
    public const string GuestRoot = "/app/wwwroot";
    public const string TimestampPolicy = "1980-01-01T00:00:00Z (declared FAT UTC)";
    private const uint EndOfChain = 0x0fffffff;
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);

    public sealed record Asset(string Path, int Bytes, string Sha256);
    public sealed record Snapshot(Asset[] Files, long TotalBytes, int MaximumFileBytes, string Sha256);
    public sealed record Extent(uint FirstCluster, uint Clusters, ulong FirstLba);
    public sealed record DiskFile(string Path, uint Bytes, string Sha256, byte Attributes,
        ushort FatDate, ushort FatTime, Extent[] Extents);
    public sealed record ImageInventory(string Sha256, ulong PartitionFirstLba, ulong PartitionLastLba,
        DiskFile[] Files);
    public sealed record Receipt(string Variant, string OriginalSha256, string FinalSha256,
        string PublishedInventorySha256, int PublishedFileCount, long PublishedBytes,
        int MaximumPublishedFileBytes, ulong PartitionFirstLba, ulong PartitionLastLba,
        string UtcTimestampPolicy, DiskFile[] WebRootFiles, DiskFile[] PreservedFiles, DiskFile Sentinel);
    public sealed record FaultReceipt(string Variant, string SourceSha256, string FinalSha256,
        string TargetPath, string ExpectedReadback);

    /// <summary>Measure sorted, plain, ASCII source files. Counts and byte limits
    /// are the actual inventory, not inflated runtime admission. Empty files are
    /// allowed; empty directories have no static-file meaning and are omitted.</summary>
    public static Snapshot SnapshotPublishedRoot(string publishedRoot)
    {
        string root = Path.GetFullPath(publishedRoot);
        AuthorityFiles.RequirePlainPath(Path.GetPathRoot(root)!, root, "webroot_source_path");
        Need(Directory.Exists(root), "source_directory");
        List<Asset> assets = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        using AuthorityFiles files = new();
        Walk(root);
        Need(assets.Count != 0, "source_empty");
        Asset[] sorted = assets.OrderBy(x => x.Path, StringComparer.Ordinal).ToArray();
        long total = sorted.Sum(x => (long)x.Bytes);
        Need(total <= MaximumImageBytes, "source_total_bytes");
        string text = string.Concat(sorted.Select(x => x.Path + "\t" +
            x.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\t" + x.Sha256 + "\n"));
        files.VerifyUnchanged();
        return new(sorted, total, sorted.Max(x => x.Bytes), Hash(Encoding.UTF8.GetBytes(text)));

        void Walk(string directory)
        {
            AuthorityFiles.RequirePlainPath(root, directory, "webroot_source_path");
            foreach (string path in Directory.GetFileSystemEntries(directory).Order(StringComparer.Ordinal))
            {
                AuthorityFiles.RequirePlainPath(root, path, "webroot_source_path");
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                ValidatePath(relative, (File.GetAttributes(path) & FileAttributes.Directory) != 0);
                Need(names.Add(relative), "source_case_collision");
                if (Directory.Exists(path)) { Walk(path); continue; }
                var file = files.Open(root, path, "published asset", "webroot_asset", MaximumImageBytes,
                    allowEmpty: true, singleLink: true);
                byte[] bytes = file.ReadBytes(MaximumImageBytes, "webroot_asset");
                assets.Add(new(relative, bytes.Length, Hash(bytes)));
            }
        }
    }

    /// <summary>Publish a new artifacts image only after exact source revalidation,
    /// prospective parser readback and preserved-file/mutation checks. DiskA/B
    /// differ only in equal-length fixture-nonce.txt bytes. Failures before the
    /// final atomic move leave no output image; an existing output is never replaced.</summary>
    public static Receipt Apply(string repositoryRoot, string baseImagePath, string publishedRoot,
        Snapshot inventory, string outputPath, string variant = "DiskA")
    {
        string root = Path.GetFullPath(repositoryRoot), source = Path.GetFullPath(publishedRoot);
        string output = Path.GetFullPath(outputPath);
        Need(variant is "DiskA" or "DiskB", "variant");
        ValidateOutput(root, output, source);
        AuthorityFiles.RequirePlainPath(root, source, "webroot_source_path");
        Snapshot current = SnapshotPublishedRoot(source);
        Need(SameSnapshot(inventory, current), "source_inventory_changed");
        using AuthorityFiles inputs = new();
        var originalFile = inputs.Open(root, baseImagePath, "base image", "webroot_base", MaximumImageBytes, singleLink: true);
        byte[] original = originalFile.ReadBytes(MaximumImageBytes, "webroot_base");
        var before = new Reader(original);
        ImageInventory originalInventory = before.Inventory();
        Need(!before.RootEntries.Any(x => x.Name.Equals("app", StringComparison.OrdinalIgnoreCase) ||
            x.Alias.Equals("APP", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("private", StringComparison.OrdinalIgnoreCase) || x.Alias.Equals("PRIVATE", StringComparison.OrdinalIgnoreCase)), "root_collision");
        Node tree = new("", true);
        Node app = tree.AddDirectory("app"), webroot = app.AddDirectory("wwwroot");
        foreach (Asset asset in current.Files)
        {
            Need(!asset.Path.Split('/')[0].Equals("fixture", StringComparison.OrdinalIgnoreCase) &&
                !asset.Path.Equals("fixture-nonce.txt", StringComparison.OrdinalIgnoreCase), "reserved_fixture_path");
            var input = inputs.Open(root, Path.Combine(source, asset.Path.Replace('/', Path.DirectorySeparatorChar)),
                "published asset", "webroot_asset", asset.Bytes, allowEmpty: true, singleLink: true);
            byte[] bytes = input.ReadBytes(asset.Bytes, "webroot_asset");
            Need(bytes.Length == asset.Bytes && Hash(bytes) == asset.Sha256, "source_inventory_changed");
            AddFile(webroot, asset.Path, bytes);
        }
        AddFile(webroot, "fixture-nonce.txt", Encoding.ASCII.GetBytes(variant == "DiskA" ? "CLROS-DISK-A-0001\n" : "CLROS-DISK-B-0001\n"));
        AddFile(webroot, "fixture/empty.bin", []);
        AddFile(webroot, "fixture/nested/info.txt", "CLROS nested fixture\n"u8.ToArray());
        AddFile(webroot, "fixture/large.bin", Pattern(LargeFixtureBytes));
        AddFile(webroot, "fixture/fragmented.bin", Pattern(FragmentedFixtureBytes));
        AddFile(tree.AddDirectory("private"), "sentinel.txt", "CLROS outside-root sentinel\n"u8.ToArray());
        byte[] disk = (byte[])original.Clone();
        Writer writer = new(disk, before);
        writer.Write(tree);
        writer.CheckMutationBounds(original);
        ImageInventory generated = new Reader(disk).Inventory();
        Need(generated.PartitionFirstLba == originalInventory.PartitionFirstLba &&
            generated.PartitionLastLba == originalInventory.PartitionLastLba, "partition_changed");
        foreach (DiskFile file in originalInventory.Files)
            Need(generated.Files.Any(x => SameFile(x, file)), "preserved_file_changed");
        DiskFile[] webFiles = generated.Files.Where(x => x.Path.StartsWith(GuestRoot + "/", StringComparison.Ordinal)).ToArray();
        Node[] expected = webroot.Descendants().Where(x => !x.Directory).ToArray();
        Need(webFiles.Length == expected.Length, "webroot_inventory");
        foreach (Node node in expected)
            Need(webFiles.Any(x => x.Path == node.Path && x.Bytes == node.Bytes.Length && x.Sha256 == Hash(node.Bytes) &&
                x.Attributes == 0x21 && x.FatDate == 0x21 && x.FatTime == 0), "webroot_readback");
        DiskFile sentinel = generated.Files.Single(x => x.Path == "/private/sentinel.txt");
        var receipt = new Receipt(variant, originalInventory.Sha256, generated.Sha256, current.Sha256,
            current.Files.Length, current.TotalBytes, current.MaximumFileBytes, generated.PartitionFirstLba,
            generated.PartitionLastLba, TimestampPolicy, webFiles, originalInventory.Files, sentinel);
        Need(generated.Files.Length == originalInventory.Files.Length + webFiles.Length + 1, "unexpected_file");
        inputs.VerifyUnchanged();
        Need(SameSnapshot(current, SnapshotPublishedRoot(source)), "source_inventory_changed");
        string parent = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(parent);
        AuthorityFiles.RequirePlainPath(root, parent, "webroot_output_path");
        string temporary = output + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(disk); stream.Flush(true); }
            Verify(temporary, receipt);
            inputs.VerifyUnchanged();
            Need(SameSnapshot(current, SnapshotPublishedRoot(source)), "source_inventory_changed");
            File.Move(temporary, output, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return receipt;
    }

    /// <summary>Independently parse actual image bytes with the reviewed numeric
    /// GPT/BPB/VFAT validators, checked chain traversal and global cross-link
    /// rejection. Host allocations are bounded by the admitted image capacity.</summary>
    public static ImageInventory ReadBack(string imagePath)
    {
        using var file = File.OpenRead(imagePath);
        Need(file.Length is >= 34 * 1024 * 1024 and <= MaximumImageBytes, "image_size");
        byte[] bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        return new Reader(bytes).Inventory();
    }

    /// <summary>Synchronously read a bounded image and compare its hash, partition
    /// and complete independently parsed file inventory to an earlier receipt.
    /// Borrows the receipt without mutation; mismatches throw before success.</summary>
    public static void Verify(string imagePath, Receipt expected)
    {
        ImageInventory actual = ReadBack(imagePath);
        Need(actual.Sha256 == expected.FinalSha256 && actual.PartitionFirstLba == expected.PartitionFirstLba &&
            actual.PartitionLastLba == expected.PartitionLastLba, "image_identity");
        DiskFile[] files = [.. expected.PreservedFiles, .. expected.WebRootFiles, expected.Sentinel];
        Need(actual.Files.Length == files.Length && files.All(file => actual.Files.Any(x => SameFile(x, file))), "image_inventory");
    }

    /// <summary>Create a new controlled missing/truncated/looped/mismatched-FAT
    /// image from a verified positive fixture. These are expected failures, not
    /// healthy image receipts; only the fragmented fixture's slots/FAT links change.</summary>
    public static FaultReceipt CreateFaultVariant(string repositoryRoot, string sourceImagePath,
        Receipt positive, string outputPath, string variant)
    {
        Need(variant is "Missing" or "Truncated" or "Loop" or "MirrorMismatch", "fault_variant");
        string root = Path.GetFullPath(repositoryRoot), output = Path.GetFullPath(outputPath);
        ValidateOutput(root, output, Path.GetFullPath(sourceImagePath));
        using AuthorityFiles inputs = new();
        var input = inputs.Open(root, sourceImagePath, "positive fixture", "webroot_positive", MaximumImageBytes, singleLink: true);
        byte[] disk = input.ReadBytes(MaximumImageBytes, "webroot_positive");
        Need(Hash(disk) == positive.FinalSha256, "positive_identity");
        var reader = new Reader(disk);
        ImageInventory before = reader.Inventory();
        const string target = GuestRoot + "/fixture/fragmented.bin";
        Need(reader.FileEntries.TryGetValue(target, out Entry? entry), "fault_target");
        string expected;
        if (variant == "Missing")
        {
            foreach (int offset in entry!.SlotOffsets) disk[offset] = 0xe5;
            ImageInventory missing = new Reader(disk).Inventory();
            Need(missing.Files.Length == before.Files.Length - 1 && !missing.Files.Any(x => x.Path == target) &&
                before.Files.Where(x => x.Path != target).All(file => missing.Files.Any(x => SameFile(x, file))), "missing_variant");
            expected = "missing_path";
        }
        else
        {
            int fat = checked((int)reader.Volume.FirstFatLba * 512 + (int)entry!.Cluster * 4);
            int mirror = fat + checked((int)reader.Volume.FatSectors * 512);
            uint value = variant == "Truncated" ? EndOfChain : entry.Cluster;
            if (variant != "MirrorMismatch") W32(disk, fat, value);
            W32(disk, mirror, value);
            expected = variant == "Truncated" ? "webroot_image_file_chain_length" :
                variant == "Loop" ? "webroot_image_chain_loop_or_crosslink" : "webroot_image_fat_mirror";
            bool rejected = false;
            try { _ = new Reader(disk); }
            catch (ToolFailure failure) when (failure.Reason == expected) { rejected = true; }
            Need(rejected, "fault_not_observed");
        }
        string hash = Hash(disk);
        string parent = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(parent);
        AuthorityFiles.RequirePlainPath(root, parent, "webroot_output_path");
        string temporary = output + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(disk); stream.Flush(true); }
            using (var readback = File.OpenRead(temporary)) Need(Convert.ToHexString(SHA256.HashData(readback)) == hash, "fault_identity");
            inputs.VerifyUnchanged();
            File.Move(temporary, output, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return new(variant, positive.FinalSha256, hash, target, expected);
    }

    private static bool SameFile(DiskFile a, DiskFile b) => a.Path == b.Path && a.Bytes == b.Bytes && a.Sha256 == b.Sha256 &&
        a.Attributes == b.Attributes && a.FatDate == b.FatDate && a.FatTime == b.FatTime && a.Extents.SequenceEqual(b.Extents);
    private static bool SameSnapshot(Snapshot a, Snapshot b) => a.Sha256 == b.Sha256 && a.TotalBytes == b.TotalBytes &&
        a.MaximumFileBytes == b.MaximumFileBytes && a.Files.SequenceEqual(b.Files);
    private static byte[] Pattern(int count)
    {
        byte[] bytes = new byte[count];
        for (int i = 0; i < count; i++) bytes[i] = unchecked((byte)((i * 73 + 19) ^ (i >> 9) ^ (i >> 17)));
        return bytes;
    }
    private static void AddFile(Node root, string relative, byte[] bytes)
    {
        string[] parts = relative.Split('/');
        Node directory = root;
        foreach (string part in parts[..^1]) directory = directory.AddDirectory(part);
        Need(!directory.Children.Any(x => x.Name.Equals(parts[^1], StringComparison.OrdinalIgnoreCase)), "path_collision");
        directory.Children.Add(new(parts[^1], false) { Parent = directory, Bytes = bytes });
    }
    private static void ValidatePath(string relative, bool directory)
    {
        string[] parts = relative.Split('/');
        Need(relative.Length != 0 && GuestRoot.Length + 1 + relative.Length <= MaximumGuestPathBytes &&
            parts.Length <= MaximumDirectoryDepth + (directory ? 0 : 1), "path_bounds");
        foreach (string part in parts)
            Need(part.Length != 0 && part is not "." and not ".." && part[^1] is not '.' and not ' ' &&
                part.All(c => c is >= '!' and <= '~' && "\\/:*?\"<>|".IndexOf(c) < 0), "path_invalid");
    }
    private static void ValidateOutput(string root, string output, string source)
    {
        string artifacts = Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Need(output.StartsWith(artifacts, comparison) && !output.StartsWith(Path.TrimEndingDirectorySeparator(source) + Path.DirectorySeparatorChar, comparison) &&
            !File.Exists(output) && !Directory.Exists(output), "output_path");
        string? existing = Path.GetDirectoryName(output);
        while (existing is not null && !Directory.Exists(existing)) existing = Path.GetDirectoryName(existing);
        Need(existing is not null, "output_path");
        AuthorityFiles.RequirePlainPath(root, existing!, "webroot_output_path");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static void W16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
    private static void W32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static void Need(bool condition, string reason) { if (!condition) throw new ToolFailure("webroot_image_" + reason); }

    private sealed class Node(string name, bool directory)
    {
        public string Name = name;
        public bool Directory = directory;
        public Node? Parent;
        public byte[] Bytes = [];
        public List<Node> Children = [];
        public uint[] Clusters = [];
        public string Alias = "";
        public string Path => Parent is null ? "" : Parent.Path + "/" + Name;
        public Node AddDirectory(string child)
        {
            Node? existing = Children.FirstOrDefault(x => x.Name.Equals(child, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) { Need(existing.Directory && existing.Name == child, "path_collision"); return existing; }
            Node result = new(child, true) { Parent = this }; Children.Add(result); return result;
        }
        public IEnumerable<Node> Descendants()
        {
            foreach (Node child in Children.OrderBy(x => x.Name, StringComparer.Ordinal))
            { yield return child; foreach (Node descendant in child.Descendants()) yield return descendant; }
        }
    }

    private sealed record Entry(string Name, string Alias, uint Cluster, uint Bytes, byte Attributes, ushort Date, ushort Time, int[] SlotOffsets);

    private sealed class Reader
    {
        public readonly byte[] Disk;
        public readonly LiveFat32Volume Volume;
        public readonly ulong PartitionFirst, PartitionLast;
        public readonly List<Entry> RootEntries;
        public readonly Dictionary<string, Entry> FileEntries = new(StringComparer.Ordinal);
        private readonly HashSet<uint> owned = [];
        private readonly List<DiskFile> files = [];
        public Reader(byte[] disk)
        {
            Disk = disk;
            Need(disk.Length is >= 34 * 1024 * 1024 and <= MaximumImageBytes && disk.Length % 512 == 0, "image_size");
            ulong capacity = (ulong)disk.Length / 512;
            var primary = Gpt(false); var backup = Gpt(true);
            Need(primary.FirstLba == backup.FirstLba && primary.LastLba == backup.LastLba &&
                disk.AsSpan(2 * 512, 16384).SequenceEqual(disk.AsSpan(disk.Length - 33 * 512, 16384)), "gpt_copies");
            PartitionFirst = primary.FirstLba; PartitionLast = primary.LastLba;
            int boot = checked((int)PartitionFirst * 512);
            LiveFat32Volume volume = default;
            Need(FATClusterChainsLoopBadClusterTruncationDetection.ParseBpb(disk.AsSpan(boot, 512), capacity,
                PartitionFirst, PartitionLast, Complete, ref volume) == LiveFat32Status.Success, "bpb");
            Volume = volume;
            Need(volume.SectorsPerCluster == 1 && volume.TotalSectors == PartitionLast - PartitionFirst + 1 &&
                U16(disk, boot + 48) == 1 && U16(disk, boot + 50) == 6, "geometry");
            int fat = checked((int)volume.FirstFatLba * 512), fatBytes = checked((int)volume.FatSectors * 512);
            Need(disk.AsSpan(fat, fatBytes).SequenceEqual(disk.AsSpan(fat + fatBytes, fatBytes)), "fat_mirror");
            Need(disk.AsSpan(boot, 512).SequenceEqual(disk.AsSpan(boot + 6 * 512, 512)) &&
                disk.AsSpan(boot + 512, 512).SequenceEqual(disk.AsSpan(boot + 7 * 512, 512)), "reserved_copies");
            int info = boot + 512;
            Need(U32(disk, info) == 0x41615252 && U32(disk, info + 484) == 0x61417272 && U32(disk, info + 508) == 0xaa550000, "fsinfo");
            uint free = 0;
            for (uint cluster = 2; cluster < volume.ClusterCount + 2; cluster++) if (RawNext(cluster) == 0) free++;
            Need(U32(disk, info + 488) is uint.MaxValue || U32(disk, info + 488) == free, "free_count");
            uint hint = U32(disk, info + 492);
            Need(hint == uint.MaxValue || hint >= 2 && hint < volume.ClusterCount + 2 && RawNext(hint) == 0, "free_hint");
            RootEntries = ReadDirectory(volume.RootCluster);
            VisitEntries("", RootEntries, 0);

            LiveEfiPartitionResult Gpt(bool isBackup)
            {
                int headerOffset = isBackup ? disk.Length - 512 : 512;
                var header = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(disk.AsSpan(headerOffset, 512), capacity, Complete, isBackup);
                Need(header.Succeeded && header.EntryLba == (isBackup ? capacity - 33 : 2UL), "gpt_header");
                // First/last usable LBAs and disk GUID must agree even when
                // both differing headers have individually correct CRCs.
                Need(disk.AsSpan(512 + 40, 32).SequenceEqual(disk.AsSpan(disk.Length - 512 + 40, 32)), "gpt_identity");
                var partition = FAT32ApplicationEFIPartition.Select(header, disk.AsSpan(checked((int)header.EntryLba * 512), 16384));
                Need(partition.Succeeded, "gpt_partition"); return partition;
            }
        }
        public ImageInventory Inventory() => new(Hash(Disk), PartitionFirst, PartitionLast, files.OrderBy(x => x.Path, StringComparer.Ordinal).ToArray());
        public int ClusterOffset(uint cluster)
        {
            ulong lba = 0;
            Need(FATClusterChainsLoopBadClusterTruncationDetection.ClusterFirstLba(Volume, cluster, ref lba) == LiveFat32Status.Success, "cluster_bounds");
            return checked((int)lba * 512);
        }
        public uint RawNext(uint cluster) => U32(Disk, checked((int)Volume.FirstFatLba * 512 + (int)cluster * 4)) & 0x0fffffff;
        public uint[] Chain(uint first, bool claim = false)
        {
            List<uint> chain = []; HashSet<uint> seen = [];
            uint cluster = first;
            while (true)
            {
                Need(cluster >= 2 && cluster - 2 < Volume.ClusterCount && seen.Add(cluster) && (!claim || owned.Add(cluster)), "chain_loop_or_crosslink");
                chain.Add(cluster);
                ulong sector = Volume.FirstFatLba + cluster / 128; uint next = 0;
                var status = FATClusterChainsLoopBadClusterTruncationDetection.ReadFatEntry(Volume, cluster, sector,
                    Disk.AsSpan(checked((int)sector * 512), 512), Complete, ref next);
                if (status == LiveFat32Status.EndOfChain) return chain.ToArray();
                Need(status == LiveFat32Status.Success && chain.Count <= Volume.ClusterCount, "chain_corrupt");
                cluster = next;
            }
        }
        private List<Entry> ReadDirectory(uint first)
        {
            List<Entry> entries = []; HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            List<int> longNameSlots = [];
            LiveVfatState state = default; ushort[] name = new ushort[260];
            foreach (uint cluster in Chain(first, claim: true))
                for (int slot = 0; slot < 16; slot++)
                {
                    int position = ClusterOffset(cluster) + slot * 32;
                    var status = MultiClusterDirectoryVFATLongNameSupportSeparate.Consume(Disk.AsSpan(position, 32), ref state, name, out var parsed);
                    Need(status is LiveDirectoryStatus.Entry or LiveDirectoryStatus.Skipped or LiveDirectoryStatus.LongName or LiveDirectoryStatus.End, "directory_entry");
                    if (status == LiveDirectoryStatus.End) return entries;
                    if (status == LiveDirectoryStatus.LongName) { longNameSlots.Add(position); continue; }
                    if (status != LiveDirectoryStatus.Entry) continue;
                    string alias = Encoding.ASCII.GetString(Disk, position, 8).TrimEnd(' ');
                    string extension = Encoding.ASCII.GetString(Disk, position + 8, 3).TrimEnd(' ');
                    if (extension.Length != 0) alias += "." + extension;
                    string actual = parsed.NameLength == 0 ? alias : new string(name.Take(parsed.NameLength).Select(x => (char)x).ToArray());
                    Need(names.Add(actual), "directory_case_collision");
                    if (!alias.Equals(actual, StringComparison.OrdinalIgnoreCase)) Need(names.Add(alias), "directory_alias_collision");
                    entries.Add(new(actual, alias, parsed.FirstCluster, parsed.ByteCount, parsed.Attributes,
                        U16(Disk, position + 24), U16(Disk, position + 22), [.. longNameSlots, position]));
                    longNameSlots.Clear();
                }
            Need(state.Slots == 0, "directory_lfn_truncated");
            return entries;
        }
        private void VisitEntries(string parent, List<Entry> entries, int depth)
        {
            Need(depth <= MaximumDirectoryDepth + 2, "directory_depth");
            foreach (Entry entry in entries)
            {
                string path = parent + "/" + entry.Name;
                if ((entry.Attributes & 0x10) != 0)
                { VisitEntries(path, ReadDirectory(entry.Cluster), depth + 1); continue; }
                FileEntries.Add(path, entry);
                uint[] chain = entry.Bytes == 0 ? [] : Chain(entry.Cluster, claim: true);
                Need(entry.Bytes == 0 ? entry.Cluster == 0 : (ulong)chain.Length == ((ulong)entry.Bytes + 511) / 512, "file_chain_length");
                Need(entry.Bytes <= Disk.Length, "file_size");
                byte[] bytes = new byte[entry.Bytes]; int offset = 0;
                foreach (uint cluster in chain)
                {
                    int count = Math.Min(512, bytes.Length - offset);
                    Disk.AsSpan(ClusterOffset(cluster), count).CopyTo(bytes.AsSpan(offset)); offset += count;
                }
                List<Extent> extents = [];
                foreach (uint cluster in chain)
                {
                    if (extents.Count != 0 && extents[^1].FirstCluster + extents[^1].Clusters == cluster)
                        extents[^1] = extents[^1] with { Clusters = extents[^1].Clusters + 1 };
                    else extents.Add(new(cluster, 1, Volume.FirstDataLba + cluster - 2));
                }
                files.Add(new(path, entry.Bytes, Hash(bytes), entry.Attributes, entry.Date, entry.Time, extents.ToArray()));
            }
        }
    }

    private sealed class Writer(byte[] disk, Reader original)
    {
        private readonly List<(int Start, int Bytes)> writes = [];
        private uint nextFree = 2;
        private int aliasNumber;
        private readonly HashSet<string> allNames = new(StringComparer.OrdinalIgnoreCase);
        public void Write(Node root)
        {
            Node[] nodes = root.Descendants().ToArray();
            foreach (Node node in nodes) allNames.Add(node.Name);
            foreach (Entry entry in original.RootEntries) { allNames.Add(entry.Name); allNames.Add(entry.Alias); }
            foreach (Node node in nodes)
            {
                do { node.Alias = "W" + (++aliasNumber).ToString("D7", System.Globalization.CultureInfo.InvariantCulture) + (node.Directory ? "   " : "DAT"); }
                while (allNames.Contains(node.Alias[..8].TrimEnd() + (node.Directory ? "" : ".DAT")));
                Need(node.Alias.Length == 11, "alias_limit");
                allNames.Add(node.Alias[..8] + (node.Directory ? "" : ".DAT"));
            }
            foreach (Node node in nodes.Where(x => x.Directory))
                node.Clusters = Allocate((2 + node.Children.Sum(x => Slots(x.Name)) + 1 + 15) / 16);
            foreach (Node node in nodes.Where(x => !x.Directory))
            {
                uint[] chain = Allocate((node.Bytes.Length + 511) / 512);
                node.Clusters = node.Path == GuestRoot + "/fixture/fragmented.bin" ?
                    chain.Where((_, i) => i % 2 == 0).Concat(chain.Where((_, i) => i % 2 != 0)).ToArray() : chain;
                Link(node.Clusters);
                for (int index = 0; index < node.Clusters.Length; index++)
                {
                    int offset = original.ClusterOffset(node.Clusters[index]);
                    node.Bytes.AsSpan(index * 512, Math.Min(512, node.Bytes.Length - index * 512)).CopyTo(disk.AsSpan(offset));
                }
            }
            foreach (Node directory in nodes.Where(x => x.Directory))
            {
                Link(directory.Clusters);
                List<byte[]> entries = [ShortEntry(".          ", 0x10, directory.Clusters[0], 0),
                    ShortEntry("..         ", 0x10, directory.Parent == root ? original.Volume.RootCluster : directory.Parent!.Clusters[0], 0)];
                foreach (Node child in directory.Children.OrderBy(x => x.Name, StringComparer.Ordinal)) entries.AddRange(Entries(child));
                WriteEntries(directory.Clusters, 0, entries);
            }
            uint[] rootChain = original.Chain(original.Volume.RootCluster);
            int end = 0;
            while (end < rootChain.Length * 16 && disk[original.ClusterOffset(rootChain[end / 16]) + end % 16 * 32] != 0) end++;
            List<byte[]> appended = [];
            foreach (Node child in root.Children.OrderBy(x => x.Name, StringComparer.Ordinal)) appended.AddRange(Entries(child));
            int required = (end + appended.Count + 1 + 15) / 16;
            if (required > rootChain.Length)
            { uint[] added = Allocate(required - rootChain.Length); rootChain = [.. rootChain, .. added]; Link(rootChain); }
            WriteEntries(rootChain, end, appended);
            uint free = 0, hint = uint.MaxValue;
            for (uint cluster = 2; cluster < original.Volume.ClusterCount + 2; cluster++)
                if (Next(cluster) == 0) { free++; if (hint == uint.MaxValue) hint = cluster; }
            int info = checked((int)original.PartitionFirst * 512 + 512);
            foreach (int copy in new[] { info, info + 6 * 512 })
            { W32(disk, copy + 488, free); W32(disk, copy + 492, hint); writes.Add((copy + 488, 8)); }
        }
        private uint Next(uint cluster) => U32(disk, checked((int)original.Volume.FirstFatLba * 512 + (int)cluster * 4)) & 0x0fffffff;
        private uint[] Allocate(int count)
        {
            uint[] result = new uint[count];
            for (int i = 0; i < count; i++)
            {
                while (nextFree < original.Volume.ClusterCount + 2 && Next(nextFree) != 0) nextFree++;
                Need(nextFree < original.Volume.ClusterCount + 2, "disk_full");
                uint cluster = nextFree++; result[i] = cluster; SetNext(cluster, EndOfChain);
                int offset = original.ClusterOffset(cluster); disk.AsSpan(offset, 512).Clear(); writes.Add((offset, 512));
            }
            return result;
        }
        private void SetNext(uint cluster, uint next)
        {
            int offset = checked((int)original.Volume.FirstFatLba * 512 + (int)cluster * 4);
            int bytes = checked((int)original.Volume.FatSectors * 512);
            foreach (int copy in new[] { offset, offset + bytes })
            { W32(disk, copy, (U32(disk, copy) & 0xf0000000) | next); writes.Add((copy, 4)); }
        }
        private void Link(uint[] chain)
        { for (int i = 0; i < chain.Length; i++) SetNext(chain[i], i + 1 == chain.Length ? EndOfChain : chain[i + 1]); }
        private static int Slots(string name) => (name.Length + 12) / 13 + 1;
        private static byte[] ShortEntry(string alias, byte attributes, uint cluster, uint bytes)
        {
            byte[] entry = new byte[32]; Encoding.ASCII.GetBytes(alias).CopyTo(entry, 0); entry[11] = attributes;
            W16(entry, 16, 0x21); W16(entry, 18, 0x21); W16(entry, 24, 0x21);
            W16(entry, 20, (ushort)(cluster >> 16)); W16(entry, 26, (ushort)cluster); W32(entry, 28, bytes);
            return entry;
        }
        private static IEnumerable<byte[]> Entries(Node node)
        {
            byte[] shortEntry = ShortEntry(node.Alias, node.Directory ? (byte)0x10 : (byte)0x21,
                node.Clusters.Length == 0 ? 0 : node.Clusters[0], (uint)node.Bytes.Length);
            byte checksum = 0;
            for (int index = 0; index < 11; index++) checksum = unchecked((byte)((checksum >> 1) + ((checksum & 1) << 7) + shortEntry[index]));
            int slots = Slots(node.Name) - 1;
            int[] offsets = [1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30];
            for (int ordinal = slots; ordinal > 0; ordinal--)
            {
                byte[] entry = new byte[32]; entry[0] = (byte)(ordinal | (ordinal == slots ? 0x40 : 0)); entry[11] = 15; entry[13] = checksum;
                for (int index = 0; index < 13; index++)
                {
                    int position = (ordinal - 1) * 13 + index;
                    W16(entry, offsets[index], position < node.Name.Length ? node.Name[position] : position == node.Name.Length ? (ushort)0 : ushort.MaxValue);
                }
                yield return entry;
            }
            yield return shortEntry;
        }
        private void WriteEntries(uint[] chain, int start, List<byte[]> entries)
        {
            foreach (byte[] entry in entries.Append(new byte[32]))
            {
                Need(start < chain.Length * 16, "directory_capacity");
                int offset = original.ClusterOffset(chain[start / 16]) + start % 16 * 32;
                entry.CopyTo(disk, offset); writes.Add((offset, 32)); start++;
            }
        }
        public void CheckMutationBounds(byte[] before)
        {
            int cursor = 0;
            foreach (var write in writes.OrderBy(x => x.Start))
            {
                if (write.Start > cursor) Need(before.AsSpan(cursor, write.Start - cursor).SequenceEqual(disk.AsSpan(cursor, write.Start - cursor)), "mutation_bounds");
                cursor = Math.Max(cursor, write.Start + write.Bytes);
            }
            Need(before.AsSpan(cursor).SequenceEqual(disk.AsSpan(cursor)), "mutation_bounds");
        }
    }
}
