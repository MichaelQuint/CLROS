using System.Runtime.CompilerServices;
using OS.Pal;
using OS.Std.Binary;
using OS.Storage;

internal static class PalPackageFileFacadeTests
{
    private const string AppPath = "bin/app.dll";
    private const string ConfigPath = "config/app.json";
    private const ushort FileCount = 2;
    private const byte AppContentSeed = 0x41;
    private const byte ConfigContentSeed = 0xA1;
    private const int AppContentLength = 23;
    private const int ConfigContentLength = 31;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.package facade opens reads info closes", TestOpensReadsInfoCloses, ref failures);
        Run("pal.package facade reports missing and unsafe paths", TestReportsMissingAndUnsafePaths, ref failures);
        Run("pal.package facade reports small read buffers", TestReportsSmallReadBuffers, ref failures);
        Run("pal.package facade rejects stale handles", TestRejectsStaleHandles, ref failures);
        Run("pal.package facade reports handle exhaustion", TestReportsHandleExhaustion, ref failures);
        Run("pal.package facade enumerates package files", TestEnumeratesPackageFiles, ref failures);
        Run("pal.package facade rejects malformed request shapes", TestRejectsMalformedRequestShapes, ref failures);
        Run("pal.package facade result layouts contain no managed references", TestResultLayoutsContainNoManagedReferences, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL package facade host tests failed: " + failures);
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

    private static bool TestOpensReadsInfoCloses()
    {
        OSHostPackageFileFacade facade = CreateFacade(capacity: 2);
        byte[] path = Bytes(AppPath);

        OSHostPackageFileOpenResult open = facade.OpenPackageFile(PathRequest(path), path);
        if (!open.Succeeded
            || !open.File.IsValid
            || open.Info.ByteLength != AppContentLength)
        {
            return false;
        }

        byte[] destination = new byte[6];
        OSHostPackageFileReadResult read = facade.ReadFileAt(
            open.File,
            fileOffset: 3,
            new OSHostByteSpan(0x2000, (ulong)destination.Length),
            destination);

        OSHostStatus infoStatus = facade.GetFileInfo(open.File, out OSHostFileInfo info);
        OSHostStatus closeStatus = facade.CloseFile(open.File);
        OSHostStatus closedInfoStatus = facade.GetFileInfo(open.File, out _);

        return read.Succeeded
            && read.BytesRead == (ulong)destination.Length
            && RangeMatches(destination, AppContentSeed, startIndex: 3)
            && infoStatus == OSHostStatus.Ok
            && info.ByteLength == AppContentLength
            && closeStatus == OSHostStatus.Ok
            && closedInfoStatus == OSHostStatus.InvalidArgument;
    }

    private static bool TestReportsMissingAndUnsafePaths()
    {
        OSHostPackageFileFacade facade = CreateFacade(capacity: 2);
        byte[] missing = Bytes("bin/missing.dll");
        byte[] traversal = Bytes("../bin/app.dll");

        OSHostPackageFileOpenResult missingResult = facade.OpenPackageFile(
            PathRequest(missing),
            missing);
        OSHostPackageFileOpenResult traversalResult = facade.OpenPackageFile(
            PathRequest(traversal),
            traversal);

        return missingResult.Status == OSHostStatus.NotFound
            && traversalResult.Status == OSHostStatus.AccessDenied;
    }

    private static bool TestReportsSmallReadBuffers()
    {
        OSHostPackageFileFacade facade = CreateFacade(capacity: 1);
        byte[] path = Bytes(ConfigPath);
        OSHostPackageFileOpenResult open = facade.OpenPackageFile(PathRequest(path), path);
        if (!open.Succeeded)
        {
            return false;
        }

        Span<byte> destination = stackalloc byte[4];
        OSHostPackageFileReadResult read = facade.ReadFileAt(
            open.File,
            fileOffset: 0,
            new OSHostByteSpan(0x3000, 7),
            destination);

        return read.Status == OSHostStatus.BufferTooSmall
            && read.BytesRead == 0
            && read.RequiredBytes == 7;
    }

    private static bool TestRejectsStaleHandles()
    {
        OSHostPackageFileFacade facade = CreateFacade(capacity: 1);
        byte[] path = Bytes(AppPath);
        OSHostPackageFileOpenResult open = facade.OpenPackageFile(PathRequest(path), path);
        if (!open.Succeeded || facade.CloseFile(open.File) != OSHostStatus.Ok)
        {
            return false;
        }

        Span<byte> destination = stackalloc byte[1];
        OSHostPackageFileReadResult read = facade.ReadFileAt(
            open.File,
            fileOffset: 0,
            new OSHostByteSpan(0x4000, 1),
            destination);

        return read.Status == OSHostStatus.InvalidArgument
            && facade.CloseFile(open.File) == OSHostStatus.InvalidArgument
            && facade.GetFileInfo(open.File, out _) == OSHostStatus.InvalidArgument;
    }

    private static bool TestReportsHandleExhaustion()
    {
        OSHostPackageFileFacade facade = CreateFacade(capacity: 1);
        byte[] appPath = Bytes(AppPath);
        byte[] configPath = Bytes(ConfigPath);

        OSHostPackageFileOpenResult first = facade.OpenPackageFile(
            PathRequest(appPath),
            appPath);
        OSHostPackageFileOpenResult second = facade.OpenPackageFile(
            PathRequest(configPath),
            configPath);

        return first.Succeeded
            && second.Status == OSHostStatus.OutOfMemory
            && !second.File.IsValid;
    }

    private static bool TestEnumeratesPackageFiles()
    {
        OSHostPackageFileFacade facade = CreateFacade(capacity: 1);
        byte[] firstPath = new byte[AppPath.Length];
        byte[] tooSmall = new byte[ConfigPath.Length - 1];

        OSHostPackageFileEnumerationResult first = facade.EnumeratePackage(
            0,
            new OSHostByteSpan(0x5000, (ulong)firstPath.Length),
            firstPath);
        OSHostPackageFileEnumerationResult small = facade.EnumeratePackage(
            1,
            new OSHostByteSpan(0x6000, (ulong)tooSmall.Length),
            tooSmall);

        return first.Succeeded
            && first.BytesWritten == (ulong)AppPath.Length
            && first.Info.ByteLength == AppContentLength
            && PathMatches(firstPath, AppPath)
            && small.Status == OSHostStatus.BufferTooSmall
            && small.RequiredBytes == (ulong)ConfigPath.Length
            && small.Info.ByteLength == ConfigContentLength;
    }

    private static bool TestRejectsMalformedRequestShapes()
    {
        OSHostPackageFileFacade facade = CreateFacade(capacity: 1);
        OSHostPackageFileFacade nullFacade = new(table: null);
        byte[] path = Bytes(AppPath);

        OSHostPackageFileOpenResult nullOpen = nullFacade.OpenPackageFile(
            PathRequest(path),
            path);
        OSHostPackageFileOpenResult flags = facade.OpenPackageFile(
            new OSHostPackagePath(new OSHostByteSpan(0x1000, (ulong)path.Length), flags: 1),
            path);
        OSHostPackageFileOpenResult lengthMismatch = facade.OpenPackageFile(
            new OSHostPackagePath(new OSHostByteSpan(0x1000, (ulong)path.Length + 1), flags: 0),
            path);
        OSHostPackageFileEnumerationResult enumerateMismatch = facade.EnumeratePackage(
            0,
            new OSHostByteSpan(0x2000, 99),
            Span<byte>.Empty);

        return nullOpen.Status == OSHostStatus.InvalidArgument
            && flags.Status == OSHostStatus.InvalidArgument
            && lengthMismatch.Status == OSHostStatus.InvalidArgument
            && enumerateMismatch.Status == OSHostStatus.InvalidArgument;
    }

    private static bool TestResultLayoutsContainNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostPackageFileOpenResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostPackageFileReadResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostPackageFileEnumerationResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostFileInfo>();
    }

    private static OSHostPackageFileFacade CreateFacade(int capacity)
    {
        return new OSHostPackageFileFacade(
            new OSHostPackageFileTable(CreateValidPackage(), capacity));
    }

    private static OSHostPackagePath PathRequest(byte[] path)
    {
        return new OSHostPackagePath(
            new OSHostByteSpan(0x1000, (ulong)path.Length),
            flags: 0);
    }

    private static byte[] Bytes(string value)
    {
        byte[] bytes = new byte[value.Length];
        for (int index = 0; index < value.Length; index++)
        {
            bytes[index] = (byte)value[index];
        }

        return bytes;
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
        EndianPrimitives.WriteUInt16LittleEndian(package.AsSpan(8, 2), OsPackageParser.SupportedVersion);
        EndianPrimitives.WriteUInt16LittleEndian(package.AsSpan(10, 2), OsPackageParser.HeaderSize);
        EndianPrimitives.WriteUInt16LittleEndian(package.AsSpan(12, 2), FileCount);
        EndianPrimitives.WriteUInt16LittleEndian(package.AsSpan(14, 2), OsPackageParser.FileTableEntrySize);
        EndianPrimitives.WriteUInt32LittleEndian(package.AsSpan(16, 4), tableOffset);
        EndianPrimitives.WriteUInt32LittleEndian(package.AsSpan(20, 4), tableLength);
        EndianPrimitives.WriteUInt32LittleEndian(package.AsSpan(24, 4), 0);
        EndianPrimitives.WriteUInt32LittleEndian(package.AsSpan(28, 4), 0);

        WriteEntry(package, tableOffset, appPathOffset, AppPath.Length, appContentOffset, AppContentLength);
        WriteEntry(
            package,
            tableOffset + OsPackageParser.FileTableEntrySize,
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
        EndianPrimitives.WriteUInt32LittleEndian(package.AsSpan(offset, 4), (uint)pathOffset);
        EndianPrimitives.WriteUInt16LittleEndian(package.AsSpan(offset + 4, 2), (ushort)pathLength);
        EndianPrimitives.WriteUInt16LittleEndian(package.AsSpan(offset + 6, 2), 0);
        EndianPrimitives.WriteUInt64LittleEndian(package.AsSpan(offset + 8, 8), (ulong)contentOffset);
        EndianPrimitives.WriteUInt64LittleEndian(package.AsSpan(offset + 16, 8), (ulong)contentLength);
        EndianPrimitives.WriteUInt64LittleEndian(package.AsSpan(offset + 24, 8), 0);
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

        for (int index = 0; index < expected.Length; index++)
        {
            if (actual[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }
}
