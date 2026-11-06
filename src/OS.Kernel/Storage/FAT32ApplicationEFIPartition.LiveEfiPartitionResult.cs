using System;
using OS.Std.Binary;

namespace OS.Storage;

public struct LiveEfiPartitionResult
{
    public LiveEfiPartitionStatus Status;
    public LiveGptStatus TableStatus;
    public uint EntryIndex;
    public ulong FirstLba, LastLba, BlockCount, ByteOffset, Attributes;
    public readonly bool Succeeded => Status == LiveEfiPartitionStatus.Success && TableStatus == LiveGptStatus.Success;
}
