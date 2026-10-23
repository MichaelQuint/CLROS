using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>
/// Measured startup syscalls for one owned Linux context. Memory operations use
/// the genuine sparse backend; the owner supplies observed single-CPU/resource
/// facts, a live monotonic clock and separately captured fresh entropy. All
/// pointers/callbacks remain exclusive and mapped for this one-use lifetime.
/// Call under the kernel root with the target user root inactive, then audit and
/// reload it before resumption. Optional realtime borrows an exclusively owned,
/// pre-exit seeded clock using this same monotonic epoch and frequency. No
/// threads, signals, clock synchronization, blocking entropy
/// refill, demand paging or broad Linux compatibility is implied. Negative errno
/// reports unsupported/malformed/exhausted requests; there are no allocations or
/// firmware calls. Output callbacks must copy the complete preflighted range.
/// </summary>
public static unsafe class LinuxStartupServices
{
    private const long Invalid = -22, Fault = -14, Unsupported = -38, Again = -11, Io = -5;
    public const uint EntropyBytes = 32;
    /// <summary>Explicit larger lifetime budget for the admitted runtime batch;
    /// individual requests still have the original 32-byte bound. No refill or
    /// deterministic expansion is provided.</summary>
    public const uint RuntimeEntropyBytes = 256;
    /// <summary>Explicit finite web lifetime of genuinely captured bytes;
    /// requests remain bounded to 32 bytes, with no refill or expansion.</summary>
    public const uint WebEntropyBytes = 4096;

    /// <summary>Immutable facts and borrowed capabilities. Physical/available
    /// bytes are actual owned snapshots, not Linux-host values. The IRQ-off
    /// single-context owner excludes other resource consumers during execution.
    /// AvailableBytes includes the unused pool; pool commitments are deducted.
    /// Entropy must be separate from AT_RANDOM and never published elsewhere.
    /// Finite CapturedEntropyBytes is exactly32,256 or4096 genuinely acquired
    /// bytes. Explicit ContinuingRandom instead borrows a private initialized
    /// generator with no reservoir; both lifetimes stay exclusive through cleanup.
    /// Realtime, when present, stays exclusively mapped through cleanup and uses
    /// this exact monotonic source/frequency. RequireRealtime prevents heap
    /// admission without an available UTC owner. No firmware authority crosses
    /// this boundary; optional absence retains the unsupported realtime errno.
    /// </summary>
    public struct Admission
    {
        public LinuxMemoryServices.State* Memory;
        public LinuxSyscallAccess User;
        public delegate* unmanaged<void*, ulong*, byte> ReadClock;
        public delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyToUser;
        public delegate* unmanaged<void*, byte> MemoryBarrier;
        public OS.Kernel.Platform.OwnedRealtimeClock.State* Realtime;
        public ulong FrequencyHz, PhysicalBytes, AvailableBytes, HeapStart, HeapBytes, StackBytes;
        /// <summary>Optional value-only budget from the immutable package and
        /// sealed boot ledger. Success must match this actual pool and cover the
        /// initial heap; failed preflight cannot grant startup. Legacy absence
        /// retains prior policy. This does not partition GC/native pool pages.</summary>
        public OS.Manifest.LinuxAppConfiguration.GcBudget GcBudget;
        public uint ProcessId;
        public byte* Entropy;
        public uint CapturedEntropyBytes;
        /// <summary>Explicit service-only borrowed generator, initialized from
        /// independently captured private entropy, never AT_RANDOM or a TCP key.
        /// ContinuingRandom requires preemptible admission and excludes the finite
        /// reservoir. Keep it exclusive/mapped through DiscardEntropy; no firmware
        /// callback or source fallback is retained. Reseed is owner-authorized.</summary>
        public HmacSha256Drbg.State* Generator;
        public bool ContinuingRandom;
        public bool SingleCpuValidated, FirmwareExited, RequireRealtime;
    }

    /// <summary>Caller-owned single-use state; do not copy a live instance.
    /// Finite output consumes captured bytes; service output advances its DRBG.
    /// After valid preflight an attempted copy irrevocably consumes the entire
    /// generated request even on callback failure. Invalid pointers consume none.
    /// </summary>
    public struct State
    {
        internal Admission Owner;
        public readonly OS.Manifest.LinuxAppConfiguration.GcBudget GcBudget => Initialized == 1 ? Owner.GcBudget : default;
        internal ulong CurrentBreak, LastTicks;
        internal uint Initialized, UsedEntropy, BarrierRegistered;
        internal uint EntropyDiscarded;
        internal ulong GeneratedRequests, GeneratedBytes;
        public readonly ulong Break => CurrentBreak;
        /// <summary>Unused finite reservoir bytes; always0 for service mode,
        /// never a DRBG availability or entropy-quality observation.</summary>
        public readonly uint RemainingEntropy => Initialized == 1 ? Owner.CapturedEntropyBytes - UsedEntropy : 0;
        /// <summary>Lifetime generated service requests, including failed output
        /// copies; serialized overflow refuses another request before mutation.</summary>
        public readonly ulong RandomRequests => GeneratedRequests;
        /// <summary>Lifetime generated service bytes, including failed output
        /// copies; these are output accounting, not credited physical entropy.</summary>
        public readonly ulong RandomBytes => GeneratedBytes;
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
            state->EntropyDiscarded != 0 || state->GeneratedRequests != 0 || state->GeneratedBytes != 0 ||
            !admission.SingleCpuValidated || !admission.FirmwareExited || admission.ProcessId == 0 ||
            admission.Memory == null || !LinuxMemoryServices.Audit(admission.Memory) ||
            admission.User.Context == null || admission.User.PageAccess == null || admission.ReadClock == null || admission.CopyToUser == null || admission.MemoryBarrier == null ||
            admission.FrequencyHz < 1000000 || admission.FrequencyHz > 10000000000 ||
            admission.PhysicalBytes == 0 || admission.PhysicalBytes > 0x100000000 ||
            admission.AvailableBytes > admission.PhysicalBytes ||
            admission.AvailableBytes < admission.Memory->Memory.PoolPages * 4096UL ||
            !AdmitGcBudget(admission) ||
            (admission.PhysicalBytes & 4095) != 0 || (admission.AvailableBytes & 4095) != 0 ||
            admission.StackBytes == 0 || admission.StackBytes > 16 * 1024 * 1024 ||
            admission.HeapStart < LinuxMemoryServices.DynamicStart || (admission.HeapStart & 4095) != 0 ||
            admission.HeapStart >= LinuxSyscallGateway.UserEnd || admission.HeapBytes == 0 || (admission.HeapBytes & 4095) != 0 ||
            admission.HeapBytes > LinuxSyscallGateway.UserEnd - admission.HeapStart ||
            Overlap((ulong)admission.Memory, (ulong)sizeof(LinuxMemoryServices.State), (ulong)state, (ulong)sizeof(State)) ||
            !AdmitEntropy(state, admission)) return false;
        bool nonzero = admission.ContinuingRandom;
        for (uint i = 0; i < admission.CapturedEntropyBytes; i++) if (admission.Entropy[i] != 0) nonzero = true;
        ulong ticks = 0;
        if (!nonzero || admission.ReadClock(admission.User.Context, &ticks) != 1 ||
            (admission.RequireRealtime && admission.Realtime == null)) return false;
        if (admission.Realtime != null &&
            (((ulong)admission.Realtime & 7) != 0 ||
             (ulong)admission.Realtime > ~0UL - (ulong)sizeof(OS.Kernel.Platform.OwnedRealtimeClock.State) ||
             admission.Realtime->FrequencyHz != admission.FrequencyHz ||
             !OS.Kernel.Platform.OwnedRealtimeClock.Read(ref *admission.Realtime, ticks, out _, out _))) return false;
        if (LinuxMemoryServices.ReserveHeap(admission.Memory, admission.HeapStart, admission.HeapBytes) != (long)admission.HeapStart) return false;
        state->Owner = admission; state->CurrentBreak = admission.HeapStart; state->LastTicks = ticks; state->Initialized = 1;
        return true;
    }

    private static bool AdmitGcBudget(Admission admission)
    {
        var budget = admission.GcBudget;
        if (budget.Result == OS.Manifest.LinuxAppConfiguration.Status.Uninitialized ||
            budget.Result == OS.Manifest.LinuxAppConfiguration.Status.Unconfigured) return true;
        return budget.Succeeded && budget.PoolBytes == admission.Memory->Memory.PoolPages * 4096UL &&
            admission.HeapBytes <= budget.NativeReserveBytes;
    }

    /// <summary>Dispatch from the explicit M16-006 gateway extension. Unknown
    /// calls return ENOSYS. The gateway independently checks the user's return
    /// mappings again after any operation that can remove/protect those pages.
    /// </summary>
    public static long Dispatch(State* state, LinuxSyscallFrame frame)
        => Dispatch(state, frame, out _);

    /// <summary>Dispatch with one copied, operation-local physical backing
    /// observation under the same inactive-root/serialized-owner contract.
    /// True proves the candidate growth needs more total backing pages than its
    /// actual exclusive pool supplies after replacement. Other metadata, table
    /// or guard refusals can coexist; this is not an exclusive errno diagnosis,
    /// a GC/native partition or permission to stop a process. False includes
    /// success and any refusal without this proven physical shortfall. Failed
    /// calls retain existing byte-level state/accounting atomicity; brk still
    /// returns its old break on failure. No observation survives into later calls.</summary>
    public static long Dispatch(State* state, LinuxSyscallFrame frame, out bool physicalBackingExhausted)
    {
        physicalBackingExhausted = false;
        if (state == null || ((ulong)state & 7) != 0 || state->Initialized != 1 ||
            !EntropyState(state) ||
            !LinuxMemoryServices.Audit(state->Owner.Memory)) return Fault;
        var a = state->Owner;
        switch (frame.Number)
        {
            case 9:
                if (frame.Argument2 > 0xffffffffUL) return Invalid;
                return LinuxMemoryServices.Map(a.Memory, frame.Argument0, frame.Argument1, (uint)frame.Argument2,
                    frame.Argument3, frame.Argument4, frame.Argument5, out physicalBackingExhausted);
            case 10:
                if (frame.Argument2 > 0xffffffffUL) return Invalid;
                return LinuxMemoryServices.Protect(a.Memory, frame.Argument0, frame.Argument1, (uint)frame.Argument2,
                    out physicalBackingExhausted);
            case 11: return LinuxMemoryServices.Unmap(a.Memory, frame.Argument0, frame.Argument1);
            case 12: return Brk(state, frame.Argument0, out physicalBackingExhausted);
            case 25: return LinuxMemoryServices.Remap(a.Memory, frame.Argument0, frame.Argument1, frame.Argument2, frame.Argument3,
                out physicalBackingExhausted);
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

    private static long Brk(State* state, ulong requested, out bool physicalBackingExhausted)
    {
        physicalBackingExhausted = false;
        var a = state->Owner; ulong current = state->CurrentBreak;
        if (requested == 0) return (long)current;
        if (requested < a.HeapStart || requested > a.HeapStart + a.HeapBytes) return (long)current;
        if (LinuxMemoryServices.ResizeHeap(a.Memory, a.HeapStart, current, requested, out physicalBackingExhausted) != 0)
            return (long)current;
        state->CurrentBreak = requested; return (long)requested;
    }

    private static long Random(State* state, ulong address, ulong requested, ulong flags)
    {
        if ((flags & ~3UL) != 0 || !state->Owner.ContinuingRandom && requested > EntropyBytes) return Invalid;
        if (requested == 0) return 0;
        if (state->Owner.ContinuingRandom) return ServiceRandom(state, address, requested, flags);
        uint count = (uint)requested;
        if (!LinuxSyscallGateway.ValidateRange(address, count, LinuxUserPageAccess.Write, state->Owner.User)) return Fault;
        uint remaining = state->Owner.CapturedEntropyBytes - state->UsedEntropy;
        if (remaining == 0) return (flags & 1) != 0 ? Again : Unsupported;
        if (count > remaining && (flags & 1) == 0) return Unsupported;
        if (count > remaining) count = remaining;
        byte* bytes = stackalloc byte[(int)EntropyBytes];
        for (uint i = 0; i < count; i++)
        { bytes[i] = state->Owner.Entropy[state->UsedEntropy + i]; state->Owner.Entropy[state->UsedEntropy + i] = 0; }
        state->UsedEntropy += count;
        bool copied = state->Owner.CopyToUser(state->Owner.User.Context, address, bytes, count) == 1;
        for (uint i = 0; i < EntropyBytes; i++) bytes[i] = 0;
        return copied ? count : Fault;
    }

    private static long ServiceRandom(State* state, ulong address, ulong requested, ulong flags)
    {
        uint count = requested > HmacSha256Drbg.MaximumRequestBytes ? HmacSha256Drbg.MaximumRequestBytes : (uint)requested;
        // Validate the bounded result prefix before producing any bytes. Once
        // output is attempted the complete request is consumed, even on partial
        // copy failure. Reusing generator state would repeat disclosed output.
        if (!LinuxSyscallGateway.ValidateRange(address, count, LinuxUserPageAccess.Write, state->Owner.User)) return Fault;
        if (state->EntropyDiscarded != 0 || state->Owner.Generator->RequiresReseed ||
            state->GeneratedRequests == ulong.MaxValue || state->GeneratedBytes > ulong.MaxValue - count)
            return (flags & 1) != 0 ? Again : Unsupported;
        byte* bytes = stackalloc byte[(int)HmacSha256Drbg.MaximumRequestBytes];
        if (!HmacSha256Drbg.Generate(state->Owner.Generator, bytes, count))
        { SensitiveMemory.Clear(bytes, HmacSha256Drbg.MaximumRequestBytes); return Io; }
        state->GeneratedRequests++; state->GeneratedBytes += count;
        bool copied = state->Owner.CopyToUser(state->Owner.User.Context, address, bytes, count) == 1;
        SensitiveMemory.Clear(bytes, HmacSha256Drbg.MaximumRequestBytes);
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
        if (id != 1 && id != 6 && (id != 0 || state->Owner.Realtime == null)) return Invalid;
        if (!LinuxSyscallGateway.ValidateRange(address, 16, LinuxUserPageAccess.Write, state->Owner.User)) return Fault;
        if (!Read(state, out ulong ticks)) return Io;
        byte* time = stackalloc byte[16]; ulong frequency = state->Owner.FrequencyHz;
        if (id == 0)
        {
            if (!OS.Kernel.Platform.OwnedRealtimeClock.Read(ref *state->Owner.Realtime, ticks,
                    out long seconds, out uint nanoseconds)) return Io;
            *(long*)time = seconds; *(ulong*)(time + 8) = nanoseconds;
        }
        else
        { *(ulong*)time = ticks / frequency; *(ulong*)(time + 8) = (ticks % frequency) * 1000000000 / frequency; }
        // CLOCK_MONOTONIC_COARSE shares the live monotonic epoch and rounds
        // down to this adapter's declared millisecond resolution.
        if (id == 6) *(ulong*)(time + 8) = *(ulong*)(time + 8) / 1000000 * 1000000;
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
        var m = admission.Memory->Memory;
        ulong source = admission.ContinuingRandom ? (ulong)admission.Generator : (ulong)admission.Entropy;
        ulong bytes = admission.ContinuingRandom ? (ulong)sizeof(HmacSha256Drbg.State) : admission.CapturedEntropyBytes;
        return !Overlap(source, bytes, (ulong)admission.Memory, (ulong)sizeof(LinuxMemoryServices.State)) &&
            !Overlap(source, bytes, (ulong)m.Pool, m.PoolPages * 4096UL) &&
            !Overlap(source, bytes, (ulong)m.Tables, m.TablePages * 4096UL) &&
            !Overlap(source, bytes, (ulong)m.CandidateTables, m.TablePages * 4096UL) &&
            !Overlap(source, bytes, (ulong)m.StaticRanges, m.StaticCount * (ulong)sizeof(OS.Kernel.Memory.X64UserMappingRange));
    }
    private static bool ValidEntropyBytes(uint bytes) => bytes == EntropyBytes || bytes == RuntimeEntropyBytes || bytes == WebEntropyBytes;

    private static bool AdmitEntropy(State* state, Admission a)
    {
        ulong source, bytes;
        if (a.ContinuingRandom)
        {
            source = (ulong)a.Generator; bytes = (ulong)sizeof(HmacSha256Drbg.State);
            if (!a.User.UserInterrupts || a.Entropy != null || a.CapturedEntropyBytes != 0 || source == 0 || (source & 7) != 0) return false;
        }
        else
        {
            source = (ulong)a.Entropy; bytes = a.CapturedEntropyBytes;
            if (a.Generator != null || source == 0 || !ValidEntropyBytes(a.CapturedEntropyBytes)) return false;
        }
        return source <= ulong.MaxValue - bytes &&
            !Overlap(source, bytes, (ulong)state, (ulong)sizeof(State)) && EntropyStorage(a) &&
            (!a.ContinuingRandom || HmacSha256Drbg.IsInitialized(a.Generator));
    }

    private static bool EntropyState(State* state) => state->Owner.ContinuingRandom ?
        state->Owner.Entropy == null && state->Owner.CapturedEntropyBytes == 0 && state->UsedEntropy == 0 &&
        state->EntropyDiscarded <= 1 && (state->EntropyDiscarded == 1 || HmacSha256Drbg.IsInitialized(state->Owner.Generator)) :
        state->Owner.Generator == null && ValidEntropyBytes(state->Owner.CapturedEntropyBytes) &&
        state->UsedEntropy <= state->Owner.CapturedEntropyBytes;

    /// <summary>Burn the unconsumed reservoir or erase the borrowed generator
    /// after this serialized owner and all borrowers stop.
    /// Borrowed storage must still be mapped/exclusive. This never resets the
    /// lifetime or permits refill; later random requests report exhaustion.
    /// Invalid state returns false without reading the entropy extent.</summary>
    public static bool DiscardEntropy(State* state)
    {
        if (state == null || ((ulong)state & 7) != 0 || state->Initialized != 1 ||
            !EntropyState(state)) return false;
        if (state->Owner.ContinuingRandom)
        {
            if (state->EntropyDiscarded == 1) return true;
            if (!HmacSha256Drbg.Erase(state->Owner.Generator)) return false;
            state->EntropyDiscarded = 1; return true;
        }
        if (state->Owner.Entropy == null) return false;
        for (uint i = 0; i < state->Owner.CapturedEntropyBytes; i++) state->Owner.Entropy[i] = 0;
        state->UsedEntropy = state->Owner.CapturedEntropyBytes;
        return true;
    }
    private static bool Overlap(ulong a, ulong aBytes, ulong b, ulong bBytes) => a < b + bBytes && b < a + aBytes;
}
