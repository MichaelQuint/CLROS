using System;
using OS.Std.Binary;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class Fat32Mutation
{
    public enum Status : byte
    {
        Uninitialized, Success, InvalidStorage, InvalidVolume, InvalidName, InvalidAlias,
        CorruptDirectory, CorruptChain, MirrorMismatch, NotFound, AlreadyExists,
        NotFile, ReadOnly, CapacityExceeded, TraversalBudgetExceeded, DiskFull,
        StaleToken, Busy, Planned, Pending, Cancelled, RecoveryRequired,
        BlockFailure, SnapshotChanged, GenerationExhausted, InvalidJournal
    }
}
