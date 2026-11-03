using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    public readonly struct Result
    {
        public readonly Status Status;
        public readonly uint Bytes;
        internal Result(Status status, uint bytes)
        {
            Status = status;
            Bytes = bytes;
        }
    }
}
