using OS.Boot;

namespace OS.Kernel.Runtime;

public static unsafe partial class TypeManagerModuleMetadataLiveNativeAOTImage
{
    // ABI storage follows this private POD. The immutable byte snapshot follows
    // the ABI. No native consumer receives a pointer to this CLROS state.
    private struct State
    {
        internal ulong Phase, Owner, Capacity;
        internal EfiLoadedImageRangeCapturePlan Capture;
        internal Image Image;
    }
}
