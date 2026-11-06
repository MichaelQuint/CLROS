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
    /// <summary>Fixed backing for one reusable operation. Path/sector/name need
    /// 256/512/260*2 bytes. History has1..4096 uints; range has1..65536 bytes.
    /// Buffers never alias each other, any slot, cache, or the block owner.</summary>
    public struct Scratch
    {
        public byte* Path, Sector, Range;
        public ushort* Name;
        public uint* History;
        public uint HistoryCapacity, RangeCapacity;
    }
}
