using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public readonly struct ProcessResult
    {
        public readonly long Value;
        public readonly ProcessToken Process;
        internal ProcessResult(long value, ProcessToken process = default)
        {
            Value = value;
            Process = process;
        }
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
