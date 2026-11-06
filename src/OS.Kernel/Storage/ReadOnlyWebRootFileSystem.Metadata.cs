using OS.Drivers;
using OS.Std.Binary;
using OS.Std.Time;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Port = OS.Storage.WritableBlockReadPort;
#endif

namespace OS.Storage;

public static unsafe partial class ReadOnlyWebRootFileSystem
{
    /// <summary>Stable physical short-entry identity and declared FAT UTC time.
    /// The fixture's UTC policy is an admission assumption, not a timezone guess.
    /// ModifiedSeconds is zero for an all-zero FAT date/time (unspecified);
    /// nonzero FAT timestamps must be valid Gregorian UTC dates from 1980 onward.
    /// Attributes distinguish regular files/directories; length is bytes.</summary>
    public struct Metadata
    {
        public ulong Inode;
        public long ModifiedSeconds;
        public uint FirstCluster, Length;
        public ushort NameBytes;
        public byte Attributes;
        public readonly bool IsDirectory => (Attributes & 0x10) != 0;
    }
}
