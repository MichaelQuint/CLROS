using OS.Boot;

namespace OS.Kernel.Runtime;

public static unsafe partial class X64PERuntimeFunctionUnwindMetadataHostileInput
{
    private struct Image
    {
        internal EfiLoadedImageRangeCapturePlan Capture;
        internal M01LoadedImageVirtualMappingEvidence Mapping;
        internal byte* Owner;
        internal ulong OwnerCapacity, Handle, Base, Bytes;
        internal uint Directory, DirectoryBytes, Count;
    }
}
