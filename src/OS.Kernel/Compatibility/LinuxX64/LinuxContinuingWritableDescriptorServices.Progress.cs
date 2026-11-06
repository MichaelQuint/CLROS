using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public enum Progress : byte
    {
        Rejected,
        Immediate,
        Pending,
        Ready,
        RecoveryRequired,
        Retired
    }
}
