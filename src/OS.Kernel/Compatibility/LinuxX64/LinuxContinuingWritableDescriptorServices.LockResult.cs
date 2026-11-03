using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public readonly struct LockResult
    {
        public readonly long Value;
        public readonly LockRequest Lock;
        public readonly ProcessToken Process;
        internal LockResult(long value, LockRequest request = default, ProcessToken process = default)
        {
            Value = value;
            Lock = request;
            Process = process;
        }
    }
}
