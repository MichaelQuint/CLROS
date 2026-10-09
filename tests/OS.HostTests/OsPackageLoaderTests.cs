using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Storage;

internal static class OsPackageLoaderTests
{
    private const uint SectorSize = 512;
    private const ulong DiskBlockCount = 256;
    private const ulong PartitionStartLba = 40;
    private const ulong PartitionBlockCount = 128;
    private const ushort ReservedSectorCount = 32;
    private const byte FatCount = 2;
    private const uint FatSizeSectors = 8;
    private const uint RootCluster = 2;
    private const uint TotalSectors = 128;
    private const uint PackageCluster = 15;
    private const uint OsDirectoryCluster = 16;
    private const ulong RootDirectoryLba = ReservedSectorCount + (FatCount * FatSizeSectors);
    private const ulong PartitionEntryLba = 2;
    private const ulong BackupPartitionEntryLba = DiskBlockCount - 33;
    private const ulong BackupHeaderLba = DiskBlockCount - 1;
    private const uint PartitionEntryCount = 128;
    private const uint PartitionEntrySize = 128;
    private const ulong FirstUsableLba = 34;
    private const ulong LastUsableLba = 220;

    private static readonly Guid DiskGuid = new Guid("15151515-3434-5656-7878-909090909090");
    private static readonly Guid EspGuid = new Guid("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    private static readonly Guid UniquePartitionGuid = new Guid("bbbbbbbb-1111-2222-3333-cccccccccccc");

    [HostTest("storage.package loader reads package from gpt esp", HostLane.Fast, 0)]
    private static bool TestReadsPackageFromGptEsp()
    {
        byte[] package = ValidPackage();
        byte[] disk = CreateDiskImage(package, hasEsp: true, hasPackage: true);
        byte[] buffer = new byte[1024];

        OsPackageLoadResult result = OsPackageLoader.LoadFromPrimaryGptEsp(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            buffer);

        OsPackageFileContentResult content = OsPackageParser.GetFileContent(
            buffer.AsSpan(0, checked((int)result.BytesRead)),
            "bin/app.dll"u8);

        return HostCheck.Result(result.Succeeded
            && result.Status == OsPackageLoadStatus.Success
            && result.GptStatus == GptStatus.Success
            && result.Fat32Status == Fat32Status.Success
            && result.PackageStatus == OsPackageStatus.Success
            && result.BytesRead == package.Length
            && result.PackageHeader.FileCount == 1
            && content.Succeeded
            && content.Content.Length == 4
            && content.Content[0] == 0xCA
            && content.Content[3] == 0xBE);
    }

    [HostTest("storage.package loader rejects missing package", HostLane.Fast, 12)]
    private static bool TestRejectsMissingPackage()
    {
        byte[] disk = CreateDiskImage(ValidPackage(), hasEsp: true, hasPackage: false);
        byte[] buffer = new byte[1024];

        OsPackageLoadResult result = OsPackageLoader.LoadFromPrimaryGptEsp(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.Status == OsPackageLoadStatus.Fat32ReadFailed
            && result.Fat32Status == Fat32Status.MissingFile);
    }

    [HostTest("storage.package loader rejects malformed package", HostLane.Fast, 13)]
    private static bool TestRejectsMalformedPackage()
    {
        byte[] package = ValidPackage();
        package[0] = 0;
        byte[] disk = CreateDiskImage(package, hasEsp: true, hasPackage: true);
        byte[] buffer = new byte[1024];

        OsPackageLoadResult result = OsPackageLoader.LoadFromPrimaryGptEsp(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.Status == OsPackageLoadStatus.PackageParseFailed
            && result.Fat32Status == Fat32Status.Success
            && result.PackageStatus == OsPackageStatus.InvalidMagic
            && result.BytesRead == package.Length);
    }

    [HostTest("storage.package loader caller-buffer gpt reads package", HostLane.Fast, 1)]
    private static bool TestCallerBufferGptReadsPackage()
    {
        byte[] package = ValidPackage();
        byte[] disk = CreateDiskImage(package, hasEsp: true, hasPackage: true);
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[1024];

        OsPackageLoadResult result = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(result.Succeeded
            && result.Status == OsPackageLoadStatus.Success
            && result.GptStatus == GptStatus.Success
            && result.GptTableStatus == GptPartitionTableStatus.Success
            && result.GptCrcStatus == GptPartitionTableCrcStatus.Validated
            && result.GptPartitionEntryIndex == 0
            && result.GptPartitionEntryBytesRequired == PartitionEntryCount * PartitionEntrySize
            && result.GptPartitionEntrySectorBytesRequired == PartitionEntryCount * PartitionEntrySize
            && result.ExpectedGptHeaderCrc32 == result.ComputedGptHeaderCrc32
            && result.ExpectedGptPartitionEntryArrayCrc32 == result.ComputedGptPartitionEntryArrayCrc32
            && result.PackageStatus == OsPackageStatus.Success
            && result.BytesRead == package.Length);
    }

    [HostTest("storage.package loader caller-buffer reports primary gpt source", HostLane.Fast, 2)]
    private static bool TestCallerBufferReportsPrimaryGptSource()
    {
        byte[] package = ValidPackage();
        byte[] disk = CreateDiskImage(package, hasEsp: true, hasPackage: true);
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[1024];

        OsPackageLoadResult result = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(result.Succeeded
            && result.GptTableSource == GptPartitionTableSource.Primary
            && result.PackageStatus == OsPackageStatus.Success);
    }

    [HostTest("storage.package loader caller-buffer gpt rejects crc mismatch", HostLane.Fast, 5)]
    private static bool TestCallerBufferGptRejectsCrcMismatch()
    {
        byte[] disk = CreateDiskImage(ValidPackage(), hasEsp: true, hasPackage: true);
        disk[checked((int)SectorSize) + 20] = 0x5A;
        disk[checked((int)(BackupHeaderLba * SectorSize)) + 20] = 0x5A;
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[1024];

        OsPackageLoadResult result = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.Status == OsPackageLoadStatus.GptFailed
            && result.GptTableStatus == GptPartitionTableStatus.HeaderCrcMismatch
            && result.GptCrcStatus == GptPartitionTableCrcStatus.HeaderMismatch
            && result.ExpectedGptHeaderCrc32 != result.ComputedGptHeaderCrc32);
    }

    [HostTest("storage.package loader caller-buffer gpt falls back to backup", HostLane.Fast, 3)]
    private static bool TestCallerBufferGptFallsBackToBackup()
    {
        byte[] package = ValidPackage();
        byte[] disk = CreateDiskImage(package, hasEsp: true, hasPackage: true);
        disk[checked((int)SectorSize) + 20] = 0x5A;
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[1024];

        OsPackageLoadResult result = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(result.Succeeded
            && result.Status == OsPackageLoadStatus.Success
            && result.GptStatus == GptStatus.Success
            && result.GptTableStatus == GptPartitionTableStatus.Success
            && result.GptCrcStatus == GptPartitionTableCrcStatus.Validated
            && ReadUInt64(primaryHeader, 24) == BackupHeaderLba
            && ReadUInt64(primaryHeader, 72) == BackupPartitionEntryLba
            && result.PackageStatus == OsPackageStatus.Success
            && result.BytesRead == package.Length);
    }

    [HostTest("storage.package loader caller-buffer reports backup gpt source", HostLane.Fast, 4)]
    private static bool TestCallerBufferReportsBackupGptSource()
    {
        byte[] package = ValidPackage();
        byte[] disk = CreateDiskImage(package, hasEsp: true, hasPackage: true);
        disk[checked((int)SectorSize) + 20] = 0x5A;
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[1024];

        OsPackageLoadResult result = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(result.Succeeded
            && result.GptTableSource == GptPartitionTableSource.Backup
            && result.PackageStatus == OsPackageStatus.Success);
    }

    [HostTest("storage.package loader caller-buffer reports gpt header capacity", HostLane.Fast, 6)]
    private static bool TestCallerBufferReportsGptHeaderCapacity()
    {
        byte[] disk = CreateDiskImage(ValidPackage(), hasEsp: true, hasPackage: true);
        byte[] primaryHeader = new byte[SectorSize - 1];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[1024];

        OsPackageLoadResult result = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.Status == OsPackageLoadStatus.GptFailed
            && result.GptStatus == GptStatus.PrimaryHeaderShortRead
            && result.BlockStatus == BlockDeviceStatus.DestinationTooSmall
            && result.GptHeaderBytesRequired == SectorSize
            && result.GptHeaderBufferBytesAvailable == checked((ulong)primaryHeader.Length)
            && result.GptPartitionEntryBufferBytesAvailable == checked((ulong)partitionEntries.Length));
    }

    [HostTest("storage.package loader caller-buffer reports gpt entry buffer capacity", HostLane.Fast, 7)]
    private static bool TestCallerBufferReportsGptEntryBufferCapacity()
    {
        byte[] disk = CreateDiskImage(ValidPackage(), hasEsp: true, hasPackage: true);
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[SectorSize];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[1024];

        OsPackageLoadResult result = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.Status == OsPackageLoadStatus.GptFailed
            && result.GptStatus == GptStatus.PartitionEntryShortRead
            && result.GptTableStatus == GptPartitionTableStatus.PartitionEntryBufferTooSmall
            && result.BlockStatus == BlockDeviceStatus.DestinationTooSmall
            && result.GptHeaderBytesRequired == SectorSize
            && result.GptHeaderBufferBytesAvailable == SectorSize
            && result.GptPartitionEntryBytesRequired == PartitionEntryCount * PartitionEntrySize
            && result.GptPartitionEntrySectorBytesRequired == PartitionEntryCount * PartitionEntrySize
            && result.GptPartitionEntryBufferBytesAvailable == checked((ulong)partitionEntries.Length));
    }

    [HostTest("storage.package loader caller-buffer reports package buffer capacity", HostLane.Fast, 8)]
    private static bool TestCallerBufferReportsPackageBufferCapacity()
    {
        byte[] package = ValidPackage();
        byte[] disk = CreateDiskImage(package, hasEsp: true, hasPackage: true);
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[package.Length - 1];

        OsPackageLoadResult result = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.Status == OsPackageLoadStatus.Fat32ReadFailed
            && result.Fat32Status == Fat32Status.FileDestinationTooSmall
            && result.PackageBytesRequired == package.Length
            && result.PackageBufferBytesAvailable == buffer.Length
            && result.BytesRead == 0
            && result.GptCrcStatus == GptPartitionTableCrcStatus.Validated);
    }

    [HostTest("storage.package loader caller-buffer uses gpt partition selector", HostLane.Fast, 9)]
    private static bool TestCallerBufferUsesGptPartitionSelector()
    {
        string source = File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Storage",
            "OsPackageLoader.cs"));

        return HostCheck.Result(source.Contains("GptPartitionSelector.SelectByPartitionTypeGuid(", StringComparison.Ordinal)
            && source.Contains("GptPartitionSelectorStatus.Success", StringComparison.Ordinal)
            && source.Contains("GptPartitionSelectorStatus.NotFound", StringComparison.Ordinal)
            && source.Contains("GptParser.EfiSystemPartitionTypeGuid", StringComparison.Ordinal));
    }

    [HostTest("storage.package loader default result is not success", HostLane.Fast, 10)]
    private static bool TestDefaultLoadResultIsNotSuccess()
    {
        OsPackageLoadResult result = default;
        OsPackageLoadResult failed = OsPackageLoadResult.GptFail(
            GptStatus.InvalidBlockDevice,
            BlockDeviceStatus.InvalidBlockSize);

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == OsPackageLoadStatus.Success
            && result.GptStatus == GptStatus.Success
            && result.GptTableStatus == GptPartitionTableStatus.Success
            && result.GptCrcStatus == GptPartitionTableCrcStatus.NotValidated
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Fat32Status == Fat32Status.Success
            && result.PackageStatus == OsPackageStatus.Success
            && result.GptTableSource == GptPartitionTableSource.Unknown
            && result.BytesRead == 0
            && result.PackageBytesRequired == 0
            && result.PackageBufferBytesAvailable == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == OsPackageLoadStatus.GptFailed
            && failed.GptStatus == GptStatus.InvalidBlockDevice
            && failed.BlockStatus == BlockDeviceStatus.InvalidBlockSize);
    }

    [HostTest("storage.package loader gpt esp readiness default result is not success", HostLane.Fast, 11)]
    private static bool TestDefaultGptEspReadinessResultIsNotSuccess()
    {
        GptEspLocationReadinessResult result = default;
        GptEspLocationReadinessResult failed = GptEspLocationReadinessResult.Fail(
            GptEspLocationReadinessStatus.InvalidBlockDevice,
            GptStatus.InvalidBlockDevice,
            BlockDeviceStatus.InvalidBlockCount,
            headerBytesRequired: SectorSize,
            headerBufferBytesAvailable: 128,
            partitionEntryBufferBytesAvailable: 256,
            tableSource: GptPartitionTableSource.Primary);

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == GptEspLocationReadinessStatus.Success
            && result.GptStatus == GptStatus.Success
            && result.GptTableStatus == GptPartitionTableStatus.Success
            && result.GptCrcStatus == GptPartitionTableCrcStatus.NotValidated
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.PartitionBlockCount == 0
            && result.HeaderBytesRequired == 0
            && result.HeaderBufferBytesAvailable == 0
            && result.PartitionEntryBufferBytesAvailable == 0
            && result.TableSource == GptPartitionTableSource.Unknown
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == GptEspLocationReadinessStatus.InvalidBlockDevice
            && failed.GptStatus == GptStatus.InvalidBlockDevice
            && failed.BlockStatus == BlockDeviceStatus.InvalidBlockCount
            && failed.HeaderBytesRequired == SectorSize
            && failed.HeaderBufferBytesAvailable == 128
            && failed.PartitionEntryBufferBytesAvailable == 256
            && failed.TableSource == GptPartitionTableSource.Primary);
    }

    private static byte[] CreateDiskImage(byte[] package, bool hasEsp, bool hasPackage)
    {
        byte[] disk = new byte[checked((int)(DiskBlockCount * SectorSize))];
        WriteGpt(disk, hasEsp);
        WriteFat32Volume(disk, checked((int)(PartitionStartLba * SectorSize)), package, hasPackage);
        return disk;
    }

    private static void WriteGpt(byte[] disk, bool hasEsp)
    {
        int primaryHeaderOffset = checked((int)SectorSize);
        int backupHeaderOffset = checked((int)(BackupHeaderLba * SectorSize));
        WriteGptHeader(
            disk,
            primaryHeaderOffset,
            currentLba: GptPartitionTableParser.PrimaryHeaderLba,
            backupLba: BackupHeaderLba,
            partitionEntryLba: PartitionEntryLba);
        WriteGptHeader(
            disk,
            backupHeaderOffset,
            currentLba: BackupHeaderLba,
            backupLba: GptPartitionTableParser.PrimaryHeaderLba,
            partitionEntryLba: BackupPartitionEntryLba);

        if (hasEsp)
        {
            WritePartitionEntry(
                disk,
                PartitionEntryLba,
                EspGuid,
                UniquePartitionGuid,
                PartitionStartLba,
                PartitionStartLba + PartitionBlockCount - 1,
                "ESP");
            WritePartitionEntry(
                disk,
                BackupPartitionEntryLba,
                EspGuid,
                UniquePartitionGuid,
                PartitionStartLba,
                PartitionStartLba + PartitionBlockCount - 1,
                "ESP");
        }

        RefreshGptCrcs(disk);
    }

    private static void WriteGptHeader(
        byte[] disk,
        int headerOffset,
        ulong currentLba,
        ulong backupLba,
        ulong partitionEntryLba)
    {
        WriteAscii(disk, headerOffset, "EFI PART");
        WriteUInt32(disk, headerOffset + 8, 0x00010000);
        WriteUInt32(disk, headerOffset + 12, GptParser.MinimumHeaderSize);
        WriteUInt64(disk, headerOffset + 24, currentLba);
        WriteUInt64(disk, headerOffset + 32, backupLba);
        WriteUInt64(disk, headerOffset + 40, FirstUsableLba);
        WriteUInt64(disk, headerOffset + 48, LastUsableLba);
        WriteGuid(disk, headerOffset + 56, DiskGuid);
        WriteUInt64(disk, headerOffset + 72, partitionEntryLba);
        WriteUInt32(disk, headerOffset + 80, PartitionEntryCount);
        WriteUInt32(disk, headerOffset + 84, PartitionEntrySize);
    }

    private static void WritePartitionEntry(
        byte[] disk,
        ulong partitionEntryLba,
        Guid partitionTypeGuid,
        Guid uniquePartitionGuid,
        ulong firstLba,
        ulong lastLba,
        string name)
    {
        int entryOffset = checked((int)(partitionEntryLba * SectorSize));
        WriteGuid(disk, entryOffset, partitionTypeGuid);
        WriteGuid(disk, entryOffset + 16, uniquePartitionGuid);
        WriteUInt64(disk, entryOffset + 32, firstLba);
        WriteUInt64(disk, entryOffset + 40, lastLba);
        WriteUtf16Name(disk, entryOffset + 56, name);
    }

    private static void WriteFat32Volume(
        byte[] disk,
        int partitionOffset,
        byte[] package,
        bool hasPackage)
    {
        disk[partitionOffset] = 0xEB;
        disk[partitionOffset + 1] = 0x58;
        disk[partitionOffset + 2] = 0x90;
        WriteAscii(disk, partitionOffset + 3, "OSFAT32 ");
        WriteUInt16(disk, partitionOffset + 11, (ushort)SectorSize);
        disk[partitionOffset + 13] = 1;
        WriteUInt16(disk, partitionOffset + 14, ReservedSectorCount);
        disk[partitionOffset + 16] = FatCount;
        WriteUInt16(disk, partitionOffset + 17, 0);
        WriteUInt16(disk, partitionOffset + 19, 0);
        disk[partitionOffset + 21] = 0xF8;
        WriteUInt16(disk, partitionOffset + 22, 0);
        WriteUInt32(disk, partitionOffset + 32, TotalSectors);
        WriteUInt32(disk, partitionOffset + 36, FatSizeSectors);
        WriteUInt32(disk, partitionOffset + 44, RootCluster);
        WriteAscii(disk, partitionOffset + 82, "FAT32   ");
        disk[partitionOffset + 510] = 0x55;
        disk[partitionOffset + 511] = 0xAA;

        int rootDirectory = partitionOffset + checked((int)(RootDirectoryLba * SectorSize));
        WriteDirectoryEntry(
            disk,
            rootDirectory,
            "OS         ",
            Fat32Parser.DirectoryAttribute,
            OsDirectoryCluster,
            0);
        disk[rootDirectory + 32] = 0x00;

        int osDirectory = partitionOffset + ClusterOffset(OsDirectoryCluster);
        WriteDirectoryEntry(
            disk,
            osDirectory,
            ".          ",
            Fat32Parser.DirectoryAttribute,
            OsDirectoryCluster,
            0);
        WriteDirectoryEntry(
            disk,
            osDirectory + 32,
            "..         ",
            Fat32Parser.DirectoryAttribute,
            RootCluster,
            0);

        if (hasPackage)
        {
            WriteDirectoryEntry(
                disk,
                osDirectory + 64,
                "APP     PKG",
                attributes: 0x20,
                firstCluster: PackageCluster,
                fileSize: (uint)package.Length);
            disk[osDirectory + 96] = 0x00;

            int packageOffset = partitionOffset + ClusterOffset(PackageCluster);
            package.CopyTo(disk.AsSpan(packageOffset));
        }
        else
        {
            disk[osDirectory + 64] = 0x00;
        }
    }

    private static int ClusterOffset(uint cluster)
    {
        ulong lba = RootDirectoryLba + (cluster - RootCluster);
        return checked((int)(lba * SectorSize));
    }

    private static byte[] ValidPackage()
    {
        const int tableOffset = OsPackageParser.HeaderSize;
        const int tableLength = OsPackageParser.FileTableEntrySize;
        const int pathOffset = tableOffset + tableLength;
        const string path = "bin/app.dll";
        const int contentOffset = pathOffset + 11;
        const int contentLength = 4;

        byte[] package = new byte[contentOffset + contentLength];
        WriteAscii(package, 0, "OSPKGv0");
        package[7] = 0;
        WriteUInt16(package, 8, OsPackageParser.SupportedVersion);
        WriteUInt16(package, 10, OsPackageParser.HeaderSize);
        WriteUInt16(package, 12, 1);
        WriteUInt16(package, 14, OsPackageParser.FileTableEntrySize);
        WriteUInt32(package, 16, tableOffset);
        WriteUInt32(package, 20, tableLength);
        WriteUInt32(package, 24, 0);
        WriteUInt32(package, 28, 0);
        WriteUInt32(package, tableOffset, pathOffset);
        WriteUInt16(package, tableOffset + 4, path.Length);
        WriteUInt16(package, tableOffset + 6, 0);
        WriteUInt64(package, tableOffset + 8, contentOffset);
        WriteUInt64(package, tableOffset + 16, contentLength);
        WriteUInt64(package, tableOffset + 24, 0);
        WriteAscii(package, pathOffset, path);
        package[contentOffset] = 0xCA;
        package[contentOffset + 1] = 0xFE;
        package[contentOffset + 2] = 0xBA;
        package[contentOffset + 3] = 0xBE;
        return package;
    }

    private static void WriteDirectoryEntry(
        byte[] image,
        int offset,
        string rawShortName,
        byte attributes,
        uint firstCluster,
        uint fileSize)
    {
        WriteAscii(image, offset, rawShortName);
        image[offset + 11] = attributes;
        WriteUInt16(image, offset + 20, (ushort)(firstCluster >> 16));
        WriteUInt16(image, offset + 26, (ushort)firstCluster);
        WriteUInt32(image, offset + 28, fileSize);
    }

    private static void WriteAscii(byte[] image, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            image[offset + i] = (byte)value[i];
        }
    }

    private static void WriteUtf16Name(byte[] image, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            WriteUInt16(image, offset + i * 2, (ushort)value[i]);
        }
    }

    private static void WriteGuid(byte[] image, int offset, Guid value)
    {
        if (!value.TryWriteBytes(image.AsSpan(offset, 16)))
        {
            throw new InvalidOperationException("Could not write test GUID.");
        }
    }

    private static void RefreshGptCrcs(byte[] image)
    {
        int primaryHeaderOffset = checked((int)SectorSize);
        int backupHeaderOffset = checked((int)(BackupHeaderLba * SectorSize));
        int entryOffset = checked((int)(PartitionEntryLba * SectorSize));
        int entryByteCount = checked((int)(PartitionEntryCount * PartitionEntrySize));
        uint entryCrc32 = Crc32.Compute(image.AsSpan(entryOffset, entryByteCount));
        WriteUInt32(image, primaryHeaderOffset + 88, entryCrc32);
        WriteUInt32(image, backupHeaderOffset + 88, entryCrc32);
        RefreshGptHeaderCrc32(image, primaryHeaderOffset);
        RefreshGptHeaderCrc32(image, backupHeaderOffset);
    }

    private static void RefreshGptHeaderCrc32(byte[] image, int headerOffset)
    {
        WriteUInt32(image, headerOffset + 16, 0);
        WriteUInt32(
            image,
            headerOffset + 16,
            Crc32.Compute(image.AsSpan(headerOffset, checked((int)GptParser.MinimumHeaderSize))));
    }

    private static void WriteUInt16(byte[] image, int offset, int value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset, 2), (ushort)value);
    }

    private static void WriteUInt32(byte[] image, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset, 4), value);
    }

    private static void WriteUInt64(byte[] image, int offset, ulong value)
    {
        EndianPrimitives.WriteUInt64LittleEndian(image.AsSpan(offset, 8), value);
    }

    private static ulong ReadUInt64(byte[] image, int offset)
    {
        return EndianPrimitives.ReadUInt64LittleEndian(image.AsSpan(offset, 8));
    }

    private sealed class MemoryBlockDevice : IReadOnlyBlockDevice
    {
        private MemoryBlockDevice(BlockDeviceInfo info, byte[] image)
        {
            Info = info;
            Image = image;
        }

        public BlockDeviceInfo Info { get; }

        private byte[] Image { get; }

        public static MemoryBlockDevice FromImage(byte[] image, uint blockSize)
        {
            BlockDeviceInfoResult info = BlockDeviceInfo.Create(blockSize, (ulong)image.Length / blockSize);
            if (!info.Succeeded)
            {
                throw new InvalidOperationException("Invalid test image geometry: " + info.Status);
            }

            return new MemoryBlockDevice(info.Info, image);
        }

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            BlockReadResult result = BlockDeviceRead.Validate(Info, firstBlock, blockCount, destination);
            if (!result.Succeeded)
            {
                return result;
            }

            int offset = checked((int)(firstBlock * Info.BlockSize));
            Image.AsSpan(offset, checked((int)result.BytesRead)).CopyTo(destination);
            return result;
        }
    }
}
