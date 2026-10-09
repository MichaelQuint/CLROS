using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Manifest;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Storage;

internal static class NativeAppSelectionLoaderTests
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
    private const uint ManifestCluster = 15;
    private const uint OsDirectoryCluster = 16;
    private const uint PackageCluster = 17;
    private const ulong RootDirectoryLba = ReservedSectorCount + (FatCount * FatSizeSectors);
    private const ulong PartitionEntryLba = 2;
    private const ulong BackupPartitionEntryLba = DiskBlockCount - 33;
    private const ulong BackupHeaderLba = DiskBlockCount - 1;
    private const uint PartitionEntryCount = 128;
    private const uint PartitionEntrySize = 128;
    private const ulong FirstUsableLba = 34;
    private const ulong LastUsableLba = 220;
    private const string AppPath = "apps/hello.bin";
    private const int AppContentLength = 48;

    private static readonly Guid DiskGuid = new("31313131-3434-5656-7878-909090909090");
    private static readonly Guid EspGuid = new("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    private static readonly Guid UniquePartitionGuid = new("dddddddd-1111-2222-3333-eeeeeeeeeeee");

    [HostTest("appmodel selection loader composes image inputs", HostLane.Fast, 0)]
    private static bool TestComposesImageInputs()
    {
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath), hasEsp: true, hasPackage: true);
        SelectionScratch scratch = new();

        NativeAppSelectionLoadResult result = Load(disk, scratch, NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(result.Succeeded
            && result.Status == NativeAppSelectionLoadStatus.Success
            && result.ManifestLoad.GptCrcStatus == GptPartitionTableCrcStatus.Validated
            && result.PackageLoad.GptCrcStatus == GptPartitionTableCrcStatus.Validated
            && result.PackageParse.Succeeded
            && result.LaunchPlan.Succeeded
            && result.Descriptor.Succeeded
            && result.Descriptor.Descriptor.AppBinaryLength == AppContentLength
            && result.Descriptor.Descriptor.PayloadHeader.EntryOffset == NativeAppPayloadHeader.HeaderSize
            && result.Descriptor.Descriptor.NetworkMode == NativeManifestNetworkMode.Dhcp
            && result.Descriptor.Descriptor.LogMode == NativeManifestLogMode.Serial
            && FieldEquals(scratch.ManifestBuffer, result.ManifestLoad.Manifest.AppPath, AppPath));
    }

    [HostTest("appmodel selection loader reports primary gpt source", HostLane.Fast, 1)]
    private static bool TestReportsPrimaryGptSource()
    {
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath), hasEsp: true, hasPackage: true);
        SelectionScratch scratch = new();

        NativeAppSelectionLoadResult result = Load(
            disk,
            scratch,
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(result.Succeeded
            && result.ManifestGptTableSource == GptPartitionTableSource.Primary
            && result.PackageGptTableSource == GptPartitionTableSource.Primary);
    }

    [HostTest("appmodel selection loader gpt falls back to backup", HostLane.Fast, 2)]
    private static bool TestGptFallsBackToBackup()
    {
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath), hasEsp: true, hasPackage: true);
        disk[checked((int)SectorSize) + 20] = 0x5A;
        SelectionScratch scratch = new();

        NativeAppSelectionLoadResult result = Load(
            disk,
            scratch,
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(result.Succeeded
            && result.Status == NativeAppSelectionLoadStatus.Success
            && result.ManifestLoad.Succeeded
            && result.PackageLoad.Succeeded
            && result.ManifestLoad.GptCrcStatus == GptPartitionTableCrcStatus.Validated
            && result.PackageLoad.GptCrcStatus == GptPartitionTableCrcStatus.Validated
            && ReadUInt64(scratch.PrimaryHeader, 24) == BackupHeaderLba
            && ReadUInt64(scratch.PrimaryHeader, 72) == BackupPartitionEntryLba
            && result.Descriptor.Succeeded
            && result.Descriptor.Descriptor.AppBinaryLength == AppContentLength);
    }

    [HostTest("appmodel selection loader reports backup gpt source", HostLane.Fast, 3)]
    private static bool TestReportsBackupGptSource()
    {
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath), hasEsp: true, hasPackage: true);
        disk[checked((int)SectorSize) + 20] = 0x5A;
        SelectionScratch scratch = new();

        NativeAppSelectionLoadResult result = Load(
            disk,
            scratch,
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(result.Succeeded
            && result.ManifestGptTableSource == GptPartitionTableSource.Backup
            && result.PackageGptTableSource == GptPartitionTableSource.Backup);
    }

    [HostTest("appmodel selection loader rejects manifest load", HostLane.Fast, 4)]
    private static bool TestRejectsManifestLoad()
    {
        byte[] disk = CreateDiskImage(MalformedManifest(), ValidPackage(AppPath), hasEsp: true, hasPackage: true);
        SelectionScratch scratch = new();

        NativeAppSelectionLoadResult result = Load(disk, scratch, NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(!result.Succeeded
            && result.Status == NativeAppSelectionLoadStatus.ManifestLoadFailed
            && result.ManifestLoad.Status == NativeManifestLoadStatus.ManifestParseFailed
            && result.ManifestLoad.ManifestStatus == NativeManifestParseStatus.InvalidNetworkMode);
    }

    [HostTest("appmodel selection loader rejects package load", HostLane.Fast, 6)]
    private static bool TestRejectsPackageLoad()
    {
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath), hasEsp: true, hasPackage: false);
        SelectionScratch scratch = new();

        NativeAppSelectionLoadResult result = Load(disk, scratch, NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(!result.Succeeded
            && result.Status == NativeAppSelectionLoadStatus.PackageLoadFailed
            && result.ManifestLoad.Succeeded
            && result.PackageLoad.Status == OsPackageLoadStatus.Fat32ReadFailed
            && result.PackageLoad.Fat32Status == Fat32Status.MissingFile
            && result.PackageParse.Status == OsPackageStatus.MissingFile);
    }

    [HostTest("appmodel selection loader reports manifest scratch capacity", HostLane.Fast, 5)]
    private static bool TestReportsManifestScratchCapacity()
    {
        byte[] manifest = ValidManifest(AppPath);
        byte[] package = ValidPackage(AppPath);
        byte[] disk = CreateDiskImage(manifest, package, hasEsp: true, hasPackage: true);
        SelectionScratch scratch = new(manifestBufferLength: manifest.Length - 1);

        NativeAppSelectionLoadResult result = Load(disk, scratch, NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(!result.Succeeded
            && result.Status == NativeAppSelectionLoadStatus.ManifestLoadFailed
            && result.ManifestLoad.Status == NativeManifestLoadStatus.Fat32ReadFailed
            && result.ManifestLoad.Fat32Status == Fat32Status.FileDestinationTooSmall
            && result.ManifestLoad.ManifestBytesRequired == manifest.Length
            && result.ManifestLoad.ManifestBufferBytesAvailable == scratch.ManifestBuffer.Length
            && result.PackageLoad.Status == OsPackageLoadStatus.Success);
    }

    [HostTest("appmodel selection loader reports package scratch capacity", HostLane.Fast, 7)]
    private static bool TestReportsPackageScratchCapacity()
    {
        byte[] manifest = ValidManifest(AppPath);
        byte[] package = ValidPackage(AppPath);
        byte[] disk = CreateDiskImage(manifest, package, hasEsp: true, hasPackage: true);
        SelectionScratch scratch = new(packageBufferLength: package.Length - 1);

        NativeAppSelectionLoadResult result = Load(disk, scratch, NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(!result.Succeeded
            && result.Status == NativeAppSelectionLoadStatus.PackageLoadFailed
            && result.ManifestLoad.Succeeded
            && result.PackageLoad.Status == OsPackageLoadStatus.Fat32ReadFailed
            && result.PackageLoad.Fat32Status == Fat32Status.FileDestinationTooSmall
            && result.PackageLoad.PackageBytesRequired == package.Length
            && result.PackageLoad.PackageBufferBytesAvailable == scratch.PackageBuffer.Length
            && result.PackageParse.Status == OsPackageStatus.MissingFile);
    }

    [HostTest("appmodel selection loader rejects launch capability", HostLane.Fast, 8)]
    private static bool TestRejectsLaunchCapability()
    {
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath), hasEsp: true, hasPackage: true);
        SelectionScratch scratch = new();

        NativeAppSelectionLoadResult result = Load(disk, scratch, NativeAppLaunchRuntimeCapabilities.PackageStorage);

        return HostCheck.Result(!result.Succeeded
            && result.Status == NativeAppSelectionLoadStatus.LaunchPlanFailed
            && result.ManifestLoad.Succeeded
            && result.PackageLoad.Succeeded
            && result.PackageParse.Succeeded
            && result.LaunchPlan.Status == NativeAppLaunchStatus.MissingRuntimeCapabilities
            && result.LaunchPlan.MissingRuntimeCapabilities == NativeAppLaunchRuntimeCapabilities.Network);
    }

    [HostTest("appmodel selection loader rejects descriptor failure", HostLane.Fast, 9)]
    private static bool TestRejectsDescriptorFailure()
    {
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage("apps/other.bin"), hasEsp: true, hasPackage: true);
        SelectionScratch scratch = new();

        NativeAppSelectionLoadResult result = Load(disk, scratch, NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(!result.Succeeded
            && result.Status == NativeAppSelectionLoadStatus.LaunchDescriptorFailed
            && result.ManifestLoad.Succeeded
            && result.PackageLoad.Succeeded
            && result.PackageParse.Succeeded
            && result.LaunchPlan.Succeeded
            && result.Descriptor.Status == NativeAppLaunchDescriptorStatus.PackageFileMissing
            && result.Descriptor.PackageFileStatus == OsPackageFileSystemStatus.MissingFile);
    }

    [HostTest("appmodel selection loader default result is not success", HostLane.Fast, 10)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppSelectionLoadResult result = default;

        return HostCheck.Result(!result.Succeeded
            && result.Status == NativeAppSelectionLoadStatus.Success);
    }

    private static NativeAppSelectionLoadResult Load(
        byte[] disk,
        SelectionScratch scratch,
        NativeAppLaunchRuntimeCapabilities runtimeCapabilities)
    {
        return NativeAppSelectionLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            scratch.PrimaryHeader,
            scratch.PartitionEntries,
            scratch.Partitions,
            scratch.ManifestBuffer,
            scratch.PackageBuffer,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            runtimeCapabilities);
    }

    private static byte[] CreateDiskImage(
        byte[] manifest,
        byte[] package,
        bool hasEsp,
        bool hasPackage)
    {
        byte[] disk = new byte[checked((int)(DiskBlockCount * SectorSize))];
        WriteGpt(disk, hasEsp);
        WriteFat32Volume(disk, checked((int)(PartitionStartLba * SectorSize)), manifest, package, hasPackage);
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
        byte[] manifest,
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
        WriteDirectoryEntry(disk, rootDirectory, "OS         ", Fat32Parser.DirectoryAttribute, OsDirectoryCluster, 0);
        disk[rootDirectory + 32] = 0x00;

        int osDirectory = partitionOffset + ClusterOffset(OsDirectoryCluster);
        WriteDirectoryEntry(disk, osDirectory, ".          ", Fat32Parser.DirectoryAttribute, OsDirectoryCluster, 0);
        WriteDirectoryEntry(disk, osDirectory + 32, "..         ", Fat32Parser.DirectoryAttribute, RootCluster, 0);
        WriteLongFileNameEntry(disk, osDirectory + 64, "MANIFEST.JSON", "MANIFE~1JSO");
        WriteDirectoryEntry(disk, osDirectory + 96, "MANIFE~1JSO", 0x20, ManifestCluster, (uint)manifest.Length);

        if (hasPackage)
        {
            WriteDirectoryEntry(disk, osDirectory + 128, "APP     PKG", 0x20, PackageCluster, (uint)package.Length);
            disk[osDirectory + 160] = 0x00;
        }
        else
        {
            disk[osDirectory + 128] = 0x00;
        }

        manifest.CopyTo(disk.AsSpan(partitionOffset + ClusterOffset(ManifestCluster)));
        if (hasPackage)
        {
            package.CopyTo(disk.AsSpan(partitionOffset + ClusterOffset(PackageCluster)));
        }
    }

    private static int ClusterOffset(uint cluster)
    {
        ulong lba = RootDirectoryLba + (cluster - RootCluster);
        return checked((int)(lba * SectorSize));
    }

    private static byte[] ValidManifest(string appPath)
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.kind=package\n" +
            "app.path=" + appPath + "\n" +
            "runtime.kind=native-aot\n" +
            "args=--port=8080\n" +
            "network=dhcp\n" +
            "log=serial\n");
    }

    private static byte[] MalformedManifest()
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.kind=package\n" +
            "app.path=" + AppPath + "\n" +
            "runtime.kind=native-aot\n" +
            "args=--port=8080\n" +
            "network=automatic\n" +
            "log=serial\n");
    }

    private static byte[] ValidPackage(string appPath)
    {
        const int tableOffset = OsPackageParser.HeaderSize;
        const int tableLength = OsPackageParser.FileTableEntrySize;
        int pathOffset = tableOffset + tableLength;
        int contentOffset = pathOffset + appPath.Length;
        int packageLength = contentOffset + AppContentLength;

        byte[] package = new byte[packageLength];
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
        WriteUInt16(package, tableOffset + 4, (ushort)appPath.Length);
        WriteUInt16(package, tableOffset + 6, 0);
        WriteUInt64(package, tableOffset + 8, (ulong)contentOffset);
        WriteUInt64(package, tableOffset + 16, (ulong)AppContentLength);
        WriteUInt64(package, tableOffset + 24, 0);
        WriteAscii(package, pathOffset, appPath);
        WritePayloadHeader(package, contentOffset);

        for (int index = NativeAppPayloadHeader.HeaderSize; index < AppContentLength; index++)
        {
            package[contentOffset + index] = (byte)(0x41 + (index & 0x0F));
        }

        return package;
    }

    private static void WritePayloadHeader(byte[] package, int offset)
    {
        package[offset] = (byte)'O';
        package[offset + 1] = (byte)'S';
        package[offset + 2] = (byte)'A';
        package[offset + 3] = (byte)'P';
        package[offset + 4] = (byte)'P';
        package[offset + 5] = (byte)'v';
        package[offset + 6] = (byte)'0';
        package[offset + 7] = 0;
        WriteUInt16(package, offset + 8, NativeAppPayloadHeader.SupportedVersion);
        WriteUInt16(package, offset + 10, NativeAppPayloadHeader.HeaderSize);
        WriteUInt32(package, offset + 12, 0);
        WriteUInt64(package, offset + 16, NativeAppPayloadHeader.HeaderSize);
        WriteUInt64(package, offset + 24, 0);
    }

    private static bool FieldEquals(byte[] source, NativeManifestField field, string expected)
    {
        if (!field.TryGetSlice(source, out ReadOnlySpan<byte> value) || value.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
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

    private static void WriteLongFileNameEntry(
        byte[] image,
        int offset,
        string fileName,
        string rawShortName)
    {
        image[offset] = 0x41;
        image[offset + 11] = 0x0F;
        image[offset + 12] = 0;
        image[offset + 13] = ComputeLongFileNameChecksum(rawShortName);
        WriteUInt16(image, offset + 26, 0);

        int fileNameIndex = 0;
        bool wroteTerminator = false;
        WriteLongFileNameChars(image, offset + 1, 5, fileName, ref fileNameIndex, ref wroteTerminator);
        WriteLongFileNameChars(image, offset + 14, 6, fileName, ref fileNameIndex, ref wroteTerminator);
        WriteLongFileNameChars(image, offset + 28, 2, fileName, ref fileNameIndex, ref wroteTerminator);
    }

    private static void WriteLongFileNameChars(
        byte[] image,
        int offset,
        int count,
        string fileName,
        ref int fileNameIndex,
        ref bool wroteTerminator)
    {
        for (int i = 0; i < count; i++)
        {
            ushort value;
            if (fileNameIndex < fileName.Length)
            {
                value = (ushort)fileName[fileNameIndex++];
            }
            else if (!wroteTerminator)
            {
                value = 0;
                wroteTerminator = true;
            }
            else
            {
                value = 0xFFFF;
            }

            WriteUInt16(image, offset + i * 2, value);
        }
    }

    private static byte ComputeLongFileNameChecksum(string rawShortName)
    {
        byte sum = 0;
        for (int index = 0; index < rawShortName.Length; index++)
        {
            sum = (byte)(((sum & 1) == 1 ? 0x80 : 0) + (sum >> 1) + (byte)rawShortName[index]);
        }

        return sum;
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

    private static void WriteAscii(byte[] image, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            image[offset + index] = (byte)value[index];
        }
    }

    private static void WriteUtf16Name(byte[] image, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            WriteUInt16(image, offset + index * 2, (ushort)value[index]);
        }
    }

    private static void WriteGuid(byte[] image, int offset, Guid value)
    {
        if (!value.TryWriteBytes(image.AsSpan(offset, 16)))
        {
            throw new InvalidOperationException("Could not write test GUID.");
        }
    }

    private static void WriteUInt16(byte[] image, int offset, ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset, 2), value);
    }

    private static void WriteUInt32(byte[] image, int offset, int value)
    {
        WriteUInt32(image, offset, (uint)value);
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

    private sealed class SelectionScratch
    {
        public SelectionScratch(
            int manifestBufferLength = NativeManifestParser.MaximumInputLength,
            int packageBufferLength = 1024)
        {
            ManifestBuffer = new byte[manifestBufferLength];
            PackageBuffer = new byte[packageBufferLength];
        }

        public byte[] PrimaryHeader { get; } = new byte[SectorSize];

        public byte[] PartitionEntries { get; } = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];

        public GptPartitionDescriptor[] Partitions { get; } = new GptPartitionDescriptor[2];

        public byte[] ManifestBuffer { get; }

        public byte[] PackageBuffer { get; }
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
