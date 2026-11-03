using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public readonly struct LockRequest(short type, short whence, long start, long length, int pid = 0)
    {
        public readonly short Type = type, Whence = whence;
        public readonly long Start = start, Length = length;
        public readonly int Pid = pid;
    }
}
