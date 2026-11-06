using System;
using OS.Std.Binary;
using OS.Std.Hash;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Storage;

public static unsafe partial class Fat32Mutation
{
    /// <summary>Copied local journal framing/integrity, not a durable-storage or
    /// native authority receipt. Retain the complete metadata and before/after
    /// buffers independently; this value alone cannot reconstruct a journal.</summary>
    public readonly struct JournalReceipt
    {
        public readonly ulong Owner, Epoch, Generation;
        public readonly int Count;
        public readonly uint Crc32;
        internal JournalReceipt(ulong owner, ulong epoch, ulong generation, int count, uint crc)
        {
            Owner = owner;
            Epoch = epoch;
            Generation = generation;
            Count = count;
            Crc32 = crc;
        }
    }
}
