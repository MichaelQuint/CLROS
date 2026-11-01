using OS.Std.Binary;
using B = OS.Storage.WritableBlockSession;
using Gpt = OS.Storage.PrimaryGPTIndependentlyBoundsCRC;
using Fat = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Bootstrap
{
    public enum Status : byte
    { Uninitialized, Success, Pending, Ready, InvalidState, InvalidStorage, InvalidDevice, InvalidBudget, StaleEpoch,
        BudgetExceeded, InvalidMbr, InvalidGpt, MissingPartition, AmbiguousPartition, UnsupportedAttributes, InvalidVolume, BlockFailure, Cancelled, Busy }
}
