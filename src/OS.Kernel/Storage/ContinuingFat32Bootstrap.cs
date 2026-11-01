using OS.Std.Binary;
using B = OS.Storage.WritableBlockSession;
using Gpt = OS.Storage.PrimaryGPTIndependentlyBoundsCRC;
using Fat = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;

namespace OS.Storage;

/// <summary>One supervisor borrows the existing exclusive writable block owner
/// to validate primary GPT, one BasicData partition and its primary FAT32 BPB.
/// All state/scratch/device/pool/observer/MMIO backing stays exclusive and stable
/// through local Stop; no user pointer, managed reference or span is retained.
/// Each Pump submits one IN OR consumes its completion/copy, never a next request.
/// This is numeric bootstrap, not a mounted filesystem: no backup/fallback,
/// FSInfo consistency, whole-FAT equality, reachability or recovery claim follows.
/// No queue setup, OUT, FLUSH, reset or device release occurs. The caller pumps
/// other services/deadlines, then mounts through ContinuingFat32Mutation.Start.</summary>
public static unsafe partial class ContinuingFat32Bootstrap
{
    public const uint SectorBytes = 512, EntryBytes = Gpt.ArrayBytes, MaximumReads = 3 + Gpt.ArraySectors;
    public const ulong BasicDataLow = 0x4433b9e5ebd0a0a2UL, BasicDataHigh = 0xc79926b7b668c087UL;
    /// <summary>Admit a fresh bounded bootstrap without I/O. Invalid storage
    /// touches neither State nor scratch; another block request is Busy. No
    /// device ownership is transferred or second queue owner established.</summary>
    public static Status Start(State* s, in Admission admission)
    {
        Admission a = admission;
        if (a.SectorBudget is 0 or > MaximumReads) return Status.InvalidBudget;
        if (a.Epoch == 0 || !Storage((ulong)s, (ulong)sizeof(State)) || !Storage((ulong)a.Device, (ulong)sizeof(B.State)) ||
            !B.CanAdmitStorage(a.Device, a.DeviceEpoch)) return Status.InvalidDevice;
        if (!Extents(s, a)) return Status.InvalidStorage;
        for (uint i = 0; i < sizeof(State); i++) if (((byte*)s)[i] != 0) return Status.InvalidState;
        if (a.Device->RequestPending) return Status.Busy;
        s->Self = (ulong)s; s->Input = a; s->DeviceOwner = (ulong)a.Device; s->PoolEpoch = a.Device->PoolEpoch;
        s->Phase = Life.Working; s->Next = Step.Mbr; s->Failure = Status.Success;
        Clear(a.Sector, SectorBytes); Clear(a.Entries, EntryBytes); return Status.Pending;
    }

    /// <summary>One bounded IN submission, completion/copy, or metadata step.
    /// Sample the block observation after every call. Errors never grant partial
    /// geometry or silently use a backup; device faults require outer retirement.</summary>
    public static Status Pump(State* s)
    {
        if (!Bound(s) || s->Phase is Life.Consumed or Life.Retired) return Status.InvalidState;
        if (s->Phase == Life.Ready) return Status.Ready;
        if (s->Phase is Life.Faulted or Life.Cancelled) return s->Failure;
        if (!Retained(s)) return Fail(s, Status.InvalidStorage);
        if (!B.CanAdmitStorage(s->Input.Device, s->Input.DeviceEpoch)) return Fail(s, Status.InvalidDevice);
        if (s->IoActive) return Complete(s);
        if (s->CancelRequested) return Fail(s, Status.Cancelled);
        if (s->Next == Step.Select) return Select(s);
        if (s->Reads == s->Input.SectorBudget) return Fail(s, Status.BudgetExceeded);
        ulong sector = s->Next == Step.Mbr ? 0 : s->Next == Step.Header ? 1 : s->Next == Step.Entries ? s->Header.EntryLba + s->EntrySector : s->First;
        ulong before = s->Input.Device->Submitted;
        var status = B.SubmitRead(s->Input.Device, sector, &s->Io);
        // Publication can precede a failed fence/notify and returned token.
        if (s->Input.Device->Submitted != before) s->Reads++;
        if (status != B.Status.Success) return BlockFail(s, status);
        s->IoSector = sector; s->IoActive = true; return Status.Pending;
    }

    private static Status Complete(State* s)
    {
        B.Completion completion = default; var status = B.Poll(s->Input.Device, &completion);
        if (status == B.Status.Pending) return Status.Pending;
        if (status is not (B.Status.Ready or B.Status.Cancelled)) return BlockFail(s, status);
        if (completion.Token.Owner != s->Io.Owner || completion.Token.Epoch != s->Io.Epoch || completion.Token.Generation != s->Io.Generation ||
            completion.Kind != B.Operation.Read || completion.Sector != s->IoSector || completion.DeviceResult != B.Status.Success || completion.Bytes != SectorBytes)
            return BlockFail(s, B.Status.InvalidCompletion);
        if (status == B.Status.Cancelled) { s->IoActive = false; s->Io = default; return Fail(s, Status.Cancelled); }
        status = B.CopyRead(s->Input.Device, s->Io, new Span<byte>(s->Input.Sector, (int)SectorBytes));
        if (status != B.Status.Success) return BlockFail(s, status);
        s->IoActive = false; s->Io = default;
        var sector = new Span<byte>(s->Input.Sector, (int)SectorBytes); var read = BlockReadResult.Success(1, SectorBytes);
        switch (s->Next)
        {
            case Step.Mbr:
                if (!LBAN0Guest.Validate(sector, s->Input.Device->SectorCount, read).Succeeded) return Fail(s, Status.InvalidMbr);
                s->Next = Step.Header; break;
            case Step.Header:
                s->Header = Gpt.ValidateHeader(sector, s->Input.Device->SectorCount, read); s->GptError = s->Header.Status;
                if (!s->Header.Succeeded) return Fail(s, Status.InvalidGpt);
                s->Next = Step.Entries; break;
            case Step.Entries:
                for (uint i = 0; i < SectorBytes; i++) s->Input.Entries[s->EntrySector * SectorBytes + i] = sector[(int)i];
                if (++s->EntrySector == Gpt.ArraySectors) s->Next = Step.Select;
                break;
            case Step.Bpb:
                s->FatError = Fat.ParseBpb(sector, s->Input.Device->SectorCount, s->First, s->Last, read, ref s->Volume);
                if (s->FatError != LiveFat32Status.Success) return Fail(s, Status.InvalidVolume);
                s->Phase = Life.Ready; return Status.Ready;
        }
        return Status.Pending;
    }
    private static Status Select(State* s)
    {
        s->GptError = Gpt.ValidateArray(s->Header, new Span<byte>(s->Input.Entries, (int)EntryBytes), out _);
        if (s->GptError != LiveGptStatus.Success) return Fail(s, Status.InvalidGpt);
        uint found = 0;
        for (uint i = 0; i < Gpt.EntryCount; i++)
        {
            byte* entry = s->Input.Entries + i * Gpt.EntrySize;
            if (EndianPrimitives.ReadUInt64LittleEndian(entry) != BasicDataLow || EndianPrimitives.ReadUInt64LittleEndian(entry + 8) != BasicDataHigh) continue;
            s->Selected = i; found++;
        }
        if (found != 1) return Fail(s, found == 0 ? Status.MissingPartition : Status.AmbiguousPartition);
        byte* selected = s->Input.Entries + s->Selected * Gpt.EntrySize;
        if (EndianPrimitives.ReadUInt64LittleEndian(selected + 48) != 0) return Fail(s, Status.UnsupportedAttributes);
        s->First = EndianPrimitives.ReadUInt64LittleEndian(selected + 32); s->Last = EndianPrimitives.ReadUInt64LittleEndian(selected + 40);
        s->PartitionLow = EndianPrimitives.ReadUInt64LittleEndian(selected + 16); s->PartitionHigh = EndianPrimitives.ReadUInt64LittleEndian(selected + 24);
        s->Next = Step.Bpb; return Status.Pending;
    }

    /// <summary>Copy/consume only complete primary geometry into exclusive
    /// ordinary RAM. Invalid aliases write nothing; other failure outputs are
    /// default. Revalidate original live device before publication; no callback
    /// or other fallible work follows the copy. Caller then mounts through C.</summary>
    public static Status CopyResult(State* s, ulong epoch, Result* result)
    {
        if (!Bound(s) || s->Phase is Life.Retired or Life.Consumed) return Status.InvalidState;
        if (epoch != s->Input.Epoch) return Status.StaleEpoch;
        if (!Retained(s) || !Output(s, (ulong)result, (ulong)sizeof(Result))) return Status.InvalidStorage;
        *result = default;
        if (s->Phase == Life.Working) return Status.Pending;
        if (s->Phase != Life.Ready) return s->Failure;
        if (!B.CanAdmitStorage(s->Input.Device, s->Input.DeviceEpoch) || s->Input.Device->RequestPending) return Fail(s, Status.InvalidDevice);
        *result = new(s); s->Phase = Life.Consumed; return Status.Success;
    }

    /// <summary>Revoke unpublished geometry and drain an actual pending IN.
    /// Does not reset or retract DMA. Faulted ownership remains for outer Stop.</summary>
    public static Status Cancel(State* s, ulong epoch)
    {
        if (!Bound(s) || s->Phase is Life.Retired or Life.Consumed) return Status.InvalidState;
        if (epoch != s->Input.Epoch) return Status.StaleEpoch;
        if (!Retained(s)) return Status.InvalidStorage;
        if (s->Phase is Life.Faulted or Life.Cancelled) return s->Failure;
        s->CancelRequested = true;
        if (s->IoActive)
        {
            bool ready = s->Input.Device->ReadReady; var status = B.Cancel(s->Input.Device, s->Io);
            if (status != B.Status.Success) return BlockFail(s, status);
            if (!ready) return Status.Pending;
            s->IoActive = false; s->Io = default;
        }
        return Fail(s, Status.Cancelled);
    }

    /// <summary>Retire/scrub local scratch after clean drain or actual original
    /// device retirement. Pending cancellation returns Pending; faults retain
    /// buffers until outer B.Stop succeeds. No device release or recovery occurs.</summary>
    public static Status Stop(State* s)
    {
        if (!Bound(s)) return Status.InvalidState;
        if (s->Phase == Life.Retired) return Status.Success;
        if (!Retained(s)) return Status.InvalidStorage;
        if (s->Input.Device->Retired) { s->IoActive = false; s->Io = default; }
        // Submit can fail AFTER index publication without returning a token.
        // FaultPhase is not an idle queue: require its actual outer retirement.
        else if (!s->Input.Device->Running) return Status.Busy;
        else if (s->IoActive)
        { var cancelled = Cancel(s, s->Input.Epoch); if (s->IoActive) return cancelled == Status.Pending ? Status.Pending : Status.Busy; }
        if (s->Input.Device->RequestPending) return Status.Busy;
        Clear(s->Input.Sector, SectorBytes); Clear(s->Input.Entries, EntryBytes); s->Phase = Life.Retired; return Status.Success;
    }
    private static Status Fail(State* s, Status status)
    { s->Failure = status; s->Phase = status == Status.Cancelled ? Life.Cancelled : Life.Faulted; return status; }
    private static Status BlockFail(State* s, B.Status status) { s->BlockError = status; return Fail(s, Status.BlockFailure); }
    private static bool Bound(State* s) => Storage((ulong)s, (ulong)sizeof(State)) && s->Self == (ulong)s && s->Phase is >= Life.Working and <= Life.Retired;
    private static bool Retained(State* s) => (ulong)s->Input.Device == s->DeviceOwner &&
        Storage((ulong)s->Input.Device, (ulong)sizeof(B.State)) && s->Input.Device->PoolEpoch == s->PoolEpoch && Extents(s, s->Input);
    private static bool Extents(State* s, Admission a) => Storage((ulong)a.Sector, SectorBytes) && Storage((ulong)a.Entries, EntryBytes) &&
        B.CanBorrowStorage(a.Device, a.DeviceEpoch, (ulong)s, (ulong)sizeof(State)) && B.CanBorrowStorage(a.Device, a.DeviceEpoch, (ulong)a.Sector, SectorBytes) &&
        B.CanBorrowStorage(a.Device, a.DeviceEpoch, (ulong)a.Entries, EntryBytes) && !Overlap((ulong)s, (ulong)sizeof(State), (ulong)a.Sector, SectorBytes) &&
        !Overlap((ulong)s, (ulong)sizeof(State), (ulong)a.Entries, EntryBytes) && !Overlap((ulong)a.Sector, SectorBytes, (ulong)a.Entries, EntryBytes);
    private static bool Output(State* s, ulong address, ulong bytes) => Storage(address, bytes) && B.CanBorrowStorage(s->Input.Device, s->Input.DeviceEpoch, address, bytes) &&
        !Overlap(address, bytes, (ulong)s, (ulong)sizeof(State)) && !Overlap(address, bytes, (ulong)s->Input.Sector, SectorBytes) && !Overlap(address, bytes, (ulong)s->Input.Entries, EntryBytes);
    private static bool Storage(ulong address, ulong bytes) => address >= 4096 && (address & 7) == 0 && address < 0x0000800000000000UL && bytes != 0 && bytes <= 0x0000800000000000UL - address;
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => a < b + m && b < a + n;
    private static void Clear(byte* address, uint bytes) { for (uint i = 0; i < bytes; i++) address[i] = 0; }
}
