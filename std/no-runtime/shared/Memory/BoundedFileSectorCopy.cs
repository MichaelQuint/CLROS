using System;

namespace OS.Std.Memory;

public enum FileSectorCopyStatus : byte
{
    Uninitialized = 0, Success, InvalidLength, IncompleteRead, InvalidOffset, Overlap
}

/// <summary>Copies only file bytes from a completed 512-byte sector into fixed
/// caller-owned storage. The caller proves read success, LBA and cluster order.
/// Neither destination nor count changes on rejection.</summary>
public static unsafe class BoundedFileSectorCopy
{
    public static FileSectorCopyStatus Copy(Span<byte> sector, uint completedReadBytes,
        uint fileBytes, Span<byte> destination, ref uint bytesWritten)
    {
        if (completedReadBytes != 512) return FileSectorCopyStatus.IncompleteRead;
        if (sector.Length != 512 || fileBytes == 0 || fileBytes > (uint)destination.Length)
            return FileSectorCopyStatus.InvalidLength;
        if (bytesWritten >= fileBytes || bytesWritten % 512 != 0) return FileSectorCopyStatus.InvalidOffset;
        fixed (byte* source = sector)
        fixed (byte* target = destination)
        fixed (uint* counter = &bytesWritten)
        {
            if (source == null || target == null || (ulong)source > ulong.MaxValue - 512 ||
                (ulong)target > ulong.MaxValue - (uint)destination.Length) return FileSectorCopyStatus.InvalidLength;
            if ((ulong)source < (ulong)target + (uint)destination.Length &&
                (ulong)target < (ulong)source + 512) return FileSectorCopyStatus.Overlap;
            if ((ulong)counter < (ulong)source + 512 && (ulong)source < (ulong)counter + 4 ||
                (ulong)counter < (ulong)target + (uint)destination.Length && (ulong)target < (ulong)counter + 4)
                return FileSectorCopyStatus.Overlap;
            uint start = bytesWritten, count = fileBytes - start;
            if (count > 512) count = 512;
            for (uint i = 0; i < count; i++) target[start + i] = source[i];
            bytesWritten = start + count;
            return FileSectorCopyStatus.Success;
        }
    }
}
