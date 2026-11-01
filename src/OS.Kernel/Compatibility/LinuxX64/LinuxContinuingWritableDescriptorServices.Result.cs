using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public readonly struct Result
    {
        public readonly Progress Phase;
        public readonly long Value;
        public readonly RequestToken Request;
        public readonly DescriptorToken Descriptor;
        public readonly C.Status Failure;
        internal Result(Progress phase, long value = 0, RequestToken request = default, DescriptorToken descriptor = default, C.Status failure = C.Status.Success)
        {
            Phase = phase;
            Value = value;
            Request = request;
            Descriptor = descriptor;
            Failure = failure;
        }
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
