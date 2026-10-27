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
public static class WritableDataImageFixture
{
    public const int ImageBytes = 64 * 1024 * 1024, SectorBytes = 512;
    public const uint PartitionFirstLba = 2048, PartitionLastLba = 131038,
        ReservedSectors = 32, FatSectors = 993, FirstFatLba = 2080,
        FirstDataLba = 4066, ClusterCount = 126973;
    public const int MaximumFiles = 64, MaximumFileBytes = 4096 * 512,
        MaximumExtractedBytes = 8 * 1024 * 1024;
    public const int MaximumTransitionSectors = 128;
    public const string MountPath = "/data", ExclusiveCreateName = "m16-write.bin";
    public const string TimestampPolicy = "1980-01-01T00:00:00Z (declared FAT UTC)";
    public static readonly Guid PartitionType = new("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7");
    public static readonly Guid DiskGuid = new("0B315D75-842F-4C81-BEB0-000000000021");
    public static readonly Guid PartitionGuid = new("0B315D75-842F-4C81-BEB0-000000000022");
    public static string LongFileName => new string('l', 251) + ".txt";
    private const uint End = 0x0fffffff, Sectors = ImageBytes / SectorBytes;
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);

    public sealed record Extent(uint FirstCluster, uint Clusters, ulong FirstLba);

    /// <summary>Copied extraction of one regular root file. Lists are readonly;
    /// CopyBytes returns a new logical-byte copy, excluding cluster slack.</summary>
    public sealed class Entry
    {
        private readonly byte[] content;
        internal Entry(string name, string alias, ulong entryOffset, byte attributes,
            ushort date, ushort time, long modified, byte[] bytes, uint[] clusters, Extent[] extents)
        {
            Name = name; ShortAlias = alias; ShortEntryOffset = entryOffset;
            Attributes = attributes; FatDate = date; FatTime = time; ModifiedSeconds = modified;
            content = bytes; Sha256 = Hash(bytes); Clusters = Array.AsReadOnly(clusters);
            Extents = Array.AsReadOnly(extents);
        }
        public string Name { get; }
        public string ShortAlias { get; }
        public ulong ShortEntryOffset { get; }
        public ulong Inode => ShortEntryOffset / 32 + 256;
        public byte Attributes { get; }
        public ushort FatDate { get; }
        public ushort FatTime { get; }
        /// <summary>Declared FAT UTC seconds; zero denotes an unspecified all-zero timestamp.</summary>
        public long ModifiedSeconds { get; }
        public int Bytes => content.Length;
        public string Sha256 { get; }
        public ReadOnlyCollection<uint> Clusters { get; }
        public ReadOnlyCollection<Extent> Extents { get; }
        public byte[] CopyBytes() => (byte[])content.Clone();
    }

    /// <summary>Immutable copied values from actual raw bytes, not the writer's
    /// allocation plan. Free candidates describe this exact image hash only.</summary>
    public sealed class Inspection
    {
        internal Inspection(string rawHash, string[] structures, uint free, uint hint,
            uint[] root, uint[] candidates, Entry[] files, uint first = PartitionFirstLba, bool checkedGpt = true)
        {
            RawSha256 = rawHash; StructureHashes = Array.AsReadOnly(structures);
            FreeClusters = free; FsInfoHint = hint; RootClusters = Array.AsReadOnly(root);
            FreeCandidates = Array.AsReadOnly(candidates); Files = Array.AsReadOnly(files);
            FirstLba = first; LastLba = checked(first + PartitionLastLba - PartitionFirstLba);
            FirstFatSector = first + ReservedSectors; FirstDataSector = FirstFatSector + 2 * FatSectors;
            CheckedGpt = checkedGpt;
        }
        public string RawSha256 { get; }
        public uint FirstLba { get; }
        public uint LastLba { get; }
        public uint FirstFatSector { get; }
        public uint FirstDataSector { get; }
        /// <summary>True only for the original strict64MiB whole-disk inspection.
        /// InspectPartition validates the actual FAT body, not its caller's GPT selection.</summary>
        public bool CheckedGpt { get; }
        /// <summary>Hashes ordered: primary GPT header, backup header, entries,
        /// BPB, FSInfo, FAT0, FAT1. GPT hashes alone are not GPT validation.</summary>
        public ReadOnlyCollection<string> StructureHashes { get; }
        public uint FreeClusters { get; }
        public uint FsInfoHint { get; }
        public ReadOnlyCollection<uint> RootClusters { get; }
        public ReadOnlyCollection<uint> FreeCandidates { get; }
        public ReadOnlyCollection<Entry> Files { get; }
        public Entry? Find(string name) => Files.SingleOrDefault(x =>
            x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || x.ShortAlias.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Exact initial-image extraction receipt, independent of output
    /// filename. CopyJson returns canonical UTF8 bytes with no timestamp/path.</summary>
    public sealed class Receipt
    {
        internal Receipt(Inspection image) { Image = image; json = Serialize(image); }
        private readonly byte[] json;
        public Inspection Image { get; }
        public byte[] CopyJson() => (byte[])json.Clone();
    }

    /// <summary>Physical ownership decoded independently in the before/after
    /// images. FileData includes allocated cluster slack; a classification does
    /// not authorize modifying any particular file or directory entry.</summary>
    public enum SectorKind { Fat0, Fat1, FsInfo, RootDirectory, FileData }

    public sealed record SectorChange(ulong Lba, SectorKind Kind, string OldSha256, string NewSha256);

    /// <summary>Copied, immutable final-image comparison for a small mechanical
    /// mutation. Both extractions remain available, including logical file bytes.
    /// This does not validate intermediate images, an operation's permitted
    /// targets, flush/sync, recovery, persistence, process or guest execution.</summary>
    public sealed class TransitionReceipt
    {
        private readonly byte[] json;
        internal TransitionReceipt(Inspection before, Inspection after, SectorChange[] changes)
        {
            Before = before; After = after; Changes = Array.AsReadOnly(changes);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject(); writer.WriteString("schema", "CLROS_WRITABLE_DATA_TRANSITION_V1");
                writer.WritePropertyName("before"); writer.WriteRawValue(Serialize(before));
                writer.WritePropertyName("after"); writer.WriteRawValue(Serialize(after));
                writer.WriteStartArray("changes");
                foreach (var change in changes)
                {
                    writer.WriteStartObject(); writer.WriteNumber("lba", change.Lba); writer.WriteString("kind", change.Kind.ToString());
                    writer.WriteString("oldSha256", change.OldSha256); writer.WriteString("newSha256", change.NewSha256); writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteEndObject();
            }
            json = stream.ToArray();
        }
        public Inspection Before { get; }
        public Inspection After { get; }
        public ReadOnlyCollection<SectorChange> Changes { get; }
        public byte[] CopyJson() => (byte[])json.Clone();
    }

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
            { stream.Write(image); stream.Flush(true); }
            Verify(temporary, receipt);
            AuthorityFiles.RequirePlainPath(root, parent, "writable_image_output_path");
            File.Move(temporary, output, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
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
        files.VerifyUnchanged(); return result;
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
        Need(image.Length is >= ImageBytes and <= WebRootImageFixture.MaximumImageBytes && image.Length % 512 == 0 &&
            firstLba >= 34 && firstLba <= uint.MaxValue && lastLba >= firstLba && lastLba < (ulong)image.Length / 512 &&
            lastLba - firstLba == PartitionLastLba - PartitionFirstLba, "partition_bounds");
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
    public static TransitionReceipt InspectTransition(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after,
        int maximumChangedSectors = MaximumTransitionSectors)
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
            var oldSector = before.Slice(offset, SectorBytes); var newSector = after.Slice(offset, SectorBytes);
            if (oldSector.SequenceEqual(newSector)) continue;
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
                    if (oldValue == newValue) continue;
                    uint cluster = baseCluster + (uint)slot;
                    Need(cluster >= 2 && cluster - 2 < ClusterCount && owned.Contains(cluster) &&
                        (oldValue & 0xf0000000) == (newValue & 0xf0000000), "transition_fat_ownership");
                }
            }
            else if (lba >= FirstDataLba)
            {
                uint cluster = lba - FirstDataLba + 2;
                if (roots.Contains(cluster)) kind = SectorKind.RootDirectory;
                else { Need(data.Contains(cluster), "transition_unallocated_data"); kind = SectorKind.FileData; }
            }
            else { Need(false, "transition_reserved"); kind = default; }
            changes.Add(new(lba, kind, Hash(oldSector), Hash(newSector)));
        }
        return new(oldImage, newImage, changes.ToArray());
    }

    private static byte[] Build()
    {
        byte[] disk = new byte[ImageBytes];
        Span<byte> mbr = disk.AsSpan(0, 512);
        mbr[450] = 0xee; W32(mbr, 454, 1); W32(mbr, 458, Sectors - 1); mbr[510] = 0x55; mbr[511] = 0xaa;
        Span<byte> entries = disk.AsSpan(1024, 16384);
        PartitionType.TryWriteBytes(entries); PartitionGuid.TryWriteBytes(entries[16..]);
        W64(entries, 32, PartitionFirstLba); W64(entries, 40, PartitionLastLba);
        Encoding.Unicode.GetBytes("CLROS DATA").CopyTo(entries[56..]);
        entries.CopyTo(disk.AsSpan((int)(Sectors - 33) * 512, 16384));
        uint crc = Crc32.Compute(entries);
        Header(disk.AsSpan(512, 512), 1, Sectors - 1, 2, crc);
        Header(disk.AsSpan((int)(Sectors - 1) * 512, 512), Sectors - 1, 1, Sectors - 33, crc);
        Span<byte> boot = disk.AsSpan((int)PartitionFirstLba * 512, 512);
        boot[0] = 0xeb; boot[1] = 0x58; boot[2] = 0x90;
        "CLROSDAT"u8.CopyTo(boot[3..]); W16(boot, 11, 512); boot[13] = 1;
        W16(boot, 14, 32); boot[16] = 2; boot[21] = 0xf8;
        W16(boot, 24, 63); W16(boot, 26, 255); W32(boot, 28, PartitionFirstLba);
        W32(boot, 32, PartitionLastLba - PartitionFirstLba + 1); W32(boot, 36, FatSectors);
        W32(boot, 44, 2); W16(boot, 48, 1); W16(boot, 50, 6);
        boot[64] = 0x80; boot[66] = 0x29; W32(boot, 67, 0x434c0021);
        "CLROS DATA "u8.CopyTo(boot[71..]); "FAT32   "u8.CopyTo(boot[82..]);
        boot[510] = 0x55; boot[511] = 0xaa;
        boot.CopyTo(disk.AsSpan(((int)PartitionFirstLba + 6) * 512, 512));

        Span<byte> fat = disk.AsSpan((int)FirstFatLba * 512, (int)FatSectors * 512);
        W32(fat, 0, 0x0ffffff8); W32(fat, 4, End);
        uint[] roots = [2, 19, 37], fragments = [5, 41, 8, 73];
        Link(roots); Link(fragments); Link([11]);
        byte[] fragmented = new byte[1537];
        for (int i = 0; i < fragmented.Length; i++) fragmented[i] = Pattern(i);
        byte[] guard = "CLROS writable data guard: preserve every byte.\n"u8.ToArray();
        WriteFile(fragments, fragmented); WriteFile([11], guard);
        byte[] directory = new byte[roots.Length * 512];
        Short("LOG     TXT", 0x20, 0, 0).CopyTo(directory, 0);
        Short("UPLOAD  BIN", 0x20, 0, 0).CopyTo(directory, 32);
        Short("SQLITE  DB ", 0x20, 0, 0).CopyTo(directory, 64);
        Short("FRAG    BIN", 0x20, fragments[0], (uint)fragmented.Length).CopyTo(directory, 96);
        Short("GUARD   TXT", 0x21, 11, (uint)guard.Length).CopyTo(directory, 128);
        // Scrubbed tombstones force the max LFN over both fragmented root links.
        for (int i = 5; i < 23; i++) directory[i * 32] = 0xe5;
        byte[] shortLong = Short("LONGNA~1TXT", 0x20, 0, 0);
        Need(Fat32Mutation.EncodeEntryRun(LongFileName, shortLong, directory.AsSpan(23 * 32), out int count) == Fat32Mutation.Status.Success && count == 21, "encode");
        WriteFile(roots, directory);
        fat.CopyTo(disk.AsSpan(((int)FirstFatLba + (int)FatSectors) * 512, fat.Length));
        uint free = ClusterCount - (uint)(roots.Length + fragments.Length + 1);
        Span<byte> info = disk.AsSpan(((int)PartitionFirstLba + 1) * 512, 512);
        W32(info, 0, 0x41615252); W32(info, 484, 0x61417272);
        W32(info, 488, free); W32(info, 492, 3); W32(info, 508, 0xaa550000);
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
                bytes.AsSpan(position, take).CopyTo(disk.AsSpan(ClusterOffset(chain[i]), take)); position += take;
            }
        }
    }

    private static byte[] Short(string alias, byte attributes, uint cluster, uint bytes)
    {
        byte[] entry = new byte[32]; Encoding.ASCII.GetBytes(alias).CopyTo(entry, 0);
        entry[11] = attributes; W16(entry, 16, 33); W16(entry, 18, 33); W16(entry, 24, 33);
        W16(entry, 20, (ushort)(cluster >> 16)); W16(entry, 26, (ushort)cluster); W32(entry, 28, bytes);
        return entry;
    }

    private static void Header(Span<byte> header, uint current, uint backup, uint entries, uint entriesCrc)
    {
        "EFI PART"u8.CopyTo(header); W32(header, 8, 0x10000); W32(header, 12, 92);
        W64(header, 24, current); W64(header, 32, backup); W64(header, 40, 34); W64(header, 48, PartitionLastLba);
        DiskGuid.TryWriteBytes(header[56..]); W64(header, 72, entries); W32(header, 80, 128); W32(header, 84, 128);
        W32(header, 88, entriesCrc); W32(header, 16, Crc32.Compute(header[..92]));
    }

    private sealed class Reader(byte[] disk, uint first = PartitionFirstLba)
    {
        private readonly HashSet<uint> claimed = [];
        private LiveFat32Volume volume;
        private readonly List<Entry> files = [];
        private readonly HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        private readonly uint last = checked(first + PartitionLastLba - PartitionFirstLba);
        private readonly uint firstFat = first + ReservedSectors, firstData = first + ReservedSectors + 2 * FatSectors;
        private int extracted;

        public Inspection Read()
        {
            ReadOnlySpan<byte> mbr = disk.AsSpan(0, 512);
            Need(mbr[510] == 0x55 && mbr[511] == 0xaa && mbr[450] == 0xee && U32(mbr, 454) == 1 && U32(mbr, 458) == Sectors - 1 &&
                mbr.Slice(462, 48).IndexOfAnyExcept((byte)0) < 0, "mbr");
            var primary = Gpt(false); var backup = Gpt(true);
            Need(primary.FirstUsableLba == backup.FirstUsableLba && primary.LastUsableLba == backup.LastUsableLba &&
                primary.DiskGuid == backup.DiskGuid && disk.AsSpan(1024, 16384).SequenceEqual(disk.AsSpan((int)(Sectors - 33) * 512, 16384)), "gpt_copies");
            return ReadPartition(checkedGpt: true);
        }

        public Inspection ReadPartition(bool checkedGpt = false)
        {
            Span<byte> boot = disk.AsSpan(checked((int)first * 512), 512);
            Need(FATClusterChainsLoopBadClusterTruncationDetection.ParseBpb(boot, (ulong)disk.Length / 512, first, last, Complete, ref volume) == LiveFat32Status.Success &&
                volume.RootCluster == 2 && volume.ReservedSectors == ReservedSectors && volume.FatSectors == FatSectors &&
                volume.SectorsPerCluster == 1 && volume.ClusterCount == ClusterCount && volume.TotalSectors == last - first + 1 &&
                volume.FirstFatLba == firstFat && volume.FirstDataLba == firstData && U32(boot, 28) == first &&
                U16(boot, 48) == 1 && U16(boot, 50) == 6, "bpb");
            Need(boot.SequenceEqual(disk.AsSpan(checked(((int)first + 6) * 512), 512)), "boot_backup");
            Span<byte> fat = disk.AsSpan(checked((int)firstFat * 512), (int)FatSectors * 512);
            Span<byte> fat1 = disk.AsSpan(checked(((int)firstFat + (int)FatSectors) * 512), fat.Length);
            Need(fat.SequenceEqual(fat1), "fat_mirror");
            Need(U32(fat, 0) == 0x0ffffff8 && U32(fat, 4) == End, "fat_reserved");
            uint[] roots = Chain(2);
            ReadRoot(roots);
            uint free = 0;
            for (uint cluster = 2; cluster < ClusterCount + 2; cluster++)
            {
                uint next = U32(fat, (int)cluster * 4) & 0x0fffffff;
                if (next == 0) { Need(!claimed.Contains(cluster), "claimed_free"); free++; }
                else Need(claimed.Contains(cluster), "unclaimed_cluster");
            }
            Span<byte> info = disk.AsSpan(checked(((int)first + 1) * 512), 512);
            Need(info.SequenceEqual(disk.AsSpan(checked(((int)first + 7) * 512), 512)), "fsinfo_backup");
            Need(U32(info, 0) == 0x41615252 && U32(info, 484) == 0x61417272 && U32(info, 508) == 0xaa550000, "fsinfo");
            Need(U32(info, 488) == uint.MaxValue || U32(info, 488) == free, "free_count");
            uint hint = U32(info, 492);
            Need(hint == uint.MaxValue || hint >= 2 && hint - 2 < ClusterCount && Next(hint) == 0, "free_hint");
            List<uint> candidates = [];
            for (uint cluster = 100; cluster < ClusterCount + 2 && candidates.Count < 16; cluster += 2)
                if (Next(cluster) == 0) { Need(!claimed.Contains(cluster), "candidate_not_free"); candidates.Add(cluster); }
            return new(Hash(disk), [Hash(disk.AsSpan(512, 512)), Hash(disk.AsSpan(disk.Length - 512, 512)),
                Hash(disk.AsSpan(1024, 16384)), Hash(boot), Hash(info), Hash(fat), Hash(fat1)], free, hint, roots, candidates.ToArray(),
                files.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray(), first, checkedGpt);
        }

        private GptHeader Gpt(bool backup)
        {
            int offset = backup ? ImageBytes - 512 : 512;
            int entryOffset = backup ? (int)(Sectors - 33) * 512 : 1024;
            Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[128];
            var parsed = backup
                ? GptPartitionTableParser.ParseBackup(disk.AsSpan(offset, 512), disk.AsSpan(entryOffset, 16384), Sectors, 512, descriptors)
                : GptPartitionTableParser.ParsePrimary(disk.AsSpan(offset, 512), disk.AsSpan(entryOffset, 16384), Sectors, 512, descriptors);
            Need(parsed.Succeeded && parsed.PartitionsWritten == 1 && parsed.Header.FirstUsableLba == 34 &&
                parsed.Header.LastUsableLba == PartitionLastLba && parsed.Header.DiskGuid == DiskGuid &&
                parsed.Header.PartitionEntryLba == (backup ? Sectors - 33 : 2) && parsed.Header.PartitionEntryCount == 128 &&
                parsed.Header.PartitionEntrySize == 128, "gpt");
            var part = descriptors[0];
            Need(part.PartitionTypeGuid == PartitionType && part.UniquePartitionGuid == PartitionGuid && part.EntryIndex == 0 &&
                part.FirstLba == PartitionFirstLba && part.LastLba == PartitionLastLba && part.Attributes == 0, "partition");
            return parsed.Header;
        }

        private int ClusterOffset(uint cluster) => checked((int)(firstData + cluster - 2) * 512);
        private uint Next(uint cluster) => U32(disk.AsSpan((int)firstFat * 512, (int)FatSectors * 512), checked((int)cluster * 4)) & 0x0fffffff;
        private uint[] Chain(uint first)
        {
            List<uint> chain = [];
            uint current = first;
            while (true)
            {
                Need(current >= 2 && current - 2 < ClusterCount && chain.Count < 4096 && claimed.Add(current), "chain_loop_or_crosslink");
                chain.Add(current);
                ulong sector = firstFat + current / 128; uint next = 0;
                var status = FATClusterChainsLoopBadClusterTruncationDetection.ReadFatEntry(volume, current, sector,
                    disk.AsSpan(checked((int)sector * 512), 512), Complete, ref next);
                if (status == LiveFat32Status.EndOfChain) return chain.ToArray();
                Need(status == LiveFat32Status.Success, "chain"); current = next;
            }
        }

        private void ReadRoot(uint[] roots)
        {
            LiveVfatState state = default; Span<ushort> words = stackalloc ushort[260];
            bool end = false; var device = new Device(disk);
            foreach (uint cluster in roots)
            {
                for (int slot = 0; slot < 16; slot++)
                {
                    if (end) continue;
                    int offset = ClusterOffset(cluster) + slot * 32;
                    Span<byte> raw = disk.AsSpan(offset, 32);
                    var status = MultiClusterDirectoryVFATLongNameSupportSeparate.Consume(raw, ref state, words, out var entry);
                    if (status == LiveDirectoryStatus.End) { end = true; continue; }
                    if (status == LiveDirectoryStatus.LongName) continue;
                    if (status == LiveDirectoryStatus.Skipped) { Need(raw[0] == 0xe5, "root_nonfile"); continue; }
                    Need(status == LiveDirectoryStatus.Entry && (entry.Attributes & 0x18) == 0 && files.Count < MaximumFiles && raw[12] == 0, "directory");
                    string alias = Alias(raw[..11]);
                    string name = entry.HasLongName ? new string(words[..entry.NameLength].ToArray().Select(x => (char)x).ToArray()) : alias;
                    Need(names.Add(name) && (alias.Equals(name, StringComparison.OrdinalIgnoreCase) || names.Add(alias)), "name_collision");
                    uint[] chain = entry.FirstCluster == 0 ? [] : Chain(entry.FirstCluster);
                    Need(entry.ByteCount <= MaximumFileBytes && (ulong)chain.Length == ((ulong)entry.ByteCount + 511) / 512 &&
                        extracted <= MaximumExtractedBytes - entry.ByteCount, "file_length");
                    extracted += (int)entry.ByteCount;
                    byte[] content = new byte[(int)entry.ByteCount];
                    for (int i = 0, position = 0; i < chain.Length; i++)
                    {
                        int take = Math.Min(512, content.Length - position);
                        disk.AsSpan(ClusterOffset(chain[i]), take).CopyTo(content.AsSpan(position)); position += take;
                    }
                    ushort date = U16(raw, 24), time = U16(raw, 22); long modified = 0;
                    Need(date == 0 && time == 0 || GregorianUtc.TryUnixSeconds((uint)(1980 + (date >> 9)), (uint)((date >> 5) & 15), (uint)(date & 31),
                        (uint)(time >> 11), (uint)((time >> 5) & 63), (uint)((time & 31) * 2), out modified), "timestamp");
                    byte[] independentlyRead = new byte[content.Length];
                    var read = Fat32Parser.ReadRootFile(device, first, name, independentlyRead);
                    Need(read.Succeeded && read.BytesRead == entry.ByteCount && read.Entry.FirstCluster == entry.FirstCluster &&
                        content.AsSpan().SequenceEqual(independentlyRead), "independent_extract");
                    List<Extent> extents = [];
                    for (int i = 0; i < chain.Length;)
                    {
                        int start = i++; while (i < chain.Length && chain[i] == chain[i - 1] + 1) i++;
                        extents.Add(new(chain[start], (uint)(i - start), firstData + (ulong)chain[start] - 2));
                    }
                    files.Add(new(name, alias, (ulong)offset, entry.Attributes, date, time, modified, content, chain, extents.ToArray()));
                }
            }
            Need(MultiClusterDirectoryVFATLongNameSupportSeparate.Finish(state) == LiveDirectoryStatus.End, "lfn_tail");
        }
    }

    private sealed class Device(byte[] disk) : IReadOnlyBlockDevice
    {
        public BlockDeviceInfo Info => BlockDeviceInfo.Create(512, (ulong)disk.Length / 512).Info;
        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            ulong sectors = (ulong)disk.Length / 512;
            if (firstBlock >= sectors || blockCount == 0 || blockCount > sectors - firstBlock) return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
            ulong bytes = (ulong)blockCount * 512;
            if ((ulong)destination.Length < bytes) return BlockReadResult.Fail(BlockDeviceStatus.DestinationTooSmall);
            disk.AsSpan(checked((int)firstBlock * 512), checked((int)bytes)).CopyTo(destination);
            return BlockReadResult.Success(blockCount, bytes);
        }
    }

    private static byte[] Serialize(Inspection image)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter json = new(stream))
        {
            json.WriteStartObject(); json.WriteString("schema", "CLROS_WRITABLE_DATA_IMAGE_V1");
            json.WriteString("mount", MountPath); json.WriteString("rawSha256", image.RawSha256);
            json.WriteNumber("imageBytes", ImageBytes); json.WriteString("timestampPolicy", TimestampPolicy);
            json.WriteString("diskGuid", DiskGuid); json.WriteString("partitionGuid", PartitionGuid); json.WriteString("partitionType", PartitionType);
            json.WriteNumber("partitionFirst", PartitionFirstLba); json.WriteNumber("partitionLast", PartitionLastLba);
            json.WriteNumber("fatSectors", FatSectors); json.WriteNumber("firstData", FirstDataLba);
            json.WriteNumber("freeClusters", image.FreeClusters); json.WriteNumber("fsInfoHint", image.FsInfoHint);
            json.WriteStartArray("structures"); foreach (string hash in image.StructureHashes) json.WriteStringValue(hash); json.WriteEndArray();
            json.WriteStartArray("rootClusters"); foreach (uint c in image.RootClusters) json.WriteNumberValue(c); json.WriteEndArray();
            json.WriteStartArray("freeCandidates"); foreach (uint c in image.FreeCandidates) json.WriteNumberValue(c); json.WriteEndArray();
            json.WriteStartArray("files");
            foreach (Entry file in image.Files)
            {
                json.WriteStartObject(); json.WriteString("name", file.Name); json.WriteString("alias", file.ShortAlias);
                json.WriteNumber("shortEntryOffset", file.ShortEntryOffset); json.WriteNumber("inode", file.Inode);
                json.WriteNumber("attributes", file.Attributes); json.WriteNumber("date", file.FatDate); json.WriteNumber("time", file.FatTime);
                json.WriteNumber("modifiedSeconds", file.ModifiedSeconds); json.WriteNumber("bytes", file.Bytes); json.WriteString("sha256", file.Sha256);
                json.WriteStartArray("clusters"); foreach (uint c in file.Clusters) json.WriteNumberValue(c); json.WriteEndArray();
                json.WriteStartArray("extents"); foreach (Extent extent in file.Extents)
                { json.WriteStartObject(); json.WriteNumber("cluster", extent.FirstCluster); json.WriteNumber("count", extent.Clusters); json.WriteNumber("lba", extent.FirstLba); json.WriteEndObject(); }
                json.WriteEndArray(); json.WriteEndObject();
            }
            json.WriteEndArray(); json.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static void ValidateOutput(string root, string output)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Need(output.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, comparison) &&
            !File.Exists(output) && !Directory.Exists(output), "output_path");
        string? existing = Path.GetDirectoryName(output);
        while (existing is not null && !Directory.Exists(existing)) existing = Path.GetDirectoryName(existing);
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
    private static void Need(bool condition, string reason) { if (!condition) throw new ToolFailure("writable_image_" + reason); }
}
