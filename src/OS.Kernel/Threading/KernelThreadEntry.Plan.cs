namespace OS.Kernel.Threading;

public static unsafe partial class KernelThreadEntry
{
    /// <summary>Bound entry state. Start uses Win64 byte(control, identity):
    /// 1 runs Entry(argument), 2 exits cooperatively as cancelled, anything else
    /// takes RejectRip. Entry returns signed EAX. Both paths put the exit value
    /// in EDX and CALL the separately emitted ThreadContextSwitch exit stub.
    /// Its admission callback reads the captured RDX, records Exit, and transfers
    /// to the retained supervisor. It must never resume ExitResumeOffset; that
    /// continuation rejects. Explicit worker exit uses the same EDX/stub ABI.
    /// Callback/control declarations supply no mapping or execution authority.</summary>

    public struct Plan
    {
        public ThreadContextSwitch.Owner Owner;
        public ThreadContextSwitch.Descriptor Initial;
        public uint CodeOffset, ControlBytes;
        public ulong Entry, Argument, Start, Control, ExitStub, RejectRip;
    }
}
