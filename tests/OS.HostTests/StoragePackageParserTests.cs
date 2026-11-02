using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static class StoragePackageParserTests
{
    private const string AppPath = "bin/app.dll";
    private const string ConfigPath = "config/app.json";
    private const ushort FileCount = 2;
    private const byte AppContentSeed = 0x31;
    private const byte ConfigContentSeed = 0x91;
    private const int AppContentLength = 17;
    private const int ConfigContentLength = 29;

    [HostTest("storage.package parses package header and file table", HostLane.Fast, 0)]
    private static bool TestParsesPackageHeaderAndFileTable()
    {
        byte[] package = CreateValidPackage();

        OsPackageParseResult parse = OsPackageParser.Parse(package);
        OsPackageFileLookupResult app = OsPackageParser.FindFile(package, AppPathBytes);
        OsPackageFileLookupResult config = OsPackageParser.FindFile(package, ConfigPathBytes);

        return HostCheck.Result(parse.Succeeded
            && parse.Status == OsPackageStatus.Success
            && parse.Header.Version == OsPackageParser.SupportedVersion
            && parse.Header.HeaderSize == OsPackageParser.HeaderSize
            && parse.Header.FileCount == FileCount
            && parse.Header.FileTableEntrySize == OsPackageParser.FileTableEntrySize
            && parse.Header.FileTableOffset == OsPackageParser.HeaderSize
            && parse.Header.FileTableLength == FileCount * OsPackageParser.FileTableEntrySize
            && app.Succeeded
            && app.Entry.Index == 0
            && app.Entry.PathLength == AppPath.Length
            && app.Entry.ContentLength == AppContentLength
            && config.Succeeded
            && config.Entry.Index == 1
            && config.Entry.PathLength == ConfigPath.Length
            && config.Entry.ContentLength == ConfigContentLength);
    }

    [HostTest("storage.package rejects malformed metadata", HostLane.Fast, 1)]
    private static bool TestRejectsMalformedMetadata()
    {
        return HostCheck.Result(ParseStatusIs(Array.Empty<byte>(), OsPackageStatus.PackageTooSmall)
            && MutatedParseStatusIs(
                static package => package[0] = 0,
                OsPackageStatus.InvalidMagic)
            && MutatedParseStatusIs(
                static package => WriteUInt16(package, 8, 1),
                OsPackageStatus.UnsupportedVersion)
            && MutatedParseStatusIs(
                static package => WriteUInt16(package, 10, 64),
                OsPackageStatus.InvalidHeaderSize)
            && MutatedParseStatusIs(
                static package => WriteUInt16(package, 12, (ushort)(OsPackageParser.MaximumFileCount + 1)),
                OsPackageStatus.InvalidFileCount)
            && MutatedParseStatusIs(
                static package => WriteUInt16(package, 14, 16),
                OsPackageStatus.InvalidFileTableEntrySize)
            && MutatedParseStatusIs(
                static package => WriteUInt32(package, 20, 1),
                OsPackageStatus.InvalidFileTableRange)
            && MutatedParseStatusIs(
                static package => WriteUInt64(package, FirstEntryOffset + 16, (ulong)package.Length),
                OsPackageStatus.InvalidEntryRange)
            && MutatedParseStatusIs(
                static package =>
                {
                    WriteUInt64(package, FirstEntryOffset + 8, OsPackageParser.HeaderSize);
                    WriteUInt64(package, FirstEntryOffset + 16, 1);
                },
                OsPackageStatus.EntryOverlapsMetadata)
            && MutatedParseStatusIs(
                static package =>
                {
                    uint firstPathOffset = ReadUInt32(package, FirstEntryOffset);
                    ushort firstPathLength = ReadUInt16(package, FirstEntryOffset + 4);
                    WriteUInt32(package, SecondEntryOffset, firstPathOffset);
                    WriteUInt16(package, SecondEntryOffset + 4, firstPathLength);
                },
                OsPackageStatus.DuplicatePath)
            && MutatedParseStatusIs(
                static package =>
                {
                    ulong firstContentOffset = ReadUInt64(package, FirstEntryOffset + 8);
                    WriteUInt64(package, SecondEntryOffset + 8, firstContentOffset + 1);
                    WriteUInt64(package, SecondEntryOffset + 16, 2);
                },
                OsPackageStatus.EntryOverlapsEntry)
            && MutatedParseStatusIs(
                static package =>
                {
                    uint pathOffset = ReadUInt32(package, FirstEntryOffset);
                    package[pathOffset] = (byte)'/';
                },
                OsPackageStatus.InvalidPath));
    }

    [HostTest("storage.package locates file content", HostLane.Fast, 2)]
    private static bool TestLocatesFileContent()
    {
        byte[] package = CreateValidPackage();

        OsPackageFileContentResult app = OsPackageParser.GetFileContent(package, AppPathBytes);
        OsPackageFileContentResult config = OsPackageParser.GetFileContent(package, ConfigPathBytes);
        OsPackageFileLookupResult missing = OsPackageParser.FindFile(package, "bin/missing.dll"u8);

        return HostCheck.Result(app.Succeeded
            && app.Status == OsPackageStatus.Success
            && app.Entry.ContentLength == AppContentLength
            && ContentMatches(app.Content, AppContentSeed)
            && config.Succeeded
            && config.Entry.ContentLength == ConfigContentLength
            && ContentMatches(config.Content, ConfigContentSeed)
            && !missing.Succeeded
            && missing.Status == OsPackageStatus.MissingFile);
    }

    [HostTest("storage.package enumerates file table", HostLane.Fast, 3)]
    private static bool TestEnumeratesFileTable()
    {
        byte[] package = CreateValidPackage();

        OsPackageFileLookupResult first = OsPackageParser.GetFileByIndex(package, 0);
        OsPackageFileLookupResult second = OsPackageParser.GetFileByIndex(package, 1);

        return HostCheck.Result(first.Succeeded
            && first.Header.FileCount == FileCount
            && first.Entry.Index == 0
            && first.Entry.PathLength == AppPath.Length
            && first.Entry.ContentLength == AppContentLength
            && second.Succeeded
            && second.Entry.Index == 1
            && second.Entry.PathLength == ConfigPath.Length
            && second.Entry.ContentLength == ConfigContentLength);
    }

    [HostTest("storage.package enumeration exposes file paths", HostLane.Fast, 4)]
    private static bool TestEnumerationExposesFilePaths()
    {
        byte[] package = CreateValidPackage();
        byte[] appPath = new byte[AppPath.Length];
        byte[] configPath = new byte[ConfigPath.Length];

        OsPackageFilePathCopyResult first = OsPackageParser.CopyFilePathByIndex(
            package,
            0,
            appPath);
        OsPackageFilePathCopyResult second = OsPackageParser.CopyFilePathByIndex(
            package,
            1,
            configPath);

        return HostCheck.Result(first.Succeeded
            && first.BytesWritten == (ulong)AppPath.Length
            && first.RequiredBytes == (ulong)AppPath.Length
            && PathMatches(appPath, AppPath)
            && second.Succeeded
            && second.BytesWritten == (ulong)ConfigPath.Length
            && second.RequiredBytes == (ulong)ConfigPath.Length
            && PathMatches(configPath, ConfigPath));
    }

    [HostTest("storage.package enumeration rejects out of range index", HostLane.Fast, 5)]
    private static bool TestEnumerationRejectsOutOfRangeIndex()
    {
        byte[] package = CreateValidPackage();
        byte[] destination = new byte[OsPackageParser.MaximumPathLength];

        OsPackageFileLookupResult lookup = OsPackageParser.GetFileByIndex(package, FileCount);
        OsPackageFilePathCopyResult copy = OsPackageParser.CopyFilePathByIndex(
            package,
            FileCount,
            destination);

        return HostCheck.Result(!lookup.Succeeded
            && lookup.Status == OsPackageStatus.FileIndexOutOfRange
            && lookup.Header.FileCount == FileCount
            && !copy.Succeeded
            && copy.Status == OsPackageStatus.FileIndexOutOfRange
            && copy.BytesWritten == 0
            && copy.RequiredBytes == 0);
    }

    [HostTest("storage.package parser result defaults are not success", HostLane.Fast, 8)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        OsPackageParseResult parse = default;
        OsPackageFileLookupResult lookup = default;
        OsPackageFilePathCopyResult copy = default;
        OsPackageFileContentResult content = default;
        OsPackageParseResult parseFailure = OsPackageParseResult.Fail(OsPackageStatus.InvalidMagic);
        OsPackageFileLookupResult lookupFailure = OsPackageFileLookupResult.Fail(OsPackageStatus.MissingFile);
        OsPackageFilePathCopyResult copyFailure = OsPackageFilePathCopyResult.Fail(
            OsPackageStatus.DestinationTooSmall,
            default,
            default,
            requiredBytes: 9);
        OsPackageFileContentResult contentFailure = OsPackageFileContentResult.Fail(OsPackageStatus.InvalidPath);

        return HostCheck.Result(!parse.IsInitialized
            && !parse.Succeeded
            && parse.Status == OsPackageStatus.Success
            && !lookup.IsInitialized
            && !lookup.Succeeded
            && lookup.Status == OsPackageStatus.Success
            && !copy.IsInitialized
            && !copy.Succeeded
            && copy.Status == OsPackageStatus.Success
            && copy.BytesWritten == 0
            && copy.RequiredBytes == 0
            && !content.IsInitialized
            && !content.Succeeded
            && content.Status == OsPackageStatus.Success
            && content.Content.Length == 0
            && parseFailure.IsInitialized
            && !parseFailure.Succeeded
            && parseFailure.Status == OsPackageStatus.InvalidMagic
            && lookupFailure.IsInitialized
            && !lookupFailure.Succeeded
            && lookupFailure.Status == OsPackageStatus.MissingFile
            && copyFailure.IsInitialized
            && !copyFailure.Succeeded
            && copyFailure.Status == OsPackageStatus.DestinationTooSmall
            && copyFailure.BytesWritten == 0
            && copyFailure.RequiredBytes == 9
            && contentFailure.IsInitialized
            && !contentFailure.Succeeded
            && contentFailure.Status == OsPackageStatus.InvalidPath
            && contentFailure.Content.Length == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OsPackageParseResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OsPackageFileLookupResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OsPackageFilePathCopyResult>());
    }

    [HostTest("storage.package parser uses shared memory equality helper", HostLane.Fast, 6)]
    private static bool TestUsesSharedMemoryEqualityHelper()
    {
        var catalog = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        string owner = typeof(OsPackageParser).FullName!;
        return HostCheck.Result(StorageEqualitySourceGuard.Literal(catalog, owner, "HasMagic", "OSPKGv0\0")
            && StoragePackageEqualitySourceGuard.Check(catalog, owner, nameof(OsPackageParser.FindFile), duplicate: false)
            && StoragePackageEqualitySourceGuard.Check(catalog, owner, "ValidateFileTable", duplicate: true));
    }

    [HostTest("storage.package parser path copy uses shared buffer operations", HostLane.Fast, 7)]
    private static bool TestPathCopyUsesSharedBufferOperations()
    {
        return HostCheck.Result(StorageBufferCopySourceGuard.Check(
            HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"), typeof(OsPackageParser).FullName!,
            nameof(OsPackageParser.CopyFilePathByIndex), typeof(OsPackageStatus).FullName!));
    }

    private static bool ParseStatusIs(byte[] package, OsPackageStatus status)
    {
        OsPackageParseResult result = OsPackageParser.Parse(package);
        return !result.Succeeded && result.Status == status;
    }

    private static bool MutatedParseStatusIs(Action<byte[]> mutate, OsPackageStatus status)
    {
        byte[] package = CreateValidPackage();
        mutate(package);
        return ParseStatusIs(package, status);
    }

    private static byte[] CreateValidPackage()
    {
        const int tableOffset = OsPackageParser.HeaderSize;
        const int tableLength = FileCount * OsPackageParser.FileTableEntrySize;
        const int appPathOffset = tableOffset + tableLength;
        const int configPathOffset = appPathOffset + 11;
        const int appContentOffset = configPathOffset + 15;
        const int configContentOffset = appContentOffset + AppContentLength;
        const int packageLength = configContentOffset + ConfigContentLength;

        byte[] package = new byte[packageLength];
        WriteMagic(package);
        WriteUInt16(package, 8, OsPackageParser.SupportedVersion);
        WriteUInt16(package, 10, OsPackageParser.HeaderSize);
        WriteUInt16(package, 12, FileCount);
        WriteUInt16(package, 14, OsPackageParser.FileTableEntrySize);
        WriteUInt32(package, 16, tableOffset);
        WriteUInt32(package, 20, tableLength);
        WriteUInt32(package, 24, 0);
        WriteUInt32(package, 28, 0);

        WriteEntry(
            package,
            FirstEntryOffset,
            appPathOffset,
            AppPath.Length,
            appContentOffset,
            AppContentLength);
        WriteEntry(
            package,
            SecondEntryOffset,
            configPathOffset,
            ConfigPath.Length,
            configContentOffset,
            ConfigContentLength);

        WriteAscii(package, appPathOffset, AppPath);
        WriteAscii(package, configPathOffset, ConfigPath);
        WriteGeneratedContent(package, appContentOffset, AppContentLength, AppContentSeed);
        WriteGeneratedContent(package, configContentOffset, ConfigContentLength, ConfigContentSeed);

        return package;
    }

    private static ReadOnlySpan<byte> AppPathBytes => "bin/app.dll"u8;

    private static ReadOnlySpan<byte> ConfigPathBytes => "config/app.json"u8;

    private static int FirstEntryOffset => OsPackageParser.HeaderSize;

    private static int SecondEntryOffset => OsPackageParser.HeaderSize + OsPackageParser.FileTableEntrySize;

    private static void WriteMagic(byte[] package)
    {
        package[0] = (byte)'O';
        package[1] = (byte)'S';
        package[2] = (byte)'P';
        package[3] = (byte)'K';
        package[4] = (byte)'G';
        package[5] = (byte)'v';
        package[6] = (byte)'0';
        package[7] = 0;
    }

    private static void WriteEntry(
        byte[] package,
        int offset,
        int pathOffset,
        int pathLength,
        int contentOffset,
        int contentLength)
    {
        WriteUInt32(package, offset, (uint)pathOffset);
        WriteUInt16(package, offset + 4, (ushort)pathLength);
        WriteUInt16(package, offset + 6, 0);
        WriteUInt64(package, offset + 8, (ulong)contentOffset);
        WriteUInt64(package, offset + 16, (ulong)contentLength);
        WriteUInt64(package, offset + 24, 0);
    }

    private static void WriteAscii(byte[] package, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            package[offset + index] = (byte)value[index];
        }
    }

    private static void WriteGeneratedContent(byte[] package, int offset, int length, byte seed)
    {
        for (int index = 0; index < length; index++)
        {
            package[offset + index] = (byte)(seed + (index * 7));
        }
    }

    private static bool ContentMatches(ReadOnlySpan<byte> actual, byte seed)
    {
        for (int index = 0; index < actual.Length; index++)
        {
            if (actual[index] != (byte)(seed + (index * 7)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PathMatches(ReadOnlySpan<byte> actual, string expected)
    {
        if (actual.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < actual.Length; index++)
        {
            if (actual[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static ushort ReadUInt16(byte[] package, int offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(package.AsSpan(offset, 2));
    }

    private static uint ReadUInt32(byte[] package, int offset)
    {
        return EndianPrimitives.ReadUInt32LittleEndian(package.AsSpan(offset, 4));
    }

    private static ulong ReadUInt64(byte[] package, int offset)
    {
        return EndianPrimitives.ReadUInt64LittleEndian(package.AsSpan(offset, 8));
    }

    private static void WriteUInt16(byte[] package, int offset, ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(package.AsSpan(offset, 2), value);
    }

    private static void WriteUInt32(byte[] package, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(package.AsSpan(offset, 4), value);
    }

    private static void WriteUInt32(byte[] package, int offset, int value)
    {
        WriteUInt32(package, offset, (uint)value);
    }

    private static void WriteUInt64(byte[] package, int offset, ulong value)
    {
        EndianPrimitives.WriteUInt64LittleEndian(package.AsSpan(offset, 8), value);
    }

    private static void WriteUInt64(byte[] package, int offset, int value)
    {
        WriteUInt64(package, offset, (ulong)value);
    }
}
