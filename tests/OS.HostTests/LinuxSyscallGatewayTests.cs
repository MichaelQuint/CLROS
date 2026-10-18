using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;

internal static unsafe class LinuxSyscallGatewayTests
{
    private struct Model
    {
        public byte* User;
        public byte* Output;
        public uint Copies, Writes, Bytes, Descriptor;
        public bool MissingSecond, CopyFails, SinkFails, High, RemovedCode;
        public uint ThreadCalls, ThreadMode;
        public long ThreadValue;
    }
    private static LinuxSyscallFrame Frame() => new()
    { Number = 1, Argument0 = 1, Argument1 = 0x402000, Argument2 = 16, Rip = 0x400100, Rsp = 0x70001000, Rflags = 2 };
    private static LinuxSyscallAdmission Admission(byte* scratch) => new()
    { UserRoot = 0x200000, MaximumCalls = 8, MaximumWrite = 64, Scratch = scratch, ScratchBytes = 64, FirmwareExited = true, OwnedBoundaryValidated = true };
    private static LinuxSyscallAccess Access(Model* model) => new()
    { Context = model, PageAccess = &Permissions, CopyFromUser = &Copy, Write = &Write };
    [UnmanagedCallersOnly]
    private static long NoopService(void* context, LinuxSyscallFrame* frame) => -38;
    [UnmanagedCallersOnly]
    private static LinuxSyscallResult ThreadService(void* context, LinuxSyscallFrame* frame)
    {
        Model* m = (Model*)context; m->ThreadCalls++;
        switch (m->ThreadMode)
        {
            case 1: return new(LinuxSyscallAction.Return);
            case 2: frame->Rip = 0x600100; return new(LinuxSyscallAction.Return);
            case 3: frame->Argument3++; return new(LinuxSyscallAction.Return);
            case 4: return new(LinuxSyscallAction.Blocked);
            case 5: return new(LinuxSyscallAction.ThreadExited, exitCode: 17);
            case 6: return new(LinuxSyscallAction.Terminate, exitCode: 42);
            case 7: return new((LinuxSyscallAction)255);
            case 8: return new(LinuxSyscallAction.Return, exitCode: 1);
            case 9: return new(LinuxSyscallAction.Blocked, 1);
            case 10: frame->FsBase = 0x403008; return new(LinuxSyscallAction.Return);
            case 11: frame->GsBase = 0x403000; return new(LinuxSyscallAction.Return);
            case 12: return new(LinuxSyscallAction.Return, m->ThreadValue);
            case 13: frame->Rip = 0x400180; frame->Argument0 = 0x1234; return new(LinuxSyscallAction.RestoreUser, 77);
            case 14: frame->FsBase = 0x403008; return new(LinuxSyscallAction.RestoreUser, 77);
            case 15: return new(LinuxSyscallAction.RestoreUser, 77, exitCode: 1);
            case 16: frame->Number = 39; return new(LinuxSyscallAction.RestoreUser, 77);
            case 17: frame->Rip = 0xffff800000001000; return new(LinuxSyscallAction.RestoreUser, 77);
            case 18: frame->GsBase = frame->Argument1; return new(LinuxSyscallAction.Return, m->ThreadValue);
            case 19: frame->GsBase = frame->Argument1; frame->FsBase = frame->Argument1; return new(LinuxSyscallAction.Return);
            case 20: frame->GsBase = 0x403008; return new(LinuxSyscallAction.Return);
            case 21: frame->GsBase = 0x403008; return new(LinuxSyscallAction.RestoreUser, 77);
            case 30: return new(LinuxSyscallAction.Yielded, m->ThreadValue);
            case 31: return new(LinuxSyscallAction.Yielded, exitCode: 1);
            case 32: frame->Argument3++; return new(LinuxSyscallAction.Yielded);
        }
        if (frame->Number == 158) { frame->FsBase = frame->Argument1; return new(LinuxSyscallAction.Return); }
        return new(LinuxSyscallAction.Return, 23);
    }
    private static LinuxSyscallGateState ThreadGate(Model* model, byte* scratch)
    {
        var access = Access(model); access.Startup = access.Descriptors = &NoopService; access.Threads = &ThreadService;
        var admission = Admission(scratch); admission.EnableStartup = admission.EnableDescriptors = admission.EnableThreads = true;
        admission.MaximumCalls = LinuxSyscallGateway.MaximumThreadCalls;
        LinuxSyscallGateState state = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref state, admission, access)); return state;
    }

    [HostTest("linux.gateway sched_yield requires exact staged handoff and rejects successful return without transfer")]
    private static void YieldAdmission()
    {
        byte* scratch=stackalloc byte[64];Model model=new(){ThreadMode=30};var gate=ThreadGate(&model,scratch);
        var frame=Frame();frame.Number=24;frame.Argument0=frame.Argument1=ulong.MaxValue;
        var result=LinuxSyscallGateway.Dispatch(ref gate,ref frame);
        HostCheck.True(result.Action==LinuxSyscallAction.Yielded && result.Value==0 && gate.Status==LinuxSyscallGateStatus.Active && gate.Calls==1);
        foreach(var invalid in new[]{(30U,24UL,1L),(30U,39UL,0L),(31U,24UL,0L),(1U,24UL,0L)})
        {
            model.ThreadMode=invalid.Item1;model.ThreadValue=invalid.Item3;gate=ThreadGate(&model,scratch);frame.Number=invalid.Item2;
            HostCheck.Equal(LinuxSyscallAction.Terminate,LinuxSyscallGateway.Dispatch(ref gate,ref frame).Action);
            HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission,gate.Status);
        }
        model.ThreadMode=32;model.ThreadValue=0;gate=ThreadGate(&model,scratch);frame=Frame();frame.Number=24;
        HostCheck.Equal(LinuxSyscallAction.Terminate,LinuxSyscallGateway.Dispatch(ref gate,ref frame).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidReturn,gate.Status);HostCheck.Equal(0UL,frame.Argument3);
        model.ThreadMode=30;model.ThreadCalls=0;gate=default;frame=Frame();frame.Number=24;
        HostCheck.Equal(LinuxSyscallGateStatus.Active,LinuxSyscallGateway.Begin(ref gate,Admission(scratch),Access(&model)));
        HostCheck.Equal(-38L,LinuxSyscallGateway.Dispatch(ref gate,ref frame).Value);HostCheck.Equal(0U,model.ThreadCalls);
    }

    [HostTest("linux.signal tkill gateway requires explicit thread admission and rejects callback frame mutation")]
    private static void TkillAdmission()
    {
        byte* scratch=stackalloc byte[64];Model model=new(){ThreadMode=1};var state=ThreadGate(&model,scratch);
        var frame=Frame();frame.Number=200;frame.Argument0=101;frame.Argument1=35;
        HostCheck.Equal(0L,LinuxSyscallGateway.Dispatch(ref state,ref frame).Value);HostCheck.Equal(1U,model.ThreadCalls);
        model.ThreadMode=3;state=ThreadGate(&model,scratch);var original=frame;
        HostCheck.Equal(LinuxSyscallAction.Terminate,LinuxSyscallGateway.Dispatch(ref state,ref frame).Action);
        HostCheck.True(state.Status==LinuxSyscallGateStatus.InvalidReturn&&frame.Argument3==original.Argument3);
        model.ThreadMode=1;model.ThreadCalls=0;state=default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active,LinuxSyscallGateway.Begin(ref state,Admission(scratch),Access(&model)));
        HostCheck.Equal(-38L,LinuxSyscallGateway.Dispatch(ref state,ref frame).Value);HostCheck.Equal(0U,model.ThreadCalls);
    }

    [HostTest("linux.signal gateway restores only syscall15 with immutable thread bases valid user targets and zero exit code")]
    private static void SignalReturnControls()
    {
        byte* scratch = stackalloc byte[64];
        for (int variant = 0; variant < 7; variant++)
        {
            Model model = new() { ThreadMode = variant < 2 ? 13U : variant == 6 ? 21U : (uint)variant + 12 }; var state = ThreadGate(&model,scratch);
            var frame = Frame(); frame.Number = variant == 1 ? 39UL : 15UL;
            var result = LinuxSyscallGateway.Dispatch(ref state,ref frame);
            if (variant == 0)
                HostCheck.True(result.Action == LinuxSyscallAction.RestoreUser && result.Value == 77 && frame.Rip == 0x400180 &&
                    frame.Argument0 == 0x1234 && state.Status == LinuxSyscallGateStatus.Active && state.BytesWritten == 0);
            else HostCheck.True(result.Action == LinuxSyscallAction.Terminate && state.Status is LinuxSyscallGateStatus.InvalidReturn or LinuxSyscallGateStatus.InvalidAdmission);
        }
    }

    [HostTest("linux.signal gateway admits blocked restart_syscall once without descriptor accounting or callback replay")]
    private static void RestartSyscall()
    {
        byte* scratch = stackalloc byte[64]; Model model = new() { ThreadMode = 4 }; var state = ThreadGate(&model,scratch);
        var frame = Frame(); frame.Number = 219;
        HostCheck.Equal(LinuxSyscallAction.Blocked,LinuxSyscallGateway.Dispatch(ref state,ref frame).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.Blocked,state.Status); HostCheck.Equal(1U,model.ThreadCalls);
        HostCheck.Equal(-4L,LinuxSyscallGateway.CompleteBlocked(ref state,new(LinuxSyscallAction.Return,-4)).Value);
        HostCheck.True(state.Status == LinuxSyscallGateStatus.Active && state.Calls == 1 && state.BytesWritten == 0 && model.ThreadCalls == 1);
        frame.Number = 39;
        HostCheck.Equal(LinuxSyscallAction.Terminate,LinuxSyscallGateway.Dispatch(ref state,ref frame).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission,state.Status);
    }

    [HostTest("m16-008 gateway admits explicit thread callback only with complete prerequisites and bounded budget")]
    private static void ThreadAdmission()
    {
        byte* scratch = stackalloc byte[64]; Model model = default; var access = Access(&model);
        access.Startup = access.Descriptors = &NoopService; access.Threads = &ThreadService;
        var admission = Admission(scratch); admission.EnableStartup = admission.EnableDescriptors = true;
        LinuxSyscallGateState state = default;
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission, LinuxSyscallGateway.Begin(ref state, admission, access));
        admission.EnableThreads = true; admission.EnableDescriptors = false;
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission, LinuxSyscallGateway.Begin(ref state, admission, access));
        admission.EnableDescriptors = true; admission.MaximumCalls = LinuxSyscallGateway.MaximumThreadCalls + 1;
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission, LinuxSyscallGateway.Begin(ref state, admission, access));
        admission.MaximumCalls--; HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref state, admission, access));
        HostCheck.Equal(0U, model.ThreadCalls);
    }

    [HostTest("m16-008 gateway publishes only the requested mapped FS base through its ref frame")]
    private static void ThreadFs()
    {
        byte* scratch = stackalloc byte[64]; Model model = default; var state = ThreadGate(&model, scratch);
        var frame = Frame(); frame.Number = 158; frame.Argument0 = 0x1002; frame.Argument1 = 0x403000;
        var result = LinuxSyscallGateway.Dispatch(ref state, ref frame);
        HostCheck.True(result.Action == LinuxSyscallAction.Return && result.Value == 0 && frame.FsBase == 0x403000 && model.ThreadCalls == 1);
        frame.Argument0 = 0xffffffff00001002; frame.Argument1 = 0x403008;
        HostCheck.Equal(LinuxSyscallAction.Return, LinuxSyscallGateway.Dispatch(ref state, ref frame).Action);
        HostCheck.Equal(0x403008UL, frame.FsBase); HostCheck.Equal(2U, model.ThreadCalls);
        state = ThreadGate(&model, scratch); frame.FsBase = 0;
        HostCheck.Equal(LinuxSyscallAction.Terminate, LinuxSyscallGateway.Dispatch(ref state, frame).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission, state.Status); HostCheck.Equal(2U, model.ThreadCalls);
        foreach (uint mode in new uint[] { 1, 2, 3, 10, 11 })
        {
            model.ThreadMode = mode; state = ThreadGate(&model, scratch); frame = Frame(); frame.Number = 158;
            frame.Argument0 = 0x1002; frame.Argument1 = 0x403000;
            HostCheck.Equal(LinuxSyscallAction.Terminate, LinuxSyscallGateway.Dispatch(ref state, ref frame).Action);
            HostCheck.Equal(LinuxSyscallGateStatus.InvalidReturn, state.Status); HostCheck.Equal(0UL, frame.FsBase);
            HostCheck.Equal(0x400100UL, frame.Rip); HostCheck.Equal(0UL, frame.Argument3); HostCheck.Equal(0UL, frame.GsBase);
        }
        model.ThreadMode = 0; state = ThreadGate(&model, scratch); frame.Argument1 = 0x500000; // RWX is never admissible TLS.
        HostCheck.Equal(LinuxSyscallAction.Terminate, LinuxSyscallGateway.Dispatch(ref state, ref frame).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidReturn, state.Status);
    }

    [HostTest("m16-009 gateway permits only successful requested mapped GS updates and retains the distinct FS and sigreturn envelope")]
    private static void ThreadGs()
    {
        byte* scratch = stackalloc byte[64]; Model model = new() { ThreadMode = 18 };
        foreach (ulong option in new ulong[] {0x1001,0xffffffff00001001})
        {
            var state = ThreadGate(&model,scratch); var frame = Frame(); frame.Number=158;
            frame.Argument0=option; frame.Argument1=0x403000; frame.FsBase=0x403008;
            var result=LinuxSyscallGateway.Dispatch(ref state,ref frame);
            HostCheck.True(result.Action==LinuxSyscallAction.Return && result.Value==0 && frame.GsBase==0x403000 && frame.FsBase==0x403008);
            frame.Argument1=0; HostCheck.Equal(LinuxSyscallAction.Return,LinuxSyscallGateway.Dispatch(ref state,ref frame).Action);
            HostCheck.Equal(0UL,frame.GsBase); HostCheck.Equal(0x403008UL,frame.FsBase);
        }
        for (int variant=0;variant<8;variant++)
        {
            model.ThreadMode=18; model.ThreadValue=0; var state=ThreadGate(&model,scratch);
            var frame=Frame(); frame.Number=158; frame.Argument0=0x1001; frame.Argument1=0x403000; frame.FsBase=0x403010;
            switch (variant)
            {
                case 0: frame.Number=39; break;
                case 1: frame.Argument0=0x1002; break;
                case 2: frame.Argument0=0x1004; break;
                case 3: model.ThreadValue=-14; break;
                case 4: model.ThreadValue=1; break;
                case 5: model.ThreadMode=19; break;
                case 6: model.ThreadMode=20; break;
                case 7: frame.Argument1=0x500000; break;
            }
            HostCheck.Equal(LinuxSyscallAction.Terminate,LinuxSyscallGateway.Dispatch(ref state,ref frame).Action);
            HostCheck.Equal(LinuxSyscallGateStatus.InvalidReturn,state.Status);
            HostCheck.True(frame.GsBase==0 && frame.FsBase==0x403010);
        }
    }

    [HostTest("m16-008 gateway distinguishes staged block thread exit and process exit without scheduling claims")]
    private static void ThreadActions()
    {
        byte* scratch = stackalloc byte[64]; Model model = new() { ThreadMode = 4 }; var state = ThreadGate(&model, scratch);
        var frame = Frame(); frame.Number = 202;
        HostCheck.Equal(LinuxSyscallAction.Blocked, LinuxSyscallGateway.Dispatch(ref state, ref frame).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.Blocked, state.Status);
        HostCheck.Equal(LinuxSyscallAction.Rejected, LinuxSyscallGateway.Dispatch(ref state, ref frame).Action);
        HostCheck.Equal(1U, model.ThreadCalls);
        HostCheck.Equal(-110L, LinuxSyscallGateway.CompleteBlocked(ref state, new(LinuxSyscallAction.Return, -110)).Value);
        HostCheck.Equal(LinuxSyscallGateStatus.Active, state.Status);
        model.ThreadMode = 5; frame.Number = 60; var exited = LinuxSyscallGateway.Dispatch(ref state, ref frame);
        HostCheck.True(exited.Action == LinuxSyscallAction.ThreadExited && exited.ExitCode == 17 && state.Status == LinuxSyscallGateStatus.Exited);
        HostCheck.Equal(2U, state.Calls);
        state = ThreadGate(&model, scratch);
        model.ThreadMode = 6; frame.Number = 231; exited = LinuxSyscallGateway.Dispatch(ref state, ref frame);
        HostCheck.True(exited.Action == LinuxSyscallAction.Terminate && exited.ExitCode == 42 && state.Status == LinuxSyscallGateStatus.Exited);
        HostCheck.Equal(1U, state.Calls);
        foreach (uint mode in new uint[] { 7, 8, 9 })
        {
            model.ThreadMode = mode; state = ThreadGate(&model, scratch); frame.Number = 202;
            new Span<byte>(scratch, 64).Fill(0x5a);
            HostCheck.Equal(LinuxSyscallAction.Terminate, LinuxSyscallGateway.Dispatch(ref state, ref frame).Action);
            HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission, state.Status);
            HostCheck.True(new ReadOnlySpan<byte>(scratch, 64).IndexOfAnyExcept((byte)0) == -1);
        }
    }

    [HostTest("m16-008 gateway routes all threaded fd mutations and counts blocked writes once after completion")]
    private static void ThreadDescriptors()
    {
        byte* scratch = stackalloc byte[64]; Model model = new() { ThreadMode = 12, ThreadValue = -9 }; var state = ThreadGate(&model, scratch);
        var frame = Frame();
        ulong[] calls = [0, 1, 2, 3, 5, 8, 16, 17, 20, 22, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 54, 55, 72, 257, 288, 293];
        foreach (ulong number in calls)
        { frame.Number = number; HostCheck.Equal(-9L, LinuxSyscallGateway.Dispatch(ref state, ref frame).Value); }
        HostCheck.Equal((uint)calls.Length, model.ThreadCalls); // The legacy fd callback would return ENOSYS.
        frame = Frame(); model.ThreadMode = 4;
        HostCheck.Equal(LinuxSyscallAction.Blocked, LinuxSyscallGateway.Dispatch(ref state, ref frame).Action);
        HostCheck.Equal(0U, state.BytesWritten);
        HostCheck.Equal(LinuxSyscallAction.Blocked, LinuxSyscallGateway.CompleteBlocked(ref state, new(LinuxSyscallAction.Blocked)).Action);
        HostCheck.Equal(0U, state.BytesWritten);
        HostCheck.Equal(16L, LinuxSyscallGateway.CompleteBlocked(ref state, new(LinuxSyscallAction.Return, 16)).Value);
        HostCheck.Equal(16U, state.BytesWritten); HostCheck.Equal((uint)calls.Length + 1, state.Calls);
        HostCheck.Equal(LinuxSyscallAction.Rejected, LinuxSyscallGateway.CompleteBlocked(ref state, new(LinuxSyscallAction.Return, 16)).Action);
        HostCheck.Equal(16U, state.BytesWritten);
        model.ThreadMode = 12; model.ThreadValue = 8;
        HostCheck.Equal(8L, LinuxSyscallGateway.Dispatch(ref state, ref frame).Value); HostCheck.Equal(24U, state.BytesWritten);
    }

    [HostTest("m16-008 gateway validates completion bounds return mappings and exit without replay or false byte accounting")]
    private static void CompletionFailures()
    {
        byte* scratch = stackalloc byte[64]; Model model = new() { ThreadMode = 4 }; var frame = Frame();
        foreach (int variant in new[] { 0, 1, 2, 3, 4 })
        {
            model.RemovedCode = false; var state = ThreadGate(&model, scratch);
            HostCheck.Equal(LinuxSyscallAction.Blocked, LinuxSyscallGateway.Dispatch(ref state, ref frame).Action);
            LinuxSyscallResult result;
            switch (variant)
            {
                case 0: result = new(LinuxSyscallAction.Return, 17); break;
                case 1: result = new(LinuxSyscallAction.Blocked, 1); break;
                case 2: result = new(LinuxSyscallAction.Return, 1, 1); break;
                case 3: model.RemovedCode = true; result = new(LinuxSyscallAction.Return, 16); break;
                default: model.RemovedCode = true; result = new(LinuxSyscallAction.ThreadExited, exitCode: 7); break;
            }
            var complete = LinuxSyscallGateway.CompleteBlocked(ref state, result);
            HostCheck.Equal(variant == 4 ? LinuxSyscallAction.ThreadExited : LinuxSyscallAction.Terminate, complete.Action);
            HostCheck.Equal(variant == 0 ? LinuxSyscallGateStatus.SinkFailed : variant == 3 ? LinuxSyscallGateStatus.InvalidReturn :
                variant == 4 ? LinuxSyscallGateStatus.Exited : LinuxSyscallGateStatus.InvalidAdmission, state.Status);
            HostCheck.Equal(0U, state.BytesWritten); HostCheck.Equal(1U, state.Calls);
            HostCheck.True(new ReadOnlySpan<byte>(scratch, 64).IndexOfAnyExcept((byte)0) == -1);
        }
        model.RemovedCode = false; model.ThreadMode = 12; model.ThreadValue = 17; var immediate = ThreadGate(&model, scratch);
        HostCheck.Equal(LinuxSyscallAction.Terminate, LinuxSyscallGateway.Dispatch(ref immediate, ref frame).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.SinkFailed, immediate.Status); HostCheck.Equal(0U, immediate.BytesWritten);
        model.ThreadMode = 10; immediate = ThreadGate(&model, scratch);
        HostCheck.Equal(LinuxSyscallAction.Terminate, LinuxSyscallGateway.Dispatch(ref immediate, ref frame).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidReturn, immediate.Status); HostCheck.Equal(0UL, frame.FsBase);
    }
    [UnmanagedCallersOnly]
    private static byte Permissions(void* context, ulong page)
    {
        Model* model = (Model*)context;
        if (model->High && page >= 0x100000000 && page <= 0x170000000) page -= 0x100000000;
        return page switch { 0x400000 => model->RemovedCode ? (byte)0 : (byte)5, 0x402000 => 1, 0x403000 => model->MissingSecond ? (byte)0 : (byte)3,
            0x70000000 => 3, 0x500000 => 7, 0x600000 => 5, _ => 0 };
    }
    [UnmanagedCallersOnly]
    private static byte Copy(void* context, ulong source, byte* destination, uint bytes)
    {
        Model* model = (Model*)context; model->Copies++;
        if (model->CopyFails) { destination[0] = 0xdd; return 0; }
        ulong start = model->High ? 0x100402000UL : 0x402000UL;
        if (source < start || source + bytes > start + 8192) return 0;
        for (uint i = 0; i < bytes; i++) destination[i] = model->User[source - start + i];
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write(void* context, uint descriptor, byte* source, uint bytes)
    {
        Model* model = (Model*)context; model->Writes++;
        if (model->SinkFails) return 0;
        model->Descriptor = descriptor; model->Bytes = bytes;
        for (uint i = 0; i < bytes; i++) model->Output[i] = source[i];
        return 1;
    }

    [UnmanagedCallersOnly]
    private static long Startup(void* context, LinuxSyscallFrame* frame)
    {
        ((Model*)context)->RemovedCode = true;
        frame->Rip = 0x600100; // A different valid RX page cannot replace the captured return being checked.
        return 0;
    }
    [HostTest("m16-006 explicit extension preserves diagnostic admission and rechecks original return after unmapping", HostLane.Fast, 5)]
    private static void Extension()
    {
        byte* scratch = stackalloc byte[64]; Model model = new() { High = true };
        var admission = Admission(scratch); var access = Access(&model); access.Startup = &Startup;
        LinuxSyscallGateState invalid = default;
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission, LinuxSyscallGateway.Begin(ref invalid, admission, access));
        admission.EnableStartup = true; LinuxSyscallGateState state = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref state, admission, access));
        var frame = Frame(); frame.Number = 11;
        HostCheck.Equal(LinuxSyscallAction.Terminate, LinuxSyscallGateway.Dispatch(ref state, frame).Action);
        HostCheck.True(state.Status == LinuxSyscallGateStatus.InvalidReturn && state.Calls == 1 &&
            new ReadOnlySpan<byte>(scratch, 64).IndexOfAnyExcept((byte)0) == -1);
    }

    [HostTest("m16-006 high syscall buffers returns TLS and physical-root rejection use distinct bounds", HostLane.Fast, 4)]
    private static void HighPointers()
    {
        byte* scratch = stackalloc byte[64]; byte* user = stackalloc byte[8192]; byte* output = stackalloc byte[64];
        for (uint i = 0; i < 8192; i++) user[i] = (byte)i;
        Model model = new() { User = user, Output = output, High = true }; LinuxSyscallGateState state = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref state, Admission(scratch), Access(&model)));
        var frame = Frame(); frame.Rip += 0x100000000; frame.Rsp += 0x100000000;
        frame.FsBase = frame.GsBase = 0x100403000; frame.Argument1 = 0x100402ff8;
        var result = LinuxSyscallGateway.Dispatch(ref state, frame);
        HostCheck.True(result.Action == LinuxSyscallAction.Return && result.Value == 16 && model.Copies == 1 && model.Writes == 1);
        HostCheck.True(new ReadOnlySpan<byte>(output, 16).SequenceEqual(new ReadOnlySpan<byte>(user + 4088, 16)));
        HostCheck.True(!LinuxSyscallGateway.ValidateRange(LinuxSyscallGateway.UserEnd - 8, 16, LinuxUserPageAccess.Read, Access(&model)));
        frame.Rip = 0xffff800000000000;
        HostCheck.True(!LinuxSyscallGateway.ValidateReturn(frame, Access(&model)));
        LinuxSyscallGateState invalid = default; var admission = Admission(scratch); admission.UserRoot = 0x100000000;
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission, LinuxSyscallGateway.Begin(ref invalid, admission, Access(&model)));
        // Existing supervisor callback rejects kernel, table and guard pages
        // even when the numerical address is inside the larger VA policy.
        foreach (ulong address in new ulong[] { 0x200000, 0x100200000, 0x100404000 })
            HostCheck.True(!LinuxSyscallGateway.ValidateRange(address, 1, LinuxUserPageAccess.Read, Access(&model)));
    }

    [HostTest("m16-005 checked cross-page write exit and syscall core allocate nothing", HostLane.Fast, 0)]
    private static void WriteExit()
    {
        byte* user = stackalloc byte[8192]; byte* output = stackalloc byte[64]; byte* scratch = stackalloc byte[64];
        for (uint i = 0; i < 8192; i++) user[i] = (byte)i;
        Model model = new() { User = user, Output = output }; LinuxSyscallGateState state = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref state, Admission(scratch), Access(&model)));
        var frame = Frame(); frame.Argument1 = 0x402ff8; frame.Argument2 = 24; frame.Argument0 = 0xffffffff00000002;
        frame.Rflags = 0x200cd7; // Includes user arithmetic, direction and ID flags.
        long before = GC.GetAllocatedBytesForCurrentThread();
        var result = LinuxSyscallGateway.Dispatch(ref state, frame);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(result.Action == LinuxSyscallAction.Return && result.Value == 24 && allocated == 0);
        HostCheck.True(model.Copies == 1 && model.Writes == 1 && model.Descriptor == 2 && state.BytesWritten == 24);
        HostCheck.True(new ReadOnlySpan<byte>(output, 24).SequenceEqual(new ReadOnlySpan<byte>(user + 4088, 24)));
        HostCheck.True(new ReadOnlySpan<byte>(scratch, 64).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.True(frame.Rflags == 0x200cd7 && frame.Rip == 0x400100 && frame.Rsp == 0x70001000);
        frame.Argument0 = 0xffffffff00000001;
        HostCheck.Equal(24L, LinuxSyscallGateway.Dispatch(ref state, frame).Value);
        HostCheck.Equal(1U, model.Descriptor);
        frame.Number = 231; frame.Argument0 = 0xffffffff0000002a;
        result = LinuxSyscallGateway.Dispatch(ref state, frame);
        HostCheck.True(result.Action == LinuxSyscallAction.Terminate && result.ExitCode == 42 && state.Status == LinuxSyscallGateStatus.Exited);
        HostCheck.Equal(LinuxSyscallAction.Rejected, LinuxSyscallGateway.Dispatch(ref state, Frame()).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.AlreadyConsumed, LinuxSyscallGateway.Begin(ref state, Admission(scratch), Access(&model)));
    }

    [HostTest("m16-005 unsupported descriptors buffers and counts fail without copy or sink effects", HostLane.Fast, 1)]
    private static void Errors()
    {
        byte* user = stackalloc byte[8192]; byte* output = stackalloc byte[64]; byte* scratch = stackalloc byte[64];
        for (int variant = 0; variant < 7; variant++)
        {
            Model model = new() { User = user, Output = output, MissingSecond = variant == 4 };
            LinuxSyscallGateState state = default;
            HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref state, Admission(scratch), Access(&model)));
            var frame = Frame(); long expected;
            if (variant == 0) { frame.Number = ulong.MaxValue; expected = -38; }
            else if (variant == 1) { frame.Argument0 = 0; expected = -9; }
            else if (variant == 2) { frame.Argument2 = 65; expected = -22; }
            else if (variant == 3) { frame.Argument1 = ulong.MaxValue; expected = -14; }
            else if (variant == 4) { frame.Argument1 = 0x402fff; frame.Argument2 = 2; expected = -14; }
            else if (variant == 5) { frame.Argument1 = 0x80000000; expected = -14; }
            else { frame.Argument1 = ulong.MaxValue; frame.Argument2 = 0; expected = 0; }
            var result = LinuxSyscallGateway.Dispatch(ref state, frame);
            HostCheck.True(result.Action == LinuxSyscallAction.Return && result.Value == expected && state.Status == LinuxSyscallGateStatus.Active);
            HostCheck.True(model.Copies == 0 && model.Writes == 0 && state.BytesWritten == 0);
        }
    }

    [HostTest("m16-005 bad return flags addresses FS GS and exhaustion terminate before unsafe transfer", HostLane.Fast, 2)]
    private static void BadReturns()
    {
        byte* scratch = stackalloc byte[64]; Model model = default;
        for (int variant = 0; variant < 9; variant++)
        {
            LinuxSyscallGateState state = default;
            HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref state, Admission(scratch), Access(&model)));
            var frame = Frame();
            switch (variant)
            {
                case 0: frame.Rflags = 0; break;
                case 1: frame.Rflags |= 0x3000; break;
                case 2: frame.Rflags |= 0x200; break; // IRQ-disabled admitted profile.
                case 3: frame.Rflags |= 0x40000; break;
                case 4: frame.Rip = 0x80000000; break;
                case 5: frame.Rip = 0x500000; break; // Hostile W+X page.
                case 6: frame.Rsp = 0xffff800000001000; break;
                case 7: frame.FsBase = 0x80000000; break;
                default: frame.GsBase = ulong.MaxValue; break;
            }
            HostCheck.Equal(LinuxSyscallAction.Terminate, LinuxSyscallGateway.Dispatch(ref state, frame).Action);
            HostCheck.Equal(LinuxSyscallGateStatus.InvalidReturn, state.Status);
            HostCheck.True(model.Copies == 0 && model.Writes == 0);
        }
        LinuxSyscallGateState bounded = default; var admission = Admission(scratch); admission.MaximumCalls = 2;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref bounded, admission, Access(&model)));
        var unknown = Frame(); unknown.Number = 999;
        HostCheck.Equal(-38L, LinuxSyscallGateway.Dispatch(ref bounded, unknown).Value);
        HostCheck.Equal(-38L, LinuxSyscallGateway.Dispatch(ref bounded, unknown).Value);
        HostCheck.Equal(LinuxSyscallAction.Terminate, LinuxSyscallGateway.Dispatch(ref bounded, unknown).Action);
        HostCheck.Equal(LinuxSyscallGateStatus.CallLimit, bounded.Status);
        HostCheck.Equal(2U, bounded.Calls);
    }

    [HostTest("m16-005 admission aliases and failed copy or sink cannot report success", HostLane.Fast, 3)]
    private static void AdmissionAndFailures()
    {
        byte* scratch = stackalloc byte[64]; byte* user = stackalloc byte[8192]; byte* output = stackalloc byte[64];
        Model model = new() { User = user, Output = output };
        for (int variant = 0; variant < 8; variant++)
        {
            LinuxSyscallGateState state = default; var admission = Admission(scratch); var access = Access(&model);
            switch (variant)
            {
                case 0: admission.FirmwareExited = false; break;
                case 1: admission.OwnedBoundaryValidated = false; break;
                case 2: admission.UserRoot++; break;
                case 3: admission.MaximumCalls = 65; break;
                case 4: admission.MaximumWrite = 4097; break;
                case 5: admission.ScratchBytes = 63; break;
                case 6: access.CopyFromUser = null; break;
                default: admission.Scratch = (byte*)&state; break;
            }
            HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission, LinuxSyscallGateway.Begin(ref state, admission, access));
            HostCheck.Equal(LinuxSyscallGateStatus.Uninitialized, state.Status);
        }
        foreach (bool copyFailure in new[] { true, false })
        {
            LinuxSyscallGateState state = default; model.CopyFails = copyFailure; model.SinkFails = !copyFailure;
            HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref state, Admission(scratch), Access(&model)));
            HostCheck.Equal(LinuxSyscallAction.Terminate, LinuxSyscallGateway.Dispatch(ref state, Frame()).Action);
            HostCheck.Equal(copyFailure ? LinuxSyscallGateStatus.CopyFailed : LinuxSyscallGateStatus.SinkFailed, state.Status);
            HostCheck.True(new ReadOnlySpan<byte>(scratch, 64).IndexOfAnyExcept((byte)0) == -1);
        }
    }
}
