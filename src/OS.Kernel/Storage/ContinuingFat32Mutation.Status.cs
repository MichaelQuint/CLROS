using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    public enum Status : byte
    {
        Uninitialized, Success, Pending, Ready, Idle, InvalidStorage, InvalidState,
        InvalidVolume, InvalidName, NotFound, NotFile, ReadOnly, Unsupported,
        CorruptDirectory, CorruptChain, MirrorMismatch, BudgetExceeded,
        CapacityExceeded, StaleToken, Busy, Cancelled, GenerationExhausted,
        BlockFailure, RecoveryRequired, AlreadyExists, InvalidAllocation,
        SearchBudgetExceeded, EndOfScan
    }
}
