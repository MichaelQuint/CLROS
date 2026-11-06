using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static class StoragePackageFileStagingPlanTests
{
    private const string AppPath = "apps/app.bin";
    private const ushort FileCount = 1;
    private const byte AppContentSeed = 0x2D;
    private const int AppContentLength = 37;

    [HostTest("storage.package staging copies opened file", HostLane.Fast, 0)]
    private static bool TestCopiesOpenedFile()
    {
        byte[] package = CreateValidPackage();
        byte[] destination = new byte[AppContentLength];
        OsPackageOpenFileResult open = OsPackageFileSystem.OpenFile(package, AppPathBytes);

        OsPackageFileStagingResult result = OsPackageFileStagingPlanner.StageOpenedFile(
            package,
            open,
            destination);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == OsPackageFileStagingStatus.Success
            && result.OpenStatus == OsPackageFileSystemStatus.Success
            && result.PackageStatus == OsPackageStatus.Success
            && result.FileIndex == 0
            && result.PackageOffset == open.Entry.ContentOffset
            && result.FileLength == AppContentLength
            && result.BytesWritten == AppContentLength
            && result.RequiredBytes == AppContentLength
            && result.CopyAttempted
            && !result.SourceRangeOverflow
            && ContentMatches(destination, AppContentSeed));
    }

    [HostTest("storage.package staging rejects short destination", HostLane.Fast, 1)]
    private static bool TestRejectsShortDestination()
    {
        byte[] package = CreateValidPackage();
        byte[] destination = new byte[AppContentLength - 1];
        Fill(destination, 0xA5);
        OsPackageOpenFileResult open = OsPackageFileSystem.OpenFile(package, AppPathBytes);

        OsPackageFileStagingResult result = OsPackageFileStagingPlanner.StageOpenedFile(
            package,
            open,
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == OsPackageFileStagingStatus.DestinationTooSmall
            && result.RequiredBytes == AppContentLength
            && result.BytesWritten == 0
            && !result.CopyAttempted
            && AllBytesEqual(destination, 0xA5));
    }

    [HostTest("storage.package staging rejects invalid package range", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidPackageRange()
    {
        byte[] package = CreateValidPackage();
        byte[] truncated = package[..^3];
        byte[] destination = new byte[AppContentLength];
        Fill(destination, 0xCC);
        OsPackageOpenFileResult open = OsPackageFileSystem.OpenFile(package, AppPathBytes);

        OsPackageFileStagingResult result = OsPackageFileStagingPlanner.StageOpenedFile(
            truncated,
            open,
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == OsPackageFileStagingStatus.PackageRangeInvalid
            && result.FileLength == AppContentLength
            && result.BytesWritten == 0
            && !result.CopyAttempted
            && !result.SourceRangeOverflow
            && AllBytesEqual(destination, 0xCC));
    }

    [HostTest("storage.package staging rejects source range overflow", HostLane.Fast, 3)]
    private static bool TestRejectsSourceRangeOverflow()
    {
        byte[] package = CreateValidPackage();
        byte[] destination = new byte[8];
        Fill(destination, 0x7B);
        OsPackageOpenFileResult open = OsPackageOpenFileResult.Success(
            CreateHeader(),
            new OsPackageFileEntry(
                index: 0,
                pathOffset: 64,
                pathLength: (ushort)AppPath.Length,
                contentOffset: ulong.MaxValue - 3,
                contentLength: 8));

        OsPackageFileStagingResult result = OsPackageFileStagingPlanner.StageOpenedFile(
            package,
            open,
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == OsPackageFileStagingStatus.PackageRangeInvalid
            && result.OpenStatus == OsPackageFileSystemStatus.Success
            && result.PackageStatus == OsPackageStatus.Success
            && result.PackageOffset == ulong.MaxValue - 3
            && result.FileLength == 8
            && result.RequiredBytes == 8
            && result.BytesWritten == 0
            && !result.CopyAttempted
            && result.SourceRangeOverflow
            && AllBytesEqual(destination, 0x7B));
    }

    [HostTest("storage.package staging rejects open failure", HostLane.Fast, 4)]
    private static bool TestRejectsOpenFailure()
    {
        byte[] package = CreateValidPackage();
        byte[] destination = new byte[AppContentLength];
        Fill(destination, 0x55);
        OsPackageOpenFileResult open = OsPackageFileSystem.OpenFile(package, "apps/missing.bin"u8);

        OsPackageFileStagingResult result = OsPackageFileStagingPlanner.StageOpenedFile(
            package,
            open,
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == OsPackageFileStagingStatus.OpenFileFailed
            && result.OpenStatus == OsPackageFileSystemStatus.MissingFile
            && result.PackageStatus == OsPackageStatus.MissingFile
            && result.BytesWritten == 0
            && !result.CopyAttempted
            && AllBytesEqual(destination, 0x55));
    }

    [HostTest("storage.package staging preserves no-live-storage boundary", HostLane.Fast, 5)]
    private static bool TestPreservesNoLiveStorageBoundary()
    {
        byte[] package = CreateValidPackage();
        byte[] before = (byte[])package.Clone();
        byte[] destination = new byte[AppContentLength];
        OsPackageOpenFileResult open = OsPackageFileSystem.OpenFile(package, AppPathBytes);

        OsPackageFileStagingResult result = OsPackageFileStagingPlanner.StageOpenedFile(
            package,
            open,
            destination);

        return HostCheck.Result(result.Succeeded
            && OsPackageFileStagingPlanner.RequiresCallerOwnedPackageBytes
            && OsPackageFileStagingPlanner.RequiresOpenedPackageFile
            && OsPackageFileStagingPlanner.WritesCallerOwnedDestinationOnly
            && !OsPackageFileStagingPlanner.ReadsLiveStorage
            && !OsPackageFileStagingPlanner.CallsPalFileApis
            && !OsPackageFileStagingPlanner.AllocatesStagingMemory
            && !OsPackageFileStagingPlanner.MapsExecutablePages
            && !OsPackageFileStagingPlanner.ExecutesAppCode
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OsPackageFileStagingResult>()
            && package.AsSpan().SequenceEqual(before)
            && ContentMatches(destination, AppContentSeed));
    }

    [HostTest("storage.package staging default result is not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        OsPackageFileStagingResult result = default;
        OsPackageFileStagingResult overflowSuccess = OsPackageFileStagingResult.Create(
            OsPackageFileStagingStatus.Success,
            OsPackageFileSystemStatus.Success,
            OsPackageStatus.Success,
            fileIndex: 0,
            packageOffset: ulong.MaxValue - 3,
            fileLength: 8,
            bytesWritten: 8,
            requiredBytes: 8,
            copyAttempted: true,
            sourceRangeOverflow: true);
        OsPackageOpenFileResult open = OsPackageOpenFileResult.Success(
            new OsPackageHeader(
                OsPackageParser.SupportedVersion,
                OsPackageParser.HeaderSize,
                FileCount,
                OsPackageParser.FileTableEntrySize,
                OsPackageParser.HeaderSize,
                FileCount * OsPackageParser.FileTableEntrySize),
            new OsPackageFileEntry(
                index: 0,
                pathOffset: 64,
                pathLength: (ushort)AppPath.Length,
                contentOffset: 80,
                contentLength: 0));

        OsPackageFileStagingResult empty = OsPackageFileStagingPlanner.StageOpenedFile(
            CreateValidPackage(),
            open,
            Span<byte>.Empty);

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == OsPackageFileStagingStatus.Success
            && result.OpenStatus == OsPackageFileSystemStatus.Success
            && result.PackageStatus == OsPackageStatus.Success
            && result.FileIndex == 0
            && result.PackageOffset == 0
            && result.FileLength == 0
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && !result.CopyAttempted
            && !result.SourceRangeOverflow
            && !empty.Succeeded
            && empty.Status == OsPackageFileStagingStatus.EmptyFile
            && empty.RequiredBytes == 1
            && !empty.CopyAttempted
            && !empty.SourceRangeOverflow
            && !overflowSuccess.Succeeded
            && overflowSuccess.SourceRangeOverflow
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OsPackageFileStagingResult>());
    }

    [HostTest("storage.package staging uses shared buffer operations", HostLane.Fast, 6)]
    private static bool TestUsesSharedBufferOperations()
    {
        return HostCheck.Result(StorageBufferCopySourceGuard.Check(
            HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"),
            "OS.Storage.OsPackageFileStagingPlanner", "StageOpenedFile",
            "OS.Storage.OsPackageFileStagingStatus"));
    }

    private static byte[] CreateValidPackage()
    {
        const int tableOffset = OsPackageParser.HeaderSize;
        const int tableLength = FileCount * OsPackageParser.FileTableEntrySize;
        const int pathOffset = tableOffset + tableLength;
        const int contentOffset = pathOffset + 12;
        const int packageLength = contentOffset + AppContentLength;

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

        WriteEntry(package, tableOffset, pathOffset, AppPath.Length, contentOffset, AppContentLength);
        WriteAscii(package, pathOffset, AppPath);
        WriteGeneratedContent(package, contentOffset, AppContentLength, AppContentSeed);
        return package;
    }

    private static OsPackageHeader CreateHeader()
    {
        return new OsPackageHeader(
            OsPackageParser.SupportedVersion,
            OsPackageParser.HeaderSize,
            FileCount,
            OsPackageParser.FileTableEntrySize,
            OsPackageParser.HeaderSize,
            FileCount * OsPackageParser.FileTableEntrySize);
    }

    private static ReadOnlySpan<byte> AppPathBytes => "apps/app.bin"u8;

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
        WriteUInt32(package, offset, pathOffset);
        WriteUInt16(package, offset + 4, (ushort)pathLength);
        WriteUInt16(package, offset + 6, 0);
        WriteUInt64(package, offset + 8, contentOffset);
        WriteUInt64(package, offset + 16, contentLength);
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
            package[offset + index] = (byte)(seed + index);
        }
    }

    private static bool ContentMatches(ReadOnlySpan<byte> actual, byte seed)
    {
        for (int index = 0; index < actual.Length; index++)
        {
            if (actual[index] != (byte)(seed + index))
            {
                return false;
            }
        }

        return true;
    }

    private static void Fill(byte[] buffer, byte value)
    {
        for (int index = 0; index < buffer.Length; index++)
        {
            buffer[index] = value;
        }
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> buffer, byte expected)
    {
        for (int index = 0; index < buffer.Length; index++)
        {
            if (buffer[index] != expected)
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
