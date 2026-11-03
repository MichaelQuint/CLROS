using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;
using OS.Std.Hash;
using OS.Storage;

namespace OS.Tooling;
/// <summary>Adds exactly two bounded files after FatImageFixture.Apply on a fresh
/// canonical image. Fault variants change only their declared bytes or FAT link.
/// Every admission, mutation and preservation check precedes the first write.</summary>

public static class PackageImageFixture
{
    private const string ManifestText = "format=os-native-manifest\nversion=0\napp.name=Hello\napp.path=bin/hello.app\nargs=\nnetwork=none\nlog=serial\n";
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);
    public static PackageImageFixtureReceipt Apply(string imagePath, string efiPath, string variant = "Intact")
    {
        Need(variant is "Intact" or "ManifestVersion" or "PackageChecksum" or "PackageHeader" or "PackageTruncated" or "PackageLoop", "variant");
        using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Need(stream.Length is >= 34 * 1024 * 1024 and <= 256 * 1024 * 1024 && stream.Length % 512 == 0, "image_size");
        byte[] original = new byte[checked((int)stream.Length)];
        stream.ReadExactly(original);
        byte[] disk = (byte[])original.Clone();
        string originalHash = Hash(original);
        ulong capacity = (ulong)disk.Length / 512;
        var primary = Gpt(false);
        var backup = Gpt(true);
        Need(primary.Partition.FirstLba == backup.Partition.FirstLba && primary.Partition.LastLba == backup.Partition.LastLba && primary.Header.DiskGuidLow == backup.Header.DiskGuidLow && primary.Header.DiskGuidHigh == backup.Header.DiskGuidHigh && primary.Header.FirstUsableLba == backup.Header.FirstUsableLba && primary.Header.LastUsableLba == backup.Header.LastUsableLba && disk.AsSpan(2 * 512, 16384).SequenceEqual(disk.AsSpan(disk.Length - 33 * 512, 16384)), "gpt_copies");
        int boot = checked((int)primary.Partition.FirstLba * 512);
        LiveFat32Volume volume = default;
        Need(FATClusterChainsLoopBadClusterTruncationDetection.ParseBpb(disk.AsSpan(boot, 512), capacity, primary.Partition.FirstLba, primary.Partition.LastLba, Complete, ref volume) == LiveFat32Status.Success, "bpb");
        Need(volume.SectorsPerCluster == 1 && volume.ReservedSectors == 32 && volume.RootCluster == 2 && volume.TotalSectors == primary.Partition.LastLba - primary.Partition.FirstLba + 1 && U16(boot + 48) == 1 && U16(boot + 50) == 6, "geometry");
        int fat = checked((int)volume.FirstFatLba * 512), fatBytes = checked((int)volume.FatSectors * 512);
        int info = boot + 512;
        Need(disk.AsSpan(fat, fatBytes).SequenceEqual(disk.AsSpan(fat + fatBytes, fatBytes)), "fat_copies");
        Need(disk.AsSpan(boot, 512).SequenceEqual(disk.AsSpan(boot + 6 * 512, 512)), "boot_copies");
        Need(disk.AsSpan(info, 512).SequenceEqual(disk.AsSpan(boot + 7 * 512, 512)) && U32(info) == 0x41615252 && U32(info + 484) == 0x61417272 && U32(info + 508) == 0xaa550000, "fsinfo");
        uint extension = Next(2);
        Need(extension != 2 && extension >= 2 && extension < volume.ClusterCount + 2 && Next(extension) >= 0x0ffffff8, "fat_fixture_root");
        int root = Cluster(2), tail = Cluster(extension);
        Need(Alias(root, "EFI        ") && disk[root + 11] == 0x10 && EntryCluster(root) == 3 && Alias(root + 32, "OS         ") && disk[root + 43] == 0x10, "root_directories");
        uint osCluster = EntryCluster(root + 32);
        int os = Cluster(osCluster);
        Need(osCluster != 2 && osCluster != extension && osCluster != 3 && osCluster != 4 && Next(osCluster) >= 0x0ffffff8 && Alias(os, ".          ") && EntryCluster(os) == osCluster && Alias(os + 32, "..         ") && EntryCluster(os + 32) == 2, "os_directory");
        // The accepted long name crosses root/extension sectors. Parse the
        // sequence independently and require the original fragmented bytes.
        LiveVfatState rootState = default;
        ushort[] name = new ushort[260];
        int rootFiles = 0, efiDirectories = 0, osDirectories = 0;
        uint fixtureFirst = 0;
        foreach (int sector in new[]
        {
            root,
            tail
        }

        )
            for (int slot = 0; slot < 16; slot++)
            {
                int entry = sector + slot * 32;
                var status = MultiClusterDirectoryVFATLongNameSupportSeparate.Consume(disk.AsSpan(entry, 32), ref rootState, name, out var parsed);
                Need(status is LiveDirectoryStatus.Entry or LiveDirectoryStatus.Skipped or LiveDirectoryStatus.LongName or LiveDirectoryStatus.End, "root_entry");
                if (status == LiveDirectoryStatus.End)
                {
                    Need(sector == tail && disk.AsSpan(entry, tail + 512 - entry).IndexOfAnyExcept((byte)0) < 0, "root_end");
                    break;
                }

                if (status != LiveDirectoryStatus.Entry)
                    continue;
                if (Alias(entry, "EFI        "))
                {
                    Need(parsed.Attributes == 0x10 && parsed.FirstCluster == 3, "root_efi");
                    efiDirectories++;
                    continue;
                }

                if (Alias(entry, "OS         "))
                {
                    Need(parsed.Attributes == 0x10 && parsed.FirstCluster == osCluster, "root_os");
                    osDirectories++;
                    continue;
                }

                Need(Alias(entry, FatImageFixture.ShortName) && NameIs(parsed.NameLength, FatImageFixture.LongName, false) && parsed.Attributes == 0x20 && parsed.ByteCount == FatImageFixture.FileBytes, "fat_fixture_entry");
                fixtureFirst = parsed.FirstCluster;
                rootFiles++;
            }

        Need(rootFiles == 1 && efiDirectories == 1 && osDirectories == 1 && rootState.Slots == 0, "fat_fixture_missing");
        byte[] fixture = ReadChain(fixtureFirst, FatImageFixture.FileBytes);
        Need(Crc32.Compute(fixture) == FatImageFixture.FileCrc32, "fat_fixture_crc");
        for (uint index = 0; index < fixture.Length; index++)
            Need(fixture[index] == FatImageFixture.FixtureByte(index), "fat_fixture_bytes");
        Need(Next(3) >= 0x0ffffff8 && Next(4) >= 0x0ffffff8 && Alias(Cluster(3) + 64, "BOOT       ") && EntryCluster(Cluster(3) + 64) == 4 && Alias(Cluster(4) + 64, "BOOTX64 EFI"), "efi_directories");
        int executable = Cluster(4) + 64;
        uint efiCluster = EntryCluster(executable), efiBytes = U32(executable + 28);
        byte[] expectedEfi = File.ReadAllBytes(efiPath), efiBefore = ReadChain(efiCluster, efiBytes);
        Need(efiBefore.AsSpan().SequenceEqual(expectedEfi), "efi_identity");
        LiveVfatState osState = default;
        int empty = -1;
        for (int slot = 0; slot < 16; slot++)
        {
            int entry = os + slot * 32;
            var status = MultiClusterDirectoryVFATLongNameSupportSeparate.Consume(disk.AsSpan(entry, 32), ref osState, name, out var parsed);
            Need(status is LiveDirectoryStatus.Entry or LiveDirectoryStatus.Skipped or LiveDirectoryStatus.LongName or LiveDirectoryStatus.End, "os_entry");
            if (status == LiveDirectoryStatus.End)
            {
                empty = entry;
                Need(disk.AsSpan(entry, os + 512 - entry).IndexOfAnyExcept((byte)0) < 0, "os_tail");
                break;
            }

            if (status != LiveDirectoryStatus.Entry)
                continue;
            Need(!Alias(entry, "NATIVE  MAN") && !Alias(entry, "APP     PKG") && !NameIs(parsed.NameLength, "NATIVE.MAN", true) && !NameIs(parsed.NameLength, "APP.PKG", true), "already_present");
            Need(parsed.Attributes == 0x20 && parsed.ByteCount != 0, "os_file");
            ReadChain(parsed.FirstCluster, parsed.ByteCount);
        }

        Need(empty >= os + 64 && empty <= os + 13 * 32 && osState.Slots == 0, "directory_slots");
        List<uint> free = [];
        uint freeCount = 0;
        for (uint cluster = 2; cluster < volume.ClusterCount + 2; cluster++)
            if (Next(cluster) == 0)
            {
                if (free.Count < 3)
                    free.Add(cluster);
                freeCount++;
            }

        Need(free.Count == 3 && U32(info + 488) == freeCount, "free_clusters");
        uint manifestCluster = free[0], packageCluster = free[1];
        byte[] manifest = Encoding.ASCII.GetBytes(ManifestText), package = CreatePackage();
        Need(manifest.Length == M06DiskPackageFixture.ManifestBytes && package.Length == M06DiskPackageFixture.PackageBytes && Crc32.Compute(package) == M06DiskPackageFixture.PackageCrc32 && Crc32.Compute(package.AsSpan(96)) == M06DiskPackageFixture.PayloadCrc32 && NativeManifestParser.Parse(manifest).Succeeded && OsPackageParser.FindFile(package, "bin/hello.app"u8).Succeeded, "canonical_fixture");
        if (variant == "ManifestVersion")
        {
            Need(manifest[34] == (byte)'0', "version_position");
            manifest[34] = (byte)'1';
        }

        if (variant == "PackageChecksum")
            package[131] ^= 1;
        if (variant == "PackageHeader")
            package[0] = (byte)'X';
        uint packageBytes = variant == "PackageTruncated" ? 700u : (uint)package.Length;
        int manifestData = Cluster(manifestCluster), packageData = Cluster(packageCluster);
        disk.AsSpan(manifestData, 512).Clear();
        manifest.CopyTo(disk, manifestData);
        disk.AsSpan(packageData, 512).Clear();
        package.CopyTo(disk, packageData);
        SetNext(manifestCluster, 0x0fffffff);
        SetNext(packageCluster, variant == "PackageLoop" ? packageCluster : 0x0fffffff);
        WriteEntry(empty, "NATIVE  MAN", manifestCluster, (uint)manifest.Length);
        WriteEntry(empty + 32, "APP     PKG", packageCluster, packageBytes);
        W32(info + 488, freeCount - 2);
        W32(info + 492, free[2]);
        disk.AsSpan(info, 512).CopyTo(disk.AsSpan(boot + 7 * 512, 512));
        // Validate the actual prospective disk contents and the precise variant.
        Need(Alias(empty, "NATIVE  MAN") && EntryCluster(empty) == manifestCluster && U32(empty + 28) == manifest.Length && Alias(empty + 32, "APP     PKG") && EntryCluster(empty + 32) == packageCluster && U32(empty + 60) == packageBytes && disk[empty + 64] == 0 && disk.AsSpan(manifestData, manifest.Length).SequenceEqual(manifest) && disk.AsSpan(packageData, package.Length).SequenceEqual(package) && disk.AsSpan(manifestData + manifest.Length, 512 - manifest.Length).IndexOfAnyExcept((byte)0) < 0 && disk.AsSpan(packageData + package.Length, 512 - package.Length).IndexOfAnyExcept((byte)0) < 0, "file_readback");
        var manifestResult = NativeManifestDisk.Validate(disk.AsSpan(manifestData, manifest.Length));
        Need(manifestResult.Status == (variant == "ManifestVersion" ? LiveManifestStatus.InvalidValue : LiveManifestStatus.Success), "manifest_variant");
        var packageResult = APPPKGDisk.Validate(disk.AsSpan(packageData, package.Length), "bin/hello.app"u8.ToArray(), M06DiskPackageFixture.PackageCrc32);
        Need(packageResult.Status == (variant == "PackageHeader" ? LivePackageStatus.InvalidHeader : variant == "PackageChecksum" ? LivePackageStatus.ChecksumMismatch : LivePackageStatus.Success), "package_variant");
        var content = OsPackageParser.GetFileContent(package, "bin/hello.app"u8);
        Need(variant == "PackageHeader" ? content.Status == OsPackageStatus.InvalidMagic : content.Succeeded && NativeAppPayloadHeader.Parse(content.Content).Header.EntryOffset == M06DiskPackageFixture.EntryOffset, "payload_variant");
        Need(ChainStatus(packageCluster, packageBytes) == (variant == "PackageTruncated" ? LiveFat32Status.Truncated : variant == "PackageLoop" ? LiveFat32Status.Loop : LiveFat32Status.Success) && Next(manifestCluster) >= 0x0ffffff8, "chain_variant");
        Need(disk.AsSpan(fat, fatBytes).SequenceEqual(disk.AsSpan(fat + fatBytes, fatBytes)) && disk.AsSpan(info, 512).SequenceEqual(disk.AsSpan(boot + 7 * 512, 512)) && U32(info + 488) == freeCount - 2 && U32(info + 492) == free[2] && Next(free[2]) == 0, "mirrored_readback");
        List<(int Start, int Bytes)> writes = [(empty, 64), (manifestData, 512), (packageData, 512), (info + 488, 8), (boot + 7 * 512 + 488, 8)];
        foreach (uint cluster in new[]
        {
            manifestCluster,
            packageCluster
        }

        )
            foreach (int copy in new[]
            {
                0,
                fatBytes
            }

            )
                writes.Add((checked(fat + copy + (int)cluster * 4), 4));
        int cursor = 0;
        foreach (var write in writes.OrderBy(write => write.Start))
        {
            Need(write.Start >= cursor && original.AsSpan(cursor, write.Start - cursor).SequenceEqual(disk.AsSpan(cursor, write.Start - cursor)), "mutation_bounds");
            cursor = write.Start + write.Bytes;
        }

        Need(original.AsSpan(cursor).SequenceEqual(disk.AsSpan(cursor)) && ReadChain(efiCluster, efiBytes).AsSpan().SequenceEqual(efiBefore) && ReadChain(fixtureFirst, FatImageFixture.FileBytes).AsSpan().SequenceEqual(fixture), "preserved_identity");
        string finalHash = Hash(disk);
        stream.Position = 0;
        stream.Write(disk);
        stream.Flush(true);
        stream.Position = 0;
        Need(stream.Length == disk.Length && Convert.ToHexString(SHA256.HashData(stream)) == finalHash, "write_readback");
        return new(variant, originalHash, finalHash, Hash(efiBefore), primary.Partition.FirstLba, primary.Partition.LastLba, 2, extension, osCluster, manifestCluster, packageCluster, (uint)manifest.Length, packageBytes, (uint)package.Length, Crc32.Compute(manifest), Crc32.Compute(package), Crc32.Compute(package.AsSpan(96)));
        (LiveGptHeaderResult Header, LiveEfiPartitionResult Partition) Gpt(bool isBackup)
        {
            int position = isBackup ? disk.Length - 512 : 512;
            var header = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(disk.AsSpan(position, 512), capacity, Complete, isBackup);
            Need(header.Succeeded && header.EntryLba == (isBackup ? capacity - 33 : 2UL), "gpt_header");
            var partition = FAT32ApplicationEFIPartition.Select(header, disk.AsSpan(checked((int)header.EntryLba * 512), 16384));
            Need(partition.Succeeded, "gpt_partition");
            return (header, partition);
        }

        ushort U16(int position) => BinaryPrimitives.ReadUInt16LittleEndian(disk.AsSpan(position, 2));
        uint U32(int position) => BinaryPrimitives.ReadUInt32LittleEndian(disk.AsSpan(position, 4));
        void W16(int position, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(disk.AsSpan(position, 2), value);
        void W32(int position, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(disk.AsSpan(position, 4), value);
        uint Next(uint cluster)
        {
            Need(cluster >= 2 && cluster < volume.ClusterCount + 2, "fat_cluster_range");
            return U32(checked(fat + (int)cluster * 4)) & 0x0fffffff;
        }

        void SetNext(uint cluster, uint next)
        {
            int position = checked(fat + (int)cluster * 4);
            uint value = (U32(position) & 0xf0000000) | next;
            W32(position, value);
            W32(position + fatBytes, value);
        }

        int Cluster(uint cluster)
        {
            Need(cluster >= 2 && cluster < volume.ClusterCount + 2, "cluster_range");
            return checked((int)(volume.FirstDataLba + cluster - 2) * 512);
        }

        uint EntryCluster(int entry) => ((uint)U16(entry + 20) << 16) | U16(entry + 26);
        bool Alias(int entry, string alias) => Encoding.ASCII.GetString(disk, entry, 11).Equals(alias, StringComparison.OrdinalIgnoreCase);
        bool NameIs(ushort length, string expected, bool ignoreCase)
        {
            if (length != expected.Length)
                return false;
            for (int i = 0; i < length; i++)
                if (ignoreCase ? char.ToUpperInvariant((char)name[i]) != char.ToUpperInvariant(expected[i]) : name[i] != expected[i])
                    return false;
            return true;
        }

        void WriteEntry(int entry, string alias, uint cluster, uint length)
        {
            disk.AsSpan(entry, 32).Clear();
            Encoding.ASCII.GetBytes(alias).CopyTo(disk, entry);
            disk[entry + 11] = 0x20;
            W16(entry + 20, (ushort)(cluster >> 16));
            W16(entry + 26, (ushort)cluster);
            W32(entry + 28, length);
        }

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

        LiveFat32Status ChainStatus(uint cluster, uint length)
        {
            Span<uint> history = stackalloc uint[8];
            uint visited = 0;
            while (true)
            {
                var status = FATClusterChainsLoopBadClusterTruncationDetection.Visit(volume, cluster, history, ref visited);
                if (status != LiveFat32Status.Success)
                    return status;
                ulong lba = volume.FirstFatLba + cluster / 128;
                uint next = 0;
                status = FATClusterChainsLoopBadClusterTruncationDetection.ReadFatEntry(volume, cluster, lba, disk.AsSpan(checked((int)lba * 512), 512), Complete, ref next);
                if (status == LiveFat32Status.EndOfChain)
                    return FATClusterChainsLoopBadClusterTruncationDetection.CheckLength(visited, (length + 511) / 512);
                if (status != LiveFat32Status.Success)
                    return status;
                cluster = next;
            }
        }
    }

    private static byte[] CreatePackage()
    {
        byte[] bytes = new byte[132];
        "OSPKGv0\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(36), 13);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(40), 96);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(48), 36);
        "bin/hello.app"u8.CopyTo(bytes.AsSpan(64));
        "OSAPPv0\0"u8.CopyTo(bytes.AsSpan(96));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(106), 32);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(112), 32);
        bytes[128] = 0x31;
        bytes[129] = 0xc0;
        bytes[130] = 0xc3;
        bytes[131] = 0x90;
        return bytes;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("package_image_fixture_" + reason);
    }
}
