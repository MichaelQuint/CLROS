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
    }
    private static LinuxSyscallFrame Frame() => new()
    { Number = 1, Argument0 = 1, Argument1 = 0x402000, Argument2 = 16, Rip = 0x400100, Rsp = 0x70001000, Rflags = 2 };
    private static LinuxSyscallAdmission Admission(byte* scratch) => new()
    { UserRoot = 0x200000, MaximumCalls = 8, MaximumWrite = 64, Scratch = scratch, ScratchBytes = 64, FirmwareExited = true, OwnedBoundaryValidated = true };
    private static LinuxSyscallAccess Access(Model* model) => new()
    { Context = model, PageAccess = &Permissions, CopyFromUser = &Copy, Write = &Write };
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
