using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;

internal static unsafe class LinuxOwnerTerminationTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong SignalKey = 0x7465726d6f776e72, Input = Base + 0x1200;
    private const ulong Word = Base + 0x1400, Timeout = Base + 0x1420, After = Base + 0x100;
    private const ulong Data = Base + 0x1800, Output = Base + 0x3400;

    private sealed class Owner : IDisposable
    {
        internal readonly LinuxBlockingPipeTests.Owner Core = new();
        internal LinuxThreadServicesTests.Model M => Core.Thread;
        internal Signals.State* State = (Signals.State*)NativeMemory.AlignedAlloc(4096, 4096);
        internal OSHostHandle Parent, Child;
        internal Owner(bool child = false, bool attached = true)
        {
            HostCheck.True(State != null && sizeof(Signals.State) <= 4096);
            new Span<byte>(State, 4096).Clear();
            HostCheck.True(Signals.Initialize(State, 4096, SignalKey, 2, new()
            { User = M.Access, CopyToUser = M.UserCopyOut, Capacity = 3, FirmwareExited = true, SingleOwnerValidated = true }));
            if (attached) HostCheck.True(M.Service->EnableSignals(Key, 2, State));
            Parent = M.Start(); HostCheck.Equal(Parent.Value, M.Kernel.Dispatch().Value);
            if (child) Child = M.Clone();
            *(ushort*)M.At(After - 2) = 0x050f;
            if (attached) { Action(); Mask(0); }
        }
        internal void Action(ulong handler = Base + 0x180, bool restart = false)
        {
            *(Signals.Disposition*)M.At(Input) = new()
            { Handler = handler, Flags = 0x04000004UL | (restart ? 0x10000000UL : 0), Restorer = Base + 0x200 };
            HostCheck.Equal(0L, Value(13, 15, Input, 0, 8));
        }
        internal void Mask(ulong mask)
        { M.Put64(Input, mask); HostCheck.Equal(0L, Value(14, 2, Input, 0, 8)); }
        internal long Value(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
        { var result = M.Call(number, a0, a1, a2, a3); HostCheck.Equal(LinuxSyscallAction.Return, result.Action); return result.Value; }
        internal bool Request() => M.Service->RequestTermination(Key, 2, Parent);
        internal void Supervisor() => M.Yield(M.Kernel.Table->Current);
        internal void ResumeParent()
        {
            var next = M.Kernel.Dispatch();
            if (next.Value == Child.Value && Child.IsValid) { M.Yield(Child); next = M.Kernel.Dispatch(); }
            HostCheck.Equal(Parent.Value, next.Value);
        }
        internal void Futex(bool timed = false)
        {
            M.Put32(Word, 7); M.Put64(Timeout, 0); M.Put64(Timeout + 8, 100);
            HostCheck.Equal(LinuxSyscallAction.Blocked, M.Call(202, Word, 128, 7, timed ? Timeout : 0).Action);
            HostCheck.True(M.Service->CommitFutexWait(Key, 2, M.Kernel.Capture(Parent), out bool handoff) && handoff);
            M.Kernel.Confirm();
        }
        internal ulong Catch(int code = 128)
        {
            HostCheck.Equal(Signals.Delivery.Delivered, M.Service->DeliverPendingSignals(Key, 2));
            ulong frame = M.CurrentImage()->Frame.Rsp;
            HostCheck.Equal(15U, M.Read32(frame + 312));
            HostCheck.Equal(0U, M.Read32(frame + 316));
            HostCheck.Equal(unchecked((uint)code), M.Read32(frame + 320));
            if (code == 128)
                HostCheck.True(new ReadOnlySpan<byte>(M.At(frame + 324), 116).IndexOfAnyExcept((byte)0) < 0);
            else
            {
                HostCheck.Equal(101U, M.Read32(frame + 328));
                HostCheck.True(new ReadOnlySpan<byte>(M.At(frame + 332), 108).IndexOfAnyExcept((byte)0) < 0);
            }
            return frame;
        }
        internal void Return(ulong frame)
        {
            M.CurrentImage()->Frame.Rsp = frame + 8;
            HostCheck.Equal(LinuxSyscallAction.RestoreUser, M.Call(15).Action);
        }
        public void Dispose()
        {
            try
            {
                Core.Drain(); HostCheck.True(Signals.End(State, SignalKey, 2));
                HostCheck.True(new ReadOnlySpan<byte>(State, sizeof(Signals.State)).IndexOfAnyExcept((byte)0) < 0);
            }
            finally { NativeMemory.AlignedFree(State); State = null; Core.Dispose(); }
        }
    }

    [HostTest("linux.owner-termination authenticates restored supervisor and retained generation without syscall capture")]
    private static void AdmissionAndReady()
    {
        using Owner o = new(); var m = o.M;
        byte[] signals = new ReadOnlySpan<byte>(o.State, sizeof(Signals.State)).ToArray();
        HostCheck.True(!o.Request()); // Actual worker still current.
        HostCheck.True(m.Kernel.Table->Yield(KernelThreadTableTests.Key, 2, o.Parent, m.Kernel.Capture(o.Parent)).Succeeded);
        HostCheck.True(m.Kernel.Table->HandoffPending && !o.Request());
        m.Kernel.Confirm();
        LinuxThreadServices copy = *m.Service;
        HostCheck.True(!copy.RequestTermination(Key, 2, o.Parent));
        HostCheck.True(!m.Service->RequestTermination(Key + 1, 2, o.Parent) &&
            !m.Service->RequestTermination(Key, 0x202, o.Parent) &&
            !m.Service->RequestTermination(Key, 2, default) &&
            !m.Service->RequestTermination(Key, 2, new(o.Parent.Value + (1UL << 32))));
        HostCheck.True(new ReadOnlySpan<byte>(o.State, sizeof(Signals.State)).SequenceEqual(signals));
        LinuxThreadServices.Slot* slot = m.Slots + ((uint)o.Parent.Value - 1);
        byte[] savedSlot = new ReadOnlySpan<byte>(slot, sizeof(LinuxThreadServices.Slot)).ToArray();
        uint reads = m.Reads, writes = m.Writes; uint ready = m.Kernel.Table->ReadyCount;
        byte* scratch = stackalloc byte[64]; LinuxSyscallGateState gate = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new()
        { UserRoot = m.Kernel.Owner.Root, MaximumCalls = 64, MaximumWrite = 64, Scratch = scratch, ScratchBytes = 64,
            FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true,
            EnableDescriptors = true, EnableThreads = true }, m.GatewayAccess));
        HostCheck.True(o.Request() && !o.Request());
        HostCheck.True(gate.Calls == 0 && gate.Status == LinuxSyscallGateStatus.Active && m.Reads == reads && m.Writes == writes);
        HostCheck.Equal(ready, m.Kernel.Table->ReadyCount);
        HostCheck.True(new ReadOnlySpan<byte>(slot, sizeof(LinuxThreadServices.Slot)).SequenceEqual(savedSlot));
        HostCheck.True(!m.View(o.Parent).Exiting && !m.Kernel.Inspect(o.Parent).CancellationRequested);
        o.ResumeParent(); o.Return(o.Catch());
        HostCheck.Equal(Signals.Delivery.None, m.Service->DeliverPendingSignals(Key, 2));
        o.Supervisor(); HostCheck.True(!o.Request()); // Consumption does not restart owner grace.

        using Owner detached = new(attached: false); detached.Supervisor();
        HostCheck.True(!detached.Request());
    }

    [HostTest("linux.owner-termination interrupts genuine futex waits with existing timed and SA_RESTART policy")]
    private static void ParkedFutex()
    {
        foreach (bool timed in new[] { false, true })
        foreach (bool restart in new[] { false, true })
        {
            using Owner o = new(); o.Action(restart: restart); o.Futex(timed);
            HostCheck.True(o.Request() && !o.Request());
            var observed = o.M.Kernel.Inspect(o.Parent);
            HostCheck.True(observed.Completion == KernelThreadTable.Completion.Interrupted && !observed.CancellationRequested);
            o.ResumeParent(); HostCheck.Equal(-4L, o.M.Service->CompleteFutexWait(Key, 2).Value);
            ulong frame = o.Catch();
            HostCheck.Equal(!timed && restart ? After - 2 : After, o.M.Read64(frame + 176));
            HostCheck.Equal(!timed && restart ? 202UL : unchecked((ulong)-4), o.M.Read64(frame + 152));
            o.Return(frame); HostCheck.Equal(0U, o.M.Kernel.Table->DeadlineCount);
            HostCheck.True(o.M.Service->MayChangeUserRange(Key, 2, Word, 4) && !o.M.View(o.Parent).Exiting);
        }
    }

    [HostTest("linux.owner-termination retains prior real wake and elapsed deadline winners with a pending kernel handler")]
    private static void WaitWinners()
    {
        foreach (bool deadline in new[] { false, true })
        {
            using Owner o = new(child: true); o.Futex(deadline);
            if (deadline) o.M.Now = 1100;
            else
            {
                HostCheck.Equal(o.Child.Value, o.M.Kernel.Dispatch().Value);
                HostCheck.Equal(1L, o.Value(202, Word, 129, 1)); o.Supervisor();
            }
            HostCheck.True(o.Request());
            HostCheck.Equal(deadline ? KernelThreadTable.Completion.Deadline : KernelThreadTable.Completion.Woken,
                o.M.Kernel.Inspect(o.Parent).Completion);
            o.ResumeParent(); HostCheck.Equal(deadline ? -110L : 0L, o.M.Service->CompleteFutexWait(Key, 2).Value);
            ulong frame = o.Catch();
            HostCheck.Equal(deadline ? unchecked((ulong)-110) : 0UL, o.M.Read64(frame + 152));
            HostCheck.Equal(After, o.M.Read64(frame + 176)); o.Return(frame);
        }
    }

    [HostTest("linux.owner-termination failed wait clock retains the request and fails closed without cancellation or retry")]
    private static void InterruptFailure()
    {
        using Owner o = new(); o.Futex(true); o.M.FailClock(true);
        HostCheck.True(!o.Request() && o.M.Service->Failed && !o.Request());
        var observed = o.M.Kernel.Inspect(o.Parent);
        HostCheck.True(observed.State == KernelThreadTable.State.Waiting &&
            observed.Completion == KernelThreadTable.Completion.None && !observed.CancellationRequested);
        HostCheck.True(!o.M.View(o.Parent).Exiting && o.M.View(o.Parent).Wait.IsValid);
        o.M.FailClock(false); // The real owner's forced cleanup remains possible.
    }

    [HostTest("linux.owner-termination masked and ignored parked targets wait for a real completion")]
    private static void NonActionableWaits()
    {
        foreach (bool masked in new[] { false, true })
        {
            using Owner o = new(child: true);
            if (masked) o.Mask(1UL << 14); else o.Action(1);
            o.Futex(); ulong sequence = o.M.View(o.Parent).Wait.Sequence;
            HostCheck.True(o.Request() && !o.Request());
            var observed = o.M.Kernel.Inspect(o.Parent);
            HostCheck.True(observed.State == KernelThreadTable.State.Waiting &&
                observed.Completion == KernelThreadTable.Completion.None && observed.Wait.Sequence == sequence && !observed.CancellationRequested);
            HostCheck.Equal(o.Child.Value, o.M.Kernel.Dispatch().Value);
            HostCheck.Equal(1L, o.Value(202, Word, 129, 1)); o.Supervisor(); o.ResumeParent();
            HostCheck.Equal(0L, o.M.Service->CompleteFutexWait(Key, 2).Value);
            HostCheck.Equal(Signals.Delivery.None, o.M.Service->DeliverPendingSignals(Key, 2));
            if (masked) { o.Mask(0); o.Return(o.Catch()); }
            else { o.Action(); HostCheck.Equal(Signals.Delivery.None, o.M.Service->DeliverPendingSignals(Key, 2)); }
            HostCheck.True(!o.M.View(o.Parent).Exiting);
        }
    }

    [HostTest("linux.owner-termination standard coalescing retains first provenance and consumption restores tgkill provenance")]
    private static void Coalescing()
    {
        foreach (bool kernelFirst in new[] { false, true })
        {
            using Owner o = new();
            if (!kernelFirst) HostCheck.Equal(0L, o.Value(234, 101, 101, 15));
            o.Supervisor(); HostCheck.True(o.Request()); o.ResumeParent();
            if (kernelFirst) HostCheck.Equal(0L, o.Value(234, 101, 101, 15));
            o.Return(o.Catch(kernelFirst ? 128 : -6));
            HostCheck.Equal(Signals.Delivery.None, o.M.Service->DeliverPendingSignals(Key, 2));
            HostCheck.Equal(0L, o.Value(234, 101, 101, 15)); o.Return(o.Catch(-6));
            o.Supervisor(); HostCheck.True(!o.Request());
        }
    }

    [HostTest("linux.owner-termination explicit ignore discards provenance without rearming and default fatal remains a real failure exit")]
    private static void Dispositions()
    {
        using (Owner o = new())
        {
            o.Mask(1UL << 14); o.Action(1); o.Supervisor(); HostCheck.True(o.Request()); o.ResumeParent();
            o.Action(); o.Mask(0); o.Return(o.Catch()); // Blocked ignored request survived until action changed.
        }
        using (Owner o = new())
        {
            o.Supervisor(); HostCheck.True(o.Request()); o.ResumeParent();
            o.Action(1); o.Action(); // Explicit ignored registration flushes the queued kernel occurrence.
            HostCheck.Equal(Signals.Delivery.None, o.M.Service->DeliverPendingSignals(Key, 2));
            HostCheck.Equal(0L, o.Value(234, 101, 101, 15)); o.Return(o.Catch(-6));
            o.Supervisor(); HostCheck.True(!o.Request());
        }
        using (Owner o = new())
        {
            o.Action(0); o.Supervisor(); HostCheck.True(o.Request());
            HostCheck.True(!o.M.View(o.Parent).Exiting && !o.M.Kernel.Inspect(o.Parent).CancellationRequested);
            o.ResumeParent(); HostCheck.Equal(Signals.Delivery.Terminate, o.M.Service->DeliverPendingSignals(Key, 2));
            HostCheck.True(o.M.View(o.Parent).Exiting && o.M.View(o.Parent).ExitCode == 143);
            o.Supervisor(); HostCheck.True(!o.Request());
        }
    }

    [HostTest("linux.owner-termination stale reaped and reused target generations cannot inherit requests")]
    private static void Generations()
    {
        using Owner o = new(child: true); var m = o.M;
        o.Supervisor(); HostCheck.True(o.Request()); o.ResumeParent();
        // Exit the requested parent while its pending occurrence is still retained.
        HostCheck.Equal(LinuxSyscallAction.ThreadExited, m.Call(60).Action);
        HostCheck.True(m.Service->CommitExit(Key, 2, m.Kernel.Capture(o.Parent), out long clear) && clear == 0);
        HostCheck.True(!o.Request()); m.Kernel.Confirm(); HostCheck.True(!o.Request());
        HostCheck.True(m.Service->Reap(Key, 2, o.Parent, m.Kernel.SupervisorRsp, out _));
        HostCheck.True(!o.Request());
        HostCheck.Equal(o.Child.Value, m.Kernel.Dispatch().Value); var reused = m.Clone();
        HostCheck.True((uint)reused.Value == (uint)o.Parent.Value && reused.Value != o.Parent.Value);
        o.Supervisor(); HostCheck.True(!o.Request());
        HostCheck.True(m.Service->RequestTermination(Key, 2, reused) && !m.Service->RequestTermination(Key, 2, reused));
        HostCheck.Equal(reused.Value, m.Kernel.Dispatch().Value); o.Return(o.Catch());
    }

    [HostTest("linux.owner-termination stolen pipe readiness rechecks the kernel signal before repark without losing wait ownership")]
    private static void StolenPipeReadiness()
    {
        using Owner o = new(child: true); var pipe = o.Core.Pipe();
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.Core.Call(0, pipe.Read, Output, 1).Action); o.Core.Park(o.Parent);
        HostCheck.Equal(o.Child.Value, o.M.Kernel.Dispatch().Value); o.M.At(Data)[0] = 0x61;
        HostCheck.Equal(1L, o.Core.Value(1, pipe.Write, Data, 1)); o.Supervisor();
        HostCheck.True(o.Request()); // Already-completed pipe wait stays Woken.
        HostCheck.Equal(KernelThreadTable.Completion.Woken, o.M.Kernel.Inspect(o.Parent).Completion);
        o.ResumeParent(); o.M.Yield(o.Parent); HostCheck.Equal(o.Child.Value, o.M.Kernel.Dispatch().Value);
        HostCheck.Equal(1L, o.Core.Value(0, pipe.Read, Data, 1)); o.Supervisor(); o.ResumeParent();
        HostCheck.Equal(LinuxSyscallAction.Blocked, o.M.Service->CompletePipeWait(Key, 2).Action);
        HostCheck.Equal(KernelThreadTable.Completion.Interrupted, o.M.Kernel.Inspect(o.Parent).Completion);
        HostCheck.True(o.M.Service->CommitPipeWait(Key, 2, o.M.Kernel.Capture(o.Parent), out bool handoff) && !handoff);
        HostCheck.Equal(-4L, o.M.Service->CompletePipeWait(Key, 2).Value);
        HostCheck.Equal(0U, o.Core.Files.State->RetainedPipeOperations);
        o.Return(o.Catch()); HostCheck.True(!o.M.View(o.Parent).Exiting);
    }
}
