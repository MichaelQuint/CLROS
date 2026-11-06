using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Std.Hash;
using OS.Storage;

namespace OS.Tooling;
/// <summary>Adds a bounded, fragmented read-only test fixture to a fresh image.
/// All admission and expected-byte checks precede the first write. This is smoke
/// tooling, not a filesystem writer or a general image migration operation.</summary>

public static class FatImageFixture
{
    public const string LongName = "CLROS Live Storage Fixture.bin", ShortName = "LSFIRE~1BIN";
    public const uint FileBytes = KnownFileSHAN256EquivalentKnownFixtureHash.FixtureBytes;
    public const uint FileCrc32 = KnownFileSHAN256EquivalentKnownFixtureHash.FixtureCrc32;
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);
    public static FatImageFixtureReceipt Apply(string imagePath, string efiPath)
    {
        using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Need(stream.Length is >= 34 * 1024 * 1024 and <= 256 * 1024 * 1024 && stream.Length % 512 == 0, "image_size");
        byte[] disk = new byte[checked((int)stream.Length)];
        stream.ReadExactly(disk);
        string originalHash = Hash(disk);
        ulong capacity = (ulong)disk.Length / 512;
        var primary = Gpt(false);
        var backup = Gpt(true);
        Need(primary.FirstLba == backup.FirstLba && primary.LastLba == backup.LastLba && disk.AsSpan(2 * 512, 16384).SequenceEqual(disk.AsSpan(disk.Length - 33 * 512, 16384)), "gpt_copies");
        int boot = checked((int)primary.FirstLba * 512);
        LiveFat32Volume volume = default;
        Need(FATClusterChainsLoopBadClusterTruncationDetection.ParseBpb(disk.AsSpan(boot, 512), capacity, primary.FirstLba, primary.LastLba, Complete, ref volume) == LiveFat32Status.Success, "bpb");
        Need(volume.SectorsPerCluster == 1 && volume.ReservedSectors == 32 && volume.RootCluster == 2 && volume.TotalSectors == primary.LastLba - primary.FirstLba + 1 && U16(boot + 48) == 1 && U16(boot + 50) == 6, "fresh_geometry");
        int fat = checked((int)volume.FirstFatLba * 512), fatBytes = checked((int)volume.FatSectors * 512);
        Need(disk.AsSpan(fat, fatBytes).SequenceEqual(disk.AsSpan(fat + fatBytes, fatBytes)), "fat_copies");
        Need(disk.AsSpan(boot, 512).SequenceEqual(disk.AsSpan(boot + 6 * 512, 512)), "boot_copies");
        Need(disk.AsSpan(boot + 512, 512).SequenceEqual(disk.AsSpan(boot + 7 * 512, 512)), "fsinfo_copies");
        int info = boot + 512;
        Need(U32(info) == 0x41615252 && U32(info + 484) == 0x61417272 && U32(info + 508) == 0xaa550000, "fsinfo");
        int root = Cluster(2);
        Need(Alias(root, "EFI        ") && disk[root + 11] == 0x10 && EntryCluster(root) == 3 && Alias(root + 32, "OS         ") && disk[root + 43] == 0x10 && Next(2) >= 0x0ffffff8, "fresh_root");
        foreach (byte value in disk.AsSpan(root + 64, 448))
            Need(value == 0, "root_not_empty");
        Need(Alias(Cluster(3) + 64, "BOOT       ") && EntryCluster(Cluster(3) + 64) == 4 && Alias(Cluster(4) + 64, "BOOTX64 EFI"), "efi_directories");
        int executable = Cluster(4) + 64;
        uint efiCluster = EntryCluster(executable), efiBytes = U32(executable + 28);
        byte[] expectedEfi = File.ReadAllBytes(efiPath);
        byte[] efiBefore = ReadChain(efiCluster, efiBytes);
        Need(efiBefore.AsSpan().SequenceEqual(expectedEfi), "efi_identity");
        List<uint> free = [];
        uint freeCount = 0;
        for (uint cluster = 2; cluster < volume.ClusterCount + 2; cluster++)
            if (Next(cluster) == 0)
            {
                if (free.Count < 5)
                    free.Add(cluster);
                freeCount++;
            }

        Need(free.Count == 5 && (U32(info + 488) == uint.MaxValue || U32(info + 488) == freeCount), "free_clusters");
        uint extension = free[0], first = free[1], second = free[3], third = free[2];
        Need(extension != 2 && first != second + 1 && first + 1 != second, "fragmentation");
        SetNext(2, extension);
        SetNext(extension, 0x0fffffff);
        SetNext(first, second);
        SetNext(second, third);
        SetNext(third, 0x0fffffff);
        for (int i = 2; i < 15; i++)
            disk[root + i * 32] = 0xe5;
        int tail = Cluster(extension);
        disk.AsSpan(tail, 512).Clear();
        byte[] alias = Encoding.ASCII.GetBytes(ShortName);
        byte checksum = 0;
        foreach (byte value in alias)
            checksum = unchecked((byte)((checksum >> 1) + ((checksum & 1) << 7) + value));
        int[] offsets = [1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30];
        for (int ordinal = 3; ordinal >= 1; ordinal--)
        {
            int entry = ordinal == 3 ? root + 480 : tail + (2 - ordinal) * 32;
            disk.AsSpan(entry, 32).Clear();
            disk[entry] = (byte)(ordinal | (ordinal == 3 ? 0x40 : 0));
            disk[entry + 11] = 0x0f;
            disk[entry + 13] = checksum;
            for (int i = 0; i < 13; i++)
            {
                int index = (ordinal - 1) * 13 + i;
                ushort word = index < LongName.Length ? LongName[index] : index == LongName.Length ? (ushort)0 : (ushort)0xffff;
                W16(entry + offsets[i], word);
            }
        }

        alias.CopyTo(disk, tail + 64);
        disk[tail + 75] = 0x20;
        W16(tail + 84, (ushort)(first >> 16));
        W16(tail + 90, (ushort)first);
        W32(tail + 92, FileBytes);
        uint[] fileClusters = [first, second, third];
        for (uint sector = 0; sector < 3; sector++)
        {
            int start = Cluster(fileClusters[sector]);
            disk.AsSpan(start, 512).Fill(0xa5);
            for (uint i = 0; i < 512 && sector * 512 + i < FileBytes; i++)
                disk[start + i] = FixtureByte(sector * 512 + i);
        }

        W32(info + 488, freeCount - 4);
        W32(info + 492, free[4]);
        disk.AsSpan(info, 512).CopyTo(disk.AsSpan(boot + 7 * 512, 512));
        byte[] file = ReadChain(first, FileBytes);
        Need(Crc32.Compute(file) == FileCrc32 && ReadChain(efiCluster, efiBytes).AsSpan().SequenceEqual(efiBefore), "content_identity");
        string finalHash = Hash(disk);
        stream.Position = 0;
        stream.Write(disk);
        stream.Flush(true);
        stream.Position = 0;
        Need(stream.Length == disk.Length && Convert.ToHexString(SHA256.HashData(stream)) == finalHash, "write_readback");
        return new(originalHash, finalHash, Hash(efiBefore), primary.FirstLba, primary.LastLba, extension, first, second, third, FileBytes, FileCrc32, LongName);
        LiveEfiPartitionResult Gpt(bool isBackup)
        {
            int position = isBackup ? disk.Length - 512 : 512;
            var header = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(disk.AsSpan(position, 512), capacity, Complete, isBackup);
            Need(header.Succeeded && header.EntryLba == (isBackup ? capacity - 33 : 2UL), "gpt_header");
            Span<byte> entries = disk.AsSpan(checked((int)header.EntryLba * 512), 16384);
            var partition = FAT32ApplicationEFIPartition.Select(header, entries);
            Need(partition.Succeeded, "gpt_partition");
            return partition;
        }

        ushort U16(int position) => BinaryPrimitives.ReadUInt16LittleEndian(disk.AsSpan(position, 2));
        uint U32(int position) => BinaryPrimitives.ReadUInt32LittleEndian(disk.AsSpan(position, 4));
        void W16(int position, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(disk.AsSpan(position, 2), value);
        void W32(int position, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(disk.AsSpan(position, 4), value);
        uint Next(uint cluster) => U32(checked(fat + (int)cluster * 4)) & 0x0fffffff;
        void SetNext(uint cluster, uint next)
        {
            int position = checked(fat + (int)cluster * 4);
            uint value = (U32(position) & 0xf0000000) | next;
            W32(position, value);
            W32(position + fatBytes, value);
        }

        int Cluster(uint cluster)
        {
            Need(cluster >= 2 && cluster - 2 < volume.ClusterCount, "cluster_range");
            return checked((int)(volume.FirstDataLba + cluster - 2) * 512);
        }

        uint EntryCluster(int entry) => ((uint)U16(entry + 20) << 16) | U16(entry + 26);
        bool Alias(int entry, string name) => disk.AsSpan(entry, 11).SequenceEqual(Encoding.ASCII.GetBytes(name));
        byte[] ReadChain(uint cluster, uint length)
        {
            Need(length is > 0 and <= 8 * 1024 * 1024, "file_size");
            byte[] bytes = new byte[length];
            HashSet<uint> seen = [];
            uint offset = 0;
            while (offset < length)
            {
                Need(seen.Add(cluster), "chain_loop");
                int start = Cluster(cluster);
                int count = (int)Math.Min(512, length - offset);
                disk.AsSpan(start, count).CopyTo(bytes.AsSpan((int)offset));
                offset += (uint)count;
                cluster = Next(cluster);
            }

            Need(cluster >= 0x0ffffff8, "chain_length");
            return bytes;
        }
    }

    public static byte FixtureByte(uint offset) => unchecked((byte)((offset * 73 + 19) ^ (offset >> 3)));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("fat_image_fixture_" + reason);
    }
}
