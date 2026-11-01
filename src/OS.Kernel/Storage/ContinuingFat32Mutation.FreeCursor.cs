using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    /// <summary>Copied forward-only scan position, invalidated by any new
    /// request or issued mutation. It is not a free-space reservation. Only
    /// CreateFreeCursor or a consumed FreeResult supplies a current cursor.</summary>
    public readonly struct FreeCursor
    {
        public readonly ulong Owner, Epoch, Revision, Generation;
        public readonly uint NextCluster;
        internal FreeCursor(State* s, uint next)
        { Owner = (ulong)s; Epoch = s->Input.Epoch; Revision = s->Revision; Generation = s->RequestGeneration; NextCluster = next; }
    }
}
