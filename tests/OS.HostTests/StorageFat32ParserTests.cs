using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static partial class StorageFat32ParserTests
{
    private const uint SectorSize = 512;
    private const ulong PartitionStartLba = 8;
    private const ushort ReservedSectorCount = 32;
    private const byte FatCount = 2;
    private const uint FatSizeSectors = 8;
    private const uint RootCluster = 2;
    private const uint TotalSectors = 256;
    private const ulong RootDirectoryLba = PartitionStartLba + ReservedSectorCount + (FatCount * FatSizeSectors);
    private const uint KernelCluster = 5;
    private const uint EfiCluster = 14;
    private const uint ManifestCluster = 15;
    private const uint OsDirectoryCluster = 16;
    private const uint ChainedFileFirstCluster = 20;
    private const uint ChainedFileSecondCluster = 22;
    private const uint ChainedFileThirdCluster = 24;
    private const uint ChainedRootSecondCluster = 28;
    private const uint ChainedDirectorySecondCluster = 30;
    private const uint ChainedRootLateFileCluster = 31;
    private const uint ChainedDirectoryLateFileCluster = 32;
    private const uint KernelFileSize = 4097;
    private const uint ManifestFileSize = 128;
    private const uint ChainedFileSize = SectorSize * 3;
    private const uint Fat32EndOfChain = 0x0FFFFFFF;

    [HostTest("storage fat32 result defaults are not success", HostLane.Fast, 32)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        Fat32BpbParseResult bpb = default;
        Fat32DirectoryReadResult directory = default;
        Fat32FileLookupResult lookup = default;
        Fat32FileReadResult read = default;

        return HostCheck.Result(!bpb.IsInitialized
            && !bpb.Succeeded
            && bpb.Status == Fat32Status.Success
            && bpb.BlockStatus == BlockDeviceStatus.Success
            && bpb.Bpb.BytesPerSector == 0
            && bpb.Bpb.ClusterSizeBytes == 0
            && !directory.IsInitialized
            && !directory.Succeeded
            && directory.Status == Fat32Status.Success
            && directory.BlockStatus == BlockDeviceStatus.Success
            && directory.Bpb.BytesPerSector == 0
            && directory.EntriesRead == 0
            && !directory.ReachedEndMarker
            && !lookup.IsInitialized
            && !lookup.Succeeded
            && lookup.Status == Fat32Status.Success
            && lookup.BlockStatus == BlockDeviceStatus.Success
            && lookup.Bpb.BytesPerSector == 0
            && lookup.Entry.ShortName is null
            && lookup.Entry.FirstCluster == 0
            && lookup.Entry.FileSize == 0
            && !read.IsInitialized
            && !read.Succeeded
            && read.Status == Fat32Status.Success
            && read.BlockStatus == BlockDeviceStatus.Success
            && read.Bpb.BytesPerSector == 0
            && read.Entry.ShortName is null
            && read.FileSize == 0
            && read.BytesRead == 0
            && !read.AssumedContiguousClusters
            && !RuntimeHelpers.IsReferenceOrContainsReferences<Fat32BpbParseResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<Fat32DirectoryReadResult>());
    }

    [HostTest("storage fat32 uses shared memory short-name helper", HostLane.Fast, 33)]
    private static bool TestUsesSharedMemoryShortNameHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Storage", "Fat32Parser.cs"));

        return HostCheck.Result(source.Contains(
                "return OS.Std.MemoryExtensions.SequenceEqual(actual, expected);",
                StringComparison.Ordinal)
            && !source.Contains("if (actual[i] != expected[i])", StringComparison.Ordinal));
    }

    [HostTest("storage fat32 uses shared ascii long-name case helper", HostLane.Fast, 34)]
    private static bool TestUsesSharedAsciiLongNameCaseHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Storage", "Fat32Parser.cs"));

        return HostCheck.Result(source.Contains("OS.Std.Ascii.ToUpperInvariant", StringComparison.Ordinal)
            && !source.Contains("private static char ToUpperAscii", StringComparison.Ordinal));
    }

    [HostTest("storage fat32 uses shared ascii trailing-space helper", HostLane.Fast, 35)]
    private static bool TestUsesSharedAsciiTrailingSpaceHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Storage", "Fat32Parser.cs"));

        return HostCheck.Result(source.Contains("AsciiString.TrimTrailingSpace(bytes[..8])", StringComparison.Ordinal)
            && source.Contains("AsciiString.TrimTrailingSpace(bytes.Slice(8, 3))", StringComparison.Ordinal)
            && !source.Contains("private static int TrimTrailingSpaces", StringComparison.Ordinal));
    }

    [HostTest("storage fat32 final block copy uses shared buffer operations", HostLane.Fast, 36)]
    private static bool TestFinalBlockCopyUsesSharedBufferOperations()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Storage", "Fat32Parser.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperationResult copy = BufferOperations.MemoryCopy(", StringComparison.Ordinal)
            && source.Contains("finalBlock[..bytesRemaining]", StringComparison.Ordinal)
            && source.Contains("destination.Slice(destinationOffset, bytesRemaining)", StringComparison.Ordinal)
            && source.Contains("Fat32Status.FileDestinationTooSmall", StringComparison.Ordinal)
            && source.Contains("if (!copy.Succeeded)", StringComparison.Ordinal)
            && !source.Contains(
                "finalBlock[..bytesRemaining].CopyTo(destination.Slice(destinationOffset, bytesRemaining))",
                StringComparison.Ordinal));
    }

    [HostTest("storage fat32 parses valid BPB", HostLane.Fast, 0)]
    private static bool TestValidBpbParse()
    {
        byte[] image = CreateValidFat32Image();

        Fat32BpbParseResult result = Fat32Parser.ReadBpb(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba);

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Bpb.PartitionStartLba == PartitionStartLba
            && result.Bpb.BytesPerSector == SectorSize
            && result.Bpb.SectorsPerCluster == 1
            && result.Bpb.ReservedSectorCount == ReservedSectorCount
            && result.Bpb.FatCount == FatCount
            && result.Bpb.FatSizeSectors == FatSizeSectors
            && result.Bpb.RootCluster == RootCluster
            && result.Bpb.TotalSectors == TotalSectors
            && result.Bpb.FirstFatLba == PartitionStartLba + ReservedSectorCount
            && result.Bpb.FirstDataLba == RootDirectoryLba
            && result.Bpb.RootDirectoryFirstLba == RootDirectoryLba);
    }

    [HostTest("storage fat32 rejects bad boot signature", HostLane.Fast, 1)]
    private static bool TestBadBootSignature()
    {
        byte[] image = CreateValidFat32Image();
        int bootSector = checked((int)(PartitionStartLba * SectorSize));
        image[bootSector + 510] = 0;

        Fat32BpbParseResult result = Fat32Parser.ReadBpb(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba);

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.InvalidBootSectorSignature);
    }

    [HostTest("storage fat32 rejects active FAT before stale file bytes", HostLane.Fast, 37)]
    private static bool TestRejectsActiveFatBeforeStaleBytes()
    {
        var device = CreateDivergentFatFixture();
        WriteUInt16(device.BootSector, 40, 0x81);
        byte[] output = new byte[SectorSize * 3];
        output.AsSpan().Fill(0xCC);

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(device, PartitionStartLba, "CHAIN.BIN", output);

        return HostCheck.Result(!result.Succeeded && result.Status == Fat32Status.UnsupportedGeometry
            && result.BytesRead == 0 && device.ReadCount == 1 && output.AsSpan().IndexOfAnyExcept((byte)0xCC) < 0);
    }

    [HostTest("storage fat32 rejects unsupported FAT flags and version", HostLane.Fast, 38)]
    private static bool TestRejectsUnsupportedFatModesAndVersion()
    {
        ushort[] flags = [0x80, 0x81, 0x82, 1, 0x100];
        foreach (ushort value in flags)
        {
            var device = new SparseFat32BlockDevice(65525);
            WriteUInt16(device.BootSector, 40, value);
            Fat32BpbParseResult result = Fat32Parser.ReadBpb(device, PartitionStartLba);
            if (result.Succeeded || result.Status != Fat32Status.UnsupportedGeometry || device.ReadCount != 1) return HostCheck.Result(false);
        }

        foreach (ushort value in new ushort[] { 1, 0x100, 0xFFFF })
        {
            var device = new SparseFat32BlockDevice(65525);
            WriteUInt16(device.BootSector, 42, value);
            Fat32BpbParseResult result = Fat32Parser.ReadBpb(device, PartitionStartLba);
            if (result.Succeeded || result.Status != Fat32Status.UnsupportedGeometry || device.ReadCount != 1) return HostCheck.Result(false);
        }

        return HostCheck.Result(true);
    }

    [HostTest("storage fat32 mirrored mode retains distinct cluster bytes", HostLane.Fast, 39)]
    private static bool TestMirroredModeDistinctClusterContents()
    {
        var device = CreateDivergentFatFixture();
        device.WriteFatEntry(1, 20, 22);
        device.WriteFatEntry(1, 22, 24);
        device.WriteFatEntry(1, 24, Fat32EndOfChain);
        byte[] output = new byte[SectorSize * 3];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(device, PartitionStartLba, "CHAIN.BIN", output);

        return HostCheck.Result(result.Succeeded && result.BytesRead == output.Length
            && output.AsSpan(0, 512).IndexOfAnyExcept((byte)0xA1) < 0
            && output.AsSpan(512, 512).IndexOfAnyExcept((byte)0xB2) < 0
            && output.AsSpan(1024, 512).IndexOfAnyExcept((byte)0xC3) < 0);
    }

    [HostTest("storage fat32 rejects unsupported sector size", HostLane.Fast, 2)]
    private static bool TestUnsupportedSectorSize()
    {
        byte[] image = CreateValidFat32Image();
        int bootSector = checked((int)(PartitionStartLba * SectorSize));
        WriteUInt16(image, bootSector + 11, 1024);

        Fat32BpbParseResult result = Fat32Parser.ReadBpb(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba);

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.UnsupportedSectorSize);
    }

    [HostTest("storage fat32 rejects invalid sectors per cluster", HostLane.Fast, 3)]
    private static bool TestInvalidSectorsPerCluster()
    {
        byte[] image = CreateValidFat32Image();
        int bootSector = checked((int)(PartitionStartLba * SectorSize));
        image[bootSector + 13] = 3;

        Fat32BpbParseResult result = Fat32Parser.ReadBpb(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba);

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.InvalidSectorsPerCluster);
    }

    [HostTest("storage fat32 rejects invalid FAT count", HostLane.Fast, 4)]
    private static bool TestInvalidFatCount()
    {
        byte[] image = CreateValidFat32Image();
        int bootSector = checked((int)(PartitionStartLba * SectorSize));
        image[bootSector + 16] = 0;

        Fat32BpbParseResult result = Fat32Parser.ReadBpb(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba);

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.InvalidFatCount);
    }

    [HostTest("storage fat32 enumerates root directory entries", HostLane.Fast, 5)]
    private static bool TestRootDirectoryEnumeration()
    {
        byte[] image = CreateValidFat32Image();
        Fat32DirectoryEntry[] entries = new Fat32DirectoryEntry[4];

        Fat32DirectoryReadResult result = Fat32Parser.ReadRootDirectoryEntries(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            entries);

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.EntriesRead == 2
            && result.ReachedEndMarker
            && entries[0].ShortName == "KERNEL.BIN"
            && !entries[0].IsDirectory
            && entries[0].FirstCluster == KernelCluster
            && entries[0].FileSize == KernelFileSize
            && entries[1].ShortName == "EFI"
            && entries[1].IsDirectory
            && entries[1].FirstCluster == EfiCluster);
    }

    [HostTest("storage fat32 reports missing root short-name file", HostLane.Fast, 6)]
    private static bool TestMissingRootFile()
    {
        byte[] image = CreateValidFat32Image();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "MISSING.BIN");

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.MissingFile);
    }

    [HostTest("storage fat32 finds root short-name file metadata", HostLane.Fast, 7)]
    private static bool TestFoundRootFileMetadata()
    {
        byte[] image = CreateValidFat32Image();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "kernel.bin");

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.Entry.ShortName == "KERNEL.BIN"
            && result.Entry.FirstCluster == KernelCluster
            && result.Entry.FileSize == KernelFileSize
            && !result.Entry.IsDirectory);
    }

    [HostTest("storage fat32 reads root short-name file content", HostLane.Fast, 8)]
    private static bool TestReadRootShortNameFileContent()
    {
        byte[] image = CreateValidFat32Image();
        byte[] destination = new byte[(int)KernelFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "kernel.bin",
            destination);

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "KERNEL.BIN"
            && result.Entry.FirstCluster == KernelCluster
            && result.FileSize == KernelFileSize
            && result.BytesRead == KernelFileSize
            && result.AssumedContiguousClusters
            && ContentMatches(destination, KernelFileSize, KernelContentByte));
    }

    [HostTest("storage fat32 reads chained file content", HostLane.Fast, 9)]
    private static bool TestReadChainedFileContent()
    {
        byte[] image = CreateFat32ImageWithChainedFile();
        byte[] destination = new byte[(int)ChainedFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "CHAINED.BIN",
            destination);

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "CHAINED.BIN"
            && result.Entry.FirstCluster == ChainedFileFirstCluster
            && result.FileSize == ChainedFileSize
            && result.BytesRead == ChainedFileSize
            && !result.AssumedContiguousClusters
            && ContentMatches(destination, ChainedFileSize, ChainedContentByte));
    }

    [HostTest("storage fat32 rejects chained file loop", HostLane.Fast, 10)]
    private static bool TestRejectsChainedFileLoop()
    {
        byte[] image = CreateFat32ImageWithChainedFile(loopChain: true);
        byte[] destination = new byte[(int)ChainedFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "CHAINED.BIN",
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.FileClusterChainLoop
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "CHAINED.BIN"
            && result.Entry.FirstCluster == ChainedFileFirstCluster
            && result.FileSize == ChainedFileSize
            && result.BytesRead == SectorSize * 2
            && !result.AssumedContiguousClusters);
    }

    [HostTest("storage fat32 rejects short chained file", HostLane.Fast, 11)]
    private static bool TestRejectsShortChainedFile()
    {
        byte[] image = CreateFat32ImageWithChainedFile(shortChain: true);
        byte[] destination = new byte[(int)ChainedFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "CHAINED.BIN",
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.FileClusterChainTooShort
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "CHAINED.BIN"
            && result.Entry.FirstCluster == ChainedFileFirstCluster
            && result.FileSize == ChainedFileSize
            && result.BytesRead == SectorSize * 2
            && !result.AssumedContiguousClusters);
    }

    [HostTest("storage fat32 reads chained root directory entries", HostLane.Fast, 12)]
    private static bool TestReadsChainedRootDirectoryEntries()
    {
        byte[] image = CreateFat32ImageWithChainedRootDirectory();
        Fat32DirectoryEntry[] entries = new Fat32DirectoryEntry[20];

        Fat32DirectoryReadResult result = Fat32Parser.ReadRootDirectoryEntries(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            entries);

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.EntriesRead == 17
            && result.ReachedEndMarker
            && entries[0].ShortName == "KERNEL.BIN"
            && entries[1].ShortName == "EFI"
            && entries[16].ShortName == "LATE.BIN"
            && entries[16].FirstCluster == ChainedRootLateFileCluster
            && entries[16].FileSize == 1);
    }

    [HostTest("storage fat32 finds chained root directory file metadata", HostLane.Fast, 13)]
    private static bool TestFindsChainedRootDirectoryFileMetadata()
    {
        byte[] image = CreateFat32ImageWithChainedRootDirectory();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "LATE.BIN");

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "LATE.BIN"
            && result.Entry.FirstCluster == ChainedRootLateFileCluster
            && result.Entry.FileSize == 1
            && !result.Entry.IsDirectory);
    }

    [HostTest("storage fat32 reads chained first-level directory entries", HostLane.Fast, 14)]
    private static bool TestReadsChainedFirstLevelDirectoryEntries()
    {
        byte[] image = CreateFat32ImageWithChainedOsDirectory();

        Fat32FileLookupResult result = Fat32Parser.FindFirstLevelDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/LATEOS.BIN");

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "LATEOS.BIN"
            && result.Entry.FirstCluster == ChainedDirectoryLateFileCluster
            && result.Entry.FileSize == 1
            && !result.Entry.IsDirectory);
    }

    [HostTest("storage fat32 rejects directory chain loop", HostLane.Fast, 15)]
    private static bool TestRejectsDirectoryChainLoop()
    {
        byte[] image = CreateFat32ImageWithChainedRootDirectory(loopChain: true, includeLateEntry: false);

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "MISSING.BIN");

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.DirectoryClusterChainLoop
            && result.BlockStatus == BlockDeviceStatus.Success);
    }

    [HostTest("storage fat32 reports short chained directory read", HostLane.Fast, 16)]
    private static bool TestReportsShortChainedDirectoryRead()
    {
        byte[] image = CreateFat32ImageWithChainedOsDirectory();

        Fat32FileLookupResult result = Fat32Parser.FindFirstLevelDirectoryEntry(
            new TruncatedDirectoryClusterBlockDevice(
                MemoryBlockDevice.FromImage(image, SectorSize),
                ChainedDirectorySecondCluster),
            PartitionStartLba,
            "OS/LATEOS.BIN");

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.DirectoryClusterShortRead
            && result.BlockStatus == BlockDeviceStatus.Success);
    }

    [HostTest("storage fat32 finds OS directory metadata", HostLane.Fast, 17)]
    private static bool TestFoundOsDirectoryMetadata()
    {
        byte[] image = CreateFat32ImageWithOsManifest();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectory(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "os");

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.Entry.ShortName == "OS"
            && result.Entry.FirstCluster == OsDirectoryCluster
            && result.Entry.FileSize == 0
            && result.Entry.IsDirectory);
    }

    [HostTest("storage fat32 finds OS/MANIFEST.JSON metadata", HostLane.Fast, 18)]
    private static bool TestFoundOsManifestMetadata()
    {
        byte[] image = CreateFat32ImageWithOsManifest();

        Fat32FileLookupResult result = Fat32Parser.FindFirstLevelDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/MANIFEST.JSON");

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.Entry.ShortName == "MANIFE~1.JSO"
            && result.Entry.FirstCluster == ManifestCluster
            && result.Entry.FileSize == ManifestFileSize
            && !result.Entry.IsDirectory);
    }

    [HostTest("storage fat32 rejects unsupported nested path", HostLane.Fast, 22)]
    private static bool TestRejectsUnsupportedNestedPath()
    {
        byte[] image = CreateFat32ImageWithOsManifest();

        Fat32FileLookupResult result = Fat32Parser.FindFirstLevelDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/CONFIG/MANIFEST.JSON");

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.UnsupportedNestedPath);
    }

    [HostTest("storage fat32 reads OS/MANIFEST.JSON content", HostLane.Fast, 19)]
    private static bool TestReadOsManifestContent()
    {
        byte[] image = CreateFat32ImageWithOsManifest();
        byte[] destination = new byte[(int)ManifestFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadFirstLevelFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/MANIFEST.JSON",
            destination);

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "MANIFE~1.JSO"
            && result.Entry.FirstCluster == ManifestCluster
            && result.FileSize == ManifestFileSize
            && result.BytesRead == ManifestFileSize
            && result.AssumedContiguousClusters
            && ContentMatches(destination, ManifestFileSize, ManifestContentByte));
    }

    [HostTest("storage fat32 rejects OS/MANIFEST.JSON destination too small", HostLane.Fast, 20)]
    private static bool TestOsManifestDestinationTooSmall()
    {
        byte[] image = CreateFat32ImageWithOsManifest();
        byte[] destination = new byte[(int)ManifestFileSize - 1];

        Fat32FileReadResult result = Fat32Parser.ReadFirstLevelFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/MANIFEST.JSON",
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.FileDestinationTooSmall
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "MANIFE~1.JSO"
            && result.Entry.FirstCluster == ManifestCluster
            && result.FileSize == ManifestFileSize
            && result.BytesRead == 0
            && !result.AssumedContiguousClusters);
    }

    [HostTest("storage fat32 rejects missing OS directory for content read", HostLane.Fast, 21)]
    private static bool TestReadOsManifestMissingDirectory()
    {
        byte[] image = CreateValidFat32Image();
        byte[] destination = new byte[(int)ManifestFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadFirstLevelFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/MANIFEST.JSON",
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.MissingDirectory
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.BytesRead == 0
            && !result.AssumedContiguousClusters);
    }

    [HostTest("storage fat32 finds MANIFEST.JSON root long-name file metadata", HostLane.Fast, 23)]
    private static bool TestFoundRootLongNameManifestMetadata()
    {
        byte[] image = CreateFat32ImageWithManifestLongName();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "manifest.json");

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.Entry.ShortName == "MANIFE~1.JSO"
            && result.Entry.FirstCluster == ManifestCluster
            && result.Entry.FileSize == ManifestFileSize
            && !result.Entry.IsDirectory);
    }

    [HostTest("storage fat32 reads MANIFEST.JSON root long-name file content", HostLane.Fast, 24)]
    private static bool TestReadRootLongNameManifestContent()
    {
        byte[] image = CreateFat32ImageWithManifestLongName();
        byte[] destination = new byte[(int)ManifestFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "MANIFEST.JSON",
            destination);

        return HostCheck.Result(result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "MANIFE~1.JSO"
            && result.Entry.FirstCluster == ManifestCluster
            && result.FileSize == ManifestFileSize
            && result.BytesRead == ManifestFileSize
            && result.AssumedContiguousClusters
            && ContentMatches(destination, ManifestFileSize, ManifestContentByte));
    }

    [HostTest("storage fat32 reports missing root long-name file", HostLane.Fast, 25)]
    private static bool TestMissingRootLongNameFile()
    {
        byte[] image = CreateValidFat32Image();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "MANIFEST.JSON");

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.MissingFile);
    }

    [HostTest("storage fat32 rejects root file destination too small", HostLane.Fast, 26)]
    private static bool TestRootFileDestinationTooSmall()
    {
        byte[] image = CreateValidFat32Image();
        byte[] destination = new byte[(int)KernelFileSize - 1];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "KERNEL.BIN",
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.FileDestinationTooSmall
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "KERNEL.BIN"
            && result.FileSize == KernelFileSize
            && result.BytesRead == 0
            && !result.AssumedContiguousClusters);
    }

    [HostTest("storage fat32 rejects directory read as file", HostLane.Fast, 27)]
    private static bool TestDirectoryReadAsFile()
    {
        byte[] image = CreateValidFat32Image();
        byte[] destination = new byte[1];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "EFI",
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.DirectoryReadAsFile
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "EFI"
            && result.Entry.FirstCluster == EfiCluster
            && result.Entry.IsDirectory
            && result.FileSize == 0
            && result.BytesRead == 0
            && !result.AssumedContiguousClusters);
    }

    [HostTest("storage fat32 rejects malformed long-name metadata", HostLane.Fast, 28)]
    private static bool TestMalformedLongNameMetadata()
    {
        byte[] image = CreateFat32ImageWithManifestLongName(corruptLongNameChecksum: true);

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "MANIFEST.JSON");

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.MalformedLongFileNameMetadata);
    }

    [HostTest("storage fat32 rejects orphan long-name metadata", HostLane.Fast, 29)]
    private static bool TestOrphanLongNameMetadata()
    {
        byte[] image = CreateValidFat32Image();
        int rootDirectory = checked((int)(RootDirectoryLba * SectorSize));
        WriteLongFileNameEntry(
            image,
            rootDirectory + 64,
            "MANIFEST.JSON",
            "MANIFE~1JSO",
            corruptChecksum: false);
        image[rootDirectory + 96] = 0x00;

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "MANIFEST.JSON");

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.MalformedLongFileNameMetadata);
    }

    [HostTest("storage fat32 rejects unsupported long-name path", HostLane.Fast, 30)]
    private static bool TestUnsupportedLongNamePath()
    {
        byte[] image = CreateValidFat32Image();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            @"OS\MANIFEST.JSON");

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.UnsupportedLongNamePath);
    }

    [HostTest("storage fat32 reports truncated directory entry", HostLane.Fast, 31)]
    private static bool TestTruncatedDirectoryEntry()
    {
        byte[] image = CreateValidFat32Image();
        Fat32DirectoryEntry[] entries = new Fat32DirectoryEntry[4];

        Fat32DirectoryReadResult result = Fat32Parser.ReadRootDirectoryEntries(
            new TruncatedRootDirectoryBlockDevice(MemoryBlockDevice.FromImage(image, SectorSize)),
            PartitionStartLba,
            entries);

        return HostCheck.Result(!result.Succeeded
            && result.Status == Fat32Status.TruncatedDirectoryEntry);
    }

    [HostTest("storage fat32 empty directory allocation is volume independent", HostLane.Fast, 40)]
    private static bool TestEmptyDirectoryAllocationBound()
    {
        var small = new SparseFat32BlockDevice(65525);
        var large = new SparseFat32BlockDevice(1_000_000);
        // Warm both API paths before measuring this thread's managed allocations.
        Fat32Parser.ReadRootDirectoryEntries(small, PartitionStartLba, Span<Fat32DirectoryEntry>.Empty);
        Fat32Parser.ReadRootDirectoryEntries(large, PartitionStartLba, Span<Fat32DirectoryEntry>.Empty);
        bool smallOk = MeasureEmptyDirectoryAllocations(small, out long smallBytes);
        bool largeOk = MeasureEmptyDirectoryAllocations(large, out long largeBytes);
        Console.WriteLine($"FAT_DIRECTORY_ALLOCATION smallClusters=65525 largeClusters=1000000 iterations=32 smallBytes={smallBytes} largeBytes={largeBytes}");
        return HostCheck.Result(smallOk && largeOk && small.ReadCount == 66 && large.ReadCount == 66
            && smallBytes <= 32 * 8192 && largeBytes <= smallBytes + 512);
    }

    [HostTest("storage fat32 directory exact and exceeded cluster budgets", HostLane.Fast, 41)]
    private static bool TestDirectoryClusterBudgets()
    {
        var exact = CreateDeletedDirectoryChain(3);
        Span<uint> history = stackalloc uint[3];
        var complete = Fat32Parser.ReadRootDirectoryEntries(exact, PartitionStartLba, Span<Fat32DirectoryEntry>.Empty, history);
        var over = CreateDeletedDirectoryChain(3);
        var exhausted = Fat32Parser.ReadRootDirectoryEntries(over, PartitionStartLba, Span<Fat32DirectoryEntry>.Empty, history[..2]);
        var lookup = Fat32Parser.FindRootDirectoryEntry(exact, PartitionStartLba, "ABSENT.BIN", history);

        return HostCheck.Result(complete.Succeeded && complete.EntriesRead == 0 && !complete.ReachedEndMarker
            && exhausted.Status == Fat32Status.DirectoryBudgetExceeded && !exhausted.Succeeded && !exhausted.ReachedEndMarker
            && over.ReadCount == 5 && exact.ReadCount == 14 && lookup.Status == Fat32Status.MissingFile);
    }

    [HostTest("storage fat32 directory cycles remain distinct at budget boundary", HostLane.Fast, 42)]
    private static bool TestDirectoryCycleAtBudgetBoundary()
    {
        var device = CreateDeletedDirectoryChain(2, loop: true);
        Span<uint> history = stackalloc uint[2];
        var enumeration = Fat32Parser.ReadRootDirectoryEntries(device, PartitionStartLba, Span<Fat32DirectoryEntry>.Empty, history);
        var lookup = Fat32Parser.FindRootDirectoryEntry(device, PartitionStartLba, "ABSENT.BIN", history);

        return HostCheck.Result(enumeration.Status == Fat32Status.DirectoryClusterChainLoop
            && lookup.Status == Fat32Status.DirectoryClusterChainLoop && device.ReadCount == 10);
    }

    [HostTest("storage fat32 directory output and scratch exhaustion differ", HostLane.Fast, 43)]
    private static bool TestDirectoryOutputAndScratchExhaustion()
    {
        var device = new SparseFat32BlockDevice(65525);
        byte[] directory = device.GetSector(device.FirstDataLba);
        WriteDirectoryEntry(directory, 0, "FIRST   BIN", 0x20, 0, 0);
        WriteDirectoryEntry(directory, 32, "SECOND  BIN", 0x20, 0, 0);
        Fat32DirectoryEntry[] output = new Fat32DirectoryEntry[1];
        Span<uint> history = stackalloc uint[1];
        var outputFull = Fat32Parser.ReadRootDirectoryEntries(device, PartitionStartLba, output, history);
        int afterOutput = device.ReadCount;
        var noHistory = Fat32Parser.ReadRootDirectoryEntries(device, PartitionStartLba, output, Span<uint>.Empty);
        Fat32DirectoryEntry[] larger = new Fat32DirectoryEntry[2];
        var retry = Fat32Parser.ReadRootDirectoryEntries(device, PartitionStartLba, larger, history);

        return HostCheck.Result(outputFull.Status == Fat32Status.DirectoryEntryBufferTooSmall
            && noHistory.Status == Fat32Status.DirectoryBudgetExceeded && afterOutput == 2 && device.ReadCount == 5
            && retry.Succeeded && retry.ReachedEndMarker && retry.EntriesRead == 2
            && larger[0].ShortName == "FIRST.BIN" && larger[1].ShortName == "SECOND.BIN");
    }

    [HostTest("storage fat32 directory budgets preserve read failures and early end", HostLane.Fast, 44)]
    private static bool TestDirectoryFailureAndEarlyEnd()
    {
        Span<uint> history = stackalloc uint[2];
        var dataFailure = CreateDeletedDirectoryChain(2);
        dataFailure.FailedLba = dataFailure.FirstDataLba + 1;
        var data = Fat32Parser.ReadRootDirectoryEntries(dataFailure, PartitionStartLba, Span<Fat32DirectoryEntry>.Empty, history);
        var fatFailure = CreateDeletedDirectoryChain(2);
        fatFailure.FailedLba = fatFailure.FirstFatLba;
        var fat = Fat32Parser.ReadRootDirectoryEntries(fatFailure, PartitionStartLba, Span<Fat32DirectoryEntry>.Empty, history);
        var early = new SparseFat32BlockDevice(65525) { FailedLba = PartitionStartLba + ReservedSectorCount };
        var end = Fat32Parser.ReadRootDirectoryEntries(early, PartitionStartLba, Span<Fat32DirectoryEntry>.Empty, history[..1]);

        return HostCheck.Result(data.Status == Fat32Status.RootDirectoryReadFailed && data.BlockStatus == BlockDeviceStatus.IoError
            && fat.Status == Fat32Status.DirectoryFatReadFailed && fat.BlockStatus == BlockDeviceStatus.IoError
            && !data.ReachedEndMarker && !fat.ReachedEndMarker && dataFailure.ReadCount == 4 && fatFailure.ReadCount == 3
            && end.Succeeded && end.ReachedEndMarker && end.EntriesRead == 0 && early.ReadCount == 2);
    }

    [HostTest("storage fat32 caller directory history is reusable for nested lookup", HostLane.Fast, 45)]
    private static bool TestReusableFirstLevelHistory()
    {
        var device = MemoryBlockDevice.FromImage(CreateFat32ImageWithChainedOsDirectory(), SectorSize);
        Span<uint> history = stackalloc uint[2];
        history.Fill(uint.MaxValue);
        var limited = Fat32Parser.FindFirstLevelDirectoryEntry(device, PartitionStartLba, "OS/LATEOS.BIN", history[..1]);
        var first = Fat32Parser.FindFirstLevelDirectoryEntry(device, PartitionStartLba, "OS/LATEOS.BIN", history);
        var root = Fat32Parser.FindRootDirectory(device, PartitionStartLba, "OS", history);
        var second = Fat32Parser.FindFirstLevelDirectoryEntry(device, PartitionStartLba, "OS/LATEOS.BIN", history);
        var missing = Fat32Parser.FindFirstLevelDirectoryEntry(device, PartitionStartLba, "OS/ABSENT.BIN", history);

        return HostCheck.Result(limited.Status == Fat32Status.DirectoryBudgetExceeded
            && first.Succeeded && root.Succeeded && second.Succeeded && first.Entry.FirstCluster == ChainedDirectoryLateFileCluster
            && second.Entry.FirstCluster == first.Entry.FirstCluster && missing.Status == Fat32Status.MissingFile);
    }

    [HostTest("storage fat32 file wrappers retain caller directory budgets", HostLane.Fast, 46)]
    private static bool TestFileWrapperDirectoryBudgets()
    {
        var rootDevice = MemoryBlockDevice.FromImage(CreateFat32ImageWithChainedRootDirectory(), SectorSize);
        var childDevice = MemoryBlockDevice.FromImage(CreateFat32ImageWithChainedOsDirectory(), SectorSize);
        Span<uint> history = stackalloc uint[2];
        byte[] output = new byte[1];
        var rootLimited = Fat32Parser.ReadRootFile(rootDevice, PartitionStartLba, "LATE.BIN", output, history[..1]);
        var root = Fat32Parser.ReadRootFile(rootDevice, PartitionStartLba, "LATE.BIN", output, history);
        var childLimited = Fat32Parser.ReadFirstLevelFile(childDevice, PartitionStartLba, "OS/LATEOS.BIN", output, history[..1]);
        var child = Fat32Parser.ReadFirstLevelFile(childDevice, PartitionStartLba, "OS/LATEOS.BIN", output, history);

        return HostCheck.Result(rootLimited.Status == Fat32Status.DirectoryBudgetExceeded
            && childLimited.Status == Fat32Status.DirectoryBudgetExceeded && root.Succeeded && root.BytesRead == 1
            && child.Succeeded && child.BytesRead == 1);
    }

    private static bool MeasureEmptyDirectoryAllocations(SparseFat32BlockDevice device, out long allocated)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool valid = true;
        for (int i = 0; i < 32; i++)
        {
            var result = Fat32Parser.ReadRootDirectoryEntries(device, PartitionStartLba, Span<Fat32DirectoryEntry>.Empty);
            valid &= result.Succeeded && result.ReachedEndMarker && result.EntriesRead == 0;
        }

        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return valid;
    }

    private static SparseFat32BlockDevice CreateDeletedDirectoryChain(int clusters, bool loop = false)
    {
        var device = new SparseFat32BlockDevice(65525);
        for (uint index = 0; index < clusters; index++)
        {
            uint cluster = RootCluster + index;
            device.GetSector(device.FirstDataLba + index).AsSpan().Fill(0xE5);
            uint next = index + 1 < clusters ? cluster + 1 : loop ? RootCluster : Fat32EndOfChain;
            device.WriteFatEntry(0, cluster, next);
            device.WriteFatEntry(1, cluster, next);
        }

        return device;
    }

    private static byte[] CreateValidFat32Image()
    {
        const ulong blockCount = PartitionStartLba + TotalSectors + 8;
        byte[] image = new byte[checked((int)(blockCount * SectorSize))];
        int bootSector = checked((int)(PartitionStartLba * SectorSize));

        image[bootSector] = 0xEB;
        image[bootSector + 1] = 0x58;
        image[bootSector + 2] = 0x90;
        WriteAscii(image, bootSector + 3, "OSFAT32 ");
        WriteUInt16(image, bootSector + 11, (ushort)SectorSize);
        image[bootSector + 13] = 1;
        WriteUInt16(image, bootSector + 14, ReservedSectorCount);
        image[bootSector + 16] = FatCount;
        WriteUInt16(image, bootSector + 17, 0);
        WriteUInt16(image, bootSector + 19, 0);
        image[bootSector + 21] = 0xF8;
        WriteUInt16(image, bootSector + 22, 0);
        WriteUInt32(image, bootSector + 32, TotalSectors);
        WriteUInt32(image, bootSector + 36, FatSizeSectors);
        WriteUInt32(image, bootSector + 44, RootCluster);
        WriteAscii(image, bootSector + 82, "FAT32   ");
        image[bootSector + 510] = 0x55;
        image[bootSector + 511] = 0xAA;

        int rootDirectory = checked((int)(RootDirectoryLba * SectorSize));
        WriteDirectoryEntry(
            image,
            rootDirectory,
            "KERNEL  BIN",
            attributes: 0x20,
            firstCluster: KernelCluster,
            fileSize: KernelFileSize);
        WriteDirectoryEntry(
            image,
            rootDirectory + 32,
            "EFI        ",
            attributes: Fat32Parser.DirectoryAttribute,
            firstCluster: EfiCluster,
            fileSize: 0);
        image[rootDirectory + 64] = 0x00;

        WriteContiguousFatChain(image, KernelCluster, KernelFileSize);
        WriteFatEntry(image, EfiCluster, Fat32EndOfChain);
        WriteGeneratedContent(image, KernelCluster, KernelFileSize, KernelContentByte);

        return image;
    }

    private static byte[] CreateFat32ImageWithChainedFile(bool loopChain = false, bool shortChain = false)
    {
        byte[] image = CreateValidFat32Image();
        int rootDirectory = checked((int)(RootDirectoryLba * SectorSize));
        WriteDirectoryEntry(
            image,
            rootDirectory + 64,
            "CHAINED BIN",
            attributes: 0x20,
            firstCluster: ChainedFileFirstCluster,
            fileSize: ChainedFileSize);
        image[rootDirectory + 96] = 0x00;

        WriteFatEntry(image, ChainedFileFirstCluster, ChainedFileSecondCluster);
        WriteFatEntry(
            image,
            ChainedFileSecondCluster,
            loopChain ? ChainedFileFirstCluster : shortChain ? Fat32EndOfChain : ChainedFileThirdCluster);
        WriteFatEntry(image, ChainedFileThirdCluster, Fat32EndOfChain);

        WriteGeneratedContentToCluster(image, ChainedFileFirstCluster, 0, ChainedContentByte);
        WriteGeneratedContentToCluster(image, ChainedFileSecondCluster, checked((int)SectorSize), ChainedContentByte);
        WriteGeneratedContentToCluster(image, ChainedFileThirdCluster, checked((int)SectorSize * 2), ChainedContentByte);

        return image;
    }

    private static byte[] CreateFat32ImageWithChainedRootDirectory(
        bool loopChain = false,
        bool includeLateEntry = true)
    {
        byte[] image = CreateValidFat32Image();
        int rootDirectory = checked((int)(RootDirectoryLba * SectorSize));

        WriteFillerDirectoryEntries(image, rootDirectory, startEntryIndex: 2, count: 14, firstNameIndex: 0);
        WriteFatEntry(image, RootCluster, ChainedRootSecondCluster);
        WriteFatEntry(image, ChainedRootSecondCluster, loopChain ? RootCluster : Fat32EndOfChain);

        int secondDirectoryCluster = ClusterOffset(ChainedRootSecondCluster);
        if (includeLateEntry)
        {
            WriteDirectoryEntry(
                image,
                secondDirectoryCluster,
                "LATE    BIN",
                attributes: 0x20,
                firstCluster: ChainedRootLateFileCluster,
                fileSize: 1);
            image[secondDirectoryCluster + 32] = 0x00;
            WriteFatEntry(image, ChainedRootLateFileCluster, Fat32EndOfChain);
        }
        else
        {
            WriteFillerDirectoryEntries(image, secondDirectoryCluster, startEntryIndex: 0, count: 16, firstNameIndex: 100);
        }

        return image;
    }

    private static byte[] CreateFat32ImageWithChainedOsDirectory()
    {
        byte[] image = CreateFat32ImageWithOsManifest();
        int osDirectory = ClusterOffset(OsDirectoryCluster);

        WriteFillerDirectoryEntries(image, osDirectory, startEntryIndex: 2, count: 14, firstNameIndex: 200);
        WriteFatEntry(image, OsDirectoryCluster, ChainedDirectorySecondCluster);
        WriteFatEntry(image, ChainedDirectorySecondCluster, Fat32EndOfChain);

        int secondDirectoryCluster = ClusterOffset(ChainedDirectorySecondCluster);
        WriteDirectoryEntry(
            image,
            secondDirectoryCluster,
            "LATEOS  BIN",
            attributes: 0x20,
            firstCluster: ChainedDirectoryLateFileCluster,
            fileSize: 1);
        image[secondDirectoryCluster + 32] = 0x00;
        WriteFatEntry(image, ChainedDirectoryLateFileCluster, Fat32EndOfChain);

        return image;
    }

    private static byte[] CreateFat32ImageWithManifestLongName(bool corruptLongNameChecksum = false)
    {
        byte[] image = CreateValidFat32Image();
        int rootDirectory = checked((int)(RootDirectoryLba * SectorSize));
        WriteLongFileNameEntry(
            image,
            rootDirectory + 64,
            "MANIFEST.JSON",
            "MANIFE~1JSO",
            corruptLongNameChecksum);
        WriteDirectoryEntry(
            image,
            rootDirectory + 96,
            "MANIFE~1JSO",
            attributes: 0x20,
            firstCluster: ManifestCluster,
            fileSize: ManifestFileSize);
        image[rootDirectory + 128] = 0x00;
        WriteFatEntry(image, ManifestCluster, Fat32EndOfChain);
        WriteGeneratedContent(image, ManifestCluster, ManifestFileSize, ManifestContentByte);

        return image;
    }

    private static byte[] CreateFat32ImageWithOsManifest()
    {
        byte[] image = CreateValidFat32Image();
        int rootDirectory = checked((int)(RootDirectoryLba * SectorSize));
        WriteDirectoryEntry(
            image,
            rootDirectory + 64,
            "OS         ",
            attributes: Fat32Parser.DirectoryAttribute,
            firstCluster: OsDirectoryCluster,
            fileSize: 0);
        image[rootDirectory + 96] = 0x00;
        WriteFatEntry(image, OsDirectoryCluster, Fat32EndOfChain);

        int osDirectory = ClusterOffset(OsDirectoryCluster);
        WriteDirectoryEntry(
            image,
            osDirectory,
            ".          ",
            attributes: Fat32Parser.DirectoryAttribute,
            firstCluster: OsDirectoryCluster,
            fileSize: 0);
        WriteDirectoryEntry(
            image,
            osDirectory + 32,
            "..         ",
            attributes: Fat32Parser.DirectoryAttribute,
            firstCluster: RootCluster,
            fileSize: 0);
        WriteLongFileNameEntry(
            image,
            osDirectory + 64,
            "MANIFEST.JSON",
            "MANIFE~1JSO",
            corruptChecksum: false);
        WriteDirectoryEntry(
            image,
            osDirectory + 96,
            "MANIFE~1JSO",
            attributes: 0x20,
            firstCluster: ManifestCluster,
            fileSize: ManifestFileSize);
        image[osDirectory + 128] = 0x00;
        WriteFatEntry(image, ManifestCluster, Fat32EndOfChain);
        WriteGeneratedContent(image, ManifestCluster, ManifestFileSize, ManifestContentByte);

        return image;
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
        string rawShortName,
        bool corruptChecksum)
    {
        image[offset] = 0x41;
        image[offset + 11] = 0x0F;
        image[offset + 12] = 0;
        image[offset + 13] = (byte)(ComputeLongFileNameChecksum(rawShortName) + (corruptChecksum ? 1 : 0));
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

    private static void WriteFillerDirectoryEntries(
        byte[] image,
        int directoryOffset,
        int startEntryIndex,
        int count,
        int firstNameIndex)
    {
        for (int i = 0; i < count; i++)
        {
            string rawShortName = "FILL" + (firstNameIndex + i).ToString("0000") + "BIN";
            WriteDirectoryEntry(
                image,
                directoryOffset + ((startEntryIndex + i) * 32),
                rawShortName,
                attributes: 0x20,
                firstCluster: 0,
                fileSize: 0);
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

    private static void WriteGeneratedContent(
        byte[] image,
        uint firstCluster,
        uint fileSize,
        Func<int, byte> contentByte)
    {
        int offset = ClusterOffset(firstCluster);
        for (int i = 0; i < fileSize; i++)
        {
            image[offset + i] = contentByte(i);
        }
    }

    private static void WriteGeneratedContentToCluster(
        byte[] image,
        uint cluster,
        int contentStartIndex,
        Func<int, byte> contentByte)
    {
        int offset = ClusterOffset(cluster);
        for (int i = 0; i < SectorSize; i++)
        {
            image[offset + i] = contentByte(contentStartIndex + i);
        }
    }

    private static void WriteContiguousFatChain(byte[] image, uint firstCluster, uint fileSize)
    {
        uint clustersNeeded = (fileSize + SectorSize - 1) / SectorSize;
        for (uint i = 0; i < clustersNeeded; i++)
        {
            uint cluster = firstCluster + i;
            uint nextCluster = i + 1 == clustersNeeded ? Fat32EndOfChain : cluster + 1;
            WriteFatEntry(image, cluster, nextCluster);
        }
    }

    private static void WriteFatEntry(byte[] image, uint cluster, uint nextCluster)
    {
        for (uint fatIndex = 0; fatIndex < FatCount; fatIndex++)
        {
            ulong fatLba = PartitionStartLba + ReservedSectorCount + (fatIndex * FatSizeSectors);
            int offset = checked((int)((fatLba * SectorSize) + (cluster * 4)));
            WriteUInt32(image, offset, nextCluster);
        }
    }

    private static bool ContentMatches(byte[] actual, uint fileSize, Func<int, byte> contentByte)
    {
        if (actual.Length < fileSize)
        {
            return false;
        }

        for (int i = 0; i < fileSize; i++)
        {
            if (actual[i] != contentByte(i))
            {
                return false;
            }
        }

        return true;
    }

    private static byte KernelContentByte(int index)
    {
        return (byte)((index * 31 + 17) & 0xFF);
    }

    private static byte ManifestContentByte(int index)
    {
        return (byte)((index * 13 + 91) & 0xFF);
    }

    private static byte ChainedContentByte(int index)
    {
        return (byte)((index * 7 + 43) & 0xFF);
    }

    private static int ClusterOffset(uint cluster)
    {
        ulong lba = RootDirectoryLba + (cluster - RootCluster);
        return (int)(lba * SectorSize);
    }

    private static ulong ClusterLba(uint cluster)
    {
        return RootDirectoryLba + (cluster - RootCluster);
    }

    private static void WriteUInt16(byte[] image, int offset, ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset, 2), value);
    }

    private static void WriteUInt32(byte[] image, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset, 4), value);
    }

    private static SparseFat32BlockDevice CreateDivergentFatFixture()
    {
        var device = new SparseFat32BlockDevice(65525);
        WriteDirectoryEntry(device.GetSector(device.FirstDataLba), 0, "CHAIN   BIN", 0x20, 20, SectorSize * 3);
        device.WriteFatEntry(0, 20, 22);
        device.WriteFatEntry(0, 22, 24);
        device.WriteFatEntry(0, 24, Fat32EndOfChain);
        device.WriteFatEntry(1, 20, 23);
        device.WriteFatEntry(1, 23, 25);
        device.WriteFatEntry(1, 25, Fat32EndOfChain);
        device.GetSector(device.FirstDataLba + 18).AsSpan().Fill(0xA1);
        device.GetSector(device.FirstDataLba + 20).AsSpan().Fill(0xB2);
        device.GetSector(device.FirstDataLba + 22).AsSpan().Fill(0xC3);
        device.GetSector(device.FirstDataLba + 21).AsSpan().Fill(0xD4);
        device.GetSector(device.FirstDataLba + 23).AsSpan().Fill(0xE5);
        return device;
    }




}
