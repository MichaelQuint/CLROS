using OS.Std.Binary;
using OS.Std.Memory;

namespace OS.Storage;

public static class OsPackageParser
{
    public const ushort SupportedVersion = 0;
    public const ushort HeaderSize = 32;
    public const ushort FileTableEntrySize = 32;
    public const ushort MaximumFileCount = 128;
    public const ushort MaximumPathLength = 255;

    private const int MagicLength = 8;
    private const uint HeaderReservedValue = 0;
    private const uint HeaderFlagsValue = 0;
    private const ushort EntryReservedValue = 0;
    private const ulong EntryReservedTailValue = 0;

    public static OsPackageParseResult Parse(ReadOnlySpan<byte> package)
    {
        OsPackageStatus headerStatus = TryReadHeader(package, out OsPackageHeader header);
        if (headerStatus != OsPackageStatus.Success)
        {
            return OsPackageParseResult.Fail(headerStatus);
        }

        OsPackageStatus tableStatus = ValidateFileTable(package, header);
        return tableStatus == OsPackageStatus.Success
            ? OsPackageParseResult.Success(header)
            : OsPackageParseResult.Fail(tableStatus);
    }

    public static OsPackageFileLookupResult FindFile(
        ReadOnlySpan<byte> package,
        ReadOnlySpan<byte> path)
    {
        OsPackageParseResult parse = Parse(package);
        if (!parse.Succeeded)
        {
            return OsPackageFileLookupResult.Fail(parse.Status);
        }

        if (!IsValidPath(path))
        {
            return OsPackageFileLookupResult.Fail(OsPackageStatus.InvalidPath, parse.Header);
        }

        OsPackageHeader header = parse.Header;
        for (uint index = 0; index < header.FileCount; index++)
        {
            OsPackageStatus entryStatus = TryReadEntry(package, header, index, out OsPackageFileEntry entry);
            if (entryStatus != OsPackageStatus.Success)
            {
                return OsPackageFileLookupResult.Fail(entryStatus, header);
            }

            ReadOnlySpan<byte> entryPath = SliceRange(package, entry.PathOffset, entry.PathLength);
            if (OS.Std.MemoryExtensions.SequenceEqual(entryPath, path))
            {
                return OsPackageFileLookupResult.Success(header, entry);
            }
        }

        return OsPackageFileLookupResult.Fail(OsPackageStatus.MissingFile, header);
    }

    public static OsPackageFileLookupResult GetFileByIndex(
        ReadOnlySpan<byte> package,
        uint fileIndex)
    {
        OsPackageParseResult parse = Parse(package);
        if (!parse.Succeeded)
        {
            return OsPackageFileLookupResult.Fail(parse.Status);
        }

        if (fileIndex >= parse.Header.FileCount)
        {
            return OsPackageFileLookupResult.Fail(
                OsPackageStatus.FileIndexOutOfRange,
                parse.Header);
        }

        OsPackageStatus entryStatus = TryReadEntry(
            package,
            parse.Header,
            fileIndex,
            out OsPackageFileEntry entry);
        return entryStatus == OsPackageStatus.Success
            ? OsPackageFileLookupResult.Success(parse.Header, entry)
            : OsPackageFileLookupResult.Fail(entryStatus, parse.Header);
    }

    public static OsPackageFilePathCopyResult CopyFilePathByIndex(
        ReadOnlySpan<byte> package,
        uint fileIndex,
        Span<byte> destination)
    {
        OsPackageFileLookupResult lookup = GetFileByIndex(package, fileIndex);
        if (!lookup.Succeeded)
        {
            return lookup.Header.HeaderSize == HeaderSize
                ? OsPackageFilePathCopyResult.Fail(lookup.Status, lookup.Header, lookup.Entry)
                : OsPackageFilePathCopyResult.Fail(lookup.Status, default, default);
        }

        if ((ulong)destination.Length < lookup.Entry.PathLength)
        {
            return OsPackageFilePathCopyResult.Fail(
                OsPackageStatus.DestinationTooSmall,
                lookup.Header,
                lookup.Entry,
                lookup.Entry.PathLength);
        }

        ReadOnlySpan<byte> path = SliceRange(
            package,
            lookup.Entry.PathOffset,
            lookup.Entry.PathLength);
        BufferOperationResult copy = BufferOperations.MemoryCopy(path, destination);
        if (!copy.Succeeded)
        {
            return OsPackageFilePathCopyResult.Fail(
                OsPackageStatus.DestinationTooSmall,
                lookup.Header,
                lookup.Entry,
                lookup.Entry.PathLength);
        }

        return OsPackageFilePathCopyResult.Success(
            lookup.Header,
            lookup.Entry,
            lookup.Entry.PathLength);
    }

    public static OsPackageFileContentResult GetFileContent(
        ReadOnlySpan<byte> package,
        ReadOnlySpan<byte> path)
    {
        OsPackageFileLookupResult lookup = FindFile(package, path);
        if (!lookup.Succeeded)
        {
            return lookup.Header.HeaderSize == HeaderSize
                ? OsPackageFileContentResult.Fail(lookup.Status, lookup.Header)
                : OsPackageFileContentResult.Fail(lookup.Status);
        }

        ReadOnlySpan<byte> content = SliceRange(
            package,
            lookup.Entry.ContentOffset,
            lookup.Entry.ContentLength);
        return OsPackageFileContentResult.Success(lookup.Header, lookup.Entry, content);
    }

    private static OsPackageStatus TryReadHeader(ReadOnlySpan<byte> package, out OsPackageHeader header)
    {
        header = default;

        if (package.Length < HeaderSize)
        {
            return OsPackageStatus.PackageTooSmall;
        }

        if (!HasMagic(package[..MagicLength]))
        {
            return OsPackageStatus.InvalidMagic;
        }

        if (!EndianPrimitives.TryReadUInt16LittleEndian(package.Slice(8, 2), out ushort version)
            || !EndianPrimitives.TryReadUInt16LittleEndian(package.Slice(10, 2), out ushort headerSize)
            || !EndianPrimitives.TryReadUInt16LittleEndian(package.Slice(12, 2), out ushort fileCount)
            || !EndianPrimitives.TryReadUInt16LittleEndian(package.Slice(14, 2), out ushort fileTableEntrySize)
            || !EndianPrimitives.TryReadUInt32LittleEndian(package.Slice(16, 4), out uint fileTableOffset)
            || !EndianPrimitives.TryReadUInt32LittleEndian(package.Slice(20, 4), out uint fileTableLength)
            || !EndianPrimitives.TryReadUInt32LittleEndian(package.Slice(24, 4), out uint flags)
            || !EndianPrimitives.TryReadUInt32LittleEndian(package.Slice(28, 4), out uint reserved))
        {
            return OsPackageStatus.PackageTooSmall;
        }

        if (version != SupportedVersion)
        {
            return OsPackageStatus.UnsupportedVersion;
        }

        if (headerSize != HeaderSize || flags != HeaderFlagsValue || reserved != HeaderReservedValue)
        {
            return OsPackageStatus.InvalidHeaderSize;
        }

        if (fileCount > MaximumFileCount)
        {
            return OsPackageStatus.InvalidFileCount;
        }

        if (fileTableEntrySize != FileTableEntrySize)
        {
            return OsPackageStatus.InvalidFileTableEntrySize;
        }

        if (!TryMultiply(fileCount, fileTableEntrySize, out ulong expectedTableLength)
            || expectedTableLength != fileTableLength)
        {
            return OsPackageStatus.InvalidFileTableRange;
        }

        if (fileTableOffset < headerSize
            || !IsRangeInsidePackage(package.Length, fileTableOffset, fileTableLength))
        {
            return OsPackageStatus.InvalidFileTableRange;
        }

        header = new OsPackageHeader(
            version,
            headerSize,
            fileCount,
            fileTableEntrySize,
            fileTableOffset,
            fileTableLength);
        return OsPackageStatus.Success;
    }

    private static OsPackageStatus ValidateFileTable(ReadOnlySpan<byte> package, OsPackageHeader header)
    {
        for (uint index = 0; index < header.FileCount; index++)
        {
            OsPackageStatus entryStatus = TryReadEntry(package, header, index, out OsPackageFileEntry entry);
            if (entryStatus != OsPackageStatus.Success)
            {
                return entryStatus;
            }

            ReadOnlySpan<byte> path = SliceRange(package, entry.PathOffset, entry.PathLength);
            if (!IsValidPath(path))
            {
                return OsPackageStatus.InvalidPath;
            }

            if (RangeOverlapsMetadata(header, entry.PathOffset, entry.PathLength)
                || RangeOverlapsMetadata(header, entry.ContentOffset, entry.ContentLength))
            {
                return OsPackageStatus.EntryOverlapsMetadata;
            }

            for (uint previousIndex = 0; previousIndex < index; previousIndex++)
            {
                OsPackageStatus previousStatus = TryReadEntry(
                    package,
                    header,
                    previousIndex,
                    out OsPackageFileEntry previousEntry);
                if (previousStatus != OsPackageStatus.Success)
                {
                    return previousStatus;
                }

                ReadOnlySpan<byte> previousPath = SliceRange(
                    package,
                    previousEntry.PathOffset,
                    previousEntry.PathLength);
                if (OS.Std.MemoryExtensions.SequenceEqual(path, previousPath))
                {
                    return OsPackageStatus.DuplicatePath;
                }

                if (RangesOverlap(entry.PathOffset, entry.PathLength, previousEntry.PathOffset, previousEntry.PathLength)
                    || RangesOverlap(entry.PathOffset, entry.PathLength, previousEntry.ContentOffset, previousEntry.ContentLength)
                    || RangesOverlap(entry.ContentOffset, entry.ContentLength, previousEntry.PathOffset, previousEntry.PathLength)
                    || RangesOverlap(entry.ContentOffset, entry.ContentLength, previousEntry.ContentOffset, previousEntry.ContentLength))
                {
                    return OsPackageStatus.EntryOverlapsEntry;
                }
            }
        }

        return OsPackageStatus.Success;
    }

    private static OsPackageStatus TryReadEntry(
        ReadOnlySpan<byte> package,
        OsPackageHeader header,
        uint index,
        out OsPackageFileEntry entry)
    {
        entry = default;
        if (index >= header.FileCount)
        {
            return OsPackageStatus.MissingFile;
        }

        ulong entryOffset = header.FileTableOffset + ((ulong)index * header.FileTableEntrySize);
        if (!IsRangeInsidePackage(package.Length, entryOffset, FileTableEntrySize))
        {
            return OsPackageStatus.InvalidFileTableRange;
        }

        ReadOnlySpan<byte> entryBytes = package.Slice((int)entryOffset, FileTableEntrySize);
        if (!EndianPrimitives.TryReadUInt32LittleEndian(entryBytes.Slice(0, 4), out uint pathOffset)
            || !EndianPrimitives.TryReadUInt16LittleEndian(entryBytes.Slice(4, 2), out ushort pathLength)
            || !EndianPrimitives.TryReadUInt16LittleEndian(entryBytes.Slice(6, 2), out ushort entryReserved)
            || !EndianPrimitives.TryReadUInt64LittleEndian(entryBytes.Slice(8, 8), out ulong contentOffset)
            || !EndianPrimitives.TryReadUInt64LittleEndian(entryBytes.Slice(16, 8), out ulong contentLength)
            || !EndianPrimitives.TryReadUInt64LittleEndian(entryBytes.Slice(24, 8), out ulong entryReservedTail))
        {
            return OsPackageStatus.InvalidEntryRange;
        }

        if (entryReserved != EntryReservedValue || entryReservedTail != EntryReservedTailValue)
        {
            return OsPackageStatus.InvalidEntryRange;
        }

        if (pathLength == 0 || pathLength > MaximumPathLength)
        {
            return OsPackageStatus.InvalidPath;
        }

        if (!IsRangeInsidePackage(package.Length, pathOffset, pathLength)
            || !IsRangeInsidePackage(package.Length, contentOffset, contentLength))
        {
            return OsPackageStatus.InvalidEntryRange;
        }

        entry = new OsPackageFileEntry(index, pathOffset, pathLength, contentOffset, contentLength);
        return OsPackageStatus.Success;
    }

    private static bool HasMagic(ReadOnlySpan<byte> value)
    {
        return value.Length == MagicLength
            && OS.Std.MemoryExtensions.SequenceEqual(value, "OSPKGv0\0"u8);
    }

    private static bool IsValidPath(ReadOnlySpan<byte> path)
    {
        if (path.Length == 0 || path.Length > MaximumPathLength)
        {
            return false;
        }

        bool previousWasSeparator = false;
        for (int index = 0; index < path.Length; index++)
        {
            byte value = path[index];
            if (value < 0x20 || value >= 0x7F || value == (byte)'\\')
            {
                return false;
            }

            if (value != (byte)'/')
            {
                previousWasSeparator = false;
                continue;
            }

            if (index == 0 || index == path.Length - 1 || previousWasSeparator)
            {
                return false;
            }

            previousWasSeparator = true;
        }

        return true;
    }

    private static bool RangeOverlapsMetadata(OsPackageHeader header, ulong offset, ulong length)
    {
        return RangesOverlap(0, header.HeaderSize, offset, length)
            || RangesOverlap(header.FileTableOffset, header.FileTableLength, offset, length);
    }

    private static bool RangesOverlap(ulong leftOffset, ulong leftLength, ulong rightOffset, ulong rightLength)
    {
        if (leftLength == 0 || rightLength == 0)
        {
            return false;
        }

        if (!TryAdd(leftOffset, leftLength, out ulong leftEnd)
            || !TryAdd(rightOffset, rightLength, out ulong rightEnd))
        {
            return true;
        }

        return leftOffset < rightEnd && rightOffset < leftEnd;
    }

    private static ReadOnlySpan<byte> SliceRange(ReadOnlySpan<byte> bytes, ulong offset, ulong length)
    {
        return bytes.Slice((int)offset, (int)length);
    }

    private static bool IsRangeInsidePackage(int packageLength, ulong offset, ulong length)
    {
        return TryAdd(offset, length, out ulong end)
            && offset <= (ulong)packageLength
            && end <= (ulong)packageLength;
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        if (left > ulong.MaxValue - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }

    private static bool TryMultiply(ulong left, ulong right, out ulong value)
    {
        if (left != 0 && right > ulong.MaxValue / left)
        {
            value = 0;
            return false;
        }

        value = left * right;
        return true;
    }
}
