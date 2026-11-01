using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    /// <summary>Exact initial bounded backing:128 changes, two64KiB journal
    /// buffers, three4096-word maps, three sector buffers,8192 copied input
    /// bytes,256 name words,260 VFAT words and672 encoding bytes. All extents
    /// remain stable, exclusive and disjoint until Stop succeeds or dirty
    /// ownership is explicitly recovered by a future owner. The outer owner
    /// authenticates a separate writable volume, device and unique lifetime.
    /// The existing block owner is borrowed, never reset or released here.</summary>
    public struct Admission
    {
        public Block.State* Device;
        public ulong Owner, Epoch, PartitionFirst, PartitionLast;
        public uint ChangeCapacity, ChainCapacity, InputCapacity, WorkBudget;
        public Fat32Mutation.Change* Changes;
        public byte* Before, After, Sector, Fat0, Fat1, Input, EncodedEntries;
        public uint* DirectoryMap, FileMap, WalkMap;
        public ushort* Name, LongName;
    }
}
