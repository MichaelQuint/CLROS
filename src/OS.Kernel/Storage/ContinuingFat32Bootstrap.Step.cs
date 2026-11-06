using OS.Std.Binary;
using B = OS.Storage.WritableBlockSession;
using Gpt = OS.Storage.PrimaryGPTIndependentlyBoundsCRC;
using Fat = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Bootstrap
{
    internal enum Step : byte
    {
        Mbr, Header, Entries, Select, Bpb
    }
}
