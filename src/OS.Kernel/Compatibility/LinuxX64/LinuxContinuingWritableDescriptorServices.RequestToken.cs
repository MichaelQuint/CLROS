using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public readonly struct RequestToken
    {
        public readonly ulong Owner, Epoch, Generation;
        internal RequestToken(State* s)
        {
            Owner = (ulong)s;
            Epoch = s->Input.Epoch;
            Generation = s->Work.Generation;
        }
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
