using System;
using OS.Std.Binary;
using OS.Std.Hash;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Storage;

public static unsafe partial class Fat32Mutation
{
    /// <summary>Conservative whole-volume generation. Every issued mutation or
    /// recovery revokes all old file tokens, including an unlinked/reused slot.
    /// This preparatory contract does not promise POSIX open-unlink behavior.</summary>
    public readonly struct FileToken
    {
        public readonly ulong Owner, Epoch, Generation, EntryOffset;
        public readonly uint FirstCluster, Length;
        internal FileToken(ulong owner, ulong epoch, ulong generation, ulong entry, uint first, uint length)
        {
            Owner = owner;
            Epoch = epoch;
            Generation = generation;
            EntryOffset = entry;
            FirstCluster = first;
            Length = length;
        }
    }
}
