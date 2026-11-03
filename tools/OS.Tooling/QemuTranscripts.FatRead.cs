using System.Buffers.Binary;
using System.Text;
using OS.Std.Hash;
using OS.Storage;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM06FatReadRecords(IReadOnlyList<string> lines)
    {
        var expected = QemuContracts.M06FatReadMarkers();
        Need(lines.Count == expected.Count, "m06_fat_record_count");
        for (int i = QemuContracts.M06GptPartitionMarkers().Count; i < lines.Count; i++)
            Need(expected[i].StartsWith("M06_FAT ", StringComparison.Ordinal) ? lines[i].StartsWith(expected[i] + " ", StringComparison.Ordinal) : lines[i] == expected[i], "m06_fat_marker_order");
        ulong[] Row(int stage) => FatRow(lines, stage);
        ulong[] gpt = GptRow(lines, 5), volume = Row(0), geometry = Row(1), root = Row(2), entry = Row(4), file = Row(7);
        Need(volume[0] == gpt[1] && volume[1] == gpt[2] && volume[0] <= volume[1] && volume[2] == volume[0] + 32 && volume[3] > volume[2] && (volume[3] - volume[2]) % 2 == 0 && volume[3] <= volume[1], "m06_fat_partition_geometry");
        ulong fatSectors = (volume[3] - volume[2]) / 2;
        Need(geometry[0] == 1 && geometry[1] >= 65525 && geometry[1] <= 0x0fffffee && geometry[2] == 2 && geometry[3] == volume[1] - volume[0] + 1 && geometry[1] == volume[1] - volume[3] + 1 && geometry[1] + 2 <= fatSectors * 128, "m06_fat_bpb");
        bool Cluster(ulong value) => value >= 2 && value - 2 < geometry[1];
        Need(root[0] == 2 && root[1] == 2 && root[2] != 2 && Cluster(root[2]) && root[3] == 5, "m06_fat_root_chain");
        Need(Row(3).SequenceEqual(new ulong[] { 2, 20, 1, 1 }), "m06_fat_directory_scan");
        Need(Cluster(entry[0]) && entry[1] == FatImageFixture.FileBytes && entry[2] == (ulong)FatImageFixture.LongName.Length && entry[3] == Crc32.Compute(Encoding.Unicode.GetBytes(FatImageFixture.LongName)), "m06_fat_long_name");
        Need(Row(5).SequenceEqual(new ulong[] { 0x317e45524946534cUL, 0x4e4942, 0x20, 7 }), "m06_fat_short_entry");
        Need(Row(6).SequenceEqual(new ulong[] { FatImageFixture.FileBytes, FatImageFixture.FileCrc32, FatImageFixture.FileCrc32, 3 }), "m06_fat_checksum");
        Need(file[0] == entry[0] && file.Take(3).All(Cluster) && file.Take(3).Distinct().Count() == 3 && file.Take(3).All(c => c != root[1] && c != root[2]) && file[1] != file[0] + 1 && file[3] == 16, "m06_fat_fragmented_file_chain");
        Need(Row(8).SequenceEqual(new ulong[] { 1, 3, 15, 85 }), "m06_fat_final_cleanup");
        ulong Fat(ulong cluster) => volume[2] + cluster / 128;
        ulong Data(ulong cluster) => volume[3] + cluster - 2;
        ulong[] lbas = [volume[0], Fat(root[1]), Fat(root[1]) + fatSectors, Fat(root[2]), Fat(root[2]) + fatSectors, Data(root[1]), Data(root[2]), Data(file[0]), Fat(file[0]), Fat(file[0]) + fatSectors, Data(file[1]), Fat(file[1]), Fat(file[1]) + fatSectors, Data(file[2]), Fat(file[2]), Fat(file[2]) + fatSectors];
        for (int i = 0; i < lbas.Length; i++)
        {
            var read = Row(0x80 + i);
            Need(read[0] == lbas[i] && read[1] <= uint.MaxValue && read[2] == (ulong)i + 70 && read[3] == 1, "m06_fat_read_lifetime");
        }

        foreach (int first in new[]
        {
            1,
            3,
            8,
            11,
            14
        }

        )
            Need(Row(0x80 + first)[1] == Row(0x81 + first)[1], "m06_fat_mirror_crc");
    }

    public static void AssertM06FatReadImage(string serialPath, string imagePath)
    {
        string[] lines = File.ReadAllLines(serialPath);
        ulong[] Row(int stage) => FatRow(lines, stage);
        using var disk = File.OpenRead(imagePath);
        Need(disk.Length >= 68 * 512 && disk.Length % 512 == 0, "m06_fat_disk_size");
        ulong capacity = (ulong)disk.Length / 512;
        byte[] Read(ulong lba)
        {
            Need(lba < capacity, "m06_fat_disk_range");
            byte[] bytes = new byte[512];
            disk.Position = checked((long)lba * 512);
            disk.ReadExactly(bytes);
            return bytes;
        }

        ulong[] bounds = Row(0), geometry = Row(1), root = Row(2), file = Row(7);
        var volume = default(LiveFat32Volume);
        Need(FATClusterChainsLoopBadClusterTruncationDetection.ParseBpb(Read(bounds[0]), capacity, bounds[0], bounds[1], BlockReadResult.Success(1, 512), ref volume) == LiveFat32Status.Success && volume.SectorsPerCluster == 1 && bounds.SequenceEqual(new ulong[] { volume.PartitionFirstLba, volume.PartitionLastLba, volume.FirstFatLba, volume.FirstDataLba }) && geometry.SequenceEqual(new ulong[] { 1, volume.ClusterCount, volume.RootCluster, volume.TotalSectors }), "m06_fat_disk_bpb");
        for (int i = 0; i < 16; i++)
        {
            ulong[] receipt = Row(0x80 + i);
            Need(Crc32.Compute(Read(receipt[0])) == receipt[1], "m06_fat_disk_sector_receipt");
        }

        uint Next(ulong cluster)
        {
            Need(cluster >= 2 && cluster - 2 < volume.ClusterCount, "m06_fat_disk_cluster");
            ulong lba = volume.FirstFatLba + cluster / 128;
            byte[] primary = Read(lba), mirror = Read(lba + volume.FatSectors);
            Need(primary.AsSpan().SequenceEqual(mirror), "m06_fat_disk_mirror");
            return BinaryPrimitives.ReadUInt32LittleEndian(primary.AsSpan((int)(cluster % 128) * 4)) & 0x0fffffff;
        }

        Need(Next(root[1]) == root[2] && Next(root[2]) >= 0x0ffffff8 && Next(file[0]) == file[1] && Next(file[1]) == file[2] && Next(file[2]) >= 0x0ffffff8, "m06_fat_disk_chains");
        // The older independent host filesystem parser supplies a second
        // traversal/name implementation, including exact file-size handling.
        var device = new FatTranscriptDevice(disk, capacity);
        byte[] contents = new byte[FatImageFixture.FileBytes];
        var read = Fat32Parser.ReadRootFile(device, bounds[0], FatImageFixture.LongName, contents);
        Need(read.Succeeded && read.BytesRead == FatImageFixture.FileBytes && read.Entry.FirstCluster == file[0] && read.Entry.FileSize == Row(4)[1] && Crc32.Compute(contents) == FatImageFixture.FileCrc32, "m06_fat_disk_file");
        for (uint i = 0; i < contents.Length; i++)
            Need(contents[i] == FatImageFixture.FixtureByte(i), "m06_fat_disk_known_bytes");
    }

    private static ulong[] FatRow(IReadOnlyList<string> lines, int stage)
    {
        string key = "M06_FAT " + stage.ToString("X2");
        string[] matches = lines.Where(s => s.StartsWith(key + " ", StringComparison.Ordinal)).ToArray();
        Need(matches.Length == 1, "m06_fat_unique_row");
        return ReadHexRecord(matches[0], key, 4);
    }
}
