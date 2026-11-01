using OS.Pal;

namespace OS.Kernel.Threading;
public unsafe partial struct KernelThreadTable
{
    /// <summary>By-value operation/snapshot result. Default is not successful.
    /// Pending means one completion won and is retained until ready space exists.
    /// Context/entry are borrowed descriptions, never execution or release grants.
    /// ExitValue belongs to this thread; JoinedExitValue is the copied join target
    /// value, so a later exit cannot be confused with an earlier joined outcome.
    /// Only successful Reap returns storage whose contents have been scrubbed.</summary>

    public readonly struct Result
    {
        public readonly Status Code;
        public readonly OSHostHandle Identity;
        public readonly KernelThreadWait Wait;
        public readonly State State;
        public readonly Completion Completion;
        public readonly bool CancellationRequested;
        public readonly int ExitValue, JoinedExitValue;
        public readonly uint Pins;
        public readonly ulong Entry, Argument;
        public readonly ThreadContextSwitch.Descriptor Context;
        private readonly bool _returned;
        public bool Succeeded => _returned && (Code == Status.Ok || Code == Status.Pending);

        internal Result(Status code, OSHostHandle identity = default, KernelThreadSlot slot = default)
        {
            Code = code;
            Identity = identity;
            State = slot.State;
            Completion = slot.Completion;
            Wait = slot.Sequence == 0 ? default : new(identity, slot.Sequence);
            CancellationRequested = slot.Cancellation;
            ExitValue = slot.ExitValue;
            JoinedExitValue = slot.JoinedExitValue;
            Pins = slot.Pins;
            Entry = slot.Entry;
            Argument = slot.Argument;
            Context = slot.Context;
            _returned = true;
        }
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
}
