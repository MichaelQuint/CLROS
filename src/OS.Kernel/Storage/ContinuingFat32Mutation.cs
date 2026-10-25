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
/// opens and demand-reads regular root files, creates empty root files, overwrites within their
/// existing length, grows files using explicit free clusters, shrinks/deletes
/// regular root files and sends actual barriers. Nested directories, rename and
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
        BlockFailure, RecoveryRequired, AlreadyExists, InvalidAllocation,
        SearchBudgetExceeded, EndOfScan
    }
    internal enum Life : byte { Mounting = 1, Idle, Working, Ready, Faulted, Retired }
    internal enum Kind : byte { Mount, Open, Overwrite, Barrier, Create, Grow, Read, Truncate, Delete, FindFree, RootMetadata }
    internal enum Step : byte { Bpb, RootChain, Scan, TargetChain, OtherChain, Plan, Issue, Flush, GrowCandidates, GrowData, GrowFat, GrowDirectory, GrowFsInfo, ReadData, ShrinkDirectory, DeleteDirectory, ReleaseFat, FindFree }
    internal enum Purpose : byte { Bpb, Fat0, Fat1, Directory, Original, Write, Flush, GrowthEdit, FileData }
    internal enum Transform : byte { Zero, Fat, Directory, FsInfo, ShrinkDirectory, Tombstone }
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
    /// <summary>Copied successful regular-file open facts. AllocatedClusters
    /// is the actual validated full-chain count, including its final cluster;
    /// Allocated512Blocks counts that chain's sectors, not rounded file bytes.
    /// The engine rejects preallocated/overlong chains. These historical facts
    /// grant no Linux stat policy, current fd/device authority or persistence.</summary>
    public readonly struct OpenFacts
    {
        public readonly FileToken File;
        public readonly uint ClusterBytes, AllocatedClusters;
        public readonly ulong Allocated512Blocks;
        internal OpenFacts(FileToken file, uint clusterBytes, uint clusters, ulong blocks)
        { File = file; ClusterBytes = clusterBytes; AllocatedClusters = clusters; Allocated512Blocks = blocks; }
    }
    /// <summary>Copied mount-root allocation facts from an actual full
    /// mirrored-FAT root-chain walk. The FAT root has no short entry, file
    /// token, Unix owner/mode or timestamp. FirstSector is its first cluster's
    /// physical sector; allocation includes every checked root cluster. These
    /// historical values grant no fd, user-copy or future device authority.</summary>
    public readonly struct RootFacts
    {
        public readonly ulong Owner, Epoch, Revision, FirstSector;
        public readonly uint ClusterBytes, AllocatedClusters;
        public readonly ulong Allocated512Blocks;
        internal RootFacts(State* s, ulong sector, uint clusterBytes, uint clusters, ulong blocks)
        { Owner = (ulong)s; Epoch = s->Input.Epoch; Revision = s->Revision; FirstSector = sector;
            ClusterBytes = clusterBytes; AllocatedClusters = clusters; Allocated512Blocks = blocks; }
    }
    public readonly struct Result
    {
        public readonly Status Status;
        public readonly uint Bytes;
        internal Result(Status status, uint bytes) { Status = status; Bytes = bytes; }
    }
    /// <summary>Copied lineage for an admitted grow/shrink/delete request.
    /// Only Success authenticates NewFile or Deleted, after every planned OUT
    /// and the observed final real FLUSH. Equal-length truncate needs no I/O.
    /// OriginalFile is the admitted physical identity, not a name-reopen guess.
    /// This is not a persistent journal or power-loss recovery receipt.</summary>
    public readonly struct MutationResult
    {
        public readonly Status Status;
        public readonly uint Bytes;
        public readonly RequestToken Request;
        public readonly FileToken OriginalFile, NewFile;
        public readonly bool Deleted;
        internal MutationResult(Status status, uint bytes, RequestToken request, FileToken original, FileToken next, bool deleted)
        { Status = status; Bytes = bytes; Request = request; OriginalFile = original; NewFile = next; Deleted = deleted; }
    }
    /// <summary>Copied forward-only scan position, invalidated by any new
    /// request or issued mutation. It is not a free-space reservation. Only
    /// CreateFreeCursor or a consumed FreeResult supplies a current cursor.</summary>
    public readonly struct FreeCursor
    {
        public readonly ulong Owner, Epoch, Revision, Generation;
        public readonly uint NextCluster;
        internal FreeCursor(State* s, uint next)
        { Owner = (ulong)s; Epoch = s->Input.Epoch; Revision = s->Revision; Generation = s->RequestGeneration; NextCluster = next; }
    }
    /// <summary>Copied candidate facts at one volume revision/request. Success
    /// found the wanted count. SearchBudgetExceeded may contain a verified
    /// partial prefix; EndOfScan reached the physical end of the remaining
    /// cursor range, not a whole-volume ENOSPC claim. No cluster is reserved.</summary>
    public readonly struct FreeResult
    {
        public readonly Status Status;
        public readonly uint Count, Scanned, SectorReads;
        public readonly RequestToken Request;
        public readonly FreeCursor NextCursor;
        internal FreeResult(Status status, uint count, uint scanned, uint reads, RequestToken request, FreeCursor next)
        { Status = status; Count = count; Scanned = scanned; SectorReads = reads; Request = request; NextCursor = next; }
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
        internal uint ScanRunStart, RunStart, RunSlots, KeepCount, ReleaseCount;
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
        if (!Live(s)) return Status.InvalidState;
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

    /// <summary>Start a fresh bounded mirrored-FAT walk of the mounted root
    /// directory. No root entry or file token is invented and no directory
    /// content, OUT or FLUSH is issued. One serialized supervisor retains all
    /// backing. Invalid output aliases write nothing; a new generation revokes
    /// prior request tokens. Pump never spins for device completion.</summary>
    public static Status BeginInspectRoot(State* s, RequestToken* request)
    {
        if (!Live(s)) return Status.InvalidState;
        if (!Output(s, (ulong)request, (ulong)sizeof(RequestToken))) return Status.InvalidStorage;
        *request = default;
        if (!Available(s)) return s->Phase == Life.Faulted ? s->Failure : Status.Busy;
        if (s->RequestGeneration == ulong.MaxValue) return Status.GenerationExhausted;
        Begin(s, Kind.RootMetadata); BeginRootChain(s, ValidateRoot); *request = new(s); return Status.Pending;
    }

    /// <summary>Copy at most8192 input bytes before return. Only in-place
    /// overwrites inside the existing length are supported. Validate all root
    /// file chains, target exclusivity and boundary sectors before first OUT.
    /// Directory metadata/timestamps remain unchanged in this initial slice.</summary>
    public static Status BeginOverwrite(State* s, FileToken file, ulong offset, ReadOnlySpan<byte> bytes, RequestToken* request)
    {
        if (!Live(s)) return Status.InvalidState;
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
        if (!Live(s)) return Status.InvalidState;
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
        if (!Live(s)) return Status.InvalidState;
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

    /// <summary>Copy the additional cluster count required by mounted geometry
    /// for a current file token. Equal length and growth within an existing tail
    /// return Success/count0; shrink is Unsupported. At most16 additional and
    ///4096 total clusters are admitted. No I/O, free-space search, reservation,
    /// mutation or physical fingerprint refresh occurs. Validate ordinary
    /// output storage before writing; valid failure output is0.</summary>
    public static Status GrowthCandidatesRequired(State* s, FileToken file, uint newLength, uint* count)
    {
        if (!Live(s)) return Status.InvalidState;
        if (!Storage((ulong)count, 4, 4) || !Caller(s, (ulong)count, 4)) return Status.InvalidStorage;
        *count = 0;
        if (!Available(s)) return s->Phase == Life.Faulted ? s->Failure : Status.Busy;
        if (!FileMatches(s, file)) return Status.StaleToken;
        if (newLength < file.Length) return Status.Unsupported;
        ulong clusterBytes = s->Volume.SectorsPerCluster * 512UL;
        if (clusterBytes == 0) return Status.InvalidVolume;
        ulong oldCount = ((ulong)file.Length + clusterBytes - 1) / clusterBytes,
            newCount = ((ulong)newLength + clusterBytes - 1) / clusterBytes;
        if (newCount > MaximumClusters || newCount - oldCount > MaximumGrowthClusters) return Status.CapacityExceeded;
        *count = (uint)(newCount - oldCount); return Status.Success;
    }

    /// <summary>Copy a scan cursor for a current idle mounted owner. Accepts
    ///2..ClusterCount+2 inclusive; the exclusive end starts an empty range.
    /// No I/O occurs. Output aliases reject before writes; valid failures clear
    /// output. A subsequent unrelated request invalidates this cursor.</summary>
    public static Status CreateFreeCursor(State* s, uint firstCluster, FreeCursor* cursor)
    {
        if (!Live(s)) return Status.InvalidState;
        if (!Output(s, (ulong)cursor, (ulong)sizeof(FreeCursor))) return Status.InvalidStorage;
        *cursor = default;
        if (!Available(s)) return s->Phase == Life.Faulted ? s->Failure : Status.Busy;
        if (firstCluster < 2 || firstCluster > s->Volume.ClusterCount + 2) return Status.InvalidAllocation;
        *cursor = new(s, firstCluster); return Status.Success;
    }

    /// <summary>Start one forward scan for1..16 free clusters, copying the
    /// cursor/budgets before return. The cluster budget may not exceed admitted
    /// WorkBudget; the independent sector budget counts actual IN submissions
    /// including both FAT copies. Zero budgets produce SearchBudgetExceeded.
    /// Every Pump inspects at most one entry or submits/completes one sector.
    /// Full mirrored sectors must agree before low28-bit zero is admitted;
    /// reserved upper bits retain the existing FAT contract. No FSInfo hint,
    /// OUT, FLUSH, allocation, wraparound or reservation is used. BeginGrow must
    /// still revalidate every returned candidate before mutation.</summary>
    public static Status BeginFindFree(State* s, FreeCursor cursor, uint wanted, uint clusterBudget, uint sectorReadBudget, RequestToken* request)
    {
        if (!Live(s)) return Status.InvalidState;
        if (!Output(s, (ulong)request, (ulong)sizeof(RequestToken))) return Status.InvalidStorage;
        *request = default;
        if (!Available(s)) return s->Phase == Life.Faulted ? s->Failure : Status.Busy;
        if (cursor.Owner != (ulong)s || cursor.Epoch != s->Input.Epoch || cursor.Revision != s->Revision || cursor.Generation != s->RequestGeneration ||
            cursor.NextCluster < 2 || cursor.NextCluster > s->Volume.ClusterCount + 2) return Status.StaleToken;
        if (wanted == 0) return Status.InvalidAllocation;
        if (wanted > MaximumGrowthClusters || clusterBudget > s->Input.WorkBudget) return Status.CapacityExceeded;
        if (s->RequestGeneration == ulong.MaxValue) return Status.GenerationExhausted;
        Begin(s, Kind.FindFree); s->Step = Step.FindFree; s->WalkNext = cursor.NextCluster; s->Offset = cursor.Revision;
        // This kind borrows otherwise inactive growth counters/16-value POD
        // storage; journal bytes/maps remain unchanged and no layout grows.
        s->Wanted = wanted; s->GrowthCursor = clusterBudget; s->FsCursor = sectorReadBudget;
        *request = new(s); return Status.Pending;
    }

    /// <summary>Consume one scan Ready prefix into exclusive ordinary RAM.
    /// Bounds/aliases and capacity authenticate before any output write. Invalid
    /// capacity preserves Ready. Success/budget/end metadata binds this request
    /// and a continuation cursor; other errors/cancellation consume clean Ready
    /// with default metadata/no candidates. Faulted owners retain cleanup.
    /// Device/reset/revision must still match before positive publication.
    /// No clock callback or fallible work follows copying; candidates remain
    /// unreserved and may not be reused after another request/mutation.</summary>
    public static Status CopyFree(State* s, RequestToken request, Span<uint> candidates, FreeResult* result)
    {
        if (!Live(s)) return Status.InvalidState;
        fixed (uint* output = candidates)
        {
            if (!Output(s, (ulong)result, (ulong)sizeof(FreeResult)) || candidates.Length > MaximumGrowthClusters || candidates.Length != 0 &&
                (!Caller(s, (ulong)output, (ulong)candidates.Length * 4) || Overlap((ulong)output, (ulong)candidates.Length * 4, (ulong)result, (ulong)sizeof(FreeResult)))) return Status.InvalidStorage;
            if (!Matches(s, request) || s->Kind != Kind.FindFree) { *result = default; return Status.StaleToken; }
            if (s->Phase != Life.Ready) { *result = default; return s->Phase == Life.Faulted ? s->Failure : Status.Pending; }
            Status status = s->Failure;
            bool usable = status is Status.Success or Status.SearchBudgetExceeded or Status.EndOfScan;
            if (usable && (s->GrowthCount > MaximumGrowthClusters || s->GrowthCount > s->Wanted || s->GrowthCount > candidates.Length)) return Status.InvalidStorage;
            *result = default;
            if (usable)
            {
                if (s->Revision != s->Offset) return Status.StaleToken;
                if (!DeviceCurrent(s) || s->Input.Device->RequestPending) return DeviceFailure(s, Block.Status.InvalidState);
                for (uint i = 0; i < s->GrowthCount; i++)
                    if (s->Growth[i] < 2 || s->Growth[i] - 2 >= s->Volume.ClusterCount || i != 0 && s->Growth[i] <= s->Growth[i - 1]) return Status.InvalidState;
                for (uint i = 0; i < s->GrowthCount; i++) output[i] = s->Growth[i];
                *result = new(status, s->GrowthCount, s->PlannedBytes, s->FatCursor, request, new(s, s->WalkNext));
            }
            ScrubFree(s);
            EndRequest(s); return status;
        }
    }

    /// <summary>Shrink one root file without reallocating or erasing hidden
    /// bytes. Plan all changes before OUT; publish/FLUSH the shorter directory
    /// length, then both FAT releases/FLUSH, then FSInfo/FLUSH when allocation
    /// changed. Zero length clears the directory first cluster before release.
    /// A later grow zeroes every newly visible byte. Result Bytes is the removed
    /// logical length; equal length succeeds0 without I/O. Growth uses BeginGrow.
    /// This is not a persistent journal, secure erase or POSIX fd lifetime.</summary>
    public static Status BeginTruncate(State* s, FileToken file, uint newLength, RequestToken* request) => BeginRetire(s, file, newLength, Kind.Truncate, request);

    /// <summary>Delete one regular root file by scrubbing each owned valid
    /// VFAT/short entry and retaining only its E5 tombstone, then FLUSH before
    /// both FAT release/FLUSH and FSInfo/FLUSH. Unrelated later entries stay
    /// visible. No cluster content erase or open-unlink semantics is promised.
    /// Result Bytes is0; first OUT revokes every old revision token. Dirty
    /// failure retains the complete original before/after journal for recovery.</summary>
    public static Status BeginDelete(State* s, FileToken file, RequestToken* request) => BeginRetire(s, file, 0, Kind.Delete, request);

    private static Status BeginRetire(State* s, FileToken file, uint newLength, Kind kind, RequestToken* request)
    {
        if (!Live(s)) return Status.InvalidState;
        if (!Output(s, (ulong)request, (ulong)sizeof(RequestToken))) return Status.InvalidStorage;
        *request = default;
        if (!Available(s)) return s->Phase == Life.Faulted ? s->Failure : Status.Busy;
        if (!FileMatches(s, file)) return Status.StaleToken;
        if (newLength > file.Length) return Status.Unsupported;
        if (s->RequestGeneration == ulong.MaxValue || s->Revision == ulong.MaxValue) return Status.GenerationExhausted;
        Begin(s, kind); s->File = file; s->NewLength = newLength; s->Wanted = kind == Kind.Delete ? 0 : file.Length - newLength;
        ulong clusterBytes = s->Volume.SectorsPerCluster * 512UL; s->KeepCount = (uint)(((ulong)newLength + clusterBytes - 1) / clusterBytes);
        if (kind == Kind.Truncate && newLength == file.Length) Finish(s, Status.Success); else BeginRootChain(s, LookupToken);
        *request = new(s); return s->Phase == Life.Ready ? Status.Ready : Status.Pending;
    }

    /// <summary>Demand-read at most8192 bytes through the same exclusive queue.
    /// Copy the request and validate the token/physical entry/full file map;
    /// stage only this range in the exclusive input buffer, never journal
    /// Before/After. No caller output pointer or whole-file preload is retained.
    /// EOF/zero bytes become Ready0. Other requests stay Busy until CopyRead or
    /// cancellation consumes the held result. The descriptor owner separately
    /// revalidates its original fd generation and memory mapping before copy.</summary>
    public static Status BeginRead(State* s, FileToken file, ulong offset, uint requestedBytes, RequestToken* request)
    {
        if (!Live(s)) return Status.InvalidState;
        if (!Output(s, (ulong)request, (ulong)sizeof(RequestToken))) return Status.InvalidStorage;
        *request = default;
        if (!Available(s)) return s->Phase == Life.Faulted ? s->Failure : Status.Busy;
        if (!FileMatches(s, file)) return Status.StaleToken;
        if (requestedBytes > WriteBytes) return Status.CapacityExceeded;
        if (s->RequestGeneration == ulong.MaxValue) return Status.GenerationExhausted;
        Begin(s, Kind.Read); s->File = file; s->Offset = offset;
        ulong remaining = offset >= file.Length ? 0 : file.Length - offset;
        s->Wanted = remaining < requestedBytes ? (uint)remaining : requestedBytes;
        if (s->Wanted == 0) Finish(s, Status.Success); else BeginRootChain(s, LookupToken);
        *request = new(s); return s->Phase == Life.Ready ? Status.Ready : Status.Pending;
    }

    /// <summary>Publish a held range into exclusive ordinary RAM, with exact
    /// returned Bytes=min(requested,length-offset). Destination may be larger
    /// than that prefix but at most8192 bytes; unused bytes stay unchanged.
    /// Validate destination/result bounds and aliases before any output write;
    /// invalid storage preserves Ready for retry. Revalidate the original block
    /// lifetime before publication, then perform no fallible callback. Pending,
    /// stale, cancelled or faulted requests never publish file bytes. Metadata
    /// failure results are default/Bytes0. Consumption scrubs copied input.</summary>
    public static Status CopyRead(State* s, RequestToken request, Span<byte> destination, Result* result)
    {
        if (!Live(s)) return Status.InvalidState;
        fixed (byte* output = destination)
        {
            if (!Output(s, (ulong)result, (ulong)sizeof(Result)) || destination.Length > WriteBytes || destination.Length != 0 &&
                (!Caller(s, (ulong)output, (ulong)destination.Length) || Overlap((ulong)output, (ulong)destination.Length, (ulong)result, (ulong)sizeof(Result)))) return Status.InvalidStorage;
            if (!Matches(s, request) || s->Kind != Kind.Read) { *result = default; return Status.StaleToken; }
            if (s->Phase != Life.Ready) { *result = default; return s->Phase == Life.Faulted ? s->Failure : Status.Pending; }
            Status status = s->Failure;
            if (status == Status.Success && (uint)destination.Length < s->Wanted) return Status.InvalidStorage;
            if (status == Status.Success && (!DeviceCurrent(s) || s->Input.Device->RequestPending)) { *result = default; return DeviceFailure(s, Block.Status.InvalidState); }
            uint bytes = status == Status.Success ? s->Wanted : 0;
            for (uint i = 0; i < bytes; i++) output[i] = s->Input.Input[i];
            *result = new(status, bytes); ScrubRead(s); EndRequest(s); return status;
        }
    }

    /// <summary>Request a real volume FLUSH, even when no data is dirty. This
    /// is a block barrier; no regular-file or directory durability is fabricated.</summary>
    public static Status BeginFlush(State* s, RequestToken* request)
    {
        if (!Live(s)) return Status.InvalidState;
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
            case Step.ReadData: return ReadFileData(s);
            case Step.FindFree: return FindFree(s);
            case Step.GrowCandidates: return CheckGrowthCandidates(s);
            case Step.GrowData: return PlanGrowthData(s);
            case Step.GrowFat: return PlanGrowthFat(s);
            case Step.GrowDirectory: return Edit(s, s->File.EntryOffset / 512, Fat32Mutation.Stage.PublishDirectory, Transform.Directory, (uint)(s->File.EntryOffset % 512), 32, 0);
            case Step.ShrinkDirectory: return Edit(s, s->File.EntryOffset / 512, Fat32Mutation.Stage.PublishDirectory, Transform.ShrinkDirectory, (uint)(s->File.EntryOffset % 512), 32, 0);
            case Step.DeleteDirectory: return PlanDeleteDirectory(s);
            case Step.ReleaseFat: return PlanReleaseFat(s);
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
        if (!Live(s)) return Status.InvalidState;
        if (!Output(s, (ulong)file, (ulong)sizeof(FileToken))) return Status.InvalidStorage;
        *file = default; if (!Matches(s, request) || s->Kind != Kind.Open) return Status.StaleToken;
        if (s->Phase != Life.Ready) return s->Phase == Life.Faulted ? s->Failure : Status.Pending;
        Status result = s->Failure; if (result == Status.Success) *file = s->File;
        EndRequest(s); return result;
    }

    /// <summary>Consume only a matching successful retained regular-file Open
    /// into exclusive aligned ordinary RAM. factsBytes must equal sizeof
    /// OpenFacts; validate the full extent/aliases before any output write.
    /// Invalid storage preserves output and Ready. Other rejections clear the
    /// admitted output but preserve the request; existing CopyOpen drains an
    /// unsuccessful open. No I/O/callback/allocation occurs. The original clean
    /// running block/DMA lifetime must still be current, while the final fd and
    /// user-memory checks remain the caller's responsibility. Raw timestamp and
    /// POSIX inode/mode policy are not introduced here.</summary>
    public static Status CopyOpenFacts(State* s, RequestToken request, OpenFacts* facts, uint factsBytes)
    {
        if (!Live(s)) return Status.InvalidState;
        if (factsBytes != sizeof(OpenFacts) || !Output(s, (ulong)facts, (ulong)sizeof(OpenFacts))) return Status.InvalidStorage;
        *facts = default;
        if (!Matches(s, request) || s->Kind != Kind.Open) return Status.StaleToken;
        if (s->Phase != Life.Ready) return s->Phase == Life.Faulted ? s->Failure : Status.Pending;
        if (s->Failure != Status.Success) return s->Failure;
        if (!CanAdmitStorage(s, s->Input.Epoch)) return Status.BlockFailure;
        uint sectors = s->Volume.SectorsPerCluster, clusterBytes = sectors * 512U;
        if (sectors is 0 or > 128 || (sectors & (sectors - 1)) != 0 || !FileMatches(s, s->File) ||
            ((s->File.Fingerprint1 >> 24) & 0x18) != 0) return Status.InvalidState;
        if (s->TargetCount > MaximumClusters || s->TargetCount != ((ulong)s->File.Length + clusterBytes - 1) / clusterBytes ||
            (s->File.Length == 0 ? s->File.FirstCluster != 0 : s->File.FirstCluster < 2 || s->File.FirstCluster - 2 >= s->Volume.ClusterCount))
            return Status.CorruptChain;
        *facts = new(s->File, clusterBytes, s->TargetCount, (ulong)s->TargetCount * sectors);
        EndRequest(s); return Status.Success;
    }

    /// <summary>Consume matching Ready root facts into exactly sizeof
    /// RootFacts bytes of disjoint ordinary RAM. Invalid size/aliases preserve
    /// output and Ready. Other rejections clear admitted output. Success needs
    /// the original clean running device/DMA owner; no post-copy action can
    /// fail. Clean failed/cancelled Ready results drain without publishing facts;
    /// faulted or stale requests remain owned for explicit cleanup. No I/O,
    /// allocation or root-file identity is supplied.</summary>
    public static Status CopyRootFacts(State* s, RequestToken request, RootFacts* facts, uint factsBytes)
    {
        if (!Live(s)) return Status.InvalidState;
        if (factsBytes != sizeof(RootFacts) || !Output(s, (ulong)facts, (ulong)sizeof(RootFacts))) return Status.InvalidStorage;
        *facts = default;
        if (!Matches(s, request) || s->Kind != Kind.RootMetadata) return Status.StaleToken;
        if (s->Phase != Life.Ready) return s->Phase == Life.Faulted ? s->Failure : Status.Pending;
        Status result = s->Failure;
        if (result == Status.Success)
        {
            if (!CanAdmitStorage(s, s->Input.Epoch)) return Status.BlockFailure;
            uint sectors = s->Volume.SectorsPerCluster;
            if (sectors is 0 or > 128 || (sectors & (sectors - 1)) != 0 || s->RootCount is 0 or > MaximumClusters ||
                s->Volume.RootCluster < 2 || s->Volume.RootCluster - 2 >= s->Volume.ClusterCount ||
                s->Input.DirectoryMap[0] != s->Volume.RootCluster) return Status.CorruptChain;
            ulong first = s->Volume.FirstDataLba + (ulong)(s->Volume.RootCluster - 2) * sectors;
            *facts = new(s, first, sectors * 512U, s->RootCount, (ulong)s->RootCount * sectors);
        }
        EndRequest(s); return result;
    }

    /// <summary>Consume a retained overwrite/barrier result. No user pointer,
    /// cursor or fallible post-copy work is retained. Dirty failures have no
    /// successful consumable result and continue owning their journals.</summary>
    public static Status CopyResult(State* s, RequestToken request, Result* result)
    {
        if (!Live(s)) return Status.InvalidState;
        if (!Output(s, (ulong)result, (ulong)sizeof(Result))) return Status.InvalidStorage;
        *result = default; if (!Matches(s, request) || s->Kind is not (Kind.Overwrite or Kind.Barrier or Kind.Create or Kind.Grow or Kind.Truncate or Kind.Delete)) return Status.StaleToken;
        if (s->Phase != Life.Ready) return s->Phase == Life.Faulted ? s->Failure : Status.Pending;
        Status status = s->Failure; *result = new(status, status == Status.Success ? s->Wanted : 0); EndRequest(s); return status;
    }

    /// <summary>Consume only the matching retained grow/truncate/delete Ready
    /// result into exclusive ordinary RAM. Bounds/aliases authenticate before
    /// any output write. Stale/pending/faulted results are default; a clean
    /// failed/cancelled Ready result carries no new-file/deletion authority.
    /// Success derives the new full physical fingerprint from the exact unique
    /// staged short entry after the actual final FLUSH. Invalid retained lineage
    /// fails closed without consuming Ready; old CopyResult remains available
    /// unchanged. No output pointer is retained, and no fallible work follows
    /// publication. Outer descriptor owners must still validate their fd/inode
    /// generations, including a close while this result was held.</summary>
    public static Status CopyMutationResult(State* s, RequestToken request, MutationResult* result)
    {
        if (!Live(s)) return Status.InvalidState;
        if (!Output(s, (ulong)result, (ulong)sizeof(MutationResult))) return Status.InvalidStorage;
        *result = default;
        if (!Matches(s, request) || s->Kind is not (Kind.Grow or Kind.Truncate or Kind.Delete)) return Status.StaleToken;
        if (s->Phase != Life.Ready) return s->Phase == Life.Faulted ? s->Failure : Status.Pending;
        Status status = s->Failure;
        FileToken next = default; bool deleted = false;
        if (status == Status.Success && !MutationLineage(s, ref next, ref deleted)) return Status.InvalidState;
        *result = new(status, status == Status.Success ? s->Wanted : 0, request, s->File, next, deleted);
        EndRequest(s); return status;
    }

    private static bool MutationLineage(State* s, ref FileToken next, ref bool deleted)
    {
        if (s->IoActive || s->DirtyValue || !s->Mounted) return false;
        if (s->Kind == Kind.Truncate && s->NewLength == s->File.Length)
        {
            if (s->Changes != 0 || s->Issued != 0 || s->FlushedThrough != 0 || s->Wanted != 0 || !FileMatches(s, s->File)) return false;
            next = s->File; return true;
        }
        FileToken original = s->File;
        if (s->Changes is 0 or > JournalChanges || s->Issued != s->Changes || s->FlushedThrough != s->Changes ||
            original.Owner != (ulong)s || original.Epoch != s->Input.Epoch || original.Revision == ulong.MaxValue ||
            original.Revision == 0 || s->Revision != original.Revision + 1 || (original.EntryOffset & 31) != 0 ||
            original.EntryOffset < s->Volume.FirstDataLba * 512 || original.EntryOffset / 512 >= s->Volume.PartitionFirstLba + s->Volume.TotalSectors) return false;
        uint found = uint.MaxValue;
        var stage = s->Kind == Kind.Delete ? Fat32Mutation.Stage.RetireDirectory : Fat32Mutation.Stage.PublishDirectory;
        for (uint i = 0; i < s->Changes; i++)
        {
            if (s->Input.Changes[i].Sector != original.EntryOffset / 512 || s->Input.Changes[i].Phase != stage) continue;
            if (found != uint.MaxValue) return false;
            found = i;
        }
        if (found == uint.MaxValue) return false;
        ReadOnlySpan<byte> raw = new(s->Input.After + found * 512 + original.EntryOffset % 512, 32);
        if (s->Kind == Kind.Delete)
        {
            if (s->Wanted != 0 || raw[0] != 0xe5) return false;
            for (int i = 1; i < 32; i++) if (raw[i] != 0) return false;
            deleted = true; return true;
        }
        Span<byte> before = stackalloc byte[32];
        EndianPrimitives.WriteUInt64LittleEndian(before, original.Fingerprint0);
        EndianPrimitives.WriteUInt64LittleEndian(before[8..], original.Fingerprint1);
        EndianPrimitives.WriteUInt64LittleEndian(before[16..], original.Fingerprint2);
        EndianPrimitives.WriteUInt64LittleEndian(before[24..], original.Fingerprint3);
        if (!raw[..20].SequenceEqual(before[..20]) || !raw.Slice(22, 4).SequenceEqual(before.Slice(22, 4))) return false;
        uint expectedFirst;
        if (s->Kind == Kind.Grow)
        {
            if (s->NewLength <= original.Length || s->Wanted != s->NewLength - original.Length) return false;
            if (original.FirstCluster == 0 && s->GrowthCount is 0 or > MaximumGrowthClusters) return false;
            expectedFirst = original.FirstCluster == 0 ? s->Growth[0] : original.FirstCluster;
        }
        else
        {
            if (s->NewLength >= original.Length || s->Wanted != original.Length - s->NewLength) return false;
            expectedFirst = s->NewLength == 0 ? 0 : original.FirstCluster;
        }
        uint first = ((uint)EndianPrimitives.ReadUInt16LittleEndian(raw[20..]) << 16) | EndianPrimitives.ReadUInt16LittleEndian(raw[26..]);
        if (first != expectedFirst || EndianPrimitives.ReadUInt32LittleEndian(raw[28..]) != s->NewLength) return false;
        next = new(s, original.EntryOffset, first, s->NewLength, raw); return true;
    }

    /// <summary>Write/barrier Ready completion wins. A held read/discovery Ready is
    /// revoked/scrubbed immediately. Root metadata Ready is revoked and kept
    /// until CopyRootFacts drains its Cancelled status. Otherwise drain
    /// the actual token without retracting DMA. Before first write the plan may
    /// be discarded after reads drain; later cancellation retains recovery.</summary>
    public static Status Cancel(State* s, RequestToken request)
    {
        if (!Bound(s) || !Matches(s, request)) return Status.StaleToken;
        if (s->Phase == Life.Ready)
        {
            if (s->Kind == Kind.RootMetadata) { s->Cancelled = true; s->Failure = Status.Cancelled; return Status.Ready; }
            if (s->Kind is not (Kind.Read or Kind.FindFree)) return Status.Ready;
            if (s->Kind == Kind.Read) ScrubRead(s); else ScrubFree(s);
            s->Failure = Status.Cancelled; EndRequest(s); return Status.Cancelled;
        }
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
        for (int i = 0; i < 14; i++)
        {
            if (!Storage(all[i].Start, all[i].Bytes, 8) || !OutsideDevice(s->Input.Device, all[i].Start, all[i].Bytes)) return Status.InvalidStorage;
            for (int j = 0; j < i; j++) if (Overlap(all[i].Start, all[i].Bytes, all[j].Start, all[j].Bytes)) return Status.InvalidStorage;
        }
        for (int i = 1; i < 14; i++) for (ulong j = 0; j < all[i].Bytes; j++) ((byte*)all[i].Start)[j] = 0;
        s->IoActive = false; s->Mounted = false; s->Phase = Life.Retired; return Status.Success;
    }

    private static void Begin(State* s, Kind kind)
    {
        s->Kind = kind; s->RequestGeneration++; s->Phase = Life.Working; s->Failure = Status.Success; s->DriverFailure = Block.Status.Success;
        s->Cancelled = s->DeviceFault = false; s->Changes = s->Issued = s->Wanted = s->PlannedBytes = s->Work = 0; s->FatPart = 0;
        s->CreateHasRun = s->CreateEnd = false; s->CreateSlots = s->CreateStart = s->CreateFree = 0;
        s->GrowthCount = s->GrowthCursor = s->FatCursor = s->FsCursor = s->FlushedThrough = 0;
        s->RunStart = s->RunSlots = s->KeepCount = s->ReleaseCount = 0;
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
            if (s->Kind == Kind.RootMetadata) return Finish(s, Status.Success);
            ResetScan(s); return Status.Pending;
        }
        uint bytes = s->Step == Step.TargetChain ? s->File.Length : s->OtherLength;
        if ((ulong)s->WalkCount != ((ulong)bytes + s->Volume.SectorsPerCluster * 512 - 1) / (s->Volume.SectorsPerCluster * 512)) return Finish(s, Status.CorruptChain);
        if (s->Step == Step.TargetChain)
        {
            s->TargetCount = s->WalkCount; if (s->Kind == Kind.Open) return Finish(s, Status.Success);
            if (s->Kind == Kind.Read) { s->Step = Step.ReadData; return Status.Pending; }
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
    private static Status FindFree(State* s)
    {
        if (s->GrowthCount == s->Wanted) return Finish(s, Status.Success);
        if (s->WalkNext == s->Volume.ClusterCount + 2) return Finish(s, Status.EndOfScan);
        if (s->WalkNext < 2 || s->WalkNext - 2 >= s->Volume.ClusterCount) return Finish(s, Status.InvalidVolume);
        if (s->PlannedBytes == s->GrowthCursor || s->Work == s->Input.WorkBudget) return Finish(s, Status.SearchBudgetExceeded);
        ulong lba = s->Volume.FirstFatLba + s->WalkNext / 128;
        bool read = s->FatPart == 0 || s->FatLba != lba || s->FatPart == 1;
        if (read && s->FatCursor == s->FsCursor) return Finish(s, Status.SearchBudgetExceeded);
        ulong submitted = s->Input.Device->Submitted;
        Status status = RawFat(s, s->WalkNext, out uint value);
        if (s->Input.Device->Submitted != submitted) s->FatCursor++;
        if (status != Status.Success) return status;
        s->WalkNext++; s->PlannedBytes++; s->Work++;
        if (value == 0) s->Growth[s->GrowthCount++] = s->WalkNext - 1;
        return Status.Pending;
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
        if (s->Vfat.Slots == 0) s->ScanRunStart = logical;
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
        if ((s->Kind is Kind.Overwrite or Kind.Grow or Kind.Truncate or Kind.Delete) && (parsed.Attributes & 1) != 0) return Finish(s, Status.ReadOnly);
        s->File = new(s, physical, parsed.FirstCluster, parsed.ByteCount, raw); s->TargetCount = s->WalkCount = 0;
        s->RunStart = s->ScanRunStart; s->RunSlots = logical - s->RunStart + 1;
        if (s->RunSlots > 21) return Finish(s, Status.CorruptDirectory);
        if (parsed.ByteCount == 0)
        {
            if (parsed.FirstCluster != 0) return Finish(s, Status.CorruptChain);
            if (s->Kind is not (Kind.Grow or Kind.Truncate or Kind.Delete)) return Finish(s, Status.Success);
            s->ScanReason = ValidateRoot; ResetScan(s); return Status.Pending;
        }
        s->WalkNext = parsed.FirstCluster; s->Step = Step.TargetChain; return Status.Pending;
    }
    private static Status ScanEnd(State* s)
    {
        if (Vfat.Finish(s->Vfat) != LiveDirectoryStatus.End) return Finish(s, Status.CorruptDirectory);
        if (s->Kind == Kind.Create) { if (!s->CreateHasRun) return Finish(s, Status.CapacityExceeded); }
        else if (s->ScanReason != ValidateRoot) return Finish(s, s->ScanReason == LookupToken ? Status.StaleToken : Status.NotFound);
        if (s->Kind is Kind.Truncate or Kind.Delete)
        {
            if (s->KeepCount > s->TargetCount) return Finish(s, Status.CorruptChain);
            s->ReleaseCount = s->TargetCount - s->KeepCount; s->Step = s->Kind == Kind.Delete ? Step.DeleteDirectory : Step.ShrinkDirectory;
        }
        else s->Step = s->Kind == Kind.Grow ? Step.GrowCandidates : Step.Plan;
        return Status.Pending;
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
    private static Status ReadFileData(State* s)
    {
        if (s->PlannedBytes == s->Wanted) return Finish(s, Status.Success);
        ulong position = s->Offset + s->PlannedBytes, clusterBytes = s->Volume.SectorsPerCluster * 512UL;
        uint map = (uint)(position / clusterBytes); if (map >= s->TargetCount) return Finish(s, Status.CorruptChain);
        ulong lba = s->Volume.FirstDataLba + (ulong)(s->Input.FileMap[map] - 2) * s->Volume.SectorsPerCluster + position % clusterBytes / 512;
        s->EditOffset = (uint)(position % 512); s->EditBytes = System.Math.Min(512 - s->EditOffset, s->Wanted - s->PlannedBytes);
        return Read(s, lba, s->Input.Sector, Purpose.FileData);
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
        if (s->GrowthCount == 0 && s->ReleaseCount == 0 || s->FsInfoSector == 0xffff || s->FsCursor == 2 || s->FsCursor == 1 && s->BackupBootSector is 0 or 0xffff)
        { s->Step = Step.Issue; return Status.Pending; }
        if (s->FsInfoSector == 0 || s->FsInfoSector >= s->Volume.ReservedSectors || s->FsCursor == 1 &&
            (s->BackupBootSector >= s->Volume.ReservedSectors || s->FsInfoSector >= s->Volume.ReservedSectors - s->BackupBootSector)) return Finish(s, Status.InvalidVolume);
        ulong lba = s->Input.PartitionFirst + s->FsInfoSector + (s->FsCursor == 0 ? 0 : s->BackupBootSector);
        return Edit(s, lba, Fat32Mutation.Stage.FsInfo, Transform.FsInfo, 0, 512, 0);
    }
    private static Status PlanDeleteDirectory(State* s)
    {
        if (s->PlannedBytes == s->RunSlots * 32) { s->Step = Step.ReleaseFat; return Status.Pending; }
        ulong position = s->RunStart * 32UL + s->PlannedBytes, clusterBytes = s->Volume.SectorsPerCluster * 512UL;
        uint map = (uint)(position / clusterBytes); if (map >= s->RootCount) return Finish(s, Status.CorruptDirectory);
        uint offset = (uint)(position % 512), bytes = System.Math.Min(512 - offset, s->RunSlots * 32 - s->PlannedBytes);
        ulong lba = s->Volume.FirstDataLba + (ulong)(s->Input.DirectoryMap[map] - 2) * s->Volume.SectorsPerCluster + position % clusterBytes / 512;
        return Edit(s, lba, Fat32Mutation.Stage.RetireDirectory, Transform.Tombstone, offset, bytes, 0);
    }
    private static Status PlanReleaseFat(State* s)
    {
        uint edges = s->ReleaseCount == 0 ? 0 : s->ReleaseCount + (s->KeepCount == 0 ? 0U : 1U);
        if (s->FatCursor == edges * 2) { s->Step = Step.GrowFsInfo; return Status.Pending; }
        uint edge = s->FatCursor / 2, cluster, value;
        if (s->KeepCount != 0 && edge == 0) { cluster = s->Input.FileMap[s->KeepCount - 1]; value = 0x0fffffff; }
        else { uint at = edge - (s->KeepCount == 0 ? 0U : 1U); cluster = s->Input.FileMap[s->KeepCount + at]; value = 0; }
        ulong lba = s->Volume.FirstFatLba + cluster / 128 + (s->FatCursor % 2) * s->Volume.FatSectors;
        return Edit(s, lba, Fat32Mutation.Stage.ReleaseFat, Transform.Fat, cluster % 128 * 4, 4, value);
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
            case Transform.ShrinkDirectory:
                Span<byte> raw = after.Slice((int)s->EditOffset, 32);
                if (!Fingerprint(s->File, raw)) return Finish(s, Status.StaleToken);
                uint first = s->EditKind == Transform.ShrinkDirectory ? s->NewLength == 0 ? 0 : s->File.FirstCluster : s->TargetCount == 0 ? s->Growth[0] : s->File.FirstCluster;
                EndianPrimitives.WriteUInt16LittleEndian(raw[20..], (ushort)(first >> 16)); EndianPrimitives.WriteUInt16LittleEndian(raw[26..], (ushort)first);
                EndianPrimitives.WriteUInt32LittleEndian(raw[28..], s->NewLength); s->Step = s->EditKind == Transform.ShrinkDirectory ? Step.ReleaseFat : Step.GrowFsInfo; break;
            case Transform.Tombstone:
                if (s->Input.Changes[s->EditIndex].Sector == s->File.EntryOffset / 512 && !Fingerprint(s->File, after.Slice((int)(s->File.EntryOffset % 512), 32))) return Finish(s, Status.StaleToken);
                for (uint i = 0; i < s->EditBytes; i += 32) { var entry = after.Slice((int)(s->EditOffset + i), 32); entry.Clear(); entry[0] = 0xe5; }
                s->PlannedBytes += s->EditBytes; break;
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
            case Purpose.FileData:
                new ReadOnlySpan<byte>(s->Input.Sector + s->EditOffset, (int)s->EditBytes).CopyTo(new Span<byte>(s->Input.Input + s->PlannedBytes, (int)s->EditBytes));
                s->PlannedBytes += s->EditBytes; break;
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
    private static void ScrubRead(State* s) { for (uint i = 0; i < s->Wanted; i++) s->Input.Input[i] = 0; }
    private static void ScrubFree(State* s) { for (uint i = 0; i < MaximumGrowthClusters; i++) s->Growth[i] = 0; }
    private static bool FileMatches(State* s, FileToken file) => file.Owner == (ulong)s && file.Epoch == s->Input.Epoch && file.Revision == s->Revision &&
        file.EntryOffset >= s->Volume.FirstDataLba * 512 && file.EntryOffset / 512 < s->Volume.PartitionFirstLba + s->Volume.TotalSectors && (file.EntryOffset & 31) == 0;
    private static bool Fingerprint(FileToken file, ReadOnlySpan<byte> raw) => U64(raw, 0) == file.Fingerprint0 && U64(raw, 8) == file.Fingerprint1 && U64(raw, 16) == file.Fingerprint2 && U64(raw, 24) == file.Fingerprint3;
    private static bool Matches(State* s, RequestToken request) => request.Owner == (ulong)s && request.Epoch == s->Input.Epoch && request.Generation != 0 && request.Generation == s->RequestGeneration && s->Phase is Life.Working or Life.Ready or Life.Faulted;
    private static bool Bound(State* s) => Storage((ulong)s, (ulong)sizeof(State), 8) && s->Self == (ulong)s && s->Phase is >= Life.Mounting and <= Life.Retired;
    private static bool Live(State* s) => Bound(s) && s->Phase != Life.Retired;
    // Structural provider admission only. A mounted owner retains these
    // extents even after a device fault; neither live I/O nor native authority
    // follows from this predicate. Provider retirement precedes engine Stop.
    internal static bool CanBorrowStorage(State* s, ulong expectedEpoch, ulong address, ulong bytes) =>
        Live(s) && s->Mounted && s->Input.Epoch == expectedEpoch && Caller(s, address, bytes);
    // Retained provider cleanup additionally authenticates its original block
    // lifetime. Dirty/fault/stopped backing remains owned; a restarted owner at
    // the same address cannot authorize cleanup. No live-I/O/copy authority.
    internal static bool CanRetainStorage(State* s, ulong expectedEpoch, ulong address, ulong bytes) =>
        CanBorrowStorage(s, expectedEpoch, address, bytes) && DeviceIdentity(s);
    // Stronger live provider admission, separate from retained teardown extents.
    // Clean pending/held reads are allowed. This checks RAM ownership only;
    // hardware/configuration and final copy are checked by the actual operation.
    internal static bool CanAdmitStorage(State* s, ulong expectedEpoch)
    {
        if (!Bound(s) || !s->Mounted || s->Input.Epoch != expectedEpoch || s->DirtyValue || s->DeviceFault ||
            s->Phase is not (Life.Idle or Life.Working or Life.Ready) || !DeviceCurrent(s) ||
            !Block.CanAdmitStorage(s->Input.Device, s->DeviceEpoch)) return false;
        Extent* all = stackalloc Extent[14]; return RetainedStorage(s, all);
    }
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
        Extent* all = stackalloc Extent[14];
        if (!RetainedStorage(s, all)) return false;
        for (int i = 0; i < 14; i++) if (Overlap(address, bytes, all[i].Start, all[i].Bytes)) return false;
        return OutsideDevice(s->Input.Device, address, bytes);
    }
    // Revalidate retained inputs, not only the proposed external interval.
    // An observer changed into engine backing must reject all new borrows.
    // No running-device predicate: fault/retired cleanup still retains extents.
    private static bool RetainedStorage(State* s, Extent* all)
    {
        Extents(s, s->Input, all);
        for (int i = 0; i < 14; i++)
        {
            if (!Storage(all[i].Start, all[i].Bytes, 8) || !OutsideDevice(s->Input.Device, all[i].Start, all[i].Bytes)) return false;
            for (int j = 0; j < i; j++) if (Overlap(all[i].Start, all[i].Bytes, all[j].Start, all[j].Bytes)) return false;
        }
        return true;
    }
    private static bool OutsideDevice(Block.State* d, ulong address, ulong bytes)
    {
        if (!Storage((ulong)d, (ulong)sizeof(Block.State), 8) || d->Self != (ulong)d) return false;
        var a = d->Input; DmaBufferPool* p = a.Pool;
        if (!Storage((ulong)p, (ulong)sizeof(DmaBufferPool), 8) || p->Self != (ulong)p || p->Epoch != d->PoolEpoch || p->Count is < 1 or > 64 ||
            !Storage((ulong)p->Slots, p->Count * (ulong)sizeof(DmaBufferSlot), 8) || !p->Window.IsInitialized || p->Window.ByteLength != p->Count * 4096UL ||
            !Storage(p->Window.CpuVirtualBase, p->Window.ByteLength, 1) || !a.Common.IsValid || !a.Notify.IsValid || !a.Device.IsValid ||
            !Storage(a.Common.VirtualStart, a.Common.Bytes, 1) || !Storage(a.Notify.VirtualStart, a.Notify.Bytes, 1) || !Storage(a.Device.VirtualStart, a.Device.Bytes, 1) ||
            a.Observation != null && !Storage((ulong)a.Observation, (ulong)sizeof(Block.Observation), 8)) return false;
        return !Overlap(address, bytes, (ulong)d, (ulong)sizeof(Block.State)) && !Overlap(address, bytes, (ulong)p, (ulong)sizeof(DmaBufferPool)) &&
            !Overlap(address, bytes, (ulong)p->Slots, p->Count * (ulong)sizeof(DmaBufferSlot)) && !Overlap(address, bytes, p->Window.CpuVirtualBase, p->Window.ByteLength) &&
            !Overlap(address, bytes, a.Common.VirtualStart, a.Common.Bytes) && !Overlap(address, bytes, a.Notify.VirtualStart, a.Notify.Bytes) && !Overlap(address, bytes, a.Device.VirtualStart, a.Device.Bytes) &&
            (a.Observation == null || !Overlap(address, bytes, (ulong)a.Observation, (ulong)sizeof(Block.Observation)));
    }
    private static bool Storage(ulong start, ulong bytes, ulong alignment) => start >= 4096 && (start & (alignment - 1)) == 0 && bytes != 0 && start < 0x0000800000000000UL && bytes <= 0x0000800000000000UL - start;
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => n != 0 && m != 0 && a < b + m && b < a + n;
}
