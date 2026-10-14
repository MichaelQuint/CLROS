using OS.Pal;

namespace OS.Kernel.Threading;

/// <summary>Unmanaged storage borrowed exclusively by one KernelThreadTable.
/// Callers provide aligned, mapped storage but never modify or copy bound slots.
/// The table publishes complete bindings, retains exit values through reap and
/// retires exhausted generations. Context/stack pages remain caller-owned.</summary>
public struct KernelThreadSlot
{
    internal uint Generation, Pins;
    internal KernelThreadTable.State State;
    internal KernelThreadTable.WaitKind WaitKind;
    internal KernelThreadTable.Completion Completion;
    internal bool Cancellation, HasDeadline;
    internal ulong Sequence, Entry, Argument;
    internal int ExitValue, JoinedExitValue;
    internal ThreadContextSwitch.Descriptor Context;
    internal OSHostHandle JoinTarget, Joiner;
}
