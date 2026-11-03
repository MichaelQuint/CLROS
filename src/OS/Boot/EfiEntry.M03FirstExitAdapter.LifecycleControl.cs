#if M09_006_PROBE || M09_009_PROBE || M09_010_PROBE
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;
using OS.Pal;
using Lifecycle = OS.Kernel.Threading.KernelThreadTable;
using WorkerSlot = OS.Kernel.Threading.KernelThreadSlot;
using WorkerWait = OS.Kernel.Threading.KernelThreadWait;
using WorkerEntry = OS.Kernel.Threading.KernelThreadEntry;
using WorkerProgram = OS.Kernel.Threading.KernelThreadProgram;
using Cooperative = OS.Kernel.Threading.ThreadContextSwitch;
using SavedContext = OS.Kernel.Threading.X64ThreadContext;
using TimerIrq = OS.Kernel.Threading.X64TimerInterrupt;
using TickTimer = OS.Kernel.Threading.SchedulerTimer;
using Deadlines = OS.Kernel.Threading.DeadlineQueue;

namespace OS.Boot;

#pragma warning disable 649 // Adjacent caller-owned buffers are written through their first element pointers.

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Caller-owned metadata resides in the retained context arena. Only the
        // single BSP/IF-clear owner accesses it; no managed object is published.
        private struct LifecycleControl
        {
            internal Storage* State;
            internal WorkerProgram.Plan Plan;
            internal Lifecycle Threads;
            internal WorkerSlot SlotA, SlotB;
            internal OSHostHandle ReadyA, ReadyB;
            internal Deadlines.Entry DeadlineA, DeadlineB;
            internal OSHostHandle A, B;
            internal TickTimer Timer;
            internal TimerIrq.Plan Handler;
            internal ulong Key, OriginalIst, Epoch, Transfers, WakeIntervals, Reuse;
            internal uint Installed, Failed, Selected, JoinObservations, Cancellations, Reaped;
            internal fixed ulong Cpu[19];
            internal fixed ulong OriginalIdtr[2], NewIdtr[2], ObservedIdtr[2];
            internal fixed byte OriginalTss[104];
#if M09_009_PROBE || M09_010_PROBE
            internal ThreadWaitControl* Wait;
#endif
        }

#if M09_009_PROBE || M09_010_PROBE

#else

#endif

#if M09_009_PROBE || M09_010_PROBE

#else

#endif

#if M09_009_PROBE || M09_010_PROBE

#else

#endif

#if M09_009_PROBE || M09_010_PROBE

#else

#endif

#if M09_009_PROBE || M09_010_PROBE

#else

#endif

    }
}
#endif
