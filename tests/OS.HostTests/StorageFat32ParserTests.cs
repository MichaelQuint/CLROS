using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageFat32ParserTests
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

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("storage fat32 parses valid BPB", TestValidBpbParse, ref failures);
        Run("storage fat32 rejects bad boot signature", TestBadBootSignature, ref failures);
        Run("storage fat32 rejects unsupported sector size", TestUnsupportedSectorSize, ref failures);
        Run("storage fat32 rejects invalid sectors per cluster", TestInvalidSectorsPerCluster, ref failures);
        Run("storage fat32 rejects invalid FAT count", TestInvalidFatCount, ref failures);
        Run("storage fat32 enumerates root directory entries", TestRootDirectoryEnumeration, ref failures);
        Run("storage fat32 reports missing root short-name file", TestMissingRootFile, ref failures);
        Run("storage fat32 finds root short-name file metadata", TestFoundRootFileMetadata, ref failures);
        Run("storage fat32 reads root short-name file content", TestReadRootShortNameFileContent, ref failures);
        Run("storage fat32 reads chained file content", TestReadChainedFileContent, ref failures);
        Run("storage fat32 rejects chained file loop", TestRejectsChainedFileLoop, ref failures);
        Run("storage fat32 rejects short chained file", TestRejectsShortChainedFile, ref failures);
        Run("storage fat32 reads chained root directory entries", TestReadsChainedRootDirectoryEntries, ref failures);
        Run("storage fat32 finds chained root directory file metadata", TestFindsChainedRootDirectoryFileMetadata, ref failures);
        Run("storage fat32 reads chained first-level directory entries", TestReadsChainedFirstLevelDirectoryEntries, ref failures);
        Run("storage fat32 rejects directory chain loop", TestRejectsDirectoryChainLoop, ref failures);
        Run("storage fat32 reports short chained directory read", TestReportsShortChainedDirectoryRead, ref failures);
        Run("storage fat32 finds OS directory metadata", TestFoundOsDirectoryMetadata, ref failures);
        Run("storage fat32 finds OS/MANIFEST.JSON metadata", TestFoundOsManifestMetadata, ref failures);
        Run("storage fat32 reads OS/MANIFEST.JSON content", TestReadOsManifestContent, ref failures);
        Run("storage fat32 rejects OS/MANIFEST.JSON destination too small", TestOsManifestDestinationTooSmall, ref failures);
        Run("storage fat32 rejects missing OS directory for content read", TestReadOsManifestMissingDirectory, ref failures);
        Run("storage fat32 rejects unsupported nested path", TestRejectsUnsupportedNestedPath, ref failures);
        Run("storage fat32 finds MANIFEST.JSON root long-name file metadata", TestFoundRootLongNameManifestMetadata, ref failures);
        Run("storage fat32 reads MANIFEST.JSON root long-name file content", TestReadRootLongNameManifestContent, ref failures);
        Run("storage fat32 reports missing root long-name file", TestMissingRootLongNameFile, ref failures);
        Run("storage fat32 rejects root file destination too small", TestRootFileDestinationTooSmall, ref failures);
        Run("storage fat32 rejects directory read as file", TestDirectoryReadAsFile, ref failures);
        Run("storage fat32 rejects malformed long-name metadata", TestMalformedLongNameMetadata, ref failures);
        Run("storage fat32 rejects orphan long-name metadata", TestOrphanLongNameMetadata, ref failures);
        Run("storage fat32 rejects unsupported long-name path", TestUnsupportedLongNamePath, ref failures);
        Run("storage fat32 reports truncated directory entry", TestTruncatedDirectoryEntry, ref failures);
        Run("storage fat32 result defaults are not success", TestDefaultResultsAreNotSuccess, ref failures);
        Run("storage fat32 uses shared memory short-name helper", TestUsesSharedMemoryShortNameHelper, ref failures);
        Run("storage fat32 uses shared ascii long-name case helper", TestUsesSharedAsciiLongNameCaseHelper, ref failures);
        Run("storage fat32 uses shared ascii trailing-space helper", TestUsesSharedAsciiTrailingSpaceHelper, ref failures);
        Run("storage fat32 final block copy uses shared buffer operations", TestFinalBlockCopyUsesSharedBufferOperations, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Storage FAT32 parser host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestDefaultResultsAreNotSuccess()
    {
        Fat32BpbParseResult bpb = default;
        Fat32DirectoryReadResult directory = default;
        Fat32FileLookupResult lookup = default;
        Fat32FileReadResult read = default;

        return !bpb.IsInitialized
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
            && !RuntimeHelpers.IsReferenceOrContainsReferences<Fat32DirectoryReadResult>();
    }

    private static bool TestUsesSharedMemoryShortNameHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Storage", "Fat32Parser.cs"));

        return source.Contains(
                "return OS.Std.MemoryExtensions.SequenceEqual(actual, expected);",
                StringComparison.Ordinal)
            && !source.Contains("if (actual[i] != expected[i])", StringComparison.Ordinal);
    }

    private static bool TestUsesSharedAsciiLongNameCaseHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Storage", "Fat32Parser.cs"));

        return source.Contains("OS.Std.Ascii.ToUpperInvariant", StringComparison.Ordinal)
            && !source.Contains("private static char ToUpperAscii", StringComparison.Ordinal);
    }

    private static bool TestUsesSharedAsciiTrailingSpaceHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Storage", "Fat32Parser.cs"));

        return source.Contains("AsciiString.TrimTrailingSpace(bytes[..8])", StringComparison.Ordinal)
            && source.Contains("AsciiString.TrimTrailingSpace(bytes.Slice(8, 3))", StringComparison.Ordinal)
            && !source.Contains("private static int TrimTrailingSpaces", StringComparison.Ordinal);
    }

    private static bool TestFinalBlockCopyUsesSharedBufferOperations()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Storage", "Fat32Parser.cs"));

        return source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperationResult copy = BufferOperations.MemoryCopy(", StringComparison.Ordinal)
            && source.Contains("finalBlock[..bytesRemaining]", StringComparison.Ordinal)
            && source.Contains("destination.Slice(destinationOffset, bytesRemaining)", StringComparison.Ordinal)
            && source.Contains("Fat32Status.FileDestinationTooSmall", StringComparison.Ordinal)
            && source.Contains("if (!copy.Succeeded)", StringComparison.Ordinal)
            && !source.Contains(
                "finalBlock[..bytesRemaining].CopyTo(destination.Slice(destinationOffset, bytesRemaining))",
                StringComparison.Ordinal);
    }

    private static bool TestValidBpbParse()
    {
        byte[] image = CreateValidFat32Image();

        Fat32BpbParseResult result = Fat32Parser.ReadBpb(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba);

        return result.Succeeded
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
            && result.Bpb.RootDirectoryFirstLba == RootDirectoryLba;
    }

    private static bool TestBadBootSignature()
    {
        byte[] image = CreateValidFat32Image();
        int bootSector = checked((int)(PartitionStartLba * SectorSize));
        image[bootSector + 510] = 0;

        Fat32BpbParseResult result = Fat32Parser.ReadBpb(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba);

        return !result.Succeeded
            && result.Status == Fat32Status.InvalidBootSectorSignature;
    }

    private static bool TestUnsupportedSectorSize()
    {
        byte[] image = CreateValidFat32Image();
        int bootSector = checked((int)(PartitionStartLba * SectorSize));
        WriteUInt16(image, bootSector + 11, 1024);

        Fat32BpbParseResult result = Fat32Parser.ReadBpb(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba);

        return !result.Succeeded
            && result.Status == Fat32Status.UnsupportedSectorSize;
    }

    private static bool TestInvalidSectorsPerCluster()
    {
        byte[] image = CreateValidFat32Image();
        int bootSector = checked((int)(PartitionStartLba * SectorSize));
        image[bootSector + 13] = 3;

        Fat32BpbParseResult result = Fat32Parser.ReadBpb(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba);

        return !result.Succeeded
            && result.Status == Fat32Status.InvalidSectorsPerCluster;
    }

    private static bool TestInvalidFatCount()
    {
        byte[] image = CreateValidFat32Image();
        int bootSector = checked((int)(PartitionStartLba * SectorSize));
        image[bootSector + 16] = 0;

        Fat32BpbParseResult result = Fat32Parser.ReadBpb(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba);

        return !result.Succeeded
            && result.Status == Fat32Status.InvalidFatCount;
    }

    private static bool TestRootDirectoryEnumeration()
    {
        byte[] image = CreateValidFat32Image();
        Fat32DirectoryEntry[] entries = new Fat32DirectoryEntry[4];

        Fat32DirectoryReadResult result = Fat32Parser.ReadRootDirectoryEntries(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            entries);

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.EntriesRead == 2
            && result.ReachedEndMarker
            && entries[0].ShortName == "KERNEL.BIN"
            && !entries[0].IsDirectory
            && entries[0].FirstCluster == KernelCluster
            && entries[0].FileSize == KernelFileSize
            && entries[1].ShortName == "EFI"
            && entries[1].IsDirectory
            && entries[1].FirstCluster == EfiCluster;
    }

    private static bool TestMissingRootFile()
    {
        byte[] image = CreateValidFat32Image();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "MISSING.BIN");

        return !result.Succeeded
            && result.Status == Fat32Status.MissingFile;
    }

    private static bool TestFoundRootFileMetadata()
    {
        byte[] image = CreateValidFat32Image();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "kernel.bin");

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.Entry.ShortName == "KERNEL.BIN"
            && result.Entry.FirstCluster == KernelCluster
            && result.Entry.FileSize == KernelFileSize
            && !result.Entry.IsDirectory;
    }

    private static bool TestReadRootShortNameFileContent()
    {
        byte[] image = CreateValidFat32Image();
        byte[] destination = new byte[(int)KernelFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "kernel.bin",
            destination);

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "KERNEL.BIN"
            && result.Entry.FirstCluster == KernelCluster
            && result.FileSize == KernelFileSize
            && result.BytesRead == KernelFileSize
            && result.AssumedContiguousClusters
            && ContentMatches(destination, KernelFileSize, KernelContentByte);
    }

    private static bool TestReadChainedFileContent()
    {
        byte[] image = CreateFat32ImageWithChainedFile();
        byte[] destination = new byte[(int)ChainedFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "CHAINED.BIN",
            destination);

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "CHAINED.BIN"
            && result.Entry.FirstCluster == ChainedFileFirstCluster
            && result.FileSize == ChainedFileSize
            && result.BytesRead == ChainedFileSize
            && !result.AssumedContiguousClusters
            && ContentMatches(destination, ChainedFileSize, ChainedContentByte);
    }

    private static bool TestRejectsChainedFileLoop()
    {
        byte[] image = CreateFat32ImageWithChainedFile(loopChain: true);
        byte[] destination = new byte[(int)ChainedFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "CHAINED.BIN",
            destination);

        return !result.Succeeded
            && result.Status == Fat32Status.FileClusterChainLoop
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "CHAINED.BIN"
            && result.Entry.FirstCluster == ChainedFileFirstCluster
            && result.FileSize == ChainedFileSize
            && result.BytesRead == SectorSize * 2
            && !result.AssumedContiguousClusters;
    }

    private static bool TestRejectsShortChainedFile()
    {
        byte[] image = CreateFat32ImageWithChainedFile(shortChain: true);
        byte[] destination = new byte[(int)ChainedFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "CHAINED.BIN",
            destination);

        return !result.Succeeded
            && result.Status == Fat32Status.FileClusterChainTooShort
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "CHAINED.BIN"
            && result.Entry.FirstCluster == ChainedFileFirstCluster
            && result.FileSize == ChainedFileSize
            && result.BytesRead == SectorSize * 2
            && !result.AssumedContiguousClusters;
    }

    private static bool TestReadsChainedRootDirectoryEntries()
    {
        byte[] image = CreateFat32ImageWithChainedRootDirectory();
        Fat32DirectoryEntry[] entries = new Fat32DirectoryEntry[20];

        Fat32DirectoryReadResult result = Fat32Parser.ReadRootDirectoryEntries(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            entries);

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.EntriesRead == 17
            && result.ReachedEndMarker
            && entries[0].ShortName == "KERNEL.BIN"
            && entries[1].ShortName == "EFI"
            && entries[16].ShortName == "LATE.BIN"
            && entries[16].FirstCluster == ChainedRootLateFileCluster
            && entries[16].FileSize == 1;
    }

    private static bool TestFindsChainedRootDirectoryFileMetadata()
    {
        byte[] image = CreateFat32ImageWithChainedRootDirectory();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "LATE.BIN");

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "LATE.BIN"
            && result.Entry.FirstCluster == ChainedRootLateFileCluster
            && result.Entry.FileSize == 1
            && !result.Entry.IsDirectory;
    }

    private static bool TestReadsChainedFirstLevelDirectoryEntries()
    {
        byte[] image = CreateFat32ImageWithChainedOsDirectory();

        Fat32FileLookupResult result = Fat32Parser.FindFirstLevelDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/LATEOS.BIN");

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "LATEOS.BIN"
            && result.Entry.FirstCluster == ChainedDirectoryLateFileCluster
            && result.Entry.FileSize == 1
            && !result.Entry.IsDirectory;
    }

    private static bool TestRejectsDirectoryChainLoop()
    {
        byte[] image = CreateFat32ImageWithChainedRootDirectory(loopChain: true, includeLateEntry: false);

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "MISSING.BIN");

        return !result.Succeeded
            && result.Status == Fat32Status.DirectoryClusterChainLoop
            && result.BlockStatus == BlockDeviceStatus.Success;
    }

    private static bool TestReportsShortChainedDirectoryRead()
    {
        byte[] image = CreateFat32ImageWithChainedOsDirectory();

        Fat32FileLookupResult result = Fat32Parser.FindFirstLevelDirectoryEntry(
            new TruncatedDirectoryClusterBlockDevice(
                MemoryBlockDevice.FromImage(image, SectorSize),
                ChainedDirectorySecondCluster),
            PartitionStartLba,
            "OS/LATEOS.BIN");

        return !result.Succeeded
            && result.Status == Fat32Status.DirectoryClusterShortRead
            && result.BlockStatus == BlockDeviceStatus.Success;
    }

    private static bool TestFoundOsDirectoryMetadata()
    {
        byte[] image = CreateFat32ImageWithOsManifest();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectory(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "os");

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.Entry.ShortName == "OS"
            && result.Entry.FirstCluster == OsDirectoryCluster
            && result.Entry.FileSize == 0
            && result.Entry.IsDirectory;
    }

    private static bool TestFoundOsManifestMetadata()
    {
        byte[] image = CreateFat32ImageWithOsManifest();

        Fat32FileLookupResult result = Fat32Parser.FindFirstLevelDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/MANIFEST.JSON");

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.Entry.ShortName == "MANIFE~1.JSO"
            && result.Entry.FirstCluster == ManifestCluster
            && result.Entry.FileSize == ManifestFileSize
            && !result.Entry.IsDirectory;
    }

    private static bool TestRejectsUnsupportedNestedPath()
    {
        byte[] image = CreateFat32ImageWithOsManifest();

        Fat32FileLookupResult result = Fat32Parser.FindFirstLevelDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/CONFIG/MANIFEST.JSON");

        return !result.Succeeded
            && result.Status == Fat32Status.UnsupportedNestedPath;
    }

    private static bool TestReadOsManifestContent()
    {
        byte[] image = CreateFat32ImageWithOsManifest();
        byte[] destination = new byte[(int)ManifestFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadFirstLevelFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/MANIFEST.JSON",
            destination);

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "MANIFE~1.JSO"
            && result.Entry.FirstCluster == ManifestCluster
            && result.FileSize == ManifestFileSize
            && result.BytesRead == ManifestFileSize
            && result.AssumedContiguousClusters
            && ContentMatches(destination, ManifestFileSize, ManifestContentByte);
    }

    private static bool TestOsManifestDestinationTooSmall()
    {
        byte[] image = CreateFat32ImageWithOsManifest();
        byte[] destination = new byte[(int)ManifestFileSize - 1];

        Fat32FileReadResult result = Fat32Parser.ReadFirstLevelFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/MANIFEST.JSON",
            destination);

        return !result.Succeeded
            && result.Status == Fat32Status.FileDestinationTooSmall
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "MANIFE~1.JSO"
            && result.Entry.FirstCluster == ManifestCluster
            && result.FileSize == ManifestFileSize
            && result.BytesRead == 0
            && !result.AssumedContiguousClusters;
    }

    private static bool TestReadOsManifestMissingDirectory()
    {
        byte[] image = CreateValidFat32Image();
        byte[] destination = new byte[(int)ManifestFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadFirstLevelFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "OS/MANIFEST.JSON",
            destination);

        return !result.Succeeded
            && result.Status == Fat32Status.MissingDirectory
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.BytesRead == 0
            && !result.AssumedContiguousClusters;
    }

    private static bool TestFoundRootLongNameManifestMetadata()
    {
        byte[] image = CreateFat32ImageWithManifestLongName();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "manifest.json");

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.Entry.ShortName == "MANIFE~1.JSO"
            && result.Entry.FirstCluster == ManifestCluster
            && result.Entry.FileSize == ManifestFileSize
            && !result.Entry.IsDirectory;
    }

    private static bool TestReadRootLongNameManifestContent()
    {
        byte[] image = CreateFat32ImageWithManifestLongName();
        byte[] destination = new byte[(int)ManifestFileSize];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "MANIFEST.JSON",
            destination);

        return result.Succeeded
            && result.Status == Fat32Status.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "MANIFE~1.JSO"
            && result.Entry.FirstCluster == ManifestCluster
            && result.FileSize == ManifestFileSize
            && result.BytesRead == ManifestFileSize
            && result.AssumedContiguousClusters
            && ContentMatches(destination, ManifestFileSize, ManifestContentByte);
    }

    private static bool TestMissingRootLongNameFile()
    {
        byte[] image = CreateValidFat32Image();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "MANIFEST.JSON");

        return !result.Succeeded
            && result.Status == Fat32Status.MissingFile;
    }

    private static bool TestRootFileDestinationTooSmall()
    {
        byte[] image = CreateValidFat32Image();
        byte[] destination = new byte[(int)KernelFileSize - 1];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "KERNEL.BIN",
            destination);

        return !result.Succeeded
            && result.Status == Fat32Status.FileDestinationTooSmall
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "KERNEL.BIN"
            && result.FileSize == KernelFileSize
            && result.BytesRead == 0
            && !result.AssumedContiguousClusters;
    }

    private static bool TestDirectoryReadAsFile()
    {
        byte[] image = CreateValidFat32Image();
        byte[] destination = new byte[1];

        Fat32FileReadResult result = Fat32Parser.ReadRootFile(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "EFI",
            destination);

        return !result.Succeeded
            && result.Status == Fat32Status.DirectoryReadAsFile
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Entry.ShortName == "EFI"
            && result.Entry.FirstCluster == EfiCluster
            && result.Entry.IsDirectory
            && result.FileSize == 0
            && result.BytesRead == 0
            && !result.AssumedContiguousClusters;
    }

    private static bool TestMalformedLongNameMetadata()
    {
        byte[] image = CreateFat32ImageWithManifestLongName(corruptLongNameChecksum: true);

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            "MANIFEST.JSON");

        return !result.Succeeded
            && result.Status == Fat32Status.MalformedLongFileNameMetadata;
    }

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

        return !result.Succeeded
            && result.Status == Fat32Status.MalformedLongFileNameMetadata;
    }

    private static bool TestUnsupportedLongNamePath()
    {
        byte[] image = CreateValidFat32Image();

        Fat32FileLookupResult result = Fat32Parser.FindRootDirectoryEntry(
            MemoryBlockDevice.FromImage(image, SectorSize),
            PartitionStartLba,
            @"OS\MANIFEST.JSON");

        return !result.Succeeded
            && result.Status == Fat32Status.UnsupportedLongNamePath;
    }

    private static bool TestTruncatedDirectoryEntry()
    {
        byte[] image = CreateValidFat32Image();
        Fat32DirectoryEntry[] entries = new Fat32DirectoryEntry[4];

        Fat32DirectoryReadResult result = Fat32Parser.ReadRootDirectoryEntries(
            new TruncatedRootDirectoryBlockDevice(MemoryBlockDevice.FromImage(image, SectorSize)),
            PartitionStartLba,
            entries);

        return !result.Succeeded
            && result.Status == Fat32Status.TruncatedDirectoryEntry;
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
                throw new InvalidOperationException("Invalid FAT32 test image geometry: " + info.Status);
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

    private sealed class TruncatedRootDirectoryBlockDevice : IReadOnlyBlockDevice
    {
        private readonly IReadOnlyBlockDevice _inner;

        public TruncatedRootDirectoryBlockDevice(IReadOnlyBlockDevice inner)
        {
            _inner = inner;
        }

        public BlockDeviceInfo Info => _inner.Info;

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            if (firstBlock == RootDirectoryLba && blockCount == 1)
            {
                return BlockReadResult.Success(1, 31);
            }

            return _inner.ReadBlocks(firstBlock, blockCount, destination);
        }
    }

    private sealed class TruncatedDirectoryClusterBlockDevice : IReadOnlyBlockDevice
    {
        private readonly IReadOnlyBlockDevice _inner;
        private readonly uint _cluster;

        public TruncatedDirectoryClusterBlockDevice(IReadOnlyBlockDevice inner, uint cluster)
        {
            _inner = inner;
            _cluster = cluster;
        }

        public BlockDeviceInfo Info => _inner.Info;

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            if (firstBlock == ClusterLba(_cluster) && blockCount == 1)
            {
                return BlockReadResult.Success(1, 31);
            }

            return _inner.ReadBlocks(firstBlock, blockCount, destination);
        }
    }
}
