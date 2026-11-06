using System;
using OS.Std.Binary;

namespace OS.Storage;

public struct LiveDirectoryEntry
{
    public LiveDirectoryStatus Status;
    public ulong ShortNameLow;
    public uint ShortNameHigh, FirstCluster, ByteCount;
    public ushort NameLength;
    public byte Attributes;
    public readonly bool Succeeded => Status == LiveDirectoryStatus.Entry;
    public readonly bool HasLongName => NameLength != 0;
}
