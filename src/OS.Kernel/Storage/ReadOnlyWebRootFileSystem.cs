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
public static unsafe partial class ReadOnlyWebRootFileSystem
{
    public const uint PathBytes = 256, MaximumRangeBytes = 65536, MaximumClusters = 4096;
    public const uint MaximumSlots = 8, MaximumCacheSlots = 8;
    public const uint MaximumImmutableDirectoryCacheSlots = 5;
    public const uint MaximumImmutableFileChainCacheSlots = 2;
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

    /// <summary>Borrow a fresh metadata snapshot without I/O. Authenticate the
    /// exact live mount/device and file generation before writing the disjoint,
    /// aligned caller-owned output. Reset/stale/alias failures change nothing.</summary>
    public static Status Inspect(Mount* m, FileToken file, FileInspection* result)
    {
        Status live = RequireLive(m);
        if (live != Status.Success)
            return live;
        if (!External(m, (ulong)result, (ulong)sizeof(FileInspection), 8))
            return Status.InvalidStorage;
        if (!File(m, file, out FileHandle* f))
            return Status.StaleToken;
        *result = new()
        {
            Info = f->Info,
            AllocationKnown = !f->Info.IsDirectory,
            AllocatedBlocks = f->Info.IsDirectory ? 0 : (ulong)f->ClusterCount * m->Volume.SectorsPerCluster
        };
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
        if (maximumDataReadSectors is 0 or > 4 || !Admit(m, a))
            return Status.InvalidAccess;
        m->Self = (ulong)m;
        m->Input = a;
        m->DeviceEpoch = a.Device->Input.Epoch;
        m->DataReadSectors = (byte)maximumDataReadSectors;
        m->PoolEpoch = a.Device->PoolEpoch;
        return Initialize(m, a);
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
            m->ImmutableDirectoryCacheSlots != 0)
            return Status.InvalidAccess;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
            return Status.InvalidAccess;
#endif
        if (!DeviceBound(m) || !m->Input.Device->Running || m->Input.Device->ReadPending ||
            m->Input.Device->ReadReady)
            return Status.InvalidState;
        if (!External(m, (ulong)backing, bytes, 8))
            return Status.InvalidStorage;
        for (uint i = 0; i < slots; i++)
            ((ImmutableDirectoryRecord*)backing)[i] = default;
        m->ImmutableDirectoryCacheBacking = backing;
        m->ImmutableDirectoryCacheSlots = slots;
        return Status.Success;
    }

    /// <summary>Exact ordinary-RAM certificate backing for1..2 successful file
    /// chains. Map data remains in the existing admitted handle arrays. Zero
    /// means an unsupported count or record size; no memory is accessed.</summary>
    public static uint ImmutableFileChainCacheBytes(uint slots) =>
        slots is 0 or > MaximumImmutableFileChainCacheSlots || sizeof(FileChainCertificate) != 56 ? 0 : slots * 56;

    /// <summary>Opt in to positive regular-file chain reuse on immutable media.
    /// Call once after Start before any mount I/O. The aligned disjoint backing,
    /// mount, volume, device/pool lifetime and every admitted map array remain
    /// exclusive/stable until Stop. Closed maps are retained only by explicit
    /// certificates and revoked before overwrite; closed bytes alone confer no
    /// authority. Path resolution still runs and file data is always read fresh.
    /// Borrowed writable ports are unsupported. Hits cannot observe hypothetical
    /// suppressed-read faults or external mutations violating this contract.
    /// Invalid configuration changes nothing and performs no I/O or allocation.</summary>
    public static Status EnableImmutableFileChainCache(Mount* m, byte* backing, uint bytes, uint slots)
    {
        uint required = ImmutableFileChainCacheBytes(slots);
        if (required == 0 || bytes != required || !Bound(m) || m->Phase != Mounting ||
            m->Reads != 0 || m->IoActive || m->ImmutableFileChainCacheBacking != null ||
            m->ImmutableFileChainCacheSlots != 0)
            return Status.InvalidAccess;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
            return Status.InvalidAccess;
#endif
        if (!DeviceBound(m) || !m->Input.Device->Running || m->Input.Device->ReadPending ||
            m->Input.Device->ReadReady)
            return Status.InvalidState;
        if (!External(m, (ulong)backing, bytes, 8))
            return Status.InvalidStorage;
        for (uint i = 0; i < slots; i++)
            ((FileChainCertificate*)backing)[i] = default;
        m->ImmutableFileChainCacheBacking = backing;
        m->ImmutableFileChainCacheSlots = slots;
        return Status.Success;
    }

#if !NOSTDLIB_EFI || M16_027_PROBE
    /// <summary>Admit the genuine borrowed backend without queue/device setup.
    /// Busy leaves every byte unchanged. No user buffer survives a call and no
    /// filesystem operation can poll another client's physical request.</summary>
    public static Status StartBorrowed(Mount* m, in BorrowedAdmission input)
    {
        BorrowedAdmission b = input;
        if (!Storage((ulong)m, (ulong)sizeof(Mount), 8) || !Storage((ulong)b.ReadPort, (ulong)sizeof(Port.State), 8))
            return Status.InvalidAccess;
        Port.Inspection facts = default;
        var status = Port.Inspect(b.ReadPort, b.PortEpoch, &facts);
        if (status == Port.Status.Busy)
            return Status.Busy;
        if (status != Port.Status.Success)
            return Status.DeviceFailed;
        if (facts.Owner != (ulong)b.ReadPort || facts.Epoch != b.PortEpoch)
            return Status.DeviceFailed;
        if (facts.RequestActive)
            return Status.Busy;
        Admission a = new()
        {
            Epoch = b.Epoch,
            PartitionFirst = facts.FirstLba,
            PartitionLast = facts.LastLba,
            Operations = b.Operations,
            Files = b.Files,
            Cache = b.Cache,
            OperationCount = b.OperationCount,
            FileCount = b.FileCount,
            CacheCount = b.CacheCount,
            DirectoryEntryBudget = b.DirectoryEntryBudget
        };
        if (!AdmitSlots(m, a) || !AdmissionOutside(m, a, b.ReadPort, b.PortEpoch))
            return Status.InvalidAccess;
        m->Self = (ulong)m;
        m->Input = a;
        m->ReadPort = b.ReadPort;
        m->PortBinding = facts;
        m->DeviceEpoch = facts.DeviceEpoch;
        return Initialize(m, a);
    }
#endif

    private static Status Initialize(Mount* m, in Admission a)
    {
        m->Phase = Mounting;
        m->Failure = Status.Success;
        for (uint i = 0; i < a.OperationCount; i++)
        {
            a.Operations[i].Self = (ulong)(a.Operations + i);
            a.Operations[i].Epoch = a.Epoch;
            a.Operations[i].Index = i;
        }
        for (uint i = 0; i < a.FileCount; i++)
        {
            a.Files[i].Self = (ulong)(a.Files + i);
            a.Files[i].Epoch = a.Epoch;
        }
        Operation* o = a.Operations;
        Begin(o, MountKind);
        o->PathLength = 12;
        const string root = "/app/wwwroot";
        for (uint i = 0; i < 12; i++)
            o->Storage.Path[i] = (byte)root[(int)i];
        return Status.Pending;
    }

    /// <summary>Advance at most one real sector completion or a bounded parser
    /// turn over one loaded sector. Return Ready only after BPB and both root
    /// components are validated; never complete and parse I/O in the same call.</summary>
    public static Status PumpMount(Mount* m)
    {
        if (!Bound(m))
            return Status.InvalidState;
        if (m->Phase == Mounted)
        {
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (m->ReadPort != null)
            {
                Status status = BackendStatus(m);
                if (status != Status.Success)
                    return status;
            }
#endif
            return Status.Ready;
        }
        if (m->Phase != Mounting)
            return Status.InvalidState;
        return Pump(m, m->Input.Operations);
    }

    /// <summary>Copy one absolute ASCII path below the exact admitted root and
    /// reserve independent operation/handle slots. Output is borrowed mapped,
    /// aligned exclusive ordinary RAM; reject aliases before any output write.
    /// Invalid path/storage publishes nothing. Poll then CopyOpen consumes success.</summary>
    public static Status BeginOpen(Mount* m, ReadOnlySpan<byte> path, OperationToken* token)
    {
        Status live = RequireLive(m);
        if (live != Status.Success)
            return live;
        if (!External(m, (ulong)token, (ulong)sizeof(OperationToken), 8))
            return Status.InvalidStorage;
        if (path.Length == 0)
            return Status.InvalidPath;
        fixed (byte* p = path)
            if (!External(m, (ulong)p, (ulong)path.Length, 1) || Overlap((ulong)p, (ulong)path.Length, (ulong)token, (ulong)sizeof(OperationToken)))
                return Status.InvalidStorage;
        Status valid = PathPolicy(path);
        if (valid != Status.Success)
            return valid;
        Status reserved = ReserveOperation(m, OpenKind, out Operation* o);
        if (reserved != Status.Success)
            return reserved;
        uint file = 0;
        for (; file < m->Input.FileCount; file++)
            if (m->Input.Files[file].Phase == Free && m->Input.Files[file].Generation != ~0UL)
                break;
        if (file == m->Input.FileCount)
        {
            bool exhausted = false;
            for (uint i = 0; i < m->Input.FileCount; i++)
                if (m->Input.Files[i].Phase == Free)
                    exhausted = true;
            Release(m, o);
            return exhausted ? Status.GenerationExhausted : Status.Full;
        }
        FileHandle* f = m->Input.Files + file;
        f->Generation++;
        f->Phase = Working;
        f->ClusterCount = 0;
        o->FileIndex = file;
        o->PathLength = (uint)path.Length;
        for (int i = 0; i < path.Length; i++)
            o->Storage.Path[i] = path[i];
        if (path.Length == 12)
        {
            o->Candidate = m->Root;
            o->LookupDone = true;
        }
        else
        {
            o->Component = 13;
            InitCursor(o, m->Root.FirstCluster);
            Component(o);
        }
        *token = OpToken(m, o);
        return Status.Pending;
    }

    /// <summary>Start a bounded offset read. Only this requested range, capped
    /// by the operation's <=64KiB scratch and EOF, is staged for atomic output.
    /// Handle cluster metadata is already validated; data is read on demand.
    /// No destination pointer is retained. Offset greater than EOF is rejected.</summary>
    public static Status BeginRead(Mount* m, FileToken file, ulong offset, uint bytes, OperationToken* token)
    {
        Status live = RequireLive(m);
        if (live != Status.Success)
            return live;
        if (!External(m, (ulong)token, (ulong)sizeof(OperationToken), 8))
            return Status.InvalidStorage;
        if (!File(m, file, out FileHandle* f))
            return Status.StaleToken;
        if (f->Info.IsDirectory)
            return Status.IsDirectory;
        if (offset > f->Info.Length || bytes > MaximumRangeBytes || offset > ~0UL - bytes)
            return Status.InvalidOffset;
        Status reserved = ReserveOperation(m, ReadKind, out Operation* o);
        if (reserved != Status.Success)
            return reserved;
        uint wanted = (uint)((ulong)bytes < f->Info.Length - offset ? bytes : f->Info.Length - offset);
        if (wanted > o->Storage.RangeCapacity)
        {
            Release(m, o);
            return Status.ShortBuffer;
        }
        o->FileIndex = file.Index;
        o->Offset = offset;
        o->Wanted = wanted;
        f->References++;
        if (wanted == 0)
            o->Phase = ReadyPhase;
        *token = OpToken(m, o);
        return Status.Pending;
    }

    /// <summary>Internal resumable directory scan, not a public filesystem
    /// listing capability. Each operation owns separate position/VFAT/history.
    /// Entry output is explicitly consumed with CopyEntry; short output does
    /// not consume an entry or reset work. Cancel or EndOfDirectory releases it.</summary>
    public static Status BeginDirectory(Mount* m, FileToken directory, OperationToken* token)
    {
        Status live = RequireLive(m);
        if (live != Status.Success)
            return live;
        if (!External(m, (ulong)token, (ulong)sizeof(OperationToken), 8))
            return Status.InvalidStorage;
        if (!File(m, directory, out FileHandle* f))
            return Status.StaleToken;
        if (!f->Info.IsDirectory)
            return Status.NotDirectory;
        Status reserved = ReserveOperation(m, ScanKind, out Operation* o);
        if (reserved != Status.Success)
            return reserved;
        o->FileIndex = directory.Index;
        f->References++;
        InitCursor(o, f->Info.FirstCluster);
        *token = OpToken(m, o);
        return Status.Pending;
    }

    /// <summary>One cooperative bounded step. Busy means another operation owns
    /// the single block lease. Ready/Entry are retained, not copies. Errors remain
    /// owned until Cancel, so callers can distinguish corruption, budget and I/O
    /// failure from EOF. No syscall thread or global IRQ-off disk spin is supplied.</summary>
    public static Status Poll(Mount* m, OperationToken token)
    {
        if (!Bound(m))
            return Status.InvalidState;
        if (m->Phase is not (Mounted or Stopping))
            return Status.InvalidState;
        if (!Op(m, token, out Operation* o))
            return Status.StaleToken;
        return Pump(m, o);
    }

    /// <summary>Publish a fully validated open result and consume its operation.
    /// Output must be aligned, mapped exclusive ordinary RAM outside all retained
    /// storage. Failure leaves output untouched and preserves a ready result.</summary>
    public static Status CopyOpen(Mount* m, OperationToken token, OpenResult* result)
    {
        Status live = RequireLive(m);
        if (live != Status.Success)
            return live;
        if (!External(m, (ulong)result, (ulong)sizeof(OpenResult), 8))
            return Status.InvalidStorage;
        if (!Op(m, token, out Operation* o))
            return Status.StaleToken;
        if (o->Kind != OpenKind || o->Phase != ReadyPhase)
            return o->Phase == Failed ? o->Failure : Status.Pending;
        FileHandle* f = m->Input.Files + o->FileIndex;
        f->Phase = ReadyPhase;
        PublishFileChain(m, f);
        *result = new()
        {
            File = new((ulong)m, m->Input.Epoch, f->Generation, o->FileIndex),
            Info = f->Info
        };
        o->FileIndex = ~0U;
        Release(m, o);
        return Status.Success;
    }

    /// <summary>Copy only a complete validated requested range, then consume
    /// the operation. Short output preserves Ready. The byte-count pointer and
    /// destination are separate mapped ordinary RAM and alias-checked before
    /// any write. Failed I/O publishes no partial range and never becomes EOF.</summary>
    public static Status CopyRead(Mount* m, OperationToken token, Span<byte> destination, uint* bytes)
    {
        Status live = RequireLive(m);
        if (live != Status.Success)
            return live;
        if (!External(m, (ulong)bytes, 4, 4))
            return Status.InvalidStorage;
        if (!Op(m, token, out Operation* o))
            return Status.StaleToken;
        if (o->Kind != ReadKind || o->Phase != ReadyPhase)
            return o->Phase == Failed ? o->Failure : Status.Pending;
        if (destination.Length < o->Wanted)
            return Status.ShortBuffer;
        fixed (byte* p = destination)
        {
            if (o->Wanted != 0 && (!External(m, (ulong)p, o->Wanted, 1) || Overlap((ulong)p, o->Wanted, (ulong)bytes, 4)))
                return Status.InvalidStorage;
            for (uint i = 0; i < o->Wanted; i++)
                p[i] = o->Storage.Range[i];
        }
        *bytes = o->Wanted;
        bool eof = o->Wanted == 0 && o->Offset == m->Input.Files[o->FileIndex].Info.Length;
        Release(m, o);
        return eof ? Status.EndOfFile : Status.Success;
    }

    /// <summary>Publish the pending ASCII canonical name (including a trailing
    /// NUL) and metadata, then advance. OutputFull preserves the pending entry,
    /// name, physical position and cumulative budgets for a later caller batch.</summary>
    public static Status CopyEntry(Mount* m, OperationToken token, Metadata* result, Span<byte> name)
    {
        Status live = RequireLive(m);
        if (live != Status.Success)
            return live;
        if (!External(m, (ulong)result, (ulong)sizeof(Metadata), 8))
            return Status.InvalidStorage;
        if (!Op(m, token, out Operation* o))
            return Status.StaleToken;
        if (o->Kind != ScanKind || !o->Cursor.HasPending)
            return o->Phase == Failed ? o->Failure : Status.Pending;
        uint count = (uint)o->Cursor.NameLength + 1;
        if (name.Length < count)
            return Status.OutputFull;
        fixed (byte* p = name)
        {
            if (!External(m, (ulong)p, count, 1) || Overlap((ulong)p, count, (ulong)result, (ulong)sizeof(Metadata)))
                return Status.InvalidStorage;
            for (uint i = 0; i + 1 < count; i++)
                p[i] = (byte)o->Storage.Name[i];
            p[count - 1] = 0;
        }
        *result = o->Cursor.Pending;
        o->Cursor.HasPending = false;
        return Status.Success;
    }

    /// <summary>Revoke an operation's publication permission immediately.
    /// Outstanding block work must still drain through Poll; Pending retains
    /// scratch. An operation without DMA releases immediately and becomes stale.</summary>
    public static Status Cancel(Mount* m, OperationToken token)
    {
        if (!Bound(m) || m->Phase == Retired)
            return Status.InvalidState;
        if (!Op(m, token, out Operation* o))
            return Status.StaleToken;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null && !RetainedBorrowed(m))
            return Status.DeviceFailed;
#endif
        o->Phase = Cancelling;
        if (m->IoActive && m->IoIndex == o->Index && m->IoGeneration == o->Generation)
        {
#if !NOSTDLIB_EFI || M16_027_PROBE
            if (m->ReadPort != null)
            {
                var status = Port.Cancel(m->ReadPort, m->PortIo);
                if (status == Port.Status.Cancelled)
                {
                    m->IoActive = false;
                    o->Waiting = 0;
                    Release(m, o);
                    return Status.Cancelled;
                }
                return status == Port.Status.Pending ? Status.Pending : DeviceFault(m);
            }
#endif
            bool ready = m->Input.Device->ReadReady;
            if (Block.Cancel(m->Input.Device, m->IoToken) != Block.Status.Success)
                return DeviceFault(m);
            if (ready)
            {
                m->IoActive = false;
                o->Waiting = 0;
                Release(m, o);
                return Status.Cancelled;
            }
            return Status.Pending;
        }
        Release(m, o);
        return Status.Cancelled;
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
        if (live != Status.Success && !(live == Status.Busy && m->ReadPort != null))
            return live;
#else
        if (live != Status.Success)
            return live;
#endif
        if (!File(m, token, out FileHandle* f))
            return Status.StaleToken;
        if (f->References != 0)
            return Status.Busy;
        f->Phase = Free;
        f->ClusterCount = 0;
        return Status.Success;
    }

    /// <summary>Revoke all cache/handle publication and drain at most one block
    /// observation per call. This owner never resets a borrowed device. If it
    /// faults, preserve backing until the platform has actually stopped it;
    /// only its retired receipt permits final local release. Retired is idempotent.</summary>
    public static Status Stop(Mount* m)
    {
        if (!Bound(m))
            return Status.InvalidState;
        if (m->Phase == Retired)
            return Status.Success;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
            return StopBorrowed(m);
#endif
        m->Phase = Stopping;
        InvalidateCache(m);
        if (!DeviceBound(m) || (!m->Input.Device->Running && !m->Input.Device->Retired))
            return Status.DeviceFailed;
        if (m->IoActive)
        {
            if (!DeviceBound(m))
                return Status.DeviceFailed;
            if (m->Input.Device->Retired)
                m->IoActive = false;
            else
            {
                Operation* o = m->Input.Operations + m->IoIndex;
                o->Phase = Cancelling;
                bool ready = m->Input.Device->ReadReady;
                if (Block.Cancel(m->Input.Device, m->IoToken) != Block.Status.Success)
                    return Status.DeviceFailed;
                if (ready)
                {
                    m->IoActive = false;
                    o->Waiting = 0;
                    Release(m, o);
                }
                else
                {
                    Status progress = CompleteIo(m, o);
                    if (progress is not (Status.Cancelled or Status.Success))
                        return progress;
                }
            }
        }
        return ReleaseAll(m);
    }

    private static Status ReleaseAll(Mount* m)
    {
        for (uint i = 0; i < m->Input.OperationCount; i++)
            if (m->Input.Operations[i].Phase != Free)
                Release(m, m->Input.Operations + i);
        for (uint i = 0; i < m->Input.FileCount; i++)
        {
            m->Input.Files[i].Phase = Free;
            m->Input.Files[i].ClusterCount = 0;
        }
        m->Phase = Retired;
        return Status.Success;
    }

#if !NOSTDLIB_EFI || M16_027_PROBE
    private static Status StopBorrowed(Mount* m)
    {
        if (!Shape(m->Input) || !Storage((ulong)m->ReadPort, (ulong)sizeof(Port.State), 8))
            return Status.DeviceFailed;
        Port.Inspection facts = default;
        var inspection = Port.Inspect(m->ReadPort, m->PortBinding.Epoch, &facts);
        if (inspection == Port.Status.Retired)
        {
            // Only local retirement is used: no released C/B pointer is read,
            // no bytes are published and no retained-backend authority follows.
            Extent* own = stackalloc Extent[68];
            uint count = Extents(m, m->Input, own);
            if (!Disjoint(own, count))
                return Status.DeviceFailed;
            for (uint i = 0; i < count; i++)
                if (Overlap(own[i].Start, own[i].Bytes, (ulong)m->ReadPort, (ulong)sizeof(Port.State)))
                    return Status.DeviceFailed;
            m->Phase = Stopping;
            InvalidateCache(m);
            m->IoActive = false;
            return ReleaseAll(m);
        }
        if (!RetainedBorrowed(m))
            return Status.DeviceFailed;
        m->Phase = Stopping;
        InvalidateCache(m);
        if (m->IoActive)
        {
            Operation* o = m->Input.Operations + m->IoIndex;
            o->Phase = Cancelling;
            var cancelled = Port.Cancel(m->ReadPort, m->PortIo);
            if (cancelled == Port.Status.Cancelled)
            {
                m->IoActive = false;
                o->Waiting = 0;
                Release(m, o);
            }
            else if (cancelled == Port.Status.Pending)
            {
                Status progress = CompleteIo(m, o);
                if (progress != Status.Cancelled)
                    return progress;
            }
            else
                return Status.DeviceFailed;
        }
        return ReleaseAll(m);
    }
#endif

    private static Status Pump(Mount* m, Operation* o)
    {
        if (o->Phase == Failed)
            return o->Failure;
        Status available = BackendStatus(m);
        if (available == Status.Busy)
            return Status.Busy;
        if (available != Status.Success)
            return Fail(m, o, DeviceFault(m));
        if (o->Phase == ReadyPhase)
            return Status.Ready;
        if (o->Cursor.HasPending && o->Kind == ScanKind)
            return Status.Entry;
        if (o->Waiting != 0)
            return CompleteIo(m, o);
        if (o->Phase == Cancelling)
        {
            Release(m, o);
            return Status.Cancelled;
        }
        if (o->Kind == MountKind && !m->Volume.Succeeded)
            return ReadSector(m, o, m->Input.PartitionFirst, BpbIo);
        if (o->Kind is MountKind or OpenKind)
        {
            if (!o->LookupDone)
                return LookupStep(m, o);
            if (o->Kind == MountKind)
            {
                if (!o->Candidate.IsDirectory)
                    return Fail(m, o, Status.NotDirectory);
                m->Root = o->Candidate;
                m->Phase = Mounted;
                Release(m, o);
                return Status.Ready;
            }
            return FileChainStep(m, o);
        }
        if (o->Kind == ReadKind)
            return ReadStep(m, o);
        Status entry = DirectoryStep(m, o);
        if (entry == Status.EndOfDirectory)
            Release(m, o);
        return entry;
    }

    private static Status ReserveOperation(Mount* m, byte kind, out Operation* result)
    {
        result = null;
        bool exhausted = false;
        for (uint i = 0; i < m->Input.OperationCount; i++)
        {
            Operation* o = m->Input.Operations + i;
            if (o->Phase != Free)
                continue;
            if (o->Generation == ~0UL)
            {
                exhausted = true;
                continue;
            }
            Begin(o, kind);
            result = o;
            return Status.Success;
        }
        return exhausted ? Status.GenerationExhausted : Status.Full;
    }
    private static void Begin(Operation* o, byte kind)
    {
        Scratch scratch = o->Storage;
        ulong self = o->Self, epoch = o->Epoch, generation = o->Generation;
        uint index = o->Index;
        *o = default;
        o->Storage = scratch;
        o->Self = self;
        o->Epoch = epoch;
        o->Generation = generation + 1;
        o->Index = index;
        o->Kind = kind;
        o->Phase = Working;
        o->FileIndex = ~0U;
        o->Failure = Status.Success;
    }
    private static void Release(Mount* m, Operation* o)
    {
        for (uint i = 0; i < m->Input.CacheCount; i++)
        {
            CacheSlot* c = m->Input.Cache + i;
            if (c->Phase == Working && c->OperationIndex == o->Index && c->OperationGeneration == o->Generation)
                c->Phase = Free;
        }
        if (o->FileIndex < m->Input.FileCount)
        {
            FileHandle* f = m->Input.Files + o->FileIndex;
            if (o->Kind == OpenKind && f->Phase == Working)
            {
                InvalidateFileChain(m, f->Clusters);
                f->Phase = Free;
                f->ClusterCount = 0;
            }
            if (o->Kind is ReadKind or ScanKind && f->References != 0)
                f->References--;
        }
        o->Phase = Free;
        o->Waiting = 0;
        o->Cursor.HasPending = false;
    }
    private static Status Fail(Mount* m, Operation* o, Status status)
    {
        o->Phase = Failed;
        o->Failure = status;
        if (o->Kind == MountKind)
        {
            m->Phase = Faulted;
            m->Failure = status;
        }
        return status;
    }
    private static Status DeviceFault(Mount* m)
    {
        InvalidateCache(m);
        m->Phase = Faulted;
        m->Failure = Status.DeviceFailed;
        return Status.DeviceFailed;
    }
    private static void InvalidateCache(Mount* m)
    {
        for (uint i = 0; i < m->Input.CacheCount; i++)
            m->Input.Cache[i].Phase = Free;
        if (m->ImmutableDirectoryCacheBacking != null)
            for (uint i = 0; i < m->ImmutableDirectoryCacheSlots; i++)
                ((ImmutableDirectoryRecord*)m->ImmutableDirectoryCacheBacking)[i].Phase = Free;
        if (m->ImmutableFileChainCacheBacking != null)
            for (uint i = 0; i < m->ImmutableFileChainCacheSlots; i++)
                ((FileChainCertificate*)m->ImmutableFileChainCacheBacking)[i].Phase = Free;
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
        if (!token.IsInitialized || token.Owner != (ulong)m || token.Epoch != m->Input.Epoch || token.Index >= m->Input.OperationCount)
            return false;
        Operation* found = m->Input.Operations + token.Index;
        if (found->Self != (ulong)found || found->Epoch != m->Input.Epoch || found->Generation != token.Generation || found->Phase == Free)
            return false;
        o = found;
        return true;
    }
    private static bool File(Mount* m, FileToken token, out FileHandle* f)
    {
        f = null;
        if (!token.IsInitialized || token.Owner != (ulong)m || token.Epoch != m->Input.Epoch || token.Index >= m->Input.FileCount)
            return false;
        FileHandle* found = m->Input.Files + token.Index;
        if (found->Self != (ulong)found || found->Epoch != m->Input.Epoch || found->Generation != token.Generation || found->Phase != ReadyPhase)
            return false;
        f = found;
        return true;
    }
    private static bool Bound(Mount* m) => Storage((ulong)m, (ulong)sizeof(Mount), 8) && m->Self == (ulong)m && m->Phase is >= Mounting and <= Faulted;
    private static bool DeviceBound(Mount* m)
    {
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
            return RetainedBorrowed(m);
#endif
        return Storage((ulong)m->Input.Device, (ulong)sizeof(Block.State), 8) && m->Input.Device->Self == (ulong)m->Input.Device &&
            m->Input.Device->Input.Epoch == m->DeviceEpoch && m->Input.Device->PoolEpoch == m->PoolEpoch;
    }
    private static Status BackendStatus(Mount* m)
    {
        if (!DeviceBound(m))
            return Status.DeviceFailed;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
        {
            Port.Inspection facts = default;
            var status = Port.Inspect(m->ReadPort, m->PortBinding.Epoch, &facts);
            if (status == Port.Status.Busy)
                return Status.Busy;
            return status == Port.Status.Success && SameBinding(m, facts) ? Status.Success : Status.DeviceFailed;
        }
#endif
        return m->Input.Device->Running ? Status.Success : Status.DeviceFailed;
    }
    private static Status RequireLive(Mount* m)
    {
        if (!Bound(m))
            return Status.InvalidState;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
        {
            if (m->Phase == Faulted)
                return m->Failure;
            return m->Phase == Mounted ? BackendStatus(m) : Status.InvalidState;
        }
#endif
        if (m->Phase != Mounted)
            return Status.InvalidState;
        return DeviceBound(m) && m->Input.Device->Running ? Status.Success : Status.InvalidState;
    }
    private static bool Live(Mount* m) => RequireLive(m) == Status.Success;
    private static ulong BackendCapacity(Mount* m)
    {
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
            return m->PortBinding.SectorCount;
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
            m->Input.PartitionLast != m->PortBinding.LastLba)
            return false;
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
            !Bound(m) || m->Phase == Retired || m->Input.Epoch != epoch || m->ReadPort == null || !RetainedBorrowed(m))
            return false;
        return Port.CanRetainStorageRanges(m->ReadPort, m->PortBinding.Epoch, pairs) && BorrowedRangesOutside(m, pairs);
    }

    // Keep this local-only scratch after RetainedBorrowed/AdmissionOutside and
    // the external port batch returned. No backend query nests inside this
    // additional1088-byte inventory. Emitted stack remains a separate gate.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool BorrowedRangesOutside(Mount* m, ReadOnlySpan<ulong> pairs)
    {
        Extent* all = stackalloc Extent[68];
        uint count = Extents(m, m->Input, all);
        for (int i = 0; i < pairs.Length; i += 2)
        {
            ulong start = pairs[i], bytes = pairs[i + 1];
            if (bytes == 0)
                continue;
            if (!Storage(start, bytes, 1))
                return false;
            for (uint j = 0; j < count; j++)
                if (Overlap(start, bytes, all[j].Start, all[j].Bytes))
                    return false;
        }
        return true;
    }
#endif

    // Admission and every external output use one complete retained extent
    // inventory. Numeric checks do not establish mapping or physical alias proof.
    private static uint Extents(Mount* m, in Admission a, Extent* all)
    {
        uint n = 0;
        all[n++] = new()
        {
            Start = (ulong)m,
            Bytes = (ulong)sizeof(Mount)
        };
        if (m->ImmutableDirectoryCacheBacking != null)
            all[n++] = new()
            {
                Start = (ulong)m->ImmutableDirectoryCacheBacking,
                Bytes = ImmutableDirectoryCacheBytes(m->ImmutableDirectoryCacheSlots)
            };
        if (m->ImmutableFileChainCacheBacking != null)
            all[n++] = new()
            {
                Start = (ulong)m->ImmutableFileChainCacheBacking,
                Bytes = ImmutableFileChainCacheBytes(m->ImmutableFileChainCacheSlots)
            };
        all[n++] = new()
        {
            Start = (ulong)a.Operations,
            Bytes = a.OperationCount * (ulong)sizeof(Operation)
        };
        all[n++] = new()
        {
            Start = (ulong)a.Files,
            Bytes = a.FileCount * (ulong)sizeof(FileHandle)
        };
        all[n++] = new()
        {
            Start = (ulong)a.Cache,
            Bytes = a.CacheCount * (ulong)sizeof(CacheSlot)
        };
        for (uint i = 0; i < a.OperationCount; i++)
        {
            Scratch s = a.Operations[i].Storage;
            all[n++] = new()
            {
                Start = (ulong)s.Path,
                Bytes = 256
            };
            all[n++] = new()
            {
                Start = (ulong)s.Sector,
                Bytes = 512
            };
            all[n++] = new()
            {
                Start = (ulong)s.Name,
                Bytes = 520
            };
            all[n++] = new()
            {
                Start = (ulong)s.History,
                Bytes = s.HistoryCapacity * 4UL
            };
            all[n++] = new()
            {
                Start = (ulong)s.Range,
                Bytes = s.RangeCapacity
            };
        }
        for (uint i = 0; i < a.FileCount; i++)
            all[n++] = new()
            {
                Start = (ulong)a.Files[i].Clusters,
                Bytes = a.Files[i].ClusterCapacity * 4UL
            };
        for (uint i = 0; i < a.CacheCount; i++)
            all[n++] = new()
            {
                Start = (ulong)a.Cache[i].Data,
                Bytes = 512
            };
        return n;
    }
    private static bool Admit(Mount* m, in Admission a)
    {
        if (!Storage((ulong)a.Device, (ulong)sizeof(Block.State), 8) ||
            a.Device->Self != (ulong)a.Device || !a.Device->Running || a.Device->ReadPending || a.Device->ReadReady ||
            a.PartitionLast >= a.Device->SectorCount || !AdmitSlots(m, a))
            return false;
        Extent* all = stackalloc Extent[68];
        uint count = Extents(m, a, all);
        if (!Disjoint(all, count))
            return false;
        for (uint i = 0; i < count; i++)
            if (!OutsideDevice(a.Device, all[i].Start, all[i].Bytes))
                return false;
        return true;
    }
    private static bool Shape(in Admission a)
    {
        if (a.Epoch == 0 || a.PartitionFirst == 0 || a.PartitionFirst > a.PartitionLast ||
            a.OperationCount is < 2 or > MaximumSlots || a.FileCount is < 2 or > MaximumSlots || a.CacheCount is 0 or > MaximumCacheSlots ||
            a.DirectoryEntryBudget is 0 or > 1048576 ||
            !Storage((ulong)a.Operations, a.OperationCount * (ulong)sizeof(Operation), 8) ||
            !Storage((ulong)a.Files, a.FileCount * (ulong)sizeof(FileHandle), 8) ||
            !Storage((ulong)a.Cache, a.CacheCount * (ulong)sizeof(CacheSlot), 8))
            return false;
        for (uint i = 0; i < a.OperationCount; i++)
            if (a.Operations[i].Storage.HistoryCapacity is 0 or > MaximumClusters ||
                a.Operations[i].Storage.RangeCapacity is 0 or > MaximumRangeBytes)
                return false;
        for (uint i = 0; i < a.FileCount; i++)
            if (a.Files[i].ClusterCapacity is 0 or > MaximumClusters)
                return false;
        return true;
    }
    private static bool AdmitSlots(Mount* m, in Admission a)
    {
        if (!Storage((ulong)m, (ulong)sizeof(Mount), 8) || !Shape(a))
            return false;
        for (uint i = 0; i < (uint)sizeof(Mount); i++)
            if (((byte*)m)[i] != 0)
                return false;
        for (uint i = 0; i < a.OperationCount; i++)
        {
            Operation* o = a.Operations + i;
            if (o->Self != 0 || o->Epoch != 0 || o->Generation != 0 || o->Phase != 0 || o->Kind != 0 ||
                o->Storage.HistoryCapacity is 0 or > MaximumClusters || o->Storage.RangeCapacity is 0 or > MaximumRangeBytes)
                return false;
        }
        for (uint i = 0; i < a.FileCount; i++)
            if (a.Files[i].Self != 0 || a.Files[i].Epoch != 0 || a.Files[i].Generation != 0 || a.Files[i].Phase != 0 || a.Files[i].References != 0 ||
                a.Files[i].ClusterCount != 0 || a.Files[i].ClusterCapacity is 0 or > MaximumClusters)
                return false;
        for (uint i = 0; i < a.CacheCount; i++)
            if (a.Cache[i].Epoch != 0 || a.Cache[i].Phase != 0)
                return false;
        return true;
    }
    private static bool Disjoint(Extent* all, uint count)
    {
        for (uint i = 0; i < count; i++)
        {
            if (!Storage(all[i].Start, all[i].Bytes, 8))
                return false;
            for (uint j = 0; j < i; j++)
                if (Overlap(all[i].Start, all[i].Bytes, all[j].Start, all[j].Bytes))
                    return false;
        }
        return true;
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
    private static bool AdmissionOutside(Mount* m, in Admission a, Port.State* port, ulong epoch)
    {
        Extent* all = stackalloc Extent[68];
        uint count = Extents(m, a, all);
        if (!Disjoint(all, count))
            return false;
        return Port.CanRetainStorageRanges(port, epoch, new ReadOnlySpan<ulong>((ulong*)all, (int)count * 2));
    }
#endif
    private static bool External(Mount* m, ulong start, ulong bytes, ulong alignment)
    {
        if (!Storage(start, bytes, alignment))
            return false;
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
        {
            if (!RetainedBorrowed(m) || !Port.CanRetainStorage(m->ReadPort, m->PortBinding.Epoch, start, bytes))
                return false;
        }
        else
#endif
            if (!OutsideDevice(m->Input.Device, start, bytes))
                return false;
        Extent* all = stackalloc Extent[68];
        uint count = Extents(m, m->Input, all);
        for (uint i = 0; i < count; i++)
            if (Overlap(start, bytes, all[i].Start, all[i].Bytes))
                return false;
        return true;
    }
    private static bool OutsideDevice(Block.State* d, ulong start, ulong bytes)
    {
        var a = d->Input;
        DmaBufferPool* p = a.Pool;
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
