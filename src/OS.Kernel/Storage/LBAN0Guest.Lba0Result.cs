using System;
using System.Runtime.CompilerServices;
using OS.Std.Binary;

namespace OS.Storage;

public struct Lba0Result
{
    public Lba0Status Status;
    public BlockReadResult Read;
    public uint FirstLba, ProtectedSectors;
    public readonly bool Succeeded => Status == Lba0Status.Success && Read.Succeeded && Read.BlocksRead == 1 && Read.BytesRead == 512;
}
