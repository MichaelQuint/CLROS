using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public readonly struct DescriptorToken
    {
        public readonly ulong Owner, Epoch, Generation;
        public readonly uint Number;
        internal DescriptorToken(State* s, uint number)
        {
            Owner = (ulong)s;
            Epoch = s->Input.Epoch;
            Number = number;
            Generation = s->Input.Descriptors[number].Generation;
        }
    }
}
