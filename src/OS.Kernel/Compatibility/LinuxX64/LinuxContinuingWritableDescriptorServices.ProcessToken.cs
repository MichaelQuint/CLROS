using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public readonly struct ProcessToken
    {
        public readonly ulong Owner, Epoch, Generation;
        public readonly uint Index, Pid;
        internal ProcessToken(State* s, uint index)
        {
            Owner = (ulong)s;
            Epoch = s->Input.Epoch;
            Index = index;
            Generation = s->LockInput.Processes[index].Generation;
            Pid = s->LockInput.Processes[index].Pid;
        }
    }
}
