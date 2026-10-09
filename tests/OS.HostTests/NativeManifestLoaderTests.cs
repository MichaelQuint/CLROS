using System.Runtime.CompilerServices;
using System.Text;
using OS.Manifest;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Storage;

internal static class NativeManifestLoaderTests
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
    private const ulong RootDirectoryLba = ReservedSectorCount + (FatCount * FatSizeSectors);
    private const ulong PartitionEntryLba = 2;
    private const ulong BackupPartitionEntryLba = DiskBlockCount - 33;
    private const ulong BackupHeaderLba = DiskBlockCount - 1;
    private const uint PartitionEntryCount = 128;
    private const uint PartitionEntrySize = 128;
    private const ulong FirstUsableLba = 34;
    private const ulong LastUsableLba = 220;

    private static readonly Guid DiskGuid = new Guid("12121212-3434-5656-7878-909090909090");
    private static readonly Guid EspGuid = new Guid("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    private static readonly Guid UniquePartitionGuid = new Guid("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb");

    [HostTest("manifest loader reads native manifest from gpt esp", HostLane.Fast, 0)]
    private static bool TestReadsNativeManifestFromGptEsp()
    {
        byte[] manifest = ValidManifest();
        byte[] disk = CreateDiskImage(manifest, hasEsp: true);
        byte[] buffer = new byte[NativeManifestParser.MaximumInputLength];

        NativeManifestLoadResult result = NativeManifestLoader.LoadFromPrimaryGptEsp(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            buffer);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == NativeManifestLoadStatus.Success
            && result.GptStatus == GptStatus.Success
            && result.Fat32Status == Fat32Status.Success
            && result.ManifestStatus == NativeManifestParseStatus.Success
            && result.BytesRead == manifest.Length
            && FieldEquals(buffer, result.Manifest.AppName, "hello.app")
            && FieldEquals(buffer, result.Manifest.AppPath, "apps/hello.bin")
            && result.Manifest.AppKind == NativeManifestAppKind.Package
            && result.Manifest.RuntimeKind == NativeManifestRuntimeKind.NativeAot
            && result.Manifest.NetworkMode == NativeManifestNetworkMode.Dhcp
            && result.Manifest.LogMode == NativeManifestLogMode.Serial);
    }

    [HostTest("manifest loader rejects missing esp", HostLane.Fast, 9)]
    private static bool TestRejectsMissingEsp()
    {
        byte[] disk = CreateDiskImage(ValidManifest(), hasEsp: false);
        byte[] buffer = new byte[NativeManifestParser.MaximumInputLength];

        NativeManifestLoadResult result = NativeManifestLoader.LoadFromPrimaryGptEsp(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == NativeManifestLoadStatus.GptFailed
            && result.GptStatus == GptStatus.EfiSystemPartitionNotFound);
    }

    [HostTest("manifest loader rejects malformed native manifest", HostLane.Fast, 10)]
    private static bool TestRejectsMalformedNativeManifest()
    {
        byte[] disk = CreateDiskImage(MalformedManifest(), hasEsp: true);
        byte[] buffer = new byte[NativeManifestParser.MaximumInputLength];

        NativeManifestLoadResult result = NativeManifestLoader.LoadFromPrimaryGptEsp(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == NativeManifestLoadStatus.ManifestParseFailed
            && result.Fat32Status == Fat32Status.Success
            && result.ManifestStatus == NativeManifestParseStatus.InvalidNetworkMode);
    }

    [HostTest("manifest loader default result is not success", HostLane.Fast, 11)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeManifestLoadResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeManifestLoadStatus.Success
            && result.GptStatus == GptStatus.Success
            && result.GptTableStatus == GptPartitionTableStatus.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Fat32Status == Fat32Status.Success
            && result.ManifestStatus == NativeManifestParseStatus.Success
            && result.BytesRead == 0
            && result.ManifestBytesRequired == 0
            && result.ManifestBufferBytesAvailable == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeManifestLoadResult>());
    }

    [HostTest("manifest loader caller-buffer gpt reads native manifest", HostLane.Fast, 1)]
    private static bool TestCallerBufferGptReadsNativeManifest()
    {
        byte[] manifest = ValidManifest();
        byte[] disk = CreateDiskImage(manifest, hasEsp: true);
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[NativeManifestParser.MaximumInputLength];

        NativeManifestLoadResult result = NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(result.Succeeded
            && result.Status == NativeManifestLoadStatus.Success
            && result.GptStatus == GptStatus.Success
            && result.GptTableStatus == GptPartitionTableStatus.Success
            && result.GptCrcStatus == GptPartitionTableCrcStatus.Validated
            && result.GptPartitionEntryIndex == 0
            && result.GptPartitionEntryBytesRequired == PartitionEntryCount * PartitionEntrySize
            && result.GptPartitionEntrySectorBytesRequired == PartitionEntryCount * PartitionEntrySize
            && result.ExpectedGptHeaderCrc32 == result.ComputedGptHeaderCrc32
            && result.ExpectedGptPartitionEntryArrayCrc32 == result.ComputedGptPartitionEntryArrayCrc32
            && result.ManifestStatus == NativeManifestParseStatus.Success
            && result.BytesRead == manifest.Length
            && FieldEquals(buffer, result.Manifest.AppName, "hello.app"));
    }

    [HostTest("manifest loader caller-buffer reports primary gpt source", HostLane.Fast, 2)]
    private static bool TestCallerBufferReportsPrimaryGptSource()
    {
        byte[] manifest = ValidManifest();
        byte[] disk = CreateDiskImage(manifest, hasEsp: true);
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[NativeManifestParser.MaximumInputLength];

        NativeManifestLoadResult result = NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(result.Succeeded
            && result.GptTableSource == GptPartitionTableSource.Primary
            && result.ManifestStatus == NativeManifestParseStatus.Success);
    }

    [HostTest("manifest loader caller-buffer gpt rejects crc mismatch", HostLane.Fast, 5)]
    private static bool TestCallerBufferGptRejectsCrcMismatch()
    {
        byte[] disk = CreateDiskImage(ValidManifest(), hasEsp: true);
        disk[checked((int)(PartitionEntryLba * SectorSize)) + GptPartitionDescriptor.NameOffsetWithinEntryBytes] ^= 0x20;
        disk[checked((int)(BackupPartitionEntryLba * SectorSize)) + GptPartitionDescriptor.NameOffsetWithinEntryBytes] ^= 0x20;
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[NativeManifestParser.MaximumInputLength];

        NativeManifestLoadResult result = NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.Status == NativeManifestLoadStatus.GptFailed
            && result.GptTableStatus == GptPartitionTableStatus.PartitionEntryArrayCrcMismatch
            && result.GptCrcStatus == GptPartitionTableCrcStatus.PartitionEntryArrayMismatch
            && result.ExpectedGptPartitionEntryArrayCrc32 != result.ComputedGptPartitionEntryArrayCrc32);
    }

    [HostTest("manifest loader caller-buffer gpt falls back to backup", HostLane.Fast, 3)]
    private static bool TestCallerBufferGptFallsBackToBackup()
    {
        byte[] manifest = ValidManifest();
        byte[] disk = CreateDiskImage(manifest, hasEsp: true);
        disk[checked((int)SectorSize) + 20] = 0x5A;
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[NativeManifestParser.MaximumInputLength];

        NativeManifestLoadResult result = NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(result.Succeeded
            && result.Status == NativeManifestLoadStatus.Success
            && result.GptStatus == GptStatus.Success
            && result.GptTableStatus == GptPartitionTableStatus.Success
            && result.GptCrcStatus == GptPartitionTableCrcStatus.Validated
            && ReadUInt64(primaryHeader, 24) == BackupHeaderLba
            && ReadUInt64(primaryHeader, 72) == BackupPartitionEntryLba
            && result.ManifestStatus == NativeManifestParseStatus.Success
            && result.BytesRead == manifest.Length
            && FieldEquals(buffer, result.Manifest.AppName, "hello.app"));
    }

    [HostTest("manifest loader caller-buffer reports backup gpt source", HostLane.Fast, 4)]
    private static bool TestCallerBufferReportsBackupGptSource()
    {
        byte[] manifest = ValidManifest();
        byte[] disk = CreateDiskImage(manifest, hasEsp: true);
        disk[checked((int)SectorSize) + 20] = 0x5A;
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[NativeManifestParser.MaximumInputLength];

        NativeManifestLoadResult result = NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(result.Succeeded
            && result.GptTableSource == GptPartitionTableSource.Backup
            && result.ManifestStatus == NativeManifestParseStatus.Success);
    }

    [HostTest("manifest loader caller-buffer reports gpt header capacity", HostLane.Fast, 6)]
    private static bool TestCallerBufferReportsGptHeaderCapacity()
    {
        byte[] disk = CreateDiskImage(ValidManifest(), hasEsp: true);
        byte[] primaryHeader = new byte[SectorSize - 1];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[NativeManifestParser.MaximumInputLength];

        NativeManifestLoadResult result = NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.Status == NativeManifestLoadStatus.GptFailed
            && result.GptStatus == GptStatus.PrimaryHeaderShortRead
            && result.BlockStatus == BlockDeviceStatus.DestinationTooSmall
            && result.GptHeaderBytesRequired == SectorSize
            && result.GptHeaderBufferBytesAvailable == checked((ulong)primaryHeader.Length)
            && result.GptPartitionEntryBufferBytesAvailable == checked((ulong)partitionEntries.Length));
    }

    [HostTest("manifest loader caller-buffer reports gpt entry buffer capacity", HostLane.Fast, 7)]
    private static bool TestCallerBufferReportsGptEntryBufferCapacity()
    {
        byte[] disk = CreateDiskImage(ValidManifest(), hasEsp: true);
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[SectorSize];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[NativeManifestParser.MaximumInputLength];

        NativeManifestLoadResult result = NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.Status == NativeManifestLoadStatus.GptFailed
            && result.GptStatus == GptStatus.PartitionEntryShortRead
            && result.GptTableStatus == GptPartitionTableStatus.PartitionEntryBufferTooSmall
            && result.BlockStatus == BlockDeviceStatus.DestinationTooSmall
            && result.GptHeaderBytesRequired == SectorSize
            && result.GptHeaderBufferBytesAvailable == SectorSize
            && result.GptPartitionEntryBytesRequired == PartitionEntryCount * PartitionEntrySize
            && result.GptPartitionEntrySectorBytesRequired == PartitionEntryCount * PartitionEntrySize
            && result.GptPartitionEntryBufferBytesAvailable == checked((ulong)partitionEntries.Length));
    }

    [HostTest("manifest loader caller-buffer reports manifest buffer capacity", HostLane.Fast, 8)]
    private static bool TestCallerBufferReportsManifestBufferCapacity()
    {
        byte[] manifest = ValidManifest();
        byte[] disk = CreateDiskImage(manifest, hasEsp: true);
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] descriptors = new GptPartitionDescriptor[2];
        byte[] buffer = new byte[manifest.Length - 1];

        NativeManifestLoadResult result = NativeManifestLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            MemoryBlockDevice.FromImage(disk, SectorSize),
            primaryHeader,
            partitionEntries,
            descriptors,
            buffer);

        return HostCheck.Result(!result.Succeeded
            && result.Status == NativeManifestLoadStatus.Fat32ReadFailed
            && result.Fat32Status == Fat32Status.FileDestinationTooSmall
            && result.ManifestBytesRequired == manifest.Length
            && result.ManifestBufferBytesAvailable == buffer.Length
            && result.BytesRead == 0
            && result.GptCrcStatus == GptPartitionTableCrcStatus.Validated);
    }

    private static byte[] CreateDiskImage(byte[] manifest, bool hasEsp)
    {
        byte[] disk = new byte[checked((int)(DiskBlockCount * SectorSize))];
        WriteGpt(disk, hasEsp);
        WriteFat32Volume(disk, checked((int)(PartitionStartLba * SectorSize)), manifest);
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
            WritePartitionEntry(disk, PartitionEntryLba, 0, EspGuid, UniquePartitionGuid, PartitionStartLba, PartitionStartLba + PartitionBlockCount - 1, "ESP");
            WritePartitionEntry(disk, BackupPartitionEntryLba, 0, EspGuid, UniquePartitionGuid, PartitionStartLba, PartitionStartLba + PartitionBlockCount - 1, "ESP");
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
        disk[headerOffset] = (byte)'E';
        disk[headerOffset + 1] = (byte)'F';
        disk[headerOffset + 2] = (byte)'I';
        disk[headerOffset + 3] = (byte)' ';
        disk[headerOffset + 4] = (byte)'P';
        disk[headerOffset + 5] = (byte)'A';
        disk[headerOffset + 6] = (byte)'R';
        disk[headerOffset + 7] = (byte)'T';

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
        uint entryIndex,
        Guid partitionTypeGuid,
        Guid uniquePartitionGuid,
        ulong firstLba,
        ulong lastLba,
        string name)
    {
        ulong byteOffset = (partitionEntryLba * SectorSize) + ((ulong)entryIndex * PartitionEntrySize);
        int entryOffset = checked((int)byteOffset);
        WriteGuid(disk, entryOffset, partitionTypeGuid);
        WriteGuid(disk, entryOffset + 16, uniquePartitionGuid);
        WriteUInt64(disk, entryOffset + 32, firstLba);
        WriteUInt64(disk, entryOffset + 40, lastLba);
        WriteUtf16Name(disk, entryOffset + 56, name);
    }

    private static void WriteFat32Volume(byte[] disk, int partitionOffset, byte[] manifest)
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
        WriteLongFileNameEntry(disk, osDirectory + 64, "MANIFEST.JSON", "MANIFE~1JSO");
        WriteDirectoryEntry(
            disk,
            osDirectory + 96,
            "MANIFE~1JSO",
            attributes: 0x20,
            firstCluster: ManifestCluster,
            fileSize: (uint)manifest.Length);
        disk[osDirectory + 128] = 0x00;

        int manifestOffset = partitionOffset + ClusterOffset(ManifestCluster);
        manifest.CopyTo(disk.AsSpan(manifestOffset));
    }

    private static int ClusterOffset(uint cluster)
    {
        ulong lba = RootDirectoryLba + (cluster - RootCluster);
        return checked((int)(lba * SectorSize));
    }

    private static byte[] ValidManifest()
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=dhcp\n" +
            "log=serial\n");
    }

    private static byte[] MalformedManifest()
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.path=apps/hello.bin\n" +
            "args=\n" +
            "app.kind=package\n" +
            "runtime.kind=native-aot\n" +
            "network=automatic\n" +
            "log=serial\n");
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
        for (int i = 0; i < rawShortName.Length; i++)
        {
            sum = (byte)(((sum & 1) == 1 ? 0x80 : 0) + (sum >> 1) + (byte)rawShortName[i]);
        }

        return sum;
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

    private static void WriteUInt16(byte[] image, int offset, ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset, 2), value);
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
