using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Std.Hash;
using OS.Tooling;
using Image = OS.Tooling.WritableDataImageFixture;

// Host image/extraction controls only. No native/device/guest persistence claim.
internal static class WritableDataImageFixtureTests
{
    [HostTest("writable data image two fresh outputs and canonical extraction receipts are byte identical", HostLane.Native)]
    private static void Deterministic() => WithImage((root, folder, path, receipt, disk) =>
    {
        string second = Path.Combine(folder, "second.raw");
        var repeated = Image.Create(root, second);
        HostCheck.True(receipt.Image.RawSha256 == repeated.Image.RawSha256 &&
            File.ReadAllBytes(second).AsSpan().SequenceEqual(disk) && receipt.CopyJson().AsSpan().SequenceEqual(repeated.CopyJson()));
        HostCheck.True(disk.Length == 67108864 && receipt.Image.Files.Count == 6 &&
            receipt.Image.Find(Image.ExclusiveCreateName) is null && receipt.Image.RootClusters.SequenceEqual(new uint[] { 2, 19, 37 }));
        using var json = JsonDocument.Parse(receipt.CopyJson());
        HostCheck.True(json.RootElement.GetProperty("schema").GetString() == "CLROS_WRITABLE_DATA_IMAGE_V1" &&
            json.RootElement.GetProperty("mount").GetString() == "/data" && !Encoding.UTF8.GetString(receipt.CopyJson()).Contains(folder, StringComparison.Ordinal));
        foreach (string empty in new[] { "LOG.TXT", "UPLOAD.BIN", "SQLITE.DB", Image.LongFileName })
            HostCheck.True(receipt.Image.Find(empty) is { Bytes: 0 } item && item.Clusters.Count == 0 && item.Sha256 == Hash([]));
        HostCheck.True(receipt.Image.Find("GUARD.TXT")!.Attributes == 0x21 && receipt.Image.Find("FRAG.BIN")!.Attributes == 0x20);
        foreach (var file in receipt.Image.Files)
        {
            HostCheck.True(file.FatDate == 33 && file.FatTime == 0 && file.ModifiedSeconds == 315532800 &&
                file.Inode == file.ShortEntryOffset / 32 + 256 && Hash(Extract(disk, file)) == file.Sha256);
        }
        Image.Verify(path, receipt);
        Console.WriteLine($"WRITABLE_DATA_IMAGE sha256={receipt.Image.RawSha256} receiptSha256={Hash(receipt.CopyJson())} bytes={disk.Length} files={receipt.Image.Files.Count} allocatedClusters=8 freeClusters={receipt.Image.FreeClusters}");
    });

    [HostTest("writable data image independently extracts fragmented sectors max LFN and both FAT free candidates", HostLane.Native)]
    private static void IndependentGeometry() => WithImage((_, _, _, receipt, disk) =>
    {
        HostCheck.True(U32(disk, 512 + 80) == 128 && U32(disk, 512 + 84) == 128 &&
            U64(disk, 512 + 40) == 34 && U64(disk, 512 + 48) == 131038 &&
            new Guid(disk.AsSpan(1024, 16)) == new Guid("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7"));
        int boot = 2048 * 512;
        HostCheck.True(U16(disk, boot + 11) == 512 && disk[boot + 13] == 1 && U16(disk, boot + 14) == 32 &&
            disk[boot + 16] == 2 && U32(disk, boot + 36) == 993 && U32(disk, boot + 44) == 2 &&
            U16(disk, boot + 48) == 1 && U16(disk, boot + 50) == 6);
        int fat = 2080 * 512, fatBytes = 993 * 512;
        HostCheck.True(disk.AsSpan(fat, fatBytes).SequenceEqual(disk.AsSpan(fat + fatBytes, fatBytes)) &&
            disk.AsSpan(boot, 512).SequenceEqual(disk.AsSpan(boot + 6 * 512, 512)) &&
            disk.AsSpan(boot + 512, 512).SequenceEqual(disk.AsSpan(boot + 7 * 512, 512)));
        var fragment = receipt.Image.Find("FRAG.BIN")!;
        byte[] bytes = Extract(disk, fragment);
        HostCheck.True(bytes.Length == 1537 && fragment.Clusters.SequenceEqual(new uint[] { 5, 41, 8, 73 }) && fragment.Extents.Count == 4);
        for (int i = 0; i < bytes.Length; i++) HostCheck.Equal(unchecked((byte)((i * 73 + 19) ^ (i / 512))), bytes[i]);
        var longFile = receipt.Image.Find(Image.LongFileName)!;
        HostCheck.True(longFile.Name.Length == 255 && longFile.ShortAlias == "LONGNA~1.TXT" && longFile.ShortEntryOffset == (ulong)ClusterOffset(37) + 11 * 32);
        HostCheck.True(disk[ClusterOffset(19) + 7 * 32] == 0x54 && disk[ClusterOffset(37) + 10 * 32] == 1);
        var owned = receipt.Image.Files.SelectMany(x => x.Clusters).Concat(receipt.Image.RootClusters).ToHashSet();
        HostCheck.True(owned.Count == 8 && receipt.Image.FreeClusters == 126973 - 8 && receipt.Image.FsInfoHint == 3 && receipt.Image.FreeCandidates.Count == 16);
        foreach (uint c in receipt.Image.FreeCandidates)
            HostCheck.True(!owned.Contains(c) && U32(disk, fat + (int)c * 4) == 0 && U32(disk, fat + fatBytes + (int)c * 4) == 0);
    });

    [HostTest("writable data image rejects corrupt and CRC valid divergent GPT identity and partition contracts", HostLane.Native)]
    private static void GptRejection() => WithImage((_, _, _, _, disk) =>
    {
        Mutant(disk, x => x[512 + 16] ^= 1, "gpt");
        Mutant(disk, x => { W64(x, x.Length - 512 + 40, 35); RecrcHeader(x, x.Length - 512); }, "gpt");
        Mutant(disk, x => { x[x.Length - 512 + 56] ^= 1; RecrcHeader(x, x.Length - 512); }, "gpt");
        Mutant(disk, x =>
        {
            new Guid("C12A7328-F81F-11D2-BA4B-00A0C93EC93B").TryWriteBytes(x.AsSpan(1024, 16));
            x.AsSpan(1024, 16384).CopyTo(x.AsSpan(x.Length - 33 * 512, 16384)); RecrcArrays(x);
        }, "partition");
        Mutant(disk, x => { x[446] = 0x80; x[450] = 0; }, "mbr");
        Mutant(disk, x => { W32(x, 1024 + 128, 1); RecrcArrays(x); }, "gpt");
    });

    [HostTest("writable data image rejects divergent FAT cycles crosslinks overlong free and unclaimed clusters", HostLane.Native)]
    private static void AllocationRejection() => WithImage((_, _, _, _, disk) =>
    {
        Mutant(disk, x => x[(2080 + 993) * 512 + 5 * 4] ^= 1, "fat_mirror");
        Mutant(disk, x => Fat(x, 37, 2), "chain_loop_or_crosslink");
        Mutant(disk, x => Fat(x, 73, 5), "chain_loop_or_crosslink");
        Mutant(disk, x => Fat(x, 41, 0), "chain");
        Mutant(disk, x => { Fat(x, 73, 91); Fat(x, 91, 0x0fffffff); }, "file_length");
        Mutant(disk, x => W16(x, ClusterOffset(2) + 4 * 32 + 26, 5), "chain_loop_or_crosslink");
        Mutant(disk, x => Fat(x, 91, 0x0fffffff), "unclaimed_cluster");
        Mutant(disk, x => Fat(x, 0, 0), "fat_reserved");
    });

    [HostTest("writable data image checks FSInfo signatures backups counts hints and backup boot", HostLane.Native)]
    private static void ReservedRejection() => WithImage((_, _, _, _, disk) =>
    {
        Mutant(disk, x => x[(2048 + 6) * 512 + 71] ^= 1, "boot_backup");
        Mutant(disk, x => x[(2048 + 7) * 512 + 488] ^= 1, "fsinfo_backup");
        Mutant(disk, x => Info(x, 0, 0), "fsinfo");
        Mutant(disk, x => Info(x, 488, 1), "free_count");
        Mutant(disk, x => Info(x, 492, 2), "free_hint");
        Mutant(disk, x => Info(x, 492, uint.MaxValue - 1), "free_hint");
        byte[] advisory = (byte[])disk.Clone(); Info(advisory, 488, uint.MaxValue); Info(advisory, 492, uint.MaxValue);
        HostCheck.True(Image.Inspect(advisory).FreeClusters == 126965);
    });

    [HostTest("writable data image rejects namespace LFN interruption malformed timestamp and hidden root kinds", HostLane.Native)]
    private static void NamespaceRejection() => WithImage((_, _, _, _, disk) =>
    {
        Mutant(disk, x => x[ClusterOffset(19) + 7 * 32 + 13] ^= 1, "directory");
        Mutant(disk, x => x[ClusterOffset(19) + 8 * 32] = 0xe5, "directory");
        Mutant(disk, x => x.AsSpan(ClusterOffset(2), 11).CopyTo(x.AsSpan(ClusterOffset(2) + 32, 11)), "name_collision");
        Mutant(disk, x => W16(x, ClusterOffset(2) + 24, 31), "timestamp");
        Mutant(disk, x => x[ClusterOffset(2) + 11] = 0x08, "root_nonfile");
        Mutant(disk, x => x[ClusterOffset(2) + 11] = 0x10, "directory");
        Mutant(disk, x => x[ClusterOffset(2) + 12] = 8, "directory");
    });

    [HostTest("writable data image independent postmutation extraction updates candidate claims without trusting seed plan", HostLane.Native)]
    private static void ChangedImage() => WithImage((_, folder, _, receipt, disk) =>
    {
        // Independently authored short entry in a scrubbed hole, not the writer.
        int entry = ClusterOffset(2) + 5 * 32;
        "NEW     BIN"u8.CopyTo(disk.AsSpan(entry, 11)); disk[entry + 11] = 0x20;
        W16(disk, entry + 24, 33); W16(disk, entry + 26, 100); W32(disk, entry + 28, 10);
        "independent"u8[..10].CopyTo(disk.AsSpan(ClusterOffset(100), 10));
        Fat(disk, 100, 0x0fffffff); Info(disk, 488, receipt.Image.FreeClusters - 1);
        var changed = Image.Inspect(disk);
        HostCheck.True(changed.Files.Count == 7 && changed.Find("new.bin") is { Bytes: 10 } file &&
            file.Clusters.SequenceEqual(new uint[] { 100 }) && file.CopyBytes().AsSpan().SequenceEqual("independen"u8) &&
            !changed.FreeCandidates.Contains(100) && changed.FreeCandidates.Contains(132));
        string altered = Path.Combine(folder, "altered.raw"); File.WriteAllBytes(altered, disk);
        Reject(() => Image.Verify(altered, receipt), "receipt_identity");
    });

    [HostTest("writable data image borrowed input payload and canonical receipt outputs are never retained mutable aliases", HostLane.Native)]
    private static void CopiedValues() => WithImage((_, _, _, receipt, disk) =>
    {
        var inspected = Image.Inspect(disk); var file = inspected.Find("FRAG.BIN")!;
        byte original = file.CopyBytes()[0]; string originalHash = inspected.RawSha256;
        byte[] payload = file.CopyBytes(); payload[0] ^= 1;
        byte[] json = receipt.CopyJson(); json.AsSpan().Clear();
        disk.AsSpan().Clear();
        HostCheck.True(file.CopyBytes()[0] == original && inspected.RawSha256 == originalHash && receipt.CopyJson()[0] == '{');
        bool readonlyList = false;
        try { ((IList<uint>)inspected.RootClusters)[0] = 100; } catch (NotSupportedException) { readonlyList = true; }
        HostCheck.True(readonlyList && inspected.RootClusters[0] == 2);
    });

    [HostTest("writable data image fresh artifacts publication rejects existing and escaping outputs without leftovers", HostLane.Native)]
    private static void OutputSafety() => WithImage((root, folder, path, receipt, _) =>
    {
        string originalHash = receipt.Image.RawSha256;
        Reject(() => Image.Create(root, path), "output_path");
        Reject(() => Image.Create(root, folder), "output_path");
        string outside = Path.Combine(root, "writable-data-should-not-exist.raw");
        HostCheck.True(!File.Exists(outside)); Reject(() => Image.Create(root, outside), "output_path");
        Reject(() => Image.Create(root, Path.Combine(folder, "..", "..", "..", "writable-data-should-not-exist.raw")), "output_path");
        HostCheck.True(Image.Inspect(path).RawSha256 == originalHash && !File.Exists(outside) &&
            !Directory.EnumerateFiles(folder, "*.tmp-*", SearchOption.AllDirectories).Any());
    });

    [HostTest("writable data image refuses a reparse output ancestor before creating a temporary image", HostLane.Native)]
    private static void ReparseOutput() => WithImage((root, folder, _, _, _) =>
    {
        string target = Path.Combine(folder, "target"), link = Path.Combine(folder, "link");
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(link, target);
        try
        {
            Reject(() => Image.Create(root, Path.Combine(link, "forbidden.raw")), "output_path");
            HostCheck.True(!Directory.EnumerateFileSystemEntries(target).Any());
        }
        finally { Directory.Delete(link); }
    });

    [HostTest("writable data image rejects truncated raw images and binds actual bytes to prior extraction receipt", HostLane.Native)]
    private static void RawIdentity() => WithImage((_, folder, _, receipt, disk) =>
    {
        Reject(() => Image.Inspect(disk.AsSpan(0, disk.Length - 1)), "size");
        disk[ClusterOffset(5) + 1] ^= 1;
        var altered = Image.Inspect(disk);
        HostCheck.True(altered.Find("FRAG.BIN")!.Sha256 != receipt.Image.Find("FRAG.BIN")!.Sha256 && altered.RawSha256 != receipt.Image.RawSha256);
        string path = Path.Combine(folder, "substituted.raw"); File.WriteAllBytes(path, disk);
        Reject(() => Image.Verify(path, receipt), "receipt_identity");
    });

    private static void WithImage(Action<string, string, string, Image.Receipt, byte[]> action)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string folder = Path.Combine(root, "artifacts", "writable-data-image-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { string path = Path.Combine(folder, "initial.raw"); var receipt = Image.Create(root, path); action(root, folder, path, receipt, File.ReadAllBytes(path)); }
        finally { Directory.Delete(folder, recursive: true); }
    }
    private static void Mutant(byte[] disk, Action<byte[]> mutate, string reason)
    { byte[] copy = (byte[])disk.Clone(); mutate(copy); Reject(() => Image.Inspect(copy), reason); }
    private static void Reject(Action action, string suffix)
    {
        try { action(); throw new InvalidOperationException("expected writable_image_" + suffix); }
        catch (ToolFailure error) { HostCheck.Equal("writable_image_" + suffix, error.Reason); }
    }
    private static byte[] Extract(byte[] disk, Image.Entry file)
    {
        byte[] bytes = new byte[file.Bytes]; int position = 0;
        foreach (var extent in file.Extents)
        {
            int take = Math.Min(checked((int)extent.Clusters * 512), bytes.Length - position);
            disk.AsSpan(checked((int)extent.FirstLba * 512), take).CopyTo(bytes.AsSpan(position)); position += take;
        }
        HostCheck.Equal(bytes.Length, position); return bytes;
    }
    private static void Fat(byte[] disk, uint cluster, uint value)
    { W32(disk, 2080 * 512 + (int)cluster * 4, value); W32(disk, (2080 + 993) * 512 + (int)cluster * 4, value); }
    private static void Info(byte[] disk, int offset, uint value)
    { W32(disk, (2048 + 1) * 512 + offset, value); W32(disk, (2048 + 7) * 512 + offset, value); }
    private static void RecrcHeader(byte[] disk, int header)
    { W32(disk, header + 16, 0); W32(disk, header + 16, Crc32.Compute(disk.AsSpan(header, 92))); }
    private static void RecrcArrays(byte[] disk)
    {
        W32(disk, 512 + 88, Crc32.Compute(disk.AsSpan(1024, 16384))); RecrcHeader(disk, 512);
        int backup = disk.Length - 512;
        W32(disk, backup + 88, Crc32.Compute(disk.AsSpan(disk.Length - 33 * 512, 16384))); RecrcHeader(disk, backup);
    }
    private static int ClusterOffset(uint c) => checked((int)(4066 + c - 2) * 512);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ulong U64(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8));
    private static void W16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
    private static void W32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static void W64(byte[] bytes, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, 8), value);
}
