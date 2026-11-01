using OS.Std.Binary;
using OS.Std.Memory;

namespace OS.Storage;

public readonly struct OsPackageFileEntry
{
    public OsPackageFileEntry(
        uint index,
        uint pathOffset,
        ushort pathLength,
        ulong contentOffset,
        ulong contentLength)
    {
        Index = index;
        PathOffset = pathOffset;
        PathLength = pathLength;
        ContentOffset = contentOffset;
        ContentLength = contentLength;
    }

    public uint Index { get; }

    public uint PathOffset { get; }

    public ushort PathLength { get; }

    public ulong ContentOffset { get; }

    public ulong ContentLength { get; }
}
