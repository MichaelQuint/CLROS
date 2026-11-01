using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Port = OS.Storage.WritableBlockReadPort;
#endif

namespace OS.Storage;

public static unsafe partial class ReadOnlyWebRootFileSystem
{
    private static Status ReadSector(Mount* m, Operation* o, ulong lba, byte action, uint sectors = 1)
    {
        if (sectors is 0 or > 4 || (sectors != 1 && action != DataIo) ||
            lba < m->Input.PartitionFirst || lba > m->Input.PartitionLast ||
            sectors - 1 > m->Input.PartitionLast - lba) return Fail(m, o, Status.CorruptFile);
        if (m->IoActive) return Status.Busy;
        if (m->ImmutableDirectoryCacheBacking != null && action == DirectoryIo &&
            m->Phase == Mounted && o->Kind != MountKind)
        {
            lba = TryCopyImmutableDirectorySector(m, o, lba);
            if (lba == 0) return Status.Pending;
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

    // Only ReadSector calls this after its nonzero partition-bounded LBA,
    // idle physical owner and immutable mounted-directory eligibility checks.
    // Zero denotes a completed hit; a miss returns the original nonzero LBA.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong TryCopyImmutableDirectorySector(Mount* m, Operation* o, ulong lba)
    {
        uint remaining = m->ImmutableDirectoryCacheSlots;
        var record = (ImmutableDirectoryRecord*)m->ImmutableDirectoryCacheBacking;
        byte* data = (byte*)(record + remaining);
        for (; remaining != 0; remaining--, record++, data += 512)
        {
            if (record->Phase != ReadyPhase || record->MountOwner != (ulong)m ||
                record->MountEpoch != m->Input.Epoch || record->DeviceEpoch != m->DeviceEpoch ||
                record->PoolEpoch != m->PoolEpoch || record->Lba != lba) continue;
            for (uint n = 0; n < 512; n++) o->Storage.Sector[n] = data[n];
            o->Cursor.SectorReady = true; o->Cursor.EntryInSector = 0; return 0;
        }
        return lba;
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
}
