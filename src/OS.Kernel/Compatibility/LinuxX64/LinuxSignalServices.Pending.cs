using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxSignalServices
{
    public const uint RealtimeQueueCapacity = 4;
    private const ulong TerminationRequested = 1, KernelTerminationPending = 2;

    internal static bool Ignored(State* s, uint signal)
    {
        Disposition action = ((Disposition*)s->Actions)[signal - 1];
        return action.Handler == 1 || action.Handler == 0 && signal is 17 or 18 or 23 or 28;
    }
    internal static bool JobControl(State* s, uint signal) => signal is >= 19 and <= 22 &&
        ((Disposition*)s->Actions)[signal - 1].Handler == 0;
    internal static bool RestartHandler(State* s, uint signal)
    {
        Disposition action = ((Disposition*)s->Actions)[signal - 1];
        return action.Handler > 1 && (action.Flags & 0x10000000) != 0;
    }

    internal static long Queue(State* s, ulong process, OSHostHandle identity, uint signal, LinuxSignalMask mask)
    {
        if (!Bound(s, process, identity) || signal is < 1 or > 64) return -5;
        // Linux retains even an ignored signal while blocked: its disposition
        // can change before unmasking. Explicit sigaction(ignore) flushes it.
        if (!mask.Contains(signal) && Ignored(s, signal)) return 0;
        ThreadState* t = (ThreadState*)s->Threads + ((uint)identity.Value - 1);
        if (signal < 32) { QueueStandard(t, signal, false); return 0; }
        if (t->RealtimeCount > RealtimeQueueCapacity) return -5;
        if (t->RealtimeCount == RealtimeQueueCapacity) return -11;
        t->Realtime[t->RealtimeCount++] = signal; return 0;
    }

    // Fixed owner-origin SIGTERM, never a process-kill ABI. The sticky request
    // bit survives consumption/ignore until the actual generation unbinds.
    internal static bool RequestTermination(State* s, ulong process, OSHostHandle identity, LinuxSignalMask mask)
    {
        if (!Bound(s, process, identity)) return false;
        ThreadState* t = (ThreadState*)s->Threads + ((uint)identity.Value - 1);
        if ((t->Termination & TerminationRequested) != 0) return false;
        t->Termination |= TerminationRequested;
        if (mask.Contains(15) || !Ignored(s, 15)) QueueStandard(t, 15, true);
        return true;
    }

    private static void QueueStandard(ThreadState* t, uint signal, bool kernelTermination)
    {
        ulong bit = 1UL << (int)(signal - 1);
        // Standard signals coalesce: the first pending occurrence owns siginfo.
        if (signal == 15 && (t->PendingStandard & bit) == 0)
            t->Termination = (t->Termination & ~KernelTerminationPending) | (kernelTermination ? KernelTerminationPending : 0);
        t->PendingStandard |= bit;
    }

    internal static bool PendingCode(State* s, ulong process, OSHostHandle identity, uint signal, out int code)
    {
        code = -6; // Directed user signals retain SI_TKILL.
        if (!Bound(s, process, identity) || signal is < 1 or > 64) return false;
        ThreadState* t = (ThreadState*)s->Threads + ((uint)identity.Value - 1);
        if (signal == 15 && (t->Termination & KernelTerminationPending) != 0) code = 128; // SI_KERNEL.
        return true;
    }

    // Return zero for no unblocked pending signal, one for a selected signal,
    // and EIO for invalid metadata. Selection does not acquire frame ownership.
    internal static long Next(State* s, ulong process, OSHostHandle identity, LinuxSignalMask mask, out uint signal)
    {
        signal = 0;
        if (!Bound(s, process, identity)) return -5;
        ThreadState* t = (ThreadState*)s->Threads + ((uint)identity.Value - 1);
        if (t->RealtimeCount > RealtimeQueueCapacity) return -5;
        ulong standard = t->PendingStandard & ~mask.Blocked;
        // Linux next_signal prioritizes synchronous-class signal numbers even
        // for a directed SI_TKILL event, then uses the lowest available number.
        const ulong synchronous = 0x400004d8; // ILL,TRAP,BUS,FPE,SEGV,SYS.
        if ((standard & synchronous) != 0) standard &= synchronous;
        for (uint candidate = 1; candidate < 32; candidate++)
            if ((standard & (1UL << (int)(candidate - 1))) != 0) { signal = candidate; return 1; }
        for (ulong i = 0; i < t->RealtimeCount; i++)
        {
            uint candidate = t->Realtime[i]; if (candidate is < 32 or > 64) return -5;
            if (!mask.Contains(candidate) && (signal == 0 || candidate < signal)) signal = candidate;
        }
        return signal == 0 ? 0 : 1;
    }

    internal static bool Consume(State* s, ulong process, OSHostHandle identity, uint signal)
    {
        if (!Bound(s, process, identity) || signal is < 1 or > 64) return false;
        ThreadState* t = (ThreadState*)s->Threads + ((uint)identity.Value - 1);
        if (signal < 32)
        {
            ulong bit = 1UL << (int)(signal - 1);
            if ((t->PendingStandard & bit) == 0) return false;
            t->PendingStandard &= ~bit;
            if (signal == 15) t->Termination &= ~KernelTerminationPending;
            return true;
        }
        if (t->RealtimeCount > RealtimeQueueCapacity) return false;
        for (ulong i = 0; i < t->RealtimeCount; i++)
        {
            if (t->Realtime[i] != signal) continue;
            for (ulong j = i + 1; j < t->RealtimeCount; j++) t->Realtime[j - 1] = t->Realtime[j];
            t->Realtime[--t->RealtimeCount] = 0; return true;
        }
        return false;
    }

    private static void Discard(State* s, uint signal)
    {
        // A valid sigaction commits before output copy, including queue flush.
        for (uint i = 0; i < s->Capacity; i++)
        {
            ThreadState* t = (ThreadState*)s->Threads + i;
            if (signal < 32)
            {
                t->PendingStandard &= ~(1UL << (int)(signal - 1));
                if (signal == 15) t->Termination &= ~KernelTerminationPending;
                continue;
            }
            ulong count = t->RealtimeCount;
            if (count > RealtimeQueueCapacity) continue; // Never walk beyond retained metadata.
            ulong keep = 0;
            for (ulong j = 0; j < count; j++) if (t->Realtime[j] != signal) t->Realtime[keep++] = t->Realtime[j];
            t->RealtimeCount = keep;
            for (; keep < count; keep++) t->Realtime[keep] = 0;
        }
    }
}
