namespace OS.Kernel.Threading;

public static unsafe partial class KernelThreadProgram
{
    /// <summary>Pinned current descriptor resources and callback extents.
    /// A/B entries use the same contexts as the corresponding switch endpoints.
    /// Admission receives Win64 byte(control, source, destination). Start is the
    /// KernelThreadEntry gate. Recovery freshly admits the supervisor and marks
    /// failure before restoration; Terminal never returns. The witness retains
    /// actual supervisor GPR/FX/FS/GS values before driver scratch instructions.
    /// Plan storage and callback addresses grant no execution authority.</summary>

    public struct Plan
    {
        public ThreadContextSwitch.Owner Owner;
        public ThreadContextSwitch.Descriptor Supervisor, A, B;
        public ThreadContextSwitch.StubPlan Stub;
        public KernelThreadEntry.Plan First, Second;
        public X64ThreadContext* Witness;
        public ulong RecoverCallback, TerminalCallback;
    }
}
