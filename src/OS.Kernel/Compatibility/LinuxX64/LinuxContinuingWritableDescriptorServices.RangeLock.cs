using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public struct RangeLock
    {
        internal C.FileToken File;
        internal ProcessToken Process;
        internal long First, Last;
        internal short Type;
        internal bool Active;
    }
}
