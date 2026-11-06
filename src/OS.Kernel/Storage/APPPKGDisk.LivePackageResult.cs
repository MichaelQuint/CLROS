using System;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Std.Text;

namespace OS.Storage;

public struct LivePackageResult
{
    public LivePackageStatus Status;
    public ushort FileCount;
    public uint ContentOffset, ContentLength, PackageCrc32;
    public readonly bool Succeeded => Status == LivePackageStatus.Success;
}
