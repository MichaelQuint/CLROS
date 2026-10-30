#if !NOSTDLIB_EFI || M16_025_PROBE
using OS.Kernel.Memory;
using OS.Pal;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
#if !NOSTDLIB_EFI || M16_027_PROBE
using P = OS.Storage.WritableBlockReadPort;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
#endif

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    public const uint DiskSlots = 8, DiskCompletionBytes = 8192, DiskExtentCount = 78;
    private const byte DiskQueued = 1, DiskWorking = 2, DiskReady = 3, DiskDraining = 4, DiskError = 5;
    private const byte DiskOpen = 1, DiskStat = 2, DiskRead = 3, DiskSeek = 4;
#if !NOSTDLIB_EFI || M16_027_PROBE
    private const byte DiskPausedReady = 6, DiskFstat = 5;
#endif
    public enum DiskProgress : byte { Rejected, NotHandled, Immediate, Pending, Ready, Draining }

    /// <summary>Value token for one noncopyable pending record. It authenticates
    /// this attachment epoch and nonwrapping request generation, never an fd.</summary>
    public readonly struct DiskToken
    {
        internal readonly ulong Owner, Epoch, Generation;
        internal readonly uint Index;
        internal DiskToken(ulong owner, ulong epoch, ulong generation, uint index)
        { Owner = owner; Epoch = epoch; Generation = generation; Index = index; }
        public bool IsActive => Owner != 0 && Epoch != 0 && Generation != 0;
    }

    /// <summary>By-value control observation. Pending/Draining never mean EOF.
    /// A draining record with empty Worker is orphaned and cannot copy or wake;
    /// PumpDiskOne must still retain/drain it before final owner detachment.</summary>
    public readonly struct DiskResult
    {
        public readonly DiskProgress Progress;
        public readonly long Value;
        public readonly DiskToken Token;
        public readonly OSHostHandle Worker;
        public readonly Fs.Status Failure;
        /// <summary>One selected live pending request made an actual block
        /// submit/completion/copy transition. Only the serialized current owner
        /// may use this to consider another bounded step; it grants no copy.</summary>
        public readonly bool Advanced;
        internal DiskResult(DiskProgress progress, long value = 0, DiskToken token = default,
            OSHostHandle worker = default, Fs.Status failure = Fs.Status.Success, bool advanced = false)
        { Progress = progress; Value = value; Token = token; Worker = worker; Failure = failure; Advanced = advanced; }
    }

    /// <summary>One original open description's filesystem handle. Duplicated
    /// fds share this binding and Description.Position. Only final reference /
    /// borrower retirement closes it. All fields initially zero.</summary>
    public struct DiskBinding
    {
        internal Fs.FileToken File;
        internal Fs.FileInspection Metadata;
        internal ulong Generation;
        internal uint Description, Active;
    }

    /// <summary>Stable pending storage independent of a thread Slot lifetime.
    /// No user address, resolved alias, caller pointer or Slot pointer is kept.
    /// Cancel revokes copy/wake first; original fd/description and FS tokens
    /// stay retained until actual cancellation drain or FS retirement.</summary>
    public struct DiskRequest
    {
        internal ulong Self, Generation, BindingGeneration, Offset, Number, OpenFlags;
        internal Fs.OperationToken Operation;
        internal Token OriginalFd;
        internal OSHostHandle Worker;
        internal long Error;
        internal uint Description, Binding, Requested, Whence;
        internal byte Phase, Kind;
        internal bool Positioned, Borrowed, RequireDirectory, Cancelling;
        internal Fs.Status Failure;
#if !NOSTDLIB_EFI || M16_027_PROBE
        // Only a Busy borrowed-backend open retains this bounded copied path.
        // It never retains/rereads an application pointer or enlarges025 layout.
        internal uint PathLength;
        internal fixed byte Path[(int)MaximumPath];
#endif
    }

    /// <summary>One exclusive, externally serialized attachment. Keep all
    /// metadata and the already mounted FS backing supervisor RW/NX and disjoint
    /// until DetachDisk succeeds. Structural audit deliberately survives device
    /// failure; it is not live I/O authority. Never copy a live instance.</summary>
    public struct DiskState
    {
        internal ulong Self, Epoch, NextGeneration;
        internal State* Owner;
        internal Fs.Mount* Mount;
        internal DiskBinding* Bindings;
        internal DiskRequest* Requests;
        internal uint NextPump;
    }
    public struct DiskAdmission
    {
        public Fs.Mount* Mount;
        public DiskBinding* Bindings;
        public DiskRequest* Requests;
        public ulong Epoch;
        public bool FirmwareExited, SingleOwnerValidated;
    }

    /// <summary>Attach eight zeroed stable records of each kind. Invalid alias,
    /// extent, capacity or ownership admission has no effects. This borrows an
    /// already ready FS; it starts no device, scheduler or user operation.</summary>
    public static bool AttachDisk(State* state, DiskState* disk, in DiskAdmission a)
    {
        if (!Audit(state) || state->Disk != null || !a.FirmwareExited || !a.SingleOwnerValidated ||
            a.Epoch == 0 || !DiskStorage(a.Mount, (ulong)sizeof(Fs.Mount)) ||
            a.Mount->Self != (ulong)a.Mount || !a.Mount->Ready || a.Mount->Input.Epoch != a.Epoch ||
            !DiskStorage(disk, (ulong)sizeof(DiskState)) ||
            !DiskStorage(a.Bindings, DiskSlots * (ulong)sizeof(DiskBinding)) ||
            !DiskStorage(a.Requests, DiskSlots * (ulong)sizeof(DiskRequest))) return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (BorrowedDiskMount(a.Mount) && !BorrowedDiskBaseStorage(state, a.Mount)) return false;
        if (state->WritableDisk != null && (!WritableBound(state) || state->WritableDisk->Provider->RequestActive ||
            !PairedDiskStorage(disk, a, state->WritableDisk, RetainedWritableAdmission(state->WritableDisk)))) return false;
#endif
        ulong* starts = stackalloc ulong[3] { (ulong)disk, (ulong)a.Bindings, (ulong)a.Requests };
        ulong* lengths = stackalloc ulong[3] { (ulong)sizeof(DiskState), DiskSlots * (ulong)sizeof(DiskBinding), DiskSlots * (ulong)sizeof(DiskRequest) };
        for (uint i = 0; i < 3; i++)
        {
            if (Overlap(starts[i], lengths[i], (ulong)state, (ulong)sizeof(State))) return false;
            for (uint j = 0; j < i; j++) if (Overlap(starts[i], lengths[i], starts[j], lengths[j])) return false;
            for (uint j = 0; j < 75; j++)
            {
                FsExtent(a.Mount, j, out ulong start, out ulong bytes);
                if (bytes != 0 && Overlap(starts[i], lengths[i], start, bytes)) return false;
            }
            if (!DiskAttachmentOutside(state, a.Mount, (void*)starts[i], lengths[i])) return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (BorrowedDiskMount(a.Mount) && !Fs.CanRetainStorage(a.Mount, a.Epoch, starts[i], lengths[i])) return false;
#endif
            for (ulong j = 0; j < lengths[i]; j++) if (((byte*)starts[i])[j] != 0) return false;
        }
        for (uint i = 0; i < a.Mount->Input.OperationCount; i++)
            if (a.Mount->Input.Operations[i].Storage.RangeCapacity < DiskCompletionBytes) return false;
        for (uint i = 0; i < 68; i++)
        {
            FsExtent(a.Mount, i, out ulong start, out ulong bytes);
            if (bytes != 0 && !DiskAttachmentOutside(state, a.Mount, (void*)start, bytes)) return false;
        }
        *disk = new() { Self = (ulong)disk, Epoch = a.Epoch, Owner = state, Mount = a.Mount, Bindings = a.Bindings, Requests = a.Requests };
        for (uint i = 0; i < DiskSlots; i++) disk->Requests[i].Self = (ulong)(disk->Requests + i);
        state->Disk = disk; return true;
    }

    /// <summary>Detach only after every description and pending/orphan record
    /// retired. FS/block lifetime stays with their native owner. Reject live
    /// records unchanged; success scrubs only this attachment's metadata.</summary>
    public static bool DetachDisk(State* state)
    {
        if (!Audit(state) || !DiskBound(state)) return false;
        DiskState* d = state->Disk;
        for (uint i = 0; i < DiskSlots; i++) if (d->Bindings[i].Active != 0 || d->Requests[i].Phase != 0) return false;
        for (uint i = 0; i < DiskSlots; i++) d->Requests[i] = default;
        state->Disk = null; *d = default; return true;
    }

    /// <summary>Select only explicit disk-root paths or existing disk fds.
    /// A null attachment returns NotHandled before user copies. The thread owner
    /// supplies its exact current generation and captured call. Paths are copied
    /// synchronously into FS scratch; output addresses are never retained.</summary>
    public static DiskResult BeginDisk(State* state, ulong owner, OSHostHandle worker, LinuxSyscallFrame call)
    {
        if (state == null || state->Disk == null) return new(DiskProgress.NotHandled);
        if (!Attached(state, owner) || !DiskBound(state) || !worker.IsValid) return new(DiskProgress.Rejected);
        byte kind = 0; bool directory = false; ulong flags = 0;
        byte* path = stackalloc byte[(int)MaximumPath]; uint length = 0;
        Token fd = default; uint description = 0, binding = 0;
        if (call.Number is 2 or 4 or 6 or 257)
        {
            long read = DiskReadPath(state, call.Number == 257 ? call.Argument1 : call.Argument0, path, out length, out directory, out bool absolute);
            if (read != 0) return new(DiskProgress.Immediate, read);
            if (!DiskPath(path, length)) return new(DiskProgress.NotHandled);
            if (state->Disk->Mount->IsRetired) return new(DiskProgress.Immediate, Io, failure: Fs.Status.Cancelled);
            kind = call.Number is 4 or 6 ? DiskStat : DiskOpen;
            if (kind == DiskOpen)
            {
                if (!absolute && call.Number == 257 && unchecked((uint)call.Argument0) != 0xffffff9c)
                    return new(DiskProgress.Immediate, Find(state, unchecked((uint)call.Argument0)) == null ? BadDescriptor : NotDirectory);
                flags = call.Number == 257 ? call.Argument2 : call.Argument1;
                if ((flags & (3UL | 0x40 | 0x200 | 0x400)) != 0) return new(DiskProgress.Immediate, ReadOnly);
                if ((flags & ~(NonBlock | CloseOnExec | LargeFile | NoFollow | 0x100UL)) != 0) return new(DiskProgress.Immediate, Invalid);
                if (FreeSlot(state, 0, uint.MaxValue) == state->Owner.DescriptorCount) return new(DiskProgress.Immediate, DescriptorTableFull);
                if (FreeDescription(state, uint.MaxValue) == state->Owner.DescriptionCount) return new(DiskProgress.Immediate, FileTableFull);
            }
        }
        else
        {
            if (call.Number is not (0 or 5 or 8 or 17)) return new(DiskProgress.NotHandled);
            Description* opened = Find(state, unchecked((uint)call.Argument0));
            if (opened == null || (Kind)opened->Type != Kind.Disk) return new(DiskProgress.NotHandled);
            if (!Capture(state, unchecked((uint)call.Argument0), out fd)) return new(DiskProgress.Rejected);
            if (state->Disk->Mount->IsRetired) return new(DiskProgress.Immediate, Io, failure: Fs.Status.Cancelled);
            description = state->Owner.Slots[fd.Number].Description - 1; binding = opened->Pipe;
            if (call.Number == 5)
            {
                Fs.FileInspection info = default;
                Fs.Status observed = Fs.Inspect(state->Disk->Mount, state->Disk->Bindings[binding].File, &info);
                if (observed == Fs.Status.Success) return new(DiskProgress.Immediate, DiskStatOutput(state, info, call.Argument1));
#if !NOSTDLIB_EFI || M16_027_PROBE
                if (observed == Fs.Status.Busy && BorrowedDiskMount(state->Disk->Mount)) kind = DiskFstat;
                else
#endif
                return new(DiskProgress.Immediate, Io, failure: observed);
            }
            else kind = call.Number == 8 ? DiskSeek : DiskRead;
            if (kind == DiskRead && (call.Argument2 > MaximumIo || call.Number == 17 && call.Argument3 > long.MaxValue)) return new(DiskProgress.Immediate, Invalid);
            if (kind == DiskSeek && call.Argument2 > 2) return new(DiskProgress.Immediate, Invalid);
            if (opened->Borrowers == MaximumDescriptors) return new(DiskProgress.Immediate, FileTableFull);
        }
        DiskState* disk = state->Disk; uint index = 0;
        for (; index < DiskSlots; index++) if (disk->Requests[index].Phase == 0) break;
        if (index == DiskSlots) return new(DiskProgress.Immediate, FileTableFull);
        if (disk->NextGeneration == ulong.MaxValue) return new(DiskProgress.Immediate, Overflow, failure: Fs.Status.GenerationExhausted);
        DiskRequest* r = disk->Requests + index;
        *r = new() { Self = (ulong)r, Generation = ++disk->NextGeneration, Worker = worker, Number = call.Number,
            Kind = kind, Phase = DiskQueued, RequireDirectory = directory, OpenFlags = flags };
        if (kind is DiskOpen or DiskStat)
        {
            Fs.OperationToken operation = default;
            Fs.Status started = Fs.BeginOpen(disk->Mount, new ReadOnlySpan<byte>(path, (int)length), &operation);
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (started == Fs.Status.Busy && BorrowedDiskMount(disk->Mount))
            {
                r->PathLength = length;
                for (uint i = 0; i < length; i++) r->Path[i] = path[i];
                return DiskObservation(disk, r);
            }
#endif
            if (started != Fs.Status.Pending) { ClearDiskRequest(r); return new(DiskProgress.Immediate, DiskErrno(started), failure: started); }
            r->Operation = operation; r->Phase = DiskWorking;
        }
        else
        {
            r->OriginalFd = fd; r->Description = description; r->Binding = binding;
            r->BindingGeneration = disk->Bindings[binding].Generation; r->Borrowed = true;
            state->Owner.Descriptions[description].Borrowers++;
            r->Positioned = call.Number == 17; r->Requested = (uint)call.Argument2;
            r->Offset = kind == DiskSeek ? call.Argument1 : call.Argument3; r->Whence = (uint)call.Argument2;
        }
        return DiskObservation(disk, r);
    }

    /// <summary>One bounded step, including detached cancellation records.
    /// Outstanding readonly I/O selects its exact operation owner; otherwise
    /// selection is round-robin. The caller scans at most eight ready token
    /// observations to wake exact wait generations. This never copies user
    /// bytes or commits cursors.</summary>
    public static DiskResult PumpDiskOne(State* state, ulong owner)
    {
        if (!Attached(state, owner) || !DiskBound(state)) return new(DiskProgress.Rejected);
        DiskState* d = state->Disk;
        uint next = d->NextPump;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (!BorrowedDiskMount(d->Mount))
#endif
        if (d->Mount->IoActive)
        {
            // Only the retained operation can consume this physical read.
            // Preserve the ordinary single StepDisk and advance the cursor
            // afterward so new work resumes fairly once the read is consumed.
            for (uint i = 0; i < DiskSlots; i++)
            {
                DiskRequest* request = d->Requests + i;
                Fs.OperationToken operation = request->Operation;
                if (request->Phase != 0 && operation.Index == d->Mount->IoIndex &&
                    operation.Generation == d->Mount->IoGeneration && DiskOperationLink(d, operation))
                { next = i; break; }
            }
        }
        for (uint n = 0; n < DiskSlots; n++)
        {
            uint i = (next + n) % DiskSlots;
            if (d->Requests[i].Phase == 0) continue;
#if !NOSTDLIB_EFI || M16_027_PROBE
            // Borrowed reads arbitrate through their original writable engine.
            // The readonly device counters below do not exist for this backend;
            // retain one selected turn without granting readonly replenishment.
            if (BorrowedDiskMount(d->Mount))
            { d->NextPump = (i + 1) % DiskSlots; return StepDisk(state, d->Requests + i); }
#endif
            var device = d->Mount->Input.Device;
            ulong epoch = device->Input.Epoch, generation = d->Requests[i].Generation;
            ulong submitted = device->Submitted, completed = device->Completed, copied = device->Copied, cancelled = device->Cancelled;
            d->NextPump = (i + 1) % DiskSlots;
            DiskResult result = StepDisk(state, d->Requests + i);
            bool advanced = result.Progress == DiskProgress.Pending && result.Worker.IsValid &&
                d->Requests[i].Generation == generation && !d->Requests[i].Cancelling &&
                d->Mount->Ready && d->Mount->Input.Device == device && device->Running && device->Input.Epoch == epoch &&
                device->Cancelled == cancelled && device->Submitted >= submitted && device->Submitted - submitted <= 1 &&
                device->Completed >= completed && device->Completed - completed <= 1 &&
                device->Copied >= copied && device->Copied - copied <= 1 &&
                (device->Submitted != submitted || device->Completed != completed || device->Copied != copied);
            return new(result.Progress, result.Value, result.Token, result.Worker, result.Failure, advanced);
        }
        return new(DiskProgress.Immediate);
    }

    /// <summary>Inspect one exact token without advancing I/O. A stale token
    /// cannot observe a reused record or acquire wake/copy authority.</summary>
    public static DiskResult PollDisk(State* state, ulong owner, DiskToken token)
    {
        if (!Attached(state, owner) || !DiskRequestFor(state, token, out DiskRequest* r)) return new(DiskProgress.Rejected);
#if !NOSTDLIB_EFI || M16_027_PROBE
        RefreshDiskAvailability(state->Disk, r);
#endif
        return DiskObservation(state->Disk, r);
    }

    /// <summary>Complete on the authenticated current worker, using its trusted
    /// original call supplied afresh. Revalidate the original fd generation
    /// before any output. Copy at most8KiB through shared descriptor scratch,
    /// recheck user mappings, scrub scratch, then commit a shared read cursor.
    /// The pending thread passes its original writable-prefix ceiling; a newly
    /// mapped tail cannot enlarge it. Zero-byte EOF remains valid at ceiling0.
    /// Direct legacy owners retain their own mapping-lifetime obligation.
    /// A close/reuse, cancel or exit grants no late output or position change.</summary>
    public static DiskResult CompleteDisk(State* state, ulong owner, OSHostHandle worker, DiskToken token, LinuxSyscallFrame call, uint outputBytes = DiskCompletionBytes)
    {
        if (outputBytes > DiskCompletionBytes || !Attached(state, owner) || !DiskRequestFor(state, token, out DiskRequest* r) ||
            !worker.IsValid || worker.Value != r->Worker.Value || call.Number != r->Number) return new(DiskProgress.Rejected);
        if (r->Borrowed && (!IsCurrent(state, r->OriginalFd) || unchecked((uint)call.Argument0) != r->OriginalFd.Number))
            return DrainDisk(state, r, BadDescriptor, true, Fs.Status.Cancelled);
#if !NOSTDLIB_EFI || M16_027_PROBE
        RefreshDiskAvailability(state->Disk, r);
#endif
        if (r->Phase is not (DiskReady or DiskError)) return DiskObservation(state->Disk, r);
        if (r->Phase == DiskError) return FinishDisk(state, r, r->Error, r->Failure);
        Fs.FileInspection live = default;
        if (r->Borrowed)
        {
            Fs.Status checkedFile = Fs.Inspect(state->Disk->Mount, state->Disk->Bindings[r->Binding].File, &live);
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (checkedFile == Fs.Status.Busy && BorrowedDiskMount(state->Disk->Mount)) return PauseDiskReady(state->Disk, r);
#endif
            if (checkedFile != Fs.Status.Success) return DrainDisk(state, r, Io, true, checkedFile);
        }
        if (r->Kind is DiskOpen or DiskStat) return CompleteDiskOpen(state, r, call, outputBytes);
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (r->Kind == DiskFstat) return FinishDisk(state, r, outputBytes < 144 ? Fault : DiskStatOutput(state, live, call.Argument1));
#endif
        Description* description = state->Owner.Descriptions + r->Description;
        if (r->Kind == DiskSeek) { description->Position = r->Offset; return FinishDisk(state, r, (long)r->Offset); }
        if (call.Argument2 != r->Requested || r->Positioned && call.Argument3 != r->Offset) return new(DiskProgress.Rejected);
        uint copied = 0; Fs.Status copiedStatus = Fs.Status.Success;
        if (r->Operation.IsInitialized)
        {
            copiedStatus = Fs.CopyRead(state->Disk->Mount, r->Operation,
                new Span<byte>(state->Owner.Scratch, (int)DiskCompletionBytes), &copied);
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (copiedStatus == Fs.Status.Busy && BorrowedDiskMount(state->Disk->Mount))
            { Clear(state->Owner.Scratch, state->Owner.ScratchBytes); return PauseDiskReady(state->Disk, r); }
#endif
            if (copiedStatus is not (Fs.Status.Success or Fs.Status.EndOfFile))
            { Clear(state->Owner.Scratch, state->Owner.ScratchBytes); return DrainDisk(state, r, Io, true, copiedStatus); }
            r->Operation = default;
        }
        long value = copied;
        if (copied > outputBytes || copied != 0 && !Output(state, call.Argument1, state->Owner.Scratch, copied)) value = Fault;
        Clear(state->Owner.Scratch, state->Owner.ScratchBytes);
        if (value >= 0 && !r->Positioned) description->Position = r->Offset + copied;
        return FinishDisk(state, r, value, copiedStatus);
    }

    /// <summary>Immediately revoke copy/wake and detach this worker. Pending
    /// DMA retains an orphan record and its original description borrow until
    /// PumpDiskOne drains it. Return Value is EINTR; caller owns restart policy.</summary>
    public static DiskResult CancelDisk(State* state, ulong owner, DiskToken token)
    {
        if (!Attached(state, owner) || !DiskRequestFor(state, token, out DiskRequest* r)) return new(DiskProgress.Rejected);
        return DrainDisk(state, r, -4, true, Fs.Status.Cancelled);
    }

    private static DiskResult StepDisk(State* state, DiskRequest* r)
    {
        DiskState* disk = state->Disk;
        if (r->Phase == DiskDraining) return DrainDisk(state, r, r->Error, !r->Worker.IsValid, r->Failure);
        if (r->Borrowed && !IsCurrent(state, r->OriginalFd)) return DrainDisk(state, r, BadDescriptor, false, Fs.Status.Cancelled);
#if !NOSTDLIB_EFI || M16_027_PROBE
        RefreshDiskAvailability(disk, r);
        if (r->Phase == DiskPausedReady)
        {
            Fs.Status available = Fs.PumpMount(disk->Mount);
            if (available == Fs.Status.Busy) return DiskObservation(disk, r);
            if (available != Fs.Status.Ready) return DrainDisk(state, r, Io, false, available);
            r->Phase = DiskReady; return DiskObservation(disk, r);
        }
#endif
        if (r->Phase is DiskReady or DiskError) return DiskObservation(disk, r);
        if (r->Phase == DiskQueued)
        {
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (r->Kind is DiskOpen or DiskStat)
            {
                Fs.OperationToken operation = default;
                Fs.Status begun = Fs.BeginOpen(disk->Mount, new ReadOnlySpan<byte>(r->Path, (int)r->PathLength), &operation);
                if (begun == Fs.Status.Busy) return DiskObservation(disk, r);
                if (begun != Fs.Status.Pending) return DrainDisk(state, r, DiskErrno(begun), false, begun);
                r->Operation = operation; r->Phase = DiskWorking; Clear(r->Path, MaximumPath); r->PathLength = 0;
                return DiskObservation(disk, r);
            }
#endif
            if (CursorDiskBusy(disk, r)) return DiskObservation(disk, r);
            Fs.FileInspection info = default;
            Fs.Status inspected = Fs.Inspect(disk->Mount, disk->Bindings[r->Binding].File, &info);
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (inspected == Fs.Status.Busy && BorrowedDiskMount(disk->Mount)) return DiskObservation(disk, r);
#endif
            if (inspected != Fs.Status.Success) return DrainDisk(state, r, Io, false, inspected);
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (r->Kind == DiskFstat) { r->Phase = DiskReady; return DiskObservation(disk, r); }
#endif
            Description* d = state->Owner.Descriptions + r->Description;
            if (r->Kind == DiskSeek)
            {
                ulong origin = r->Whence == 0 ? 0 : r->Whence == 1 ? d->Position : d->Bytes;
                long offset = unchecked((long)r->Offset);
                ulong magnitude = offset < 0 ? unchecked((ulong)(-(offset + 1))) + 1 : (ulong)offset;
                if (info.Info.IsDirectory) return DrainDisk(state, r, Unsupported, false, Fs.Status.IsDirectory);
                if (offset < 0 && magnitude > origin) return DrainDisk(state, r, Invalid, false, Fs.Status.InvalidOffset);
                if (offset >= 0 && magnitude > (ulong)long.MaxValue - origin) return DrainDisk(state, r, Overflow, false, Fs.Status.InvalidOffset);
                r->Offset = offset < 0 ? origin - magnitude : origin + magnitude; r->Phase = DiskReady;
                return DiskObservation(disk, r);
            }
            if (info.Info.IsDirectory) return DrainDisk(state, r, -21, false, Fs.Status.IsDirectory);
            if (!r->Positioned) r->Offset = d->Position;
            if (r->Requested == 0 || r->Offset >= d->Bytes) { r->Phase = DiskReady; return DiskObservation(disk, r); }
            uint wanted = r->Requested < DiskCompletionBytes ? r->Requested : DiskCompletionBytes;
            Fs.OperationToken op = default;
            Fs.Status started = Fs.BeginRead(disk->Mount, disk->Bindings[r->Binding].File, r->Offset, wanted, &op);
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (started == Fs.Status.Busy && BorrowedDiskMount(disk->Mount)) return DiskObservation(disk, r);
#endif
            if (started != Fs.Status.Pending) return DrainDisk(state, r, DiskErrno(started), false, started);
            r->Operation = op; r->Phase = DiskWorking;
            // BeginRead only reserves the range. Use this selected turn's one
            // ordinary Poll to submit its first sector; never poll it twice.
        }
        Fs.Status status = Fs.Poll(disk->Mount, r->Operation);
        if (status == Fs.Status.Ready) { r->Phase = DiskReady; return DiskObservation(disk, r); }
        if (status is Fs.Status.Pending or Fs.Status.Busy) return DiskObservation(disk, r);
        return DrainDisk(state, r, DiskErrno(status), false, status);
    }

    private static DiskResult CompleteDiskOpen(State* state, DiskRequest* r, LinuxSyscallFrame call, uint outputBytes)
    {
        Fs.OpenResult file = default;
        Fs.Status copied = Fs.CopyOpen(state->Disk->Mount, r->Operation, &file);
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (copied == Fs.Status.Busy && BorrowedDiskMount(state->Disk->Mount)) return PauseDiskReady(state->Disk, r);
#endif
        if (copied != Fs.Status.Success) return DrainDisk(state, r, Io, true, copied);
        r->Operation = default; Fs.FileInspection info = new() { Info = file.Info };
        Fs.Status checkedFile = Fs.Inspect(state->Disk->Mount, file.File, &info);
        long value = checkedFile == Fs.Status.Success ? 0 : Io;
        if (value == 0 && r->RequireDirectory && !info.Info.IsDirectory) value = NotDirectory;
        if (value == 0 && r->Kind == DiskStat) value = outputBytes < 144 ? Fault : DiskStatOutput(state, info, call.Argument1);
        if (value == 0 && r->Kind == DiskOpen)
        {
            if (info.Info.IsDirectory) value = Unsupported; // No public directory-fd API in this slice.
            else
            {
                uint fd = FreeSlot(state, 0, uint.MaxValue), description = FreeDescription(state, uint.MaxValue), binding = 0;
                for (; binding < DiskSlots; binding++) if (state->Disk->Bindings[binding].Active == 0) break;
                value = fd == state->Owner.DescriptorCount ? DescriptorTableFull :
                    description == state->Owner.DescriptionCount || binding == DiskSlots ? FileTableFull : 0;
                if (value == 0)
                {
                    state->Disk->Bindings[binding] = new() { File = file.File, Metadata = info, Generation = r->Generation, Description = description, Active = 1 };
                    state->Owner.Descriptions[description] = new() { Type = (uint)Kind.Disk, References = 1,
                        Flags = (uint)r->OpenFlags & (NonBlock | LargeFile | NoFollow), Pipe = binding, Bytes = info.Info.Length, Inode = info.Info.Inode };
                    Assign(state, fd, description, (r->OpenFlags & CloseOnExec) != 0 ? 1U : 0U);
                    return FinishDisk(state, r, fd);
                }
            }
        }
        Fs.Status closed = Fs.Close(state->Disk->Mount, file.File);
        if (closed != Fs.Status.Success)
        {
            // A failed/uncertain close must retain its file token until the FS
            // retirement receipt. The detached record owns this temporary file.
            uint binding = 0;
            for (; binding < DiskSlots; binding++) if (state->Disk->Bindings[binding].Active == 0) break;
            if (binding == DiskSlots) return new(DiskProgress.Rejected, Io, failure: closed);
            state->Disk->Bindings[binding] = new() { File = file.File, Metadata = info, Generation = r->Generation, Description = uint.MaxValue, Active = 1 };
            r->Binding = binding; r->BindingGeneration = r->Generation; r->Error = Io; r->Failure = closed; r->Worker = default; r->Phase = DiskDraining;
            return DiskObservation(state->Disk, r);
        }
        return FinishDisk(state, r, value, checkedFile);
    }

    private static DiskResult DrainDisk(State* state, DiskRequest* r, long value, bool detach, Fs.Status failure)
    {
        r->Phase = DiskDraining; r->Error = value; r->Failure = failure;
        if (detach) r->Worker = default;
        DiskState* disk = state->Disk;
        if (r->Operation.IsInitialized)
        {
            Fs.Status status = DiskRetired(disk) ? Fs.Status.Cancelled :
                r->Cancelling ? Fs.Poll(disk->Mount, r->Operation) : Fs.Cancel(disk->Mount, r->Operation);
            r->Cancelling = true;
            if (status != Fs.Status.Cancelled)
            {
                if (status is not (Fs.Status.Pending or Fs.Status.Busy) && r->Failure is Fs.Status.Success or Fs.Status.Cancelled) r->Failure = status;
                return DiskObservation(disk, r);
            }
            r->Operation = default;
        }
        if (!r->Borrowed && r->BindingGeneration != 0)
        {
            DiskBinding* b = disk->Bindings + r->Binding;
            if (!DiskRetired(disk) && Fs.Close(disk->Mount, b->File) != Fs.Status.Success) return DiskObservation(disk, r);
            *b = default; r->BindingGeneration = 0;
        }
        if (!r->Worker.IsValid) return FinishDisk(state, r, value, r->Failure);
        r->Phase = DiskError; return DiskObservation(disk, r);
    }

    private static bool CursorDiskBusy(DiskState* disk, DiskRequest* request)
    {
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (request->Kind == DiskFstat) return false;
#endif
        if (request->Kind == DiskRead && request->Positioned) return false;
        for (uint i = 0; i < DiskSlots; i++)
        {
            DiskRequest* other = disk->Requests + i;
            if (other->Phase == 0 || other->Phase == DiskDraining || !other->Borrowed || other->Binding != request->Binding ||
                other->Generation >= request->Generation || other->Kind == DiskRead && other->Positioned) continue;
            return true;
        }
        return false;
    }

    private static DiskResult FinishDisk(State* state, DiskRequest* r, long value, Fs.Status failure = Fs.Status.Success)
    {
        if (r->Borrowed && !DropDescription(state, r->Description, true))
        {
            r->Phase = DiskDraining; r->Worker = default; r->Error = Io;
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (BorrowedDiskMount(state->Disk->Mount))
            {
                // Cleanup cannot replace an already known cancellation/closed
                // fd result merely because C owns a temporary writer turn.
                r->Error = value; r->Failure = failure;
                return DiskObservation(state->Disk, r);
            }
#endif
            if (r->Failure == Fs.Status.Success) r->Failure = Fs.Status.InvalidState;
            return DiskObservation(state->Disk, r);
        }
        ClearDiskRequest(r);
        return new(DiskProgress.Immediate, value, failure: failure);
    }
    private static void ClearDiskRequest(DiskRequest* r) { ulong self = r->Self; *r = default; r->Self = self; }
#if !NOSTDLIB_EFI || M16_027_PROBE
    private static bool BorrowedDiskMount(Fs.Mount* mount) => mount->ReadPort != null;
    private static DiskResult PauseDiskReady(DiskState* disk, DiskRequest* request)
    { request->Phase = DiskPausedReady; return DiskObservation(disk, request); }
    // PumpMount on an already mounted borrowed FS only inspects availability;
    // it does not submit/poll I/O, copy a range or consume its typed result.
    private static void RefreshDiskAvailability(DiskState* disk, DiskRequest* request)
    {
        if (!BorrowedDiskMount(disk->Mount) || request->Phase is not (DiskReady or DiskPausedReady)) return;
        Fs.Status status = Fs.PumpMount(disk->Mount);
        if (status == Fs.Status.Busy) request->Phase = DiskPausedReady;
        else if (status == Fs.Status.Ready) request->Phase = DiskReady;
    }
#endif
    private static DiskResult DiskObservation(DiskState* disk, DiskRequest* r)
    {
        uint index = (uint)(r - disk->Requests);
        return new(r->Phase is DiskReady or DiskError ? DiskProgress.Ready : r->Phase == DiskDraining ? DiskProgress.Draining : DiskProgress.Pending,
            r->Error, new((ulong)disk, disk->Epoch, r->Generation, index), r->Worker, r->Failure);
    }
    private static bool DiskRequestFor(State* state, DiskToken token, out DiskRequest* r)
    {
        r = null;
        if (!DiskBound(state) || token.Owner != (ulong)state->Disk || token.Epoch != state->Disk->Epoch || token.Index >= DiskSlots || token.Generation == 0) return false;
        DiskRequest* candidate = state->Disk->Requests + token.Index;
        if (candidate->Phase == 0 || candidate->Generation != token.Generation) return false;
        r = candidate; return true;
    }
    private static bool DiskStorage(void* value, ulong bytes, ulong alignment = 8)
    {
        const ulong end = 0x0000800000000000;
        ulong start = (ulong)value;
        return start >= 4096 && start < end && bytes != 0 && bytes <= end - start && (start & (alignment - 1)) == 0;
    }

    /// <summary>Structural full-tree external-buffer query for the owning core
    /// and thread admission guards. It supplies no current-device/user-copy or
    /// mapping authority. A null attachment imposes no disk exclusions. A
    /// borrowed retired FS grants no new backing borrow; thread-local cleanup
    /// uses the separately constrained DiskThreadStorageOutside query.
    /// No I/O, callback, allocation or retained caller pointer is introduced.</summary>
    internal static bool DiskCanRetainStorage(State* state, ulong address, ulong bytes)
    {
        if (state == null) return false;
        if (state->Disk == null) return true;
        if (!DiskStorage((void*)address, bytes, 1) || !DiskBound(state)) return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (!AuditDiskPair(state) || !PairedDiskExternal(state, address, bytes)) return false;
#endif
        DiskState* d = state->Disk;
        if (Overlap(address, bytes, (ulong)d, (ulong)sizeof(DiskState)) ||
            Overlap(address, bytes, (ulong)d->Bindings, DiskSlots * (ulong)sizeof(DiskBinding)) ||
            Overlap(address, bytes, (ulong)d->Requests, DiskSlots * (ulong)sizeof(DiskRequest))) return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (BorrowedDiskMount(d->Mount)) return !d->Mount->IsRetired && Fs.CanRetainStorage(d->Mount, d->Epoch, address, bytes);
#endif
        for (uint i = 0; i < 75; i++)
        { FsExtent(d->Mount, i, out ulong held, out ulong count); if (count != 0 && Overlap(address, bytes, held, count)) return false; }
        return true;
    }

    /// <summary>Structural process-storage exclusion for retained thread
    /// cleanup. A retired borrowed mount may exclude only its retained local
    /// metadata/scratch and numeric port extent after its requests/files are
    /// scrubbed. No backend is dereferenced or borrowed in that branch. A live
    /// writable attachment requires the original pair proof and cannot use
    /// this local retirement path. This grants no I/O, copy, mapping or new
    /// attachment authority; all live cases retain the strict full-tree query.</summary>
    internal static bool DiskThreadStorageOutside(State* state, ulong address, ulong bytes)
    {
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (!DiskStorage(state, (ulong)sizeof(State))) return false;
        if (state->Disk != null)
        {
            DiskState* d = state->Disk;
            if (!DiskStorage(d, (ulong)sizeof(DiskState)) || !DiskStorage(d->Mount, (ulong)sizeof(Fs.Mount))) return false;
            if (BorrowedDiskMount(d->Mount) && d->Mount->IsRetired)
            {
                if (state->WritableDisk != null || !DiskStorage((void*)address, bytes, 1) || !DiskBound(state)) return false;
                for (uint i = 0; i < DiskExtentCount; i++)
                {
                    DiskLocalExtent(d, i, out ulong held, out ulong count);
                    if (count != 0 && (!DiskStorage((void*)held, count, 1) || Overlap(address, bytes, held, count))) return false;
                }
                return true;
            }
        }
#endif
        return DiskCanRetainStorage(state, address, bytes);
    }

#if !NOSTDLIB_EFI || M16_027_PROBE
    internal const uint MaximumThreadStorageRanges = 8 + 9 +
        2 * OS.Kernel.Threading.KernelThreadTable.MaximumSlots + 2 * LinuxThreadServices.MaximumThreads;

    /// <summary>Check a bounded, flat start/byte-count inventory against the
    /// original paired read/write descriptor tree. The original serialized
    /// supervisor keeps all backing and this input mapped through return.
    /// Whole-tree admission runs once; each nonempty range retains the complete
    /// provider engine/filesystem port queries and every exclusion. Provider
    /// and filesystem inventories each share one synchronous retained audit.
    /// Empty extents are skipped as in process inventory. Invalid/stale pairs
    /// fail closed. No callback, mutation, allocation, I/O, output, retained
    /// input or escaping validation token is introduced; no later authority
    /// follows. Other attachment/retirement combinations use their old path.</summary>
    internal static bool PairedThreadStorageOutside(State* state, ReadOnlySpan<ulong> ranges)
    {
        if (ranges.Length == 0 || (ranges.Length & 1) != 0 || ranges.Length > 2 * MaximumThreadStorageRanges ||
            !DiskStorage(state, (ulong)sizeof(State)) || state->Initialized != 1 ||
            state->Disk == null || state->WritableDisk == null || !AuditWritableDisk(state) || !AuditDiskPair(state)) return false;
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (state->Sockets == null ? state->Epoll != null : !TransportShape(state)) return false;
#endif
        DiskState* d = state->Disk;
        for (int i = 0; i < ranges.Length; i += 2)
        {
            ulong start = ranges[i], bytes = ranges[i + 1];
            if (bytes == 0) continue;
            if (!DiskStorage((void*)start, bytes, 1) || !WritableBaseOutside(state, start, bytes) ||
                !PairedDiskLocalOutside(state, start, bytes)) return false;
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
            if (!TransportOutsideRange(state, start, bytes)) return false;
#endif
        }
        WritableDiskState* w = state->WritableDisk;
        return W.CanRetainStorageRanges(w->Provider, w->Epoch, ranges) &&
            Fs.CanRetainStorageRanges(d->Mount, d->Epoch, ranges);
    }
#endif

    private static bool DiskAttachmentOutside(State* state, Fs.Mount* mount, void* pointer, ulong bytes)
    {
#if !NOSTDLIB_EFI || M16_027_PROBE
        return SocketOutsideCore(state, pointer, bytes, writablePair: BorrowedDiskMount(mount));
#else
        return SocketOutside(state, pointer, bytes);
#endif
    }

#if !NOSTDLIB_EFI || M16_027_PROBE
    private static DiskAdmission RetainedDiskAdmission(DiskState* d) => new()
    { Mount = d->Mount, Bindings = d->Bindings, Requests = d->Requests, Epoch = d->Epoch };

    private static WritableDiskAdmission RetainedWritableAdmission(WritableDiskState* d) => new()
    { Provider = d->Provider, Bindings = d->Bindings, BindingCount = d->BindingCount, Epoch = d->Epoch };

    // Leaf structural pair proof: neither global Audit nor either global
    // external-buffer query is called recursively. Dirty/orphan backing stays
    // retained; each operation separately requires its live/copy authority.
    private static bool AuditDiskPair(State* s)
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
    {
        var stamp = OS.Kernel.Diagnostics.WritableStorageTiming.Enter(OS.Kernel.Diagnostics.WritableStorageTiming.Bucket.Pair);
        bool result = AuditDiskPairCore(s);
        OS.Kernel.Diagnostics.WritableStorageTiming.Leave(stamp);
        return result;
    }
    private static bool AuditDiskPairCore(State* s)
#endif
    {
        if (s->Disk == null || s->WritableDisk == null) return true;
        return DiskBound(s) && WritableBound(s) &&
            PairedDiskStorage(s->Disk, RetainedDiskAdmission(s->Disk), s->WritableDisk, RetainedWritableAdmission(s->WritableDisk));
    }

    private static bool PairedDiskStorage(DiskState* read, in DiskAdmission r, WritableDiskState* write, in WritableDiskAdmission w)
    {
        if (!DiskStorage(read, (ulong)sizeof(DiskState)) || !DiskStorage(write, (ulong)sizeof(WritableDiskState)) ||
            !DiskStorage(r.Mount, (ulong)sizeof(Fs.Mount)) || !DiskStorage(w.Provider, (ulong)sizeof(W.State)) ||
            !DiskStorage(r.Bindings, DiskSlots * (ulong)sizeof(DiskBinding)) || !DiskStorage(r.Requests, DiskSlots * (ulong)sizeof(DiskRequest)) ||
            w.BindingCount is 0 or > MaximumDescriptors || !DiskStorage(w.Bindings, w.BindingCount * (ulong)sizeof(WritableDiskBinding)) ||
            r.Epoch == 0 || w.Epoch == 0 || r.Mount->Input.Epoch != r.Epoch || !BorrowedDiskMount(r.Mount) || r.Mount->IsRetired) return false;
        var binding = r.Mount->PortBinding;
        if (!P.MatchesRetainedEngine(r.Mount->ReadPort, binding.Epoch, binding.EngineOwner, binding.EngineEpoch) ||
            !W.MatchesRetainedEngine(w.Provider, w.Epoch, binding.EngineOwner, binding.EngineEpoch)) return false;
        ulong writableBytes = w.BindingCount * (ulong)sizeof(WritableDiskBinding);
        ulong* extents = stackalloc ulong[(int)DiskExtentCount * 2];
        extents[0] = (ulong)write; extents[1] = (ulong)sizeof(WritableDiskState);
        extents[2] = (ulong)w.Bindings; extents[3] = writableBytes;
        // Authenticate the FS-owned fields before FsExtent reads any of them.
        // Reuse this bounded scratch for the later readonly candidate inventory.
        if (!Fs.CanRetainStorageRanges(r.Mount, r.Epoch, new ReadOnlySpan<ulong>(extents, 4))) return false;
        for (uint i = 0; i < DiskExtentCount; i++)
        {
            ulong start, bytes;
            if (i == 0) { start = (ulong)read; bytes = (ulong)sizeof(DiskState); }
            else if (i == 1) { start = (ulong)r.Bindings; bytes = DiskSlots * (ulong)sizeof(DiskBinding); }
            else if (i == 2) { start = (ulong)r.Requests; bytes = DiskSlots * (ulong)sizeof(DiskRequest); }
            else FsExtent(r.Mount, i - 3, out start, out bytes);
            extents[i * 2] = start; extents[i * 2 + 1] = bytes;
        }
        if (!W.CanRetainStorageRanges(w.Provider, w.Epoch, new ReadOnlySpan<ulong>(extents, (int)DiskExtentCount * 2))) return false;
        for (uint i = 0; i < DiskExtentCount; i++)
        {
            ulong start = extents[i * 2], bytes = extents[i * 2 + 1];
            if (bytes != 0 && (Overlap(start, bytes, (ulong)write, (ulong)sizeof(WritableDiskState)) ||
                Overlap(start, bytes, (ulong)w.Bindings, writableBytes))) return false;
        }
        return true;
    }

    // Both external-buffer queries exclude the complete paired private tree.
    // Pair audit already authenticated the common backend and its full extents;
    // neither query may accept the other client's local metadata or buffers.
    private static bool PairedDiskExternal(State* s, ulong address, ulong bytes)
    {
        if (s->Disk == null || s->WritableDisk == null) return true;
        WritableDiskState* w = s->WritableDisk;
        return W.CanRetainStorage(w->Provider, w->Epoch, address, bytes) && PairedDiskLocalOutside(s, address, bytes);
    }

    // Only the same callback-free paired traversal may use this leaf after
    // auditing its retained owners. The checked scalar entry above retains
    // its provider query; the batch checks every candidate through W once.
    private static bool PairedDiskLocalOutside(State* s, ulong address, ulong bytes)
    {
        WritableDiskState* w = s->WritableDisk;
        if (Overlap(address, bytes, (ulong)w, (ulong)sizeof(WritableDiskState)) ||
            Overlap(address, bytes, (ulong)w->Bindings, w->BindingCount * (ulong)sizeof(WritableDiskBinding))) return false;
        for (uint i = 0; i < DiskExtentCount; i++)
        {
            DiskLocalExtent(s->Disk, i, out ulong start, out ulong count);
            if (count != 0 && Overlap(address, bytes, start, count)) return false;
        }
        return true;
    }

    private static bool BorrowedDiskBaseStorage(State* state, Fs.Mount* mount)
    {
        var a = state->Owner;
        ulong* extents = stackalloc ulong[26]
        {
            (ulong)state, (ulong)sizeof(State), (ulong)a.Slots, a.DescriptorCount * (ulong)sizeof(Slot),
            (ulong)a.Descriptions, a.DescriptionCount * (ulong)sizeof(Description), (ulong)a.Pipes, a.PipeCount * (ulong)sizeof(LinuxPipe),
            (ulong)a.PipeBuffers, a.PipeCount * a.PipeCapacity, (ulong)a.Snapshots, a.DescriptionCount * SnapshotBytes,
            (ulong)a.Scratch, a.ScratchBytes, (ulong)a.Memory, (ulong)sizeof(LinuxMemoryServices.State),
            (ulong)a.Memory->Memory.Pool, a.Memory->Memory.PoolPages * 4096UL,
            (ulong)a.Memory->Memory.Tables, a.Memory->Memory.TablePages * 4096UL,
            (ulong)a.Memory->Memory.CandidateTables, a.Memory->Memory.TablePages * 4096UL,
            (ulong)a.Memory->Memory.StaticRanges, a.Memory->Memory.StaticCount * (ulong)sizeof(X64UserMappingRange),
            (ulong)a.Views, a.ViewCount * (ulong)sizeof(View)
        };
        if (!Fs.CanRetainStorageRanges(mount, mount->Input.Epoch, new ReadOnlySpan<ulong>(extents, 26))) return false;
        for (uint i = 0; i < a.ViewCount; i++)
            if (!Fs.CanRetainStorage(mount, mount->Input.Epoch, (ulong)a.Views[i].Path, a.Views[i].PathBytes) ||
                a.Views[i].Bytes != 0 && !Fs.CanRetainStorage(mount, mount->Input.Epoch, (ulong)a.Views[i].Data, a.Views[i].Bytes)) return false;
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (!TransportStorageOutside(state, mount, null, 0, null, null, 0)) return false;
        var sockets = state->Sockets;
        if (sockets != null && (!Fs.CanRetainStorage(mount, mount->Input.Epoch, (ulong)sockets, (ulong)sizeof(LinuxSocketServices.State)) ||
            !Fs.CanRetainStorage(mount, mount->Input.Epoch, (ulong)sockets->Slots, sockets->Count * (ulong)sizeof(LinuxSocketServices.Endpoint)))) return false;
        var epoll = state->Epoll;
        if (epoll != null && (!Fs.CanRetainStorage(mount, mount->Input.Epoch, (ulong)epoll, (ulong)sizeof(LinuxEpollServices.State)) ||
            !Fs.CanRetainStorage(mount, mount->Input.Epoch, (ulong)epoll->Ports, epoll->PortCount * (ulong)sizeof(LinuxEpollServices.Port)) ||
            !Fs.CanRetainStorage(mount, mount->Input.Epoch, (ulong)epoll->Registrations, epoll->RegistrationCount * (ulong)sizeof(LinuxEpollServices.Registration)))) return false;
#endif
        return true;
    }
#endif
    private static bool DiskBound(State* state)
    {
        if (!DiskStorage(state, (ulong)sizeof(State))) return false;
        DiskState* d = state->Disk;
        if (!DiskStorage(d, (ulong)sizeof(DiskState)) || d->Self != (ulong)d || d->Owner != state || d->Epoch == 0 ||
            !DiskStorage(d->Mount, (ulong)sizeof(Fs.Mount)) || !DiskStorage(d->Bindings, DiskSlots * (ulong)sizeof(DiskBinding)) ||
            !DiskStorage(d->Requests, DiskSlots * (ulong)sizeof(DiskRequest))) return false;
        Fs.Mount* m = d->Mount; var a = m->Input;
        if (m->Self != (ulong)m || a.Epoch != d->Epoch || m->Phase is < 2 or > 5 ||
            a.OperationCount is < 2 or > Fs.MaximumSlots || a.FileCount is < 2 or > Fs.MaximumSlots || a.CacheCount is 0 or > Fs.MaximumCacheSlots ||
            !DiskStorage(a.Operations, a.OperationCount * (ulong)sizeof(Fs.Operation)) ||
            !DiskStorage(a.Files, a.FileCount * (ulong)sizeof(Fs.FileHandle)) ||
            !DiskStorage(a.Cache, a.CacheCount * (ulong)sizeof(Fs.CacheSlot))) return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (BorrowedDiskMount(m))
        {
            if (a.Device != null || !m->IsRetired && !BorrowedDiskBaseStorage(state, m)) return false;
        }
        else
#endif
        {
        if (!DiskStorage(a.Device, (ulong)sizeof(OS.Storage.ContinuingReadOnlyBlockService.State))) return false;
        var device = a.Device; var pool = device->Input.Pool;
        if (device->Self != (ulong)device || device->Input.Epoch != m->DeviceEpoch || device->PoolEpoch != m->PoolEpoch ||
            !DiskStorage(pool, (ulong)sizeof(OS.Drivers.DmaBufferPool)) || pool->Self != (ulong)pool || pool->Epoch != m->PoolEpoch ||
            pool->Count is 0 or > 64 || !DiskStorage(pool->Slots, pool->Count * (ulong)sizeof(OS.Drivers.DmaBufferSlot))) return false;
        }
        for (uint i = 0; i < a.OperationCount; i++)
        {
            Fs.Operation* o = a.Operations + i; var s = o->Storage;
            if (o->Self != (ulong)o || o->Epoch != d->Epoch || o->Index != i ||
                s.HistoryCapacity is 0 or > Fs.MaximumClusters || s.RangeCapacity is < DiskCompletionBytes or > Fs.MaximumRangeBytes ||
                !DiskStorage(s.Path, 256, 1) || !DiskStorage(s.Sector, 512, 1) || !DiskStorage(s.Name, 520, 2) ||
                !DiskStorage(s.History, s.HistoryCapacity * 4UL, 4) || !DiskStorage(s.Range, s.RangeCapacity, 1)) return false;
        }
        for (uint i = 0; i < a.FileCount; i++)
        {
            Fs.FileHandle* f = a.Files + i;
            if (f->Self != (ulong)f || f->Epoch != d->Epoch || f->ClusterCapacity is 0 or > Fs.MaximumClusters ||
                !DiskStorage(f->Clusters, f->ClusterCapacity * 4UL, 4)) return false;
        }
        for (uint i = 0; i < a.CacheCount; i++) if (!DiskStorage(a.Cache[i].Data, 512, 1)) return false;
        ulong* starts = stackalloc ulong[3] { (ulong)d, (ulong)d->Bindings, (ulong)d->Requests };
        ulong* bytes = stackalloc ulong[3] { (ulong)sizeof(DiskState), DiskSlots * (ulong)sizeof(DiskBinding), DiskSlots * (ulong)sizeof(DiskRequest) };
        for (uint i = 0; i < 3; i++)
        {
            if (Overlap(starts[i], bytes[i], (ulong)state, (ulong)sizeof(State)) || Overlap(starts[i], bytes[i], (ulong)m, (ulong)sizeof(Fs.Mount))) return false;
            for (uint j = 0; j < i; j++) if (Overlap(starts[i], bytes[i], starts[j], bytes[j])) return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (BorrowedDiskMount(m))
            {
                if (!m->IsRetired && !Fs.CanRetainStorage(m, d->Epoch, starts[i], bytes[i])) return false;
                for (uint j = 0; j < 68; j++)
                { FsExtent(m, j, out ulong local, out ulong count); if (count != 0 && Overlap(starts[i], bytes[i], local, count)) return false; }
            }
#endif
        }
        // This is structural ownership, not device liveness or I/O admission.
        return !m->IsRetired || DiskRetired(d);
    }

    private static bool DiskRetired(DiskState* d)
    {
        Fs.Mount* m = d->Mount;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (BorrowedDiskMount(m))
        {
            if (!m->IsRetired || m->IoActive) return false;
        }
        else
#endif
        {
        var device = m->Input.Device;
        if (!m->IsRetired || m->IoActive || !device->Retired || !device->Receipt.ResetComplete ||
            !device->Receipt.BusMasterDisabled || !device->Receipt.DmaReleased || !device->ReturnedToCpu || !device->Scrubbed) return false;
        }
        for (uint i = 0; i < m->Input.OperationCount; i++) if (m->Input.Operations[i].Phase != 0) return false;
        for (uint i = 0; i < m->Input.FileCount; i++) if (m->Input.Files[i].Phase != 0 || m->Input.Files[i].References != 0) return false;
        return true;
    }

    private static bool DiskFileLink(DiskState* d, Fs.FileToken file)
    {
        if (file.Owner != (ulong)d->Mount || file.Epoch != d->Epoch || file.Generation == 0 || file.Index >= d->Mount->Input.FileCount) return false;
        Fs.FileHandle* f = d->Mount->Input.Files + file.Index;
        return f->Self == (ulong)f && f->Epoch == d->Epoch && f->Generation == file.Generation;
    }

    private static bool DiskOperationLink(DiskState* d, Fs.OperationToken operation)
    {
        if (operation.Owner != (ulong)d->Mount || operation.Epoch != d->Epoch || operation.Generation == 0 || operation.Index >= d->Mount->Input.OperationCount) return false;
        Fs.Operation* o = d->Mount->Input.Operations + operation.Index;
        return o->Self == (ulong)o && o->Epoch == d->Epoch && o->Generation == operation.Generation;
    }

    // Core Audit invokes this only after validating its own base storage. Never
    // use FS.Ready here: failed devices still require audited descriptor cleanup.
    private static bool AuditDisk(State* state)
    {
        if (state->Disk == null) return true;
        if (!DiskBound(state)) return false;
        DiskState* disk = state->Disk;
        for (uint i = 0; i < DiskSlots; i++)
        {
            DiskBinding* b = disk->Bindings + i; DiskRequest* r = disk->Requests + i;
            if (b->Active > 1 || b->Active != 0 && (!DiskFileLink(disk, b->File) || b->Generation == 0 || b->Generation > disk->NextGeneration)) return false;
            if (b->Active == 0 && (b->File.IsInitialized || b->Generation != 0)) return false;
            if (b->Active != 0 && b->Description != uint.MaxValue)
            {
                if (b->Description >= state->Owner.DescriptionCount) return false;
                Description d = state->Owner.Descriptions[b->Description];
                if ((Kind)d.Type != Kind.Disk || d.Pipe != i || d.Inode != b->Metadata.Info.Inode || d.Bytes != b->Metadata.Info.Length || d.Data != null) return false;
            }
            if (b->Active != 0)
            {
                Fs.FileHandle* file = disk->Mount->Input.Files + b->File.Index;
                Fs.Metadata info = file->Info;
                if (b->Metadata.Info.Inode != info.Inode || b->Metadata.Info.Length != info.Length ||
                    b->Metadata.Info.Attributes != info.Attributes || b->Metadata.Info.FirstCluster != info.FirstCluster ||
                    b->Metadata.Info.ModifiedSeconds != info.ModifiedSeconds) return false;
                if (!disk->Mount->IsRetired && b->Description != uint.MaxValue &&
                    (file->Phase != 2 || b->Metadata.AllocationKnown != !info.IsDirectory ||
                    b->Metadata.AllocatedBlocks != (info.IsDirectory ? 0UL : (ulong)file->ClusterCount * disk->Mount->Volume.SectorsPerCluster))) return false;
                if (b->Description == uint.MaxValue)
                {
                    uint owners = 0;
                    for (uint j = 0; j < DiskSlots; j++) if (disk->Requests[j].Phase == DiskDraining && !disk->Requests[j].Borrowed &&
                        disk->Requests[j].Binding == i && disk->Requests[j].BindingGeneration == b->Generation) owners++;
                    if (owners != 1) return false;
                }
            }
            bool paused = false, fstat = false;
#if !NOSTDLIB_EFI || M16_027_PROBE
            paused = BorrowedDiskMount(disk->Mount) && r->Phase == DiskPausedReady;
            fstat = BorrowedDiskMount(disk->Mount) && r->Kind == DiskFstat && r->Number == 5;
            if (r->PathLength >= MaximumPath || r->PathLength != 0 &&
                (!BorrowedDiskMount(disk->Mount) || r->Phase != DiskQueued || r->Kind is not (DiskOpen or DiskStat))) return false;
#endif
            if (r->Self != (ulong)r || r->Phase > DiskError && !paused) return false;
            if (r->Phase == 0)
            { if (r->Generation != 0 || r->Operation.IsInitialized || r->Borrowed || r->Worker.IsValid) return false; continue; }
            if (r->Generation == 0 || r->Generation > disk->NextGeneration || (r->Kind is < DiskOpen or > DiskSeek) && !fstat ||
                !r->Worker.IsValid && r->Phase != DiskDraining) return false;
            if (r->Kind == DiskSeek && r->Number != 8 || r->Kind == DiskRead && r->Number != (r->Positioned ? 17UL : 0) ||
                r->Kind == DiskOpen && r->Number is not (2 or 257) || r->Kind == DiskStat && r->Number is not (4 or 6)) return false;
            if (r->Borrowed != (r->Kind is DiskRead or DiskSeek || fstat)) return false;
            if (r->Operation.IsInitialized && !DiskOperationLink(disk, r->Operation)) return false;
            if (r->Borrowed && (r->Description >= state->Owner.DescriptionCount || r->Binding >= DiskSlots ||
                disk->Bindings[r->Binding].Active != 1 || disk->Bindings[r->Binding].Generation != r->BindingGeneration ||
                disk->Bindings[r->Binding].Description != r->Description || r->OriginalFd.Owner != state ||
                r->OriginalFd.Number >= state->Owner.DescriptorCount || r->OriginalFd.Generation == 0)) return false;
            if (r->Borrowed)
            {
                DiskBinding* binding = disk->Bindings + r->Binding;
                Slot fd = state->Owner.Slots[r->OriginalFd.Number];
                if (fd.Description != 0 && fd.Generation == r->OriginalFd.Generation && fd.Description != r->Description + 1) return false;
                if (r->Kind == DiskSeek && (r->Operation.IsInitialized || r->Whence > 2 || r->Phase == DiskWorking) ||
                    r->Kind == DiskRead && (r->Requested > MaximumIo || r->Phase == DiskWorking && !r->Operation.IsInitialized ||
                    (r->Phase == DiskReady || paused) && !r->Operation.IsInitialized && r->Requested != 0 && r->Offset < binding->Metadata.Info.Length)) return false;
                if (fstat && (r->Operation.IsInitialized || r->Phase == DiskWorking)) return false;
                if (r->Operation.IsInitialized && disk->Mount->Input.Operations[r->Operation.Index].FileIndex != binding->File.Index) return false;
            }
            else
            {
                bool queued = false;
#if !NOSTDLIB_EFI || M16_027_PROBE
                queued = BorrowedDiskMount(disk->Mount) && r->Phase == DiskQueued && r->PathLength is >= 12 and < MaximumPath && DiskPath(r->Path, r->PathLength);
#endif
                if (r->OriginalFd.Owner != null || r->Phase == DiskQueued && !queued ||
                    (r->Phase is DiskWorking or DiskReady || paused) && !r->Operation.IsInitialized) return false;
                if (r->BindingGeneration != 0 && (r->Phase != DiskDraining || r->Worker.IsValid || r->Binding >= DiskSlots ||
                    disk->Bindings[r->Binding].Active != 1 || disk->Bindings[r->Binding].Description != uint.MaxValue ||
                    disk->Bindings[r->Binding].Generation != r->BindingGeneration)) return false;
            }
        }
        for (uint i = 0; i < state->Owner.DescriptionCount; i++)
        {
            Description d = state->Owner.Descriptions[i]; if ((Kind)d.Type != Kind.Disk) continue;
            if (d.Pipe >= DiskSlots || disk->Bindings[d.Pipe].Active != 1 || disk->Bindings[d.Pipe].Description != i) return false;
            uint borrows = 0;
            for (uint j = 0; j < DiskSlots; j++) if (disk->Requests[j].Phase != 0 && disk->Requests[j].Borrowed && disk->Requests[j].Description == i) borrows++;
            if (borrows != d.Borrowers) return false;
        }
        return true;
    }

    // Called by the core only for the actual last shared-description reference.
    // A real retired-FS receipt permits local scrub after device failure.
    private static bool CloseDiskDescription(State* state, uint description)
    {
        if (!DiskBound(state) || description >= state->Owner.DescriptionCount) return false;
        Description* d = state->Owner.Descriptions + description;
        if (d->Pipe >= DiskSlots) return false;
        DiskBinding* b = state->Disk->Bindings + d->Pipe;
        if (b->Active != 1 || b->Description != description) return false;
        if (!DiskRetired(state->Disk) && Fs.Close(state->Disk->Mount, b->File) != Fs.Status.Success) return false;
        *b = default; return true;
    }

    private static long DiskReadPath(State* state, ulong address, byte* path, out uint length, out bool directory, out bool absolute)
    {
        length = 0; directory = absolute = false;
        for (; length < MaximumPath; length++)
        {
            if (address > ulong.MaxValue - length || !LinuxSyscallGateway.ValidateRange(address + length, 1, LinuxUserPageAccess.Read, state->Owner.User) ||
                state->Owner.User.CopyFromUser(state->Owner.User.Context, address + length, path + length, 1) != 1) return Fault;
            if (path[length] == 0) break;
        }
        if (length == MaximumPath) return NameTooLong;
        if (length == 0) return Missing;
        absolute = path[0] == '/';
        if (!absolute)
        {
            if (length + 1 >= MaximumPath) return NameTooLong;
            for (uint i = length; i != 0; i--) path[i] = path[i - 1]; path[0] = (byte)'/'; length++;
        }
        if (length > 1 && path[length - 1] == '/') { directory = true; length--; }
        return 0;
    }
    private static bool DiskPath(byte* path, uint bytes)
    {
        const string root = "/app/wwwroot";
        if (bytes < 12 || bytes > 12 && path[12] != '/') return false;
        for (uint i = 0; i < 12; i++) if (path[i] != root[(int)i]) return false;
        return true;
    }
    private static long DiskStatOutput(State* state, Fs.FileInspection info, ulong address)
    {
        byte* wire = stackalloc byte[144]; Clear(wire, 144);
        *(ulong*)(wire + 8) = info.Info.Inode; *(ulong*)(wire + 16) = 1;
        *(uint*)(wire + 24) = info.Info.IsDirectory ? 0x416dU : 0x8124U;
        *(ulong*)(wire + 48) = info.Info.Length; *(ulong*)(wire + 56) = 4096;
        *(ulong*)(wire + 64) = info.AllocationKnown ? info.AllocatedBlocks : 0;
        *(long*)(wire + 88) = info.Info.ModifiedSeconds; // Other unavailable times remain zero.
        return Output(state, address, wire, 144) ? 0 : Fault;
    }
    private static long DiskErrno(Fs.Status status) => status switch
    {
        Fs.Status.NotFound => Missing, Fs.Status.NotDirectory => NotDirectory, Fs.Status.IsDirectory => -21,
        Fs.Status.InvalidPath or Fs.Status.OutsideRoot or Fs.Status.UnsupportedEncoding => Denied,
        Fs.Status.Full => FileTableFull, Fs.Status.GenerationExhausted => Overflow,
        Fs.Status.InvalidOffset => Invalid, _ => Io
    };

    /// <summary>Compatible scalar inventory for the thread/native owner. For a
    /// borrowed backend this lists FS-local metadata and its port only; EVERY
    /// external-buffer admission must also use DiskCanRetainStorage for the
    /// complete C/B/DMA/MMIO/observer tree. Numeric ranges are not mapping proof.</summary>
    public static void DiskExtentAt(State* state, uint index, out ulong start, out ulong bytes)
    {
        start = bytes = 0; if (!DiskBound(state)) return;
        DiskLocalExtent(state->Disk, index, out start, out bytes);
    }
    private static void DiskLocalExtent(DiskState* d, uint index, out ulong start, out ulong bytes)
    {
        start = bytes = 0;
        if (index == 0) { start = (ulong)d; bytes = (ulong)sizeof(DiskState); }
        else if (index == 1) { start = (ulong)d->Bindings; bytes = DiskSlots * (ulong)sizeof(DiskBinding); }
        else if (index == 2) { start = (ulong)d->Requests; bytes = DiskSlots * (ulong)sizeof(DiskRequest); }
        else if (index < DiskExtentCount) FsExtent(d->Mount, index - 3, out start, out bytes);
    }
    private static void FsExtent(Fs.Mount* m, uint index, out ulong start, out ulong bytes)
    {
        var a = m->Input; start = bytes = 0;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (BorrowedDiskMount(m) && index >= 68)
        {
            if (index == 68) { start = (ulong)m->ReadPort; bytes = (ulong)sizeof(OS.Storage.WritableBlockReadPort.State); }
            return;
        }
#endif
        if (index == 0) { start = (ulong)m; bytes = (ulong)sizeof(Fs.Mount); }
        else if (index == 1) { start = (ulong)a.Operations; bytes = a.OperationCount * (ulong)sizeof(Fs.Operation); }
        else if (index == 2) { start = (ulong)a.Files; bytes = a.FileCount * (ulong)sizeof(Fs.FileHandle); }
        else if (index == 3) { start = (ulong)a.Cache; bytes = a.CacheCount * (ulong)sizeof(Fs.CacheSlot); }
        else if (index < 52)
        {
            uint op = (index - 4) / 6, part = (index - 4) % 6;
            // Two unused sixth scratch-range positions name the separate
            // immutable directory and chain-certificate backing. No operation
            // buffer is borrowed; the bounded inventory keeps its capacity.
            if (part == 5)
            {
                if (op == 0)
                {
                    start = (ulong)m->ImmutableDirectoryCacheBacking;
                    bytes = Fs.ImmutableDirectoryCacheBytes(m->ImmutableDirectoryCacheSlots);
                }
                else if (op == 1)
                {
                    start = (ulong)m->ImmutableFileChainCacheBacking;
                    bytes = Fs.ImmutableFileChainCacheBytes(m->ImmutableFileChainCacheSlots);
                }
                return;
            }
            if (op >= a.OperationCount) return;
            var s = a.Operations[op].Storage;
            if (part == 0) { start = (ulong)s.Path; bytes = 256; }
            else if (part == 1) { start = (ulong)s.Sector; bytes = 512; }
            else if (part == 2) { start = (ulong)s.Name; bytes = 520; }
            else if (part == 3) { start = (ulong)s.History; bytes = s.HistoryCapacity * 4UL; }
            else if (part == 4) { start = (ulong)s.Range; bytes = s.RangeCapacity; }
        }
        else if (index < 60) { uint i = index - 52; if (i < a.FileCount) { start = (ulong)a.Files[i].Clusters; bytes = a.Files[i].ClusterCapacity * 4UL; } }
        else if (index < 68) { uint i = index - 60; if (i < a.CacheCount) { start = (ulong)a.Cache[i].Data; bytes = 512; } }
        else if (index == 68) { start = (ulong)a.Device; bytes = (ulong)sizeof(OS.Storage.ContinuingReadOnlyBlockService.State); }
        else if (index == 69) { start = (ulong)a.Device->Input.Pool; bytes = (ulong)sizeof(OS.Drivers.DmaBufferPool); }
        else if (index == 70) { start = (ulong)a.Device->Input.Pool->Slots; bytes = a.Device->Input.Pool->Count * (ulong)sizeof(OS.Drivers.DmaBufferSlot); }
        else if (index == 71) { start = a.Device->Input.Pool->Window.CpuVirtualBase; bytes = a.Device->Input.Pool->Window.ByteLength; }
        else if (index == 72) { start = a.Device->Input.Common.VirtualStart; bytes = a.Device->Input.Common.Bytes; }
        else if (index == 73) { start = a.Device->Input.Notify.VirtualStart; bytes = a.Device->Input.Notify.Bytes; }
        else if (index == 74) { start = a.Device->Input.Device.VirtualStart; bytes = a.Device->Input.Device.Bytes; }
    }
}
#endif
