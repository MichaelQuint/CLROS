using System.Buffers.Binary;
using OS.Std.Hash;
using OS.Storage;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM06PackageReadRecords(IReadOnlyList<string> lines, string variant = "Intact")
    {
        var expected = QemuContracts.M06PackageReadMarkers(variant);
        Need(lines.Count == expected.Count, "m06_package_record_count");
        int start = QemuContracts.M06FatReadMarkers().Count;
        for (int i = start; i < lines.Count; i++)
            Need(expected[i].StartsWith("M06_PKG ", StringComparison.Ordinal)
                ? lines[i].StartsWith(expected[i] + " ", StringComparison.Ordinal) : lines[i] == expected[i], "m06_package_marker_order");
        ulong[] Row(int stage) => PackageRow(lines, stage);
        ulong[] volume = Row(0), root = Row(1), files = Row(2), parentRoot = FatRow(lines, 2);
        Need(volume.SequenceEqual(FatRow(lines, 0)), "m06_package_partition");
        ulong[] geometry = FatRow(lines, 1);
        bool Cluster(ulong c) => c >= 2 && c - 2 < geometry[1];
        Need(root[0] == 2 && root[1] == parentRoot[1] && root[2] == parentRoot[2] && Cluster(root[3]) &&
            root.Skip(1).Distinct().Count() == 3, "m06_package_root_directory");
        ulong[] reserved = [root[1], root[2], root[3], .. FatRow(lines, 7).Take(3)];
        Need(files[0] == root[3] && Cluster(files[1]) && Cluster(files[2]) && files[1] != files[2] &&
            files.Skip(1).Take(2).All(c => !reserved.Contains(c)) && files[3] == 10, "m06_package_file_directory");
        ulong fatSectors = (volume[3] - volume[2]) / 2;
        ulong Fat(ulong c) => volume[2] + c / 128;
        ulong Data(ulong c) => volume[3] + c - 2;
        ulong[] lbas = [volume[0], Data(root[1]), Fat(root[1]), Fat(root[1]) + fatSectors,
            Data(root[2]), Fat(root[2]), Fat(root[2]) + fatSectors,
            Fat(root[3]), Fat(root[3]) + fatSectors, Data(root[3]),
            Data(files[1]), Fat(files[1]), Fat(files[1]) + fatSectors,
            Data(files[2]), Fat(files[2]), Fat(files[2]) + fatSectors];
        int reads = variant == "ManifestVersion" ? 13 : 16;
        for (int i = 0; i < reads; i++)
        {
            ulong[] read = Row(0x80 + i);
            Need(read[0] == lbas[i] && read[0] >= volume[0] && read[0] <= volume[1] &&
                read[1] <= uint.MaxValue && read[2] == (ulong)i + 134 && read[3] == 1, "m06_package_read_lifetime");
        }
        foreach (int first in new[] { 2, 5, 7, 11, 14 }.Where(i => i + 1 < reads))
            Need(Row(0x80 + first)[1] == Row(0x81 + first)[1], "m06_package_mirror_receipt");
        if (variant != "ManifestVersion")
            Need(Row(3).SequenceEqual(new ulong[] { M06DiskPackageFixture.ManifestBytes, M06DiskPackageFixture.AppPathOffset,
                M06DiskPackageFixture.AppPathLength, M06DiskPackageFixture.ManifestCrc32 }), "m06_package_manifest");
        if (variant == "Intact")
        {
            Need(Row(4).SequenceEqual(new ulong[] { M06DiskPackageFixture.PackageBytes, 96,
                M06DiskPackageFixture.PayloadBytes, M06DiskPackageFixture.PackageCrc32 }), "m06_package_selection");
            ulong[] staged = Row(5), dma = PrefixRow(lines, "M06_BLK 5", 4);
            Need(staged.SequenceEqual(new ulong[] { dma[0], M06DiskPackageFixture.PayloadBytes,
                M06DiskPackageFixture.EntryOffset, M06DiskPackageFixture.PayloadCrc32 }), "m06_package_owned_payload");
            Need(Row(6).SequenceEqual(PrefixRow(lines, "M06_BLK B", 4)), "m06_package_allocator_restore");
            Need(Row(7).SequenceEqual(new ulong[] { 16, 149, 17, 1 }), "m06_package_final_lifetime");
        }
        else
        {
            (ulong kind, ulong code, ulong phase) = variant switch
            {
                "ManifestVersion" => ((ulong)LiveStorageFailureKind.Manifest, (ulong)LiveManifestStatus.InvalidValue, 15UL),
                "PackageChecksum" => ((ulong)LiveStorageFailureKind.Package, (ulong)LivePackageStatus.ChecksumMismatch, 16UL),
                "PackageHeader" => ((ulong)LiveStorageFailureKind.Package, (ulong)LivePackageStatus.InvalidHeader, 16UL),
                "PackageTruncated" => ((ulong)LiveStorageFailureKind.Disk, (ulong)LiveFat32Status.Truncated, 16UL),
                "PackageLoop" => ((ulong)LiveStorageFailureKind.Disk, (ulong)LiveFat32Status.Loop, 16UL),
                _ => throw new ToolFailure("package_image_variant_invalid")
            };
            Need(Row(8).SequenceEqual(new ulong[] { kind, code, phase, (ulong)reads }), "m06_package_exact_fault");
        }
        Need(Row(9).SequenceEqual(new ulong[] { 1, 0, 1, 1 }), "m06_package_fault_cleanup");
    }

    public static void AssertM06PackageReadImage(string serialPath, string imagePath, string variant = "Intact")
    {
        string[] lines = FirmwareProtocol(serialPath, "TEST_PASS m06.018\r\n", true, false, "m06_package", true);
        AssertM06PackageReadImage(lines, imagePath, variant);
    }

    internal static void AssertM06PackageReadImage(IReadOnlyList<string> lines, string imagePath, string variant)
    {
        AssertM06PackageReadRecords(lines, variant);
        ulong[] Row(int stage) => PackageRow(lines, stage);
        using var disk = File.OpenRead(imagePath);
        Need(disk.Length >= 68 * 512 && disk.Length % 512 == 0, "m06_package_disk_size");
        ulong capacity = (ulong)disk.Length / 512;
        byte[] Read(ulong lba)
        {
            Need(lba < capacity, "m06_package_disk_range");
            byte[] bytes = new byte[512]; disk.Position = checked((long)lba * 512); disk.ReadExactly(bytes); return bytes;
        }
        ulong[] volume = Row(0), root = Row(1), files = Row(2);
        int reads = variant == "ManifestVersion" ? 13 : 16;
        for (int i = 0; i < reads; i++)
        {
            ulong[] receipt = Row(0x80 + i);
            Need(Crc32.Compute(Read(receipt[0])) == receipt[1], "m06_package_disk_sector_receipt");
        }
        var device = new FatTranscriptDevice(disk, capacity);
        var os = Fat32Parser.FindRootDirectory(device, volume[0], "OS");
        var manifest = Fat32Parser.FindFirstLevelDirectoryEntry(device, volume[0], "OS/NATIVE.MAN");
        var package = Fat32Parser.FindFirstLevelDirectoryEntry(device, volume[0], "OS/APP.PKG");
        Need(os.Succeeded && os.Entry.FirstCluster == root[3] && manifest.Succeeded && package.Succeeded &&
            manifest.Entry.FirstCluster == files[1] && manifest.Entry.FileSize == M06DiskPackageFixture.ManifestBytes &&
            package.Entry.FirstCluster == files[2] && package.Entry.FileSize == (variant == "PackageTruncated" ? 700 : M06DiskPackageFixture.PackageBytes),
            "m06_package_disk_directory");
        uint Next(ulong cluster)
        {
            byte[] primary = Read(volume[2] + cluster / 128), mirror = Read(volume[2] + cluster / 128 + (volume[3] - volume[2]) / 2);
            Need(primary.AsSpan().SequenceEqual(mirror), "m06_package_disk_mirror");
            return BinaryPrimitives.ReadUInt32LittleEndian(primary.AsSpan((int)(cluster % 128) * 4)) & 0x0fffffff;
        }
        Need(Next(root[3]) >= 0x0ffffff8 && Next(files[1]) >= 0x0ffffff8 &&
            (variant == "PackageLoop" ? Next(files[2]) == files[2] : Next(files[2]) >= 0x0ffffff8), "m06_package_disk_chains");
        byte[] manifestBytes = Read(volume[3] + files[1] - 2)[..(int)M06DiskPackageFixture.ManifestBytes];
        var parsedManifest = NativeManifestDisk.Validate(manifestBytes);
        Need(parsedManifest.Status == (variant == "ManifestVersion" ? LiveManifestStatus.InvalidValue : LiveManifestStatus.Success),
            "m06_package_disk_manifest_status");
        if (variant != "ManifestVersion")
        {
            Need(Crc32.Compute(manifestBytes) == Row(3)[3] && parsedManifest.AppPathOffset == Row(3)[1] &&
                parsedManifest.AppPathLength == Row(3)[2], "m06_package_disk_manifest_bytes");
            byte[] packageBytes = Read(volume[3] + files[2] - 2)[..(int)M06DiskPackageFixture.PackageBytes];
            var selected = APPPKGDisk.Validate(packageBytes,
                manifestBytes.AsSpan((int)parsedManifest.AppPathOffset, (int)parsedManifest.AppPathLength), M06DiskPackageFixture.PackageCrc32);
            LivePackageStatus status = variant == "PackageHeader" ? LivePackageStatus.InvalidHeader :
                variant == "PackageChecksum" ? LivePackageStatus.ChecksumMismatch : LivePackageStatus.Success;
            Need(selected.Status == status, "m06_package_disk_package_status");
            if (variant == "Intact")
            {
                Need(selected.ContentOffset == Row(4)[1] && selected.ContentLength == Row(4)[2] && selected.PackageCrc32 == Row(4)[3],
                    "m06_package_disk_package_bytes");
                byte[] payload = new byte[4096];
                var staged = SelectedPayloadOwnedMemory.Stage(packageBytes,
                    manifestBytes.AsSpan((int)parsedManifest.AppPathOffset, (int)parsedManifest.AppPathLength), M06DiskPackageFixture.PackageCrc32, payload);
                Need(staged.Succeeded && staged.BytesWritten == Row(5)[1] && staged.EntryOffset == Row(5)[2] &&
                    staged.PayloadCrc32 == Row(5)[3] && payload.AsSpan(0, (int)staged.BytesWritten).SequenceEqual(
                        packageBytes.AsSpan((int)selected.ContentOffset, (int)selected.ContentLength)), "m06_package_disk_payload_bytes");
            }
        }
    }

    private static ulong[] PackageRow(IReadOnlyList<string> lines, int stage) =>
        PrefixRow(lines, "M06_PKG " + stage.ToString("X2"), 4);

    private static ulong[] PrefixRow(IReadOnlyList<string> lines, string prefix, int count)
    {
        string[] matches = lines.Where(s => s.StartsWith(prefix + " ", StringComparison.Ordinal)).ToArray();
        Need(matches.Length == 1, "m06_package_unique_row");
        return ReadHexRecord(matches[0], prefix, count);
    }
}
