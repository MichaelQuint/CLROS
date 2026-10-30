using OS.Drivers;
using OS.Std.Binary;
using OS.Std.Time;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Port = OS.Storage.WritableBlockReadPort;
#endif

namespace OS.Storage;

/// <summary>Confined immutable FAT32 namespace over an exclusively borrowed
/// continuing block owner. One serialized supervisor caller pumps bounded steps;
/// no method spins for disk I/O or retains a user destination. All mount, slot,
/// cache and scratch storage is caller-owned, mapped ordinary RAM without hidden
/// aliases, and remains exclusive/stable until Stop succeeds. This supplies no
/// Linux descriptors, public directory browsing, writes or device mapping proof.</summary>
public static unsafe class ReadOnlyWebRootFileSystem
{
    public const uint PathBytes = 256, MaximumRangeBytes = 65536, MaximumClusters = 4096;
    public const uint MaximumSlots = 8, MaximumCacheSlots = 8;
    public const uint MaximumImmutableDirectoryCacheSlots = 5;
    /// <summary>Maximum cached cluster visits in one selected open-chain turn.
    /// Each visit may scan at most4096 prior clusters. Stop at the first real
    /// I/O transition; this never polls a newly submitted device request.</summary>
    public const uint MaximumCachedChainSteps = 64;
    /// <summary>Maximum physical entries examined by one internal lookup turn
    /// within an already loaded512-byte directory sector. Every entry still
    /// consumes the cumulative traversal budget and VFAT/collision validation.
    /// Public directory enumeration retains its single pending entry contract.</summary>
    public const uint MaximumLoadedDirectorySteps = 16;
    private const ulong AddressLimit = 0x0000800000000000UL;
    private const byte Free = 0, Working = 1, ReadyPhase = 2, Failed = 3, Cancelling = 4;
    private const byte Mounting = 1, Mounted = 2, Stopping = 3, Retired = 4, Faulted = 5;
    private const byte MountKind = 1, OpenKind = 2, ReadKind = 3, ScanKind = 4;
    private const byte BpbIo = 1, DirectoryIo = 2, Fat0Io = 3, Fat1Io = 4;
    // Native diagnostics inspect this discriminator only under the original
    // mount/operation generation; it never grants progress or copy authority.
    internal const byte DataIo = 5;

    public enum Status : byte
    {
        Uninitialized, Success, Pending, Ready, Entry, EndOfDirectory, EndOfFile,
        Busy, Full, InvalidAccess, InvalidState, InvalidStorage, InvalidPath,
        OutsideRoot, UnsupportedEncoding, UnsupportedGeometry, StaleToken,
        GenerationExhausted, NotFound, NotDirectory, IsDirectory, Ambiguous,
        InvalidOffset, ShortBuffer, OutputFull, TraversalBudgetExceeded,
        CorruptDirectory, CorruptFile, ChainLoop, Truncated, Overlong, BadCluster,
        FreeCluster, ReservedCluster, InvalidCluster, InvalidBpb, MirrorMismatch, DeviceFailed, Cancelled
    }

    /// <summary>Fixed backing for one reusable operation. Path/sector/name need
    /// 256/512/260*2 bytes. History has1..4096 uints; range has1..65536 bytes.
    /// Buffers never alias each other, any slot, cache, or the block owner.</summary>
    public struct Scratch
    {
        public byte* Path, Sector, Range;
        public ushort* Name;
        public uint* History;
        public uint HistoryCapacity, RangeCapacity;
    }

    /// <summary>One paired FAT-sector cache entry. Data is512 bytes. A slot is
    /// invalid while filling and becomes valid only after both FAT copies match.
    /// The epoch and both absolute LBAs identify its complete validation scope.</summary>
    public struct CacheSlot
    {
        public byte* Data;
        internal ulong Epoch, Lba0, Lba1, OperationGeneration;
        internal uint OperationIndex;
        internal byte Phase;
    }

    private struct ImmutableDirectoryRecord
    {
        internal ulong MountOwner, MountEpoch, DeviceEpoch, PoolEpoch, Lba;
        internal ulong Phase; // Free until completed copy; ReadyPhase publishes last.
    }

    /// <summary>Caller-owned handle slot and immutable checked cluster map.
    /// Supply1..4096 uints before Start; all other fields start zero. An active
    /// handle's map is private until Close. Directory handles need no data map.</summary>
    public struct FileHandle
    {
        public uint* Clusters;
        public uint ClusterCapacity;
        internal ulong Self, Epoch, Generation;
        internal Metadata Info;
        internal uint ClusterCount, References;
        internal byte Phase;
    }

    /// <summary>Resumable physical directory position and VFAT sequence. Only
    /// the enclosing operation owns its scratch/history. Pending metadata/name
    /// stays unpublished across OutputFull; work and visits never reset on retry.</summary>
    public struct DirectoryCursor
    {
        internal uint Cluster, SectorInCluster, EntryInSector, Visits;
        internal LiveVfatState Vfat;
        internal Metadata Pending;
        internal ushort NameLength;
        internal bool Visited, SectorReady, HasPending, Ended;
    }

    /// <summary>Reusable noncopyable operation slot. Supply Scratch before
    /// Start, leave every other field zero, and never change it while mounted.
    /// Failed operations remain owned until Cancel; successful copy consumes
    /// open/read results. A directory operation ends or is explicitly cancelled.</summary>
    public struct Operation
    {
        public Scratch Storage;
        internal ulong Self, Epoch, Generation;
        internal DirectoryCursor Cursor;
        internal Metadata Candidate;
        internal ulong Offset;
        internal uint Index, FileIndex, Component, ComponentLength, PathLength, Work;
        internal uint Wanted, Filled, FatCluster, FatSlot, RequiredClusters, MatchCount;
        internal byte Phase, Kind, Waiting, FatStage;
        internal bool CandidateExact, HaveCandidate, LookupDone;
        internal Status Failure;
    }

    /// <summary>Borrowed immutable admission. The block owner is already idle
    /// and running; GPT authority supplies the ESP bounds. Epoch is unique for
    /// this mount. Supply2..8 operation and handle slots,1..8 cache slots, and a
    /// positive cumulative physical-directory-entry budget per operation.
    /// Scratch capacities remain independent of advertised volume size.</summary>
    public struct Admission
    {
        public Block.State* Device;
        public ulong Epoch, PartitionFirst, PartitionLast;
        public Operation* Operations;
        public FileHandle* Files;
        public CacheSlot* Cache;
        public uint OperationCount, FileCount, CacheCount, DirectoryEntryBudget;
    }

#if !NOSTDLIB_EFI || M16_027_PROBE
    /// <summary>Borrow one exclusively retained read port on the original
    /// writable device. Exact readonly bounds/capacity come from authenticated
    /// port facts, never a caller-selected subset or fabricated readonly owner.
    /// Common scratch/slot lifetime is identical to Admission. The outer owner
    /// retains this port until FS Stop, then stops port before releasing C/B.
    /// Busy is retryable without reservation, output or result consumption.</summary>
    public struct BorrowedAdmission
    {
        public Port.State* ReadPort;
        public ulong PortEpoch, Epoch;
        public Operation* Operations;
        public FileHandle* Files;
        public CacheSlot* Cache;
        public uint OperationCount, FileCount, CacheCount, DirectoryEntryBudget;
    }
#endif

    /// <summary>Stable noncopyable owner. Failed block activity revokes cache
    /// publication and all new work. Preserve admission/backing until Stop
    /// succeeds; failed device cleanup remains the platform owner's duty.</summary>
    public struct Mount
    {
        internal ulong Self, DeviceEpoch, PoolEpoch;
        internal Admission Input;
        internal LiveFat32Volume Volume;
        internal Metadata Root;
        internal Block.ReadToken IoToken;
        internal ulong IoGeneration;
        internal uint IoIndex, NextVictim;
        internal byte Phase, DataReadSectors;
        internal bool IoActive;
        internal Status Failure;
        internal ulong Reads, Hits, Misses;
        internal byte* ImmutableDirectoryCacheBacking;
        internal uint ImmutableDirectoryCacheSlots;
#if !NOSTDLIB_EFI || M16_027_PROBE
        internal Port.State* ReadPort;
        internal Port.Inspection PortBinding;
        internal Port.Token PortIo;
#endif
        public readonly bool Ready => Phase == Mounted;
        public readonly bool IsRetired => Phase == Retired;
        public readonly bool RequiresCleanup => Self != 0 && Phase != Retired;
        public readonly Status LastFailure => Failure;
        public readonly ulong SectorReads => Reads;
        public readonly ulong CacheHits => Hits;
        public readonly ulong CacheMisses => Misses;
    }

    public readonly struct OperationToken
    {
        internal readonly ulong Owner, Epoch, Generation;
        internal readonly uint Index;
        internal OperationToken(ulong owner, ulong epoch, ulong generation, uint index)
        { Owner = owner; Epoch = epoch; Generation = generation; Index = index; }
        public bool IsInitialized => Owner != 0 && Epoch != 0 && Generation != 0;
    }
    public readonly struct FileToken
    {
        internal readonly ulong Owner, Epoch, Generation;
        internal readonly uint Index;
        internal FileToken(ulong owner, ulong epoch, ulong generation, uint index)
        { Owner = owner; Epoch = epoch; Generation = generation; Index = index; }
        public bool IsInitialized => Owner != 0 && Epoch != 0 && Generation != 0;
    }

    /// <summary>Stable physical short-entry identity and declared FAT UTC time.
    /// The fixture's UTC policy is an admission assumption, not a timezone guess.
    /// ModifiedSeconds is zero for an all-zero FAT date/time (unspecified);
    /// nonzero FAT timestamps must be valid Gregorian UTC dates from 1980 onward.
    /// Attributes distinguish regular files/directories; length is bytes.</summary>
    public struct Metadata
    {
        public ulong Inode;
        public long ModifiedSeconds;
        public uint FirstCluster, Length;
        public ushort NameBytes;
        public byte Attributes;
        public readonly bool IsDirectory => (Attributes & 0x10) != 0;
    }
    public struct OpenResult { public FileToken File; public Metadata Info; }
    /// <summary>Live handle metadata. Regular allocation is the fully checked
    /// cluster map multiplied by sectors per cluster. Directory allocation is
    /// unknown because opening a directory does not preload its complete chain.</summary>
    public struct FileInspection { public Metadata Info; public ulong AllocatedBlocks; public bool AllocationKnown; }

    /// <summary>Borrow a fresh metadata snapshot without I/O. Authenticate the
    /// exact live mount/device and file generation before writing the disjoint,
    /// aligned caller-owned output. Reset/stale/alias failures change nothing.</summary>
    public static Status Inspect(Mount* m, FileToken file, FileInspection* result)
    {
        Status live = RequireLive(m); if (live != Status.Success) return live;
        if (!External(m, (ulong)result, (ulong)sizeof(FileInspection), 8)) return Status.InvalidStorage;
        if (!File(m, file, out FileHandle* f)) return Status.StaleToken;
        *result = new() { Info = f->Info, AllocationKnown = !f->Info.IsDirectory,
            AllocatedBlocks = f->Info.IsDirectory ? 0 : (ulong)f->ClusterCount * m->Volume.SectorsPerCluster };
        return Status.Success;
    }

    /// <summary>Admit all storage before mutation, then start asynchronous BPB
    /// and /app/wwwroot resolution using operation0. Pending requires PumpMount.
    /// No supplied buffer is device DMA; block Copy borrows sector scratch only
    /// through a pump call. Invalid admission has no effects.</summary>
    public static Status Start(Mount* m, in Admission input) => Start(m, input, 1);

    /// <summary>Opt into at most four contiguous data sectors per owned request.
    /// Metadata, partial sectors and borrowed backends retain single-sector I/O.
    /// Uses existing caller range storage; invalid bounds leave admission untouched.</summary>
    public static Status Start(Mount* m, in Admission input, uint maximumDataReadSectors)
    {
        Admission a = input;
        if (maximumDataReadSectors is 0 or > 4 || !Admit(m, a)) return Status.InvalidAccess;
        m->Self = (ulong)m; m->Input = a; m->DeviceEpoch = a.Device->Input.Epoch;
        m->DataReadSectors = (byte)maximumDataReadSectors;
        m->PoolEpoch = a.Device->PoolEpoch; return Initialize(m, a);
    }

    /// <summary>Exact contiguous ordinary-RAM backing for1..5 protected directory
    /// sectors. Zero means an unsupported slot count or record size; no memory is accessed.</summary>
    public static uint ImmutableDirectoryCacheBytes(uint slots) =>
        slots is 0 or > MaximumImmutableDirectoryCacheSlots || sizeof(ImmutableDirectoryRecord) != 48 ? 0 : slots * (48 + 512);

    /// <summary>Opt in to completed-requested-sector reuse on immutable media.
    /// Call once after Start and before any mount I/O. The caller exclusively
    /// retains the aligned disjoint backing and guarantees unchanged media until
    /// Stop retires this mount. Borrowed writable ports are unsupported. Only
    /// ordinary mounted directory reads fill protected non-evicting slots; all
    /// original parsing and work checks still run. Hits cannot observe a later
    /// hypothetical read fault or unsampled external media mutation. Invalid
    /// configuration changes no backing or mount state; no I/O or allocation runs.</summary>
    public static Status EnableImmutableDirectoryCache(Mount* m, byte* backing, uint bytes, uint slots)
    {
        uint required = ImmutableDirectoryCacheBytes(slots);
        if (required == 0 || bytes != required || !Bound(m) || m->Phase != Mounting ||
            m->Reads != 0 || m->IoActive || m->ImmutableDirectoryCacheBacking != null ||
            m->ImmutableDirectoryCacheSlots != 0) return Status.InvalidAccess;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null) return Status.InvalidAccess;
#endif
        if (!DeviceBound(m) || !m->Input.Device->Running || m->Input.Device->ReadPending ||
            m->Input.Device->ReadReady) return Status.InvalidState;
        if (!External(m, (ulong)backing, bytes, 8)) return Status.InvalidStorage;
        for (uint i = 0; i < slots; i++) ((ImmutableDirectoryRecord*)backing)[i] = default;
        m->ImmutableDirectoryCacheBacking = backing; m->ImmutableDirectoryCacheSlots = slots;
        return Status.Success;
    }

#if !NOSTDLIB_EFI || M16_027_PROBE
    /// <summary>Admit the genuine borrowed backend without queue/device setup.
    /// Busy leaves every byte unchanged. No user buffer survives a call and no
    /// filesystem operation can poll another client's physical request.</summary>
    public static Status StartBorrowed(Mount* m, in BorrowedAdmission input)
    {
        BorrowedAdmission b = input;
        if (!Storage((ulong)m, (ulong)sizeof(Mount), 8) || !Storage((ulong)b.ReadPort, (ulong)sizeof(Port.State), 8)) return Status.InvalidAccess;
        Port.Inspection facts = default; var status = Port.Inspect(b.ReadPort, b.PortEpoch, &facts);
        if (status == Port.Status.Busy) return Status.Busy;
        if (status != Port.Status.Success) return Status.DeviceFailed;
        if (facts.Owner != (ulong)b.ReadPort || facts.Epoch != b.PortEpoch) return Status.DeviceFailed;
        if (facts.RequestActive) return Status.Busy;
        Admission a = new() { Epoch = b.Epoch, PartitionFirst = facts.FirstLba, PartitionLast = facts.LastLba,
            Operations = b.Operations, Files = b.Files, Cache = b.Cache, OperationCount = b.OperationCount,
            FileCount = b.FileCount, CacheCount = b.CacheCount, DirectoryEntryBudget = b.DirectoryEntryBudget };
        if (!AdmitSlots(m, a) || !AdmissionOutside(m, a, b.ReadPort, b.PortEpoch)) return Status.InvalidAccess;
        m->Self = (ulong)m; m->Input = a; m->ReadPort = b.ReadPort; m->PortBinding = facts; m->DeviceEpoch = facts.DeviceEpoch;
        return Initialize(m, a);
    }
#endif

    private static Status Initialize(Mount* m, in Admission a)
    {
        m->Phase = Mounting; m->Failure = Status.Success;
        for (uint i = 0; i < a.OperationCount; i++)
        { a.Operations[i].Self = (ulong)(a.Operations + i); a.Operations[i].Epoch = a.Epoch; a.Operations[i].Index = i; }
        for (uint i = 0; i < a.FileCount; i++) { a.Files[i].Self = (ulong)(a.Files + i); a.Files[i].Epoch = a.Epoch; }
        Operation* o = a.Operations; Begin(o, MountKind); o->PathLength = 12;
        const string root = "/app/wwwroot";
        for (uint i = 0; i < 12; i++) o->Storage.Path[i] = (byte)root[(int)i];
        return Status.Pending;
    }

    /// <summary>Advance at most one real sector completion or a bounded parser
    /// turn over one loaded sector. Return Ready only after BPB and both root
    /// components are validated; never complete and parse I/O in the same call.</summary>
    public static Status PumpMount(Mount* m)
    {
        if (!Bound(m)) return Status.InvalidState;
        if (m->Phase == Mounted)
        {
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (m->ReadPort != null) { Status status = BackendStatus(m); if (status != Status.Success) return status; }
#endif
            return Status.Ready;
        }
        if (m->Phase != Mounting) return Status.InvalidState;
        return Pump(m, m->Input.Operations);
    }

    /// <summary>Copy one absolute ASCII path below the exact admitted root and
    /// reserve independent operation/handle slots. Output is borrowed mapped,
    /// aligned exclusive ordinary RAM; reject aliases before any output write.
    /// Invalid path/storage publishes nothing. Poll then CopyOpen consumes success.</summary>
    public static Status BeginOpen(Mount* m, ReadOnlySpan<byte> path, OperationToken* token)
    {
        Status live = RequireLive(m); if (live != Status.Success) return live;
        if (!External(m, (ulong)token, (ulong)sizeof(OperationToken), 8)) return Status.InvalidStorage;
        if (path.Length == 0) return Status.InvalidPath;
        fixed (byte* p = path)
            if (!External(m, (ulong)p, (ulong)path.Length, 1) || Overlap((ulong)p, (ulong)path.Length, (ulong)token, (ulong)sizeof(OperationToken))) return Status.InvalidStorage;
        Status valid = PathPolicy(path); if (valid != Status.Success) return valid;
        Status reserved = ReserveOperation(m, OpenKind, out Operation* o); if (reserved != Status.Success) return reserved;
        uint file = 0;
        for (; file < m->Input.FileCount; file++) if (m->Input.Files[file].Phase == Free && m->Input.Files[file].Generation != ~0UL) break;
        if (file == m->Input.FileCount)
        {
            bool exhausted = false; for (uint i = 0; i < m->Input.FileCount; i++) if (m->Input.Files[i].Phase == Free) exhausted = true;
            Release(m, o); return exhausted ? Status.GenerationExhausted : Status.Full;
        }
        FileHandle* f = m->Input.Files + file; f->Generation++; f->Phase = Working; f->ClusterCount = 0;
        o->FileIndex = file; o->PathLength = (uint)path.Length;
        for (int i = 0; i < path.Length; i++) o->Storage.Path[i] = path[i];
        if (path.Length == 12) { o->Candidate = m->Root; o->LookupDone = true; }
        else { o->Component = 13; InitCursor(o, m->Root.FirstCluster); Component(o); }
        *token = OpToken(m, o); return Status.Pending;
    }

    /// <summary>Start a bounded offset read. Only this requested range, capped
    /// by the operation's <=64KiB scratch and EOF, is staged for atomic output.
    /// Handle cluster metadata is already validated; data is read on demand.
    /// No destination pointer is retained. Offset greater than EOF is rejected.</summary>
    public static Status BeginRead(Mount* m, FileToken file, ulong offset, uint bytes, OperationToken* token)
    {
        Status live = RequireLive(m); if (live != Status.Success) return live;
        if (!External(m, (ulong)token, (ulong)sizeof(OperationToken), 8)) return Status.InvalidStorage;
        if (!File(m, file, out FileHandle* f)) return Status.StaleToken;
        if (f->Info.IsDirectory) return Status.IsDirectory;
        if (offset > f->Info.Length || bytes > MaximumRangeBytes || offset > ~0UL - bytes) return Status.InvalidOffset;
        Status reserved = ReserveOperation(m, ReadKind, out Operation* o); if (reserved != Status.Success) return reserved;
        uint wanted = (uint)((ulong)bytes < f->Info.Length - offset ? bytes : f->Info.Length - offset);
        if (wanted > o->Storage.RangeCapacity) { Release(m, o); return Status.ShortBuffer; }
        o->FileIndex = file.Index; o->Offset = offset; o->Wanted = wanted; f->References++;
        if (wanted == 0) o->Phase = ReadyPhase;
        *token = OpToken(m, o); return Status.Pending;
    }

    /// <summary>Internal resumable directory scan, not a public filesystem
    /// listing capability. Each operation owns separate position/VFAT/history.
    /// Entry output is explicitly consumed with CopyEntry; short output does
    /// not consume an entry or reset work. Cancel or EndOfDirectory releases it.</summary>
    public static Status BeginDirectory(Mount* m, FileToken directory, OperationToken* token)
    {
        Status live = RequireLive(m); if (live != Status.Success) return live;
        if (!External(m, (ulong)token, (ulong)sizeof(OperationToken), 8)) return Status.InvalidStorage;
        if (!File(m, directory, out FileHandle* f)) return Status.StaleToken;
        if (!f->Info.IsDirectory) return Status.NotDirectory;
        Status reserved = ReserveOperation(m, ScanKind, out Operation* o); if (reserved != Status.Success) return reserved;
        o->FileIndex = directory.Index; f->References++; InitCursor(o, f->Info.FirstCluster);
        *token = OpToken(m, o); return Status.Pending;
    }

    /// <summary>One cooperative bounded step. Busy means another operation owns
    /// the single block lease. Ready/Entry are retained, not copies. Errors remain
    /// owned until Cancel, so callers can distinguish corruption, budget and I/O
    /// failure from EOF. No syscall thread or global IRQ-off disk spin is supplied.</summary>
    public static Status Poll(Mount* m, OperationToken token)
    {
        if (!Bound(m)) return Status.InvalidState;
        if (m->Phase is not (Mounted or Stopping)) return Status.InvalidState;
        if (!Op(m, token, out Operation* o)) return Status.StaleToken;
        return Pump(m, o);
    }

    /// <summary>Publish a fully validated open result and consume its operation.
    /// Output must be aligned, mapped exclusive ordinary RAM outside all retained
    /// storage. Failure leaves output untouched and preserves a ready result.</summary>
    public static Status CopyOpen(Mount* m, OperationToken token, OpenResult* result)
    {
        Status live = RequireLive(m); if (live != Status.Success) return live;
        if (!External(m, (ulong)result, (ulong)sizeof(OpenResult), 8)) return Status.InvalidStorage;
        if (!Op(m, token, out Operation* o)) return Status.StaleToken;
        if (o->Kind != OpenKind || o->Phase != ReadyPhase) return o->Phase == Failed ? o->Failure : Status.Pending;
        FileHandle* f = m->Input.Files + o->FileIndex; f->Phase = ReadyPhase;
        *result = new() { File = new((ulong)m, m->Input.Epoch, f->Generation, o->FileIndex), Info = f->Info };
        o->FileIndex = ~0U; Release(m, o); return Status.Success;
    }

    /// <summary>Copy only a complete validated requested range, then consume
    /// the operation. Short output preserves Ready. The byte-count pointer and
    /// destination are separate mapped ordinary RAM and alias-checked before
    /// any write. Failed I/O publishes no partial range and never becomes EOF.</summary>
    public static Status CopyRead(Mount* m, OperationToken token, Span<byte> destination, uint* bytes)
    {
        Status live = RequireLive(m); if (live != Status.Success) return live;
        if (!External(m, (ulong)bytes, 4, 4)) return Status.InvalidStorage;
        if (!Op(m, token, out Operation* o)) return Status.StaleToken;
        if (o->Kind != ReadKind || o->Phase != ReadyPhase) return o->Phase == Failed ? o->Failure : Status.Pending;
        if (destination.Length < o->Wanted) return Status.ShortBuffer;
        fixed (byte* p = destination)
        {
            if (o->Wanted != 0 && (!External(m, (ulong)p, o->Wanted, 1) || Overlap((ulong)p, o->Wanted, (ulong)bytes, 4))) return Status.InvalidStorage;
            for (uint i = 0; i < o->Wanted; i++) p[i] = o->Storage.Range[i];
        }
        *bytes = o->Wanted; bool eof = o->Wanted == 0 && o->Offset == m->Input.Files[o->FileIndex].Info.Length;
        Release(m, o); return eof ? Status.EndOfFile : Status.Success;
    }

    /// <summary>Publish the pending ASCII canonical name (including a trailing
    /// NUL) and metadata, then advance. OutputFull preserves the pending entry,
    /// name, physical position and cumulative budgets for a later caller batch.</summary>
    public static Status CopyEntry(Mount* m, OperationToken token, Metadata* result, Span<byte> name)
    {
        Status live = RequireLive(m); if (live != Status.Success) return live;
        if (!External(m, (ulong)result, (ulong)sizeof(Metadata), 8)) return Status.InvalidStorage;
        if (!Op(m, token, out Operation* o)) return Status.StaleToken;
        if (o->Kind != ScanKind || !o->Cursor.HasPending) return o->Phase == Failed ? o->Failure : Status.Pending;
        uint count = (uint)o->Cursor.NameLength + 1;
        if (name.Length < count) return Status.OutputFull;
        fixed (byte* p = name)
        {
            if (!External(m, (ulong)p, count, 1) || Overlap((ulong)p, count, (ulong)result, (ulong)sizeof(Metadata))) return Status.InvalidStorage;
            for (uint i = 0; i + 1 < count; i++) p[i] = (byte)o->Storage.Name[i];
            p[count - 1] = 0;
        }
        *result = o->Cursor.Pending; o->Cursor.HasPending = false; return Status.Success;
    }

    /// <summary>Revoke an operation's publication permission immediately.
    /// Outstanding block work must still drain through Poll; Pending retains
    /// scratch. An operation without DMA releases immediately and becomes stale.</summary>
    public static Status Cancel(Mount* m, OperationToken token)
    {
        if (!Bound(m) || m->Phase == Retired) return Status.InvalidState;
        if (!Op(m, token, out Operation* o)) return Status.StaleToken;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null && !RetainedBorrowed(m)) return Status.DeviceFailed;
#endif
        o->Phase = Cancelling;
        if (m->IoActive && m->IoIndex == o->Index && m->IoGeneration == o->Generation)
        {
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (m->ReadPort != null)
            {
                var status = Port.Cancel(m->ReadPort, m->PortIo);
                if (status == Port.Status.Cancelled) { m->IoActive = false; o->Waiting = 0; Release(m, o); return Status.Cancelled; }
                return status == Port.Status.Pending ? Status.Pending : DeviceFault(m);
            }
#endif
            bool ready = m->Input.Device->ReadReady;
            if (Block.Cancel(m->Input.Device, m->IoToken) != Block.Status.Success) return DeviceFault(m);
            if (ready) { m->IoActive = false; o->Waiting = 0; Release(m, o); return Status.Cancelled; }
            return Status.Pending;
        }
        Release(m, o); return Status.Cancelled;
    }

    /// <summary>Close an independent handle. Active operations keep it Busy;
    /// stale/copied tokens cannot close a reused slot. An authenticated borrowed
    /// backend Busy turn still permits this ONLY local unreferenced metadata
    /// release: no I/O/cancel/cache or writer state is changed, and no current
    /// volume/copy authority is granted. Legacy readonly liveness is unchanged.</summary>
    public static Status Close(Mount* m, FileToken token)
    {
        Status live = RequireLive(m);
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (live != Status.Success && !(live == Status.Busy && m->ReadPort != null)) return live;
#else
        if (live != Status.Success) return live;
#endif
        if (!File(m, token, out FileHandle* f)) return Status.StaleToken;
        if (f->References != 0) return Status.Busy;
        f->Phase = Free; f->ClusterCount = 0; return Status.Success;
    }

    /// <summary>Revoke all cache/handle publication and drain at most one block
    /// observation per call. This owner never resets a borrowed device. If it
    /// faults, preserve backing until the platform has actually stopped it;
    /// only its retired receipt permits final local release. Retired is idempotent.</summary>
    public static Status Stop(Mount* m)
    {
        if (!Bound(m)) return Status.InvalidState;
        if (m->Phase == Retired) return Status.Success;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null) return StopBorrowed(m);
#endif
        m->Phase = Stopping; InvalidateCache(m);
        if (!DeviceBound(m) || (!m->Input.Device->Running && !m->Input.Device->Retired)) return Status.DeviceFailed;
        if (m->IoActive)
        {
            if (!DeviceBound(m)) return Status.DeviceFailed;
            if (m->Input.Device->Retired) m->IoActive = false;
            else
            {
                Operation* o = m->Input.Operations + m->IoIndex; o->Phase = Cancelling;
                bool ready = m->Input.Device->ReadReady;
                if (Block.Cancel(m->Input.Device, m->IoToken) != Block.Status.Success) return Status.DeviceFailed;
                if (ready) { m->IoActive = false; o->Waiting = 0; Release(m, o); }
                else { Status progress = CompleteIo(m, o); if (progress is not (Status.Cancelled or Status.Success)) return progress; }
            }
        }
        return ReleaseAll(m);
    }

    private static Status ReleaseAll(Mount* m)
    {
        for (uint i = 0; i < m->Input.OperationCount; i++) if (m->Input.Operations[i].Phase != Free) Release(m, m->Input.Operations + i);
        for (uint i = 0; i < m->Input.FileCount; i++) { m->Input.Files[i].Phase = Free; m->Input.Files[i].ClusterCount = 0; }
        m->Phase = Retired; return Status.Success;
    }

#if !NOSTDLIB_EFI || M16_027_PROBE
    private static Status StopBorrowed(Mount* m)
    {
        if (!Shape(m->Input) || !Storage((ulong)m->ReadPort, (ulong)sizeof(Port.State), 8)) return Status.DeviceFailed;
        Port.Inspection facts = default; var inspection = Port.Inspect(m->ReadPort, m->PortBinding.Epoch, &facts);
        if (inspection == Port.Status.Retired)
        {
            // Only local retirement is used: no released C/B pointer is read,
            // no bytes are published and no retained-backend authority follows.
            Extent* own = stackalloc Extent[68]; uint count = Extents(m, m->Input, own);
            if (!Disjoint(own, count)) return Status.DeviceFailed;
            for (uint i = 0; i < count; i++) if (Overlap(own[i].Start, own[i].Bytes, (ulong)m->ReadPort, (ulong)sizeof(Port.State))) return Status.DeviceFailed;
            m->Phase = Stopping; InvalidateCache(m);
            m->IoActive = false; return ReleaseAll(m);
        }
        if (!RetainedBorrowed(m)) return Status.DeviceFailed;
        m->Phase = Stopping; InvalidateCache(m);
        if (m->IoActive)
        {
            Operation* o = m->Input.Operations + m->IoIndex; o->Phase = Cancelling;
            var cancelled = Port.Cancel(m->ReadPort, m->PortIo);
            if (cancelled == Port.Status.Cancelled) { m->IoActive = false; o->Waiting = 0; Release(m, o); }
            else if (cancelled == Port.Status.Pending)
            { Status progress = CompleteIo(m, o); if (progress != Status.Cancelled) return progress; }
            else return Status.DeviceFailed;
        }
        return ReleaseAll(m);
    }
#endif

    private static Status Pump(Mount* m, Operation* o)
    {
        if (o->Phase == Failed) return o->Failure;
        Status available = BackendStatus(m);
        if (available == Status.Busy) return Status.Busy;
        if (available != Status.Success) return Fail(m, o, DeviceFault(m));
        if (o->Phase == ReadyPhase) return Status.Ready;
        if (o->Cursor.HasPending && o->Kind == ScanKind) return Status.Entry;
        if (o->Waiting != 0) return CompleteIo(m, o);
        if (o->Phase == Cancelling) { Release(m, o); return Status.Cancelled; }
        if (o->Kind == MountKind && !m->Volume.Succeeded) return ReadSector(m, o, m->Input.PartitionFirst, BpbIo);
        if (o->Kind is MountKind or OpenKind)
        {
            if (!o->LookupDone) return LookupStep(m, o);
            if (o->Kind == MountKind)
            {
                if (!o->Candidate.IsDirectory) return Fail(m, o, Status.NotDirectory);
                m->Root = o->Candidate; m->Phase = Mounted; Release(m, o); return Status.Ready;
            }
            return FileChainStep(m, o);
        }
        if (o->Kind == ReadKind) return ReadStep(m, o);
        Status entry = DirectoryStep(m, o);
        if (entry == Status.EndOfDirectory) Release(m, o);
        return entry;
    }

    private static Status ReadSector(Mount* m, Operation* o, ulong lba, byte action, uint sectors = 1)
    {
        if (sectors is 0 or > 4 || (sectors != 1 && action != DataIo) ||
            lba < m->Input.PartitionFirst || lba > m->Input.PartitionLast ||
            sectors - 1 > m->Input.PartitionLast - lba) return Fail(m, o, Status.CorruptFile);
        if (m->IoActive) return Status.Busy;
        if (m->ImmutableDirectoryCacheBacking != null && action == DirectoryIo &&
            m->Phase == Mounted && o->Kind != MountKind)
        {
            for (uint i = 0; i < m->ImmutableDirectoryCacheSlots; i++)
            {
                var record = (ImmutableDirectoryRecord*)m->ImmutableDirectoryCacheBacking + i;
                if (record->Phase != ReadyPhase || record->MountOwner != (ulong)m ||
                    record->MountEpoch != m->Input.Epoch || record->DeviceEpoch != m->DeviceEpoch ||
                    record->PoolEpoch != m->PoolEpoch || record->Lba != lba) continue;
                byte* data = m->ImmutableDirectoryCacheBacking + m->ImmutableDirectoryCacheSlots * 48 + i * 512;
                for (uint n = 0; n < 512; n++) o->Storage.Sector[n] = data[n];
                o->Cursor.SectorReady = true; o->Cursor.EntryInSector = 0; return Status.Pending;
            }
        }
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
        {
            Port.Token borrowed = default; var submitted = Port.Submit(m->ReadPort, lba, &borrowed);
            if (submitted == Port.Status.Busy) return Status.Busy;
            if (submitted != Port.Status.Success) return Fail(m, o, DeviceFault(m));
            m->PortIo = borrowed; return Submitted(m, o, action);
        }
#endif
        Block.ReadToken token = default;
        var status = Block.Submit(m->Input.Device, lba, sectors, &token);
        if (status == Block.Status.Busy) return Status.Busy;
        if (status != Block.Status.Success) return Fail(m, o, DeviceFault(m));
        m->IoToken = token; return Submitted(m, o, action, sectors);
    }

    private static Status Submitted(Mount* m, Operation* o, byte action, uint sectors = 1)
    {
        m->IoIndex = o->Index; m->IoGeneration = o->Generation; m->IoActive = true;
        // Actions occupy the low nibble; the bounded length uses its spare bits.
        o->Waiting = (byte)(action | ((sectors - 1) << 4)); m->Reads += sectors; return Status.Pending;
    }

    private static Status CompleteIo(Mount* m, Operation* o)
    {
        if (!m->IoActive || m->IoIndex != o->Index || m->IoGeneration != o->Generation) return Fail(m, o, Status.InvalidState);
        byte action = (byte)(o->Waiting & 15);
        uint sectors = (uint)(o->Waiting >> 4) + 1, requestedBytes = sectors * 512;
        bool direct = sectors > 1;
        ulong actualRequestSector = 0;
        if (sectors > 4 || direct && (action != DataIo || o->Filled > o->Wanted ||
            requestedBytes > o->Wanted - o->Filled || o->Wanted > o->Storage.RangeCapacity ||
            ((o->Offset + o->Filled) & 511) != 0)) return Fail(m, o, Status.InvalidState);
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
        {
            Port.Completion value = default; var progress = Port.Poll(m->ReadPort, m->PortIo, &value);
            if (progress == Port.Status.Pending) return Status.Pending;
            if (progress == Port.Status.Cancelled && o->Phase == Cancelling)
            { m->IoActive = false; o->Waiting = 0; Release(m, o); return Status.Cancelled; }
            if (progress != Port.Status.Ready || value.AvailableBytes != 512 ||
                Port.Copy(m->ReadPort, m->PortIo, new Span<byte>(o->Storage.Sector, 512)) != Port.Status.Success)
                return Fail(m, o, DeviceFault(m));
        }
        else
#endif
        {
        Block.Completion completion = default;
        var status = Block.Poll(m->Input.Device, &completion);
        if (status == Block.Status.Pending) return Status.Pending;
        if (status == Block.Status.Cancelled && o->Phase == Cancelling)
        { m->IoActive = false; o->Waiting = 0; Release(m, o); return Status.Cancelled; }
        if (status != Block.Status.Ready || completion.Bytes != requestedBytes) return Fail(m, o, DeviceFault(m));
        if (Block.Copy(m->Input.Device, m->IoToken,
            new Span<byte>(direct ? o->Storage.Range + o->Filled : o->Storage.Sector, (int)requestedBytes)) != Block.Status.Success)
            return Fail(m, o, DeviceFault(m));
        actualRequestSector = completion.Sector;
        }
        m->IoActive = false; o->Waiting = 0;
        if (o->Phase == Cancelling) { Release(m, o); return Status.Cancelled; }
        if (action == BpbIo)
        {
            var result = Chain.ParseBpb(new Span<byte>(o->Storage.Sector, 512), BackendCapacity(m),
                m->Input.PartitionFirst, m->Input.PartitionLast, BlockReadResult.Success(1, 512), ref m->Volume);
            if (result != LiveFat32Status.Success) return Fail(m, o, Map(result));
            o->Component = 1; Component(o); InitCursor(o, m->Volume.RootCluster); return Status.Pending;
        }
        if (action == DirectoryIo)
        {
            if (m->ImmutableDirectoryCacheBacking != null && m->Phase == Mounted && o->Kind != MountKind)
            {
                // The sole physical request has completed and its accepted copy
                // belongs to this operation. No cache record exists in flight.
                for (uint i = 0; i < m->ImmutableDirectoryCacheSlots; i++)
                {
                    var record = (ImmutableDirectoryRecord*)m->ImmutableDirectoryCacheBacking + i;
                    if (record->Phase != Free) continue;
                    record->MountOwner = (ulong)m; record->MountEpoch = m->Input.Epoch;
                    record->DeviceEpoch = m->DeviceEpoch; record->PoolEpoch = m->PoolEpoch;
                    record->Lba = actualRequestSector;
                    byte* data = m->ImmutableDirectoryCacheBacking + m->ImmutableDirectoryCacheSlots * 48 + i * 512;
                    for (uint n = 0; n < 512; n++) data[n] = o->Storage.Sector[n];
                    record->Phase = ReadyPhase; break;
                }
            }
            o->Cursor.SectorReady = true; o->Cursor.EntryInSector = 0; return Status.Pending;
        }
        if (action == Fat0Io)
        {
            CacheSlot* slot = m->Input.Cache + o->FatSlot;
            for (uint i = 0; i < 512; i++) slot->Data[i] = o->Storage.Sector[i];
            o->FatStage = 2; return Status.Pending;
        }
        if (action == Fat1Io)
        {
            CacheSlot* slot = m->Input.Cache + o->FatSlot;
            for (uint i = 0; i < 512; i++) if (slot->Data[i] != o->Storage.Sector[i]) { slot->Phase = Free; return Fail(m, o, Status.MirrorMismatch); }
            // Keep the verified slot pinned until this operation consumes the
            // entry. Another operation must not evict it between pump calls.
            o->FatStage = 3; return Status.Pending;
        }
        if (action != DataIo) return Fail(m, o, Status.InvalidState);
        if (direct)
        {
            o->Filled += requestedBytes;
            if (o->Filled == o->Wanted) { o->Phase = ReadyPhase; return Status.Ready; }
            return Status.Pending;
        }
        uint skip = (uint)((o->Offset + o->Filled) & 511);
        uint count = 512 - skip; if (count > o->Wanted - o->Filled) count = o->Wanted - o->Filled;
        for (uint i = 0; i < count; i++) o->Storage.Range[o->Filled + i] = o->Storage.Sector[skip + i];
        o->Filled += count; if (o->Filled == o->Wanted) { o->Phase = ReadyPhase; return Status.Ready; }
        return Status.Pending;
    }

    private static Status ReadStep(Mount* m, Operation* o)
    {
        if (o->Filled == o->Wanted) { o->Phase = ReadyPhase; return Status.Ready; }
        FileHandle* f = m->Input.Files + o->FileIndex;
        ulong position = o->Offset + o->Filled, clusterBytes = (ulong)m->Volume.SectorsPerCluster * 512;
        uint map = (uint)(position / clusterBytes); if (map >= f->ClusterCount) return Fail(m, o, Status.CorruptFile);
        ulong lba = 0;
        if (Chain.ClusterFirstLba(m->Volume, f->Clusters[map], ref lba) != LiveFat32Status.Success) return Fail(m, o, Status.CorruptFile);
        lba += (position % clusterBytes) / 512;
        uint sectors = 1, maximum = m->DataReadSectors;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null) maximum = 1;
#endif
        // The complete chain was validated on open. Cross a cluster boundary
        // only when its retained mapping gives the next physical sector.
        if ((position & 511) == 0 && maximum > 1)
        {
            uint whole = (o->Wanted - o->Filled) / 512;
            if (maximum > whole) maximum = whole;
            while (sectors < maximum)
            {
                ulong nextPosition = position + sectors * 512UL;
                uint nextMap = (uint)(nextPosition / clusterBytes);
                ulong nextLba = 0;
                if (nextMap >= f->ClusterCount ||
                    Chain.ClusterFirstLba(m->Volume, f->Clusters[nextMap], ref nextLba) != LiveFat32Status.Success)
                    return Fail(m, o, Status.CorruptFile);
                nextLba += (nextPosition % clusterBytes) / 512;
                if (nextLba != lba + sectors) break;
                sectors++;
            }
        }
        return ReadSector(m, o, lba, DataIo, sectors);
    }

    private static Status FileChainStep(Mount* m, Operation* o)
    {
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null) return FileChainOne(m, o);
#endif
        FileHandle* f = m->Input.Files + o->FileIndex;
        Block.State* device = m->Input.Device;
        ulong epoch = m->Input.Epoch, generation = o->Generation, fileGeneration = f->Generation;
        ulong submitted = device->Submitted, completed = device->Completed, copied = device->Copied, cancelled = device->Cancelled;
        uint fileIndex = o->FileIndex, operationIndex = o->Index;
        bool batch = !m->IoActive && o->Waiting == 0;
        for (uint step = 0; ; step++)
        {
            uint previous = f->ClusterCount;
            Status status = FileChainOne(m, o);
            if (status != Status.Pending || !batch || step + 1 == MaximumCachedChainSteps ||
                o->Waiting != 0 || m->IoActive || f->ClusterCount <= previous ||
                device->Submitted != submitted || device->Completed != completed || device->Copied != copied || device->Cancelled != cancelled ||
                m->Input.Device != device || !Live(m) || m->Input.Epoch != epoch ||
                o->Self != (ulong)o || o->Epoch != epoch || o->Generation != generation || o->Phase != Working || o->Kind != OpenKind ||
                o->Index != operationIndex || m->Input.Operations + operationIndex != o || !o->LookupDone ||
                o->FileIndex != fileIndex || m->Input.Files + fileIndex != f ||
                f->Self != (ulong)f || f->Epoch != epoch || f->Generation != fileGeneration || f->Phase != Working)
                return status;
        }
    }

    // Preserve the original single-step validation and all failure results.
    // The caller batches only genuine cached cluster progress on this owner.
    private static Status FileChainOne(Mount* m, Operation* o)
    {
        FileHandle* f = m->Input.Files + o->FileIndex;
        if (!o->HaveCandidate)
        {
            f->Info = o->Candidate; o->HaveCandidate = true;
            if (f->Info.IsDirectory || f->Info.Length == 0)
            {
                if (!f->Info.IsDirectory && f->Info.FirstCluster != 0) return Fail(m, o, Status.CorruptFile);
                o->Phase = ReadyPhase; return Status.Ready;
            }
            ulong clusterBytes = (ulong)m->Volume.SectorsPerCluster * 512;
            o->RequiredClusters = (uint)(((ulong)f->Info.Length + clusterBytes - 1) / clusterBytes);
            if (o->RequiredClusters > f->ClusterCapacity) return Fail(m, o, Status.TraversalBudgetExceeded);
            var visit = Chain.Visit(m->Volume, f->Info.FirstCluster, new Span<uint>(f->Clusters, (int)f->ClusterCapacity), ref f->ClusterCount);
            if (visit != LiveFat32Status.Success) return Fail(m, o, Map(visit));
            return Status.Pending;
        }
        Status status = Fat(m, o, f->Clusters[f->ClusterCount - 1], out uint next, out bool end);
        if (status != Status.Success) return status;
        if (end)
        {
            var length = Chain.CheckLength(f->ClusterCount, o->RequiredClusters);
            if (length != LiveFat32Status.Success) return Fail(m, o, Map(length));
            o->Phase = ReadyPhase; return Status.Ready;
        }
        if (f->ClusterCount == o->RequiredClusters)
        {
            for (uint i = 0; i < f->ClusterCount; i++) if (f->Clusters[i] == next) return Fail(m, o, Status.ChainLoop);
            return Fail(m, o, Status.Overlong);
        }
        var result = Chain.Visit(m->Volume, next, new Span<uint>(f->Clusters, (int)f->ClusterCapacity), ref f->ClusterCount);
        if (result != LiveFat32Status.Success) return Fail(m, o, Map(result));
        return f->ClusterCount > o->RequiredClusters ? Fail(m, o, Status.CorruptFile) : Status.Pending;
    }

    private static Status Fat(Mount* m, Operation* o, uint cluster, out uint next, out bool end)
    {
        next = 0; end = false;
        ulong check = 0;
        if (Chain.ClusterFirstLba(m->Volume, cluster, ref check) != LiveFat32Status.Success) return Fail(m, o, Status.CorruptFile);
        ulong lba0 = m->Volume.FirstFatLba + cluster / 128, lba1 = lba0 + m->Volume.FatSectors;
        CacheSlot* slot = null;
        if (o->FatStage != 0)
        {
            slot = m->Input.Cache + o->FatSlot;
            if (o->FatCluster != cluster || slot->Epoch != m->Input.Epoch || slot->Lba0 != lba0 || slot->Lba1 != lba1) return Fail(m, o, Status.InvalidState);
            if (o->FatStage == 1) return ReadSector(m, o, lba0, Fat0Io);
            if (o->FatStage == 2) return ReadSector(m, o, lba1, Fat1Io);
            if (slot->Phase != Working || slot->OperationIndex != o->Index || slot->OperationGeneration != o->Generation) return Fail(m, o, Status.InvalidState);
        }
        else
        {
            for (uint i = 0; i < m->Input.CacheCount; i++)
            {
                CacheSlot* candidate = m->Input.Cache + i;
                if (candidate->Phase == Free || candidate->Epoch != m->Input.Epoch || candidate->Lba0 != lba0 || candidate->Lba1 != lba1) continue;
                if (candidate->Phase != ReadyPhase) return Status.Busy;
                slot = candidate; m->Hits++; break;
            }
            if (slot == null)
            {
                uint index = m->NextVictim;
                for (uint n = 0; n < m->Input.CacheCount; n++)
                {
                    uint at = (index + n) % m->Input.CacheCount;
                    if (m->Input.Cache[at].Phase == Working) continue;
                    slot = m->Input.Cache + at; o->FatSlot = at; m->NextVictim = (at + 1) % m->Input.CacheCount; break;
                }
                if (slot == null) return Status.Busy;
                slot->Phase = Working; slot->Epoch = m->Input.Epoch; slot->Lba0 = lba0; slot->Lba1 = lba1;
                slot->OperationIndex = o->Index; slot->OperationGeneration = o->Generation;
                o->FatCluster = cluster; o->FatStage = 1; m->Misses++;
                return ReadSector(m, o, lba0, Fat0Io);
            }
        }
        uint value = 0;
        var parsed = Chain.ReadFatEntry(m->Volume, cluster, lba0, new Span<byte>(slot->Data, 512), BlockReadResult.Success(1, 512), ref value);
        o->FatStage = 0;
        if (parsed is not (LiveFat32Status.Success or LiveFat32Status.EndOfChain))
        { slot->Phase = Free; return Fail(m, o, Map(parsed)); }
        slot->Phase = ReadyPhase;
        if (parsed == LiveFat32Status.EndOfChain) { end = true; return Status.Success; }
        next = value; return Status.Success;
    }

    private static Status LookupStep(Mount* m, Operation* o)
    {
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null) return LookupOne(m, o);
#endif
        Block.State* device = m->Input.Device;
        ulong epoch = m->Input.Epoch, generation = o->Generation;
        ulong submitted = device->Submitted, completed = device->Completed, copied = device->Copied, cancelled = device->Cancelled;
        uint operationIndex = o->Index, component = o->Component, componentLength = o->ComponentLength;
        uint cluster = o->Cursor.Cluster, sector = o->Cursor.SectorInCluster;
        byte mountPhase = m->Phase, kind = o->Kind;
        bool batch = !m->IoActive && o->Waiting == 0 && o->Cursor.Visited && o->Cursor.SectorReady &&
            !o->Cursor.HasPending && !o->Cursor.Ended && o->Cursor.EntryInSector < 16;
        for (uint step = 0; ; step++)
        {
            uint work = o->Work, entry = o->Cursor.EntryInSector;
            Status status = LookupOne(m, o);
            if (status != Status.Pending || !batch || step + 1 == MaximumLoadedDirectorySteps ||
                o->Work != work + 1 || o->Cursor.EntryInSector != entry + 1 || o->Cursor.EntryInSector >= 16 ||
                o->Waiting != 0 || m->IoActive || o->LookupDone || o->Cursor.HasPending || o->Cursor.Ended ||
                !o->Cursor.Visited || !o->Cursor.SectorReady || o->Cursor.Cluster != cluster || o->Cursor.SectorInCluster != sector ||
                o->Component != component || o->ComponentLength != componentLength ||
                device->Submitted != submitted || device->Completed != completed || device->Copied != copied || device->Cancelled != cancelled ||
                m->Input.Device != device || !Bound(m) || m->Phase != mountPhase || !DeviceBound(m) || !device->Running || m->Input.Epoch != epoch ||
                o->Self != (ulong)o || o->Epoch != epoch || o->Generation != generation || o->Phase != Working || o->Kind != kind ||
                o->Index != operationIndex || m->Input.Operations + operationIndex != o)
                return status;
        }
    }

    // Preserve every single-entry VFAT, name/alias collision and work-budget
    // check. Only internal lookups consume entries here; public scans do not.
    private static Status LookupOne(Mount* m, Operation* o)
    {
        Status step = DirectoryStep(m, o);
        if (step == Status.Entry)
        {
            bool exact = NameEquals(o, false), folded = NameEquals(o, true), alias = AliasEquals(o);
            if (folded || alias)
            {
                o->MatchCount++;
                if (exact) { o->Candidate = o->Cursor.Pending; o->CandidateExact = true; }
            }
            o->Cursor.HasPending = false; return Status.Pending;
        }
        if (step != Status.EndOfDirectory) return step;
        if (o->MatchCount > 1) return Fail(m, o, Status.Ambiguous);
        if (!o->CandidateExact) return Fail(m, o, Status.NotFound);
        uint after = o->Component + o->ComponentLength;
        if (after == o->PathLength)
        {
            if (o->Kind == OpenKind && o->Candidate.IsDirectory)
            {
                uint depth = 1; for (uint i = 13; i < o->PathLength; i++) if (o->Storage.Path[i] == '/') depth++;
                if (depth > 8) return Fail(m, o, Status.InvalidPath);
            }
            o->LookupDone = true; return Status.Pending;
        }
        if (!o->Candidate.IsDirectory) return Fail(m, o, Status.NotDirectory);
        o->Component = after + 1; Component(o); uint directory = o->Candidate.FirstCluster;
        o->CandidateExact = false; o->MatchCount = 0; InitCursor(o, directory); return Status.Pending;
    }

    private static Status DirectoryStep(Mount* m, Operation* o)
    {
        if (o->Cursor.HasPending) return Status.Entry;
        if (o->Cursor.Ended) return Status.EndOfDirectory;
        if (!o->Cursor.Visited)
        {
            var visit = Chain.Visit(m->Volume, o->Cursor.Cluster, new Span<uint>(o->Storage.History, (int)o->Storage.HistoryCapacity), ref o->Cursor.Visits);
            if (visit != LiveFat32Status.Success) return Fail(m, o, Map(visit));
            o->Cursor.Visited = true;
        }
        if (!o->Cursor.SectorReady)
        {
            ulong lba = 0;
            if (Chain.ClusterFirstLba(m->Volume, o->Cursor.Cluster, ref lba) != LiveFat32Status.Success) return Fail(m, o, Status.CorruptDirectory);
            return ReadSector(m, o, lba + o->Cursor.SectorInCluster, DirectoryIo);
        }
        if (o->Cursor.EntryInSector == 16)
        {
            o->Cursor.SectorReady = false;
            if (o->Cursor.SectorInCluster + 1 < m->Volume.SectorsPerCluster) { o->Cursor.SectorInCluster++; return Status.Pending; }
            o->Cursor.SectorInCluster = m->Volume.SectorsPerCluster;
            // Preserve the exhausted sector until both FAT-copy reads finish.
            o->Cursor.SectorReady = true;
            Status fat = Fat(m, o, o->Cursor.Cluster, out uint next, out bool end);
            if (fat != Status.Success) return fat;
            if (end)
            {
                if (Vfat.Finish(o->Cursor.Vfat) != LiveDirectoryStatus.End) return Fail(m, o, Status.CorruptDirectory);
                o->Cursor.Ended = true; return Status.EndOfDirectory;
            }
            o->Cursor.Cluster = next; o->Cursor.SectorInCluster = 0; o->Cursor.Visited = false; o->Cursor.SectorReady = false;
            return Status.Pending;
        }
        if (o->Work == m->Input.DirectoryEntryBudget) return Fail(m, o, Status.TraversalBudgetExceeded);
        o->Work++;
        byte* raw = o->Storage.Sector + o->Cursor.EntryInSector * 32; uint physicalEntry = o->Cursor.EntryInSector++;
        var parsed = Vfat.Consume(new Span<byte>(raw, 32), ref o->Cursor.Vfat, new Span<ushort>(o->Storage.Name, 260), out var entry);
        if (parsed is LiveDirectoryStatus.Skipped or LiveDirectoryStatus.LongName) return Status.Pending;
        if (parsed == LiveDirectoryStatus.End) { o->Cursor.Ended = true; return Status.EndOfDirectory; }
        if (parsed != LiveDirectoryStatus.Entry) return Fail(m, o, Status.CorruptDirectory);
        ushort length = entry.NameLength;
        if (length == 0)
        {
            for (uint i = 0; i < 8 && raw[i] != ' '; i++) o->Storage.Name[length++] = raw[i];
            if (raw[8] != ' ') { o->Storage.Name[length++] = '.'; for (uint i = 8; i < 11 && raw[i] != ' '; i++) o->Storage.Name[length++] = raw[i]; }
        }
        for (uint i = 0; i < length; i++) if (o->Storage.Name[i] is < 33 or > 126) return Fail(m, o, Status.UnsupportedEncoding);
        ushort date = EndianPrimitives.ReadUInt16LittleEndian(raw + 24), time = EndianPrimitives.ReadUInt16LittleEndian(raw + 22);
        long seconds = 0; // Project policy: all-zero FAT date/time is unspecified.
        if ((date != 0 || time != 0) && !GregorianUtc.TryUnixSeconds((uint)(1980 + (date >> 9)), (uint)((date >> 5) & 15), (uint)(date & 31),
            (uint)(time >> 11), (uint)((time >> 5) & 63), (uint)((time & 31) * 2), out seconds)) return Fail(m, o, Status.CorruptDirectory);
        ulong sector = 0;
        if (Chain.ClusterFirstLba(m->Volume, o->Cursor.Cluster, ref sector) != LiveFat32Status.Success) return Fail(m, o, Status.CorruptDirectory);
        o->Cursor.Pending = new() { FirstCluster = entry.FirstCluster, Length = entry.ByteCount, Attributes = entry.Attributes,
            NameBytes = length, ModifiedSeconds = seconds, Inode = (sector + o->Cursor.SectorInCluster) * 16 + physicalEntry + 256 };
        o->Cursor.NameLength = length; o->Cursor.HasPending = true; return Status.Entry;
    }

    private static bool NameEquals(Operation* o, bool fold)
    {
        if (o->Cursor.NameLength != o->ComponentLength) return false;
        for (uint i = 0; i < o->ComponentLength; i++)
        {
            byte a = (byte)o->Storage.Name[i], b = o->Storage.Path[o->Component + i];
            if (fold) { a = Fold(a); b = Fold(b); }
            if (a != b) return false;
        }
        return true;
    }
    private static bool AliasEquals(Operation* o)
    {
        byte* raw = o->Storage.Sector + (o->Cursor.EntryInSector - 1) * 32;
        byte* name = stackalloc byte[12]; uint length = 0;
        for (uint i = 0; i < 8 && raw[i] != ' '; i++) name[length++] = raw[i];
        if (raw[8] != ' ') { name[length++] = (byte)'.'; for (uint i = 8; i < 11 && raw[i] != ' '; i++) name[length++] = raw[i]; }
        if (length != o->ComponentLength) return false;
        for (uint i = 0; i < length; i++) if (Fold(name[i]) != Fold(o->Storage.Path[o->Component + i])) return false;
        return true;
    }
    private static byte Fold(byte value) => value is >= (byte)'a' and <= (byte)'z' ? (byte)(value - 32) : value;
    private static void InitCursor(Operation* o, uint cluster) { o->Cursor = default; o->Cursor.Cluster = cluster; o->FatStage = 0; }
    private static void Component(Operation* o)
    { o->ComponentLength = 0; while (o->Component + o->ComponentLength < o->PathLength && o->Storage.Path[o->Component + o->ComponentLength] != '/') o->ComponentLength++; }

    private static Status PathPolicy(ReadOnlySpan<byte> path)
    {
        if (path.Length is < 12 or >= 256) return Status.InvalidPath;
        const string root = "/app/wwwroot";
        for (int i = 0; i < 12; i++) if (path[i] != root[i]) return Status.OutsideRoot;
        if (path.Length == 12) return Status.Success;
        if (path[12] != '/') return Status.OutsideRoot;
        uint components = 0; int start = 13;
        for (int i = start; i <= path.Length; i++)
        {
            if (i != path.Length && path[i] != '/')
            {
                byte c = path[i]; if (c is < 33 or > 126) return Status.UnsupportedEncoding;
                if (c is (byte)'\\' or (byte)':' or (byte)'*' or (byte)'?' or (byte)'"' or (byte)'<' or (byte)'>' or (byte)'|') return Status.InvalidPath;
                continue;
            }
            int length = i - start;
            if (length == 0 || path[i - 1] is (byte)'.' or (byte)' ' ||
                (length == 1 && path[start] == '.') || (length == 2 && path[start] == '.' && path[start + 1] == '.')) return Status.InvalidPath;
            if (++components > 9) return Status.InvalidPath;
            start = i + 1;
        }
        return Status.Success;
    }

    private static Status ReserveOperation(Mount* m, byte kind, out Operation* result)
    {
        result = null; bool exhausted = false;
        for (uint i = 0; i < m->Input.OperationCount; i++)
        {
            Operation* o = m->Input.Operations + i;
            if (o->Phase != Free) continue;
            if (o->Generation == ~0UL) { exhausted = true; continue; }
            Begin(o, kind); result = o; return Status.Success;
        }
        return exhausted ? Status.GenerationExhausted : Status.Full;
    }
    private static void Begin(Operation* o, byte kind)
    {
        Scratch scratch = o->Storage; ulong self = o->Self, epoch = o->Epoch, generation = o->Generation; uint index = o->Index;
        *o = default; o->Storage = scratch; o->Self = self; o->Epoch = epoch; o->Generation = generation + 1; o->Index = index;
        o->Kind = kind; o->Phase = Working; o->FileIndex = ~0U; o->Failure = Status.Success;
    }
    private static void Release(Mount* m, Operation* o)
    {
        for (uint i = 0; i < m->Input.CacheCount; i++)
        {
            CacheSlot* c = m->Input.Cache + i;
            if (c->Phase == Working && c->OperationIndex == o->Index && c->OperationGeneration == o->Generation) c->Phase = Free;
        }
        if (o->FileIndex < m->Input.FileCount)
        {
            FileHandle* f = m->Input.Files + o->FileIndex;
            if (o->Kind == OpenKind && f->Phase == Working) { f->Phase = Free; f->ClusterCount = 0; }
            if (o->Kind is ReadKind or ScanKind && f->References != 0) f->References--;
        }
        o->Phase = Free; o->Waiting = 0; o->Cursor.HasPending = false;
    }
    private static Status Fail(Mount* m, Operation* o, Status status)
    { o->Phase = Failed; o->Failure = status; if (o->Kind == MountKind) { m->Phase = Faulted; m->Failure = status; } return status; }
    private static Status DeviceFault(Mount* m) { InvalidateCache(m); m->Phase = Faulted; m->Failure = Status.DeviceFailed; return Status.DeviceFailed; }
    private static void InvalidateCache(Mount* m)
    {
        for (uint i = 0; i < m->Input.CacheCount; i++) m->Input.Cache[i].Phase = Free;
        if (m->ImmutableDirectoryCacheBacking != null)
            for (uint i = 0; i < m->ImmutableDirectoryCacheSlots; i++)
                ((ImmutableDirectoryRecord*)m->ImmutableDirectoryCacheBacking)[i].Phase = Free;
    }

    private static Status Map(LiveFat32Status status) => status switch
    {
        LiveFat32Status.UnsupportedGeometry => Status.UnsupportedGeometry,
        LiveFat32Status.ScratchLimit => Status.TraversalBudgetExceeded,
        LiveFat32Status.ReadFailed => Status.DeviceFailed,
        LiveFat32Status.Loop => Status.ChainLoop,
        LiveFat32Status.Truncated => Status.Truncated,
        LiveFat32Status.Overlong => Status.Overlong,
        LiveFat32Status.BadCluster => Status.BadCluster,
        LiveFat32Status.FreeCluster => Status.FreeCluster,
        LiveFat32Status.ReservedCluster => Status.ReservedCluster,
        LiveFat32Status.InvalidCluster => Status.InvalidCluster,
        LiveFat32Status.InvalidBpb => Status.InvalidBpb,
        _ => Status.CorruptFile
    };
    private static OperationToken OpToken(Mount* m, Operation* o) => new((ulong)m, m->Input.Epoch, o->Generation, o->Index);
    private static bool Op(Mount* m, OperationToken token, out Operation* o)
    {
        o = null;
        if (!token.IsInitialized || token.Owner != (ulong)m || token.Epoch != m->Input.Epoch || token.Index >= m->Input.OperationCount) return false;
        Operation* found = m->Input.Operations + token.Index;
        if (found->Self != (ulong)found || found->Epoch != m->Input.Epoch || found->Generation != token.Generation || found->Phase == Free) return false;
        o = found; return true;
    }
    private static bool File(Mount* m, FileToken token, out FileHandle* f)
    {
        f = null;
        if (!token.IsInitialized || token.Owner != (ulong)m || token.Epoch != m->Input.Epoch || token.Index >= m->Input.FileCount) return false;
        FileHandle* found = m->Input.Files + token.Index;
        if (found->Self != (ulong)found || found->Epoch != m->Input.Epoch || found->Generation != token.Generation || found->Phase != ReadyPhase) return false;
        f = found; return true;
    }
    private static bool Bound(Mount* m) => Storage((ulong)m, (ulong)sizeof(Mount), 8) && m->Self == (ulong)m && m->Phase is >= Mounting and <= Faulted;
    private static bool DeviceBound(Mount* m)
    {
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null) return RetainedBorrowed(m);
#endif
        return Storage((ulong)m->Input.Device, (ulong)sizeof(Block.State), 8) && m->Input.Device->Self == (ulong)m->Input.Device &&
            m->Input.Device->Input.Epoch == m->DeviceEpoch && m->Input.Device->PoolEpoch == m->PoolEpoch;
    }
    private static Status BackendStatus(Mount* m)
    {
        if (!DeviceBound(m)) return Status.DeviceFailed;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
        {
            Port.Inspection facts = default; var status = Port.Inspect(m->ReadPort, m->PortBinding.Epoch, &facts);
            if (status == Port.Status.Busy) return Status.Busy;
            return status == Port.Status.Success && SameBinding(m, facts) ? Status.Success : Status.DeviceFailed;
        }
#endif
        return m->Input.Device->Running ? Status.Success : Status.DeviceFailed;
    }
    private static Status RequireLive(Mount* m)
    {
        if (!Bound(m)) return Status.InvalidState;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
        {
            if (m->Phase == Faulted) return m->Failure;
            return m->Phase == Mounted ? BackendStatus(m) : Status.InvalidState;
        }
#endif
        if (m->Phase != Mounted) return Status.InvalidState;
        return DeviceBound(m) && m->Input.Device->Running ? Status.Success : Status.InvalidState;
    }
    private static bool Live(Mount* m) => RequireLive(m) == Status.Success;
    private static ulong BackendCapacity(Mount* m)
    {
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null) return m->PortBinding.SectorCount;
#endif
        return m->Input.Device->SectorCount;
    }

#if !NOSTDLIB_EFI || M16_027_PROBE
    private static bool SameBinding(Mount* m, Port.Inspection facts) => facts.Owner == m->PortBinding.Owner && facts.Epoch == m->PortBinding.Epoch &&
        facts.EngineOwner == m->PortBinding.EngineOwner && facts.EngineEpoch == m->PortBinding.EngineEpoch &&
        facts.DeviceOwner == m->PortBinding.DeviceOwner && facts.DeviceEpoch == m->DeviceEpoch &&
        facts.SectorCount == m->PortBinding.SectorCount && facts.FirstLba == m->Input.PartitionFirst && facts.LastLba == m->Input.PartitionLast;
    private static bool RetainedBorrowed(Mount* m)
    {
        if (!Bound(m) || m->Phase == Retired || m->Input.Device != null ||
            !Storage((ulong)m->ReadPort, (ulong)sizeof(Port.State), 8) || m->PortBinding.Owner != (ulong)m->ReadPort ||
            m->PortBinding.Epoch == 0 || !Shape(m->Input) || m->Input.PartitionFirst != m->PortBinding.FirstLba ||
            m->Input.PartitionLast != m->PortBinding.LastLba) return false;
        return AdmissionOutside(m, m->Input, m->ReadPort, m->PortBinding.Epoch);
    }
    // Full FS+port/C/B retained-tree query. It supplies no live-copy permission.
    internal static bool CanRetainStorage(Mount* m, ulong epoch, ulong address, ulong bytes) =>
        Bound(m) && m->Phase != Retired && m->Input.Epoch == epoch && DeviceBound(m) && External(m, address, bytes, 1);

    internal const int MaximumRetainedStorageRanges = 177;

    /// <summary>Check a flat, bounded start/byte-count inventory against one
    /// original borrowed mount. The serialized owner keeps the input and all
    /// backing stable/mapped through return. Audit the retained FS/backend once;
    /// the original candidate span uses one complete port batch, followed by
    /// every local exclusion after the backend audit scratch has returned.
    /// Empty extents are skipped, but stale/retired or malformed owners reject
    /// even an all-empty inventory. Dirty/fault/stopped backing may remain
    /// structurally retained. No live I/O, copy, release or later permission
    /// follows; no input, token, output, callback or mutation is retained.</summary>
    internal static bool CanRetainStorageRanges(Mount* m, ulong epoch, ReadOnlySpan<ulong> pairs)
    {
        if (pairs.Length == 0 || (pairs.Length & 1) != 0 || pairs.Length > MaximumRetainedStorageRanges * 2 ||
            !Bound(m) || m->Phase == Retired || m->Input.Epoch != epoch || m->ReadPort == null || !RetainedBorrowed(m)) return false;
        return Port.CanRetainStorageRanges(m->ReadPort, m->PortBinding.Epoch, pairs) && BorrowedRangesOutside(m, pairs);
    }

    // Keep this local-only scratch after RetainedBorrowed/AdmissionOutside and
    // the external port batch returned. No backend query nests inside this
    // additional1088-byte inventory. Emitted stack remains a separate gate.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool BorrowedRangesOutside(Mount* m, ReadOnlySpan<ulong> pairs)
    {
        Extent* all = stackalloc Extent[68]; uint count = Extents(m, m->Input, all);
        for (int i = 0; i < pairs.Length; i += 2)
        {
            ulong start = pairs[i], bytes = pairs[i + 1];
            if (bytes == 0) continue;
            if (!Storage(start, bytes, 1)) return false;
            for (uint j = 0; j < count; j++) if (Overlap(start, bytes, all[j].Start, all[j].Bytes)) return false;
        }
        return true;
    }
#endif

    // Admission and every external output use one complete retained extent
    // inventory. Numeric checks do not establish mapping or physical alias proof.
    private struct Extent { internal ulong Start, Bytes; }
    private static uint Extents(Mount* m, in Admission a, Extent* all)
    {
        uint n = 0;
        all[n++] = new() { Start = (ulong)m, Bytes = (ulong)sizeof(Mount) };
        if (m->ImmutableDirectoryCacheBacking != null)
            all[n++] = new() { Start = (ulong)m->ImmutableDirectoryCacheBacking, Bytes = ImmutableDirectoryCacheBytes(m->ImmutableDirectoryCacheSlots) };
        all[n++] = new() { Start = (ulong)a.Operations, Bytes = a.OperationCount * (ulong)sizeof(Operation) };
        all[n++] = new() { Start = (ulong)a.Files, Bytes = a.FileCount * (ulong)sizeof(FileHandle) };
        all[n++] = new() { Start = (ulong)a.Cache, Bytes = a.CacheCount * (ulong)sizeof(CacheSlot) };
        for (uint i = 0; i < a.OperationCount; i++)
        {
            Scratch s = a.Operations[i].Storage;
            all[n++] = new() { Start = (ulong)s.Path, Bytes = 256 }; all[n++] = new() { Start = (ulong)s.Sector, Bytes = 512 };
            all[n++] = new() { Start = (ulong)s.Name, Bytes = 520 }; all[n++] = new() { Start = (ulong)s.History, Bytes = s.HistoryCapacity * 4UL };
            all[n++] = new() { Start = (ulong)s.Range, Bytes = s.RangeCapacity };
        }
        for (uint i = 0; i < a.FileCount; i++) all[n++] = new() { Start = (ulong)a.Files[i].Clusters, Bytes = a.Files[i].ClusterCapacity * 4UL };
        for (uint i = 0; i < a.CacheCount; i++) all[n++] = new() { Start = (ulong)a.Cache[i].Data, Bytes = 512 };
        return n;
    }
    private static bool Admit(Mount* m, in Admission a)
    {
        if (!Storage((ulong)a.Device, (ulong)sizeof(Block.State), 8) ||
            a.Device->Self != (ulong)a.Device || !a.Device->Running || a.Device->ReadPending || a.Device->ReadReady ||
            a.PartitionLast >= a.Device->SectorCount || !AdmitSlots(m, a)) return false;
        Extent* all = stackalloc Extent[68]; uint count = Extents(m, a, all);
        if (!Disjoint(all, count)) return false;
        for (uint i = 0; i < count; i++) if (!OutsideDevice(a.Device, all[i].Start, all[i].Bytes)) return false;
        return true;
    }
    private static bool Shape(in Admission a)
    {
        if (a.Epoch == 0 || a.PartitionFirst == 0 || a.PartitionFirst > a.PartitionLast ||
            a.OperationCount is < 2 or > MaximumSlots || a.FileCount is < 2 or > MaximumSlots || a.CacheCount is 0 or > MaximumCacheSlots ||
            a.DirectoryEntryBudget is 0 or > 1048576 ||
            !Storage((ulong)a.Operations, a.OperationCount * (ulong)sizeof(Operation), 8) ||
            !Storage((ulong)a.Files, a.FileCount * (ulong)sizeof(FileHandle), 8) ||
            !Storage((ulong)a.Cache, a.CacheCount * (ulong)sizeof(CacheSlot), 8)) return false;
        for (uint i = 0; i < a.OperationCount; i++) if (a.Operations[i].Storage.HistoryCapacity is 0 or > MaximumClusters ||
            a.Operations[i].Storage.RangeCapacity is 0 or > MaximumRangeBytes) return false;
        for (uint i = 0; i < a.FileCount; i++) if (a.Files[i].ClusterCapacity is 0 or > MaximumClusters) return false;
        return true;
    }
    private static bool AdmitSlots(Mount* m, in Admission a)
    {
        if (!Storage((ulong)m, (ulong)sizeof(Mount), 8) || !Shape(a)) return false;
        for (uint i = 0; i < (uint)sizeof(Mount); i++) if (((byte*)m)[i] != 0) return false;
        for (uint i = 0; i < a.OperationCount; i++)
        {
            Operation* o = a.Operations + i;
            if (o->Self != 0 || o->Epoch != 0 || o->Generation != 0 || o->Phase != 0 || o->Kind != 0 ||
                o->Storage.HistoryCapacity is 0 or > MaximumClusters || o->Storage.RangeCapacity is 0 or > MaximumRangeBytes) return false;
        }
        for (uint i = 0; i < a.FileCount; i++)
            if (a.Files[i].Self != 0 || a.Files[i].Epoch != 0 || a.Files[i].Generation != 0 || a.Files[i].Phase != 0 || a.Files[i].References != 0 ||
                a.Files[i].ClusterCount != 0 || a.Files[i].ClusterCapacity is 0 or > MaximumClusters) return false;
        for (uint i = 0; i < a.CacheCount; i++) if (a.Cache[i].Epoch != 0 || a.Cache[i].Phase != 0) return false;
        return true;
    }
    private static bool Disjoint(Extent* all, uint count)
    {
        for (uint i = 0; i < count; i++)
        {
            if (!Storage(all[i].Start, all[i].Bytes, 8)) return false;
            for (uint j = 0; j < i; j++) if (Overlap(all[i].Start, all[i].Bytes, all[j].Start, all[j].Bytes)) return false;
        }
        return true;
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
    private static bool AdmissionOutside(Mount* m, in Admission a, Port.State* port, ulong epoch)
    {
        Extent* all = stackalloc Extent[68]; uint count = Extents(m, a, all);
        if (!Disjoint(all, count)) return false;
        return Port.CanRetainStorageRanges(port, epoch, new ReadOnlySpan<ulong>((ulong*)all, (int)count * 2));
    }
#endif
    private static bool External(Mount* m, ulong start, ulong bytes, ulong alignment)
    {
        if (!Storage(start, bytes, alignment)) return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
        { if (!RetainedBorrowed(m) || !Port.CanRetainStorage(m->ReadPort, m->PortBinding.Epoch, start, bytes)) return false; }
        else
#endif
        if (!OutsideDevice(m->Input.Device, start, bytes)) return false;
        Extent* all = stackalloc Extent[68]; uint count = Extents(m, m->Input, all);
        for (uint i = 0; i < count; i++) if (Overlap(start, bytes, all[i].Start, all[i].Bytes)) return false;
        return true;
    }
    private static bool OutsideDevice(Block.State* d, ulong start, ulong bytes)
    {
        var a = d->Input; DmaBufferPool* p = a.Pool;
        return !Overlap(start, bytes, (ulong)d, (ulong)sizeof(Block.State)) &&
            !Overlap(start, bytes, (ulong)p, (ulong)sizeof(DmaBufferPool)) &&
            !Overlap(start, bytes, (ulong)p->Slots, p->Count * (ulong)sizeof(DmaBufferSlot)) &&
            !Overlap(start, bytes, p->Window.CpuVirtualBase, p->Window.ByteLength) &&
            !Overlap(start, bytes, a.Common.VirtualStart, a.Common.Bytes) && !Overlap(start, bytes, a.Notify.VirtualStart, a.Notify.Bytes) &&
            !Overlap(start, bytes, a.Device.VirtualStart, a.Device.Bytes);
    }
    private static bool Storage(ulong start, ulong bytes, ulong alignment) => start >= 4096 && start < AddressLimit &&
        bytes != 0 && bytes <= AddressLimit - start && (start & (alignment - 1)) == 0;
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => a < b + m && b < a + n;
}
