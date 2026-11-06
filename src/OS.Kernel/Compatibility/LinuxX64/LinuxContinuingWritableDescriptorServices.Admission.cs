using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public struct Admission
    {
        public C.State* Engine;
        public Descriptor* Descriptors;
        public Description* Descriptions;
        public byte* Input;
        public ulong Epoch;
        public uint DescriptorCount, DescriptionCount, InputBytes;
        public bool ExclusiveOwnerValidated;
    }
}
