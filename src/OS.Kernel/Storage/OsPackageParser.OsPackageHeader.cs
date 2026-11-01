using OS.Std.Binary;
using OS.Std.Memory;

namespace OS.Storage;

public readonly struct OsPackageHeader
{
    public OsPackageHeader(
        ushort version,
        ushort headerSize,
        ushort fileCount,
        ushort fileTableEntrySize,
        uint fileTableOffset,
        uint fileTableLength)
    {
        Version = version;
        HeaderSize = headerSize;
        FileCount = fileCount;
        FileTableEntrySize = fileTableEntrySize;
        FileTableOffset = fileTableOffset;
        FileTableLength = fileTableLength;
    }

    public ushort Version { get; }

    public ushort HeaderSize { get; }

    public ushort FileCount { get; }

    public ushort FileTableEntrySize { get; }

    public uint FileTableOffset { get; }

    public uint FileTableLength { get; }
}
