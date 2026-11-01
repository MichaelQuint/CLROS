using OS.Boot;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
public static unsafe partial class GCStaticRootsMalformedMetadata
{
    private struct Root
    {
        internal ulong Phase, Integrity, Self, Capacity, TypeOwner, TypeCapacity, Handle, Bump, BumpCapacity;
        internal EfiLoadedImageRangeCapturePlan Capture;
        internal GCStaticReadableWindow Metadata, Cells;
        internal RhpNewFastHeapAccess Heap;
        internal GCStaticMetadataResult Decoded;
        internal RhpNewFastResult Receipt;
        internal fixed byte Snapshot[68]; // entry4 + descriptor/MT40 + Object24
        internal uint Reserved;
    }
}
