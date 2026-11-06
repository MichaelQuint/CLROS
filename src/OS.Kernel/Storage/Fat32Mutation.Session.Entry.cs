using System;
using OS.Std.Binary;
using OS.Std.Hash;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Storage;

public static unsafe partial class Fat32Mutation
{
    public ref partial struct Session
    {
        private struct Entry
        {
            internal ulong Offset;
            internal uint Cluster, Length, Directory;
            internal int StartSlot, Slots;
            internal byte Attributes;
        }
    }
}
