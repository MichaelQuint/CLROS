using System;
using OS.Std.Binary;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class Fat32Mutation
{
    public enum Stage : byte
    {
        Data = 1, AllocateFat, PublishDirectory, RetireDirectory, ReleaseFat, FsInfo
    }
}
