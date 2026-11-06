using System;
using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Std.Hash;

namespace OS.Storage;

public struct LiveGptHeaderResult
{
    public LiveGptStatus Status;
    public ulong Capacity, CurrentLba, AlternateLba, FirstUsableLba, LastUsableLba;
    public ulong DiskGuidLow, DiskGuidHigh, EntryLba;
    public uint HeaderSize, ExpectedHeaderCrc, ComputedHeaderCrc, ArrayCrc;
    public readonly bool Succeeded => Status == LiveGptStatus.Success;
}
