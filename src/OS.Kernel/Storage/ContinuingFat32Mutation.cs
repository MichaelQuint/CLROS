using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

/// <summary>Preparatory root-only FAT32 mutation owner over real continuing
/// sector IN/OUT/FLUSH. One serialized supervisor owns caller-provided POD state
/// and backing; no snapshot, retained Span/ref struct, allocation, user pointer,
/// synchronous disk spin or native activation is supplied. This first slice
/// opens regular root files, creates empty root files, overwrites within their
/// existing length, grows files using explicit free clusters and sends actual
/// barriers. Nested directories, truncate/rename/delete and
/// persistent crash recovery remain unsupported. Any issued-write failure keeps
/// dirty before/after buffers owned; device reset never authorizes their loss.</summary>
public static unsafe class ContinuingFat32Mutation
{
    public const uint JournalChanges = 128, MaximumClusters = 4096, WriteBytes = 8192, NameWords = 256, VfatWords = 260;
    public const uint SectorBytes = 512, EncodedEntryBytes = 672;
    public const int MaximumGrowthClusters = 16;
    public enum Status : byte
    {
        Uninitialized, Success, Pending, Ready, Idle, InvalidStorage, InvalidState,
        InvalidVolume, InvalidName, NotFound, NotFile, ReadOnly, Unsupported,
        CorruptDirectory, CorruptChain, MirrorMismatch, BudgetExceeded,
        CapacityExceeded, StaleToken, Busy, Cancelled, GenerationExhausted,
        BlockFailure, RecoveryRequired, AlreadyExists, InvalidAllocation
    }
    internal enum Life : byte { Mounting = 1, Idle, Working, Ready, Faulted, Retired }
    internal enum Kind : byte { Mount, Open, Overwrite, Barrier, Create, Grow }
    internal enum Step : byte { Bpb, RootChain, Scan, TargetChain, OtherChain, Plan, Issue, Flush, GrowCandidates, GrowData, GrowFat, GrowDirectory, GrowFsInfo }
    internal enum Purpose : byte { Bpb, Fat0, Fat1, Directory, Original, Write, Flush, GrowthEdit }
    internal enum Transform : byte { Zero, Fat, Directory, FsInfo }
    private const byte LookupName = 1, LookupToken = 2, ValidateRoot = 3, CreateRoot = 4;

    /// <summary>Exact initial bounded backing:128 changes, two64KiB journal
    /// buffers, three4096-word maps, three sector buffers,8192 copied input
    /// bytes,256 name words,260 VFAT words and672 encoding bytes. All extents
    /// remain stable, exclusive and disjoint until Stop succeeds or dirty
    /// ownership is explicitly recovered by a future owner. The outer owner
    /// authenticates a separate writable volume, device and unique lifetime.
    /// The existing block owner is borrowed, never reset or released here.</summary>
    public struct Admission
    {
        public Block.State* Device;
        public ulong Owner, Epoch, PartitionFirst, PartitionLast;
        public uint ChangeCapacity, ChainCapacity, InputCapacity, WorkBudget;
        public Fat32Mutation.Change* Changes;
        public byte* Before, After, Sector, Fat0, Fat1, Input, EncodedEntries;
        public uint* DirectoryMap, FileMap, WalkMap;
        public ushort* Name, LongName;
    }
    public readonly struct RequestToken
    {
        public readonly ulong Owner, Epoch, Generation;
        internal RequestToken(State* s) { Owner = (ulong)s; Epoch = s->Input.Epoch; Generation = s->RequestGeneration; }
    }
    /// <summary>Copied physical root-file identity. Every issued mutation
    /// revokes the volume revision, so old tokens cannot recapture another file.
    /// Four fingerprint words retain the complete original short entry.
    /// Logical POSIX inode/description lifetimes belong to a later fd port.</summary>
    public readonly struct FileToken
    {
        public readonly ulong Owner, Epoch, Revision, EntryOffset, Fingerprint0, Fingerprint1, Fingerprint2, Fingerprint3;
        public readonly uint FirstCluster, Length;
        internal FileToken(State* s, ulong offset, uint first, uint length, ReadOnlySpan<byte> raw)
        {
            Owner = (ulong)s; Epoch = s->Input.Epoch; Revision = s->Revision; EntryOffset = offset; FirstCluster = first; Length = length;
            Fingerprint0 = U64(raw, 0); Fingerprint1 = U64(raw, 8); Fingerprint2 = U64(raw, 16); Fingerprint3 = U64(raw, 24);
        }
    }
    public readonly struct Result
    {
        public readonly Status Status;
        public readonly uint Bytes;
        internal Result(Status status, uint bytes) { Status = status; Bytes = bytes; }
    }
    /// <summary>Zeroed retained unmanaged owner; it contains only POD values
    /// and pointers into Admission. Copying/moving an active owner is forbidden.
    /// Inspect copied properties after failure; never free dirty journals merely
    /// because the outer block Stop/reset succeeded.</summary>
    public struct State
    {
        internal ulong Self, DeviceEpoch, PoolEpoch, Revision, RequestGeneration;
        internal Admission Input;
        internal LiveFat32Volume Volume;
        internal Life Phase;
        internal Kind Kind;
        internal Step Step;
        internal Purpose Purpose;
        internal byte ScanReason, FatPart;
        internal bool Mounted, IoActive, Cancelled, DirtyValue, DeviceFault, CreateHasRun, CreateEnd;
        internal ulong IoLba, FatLba, LoadedDirectoryLba, Offset;
        internal byte* IoTarget;
        internal Block.RequestToken Io;
        internal FileToken File;
        internal uint RootCount, TargetCount, WalkCount, WalkNext, OtherLength;
        internal uint DirectorySector, DirectorySlot, NameLength, Wanted, PlannedBytes, Changes, Issued, Work;
        internal uint CreateSlots, CreateStart, CreateFree;
        internal fixed uint Growth[MaximumGrowthClusters];
        internal uint GrowthCount, NewLength, GrowthCursor, FatCursor, FsCursor, FsInfoSector, BackupBootSector, FlushedThrough;
        internal uint EditIndex, EditOffset, EditBytes, EditValue;
        internal ulong ZeroPosition, ZeroEnd;
        internal Fat32Mutation.Stage EditStage;
        internal Transform EditKind;
        internal LiveVfatState Vfat;
        internal Status Failure;
        internal Block.Status DriverFailure;
        public readonly bool IsMounted => Mounted;
        public readonly bool RequestActive => Phase is Life.Mounting or Life.Working or Life.Ready;
        public readonly bool IsReady => Phase == Life.Ready;
        public readonly bool Dirty => DirtyValue;
        public readonly bool Retired => Phase == Life.Retired;
        public readonly Status LastFailure => Failure;
        public readonly Block.Status LastBlockFailure => DriverFailure;
        public readonly uint ChangeCount => Changes;
        public readonly uint IssuedChanges => Issued;
        public readonly ulong VolumeRevision => Revision;
    }

    /// <summary>Borrow validated zero-state storage without issuing I/O. Pump
    /// mounts from an actual BPB read. Exact128 slots are deliberate initial
    /// admission, not permission to allocate or expand during a request.</summary>
    public static Status Start(State* s, in Admission input)
    {
        Admission a = input;
        if (!Admit(s, a)) return Status.InvalidStorage;
        s->Self = (ulong)s; s->Input = a; s->DeviceEpoch = a.Device->Input.Epoch; s->PoolEpoch = a.Device->PoolEpoch;
        s->Revision = 1; s->Kind = Kind.Mount; s->Phase = Life.Mounting; s->Step = Step.Bpb;
        s->Failure = Status.Success; s->DriverFailure = Block.Status.Success; s->LoadedDirectoryLba = ulong.MaxValue;
        return Status.Pending;
    }

    /// <summary>Copy one root component synchronously, then asynchronously
    /// validate its directory entry and complete cluster map. No input pointer
    /// or output metadata pointer is retained. Invalid aliases write nothing.</summary>
    public static Status BeginOpenRoot(State* s, ReadOnlySpan<char> name, RequestToken* request)
    {
        if (!Bound(s)) return Status.InvalidState;
        fixed (char* source = name)
        {
            if (!Output(s, (ulong)request, (ulong)sizeof(RequestToken)) ||
                name.Length != 0 && (!Caller(s, (ulong)source, (ulong)name.Length * 2) || Overlap((ulong)source, (ulong)name.Length * 2, (ulong)request, (ulong)sizeof(RequestToken))))
                return Status.InvalidStorage;
            *request = default;
            if (!Available(s)) return s->Phase == Life.Faulted ? s->Failure : Status.Busy;
            if (!NameValid(name)) return Status.InvalidName;
            if (s->RequestGeneration == ulong.MaxValue) return Status.GenerationExhausted;
            Begin(s, Kind.Open); s->NameLength = (uint)name.Length;
            for (int i = 0; i < name.Length; i++) s->Input.Name[i] = name[i];
            BeginRootChain(s, LookupName); *request = new(s); return Status.Pending;
        }
    }

    /// <summary>Copy at most8192 input bytes before return. Only in-place
    /// overwrites inside the existing length are supported. Validate all root
    /// file chains, target exclusivity and boundary sectors before first OUT.
    /// Directory metadata/timestamps remain unchanged in this initial slice.</summary>
    public static Status BeginOverwrite(State* s, FileToken file, ulong offset, ReadOnlySpan<byte> bytes, RequestToken* request)
    {
        if (!Bound(s)) return Status.InvalidState;
        fixed (byte* source = bytes)
        {
            if (!Output(s, (ulong)request, (ulong)sizeof(RequestToken)) || bytes.Length != 0 &&
                (!Caller(s, (ulong)source, (ulong)bytes.Length) || Overlap((ulong)source, (ulong)bytes.Length, (ulong)request, (ulong)sizeof(RequestToken)))) return Status.InvalidStorage;
            *request = default;
            if (!Available(s)) return s->Phase == Life.Faulted ? s->Failure : Status.Busy;
            if (!FileMatches(s, file)) return Status.StaleToken;
            if (bytes.Length > WriteBytes) return Status.CapacityExceeded;
            if (offset > file.Length || (ulong)bytes.Length > file.Length - offset) return Status.Unsupported;
            if (s->RequestGeneration == ulong.MaxValue || s->Revision == ulong.MaxValue) return Status.GenerationExhausted;
            Begin(s, Kind.Overwrite); s->File = file; s->Offset = offset; s->Wanted = (uint)bytes.Length;
            for (int i = 0; i < bytes.Length; i++) s->Input.Input[i] = source[i];
            if (bytes.Length == 0) Finish(s, Status.Success); else BeginRootChain(s, LookupToken);
            *request = new(s); return s->Phase == Life.Ready ? Status.Ready : Status.Pending;
        }
    }

    /// <summary>Copy an explicit validated11-byte alias and root name, then
    /// create one empty regular file without allocating clusters. The existing
    /// kernel encoder supplies bounded VFAT entries when the name differs from
    /// the rendered short alias. It validates namespace and
    /// existing chains before journaled directory RMW and actual FLUSH. A new
    /// end marker hides bytes beyond the original logical end. No output or
    /// caller input pointer is retained; partial publication requires recovery.</summary>
    public static Status BeginCreateEmpty(State* s, ReadOnlySpan<char> name, ReadOnlySpan<byte> alias, RequestToken* request)
    {
        if (!Bound(s)) return Status.InvalidState;
        fixed (char* letters = name)
        fixed (byte* shortName = alias)
        {
            if (!Output(s, (ulong)request, (ulong)sizeof(RequestToken)) ||
                name.Length != 0 && (!Caller(s, (ulong)letters, (ulong)name.Length * 2) || Overlap((ulong)letters, (ulong)name.Length * 2, (ulong)request, (ulong)sizeof(RequestToken))) ||
                alias.Length != 0 && (!Caller(s, (ulong)shortName, (ulong)alias.Length) || Overlap((ulong)shortName, (ulong)alias.Length, (ulong)request, (ulong)sizeof(RequestToken)))) return Status.InvalidStorage;
            *request = default;
            if (!Available(s)) return s->Phase == Life.Faulted ? s->Failure : Status.Busy;
            if (!NameValid(name) || alias.Length != 11) return Status.InvalidName;
            Span<byte> entry = stackalloc byte[32]; entry.Clear(); alias.CopyTo(entry); entry[11] = 0x20;
            Span<ushort> words = stackalloc ushort[260]; LiveVfatState vfat = default;
            if (alias[0] == '.' || Vfat.Consume(entry, ref vfat, words, out _) != LiveDirectoryStatus.Entry) return Status.InvalidName;
            Span<byte> encoded = stackalloc byte[(int)EncodedEntryBytes]; int slots = 1;
            if (ShortEquals(entry, name, false)) entry.CopyTo(encoded);
            else if (Fat32Mutation.EncodeEntryRun(name, entry, encoded, out slots) != Fat32Mutation.Status.Success) return Status.InvalidName;
            if (s->RequestGeneration == ulong.MaxValue || s->Revision == ulong.MaxValue) return Status.GenerationExhausted;
            Begin(s, Kind.Create); s->NameLength = (uint)name.Length;
            for (int i = 0; i < name.Length; i++) s->Input.Name[i] = name[i];
            encoded[..(slots * 32)].CopyTo(new Span<byte>(s->Input.EncodedEntries, slots * 32)); s->CreateSlots = (uint)slots;
            s->TargetCount = 0; BeginRootChain(s, CreateRoot); *request = new(s); return Status.Pending;
        }
    }

    /// <summary>Copy at most16 explicit candidate clusters and grow a root
    /// file; no allocation search or payload write is supplied. Candidates must
    /// exactly cover the additional length and be distinct free clusters in
    /// both FATs. Preserve old data, zero the visible tail and entire new
    /// clusters, then FLUSH data, both FATs, directory and advisory FSInfo in
    /// order. Plan every change before first OUT;128 entries remain the separate
    /// ceiling. FSInfo unknowns remain unknown, never guessed. Growth does not
    /// provide power-loss atomicity or recovery. Copied result Bytes is the
    /// increase in logical length; old file tokens are revoked on first OUT.</summary>
    public static Status BeginGrow(State* s, FileToken file, uint newLength, ReadOnlySpan<uint> candidates, RequestToken* request)
    {
        if (!Bound(s)) return Status.InvalidState;
        fixed (uint* source = candidates)
        {
            if (!Output(s, (ulong)request, (ulong)sizeof(RequestToken)) || candidates.Length != 0 &&
                (!Caller(s, (ulong)source, (ulong)candidates.Length * 4) || Overlap((ulong)source, (ulong)candidates.Length * 4, (ulong)request, (ulong)sizeof(RequestToken)))) return Status.InvalidStorage;
            *request = default;
            if (!Available(s)) return s->Phase == Life.Faulted ? s->Failure : Status.Busy;
            if (!FileMatches(s, file)) return Status.StaleToken;
            if (newLength <= file.Length) return Status.Unsupported;
            ulong clusterBytes = s->Volume.SectorsPerCluster * 512UL;
            uint oldCount = (uint)(((ulong)file.Length + clusterBytes - 1) / clusterBytes), newCount = (uint)(((ulong)newLength + clusterBytes - 1) / clusterBytes);
            if (newCount > MaximumClusters || newCount - oldCount > MaximumGrowthClusters || candidates.Length > MaximumGrowthClusters) return Status.CapacityExceeded;
            if ((uint)candidates.Length != newCount - oldCount) return Status.InvalidAllocation;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (source[i] < 2 || source[i] - 2 >= s->Volume.ClusterCount) return Status.InvalidAllocation;
                for (int j = 0; j < i; j++) if (source[j] == source[i]) return Status.InvalidAllocation;
            }
            if (s->RequestGeneration == ulong.MaxValue || s->Revision == ulong.MaxValue) return Status.GenerationExhausted;
            Begin(s, Kind.Grow); s->File = file; s->NewLength = newLength; s->Wanted = newLength - file.Length; s->GrowthCount = (uint)candidates.Length;
            for (int i = 0; i < candidates.Length; i++) s->Growth[i] = source[i];
            s->ZeroPosition = file.Length; s->ZeroEnd = candidates.Length == 0 ? newLength : newCount * clusterBytes;
            BeginRootChain(s, LookupToken); *request = new(s); return Status.Pending;
        }
    }

    /// <summary>Request a real volume FLUSH, even when no data is dirty. This
    /// is a block barrier; no regular-file or directory durability is fabricated.</summary>
    public static Status BeginFlush(State* s, RequestToken* request)
    {
        if (!Bound(s)) return Status.InvalidState;
        if (!Output(s, (ulong)request, (ulong)sizeof(RequestToken))) return Status.InvalidStorage;
        *request = default; if (!Available(s)) return s->Phase == Life.Faulted ? s->Failure : Status.Busy;
        if (s->RequestGeneration == ulong.MaxValue) return Status.GenerationExhausted;
        Begin(s, Kind.Barrier); s->Step = Step.Flush; *request = new(s); return Status.Pending;
    }

    /// <summary>One submit, one completion/copy, or one bounded metadata step.
    /// Never waits synchronously. Successful overwrite becomes Ready only after
    /// its actual FLUSH. Read errors before publication change no media; issued
    /// failures/cancellation retain RecoveryRequired and all journal bytes.</summary>
    public static Status Pump(State* s)
    {
        if (!Bound(s)) return Status.InvalidState;
        if (s->Phase == Life.Faulted) return s->Failure;
        if (s->Phase == Life.Retired) return Status.InvalidState;
        if (s->Phase == Life.Ready) return Status.Ready;
        if (s->Phase == Life.Idle) return Status.Idle;
        if (!DeviceCurrent(s)) return DeviceFailure(s, Block.Status.InvalidState);
        if (s->IoActive) return CompleteIo(s);
        if (s->Cancelled) return s->DirtyValue ? Fail(s, Status.RecoveryRequired, true) : Finish(s, Status.Cancelled);
        switch (s->Step)
        {
            case Step.Bpb: return Read(s, s->Input.PartitionFirst, s->Input.Sector, Purpose.Bpb);
            case Step.RootChain: case Step.TargetChain: case Step.OtherChain: return Walk(s);
            case Step.Scan: return Scan(s);
            case Step.Plan: return Plan(s);
            case Step.GrowCandidates: return CheckGrowthCandidates(s);
            case Step.GrowData: return PlanGrowthData(s);
            case Step.GrowFat: return PlanGrowthFat(s);
            case Step.GrowDirectory: return Edit(s, s->File.EntryOffset / 512, Fat32Mutation.Stage.PublishDirectory, Transform.Directory, (uint)(s->File.EntryOffset % 512), 32, 0);
            case Step.GrowFsInfo: return PlanGrowthFsInfo(s);
            case Step.Issue:
                if (s->Issued == s->Changes || s->Issued != s->FlushedThrough && s->Input.Changes[s->Issued].Phase != s->Input.Changes[s->Issued - 1].Phase)
                { s->Step = Step.Flush; return Status.Pending; }
                return Write(s);
            case Step.Flush: return Flush(s);
            default: return Fail(s, Status.InvalidState, true);
        }
    }

    /// <summary>Consume one retained open result into exclusive ordinary RAM.
    /// Failure returns default metadata; aliases authenticate before any output
    /// write and token/kind authenticate before publication. No file data is
    /// copied or preloaded.</summary>
    public static Status CopyOpen(State* s, RequestToken request, FileToken* file)
    {
        if (!Bound(s)) return Status.InvalidState;
        if (!Output(s, (ulong)file, (ulong)sizeof(FileToken))) return Status.InvalidStorage;
        *file = default; if (!Matches(s, request) || s->Kind != Kind.Open) return Status.StaleToken;
        if (s->Phase != Life.Ready) return s->Phase == Life.Faulted ? s->Failure : Status.Pending;
        Status result = s->Failure; if (result == Status.Success) *file = s->File;
        EndRequest(s); return result;
    }

    /// <summary>Consume a retained overwrite/barrier result. No user pointer,
    /// cursor or fallible post-copy work is retained. Dirty failures have no
    /// successful consumable result and continue owning their journals.</summary>
    public static Status CopyResult(State* s, RequestToken request, Result* result)
    {
        if (!Bound(s)) return Status.InvalidState;
        if (!Output(s, (ulong)result, (ulong)sizeof(Result))) return Status.InvalidStorage;
        *result = default; if (!Matches(s, request) || s->Kind is not (Kind.Overwrite or Kind.Barrier or Kind.Create or Kind.Grow)) return Status.StaleToken;
        if (s->Phase != Life.Ready) return s->Phase == Life.Faulted ? s->Failure : Status.Pending;
        Status status = s->Failure; *result = new(status, status == Status.Success ? s->Wanted : 0); EndRequest(s); return status;
    }

    /// <summary>Ready completion wins. Otherwise revoke continuation and drain
    /// the actual token without retracting DMA. Before first write the plan may
    /// be discarded after reads drain; later cancellation retains recovery.</summary>
    public static Status Cancel(State* s, RequestToken request)
    {
        if (!Bound(s) || !Matches(s, request)) return Status.StaleToken;
        if (s->Phase == Life.Ready) return Status.Ready;
        if (s->Phase == Life.Faulted) return s->Failure;
        s->Cancelled = true;
        if (s->IoActive)
        {
            var status = Block.Cancel(s->Input.Device, s->Io);
            return status == Block.Status.Success ? Status.Pending : DeviceFailure(s, status);
        }
        return s->DirtyValue ? Fail(s, Status.RecoveryRequired, true) : Finish(s, Status.Cancelled);
    }

    /// <summary>Retire local clean backing only; the outer owner still owns
    /// block/DMA Stop. Active reads must drain first. A device fault requires
    /// its actual retirement receipt; Dirty always rejects, even after reset.
    /// Persistent/volatile rollback transfer is deliberately not invented here.</summary>
    public static Status Stop(State* s)
    {
        if (!Bound(s)) return Status.InvalidState;
        if (s->Phase == Life.Retired) return Status.Success;
        if (s->DirtyValue) return Status.RecoveryRequired;
        if (!DeviceIdentity(s)) return Status.InvalidState;
        if (s->DeviceFault && !s->Input.Device->Retired) return Status.Busy;
        if (s->IoActive && !s->Input.Device->Retired)
        {
            s->Cancelled = true;
            var status = Block.Cancel(s->Input.Device, s->Io);
            return status == Block.Status.Success ? Status.Pending : DeviceFailure(s, status);
        }
        if (!s->Input.Device->Retired && s->Input.Device->RequestPending) return Status.Busy;
        Extent* all = stackalloc Extent[14]; Extents(s, s->Input, all);
        for (int i = 1; i < 14; i++) for (ulong j = 0; j < all[i].Bytes; j++) ((byte*)all[i].Start)[j] = 0;
        s->IoActive = false; s->Mounted = false; s->Phase = Life.Retired; return Status.Success;
    }

    private static void Begin(State* s, Kind kind)
    {
        s->Kind = kind; s->RequestGeneration++; s->Phase = Life.Working; s->Failure = Status.Success; s->DriverFailure = Block.Status.Success;
        s->Cancelled = s->DeviceFault = false; s->Changes = s->Issued = s->Wanted = s->PlannedBytes = s->Work = 0; s->FatPart = 0;
        s->CreateHasRun = s->CreateEnd = false; s->CreateSlots = s->CreateStart = s->CreateFree = 0;
        s->GrowthCount = s->GrowthCursor = s->FatCursor = s->FsCursor = s->FlushedThrough = 0;
    }
    private static void BeginRootChain(State* s, byte reason)
    { s->RootCount = s->WalkCount = 0; s->WalkNext = s->Volume.RootCluster; s->ScanReason = reason; s->Step = Step.RootChain; }
    private static bool Spend(State* s) { if (s->Work >= s->Input.WorkBudget) return false; s->Work++; return true; }
    private static Status Walk(State* s)
    {
        uint cluster = s->WalkNext; uint* map = s->Step == Step.RootChain ? s->Input.DirectoryMap : s->Step == Step.TargetChain ? s->Input.FileMap : s->Input.WalkMap;
        if (cluster < 2 || cluster - 2 >= s->Volume.ClusterCount) return Finish(s, Status.CorruptChain);
        Status ready = Fat(s, cluster, out uint next, out bool end); if (ready != Status.Success) return ready;
        if (s->WalkCount == MaximumClusters) return Finish(s, Status.CapacityExceeded);
        for (uint i = 0; i < s->WalkCount; i++) { if (!Spend(s)) return Finish(s, Status.BudgetExceeded); if (map[i] == cluster) return Finish(s, Status.CorruptChain); }
        if (s->Step != Step.RootChain)
        {
            for (uint i = 0; i < s->RootCount; i++) { if (!Spend(s)) return Finish(s, Status.BudgetExceeded); if (s->Input.DirectoryMap[i] == cluster) return Finish(s, Status.CorruptChain); }
            if (s->Step == Step.OtherChain) for (uint i = 0; i < s->TargetCount; i++)
            { if (!Spend(s)) return Finish(s, Status.BudgetExceeded); if (s->Input.FileMap[i] == cluster) return Finish(s, Status.CorruptChain); }
        }
        if (!Spend(s)) return Finish(s, Status.BudgetExceeded);
        map[s->WalkCount++] = cluster; s->WalkNext = next;
        if (!end) return Status.Pending;
        if (s->Step == Step.RootChain)
        {
            s->RootCount = s->WalkCount;
            if (s->Kind == Kind.Mount) { s->Mounted = true; s->Phase = Life.Idle; return Status.Ready; }
            ResetScan(s); return Status.Pending;
        }
        uint bytes = s->Step == Step.TargetChain ? s->File.Length : s->OtherLength;
        if ((ulong)s->WalkCount != ((ulong)bytes + s->Volume.SectorsPerCluster * 512 - 1) / (s->Volume.SectorsPerCluster * 512)) return Finish(s, Status.CorruptChain);
        if (s->Step == Step.TargetChain)
        {
            s->TargetCount = s->WalkCount; if (s->Kind == Kind.Open) return Finish(s, Status.Success);
            s->ScanReason = ValidateRoot; ResetScan(s);
        }
        else s->Step = Step.Scan;
        return Status.Pending;
    }
    private static Status Fat(State* s, uint cluster, out uint next, out bool end)
    {
        next = 0; end = false; Status ready = RawFat(s, cluster, out _); if (ready != Status.Success) return ready;
        uint value = 0; var status = Chain.ReadFatEntry(s->Volume, cluster, s->FatLba, new Span<byte>(s->Input.Fat0, 512), BlockReadResult.Success(1, 512), ref value);
        if (status == LiveFat32Status.EndOfChain) { end = true; return Status.Success; }
        if (status != LiveFat32Status.Success) return Finish(s, Status.CorruptChain);
        next = value; return Status.Success;
    }
    private static Status RawFat(State* s, uint cluster, out uint value)
    {
        value = 0; ulong lba = s->Volume.FirstFatLba + cluster / 128;
        if (s->FatPart == 0 || s->FatLba != lba) { s->FatLba = lba; s->FatPart = 0; return Read(s, lba, s->Input.Fat0, Purpose.Fat0); }
        if (s->FatPart == 1) return Read(s, lba + s->Volume.FatSectors, s->Input.Fat1, Purpose.Fat1);
        if (!new ReadOnlySpan<byte>(s->Input.Fat0, 512).SequenceEqual(new ReadOnlySpan<byte>(s->Input.Fat1, 512))) return Finish(s, Status.MirrorMismatch);
        value = EndianPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(s->Input.Fat0 + (cluster % 128) * 4, 4)) & 0x0fffffff;
        return Status.Success;
    }
    private static void ResetScan(State* s)
    { s->DirectorySector = s->DirectorySlot = 0; s->Vfat = default; s->LoadedDirectoryLba = ulong.MaxValue; s->Step = Step.Scan; }
    private static void Advance(State* s)
    { if (++s->DirectorySlot == 16) { s->DirectorySlot = 0; s->DirectorySector++; } }
    private static Status Scan(State* s)
    {
        if (s->DirectorySector == s->RootCount * s->Volume.SectorsPerCluster) return ScanEnd(s);
        uint cluster = s->Input.DirectoryMap[s->DirectorySector / s->Volume.SectorsPerCluster];
        ulong lba = s->Volume.FirstDataLba + (ulong)(cluster - 2) * s->Volume.SectorsPerCluster + s->DirectorySector % s->Volume.SectorsPerCluster;
        if (s->LoadedDirectoryLba != lba) return Read(s, lba, s->Input.Sector, Purpose.Directory);
        if (!Spend(s)) return Finish(s, Status.BudgetExceeded);
        Span<byte> raw = new(s->Input.Sector + s->DirectorySlot * 32, 32);
        uint logical = s->DirectorySector * 16 + s->DirectorySlot;
        var status = Vfat.Consume(raw, ref s->Vfat, new Span<ushort>(s->Input.LongName, (int)VfatWords), out var parsed);
        ulong physical = lba * 512 + s->DirectorySlot * 32;
        Advance(s);
        if (status == LiveDirectoryStatus.End)
        {
            if (s->Kind == Kind.Create && !s->CreateHasRun)
            {
                uint start = s->CreateFree == 0 ? logical : logical - s->CreateFree;
                if (s->RootCount * s->Volume.SectorsPerCluster * 16 - start <= s->CreateSlots) return Finish(s, Status.CapacityExceeded);
                s->CreateStart = start; s->CreateHasRun = s->CreateEnd = true;
            }
            return ScanEnd(s);
        }
        if (s->Kind == Kind.Create)
        {
            if (status == LiveDirectoryStatus.Skipped && raw[0] == 0xe5)
            {
                s->CreateFree++;
                if (!s->CreateHasRun && s->CreateFree == s->CreateSlots) { s->CreateStart = logical + 1 - s->CreateFree; s->CreateHasRun = true; }
            }
            else s->CreateFree = 0;
        }
        if (status is LiveDirectoryStatus.LongName or LiveDirectoryStatus.Skipped) return Status.Pending;
        if (status != LiveDirectoryStatus.Entry) return Finish(s, Status.CorruptDirectory);
        if ((parsed.Attributes & 0x10) != 0) return Finish(s, Status.Unsupported);
        if (s->Kind == Kind.Create)
        {
            ReadOnlySpan<byte> proposed = new(s->Input.EncodedEntries + (s->CreateSlots - 1) * 32, 32);
            bool aliasEqual = true; for (int i = 0; i < 11; i++) if (Fold(proposed[i]) != Fold(raw[i])) { aliasEqual = false; break; }
            if (NameMatches(s, raw, parsed.NameLength) || aliasEqual || LongEqualsShort(s, proposed, parsed.NameLength)) return Finish(s, Status.AlreadyExists);
            if (parsed.ByteCount == 0) return parsed.FirstCluster == 0 ? Status.Pending : Finish(s, Status.CorruptChain);
            s->OtherLength = parsed.ByteCount; s->WalkCount = 0; s->WalkNext = parsed.FirstCluster; s->Step = Step.OtherChain; return Status.Pending;
        }
        if (s->ScanReason == ValidateRoot)
        {
            if (physical == s->File.EntryOffset) return Status.Pending;
            if (parsed.ByteCount == 0) return parsed.FirstCluster == 0 ? Status.Pending : Finish(s, Status.CorruptChain);
            s->OtherLength = parsed.ByteCount; s->WalkCount = 0; s->WalkNext = parsed.FirstCluster; s->Step = Step.OtherChain; return Status.Pending;
        }
        bool match = s->ScanReason == LookupToken ? physical == s->File.EntryOffset : NameMatches(s, raw, parsed.NameLength);
        if (!match) return Status.Pending;
        if (s->ScanReason == LookupToken && (!Fingerprint(s->File, raw) || parsed.FirstCluster != s->File.FirstCluster || parsed.ByteCount != s->File.Length)) return Finish(s, Status.StaleToken);
        if (s->Kind is Kind.Overwrite or Kind.Grow && (parsed.Attributes & 1) != 0) return Finish(s, Status.ReadOnly);
        s->File = new(s, physical, parsed.FirstCluster, parsed.ByteCount, raw); s->TargetCount = s->WalkCount = 0;
        if (parsed.ByteCount == 0)
        {
            if (parsed.FirstCluster != 0) return Finish(s, Status.CorruptChain);
            if (s->Kind != Kind.Grow) return Finish(s, Status.Success);
            s->ScanReason = ValidateRoot; ResetScan(s); return Status.Pending;
        }
        s->WalkNext = parsed.FirstCluster; s->Step = Step.TargetChain; return Status.Pending;
    }
    private static Status ScanEnd(State* s)
    {
        if (Vfat.Finish(s->Vfat) != LiveDirectoryStatus.End) return Finish(s, Status.CorruptDirectory);
        if (s->Kind == Kind.Create) { if (!s->CreateHasRun) return Finish(s, Status.CapacityExceeded); }
        else if (s->ScanReason != ValidateRoot) return Finish(s, s->ScanReason == LookupToken ? Status.StaleToken : Status.NotFound);
        s->Step = s->Kind == Kind.Grow ? Step.GrowCandidates : Step.Plan; return Status.Pending;
    }
    private static bool NameMatches(State* s, ReadOnlySpan<byte> raw, ushort longLength)
    {
        bool equal = longLength == s->NameLength && longLength != 0;
        if (equal) { for (uint i = 0; i < longLength; i++) if (Fold(s->Input.LongName[i]) != Fold(s->Input.Name[i])) { equal = false; break; } }
        if (equal) return true;
        return ShortEquals(raw, new ReadOnlySpan<char>(s->Input.Name, (int)s->NameLength), true);
    }
    private static int ShortLength(ReadOnlySpan<byte> raw, out int baseLength)
    { baseLength = 8; int extension = 3; while (baseLength > 0 && raw[baseLength - 1] == ' ') baseLength--; while (extension > 0 && raw[8 + extension - 1] == ' ') extension--; return baseLength + (extension == 0 ? 0 : extension + 1); }
    private static ushort ShortWord(ReadOnlySpan<byte> raw, int at, int baseLength) => at < baseLength ? at == 0 && raw[0] == 5 ? (ushort)0xe5 : raw[at] : at == baseLength ? (ushort)'.' : raw[8 + at - baseLength - 1];
    private static bool ShortEquals(ReadOnlySpan<byte> raw, ReadOnlySpan<char> name, bool fold)
    {
        int total = ShortLength(raw, out int baseLength); if (total != name.Length) return false;
        for (int i = 0; i < total; i++) { ushort actual = ShortWord(raw, i, baseLength); if (fold ? Fold(actual) != Fold(name[i]) : actual != name[i]) return false; }
        return true;
    }
    private static bool LongEqualsShort(State* s, ReadOnlySpan<byte> proposed, ushort length)
    {
        if (length == 0 || ShortLength(proposed, out int baseLength) != length) return false;
        for (int i = 0; i < length; i++) if (Fold(ShortWord(proposed, i, baseLength)) != Fold(s->Input.LongName[i])) return false;
        return true;
    }
    private static Status Plan(State* s)
    {
        uint goal = s->Kind == Kind.Create ? (s->CreateSlots + (s->CreateEnd ? 1U : 0U)) * 32 : s->Wanted;
        if (s->PlannedBytes == goal) { s->Step = Step.Issue; return Status.Pending; }
        if (s->Changes == JournalChanges) return Finish(s, Status.CapacityExceeded);
        ulong position = (s->Kind == Kind.Create ? s->CreateStart * 32UL : s->Offset) + s->PlannedBytes, clusterBytes = s->Volume.SectorsPerCluster * 512UL;
        uint map = (uint)(position / clusterBytes); if (map >= (s->Kind == Kind.Create ? s->RootCount : s->TargetCount)) return Finish(s, Status.CorruptChain);
        uint cluster = s->Kind == Kind.Create ? s->Input.DirectoryMap[map] : s->Input.FileMap[map];
        ulong lba = s->Volume.FirstDataLba + (ulong)(cluster - 2) * s->Volume.SectorsPerCluster + (position % clusterBytes) / 512;
        return Read(s, lba, s->Input.Before + s->Changes * 512, Purpose.Original);
    }
    private static Status CheckGrowthCandidates(State* s)
    {
        if (s->GrowthCursor == s->GrowthCount) { s->Step = Step.GrowData; return Status.Pending; }
        Status status = RawFat(s, s->Growth[s->GrowthCursor], out uint value); if (status != Status.Success) return status;
        if (!Spend(s)) return Finish(s, Status.BudgetExceeded);
        if (value != 0) return Finish(s, Status.InvalidAllocation);
        s->GrowthCursor++; return Status.Pending;
    }
    private static Status PlanGrowthData(State* s)
    {
        if (s->ZeroPosition == s->ZeroEnd) { s->Step = Step.GrowFat; return Status.Pending; }
        ulong clusterBytes = s->Volume.SectorsPerCluster * 512UL; uint logical = (uint)(s->ZeroPosition / clusterBytes);
        uint cluster = logical < s->TargetCount ? s->Input.FileMap[logical] : s->Growth[logical - s->TargetCount];
        ulong lba = s->Volume.FirstDataLba + (ulong)(cluster - 2) * s->Volume.SectorsPerCluster + s->ZeroPosition % clusterBytes / 512;
        uint offset = (uint)(s->ZeroPosition % 512); ulong remaining = s->ZeroEnd - s->ZeroPosition;
        uint bytes = remaining < 512 - offset ? (uint)remaining : 512 - offset;
        return Edit(s, lba, Fat32Mutation.Stage.Data, Transform.Zero, offset, bytes, 0);
    }
    private static Status PlanGrowthFat(State* s)
    {
        uint links = s->GrowthCount == 0 ? 0 : s->GrowthCount + (s->TargetCount == 0 ? 0U : 1U);
        if (s->FatCursor == links * 2) { s->Step = Step.GrowDirectory; return Status.Pending; }
        uint edge = s->FatCursor / 2, cluster, value;
        if (s->TargetCount != 0 && edge == 0) { cluster = s->Input.FileMap[s->TargetCount - 1]; value = s->Growth[0]; }
        else
        {
            uint at = edge - (s->TargetCount == 0 ? 0U : 1U); cluster = s->Growth[at];
            value = at + 1 == s->GrowthCount ? 0x0fffffff : s->Growth[at + 1];
        }
        ulong lba = s->Volume.FirstFatLba + cluster / 128 + (s->FatCursor % 2) * s->Volume.FatSectors;
        return Edit(s, lba, Fat32Mutation.Stage.AllocateFat, Transform.Fat, cluster % 128 * 4, 4, value);
    }
    private static Status PlanGrowthFsInfo(State* s)
    {
        if (s->GrowthCount == 0 || s->FsInfoSector == 0xffff || s->FsCursor == 2 || s->FsCursor == 1 && s->BackupBootSector is 0 or 0xffff)
        { s->Step = Step.Issue; return Status.Pending; }
        if (s->FsInfoSector == 0 || s->FsInfoSector >= s->Volume.ReservedSectors || s->FsCursor == 1 &&
            (s->BackupBootSector >= s->Volume.ReservedSectors || s->FsInfoSector >= s->Volume.ReservedSectors - s->BackupBootSector)) return Finish(s, Status.InvalidVolume);
        ulong lba = s->Input.PartitionFirst + s->FsInfoSector + (s->FsCursor == 0 ? 0 : s->BackupBootSector);
        return Edit(s, lba, Fat32Mutation.Stage.FsInfo, Transform.FsInfo, 0, 512, 0);
    }
    // Coalesce only within a stage. Before remains the original actual read;
    // After accumulates every link in that FAT sector without losing edits.
    private static Status Edit(State* s, ulong lba, Fat32Mutation.Stage stage, Transform kind, uint offset, uint bytes, uint value)
    {
        s->EditStage = stage; s->EditKind = kind; s->EditOffset = offset; s->EditBytes = bytes; s->EditValue = value;
        for (uint i = 0; i < s->Changes; i++)
        {
            if (!Spend(s)) return Finish(s, Status.BudgetExceeded);
            if (s->Input.Changes[i].Sector == lba && s->Input.Changes[i].Phase == stage) { s->EditIndex = i; return ApplyEdit(s); }
        }
        if (s->Changes == JournalChanges) return Finish(s, Status.CapacityExceeded);
        s->EditIndex = s->Changes; return Read(s, lba, s->Input.Before + s->Changes * 512, Purpose.GrowthEdit);
    }
    private static Status ApplyEdit(State* s)
    {
        Span<byte> after = new(s->Input.After + s->EditIndex * 512, 512);
        switch (s->EditKind)
        {
            case Transform.Zero:
                after.Slice((int)s->EditOffset, (int)s->EditBytes).Clear(); s->ZeroPosition += s->EditBytes; break;
            case Transform.Fat:
                uint original = EndianPrimitives.ReadUInt32LittleEndian(after[(int)s->EditOffset..]);
                EndianPrimitives.WriteUInt32LittleEndian(after[(int)s->EditOffset..], (original & 0xf0000000) | s->EditValue); s->FatCursor++; break;
            case Transform.Directory:
                Span<byte> raw = after.Slice((int)s->EditOffset, 32);
                if (!Fingerprint(s->File, raw)) return Finish(s, Status.StaleToken);
                uint first = s->TargetCount == 0 ? s->Growth[0] : s->File.FirstCluster;
                EndianPrimitives.WriteUInt16LittleEndian(raw[20..], (ushort)(first >> 16)); EndianPrimitives.WriteUInt16LittleEndian(raw[26..], (ushort)first);
                EndianPrimitives.WriteUInt32LittleEndian(raw[28..], s->NewLength); s->Step = Step.GrowFsInfo; break;
            case Transform.FsInfo:
                if (EndianPrimitives.ReadUInt32LittleEndian(after) != 0x41615252 || EndianPrimitives.ReadUInt32LittleEndian(after[484..]) != 0x61417272 ||
                    EndianPrimitives.ReadUInt32LittleEndian(after[508..]) != 0xaa550000) return Finish(s, Status.InvalidVolume);
                // Both are advisory. Preserve unknowns and invalidate old hints
                // rather than inventing a free-count or a verified next slot.
                EndianPrimitives.WriteUInt32LittleEndian(after[488..], uint.MaxValue); EndianPrimitives.WriteUInt32LittleEndian(after[492..], uint.MaxValue); s->FsCursor++; break;
        }
        return Status.Pending;
    }
    private static Status Read(State* s, ulong lba, byte* target, Purpose purpose)
    {
        Block.RequestToken token = default; var status = Block.SubmitRead(s->Input.Device, lba, &token);
        if (status != Block.Status.Success) return DeviceFailure(s, status);
        s->Io = token; s->IoLba = lba; s->IoTarget = target; s->Purpose = purpose; s->IoActive = true; return Status.Pending;
    }
    private static Status Write(State* s)
    {
        if (!s->DirtyValue && s->Revision == ulong.MaxValue) return Finish(s, Status.GenerationExhausted);
        ulong before = s->Input.Device->Submitted; Block.RequestToken token = default;
        var status = Block.SubmitWrite(s->Input.Device, s->Input.Changes[s->Issued].Sector, new ReadOnlySpan<byte>(s->Input.After + s->Issued * 512, 512), &token);
        if (s->Input.Device->Submitted != before) { if (!s->DirtyValue) s->Revision++; s->DirtyValue = true; }
        if (status != Block.Status.Success) return DeviceFailure(s, status);
        s->Io = token; s->IoLba = s->Input.Changes[s->Issued].Sector; s->Purpose = Purpose.Write; s->IoActive = true; return Status.Pending;
    }
    private static Status Flush(State* s)
    {
        Block.RequestToken token = default; var status = Block.SubmitFlush(s->Input.Device, &token);
        if (status != Block.Status.Success) return DeviceFailure(s, status);
        s->Io = token; s->IoLba = 0; s->Purpose = Purpose.Flush; s->IoActive = true; return Status.Pending;
    }
    private static Status CompleteIo(State* s)
    {
        Block.Completion completion = default; var status = Block.Poll(s->Input.Device, &completion);
        if (status == Block.Status.Pending) return Status.Pending;
        bool read = s->Purpose is not (Purpose.Write or Purpose.Flush);
        if (status is not (Block.Status.Success or Block.Status.Ready or Block.Status.Cancelled)) return DeviceFailure(s, status);
        if (completion.Token.Owner != s->Io.Owner || completion.Token.Epoch != s->Io.Epoch || completion.Token.Generation != s->Io.Generation ||
            completion.Sector != s->IoLba || completion.Kind != (read ? Block.Operation.Read : s->Purpose == Purpose.Write ? Block.Operation.Write : Block.Operation.Flush) ||
            completion.DeviceResult != Block.Status.Success) return DeviceFailure(s, Block.Status.InvalidCompletion);
        if (status == Block.Status.Cancelled)
        {
            s->IoActive = false;
            return s->DirtyValue ? Fail(s, Status.RecoveryRequired, true) : Finish(s, Status.Cancelled);
        }
        if (read)
        {
            if (status != Block.Status.Ready || completion.Bytes != 512) return DeviceFailure(s, Block.Status.InvalidCompletion);
            status = Block.CopyRead(s->Input.Device, s->Io, new Span<byte>(s->IoTarget, 512));
            if (status != Block.Status.Success) return DeviceFailure(s, status);
        }
        else if (status != Block.Status.Success || completion.Bytes != (s->Purpose == Purpose.Write ? 512U : 0U)) return DeviceFailure(s, Block.Status.InvalidCompletion);
        s->IoActive = false;
        switch (s->Purpose)
        {
            case Purpose.Bpb:
                var parsed = Chain.ParseBpb(new Span<byte>(s->Input.Sector, 512), s->Input.Device->SectorCount, s->Input.PartitionFirst, s->Input.PartitionLast,
                    BlockReadResult.Success(1, 512), ref s->Volume);
                if (parsed != LiveFat32Status.Success) return Fail(s, Status.InvalidVolume, true);
                s->FsInfoSector = EndianPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(s->Input.Sector + 48, 2));
                s->BackupBootSector = EndianPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(s->Input.Sector + 50, 2));
                BeginRootChain(s, 0); break;
            case Purpose.Fat0: s->FatPart = 1; break;
            case Purpose.Fat1: s->FatPart = 2; break;
            case Purpose.Directory: s->LoadedDirectoryLba = s->IoLba; break;
            case Purpose.Original:
                bool create = s->Kind == Kind.Create;
                uint goal = create ? (s->CreateSlots + (s->CreateEnd ? 1U : 0U)) * 32 : s->Wanted;
                uint index = s->Changes, skip = (uint)(((create ? s->CreateStart * 32UL : s->Offset) + s->PlannedBytes) % 512), take = System.Math.Min(512 - skip, goal - s->PlannedBytes);
                new ReadOnlySpan<byte>(s->Input.Before + index * 512, 512).CopyTo(new Span<byte>(s->Input.After + index * 512, 512));
                for (uint i = 0; i < take; i++) s->Input.After[index * 512 + skip + i] = create ?
                    s->PlannedBytes + i < s->CreateSlots * 32 ? s->Input.EncodedEntries[s->PlannedBytes + i] : (byte)0 : s->Input.Input[s->PlannedBytes + i];
                s->Input.Changes[index] = new() { Sector = s->IoLba, Phase = create ? Fat32Mutation.Stage.PublishDirectory : Fat32Mutation.Stage.Data }; s->Changes++; s->PlannedBytes += take; break;
            case Purpose.Write: s->Issued++; break;
            case Purpose.Flush:
                s->FlushedThrough = s->Issued;
                if (s->Issued < s->Changes) { s->Step = Step.Issue; return Status.Pending; }
                s->DirtyValue = false; return Finish(s, Status.Success);
            case Purpose.GrowthEdit:
                new ReadOnlySpan<byte>(s->Input.Before + s->EditIndex * 512, 512).CopyTo(new Span<byte>(s->Input.After + s->EditIndex * 512, 512));
                s->Input.Changes[s->EditIndex] = new() { Sector = s->IoLba, Phase = s->EditStage }; s->Changes++; return ApplyEdit(s);
        }
        return Status.Pending;
    }
    private static Status DeviceFailure(State* s, Block.Status status)
    { s->DriverFailure = status; s->DeviceFault = true; return Fail(s, s->DirtyValue ? Status.RecoveryRequired : Status.BlockFailure, true); }
    private static Status Fail(State* s, Status status, bool terminal)
    { s->Failure = status; s->Phase = terminal || s->Kind == Kind.Mount ? Life.Faulted : Life.Ready; return status; }
    private static Status Finish(State* s, Status status)
    { s->Failure = status; s->Phase = s->Kind == Kind.Mount ? Life.Faulted : Life.Ready; return Status.Ready; }
    private static void EndRequest(State* s) { s->Phase = Life.Idle; s->IoActive = false; s->Cancelled = false; }
    private static bool FileMatches(State* s, FileToken file) => file.Owner == (ulong)s && file.Epoch == s->Input.Epoch && file.Revision == s->Revision &&
        file.EntryOffset >= s->Volume.FirstDataLba * 512 && file.EntryOffset / 512 < s->Volume.PartitionFirstLba + s->Volume.TotalSectors && (file.EntryOffset & 31) == 0;
    private static bool Fingerprint(FileToken file, ReadOnlySpan<byte> raw) => U64(raw, 0) == file.Fingerprint0 && U64(raw, 8) == file.Fingerprint1 && U64(raw, 16) == file.Fingerprint2 && U64(raw, 24) == file.Fingerprint3;
    private static bool Matches(State* s, RequestToken request) => request.Owner == (ulong)s && request.Epoch == s->Input.Epoch && request.Generation != 0 && request.Generation == s->RequestGeneration && s->Phase is Life.Working or Life.Ready or Life.Faulted;
    private static bool Bound(State* s) => Storage((ulong)s, (ulong)sizeof(State), 8) && s->Self == (ulong)s && s->Phase is >= Life.Mounting and <= Life.Retired;
    private static bool DeviceIdentity(State* s) => Storage((ulong)s->Input.Device, (ulong)sizeof(Block.State), 8) && s->Input.Device->Self == (ulong)s->Input.Device &&
        s->Input.Device->Input.Epoch == s->DeviceEpoch && s->Input.Device->PoolEpoch == s->PoolEpoch;
    private static bool DeviceCurrent(State* s) => DeviceIdentity(s) && s->Input.Device->Running;
    private static bool Available(State* s) => s->Phase == Life.Idle && s->Mounted && !s->DirtyValue && DeviceCurrent(s) && !s->Input.Device->RequestPending;
    private static ushort Fold(ushort value) => value is >= 'a' and <= 'z' ? (ushort)(value - 32) : value;
    private static ulong U64(ReadOnlySpan<byte> raw, int offset) => EndianPrimitives.ReadUInt64LittleEndian(raw[offset..]);
    private static bool NameValid(ReadOnlySpan<char> name)
    {
        if (name.Length is < 1 or > 255 || name[^1] is '.' or ' ' || name.SequenceEqual(".") || name.SequenceEqual("..")) return false;
        bool high = false;
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c < 32 || c is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|' || (high ? c is < '\udc00' or > '\udfff' : c is >= '\udc00' and <= '\udfff')) return false;
            high = c is >= '\ud800' and <= '\udbff';
        }
        return !high;
    }
    private struct Extent { internal ulong Start, Bytes; }
    private static void Extents(State* s, Admission a, Extent* all)
    {
        all[0] = new() { Start = (ulong)s, Bytes = (ulong)sizeof(State) };
        all[1] = new() { Start = (ulong)a.Changes, Bytes = JournalChanges * (ulong)sizeof(Fat32Mutation.Change) };
        all[2] = new() { Start = (ulong)a.Before, Bytes = JournalChanges * 512UL }; all[3] = new() { Start = (ulong)a.After, Bytes = JournalChanges * 512UL };
        all[4] = new() { Start = (ulong)a.DirectoryMap, Bytes = MaximumClusters * 4UL }; all[5] = new() { Start = (ulong)a.FileMap, Bytes = MaximumClusters * 4UL };
        all[6] = new() { Start = (ulong)a.WalkMap, Bytes = MaximumClusters * 4UL }; all[7] = new() { Start = (ulong)a.Sector, Bytes = 512 };
        all[8] = new() { Start = (ulong)a.Fat0, Bytes = 512 }; all[9] = new() { Start = (ulong)a.Fat1, Bytes = 512 };
        all[10] = new() { Start = (ulong)a.Input, Bytes = WriteBytes }; all[11] = new() { Start = (ulong)a.Name, Bytes = NameWords * 2UL };
        all[12] = new() { Start = (ulong)a.LongName, Bytes = VfatWords * 2UL }; all[13] = new() { Start = (ulong)a.EncodedEntries, Bytes = EncodedEntryBytes };
    }
    private static bool Admit(State* s, Admission a)
    {
        if (!Storage((ulong)s, (ulong)sizeof(State), 8) || a.Owner == 0 || a.Epoch == 0 || a.ChangeCapacity != JournalChanges ||
            a.ChainCapacity != MaximumClusters || a.InputCapacity != WriteBytes || a.WorkBudget is 0 or > 1000000 ||
            !Storage((ulong)a.Device, (ulong)sizeof(Block.State), 8) || a.Device->Self != (ulong)a.Device || !a.Device->Running || a.Device->RequestPending || a.Device->Dirty ||
            a.PartitionFirst == 0 || a.PartitionLast < a.PartitionFirst || a.PartitionLast >= a.Device->SectorCount) return false;
        Extent* all = stackalloc Extent[14]; Extents(s, a, all);
        for (int i = 0; i < 14; i++)
        {
            if (!Storage(all[i].Start, all[i].Bytes, 8) || !OutsideDevice(a.Device, all[i].Start, all[i].Bytes)) return false;
            for (int j = 0; j < i; j++) if (Overlap(all[i].Start, all[i].Bytes, all[j].Start, all[j].Bytes)) return false;
        }
        for (ulong i = 0; i < (ulong)sizeof(State); i++) if (((byte*)s)[i] != 0) return false;
        return true;
    }
    private static bool Output(State* s, ulong address, ulong bytes) => Storage(address, bytes, 8) && Caller(s, address, bytes);
    private static bool Caller(State* s, ulong address, ulong bytes)
    {
        if (!Storage(address, bytes, 1)) return false;
        Extent* all = stackalloc Extent[14]; Extents(s, s->Input, all);
        for (int i = 0; i < 14; i++) if (Overlap(address, bytes, all[i].Start, all[i].Bytes)) return false;
        return OutsideDevice(s->Input.Device, address, bytes);
    }
    private static bool OutsideDevice(Block.State* d, ulong address, ulong bytes)
    {
        if (!Storage((ulong)d, (ulong)sizeof(Block.State), 8) || d->Self != (ulong)d) return false;
        var a = d->Input; DmaBufferPool* p = a.Pool;
        if (!Storage((ulong)p, (ulong)sizeof(DmaBufferPool), 8) || p->Count is < 1 or > 64 || !Storage((ulong)p->Slots, p->Count * (ulong)sizeof(DmaBufferSlot), 8)) return false;
        return !Overlap(address, bytes, (ulong)d, (ulong)sizeof(Block.State)) && !Overlap(address, bytes, (ulong)p, (ulong)sizeof(DmaBufferPool)) &&
            !Overlap(address, bytes, (ulong)p->Slots, p->Count * (ulong)sizeof(DmaBufferSlot)) && !Overlap(address, bytes, p->Window.CpuVirtualBase, p->Window.ByteLength) &&
            !Overlap(address, bytes, a.Common.VirtualStart, a.Common.Bytes) && !Overlap(address, bytes, a.Notify.VirtualStart, a.Notify.Bytes) && !Overlap(address, bytes, a.Device.VirtualStart, a.Device.Bytes);
    }
    private static bool Storage(ulong start, ulong bytes, ulong alignment) => start >= 4096 && (start & (alignment - 1)) == 0 && bytes != 0 && start < 0x0000800000000000UL && bytes <= 0x0000800000000000UL - start;
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => n != 0 && m != 0 && a < b + m && b < a + n;
}
