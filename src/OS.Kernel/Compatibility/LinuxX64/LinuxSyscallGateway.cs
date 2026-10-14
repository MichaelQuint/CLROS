namespace OS.Kernel.Compatibility.LinuxX64;

public enum LinuxSyscallGateStatus : byte
{ Uninitialized, Active, Exited, InvalidAdmission, AlreadyConsumed, InvalidReturn, CallLimit, CopyFailed, SinkFailed, Blocked }
public enum LinuxSyscallAction : byte { Rejected, Return, Terminate, Blocked, ThreadExited, RestoreUser }
public enum LinuxUserPageAccess : byte { None = 0, Read = 1, Write = 2, Execute = 4 }

/// <summary>Captured Linux x64 syscall values; RCX/R11 supply return RIP/flags.</summary>
public struct LinuxSyscallFrame
{
    public ulong Number, Argument0, Argument1, Argument2, Argument3, Argument4, Argument5;
    public ulong Rip, Rsp, Rflags, FsBase, GsBase;
}

public readonly struct LinuxSyscallResult
{
    public readonly LinuxSyscallAction Action;
    public readonly long Value;
    public readonly uint ExitCode;
    /// <summary>Result from a separately admitted service. Blocked/ThreadExited
    /// require native cleanup before the scheduler parks/reaps its kernel worker.
    /// RestoreUser is exclusively a validated syscall15 return; the native owner
    /// must install its complete saved GPR/FX image, not just Frame/Value.</summary>
    public LinuxSyscallResult(LinuxSyscallAction action, long value = 0, uint exitCode = 0)
    { Action = action; Value = value; ExitCode = exitCode; }
}

public unsafe struct LinuxSyscallAccess
{
    public void* Context;
    // Trusted owner policy, copied at admission. Only explicit threaded user
    // IRQ mode may retain IF=1; kernel callbacks themselves still require IF=0.
    public bool UserInterrupts;
    // Only actual present USER leaves may return access. Read is mandatory;
    // RW+X is forbidden. The owner binds lookup/copy to the active admitted root.
    public delegate* unmanaged<void*, ulong, byte> PageAccess;
    // After complete range preflight, copy exactly the requested user bytes.
    // The owner prevents mapping changes and proves scratch/frame nonaliasing.
    public delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyFromUser;
    public delegate* unmanaged<void*, uint, byte*, uint, byte> Write;
    // Explicit M16-006 extension only; executes on the separately owned kernel
    // root, publishes no transfer, and returns a Linux value/negative errno.
    public delegate* unmanaged<void*, LinuxSyscallFrame*, long> Startup;
    // Explicit M16-007 fd extension; owns its separate retained tables/scratch.
    public delegate* unmanaged<void*, LinuxSyscallFrame*, long> Descriptors;
    // Explicit M16-008 thread AND descriptor extension. It wraps every fd
    // mutation so pipe readiness cannot bypass the actual thread wait owner.
    // ARCH_SET_FS/GS and explicit checked syscall15 RestoreUser may change the
    // return frame; all other return/control fields remain immutable. A blocked
    // result is a staged scheduler wait, not a fabricated Linux return value.
    public delegate* unmanaged<void*, LinuxSyscallFrame*, LinuxSyscallResult> Threads;
}

public unsafe struct LinuxSyscallAdmission
{
    public ulong UserRoot;
    public uint MaximumCalls, MaximumWrite;
    public byte* Scratch;
    public uint ScratchBytes;
    public bool FirmwareExited, OwnedBoundaryValidated;
    public bool EnableStartup;
    public bool EnableDescriptors;
    public bool EnableThreads;
}

public unsafe struct LinuxSyscallGateState
{
    internal LinuxSyscallGateStatus StatusValue;
    internal LinuxSyscallAdmission Admission;
    internal LinuxSyscallAccess Access;
    internal uint CallsValue, BytesValue;
    internal LinuxSyscallFrame Pending;
    public readonly LinuxSyscallGateStatus Status => StatusValue;
    public readonly uint Calls => CallsValue;
    public readonly uint BytesWritten => BytesValue;
}

/// <summary>
/// One-use checked dispatcher for the declared low-64GiB, single-CPU
/// boundary. Admission declarations and callback simulations are host contracts,
/// not CPL3 evidence. The native owner proves selectors/MSRs, dedicated kernel
/// stack, FS/GS/FP preservation, current root and immutable user mappings. It
/// validates every return before IRETQ; termination never resumes user code.
/// Kernel scratch is cleared after every attempted copy/write. No allocations,
/// exceptions, firmware calls, OS threading or upstream app acceptance is added.
/// Thread mode requires one retained state per actual worker generation; never
/// copy/reuse it or dispatch another worker through a blocked state. The IRQ-off
/// owner binds that storage to the real scheduled identity, captures the user
/// image and supplies actual wait completion after native restoration. Separate
/// gates may share scratch only under that same serialization. Counters are per
/// gate and include completed successful write prefixes, never pending progress.
/// </summary>
public static unsafe class LinuxSyscallGateway
{
    public const ulong UserEnd = OS.Kernel.Memory.X64UserAddressSpace.UserEnd, UserRflagsMask = 0x200cd7;
    public const uint MaximumCalls = 64, MaximumWrite = 4096;
    public const uint MaximumThreadCalls = 4096;
    private const long BadDescriptor = -9, Fault = -14, Invalid = -22, Unsupported = -38;

    public static LinuxSyscallGateStatus Begin(ref LinuxSyscallGateState state,
        LinuxSyscallAdmission admission, LinuxSyscallAccess access)
    {
        if (state.StatusValue != LinuxSyscallGateStatus.Uninitialized) return LinuxSyscallGateStatus.AlreadyConsumed;
        if (state.CallsValue != 0 || state.BytesValue != 0 || !Empty(state.Pending) || !admission.FirmwareExited || !admission.OwnedBoundaryValidated ||
            admission.UserRoot < 4096 || admission.UserRoot >= OS.Kernel.Memory.X64UserAddressSpace.PhysicalEnd || (admission.UserRoot & 4095) != 0 ||
            admission.MaximumCalls == 0 || admission.MaximumCalls > (admission.EnableThreads ? MaximumThreadCalls : MaximumCalls) ||
            admission.MaximumWrite == 0 || admission.MaximumWrite > MaximumWrite ||
            admission.Scratch == null || admission.ScratchBytes < admission.MaximumWrite || admission.ScratchBytes > MaximumWrite ||
            (ulong)admission.Scratch > ~0UL - admission.ScratchBytes || access.Context == null ||
            access.PageAccess == null || access.CopyFromUser == null || access.Write == null)
            return LinuxSyscallGateStatus.InvalidAdmission;
        if (admission.EnableStartup != (access.Startup != null) ||
            admission.EnableDescriptors != (access.Descriptors != null) ||
            admission.EnableThreads != (access.Threads != null) ||
            admission.EnableDescriptors && !admission.EnableStartup || admission.EnableThreads && !admission.EnableDescriptors ||
            access.UserInterrupts && !admission.EnableThreads)
            return LinuxSyscallGateStatus.InvalidAdmission;
        fixed (LinuxSyscallGateState* original = &state)
            if ((ulong)admission.Scratch < (ulong)original + (uint)sizeof(LinuxSyscallGateState) &&
                (ulong)original < (ulong)admission.Scratch + admission.ScratchBytes)
                return LinuxSyscallGateStatus.InvalidAdmission;
        state.Admission = admission; state.Access = access;
        Clear(admission.Scratch, admission.ScratchBytes);
        state.StatusValue = LinuxSyscallGateStatus.Active;
        return state.StatusValue;
    }

    public static LinuxSyscallResult Dispatch(ref LinuxSyscallGateState state, LinuxSyscallFrame frame)
    {
        // A value-only caller cannot publish an accepted FS-base update.
        if (state.Admission.EnableThreads) return Stop(ref state, LinuxSyscallGateStatus.InvalidAdmission);
        return Dispatch(ref state, ref frame);
    }

    /// <summary>Thread-enabled native callers pass the actual captured frame by
    /// reference so a checked ARCH_SET_FS change reaches IRET. The old value
    /// overload retains its original behavior for diagnostics without threads.</summary>
    public static LinuxSyscallResult Dispatch(ref LinuxSyscallGateState state, ref LinuxSyscallFrame frame)
    {
        if (state.StatusValue != LinuxSyscallGateStatus.Active) return new(LinuxSyscallAction.Rejected);
        if (!ValidateReturn(frame, state.Access)) return Stop(ref state, LinuxSyscallGateStatus.InvalidReturn);
        if (state.CallsValue == state.Admission.MaximumCalls) return Stop(ref state, LinuxSyscallGateStatus.CallLimit);
        state.CallsValue++;
        if (state.Admission.EnableThreads && (ThreadCall(frame.Number) || DescriptorCall(frame.Number)))
        {
            LinuxSyscallFrame original = frame;
            LinuxSyscallResult result;
            fixed (LinuxSyscallFrame* captured = &frame) result = state.Access.Threads(state.Access.Context, captured);
            Clear(state.Admission.Scratch, state.Admission.ScratchBytes);
            if (!ThreadFrame(original, frame, result) || !ValidateReturn(original, state.Access) || !ValidateReturn(frame, state.Access))
            { frame = original; return Stop(ref state, LinuxSyscallGateStatus.InvalidReturn); }
            bool valid = (result.Action == LinuxSyscallAction.Return || result.Action == LinuxSyscallAction.RestoreUser && original.Number == 15) && result.ExitCode == 0 ||
                result.Action == LinuxSyscallAction.Blocked && result.Value == 0 && result.ExitCode == 0 && frame.Number is 0 or 1 or 202 or 219 ||
                (result.Action == LinuxSyscallAction.ThreadExited || result.Action == LinuxSyscallAction.Terminate) && result.Value == 0;
            if (!valid) return Stop(ref state, LinuxSyscallGateStatus.InvalidAdmission);
            if (result.Action == LinuxSyscallAction.Return && !AccountDescriptor(ref state, frame, result.Value))
                return Stop(ref state, LinuxSyscallGateStatus.SinkFailed);
            if (result.Action == LinuxSyscallAction.Blocked)
            { state.Pending = original; state.StatusValue = LinuxSyscallGateStatus.Blocked; }
            if (result.Action == LinuxSyscallAction.ThreadExited) state.StatusValue = LinuxSyscallGateStatus.Exited;
            if (result.Action == LinuxSyscallAction.Terminate) state.StatusValue = LinuxSyscallGateStatus.Exited;
            return result;
        }
        if (frame.Number == 60 || frame.Number == 231)
        {
            state.StatusValue = LinuxSyscallGateStatus.Exited;
            // Linux takes the low int argument; wait-status encoding is a later
            // process contract. Retain the original int bits without fabricating it.
            return new(LinuxSyscallAction.Terminate, exitCode: unchecked((uint)frame.Argument0));
        }
        if (state.Admission.EnableDescriptors && DescriptorCall(frame.Number))
        {
            LinuxSyscallFrame returnFrame = frame, callbackFrame = frame;
            long value = state.Access.Descriptors(state.Access.Context, &callbackFrame);
            Clear(state.Admission.Scratch, state.Admission.ScratchBytes);
            if (!ValidateReturn(returnFrame, state.Access)) return Stop(ref state, LinuxSyscallGateStatus.InvalidReturn);
            // With descriptors enabled this counter includes every successful
            // write/writev prefix, including pipes and duplicated output fds.
            if ((frame.Number == 1 || frame.Number == 20) && value > 0)
            {
                if (value > LinuxDescriptorServices.MaximumIo) return Stop(ref state, LinuxSyscallGateStatus.SinkFailed);
                state.BytesValue += (uint)value; // At most MaximumThreadCalls * 65536, below uint.MaxValue.
            }
            return new(LinuxSyscallAction.Return, value);
        }
        if (frame.Number != 1)
        {
            if (!state.Admission.EnableStartup) return new(LinuxSyscallAction.Return, Unsupported);
            LinuxSyscallFrame returnFrame = frame, callbackFrame = frame;
            long value = state.Access.Startup(state.Access.Context, &callbackFrame);
            Clear(state.Admission.Scratch, state.Admission.ScratchBytes);
            if (!ValidateReturn(returnFrame, state.Access)) return Stop(ref state, LinuxSyscallGateStatus.InvalidReturn);
            return new(LinuxSyscallAction.Return, value);
        }
        uint descriptor = unchecked((uint)frame.Argument0); // Linux write takes unsigned int fd.
        if (descriptor != 1 && descriptor != 2) return new(LinuxSyscallAction.Return, BadDescriptor);
        if (frame.Argument2 > state.Admission.MaximumWrite) return new(LinuxSyscallAction.Return, Invalid);
        uint bytes = (uint)frame.Argument2;
        if (bytes == 0) return new(LinuxSyscallAction.Return);
        if (!ValidateRange(frame.Argument1, bytes, LinuxUserPageAccess.Read, state.Access))
            return new(LinuxSyscallAction.Return, Fault);
        byte* scratch = state.Admission.Scratch;
        bool copied = state.Access.CopyFromUser(state.Access.Context, frame.Argument1, scratch, bytes) == 1;
        bool written = copied && state.Access.Write(state.Access.Context, descriptor, scratch, bytes) == 1;
        Clear(scratch, state.Admission.ScratchBytes);
        if (!copied) return Stop(ref state, LinuxSyscallGateStatus.CopyFailed);
        if (!written) return Stop(ref state, LinuxSyscallGateStatus.SinkFailed);
        state.BytesValue += bytes; // At most 64 * 4096; no wrap is admitted.
        return new(LinuxSyscallAction.Return, bytes);
    }

    /// <summary>Finish this worker's actual staged wait after it is scheduled
    /// again. The owner authenticates its kernel generation and consumes the
    /// pipe/futex completion first. Blocked retains the same call across a
    /// readiness retry; Return revalidates the saved user target and counts a
    /// write exactly once. Exit drains without validating an unused return
    /// target. No syscall is replayed, scheduled or counted here. Terminal gate
    /// failure still requires the native owner to drain all process resources.</summary>
    public static LinuxSyscallResult CompleteBlocked(ref LinuxSyscallGateState state, LinuxSyscallResult completion)
    {
        if (state.StatusValue != LinuxSyscallGateStatus.Blocked || !state.Admission.EnableThreads)
            return new(LinuxSyscallAction.Rejected);
        Clear(state.Admission.Scratch, state.Admission.ScratchBytes);
        if (completion.Action == LinuxSyscallAction.Blocked && completion.Value == 0 && completion.ExitCode == 0) return completion;
        if ((completion.Action == LinuxSyscallAction.ThreadExited || completion.Action == LinuxSyscallAction.Terminate) && completion.Value == 0)
        { state.Pending = default; state.StatusValue = LinuxSyscallGateStatus.Exited; return completion; }
        if (completion.Action != LinuxSyscallAction.Return || completion.ExitCode != 0)
            return Stop(ref state, LinuxSyscallGateStatus.InvalidAdmission);
        if (!ValidateReturn(state.Pending, state.Access)) return Stop(ref state, LinuxSyscallGateStatus.InvalidReturn);
        if (!AccountDescriptor(ref state, state.Pending, completion.Value)) return Stop(ref state, LinuxSyscallGateStatus.SinkFailed);
        state.Pending = default; state.StatusValue = LinuxSyscallGateStatus.Active; return completion;
    }

    private static bool AccountDescriptor(ref LinuxSyscallGateState state, LinuxSyscallFrame frame, long value)
    {
        if (value <= 0) return true;
        if (frame.Number is 0 or 1 && ((ulong)value > frame.Argument2 || value > LinuxDescriptorServices.MaximumIo)) return false;
        if (frame.Number is not (1 or 20)) return true;
        if (value > LinuxDescriptorServices.MaximumIo) return false;
        state.BytesValue += (uint)value; // MaximumThreadCalls * MaximumIo fits uint.
        return true;
    }

    public static bool ValidateReturn(LinuxSyscallFrame frame, LinuxSyscallAccess access) =>
        access.Context != null && access.PageAccess != null &&
        ValidateUserFlags(frame.Rflags,access.UserInterrupts) &&
        frame.Rsp >= 4104 && frame.Rsp <= UserEnd &&
        ValidateRange(frame.Rip, 1, LinuxUserPageAccess.Execute, access) &&
        ValidateRange(frame.Rsp - 8, 8, LinuxUserPageAccess.Write, access) &&
        (frame.FsBase == 0 || ValidateRange(frame.FsBase, 1, LinuxUserPageAccess.Read, access)) &&
        (frame.GsBase == 0 || ValidateRange(frame.GsBase, 1, LinuxUserPageAccess.Read, access));

    /// <summary>Check user status flags under the retained native owner's IRQ
    /// policy. IF must match that policy; IOPL/TF/RF/AC/VM remain unsupported.
    /// This checks a value only and grants no CPU or return authority.</summary>
    public static bool ValidateUserFlags(ulong flags,bool userInterrupts) =>
        (flags & 2)!=0 && (flags & ~(UserRflagsMask | (userInterrupts ? 0x200UL : 0)))==0 &&
        (flags & 0x200)==(userInterrupts ? 0x200UL : 0);

    public static bool ValidateRange(ulong address, ulong bytes, LinuxUserPageAccess required, LinuxSyscallAccess access)
    {
        if (access.Context == null || access.PageAccess == null || required == LinuxUserPageAccess.None ||
            ((byte)required & ~7) != 0 || address < 4096 || address >= UserEnd || bytes == 0 || bytes > UserEnd - address)
            return false;
        ulong last = (address + bytes - 1) & ~4095UL;
        for (ulong page = address & ~4095UL; ; page += 4096)
        {
            byte value = access.PageAccess(access.Context, page);
            if ((value & 1) == 0 || (value & ~7) != 0 || (value & 6) == 6 || (value & (byte)required) != (byte)required)
                return false;
            if (page == last) return true;
        }
    }

    private static LinuxSyscallResult Stop(ref LinuxSyscallGateState state, LinuxSyscallGateStatus status)
    { Clear(state.Admission.Scratch, state.Admission.ScratchBytes); state.Pending = default; state.StatusValue = status; return new(LinuxSyscallAction.Terminate); }
    private static bool Empty(LinuxSyscallFrame f) => f.Number == 0 && f.Argument0 == 0 && f.Argument1 == 0 && f.Argument2 == 0 &&
        f.Argument3 == 0 && f.Argument4 == 0 && f.Argument5 == 0 && f.Rip == 0 && f.Rsp == 0 && f.Rflags == 0 && f.FsBase == 0 && f.GsBase == 0;
    private static void Clear(byte* bytes, uint count) { for (uint i = 0; i < count; i++) bytes[i] = 0; }
    private static bool DescriptorCall(ulong number) => number is 0 or 1 or 2 or 3 or 5 or 8 or 16 or 17 or 20 or 22 or 72 or 257 or 293;
    private static bool ThreadCall(ulong number) => number is 13 or 14 or 15 or 24 or 35 or 39 or 56 or 60 or 131 or 158 or 186 or 202 or 218 or 219 or 231 or 234 or 273 or 274;
    private static bool ThreadFrame(LinuxSyscallFrame before, LinuxSyscallFrame after, LinuxSyscallResult result) =>
        result.Action == LinuxSyscallAction.RestoreUser
        ? before.Number == 15 && after.Number == 15 && before.FsBase == after.FsBase && before.GsBase == after.GsBase
        :
        before.Number == after.Number && before.Argument0 == after.Argument0 && before.Argument1 == after.Argument1 && before.Argument2 == after.Argument2 &&
        before.Argument3 == after.Argument3 && before.Argument4 == after.Argument4 && before.Argument5 == after.Argument5 &&
        before.Rip == after.Rip && before.Rsp == after.Rsp && before.Rflags == after.Rflags &&
        (before.Number == 158 && unchecked((uint)before.Argument0) == 0x1002 && result.Action == LinuxSyscallAction.Return && result.Value == 0
            ? after.FsBase == before.Argument1 : before.FsBase == after.FsBase) &&
        (before.Number == 158 && unchecked((uint)before.Argument0) == 0x1001 && result.Action == LinuxSyscallAction.Return && result.Value == 0
            ? after.GsBase == before.Argument1 : before.GsBase == after.GsBase);
}
