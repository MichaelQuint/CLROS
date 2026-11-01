using OS.Boot;
using Layout = OS.Kernel.Runtime.ObjectArrayStringEETypeMethodTableLayoutContracts;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

namespace OS.Kernel.Runtime;
public static unsafe partial class NativeAotEhClauseMetadata
{
    private struct Image
    {
        internal EfiLoadedImageRangeCapturePlan Capture;
        internal M01LoadedImageVirtualMappingEvidence Mapping;
        internal byte* Owner;
        internal ulong Capacity, Handle, Base, Bytes;
    }
}
