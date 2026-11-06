namespace OS.Kernel.Threading;

public static unsafe partial class X64TimerInterrupt
{
    /// <summary>Disjoint fields in the live R12 control extent. R13 independently
    /// holds the pinned two-word object address. The worker never calls, yields or
    /// returns: IRQ-written commands permit each initialization/publication phase,
    /// then actual interrupt redirection is its only exit. The owner initializes
    /// phase1/command0/root0 and admits data/stack/code before entry.</summary>

    public struct WorkerPlan
    {
        public uint Phase, Command, Progress, First, Second, Root, ControlBytes;
    }
}
