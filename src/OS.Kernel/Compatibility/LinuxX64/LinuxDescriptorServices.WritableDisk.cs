#if !NOSTDLIB_EFI || M16_027_PROBE
using OS.Kernel.Memory;
using OS.Pal;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    private const long WritableUnsupported = -95, WritableAgain = -11;
    private const byte WritablePending = 1, WritableReady = 2, WritableDraining = 3, WritableRecovery = 4;
    private const byte WritableOpen = 1, WritableRead = 2, WritableWrite = 3, WritableSync = 4, WritableUnlink = 5, WritableStat = 6;
    public enum WritableDiskProgress : byte { Rejected, NotHandled, Immediate, Pending, Ready, Draining, RecoveryRequired }

    /// <summary>Copied attachment/request generation, not a user fd, mapping
    /// pin or permission to copy/wake. The original serialized owner validates it.</summary>
    public readonly struct WritableDiskToken
    {
        internal readonly ulong Owner, Epoch, Generation;
        internal WritableDiskToken(WritableDiskState* s) { Owner = (ulong)s; Epoch = s->Epoch; Generation = s->Request.Generation; }
        public bool IsActive => Owner != 0 && Epoch != 0 && Generation != 0;
    }

    /// <summary>Control observation. An empty Worker revokes all wake/copy
    /// authority while Draining/RecoveryRequired still retains original backing.
    /// Ready keeps the exact live worker for actual completion. No result grants
    /// mutation replay, user mapping or device durability authority.</summary>
    public readonly struct WritableDiskResult
    {
        public readonly WritableDiskProgress Progress;
        public readonly long Value;
        public readonly WritableDiskToken Token;
        public readonly OSHostHandle Worker;
        public readonly C.Status Failure;
        internal WritableDiskResult(WritableDiskProgress progress, long value = 0, WritableDiskToken token = default,
            OSHostHandle worker = default, C.Status failure = C.Status.Success)
        { Progress = progress; Value = value; Token = token; Worker = worker; Failure = failure; }
    }

    /// <summary>One original provider handle per global description; private
    /// provider fd integers never enter the Linux namespace. No dup in this slice.
    /// Closing retains the binding while a real pending request borrows it.</summary>
    public struct WritableDiskBinding
    {
        internal W.DescriptorToken File;
        internal ulong Generation;
        internal uint Active, Closing;
    }

    /// <summary>Stable one-request POD independent of a thread Slot. It owns
    /// reservations/borrows until real completion or cancellation drain. No user
    /// address, resolved pointer, caller Span, path or iovec is retained.</summary>
    public struct WritableDiskRequest
    {
        internal ulong Self, Generation, BindingGeneration, Number, Offset;
        internal Token OriginalFd;
        internal W.RequestToken ProviderRequest;
        internal W.DescriptorToken TemporaryOpened;
        internal OSHostHandle Worker;
        internal long Value;
        internal C.Status Failure;
        internal uint Description, ReservedFd, ReservedDescription, ReservationGeneration, Requested, Wanted, OpenFlags;
        internal byte Phase, Operation;
        internal bool Positioned, Borrowed, Reserved, Orphan, Cancelling, CopyRevoked, Closed;
    }

    /// <summary>One noncopyable, IRQ-off externally serialized attachment.
    /// Keep it, its description-sized binding table and the complete provider
    /// tree mapped supervisor RW/NX until Detach succeeds. Provider/engine/block
    /// teardown follows adapter detach. Dirty recovery prohibits successful End.
    /// This dedicated mechanical slice excludes read-only disk/transport owners.</summary>
    public struct WritableDiskState
    {
        internal ulong Self, Epoch, NextGeneration;
        internal State* Owner;
        internal W.State* Provider;
        internal W.ProcessToken Process;
        internal W.FreeSearch Search;
        internal WritableDiskBinding* Bindings;
        internal uint BindingCount;
        internal WritableDiskRequest Request;
        public readonly bool RequestActive => Request.Phase != 0;
    }

    public struct WritableDiskAdmission
    {
        public W.State* Provider;
        public W.ProcessToken Process;
        public W.FreeSearch Search;
        public WritableDiskBinding* Bindings;
        public uint BindingCount;
        public ulong Epoch;
        public bool FirmwareExited, SingleOwnerValidated;
    }

    /// <summary>Borrow an already admitted idle provider and a zeroed table
    /// indexed by global description. Require its exact configured primary;
    /// copied PID alone is not kernel process authority. Invalid admission has
    /// no effects, callbacks or I/O. Native mapping/process proof remains outer.</summary>
    public static bool AttachWritableDisk(State* state, WritableDiskState* disk, in WritableDiskAdmission a)
    {
        if (!Audit(state) || state->WritableDisk != null || !a.FirmwareExited || !a.SingleOwnerValidated || a.Epoch == 0 ||
            !WritableExtent(disk, (ulong)sizeof(WritableDiskState)) || !WritableExtent(a.Provider, (ulong)sizeof(W.State)) ||
            a.BindingCount != state->Owner.DescriptionCount || !WritableExtent(a.Bindings, a.BindingCount * (ulong)sizeof(WritableDiskBinding)) ||
            a.Provider->RequestActive || !WritableProcess(a.Process, W.PrimaryProcess(a.Provider))) return false;
#if !NOSTDLIB_EFI || M16_025_PROBE
        if (state->Disk != null) return false;
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        if (state->Sockets != null || state->Epoll != null) return false;
#endif
        if (!SocketOutside(state, disk, (ulong)sizeof(WritableDiskState)) ||
            !SocketOutside(state, a.Bindings, a.BindingCount * (ulong)sizeof(WritableDiskBinding)) ||
            Overlap((ulong)disk, (ulong)sizeof(WritableDiskState), (ulong)a.Bindings, a.BindingCount * (ulong)sizeof(WritableDiskBinding)) ||
            !W.CanBorrowStorage(a.Provider, a.Epoch, (ulong)disk, (ulong)sizeof(WritableDiskState)) ||
            !W.CanBorrowStorage(a.Provider, a.Epoch, (ulong)a.Bindings, a.BindingCount * (ulong)sizeof(WritableDiskBinding))) return false;
        for (uint i = 0; i < WritableBaseExtentCount(state); i++)
        {
            WritableBaseExtentAt(state, i, out ulong start, out ulong bytes);
            if (bytes != 0 && !W.CanBorrowStorage(a.Provider, a.Epoch, start, bytes)) return false;
        }
        if (!WritableZero(disk, (ulong)sizeof(WritableDiskState)) || !WritableZero(a.Bindings, a.BindingCount * (ulong)sizeof(WritableDiskBinding))) return false;
        *disk = new() { Self = (ulong)disk, Epoch = a.Epoch, Owner = state, Provider = a.Provider, Process = a.Process,
            Search = a.Search, Bindings = a.Bindings, BindingCount = a.BindingCount };
        disk->Request.Self = (ulong)&disk->Request; state->WritableDisk = disk; return true;
    }

    /// <summary>Scrub empty attachment metadata only. Live handles, reserved
    /// opens, pending/orphan/recovery records prohibit detach. Does not stop or
    /// free the provider, engine, device, DMA or observation backing.</summary>
    public static bool DetachWritableDisk(State* state)
    {
        if (!Audit(state) || state->WritableDisk == null || !CanEndWritableDisk(state)) return false;
        WritableDiskState* d = state->WritableDisk;
        for (uint i = 0; i < d->BindingCount; i++) if (d->Bindings[i].Active != 0) return false;
        for (uint i = 0; i < d->BindingCount; i++) d->Bindings[i] = default;
        state->WritableDisk = null; *d = default; return true;
    }

    /// <summary>Start confined /data opens/unlinks or measured reads, positioned
    /// writes, syncs and locks on bound global
    /// fds. A null attachment is neutral before input copies. The parent supplies
    /// an actual current worker and retains its original call and output ceiling.
    /// One outstanding I/O request; other I/O admissions return EAGAIN.
    /// Nonblocking flock is synchronous and may run during a clean read.</summary>
    public static WritableDiskResult BeginWritableDisk(State* state, ulong owner, OSHostHandle worker, LinuxSyscallFrame call)
    {
        if (state == null || state->WritableDisk == null) return new(WritableDiskProgress.NotHandled);
        if (!Attached(state, owner) || !worker.IsValid) return new(WritableDiskProgress.Rejected);
        WritableDiskState* d = state->WritableDisk;
        if (call.Number is 2 or 87 or 257)
        {
            byte* path = stackalloc byte[(int)MaximumPath];
            long copied = WritableReadPath(state, call.Number == 257 ? call.Argument1 : call.Argument0, path, out uint bytes);
            if (copied != 0) return new(WritableDiskProgress.Immediate, copied);
            if (!WritablePath(path, bytes)) return new(WritableDiskProgress.NotHandled);
            if (d->Request.Phase != 0) return new(WritableDiskProgress.Immediate, WritableAgain);
            if (call.Number == 87)
            {
                long unlinkStarted = WritableStart(state, worker, call.Number, WritableUnlink);
                if (unlinkStarted != 0) return new(WritableDiskProgress.Immediate, unlinkStarted);
                return WritableStarted(state, W.BeginUnlinkForProcess(d->Provider, d->Process, new ReadOnlySpan<byte>(path, (int)bytes)));
            }
            ulong flags = call.Number == 2 ? call.Argument1 : call.Argument2;
            if (flags > uint.MaxValue) return new(WritableDiskProgress.Immediate, Invalid);
            const uint allowed = 3 | W.Create | W.Exclusive | CloseOnExec | NoFollow | LargeFile | W.DirectoryOnly | W.NoControllingTerminal;
            if (((uint)flags & ~allowed) != 0 || (flags & 3) == 3) return new(WritableDiskProgress.Immediate, WritableUnsupported);
            ulong mode = call.Number == 2 ? call.Argument2 : call.Argument3;
            if ((flags & W.Create) != 0 && mode != 0x180) return new(WritableDiskProgress.Immediate, WritableUnsupported);
            uint fd = FreeSlot(state, 0, uint.MaxValue), description = FreeDescription(state, uint.MaxValue);
            if (fd == state->Owner.DescriptorCount) return new(WritableDiskProgress.Immediate, DescriptorTableFull);
            if (description == state->Owner.DescriptionCount || state->NextInode == uint.MaxValue) return new(WritableDiskProgress.Immediate, FileTableFull);
            long started = WritableStart(state, worker, call.Number, WritableOpen);
            if (started != 0) return new(WritableDiskProgress.Immediate, started);
            WritableDiskRequest* r = &d->Request;
            r->Reserved = true; r->ReservedFd = fd; r->ReservedDescription = description;
            r->ReservationGeneration = state->Owner.Slots[fd].Generation; r->OpenFlags = (uint)flags;
            W.Result begun = W.OpenForProcess(d->Provider, d->Process, new ReadOnlySpan<byte>(path, (int)bytes), (uint)flags);
            if (begun.Phase == W.Progress.Immediate)
            {
                if (begun.Value < 0) return FinishWritable(state, begun.Value, begun.Failure);
                r->TemporaryOpened = begun.Descriptor; return PublishWritableOpen(state);
            }
            return WritableStarted(state, begun);
        }
        if (call.Number is not (0 or 5 or 8 or 17 or 72 or 73 or 74 or 75 or 328)) return new(WritableDiskProgress.NotHandled);
        Description* opened = Find(state, unchecked((uint)call.Argument0));
        if (opened == null || (Kind)opened->Type != Kind.WritableDisk) return new(WritableDiskProgress.NotHandled);
        // Flock is a synchronous open-description operation. An unrelated
        // pending read does not invent EWOULDBLOCK; the provider authenticates
        // its exact fd/process generations and current clean storage itself.
        if (call.Number == 73)
        {
            if (!W.CanBorrowStorage(d->Provider, d->Epoch, (ulong)d, (ulong)sizeof(WritableDiskState)))
                return new(WritableDiskProgress.Immediate, Io);
            return new(WritableDiskProgress.Immediate,
                W.FlockForProcess(d->Provider, d->Process, d->Bindings[opened->Pipe].File, (uint)call.Argument1));
        }
        if (d->Request.Phase != 0) return new(WritableDiskProgress.Immediate, WritableAgain);
        // Retained backing is sufficient for close/orphan cleanup, never new
        // I/O, a cursor change or a copied lock query from a stopped device.
        if (!W.CanBorrowStorage(d->Provider, d->Epoch, (ulong)d, (ulong)sizeof(WritableDiskState)))
            return new(WritableDiskProgress.Immediate, Io);
        if (call.Number == 8) return WritableSeek(state, opened, unchecked((long)call.Argument1), call.Argument2);
        if (call.Number == 72) return WritableLock(state, opened, call);
        if (call.Number == 328) return WritablePwrite(state, worker, opened, call);
        if (call.Number == 5)
        {
            long statStarted = WritableStart(state, worker, call.Number, WritableStat);
            if (statStarted != 0) return new(WritableDiskProgress.Immediate, statStarted);
            WritableBorrow(state, (uint)call.Argument0, opened);
            d->Request.Requested = d->Request.Wanted = 144;
            return WritableStarted(state, W.BeginStatForProcess(d->Provider, d->Process, d->Bindings[d->Request.Description].File));
        }
        if (call.Number is 74 or 75)
        {
            long syncStarted = WritableStart(state, worker, call.Number, WritableSync);
            if (syncStarted != 0) return new(WritableDiskProgress.Immediate, syncStarted);
            WritableBorrow(state, (uint)call.Argument0, opened);
            return WritableStarted(state, W.BeginSync(d->Provider, d->Bindings[d->Request.Description].File, call.Number == 75));
        }
        if ((opened->Flags & 3) == 1) return new(WritableDiskProgress.Immediate, BadDescriptor);
        if (call.Argument2 > MaximumIo || call.Number == 17 && call.Argument3 > long.MaxValue) return new(WritableDiskProgress.Immediate, Invalid);
        if (!Capture(state, unchecked((uint)call.Argument0), out Token original)) return new(WritableDiskProgress.Rejected);
        long readStarted = WritableStart(state, worker, call.Number, WritableRead);
        if (readStarted != 0) return new(WritableDiskProgress.Immediate, readStarted);
        WritableDiskRequest* request = &d->Request;
        WritableBorrow(state, original.Number, opened);
        request->Positioned = call.Number == 17; request->Requested = (uint)call.Argument2;
        request->Wanted = (uint)(call.Argument2 < W.IoBytes ? call.Argument2 : W.IoBytes);
        request->Offset = request->Positioned ? call.Argument3 : opened->Position;
        return WritableStarted(state, W.BeginRead(d->Provider, d->Bindings[request->Description].File, request->Wanted, request->Offset, true));
    }

    /// <summary>One bounded provider turn; no user copies or common cursor
    /// commits. Keep pumping owner-independent orphan records through actual
    /// clean cancellation drain. RecoveryRequired retains all backing.</summary>
    public static WritableDiskResult PumpWritableDiskOne(State* state, ulong owner)
    {
        if (!Attached(state, owner)) return new(WritableDiskProgress.Rejected);
        WritableDiskState* d = state->WritableDisk; if (d == null) return new(WritableDiskProgress.NotHandled);
        WritableDiskRequest* r = &d->Request;
        if (r->Phase == 0) return new(WritableDiskProgress.Immediate);
        if (r->Phase is WritableReady or WritableRecovery) return ObserveWritable(d);
        W.Result progress = d->Provider->RequestActive ? W.Pump(d->Provider) : new(W.Progress.Immediate);
        if (progress.Phase == W.Progress.RecoveryRequired)
        { r->Phase = WritableRecovery; r->Value = Io; r->Failure = progress.Failure; return ObserveWritable(d); }
        if (r->Cancelling)
        {
            if (d->Provider->RequestActive)
            {
                if (progress.Phase != W.Progress.Ready) return ObserveWritable(d);
                W.Result consumed = W.Complete(d->Provider, r->ProviderRequest);
                if (consumed.Phase == W.Progress.RecoveryRequired)
                { r->Phase = WritableRecovery; r->Value = Io; r->Failure = consumed.Failure; return ObserveWritable(d); }
                if (d->Provider->RequestActive) return ObserveWritable(d);
            }
            r->ProviderRequest = default;
            if (r->Orphan) return FinishWritable(state, r->Value, r->Failure);
            r->Phase = WritableReady; return ObserveWritable(d);
        }
        if (progress.Phase == W.Progress.Ready)
        { r->Phase = WritableReady; r->Value = progress.Value; r->Failure = progress.Failure; return ObserveWritable(d); }
        if (progress.Phase == W.Progress.Pending) return ObserveWritable(d);
        r->Phase = WritableRecovery; r->Value = Io; r->Failure = progress.Failure; return ObserveWritable(d);
    }

    public static WritableDiskResult PollWritableDisk(State* state, ulong owner, WritableDiskToken token) =>
        Attached(state, owner) && WritableRequestFor(state, token) ? ObserveWritable(state->WritableDisk) : new(WritableDiskProgress.Rejected);

    /// <summary>Only the exact live worker completes its saved original call.
    /// Read into supervisor scratch using a positioned provider request; validate
    /// the captured ceiling/current output, copy to user, scrub, then commit the
    /// GLOBAL cursor. Close/cancel/fd reuse cannot publish late bytes or position.</summary>
    public static WritableDiskResult CompleteWritableDisk(State* state, ulong owner, OSHostHandle worker,
        WritableDiskToken token, LinuxSyscallFrame originalCall, uint outputBytes = 8192)
    {
        if (outputBytes > W.IoBytes || !Attached(state, owner) || !WritableRequestFor(state, token)) return new(WritableDiskProgress.Rejected);
        WritableDiskState* d = state->WritableDisk; WritableDiskRequest* r = &d->Request;
        if (!worker.IsValid || worker.Value != r->Worker.Value || originalCall.Number != r->Number) return new(WritableDiskProgress.Rejected);
        if (r->Phase == WritableRecovery)
        { r->CopyRevoked = true; r->Orphan = true; r->Worker = default; return ObserveWritable(d); }
        if (r->Phase != WritableReady) return ObserveWritable(d);
        if (r->Closed || r->CopyRevoked) return FinishWritable(state, r->Value, r->Failure);
        if (r->Operation == WritableOpen)
        {
            if (!WritableReservationCurrent(state)) return AbandonWritable(state, Io);
            W.Result opened = W.Complete(d->Provider, r->ProviderRequest);
            if (opened.Phase != W.Progress.Immediate) return AbandonWritable(state, Io);
            r->ProviderRequest = default;
            if (opened.Value < 0) return FinishWritable(state, opened.Value, opened.Failure);
            r->TemporaryOpened = opened.Descriptor; return PublishWritableOpen(state);
        }
        if (r->Operation == WritableUnlink) return CompleteWritableMutation(state);
        if (!IsCurrent(state, r->OriginalFd) || unchecked((uint)originalCall.Argument0) != r->OriginalFd.Number)
            return AbandonWritable(state, BadDescriptor);
        if (r->Operation == WritableWrite)
        {
            if (originalCall.Argument2 != 1 || originalCall.Argument3 != r->Offset || originalCall.Argument4 != 0 || originalCall.Argument5 != W.RwfNoAppend)
                return new(WritableDiskProgress.Rejected);
            return CompleteWritableMutation(state);
        }
        if (r->Operation == WritableSync) return CompleteWritableMutation(state);
        if (r->Operation == WritableStat)
        {
            W.StatMetadata metadata = default;
            W.Result observed = W.CopyStat(d->Provider, r->ProviderRequest, &metadata, (uint)sizeof(W.StatMetadata));
            if (observed.Phase != W.Progress.Immediate) return AbandonWritable(state, Io);
            if (d->Provider->RequestActive) return AbandonWritable(state, observed.Value < 0 ? observed.Value : Io);
            r->ProviderRequest = default;
            if (observed.Value < 0) return FinishWritable(state, observed.Value, observed.Failure);
            long statValue = WritableStatOutput(state, metadata, originalCall.Argument1, outputBytes, out C.Status failure);
            if (statValue == 0) state->Owner.Descriptions[r->Description].Bytes = metadata.Facts.File.Length;
            return FinishWritable(state, statValue, failure);
        }
        if (originalCall.Argument2 != r->Requested || r->Positioned && originalCall.Argument3 != r->Offset) return new(WritableDiskProgress.Rejected);
        W.Result read = W.CopyRead(d->Provider, r->ProviderRequest, new Span<byte>(state->Owner.Scratch, (int)W.IoBytes));
        if (read.Phase != W.Progress.Immediate)
        { Clear(state->Owner.Scratch, state->Owner.ScratchBytes); return AbandonWritable(state, Io); }
        r->ProviderRequest = default; long value = read.Value;
        if (value > W.IoBytes) value = Io;
        if (value > 0 && (value > outputBytes || !Output(state, originalCall.Argument1, state->Owner.Scratch, (uint)value))) value = Fault;
        Clear(state->Owner.Scratch, state->Owner.ScratchBytes);
        if (value >= 0 && !r->Positioned) state->Owner.Descriptions[r->Description].Position = r->Offset + (ulong)value;
        return FinishWritable(state, value, read.Failure);
    }

    /// <summary>Cancel copy before detaching a worker. A live already-ready
    /// non-read result wins interruption; abandoning consumes effects without user output.
    /// Clean pending cancellation returns an orphan EINTR while actual DMA drains.
    /// RecoveryRequired retains the record with empty Worker on abandon. No replay.</summary>
    public static WritableDiskResult CancelWritableDisk(State* state, ulong owner, WritableDiskToken token, bool abandoning)
    {
        if (!Attached(state, owner) || !WritableRequestFor(state, token)) return new(WritableDiskProgress.Rejected);
        WritableDiskState* d = state->WritableDisk; WritableDiskRequest* r = &d->Request;
        if (!abandoning && r->Operation is not (WritableRead or WritableStat) && r->Phase == WritableReady) return ObserveWritable(d);
        r->CopyRevoked = true; r->Orphan = true; r->Worker = default; r->Value = -4;
        return CancelWritableProvider(state);
    }

    // Structural full-tree query for outer thread/futex/signal/native-worker
    // buffers. No callback/I/O/user-copy or new-admission authority. A null
    // attachment is neutral; dirty/fault/orphan backing remains excluded.
    internal static bool WritableDiskCanRetainStorage(State* state, ulong address, ulong bytes) =>
        WritableDiskCanRetainStorage(state, address, bytes, includeUserBacking: true);

    // Only the existing futex resolved-user-alias query deliberately excludes
    // application backing. A range intersecting that pool must be wholly
    // contained; no supervisor/provider/engine/device exclusion is relaxed.
    // The default wrapper above preserves existing query/delegate semantics.
    internal static bool WritableDiskCanRetainStorage(State* state, ulong address, ulong bytes, bool includeUserBacking)
    {
        if (!WritableExtent(state, (ulong)sizeof(State)) || !WritableExtent((void*)address, bytes, 1)) return false;
        if (state->WritableDisk == null) return true;
        if (!AuditWritableDisk(state) || !WritableBaseOutside(state, address, bytes, includeUserBacking)) return false;
        WritableDiskState* d = state->WritableDisk;
        return !Overlap(address, bytes, (ulong)d, (ulong)sizeof(WritableDiskState)) &&
            !Overlap(address, bytes, (ulong)d->Bindings, d->BindingCount * (ulong)sizeof(WritableDiskBinding)) &&
            W.CanRetainStorage(d->Provider, d->Epoch, address, bytes);
    }

    private static bool CanEndWritableDisk(State* s) => s->WritableDisk == null || s->WritableDisk->Request.Phase == 0;
    private static bool WritableSlotReserved(State* s, uint i) => s->WritableDisk != null && s->WritableDisk->Request.Reserved && s->WritableDisk->Request.ReservedFd == i;
    private static bool WritableDescriptionReserved(State* s, uint i) => s->WritableDisk != null && s->WritableDisk->Request.Reserved && s->WritableDisk->Request.ReservedDescription == i;
    private static bool WritableProcess(W.ProcessToken a, W.ProcessToken b) => a.Owner != 0 && a.Owner == b.Owner && a.Epoch == b.Epoch && a.Generation == b.Generation && a.Index == b.Index && a.Pid == b.Pid;
    private static bool WritableFile(W.DescriptorToken a, W.DescriptorToken b) => a.Owner == b.Owner && a.Epoch == b.Epoch && a.Generation == b.Generation && a.Number == b.Number;
    private static bool WritableExtent(void* p, ulong bytes, ulong alignment = 8) => (ulong)p >= 4096 && (ulong)p < 0x0000800000000000 &&
        ((ulong)p & (alignment - 1)) == 0 && bytes != 0 && bytes <= 0x0000800000000000 - (ulong)p;
    private static bool WritableZero(void* p, ulong bytes) { for (ulong i = 0; i < bytes; i++) if (((byte*)p)[i] != 0) return false; return true; }

    private static long WritableStart(State* state, OSHostHandle worker, ulong number, byte operation)
    {
        WritableDiskState* d = state->WritableDisk;
        if (d->NextGeneration == ulong.MaxValue) return Overflow;
        if (!W.CanBorrowStorage(d->Provider, d->Epoch, (ulong)d, (ulong)sizeof(WritableDiskState))) return Io;
        ulong self = d->Request.Self;
        d->Request = new() { Self = self, Generation = ++d->NextGeneration, Number = number, Operation = operation,
            Worker = worker, Phase = WritablePending }; return 0;
    }
    private static void WritableBorrow(State* state, uint fd, Description* opened)
    {
        WritableDiskState* d = state->WritableDisk; WritableDiskRequest* r = &d->Request;
        Capture(state, fd, out r->OriginalFd);
        r->Description = state->Owner.Slots[fd].Description - 1; r->Borrowed = true;
        r->BindingGeneration = d->Bindings[r->Description].Generation; opened->Borrowers++;
    }
    // Linux x64 LP64 pwritev2 uses full64 pos_l in argument3; argument4 is
    // unused by that ABI. This finite contract requires zero there, one copied
    // iovec and exactly RWF_NOAPPEND. No iovec/payload pointer survives yield.
    private static WritableDiskResult WritablePwrite(State* state, OSHostHandle worker, Description* opened, LinuxSyscallFrame call)
    {
        if ((opened->Flags & 3) == 0) return new(WritableDiskProgress.Immediate, BadDescriptor);
        if (call.Argument2 != 1 || call.Argument4 != 0 || call.Argument5 != W.RwfNoAppend) return new(WritableDiskProgress.Immediate, WritableUnsupported);
        if (call.Argument3 > long.MaxValue) return new(WritableDiskProgress.Immediate, Invalid);
        ulong* vector = stackalloc ulong[2];
        if (!WritableInput(state, call.Argument1, (byte*)vector, 16)) return new(WritableDiskProgress.Immediate, Fault);
        if (vector[1] > W.IoBytes) return new(WritableDiskProgress.Immediate, Invalid);
        uint bytes = (uint)vector[1];
        if (bytes != 0 && !WritableInput(state, vector[0], state->Owner.Scratch, bytes))
        { Clear(state->Owner.Scratch, state->Owner.ScratchBytes); return new(WritableDiskProgress.Immediate, Fault); }
        long started = WritableStart(state, worker, call.Number, WritableWrite);
        if (started != 0) { Clear(state->Owner.Scratch, state->Owner.ScratchBytes); return new(WritableDiskProgress.Immediate, started); }
        WritableDiskState* d = state->WritableDisk; WritableDiskRequest* r = &d->Request;
        WritableBorrow(state, (uint)call.Argument0, opened); r->Positioned = true; r->Offset = call.Argument3; r->Requested = bytes;
        W.Result begun = W.Pwritev2SingleWithFreeSearch(d->Provider, d->Bindings[r->Description].File,
            new ReadOnlySpan<byte>(state->Owner.Scratch, (int)bytes), r->Offset, W.RwfNoAppend, d->Search);
        Clear(state->Owner.Scratch, state->Owner.ScratchBytes); return WritableStarted(state, begun);
    }
    private static WritableDiskResult CompleteWritableMutation(State* state)
    {
        WritableDiskState* d = state->WritableDisk; WritableDiskRequest* r = &d->Request;
        W.Result done = W.Complete(d->Provider, r->ProviderRequest);
        if (done.Phase != W.Progress.Immediate) return AbandonWritable(state, Io);
        r->ProviderRequest = default;
        if (done.Value >= 0)
            for (uint i = 0; i < d->BindingCount; i++)
                if (d->Bindings[i].Active == 1 && d->Bindings[i].Closing == 0)
                {
                    W.Metadata metadata = W.InspectCopied(d->Provider, d->Bindings[i].File);
                    if (!metadata.Valid) { r->Phase = WritableRecovery; r->Failure = C.Status.InvalidState; return ObserveWritable(d); }
                    state->Owner.Descriptions[i].Bytes = metadata.Length;
                }
        return FinishWritable(state, done.Value, done.Failure);
    }
    // Linux x64 stat is exactly 144 bytes. FAT modification fields are treated
    // as UTC under this finite fixture policy; unavailable times stay zero.
    // Invalid civil fields are corrupt metadata (EIO), never normalized dates.
    private static long WritableStatOutput(State* state, W.StatMetadata metadata, ulong address, uint outputBytes, out C.Status failure)
    {
        failure = C.Status.Success;
        C.OpenFacts facts = metadata.Facts; C.FileToken file = facts.File;
        ushort time = (ushort)(file.Fingerprint2 >> 48), date = (ushort)file.Fingerprint3;
        long modified = 0;
        if ((time != 0 || date != 0) && !OS.Std.Time.GregorianUtc.TryUnixSeconds((uint)(1980 + (date >> 9)),
            (uint)((date >> 5) & 15), (uint)(date & 31), (uint)(time >> 11), (uint)((time >> 5) & 63), (uint)((time & 31) * 2), out modified))
        { failure = C.Status.CorruptDirectory; return Io; }
        if (outputBytes < 144) return Fault;
        byte* wire = stackalloc byte[144]; Clear(wire, 144);
        *(ulong*)(wire + 8) = file.EntryOffset / 32 + 1; *(ulong*)(wire + 16) = 1;
        *(uint*)(wire + 24) = ((file.Fingerprint1 >> 24) & 1) != 0 ? 0x8100U : 0x8180U; // regular 0400 or 0600
        *(ulong*)(wire + 48) = file.Length; *(ulong*)(wire + 56) = facts.ClusterBytes;
        *(ulong*)(wire + 64) = facts.Allocated512Blocks; *(long*)(wire + 88) = modified;
        return Output(state, address, wire, 144) ? 0 : Fault;
    }

    // Copy32-byte x64 flock with explicit field offsets; padding is preserved
    // for GETLK, and a no-conflict result changes only l_type. l_pid is data,
    // never process authority. No blocking/OFD/other whence support.
    private static WritableDiskResult WritableLock(State* state, Description* opened, LinuxSyscallFrame call)
    {
        if (call.Argument1 is not (5 or 6)) return new(WritableDiskProgress.NotHandled);
        byte* wire = stackalloc byte[32];
        if (!WritableInput(state, call.Argument2, wire, 32)) return new(WritableDiskProgress.Immediate, Fault);
        W.LockRequest request = new(*(short*)wire, *(short*)(wire + 2), *(long*)(wire + 8), *(long*)(wire + 16), *(int*)(wire + 24));
        WritableDiskState* d = state->WritableDisk;
        W.LockResult result = W.FcntlLockForProcess(d->Provider, d->Process, d->Bindings[opened->Pipe].File, (uint)call.Argument1, request);
        if (result.Value == 0 && call.Argument1 == 5)
        {
            *(short*)wire = result.Lock.Type;
            if (result.Lock.Type != 2)
            { *(short*)(wire + 2) = result.Lock.Whence; *(long*)(wire + 8) = result.Lock.Start; *(long*)(wire + 16) = result.Lock.Length; *(int*)(wire + 24) = result.Lock.Pid; }
            if (!Output(state, call.Argument2, wire, 32)) return new(WritableDiskProgress.Immediate, Fault);
        }
        return new(WritableDiskProgress.Immediate, result.Value);
    }
    private static bool WritableInput(State* s, ulong address, byte* destination, uint bytes) =>
        LinuxSyscallGateway.ValidateRange(address, bytes, LinuxUserPageAccess.Read, s->Owner.User) &&
        s->Owner.User.CopyFromUser(s->Owner.User.Context, address, destination, bytes) == 1;
    private static WritableDiskResult WritableStarted(State* state, W.Result begun)
    {
        WritableDiskRequest* r = &state->WritableDisk->Request;
        r->ProviderRequest = begun.Request; r->Value = begun.Value; r->Failure = begun.Failure;
        if (begun.Phase == W.Progress.Immediate) return FinishWritable(state, begun.Value, begun.Failure);
        r->Phase = begun.Phase == W.Progress.Ready ? WritableReady : begun.Phase == W.Progress.Pending ? WritablePending : WritableRecovery;
        return ObserveWritable(state->WritableDisk);
    }
    private static bool WritableReservationCurrent(State* s)
    {
        WritableDiskRequest* r = &s->WritableDisk->Request;
        return r->Reserved && r->ReservedFd < s->Owner.DescriptorCount && r->ReservedDescription < s->Owner.DescriptionCount &&
            s->Owner.Slots[r->ReservedFd].Description == 0 && s->Owner.Slots[r->ReservedFd].Generation == r->ReservationGeneration &&
            s->Owner.Descriptions[r->ReservedDescription].Type == 0 && s->Owner.Descriptions[r->ReservedDescription].References == 0 &&
            s->Owner.Descriptions[r->ReservedDescription].Borrowers == 0 && s->NextInode != uint.MaxValue;
    }
    private static WritableDiskResult PublishWritableOpen(State* s)
    {
        WritableDiskState* d = s->WritableDisk; WritableDiskRequest* r = &d->Request;
        if (!WritableReservationCurrent(s)) return AbandonWritable(s, Io);
        W.Metadata metadata = W.InspectCopied(d->Provider, r->TemporaryOpened);
        if (!metadata.Valid) return AbandonWritable(s, Io);
        uint fd = r->ReservedFd, index = r->ReservedDescription;
        d->Bindings[index] = new() { Active = 1, File = r->TemporaryOpened, Generation = r->Generation };
        s->Owner.Descriptions[index] = new() { Type = (uint)Kind.WritableDisk, References = 1, Pipe = index,
            Flags = (r->OpenFlags & (3 | NoFollow | LargeFile)) | LargeFile, Bytes = metadata.Length, Inode = s->NextInode++ };
        Assign(s, fd, index, (r->OpenFlags & CloseOnExec) != 0 ? 1U : 0U);
        r->TemporaryOpened = default; r->Reserved = false; return FinishWritable(s, fd);
    }
    private static WritableDiskResult WritableSeek(State* s, Description* d, long offset, ulong whence)
    {
        W.Metadata info = W.InspectCopied(s->WritableDisk->Provider, s->WritableDisk->Bindings[d->Pipe].File);
        if (!info.Valid) return new(WritableDiskProgress.Immediate, Io);
        if (info.Directory) return new(WritableDiskProgress.Immediate, NotSeekable);
        if (whence > 2) return new(WritableDiskProgress.Immediate, Invalid);
        ulong origin = whence == 0 ? 0 : whence == 1 ? d->Position : info.Length, next;
        if (offset < 0) { ulong magnitude = (ulong)(-(offset + 1)) + 1; if (magnitude > origin) return new(WritableDiskProgress.Immediate, Invalid); next = origin - magnitude; }
        else { if ((ulong)offset > (ulong)long.MaxValue - origin) return new(WritableDiskProgress.Immediate, Overflow); next = origin + (ulong)offset; }
        d->Position = next; return new(WritableDiskProgress.Immediate, (long)next);
    }
    private static long CloseWritableDiskFd(State* s, uint fd)
    {
        Description* description = Find(s, fd); if (description == null || !WritableBound(s)) return Io;
        WritableDiskState* d = s->WritableDisk; WritableDiskBinding* binding = d->Bindings + description->Pipe;
        long closed = W.CloseForProcess(d->Provider, d->Process, binding->File); if (closed != 0) return closed;
        binding->Closing = 1;
        WritableDiskRequest* r = &d->Request;
        if (r->Borrowed && r->Description == description->Pipe)
        {
            r->CopyRevoked = true; r->Closed = true; r->Value = BadDescriptor; r->Cancelling = true;
            if (!d->Provider->RequestActive) { r->ProviderRequest = default; r->Phase = WritableReady; }
            else r->Phase = WritableDraining;
        }
        return 0;
    }
    private static bool RetireWritableBinding(State* s, uint index)
    {
        if (!WritableBound(s) || index >= s->WritableDisk->BindingCount) return false;
        WritableDiskBinding* binding = s->WritableDisk->Bindings + index;
        if (binding->Active != 1 || binding->Closing != 1) return false;
        *binding = default; return true;
    }
    private static WritableDiskResult AbandonWritable(State* s, long error)
    {
        WritableDiskRequest* r = &s->WritableDisk->Request;
        r->CopyRevoked = true; r->Orphan = true; r->Worker = default; r->Value = error;
        return CancelWritableProvider(s);
    }
    private static WritableDiskResult CancelWritableProvider(State* s)
    {
        WritableDiskState* d = s->WritableDisk; WritableDiskRequest* r = &d->Request;
        if (r->TemporaryOpened.Owner != 0)
        { if (W.CloseForProcess(d->Provider, d->Process, r->TemporaryOpened) != 0) { r->Phase = WritableRecovery; return ObserveWritable(d); } r->TemporaryOpened = default; }
        r->Cancelling = true;
        W.Result cancelled = d->Provider->RequestActive ? W.Cancel(d->Provider, r->ProviderRequest) : new(W.Progress.Immediate);
        if (cancelled.Phase == W.Progress.RecoveryRequired) { r->Phase = WritableRecovery; r->Failure = cancelled.Failure; return ObserveWritable(d); }
        if (!d->Provider->RequestActive) return FinishWritable(s, r->Value, r->Failure);
        r->Phase = WritableDraining; return ObserveWritable(d);
    }
    private static WritableDiskResult FinishWritable(State* s, long value, C.Status failure = C.Status.Success)
    {
        WritableDiskRequest* r = &s->WritableDisk->Request;
        if (r->Borrowed && !DropDescription(s, r->Description, true))
        { r->Phase = WritableRecovery; r->Worker = default; r->Orphan = true; r->CopyRevoked = true; r->Value = Io; return ObserveWritable(s->WritableDisk); }
        ulong self = r->Self; *r = default; r->Self = self; return new(WritableDiskProgress.Immediate, value, failure: failure);
    }
    private static WritableDiskResult ObserveWritable(WritableDiskState* d)
    {
        WritableDiskRequest* r = &d->Request;
        return new(r->Phase == WritableReady ? WritableDiskProgress.Ready : r->Phase == WritableDraining ? WritableDiskProgress.Draining :
            r->Phase == WritableRecovery ? WritableDiskProgress.RecoveryRequired : WritableDiskProgress.Pending,
            r->Value, new(d), r->Worker, r->Failure);
    }
    private static bool WritableRequestFor(State* s, WritableDiskToken t) => s->WritableDisk != null && t.Owner == (ulong)s->WritableDisk &&
        t.Epoch == s->WritableDisk->Epoch && t.Generation != 0 && t.Generation == s->WritableDisk->Request.Generation && s->WritableDisk->Request.Phase != 0;

    private static bool WritableBound(State* s)
    {
        if (!WritableExtent(s, (ulong)sizeof(State)) || s->Initialized != 1 || !Storage(s, s->Owner)) return false;
        WritableDiskState* d = s->WritableDisk;
        if (!WritableExtent(d, (ulong)sizeof(WritableDiskState)) || d->Self != (ulong)d || d->Owner != s || d->Epoch == 0 ||
            d->BindingCount != s->Owner.DescriptionCount || !WritableExtent(d->Bindings, d->BindingCount * (ulong)sizeof(WritableDiskBinding)) ||
            !WritableExtent(d->Provider, (ulong)sizeof(W.State)) || d->Process.Owner != (ulong)d->Provider || d->Process.Epoch != d->Epoch ||
            d->Process.Generation == 0 || d->Process.Pid == 0 || d->Process.Index >= W.MaximumProcesses ||
            !WritableBaseOutside(s, (ulong)d, (ulong)sizeof(WritableDiskState)) ||
            !WritableBaseOutside(s, (ulong)d->Bindings, d->BindingCount * (ulong)sizeof(WritableDiskBinding)) ||
            Overlap((ulong)d, (ulong)sizeof(WritableDiskState), (ulong)d->Bindings, d->BindingCount * (ulong)sizeof(WritableDiskBinding)) ||
            !W.CanRetainStorage(d->Provider, d->Epoch, (ulong)d, (ulong)sizeof(WritableDiskState)) ||
            !W.CanRetainStorage(d->Provider, d->Epoch, (ulong)d->Bindings, d->BindingCount * (ulong)sizeof(WritableDiskBinding))) return false;
        for (uint i = 0; i < WritableBaseExtentCount(s); i++)
        { WritableBaseExtentAt(s, i, out ulong start, out ulong bytes); if (bytes != 0 && !W.CanRetainStorage(d->Provider, d->Epoch, start, bytes)) return false; }
        return true;
    }
    private static bool AuditWritableDisk(State* s)
    {
        if (s->WritableDisk == null) return true;
        if (!WritableBound(s)) return false;
        WritableDiskState* d = s->WritableDisk; WritableDiskRequest* r = &d->Request;
        if (r->Self != (ulong)r || r->Phase > WritableRecovery) return false;
        if (r->Phase == 0)
        { if (r->Generation != 0 || r->Reserved || r->Borrowed || r->Worker.IsValid || r->ProviderRequest.Owner != 0 || r->TemporaryOpened.Owner != 0 || d->Provider->RequestActive) return false; }
        else if (r->Generation == 0 || r->Generation > d->NextGeneration || r->Operation is < WritableOpen or > WritableStat ||
            r->Orphan != !r->Worker.IsValid || r->Orphan && !r->CopyRevoked ||
            r->ProviderRequest.Owner != 0 && (r->ProviderRequest.Owner != (ulong)d->Provider || r->ProviderRequest.Epoch != d->Epoch || r->ProviderRequest.Generation == 0)) return false;
        if (r->Reserved && (!WritableReservationCurrent(s) || r->Operation != WritableOpen)) return false;
        if (r->Borrowed && (r->Description >= d->BindingCount || r->OriginalFd.Owner != s || r->OriginalFd.Generation == 0 ||
            r->OriginalFd.Number >= s->Owner.DescriptorCount || d->Bindings[r->Description].Generation != r->BindingGeneration)) return false;
        for (uint i = 0; i < d->BindingCount; i++)
        {
            WritableDiskBinding binding = d->Bindings[i]; Description description = s->Owner.Descriptions[i];
            if (binding.Active == 0)
            { if (binding.Closing != 0 || binding.Generation != 0 || binding.File.Owner != 0 || (Kind)description.Type == Kind.WritableDisk) return false; continue; }
            if (binding.Active != 1 || binding.Closing > 1 || binding.Generation == 0 || binding.Generation > d->NextGeneration ||
                binding.File.Owner != (ulong)d->Provider || binding.File.Epoch != d->Epoch || binding.File.Generation == 0 ||
                binding.File.Number is < 3 or >= W.MaximumDescriptors || (Kind)description.Type != Kind.WritableDisk || description.Pipe != i ||
                description.Data != null || description.References > 1 || description.Borrowers != (r->Borrowed && r->Description == i ? 1U : 0U) ||
                binding.Closing != 0 && (description.References != 0 || description.Borrowers != 1 || !r->Closed && !r->Orphan)) return false;
            if (binding.Closing == 0)
            { W.Result captured = W.CaptureForProcess(d->Provider, d->Process, binding.File.Number); if (captured.Value < 0 || !WritableFile(captured.Descriptor, binding.File)) return false; }
        }
        return true;
    }

    private static uint WritableBaseExtentCount(State* s) => 13 + s->Owner.ViewCount * 2;
    private static void WritableBaseExtentAt(State* s, uint index, out ulong start, out ulong bytes)
    {
        var a = s->Owner; start = bytes = 0;
        ulong* ranges = stackalloc ulong[26] { (ulong)s, (ulong)sizeof(State), (ulong)a.Slots, a.DescriptorCount * (ulong)sizeof(Slot),
            (ulong)a.Descriptions, a.DescriptionCount * (ulong)sizeof(Description), (ulong)a.Pipes, a.PipeCount * (ulong)sizeof(LinuxPipe),
            (ulong)a.PipeBuffers, a.PipeCount * a.PipeCapacity, (ulong)a.Snapshots, a.DescriptionCount * SnapshotBytes, (ulong)a.Scratch, a.ScratchBytes,
            (ulong)a.Memory, (ulong)sizeof(LinuxMemoryServices.State), (ulong)a.Memory->Memory.Pool, a.Memory->Memory.PoolPages * 4096UL,
            (ulong)a.Memory->Memory.Tables, a.Memory->Memory.TablePages * 4096UL, (ulong)a.Memory->Memory.CandidateTables, a.Memory->Memory.TablePages * 4096UL,
            (ulong)a.Memory->Memory.StaticRanges, a.Memory->Memory.StaticCount * (ulong)sizeof(X64UserMappingRange), (ulong)a.Views, a.ViewCount * (ulong)sizeof(View) };
        if (index < 13) { start = ranges[index * 2]; bytes = ranges[index * 2 + 1]; }
        else if (index < WritableBaseExtentCount(s))
        { View view = a.Views[(index - 13) / 2]; bool path = ((index - 13) & 1) == 0; start = (ulong)(path ? view.Path : view.Data); bytes = path ? view.PathBytes : view.Bytes; }
    }
    private static bool WritableBaseOutside(State* s, ulong address, ulong bytes, bool includeUserBacking = true)
    {
        for (uint i = 0; i < WritableBaseExtentCount(s); i++)
        {
            WritableBaseExtentAt(s, i, out ulong start, out ulong count);
            if (count == 0 || !Overlap(address, bytes, start, count)) continue;
            if (i != 8 || includeUserBacking || address < start || address - start > count || bytes > count - (address - start)) return false;
        }
        return true;
    }
    private static long WritableReadPath(State* s, ulong address, byte* path, out uint length)
    {
        length = 0;
        for (; length < MaximumPath; length++)
        {
            if (address > ulong.MaxValue - length || !LinuxSyscallGateway.ValidateRange(address + length, 1, LinuxUserPageAccess.Read, s->Owner.User) ||
                s->Owner.User.CopyFromUser(s->Owner.User.Context, address + length, path + length, 1) != 1) return Fault;
            if (path[length] == 0) return length == 0 ? Missing : 0;
        }
        return NameTooLong;
    }
    private static bool WritablePath(byte* path, uint bytes) => bytes >= 5 && path[0] == '/' && path[1] == 'd' && path[2] == 'a' &&
        path[3] == 't' && path[4] == 'a' && (bytes == 5 || path[5] == '/');
}
#endif
