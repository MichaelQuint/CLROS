#if M07_026_PROBE
using OS.Kernel.Threading;
using OS.Pal;
using Cooperative = OS.Kernel.Threading.ThreadContextSwitch;
using SavedContext = OS.Kernel.Threading.X64ThreadContext;
using Program = OS.Kernel.Threading.KernelThreadProgram;
using ProgressProgram = OS.Kernel.Threading.KernelThreadProgressProgram;
using Lifecycle = OS.Kernel.Threading.KernelThreadTable;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Pool pages/context offsets and live admission are the accepted M09
        // layout. One persistent ready worker runs on its guarded stack; no
        // timer IRQ, arbitrary entry, firmware reclamation or SMP is supplied.
        private struct NetworkSchedulerControl
        {
            internal Storage* State;
            internal Program.Plan Plan;
            internal Lifecycle Threads;
            internal KernelThreadSlot Slot;
            internal OSHostHandle Ready, Worker;
            internal DeadlineQueue.Entry Deadline;
            internal ulong Key, Progress, PacketDispatches, TimeWaitDispatches, LastProgress, NextDue, MaximumGap;
            internal uint Stop, Bound, Selected, Failed, Transfers;
            internal fixed ulong Cpu[19];
        }

















































































































































































































































    }
}
#endif
