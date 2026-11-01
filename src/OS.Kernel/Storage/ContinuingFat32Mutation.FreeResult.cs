using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    /// <summary>Copied candidate facts at one volume revision/request. Success
    /// found the wanted count. SearchBudgetExceeded may contain a verified
    /// partial prefix; EndOfScan reached the physical end of the remaining
    /// cursor range, not a whole-volume ENOSPC claim. No cluster is reserved.</summary>
    public readonly struct FreeResult
    {
        public readonly Status Status;
        public readonly uint Count, Scanned, SectorReads;
        public readonly RequestToken Request;
        public readonly FreeCursor NextCursor;
        internal FreeResult(Status status, uint count, uint scanned, uint reads, RequestToken request, FreeCursor next)
        { Status = status; Count = count; Scanned = scanned; SectorReads = reads; Request = request; NextCursor = next; }
    }
}
