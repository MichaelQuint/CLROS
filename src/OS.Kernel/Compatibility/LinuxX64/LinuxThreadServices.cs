using OS.Kernel.Threading;
using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>Bounded single-process Linux identities, saved user images, masks
/// and clone publication over one exclusive KernelThreadTable. One stable
/// IRQ-off BSP owner retains all metadata, prepared kernel entries and user
/// mappings. No scheduler, native transfer, allocation or user stack/TLS freeing
/// is supplied here. Restore the full CPL3 envelope before committing kernel
/// exit/reap. Signal delivery and process clone/fork are unsupported. Futex and
/// blocking-pipe dispatch are not yet connected; unsupported calls return ENOSYS.
/// </summary>
public unsafe struct LinuxThreadServices
{
    public const uint MaximumThreads = 16;
    // Exact musl v1.2.6 pthread_create subset, including historical DETACHED.
    public const ulong PthreadCloneFlags = 0x7d0f00;
    private const long Invalid = -22, Fault = -14, Again = -11, Io = -5, Unsupported = -38;

    /// <summary>Prepared kernel entry for the same slot/generation returned by
    /// InspectNextCreateIdentity. Its argument is the corresponding Slot address.
    /// The native owner admits emitted entry bytes and all code/context/guarded
    /// stack lifetimes. After reap it must prepare the next generation before
    /// clone can use that slot again. Do not change a bound entry.</summary>
    public struct Worker
    {
        public ThreadContextSwitch.Descriptor Initial;
        public ulong Entry;
    }
    /// <summary>Exclusive kernel metadata, never user-mapped or copied live.
    /// Image is a borrowed saved user value; the process memory owner, not this
    /// slot, owns the application's shared address space, stack and TLS backing.</summary>
    public struct Slot
    {
        internal OSHostHandle Kernel;
        internal uint Tid, ExitCode, Exiting;
        internal ulong ClearTid;
        internal LinuxSignalMask Mask;
        internal X64LinuxUserTransition.UserImage Image;
    }
    /// <summary>Actual native owner capabilities. All ranges are mapped,
    /// exclusive, supervisor-only and disjoint from code and other kernel
    /// resources; declared sizes grant no mapping authority. User copies must
    /// complete their preflighted extent, without changing scheduler/mappings.
    /// A failed TID copy after publication poisons execution for owned shutdown.
    /// </summary>
    public struct Admission
    {
        public KernelThreadTable* Threads;
        public ulong ThreadKey;
        public Slot* Slots;
        public Worker* Workers;
        public uint Capacity, SlotBytes, WorkerBytes, ProcessId, ObservedMxcsrMask;
        public LinuxSyscallAccess User;
        public delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyToUser;
        public bool FirmwareExited, SingleOwnerValidated;
    }
    /// <summary>Non-owning current generation snapshot. A default view is not a
    /// live thread. Saved user data may change at the next syscall; no pin or
    /// native execution authority is acquired by reading it.</summary>
    public readonly struct View
    {
        public readonly OSHostHandle Kernel;
        public readonly uint Tid, ExitCode;
        public readonly bool Exiting;
        public readonly ulong ClearTid, BlockedSignals;
        public readonly LinuxSyscallFrame Frame;
        internal View(Slot s)
        { Kernel = s.Kernel; Tid = s.Tid; ExitCode = s.ExitCode; Exiting = s.Exiting != 0; ClearTid = s.ClearTid; BlockedSignals = s.Mask.Blocked; Frame = s.Image.Frame; }
    }

    private Admission _owner;
    private ulong _self, _key;
    private uint _nextTid, _active, _initialized;
    private bool _failed, _stopping;
    public readonly uint ActiveThreads => _initialized == 1 ? _active : 0;
    public readonly bool Failed => _failed;

    /// <summary>Admit a fresh process owner and an empty, separately initialized
    /// kernel table. The caller proves table exclusivity and fresh owner keys;
    /// never reuse a key for another lifetime. All checks precede slot writes.</summary>
    public bool Initialize(ulong key, ulong flags, Admission a)
    {
        if (_initialized != 0 || key == 0 || !IfClear(flags) || !a.FirmwareExited || !a.SingleOwnerValidated ||
            a.ProcessId == 0 || a.ProcessId > int.MaxValue || a.ThreadKey == 0 || a.Threads == null ||
            a.Capacity == 0 || a.Capacity > MaximumThreads || a.User.Context == null || a.User.PageAccess == null ||
            a.User.CopyFromUser == null || a.CopyToUser == null ||
            !Buffer((ulong)a.Threads, (uint)sizeof(KernelThreadTable), (uint)sizeof(KernelThreadTable)) ||
            !Buffer((ulong)a.Slots, a.SlotBytes, a.Capacity * (uint)sizeof(Slot)) ||
            !Buffer((ulong)a.Workers, a.WorkerBytes, a.Capacity * (uint)sizeof(Worker))) return false;
        ulong self; fixed (LinuxThreadServices* p = &this) self = (ulong)p;
        if (!Buffer(self, (uint)sizeof(LinuxThreadServices), (uint)sizeof(LinuxThreadServices))) return false;
        ulong* starts = stackalloc ulong[4] { self, (ulong)a.Threads, (ulong)a.Slots, (ulong)a.Workers };
        ulong* sizes = stackalloc ulong[4] { (uint)sizeof(LinuxThreadServices), (uint)sizeof(KernelThreadTable), a.SlotBytes, a.WorkerBytes };
        for (uint i = 0; i < 4; i++) for (uint j = 0; j < i; j++) if (Overlap(starts[i], sizes[i], starts[j], sizes[j])) return false;
        if (a.Threads->Current.IsValid || a.Threads->HandoffPending || a.Threads->ReadyCount != 0 || a.Threads->DeadlineCount != 0 ||
            a.Threads->Inspect(a.ThreadKey, flags, default).Code != KernelThreadTable.Status.InvalidIdentity ||
            a.Threads->InspectNextCreateIdentity(a.ThreadKey, flags, out var next) != KernelThreadTable.Status.Ok || (uint)next.Value != 1) return false;
        uint effective = a.ObservedMxcsrMask == 0 ? 0xffbfU : a.ObservedMxcsrMask;
        if ((effective & ~0xffffU) != 0 || (0x1f80U & ~effective) != 0) return false;
        for (uint i = 0; i < a.Capacity; i++)
        {
            var d = a.Workers[i].Initial;
            if (d.Context == null || d.ContextBytes < X64ThreadContext.Bytes || d.StackBytes < 3 * ThreadContextSwitch.Page ||
                !Range((ulong)d.Context, d.ContextBytes) || !Range(d.StackGuardStart, d.StackBytes)) return false;
            for (uint j = 0; j < 4; j++)
                if (Overlap(starts[j], sizes[j], (ulong)d.Context, d.ContextBytes) || Overlap(starts[j], sizes[j], d.StackGuardStart, d.StackBytes)) return false;
            // Metadata must be fresh; do not erase a foreign/stale lifetime.
            byte* bytes = (byte*)(a.Slots + i); for (uint j = 0; j < sizeof(Slot); j++) if (bytes[j] != 0) return false;
        }
        _owner = a; _self = self; _key = key; _nextTid = a.ProcessId; _active = 0; _failed = _stopping = false; _initialized = 1;
        return true;
    }

    /// <summary>Publish the initial process thread only after its complete user
    /// image and kernel entry are admitted. Failure acquires no kernel binding.
    /// Source image stays caller-owned and must not alias this owner's metadata.</summary>
    public long Start(ulong key, ulong flags, X64LinuxUserTransition.UserImage* image, uint imageBytes,
        LinuxSignalMask inherited, out OSHostHandle kernel)
    {
        kernel = default;
        if (!Access(key, flags) || _failed || _stopping || _active != 0 || _nextTid != _owner.ProcessId) return Invalid;
        if (!X64LinuxUserTransition.ValidateUserImage(image, imageBytes, _owner.ObservedMxcsrMask, _owner.User)) return Invalid;
        if (BorrowOverlap((ulong)image, X64LinuxUserTransition.UserImageBytes)) return Invalid;
        return Publish(flags, *image, inherited, 0, out kernel);
    }

    /// <summary>Borrow the current scheduled worker's user image for actual
    /// gateway capture/install. Authenticate the real table identity each time;
    /// the pointer expires at reap and grants no ownership or execution proof.</summary>
    public bool CurrentImage(ulong key, ulong flags, out X64LinuxUserTransition.UserImage* image)
    {
        image = null; if (!Access(key, flags) || !Current(flags, out uint index)) return false;
        image = &_owner.Slots[index].Image; return true;
    }
    /// <summary>Read only a live actual kernel generation belonging to this
    /// process. Missing/stale table bindings and copied owners fail without a pin.</summary>
    public bool Inspect(ulong key, ulong flags, OSHostHandle kernel, out View view)
    {
        view = default;
        if (!Access(key, flags) || !Lookup(flags, kernel, out uint index)) return false;
        view = new(_owner.Slots[index]); return true;
    }

    /// <summary>Execute only the explicit thread ABI slice for the actual running
    /// worker. The native owner has freshly captured its complete user image.
    /// Successful ARCH_SET_FS updates the real ref frame. Exits are staged;
    /// CommitExit runs after full native restoration with a captured kernel CALL.
    /// Unsupported futex/pipe/signal-delivery operations cannot report success.</summary>
    public LinuxSyscallResult Dispatch(ulong key, ulong flags, ref LinuxSyscallFrame frame)
    {
        if (!Access(key, flags) || _failed || _stopping || !Current(flags, out uint index)) return new(LinuxSyscallAction.Rejected);
        Slot* slot = _owner.Slots + index;
        if (slot->Exiting != 0 || !LinuxSyscallGateway.ValidateReturn(frame, _owner.User)) return new(LinuxSyscallAction.Rejected);
        long result;
        switch (frame.Number)
        {
            case 14: result = SignalMask(slot, frame); break;
            case 39: result = _owner.ProcessId; break;
            case 56: result = Clone(flags, slot, frame); break;
            case 60:
                slot->Exiting = 1; slot->ExitCode = unchecked((uint)frame.Argument0);
                return new(LinuxSyscallAction.ThreadExited, exitCode: slot->ExitCode);
            case 158: result = ArchPrctl(slot, ref frame); break;
            case 186: result = slot->Tid; break;
            case 218:
                // Linux records this raw pointer without accessing it. Only
                // the best-effort clear at exit checks the then-current mapping.
                slot->ClearTid = frame.Argument0; result = slot->Tid; break;
            case 231:
                if (!Shutdown(key, flags, unchecked((uint)frame.Argument0))) return new(LinuxSyscallAction.Rejected);
                return new(LinuxSyscallAction.Terminate, exitCode: unchecked((uint)frame.Argument0));
            default: result = Unsupported; break;
        }
        return _failed ? new(LinuxSyscallAction.Rejected) : new(LinuxSyscallAction.Return, result);
    }

    private long Clone(ulong flags, Slot* parent, LinuxSyscallFrame frame)
    {
        if (frame.Argument0 != PthreadCloneFlags || frame.Argument1 == 0 || (frame.Argument1 & 7) != 0 || frame.Argument4 == 0) return Invalid;
        if (!TidAddress(frame.Argument2) || frame.Argument2 == 0 || !TidAddress(frame.Argument3) || frame.Argument3 == 0) return Fault;
        var image = parent->Image; image.Frame = frame; image.Frame.Rsp = frame.Argument1; image.Frame.FsBase = frame.Argument4; image.Value = 0;
        if (!X64LinuxUserTransition.ValidateUserImage(&image, X64LinuxUserTransition.UserImageBytes, _owner.ObservedMxcsrMask, _owner.User)) return Fault;
        long tid = Publish(flags, image, parent->Mask, frame.Argument3, out _);
        if (tid < 0) return tid;
        uint word = (uint)tid;
        if (_owner.CopyToUser(_owner.User.Context, frame.Argument2, (byte*)&word, 4) != 1)
        { _failed = true; return Io; } // Child is retained for owned shutdown, never exposed to user resumption.
        return tid;
    }
    private long Publish(ulong flags, X64LinuxUserTransition.UserImage image, LinuxSignalMask mask, ulong clearTid, out OSHostHandle kernel)
    {
        kernel = default;
        if (_nextTid == 0 || _nextTid > int.MaxValue || _owner.Threads->InspectNextCreateIdentity(_owner.ThreadKey, flags, out var next) != KernelThreadTable.Status.Ok) return Again;
        uint index = (uint)next.Value - 1;
        if (index >= _owner.Capacity || _owner.Slots[index].Tid != 0) return Again;
        Worker worker = _owner.Workers[index];
        if (worker.Initial.Identity != next.Value || worker.Initial.CurrentIdentity != next.Value || worker.Initial.Kind != ThreadContextSwitch.ResumeKind.Entry) return Invalid;
        Slot candidate = new() { Kernel = next, Tid = _nextTid, Image = image, Mask = new(mask.Blocked), ClearTid = clearTid };
        _owner.Slots[index] = candidate;
        var created = _owner.Threads->Create(_owner.ThreadKey, flags, worker.Initial.Context, worker.Initial.ContextBytes,
            worker.Initial.StackGuardStart, worker.Initial.StackBytes, worker.Initial.ResumeRip, worker.Entry, (ulong)(_owner.Slots + index));
        if (!created.Succeeded) { _owner.Slots[index] = default; return Again; }
        _active++; _nextTid = _nextTid == int.MaxValue ? 0 : _nextTid + 1; kernel = created.Identity;
        if (created.Identity.Value != next.Value) { _failed = true; return Io; }
        return candidate.Tid;
    }
    private long SignalMask(Slot* slot, LinuxSyscallFrame frame)
    {
        ulong requested = 0;
        if (frame.Argument3 != LinuxSignalMask.SetBytes) return Invalid;
        if (frame.Argument1 != 0 && !Input(frame.Argument1, (byte*)&requested, 8)) return Fault;
        var change = slot->Mask.Evaluate(unchecked((uint)frame.Argument0), frame.Argument1 != 0, requested, frame.Argument3);
        if (!change.Succeeded) return change.Error;
        // Linux commits a valid set before attempting oldset copyout. An
        // oldset EFAULT therefore retains the newly installed mask.
        slot->Mask = change.After;
        ulong prior = change.Before.Blocked;
        if (frame.Argument2 != 0 && (!LinuxSyscallGateway.ValidateRange(frame.Argument2, 8, LinuxUserPageAccess.Write, _owner.User) ||
            _owner.CopyToUser(_owner.User.Context, frame.Argument2, (byte*)&prior, 8) != 1)) return Fault;
        return 0;
    }
    private long ArchPrctl(Slot* slot, ref LinuxSyscallFrame frame)
    {
        uint code = unchecked((uint)frame.Argument0); // Linux's int option ignores the upper argument bits.
        if (code == 0x1002)
        {
            if (frame.Argument1 != 0 && !LinuxSyscallGateway.ValidateRange(frame.Argument1, 1, LinuxUserPageAccess.Read, _owner.User)) return Fault;
            frame.FsBase = slot->Image.Frame.FsBase = frame.Argument1; return 0;
        }
        if (code is not (0x1003 or 0x1004)) return Invalid; // SET_GS is not admitted by this subset.
        if (!LinuxSyscallGateway.ValidateRange(frame.Argument1, 8, LinuxUserPageAccess.Write, _owner.User)) return Fault;
        ulong value = code == 0x1003 ? frame.FsBase : frame.GsBase;
        return _owner.CopyToUser(_owner.User.Context, frame.Argument1, (byte*)&value, 8) == 1 ? 0 : Fault;
    }

    /// <summary>Stop user resumption and request cancellation for every active
    /// kernel worker, including parked ones. Their real native paths must drain
    /// to CommitExit and supervisor-only Reap; this does not free any backing.</summary>
    public bool Shutdown(ulong key, ulong flags, uint exitCode)
    {
        if (!Access(key, flags)) return false;
        _stopping = true;
        for (uint i = 0; i < _owner.Capacity; i++)
        {
            Slot* s = _owner.Slots + i; if (s->Tid == 0) continue;
            var current = _owner.Threads->Inspect(_owner.ThreadKey, flags, s->Kernel);
            if (!current.Succeeded) { _failed = true; return false; }
            if (current.State == KernelThreadTable.State.Exited) continue;
            s->Exiting = 1; s->ExitCode = exitCode;
            if (!_owner.Threads->Cancel(_owner.ThreadKey, flags, s->Kernel).Succeeded) { _failed = true; return false; }
        }
        return true;
    }
    /// <summary>Commit a staged exit only on the actual current kernel worker
    /// after the CPL3 envelope restores. Actual kernel capture admission precedes
    /// user mutation. A failed user TID clear reports EFAULT separately and does
    /// not resurrect an exited thread, matching Linux's best-effort clear. No
    /// futex registration can exist while futex dispatch remains unsupported.
    /// The native caller must finish the real handoff and ConfirmSupervisor.</summary>
    public bool CommitExit(ulong key, ulong flags, ThreadContextSwitch.Descriptor captured, out long clearTidResult)
    {
        clearTidResult = Invalid;
        if (!Access(key, flags) || !Current(flags, out uint index)) return false;
        Slot* s = _owner.Slots + index;
        if (s->Exiting != 1 || captured.Identity != s->Kernel.Value) return false;
        if (!_owner.Threads->Exit(_owner.ThreadKey, flags, s->Kernel, captured, unchecked((int)s->ExitCode)).Succeeded) return false;
        clearTidResult = 0;
        if (s->ClearTid != 0)
        {
            uint zero = 0;
            if (!LinuxSyscallGateway.ValidateRange(s->ClearTid, 4, LinuxUserPageAccess.Write, _owner.User) ||
                _owner.CopyToUser(_owner.User.Context, s->ClearTid, (byte*)&zero, 4) != 1)
                clearTidResult = Fault;
        }
        s->ClearTid = 0; return true;
    }
    /// <summary>Scrub only this slot's saved user metadata after actual
    /// supervisor-only kernel reap succeeds. The caller revokes native/IRQ/image
    /// references first and owns all app mappings. A failed reap changes nothing;
    /// user TLS/stack mappings are not erased or freed by a Linux thread exit.</summary>
    public bool Reap(ulong key, ulong flags, OSHostHandle kernel, ulong supervisorRsp, out ThreadContextSwitch.Descriptor released)
    {
        released = default;
        if (!Access(key, flags) || !Lookup(flags, kernel, out uint index)) return false;
        if (_owner.Slots[index].Exiting != 1 || _owner.Slots[index].ClearTid != 0) return false;
        var result = _owner.Threads->Reap(_owner.ThreadKey, flags, kernel, supervisorRsp);
        if (!result.Succeeded) return false;
        _owner.Slots[index] = default; _active--; released = result.Context; return true;
    }
    /// <summary>Release this metadata borrow only after all actual kernel workers
    /// were reaped and every native reference revoked by the owner. Table and
    /// application backing remain separately owned. Keys must not be reused.</summary>
    public bool Release(ulong key, ulong flags)
    {
        if (!Access(key, flags) || _active != 0 || _owner.Threads->Current.IsValid || _owner.Threads->HandoffPending) return false;
        this = default; return true;
    }
    private bool Current(ulong flags, out uint index)
    {
        var kernel = _owner.Threads->Current;
        return Lookup(flags, kernel, out index) && _owner.Threads->Inspect(_owner.ThreadKey, flags, kernel).State == KernelThreadTable.State.Running;
    }
    private bool Lookup(ulong flags, OSHostHandle kernel, out uint index)
    {
        index = (uint)kernel.Value - 1;
        return kernel.IsValid && index < _owner.Capacity && _owner.Slots[index].Tid != 0 && _owner.Slots[index].Kernel.Value == kernel.Value &&
            _owner.Threads->Inspect(_owner.ThreadKey, flags, kernel).Succeeded;
    }
    private bool Access(ulong key, ulong flags)
    {
        if (_initialized != 1 || key == 0 || key != _key || !IfClear(flags)) return false;
        fixed (LinuxThreadServices* p = &this) return (ulong)p == _self &&
            _owner.Threads->Inspect(_owner.ThreadKey, flags, default).Code == KernelThreadTable.Status.InvalidIdentity;
    }
    private bool Input(ulong address, byte* output, uint bytes) => LinuxSyscallGateway.ValidateRange(address, bytes, LinuxUserPageAccess.Read, _owner.User) &&
        _owner.User.CopyFromUser(_owner.User.Context, address, output, bytes) == 1;
    private bool TidAddress(ulong address) => address == 0 || (address & 3) == 0 && LinuxSyscallGateway.ValidateRange(address, 4, LinuxUserPageAccess.Write, _owner.User);
    private bool BorrowOverlap(ulong start, ulong bytes) => Overlap(start, bytes, _self, (uint)sizeof(LinuxThreadServices)) ||
        Overlap(start, bytes, (ulong)_owner.Slots, _owner.SlotBytes) || Overlap(start, bytes, (ulong)_owner.Workers, _owner.WorkerBytes) ||
        Overlap(start, bytes, (ulong)_owner.Threads, (uint)sizeof(KernelThreadTable));
    private static bool IfClear(ulong flags) => (flags & 0x202) == 2;
    private static bool Buffer(ulong start, uint bytes, uint needed) => bytes >= needed && (start & 7) == 0 && Range(start, bytes);
    private static bool Range(ulong start, ulong bytes) => start != 0 && bytes != 0 && bytes <= 0x0000800000000000UL && start <= 0x0000800000000000UL - bytes;
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => a < b + bn && b < a + an;
}
