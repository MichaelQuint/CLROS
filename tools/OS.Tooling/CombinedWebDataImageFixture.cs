using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Std.Hash;
using OS.Storage;
using DataImage = OS.Tooling.WritableDataImageFixture;
using Gpt = OS.Storage.PrimaryGPTIndependentlyBoundsCRC;

namespace OS.Tooling;

/// <summary>Host-only deterministic two-partition fixture. Preserve an original
///64MiB EFI/package/webroot partition and append a relocated writable-data FAT32
/// partition on the same128MiB image. This creates no device, queue, mounted
/// volume, guest admission, flush, persistence or execution authority.</summary>
public static class CombinedWebDataImageFixture
{
    public const int ImageBytes = 128 * 1024 * 1024, InputImageBytes = DataImage.ImageBytes;
    public const uint Sectors = ImageBytes / 512, WebFirstLba = 2048, WebLastLba = 131038,
        DataFirstLba = 131072, DataLastLba = 260062, DataLbaDelta = 129024;
    public static readonly Guid EfiPartitionType = new("C12A7328-F81F-11D2-BA4B-00A0C93EC93B");
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);

    /// <summary>Immutable copied extraction of a preserved EFI/package/web file.
    /// Content excludes slack; physical extents refer to this actual image.</summary>
    public sealed class WebFile
    {
        private readonly byte[] content;
        internal WebFile(WebRootImageFixture.DiskFile file, byte[] raw)
        {
            Path = file.Path; Bytes = file.Bytes; Sha256 = file.Sha256; Attributes = file.Attributes;
            FatDate = file.FatDate; FatTime = file.FatTime;
            Extents = Array.AsReadOnly(file.Extents.Select(x => new WebRootImageFixture.Extent(x.FirstCluster, x.Clusters, x.FirstLba)).ToArray());
            content = new byte[checked((int)file.Bytes)]; int position = 0;
            foreach (var extent in Extents)
            {
                int take = (int)Math.Min((ulong)(content.Length - position), (ulong)extent.Clusters * 512);
                Need(extent.FirstLba < (ulong)raw.Length / 512 && (ulong)take <= (ulong)raw.Length - extent.FirstLba * 512, "web_extent");
                raw.AsSpan(checked((int)extent.FirstLba * 512), take).CopyTo(content.AsSpan(position)); position += take;
            }
            Need(position == content.Length && Hash(content) == Sha256, "web_extract");
        }
        public string Path { get; }
        public uint Bytes { get; }
        public string Sha256 { get; }
        public byte Attributes { get; }
        public ushort FatDate { get; }
        public ushort FatTime { get; }
        public ReadOnlyCollection<WebRootImageFixture.Extent> Extents { get; }
        public byte[] CopyBytes() => (byte[])content.Clone();
    }

    /// <summary>Copied facts from exact actual GPT/partition bytes. Both GPT
    /// copies and both filesystem bodies have been independently inspected.
    /// The nested Data.CheckedGpt stays false because that helper checks only
    /// its actual FAT body; this enclosing inspection checks its GPT selection.</summary>
    public sealed class Inspection
    {
        internal Inspection(byte[] raw, Guid disk, GptPartitionDescriptor web, GptPartitionDescriptor data,
            WebRootImageFixture.ImageInventory webFiles, DataImage.Inspection dataFiles)
        {
            RawSha256 = Hash(raw); DiskGuid = disk; WebPartitionGuid = web.UniquePartitionGuid; DataPartitionGuid = data.UniquePartitionGuid;
            WebFirst = web.FirstLba; WebLast = web.LastLba; DataFirst = data.FirstLba; DataLast = data.LastLba;
            WebPartitionSha256 = Hash(Partition(raw, web.FirstLba, web.LastLba));
            DataPartitionSha256 = Hash(Partition(raw, data.FirstLba, data.LastLba));
            WebFiles = Array.AsReadOnly(webFiles.Files.Select(file => new WebFile(file, raw)).ToArray()); Data = dataFiles;
        }
        public string RawSha256 { get; }
        public Guid DiskGuid { get; }
        public Guid WebPartitionGuid { get; }
        public Guid DataPartitionGuid { get; }
        public ulong CapacitySectors => Sectors;
        public ulong WebFirst { get; }
        public ulong WebLast { get; }
        public ulong DataFirst { get; }
        public ulong DataLast { get; }
        public string WebPartitionSha256 { get; }
        public string DataPartitionSha256 { get; }
        public ReadOnlyCollection<WebFile> WebFiles { get; }
        public DataImage.Inspection Data { get; }
    }

    /// <summary>Canonical copied creation receipt without filename or clock.
    /// Input hashes describe held original files checked during publication;
    /// subsequent Verify rechecks the output, not those former input leases.</summary>
    public sealed class Receipt
    {
        private readonly byte[] json;
        internal Receipt(string webHash, string dataHash, Inspection image)
        { OriginalWebSha256 = webHash; OriginalDataSha256 = dataHash; Image = image; json = Serialize(webHash, dataHash, image); }
        public string OriginalWebSha256 { get; }
        public string OriginalDataSha256 { get; }
        public Inspection Image { get; }
        public byte[] CopyJson() => (byte[])json.Clone();
    }

    /// <summary>Hold exact original inputs read-only, independently inspect
    /// them, and publish a new artifacts file without replacement. Preserve
    /// the entire ESP; data changes only its primary/backup HiddenSectors fields.
    /// GPT framing and protective-MBR capacity change to describe both actual
    /// partitions. Private staging is fully reread before publication; failures
    /// remove it. Sources and receipts are never modified or retained by alias.</summary>
    public static Receipt Create(string repositoryRoot, string webImagePath, string dataImagePath, string outputPath)
    {
        string root = Path.GetFullPath(repositoryRoot), output = Path.GetFullPath(outputPath);
        ValidateOutput(root, output);
        using AuthorityFiles inputs = new();
        var webFile = inputs.Open(root, webImagePath, "original web image", "combined_web_input", InputImageBytes, singleLink: true);
        var dataFile = inputs.Open(root, dataImagePath, "original data image", "combined_data_input", InputImageBytes, singleLink: true);
        byte[] web = webFile.ReadBytes(InputImageBytes, "combined_web_input"), data = dataFile.ReadBytes(InputImageBytes, "combined_data_input");
        Need(web.Length == InputImageBytes && data.Length == InputImageBytes, "input_size");
        var originalWeb = WebRootImageFixture.ReadBack(webFile.Path);
        Need(originalWeb.Sha256 == Hash(web) && originalWeb.Files.Any(x => x.Path.StartsWith(WebRootImageFixture.GuestRoot + "/", StringComparison.Ordinal)), "web_input");
        var webLayout = ReadGpt(web, 1); var dataLayout = ReadGpt(data, 1);
        var webPart = webLayout.Parts[0]; var dataPart = dataLayout.Parts[0];
        Need(webPart.EntryIndex == 0 && webPart.PartitionTypeGuid == EfiPartitionType && webPart.FirstLba == WebFirstLba &&
            webPart.LastLba == WebLastLba && webPart.Attributes == 0 && dataPart.EntryIndex == 0 &&
            dataPart.PartitionTypeGuid == DataImage.PartitionType && dataPart.FirstLba == DataImage.PartitionFirstLba &&
            dataPart.LastLba == DataImage.PartitionLastLba && dataPart.Attributes == 0 && webPart.UniquePartitionGuid != dataPart.UniquePartitionGuid, "input_partition");
        var initialData = DataImage.Inspect(data);
        Need(initialData.Find(DataImage.ExclusiveCreateName) is null &&
            new[] { "LOG.TXT", "UPLOAD.BIN", "SQLITE.DB" }.All(name => initialData.Find(name) is { Bytes: 0 }), "data_seed");
        byte[] combined = new byte[ImageBytes]; web.CopyTo(combined, 0);
        Partition(data, DataImage.PartitionFirstLba, DataImage.PartitionLastLba).CopyTo(Partition(combined, DataFirstLba, DataLastLba));
        foreach (uint sector in new[] { DataFirstLba, DataFirstLba + 6 }) W32(combined, checked((int)sector * 512 + 28), DataFirstLba);
        Span<byte> entries = combined.AsSpan(1024, Gpt.ArrayBytes);
        data.AsSpan(1024, Gpt.EntrySize).CopyTo(entries[Gpt.EntrySize..]);
        W64(entries, Gpt.EntrySize + 32, DataFirstLba); W64(entries, Gpt.EntrySize + 40, DataLastLba);
        entries.CopyTo(combined.AsSpan(ImageBytes - 33 * 512, Gpt.ArrayBytes));
        uint crc = Crc32.Compute(entries); W32(combined, 458, Sectors - 1);
        Header(combined.AsSpan(512, 512), 1, Sectors - 1, 2, crc);
        web.AsSpan(InputImageBytes - 512, 512).CopyTo(combined.AsSpan(ImageBytes - 512, 512));
        Header(combined.AsSpan(ImageBytes - 512, 512), Sectors - 1, 1, Sectors - 33, crc);
        Need(Partition(web, WebFirstLba, WebLastLba).SequenceEqual(Partition(combined, WebFirstLba, WebLastLba)), "web_preservation");
        CheckRelocation(data, combined); inputs.VerifyUnchanged();
        string parent = Path.GetDirectoryName(output)!; Directory.CreateDirectory(parent);
        AuthorityFiles.RequirePlainPath(root, parent, "combined_output_path");
        string stage = output + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(combined); stream.Flush(true); }
            Inspection actual = Inspect(stage);
            Need(actual.RawSha256 == Hash(combined) && actual.WebFiles.Count == originalWeb.Files.Length &&
                originalWeb.Files.All(old => actual.WebFiles.Any(now => Same(old, now))), "web_readback");
            Need(initialData.Files.Count == actual.Data.Files.Count && initialData.Files.All(old => actual.Data.Files.Any(now =>
                old.Name == now.Name && old.ShortAlias == now.ShortAlias && old.Sha256 == now.Sha256 && old.Bytes == now.Bytes &&
                old.Attributes == now.Attributes && old.FatDate == now.FatDate && old.FatTime == now.FatTime &&
                old.ShortEntryOffset + DataLbaDelta * 512UL == now.ShortEntryOffset && old.Clusters.SequenceEqual(now.Clusters) &&
                old.Extents.Select(x => x with { FirstLba = x.FirstLba + DataLbaDelta }).SequenceEqual(now.Extents))), "data_readback");
            Receipt receipt = new(Hash(web), Hash(data), actual);
            inputs.VerifyUnchanged(); AuthorityFiles.RequirePlainPath(root, parent, "combined_output_path");
            File.Move(stage, output, overwrite: false); return receipt;
        }
        finally { if (File.Exists(stage)) File.Delete(stage); }
    }

    /// <summary>Hold a plain exact128MiB file through all copied extraction.
    /// Validate both GPT copies, exact disjoint selected partitions, actual FAT
    /// bodies and bytes via the independent existing readers. This does not
    /// authenticate an earlier source image, operation, device or execution.</summary>
    public static Inspection Inspect(string imagePath)
    {
        string path = Path.GetFullPath(imagePath);
        using AuthorityFiles held = new();
        var file = held.Open(Path.GetPathRoot(path)!, path, "combined image", "combined_image", ImageBytes, singleLink: true);
        byte[] raw = file.ReadBytes(ImageBytes, "combined_image"); Need(raw.Length == ImageBytes, "size");
        var layout = ReadGpt(raw, 2); var web = layout.Parts[0]; var data = layout.Parts[1];
        Need(web.EntryIndex == 0 && web.PartitionTypeGuid == EfiPartitionType && web.FirstLba == WebFirstLba && web.LastLba == WebLastLba && web.Attributes == 0 &&
            data.EntryIndex == 1 && data.PartitionTypeGuid == DataImage.PartitionType && data.FirstLba == DataFirstLba && data.LastLba == DataLastLba && data.Attributes == 0, "partitions");
        var webFiles = WebRootImageFixture.ReadBack(path);
        Need(webFiles.Sha256 == Hash(raw) && webFiles.PartitionFirstLba == web.FirstLba && webFiles.PartitionLastLba == web.LastLba, "web_identity");
        var dataFiles = DataImage.InspectPartition(raw, data.FirstLba, data.LastLba);
        Inspection result = new(raw, layout.Disk, web, data, webFiles, dataFiles); held.VerifyUnchanged(); return result;
    }

    /// <summary>Reinspect output and compare the complete canonical copied
    /// receipt. It adds no authority over the original source leases.</summary>
    public static Inspection Verify(string imagePath, Receipt expected)
    {
        ArgumentNullException.ThrowIfNull(expected); Inspection actual = Inspect(imagePath);
        Need(Serialize(expected.OriginalWebSha256, expected.OriginalDataSha256, actual).AsSpan().SequenceEqual(expected.CopyJson()), "receipt_identity"); return actual;
    }

    private static (Guid Disk, GptPartitionDescriptor[] Parts) ReadGpt(byte[] raw, int count)
    {
        ulong capacity = (ulong)raw.Length / 512;
        Need(raw.Length is InputImageBytes or ImageBytes && raw[510] == 0x55 && raw[511] == 0xaa && raw[450] == 0xee &&
            U32(raw, 454) == 1 && U32(raw, 458) == capacity - 1 && raw.AsSpan(462, 48).IndexOfAnyExcept((byte)0) < 0, "mbr");
        var a = Gpt.ValidateHeader(raw.AsSpan(512, 512), capacity, Complete);
        var b = Gpt.ValidateHeader(raw.AsSpan(raw.Length - 512, 512), capacity, Complete, backup: true);
        Need(a.Succeeded && b.Succeeded && a.HeaderSize == 92 && b.HeaderSize == 92 && a.FirstUsableLba == 34 && b.FirstUsableLba == 34 &&
            a.LastUsableLba == capacity - 34 && b.LastUsableLba == capacity - 34 && a.EntryLba == 2 && b.EntryLba == capacity - 33 &&
            a.DiskGuidLow == b.DiskGuidLow && a.DiskGuidHigh == b.DiskGuidHigh, "gpt_header");
        var entries = raw.AsSpan(1024, Gpt.ArrayBytes); var backup = raw.AsSpan(raw.Length - 33 * 512, Gpt.ArrayBytes);
        Need(entries.SequenceEqual(backup) && Gpt.ValidateArray(a, entries, out _) == LiveGptStatus.Success &&
            Gpt.ValidateArray(b, backup, out _) == LiveGptStatus.Success, "gpt_array");
        Span<GptPartitionDescriptor> parts = stackalloc GptPartitionDescriptor[Gpt.EntryCount];
        var parsed = GptPartitionTableParser.ParsePrimary(raw.AsSpan(512, 512), entries, capacity, 512, parts);
        Need(parsed.Succeeded && parsed.PartitionsWritten == count, "gpt_selection");
        return (parsed.Header.DiskGuid, parts[..count].ToArray());
    }
    private static void Header(Span<byte> header, uint current, uint alternate, uint entries, uint crc)
    {
        W64(header, 24, current); W64(header, 32, alternate); W64(header, 48, Sectors - 34); W64(header, 72, entries);
        W32(header, 88, crc); W32(header, 16, 0); W32(header, 16, Crc32.Compute(header[..92]));
    }
    private static void CheckRelocation(byte[] original, byte[] combined)
    {
        var old = Partition(original, DataImage.PartitionFirstLba, DataImage.PartitionLastLba); var now = Partition(combined, DataFirstLba, DataLastLba);
        Need(old[..28].SequenceEqual(now[..28]) && old.Slice(32, 6 * 512 - 4).SequenceEqual(now.Slice(32, 6 * 512 - 4)) &&
            old[(6 * 512 + 32)..].SequenceEqual(now[(6 * 512 + 32)..]) && U32(now, 28) == DataFirstLba && U32(now, 6 * 512 + 28) == DataFirstLba, "data_preservation");
    }
    private static bool Same(WebRootImageFixture.DiskFile old, WebFile now) => old.Path == now.Path && old.Bytes == now.Bytes && old.Sha256 == now.Sha256 &&
        old.Attributes == now.Attributes && old.FatDate == now.FatDate && old.FatTime == now.FatTime && old.Extents.SequenceEqual(now.Extents);
    private static Span<byte> Partition(byte[] raw, ulong first, ulong last) => raw.AsSpan(checked((int)first * 512), checked((int)(last - first + 1) * 512));
    private static byte[] Serialize(string webHash, string dataHash, Inspection image)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject(); json.WriteString("schema", "CLROS_COMBINED_WEB_DATA_IMAGE_V1"); json.WriteString("originalWebSha256", webHash);
            json.WriteString("originalDataSha256", dataHash); json.WriteString("rawSha256", image.RawSha256); json.WriteNumber("sectors", image.CapacitySectors);
            json.WriteString("diskGuid", image.DiskGuid); json.WriteString("webGuid", image.WebPartitionGuid); json.WriteString("dataGuid", image.DataPartitionGuid);
            json.WriteNumber("webFirst", image.WebFirst); json.WriteNumber("webLast", image.WebLast); json.WriteNumber("dataFirst", image.DataFirst); json.WriteNumber("dataLast", image.DataLast);
            json.WriteString("webPartitionSha256", image.WebPartitionSha256); json.WriteString("dataPartitionSha256", image.DataPartitionSha256);
            json.WriteStartArray("webFiles");
            foreach (var file in image.WebFiles)
            {
                json.WriteStartObject(); json.WriteString("path", file.Path); json.WriteNumber("bytes", file.Bytes); json.WriteString("sha256", file.Sha256);
                json.WriteNumber("attributes", file.Attributes); json.WriteNumber("date", file.FatDate); json.WriteNumber("time", file.FatTime);
                json.WriteStartArray("extents"); foreach (var extent in file.Extents)
                { json.WriteStartArray(); json.WriteNumberValue(extent.FirstCluster); json.WriteNumberValue(extent.Clusters); json.WriteNumberValue(extent.FirstLba); json.WriteEndArray(); }
                json.WriteEndArray(); json.WriteEndObject();
            }
            json.WriteEndArray(); json.WriteNumber("freeClusters", image.Data.FreeClusters); json.WriteNumber("fsInfoHint", image.Data.FsInfoHint);
            json.WriteStartArray("rootClusters"); foreach (uint c in image.Data.RootClusters) json.WriteNumberValue(c); json.WriteEndArray();
            json.WriteStartArray("dataFiles");
            foreach (var file in image.Data.Files)
            {
                json.WriteStartObject(); json.WriteString("name", file.Name); json.WriteString("alias", file.ShortAlias); json.WriteNumber("bytes", file.Bytes);
                json.WriteString("sha256", file.Sha256); json.WriteNumber("entryOffset", file.ShortEntryOffset); json.WriteNumber("inode", file.Inode);
                json.WriteNumber("attributes", file.Attributes); json.WriteNumber("date", file.FatDate); json.WriteNumber("time", file.FatTime);
                json.WriteStartArray("clusters"); foreach (uint c in file.Clusters) json.WriteNumberValue(c); json.WriteEndArray();
                json.WriteStartArray("extents"); foreach (var extent in file.Extents)
                { json.WriteStartArray(); json.WriteNumberValue(extent.FirstCluster); json.WriteNumberValue(extent.Clusters); json.WriteNumberValue(extent.FirstLba); json.WriteEndArray(); }
                json.WriteEndArray(); json.WriteEndObject();
            }
            json.WriteEndArray(); json.WriteEndObject();
        }
        return stream.ToArray();
    }
    private static void ValidateOutput(string root, string output)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Need(output.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, comparison) && !File.Exists(output) && !Directory.Exists(output), "output_path");
        string? ancestor = Path.GetDirectoryName(output); while (ancestor is not null && !Directory.Exists(ancestor)) ancestor = Path.GetDirectoryName(ancestor);
        Need(ancestor is not null, "output_path"); AuthorityFiles.RequirePlainPath(root, ancestor!, "combined_output_path");
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static void W32(Span<byte> bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset, 4), value);
    private static void W64(Span<byte> bytes, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(offset, 8), value);
    private static void Need(bool valid, string reason) { if (!valid) throw new ToolFailure("combined_image_" + reason); }
}
