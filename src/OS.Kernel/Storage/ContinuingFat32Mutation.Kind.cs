using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    internal enum Kind : byte
    {
        Mount, Open, Overwrite, Barrier, Create, Grow, Read, Truncate, Delete, FindFree, RootMetadata
    }
}
