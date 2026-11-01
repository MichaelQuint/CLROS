using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;
/// <summary>Retained POD descriptor provider over one exclusively borrowed
/// continuing FAT owner. One serialized supervisor keeps every extent stable
/// and disjoint until Detach succeeds, BEFORE stopping/freeing the engine or
/// device backing. No whole-image snapshot, ref struct, user pointer, scheduler
/// or syscall gateway is retained. Root files and empty creation are confined
/// to /data. Growth requires explicit copied candidate clusters; no allocator
/// or free-space receipt is trusted. Unlink requires a closed file. Nested
/// paths and POSIX open-unlink retention are not supplied. Optional
/// process locks use copied caller PIDs, not kernel process authority. The one
/// supervisor handle table is NOT a per-process Linux fd namespace.</summary>

public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public const uint MaximumDescriptors = 32, MaximumDescriptions = 32, PathBytes = 256, IoBytes = 8192;
    public const uint MaximumProcesses = 4, LockSlots = 32, LockScratchSlots = 65;
    public const uint Create = 0x40, Exclusive = 0x80, CloseOnExec = 0x80000, NoFollow = 0x20000, DirectoryOnly = 0x10000, LargeFile = 0x8000, NoControllingTerminal = 0x100, RwfNoAppend = 0x20;
    private const long BadFd = -9, Again = -11, Fault = -14, Exists = -17, Invalid = -22, TableFull = -23, FdFull = -24, NoSpace = -28, Unsupported = -95, Overflow = -75, Stale = -116, Interrupted = -4, Missing = -2, IsDirectory = -21, Access = -13, NotDirectory = -20, Io = -5, IllegalSeek = -29, NoLocks = -37, NoSys = -38, Busy = -16;
    /// <summary>Borrow an already idle mounted engine and zeroed exclusive
    /// tables/input storage. Structural engine alias checks include its DMA and
    /// MMIO extents. Failure writes nothing and issues no request.</summary>

    public static bool Start(State* s, in Admission a)
    {
        if (!a.ExclusiveOwnerValidated || a.Epoch == 0 || a.DescriptorCount is < 4 or > MaximumDescriptors || a.DescriptionCount is < 1 or > MaximumDescriptions || a.InputBytes != IoBytes || !Ordinary((ulong)s, (ulong)sizeof(State), 8) || !Ordinary((ulong)a.Engine, (ulong)sizeof(C.State), 8) || !a.Engine->IsMounted || a.Engine->Retired || a.Engine->RequestActive || a.Engine->Dirty)
            return false;
        ulong* starts = stackalloc ulong[4]
        {
            (ulong)s,
            (ulong)a.Descriptors,
            (ulong)a.Descriptions,
            (ulong)a.Input
        };
        ulong* sizes = stackalloc ulong[4]
        {
            (ulong)sizeof(State),
            a.DescriptorCount * (ulong)sizeof(Descriptor),
            a.DescriptionCount * (ulong)sizeof(Description),
            IoBytes
        };
        for (int i = 0; i < 4; i++)
        {
            if (!Ordinary(starts[i], sizes[i], i == 3 ? 1UL : 8UL) || !C.CanBorrowStorage(a.Engine, a.Epoch, starts[i], sizes[i]))
                return false;
            for (int j = 0; j < i; j++)
                if (Overlap(starts[i], sizes[i], starts[j], sizes[j]))
                    return false;
            for (ulong j = 0; j < sizes[i]; j++)
                if (((byte*)starts[i])[j] != 0)
                    return false;
        }

        s->Self = (ulong)s;
        s->Input = a;
        s->KnownRevision = a.Engine->VolumeRevision;
        s->NextAlias = 1;
        s->Active = true;
        return true;
    }

    /// <summary>Read-only structural admission for an external supervisor
    /// extent, including future wire metadata or output scratch. The same
    /// serialized owner retains all mappings during this call. Validate exact
    /// provider/mount epoch, current clean device/DMA ownership and every own
    /// extent, including optional device observation storage, before accepting
    /// a nonempty disjoint byte range. Clean pending
    /// reads may pass; dirty, retiring, faulted or stale owners cannot. This
    /// performs no callbacks, I/O, allocation or output writes and grants no
    /// user mapping, copy, operation-completion or native execution authority.
    /// Teardown deliberately keeps its separate permissive predicates.</summary>

    public static bool CanBorrowStorage(State* s, ulong expectedEpoch, ulong address, ulong bytes)
    {
        if (!CanRetainStorage(s, expectedEpoch, address, bytes) || s->Retiring || s->Poisoned || s->Input.Engine->Dirty || s->KnownRevision != s->Input.Engine->VolumeRevision)
            return false;
        return (!s->Locking || ProcessCurrent(s, s->PrimaryOwner)) && C.CanAdmitStorage(s->Input.Engine, expectedEpoch);
    }

    /// <summary>Structural audit for an already retained supervisor interval,
    /// including dirty/recovery/orphan cleanup. Exact provider, mount and
    /// original block epochs and disjoint extents must remain mapped under the
    /// same serialized owner. A fault, stopped original block, retiring provider
    /// or exited primary does not release that backing. No I/O, current-volume,
    /// live-process, user-copy or new-admission authority follows. Provider
    /// Detach must precede engine Stop and backing release: final Retired or an
    /// unmounted/replaced engine grants no retained ownership. No callbacks,
    /// allocation, output writes or mutation occur.</summary>

    internal static bool CanRetainStorage(State* s, ulong expectedEpoch, ulong address, ulong bytes) => RetainedStorage(s, expectedEpoch, address, bytes, true);
    internal const int MaximumRetainedStorageRanges = 177;
    /// <summary>Check a borrowed flat start/byte-count inventory under one
    /// serialized retained owner. Nonempty/even input is bounded to177 pairs.
    /// Audit the provider tree once, then borrow the original candidate span
    /// for one complete engine batch and all local exclusions. Zero byte
    /// counts skip only that candidate, never the owner audit. Dirty, faulted,
    /// retiring, orphaned or original-stopped backing retains the scalar
    /// cleanup semantics; final retirement rejects. No input/span/token is
    /// retained and no callback, I/O, allocation, output or mutation occurs.
    /// Return supplies no later permission, live process, copy or release.</summary>

    internal static bool CanRetainStorageRanges(State* s, ulong expectedEpoch, ReadOnlySpan<ulong> pairs)
    {
        if (pairs.Length == 0 || (pairs.Length & 1) != 0 || pairs.Length > MaximumRetainedStorageRanges * 2 || !RetainedStorage(s, expectedEpoch, 0, 0, false))
            return false;
        return C.CanRetainStorageRanges(s->Input.Engine, expectedEpoch, pairs) && RetainedRangesOutside(s, pairs);
    }

    // Local inventory scratch starts only after both complete provider and
    // engine batches returned. Actual emitted stack/fit is a separate gate.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool RetainedRangesOutside(State* s, ReadOnlySpan<ulong> pairs)
    {
        Admission a = s->Input;
        LockAdmission l = s->LockInput;
        ulong* starts = stackalloc ulong[7]
        {
            (ulong)s,
            (ulong)a.Descriptors,
            (ulong)a.Descriptions,
            (ulong)a.Input,
            (ulong)l.Processes,
            (ulong)l.Locks,
            (ulong)l.Scratch
        };
        ulong* sizes = stackalloc ulong[7]
        {
            (ulong)sizeof(State),
            a.DescriptorCount * (ulong)sizeof(Descriptor),
            a.DescriptionCount * (ulong)sizeof(Description),
            IoBytes,
            l.ProcessCount * (ulong)sizeof(ProcessSlot),
            l.LockCount * (ulong)sizeof(RangeLock),
            l.ScratchCount * (ulong)sizeof(RangeLock)
        };
        uint count = s->Locking ? 7U : 4U;
        for (int i = 0; i < pairs.Length; i += 2)
        {
            ulong address = pairs[i], bytes = pairs[i + 1];
            if (bytes == 0)
                continue;
            if (!Ordinary(address, bytes, 1))
                return false;
            for (uint j = 0; j < count; j++)
                if (Overlap(address, bytes, starts[j], sizes[j]))
                    return false;
        }

        return true;
    }

    /// <summary>Match copied engine identity against this provider's complete
    /// original retained tree under its serialized supervisor. Dirty, orphaned,
    /// retiring, faulted or original-stopped backing remains identifiable until
    /// Detach; final retirement rejects before dereferencing released engine
    /// backing. The expected owner is a value, never a pointer to dereference.
    /// No output, I/O, allocation, live process/file/device, new admission or
    /// copy authority follows. This does not authenticate a readonly partition.</summary>

    internal static bool MatchesRetainedEngine(State* s, ulong expectedEpoch, ulong engineOwner, ulong engineEpoch) => RetainedStorage(s, expectedEpoch, 0, 0, false) && (ulong)s->Input.Engine == engineOwner && s->Input.Epoch == engineEpoch;
    private static bool RetainedStorage(State* s, ulong expectedEpoch, ulong address, ulong bytes, bool external)
    {
        if (!Ordinary((ulong)s, (ulong)sizeof(State), 8) || external && !Ordinary(address, bytes, 1) || expectedEpoch == 0 || s->Self != (ulong)s || !s->Active || s->RetiredValue || s->Input.Epoch != expectedEpoch)
            return false;
        Admission a = s->Input;
        if (!a.ExclusiveOwnerValidated || a.DescriptorCount is < 4 or > MaximumDescriptors || a.DescriptionCount is < 1 or > MaximumDescriptions || a.InputBytes != IoBytes || !Ordinary((ulong)a.Engine, (ulong)sizeof(C.State), 8) || !a.Engine->IsMounted || a.Engine->Retired)
            return false;
        LockAdmission l = s->LockInput;
        if (s->Locking)
        {
            if (l.ProcessCount is < 1 or > MaximumProcesses || l.LockCount != LockSlots || l.ScratchCount != LockScratchSlots || l.PrimaryPid is 0 or > int.MaxValue || l.PrimaryPid != s->PrimaryOwner.Pid || s->PrimaryOwner.Owner != (ulong)s || s->PrimaryOwner.Epoch != expectedEpoch || s->PrimaryOwner.Generation == 0 || s->PrimaryOwner.Index >= l.ProcessCount)
                return false;
        }
        else if (l.Processes != null || l.Locks != null || l.Scratch != null || l.ProcessCount != 0 || l.LockCount != 0 || l.ScratchCount != 0 || l.PrimaryPid != 0 || !Same(s->PrimaryOwner, default))
            return false;
        Span<ulong> pairs = stackalloc ulong[16]
        {
            (ulong)s,
            (ulong)sizeof(State),
            (ulong)a.Descriptors,
            a.DescriptorCount * (ulong)sizeof(Descriptor),
            (ulong)a.Descriptions,
            a.DescriptionCount * (ulong)sizeof(Description),
            (ulong)a.Input,
            IoBytes,
            (ulong)l.Processes,
            l.ProcessCount * (ulong)sizeof(ProcessSlot),
            (ulong)l.Locks,
            l.LockCount * (ulong)sizeof(RangeLock),
            (ulong)l.Scratch,
            l.ScratchCount * (ulong)sizeof(RangeLock),
            0,
            0
        };
        uint count = s->Locking ? 7U : 4U;
        for (uint i = 0; i < count; i++)
        {
            ulong start = pairs[(int)(i * 2)], extentBytes = pairs[(int)(i * 2 + 1)];
            if (!Ordinary(start, extentBytes, i == 3 ? 1UL : 8UL) || external && Overlap(address, bytes, start, extentBytes))
                return false;
            for (uint j = 0; j < i; j++)
                if (Overlap(start, extentBytes, pairs[(int)(j * 2)], pairs[(int)(j * 2 + 1)]))
                    return false;
        }

        if (external)
        {
            pairs[(int)(count * 2)] = address;
            pairs[(int)(count * 2 + 1)] = bytes;
            count++;
        }

        return C.CanRetainStorageRanges(a.Engine, expectedEpoch, pairs.Slice(0, (int)(count * 2)));
    }

    /// <summary>Enable fixed process-associated lock tables while no handle or
    /// request is active. Existing APIs subsequently use this exact primary
    /// generation. Caller PIDs are copied values only. No table is allocated.</summary>

    public static bool EnableLocks(State* s, in LockAdmission input)
    {
        LockAdmission a = input;
        if (!Bound(s) || s->Locking || s->Retiring || s->Work.Active || !Revision(s) || a.ProcessCount is < 1 or > MaximumProcesses || a.LockCount != LockSlots || a.ScratchCount != LockScratchSlots || a.PrimaryPid is 0 or > int.MaxValue)
            return false;
        for (uint i = 3; i < s->Input.DescriptorCount; i++)
            if (s->Input.Descriptors[i].Description != 0)
                return false;
        ulong* starts = stackalloc ulong[3]
        {
            (ulong)a.Processes,
            (ulong)a.Locks,
            (ulong)a.Scratch
        };
        ulong* sizes = stackalloc ulong[3]
        {
            a.ProcessCount * (ulong)sizeof(ProcessSlot),
            LockSlots * (ulong)sizeof(RangeLock),
            LockScratchSlots * (ulong)sizeof(RangeLock)
        };
        for (int i = 0; i < 3; i++)
        {
            if (!Ordinary(starts[i], sizes[i], 8) || !External(s, new ReadOnlySpan<byte>((void*)starts[i], (int)sizes[i])))
                return false;
            for (int j = 0; j < i; j++)
                if (Overlap(starts[i], sizes[i], starts[j], sizes[j]))
                    return false;
            for (ulong j = 0; j < sizes[i]; j++)
                if (((byte*)starts[i])[j] != 0)
                    return false;
        }

        s->LockInput = a;
        s->Locking = true;
        a.Processes[0] = new()
        {
            Active = true,
            Pid = a.PrimaryPid,
            Generation = 1
        };
        s->PrimaryOwner = new(s, 0);
        return true;
    }

    public static ProcessToken PrimaryProcess(State* s) => Bound(s) && s->Locking && ProcessCurrent(s, s->PrimaryOwner) ? s->PrimaryOwner : default;
    /// <summary>Register another copied host owner in the same supervisor
    /// table. This creates neither a Linux process nor its fd namespace.</summary>

    public static ProcessResult RegisterProcess(State* s, uint pid)
    {
        if (!Bound(s) || !s->Locking)
            return new(NoSys);
        if (s->Retiring || pid is 0 or > int.MaxValue)
            return new(Invalid);
        int free = -1;
        for (uint i = 0; i < s->LockInput.ProcessCount; i++)
        {
            ProcessSlot* p = s->LockInput.Processes + i;
            if (p->Active && p->Pid == pid)
                return new(Exists);
            if (free < 0 && !p->Active && p->Generation != ulong.MaxValue)
                free = (int)i;
        }

        if (free < 0)
            return new(TableFull);
        ProcessSlot* slot = s->LockInput.Processes + free;
        ulong generation = slot->Generation;
        *slot = new()
        {
            Active = true,
            Pid = pid,
            Generation = generation + 1
        };
        return new(0, new(s, (uint)free));
    }

    /// <summary>Revoke one registered owner before closing its handles and
    /// releasing its locks. Pending original requests retain their description
    /// and engine drain; PID/slot reuse cannot resurrect the old generation.</summary>

    public static Result ExitProcess(State* s, ProcessToken process)
    {
        if (!Bound(s) || !s->Locking || !ProcessCurrent(s, process))
            return Immediate(BadFd);
        bool ownedPending = s->Work.Active && Same(s->Work.Process, process);
        s->LockInput.Processes[process.Index].Exiting = true;
        ReleaseProcessLocks(s, process);
        for (uint fd = 3; fd < s->Input.DescriptorCount; fd++)
            if (Find(s, fd, out uint description) && Same(s->Input.Descriptions[description].Process, process))
                CloseCore(s, process, new(s, fd), true);
        if (s->Work.Active && Same(s->Work.Process, process))
        {
            s->Work.Discard = true;
            Cancel(s, new(s));
        }

        s->LockInput.Processes[process.Index].Active = false;
        if (ownedPending && s->Input.Engine->Dirty)
            return Recovery(s);
        return s->Work.Active && Same(s->Work.Process, process) ? Observe(s) : Immediate(0);
    }

    /// <summary>Finite POSIX process locks: GETLK5/SETLK6, SEEK_SET and
    /// nonnegative ranges only; zero length means through signed off_t EOF.
    /// Read/write access checks apply to SETLK, not GETLK. Blocking/OFD modes
    /// reject. No-conflict GETLK changes only Type; it grants no future lock.</summary>

    public static LockResult FcntlLock(State* s, DescriptorToken file, uint command, LockRequest request) => FcntlLockForProcess(s, PrimaryProcess(s), file, command, request);
    public static LockResult FcntlLockForProcess(State* s, ProcessToken process, DescriptorToken file, uint command, LockRequest request)
    {
        if (!Current(s, file, out uint description) || !Owned(s, process, description))
            return new(BadFd);
        if (!s->Locking)
            return new(NoSys);
        if (s->Retiring || !Revision(s))
            return new(Io);
        if (command is not (5 or 6))
            return new(NoSys);
        if (request.Whence != 0 || request.Start < 0 || request.Length < 0 || request.Type is < 0 or > 2 || command == 5 && request.Type == 2)
            return new(Invalid);
        if (request.Length != 0 && request.Length - 1 > long.MaxValue - request.Start)
            return new(Overflow);
        Description* d = s->Input.Descriptions + description;
        if (d->Directory)
            return new(IsDirectory);
        if (command == 6 && (request.Type == 0 && (d->Flags & 3) == 1 || request.Type == 1 && (d->Flags & 3) == 0))
            return new(BadFd);
        long first = request.Start, last = request.Length == 0 ? long.MaxValue : first + request.Length - 1;
        if (request.Type != 2)
            for (uint i = 0; i < LockSlots; i++)
            {
                RangeLock row = s->LockInput.Locks[i];
                if (!row.Active || Same(row.Process, process) || !Physical(row.File, d->File) || row.Last < first || row.First > last || row.Type == 0 && request.Type == 0)
                    continue;
                if (command == 6)
                    return new(Again);
                return new(0, new(row.Type, 0, row.First, row.Last == long.MaxValue ? 0 : row.Last - row.First + 1, (int)row.Process.Pid), row.Process);
            }

        if (command == 5)
            return new(0, new(2, request.Whence, request.Start, request.Length, request.Pid));
        uint count = 0;
        for (uint i = 0; i < LockSlots; i++)
        {
            RangeLock row = s->LockInput.Locks[i];
            if (!row.Active)
                continue;
            if (!Same(row.Process, process) || !Physical(row.File, d->File) || row.Last < first || row.First > last)
                s->LockInput.Scratch[count++] = row;
            else
            {
                if (row.First < first)
                {
                    RangeLock left = row;
                    left.Last = first - 1;
                    s->LockInput.Scratch[count++] = left;
                }

                if (row.Last > last)
                {
                    RangeLock right = row;
                    right.First = last + 1;
                    s->LockInput.Scratch[count++] = right;
                }
            }
        }

        if (request.Type != 2)
            s->LockInput.Scratch[count++] = new()
            {
                Active = true,
                File = d->File,
                Process = process,
                First = first,
                Last = last,
                Type = request.Type
            };
        // At most two fragments per32 existing rows plus one new range.
        for (uint i = 0; i < count; i++)
            for (uint j = i + 1; j < count;)
            {
                RangeLock a = s->LockInput.Scratch[i], b = s->LockInput.Scratch[j];
                bool near = a.First <= (b.Last == long.MaxValue ? b.Last : b.Last + 1) && b.First <= (a.Last == long.MaxValue ? a.Last : a.Last + 1);
                if (a.Type != b.Type || !Same(a.Process, b.Process) || !Physical(a.File, b.File) || !near)
                {
                    j++;
                    continue;
                }

                a.First = a.First < b.First ? a.First : b.First;
                a.Last = a.Last > b.Last ? a.Last : b.Last;
                s->LockInput.Scratch[i] = a;
                for (uint k = j + 1; k < count; k++)
                    s->LockInput.Scratch[k - 1] = s->LockInput.Scratch[k];
                count--;
                // A merge may now meet an earlier interval; restart this row.
                j = i + 1;
            }

        if (count > LockSlots)
        {
            ScrubLocksScratch(s);
            return new(NoLocks);
        }

        for (uint i = 0; i < LockSlots; i++)
            s->LockInput.Locks[i] = i < count ? s->LockInput.Scratch[i] : default;
        ScrubLocksScratch(s);
        return new(0, request);
    }

    /// <summary>Nonblocking BSD whole-file locks on this confined regular-file
    /// namespace: SH|NB5, EX|NB6 and UN8 (also UN|NB12). Blocking modes return
    /// ENOSYS; other flags return EINVAL. A serialized supervisor must retain
    /// all admitted storage. No I/O, allocation, cursor change or user copy
    /// occurs; dirty, retired or replaced storage grants no lock operation.
    /// Locks belong to open descriptions: dup shares a lock, independent opens
    /// conflict even in one process, and final reference/borrow retirement
    /// releases it. POSIX process byte locks remain separate. Conversion first
    /// drops the old mode, so EAGAIN on conversion does not restore that mode.</summary>

    public static long Flock(State* s, DescriptorToken file, uint operation) => FlockForProcess(s, PrimaryProcess(s), file, operation);
    /// <summary>Process-scoped form of Flock. The exact current process and fd
    /// generations authenticate access, but the lock belongs to the shared
    /// open description rather than to the PID. Failed admission or invalid
    /// flags preserve the existing mode; a conflicting conversion drops it.</summary>

    public static long FlockForProcess(State* s, ProcessToken process, DescriptorToken file, uint operation)
    {
        if (!Ordinary((ulong)s, (ulong)sizeof(State), 8) || s->Self != (ulong)s || !s->Active)
            return BadFd;
        if (!RetainedStorage(s, s->Input.Epoch, 0, 0, false))
            return Io;
        if (!Current(s, file, out uint description) || !Owned(s, process, description))
            return BadFd;
        if (s->Retiring || !Revision(s) || s->Input.Engine->Dirty || !C.CanAdmitStorage(s->Input.Engine, s->Input.Epoch))
            return Io;
        byte mode;
        if (operation is 5 or 6)
            mode = (byte)(operation & 3);
        else if (operation is 8 or 12)
            mode = 0;
        else
            return operation is 1 or 2 ? NoSys : Invalid;
        Description* target = s->Input.Descriptions + description;
        if (target->Directory)
            return Unsupported;
        // Validate every retained mode before any conversion can change one.
        for (uint i = 0; i < s->Input.DescriptionCount; i++)
        {
            Description* row = s->Input.Descriptions + i;
            if (row->FlockMode > 2 || row->FlockMode != 0 && (!row->Active || row->Reserved || row->Directory || row->References == 0 && row->Borrowers == 0))
                return Io;
        }

        if (target->FlockMode == mode)
            return 0;
        target->FlockMode = 0;
        if (mode != 0)
            for (uint i = 0; i < s->Input.DescriptionCount; i++)
            {
                Description* row = s->Input.Descriptions + i;
                if (i != description && row->FlockMode != 0 && Physical(row->File, target->File) && (mode == 2 || row->FlockMode == 2))
                    return Again;
            }

        target->FlockMode = mode;
        return 0;
    }

    /// <summary>Copy the current fd identity. Its generation grants no pin or
    /// permission to resolve a subsequently reused descriptor number.</summary>

    public static Result Capture(State* s, uint number) => CaptureForProcess(s, PrimaryProcess(s), number);
    public static Result CaptureForProcess(State* s, ProcessToken process, uint number) => Bound(s) && Find(s, number, out uint description) && Owned(s, process, description) ? new(Progress.Immediate, number, descriptor: new(s, number)) : Immediate(BadFd);
    /// <summary>Inspect copied open metadata without new I/O. The caller must
    /// retain the original token and separately request fresh authentication
    /// for a future Linux stat wire; this is not a live disk observation.</summary>

    public static Metadata InspectCopied(State* s, DescriptorToken token) => CurrentPrimary(s, token, out uint description) && Revision(s) ? new(s->Input.Descriptions + description) : default;
    /// <summary>Open exact /data as a read-only barrier descriptor, or copy one
    /// printable-ASCII root component and begin actual metadata reads. Creation
    /// O_NOCTTY is inert for this confined regular-file/directory namespace and
    /// is not retained as a status flag. Creation retries at most64 deterministic aliases and publishes an fd only after
    /// engine success and an independent reopen. Reservations prevent another
    /// root open from consuming the pending fd/description.</summary>

    public static Result Open(State* s, ReadOnlySpan<byte> path, uint flags) => OpenForProcess(s, PrimaryProcess(s), path, flags);
    public static Result OpenForProcess(State* s, ProcessToken process, ReadOnlySpan<byte> path, uint flags)
    {
        if (!Bound(s) || s->Retiring || !ProcessCurrent(s, process))
            return Immediate(BadFd);
        if (!Revision(s))
            return Immediate(Io);
        const uint allowed = 3 | Create | Exclusive | CloseOnExec | NoFollow | DirectoryOnly | LargeFile | NoControllingTerminal;
        if ((flags & ~allowed) != 0 || (flags & 3) == 3)
            return Immediate(Unsupported);
        if (!External(s, path))
            return Immediate(Fault);
        bool root = IsRoot(path);
        if (root && (flags & (3 | Create | Exclusive)) != 0)
            return Immediate(IsDirectory);
        long policy = root ? 0 : Path(path);
        if (policy != 0)
            return Immediate(policy);
        if (!root && (flags & DirectoryOnly) != 0)
            return Immediate(NotDirectory);
        if (!root && s->Work.Active)
            return Immediate(Again);
        int fd = FreeFd(s), description = FreeDescription(s);
        if (fd < 0)
            return Immediate(FdFull);
        if (description < 0)
            return Immediate(TableFull);
        if (!root && s->NextGeneration == ulong.MaxValue)
            return Immediate(Overflow);
        Reserve(s, (uint)fd, (uint)description, flags, process);
        Description* d = s->Input.Descriptions + description;
        if (root)
        {
            d->Directory = true;
            d->Reserved = false;
            d->References = 1;
            s->Input.Descriptors[fd].Reserved = false;
            return new(Progress.Immediate, fd, descriptor: new(s, (uint)fd));
        }

        s->Work = new()
        {
            Active = true,
            Generation = ++s->NextGeneration,
            Kind = Operation.Open,
            Step = Step.Lookup,
            Description = (uint)description,
            DescriptionGeneration = d->Generation,
            Number = (uint)fd,
            Flags = flags,
            Process = process,
            NameLength = (ushort)(path.Length - 6)
        };
        for (int i = 6; i < path.Length; i++)
            s->Work.Name[i - 6] = path[i];
        return StartedFirstEngine(s, Lookup(s));
    }

    /// <summary>Share a live description and cursor. The new descriptor has
    /// its own nonwrapping generation and close-on-exec flag.</summary>

    public static Result Dup(State* s, DescriptorToken token) => DupForProcess(s, PrimaryProcess(s), token);
    public static Result DupForProcess(State* s, ProcessToken process, DescriptorToken token)
    {
        if (!Current(s, token, out uint description) || !Owned(s, process, description) || s->Retiring)
            return Immediate(BadFd);
        if (!Revision(s))
            return Immediate(Io);
        int fd = FreeFd(s);
        if (fd < 0)
            return Immediate(FdFull);
        Description* d = s->Input.Descriptions + description;
        if (d->References == uint.MaxValue)
            return Immediate(Overflow);
        Descriptor* f = s->Input.Descriptors + fd;
        f->Generation++;
        f->Description = description + 1;
        f->Flags = 0;
        f->Reserved = false;
        d->References++;
        return new(Progress.Immediate, fd, descriptor: new(s, (uint)fd));
    }

    /// <summary>Revoke the fd before cancelling a borrower. A reused number
    /// cannot receive late bytes/cursor commits. The old description remains
    /// owned through actual engine cancellation drain; dirty failures retain it.</summary>

    public static long Close(State* s, DescriptorToken token) => CloseForProcess(s, PrimaryProcess(s), token);
    public static long CloseForProcess(State* s, ProcessToken process, DescriptorToken token) => CloseCore(s, process, token, false);
    private static long CloseCore(State* s, ProcessToken process, DescriptorToken token, bool exiting)
    {
        if (!CurrentBase(s, token, out uint description) || !Owned(s, process, description, exiting))
            return BadFd;
        if (s->Locking && !s->Input.Descriptions[description].Directory)
            ReleaseFileLocks(s, process, s->Input.Descriptions[description].File);
        s->Input.Descriptors[token.Number].Description = s->Input.Descriptors[token.Number].Flags = 0;
        Description* d = s->Input.Descriptions + description;
        d->References--;
        if (s->Work.Active && s->Work.Borrowed && Same(s->Work.Original, token))
        {
            s->Work.Discard = true;
            Cancel(s, new(s));
        }

        RetireDescription(s, description);
        return 0;
    }

    /// <summary>Move one shared cursor without allocating media. A pending
    /// borrower of this description rejects cursor changes until consumed.</summary>

    public static Result Seek(State* s, DescriptorToken token, long offset, uint whence)
    {
        if (!CurrentPrimary(s, token, out uint description) || s->Retiring)
            return Immediate(BadFd);
        if (!Revision(s))
            return Immediate(Io);
        Description* d = s->Input.Descriptions + description;
        if (d->Directory)
            return Immediate(IllegalSeek);
        if (d->Borrowers != 0)
            return Immediate(Again);
        if (whence > 2)
            return Immediate(Invalid);
        ulong start = whence == 0 ? 0 : whence == 1 ? d->Position : d->File.Length;
        ulong next;
        if (offset < 0)
        {
            ulong subtract = unchecked((ulong)(-(offset + 1))) + 1;
            if (subtract > start)
                return Immediate(Invalid);
            next = start - subtract;
        }
        else
        {
            if ((ulong)offset > long.MaxValue - start)
                return Immediate(Overflow);
            next = start + (ulong)offset;
        }

        d->Position = next;
        return Immediate((long)next);
    }

    /// <summary>Copy at most8192 write bytes before return. The physical entry
    /// is reopened/authenticated before the engine receives the copied bytes.
    /// This entry rejects growth; its explicit-candidate variant supplies it.
    /// Append mode remains unsupported.</summary>

    public static Result BeginWrite(State* s, DescriptorToken token, ReadOnlySpan<byte> bytes, ulong offset = 0, bool positioned = false) => BeginWriteCore(s, token, bytes, offset, positioned, false, default);
    /// <summary>Explicit allocation variant. Copy at most16 candidate clusters
    /// before yield; the engine independently checks both FATs and exact count.
    /// Grow/zero/flush finishes before the payload write starts. Cancellation
    /// can leave a successfully grown zero tail; this is not an atomic write.</summary>

    public static Result BeginWriteWithGrowth(State* s, DescriptorToken token, ReadOnlySpan<byte> bytes, ReadOnlySpan<uint> candidates, ulong offset = 0, bool positioned = false) => BeginWriteCore(s, token, bytes, offset, positioned, true, candidates);
    /// <summary>Opt in to one explicitly bounded mirrored-FAT search before
    /// the existing grow/write sequence. Copy bytes/policy before yield. Exact
    /// cluster count comes from the mounted engine, never guessed geometry.
    /// In-place writes and tail-only growth need no candidate search.</summary>

    public static Result BeginWriteWithFreeSearch(State* s, DescriptorToken token, ReadOnlySpan<byte> bytes, FreeSearch search, ulong offset = 0, bool positioned = false)
    {
        var result = BeginWriteCore(s, token, bytes, offset, positioned, true, default);
        if (result.Phase == Progress.Pending)
        {
            s->Work.SearchRequested = true;
            s->Work.Search = search;
        }

        return result;
    }

    private static Result BeginWriteCore(State* s, DescriptorToken token, ReadOnlySpan<byte> bytes, ulong offset, bool positioned, bool grow, ReadOnlySpan<uint> candidates)
    {
        if (!CurrentPrimary(s, token, out uint description) || s->Retiring)
            return Immediate(BadFd);
        if (!Revision(s))
            return Immediate(Io);
        Description* d = s->Input.Descriptions + description;
        if (d->Directory)
            return Immediate(IsDirectory);
        if ((d->Flags & 3) == 0)
            return Immediate(BadFd);
        if (!External(s, bytes))
            return Immediate(Fault);
        if (bytes.Length > IoBytes)
            return Immediate(Invalid);
        ulong position = positioned ? offset : d->Position;
        if (position > long.MaxValue)
            return Immediate(Invalid);
        if (position > uint.MaxValue || (ulong)bytes.Length > uint.MaxValue - position)
            return Immediate(Overflow);
        uint length = (uint)(position + (ulong)bytes.Length);
        if (length > d->File.Length && !grow)
            return Immediate(Unsupported);
        long allocation = CandidateInput(s, candidates);
        if (allocation != 0)
            return Immediate(allocation);
        if (length <= d->File.Length && candidates.Length != 0)
            return Immediate(Invalid);
        if (bytes.Length == 0)
            return Immediate(0);
        var started = BeginBorrow(s, token, description, Operation.Write, position, (uint)bytes.Length, positioned);
        if (started.Phase != Progress.Pending)
            return started;
        OS.Std.Memory.BufferOperations.MemoryCopy(bytes, new Span<byte>(s->Input.Input, bytes.Length));
        s->Work.NewLength = length;
        s->Work.GrowAllowed = grow;
        CopyCandidates(s, candidates);
        return started;
    }

    /// <summary>Copied single-iovec leaf for the measured explicit-offset wire.
    /// The future syscall adapter owns iovec/user-memory validation separately.</summary>

    public static Result Pwritev2Single(State* s, DescriptorToken token, ReadOnlySpan<byte> bytes, ulong offset, uint flags) => flags == RwfNoAppend ? BeginWrite(s, token, bytes, offset, true) : Immediate(Unsupported);
    public static Result Pwritev2SingleWithGrowth(State* s, DescriptorToken token, ReadOnlySpan<byte> bytes, ulong offset, uint flags, ReadOnlySpan<uint> candidates) => flags == RwfNoAppend ? BeginWriteWithGrowth(s, token, bytes, candidates, offset, true) : Immediate(Unsupported);
    public static Result Pwritev2SingleWithFreeSearch(State* s, DescriptorToken token, ReadOnlySpan<byte> bytes, ulong offset, uint flags, FreeSearch search) => flags == RwfNoAppend ? BeginWriteWithFreeSearch(s, token, bytes, search, offset, true) : Immediate(Unsupported);
    /// <summary>Grow/zero through explicitly copied allocation candidates, or
    /// shrink through the separate truncate API. Neither operation moves the
    /// cursor; authenticated mutation receipts update every retained lineage.</summary>

    public static Result BeginGrow(State* s, DescriptorToken token, uint newLength, ReadOnlySpan<uint> candidates)
    {
        if (!CurrentPrimary(s, token, out uint description) || s->Retiring)
            return Immediate(BadFd);
        if (!Revision(s))
            return Immediate(Io);
        Description* d = s->Input.Descriptions + description;
        if (d->Directory)
            return Immediate(IsDirectory);
        if ((d->Flags & 3) == 0)
            return Immediate(BadFd);
        if (newLength <= d->File.Length)
            return Immediate(Invalid);
        long allocation = CandidateInput(s, candidates);
        if (allocation != 0)
            return Immediate(allocation);
        var started = BeginBorrow(s, token, description, Operation.Grow, 0, 0, true);
        if (started.Phase == Progress.Pending)
        {
            s->Work.NewLength = newLength;
            CopyCandidates(s, candidates);
        }

        return started;
    }

    public static Result BeginTruncate(State* s, DescriptorToken token, uint newLength)
    {
        if (!CurrentPrimary(s, token, out uint description) || s->Retiring)
            return Immediate(BadFd);
        if (!Revision(s))
            return Immediate(Io);
        Description* d = s->Input.Descriptions + description;
        if (d->Directory)
            return Immediate(IsDirectory);
        if ((d->Flags & 3) == 0)
            return Immediate(BadFd);
        if (newLength > d->File.Length)
            return Immediate(Unsupported);
        var started = BeginBorrow(s, token, description, Operation.Truncate, 0, 0, true);
        if (started.Phase == Progress.Pending)
            s->Work.NewLength = newLength;
        return started;
    }

    /// <summary>Grow/zero without a payload through one copied bounded search.
    /// This is an opt-in host contract, not an allocator or reservation. The
    /// normal authenticated result path preserves handles and process locks.</summary>

    public static Result BeginGrowWithFreeSearch(State* s, DescriptorToken token, uint newLength, FreeSearch search)
    {
        var result = BeginGrow(s, token, newLength, default);
        if (result.Phase == Progress.Pending)
        {
            s->Work.SearchRequested = true;
            s->Work.Search = search;
        }

        return result;
    }

    /// <summary>Unlink by copied root path only when NO live/borrowed regular
    /// description refers to its physical entry. Open-unlink retention is
    /// deliberately refused. A stale closed fd cannot act on slot/name reuse.</summary>

    public static Result BeginUnlink(State* s, ReadOnlySpan<byte> path) => BeginUnlinkForProcess(s, PrimaryProcess(s), path);
    public static Result BeginUnlinkForProcess(State* s, ProcessToken process, ReadOnlySpan<byte> path)
    {
        if (!Bound(s) || s->Retiring || !ProcessCurrent(s, process))
            return Immediate(BadFd);
        if (!Revision(s))
            return Immediate(Io);
        if (!External(s, path))
            return Immediate(Fault);
        if (IsRoot(path))
            return Immediate(IsDirectory);
        long policy = Path(path);
        if (policy != 0)
            return Immediate(policy);
        if (s->Work.Active || s->Input.Engine->RequestActive)
            return Immediate(Again);
        if (s->NextGeneration == ulong.MaxValue)
            return Immediate(Overflow);
        s->Work = new()
        {
            Active = true,
            Generation = ++s->NextGeneration,
            Kind = Operation.Unlink,
            Step = Step.DeleteLookup,
            Description = uint.MaxValue,
            Process = process,
            NameLength = (ushort)(path.Length - 6)
        };
        for (int i = 6; i < path.Length; i++)
            s->Work.Name[i - 6] = path[i];
        return StartedFirstEngine(s, Lookup(s));
    }

    /// <summary>Begin a held bounded read. The final copy is a separate call
    /// after original fd/description generation checks; no destination is kept.</summary>

    public static Result BeginRead(State* s, DescriptorToken token, uint bytes, ulong offset = 0, bool positioned = false)
    {
        if (!CurrentPrimary(s, token, out uint description) || s->Retiring)
            return Immediate(BadFd);
        if (!Revision(s))
            return Immediate(Io);
        Description* d = s->Input.Descriptions + description;
        if (d->Directory)
            return Immediate(IsDirectory);
        if ((d->Flags & 3) == 1)
            return Immediate(BadFd);
        if (bytes > IoBytes || offset > long.MaxValue)
            return Immediate(Invalid);
        if (bytes == 0)
            return Immediate(0);
        return BeginBorrow(s, token, description, Operation.Read, positioned ? offset : d->Position, bytes, positioned);
    }

    /// <summary>Begin a fresh regular-file metadata lookup using the configured
    /// primary process. This may yield for real directory/FAT reads. It neither
    /// uses historical InspectCopied facts nor changes any file cursor.</summary>

    public static Result BeginStat(State* s, DescriptorToken token) => BeginStatForProcess(s, PrimaryProcess(s), token);
    /// <summary>Process-scoped form of BeginStat. Only the description's exact
    /// registered process token may borrow it. Directory stat is unsupported.</summary>

    public static Result BeginStatForProcess(State* s, ProcessToken process, DescriptorToken token)
    {
        if (!Current(s, token, out uint description) || !Owned(s, process, description) || s->Retiring)
            return Immediate(BadFd);
        if (!Revision(s))
            return Immediate(Io);
        if (s->Input.Descriptions[description].Directory)
            return Immediate(Unsupported);
        return BeginBorrow(s, token, description, Operation.Stat, 0, 0, true);
    }

    /// <summary>Fresh stat/lstat for exact /data (also /data/) or one confined
    /// regular root component. Both follow policies agree because this FAT
    /// namespace has no symlink objects. No fd/description is allocated or
    /// borrowed, and no cursor/lock is changed. The path is copied before return;
    /// real directory and mirrored-FAT reads may yield. Nested paths, traversal
    /// and relative names remain unsupported. A trailing slash on an existing
    /// regular file returns ENOTDIR after its actual lookup. Successful facts
    /// stay held for CopyStat under the original process/request/revision.</summary>

    public static Result BeginPathStat(State* s, ReadOnlySpan<byte> path, bool noFollow = false) => BeginPathStatForProcess(s, PrimaryProcess(s), path, noFollow);
    /// <summary>Process-scoped BeginPathStat. The copied process generation is
    /// host ownership, not Linux PID authority; exit/cancellation revokes late
    /// metadata. Root allocation is obtained by an actual root-chain walk,
    /// without fabricating a regular-file token.</summary>

    public static Result BeginPathStatForProcess(State* s, ProcessToken process, ReadOnlySpan<byte> path, bool noFollow = false)
    {
        if (!Bound(s) || s->Retiring || !ProcessCurrent(s, process))
            return Immediate(BadFd);
        if (!Revision(s))
            return Immediate(Io);
        if (!External(s, path))
            return Immediate(Fault);
        bool root = IsRoot(path);
        bool directoryRequired = !root && path.Length != 0 && path[path.Length - 1] == '/';
        ReadOnlySpan<byte> admitted = directoryRequired ? path.Slice(0, path.Length - 1) : path;
        long policy = root ? 0 : Path(admitted);
        if (policy != 0)
            return Immediate(policy);
        if (s->Work.Active || s->Input.Engine->RequestActive)
            return Immediate(Again);
        if (s->NextGeneration == ulong.MaxValue)
            return Immediate(Overflow);
        s->Work = new()
        {
            Active = true,
            Generation = ++s->NextGeneration,
            Kind = Operation.PathStat,
            Step = root ? Step.RootStat : Step.Lookup,
            Description = uint.MaxValue,
            Process = process,
            NameLength = root ? (ushort)0 : (ushort)(admitted.Length - 6),
            DirectoryRequired = directoryRequired
        };
        for (uint i = 0; i < s->Work.NameLength; i++)
            s->Work.Name[i] = admitted[(int)i + 6];
        C.RequestToken request = default;
        var status = root ? C.BeginInspectRoot(s->Input.Engine, &request) : Lookup(s);
        if (root)
            s->Work.EngineRequest = request;
        return StartedFirstEngine(s, status);
    }

    /// <summary>Issue an actual engine volume FLUSH for a file or root fd.
    /// File fdatasync uses the same stronger barrier; directory fdatasync rejects.
    /// This supplies no filesystem crash or power-loss durability assertion.</summary>

    public static Result BeginSync(State* s, DescriptorToken token, bool dataOnly = false)
    {
        if (!CurrentPrimary(s, token, out uint description) || s->Retiring)
            return Immediate(BadFd);
        if (!Revision(s))
            return Immediate(Io);
        if (dataOnly && s->Input.Descriptions[description].Directory)
            return Immediate(Invalid);
        return BeginBorrow(s, token, description, Operation.Sync, 0, 0, false);
    }

    /// <summary>One bounded engine step. It never copies caller/user output
    /// or commits a description cursor. Positive read readiness holds the
    /// engine's copied bytes until CopyRead; unrelated work cannot bypass it.
    /// A first begin blocked by a borrowed block IN retains its copied request.
    /// Until admitted, Pump retries only that unissued begin; it never polls or
    /// cancels another client's engine/block request or replays issued work.</summary>

    public static Result Pump(State* s)
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
    {
        var stamp = OS.Kernel.Diagnostics.WritableStorageTiming.Enter(OS.Kernel.Diagnostics.WritableStorageTiming.Bucket.Pump);
        Result result = PumpCore(s);
        OS.Kernel.Diagnostics.WritableStorageTiming.Leave(stamp);
        return result;
    }

    private static Result PumpCore(State* s)
#endif
    {
        if (!Bound(s))
            return new(Progress.Rejected);
        if (!s->Work.Active)
            return s->Poisoned ? Immediate(Io) : Immediate(0);
        if (s->Work.Ready)
            return Observe(s);
        if (!Revision(s))
            return Recovery(s);
        if (s->Work.EngineQueued)
            return PumpFirstEngine(s);
        var status = C.Pump(s->Input.Engine);
        if (!AcceptOwnRevision(s))
            return Recovery(s);
        if (status == C.Status.Pending)
            return Observe(s);
        if (status is C.Status.RecoveryRequired || s->Input.Engine->Dirty)
            return Recovery(s);
        if (status is not C.Status.Ready)
        {
            s->Poisoned = true;
            Finish(s, Io, status);
            return AfterFinish(s);
        }

        Pending* p = &s->Work;
        if (p->Step == Step.RootStat)
        {
            C.RootFacts facts = default;
            status = C.CopyRootFacts(s->Input.Engine, p->EngineRequest, &facts, (uint)sizeof(C.RootFacts));
            if (status != C.Status.Success && s->Input.Engine->IsReady)
            {
                C.Cancel(s->Input.Engine, p->EngineRequest);
                C.RootFacts ignored = default;
                C.CopyRootFacts(s->Input.Engine, p->EngineRequest, &ignored, (uint)sizeof(C.RootFacts));
            }

            if (status is C.Status.BlockFailure or C.Status.InvalidState)
                s->Poisoned = true;
            if (p->Cancelled)
                Finish(s, Interrupted, C.Status.Cancelled);
            else if (status != C.Status.Success)
                Finish(s, Map(status), status);
            else if (facts.Owner != (ulong)s->Input.Engine || facts.Epoch != s->Input.Epoch || facts.Revision != s->KnownRevision)
            {
                s->Poisoned = true;
                Finish(s, Io, C.Status.StaleToken);
            }
            else
            {
                p->RootFacts = facts;
                Finish(s, 0, status);
            }

            return AfterFinish(s);
        }

        if (p->Step is Step.Lookup or Step.CreatedLookup or Step.Authenticate or Step.DeleteLookup)
        {
            if (p->Kind is Operation.Stat or Operation.PathStat && !p->Cancelled)
            {
                C.OpenFacts facts = default;
                status = C.CopyOpenFacts(s->Input.Engine, p->EngineRequest, &facts, (uint)sizeof(C.OpenFacts));
                if (status == C.Status.Success)
                {
                    if (p->Kind == Operation.Stat)
                    {
                        if (!Physical(s->Input.Descriptions[p->Description].File, facts.File))
                        {
                            Finish(s, Stale, C.Status.StaleToken);
                            return AfterFinish(s);
                        }

                        s->Input.Descriptions[p->Description].File = facts.File;
                    }

                    p->StatFacts = facts;
                    Finish(s, p->DirectoryRequired ? NotDirectory : 0, status);
                    return AfterFinish(s);
                }

                // CopyOpenFacts deliberately preserves unsuccessful Ready
                // requests. Consume that original lookup through its old API.
                C.FileToken ignored = default;
                C.CopyOpen(s->Input.Engine, p->EngineRequest, &ignored);
                Finish(s, Map(status), status);
                return AfterFinish(s);
            }

            C.FileToken file = default;
            status = C.CopyOpen(s->Input.Engine, p->EngineRequest, &file);
            if (p->Cancelled)
            {
                Finish(s, Interrupted, status);
                return AfterFinish(s);
            }

            if (p->Step == Step.Lookup && status == C.Status.NotFound && (p->Flags & Create) != 0)
            {
                p->Step = Step.Create;
                status = CreateEmpty(s);
                if (status != C.Status.Pending)
                    Finish(s, Map(status), status);
                return AfterFinish(s);
            }

            if (status != C.Status.Success)
            {
                Finish(s, Map(status), status);
                return AfterFinish(s);
            }

            if (p->Step == Step.DeleteLookup)
            {
                if (OpenPhysicalEntry(s, file))
                {
                    Finish(s, Busy, C.Status.Busy);
                    return AfterFinish(s);
                }

                p->File = file;
                p->Step = Step.Delete;
                p->RevisionAccepted = false;
                C.RequestToken request = default;
                status = C.BeginDelete(s->Input.Engine, file, &request);
                p->EngineRequest = request;
                if (status is not (C.Status.Pending or C.Status.Ready))
                    Finish(s, Map(status), status);
                return AfterFinish(s);
            }

            if (p->Step == Step.Authenticate)
            {
                if (!Physical(s->Input.Descriptions[p->Description].File, file))
                {
                    Finish(s, Stale, C.Status.StaleToken);
                    return AfterFinish(s);
                }

                s->Input.Descriptions[p->Description].File = file;
                p->File = file;
                status = BeginData(s);
                if (status is not (C.Status.Pending or C.Status.Ready))
                    Finish(s, Map(status), status);
                return AfterFinish(s);
            }

            if (p->Step == Step.Lookup && (p->Flags & (Create | Exclusive)) == (Create | Exclusive))
            {
                Finish(s, Exists, C.Status.AlreadyExists);
                return AfterFinish(s);
            }

            if ((p->Flags & 3) != 0 && ((file.Fingerprint1 >> 24) & 1) != 0)
            {
                Finish(s, Access, C.Status.ReadOnly);
                return AfterFinish(s);
            }

            p->File = file;
            Finish(s, p->Number, C.Status.Success);
            return AfterFinish(s);
        }

        if (p->Step == Step.Read)
        {
            var failure = s->Input.Engine->LastFailure;
            if (p->Cancelled)
            {
                DrainRead(s);
                return AfterFinish(s);
            }

            Finish(s, failure == C.Status.Success ? p->Expected : Map(failure), failure);
            return AfterFinish(s);
        }

        if (p->Step == Step.FindFree)
        {
            if (!p->Cancelled && (!Current(s, p->Original, out uint description) || description != p->Description || s->Input.Descriptions[description].Generation != p->DescriptionGeneration || !ProcessCurrent(s, p->Process)))
            {
                p->Discard = true;
                return Cancel(s, new(s));
            }

            C.FreeResult selected = default;
            status = C.CopyFree(s->Input.Engine, p->EngineRequest, new Span<uint>(p->Growth, C.MaximumGrowthClusters), &selected);
            if (status is C.Status.InvalidState or C.Status.InvalidStorage or C.Status.StaleToken)
            {
                p->Failure = status;
                return Recovery(s);
            }

            if (status == C.Status.BlockFailure)
                s->Poisoned = true;
            if (!p->Cancelled && status == C.Status.Success)
            {
                if (!SelectionMatches(s, selected))
                {
                    p->Failure = C.Status.InvalidState;
                    return Recovery(s);
                }

                p->SearchComplete = true;
                status = BeginData(s);
                if (status is not (C.Status.Pending or C.Status.Ready))
                    Finish(s, Map(status), status);
                return AfterFinish(s);
            }

            Finish(s, p->Cancelled ? Interrupted : Map(status), status);
            return AfterFinish(s);
        }

        if (p->Step is Step.Grow or Step.Truncate or Step.Delete)
        {
            C.MutationResult mutation = default;
            status = C.CopyMutationResult(s->Input.Engine, p->EngineRequest, &mutation);
            if (status is C.Status.InvalidState or C.Status.InvalidStorage or C.Status.StaleToken)
            {
                p->Failure = status;
                return Recovery(s);
            }

            if (status == C.Status.Success)
            {
                uint expected = p->Step == Step.Delete ? 0 : p->Step == Step.Grow ? p->NewLength - p->File.Length : p->File.Length - p->NewLength;
                if (mutation.Status != status || mutation.Bytes != expected || !Same(mutation.Request, p->EngineRequest) || mutation.Deleted != (p->Step == Step.Delete) || !RebindLineage(s, mutation.OriginalFile, mutation.NewFile, mutation.Deleted))
                {
                    p->Failure = C.Status.InvalidState;
                    return Recovery(s);
                }

                // Physical success belongs to the inode even if the original
                // fd/process was closed while the final FLUSH completed. The
                // receipt updates other descriptions before any result drop.
                if (!p->Cancelled && p->Kind == Operation.Write)
                {
                    status = BeginData(s);
                    if (status is not (C.Status.Pending or C.Status.Ready))
                        Finish(s, Map(status), status);
                    return AfterFinish(s);
                }
            }

            Finish(s, p->Cancelled ? Interrupted : Map(status), status);
            return AfterFinish(s);
        }

        C.Result result = default;
        status = C.CopyResult(s->Input.Engine, p->EngineRequest, &result);
        if (p->Step == Step.Create && status == C.Status.AlreadyExists && !p->Cancelled)
        {
            status = CreateEmpty(s);
            if (status != C.Status.Pending)
                Finish(s, Map(status), status);
            return AfterFinish(s);
        }

        if (p->Step == Step.Create && status == C.Status.Success && !p->Cancelled)
        {
            p->Step = Step.CreatedLookup;
            status = Lookup(s);
            if (status != C.Status.Pending)
                Finish(s, Map(status), status);
            return AfterFinish(s);
        }

        if (p->Cancelled)
            Finish(s, Interrupted, status);
        else if (status != C.Status.Success)
            Finish(s, Map(status), status);
        else if (p->Step == Step.Write && result.Bytes != p->Bytes)
        {
            s->Poisoned = true;
            Finish(s, Io, C.Status.InvalidState);
        }
        else
            Finish(s, p->Step == Step.Write ? result.Bytes : 0, status);
        return AfterFinish(s);
    }

    public static Result Poll(State* s, RequestToken request) => Matches(s, request) ? Observe(s) : new(Progress.Rejected);
    /// <summary>Consume one ready open/write/barrier/error. Only this step
    /// publishes the reserved fd or commits a successful sequential write
    /// cursor. Successful reads/stat require CopyRead/CopyStat instead.</summary>

    public static Result Complete(State* s, RequestToken request)
    {
        if (!Matches(s, request))
            return new(Progress.Rejected);
        Pending* p = &s->Work;
        if (!p->Ready)
            return Observe(s);
        if (s->Input.Engine->Dirty || s->KnownRevision != s->Input.Engine->VolumeRevision || s->Poisoned && p->Value >= 0)
            return Recovery(s);
        if (p->Kind == Operation.Read && p->Step == Step.Read || p->Kind is Operation.Stat or Operation.PathStat && p->Value >= 0)
            return new(Progress.Rejected);
        if (!ProcessCurrent(s, p->Process))
        {
            Drop(s);
            return Immediate(BadFd);
        }

        if (p->Borrowed && !Current(s, p->Original, out _))
        {
            p->Discard = true;
            Drop(s);
            return Immediate(BadFd);
        }

        long value = p->Value;
        DescriptorToken descriptor = default;
        var failure = p->Failure;
        if (p->Kind == Operation.Open && value >= 0)
        {
            Descriptor* f = s->Input.Descriptors + p->Number;
            Description* d = s->Input.Descriptions + p->Description;
            if (!ProcessCurrent(s, d->Process))
            {
                Drop(s);
                return Immediate(BadFd);
            }

            if (!f->Reserved || f->Description != p->Description + 1 || !d->Reserved || d->Generation != p->DescriptionGeneration)
            {
                s->Poisoned = true;
                Drop(s);
                return Immediate(Io);
            }

            d->Reserved = false;
            d->References = 1;
            d->File = p->File;
            d->NameLength = p->NameLength;
            for (uint i = 0; i < p->NameLength; i++)
                d->Name[i] = p->Name[i];
            f->Reserved = false;
            descriptor = new(s, p->Number);
        }
        else if (p->Kind == Operation.Write && value >= 0 && !p->Positioned)
            s->Input.Descriptions[p->Description].Position += (ulong)value;
        Drop(s);
        return new(Progress.Immediate, value, descriptor: descriptor, failure: failure);
    }

    /// <summary>Revalidate the original fd/description and all output aliases
    /// before consuming retained engine bytes. Caller output is synchronous
    /// supervisor RAM; the future syscall owner must revalidate user generations
    /// separately. Capacity failure keeps the read held for retry.</summary>

    public static Result CopyRead(State* s, RequestToken request, Span<byte> destination)
    {
        if (!Matches(s, request) || s->Work.Kind != Operation.Read)
            return new(Progress.Rejected);
        Pending* p = &s->Work;
        if (!p->Ready)
            return Observe(s);
        if (s->Input.Engine->Dirty || s->KnownRevision != s->Input.Engine->VolumeRevision || s->Poisoned && p->Value >= 0)
            return Recovery(s);
        if (p->Step != Step.Read)
            return Complete(s, request);
        if (!Current(s, p->Original, out _) || p->Discard)
        {
            p->Discard = true;
            Cancel(s, request);
            return Immediate(BadFd);
        }

        if (destination.Length > IoBytes || !External(s, destination) || p->Value > destination.Length)
            return Immediate(Fault);
        C.Result result = default;
        var status = C.CopyRead(s->Input.Engine, p->EngineRequest, destination, &result);
        if (status == C.Status.Pending)
            return Observe(s);
        if (status == C.Status.InvalidStorage)
            return Immediate(Fault);
        if (status == C.Status.RecoveryRequired || s->Input.Engine->Dirty)
            return Recovery(s);
        if (status is C.Status.BlockFailure or C.Status.InvalidState)
            s->Poisoned = true;
        long value = Map(status);
        var failure = status;
        if (status == C.Status.Success)
        {
            if (result.Bytes != p->Expected)
            {
                s->Poisoned = true;
                value = Io;
                failure = C.Status.InvalidState;
            }
            else
            {
                value = result.Bytes;
                if (!p->Positioned)
                    s->Input.Descriptions[p->Description].Position += result.Bytes;
            }
        }

        Drop(s);
        return new(Progress.Immediate, value, failure: failure);
    }

    /// <summary>Copy exactly one fresh StatMetadata to disjoint synchronous
    /// supervisor storage. Validate the full size, provider/device lifetime,
    /// process/request/revision and any borrowed fd generation before any write.
    /// Path requests never publish an fd. Bad output keeps successful Ready
    /// held for retry; errors leave output untouched. No cursor
    /// is changed. The syscall owner separately validates its user mapping.</summary>

    public static Result CopyStat(State* s, RequestToken request, StatMetadata* metadata, uint metadataBytes)
    {
        if (!Matches(s, request) || s->Work.Kind is not (Operation.Stat or Operation.PathStat))
            return new(Progress.Rejected);
        Pending* p = &s->Work;
        if (!p->Ready)
            return Observe(s);
        if (s->Input.Engine->Dirty || s->KnownRevision != s->Input.Engine->VolumeRevision || s->Poisoned && p->Value >= 0)
            return Recovery(s);
        if (p->Value < 0)
            return Complete(s, request);
        if (!ProcessCurrent(s, p->Process) || p->Discard || p->Kind == Operation.Stat && (!Current(s, p->Original, out uint description) || description != p->Description || s->Input.Descriptions[description].Generation != p->DescriptionGeneration))
        {
            p->Discard = true;
            Drop(s);
            return Immediate(BadFd);
        }

        if (metadataBytes != sizeof(StatMetadata) || !Ordinary((ulong)metadata, (ulong)sizeof(StatMetadata), 8) || !External(s, new ReadOnlySpan<byte>(metadata, sizeof(StatMetadata))))
            return Immediate(Fault);
        if (!CanBorrowStorage(s, s->Input.Epoch, (ulong)metadata, (ulong)sizeof(StatMetadata)))
            return new(Progress.Immediate, Io, failure: C.Status.InvalidState);
        *metadata = new(p->StatFacts, p->RootFacts, p->DescriptionGeneration, p->Step == Step.RootStat);
        Drop(s);
        return Immediate(0);
    }

    /// <summary>Revoke pending read/open continuation, or honor an already
    /// completed write/barrier. Close/retirement additionally discard every
    /// result. An unissued queued begin releases only its own copied work and
    /// borrow; it never cancels the foreign block IN. Issued dirty cancellation
    /// retains recovery, never fake rollback.</summary>

    public static Result Cancel(State* s, RequestToken request)
    {
        if (!Matches(s, request))
            return new(Progress.Rejected);
        Pending* p = &s->Work;
        if (p->EngineQueued)
        {
            if (!FirstEngineShape(p) || !RetainedStorage(s, s->Input.Epoch, 0, 0, false))
            {
                p->Failure = C.Status.InvalidState;
                return Recovery(s);
            }

            p->EngineQueued = false;
            p->Cancelled = true;
            p->Step = Step.Done;
            Finish(s, Interrupted, C.Status.Cancelled);
            return AfterFinish(s);
        }

        if (p->Ready && p->Step != Step.Read)
        {
            if (p->Kind is Operation.Open or Operation.Read or Operation.Stat or Operation.PathStat)
            {
                p->Cancelled = true;
                p->Step = Step.Done;
                Finish(s, Interrupted, C.Status.Cancelled);
            }

            if (p->Discard)
                Drop(s);
            return p->Active ? Observe(s) : Immediate(0);
        }

        p->Cancelled = true;
        var status = C.Cancel(s->Input.Engine, p->EngineRequest);
        if (status == C.Status.RecoveryRequired || s->Input.Engine->Dirty)
            return Recovery(s);
        if (status == C.Status.Cancelled && !s->Input.Engine->RequestActive)
        {
            p->Step = Step.Done;
            Finish(s, Interrupted, status);
            return AfterFinish(s);
        }

        if (p->Ready && p->Step == Step.Read)
        {
            DrainRead(s);
            return AfterFinish(s);
        }

        return Observe(s);
    }

    /// <summary>Revoke all descriptors, cancel and drain pending work, then
    /// scrub local tables/input while engine and device extents remain retained.
    /// Detach never calls engine/device Stop. Dirty state refuses retirement.</summary>

    public static Result Detach(State* s)
    {
        if (!Bound(s))
            return new(Progress.Rejected);
        s->Retiring = true;
        for (uint fd = 3; fd < s->Input.DescriptorCount; fd++)
            if (Find(s, fd, out uint description))
                CloseCore(s, s->Input.Descriptions[description].Process, new(s, fd), true);
        if (s->Poisoned && s->Work.Active && !s->Work.Ready)
            return Recovery(s);
        if (s->Input.Engine->Dirty)
            return Recovery(s);
        if (s->Work.Active)
        {
            s->Work.Discard = true;
            Cancel(s, new(s));
            if (s->Work.Active)
                return Observe(s);
        }

        for (uint i = 0; i < s->Input.DescriptorCount; i++)
            s->Input.Descriptors[i] = default;
        for (uint i = 0; i < s->Input.DescriptionCount; i++)
            s->Input.Descriptions[i] = default;
        if (s->Locking)
        {
            for (uint i = 0; i < s->LockInput.ProcessCount; i++)
                s->LockInput.Processes[i] = default;
            for (uint i = 0; i < LockSlots; i++)
                s->LockInput.Locks[i] = default;
            ScrubLocksScratch(s);
        }

        new Span<byte>(s->Input.Input, (int)IoBytes).Clear();
        s->Active = false;
        s->RetiredValue = true;
        return new(Progress.Retired);
    }

    private static Result BeginBorrow(State* s, DescriptorToken token, uint description, Operation kind, ulong offset, uint bytes, bool positioned)
    {
        if (s->Work.Active || s->Input.Engine->RequestActive)
            return Immediate(Again);
        if (s->NextGeneration == ulong.MaxValue || s->Input.Descriptions[description].Borrowers == uint.MaxValue)
            return Immediate(Overflow);
        Description* d = s->Input.Descriptions + description;
        s->Work = new()
        {
            Active = true,
            Generation = ++s->NextGeneration,
            Kind = kind,
            Step = kind == Operation.Sync ? Step.Flush : Step.Authenticate,
            Original = token,
            Description = description,
            DescriptionGeneration = d->Generation,
            Offset = offset,
            Bytes = bytes,
            Positioned = positioned,
            Borrowed = true,
            NameLength = d->NameLength,
            Process = d->Process
        };
        d->Borrowers++;
        for (uint i = 0; i < d->NameLength; i++)
            s->Work.Name[i] = d->Name[i];
        C.RequestToken request = default;
        var status = kind == Operation.Sync ? C.BeginFlush(s->Input.Engine, &request) : Lookup(s);
        if (kind == Operation.Sync)
            s->Work.EngineRequest = request;
        return StartedFirstEngine(s, status);
    }

    // Only a first, unissued begin may wait behind another client's block IN.
    // There are no aliased caller inputs or effects to replay: lookup names,
    // write bytes, offsets, process and descriptor generations are already owned.
    private static Result StartedFirstEngine(State* s, C.Status status)
    {
        if (status == C.Status.Busy && FirstEngineShape(&s->Work) && !s->Input.Engine->RequestActive && C.CanAdmitStorage(s->Input.Engine, s->Input.Epoch))
        {
            s->Work.EngineQueued = true;
            return Observe(s);
        }

        if (status != C.Status.Pending)
            Finish(s, Map(status), status);
        return Observe(s);
    }

    private static bool FirstEngineShape(Pending* p) => p->Active && !p->Ready && p->Generation != 0 && p->NameLength < PathBytes && p->EngineRequest.Owner == 0 && p->EngineRequest.Epoch == 0 && p->EngineRequest.Generation == 0 && (p->Step == Step.Lookup && p->Kind is Operation.Open or Operation.PathStat || p->Step == Step.Authenticate && p->Borrowed && p->Kind is Operation.Read or Operation.Write or Operation.Grow or Operation.Truncate or Operation.Stat || p->Step == Step.DeleteLookup && p->Kind == Operation.Unlink || p->Step == Step.RootStat && p->Kind == Operation.PathStat || p->Step == Step.Flush && p->Kind == Operation.Sync);
    private static Result PumpFirstEngine(State* s)
    {
        Pending* p = &s->Work;
        if (!FirstEngineShape(p) || !RetainedStorage(s, s->Input.Epoch, 0, 0, false))
        {
            p->Failure = C.Status.InvalidState;
            return Recovery(s);
        }

        if (!ProcessCurrent(s, p->Process) || p->Borrowed && (!Current(s, p->Original, out uint description) || description != p->Description || s->Input.Descriptions[description].Generation != p->DescriptionGeneration))
        {
            p->Discard = true;
            Drop(s);
            return Immediate(BadFd);
        }

        if (p->Kind == Operation.Open && (p->Number >= s->Input.DescriptorCount || p->Description >= s->Input.DescriptionCount || !s->Input.Descriptors[p->Number].Reserved || s->Input.Descriptors[p->Number].Description != p->Description + 1 || !s->Input.Descriptions[p->Description].Reserved || s->Input.Descriptions[p->Description].Generation != p->DescriptionGeneration))
        {
            p->Failure = C.Status.InvalidState;
            return Recovery(s);
        }

        // A different logical C request is never ours to pump, even if a
        // supervisor started it after the borrowed read released the device.
        if (s->Input.Engine->RequestActive)
            return Observe(s);
        if (!C.CanAdmitStorage(s->Input.Engine, s->Input.Epoch))
        {
            s->Poisoned = true;
            p->EngineQueued = false;
            Finish(s, Io, C.Status.InvalidState);
            return AfterFinish(s);
        }

        C.RequestToken request = default;
        var status = p->Step == Step.Flush ? C.BeginFlush(s->Input.Engine, &request) : p->Step == Step.RootStat ? C.BeginInspectRoot(s->Input.Engine, &request) : Lookup(s);
        if (p->Step is Step.Flush or Step.RootStat)
            p->EngineRequest = request;
        p->EngineQueued = false;
        return StartedFirstEngine(s, status);
    }

    private static C.Status Lookup(State* s)
    {
        Span<char> name = stackalloc char[(int)PathBytes];
        for (uint i = 0; i < s->Work.NameLength; i++)
            name[(int)i] = (char)s->Work.Name[i];
        C.RequestToken request = default;
        var status = C.BeginOpenRoot(s->Input.Engine, name.Slice(0, s->Work.NameLength), &request);
        s->Work.EngineRequest = request;
        return status;
    }

    private static C.Status CreateEmpty(State* s)
    {
        if (++s->Work.AliasAttempts > 64)
            return C.Status.CapacityExceeded;
        if (s->NextAlias > 9999999)
            return C.Status.GenerationExhausted;
        Span<byte> alias = stackalloc byte[11];
        uint value = (uint)s->NextAlias++;
        alias[0] = (byte)'F';
        for (int i = 7; i > 0; i--)
        {
            alias[i] = (byte)('0' + value % 10);
            value /= 10;
        }

        alias[8] = (byte)'D';
        alias[9] = (byte)'A';
        alias[10] = (byte)'T';
        Span<char> name = stackalloc char[(int)PathBytes];
        for (uint i = 0; i < s->Work.NameLength; i++)
            name[(int)i] = (char)s->Work.Name[i];
        C.RequestToken request = default;
        var status = C.BeginCreateEmpty(s->Input.Engine, name.Slice(0, s->Work.NameLength), alias, &request);
        s->Work.EngineRequest = request;
        return status;
    }

    private static C.Status BeginData(State* s)
    {
        Pending* p = &s->Work;
        C.RequestToken request = default;
        C.Status status;
        p->RevisionAccepted = false;
        if (p->Kind == Operation.Grow || p->Kind == Operation.Write && p->GrowAllowed && p->NewLength > p->File.Length)
        {
            if (p->SearchRequested && !p->SearchComplete)
            {
                uint count = 0;
                status = C.GrowthCandidatesRequired(s->Input.Engine, p->File, p->NewLength, &count);
                if (status != C.Status.Success)
                    return status;
                p->GrowthCount = count;
                if (count != 0)
                {
                    C.FreeCursor cursor = default;
                    status = C.CreateFreeCursor(s->Input.Engine, p->Search.FirstCluster, &cursor);
                    if (status != C.Status.Success)
                        return status;
                    p->Step = Step.FindFree;
                    status = C.BeginFindFree(s->Input.Engine, cursor, count, p->Search.ClusterBudget, p->Search.SectorReadBudget, &request);
                    p->EngineRequest = request;
                    return status;
                }

                p->SearchComplete = true;
            }

            p->Step = Step.Grow;
            status = C.BeginGrow(s->Input.Engine, p->File, p->NewLength, new ReadOnlySpan<uint>(p->Growth, (int)p->GrowthCount), &request);
        }
        else if (p->Kind == Operation.Truncate)
        {
            p->Step = Step.Truncate;
            status = C.BeginTruncate(s->Input.Engine, p->File, p->NewLength, &request);
        }
        else if (p->Kind == Operation.Write)
        {
            p->Step = Step.Write;
            status = C.BeginOverwrite(s->Input.Engine, p->File, p->Offset, new ReadOnlySpan<byte>(s->Input.Input, (int)p->Bytes), &request);
        }
        else
        {
            p->Step = Step.Read;
            p->Expected = p->Offset >= p->File.Length ? 0 : (uint)(p->Bytes < p->File.Length - p->Offset ? p->Bytes : p->File.Length - p->Offset);
            status = C.BeginRead(s->Input.Engine, p->File, p->Offset, p->Bytes, &request);
        }

        p->EngineRequest = request;
        return status;
    }

    private static void DrainRead(State* s)
    {
        C.Result result = default;
        var status = C.CopyRead(s->Input.Engine, s->Work.EngineRequest, Span<byte>.Empty, &result);
        if (status == C.Status.Pending)
            return;
        if (status is not (C.Status.Cancelled or C.Status.Success))
        {
            s->Poisoned = true;
            Finish(s, Io, status);
        }
        else
        {
            s->Work.Step = Step.Done;
            Finish(s, Interrupted, C.Status.Cancelled);
        }
    }

    private static void Finish(State* s, long value, C.Status failure)
    {
        s->Work.Ready = true;
        s->Work.Value = value;
        s->Work.Failure = failure;
    }

    private static Result AfterFinish(State* s)
    {
        if (s->Work.Ready && s->Work.Discard && !s->Input.Engine->Dirty)
        {
            Drop(s);
            return Immediate(0);
        }

        return Observe(s);
    }

    private static Result Observe(State* s) => new(s->Poisoned && !s->Work.Ready || s->Input.Engine->Dirty && s->Input.Engine->LastFailure == C.Status.RecoveryRequired ? Progress.RecoveryRequired : s->Work.Ready ? Progress.Ready : Progress.Pending, s->Work.Ready ? s->Work.Value : 0, new(s), failure: s->Work.Failure);
    private static Result Recovery(State* s)
    {
        s->Poisoned = true;
        return new(Progress.RecoveryRequired, Io, s->Work.Active ? new(s) : default, failure: s->Work.Failure == C.Status.Success ? s->Input.Engine->LastFailure : s->Work.Failure);
    }

    private static Result Immediate(long value) => new(Progress.Immediate, value);
    private static bool Revision(State* s)
    {
        if (s->Poisoned)
            return false;
        if (s->KnownRevision == s->Input.Engine->VolumeRevision)
            return true;
        s->Poisoned = true;
        return false;
    }

    private static bool AcceptOwnRevision(State* s)
    {
        ulong current = s->Input.Engine->VolumeRevision;
        if (s->KnownRevision == current)
            return true;
        if (!s->Work.RevisionAccepted && s->Work.Step is Step.Write or Step.Create or Step.Grow or Step.Truncate or Step.Delete && s->KnownRevision != ulong.MaxValue && current == s->KnownRevision + 1)
        {
            s->KnownRevision = current;
            s->Work.RevisionAccepted = true;
            return true;
        }

        s->Poisoned = true;
        return false;
    }

    private static long CandidateInput(State* s, ReadOnlySpan<uint> candidates)
    {
        if (candidates.Length > C.MaximumGrowthClusters)
            return Invalid;
        if (candidates.Length == 0)
            return 0;
        fixed (uint* p = candidates)
            return External(s, new ReadOnlySpan<byte>(p, candidates.Length * 4)) ? 0 : Fault;
    }

    private static void CopyCandidates(State* s, ReadOnlySpan<uint> candidates)
    {
        s->Work.GrowthCount = (uint)candidates.Length;
        for (int i = 0; i < candidates.Length; i++)
            s->Work.Growth[i] = candidates[i];
    }

    private static bool OpenPhysicalEntry(State* s, C.FileToken file)
    {
        for (uint i = 0; i < s->Input.DescriptionCount; i++)
        {
            Description* d = s->Input.Descriptions + i;
            if (d->Active && !d->Reserved && !d->Directory && (d->References != 0 || d->Borrowers != 0) && d->File.Owner == file.Owner && d->File.Epoch == file.Epoch && d->File.EntryOffset == file.EntryOffset)
                return true;
        }

        return false;
    }

    private static bool RebindLineage(State* s, C.FileToken before, C.FileToken after, bool deleted)
    {
        Pending* p = &s->Work;
        if (!Physical(before, p->File) || before.Revision != p->File.Revision)
            return false;
        if (deleted)
        {
            if (p->Kind != Operation.Unlink || !p->RevisionAccepted || before.Revision == ulong.MaxValue || s->KnownRevision != before.Revision + 1 || OpenPhysicalEntry(s, before) || after.Owner != 0 || after.Epoch != 0 || after.Revision != 0 || after.EntryOffset != 0 || after.FirstCluster != 0 || after.Length != 0 || (after.Fingerprint0 | after.Fingerprint1 | after.Fingerprint2 | after.Fingerprint3) != 0)
                return false;
        }
        else
        {
            uint encoded = (((uint)(after.Fingerprint2 >> 32) & 0xffff) << 16) | ((uint)(after.Fingerprint3 >> 16) & 0xffff);
            bool changed = before.Length != p->NewLength;
            if (changed && (!p->RevisionAccepted || before.Revision == ulong.MaxValue || after.Revision != before.Revision + 1) || !changed && after.Revision != before.Revision || after.Owner != before.Owner || after.Epoch != before.Epoch || after.Revision != s->KnownRevision || after.EntryOffset != before.EntryOffset || after.Fingerprint0 != before.Fingerprint0 || after.Fingerprint1 != before.Fingerprint1 || (after.Fingerprint2 & ~(0xffffUL << 32)) != (before.Fingerprint2 & ~(0xffffUL << 32)) || (after.Fingerprint3 & 0xffff) != (before.Fingerprint3 & 0xffff) || after.Length != p->NewLength || (uint)(after.Fingerprint3 >> 32) != after.Length || encoded != after.FirstCluster || after.Length == 0 && after.FirstCluster != 0 || after.Length != 0 && after.FirstCluster < 2)
                return false;
        }

        for (uint i = 0; i < s->Input.DescriptionCount; i++)
        {
            Description* d = s->Input.Descriptions + i;
            if (d->Active && !d->Directory && Physical(d->File, before))
                d->File = after;
        }

        if (s->Locking)
            for (uint i = 0; i < LockSlots; i++)
                if (s->LockInput.Locks[i].Active && Physical(s->LockInput.Locks[i].File, before))
                {
                    if (deleted)
                        s->LockInput.Locks[i] = default;
                    else
                        s->LockInput.Locks[i].File = after;
                }

        p->File = after;
        return true;
    }

    private static bool SelectionMatches(State* s, C.FreeResult result)
    {
        Pending* p = &s->Work;
        if (result.Status != C.Status.Success || !Same(result.Request, p->EngineRequest) || result.Count != p->GrowthCount || result.Count is 0 or > C.MaximumGrowthClusters || result.Count > result.Scanned || result.Scanned > p->Search.ClusterBudget || result.SectorReads > p->Search.SectorReadBudget || result.NextCursor.Owner != p->File.Owner || result.NextCursor.Epoch != p->File.Epoch || result.NextCursor.Revision != s->KnownRevision || result.NextCursor.Generation != p->EngineRequest.Generation || result.NextCursor.NextCluster < p->Search.FirstCluster || (ulong)result.NextCursor.NextCluster - p->Search.FirstCluster != result.Scanned)
            return false;
        for (uint i = 0; i < result.Count; i++)
            if (p->Growth[i] < p->Search.FirstCluster || p->Growth[i] >= result.NextCursor.NextCluster || i != 0 && p->Growth[i] <= p->Growth[i - 1])
                return false;
        return true;
    }

    private static bool Physical(C.FileToken a, C.FileToken b) => a.Owner == b.Owner && a.Epoch == b.Epoch && a.EntryOffset == b.EntryOffset && a.FirstCluster == b.FirstCluster && a.Length == b.Length && a.Fingerprint0 == b.Fingerprint0 && a.Fingerprint1 == b.Fingerprint1 && a.Fingerprint2 == b.Fingerprint2 && a.Fingerprint3 == b.Fingerprint3;
    private static bool Same(DescriptorToken a, DescriptorToken b) => a.Owner == b.Owner && a.Epoch == b.Epoch && a.Generation == b.Generation && a.Number == b.Number;
    private static bool Same(C.RequestToken a, C.RequestToken b) => a.Owner == b.Owner && a.Epoch == b.Epoch && a.Generation == b.Generation;
    private static bool Same(ProcessToken a, ProcessToken b) => a.Owner == b.Owner && a.Epoch == b.Epoch && a.Generation == b.Generation && a.Index == b.Index && a.Pid == b.Pid;
    private static bool ProcessMatches(State* s, ProcessToken process, bool exiting = false)
    {
        if (!Bound(s))
            return false;
        if (!s->Locking)
            return process.Owner == 0 && process.Epoch == 0 && process.Generation == 0 && process.Index == 0 && process.Pid == 0;
        if (process.Owner != (ulong)s || process.Epoch != s->Input.Epoch || process.Generation == 0 || process.Index >= s->LockInput.ProcessCount)
            return false;
        ProcessSlot* p = s->LockInput.Processes + process.Index;
        return p->Active && (exiting || !p->Exiting) && p->Generation == process.Generation && p->Pid == process.Pid;
    }

    private static bool ProcessCurrent(State* s, ProcessToken process) => ProcessMatches(s, process);
    private static bool Owned(State* s, ProcessToken process, uint description, bool exiting = false) => ProcessMatches(s, process, exiting) && Same(s->Input.Descriptions[description].Process, process);
    private static bool CurrentPrimary(State* s, DescriptorToken token, out uint description) => Current(s, token, out description) && Owned(s, PrimaryProcess(s), description);
    private static void ReleaseFileLocks(State* s, ProcessToken process, C.FileToken file)
    {
        for (uint i = 0; i < LockSlots; i++)
            if (s->LockInput.Locks[i].Active && Same(s->LockInput.Locks[i].Process, process) && Physical(s->LockInput.Locks[i].File, file))
                s->LockInput.Locks[i] = default;
    }

    private static void ReleaseProcessLocks(State* s, ProcessToken process)
    {
        for (uint i = 0; i < LockSlots; i++)
            if (s->LockInput.Locks[i].Active && Same(s->LockInput.Locks[i].Process, process))
                s->LockInput.Locks[i] = default;
    }

    private static void ScrubLocksScratch(State* s)
    {
        for (uint i = 0; i < LockScratchSlots; i++)
            s->LockInput.Scratch[i] = default;
    }

    private static bool Matches(State* s, RequestToken t) => Bound(s) && s->Work.Active && t.Owner == (ulong)s && t.Epoch == s->Input.Epoch && t.Generation == s->Work.Generation;
    private static bool Bound(State* s) => Ordinary((ulong)s, (ulong)sizeof(State), 8) && s->Self == (ulong)s && s->Active && C.CanBorrowStorage(s->Input.Engine, s->Input.Epoch, (ulong)s, (ulong)sizeof(State));
    private static bool Find(State* s, uint number, out uint description)
    {
        description = 0;
        if (number < 3 || number >= s->Input.DescriptorCount)
            return false;
        Descriptor* f = s->Input.Descriptors + number;
        if (f->Reserved || f->Description == 0 || f->Description > s->Input.DescriptionCount)
            return false;
        description = f->Description - 1;
        Description* d = s->Input.Descriptions + description;
        return d->Active && !d->Reserved && d->References != 0;
    }

    private static bool CurrentBase(State* s, DescriptorToken token, out uint description)
    {
        description = 0;
        return Bound(s) && token.Owner == (ulong)s && token.Epoch == s->Input.Epoch && token.Generation != 0 && Find(s, token.Number, out description) && s->Input.Descriptors[token.Number].Generation == token.Generation;
    }

    private static bool Current(State* s, DescriptorToken token, out uint description) => CurrentBase(s, token, out description) && ProcessCurrent(s, s->Input.Descriptions[description].Process);
    private static int FreeFd(State* s)
    {
        for (uint i = 3; i < s->Input.DescriptorCount; i++)
            if (s->Input.Descriptors[i].Description == 0 && s->Input.Descriptors[i].Generation != ulong.MaxValue)
                return (int)i;
        return -1;
    }

    private static int FreeDescription(State* s)
    {
        for (uint i = 0; i < s->Input.DescriptionCount; i++)
            if (!s->Input.Descriptions[i].Active && s->Input.Descriptions[i].Generation != ulong.MaxValue)
                return (int)i;
        return -1;
    }

    private static void Reserve(State* s, uint number, uint description, uint flags, ProcessToken process)
    {
        Descriptor* f = s->Input.Descriptors + number;
        f->Generation++;
        f->Description = description + 1;
        f->Flags = flags & CloseOnExec;
        f->Reserved = true;
        Description* d = s->Input.Descriptions + description;
        ulong generation = d->Generation;
        *d = new()
        {
            Active = true,
            Reserved = true,
            Generation = generation + 1,
            Flags = flags & ~(CloseOnExec | NoControllingTerminal),
            Process = process
        };
    }

    private static void RetireDescription(State* s, uint index)
    {
        Description* d = s->Input.Descriptions + index;
        if (!d->Active || d->References != 0 || d->Borrowers != 0)
            return;
        ulong generation = d->Generation;
        *d = new()
        {
            Generation = generation
        };
    }

    private static void Drop(State* s)
    {
        Pending p = s->Work;
        if (p.Kind == Operation.Open && s->Input.Descriptors[p.Number].Reserved)
        {
            s->Input.Descriptors[p.Number].Description = s->Input.Descriptors[p.Number].Flags = 0;
            s->Input.Descriptors[p.Number].Reserved = false;
        }

        if (p.Description < s->Input.DescriptionCount)
        {
            Description* d = s->Input.Descriptions + p.Description;
            if (p.Borrowed && d->Active && d->Generation == p.DescriptionGeneration && d->Borrowers != 0)
                d->Borrowers--;
            RetireDescription(s, p.Description);
        }

        new Span<byte>(s->Input.Input, (int)IoBytes).Clear();
        s->Work = default;
    }

    private static bool IsRoot(ReadOnlySpan<byte> path) => path.Length is 5 or 6 && path[0] == '/' && path[1] == 'd' && path[2] == 'a' && path[3] == 't' && path[4] == 'a' && (path.Length == 5 || path[5] == '/');
    private static long Path(ReadOnlySpan<byte> path)
    {
        if (path.Length is < 7 or >= (int)PathBytes || !IsRoot(path.Slice(0, 6)))
            return Access;
        var name = path.Slice(6);
        if (name[name.Length - 1] is (byte)'.' or (byte)' ')
            return Access;
        int component = 0;
        bool nested = false;
        for (int i = 0; i <= name.Length; i++)
            if (i == name.Length || name[i] == '/')
            {
                var part = name.Slice(component, i - component);
                if (part.Length == 0 || part.Length == 1 && part[0] == '.' || part.Length == 2 && part[0] == '.' && part[1] == '.')
                    return Access;
                if (i < name.Length)
                    nested = true;
                component = i + 1;
            }

        if (nested)
            return Unsupported;
        for (int i = 0; i < name.Length; i++)
        {
            byte b = name[i];
            if (b == '/')
                return Unsupported;
            if (b is < 33 or > 126 || b is (byte)'\\' or (byte)':' or (byte)'*' or (byte)'?' or (byte)'"' or (byte)'<' or (byte)'>' or (byte)'|')
                return Invalid;
        }

        return 0;
    }

    private static bool External(State* s, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return true;
        fixed (byte* p = bytes)
        {
            ulong start = (ulong)p, count = (ulong)bytes.Length;
            return C.CanBorrowStorage(s->Input.Engine, s->Input.Epoch, start, count) && !Overlap(start, count, (ulong)s, (ulong)sizeof(State)) && !Overlap(start, count, (ulong)s->Input.Descriptors, s->Input.DescriptorCount * (ulong)sizeof(Descriptor)) && !Overlap(start, count, (ulong)s->Input.Descriptions, s->Input.DescriptionCount * (ulong)sizeof(Description)) && !Overlap(start, count, (ulong)s->Input.Input, IoBytes) && (!s->Locking || !Overlap(start, count, (ulong)s->LockInput.Processes, s->LockInput.ProcessCount * (ulong)sizeof(ProcessSlot)) && !Overlap(start, count, (ulong)s->LockInput.Locks, LockSlots * (ulong)sizeof(RangeLock)) && !Overlap(start, count, (ulong)s->LockInput.Scratch, LockScratchSlots * (ulong)sizeof(RangeLock)));
        }
    }

    private static long Map(C.Status status) => status switch
    {
        C.Status.Success => 0,
        C.Status.NotFound => Missing,
        C.Status.AlreadyExists => Exists,
        C.Status.SearchBudgetExceeded or C.Status.EndOfScan => Again,
        C.Status.InvalidName or C.Status.InvalidAllocation => Invalid,
        C.Status.NotFile => IsDirectory,
        C.Status.ReadOnly => Access,
        C.Status.Unsupported => Unsupported,
        C.Status.StaleToken => Stale,
        C.Status.Busy => Again,
        C.Status.CapacityExceeded => NoSpace,
        C.Status.GenerationExhausted or C.Status.BudgetExceeded => Overflow,
        C.Status.Cancelled => Interrupted,
        C.Status.InvalidStorage => Fault,
        _ => Io
    };
    private static bool Ordinary(ulong start, ulong bytes, ulong alignment) => start >= 4096 && start < 0x0000800000000000UL && (start & (alignment - 1)) == 0 && bytes != 0 && bytes <= 0x0000800000000000UL - start;
    private static bool Overlap(ulong start, ulong bytes, ulong other, ulong length) => bytes != 0 && length != 0 && start < other + length && other < start + bytes;
}
