using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;
/// <summary>Host-only import of an immutable published webroot into a fresh
/// copy of a checked GPT/FAT32 image. Images and receipts do not establish guest
/// I/O. The base and source files remain held read-only through publication.</summary>

public static partial class WebRootImageFixture
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
        string text = string.Concat(sorted.Select(x => x.Path + "\t" + x.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\t" + x.Sha256 + "\n"));
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
                if (Directory.Exists(path))
                {
                    Walk(path);
                    continue;
                }

                var file = files.Open(root, path, "published asset", "webroot_asset", MaximumImageBytes, allowEmpty: true, singleLink: true);
                byte[] bytes = file.ReadBytes(MaximumImageBytes, "webroot_asset");
                assets.Add(new(relative, bytes.Length, Hash(bytes)));
            }
        }
    }

    /// <summary>Publish a new artifacts image only after exact source revalidation,
    /// prospective parser readback and preserved-file/mutation checks. DiskA/B
    /// differ only in equal-length fixture-nonce.txt bytes. Failures before the
    /// final atomic move leave no output image; an existing output is never replaced.</summary>

    public static Receipt Apply(string repositoryRoot, string baseImagePath, string publishedRoot, Snapshot inventory, string outputPath, string variant = "DiskA")
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
        Need(!before.RootEntries.Any(x => x.Name.Equals("app", StringComparison.OrdinalIgnoreCase) || x.Alias.Equals("APP", StringComparison.OrdinalIgnoreCase) || x.Name.Equals("private", StringComparison.OrdinalIgnoreCase) || x.Alias.Equals("PRIVATE", StringComparison.OrdinalIgnoreCase)), "root_collision");
        Node tree = new("", true);
        Node app = tree.AddDirectory("app"), webroot = app.AddDirectory("wwwroot");
        foreach (Asset asset in current.Files)
        {
            Need(!asset.Path.Split('/')[0].Equals("fixture", StringComparison.OrdinalIgnoreCase) && !asset.Path.Equals("fixture-nonce.txt", StringComparison.OrdinalIgnoreCase), "reserved_fixture_path");
            var input = inputs.Open(root, Path.Combine(source, asset.Path.Replace('/', Path.DirectorySeparatorChar)), "published asset", "webroot_asset", asset.Bytes, allowEmpty: true, singleLink: true);
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
        Need(generated.PartitionFirstLba == originalInventory.PartitionFirstLba && generated.PartitionLastLba == originalInventory.PartitionLastLba, "partition_changed");
        foreach (DiskFile file in originalInventory.Files)
            Need(generated.Files.Any(x => SameFile(x, file)), "preserved_file_changed");
        DiskFile[] webFiles = generated.Files.Where(x => x.Path.StartsWith(GuestRoot + "/", StringComparison.Ordinal)).ToArray();
        Node[] expected = webroot.Descendants().Where(x => !x.Directory).ToArray();
        Need(webFiles.Length == expected.Length, "webroot_inventory");
        foreach (Node node in expected)
            Need(webFiles.Any(x => x.Path == node.Path && x.Bytes == node.Bytes.Length && x.Sha256 == Hash(node.Bytes) && x.Attributes == 0x21 && x.FatDate == 0x21 && x.FatTime == 0), "webroot_readback");
        DiskFile sentinel = generated.Files.Single(x => x.Path == "/private/sentinel.txt");
        var receipt = new Receipt(variant, originalInventory.Sha256, generated.Sha256, current.Sha256, current.Files.Length, current.TotalBytes, current.MaximumFileBytes, generated.PartitionFirstLba, generated.PartitionLastLba, TimestampPolicy, webFiles, originalInventory.Files, sentinel);
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
            {
                stream.Write(disk);
                stream.Flush(true);
            }

            Verify(temporary, receipt);
            inputs.VerifyUnchanged();
            Need(SameSnapshot(current, SnapshotPublishedRoot(source)), "source_inventory_changed");
            File.Move(temporary, output, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

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
        Need(actual.Sha256 == expected.FinalSha256 && actual.PartitionFirstLba == expected.PartitionFirstLba && actual.PartitionLastLba == expected.PartitionLastLba, "image_identity");
        DiskFile[] files = [..expected.PreservedFiles, ..expected.WebRootFiles, expected.Sentinel];
        Need(actual.Files.Length == files.Length && files.All(file => actual.Files.Any(x => SameFile(x, file))), "image_inventory");
    }

    /// <summary>Create a new controlled missing/truncated/looped/mismatched-FAT
    /// image from a verified positive fixture. These are expected failures, not
    /// healthy image receipts; only the fragmented fixture's slots/FAT links change.</summary>

    public static FaultReceipt CreateFaultVariant(string repositoryRoot, string sourceImagePath, Receipt positive, string outputPath, string variant)
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
            foreach (int offset in entry!.SlotOffsets)
                disk[offset] = 0xe5;
            ImageInventory missing = new Reader(disk).Inventory();
            Need(missing.Files.Length == before.Files.Length - 1 && !missing.Files.Any(x => x.Path == target) && before.Files.Where(x => x.Path != target).All(file => missing.Files.Any(x => SameFile(x, file))), "missing_variant");
            expected = "missing_path";
        }
        else
        {
            int fat = checked((int)reader.Volume.FirstFatLba * 512 + (int)entry!.Cluster * 4);
            int mirror = fat + checked((int)reader.Volume.FatSectors * 512);
            uint value = variant == "Truncated" ? EndOfChain : entry.Cluster;
            if (variant != "MirrorMismatch")
                W32(disk, fat, value);
            W32(disk, mirror, value);
            expected = variant == "Truncated" ? "webroot_image_file_chain_length" : variant == "Loop" ? "webroot_image_chain_loop_or_crosslink" : "webroot_image_fat_mirror";
            bool rejected = false;
            try
            {
                _ = new Reader(disk);
            }
            catch (ToolFailure failure)when (failure.Reason == expected)
            {
                rejected = true;
            }

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
            {
                stream.Write(disk);
                stream.Flush(true);
            }

            using (var readback = File.OpenRead(temporary))
                Need(Convert.ToHexString(SHA256.HashData(readback)) == hash, "fault_identity");
            inputs.VerifyUnchanged();
            File.Move(temporary, output, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

        return new(variant, positive.FinalSha256, hash, target, expected);
    }

    /// <summary>Hold distinct plain positive/fault images read-only and verify
    /// an existing fixed-target fault receipt. The positive image must pass the
    /// complete healthy inventory. Only the exact variant's directory deletion
    /// bytes or first mirrored FAT words may differ; every other byte, including
    /// EFI/package payloads, remains equal. Independently observe the declared
    /// missing path or extraction failure without admitting a malformed image
    /// as a healthy inventory. Invalid evidence throws ToolFailure; no files are
    /// changed, device callbacks invoked, or guest failure authority granted.
    /// Original consumers must separately prove their actual failure and cleanup.</summary>

    public static FaultInspection VerifyFaultVariant(string repositoryRoot, string sourceImagePath, Receipt positive, string faultImagePath, FaultReceipt expected)
    {
        if (positive is null || expected is null)
            throw new ToolFailure("webroot_image_fault_input");
        string variant = expected.Variant;
        Need(variant is "Missing" or "Truncated" or "Loop" or "MirrorMismatch", "fault_variant");
        const string target = GuestRoot + "/fixture/fragmented.bin";
        string failure = variant switch
        {
            "Missing" => "missing_path",
            "Truncated" => "webroot_image_file_chain_length",
            "Loop" => "webroot_image_chain_loop_or_crosslink",
            _ => "webroot_image_fat_mirror"
        };
        Need(expected.TargetPath == target && expected.ExpectedReadback == failure && expected.SourceSha256 == positive.FinalSha256, "fault_receipt");
        string root = Path.GetFullPath(repositoryRoot);
        using AuthorityFiles inputs = new();
        var source = inputs.Open(root, sourceImagePath, "positive fixture", "webroot_positive", MaximumImageBytes, singleLink: true);
        var fault = inputs.Open(root, faultImagePath, "fault fixture", "webroot_fault", MaximumImageBytes, singleLink: true);
        AuthorityFiles.RequireDistinct([source, fault]);
        byte[] original = source.ReadBytes(MaximumImageBytes, "webroot_positive");
        byte[] actual = fault.ReadBytes(MaximumImageBytes, "webroot_fault");
        Need(Hash(original) == positive.FinalSha256, "fault_positive_identity");
        Need(actual.Length == original.Length && Hash(actual) == expected.FinalSha256, "fault_identity");
        // Do not weaken the healthy parser to extract around a malformed file.
        _ = WebRootDiskInventory.Inspect(source.Path, original, positive);
        var reader = new Reader(original);
        var baseline = reader.Inventory();
        Need(reader.FileEntries.TryGetValue(target, out Entry? entry), "fault_target");
        Entry selected = entry!;
        DiskFile originalTarget = baseline.Files.Single(f => f.Path == target);
        int fat0 = checked((int)reader.Volume.FirstFatLba * 512 + (int)selected.Cluster * 4);
        int fat1 = checked(fat0 + (int)reader.Volume.FatSectors * 512);
        Dictionary<int, byte> edits = [];
        if (variant == "Missing")
            foreach (int offset in selected.SlotOffsets)
                edits.Add(offset, 0xe5);
        else
        {
            uint value = variant == "Truncated" ? EndOfChain : selected.Cluster;
            if (variant != "MirrorMismatch")
                Word(fat0, value);
            Word(fat1, value);
        }

        int changed = 0, at = 0;
        foreach (var(offset, value)in edits.OrderBy(e => e.Key))
        {
            Need(actual.AsSpan(at, offset - at).SequenceEqual(original.AsSpan(at, offset - at)) && actual[offset] == value, "fault_mutation_scope");
            if (actual[offset] != original[offset])
                changed++;
            at = offset + 1;
        }

        Need(actual.AsSpan(at).SequenceEqual(original.AsSpan(at)), "fault_mutation_scope");
        Need(changed != 0, "fault_not_observed");
        bool observed = false;
        if (variant == "Missing")
        {
            ImageInventory missing = new Reader(actual).Inventory();
            observed = missing.Files.Length == baseline.Files.Length - 1 && !missing.Files.Any(f => f.Path == target) && baseline.Files.Where(f => f.Path != target).All(f => missing.Files.Any(other => SameFile(f, other)));
        }
        else
        {
            try
            {
                _ = new Reader(actual);
            }
            catch (ToolFailure rejected)when (rejected.Reason == failure)
            {
                observed = true;
            }
        }

        Need(observed, "fault_not_observed");
        inputs.VerifyUnchanged();
        return new(expected, baseline.PartitionFirstLba, baseline.PartitionLastLba, selected.Cluster, originalTarget.Bytes, originalTarget.Sha256, (ulong)fat0, (ulong)fat1, U32(original, fat0), U32(original, fat1), U32(actual, fat0), U32(actual, fat1), changed);
        void Word(int offset, uint value)
        {
            for (int i = 0; i < 4; i++)
                edits.Add(offset + i, unchecked((byte)(value >> (i * 8))));
        }
    }

    private static bool SameFile(DiskFile a, DiskFile b) => a.Path == b.Path && a.Bytes == b.Bytes && a.Sha256 == b.Sha256 && a.Attributes == b.Attributes && a.FatDate == b.FatDate && a.FatTime == b.FatTime && a.Extents.SequenceEqual(b.Extents);
    private static bool SameSnapshot(Snapshot a, Snapshot b) => a.Sha256 == b.Sha256 && a.TotalBytes == b.TotalBytes && a.MaximumFileBytes == b.MaximumFileBytes && a.Files.SequenceEqual(b.Files);
    private static byte[] Pattern(int count)
    {
        byte[] bytes = new byte[count];
        for (int i = 0; i < count; i++)
            bytes[i] = unchecked((byte)((i * 73 + 19) ^ (i >> 9) ^ (i >> 17)));
        return bytes;
    }

    private static void AddFile(Node root, string relative, byte[] bytes)
    {
        string[] parts = relative.Split('/');
        Node directory = root;
        foreach (string part in parts[..^1])
            directory = directory.AddDirectory(part);
        Need(!directory.Children.Any(x => x.Name.Equals(parts[^1], StringComparison.OrdinalIgnoreCase)), "path_collision");
        directory.Children.Add(new(parts[^1], false) { Parent = directory, Bytes = bytes });
    }

    private static void ValidatePath(string relative, bool directory)
    {
        string[] parts = relative.Split('/');
        Need(relative.Length != 0 && GuestRoot.Length + 1 + relative.Length <= MaximumGuestPathBytes && parts.Length <= MaximumDirectoryDepth + (directory ? 0 : 1), "path_bounds");
        foreach (string part in parts)
            Need(part.Length != 0 && part is not "." and not ".." && part[^1] is not '.' and not ' ' && part.All(c => c is >= '!' and <= '~' && "\\/:*?\"<>|".IndexOf(c) < 0), "path_invalid");
    }

    private static void ValidateOutput(string root, string output, string source)
    {
        string artifacts = Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Need(output.StartsWith(artifacts, comparison) && !output.StartsWith(Path.TrimEndingDirectorySeparator(source) + Path.DirectorySeparatorChar, comparison) && !File.Exists(output) && !Directory.Exists(output), "output_path");
        string? existing = Path.GetDirectoryName(output);
        while (existing is not null && !Directory.Exists(existing))
            existing = Path.GetDirectoryName(existing);
        Need(existing is not null, "output_path");
        AuthorityFiles.RequirePlainPath(root, existing!, "webroot_output_path");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static void W16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
    private static void W32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("webroot_image_" + reason);
    }
}
