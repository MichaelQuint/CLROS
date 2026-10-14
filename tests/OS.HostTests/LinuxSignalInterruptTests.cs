using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;
using Descriptors = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe class LinuxSignalInterruptTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong Input = Base + 0x1200, Word = Base + 0x1400, Timeout = Base + 0x1420;
    private const ulong Data = Base + 0x1800, Output = Base + 0x4000, After = Base + 0x100;
    private const ulong SignalKey = 0x736967696e747270;

    private sealed class Owner : IDisposable
    {
        internal readonly LinuxBlockingPipeTests.Owner Core = new();
        internal LinuxThreadServicesTests.Model M => Core.Thread;
        internal Signals.State* State = (Signals.State*)NativeMemory.AlignedAlloc(4096,4096);
        internal OSHostHandle Parent, Child;
        internal Owner(bool restart = false)
        {
            HostCheck.True(State != null && sizeof(Signals.State) <= 4096); new Span<byte>(State,4096).Clear();
            HostCheck.True(Signals.Initialize(State,4096,SignalKey,2,new()
            { User = M.Access,CopyToUser = M.UserCopyOut,Capacity = 3,FirmwareExited = true,SingleOwnerValidated = true }));
            HostCheck.True(M.Service->EnableSignals(Key,2,State));
            Parent = M.Start(); M.Kernel.Dispatch(); Child = M.Clone();
            *(ushort*)M.At(After - 2) = 0x050f;
            Action(restart); M.Put64(Input,0); HostCheck.Equal(0L,Value(14,2,Input,0,8));
        }
        internal LinuxSyscallResult Call(ulong number,ulong a0 = 0,ulong a1 = 0,ulong a2 = 0,ulong a3 = 0,ulong a4 = 0,ulong a5 = 0)
        {
            var image = M.CurrentImage();
            image->Gpr[3] = a0; image->Gpr[2] = a1; image->Gpr[4] = a2;
            image->Gpr[7] = a3; image->Gpr[5] = a4; image->Gpr[6] = a5;
            return M.Call(number,a0,a1,a2,a3,a4,a5); // Model the actual already captured syscall GPRs too.
        }
        internal long Value(ulong number,ulong a0 = 0,ulong a1 = 0,ulong a2 = 0,ulong a3 = 0,ulong a4 = 0,ulong a5 = 0)
        { var r = Call(number,a0,a1,a2,a3,a4,a5); HostCheck.Equal(LinuxSyscallAction.Return,r.Action); return r.Value; }
        internal void Action(bool restart,ulong handler = Base + 0x180)
        {
            *(Signals.Disposition*)M.At(Input) = new() { Handler = handler,Flags = 0x04000004UL | (restart ? 0x10000000UL : 0),Restorer = Base + 0x200 };
            HostCheck.Equal(0L,Value(13,10,Input,0,8));
        }
        internal void Park()
        {
            var current = M.Kernel.Table->Current; bool pipe = M.View(current).PipePending;
            bool ok = pipe ? M.Service->CommitPipeWait(Key,2,M.Kernel.Capture(current),out bool handoff)
                : M.Service->CommitFutexWait(Key,2,M.Kernel.Capture(current),out handoff);
            HostCheck.True(ok && handoff); M.Kernel.Confirm();
        }
        internal void Sender() { Park(); HostCheck.Equal(Child.Value,M.Kernel.Dispatch().Value); }
        internal void Send() => HostCheck.Equal(0L,Value(234,101,101,10));
        internal void ParentAgain() { M.Yield(Child); HostCheck.Equal(Parent.Value,M.Kernel.Dispatch().Value); }
        internal LinuxSyscallResult Complete()
        { return M.View(Parent).PipePending ? M.Service->CompletePipeWait(Key,2) : M.Service->CompleteFutexWait(Key,2); }
        internal Signals.Delivery Deliver() => M.Service->DeliverPendingSignals(Key,2);
        internal ulong Caught()
        { HostCheck.Equal(Signals.Delivery.Delivered,Deliver()); return M.CurrentImage()->Frame.Rsp; }
        internal LinuxSyscallResult Return(ulong frame)
        { M.CurrentImage()->Frame.Rsp = frame + 8; return Call(15); }
        internal void Futex(bool timed = false,bool bitset = false,ulong nanos = 100)
        {
            M.Put32(Word,7); M.Put64(Timeout,0); M.Put64(Timeout + 8,nanos);
            HostCheck.Equal(LinuxSyscallAction.Blocked,Call(202,Word,bitset ? 137UL : 128UL,7,timed ? Timeout : 0,0,bitset ? 4UL : 0).Action);
        }
        public void Dispose()
        {
            try { Core.Drain(); HostCheck.True(Signals.End(State,SignalKey,2)); }
            finally { NativeMemory.AlignedFree(State); State = null; Core.Dispose(); }
        }
    }

    [HostTest("linux.signal-interrupt untimed futex consumes a genuine wait and constructs EINTR or SA_RESTART context")]
    private static void FutexHandlers()
    {
        foreach (bool restart in new[] { false,true })
        {
            using Owner o = new(restart); o.Futex(); o.Sender(); o.Send();
            HostCheck.Equal(KernelThreadTable.Completion.Interrupted,o.M.Kernel.Inspect(o.Parent).Completion);
            HostCheck.Equal(0L,o.Value(202,Word,129,1)); o.ParentAgain();
            var r = o.Complete(); HostCheck.True(r.Action == LinuxSyscallAction.Return && r.Value == -4);
            HostCheck.True(o.M.Service->MayChangeUserRange(Key,2,Word,4) && !o.M.Service->CurrentImage(Key,2,out _));
            ulong frame = o.Caught();
            HostCheck.Equal(restart ? 202UL : unchecked((ulong)-4),o.M.Read64(frame + 152));
            HostCheck.Equal(restart ? After - 2 : After,o.M.Read64(frame + 176));
            HostCheck.Equal(Word,o.M.Read64(frame + 112));
            HostCheck.Equal(LinuxSyscallAction.RestoreUser,o.Return(frame).Action);
            HostCheck.Equal(restart ? 202L : -4L,o.M.CurrentImage()->Value);
            HostCheck.Equal(-4L,o.Value(219)); // rt_sigreturn invalidates any restart callback.
        }
    }

    [HostTest("linux.signal-interrupt timed WAIT and BITSET return EINTR to handlers regardless of SA_RESTART")]
    private static void TimedHandlers()
    {
        foreach (bool bitset in new[] { false,true })
        {
            using Owner o = new(true); o.Futex(true,bitset,bitset ? 1100UL : 100UL); o.Sender(); o.M.Now = 1050; o.Send();
            o.ParentAgain(); HostCheck.Equal(-4L,o.Complete().Value); HostCheck.Equal(0U,o.M.Kernel.Table->DeadlineCount);
            ulong frame = o.Caught(); HostCheck.Equal(unchecked((ulong)-4),o.M.Read64(frame + 152)); HostCheck.Equal(After,o.M.Read64(frame + 176));
            HostCheck.Equal(LinuxSyscallAction.RestoreUser,o.Return(frame).Action);
            HostCheck.Equal(-4L,o.Value(219));
        }
    }

    [HostTest("linux.signal-interrupt timed restart callback survives handler I/O until sigreturn")]
    private static void HandlerRestartCapability()
    {
        foreach (bool withIo in new[] { false,true })
        {
            using Owner o = new(); o.Futex(true); o.Sender(); o.M.Now = 1050; o.Send(); o.ParentAgain();
            HostCheck.Equal(-4L,o.Complete().Value); ulong frame = o.Caught();
            if (withIo)
            {
                var pipe = o.Core.Pipe(); HostCheck.Equal(LinuxSyscallAction.Blocked,o.Call(0,pipe.Read,Output,4).Action);
                o.Sender(); o.M.Put32(Data,0x789abcde); HostCheck.Equal(4L,o.Value(1,pipe.Write,Data,4)); o.ParentAgain();
                HostCheck.Equal(4L,o.Complete().Value); HostCheck.Equal(0x789abcdeU,o.M.Read32(Output));
            }
            o.M.Now = 1100; o.M.Put64(Timeout + 8,9999999); o.M.FailRead(Timeout);
            HostCheck.Equal(-110L,o.Value(219)); // Still uses the original callback/deadline inside the handler.
            o.M.FailRead(ulong.MaxValue); HostCheck.Equal(LinuxSyscallAction.RestoreUser,o.Return(frame).Action);
            HostCheck.Equal(-4L,o.Value(219));
        }
    }

    [HostTest("linux.signal-interrupt no-handler timed restarts preserve original absolute deadline and never reread the timeout pointer")]
    private static void TimedNoHandler()
    {
        foreach (bool bitset in new[] { false,true })
        {
            using Owner o = new(); o.Futex(true,bitset,bitset ? 1100UL : 100UL); o.Sender();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                o.M.Now = attempt == 0 ? 1050UL : 1070UL; o.Send(); o.Action(false,1); o.ParentAgain();
                HostCheck.Equal(-4L,o.Complete().Value); HostCheck.Equal(Signals.Delivery.None,o.Deliver());
                HostCheck.True(o.M.CurrentImage()->Value == 219 && o.M.CurrentImage()->Frame.Rip == After - 2);
                o.M.CurrentImage()->Frame.Rip = After; o.M.Put64(Timeout + 8,99999999); o.M.FailRead(Timeout);
                if (attempt == 0)
                {
                    HostCheck.Equal(LinuxSyscallAction.Blocked,o.Call(219,ulong.MaxValue,ulong.MaxValue,ulong.MaxValue).Action);
                    o.Sender(); o.Action(false);
                }
            }
            o.M.Now = 1100; HostCheck.Equal(-110L,o.Value(219)); o.M.FailRead(ulong.MaxValue);
            HostCheck.Equal(-4L,o.Value(219)); HostCheck.Equal(0U,o.M.Kernel.Table->DeadlineCount);
            HostCheck.True(o.M.Service->MayChangeUserRange(Key,2,Word,4));
        }
    }

    [HostTest("linux.signal-interrupt no-handler retries re-admit changed futex words and mapping lifetimes")]
    private static void FreshFutexAdmission()
    {
        foreach (bool timed in new[] { false,true })
        {
            using Owner o = new(); o.Futex(timed); o.Sender(); o.M.Now = 1050; o.Send(); o.Action(false,1); o.ParentAgain();
            HostCheck.Equal(-4L,o.Complete().Value); HostCheck.True(o.M.Service->MayChangeUserRange(Key,2,Word,4));
            o.M.ChangeMappingLifetime(2); o.M.Put32(Word,8);
            HostCheck.Equal(Signals.Delivery.None,o.Deliver());
            HostCheck.Equal(timed ? 219L : 202L,o.M.CurrentImage()->Value); o.M.CurrentImage()->Frame.Rip = After;
            HostCheck.Equal(-11L,timed ? o.Value(219) : o.Value(202,Word,128,7));
            HostCheck.True(o.M.Service->MayChangeUserRange(Key,2,Word,4));
        }
    }

    [HostTest("linux.signal-interrupt real wake or elapsed timeout wins without losing the pending handler")]
    private static void Winners()
    {
        foreach (bool timeout in new[] { false,true })
        {
            using Owner o = new(true); o.Futex(timeout); o.Sender();
            if (timeout) o.M.Now = 1100; else HostCheck.Equal(1L,o.Value(202,Word,129,1));
            o.Send(); HostCheck.Equal(timeout ? KernelThreadTable.Completion.Deadline : KernelThreadTable.Completion.Woken,o.M.Kernel.Inspect(o.Parent).Completion);
            o.ParentAgain(); HostCheck.Equal(timeout ? -110L : 0L,o.Complete().Value);
            ulong frame = o.Caught(); HostCheck.Equal(timeout ? unchecked((ulong)-110) : 0UL,o.M.Read64(frame + 152));
            HostCheck.Equal(After,o.M.Read64(frame + 176)); HostCheck.Equal(LinuxSyscallAction.RestoreUser,o.Return(frame).Action);
        }
    }

    [HostTest("linux.signal-interrupt zero-progress pipe reads release original descriptions and honor actual handler restart flags")]
    private static void PipeReads()
    {
        foreach (bool restart in new[] { false,true })
        {
            using Owner o = new(restart); var pipe = o.Core.Pipe();
            HostCheck.Equal(LinuxSyscallAction.Blocked,o.Call(0,pipe.Read,Output,4).Action); o.Sender(); o.Send(); o.ParentAgain();
            HostCheck.Equal(-4L,o.Complete().Value); HostCheck.Equal(0U,o.Core.Files.State->RetainedPipeOperations);
            ulong frame = o.Caught(); HostCheck.Equal(restart ? 0UL : unchecked((ulong)-4),o.M.Read64(frame + 152));
            HostCheck.Equal(restart ? After - 2 : After,o.M.Read64(frame + 176)); HostCheck.Equal(pipe.Read,o.M.Read32(frame + 112));
            HostCheck.Equal(LinuxSyscallAction.RestoreUser,o.Return(frame).Action);
        }
    }

    [HostTest("linux.signal-interrupt partial pipe writes keep their transferred prefix and never rewind it")]
    private static void PartialWrite()
    {
        using Owner o = new(true); var pipe = o.Core.Pipe();
        new Span<byte>(o.M.At(Data),4100).Fill(0x73);
        HostCheck.Equal(LinuxSyscallAction.Blocked,o.Call(1,pipe.Write,Data,4100).Action); o.Sender(); o.Send(); o.ParentAgain();
        HostCheck.Equal(4096L,o.Complete().Value); HostCheck.Equal(0U,o.Core.Files.State->RetainedPipeOperations);
        ulong frame = o.Caught(); HostCheck.Equal(4096UL,o.M.Read64(frame + 152)); HostCheck.Equal(After,o.M.Read64(frame + 176));
        HostCheck.Equal(LinuxSyscallAction.RestoreUser,o.Return(frame).Action);
        HostCheck.Equal(4096L,o.Value(0,pipe.Read,Output,4096)); HostCheck.Equal(0U,o.Core.Files.Admission.Pipes[0].BufferedBytes);
        HostCheck.True(new ReadOnlySpan<byte>(o.M.At(Output),4096).IndexOfAnyExcept((byte)0x73) < 0);
    }

    [HostTest("linux.signal-interrupt ignored handler restarts pipe through a reused fd instead of its released description")]
    private static void ReusedPipe()
    {
        using Owner o = new(); var old = o.Core.Pipe();
        HostCheck.Equal(LinuxSyscallAction.Blocked,o.Call(0,old.Read,Output,4).Action); o.Sender(); o.Send();
        o.Action(false,1); HostCheck.Equal(0L,o.Value(3,old.Read)); var replacement = o.Core.Pipe(); HostCheck.Equal(old.Read,replacement.Read);
        o.M.Put32(Data,0x87654321); HostCheck.Equal(4L,o.Value(1,replacement.Write,Data,4));
        o.ParentAgain(); HostCheck.Equal(-4L,o.Complete().Value); HostCheck.Equal(0U,o.Core.Files.State->RetainedPipeOperations);
        HostCheck.Equal(Signals.Delivery.None,o.Deliver()); HostCheck.True(o.M.CurrentImage()->Frame.Rip == After - 2 && o.M.CurrentImage()->Value == 0);
        o.M.CurrentImage()->Frame.Rip = After; HostCheck.Equal(4L,o.Value(0,replacement.Read,Output,4)); HostCheck.Equal(0x87654321U,o.M.Read32(Output));
    }

    [HostTest("linux.signal-interrupt restart validation rejects changed opcode or failed user code copy before exposing an image")]
    private static void BadRewind()
    {
        foreach (bool copyFault in new[] { false,true })
        {
            using Owner o = new(true); o.Futex(); o.Sender(); o.Send(); o.ParentAgain(); HostCheck.Equal(-4L,o.Complete().Value);
            if (copyFault) o.M.FailRead(After - 2); else *o.M.At(After - 2) = 0x90;
            HostCheck.Equal(Signals.Delivery.BadFrame,o.Deliver()); HostCheck.True(o.M.View(o.Parent).Exiting && o.M.View(o.Parent).ExitCode == 139);
            HostCheck.True(o.M.Service->MayChangeUserRange(Key,2,Word,4)); o.M.FailRead(ulong.MaxValue);
        }
    }
}
