namespace OS.Kernel.Threading;

public static unsafe partial class ThreadContextSwitch
{
    /// <summary>
    /// One original driver and two mechanical workers with fixed code labels.
    /// The callback owns bounded progress and pins the three descriptors/control
    /// extents. Witness is separate live context-sized storage written by the
    /// resumed driver before it changes any GPR. BCount and FlagScratch lie in
    /// callback control storage; the latter retains actual restored flags across
    /// B's bounded branch, without substituting expected flags. RecoveryCallback
    /// returns byte1 only after fresh original-context admission, records failure
    /// and preserves context storage. TerminalCallback is owned no-return failure
    /// reporting. Both callback addresses need separate RX/image admission.
    /// These declarations grant no transfer or resource-cleanup authority.
    /// </summary>

    public struct ProgramPlan
    {
        public Owner Owner;
        public Descriptor Original, A, B;
        public StubPlan Stub;
        public X64ThreadContext* Witness;
        public ulong BCount, FlagScratch, RecoveryCallback, TerminalCallback;
        public byte Cycles;
    }
}
