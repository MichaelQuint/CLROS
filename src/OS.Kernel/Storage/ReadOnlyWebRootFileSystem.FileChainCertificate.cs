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
    // Explicit positive authority over an admitted map, independent of the
    // public handle slot. Any map overwrite revokes every certificate naming it.
    private struct FileChainCertificate
    {
        internal ulong Epoch;
        internal Metadata Info;
        internal uint* Clusters;
        internal uint Count, Phase;
    }
}
