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
    /// <summary>Live handle metadata. Regular allocation is the fully checked
    /// cluster map multiplied by sectors per cluster. Directory allocation is
    /// unknown because opening a directory does not preload its complete chain.</summary>
    public struct FileInspection { public Metadata Info; public ulong AllocatedBlocks; public bool AllocationKnown; }
}
