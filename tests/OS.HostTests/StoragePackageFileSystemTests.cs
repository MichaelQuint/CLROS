using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static class StoragePackageFileSystemTests
{
    private const string AppPath = "bin/app.dll";
    private const string ConfigPath = "config/app.json";
    private const ushort FileCount = 2;
    private const byte AppContentSeed = 0x41;
    private const byte ConfigContentSeed = 0xA1;
    private const int AppContentLength = 23;
    private const int ConfigContentLength = 31;

    [HostTest("storage.package opens file", HostLane.Fast, 0)]
    private static bool TestOpensFile()
    {
        byte[] package = CreateValidPackage();
        byte[] destination = new byte[AppContentLength];
        byte[] rangeDestination = new byte[9];

        OsPackageOpenFileResult open = OsPackageFileSystem.OpenFile(package, AppPathBytes);
        OsPackageReadFileResult read = OsPackageFileSystem.ReadFile(package, AppPathBytes, destination);
        OsPackageReadFileResult partial = OsPackageFileSystem.ReadFileRange(
            package,
            ConfigPathBytes,
            fileOffset: 5,
            byteCount: 9,
            rangeDestination);

        return HostCheck.Result(open.Succeeded
            && open.Status == OsPackageFileSystemStatus.Success
            && open.PackageStatus == OsPackageStatus.Success
            && open.Entry.Index == 0
            && open.FileLength == AppContentLength
            && read.Succeeded
            && read.BytesRead == AppContentLength
            && read.RequiredBytes == AppContentLength
            && ContentMatches(destination, AppContentSeed, startIndex: 0)
            && partial.Succeeded
            && partial.BytesRead == 9
            && RangeMatches(rangeDestination, ConfigContentSeed, startIndex: 5));
    }

    [HostTest("storage.package rejects missing file", HostLane.Fast, 1)]
    private static bool TestRejectsMissingFile()
    {
        byte[] package = CreateValidPackage();
        byte[] destination = new byte[16];

        OsPackageOpenFileResult open = OsPackageFileSystem.OpenFile(package, "bin/missing.dll"u8);
        OsPackageReadFileResult read = OsPackageFileSystem.ReadFile(
            package,
            "bin/missing.dll"u8,
            destination);

        return HostCheck.Result(!open.Succeeded
            && open.Status == OsPackageFileSystemStatus.MissingFile
            && open.PackageStatus == OsPackageStatus.MissingFile
            && !read.Succeeded
            && read.Status == OsPackageFileSystemStatus.MissingFile
            && read.PackageStatus == OsPackageStatus.MissingFile
            && read.BytesRead == 0);
    }

    [HostTest("storage.package enforces bounds", HostLane.Fast, 2)]
    private static bool TestEnforcesBounds()
    {
        byte[] package = CreateValidPackage();
        byte[] tooSmall = new byte[AppContentLength - 1];
        byte[] destination = new byte[AppContentLength];

        OsPackageReadFileResult tooSmallResult = OsPackageFileSystem.ReadFile(
            package,
            AppPathBytes,
            tooSmall);
        OsPackageReadFileResult invalidRangeResult = OsPackageFileSystem.ReadFileRange(
            package,
            AppPathBytes,
            fileOffset: AppContentLength - 2,
            byteCount: 3,
            destination);
        OsPackageReadFileResult invalidPathResult = OsPackageFileSystem.ReadFile(
            package,
            "/bin/app.dll"u8,
            destination);

        return HostCheck.Result(!tooSmallResult.Succeeded
            && tooSmallResult.Status == OsPackageFileSystemStatus.DestinationTooSmall
            && tooSmallResult.PackageStatus == OsPackageStatus.Success
            && tooSmallResult.RequiredBytes == AppContentLength
            && tooSmallResult.BytesRead == 0
            && !invalidRangeResult.Succeeded
            && invalidRangeResult.Status == OsPackageFileSystemStatus.InvalidReadRange
            && invalidRangeResult.PackageStatus == OsPackageStatus.Success
            && !invalidPathResult.Succeeded
            && invalidPathResult.Status == OsPackageFileSystemStatus.InvalidPath
            && invalidPathResult.PackageStatus == OsPackageStatus.InvalidPath);
    }

    [HostTest("storage.package filesystem enumerates bounded paths", HostLane.Fast, 3)]
    private static bool TestEnumeratesBoundedPaths()
    {
        byte[] package = CreateValidPackage();
        byte[] destination = new byte[ConfigPath.Length];
        byte[] tooSmall = new byte[ConfigPath.Length - 1];

        OsPackageEnumerateFileResult enumerate = OsPackageFileSystem.EnumerateFile(
            package,
            1,
            destination);
        OsPackageEnumerateFileResult small = OsPackageFileSystem.EnumerateFile(
            package,
            1,
            tooSmall);
        OsPackageEnumerateFileResult outOfRange = OsPackageFileSystem.EnumerateFile(
            package,
            FileCount,
            destination);

        return HostCheck.Result(enumerate.Succeeded
            && enumerate.Status == OsPackageFileSystemStatus.Success
            && enumerate.PackageStatus == OsPackageStatus.Success
            && enumerate.Entry.Index == 1
            && enumerate.Entry.ContentLength == ConfigContentLength
            && enumerate.BytesWritten == (ulong)ConfigPath.Length
            && PathMatches(destination, ConfigPath)
            && !small.Succeeded
            && small.Status == OsPackageFileSystemStatus.DestinationTooSmall
            && small.PackageStatus == OsPackageStatus.DestinationTooSmall
            && small.RequiredBytes == (ulong)ConfigPath.Length
            && small.BytesWritten == 0
            && !outOfRange.Succeeded
            && outOfRange.Status == OsPackageFileSystemStatus.FileIndexOutOfRange
            && outOfRange.PackageStatus == OsPackageStatus.FileIndexOutOfRange);
    }

    [HostTest("storage.package filesystem result defaults are not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        OsPackageOpenFileResult open = default;
        OsPackageReadFileResult read = default;
        OsPackageEnumerateFileResult enumerate = default;
        OsPackageOpenFileResult openFailure = OsPackageOpenFileResult.Fail(
            OsPackageFileSystemStatus.MissingFile,
            OsPackageStatus.MissingFile);
        OsPackageReadFileResult readFailure = OsPackageReadFileResult.Fail(
            OsPackageFileSystemStatus.DestinationTooSmall,
            OsPackageStatus.Success,
            default,
            default,
            bytesRead: 2,
            requiredBytes: 4);
        OsPackageEnumerateFileResult enumerateFailure = OsPackageEnumerateFileResult.Fail(
            OsPackageFileSystemStatus.FileIndexOutOfRange,
            OsPackageStatus.FileIndexOutOfRange,
            default,
            default,
            requiredBytes: 8);

        return HostCheck.Result(!open.IsInitialized
            && !open.Succeeded
            && open.Status == OsPackageFileSystemStatus.Success
            && open.PackageStatus == OsPackageStatus.Success
            && open.FileLength == 0
            && !read.IsInitialized
            && !read.Succeeded
            && read.Status == OsPackageFileSystemStatus.Success
            && read.PackageStatus == OsPackageStatus.Success
            && read.BytesRead == 0
            && read.RequiredBytes == 0
            && !enumerate.IsInitialized
            && !enumerate.Succeeded
            && enumerate.Status == OsPackageFileSystemStatus.Success
            && enumerate.PackageStatus == OsPackageStatus.Success
            && enumerate.BytesWritten == 0
            && enumerate.RequiredBytes == 0
            && openFailure.IsInitialized
            && !openFailure.Succeeded
            && openFailure.Status == OsPackageFileSystemStatus.MissingFile
            && openFailure.PackageStatus == OsPackageStatus.MissingFile
            && readFailure.IsInitialized
            && !readFailure.Succeeded
            && readFailure.Status == OsPackageFileSystemStatus.DestinationTooSmall
            && readFailure.PackageStatus == OsPackageStatus.Success
            && readFailure.BytesRead == 2
            && readFailure.RequiredBytes == 4
            && enumerateFailure.IsInitialized
            && !enumerateFailure.Succeeded
            && enumerateFailure.Status == OsPackageFileSystemStatus.FileIndexOutOfRange
            && enumerateFailure.PackageStatus == OsPackageStatus.FileIndexOutOfRange
            && enumerateFailure.BytesWritten == 0
            && enumerateFailure.RequiredBytes == 8
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OsPackageOpenFileResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OsPackageReadFileResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OsPackageEnumerateFileResult>());
    }

    [HostTest("storage.package filesystem copies use shared buffer operations", HostLane.Fast, 4)]
    private static bool TestCopiesUseSharedBufferOperations()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Storage",
            "OsPackageFileSystem.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains(
                "BufferOperations.MemoryCopy(content.Content, destination)",
                StringComparison.Ordinal)
            && source.Contains(
                "ReadOnlySpan<byte> source = content.Content.Slice",
                StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(source, destination)", StringComparison.Ordinal)
            && source.Contains("OsPackageFileSystemStatus.DestinationTooSmall", StringComparison.Ordinal)
            && !source.Contains("content.Content.CopyTo(destination)", StringComparison.Ordinal)
            && !source.Contains(".CopyTo(destination)", StringComparison.Ordinal));
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
            OsPackageParser.HeaderSize,
            appPathOffset,
            AppPath.Length,
            appContentOffset,
            AppContentLength);
        WriteEntry(
            package,
            OsPackageParser.HeaderSize + OsPackageParser.FileTableEntrySize,
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
            package[offset + index] = (byte)(seed + (index * 5));
        }
    }

    private static bool ContentMatches(ReadOnlySpan<byte> actual, byte seed, int startIndex)
    {
        return RangeMatches(actual, seed, startIndex);
    }

    private static bool RangeMatches(ReadOnlySpan<byte> actual, byte seed, int startIndex)
    {
        for (int index = 0; index < actual.Length; index++)
        {
            if (actual[index] != (byte)(seed + ((startIndex + index) * 5)))
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
