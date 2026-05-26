using OS.Std.Memory;
using OS.Storage;

namespace OS.Pal;

public readonly struct OSHostPackageFileOpenResult
{
    public readonly OSHostStatus Status;

    public readonly OSHostHandle File;

    public readonly OSHostFileInfo Info;

    public OSHostPackageFileOpenResult(
        OSHostStatus status,
        OSHostHandle file,
        OSHostFileInfo info,
        bool isInitialized = true)
    {
        Status = status;
        File = file;
        Info = info;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostPackageFileOpenResult FromStatus(OSHostStatus status)
    {
        return new OSHostPackageFileOpenResult(status, default, default);
    }
}

public readonly struct OSHostPackageFileReadResult
{
    public readonly OSHostStatus Status;

    public readonly ulong BytesRead;

    public readonly ulong RequiredBytes;

    public OSHostPackageFileReadResult(
        OSHostStatus status,
        ulong bytesRead,
        ulong requiredBytes,
        bool isInitialized = true)
    {
        Status = status;
        BytesRead = bytesRead;
        RequiredBytes = requiredBytes;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostPackageFileReadResult FromStatus(
        OSHostStatus status,
        ulong requiredBytes = 0)
    {
        return new OSHostPackageFileReadResult(status, bytesRead: 0, requiredBytes);
    }
}

public readonly struct OSHostPackageFileEnumerationResult
{
    public readonly OSHostStatus Status;

    public readonly ulong BytesWritten;

    public readonly ulong RequiredBytes;

    public readonly OSHostFileInfo Info;

    public OSHostPackageFileEnumerationResult(
        OSHostStatus status,
        ulong bytesWritten,
        ulong requiredBytes,
        OSHostFileInfo info,
        bool isInitialized = true)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Info = info;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostPackageFileEnumerationResult FromStatus(
        OSHostStatus status,
        ulong requiredBytes = 0,
        OSHostFileInfo info = default)
    {
        return new OSHostPackageFileEnumerationResult(
            status,
            bytesWritten: 0,
            requiredBytes,
            info);
    }
}

public sealed class OSHostPackageFileTable
{
    private readonly ReadOnlyMemory<byte> package;
    private readonly PackageFileSlot[] slots;
    private ulong nextHandleValue;

    public OSHostPackageFileTable(ReadOnlyMemory<byte> package, int capacity)
    {
        this.package = package;
        slots = capacity > 0
            ? new PackageFileSlot[capacity]
            : [];
        nextHandleValue = 1;
    }

    public int Capacity => slots.Length;

    public int ActiveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].InUse)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public OSHostPackageFileOpenResult Open(ReadOnlySpan<byte> packageRelativePath)
    {
        if (!IsSafePackageRelativePath(packageRelativePath))
        {
            return OSHostPackageFileOpenResult.FromStatus(OSHostStatus.AccessDenied);
        }

        OsPackageOpenFileResult open = OsPackageFileSystem.OpenFile(
            package.Span,
            packageRelativePath);
        if (!open.Succeeded)
        {
            return OSHostPackageFileOpenResult.FromStatus(MapFileSystemStatus(open.Status));
        }

        int slotIndex = FindFreeSlot();
        if (slotIndex < 0 || nextHandleValue == 0)
        {
            return OSHostPackageFileOpenResult.FromStatus(OSHostStatus.OutOfMemory);
        }

        OSHostHandle file = new(nextHandleValue);
        nextHandleValue++;
        slots[slotIndex] = PackageFileSlot.Create(file, open.Entry);

        return new OSHostPackageFileOpenResult(
            OSHostStatus.Ok,
            file,
            CreateFileInfo(open.Entry));
    }

    public OSHostPackageFileEnumerationResult Enumerate(
        uint fileIndex,
        Span<byte> pathDestination)
    {
        OsPackageEnumerateFileResult enumerate = OsPackageFileSystem.EnumerateFile(
            package.Span,
            fileIndex,
            pathDestination);
        if (!enumerate.Succeeded)
        {
            return OSHostPackageFileEnumerationResult.FromStatus(
                MapFileSystemStatus(enumerate.Status),
                enumerate.RequiredBytes,
                enumerate.Entry.PathLength != 0
                    ? CreateFileInfo(enumerate.Entry)
                    : default);
        }

        return new OSHostPackageFileEnumerationResult(
            OSHostStatus.Ok,
            enumerate.BytesWritten,
            enumerate.RequiredBytes,
            CreateFileInfo(enumerate.Entry));
    }

    public OSHostStatus GetInfo(OSHostHandle file, out OSHostFileInfo info)
    {
        OSHostStatus status = TryFindSlot(file, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            info = default;
            return status;
        }

        info = CreateFileInfo(slots[slotIndex].Entry);
        return OSHostStatus.Ok;
    }

    public OSHostPackageFileReadResult ReadAt(
        OSHostHandle file,
        ulong fileOffset,
        ulong byteCount,
        Span<byte> destination)
    {
        OSHostStatus status = TryFindSlot(file, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return OSHostPackageFileReadResult.FromStatus(status);
        }

        OsPackageFileEntry entry = slots[slotIndex].Entry;
        if (fileOffset > entry.ContentLength
            || byteCount > entry.ContentLength - fileOffset
            || entry.ContentOffset > ulong.MaxValue - fileOffset)
        {
            return OSHostPackageFileReadResult.FromStatus(
                OSHostStatus.InvalidArgument,
                byteCount);
        }

        if ((ulong)destination.Length < byteCount)
        {
            return OSHostPackageFileReadResult.FromStatus(
                OSHostStatus.BufferTooSmall,
                byteCount);
        }

        ulong packageOffset = entry.ContentOffset + fileOffset;
        if (packageOffset > int.MaxValue || byteCount > int.MaxValue)
        {
            return OSHostPackageFileReadResult.FromStatus(
                OSHostStatus.InvalidArgument,
                byteCount);
        }

        ReadOnlySpan<byte> source = package.Span.Slice((int)packageOffset, (int)byteCount);
        BufferOperationResult copy = BufferOperations.MemoryCopy(source, destination);
        if (!copy.Succeeded)
        {
            return OSHostPackageFileReadResult.FromStatus(
                OSHostStatus.BufferTooSmall,
                byteCount);
        }

        return new OSHostPackageFileReadResult(OSHostStatus.Ok, byteCount, byteCount);
    }

    public OSHostStatus Close(OSHostHandle file)
    {
        OSHostStatus status = TryFindSlot(file, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        slots[slotIndex] = default;
        return OSHostStatus.Ok;
    }

    private OSHostStatus TryFindSlot(OSHostHandle file, out int slotIndex)
    {
        if (!file.IsValid)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].InUse && slots[i].File.Value == file.Value)
            {
                slotIndex = i;
                return OSHostStatus.Ok;
            }
        }

        slotIndex = -1;
        return OSHostStatus.InvalidArgument;
    }

    private int FindFreeSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].InUse)
            {
                return i;
            }
        }

        return -1;
    }

    private static OSHostFileInfo CreateFileInfo(OsPackageFileEntry entry)
    {
        return new OSHostFileInfo(
            entry.ContentLength,
            attributes: 1,
            modifiedUnixNanoseconds: 0);
    }

    private static bool IsSafePackageRelativePath(ReadOnlySpan<byte> path)
    {
        if (path.Length == 0)
        {
            return false;
        }

        int segmentStart = 0;
        bool previousWasSeparator = false;
        for (int i = 0; i < path.Length; i++)
        {
            byte value = path[i];
            if (value < 0x20
                || value >= 0x7F
                || value == (byte)'\\'
                || value == (byte)':')
            {
                return false;
            }

            if (value != (byte)'/')
            {
                previousWasSeparator = false;
                continue;
            }

            if (i == 0 || i == path.Length - 1 || previousWasSeparator)
            {
                return false;
            }

            if (IsDotSegment(path[segmentStart..i]))
            {
                return false;
            }

            previousWasSeparator = true;
            segmentStart = i + 1;
        }

        return !IsDotSegment(path[segmentStart..]);
    }

    private static bool IsDotSegment(ReadOnlySpan<byte> segment)
    {
        return segment.Length == 1 && segment[0] == (byte)'.'
            || segment.Length == 2 && segment[0] == (byte)'.' && segment[1] == (byte)'.';
    }

    private static OSHostStatus MapFileSystemStatus(OsPackageFileSystemStatus status)
    {
        return status switch
        {
            OsPackageFileSystemStatus.Success => OSHostStatus.Ok,
            OsPackageFileSystemStatus.InvalidPath => OSHostStatus.AccessDenied,
            OsPackageFileSystemStatus.MissingFile => OSHostStatus.NotFound,
            OsPackageFileSystemStatus.DestinationTooSmall => OSHostStatus.BufferTooSmall,
            OsPackageFileSystemStatus.InvalidReadRange => OSHostStatus.InvalidArgument,
            OsPackageFileSystemStatus.FileIndexOutOfRange => OSHostStatus.InvalidArgument,
            _ => OSHostStatus.IoError
        };
    }

    private struct PackageFileSlot
    {
        public bool InUse;

        public OSHostHandle File;

        public OsPackageFileEntry Entry;

        public static PackageFileSlot Create(OSHostHandle file, OsPackageFileEntry entry)
        {
            return new PackageFileSlot
            {
                InUse = true,
                File = file,
                Entry = entry
            };
        }
    }
}
