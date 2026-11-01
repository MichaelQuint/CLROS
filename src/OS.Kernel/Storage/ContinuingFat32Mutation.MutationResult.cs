using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    /// <summary>Copied lineage for an admitted grow/shrink/delete request.
    /// Only Success authenticates NewFile or Deleted, after every planned OUT
    /// and the observed final real FLUSH. Equal-length truncate needs no I/O.
    /// OriginalFile is the admitted physical identity, not a name-reopen guess.
    /// This is not a persistent journal or power-loss recovery receipt.</summary>
    public readonly struct MutationResult
    {
        public readonly Status Status;
        public readonly uint Bytes;
        public readonly RequestToken Request;
        public readonly FileToken OriginalFile, NewFile;
        public readonly bool Deleted;
        internal MutationResult(Status status, uint bytes, RequestToken request, FileToken original, FileToken next, bool deleted)
        { Status = status; Bytes = bytes; Request = request; OriginalFile = original; NewFile = next; Deleted = deleted; }
    }
}
