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
    private static Status LookupStep(Mount* m, Operation* o)
    {
#if !NOSTDLIB_EFI || M16_027_PROBE
        if (m->ReadPort != null)
            return LookupOne(m, o);
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
                if (exact)
                {
                    o->Candidate = o->Cursor.Pending;
                    o->CandidateExact = true;
                }
            }
            o->Cursor.HasPending = false;
            return Status.Pending;
        }
        if (step != Status.EndOfDirectory)
            return step;
        if (o->MatchCount > 1)
            return Fail(m, o, Status.Ambiguous);
        if (!o->CandidateExact)
            return Fail(m, o, Status.NotFound);
        uint after = o->Component + o->ComponentLength;
        if (after == o->PathLength)
        {
            if (o->Kind == OpenKind && o->Candidate.IsDirectory)
            {
                uint depth = 1;
                for (uint i = 13; i < o->PathLength; i++)
                    if (o->Storage.Path[i] == '/')
                        depth++;
                if (depth > 8)
                    return Fail(m, o, Status.InvalidPath);
            }
            o->LookupDone = true;
            return Status.Pending;
        }
        if (!o->Candidate.IsDirectory)
            return Fail(m, o, Status.NotDirectory);
        o->Component = after + 1;
        Component(o);
        uint directory = o->Candidate.FirstCluster;
        o->CandidateExact = false;
        o->MatchCount = 0;
        InitCursor(o, directory);
        return Status.Pending;
    }

    private static Status DirectoryStep(Mount* m, Operation* o)
    {
        if (o->Cursor.HasPending)
            return Status.Entry;
        if (o->Cursor.Ended)
            return Status.EndOfDirectory;
        if (!o->Cursor.Visited)
        {
            var visit = Chain.Visit(m->Volume, o->Cursor.Cluster, new Span<uint>(o->Storage.History, (int)o->Storage.HistoryCapacity), ref o->Cursor.Visits);
            if (visit != LiveFat32Status.Success)
                return Fail(m, o, Map(visit));
            o->Cursor.Visited = true;
        }
        if (!o->Cursor.SectorReady)
        {
            ulong lba = 0;
            if (Chain.ClusterFirstLba(m->Volume, o->Cursor.Cluster, ref lba) != LiveFat32Status.Success)
                return Fail(m, o, Status.CorruptDirectory);
            return ReadSector(m, o, lba + o->Cursor.SectorInCluster, DirectoryIo);
        }
        if (o->Cursor.EntryInSector == 16)
        {
            o->Cursor.SectorReady = false;
            if (o->Cursor.SectorInCluster + 1 < m->Volume.SectorsPerCluster)
            {
                o->Cursor.SectorInCluster++;
                return Status.Pending;
            }
            o->Cursor.SectorInCluster = m->Volume.SectorsPerCluster;
            // Preserve the exhausted sector until both FAT-copy reads finish.
            o->Cursor.SectorReady = true;
            Status fat = Fat(m, o, o->Cursor.Cluster, out uint next, out bool end);
            if (fat != Status.Success)
                return fat;
            if (end)
            {
                if (Vfat.Finish(o->Cursor.Vfat) != LiveDirectoryStatus.End)
                    return Fail(m, o, Status.CorruptDirectory);
                o->Cursor.Ended = true;
                return Status.EndOfDirectory;
            }
            o->Cursor.Cluster = next;
            o->Cursor.SectorInCluster = 0;
            o->Cursor.Visited = false;
            o->Cursor.SectorReady = false;
            return Status.Pending;
        }
        if (o->Work == m->Input.DirectoryEntryBudget)
            return Fail(m, o, Status.TraversalBudgetExceeded);
        o->Work++;
        byte* raw = o->Storage.Sector + o->Cursor.EntryInSector * 32;
        uint physicalEntry = o->Cursor.EntryInSector++;
        var parsed = Vfat.Consume(new Span<byte>(raw, 32), ref o->Cursor.Vfat, new Span<ushort>(o->Storage.Name, 260), out var entry);
        if (parsed is LiveDirectoryStatus.Skipped or LiveDirectoryStatus.LongName)
            return Status.Pending;
        if (parsed == LiveDirectoryStatus.End)
        {
            o->Cursor.Ended = true;
            return Status.EndOfDirectory;
        }
        if (parsed != LiveDirectoryStatus.Entry)
            return Fail(m, o, Status.CorruptDirectory);
        ushort length = entry.NameLength;
        if (length == 0)
        {
            for (uint i = 0; i < 8 && raw[i] != ' '; i++)
                o->Storage.Name[length++] = raw[i];
            if (raw[8] != ' ')
            {
                o->Storage.Name[length++] = '.';
                for (uint i = 8; i < 11 && raw[i] != ' '; i++)
                    o->Storage.Name[length++] = raw[i];
            }
        }
        for (uint i = 0; i < length; i++)
            if (o->Storage.Name[i] is < 33 or > 126)
                return Fail(m, o, Status.UnsupportedEncoding);
        ushort date = EndianPrimitives.ReadUInt16LittleEndian(raw + 24), time = EndianPrimitives.ReadUInt16LittleEndian(raw + 22);
        long seconds = 0; // Project policy: all-zero FAT date/time is unspecified.
        if ((date != 0 || time != 0) && !GregorianUtc.TryUnixSeconds((uint)(1980 + (date >> 9)), (uint)((date >> 5) & 15), (uint)(date & 31),
            (uint)(time >> 11), (uint)((time >> 5) & 63), (uint)((time & 31) * 2), out seconds))
            return Fail(m, o, Status.CorruptDirectory);
        ulong sector = 0;
        if (Chain.ClusterFirstLba(m->Volume, o->Cursor.Cluster, ref sector) != LiveFat32Status.Success)
            return Fail(m, o, Status.CorruptDirectory);
        o->Cursor.Pending = new()
        {
            FirstCluster = entry.FirstCluster,
            Length = entry.ByteCount,
            Attributes = entry.Attributes,
            NameBytes = length,
            ModifiedSeconds = seconds,
            Inode = (sector + o->Cursor.SectorInCluster) * 16 + physicalEntry + 256
        };
        o->Cursor.NameLength = length;
        o->Cursor.HasPending = true;
        return Status.Entry;
    }

    private static bool NameEquals(Operation* o, bool fold)
    {
        if (o->Cursor.NameLength != o->ComponentLength)
            return false;
        for (uint i = 0; i < o->ComponentLength; i++)
        {
            byte a = (byte)o->Storage.Name[i], b = o->Storage.Path[o->Component + i];
            if (fold)
            {
                a = Fold(a);
                b = Fold(b);
            }
            if (a != b)
                return false;
        }
        return true;
    }
    private static bool AliasEquals(Operation* o)
    {
        byte* raw = o->Storage.Sector + (o->Cursor.EntryInSector - 1) * 32;
        byte* name = stackalloc byte[12];
        uint length = 0;
        for (uint i = 0; i < 8 && raw[i] != ' '; i++)
            name[length++] = raw[i];
        if (raw[8] != ' ')
        {
            name[length++] = (byte)'.';
            for (uint i = 8; i < 11 && raw[i] != ' '; i++)
                name[length++] = raw[i];
        }
        if (length != o->ComponentLength)
            return false;
        for (uint i = 0; i < length; i++)
            if (Fold(name[i]) != Fold(o->Storage.Path[o->Component + i]))
                return false;
        return true;
    }
    private static byte Fold(byte value) => value is >= (byte)'a' and <= (byte)'z' ? (byte)(value - 32) : value;
    private static void InitCursor(Operation* o, uint cluster)
    {
        o->Cursor = default;
        o->Cursor.Cluster = cluster;
        o->FatStage = 0;
    }
    private static void Component(Operation* o)
    {
        o->ComponentLength = 0;
        while (o->Component + o->ComponentLength < o->PathLength && o->Storage.Path[o->Component + o->ComponentLength] != '/')
            o->ComponentLength++;
    }

    private static Status PathPolicy(ReadOnlySpan<byte> path)
    {
        if (path.Length is < 12 or >= 256)
            return Status.InvalidPath;
        const string root = "/app/wwwroot";
        for (int i = 0; i < 12; i++)
            if (path[i] != root[i])
                return Status.OutsideRoot;
        if (path.Length == 12)
            return Status.Success;
        if (path[12] != '/')
            return Status.OutsideRoot;
        uint components = 0;
        int start = 13;
        for (int i = start; i <= path.Length; i++)
        {
            if (i != path.Length && path[i] != '/')
            {
                byte c = path[i];
                if (c is < 33 or > 126)
                    return Status.UnsupportedEncoding;
                if (c is (byte)'\\' or (byte)':' or (byte)'*' or (byte)'?' or (byte)'"' or (byte)'<' or (byte)'>' or (byte)'|')
                    return Status.InvalidPath;
                continue;
            }
            int length = i - start;
            if (length == 0 || path[i - 1] is (byte)'.' or (byte)' ' ||
                (length == 1 && path[start] == '.') || (length == 2 && path[start] == '.' && path[start + 1] == '.'))
                return Status.InvalidPath;
            if (++components > 9)
                return Status.InvalidPath;
            start = i + 1;
        }
        return Status.Success;
    }

}
