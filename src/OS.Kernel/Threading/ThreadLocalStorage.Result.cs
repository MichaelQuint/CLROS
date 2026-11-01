using OS.Pal;

namespace OS.Kernel.Threading;
public unsafe partial struct ThreadLocalStorage
{
    /// <summary>Default is not success. Descriptor is a borrowed current snapshot
    /// (a retired extent after Reap), not transfer authority. Failure retains TLS backing; ThreadStatus preserves
    /// the exact lifecycle failure. ReapedBytes names spans scrubbed after Reap,
    /// not freed pages or independently verified hardware state.</summary>

    public readonly struct Result
    {
        public readonly Status Code;
        public readonly Binding Identity;
        public readonly ThreadContextSwitch.Descriptor Descriptor;
        public readonly Region Fs, Gs;
        public readonly ulong ReapedBytes;
        public readonly KernelThreadTable.Status ThreadStatus;
        private readonly bool _returned;
        public bool Succeeded => _returned && Code == Status.Ok;

        internal Result(Status code, Binding identity = default, ThreadContextSwitch.Descriptor descriptor = default, Region fs = default, Region gs = default, ulong reapedBytes = 0, KernelThreadTable.Status threadStatus = KernelThreadTable.Status.Ok)
        {
            Code = code;
            Identity = identity;
            Descriptor = descriptor;
            Fs = fs;
            Gs = gs;
            ReapedBytes = reapedBytes;
            ThreadStatus = threadStatus;
            _returned = true;
        }
    }
}
