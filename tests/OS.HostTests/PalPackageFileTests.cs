using System.Runtime.CompilerServices;
using OS.Pal;
using OS.Std.Binary;
using OS.Storage;

internal static class PalPackageFileTests
{
    private const string AppPath = "bin/app.dll";
    private const string ConfigPath = "config/app.json";
    private const ushort FileCount = 2;
    private const byte AppContentSeed = 0x41;
    private const byte ConfigContentSeed = 0xA1;
    private const int AppContentLength = 23;
    private const int ConfigContentLength = 31;

    [HostTest("pal.file opens package file", HostLane.Fast, 0)]
    private static bool TestOpensPackageFile()
    {
        byte[] package = CreateValidPackage();
        OSHostPackageFileTable table = new(package, capacity: 2);

        OSHostPackageFileOpenResult open = table.Open(AppPathBytes);
        if (!open.Succeeded
            || !open.IsInitialized
            || open.Status != OSHostStatus.Ok
            || !open.File.IsValid
            || open.File.Value != 1
            || open.Info.ByteLength != AppContentLength
            || table.ActiveCount != 1)
        {
            return HostCheck.Result(false);
        }

        if (table.GetInfo(open.File, out OSHostFileInfo info) != OSHostStatus.Ok
            || info.ByteLength != AppContentLength)
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(table.Close(open.File) == OSHostStatus.Ok
            && table.ActiveCount == 0
            && table.GetInfo(open.File, out _) == OSHostStatus.InvalidArgument
            && table.Close(open.File) == OSHostStatus.InvalidArgument);
    }

    [HostTest("pal.file reads offset ranges", HostLane.Fast, 1)]
    private static bool TestReadsOffsetRanges()
    {
        byte[] package = CreateValidPackage();
        OSHostPackageFileTable table = new(package, capacity: 1);
        OSHostPackageFileOpenResult open = table.Open(ConfigPathBytes);
        if (!open.Succeeded)
        {
            return HostCheck.Result(false);
        }

        byte[] destination = new byte[9];
        OSHostPackageFileReadResult read = table.ReadAt(
            open.File,
            fileOffset: 5,
            byteCount: 9,
            destination);

        byte[] tooSmall = new byte[8];
        OSHostPackageFileReadResult small = table.ReadAt(
            open.File,
            fileOffset: 5,
            byteCount: 9,
            tooSmall);

        OSHostPackageFileReadResult invalidRange = table.ReadAt(
            open.File,
            fileOffset: ConfigContentLength - 2,
            byteCount: 3,
            destination);

        OSHostPackageFileReadResult zeroAtEnd = table.ReadAt(
            open.File,
            fileOffset: ConfigContentLength,
            byteCount: 0,
            Span<byte>.Empty);

        return HostCheck.Result(read.Succeeded
            && read.IsInitialized
            && read.BytesRead == 9
            && read.RequiredBytes == 9
            && RangeMatches(destination, ConfigContentSeed, startIndex: 5)
            && small.Status == OSHostStatus.BufferTooSmall
            && small.IsInitialized
            && small.BytesRead == 0
            && small.RequiredBytes == 9
            && invalidRange.Status == OSHostStatus.InvalidArgument
            && invalidRange.IsInitialized
            && invalidRange.RequiredBytes == 3
            && zeroAtEnd.Status == OSHostStatus.Ok
            && zeroAtEnd.IsInitialized
            && zeroAtEnd.BytesRead == 0);
    }

    [HostTest("pal.file rejects unsafe paths", HostLane.Fast, 2)]
    private static bool TestRejectsUnsafePaths()
    {
        byte[] package = CreateValidPackage();
        OSHostPackageFileTable table = new(package, capacity: 2);

        OSHostPackageFileOpenResult absolute = table.Open("/bin/app.dll"u8);
        OSHostPackageFileOpenResult traversal = table.Open("../bin/app.dll"u8);
        OSHostPackageFileOpenResult drive = table.Open("C:/bin/app.dll"u8);
        OSHostPackageFileOpenResult missing = table.Open("bin/missing.dll"u8);

        return HostCheck.Result(absolute.Status == OSHostStatus.AccessDenied
            && absolute.IsInitialized
            && traversal.Status == OSHostStatus.AccessDenied
            && traversal.IsInitialized
            && drive.Status == OSHostStatus.AccessDenied
            && drive.IsInitialized
            && missing.Status == OSHostStatus.NotFound
            && missing.IsInitialized
            && table.ActiveCount == 0);
    }

    [HostTest("pal.file rejects invalid handles", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidHandles()
    {
        byte[] package = CreateValidPackage();
        OSHostPackageFileTable table = new(package, capacity: 1);
        Span<byte> destination = stackalloc byte[4];

        OSHostPackageFileOpenResult open = table.Open(AppPathBytes);
        if (!open.Succeeded)
        {
            return HostCheck.Result(false);
        }

        OSHostHandle zero = default;
        OSHostHandle unknown = new(99);
        OSHostStatus closeOpen = table.Close(open.File);
        OSHostPackageFileReadResult closedRead = table.ReadAt(
            open.File,
            fileOffset: 0,
            byteCount: 1,
            destination);

        return HostCheck.Result(table.ReadAt(zero, 0, 1, destination).Status == OSHostStatus.InvalidArgument
            && table.ReadAt(unknown, 0, 1, destination).Status == OSHostStatus.InvalidArgument
            && table.GetInfo(zero, out _) == OSHostStatus.InvalidArgument
            && table.GetInfo(unknown, out _) == OSHostStatus.InvalidArgument
            && table.Close(zero) == OSHostStatus.InvalidArgument
            && table.Close(unknown) == OSHostStatus.InvalidArgument
            && closeOpen == OSHostStatus.Ok
            && closedRead.Status == OSHostStatus.InvalidArgument
            && closedRead.IsInitialized
            && table.GetInfo(open.File, out _) == OSHostStatus.InvalidArgument
            && table.Close(open.File) == OSHostStatus.InvalidArgument
            && table.ActiveCount == 0);
    }

    [HostTest("pal.file reports deterministic exhaustion", HostLane.Fast, 4)]
    private static bool TestReportsDeterministicExhaustion()
    {
        byte[] package = CreateValidPackage();
        OSHostPackageFileTable table = new(package, capacity: 2);

        OSHostPackageFileOpenResult first = table.Open(AppPathBytes);
        OSHostPackageFileOpenResult second = table.Open(ConfigPathBytes);
        OSHostPackageFileOpenResult third = table.Open(AppPathBytes);
        if (!first.Succeeded
            || !first.IsInitialized
            || !second.Succeeded
            || !second.IsInitialized
            || first.File.Value != 1
            || second.File.Value != 2
            || third.Status != OSHostStatus.OutOfMemory
            || !third.IsInitialized
            || third.File.IsValid
            || table.ActiveCount != 2)
        {
            return HostCheck.Result(false);
        }

        if (table.Close(first.File) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostPackageFileOpenResult reusedSlot = table.Open(AppPathBytes);
        if (!reusedSlot.Succeeded
            || !reusedSlot.IsInitialized
            || reusedSlot.File.Value != 3
            || table.ActiveCount != 2
            || table.GetInfo(first.File, out _) != OSHostStatus.InvalidArgument)
        {
            return HostCheck.Result(false);
        }

        OSHostPackageFileTable emptyTable = new(package, capacity: 0);
        OSHostPackageFileOpenResult emptyOpen = emptyTable.Open(AppPathBytes);
        return HostCheck.Result(emptyTable.Capacity == 0
            && emptyOpen.Status == OSHostStatus.OutOfMemory
            && emptyOpen.IsInitialized
            && !emptyOpen.File.IsValid);
    }

    [HostTest("pal.file enumerates package files", HostLane.Fast, 5)]
    private static bool TestEnumeratesPackageFiles()
    {
        byte[] package = CreateValidPackage();
        OSHostPackageFileTable table = new(package, capacity: 1);
        byte[] firstPath = new byte[AppPath.Length];
        byte[] secondPath = new byte[ConfigPath.Length];

        OSHostPackageFileEnumerationResult first = table.Enumerate(0, firstPath);
        OSHostPackageFileEnumerationResult second = table.Enumerate(1, secondPath);
        OSHostPackageFileEnumerationResult outOfRange = table.Enumerate(2, secondPath);

        return HostCheck.Result(first.Succeeded
            && first.IsInitialized
            && first.Status == OSHostStatus.Ok
            && first.BytesWritten == (ulong)AppPath.Length
            && first.RequiredBytes == (ulong)AppPath.Length
            && first.Info.ByteLength == AppContentLength
            && PathMatches(firstPath, AppPath)
            && second.Succeeded
            && second.IsInitialized
            && second.Status == OSHostStatus.Ok
            && second.BytesWritten == (ulong)ConfigPath.Length
            && second.RequiredBytes == (ulong)ConfigPath.Length
            && second.Info.ByteLength == ConfigContentLength
            && PathMatches(secondPath, ConfigPath)
            && outOfRange.Status == OSHostStatus.InvalidArgument
            && outOfRange.IsInitialized
            && outOfRange.BytesWritten == 0
            && outOfRange.RequiredBytes == 0);
    }

    [HostTest("pal.file enumeration reports buffer too small", HostLane.Fast, 6)]
    private static bool TestEnumerationReportsBufferTooSmall()
    {
        byte[] package = CreateValidPackage();
        OSHostPackageFileTable table = new(package, capacity: 1);
        byte[] destination = new byte[ConfigPath.Length - 1];

        OSHostPackageFileEnumerationResult result = table.Enumerate(1, destination);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == OSHostStatus.BufferTooSmall
            && result.BytesWritten == 0
            && result.RequiredBytes == (ulong)ConfigPath.Length
            && result.Info.ByteLength == ConfigContentLength);
    }

    [HostTest("pal.file default results are not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        OSHostPackageFileOpenResult open = default;
        OSHostPackageFileReadResult read = default;
        OSHostPackageFileEnumerationResult enumeration = default;

        return HostCheck.Result(!open.IsInitialized
            && !open.Succeeded
            && open.Status == OSHostStatus.Ok
            && open.File.Value == 0
            && open.Info.ByteLength == 0
            && !read.IsInitialized
            && !read.Succeeded
            && read.Status == OSHostStatus.Ok
            && read.BytesRead == 0
            && read.RequiredBytes == 0
            && !enumeration.IsInitialized
            && !enumeration.Succeeded
            && enumeration.Status == OSHostStatus.Ok
            && enumeration.BytesWritten == 0
            && enumeration.RequiredBytes == 0
            && enumeration.Info.ByteLength == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostPackageFileOpenResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostPackageFileReadResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostPackageFileEnumerationResult>());
    }

    [HostTest("pal.package file table read uses shared buffer operations", HostLane.Fast, 8)]
    private static bool TestReadUsesSharedBufferOperations()
    {
        return HostCheck.Result(StorageBufferCopySourceGuard.Check(
            HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"), typeof(OSHostPackageFileTable).FullName!,
            "ReadAt", typeof(OSHostStatus).FullName!, nameof(OSHostStatus.BufferTooSmall)));
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
