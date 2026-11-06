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
    public enum Status : byte
    {
        Uninitialized, Success, Pending, Ready, Entry, EndOfDirectory, EndOfFile,
        Busy, Full, InvalidAccess, InvalidState, InvalidStorage, InvalidPath,
        OutsideRoot, UnsupportedEncoding, UnsupportedGeometry, StaleToken,
        GenerationExhausted, NotFound, NotDirectory, IsDirectory, Ambiguous,
        InvalidOffset, ShortBuffer, OutputFull, TraversalBudgetExceeded,
        CorruptDirectory, CorruptFile, ChainLoop, Truncated, Overlong, BadCluster,
        FreeCluster, ReservedCluster, InvalidCluster, InvalidBpb, MirrorMismatch, DeviceFailed, Cancelled
    }
}
