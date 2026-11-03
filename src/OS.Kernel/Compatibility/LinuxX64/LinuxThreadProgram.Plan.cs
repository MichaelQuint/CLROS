using OS.Kernel.Threading;
using OS.Kernel.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxThreadProgram
{
    /// <summary>Bindings reside in a separate exclusive part of ContextArena.
    /// Prepare uses byte(control, uint index): 1 installs a checked saved image
    /// and enters Gateway; 2 stages an actual wait/exit without user reentry;
    /// other values recover. Gateway returns only after its complete original
    /// CPU envelope is restored. Then the fixed FromWorker CALL captures the
    /// actual kernel continuation; its admission callback authenticates and
    /// commits Park/Exit or in-place completion before transfer. Recovery admits
    /// the retained supervisor afresh; Terminal never returns. The timer region
    /// is separately emitted/verified. All resources outlive every reference.</summary>

    public struct Plan
    {
        public ThreadContextSwitch.Owner Owner;
        public ThreadContextSwitch.Descriptor Supervisor;
        public ThreadContextSwitch.StubPlan Stub;
        public Binding* Bindings;
        public uint Count, BindingBytes;
        public X64ThreadContext* Witness;
        public ulong Prepare, Gateway, Recover, TerminalCallback;
        public bool SeparateUserFaultCapture; // Owner emits/verifies the explicit UserFaultCapture region.
    }
}
