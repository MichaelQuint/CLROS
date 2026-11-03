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

public static unsafe partial class ReadOnlyWebRootFileSystem
{
    private static Status FileChainStep(Mount* m, Operation* o)
    {
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
            return FileChainOne(m, o);
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
            f->Info = o->Candidate;
            o->HaveCandidate = true;
            if (f->Info.IsDirectory || f->Info.Length == 0)
            {
                if (!f->Info.IsDirectory && f->Info.FirstCluster != 0)
                    return Fail(m, o, Status.CorruptFile);
                o->Phase = ReadyPhase;
                return Status.Ready;
            }
            ulong clusterBytes = (ulong)m->Volume.SectorsPerCluster * 512;
            o->RequiredClusters = (uint)(((ulong)f->Info.Length + clusterBytes - 1) / clusterBytes);
            if (o->RequiredClusters > f->ClusterCapacity)
                return Fail(m, o, Status.TraversalBudgetExceeded);
            if (TryCopyFileChain(m, f, o->RequiredClusters))
            {
                o->Phase = ReadyPhase;
                return Status.Ready;
            }
            InvalidateFileChain(m, f->Clusters);
            var visit = Chain.Visit(m->Volume, f->Info.FirstCluster, new Span<uint>(f->Clusters, (int)f->ClusterCapacity), ref f->ClusterCount);
            if (visit != LiveFat32Status.Success)
                return Fail(m, o, Map(visit));
            return Status.Pending;
        }
        Status status = Fat(m, o, f->Clusters[f->ClusterCount - 1], out uint next, out bool end);
        if (status != Status.Success)
            return status;
        if (end)
        {
            var length = Chain.CheckLength(f->ClusterCount, o->RequiredClusters);
            if (length != LiveFat32Status.Success)
                return Fail(m, o, Map(length));
            o->Phase = ReadyPhase;
            return Status.Ready;
        }
        if (f->ClusterCount == o->RequiredClusters)
        {
            for (uint i = 0; i < f->ClusterCount; i++)
                if (f->Clusters[i] == next)
                    return Fail(m, o, Status.ChainLoop);
            return Fail(m, o, Status.Overlong);
        }
        var result = Chain.Visit(m->Volume, next, new Span<uint>(f->Clusters, (int)f->ClusterCapacity), ref f->ClusterCount);
        if (result != LiveFat32Status.Success)
            return Fail(m, o, Map(result));
        return f->ClusterCount > o->RequiredClusters ? Fail(m, o, Status.CorruptFile) : Status.Pending;
    }

    private static bool TryCopyFileChain(Mount* m, FileHandle* f, uint required)
    {
        if (m->ImmutableFileChainCacheBacking == null)
            return false;
        for (uint i = 0; i < m->ImmutableFileChainCacheSlots; i++)
        {
            var c = (FileChainCertificate*)m->ImmutableFileChainCacheBacking + i;
            if (c->Phase != ReadyPhase || c->Epoch != m->Input.Epoch || c->Count != required ||
                c->Info.Inode != f->Info.Inode || c->Info.ModifiedSeconds != f->Info.ModifiedSeconds ||
                c->Info.FirstCluster != f->Info.FirstCluster || c->Info.Length != f->Info.Length ||
                c->Info.NameBytes != f->Info.NameBytes || c->Info.Attributes != f->Info.Attributes)
                continue;
            if (f->Clusters != c->Clusters)
            {
                InvalidateFileChain(m, f->Clusters);
                for (uint j = 0; j < required; j++)
                    f->Clusters[j] = c->Clusters[j];
            }
            f->ClusterCount = required;
            return true;
        }
        return false;
    }

    private static void PublishFileChain(Mount* m, FileHandle* f)
    {
        if (m->ImmutableFileChainCacheBacking == null || f->Info.IsDirectory || f->Info.Length == 0)
            return;
        uint slot = 0;
        for (uint i = 0; i < m->ImmutableFileChainCacheSlots; i++)
        {
            var existing = (FileChainCertificate*)m->ImmutableFileChainCacheBacking + i;
            if (existing->Phase == Free || existing->Clusters == f->Clusters)
            {
                slot = i;
                break;
            }
        }
        var c = (FileChainCertificate*)m->ImmutableFileChainCacheBacking + slot;
        c->Phase = Free;
        c->Epoch = m->Input.Epoch;
        c->Info = f->Info;
        c->Clusters = f->Clusters;
        c->Count = f->ClusterCount;
        c->Phase = ReadyPhase;
    }

    private static void InvalidateFileChain(Mount* m, uint* clusters)
    {
        if (m->ImmutableFileChainCacheBacking != null)
            for (uint i = 0; i < m->ImmutableFileChainCacheSlots; i++)
            {
                var c = (FileChainCertificate*)m->ImmutableFileChainCacheBacking + i;
                if (c->Clusters == clusters)
                    c->Phase = Free;
            }
    }

    private static Status Fat(Mount* m, Operation* o, uint cluster, out uint next, out bool end)
    {
        next = 0;
        end = false;
        ulong check = 0;
        if (Chain.ClusterFirstLba(m->Volume, cluster, ref check) != LiveFat32Status.Success)
            return Fail(m, o, Status.CorruptFile);
        ulong lba0 = m->Volume.FirstFatLba + cluster / 128, lba1 = lba0 + m->Volume.FatSectors;
        CacheSlot* slot = null;
        if (o->FatStage != 0)
        {
            slot = m->Input.Cache + o->FatSlot;
            if (o->FatCluster != cluster || slot->Epoch != m->Input.Epoch || slot->Lba0 != lba0 || slot->Lba1 != lba1)
                return Fail(m, o, Status.InvalidState);
            if (o->FatStage == 1)
                return ReadSector(m, o, lba0, Fat0Io);
            if (o->FatStage == 2)
                return ReadSector(m, o, lba1, Fat1Io);
            if (slot->Phase != Working || slot->OperationIndex != o->Index || slot->OperationGeneration != o->Generation)
                return Fail(m, o, Status.InvalidState);
        }
        else
        {
            for (uint i = 0; i < m->Input.CacheCount; i++)
            {
                CacheSlot* candidate = m->Input.Cache + i;
                if (candidate->Phase == Free || candidate->Epoch != m->Input.Epoch || candidate->Lba0 != lba0 || candidate->Lba1 != lba1)
                    continue;
                if (candidate->Phase != ReadyPhase)
                    return Status.Busy;
                slot = candidate;
                m->Hits++;
                break;
            }
            if (slot == null)
            {
                uint index = m->NextVictim;
                for (uint n = 0; n < m->Input.CacheCount; n++)
                {
                    uint at = (index + n) % m->Input.CacheCount;
                    if (m->Input.Cache[at].Phase == Working)
                        continue;
                    slot = m->Input.Cache + at;
                    o->FatSlot = at;
                    m->NextVictim = (at + 1) % m->Input.CacheCount;
                    break;
                }
                if (slot == null)
                    return Status.Busy;
                slot->Phase = Working;
                slot->Epoch = m->Input.Epoch;
                slot->Lba0 = lba0;
                slot->Lba1 = lba1;
                slot->OperationIndex = o->Index;
                slot->OperationGeneration = o->Generation;
                o->FatCluster = cluster;
                o->FatStage = 1;
                m->Misses++;
                return ReadSector(m, o, lba0, Fat0Io);
            }
        }
        uint value = 0;
        var parsed = Chain.ReadFatEntry(m->Volume, cluster, lba0, new Span<byte>(slot->Data, 512), BlockReadResult.Success(1, 512), ref value);
        o->FatStage = 0;
        if (parsed is not (LiveFat32Status.Success or LiveFat32Status.EndOfChain))
        {
            slot->Phase = Free;
            return Fail(m, o, Map(parsed));
        }
        slot->Phase = ReadyPhase;
        if (parsed == LiveFat32Status.EndOfChain)
        {
            end = true;
            return Status.Success;
        }
        next = value;
        return Status.Success;
    }

}
