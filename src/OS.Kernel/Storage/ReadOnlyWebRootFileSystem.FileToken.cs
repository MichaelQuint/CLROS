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
    public readonly struct FileToken
    {
        internal readonly ulong Owner, Epoch, Generation;
        internal readonly uint Index;
        internal FileToken(ulong owner, ulong epoch, ulong generation, uint index)
        { Owner = owner; Epoch = epoch; Generation = generation; Index = index; }
        public bool IsInitialized => Owner != 0 && Epoch != 0 && Generation != 0;
    }
}
