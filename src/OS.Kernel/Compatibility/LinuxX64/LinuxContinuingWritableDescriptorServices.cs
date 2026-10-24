using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>Retained POD descriptor provider over one exclusively borrowed
/// continuing FAT owner. One serialized supervisor keeps every extent stable
/// and disjoint until Detach succeeds, BEFORE stopping/freeing the engine or
/// device backing. No whole-image snapshot, ref struct, user pointer, scheduler
/// or syscall gateway is retained. Root files and empty creation are confined
/// to /data; writes remain within existing length. Nested paths, growth,
/// deletion/truncation, locks and POSIX unlink retention are not supplied.</summary>
public static unsafe class LinuxContinuingWritableDescriptorServices
{
    public const uint MaximumDescriptors = 32, MaximumDescriptions = 32, PathBytes = 256, IoBytes = 8192;
    public const uint Create = 0x40, Exclusive = 0x80, CloseOnExec = 0x80000, NoFollow = 0x20000,
        DirectoryOnly = 0x10000, LargeFile = 0x8000, RwfNoAppend = 0x20;
    private const long BadFd = -9, Again = -11, Fault = -14, Exists = -17, Invalid = -22, TableFull = -23,
        FdFull = -24, NoSpace = -28, Unsupported = -95, Overflow = -75, Stale = -116, Interrupted = -4,
        Missing = -2, IsDirectory = -21, Access = -13, NotDirectory = -20, Io = -5, IllegalSeek = -29;
    public enum Progress : byte { Rejected, Immediate, Pending, Ready, RecoveryRequired, Retired }
    internal enum Operation : byte { Open, Read, Write, Sync }
    internal enum Step : byte { Lookup, Create, CreatedLookup, Authenticate, Read, Write, Flush, Done }

    /// <summary>Zeroed caller-owned fd/description arrays. FD0..2 are reserved.
    /// A description survives its last close while a pending request borrows it;
    /// duplicates share its cursor, independent opens never do.</summary>
    public struct Descriptor { internal ulong Generation; internal uint Description, Flags; internal bool Reserved; }
    public struct Description
    {
        internal ulong Generation, Position;
        internal C.FileToken File;
        internal uint References, Borrowers, Flags;
        internal ushort NameLength;
        internal bool Active, Reserved, Directory;
        internal fixed byte Name[(int)PathBytes];
    }
    public struct Admission
    {
        public C.State* Engine;
        public Descriptor* Descriptors;
        public Description* Descriptions;
        public byte* Input;
        public ulong Epoch;
        public uint DescriptorCount, DescriptionCount, InputBytes;
        public bool ExclusiveOwnerValidated;
    }
    public readonly struct DescriptorToken
    {
        public readonly ulong Owner, Epoch, Generation;
        public readonly uint Number;
        internal DescriptorToken(State* s, uint number)
        { Owner = (ulong)s; Epoch = s->Input.Epoch; Number = number; Generation = s->Input.Descriptors[number].Generation; }
    }
    public readonly struct RequestToken
    {
        public readonly ulong Owner, Epoch, Generation;
        internal RequestToken(State* s) { Owner = (ulong)s; Epoch = s->Input.Epoch; Generation = s->Work.Generation; }
    }
    public readonly struct Result
    {
        public readonly Progress Phase;
        public readonly long Value;
        public readonly RequestToken Request;
        public readonly DescriptorToken Descriptor;
        public readonly C.Status Failure;
        internal Result(Progress phase, long value = 0, RequestToken request = default,
            DescriptorToken descriptor = default, C.Status failure = C.Status.Success)
        { Phase = phase; Value = value; Request = request; Descriptor = descriptor; Failure = failure; }
    }
    public readonly struct Metadata
    {
        public readonly ulong EntryOffset, Length, DescriptionGeneration;
        public readonly bool Valid, Directory;
        internal Metadata(Description* d) { EntryOffset = d->File.EntryOffset; Length = d->File.Length; DescriptionGeneration = d->Generation; Directory = d->Directory; Valid = true; }
    }
    internal struct Pending
    {
        internal ulong Generation, DescriptionGeneration, Offset;
        internal DescriptorToken Original;
        internal C.RequestToken EngineRequest;
        internal C.FileToken File;
        internal Operation Kind;
        internal Step Step;
        internal uint Description, Number, Flags, Bytes, Expected, AliasAttempts;
        internal ushort NameLength;
        internal bool Active, Ready, Cancelled, Discard, Positioned, Borrowed, RevisionAccepted;
        internal long Value;
        internal C.Status Failure;
        internal fixed byte Name[(int)PathBytes];
    }
    /// <summary>Noncopyable-by-contract retained owner. Contains only POD and
    /// borrowed pointers, never Span or managed references. Dirty/recovery
    /// failure retains the complete provider and engine lifetime; resetting the
    /// device is not permission to discard either owner's unresolved state.</summary>
    public struct State
    {
        internal ulong Self, KnownRevision, NextGeneration, NextAlias;
        internal Admission Input;
        internal Pending Work;
        internal bool Active, Retiring, Poisoned, RetiredValue;
        public readonly bool RequestActive => Work.Active;
        public readonly bool IsRetiring => Retiring;
        public readonly bool Retired => RetiredValue;
        public readonly bool Quarantined => Poisoned;
    }

    /// <summary>Borrow an already idle mounted engine and zeroed exclusive
    /// tables/input storage. Structural engine alias checks include its DMA and
    /// MMIO extents. Failure writes nothing and issues no request.</summary>
    public static bool Start(State* s, in Admission a)
    {
        if (!a.ExclusiveOwnerValidated || a.Epoch == 0 || a.DescriptorCount is < 4 or > MaximumDescriptors ||
            a.DescriptionCount is < 1 or > MaximumDescriptions || a.InputBytes != IoBytes ||
            !Ordinary((ulong)s, (ulong)sizeof(State), 8) || !Ordinary((ulong)a.Engine, (ulong)sizeof(C.State), 8) ||
            !a.Engine->IsMounted || a.Engine->Retired || a.Engine->RequestActive || a.Engine->Dirty) return false;
        ulong* starts = stackalloc ulong[4] { (ulong)s, (ulong)a.Descriptors, (ulong)a.Descriptions, (ulong)a.Input };
        ulong* sizes = stackalloc ulong[4] { (ulong)sizeof(State), a.DescriptorCount * (ulong)sizeof(Descriptor), a.DescriptionCount * (ulong)sizeof(Description), IoBytes };
        for (int i = 0; i < 4; i++)
        {
            if (!Ordinary(starts[i], sizes[i], i == 3 ? 1UL : 8UL) || !C.CanBorrowStorage(a.Engine, a.Epoch, starts[i], sizes[i])) return false;
            for (int j = 0; j < i; j++) if (Overlap(starts[i], sizes[i], starts[j], sizes[j])) return false;
            for (ulong j = 0; j < sizes[i]; j++) if (((byte*)starts[i])[j] != 0) return false;
        }
        s->Self = (ulong)s; s->Input = a; s->KnownRevision = a.Engine->VolumeRevision; s->NextAlias = 1; s->Active = true; return true;
    }

    /// <summary>Copy the current fd identity. Its generation grants no pin or
    /// permission to resolve a subsequently reused descriptor number.</summary>
    public static Result Capture(State* s, uint number) => Bound(s) && Find(s, number, out _) ?
        new(Progress.Immediate, number, descriptor: new(s, number)) : Immediate(BadFd);

    /// <summary>Inspect copied open metadata without new I/O. The caller must
    /// retain the original token and separately request fresh authentication
    /// for a future Linux stat wire; this is not a live disk observation.</summary>
    public static Metadata InspectCopied(State* s, DescriptorToken token) =>
        Current(s, token, out uint description) && Revision(s) ? new(s->Input.Descriptions + description) : default;

    /// <summary>Open exact /data as a read-only barrier descriptor, or copy one
    /// printable-ASCII root component and begin actual metadata reads. Creation
    /// retries at most64 deterministic aliases and publishes an fd only after
    /// engine success and an independent reopen. Reservations prevent another
    /// root open from consuming the pending fd/description.</summary>
    public static Result Open(State* s, ReadOnlySpan<byte> path, uint flags)
    {
        if (!Bound(s) || s->Retiring) return Immediate(BadFd);
        if (!Revision(s)) return Immediate(Io);
        const uint allowed = 3 | Create | Exclusive | CloseOnExec | NoFollow | DirectoryOnly | LargeFile;
        if ((flags & ~allowed) != 0 || (flags & 3) == 3) return Immediate(Unsupported);
        if (!External(s, path)) return Immediate(Fault);
        bool root = path.SequenceEqual("/data"u8) || path.SequenceEqual("/data/"u8);
        if (root && (flags & (3 | Create | Exclusive)) != 0) return Immediate(IsDirectory);
        long policy = root ? 0 : Path(path); if (policy != 0) return Immediate(policy);
        if (!root && (flags & DirectoryOnly) != 0) return Immediate(NotDirectory);
        if (!root && s->Work.Active) return Immediate(Again);
        int fd = FreeFd(s), description = FreeDescription(s); if (fd < 0) return Immediate(FdFull); if (description < 0) return Immediate(TableFull);
        if (!root && s->NextGeneration == ulong.MaxValue) return Immediate(Overflow);
        Reserve(s, (uint)fd, (uint)description, flags);
        Description* d = s->Input.Descriptions + description;
        if (root)
        {
            d->Directory = true; d->Reserved = false; d->References = 1; s->Input.Descriptors[fd].Reserved = false;
            return new(Progress.Immediate, fd, descriptor: new(s, (uint)fd));
        }
        s->Work = new() { Active = true, Generation = ++s->NextGeneration, Kind = Operation.Open, Step = Step.Lookup,
            Description = (uint)description, DescriptionGeneration = d->Generation, Number = (uint)fd, Flags = flags, NameLength = (ushort)(path.Length - 6) };
        for (int i = 6; i < path.Length; i++) s->Work.Name[i - 6] = path[i];
        var status = Lookup(s); if (status != C.Status.Pending) Finish(s, Map(status), status);
        return Observe(s);
    }

    /// <summary>Share a live description and cursor. The new descriptor has
    /// its own nonwrapping generation and close-on-exec flag.</summary>
    public static Result Dup(State* s, DescriptorToken token)
    {
        if (!Current(s, token, out uint description) || s->Retiring) return Immediate(BadFd);
        if (!Revision(s)) return Immediate(Io);
        int fd = FreeFd(s); if (fd < 0) return Immediate(FdFull);
        Description* d = s->Input.Descriptions + description; if (d->References == uint.MaxValue) return Immediate(Overflow);
        Descriptor* f = s->Input.Descriptors + fd; f->Generation++; f->Description = description + 1; f->Flags = 0; f->Reserved = false; d->References++;
        return new(Progress.Immediate, fd, descriptor: new(s, (uint)fd));
    }

    /// <summary>Revoke the fd before cancelling a borrower. A reused number
    /// cannot receive late bytes/cursor commits. The old description remains
    /// owned through actual engine cancellation drain; dirty failures retain it.</summary>
    public static long Close(State* s, DescriptorToken token)
    {
        if (!Current(s, token, out uint description)) return BadFd;
        s->Input.Descriptors[token.Number].Description = s->Input.Descriptors[token.Number].Flags = 0;
        Description* d = s->Input.Descriptions + description; d->References--;
        if (s->Work.Active && s->Work.Borrowed && Same(s->Work.Original, token))
        { s->Work.Discard = true; Cancel(s, new(s)); }
        RetireDescription(s, description); return 0;
    }

    /// <summary>Move one shared cursor without allocating media. A pending
    /// borrower of this description rejects cursor changes until consumed.</summary>
    public static Result Seek(State* s, DescriptorToken token, long offset, uint whence)
    {
        if (!Current(s, token, out uint description) || s->Retiring) return Immediate(BadFd);
        if (!Revision(s)) return Immediate(Io);
        Description* d = s->Input.Descriptions + description; if (d->Directory) return Immediate(IllegalSeek);
        if (d->Borrowers != 0) return Immediate(Again);
        if (whence > 2) return Immediate(Invalid);
        ulong start = whence == 0 ? 0 : whence == 1 ? d->Position : d->File.Length;
        ulong next;
        if (offset < 0) { ulong subtract = unchecked((ulong)(-(offset + 1))) + 1; if (subtract > start) return Immediate(Invalid); next = start - subtract; }
        else { if ((ulong)offset > long.MaxValue - start) return Immediate(Overflow); next = start + (ulong)offset; }
        d->Position = next; return Immediate((long)next);
    }

    /// <summary>Copy at most8192 write bytes before return. The physical entry
    /// is reopened/authenticated before the engine receives the copied bytes.
    /// Growth and append are deliberately unsupported in this initial port.</summary>
    public static Result BeginWrite(State* s, DescriptorToken token, ReadOnlySpan<byte> bytes, ulong offset = 0, bool positioned = false)
    {
        if (!Current(s, token, out uint description) || s->Retiring) return Immediate(BadFd);
        if (!Revision(s)) return Immediate(Io);
        Description* d = s->Input.Descriptions + description;
        if (d->Directory) return Immediate(IsDirectory); if ((d->Flags & 3) == 0) return Immediate(BadFd);
        if (!External(s, bytes)) return Immediate(Fault);
        if (bytes.Length > IoBytes) return Immediate(Invalid);
        ulong position = positioned ? offset : d->Position;
        if (position > long.MaxValue) return Immediate(Invalid);
        if (position > d->File.Length || (ulong)bytes.Length > d->File.Length - position) return Immediate(Unsupported);
        if (bytes.Length == 0) return Immediate(0);
        var started = BeginBorrow(s, token, description, Operation.Write, position, (uint)bytes.Length, positioned);
        if (started.Phase != Progress.Pending) return started;
        bytes.CopyTo(new Span<byte>(s->Input.Input, bytes.Length)); return started;
    }
    /// <summary>Copied single-iovec leaf for the measured explicit-offset wire.
    /// The future syscall adapter owns iovec/user-memory validation separately.</summary>
    public static Result Pwritev2Single(State* s, DescriptorToken token, ReadOnlySpan<byte> bytes, ulong offset, uint flags) =>
        flags == RwfNoAppend ? BeginWrite(s, token, bytes, offset, true) : Immediate(Unsupported);

    /// <summary>Begin a held bounded read. The final copy is a separate call
    /// after original fd/description generation checks; no destination is kept.</summary>
    public static Result BeginRead(State* s, DescriptorToken token, uint bytes, ulong offset = 0, bool positioned = false)
    {
        if (!Current(s, token, out uint description) || s->Retiring) return Immediate(BadFd);
        if (!Revision(s)) return Immediate(Io);
        Description* d = s->Input.Descriptions + description;
        if (d->Directory) return Immediate(IsDirectory); if ((d->Flags & 3) == 1) return Immediate(BadFd);
        if (bytes > IoBytes || offset > long.MaxValue) return Immediate(Invalid);
        if (bytes == 0) return Immediate(0);
        return BeginBorrow(s, token, description, Operation.Read, positioned ? offset : d->Position, bytes, positioned);
    }

    /// <summary>Issue an actual engine volume FLUSH for a file or root fd.
    /// File fdatasync uses the same stronger barrier; directory fdatasync rejects.
    /// This supplies no filesystem crash or power-loss durability assertion.</summary>
    public static Result BeginSync(State* s, DescriptorToken token, bool dataOnly = false)
    {
        if (!Current(s, token, out uint description) || s->Retiring) return Immediate(BadFd);
        if (!Revision(s)) return Immediate(Io);
        if (dataOnly && s->Input.Descriptions[description].Directory) return Immediate(Invalid);
        return BeginBorrow(s, token, description, Operation.Sync, 0, 0, false);
    }

    /// <summary>One bounded engine step. It never copies caller/user output
    /// or commits a description cursor. Positive read readiness holds the
    /// engine's copied bytes until CopyRead; unrelated work cannot bypass it.</summary>
    public static Result Pump(State* s)
    {
        if (!Bound(s)) return new(Progress.Rejected);
        if (!s->Work.Active) return s->Poisoned ? Immediate(Io) : Immediate(0);
        if (s->Work.Ready) return Observe(s);
        if (!Revision(s)) return Recovery(s);
        var status = C.Pump(s->Input.Engine);
        if (!AcceptOwnRevision(s)) return Recovery(s);
        if (status == C.Status.Pending) return Observe(s);
        if (status is C.Status.RecoveryRequired || s->Input.Engine->Dirty) return Recovery(s);
        if (status is not C.Status.Ready)
        { s->Poisoned = true; Finish(s, Io, status); return AfterFinish(s); }
        Pending* p = &s->Work;
        if (p->Step is Step.Lookup or Step.CreatedLookup or Step.Authenticate)
        {
            C.FileToken file = default; status = C.CopyOpen(s->Input.Engine, p->EngineRequest, &file);
            if (p->Cancelled) { Finish(s, Interrupted, status); return AfterFinish(s); }
            if (p->Step == Step.Lookup && status == C.Status.NotFound && (p->Flags & Create) != 0)
            { p->Step = Step.Create; status = CreateEmpty(s); if (status != C.Status.Pending) Finish(s, Map(status), status); return AfterFinish(s); }
            if (status != C.Status.Success) { Finish(s, Map(status), status); return AfterFinish(s); }
            if (p->Step == Step.Authenticate)
            {
                if (!Physical(s->Input.Descriptions[p->Description].File, file)) { Finish(s, Stale, C.Status.StaleToken); return AfterFinish(s); }
                s->Input.Descriptions[p->Description].File = file; p->File = file;
                status = BeginData(s); if (status is not (C.Status.Pending or C.Status.Ready)) Finish(s, Map(status), status);
                return AfterFinish(s);
            }
            if (p->Step == Step.Lookup && (p->Flags & (Create | Exclusive)) == (Create | Exclusive))
            { Finish(s, Exists, C.Status.AlreadyExists); return AfterFinish(s); }
            if ((p->Flags & 3) != 0 && ((file.Fingerprint1 >> 24) & 1) != 0) { Finish(s, Access, C.Status.ReadOnly); return AfterFinish(s); }
            p->File = file; Finish(s, p->Number, C.Status.Success); return AfterFinish(s);
        }
        if (p->Step == Step.Read)
        {
            var failure = s->Input.Engine->LastFailure;
            if (p->Cancelled)
            { DrainRead(s); return AfterFinish(s); }
            Finish(s, failure == C.Status.Success ? p->Expected : Map(failure), failure); return AfterFinish(s);
        }
        C.Result result = default; status = C.CopyResult(s->Input.Engine, p->EngineRequest, &result);
        if (p->Step == Step.Create && status == C.Status.AlreadyExists && !p->Cancelled)
        { status = CreateEmpty(s); if (status != C.Status.Pending) Finish(s, Map(status), status); return AfterFinish(s); }
        if (p->Step == Step.Create && status == C.Status.Success && !p->Cancelled)
        { p->Step = Step.CreatedLookup; status = Lookup(s); if (status != C.Status.Pending) Finish(s, Map(status), status); return AfterFinish(s); }
        if (p->Cancelled) Finish(s, Interrupted, status);
        else if (status != C.Status.Success) Finish(s, Map(status), status);
        else if (p->Step == Step.Write && result.Bytes != p->Bytes) { s->Poisoned = true; Finish(s, Io, C.Status.InvalidState); }
        else Finish(s, p->Step == Step.Write ? result.Bytes : 0, status);
        return AfterFinish(s);
    }

    public static Result Poll(State* s, RequestToken request) => Matches(s, request) ? Observe(s) : new(Progress.Rejected);

    /// <summary>Consume one ready open/write/barrier/error. Only this step
    /// publishes the reserved fd or commits a successful sequential write
    /// cursor. Successful positive reads require CopyRead instead.</summary>
    public static Result Complete(State* s, RequestToken request)
    {
        if (!Matches(s, request)) return new(Progress.Rejected);
        Pending* p = &s->Work; if (!p->Ready) return Observe(s);
        if (s->Input.Engine->Dirty || s->KnownRevision != s->Input.Engine->VolumeRevision || s->Poisoned && p->Value >= 0) return Recovery(s);
        if (p->Kind == Operation.Read && p->Step == Step.Read) return new(Progress.Rejected);
        if (p->Borrowed && !Current(s, p->Original, out _)) { p->Discard = true; Drop(s); return Immediate(BadFd); }
        long value = p->Value; DescriptorToken descriptor = default; var failure = p->Failure;
        if (p->Kind == Operation.Open && value >= 0)
        {
            Descriptor* f = s->Input.Descriptors + p->Number; Description* d = s->Input.Descriptions + p->Description;
            if (!f->Reserved || f->Description != p->Description + 1 || !d->Reserved || d->Generation != p->DescriptionGeneration)
            { s->Poisoned = true; Drop(s); return Immediate(Io); }
            d->Reserved = false; d->References = 1; d->File = p->File; d->NameLength = p->NameLength;
            for (uint i = 0; i < p->NameLength; i++) d->Name[i] = p->Name[i]; f->Reserved = false; descriptor = new(s, p->Number);
        }
        else if (p->Kind == Operation.Write && value >= 0 && !p->Positioned) s->Input.Descriptions[p->Description].Position += (ulong)value;
        Drop(s); return new(Progress.Immediate, value, descriptor: descriptor, failure: failure);
    }

    /// <summary>Revalidate the original fd/description and all output aliases
    /// before consuming retained engine bytes. Caller output is synchronous
    /// supervisor RAM; the future syscall owner must revalidate user generations
    /// separately. Capacity failure keeps the read held for retry.</summary>
    public static Result CopyRead(State* s, RequestToken request, Span<byte> destination)
    {
        if (!Matches(s, request) || s->Work.Kind != Operation.Read) return new(Progress.Rejected);
        Pending* p = &s->Work; if (!p->Ready) return Observe(s);
        if (s->Input.Engine->Dirty || s->KnownRevision != s->Input.Engine->VolumeRevision || s->Poisoned && p->Value >= 0) return Recovery(s);
        if (p->Step != Step.Read) return Complete(s, request);
        if (!Current(s, p->Original, out _) || p->Discard) { p->Discard = true; Cancel(s, request); return Immediate(BadFd); }
        if (destination.Length > IoBytes || !External(s, destination) || p->Value > destination.Length) return Immediate(Fault);
        C.Result result = default; var status = C.CopyRead(s->Input.Engine, p->EngineRequest, destination, &result);
        if (status == C.Status.Pending) return Observe(s);
        if (status == C.Status.InvalidStorage) return Immediate(Fault);
        if (status == C.Status.RecoveryRequired || s->Input.Engine->Dirty) return Recovery(s);
        if (status is C.Status.BlockFailure or C.Status.InvalidState) s->Poisoned = true;
        long value = Map(status); var failure = status;
        if (status == C.Status.Success)
        {
            if (result.Bytes != p->Expected) { s->Poisoned = true; value = Io; failure = C.Status.InvalidState; }
            else { value = result.Bytes; if (!p->Positioned) s->Input.Descriptions[p->Description].Position += result.Bytes; }
        }
        Drop(s); return new(Progress.Immediate, value, failure: failure);
    }

    /// <summary>Revoke pending read/open continuation, or honor an already
    /// completed write/barrier. Close/retirement additionally discard every
    /// result. Issued dirty cancellation retains recovery, never fake rollback.</summary>
    public static Result Cancel(State* s, RequestToken request)
    {
        if (!Matches(s, request)) return new(Progress.Rejected);
        Pending* p = &s->Work;
        if (p->Ready && p->Step != Step.Read)
        {
            if (p->Kind is Operation.Open or Operation.Read) { p->Cancelled = true; p->Step = Step.Done; Finish(s, Interrupted, C.Status.Cancelled); }
            if (p->Discard) Drop(s); return p->Active ? Observe(s) : Immediate(0);
        }
        p->Cancelled = true;
        var status = C.Cancel(s->Input.Engine, p->EngineRequest);
        if (status == C.Status.RecoveryRequired || s->Input.Engine->Dirty) return Recovery(s);
        if (status == C.Status.Cancelled && !s->Input.Engine->RequestActive)
        { p->Step = Step.Done; Finish(s, Interrupted, status); return AfterFinish(s); }
        if (p->Ready && p->Step == Step.Read) { DrainRead(s); return AfterFinish(s); }
        return Observe(s);
    }

    /// <summary>Revoke all descriptors, cancel and drain pending work, then
    /// scrub local tables/input while engine and device extents remain retained.
    /// Detach never calls engine/device Stop. Dirty state refuses retirement.</summary>
    public static Result Detach(State* s)
    {
        if (!Bound(s)) return new(Progress.Rejected);
        s->Retiring = true;
        for (uint fd = 3; fd < s->Input.DescriptorCount; fd++) if (Find(s, fd, out _)) Close(s, new(s, fd));
        if (s->Input.Engine->Dirty) return Recovery(s);
        if (s->Work.Active)
        {
            s->Work.Discard = true; Cancel(s, new(s));
            if (s->Work.Active) return Observe(s);
        }
        for (uint i = 0; i < s->Input.DescriptorCount; i++) s->Input.Descriptors[i] = default;
        for (uint i = 0; i < s->Input.DescriptionCount; i++) s->Input.Descriptions[i] = default;
        new Span<byte>(s->Input.Input, (int)IoBytes).Clear(); s->Active = false; s->RetiredValue = true;
        return new(Progress.Retired);
    }

    private static Result BeginBorrow(State* s, DescriptorToken token, uint description, Operation kind, ulong offset, uint bytes, bool positioned)
    {
        if (s->Work.Active || s->Input.Engine->RequestActive) return Immediate(Again);
        if (s->NextGeneration == ulong.MaxValue || s->Input.Descriptions[description].Borrowers == uint.MaxValue) return Immediate(Overflow);
        Description* d = s->Input.Descriptions + description;
        s->Work = new() { Active = true, Generation = ++s->NextGeneration, Kind = kind, Step = kind == Operation.Sync ? Step.Flush : Step.Authenticate,
            Original = token, Description = description, DescriptionGeneration = d->Generation, Offset = offset, Bytes = bytes,
            Positioned = positioned, Borrowed = true, NameLength = d->NameLength };
        d->Borrowers++; for (uint i = 0; i < d->NameLength; i++) s->Work.Name[i] = d->Name[i];
        C.RequestToken request = default;
        var status = kind == Operation.Sync ? C.BeginFlush(s->Input.Engine, &request) : Lookup(s);
        if (kind == Operation.Sync) s->Work.EngineRequest = request;
        if (status != C.Status.Pending) Finish(s, Map(status), status);
        return Observe(s);
    }
    private static C.Status Lookup(State* s)
    {
        Span<char> name = stackalloc char[(int)PathBytes]; for (uint i = 0; i < s->Work.NameLength; i++) name[(int)i] = (char)s->Work.Name[i];
        C.RequestToken request = default; var status = C.BeginOpenRoot(s->Input.Engine, name[..s->Work.NameLength], &request);
        s->Work.EngineRequest = request; return status;
    }
    private static C.Status CreateEmpty(State* s)
    {
        if (++s->Work.AliasAttempts > 64) return C.Status.CapacityExceeded;
        if (s->NextAlias > 9999999) return C.Status.GenerationExhausted;
        Span<byte> alias = stackalloc byte[11]; uint value = (uint)s->NextAlias++;
        alias[0] = (byte)'F'; for (int i = 7; i > 0; i--) { alias[i] = (byte)('0' + value % 10); value /= 10; }
        alias[8] = (byte)'D'; alias[9] = (byte)'A'; alias[10] = (byte)'T';
        Span<char> name = stackalloc char[(int)PathBytes]; for (uint i = 0; i < s->Work.NameLength; i++) name[(int)i] = (char)s->Work.Name[i];
        C.RequestToken request = default; var status = C.BeginCreateEmpty(s->Input.Engine, name[..s->Work.NameLength], alias, &request);
        s->Work.EngineRequest = request; return status;
    }
    private static C.Status BeginData(State* s)
    {
        Pending* p = &s->Work; C.RequestToken request = default; C.Status status;
        if (p->Kind == Operation.Write)
        { p->Step = Step.Write; status = C.BeginOverwrite(s->Input.Engine, p->File, p->Offset, new ReadOnlySpan<byte>(s->Input.Input, (int)p->Bytes), &request); }
        else
        {
            p->Step = Step.Read; p->Expected = p->Offset >= p->File.Length ? 0 : (uint)System.Math.Min(p->Bytes, p->File.Length - p->Offset);
            status = C.BeginRead(s->Input.Engine, p->File, p->Offset, p->Bytes, &request);
        }
        p->EngineRequest = request; return status;
    }
    private static void DrainRead(State* s)
    {
        C.Result result = default; var status = C.CopyRead(s->Input.Engine, s->Work.EngineRequest, Span<byte>.Empty, &result);
        if (status == C.Status.Pending) return;
        if (status is not (C.Status.Cancelled or C.Status.Success)) { s->Poisoned = true; Finish(s, Io, status); }
        else { s->Work.Step = Step.Done; Finish(s, Interrupted, C.Status.Cancelled); }
    }
    private static void Finish(State* s, long value, C.Status failure)
    { s->Work.Ready = true; s->Work.Value = value; s->Work.Failure = failure; }
    private static Result AfterFinish(State* s)
    { if (s->Work.Ready && s->Work.Discard && !s->Input.Engine->Dirty) { Drop(s); return Immediate(0); } return Observe(s); }
    private static Result Observe(State* s) => new(s->Input.Engine->Dirty && s->Input.Engine->LastFailure == C.Status.RecoveryRequired ? Progress.RecoveryRequired :
        s->Work.Ready ? Progress.Ready : Progress.Pending, s->Work.Ready ? s->Work.Value : 0, new(s), failure: s->Work.Failure);
    private static Result Recovery(State* s)
    { s->Poisoned = true; return new(Progress.RecoveryRequired, Io, s->Work.Active ? new(s) : default, failure: s->Input.Engine->LastFailure); }
    private static Result Immediate(long value) => new(Progress.Immediate, value);
    private static bool Revision(State* s)
    {
        if (s->Poisoned) return false;
        if (s->KnownRevision == s->Input.Engine->VolumeRevision) return true;
        s->Poisoned = true; return false;
    }
    private static bool AcceptOwnRevision(State* s)
    {
        ulong current = s->Input.Engine->VolumeRevision;
        if (s->KnownRevision == current) return true;
        if (!s->Work.RevisionAccepted && s->Work.Step is Step.Write or Step.Create &&
            s->KnownRevision != ulong.MaxValue && current == s->KnownRevision + 1)
        { s->KnownRevision = current; s->Work.RevisionAccepted = true; return true; }
        s->Poisoned = true; return false;
    }
    private static bool Physical(C.FileToken a, C.FileToken b) => a.Owner == b.Owner && a.Epoch == b.Epoch && a.EntryOffset == b.EntryOffset &&
        a.FirstCluster == b.FirstCluster && a.Length == b.Length && a.Fingerprint0 == b.Fingerprint0 && a.Fingerprint1 == b.Fingerprint1 &&
        a.Fingerprint2 == b.Fingerprint2 && a.Fingerprint3 == b.Fingerprint3;
    private static bool Same(DescriptorToken a, DescriptorToken b) => a.Owner == b.Owner && a.Epoch == b.Epoch && a.Generation == b.Generation && a.Number == b.Number;
    private static bool Matches(State* s, RequestToken t) => Bound(s) && s->Work.Active && t.Owner == (ulong)s && t.Epoch == s->Input.Epoch && t.Generation == s->Work.Generation;
    private static bool Bound(State* s) => Ordinary((ulong)s, (ulong)sizeof(State), 8) && s->Self == (ulong)s && s->Active &&
        C.CanBorrowStorage(s->Input.Engine, s->Input.Epoch, (ulong)s, (ulong)sizeof(State));
    private static bool Find(State* s, uint number, out uint description)
    {
        description = 0; if (number < 3 || number >= s->Input.DescriptorCount) return false;
        Descriptor* f = s->Input.Descriptors + number;
        if (f->Reserved || f->Description == 0 || f->Description > s->Input.DescriptionCount) return false;
        description = f->Description - 1; Description* d = s->Input.Descriptions + description;
        return d->Active && !d->Reserved && d->References != 0;
    }
    private static bool Current(State* s, DescriptorToken token, out uint description)
    { description = 0; return Bound(s) && token.Owner == (ulong)s && token.Epoch == s->Input.Epoch && token.Generation != 0 && Find(s, token.Number, out description) && s->Input.Descriptors[token.Number].Generation == token.Generation; }
    private static int FreeFd(State* s)
    { for (uint i = 3; i < s->Input.DescriptorCount; i++) if (s->Input.Descriptors[i].Description == 0 && s->Input.Descriptors[i].Generation != ulong.MaxValue) return (int)i; return -1; }
    private static int FreeDescription(State* s)
    { for (uint i = 0; i < s->Input.DescriptionCount; i++) if (!s->Input.Descriptions[i].Active && s->Input.Descriptions[i].Generation != ulong.MaxValue) return (int)i; return -1; }
    private static void Reserve(State* s, uint number, uint description, uint flags)
    {
        Descriptor* f = s->Input.Descriptors + number; f->Generation++; f->Description = description + 1; f->Flags = flags & CloseOnExec; f->Reserved = true;
        Description* d = s->Input.Descriptions + description; ulong generation = d->Generation;
        *d = new() { Active = true, Reserved = true, Generation = generation + 1, Flags = flags & ~CloseOnExec };
    }
    private static void RetireDescription(State* s, uint index)
    {
        Description* d = s->Input.Descriptions + index;
        if (!d->Active || d->References != 0 || d->Borrowers != 0) return;
        ulong generation = d->Generation; *d = new() { Generation = generation };
    }
    private static void Drop(State* s)
    {
        Pending p = s->Work;
        if (p.Kind == Operation.Open && s->Input.Descriptors[p.Number].Reserved)
        { s->Input.Descriptors[p.Number].Description = s->Input.Descriptors[p.Number].Flags = 0; s->Input.Descriptors[p.Number].Reserved = false; }
        Description* d = s->Input.Descriptions + p.Description;
        if (p.Borrowed && d->Active && d->Generation == p.DescriptionGeneration && d->Borrowers != 0) d->Borrowers--;
        RetireDescription(s, p.Description); new Span<byte>(s->Input.Input, (int)IoBytes).Clear(); s->Work = default;
    }
    private static long Path(ReadOnlySpan<byte> path)
    {
        if (path.Length is < 7 or >= (int)PathBytes || !path[..6].SequenceEqual("/data/"u8)) return Access;
        var name = path[6..]; if (name[^1] is (byte)'.' or (byte)' ') return Access;
        int component = 0; bool nested = false;
        for (int i = 0; i <= name.Length; i++)
            if (i == name.Length || name[i] == '/')
            {
                var part = name.Slice(component, i - component);
                if (part.Length == 0 || part.SequenceEqual("."u8) || part.SequenceEqual(".."u8)) return Access;
                if (i < name.Length) nested = true;
                component = i + 1;
            }
        if (nested) return Unsupported;
        foreach (byte b in name)
        { if (b == '/') return Unsupported; if (b is < 33 or > 126 || b is (byte)'\\' or (byte)':' or (byte)'*' or (byte)'?' or (byte)'"' or (byte)'<' or (byte)'>' or (byte)'|') return Invalid; }
        return 0;
    }
    private static bool External(State* s, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return true;
        fixed (byte* p = bytes)
        {
            ulong start = (ulong)p, count = (ulong)bytes.Length;
            return C.CanBorrowStorage(s->Input.Engine, s->Input.Epoch, start, count) &&
                !Overlap(start, count, (ulong)s, (ulong)sizeof(State)) &&
                !Overlap(start, count, (ulong)s->Input.Descriptors, s->Input.DescriptorCount * (ulong)sizeof(Descriptor)) &&
                !Overlap(start, count, (ulong)s->Input.Descriptions, s->Input.DescriptionCount * (ulong)sizeof(Description)) &&
                !Overlap(start, count, (ulong)s->Input.Input, IoBytes);
        }
    }
    private static long Map(C.Status status) => status switch
    {
        C.Status.Success => 0, C.Status.NotFound => Missing, C.Status.AlreadyExists => Exists,
        C.Status.InvalidName => Invalid, C.Status.NotFile => IsDirectory, C.Status.ReadOnly => Access,
        C.Status.Unsupported => Unsupported, C.Status.StaleToken => Stale, C.Status.Busy => Again,
        C.Status.CapacityExceeded => NoSpace, C.Status.GenerationExhausted or C.Status.BudgetExceeded => Overflow,
        C.Status.Cancelled => Interrupted, C.Status.InvalidStorage => Fault, _ => Io
    };
    private static bool Ordinary(ulong start, ulong bytes, ulong alignment) => start >= 4096 && start < 0x0000800000000000UL &&
        (start & (alignment - 1)) == 0 && bytes != 0 && bytes <= 0x0000800000000000UL - start;
    private static bool Overlap(ulong start, ulong bytes, ulong other, ulong length) => bytes != 0 && length != 0 && start < other + length && other < start + bytes;
}
