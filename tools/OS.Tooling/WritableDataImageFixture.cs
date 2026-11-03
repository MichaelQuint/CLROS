using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Std.Hash;
using OS.Std.Time;
using OS.Storage;

namespace OS.Tooling;
/// <summary>Deterministic host-only separate writable-data image and independent
/// extraction. This is not a boot disk, device owner, persistence receipt or guest
/// admission. Future guests must demand-read sectors; no image preload is implied.</summary>

public static partial class WritableDataImageFixture
{
    public const int ImageBytes = 64 * 1024 * 1024, SectorBytes = 512;
    public const uint PartitionFirstLba = 2048, PartitionLastLba = 131038, ReservedSectors = 32, FatSectors = 993, FirstFatLba = 2080, FirstDataLba = 4066, ClusterCount = 126973;
    public const int MaximumFiles = 64, MaximumFileBytes = 4096 * 512, MaximumExtractedBytes = 8 * 1024 * 1024;
    public const int MaximumTransitionSectors = 128;
    public const string MountPath = "/data", ExclusiveCreateName = "m16-write.bin";
    public const string TimestampPolicy = "1980-01-01T00:00:00Z (declared FAT UTC)";
    public static readonly Guid PartitionType = new("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7");
    public static readonly Guid DiskGuid = new("0B315D75-842F-4C81-BEB0-000000000021");
    public static readonly Guid PartitionGuid = new("0B315D75-842F-4C81-BEB0-000000000022");
    public static string LongFileName => new string('l', 251) + ".txt";

    private const uint End = 0x0fffffff, Sectors = ImageBytes / SectorBytes;
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);
    /// <summary>Create a new image below repository artifacts. Existing paths
    /// and reparse ancestors reject before staging. No EFI/package inputs are
    /// accepted. Prospective and on-disk independent inspection precede the
    /// no-overwrite move; failure removes the private temporary file.</summary>

    public static Receipt Create(string repositoryRoot, string outputPath)
    {
        string root = Path.GetFullPath(repositoryRoot), output = Path.GetFullPath(outputPath);
        ValidateOutput(root, output);
        byte[] image = Build();
        Inspection checkedImage = Inspect(image);
        Need(checkedImage.Find(ExclusiveCreateName) is null, "exclusive_seed_collision");
        Receipt receipt = new(checkedImage);
        string parent = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(parent);
        AuthorityFiles.RequirePlainPath(root, parent, "writable_image_output_path");
        string temporary = output + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(image);
                stream.Flush(true);
            }

            Verify(temporary, receipt);
            AuthorityFiles.RequirePlainPath(root, parent, "writable_image_output_path");
            File.Move(temporary, output, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

        return receipt;
    }

    /// <summary>Inspect a plain held raw file. Identity/content remain held and
    /// revalidated until all extraction completes; only copied values escape.</summary>

    public static Inspection Inspect(string imagePath)
    {
        string path = Path.GetFullPath(imagePath);
        using AuthorityFiles files = new();
        var file = files.Open(Path.GetPathRoot(path)!, path, "writable data image", "writable_image", ImageBytes, singleLink: true);
        Inspection result = Inspect(file.ReadBytes(ImageBytes, "writable_image"));
        files.VerifyUnchanged();
        return result;
    }

    /// <summary>Synchronously borrow exactly64MiB stable raw bytes and parse a
    /// private bounded copy. All allocated root/file clusters must be accounted
    /// for globally, both FATs must agree, and file bytes are cross-checked by
    /// the separate host Fat32Parser. Supports regular root files only, at most
    ///64 files/2MiB each/8MiB total/4096 clusters per chain. Throws ToolFailure
    /// on malformed/unsupported input; grants no process/native/device authority.</summary>

    public static Inspection Inspect(ReadOnlySpan<byte> image)
    {
        Need(image.Length == ImageBytes, "size");
        return new Reader(image.ToArray()).Read();
    }

    /// <summary>Synchronously borrow stable raw bytes and independently inspect
    /// this fixture's FAT32 body at actual relocated bounds. The fixed partition
    /// length, SPC1/FAT993 geometry, HiddenSectors, backups, full FAT allocation,
    /// root and all file bytes are checked. All returned offsets/extents refer
    /// to the actual image, never a synthetic projected disk. The caller must
    /// authenticate GPT selection separately; CheckedGpt is false. At most256MiB
    /// is copied; old Inspect and its64MiB receipt format remain unchanged.</summary>

    public static Inspection InspectPartition(ReadOnlySpan<byte> image, ulong firstLba, ulong lastLba)
    {
        Need(image.Length is >= ImageBytes and <= WebRootImageFixture.MaximumImageBytes && image.Length % 512 == 0 && firstLba >= 34 && firstLba <= uint.MaxValue && lastLba >= firstLba && lastLba < (ulong)image.Length / 512 && lastLba - firstLba == PartitionLastLba - PartitionFirstLba, "partition_bounds");
        return new Reader(image.ToArray(), (uint)firstLba).ReadPartition();
    }

    /// <summary>Independently reread and compare exact raw-image identity and
    /// canonical extracted receipt. The expected receipt is borrowed unchanged.</summary>

    public static Inspection Verify(string imagePath, Receipt expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        Inspection actual = Inspect(imagePath);
        Need(actual.RawSha256 == expected.Image.RawSha256 && Serialize(actual).AsSpan().SequenceEqual(expected.CopyJson()), "receipt_identity");
        return actual;
    }

    /// <summary>Synchronously borrow two stable exact64MiB snapshots and strictly
    /// inspect both, then copy at most1..128 changed-sector facts. Outside the
    /// partition, boot/reserved bytes, FAT padding/reserved entries/high bits and
    /// unallocated data must remain identical. Changed FAT entries must belong
    /// to a decoded root/file chain in either snapshot. Allocated data includes
    /// slack and freed/new chains, so callers must separately assert permitted
    /// operation targets. Throws ToolFailure without a partial receipt. No input
    /// spans are retained, no files are written and no execution/flush/durability
    /// authority is granted; intermediate dirty images are unsupported.</summary>

    public static TransitionReceipt InspectTransition(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after, int maximumChangedSectors = MaximumTransitionSectors)
    {
        Need(maximumChangedSectors is >= 1 and <= MaximumTransitionSectors, "transition_limit");
        Inspection oldImage = Inspect(before), newImage = Inspect(after);
        int start = checked((int)PartitionFirstLba * SectorBytes), end = checked(((int)PartitionLastLba + 1) * SectorBytes);
        Need(before[..start].SequenceEqual(after[..start]) && before[end..].SequenceEqual(after[end..]), "transition_outside_partition");
        HashSet<uint> roots = oldImage.RootClusters.Concat(newImage.RootClusters).ToHashSet();
        HashSet<uint> data = oldImage.Files.Concat(newImage.Files).SelectMany(x => x.Clusters).ToHashSet();
        var owned = roots.Concat(data).ToHashSet();
        List<SectorChange> changes = [];
        for (uint lba = PartitionFirstLba; lba <= PartitionLastLba; lba++)
        {
            int offset = checked((int)lba * SectorBytes);
            var oldSector = before.Slice(offset, SectorBytes);
            var newSector = after.Slice(offset, SectorBytes);
            if (oldSector.SequenceEqual(newSector))
                continue;
            Need(changes.Count < maximumChangedSectors, "transition_capacity");
            SectorKind kind;
            if (lba is PartitionFirstLba + 1 or PartitionFirstLba + 7)
            {
                Need(oldSector[..488].SequenceEqual(newSector[..488]) && oldSector[496..].SequenceEqual(newSector[496..]), "transition_fsinfo_reserved");
                kind = SectorKind.FsInfo;
            }
            else if (lba >= FirstFatLba && lba < FirstDataLba)
            {
                kind = lba < FirstFatLba + FatSectors ? SectorKind.Fat0 : SectorKind.Fat1;
                uint baseCluster = (lba - (kind == SectorKind.Fat0 ? FirstFatLba : FirstFatLba + FatSectors)) * 128;
                for (int slot = 0; slot < 128; slot++)
                {
                    uint oldValue = U32(oldSector, slot * 4), newValue = U32(newSector, slot * 4);
                    if (oldValue == newValue)
                        continue;
                    uint cluster = baseCluster + (uint)slot;
                    Need(cluster >= 2 && cluster - 2 < ClusterCount && owned.Contains(cluster) && (oldValue & 0xf0000000) == (newValue & 0xf0000000), "transition_fat_ownership");
                }
            }
            else if (lba >= FirstDataLba)
            {
                uint cluster = lba - FirstDataLba + 2;
                if (roots.Contains(cluster))
                    kind = SectorKind.RootDirectory;
                else
                {
                    Need(data.Contains(cluster), "transition_unallocated_data");
                    kind = SectorKind.FileData;
                }
            }
            else
            {
                Need(false, "transition_reserved");
                kind = default;
            }

            changes.Add(new(lba, kind, Hash(oldSector), Hash(newSector)));
        }

        return new(oldImage, newImage, changes.ToArray());
    }

    private static byte[] Build()
    {
        byte[] disk = new byte[ImageBytes];
        Span<byte> mbr = disk.AsSpan(0, 512);
        mbr[450] = 0xee;
        W32(mbr, 454, 1);
        W32(mbr, 458, Sectors - 1);
        mbr[510] = 0x55;
        mbr[511] = 0xaa;
        Span<byte> entries = disk.AsSpan(1024, 16384);
        PartitionType.TryWriteBytes(entries);
        PartitionGuid.TryWriteBytes(entries[16..]);
        W64(entries, 32, PartitionFirstLba);
        W64(entries, 40, PartitionLastLba);
        Encoding.Unicode.GetBytes("CLROS DATA").CopyTo(entries[56..]);
        entries.CopyTo(disk.AsSpan((int)(Sectors - 33) * 512, 16384));
        uint crc = Crc32.Compute(entries);
        Header(disk.AsSpan(512, 512), 1, Sectors - 1, 2, crc);
        Header(disk.AsSpan((int)(Sectors - 1) * 512, 512), Sectors - 1, 1, Sectors - 33, crc);
        Span<byte> boot = disk.AsSpan((int)PartitionFirstLba * 512, 512);
        boot[0] = 0xeb;
        boot[1] = 0x58;
        boot[2] = 0x90;
        "CLROSDAT"u8.CopyTo(boot[3..]);
        W16(boot, 11, 512);
        boot[13] = 1;
        W16(boot, 14, 32);
        boot[16] = 2;
        boot[21] = 0xf8;
        W16(boot, 24, 63);
        W16(boot, 26, 255);
        W32(boot, 28, PartitionFirstLba);
        W32(boot, 32, PartitionLastLba - PartitionFirstLba + 1);
        W32(boot, 36, FatSectors);
        W32(boot, 44, 2);
        W16(boot, 48, 1);
        W16(boot, 50, 6);
        boot[64] = 0x80;
        boot[66] = 0x29;
        W32(boot, 67, 0x434c0021);
        "CLROS DATA "u8.CopyTo(boot[71..]);
        "FAT32   "u8.CopyTo(boot[82..]);
        boot[510] = 0x55;
        boot[511] = 0xaa;
        boot.CopyTo(disk.AsSpan(((int)PartitionFirstLba + 6) * 512, 512));
        Span<byte> fat = disk.AsSpan((int)FirstFatLba * 512, (int)FatSectors * 512);
        W32(fat, 0, 0x0ffffff8);
        W32(fat, 4, End);
        uint[] roots = [2, 19, 37], fragments = [5, 41, 8, 73];
        Link(roots);
        Link(fragments);
        Link([11]);
        byte[] fragmented = new byte[1537];
        for (int i = 0; i < fragmented.Length; i++)
            fragmented[i] = Pattern(i);
        byte[] guard = "CLROS writable data guard: preserve every byte.\n"u8.ToArray();
        WriteFile(fragments, fragmented);
        WriteFile([11], guard);
        byte[] directory = new byte[roots.Length * 512];
        Short("LOG     TXT", 0x20, 0, 0).CopyTo(directory, 0);
        Short("UPLOAD  BIN", 0x20, 0, 0).CopyTo(directory, 32);
        Short("SQLITE  DB ", 0x20, 0, 0).CopyTo(directory, 64);
        Short("FRAG    BIN", 0x20, fragments[0], (uint)fragmented.Length).CopyTo(directory, 96);
        Short("GUARD   TXT", 0x21, 11, (uint)guard.Length).CopyTo(directory, 128);
        // Scrubbed tombstones force the max LFN over both fragmented root links.
        for (int i = 5; i < 23; i++)
            directory[i * 32] = 0xe5;
        byte[] shortLong = Short("LONGNA~1TXT", 0x20, 0, 0);
        Need(Fat32Mutation.EncodeEntryRun(LongFileName, shortLong, directory.AsSpan(23 * 32), out int count) == Fat32Mutation.Status.Success && count == 21, "encode");
        WriteFile(roots, directory);
        fat.CopyTo(disk.AsSpan(((int)FirstFatLba + (int)FatSectors) * 512, fat.Length));
        uint free = ClusterCount - (uint)(roots.Length + fragments.Length + 1);
        Span<byte> info = disk.AsSpan(((int)PartitionFirstLba + 1) * 512, 512);
        W32(info, 0, 0x41615252);
        W32(info, 484, 0x61417272);
        W32(info, 488, free);
        W32(info, 492, 3);
        W32(info, 508, 0xaa550000);
        info.CopyTo(disk.AsSpan(((int)PartitionFirstLba + 7) * 512, 512));
        return disk;
        void Link(uint[] chain)
        {
            for (int i = 0; i < chain.Length; i++)
                W32(disk.AsSpan((int)FirstFatLba * 512, (int)FatSectors * 512), (int)chain[i] * 4, i + 1 == chain.Length ? End : chain[i + 1]);
        }

        void WriteFile(uint[] chain, byte[] bytes)
        {
            for (int i = 0, position = 0; i < chain.Length && position < bytes.Length; i++)
            {
                int take = Math.Min(512, bytes.Length - position);
                bytes.AsSpan(position, take).CopyTo(disk.AsSpan(ClusterOffset(chain[i]), take));
                position += take;
            }
        }
    }

    private static byte[] Short(string alias, byte attributes, uint cluster, uint bytes)
    {
        byte[] entry = new byte[32];
        Encoding.ASCII.GetBytes(alias).CopyTo(entry, 0);
        entry[11] = attributes;
        W16(entry, 16, 33);
        W16(entry, 18, 33);
        W16(entry, 24, 33);
        W16(entry, 20, (ushort)(cluster >> 16));
        W16(entry, 26, (ushort)cluster);
        W32(entry, 28, bytes);
        return entry;
    }

    private static void Header(Span<byte> header, uint current, uint backup, uint entries, uint entriesCrc)
    {
        "EFI PART"u8.CopyTo(header);
        W32(header, 8, 0x10000);
        W32(header, 12, 92);
        W64(header, 24, current);
        W64(header, 32, backup);
        W64(header, 40, 34);
        W64(header, 48, PartitionLastLba);
        DiskGuid.TryWriteBytes(header[56..]);
        W64(header, 72, entries);
        W32(header, 80, 128);
        W32(header, 84, 128);
        W32(header, 88, entriesCrc);
        W32(header, 16, Crc32.Compute(header[..92]));
    }

    private static byte[] Serialize(Inspection image)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter json = new(stream))
        {
            json.WriteStartObject();
            json.WriteString("schema", "CLROS_WRITABLE_DATA_IMAGE_V1");
            json.WriteString("mount", MountPath);
            json.WriteString("rawSha256", image.RawSha256);
            json.WriteNumber("imageBytes", ImageBytes);
            json.WriteString("timestampPolicy", TimestampPolicy);
            json.WriteString("diskGuid", DiskGuid);
            json.WriteString("partitionGuid", PartitionGuid);
            json.WriteString("partitionType", PartitionType);
            json.WriteNumber("partitionFirst", PartitionFirstLba);
            json.WriteNumber("partitionLast", PartitionLastLba);
            json.WriteNumber("fatSectors", FatSectors);
            json.WriteNumber("firstData", FirstDataLba);
            json.WriteNumber("freeClusters", image.FreeClusters);
            json.WriteNumber("fsInfoHint", image.FsInfoHint);
            json.WriteStartArray("structures");
            foreach (string hash in image.StructureHashes)
                json.WriteStringValue(hash);
            json.WriteEndArray();
            json.WriteStartArray("rootClusters");
            foreach (uint c in image.RootClusters)
                json.WriteNumberValue(c);
            json.WriteEndArray();
            json.WriteStartArray("freeCandidates");
            foreach (uint c in image.FreeCandidates)
                json.WriteNumberValue(c);
            json.WriteEndArray();
            json.WriteStartArray("files");
            foreach (Entry file in image.Files)
            {
                json.WriteStartObject();
                json.WriteString("name", file.Name);
                json.WriteString("alias", file.ShortAlias);
                json.WriteNumber("shortEntryOffset", file.ShortEntryOffset);
                json.WriteNumber("inode", file.Inode);
                json.WriteNumber("attributes", file.Attributes);
                json.WriteNumber("date", file.FatDate);
                json.WriteNumber("time", file.FatTime);
                json.WriteNumber("modifiedSeconds", file.ModifiedSeconds);
                json.WriteNumber("bytes", file.Bytes);
                json.WriteString("sha256", file.Sha256);
                json.WriteStartArray("clusters");
                foreach (uint c in file.Clusters)
                    json.WriteNumberValue(c);
                json.WriteEndArray();
                json.WriteStartArray("extents");
                foreach (Extent extent in file.Extents)
                {
                    json.WriteStartObject();
                    json.WriteNumber("cluster", extent.FirstCluster);
                    json.WriteNumber("count", extent.Clusters);
                    json.WriteNumber("lba", extent.FirstLba);
                    json.WriteEndObject();
                }

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void ValidateOutput(string root, string output)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Need(output.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, comparison) && !File.Exists(output) && !Directory.Exists(output), "output_path");
        string? existing = Path.GetDirectoryName(output);
        while (existing is not null && !Directory.Exists(existing))
            existing = Path.GetDirectoryName(existing);
        Need(existing is not null, "output_path");
        AuthorityFiles.RequirePlainPath(root, existing!, "writable_image_output_path");
    }

    private static string Alias(ReadOnlySpan<byte> raw)
    {
        string stem = Encoding.ASCII.GetString(raw[..8]).TrimEnd(' '), extension = Encoding.ASCII.GetString(raw.Slice(8, 3)).TrimEnd(' ');
        return extension.Length == 0 ? stem : stem + "." + extension;
    }

    private static int ClusterOffset(uint cluster) => checked((int)(FirstDataLba + cluster - 2) * 512);
    private static byte Pattern(int position) => unchecked((byte)((position * 73 + 19) ^ (position / 512)));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static void W16(Span<byte> bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(offset, 2), value);
    private static void W32(Span<byte> bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset, 4), value);
    private static void W64(Span<byte> bytes, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(offset, 8), value);
    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("writable_image_" + reason);
    }
}
