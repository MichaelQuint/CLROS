using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;
/// <summary>Linux x64 signal startup state for one externally serialized process.
/// The existing thread owner authenticates actual generations and holds this
/// supervisor-only storage until detach/End. Actions are shared; alternate
/// stacks and bounded user frames are per-thread. No allocation or kernel-side
/// handler invocation occurs. Pending selection and asynchronous capture belong
/// to the thread/native owner, not this metadata block.</summary>

public static unsafe partial class LinuxSignalServices
{
    public const uint SignalCount = 64, MaximumThreads = 16;
    public const uint OnStack = 1, Disabled = 2, AutoDisarm = 0x80000000, MinimumStackBytes = 2048;
    // Linux UAPI flags, including x86 SA_RESTORER; SA_UNSUPPORTED is cleared.
    public const ulong ActionFlags = 0xdc000807;
    /// <summary>Initialize one fresh metadata lifetime with IRQs disabled.
    /// Reject invalid/copy/overlap admission before mutation; no user bytes or
    /// scheduler state are accessed. Success owns only the supplied metadata.</summary>

    public static bool Initialize(State* state, uint bytes, ulong key, ulong flags, Admission a)
    {
        if (!Buffer((ulong)state, bytes) || state->Initialized != 0 || key == 0 || !IfClear(flags) || !a.FirmwareExited || !a.SingleOwnerValidated || a.Capacity == 0 || a.Capacity > MaximumThreads || a.User.Context == null || a.User.PageAccess == null || a.User.CopyFromUser == null || a.CopyToUser == null || Overlap((ulong)state, (uint)sizeof(State), (ulong)a.User.Context, 1))
            return false;
        *state = default;
        state->Self = (ulong)state;
        state->Key = key;
        state->Capacity = a.Capacity;
        state->User = a.User;
        state->CopyToUser = a.CopyToUser;
        state->Initialized = 1;
        return true;
    }

    /// <summary>Scrub this metadata after actual process detach and all thread
    /// reaps. Wrong keys, enabled IRQs, copies and live borrows fail unchanged.
    /// User stack backing remains owned by the separate process memory owner.</summary>

    public static bool End(State* state, ulong key, ulong flags)
    {
        if (!Audit(state) || state->Key != key || !IfClear(flags) || state->Process != 0 || !Empty(state))
            return false;
        *state = default;
        return true;
    }

    internal static bool Audit(State* s) => Buffer((ulong)s, (uint)sizeof(State)) && s->Initialized == 1 && s->Self == (ulong)s && s->Key != 0 && s->Capacity is > 0 and <= MaximumThreads && s->User.Context != null && s->User.PageAccess != null && s->User.CopyFromUser != null && s->CopyToUser != null;
    internal static bool Attach(State* s, ulong process, uint capacity, LinuxSyscallAccess user, delegate* unmanaged<void*, ulong, byte*, uint, byte> copyToUser)
    {
        if (!Audit(s) || process == 0 || s->Process != 0 || capacity != s->Capacity || !Empty(s) || s->User.Context != user.Context || s->User.UserInterrupts != user.UserInterrupts || (ulong)s->User.PageAccess != (ulong)user.PageAccess || (ulong)s->User.CopyFromUser != (ulong)user.CopyFromUser || (ulong)s->CopyToUser != (ulong)copyToUser)
            return false;
        s->Process = process;
        return true;
    }

    internal static bool Detach(State* s, ulong process)
    {
        if (!Attached(s, process) || !Empty(s))
            return false;
        s->Process = 0;
        return true;
    }

    internal static bool Bind(State* s, ulong process, OSHostHandle identity)
    {
        uint index = (uint)identity.Value - 1;
        if (!Attached(s, process) || !identity.IsValid || index >= s->Capacity)
            return false;
        ThreadState* slots = (ThreadState*)s->Threads;
        if (slots[index].Identity != 0)
            return false;
        // pthread clone shares VM: Linux clears the child's alternate stack.
        slots[index] = new()
        {
            Identity = identity.Value,
            Stack = new()
            {
                Flags = Disabled
            }
        };
        return true;
    }

    internal static bool Bound(State* s, ulong process, OSHostHandle identity) => Attached(s, process) && identity.IsValid && (uint)identity.Value - 1 < s->Capacity && ((ThreadState*)s->Threads)[(uint)identity.Value - 1].Identity == identity.Value;
    // Non-owning diagnostic values from the already authenticated generation.
    // Never read user frames or expose the private hardware-provenance tag.
    internal static bool InspectFrame(State* s, ulong process, OSHostHandle identity, out ulong depth, out ulong frame)
    {
        depth = frame = 0;
        if (!Bound(s, process, identity))
            return false;
        ThreadState* thread = (ThreadState*)s->Threads + ((uint)identity.Value - 1);
        if (thread->Depth > MaximumNesting)
            return false;
        depth = thread->Depth;
        if (depth != 0)
            frame = thread->Frames[depth - 1] & ~1UL;
        return true;
    }

    internal static bool Unbind(State* s, ulong process, OSHostHandle identity)
    {
        if (!Bound(s, process, identity))
            return false;
        ((ThreadState*)s->Threads)[(uint)identity.Value - 1] = default;
        return true;
    }

    internal static long Dispatch(State* s, ulong process, OSHostHandle identity, LinuxSyscallFrame frame)
    {
        if (!Bound(s, process, identity))
            return -5;
        if (frame.Number == 13)
            return Sigaction(s, frame);
        if (frame.Number == 131)
            return Sigaltstack(s, (ThreadState*)s->Threads + ((uint)identity.Value - 1), frame);
        return -38; // sigreturn needs the actual owner's complete user image.
    }

    private static long Sigaction(State* s, LinuxSyscallFrame f)
    {
        if (f.Argument3 != 8)
            return -22;
        Disposition requested = default;
        if (f.Argument1 != 0 && !Input(s, f.Argument1, (byte*)&requested, 32))
            return -14;
        int signal = unchecked((int)f.Argument0);
        if (signal < 1 || signal > 64 || f.Argument1 != 0 && signal is 9 or 19)
            return -22;
        Disposition* action = (Disposition*)s->Actions + signal - 1;
        Disposition prior = *action;
        if (f.Argument1 != 0)
        {
            requested.Flags &= ActionFlags;
            requested.Mask &= ~LinuxSignalMask.Unblockable;
            *action = requested;
            if (Ignored(s, (uint)signal))
                Discard(s, (uint)signal);
        }

        // Linux commits a valid action before old-action copyout. EFAULT must
        // preserve the new disposition rather than silently rolling it back.
        return f.Argument2 == 0 || Output(s, f.Argument2, (byte*)&prior, 32) ? 0 : -14;
    }

    private static long Sigaltstack(State* s, ThreadState* thread, LinuxSyscallFrame f)
    {
        AlternateStack requested = default;
        if (f.Argument0 != 0 && !Input(s, f.Argument0, (byte*)&requested, 24))
            return -14;
        AlternateStack prior = thread->Stack;
        bool on = OnAlternate(prior, f.Rsp);
        prior.Flags = (prior.Bytes == 0 ? Disabled : on ? OnStack : 0) | (prior.Flags & AutoDisarm);
        prior.Padding = 0;
        if (f.Argument0 != 0)
        {
            if (on)
                return -1;
            uint mode = requested.Flags & ~AutoDisarm;
            if (mode is not (0 or OnStack or Disabled))
                return -22;
            if (mode == Disabled)
                requested.Address = requested.Bytes = 0;
            else if (requested.Bytes < MinimumStackBytes)
                return -12;
            requested.Padding = 0;
            thread->Stack = requested;
        }

        // As with sigaction, a failed output copy does not undo a valid update.
        return f.Argument1 == 0 || Output(s, f.Argument1, (byte*)&prior, 24) ? 0 : -14;
    }

    private static bool OnAlternate(AlternateStack stack, ulong rsp) => (stack.Flags & AutoDisarm) == 0 && stack.Bytes != 0 && rsp > stack.Address && rsp - stack.Address <= stack.Bytes;
    private static bool Empty(State* s)
    {
        for (uint i = 0; i < MaximumThreads * 14; i++)
            if (s->Threads[i] != 0)
                return false;
        return true;
    }

    private static bool Attached(State* s, ulong process) => Audit(s) && process != 0 && s->Process == process;
    private static bool Input(State* s, ulong address, byte* value, uint bytes) => LinuxSyscallGateway.ValidateRange(address, bytes, LinuxUserPageAccess.Read, s->User) && s->User.CopyFromUser(s->User.Context, address, value, bytes) == 1;
    private static bool Output(State* s, ulong address, byte* value, uint bytes) => LinuxSyscallGateway.ValidateRange(address, bytes, LinuxUserPageAccess.Write, s->User) && s->CopyToUser(s->User.Context, address, value, bytes) == 1;
    private static bool IfClear(ulong flags) => (flags & 0x202) == 2;
    private static bool Buffer(ulong address, uint bytes) => address >= 4096 && (address & 7) == 0 && bytes >= sizeof(State) && address <= 0x0000800000000000UL - bytes;
    private static bool Overlap(ulong a, uint an, ulong b, uint bn) => a < b + bn && b < a + an;
}
