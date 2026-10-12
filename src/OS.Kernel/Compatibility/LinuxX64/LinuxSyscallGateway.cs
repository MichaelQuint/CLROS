namespace OS.Kernel.Compatibility.LinuxX64;

public enum LinuxSyscallGateStatus : byte
{ Uninitialized, Active, Exited, InvalidAdmission, AlreadyConsumed, InvalidReturn, CallLimit, CopyFailed, SinkFailed }
public enum LinuxSyscallAction : byte { Rejected, Return, Terminate }
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
    internal LinuxSyscallResult(LinuxSyscallAction action, long value = 0, uint exitCode = 0)
    { Action = action; Value = value; ExitCode = exitCode; }
}

public unsafe struct LinuxSyscallAccess
{
    public void* Context;
    // Only actual present USER leaves may return access. Read is mandatory;
    // RW+X is forbidden. The owner binds lookup/copy to the active admitted root.
    public delegate* unmanaged<void*, ulong, byte> PageAccess;
    // After complete range preflight, copy exactly the requested user bytes.
    // The owner prevents mapping changes and proves scratch/frame nonaliasing.
    public delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyFromUser;
    public delegate* unmanaged<void*, uint, byte*, uint, byte> Write;
}

public unsafe struct LinuxSyscallAdmission
{
    public ulong UserRoot;
    public uint MaximumCalls, MaximumWrite;
    public byte* Scratch;
    public uint ScratchBytes;
    public bool FirmwareExited, OwnedBoundaryValidated;
}

public unsafe struct LinuxSyscallGateState
{
    internal LinuxSyscallGateStatus StatusValue;
    internal LinuxSyscallAdmission Admission;
    internal LinuxSyscallAccess Access;
    internal uint CallsValue, BytesValue;
    public readonly LinuxSyscallGateStatus Status => StatusValue;
    public readonly uint Calls => CallsValue;
    public readonly uint BytesWritten => BytesValue;
}

/// <summary>
/// One-use checked write/exit dispatcher for the declared low-64GiB, single-CPU
/// boundary. Admission declarations and callback simulations are host contracts,
/// not CPL3 evidence. The native owner proves selectors/MSRs, dedicated kernel
/// stack, FS/GS/FP preservation, current root and immutable user mappings. It
/// validates every return before IRETQ; termination never resumes user code.
/// Kernel scratch is cleared after every attempted copy/write. No allocations,
/// exceptions, firmware calls, OS threading or upstream app acceptance is added.
/// </summary>
public static unsafe class LinuxSyscallGateway
{
    public const ulong UserEnd = OS.Kernel.Memory.X64UserAddressSpace.UserEnd, UserRflagsMask = 0x200cd7;
    public const uint MaximumCalls = 64, MaximumWrite = 4096;
    private const long BadDescriptor = -9, Fault = -14, Invalid = -22, Unsupported = -38;

    public static LinuxSyscallGateStatus Begin(ref LinuxSyscallGateState state,
        LinuxSyscallAdmission admission, LinuxSyscallAccess access)
    {
        if (state.StatusValue != LinuxSyscallGateStatus.Uninitialized) return LinuxSyscallGateStatus.AlreadyConsumed;
        if (state.CallsValue != 0 || state.BytesValue != 0 || !admission.FirmwareExited || !admission.OwnedBoundaryValidated ||
            admission.UserRoot < 4096 || admission.UserRoot >= OS.Kernel.Memory.X64UserAddressSpace.PhysicalEnd || (admission.UserRoot & 4095) != 0 ||
            admission.MaximumCalls == 0 || admission.MaximumCalls > MaximumCalls ||
            admission.MaximumWrite == 0 || admission.MaximumWrite > MaximumWrite ||
            admission.Scratch == null || admission.ScratchBytes < admission.MaximumWrite || admission.ScratchBytes > MaximumWrite ||
            (ulong)admission.Scratch > ~0UL - admission.ScratchBytes || access.Context == null ||
            access.PageAccess == null || access.CopyFromUser == null || access.Write == null)
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
        if (state.StatusValue != LinuxSyscallGateStatus.Active) return new(LinuxSyscallAction.Rejected);
        if (!ValidateReturn(frame, state.Access)) return Stop(ref state, LinuxSyscallGateStatus.InvalidReturn);
        if (state.CallsValue == state.Admission.MaximumCalls) return Stop(ref state, LinuxSyscallGateStatus.CallLimit);
        state.CallsValue++;
        if (frame.Number == 60 || frame.Number == 231)
        {
            state.StatusValue = LinuxSyscallGateStatus.Exited;
            // Linux takes the low int argument; wait-status encoding is a later
            // process contract. Retain the original int bits without fabricating it.
            return new(LinuxSyscallAction.Terminate, exitCode: unchecked((uint)frame.Argument0));
        }
        if (frame.Number != 1) return new(LinuxSyscallAction.Return, Unsupported);
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

    public static bool ValidateReturn(LinuxSyscallFrame frame, LinuxSyscallAccess access) =>
        access.Context != null && access.PageAccess != null &&
        (frame.Rflags & 2) != 0 && (frame.Rflags & ~UserRflagsMask) == 0 &&
        frame.Rsp >= 4104 && frame.Rsp <= UserEnd &&
        ValidateRange(frame.Rip, 1, LinuxUserPageAccess.Execute, access) &&
        ValidateRange(frame.Rsp - 8, 8, LinuxUserPageAccess.Write, access) &&
        (frame.FsBase == 0 || ValidateRange(frame.FsBase, 1, LinuxUserPageAccess.Read, access)) &&
        (frame.GsBase == 0 || ValidateRange(frame.GsBase, 1, LinuxUserPageAccess.Read, access));

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
    { Clear(state.Admission.Scratch, state.Admission.ScratchBytes); state.StatusValue = status; return new(LinuxSyscallAction.Terminate); }
    private static void Clear(byte* bytes, uint count) { for (uint i = 0; i < count; i++) bytes[i] = 0; }
}
