using OS.Pal;

namespace OS.Kernel.Threading;
public unsafe partial struct WaitAddress
{
    /// <summary>Default is not success. ThreadFailure preserves the exact
    /// scheduler status. Wake Count reports committed winners even if a later
    /// corrupted live context rejects; earlier winners are never rolled back.
    /// A returned Wait remains owned by the scheduler through Park/completion.</summary>

    public readonly struct Result
    {
        public readonly Status Code;
        public readonly Binding Address;
        public readonly KernelThreadWait Wait;
        public readonly uint Count;
        public readonly KernelThreadTable.Status ThreadStatus;
        private readonly bool _returned;
        public bool Succeeded => _returned && Code == Status.Ok;

        internal Result(Status code, Binding address = default, KernelThreadWait wait = default, uint count = 0, KernelThreadTable.Status threadStatus = KernelThreadTable.Status.Ok)
        {
            Code = code;
            Address = address;
            Wait = wait;
            Count = count;
            ThreadStatus = threadStatus;
            _returned = true;
        }
    }
}
