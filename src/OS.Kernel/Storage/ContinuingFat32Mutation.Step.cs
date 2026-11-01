using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    internal enum Step : byte { Bpb, RootChain, Scan, TargetChain, OtherChain, Plan, Issue, Flush, GrowCandidates, GrowData, GrowFat, GrowDirectory, GrowFsInfo, ReadData, ShrinkDirectory, DeleteDirectory, ReleaseFat, FindFree }
}
