namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>
/// Measured startup syscalls for one owned Linux context. Memory operations use
/// the genuine sparse backend; the owner supplies observed single-CPU/resource
/// facts, a live monotonic clock and separately captured fresh entropy. All
/// pointers/callbacks remain exclusive and mapped for this one-use lifetime.
/// Call under the kernel root with the target user root inactive, then audit and
/// reload it before resumption. No threads, signals, wall clock, blocking entropy
/// refill, demand paging or broad Linux compatibility is implied. Negative errno
/// reports unsupported/malformed/exhausted requests; there are no allocations or
/// firmware calls. Output callbacks must copy the complete preflighted range.
/// </summary>
public static unsafe class LinuxStartupServices
{
    private const long Invalid = -22, Fault = -14, Unsupported = -38, Again = -11, Io = -5;
    public const uint EntropyBytes = 32;

    /// <summary>Immutable facts and borrowed capabilities. Physical/available
    /// bytes are actual owned snapshots, not Linux-host values. The IRQ-off
    /// single-context owner excludes other resource consumers during execution.
    /// AvailableBytes includes the unused pool; pool commitments are deducted.
    /// Entropy must be separate from AT_RANDOM and never published elsewhere.
    /// </summary>
    public struct Admission
    {
        public LinuxMemoryServices.State* Memory;
        public LinuxSyscallAccess User;
        public delegate* unmanaged<void*, ulong*, byte> ReadClock;
        public delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyToUser;
        public delegate* unmanaged<void*, byte> MemoryBarrier;
        public ulong FrequencyHz, PhysicalBytes, AvailableBytes, HeapStart, HeapBytes, StackBytes;
        public uint ProcessId;
        public byte* Entropy;
        public uint CapturedEntropyBytes;
        public bool SingleCpuValidated, FirmwareExited;
    }

    /// <summary>Caller-owned single-use state; do not copy a live instance.
    /// Successful output burns fresh entropy once. After valid preflight an
    /// attempted copy also burns it on callback failure, preventing reuse of
    /// partially exposed bytes. Invalid pointers consume no entropy.
    /// </summary>
    public struct State
    {
        internal Admission Owner;
        internal ulong CurrentBreak, LastTicks;
        internal uint Initialized, UsedEntropy, BarrierRegistered;
        public readonly ulong Break => CurrentBreak;
        public readonly uint RemainingEntropy => Initialized == 1 ? EntropyBytes - UsedEntropy : 0;
    }

    /// <summary>Reserve the actual bounded brk interval with PROT_NONE after
    /// complete input/clock admission. Failure leaves state and ownership empty.
    /// A consumed state cannot be restarted. Owner proves the entropy source
    /// and all callback/context identities and inactive-root authority.
    /// </summary>
    public static bool Begin(State* state, Admission admission)
    {
        if (state == null || ((ulong)state & 7) != 0 || (ulong)state > ~0UL - (ulong)sizeof(State) ||
            state->Initialized != 0 || state->UsedEntropy != 0 || state->BarrierRegistered != 0 || state->CurrentBreak != 0 || state->LastTicks != 0 ||
            !admission.SingleCpuValidated || !admission.FirmwareExited || admission.ProcessId == 0 ||
            admission.Memory == null || !LinuxMemoryServices.Audit(admission.Memory) ||
            admission.User.Context == null || admission.User.PageAccess == null || admission.ReadClock == null || admission.CopyToUser == null || admission.MemoryBarrier == null ||
            admission.FrequencyHz < 1000000 || admission.FrequencyHz > 10000000000 ||
            admission.PhysicalBytes == 0 || admission.PhysicalBytes > 0x100000000 ||
            admission.AvailableBytes > admission.PhysicalBytes ||
            admission.AvailableBytes < admission.Memory->Memory.PoolPages * 4096UL ||
            (admission.PhysicalBytes & 4095) != 0 || (admission.AvailableBytes & 4095) != 0 ||
            admission.StackBytes == 0 || admission.StackBytes > 16 * 1024 * 1024 ||
            admission.HeapStart < LinuxMemoryServices.DynamicStart || (admission.HeapStart & 4095) != 0 ||
            admission.HeapStart >= LinuxSyscallGateway.UserEnd || admission.HeapBytes == 0 || (admission.HeapBytes & 4095) != 0 ||
            admission.HeapBytes > LinuxSyscallGateway.UserEnd - admission.HeapStart ||
            admission.Entropy == null || admission.CapturedEntropyBytes != EntropyBytes ||
            (ulong)admission.Entropy > ~0UL - EntropyBytes ||
            Overlap((ulong)admission.Entropy, EntropyBytes, (ulong)state, (ulong)sizeof(State)) ||
            Overlap((ulong)admission.Memory, (ulong)sizeof(LinuxMemoryServices.State), (ulong)state, (ulong)sizeof(State)) ||
            !EntropyStorage(admission)) return false;
        bool nonzero = false;
        for (uint i = 0; i < EntropyBytes; i++) if (admission.Entropy[i] != 0) nonzero = true;
        ulong ticks = 0;
        if (!nonzero || admission.ReadClock(admission.User.Context, &ticks) != 1) return false;
        if (LinuxMemoryServices.ReserveHeap(admission.Memory, admission.HeapStart, admission.HeapBytes) != (long)admission.HeapStart) return false;
        state->Owner = admission; state->CurrentBreak = admission.HeapStart; state->LastTicks = ticks; state->Initialized = 1;
        return true;
    }

    /// <summary>Dispatch from the explicit M16-006 gateway extension. Unknown
    /// calls return ENOSYS. The gateway independently checks the user's return
    /// mappings again after any operation that can remove/protect those pages.
    /// </summary>
    public static long Dispatch(State* state, LinuxSyscallFrame frame)
    {
        if (state == null || ((ulong)state & 7) != 0 || state->Initialized != 1 || state->UsedEntropy > EntropyBytes ||
            !LinuxMemoryServices.Audit(state->Owner.Memory)) return Fault;
        var a = state->Owner;
        switch (frame.Number)
        {
            case 9:
                if (frame.Argument2 > 0xffffffffUL) return Invalid;
                return LinuxMemoryServices.Map(a.Memory, frame.Argument0, frame.Argument1, (uint)frame.Argument2,
                    frame.Argument3, frame.Argument4, frame.Argument5);
            case 10:
                if (frame.Argument2 > 0xffffffffUL) return Invalid;
                return LinuxMemoryServices.Protect(a.Memory, frame.Argument0, frame.Argument1, (uint)frame.Argument2);
            case 11: return LinuxMemoryServices.Unmap(a.Memory, frame.Argument0, frame.Argument1);
            case 12: return Brk(state, frame.Argument0);
            case 25: return LinuxMemoryServices.Remap(a.Memory, frame.Argument0, frame.Argument1, frame.Argument2, frame.Argument3);
            case 28: return LinuxMemoryServices.Advise(a.Memory, frame.Argument0, frame.Argument1, unchecked((uint)frame.Argument2));
            case 39: case 186: return a.ProcessId;
            case 97: return frame.Argument1 == 0 ? Fault : Limit(state, unchecked((uint)frame.Argument0), frame.Argument1);
            case 99: return Info(state, frame.Argument0);
            case 204:
                if (unchecked((uint)frame.Argument0) != 0 && unchecked((uint)frame.Argument0) != a.ProcessId) return -3;
                if (frame.Argument1 < 8 || frame.Argument1 > 4096) return Invalid;
                byte* mask = stackalloc byte[8]; *(ulong*)mask = 1;
                return Output(state, frame.Argument2, mask, 8) == 0 ? 8 : Fault;
            case 228: return Clock(state, unchecked((uint)frame.Argument0), frame.Argument1);
            case 302:
                if (unchecked((uint)frame.Argument0) != 0 && unchecked((uint)frame.Argument0) != a.ProcessId) return -3;
                if (frame.Argument2 != 0) return -1; // Fixed resource policy cannot be changed by this context.
                return Limit(state, unchecked((uint)frame.Argument1), frame.Argument3);
            case 318: return Random(state, frame.Argument0, frame.Argument1, frame.Argument2);
            case 324:
                if (frame.Argument1 != 0 || frame.Argument2 != 0) return Invalid;
                if (frame.Argument0 == 0) return 0x18; // Only PRIVATE_EXPEDITED and its registration.
                if (frame.Argument0 == 16) { state->BarrierRegistered = 1; return 0; }
                if (frame.Argument0 != 8) return Invalid;
                if (state->BarrierRegistered != 1) return -1;
                return a.MemoryBarrier(a.User.Context) == 1 ? 0 : Io;
            default: return Unsupported;
        }
    }

    private static long Brk(State* state, ulong requested)
    {
        var a = state->Owner; ulong current = state->CurrentBreak;
        if (requested == 0) return (long)current;
        if (requested < a.HeapStart || requested > a.HeapStart + a.HeapBytes) return (long)current;
        if (LinuxMemoryServices.ResizeHeap(a.Memory, a.HeapStart, current, requested) != 0) return (long)current;
        state->CurrentBreak = requested; return (long)requested;
    }

    private static long Random(State* state, ulong address, ulong requested, ulong flags)
    {
        if ((flags & ~3UL) != 0 || requested > EntropyBytes) return Invalid;
        if (requested == 0) return 0;
        uint count = (uint)requested;
        if (!LinuxSyscallGateway.ValidateRange(address, count, LinuxUserPageAccess.Write, state->Owner.User)) return Fault;
        if (state->UsedEntropy == EntropyBytes) return (flags & 1) != 0 ? Again : Unsupported;
        if (count > EntropyBytes - state->UsedEntropy && (flags & 1) == 0) return Unsupported;
        if (count > EntropyBytes - state->UsedEntropy) count = EntropyBytes - state->UsedEntropy;
        byte* bytes = stackalloc byte[(int)EntropyBytes];
        for (uint i = 0; i < count; i++)
        { bytes[i] = state->Owner.Entropy[state->UsedEntropy + i]; state->Owner.Entropy[state->UsedEntropy + i] = 0; }
        state->UsedEntropy += count;
        bool copied = state->Owner.CopyToUser(state->Owner.User.Context, address, bytes, count) == 1;
        for (uint i = 0; i < EntropyBytes; i++) bytes[i] = 0;
        return copied ? count : Fault;
    }

    private static bool Read(State* state, out ulong ticks)
    {
        ticks = 0; ulong value = 0;
        if (state->Owner.ReadClock(state->Owner.User.Context, &value) != 1 || value < state->LastTicks) return false;
        state->LastTicks = ticks = value; return true;
    }
    private static long Clock(State* state, uint id, ulong address)
    {
        if (id != 1) return Invalid; // No fabricated realtime/UTC source.
        if (!LinuxSyscallGateway.ValidateRange(address, 16, LinuxUserPageAccess.Write, state->Owner.User)) return Fault;
        if (!Read(state, out ulong ticks)) return Io;
        byte* time = stackalloc byte[16]; ulong frequency = state->Owner.FrequencyHz;
        *(ulong*)time = ticks / frequency; *(ulong*)(time + 8) = (ticks % frequency) * 1000000000 / frequency;
        return Output(state, address, time, 16);
    }
    private static long Info(State* state, ulong address)
    {
        if (!LinuxSyscallGateway.ValidateRange(address, 112, LinuxUserPageAccess.Write, state->Owner.User)) return Fault;
        if (!Read(state, out ulong ticks)) return Io;
        byte* bytes = stackalloc byte[112]; for (uint i = 0; i < 112; i++) bytes[i] = 0;
        *(ulong*)bytes = ticks / state->Owner.FrequencyHz;
        *(ulong*)(bytes + 32) = state->Owner.PhysicalBytes;
        *(ulong*)(bytes + 40) = state->Owner.AvailableBytes - state->Owner.Memory->BackedPages * 4096UL;
        *(ushort*)(bytes + 80) = 1; *(uint*)(bytes + 104) = 1;
        // No swap/high-memory/page-cache or sampled scheduler-load service exists.
        // Only the admitted process count, memory quantities and uptime are supplied.
        return Output(state, address, bytes, 112);
    }
    private static long Limit(State* state, uint resource, ulong address)
    {
        ulong limit;
        if (resource == 3) limit = state->Owner.StackBytes;
        else if (resource == 9)
        {
            limit = state->Owner.Memory->Memory.ReservationBytes;
            var memory = state->Owner.Memory->Memory;
            for (uint i = 0; i < memory.StaticCount; i++) if (memory.StaticRanges[i].User) limit += memory.StaticRanges[i].Pages * 4096;
        }
        else return Invalid;
        if (address == 0) return 0;
        byte* bytes = stackalloc byte[16]; *(ulong*)bytes = *(ulong*)(bytes + 8) = limit;
        return Output(state, address, bytes, 16);
    }
    private static long Output(State* state, ulong address, byte* source, uint bytes) =>
        LinuxSyscallGateway.ValidateRange(address, bytes, LinuxUserPageAccess.Write, state->Owner.User) &&
        state->Owner.CopyToUser(state->Owner.User.Context, address, source, bytes) == 1 ? 0 : Fault;
    private static bool EntropyStorage(Admission admission)
    {
        var m = admission.Memory->Memory; ulong source = (ulong)admission.Entropy;
        return !Overlap(source, EntropyBytes, (ulong)admission.Memory, (ulong)sizeof(LinuxMemoryServices.State)) &&
            !Overlap(source, EntropyBytes, (ulong)m.Pool, m.PoolPages * 4096UL) &&
            !Overlap(source, EntropyBytes, (ulong)m.Tables, m.TablePages * 4096UL) &&
            !Overlap(source, EntropyBytes, (ulong)m.CandidateTables, m.TablePages * 4096UL) &&
            !Overlap(source, EntropyBytes, (ulong)m.StaticRanges, m.StaticCount * (ulong)sizeof(OS.Kernel.Memory.X64UserMappingRange));
    }
    private static bool Overlap(ulong a, ulong aBytes, ulong b, ulong bBytes) => a < b + bBytes && b < a + aBytes;
}
