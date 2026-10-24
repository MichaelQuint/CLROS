#if !NOSTDLIB_EFI || M16_025_PROBE
using OS.Pal;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    public const uint DiskSlots = 8, DiskCompletionBytes = 8192, DiskExtentCount = 78;
    private const byte DiskQueued = 1, DiskWorking = 2, DiskReady = 3, DiskDraining = 4, DiskError = 5;
    private const byte DiskOpen = 1, DiskStat = 2, DiskRead = 3, DiskSeek = 4;
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
        internal DiskResult(DiskProgress progress, long value = 0, DiskToken token = default,
            OSHostHandle worker = default, Fs.Status failure = Fs.Status.Success)
        { Progress = progress; Value = value; Token = token; Worker = worker; Failure = failure; }
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
            if (!SocketOutside(state, (void*)starts[i], lengths[i])) return false;
            for (ulong j = 0; j < lengths[i]; j++) if (((byte*)starts[i])[j] != 0) return false;
        }
        for (uint i = 0; i < a.Mount->Input.OperationCount; i++)
            if (a.Mount->Input.Operations[i].Storage.RangeCapacity < DiskCompletionBytes) return false;
        for (uint i = 0; i < 68; i++)
        {
            FsExtent(a.Mount, i, out ulong start, out ulong bytes);
            if (bytes != 0 && !SocketOutside(state, (void*)start, bytes)) return false;
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
            description = state->Owner.Slots[fd.Number].Description - 1; binding = opened->Pipe;
            if (call.Number == 5)
            {
                Fs.FileInspection info = default;
                Fs.Status observed = Fs.Inspect(state->Disk->Mount, state->Disk->Bindings[binding].File, &info);
                return observed == Fs.Status.Success ? new(DiskProgress.Immediate, DiskStatOutput(state, info, call.Argument1)) :
                    new(DiskProgress.Immediate, Io, failure: observed);
            }
            kind = call.Number == 8 ? DiskSeek : DiskRead;
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

    /// <summary>One bounded round-robin step, including detached cancellation
    /// records. The caller scans at most eight ready token observations to wake
    /// exact wait generations. This never copies user bytes or commits cursors.</summary>
    public static DiskResult PumpDiskOne(State* state, ulong owner)
    {
        if (!Attached(state, owner) || !DiskBound(state)) return new(DiskProgress.Rejected);
        DiskState* d = state->Disk;
        for (uint n = 0; n < DiskSlots; n++)
        {
            uint i = (d->NextPump + n) % DiskSlots;
            if (d->Requests[i].Phase == 0) continue;
            d->NextPump = (i + 1) % DiskSlots; return StepDisk(state, d->Requests + i);
        }
        return new(DiskProgress.Immediate);
    }

    /// <summary>Inspect one exact token without advancing I/O. A stale token
    /// cannot observe a reused record or acquire wake/copy authority.</summary>
    public static DiskResult PollDisk(State* state, ulong owner, DiskToken token)
    {
        if (!Attached(state, owner) || !DiskRequestFor(state, token, out DiskRequest* r)) return new(DiskProgress.Rejected);
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
        if (r->Phase is not (DiskReady or DiskError)) return DiskObservation(state->Disk, r);
        if (r->Phase == DiskError) return FinishDisk(state, r, r->Error, r->Failure);
        Fs.FileInspection live = default;
        if (r->Borrowed)
        {
            Fs.Status checkedFile = Fs.Inspect(state->Disk->Mount, state->Disk->Bindings[r->Binding].File, &live);
            if (checkedFile != Fs.Status.Success) return DrainDisk(state, r, Io, true, checkedFile);
        }
        if (r->Kind is DiskOpen or DiskStat) return CompleteDiskOpen(state, r, call, outputBytes);
        Description* description = state->Owner.Descriptions + r->Description;
        if (r->Kind == DiskSeek) { description->Position = r->Offset; return FinishDisk(state, r, (long)r->Offset); }
        if (call.Argument2 != r->Requested || r->Positioned && call.Argument3 != r->Offset) return new(DiskProgress.Rejected);
        uint copied = 0; Fs.Status copiedStatus = Fs.Status.Success;
        if (r->Operation.IsInitialized)
        {
            copiedStatus = Fs.CopyRead(state->Disk->Mount, r->Operation,
                new Span<byte>(state->Owner.Scratch, (int)DiskCompletionBytes), &copied);
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
        if (r->Phase is DiskReady or DiskError) return DiskObservation(disk, r);
        if (r->Phase == DiskQueued)
        {
            if (CursorDiskBusy(disk, r)) return DiskObservation(disk, r);
            Fs.FileInspection info = default;
            Fs.Status inspected = Fs.Inspect(disk->Mount, disk->Bindings[r->Binding].File, &info);
            if (inspected != Fs.Status.Success) return DrainDisk(state, r, Io, false, inspected);
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
            if (started != Fs.Status.Pending) return DrainDisk(state, r, DiskErrno(started), false, started);
            r->Operation = op; r->Phase = DiskWorking; return DiskObservation(disk, r);
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
            if (r->Failure == Fs.Status.Success) r->Failure = Fs.Status.InvalidState;
            return DiskObservation(state->Disk, r);
        }
        ClearDiskRequest(r);
        return new(DiskProgress.Immediate, value, failure: failure);
    }
    private static void ClearDiskRequest(DiskRequest* r) { ulong self = r->Self; *r = default; r->Self = self; }
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
            !DiskStorage(a.Cache, a.CacheCount * (ulong)sizeof(Fs.CacheSlot)) ||
            !DiskStorage(a.Device, (ulong)sizeof(OS.Storage.ContinuingReadOnlyBlockService.State))) return false;
        var device = a.Device; var pool = device->Input.Pool;
        if (device->Self != (ulong)device || device->Input.Epoch != m->DeviceEpoch || device->PoolEpoch != m->PoolEpoch ||
            !DiskStorage(pool, (ulong)sizeof(OS.Drivers.DmaBufferPool)) || pool->Self != (ulong)pool || pool->Epoch != m->PoolEpoch ||
            pool->Count is 0 or > 64 || !DiskStorage(pool->Slots, pool->Count * (ulong)sizeof(OS.Drivers.DmaBufferSlot))) return false;
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
        }
        // This is structural ownership, not device liveness or I/O admission.
        return !m->IsRetired || DiskRetired(d);
    }

    private static bool DiskRetired(DiskState* d)
    {
        Fs.Mount* m = d->Mount; var device = m->Input.Device;
        if (!m->IsRetired || m->IoActive || !device->Retired || !device->Receipt.ResetComplete ||
            !device->Receipt.BusMasterDisabled || !device->Receipt.DmaReleased || !device->ReturnedToCpu || !device->Scrubbed) return false;
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
            if (r->Self != (ulong)r || r->Phase > DiskError) return false;
            if (r->Phase == 0)
            { if (r->Generation != 0 || r->Operation.IsInitialized || r->Borrowed || r->Worker.IsValid) return false; continue; }
            if (r->Generation == 0 || r->Generation > disk->NextGeneration || r->Kind is < DiskOpen or > DiskSeek ||
                !r->Worker.IsValid && r->Phase != DiskDraining) return false;
            if (r->Kind == DiskSeek && r->Number != 8 || r->Kind == DiskRead && r->Number != (r->Positioned ? 17UL : 0) ||
                r->Kind == DiskOpen && r->Number is not (2 or 257) || r->Kind == DiskStat && r->Number is not (4 or 6)) return false;
            if (r->Borrowed != (r->Kind is DiskRead or DiskSeek)) return false;
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
                    r->Phase == DiskReady && !r->Operation.IsInitialized && r->Requested != 0 && r->Offset < binding->Metadata.Info.Length)) return false;
                if (r->Operation.IsInitialized && disk->Mount->Input.Operations[r->Operation.Index].FileIndex != binding->File.Index) return false;
            }
            else
            {
                if (r->OriginalFd.Owner != null || r->Phase == DiskQueued ||
                    r->Phase is DiskWorking or DiskReady && !r->Operation.IsInitialized) return false;
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
        ReadOnlySpan<byte> root = "/app/wwwroot"u8;
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

    /// <summary>Bounded retained-extent inventory for the thread/native owner.
    /// These numeric ranges do not prove mappings or physical alias freedom.</summary>
    public static void DiskExtentAt(State* state, uint index, out ulong start, out ulong bytes)
    {
        start = bytes = 0; if (!DiskBound(state)) return;
        DiskState* d = state->Disk;
        if (index == 0) { start = (ulong)d; bytes = (ulong)sizeof(DiskState); }
        else if (index == 1) { start = (ulong)d->Bindings; bytes = DiskSlots * (ulong)sizeof(DiskBinding); }
        else if (index == 2) { start = (ulong)d->Requests; bytes = DiskSlots * (ulong)sizeof(DiskRequest); }
        else if (index < DiskExtentCount) FsExtent(d->Mount, index - 3, out start, out bytes);
    }
    private static void FsExtent(Fs.Mount* m, uint index, out ulong start, out ulong bytes)
    {
        var a = m->Input; start = bytes = 0;
        if (index == 0) { start = (ulong)m; bytes = (ulong)sizeof(Fs.Mount); }
        else if (index == 1) { start = (ulong)a.Operations; bytes = a.OperationCount * (ulong)sizeof(Fs.Operation); }
        else if (index == 2) { start = (ulong)a.Files; bytes = a.FileCount * (ulong)sizeof(Fs.FileHandle); }
        else if (index == 3) { start = (ulong)a.Cache; bytes = a.CacheCount * (ulong)sizeof(Fs.CacheSlot); }
        else if (index < 52)
        {
            uint op = (index - 4) / 6, part = (index - 4) % 6; if (op >= a.OperationCount) return;
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
