using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Storage;

/// <summary>One serialized supervisor borrows sector reads from the ORIGINAL
/// writable block owner between complete mutation-engine requests. No second
/// queue, DMA buffer, device setup, reset or Stop authority is acquired. The
/// readonly range must be an independently selected partition disjoint from
/// the engine's original data partition; no GPT or mapping proof is supplied.
/// This intermediate gate provides exclusion, not Linux waits or fair scheduling.</summary>
public static unsafe class WritableBlockReadPort
{
    public const uint SectorBytes = 512;
    private const ulong Limit = 0x0000800000000000UL;
    private const byte IdlePhase = 1, PendingPhase = 2, ReadyPhase = 3, FaultPhase = 4, RetiredPhase = 5;

    public enum Status : byte
    { Uninitialized, Success, Pending, Ready, Busy, Idle, Cancelled, InvalidState, InvalidStorage,
        InvalidRange, StaleToken, GenerationExhausted, DeviceFailed, Retired }

    /// <summary>Stable original engine/device and independently authorized
    /// inclusive readonly partition bounds. All epochs are nonzero and Epoch
    /// is unique for this zero-State lifetime. The caller holds engine/block,
    /// pool/DMA/MMIO/observer and port backing until port Stop succeeds, before
    /// engine Stop. There is no retained destination or hidden allocation.</summary>
    public struct Admission
    {
        public C.State* Engine;
        public B.State* Device;
        public ulong Epoch, EngineEpoch, DeviceEpoch, FirstLba, LastLba;
    }

    /// <summary>Copied port identity only. It authenticates one admitted IN;
    /// successful Copy, held cancellation or drained cancellation revokes it.</summary>
    public readonly struct Token
    {
        public readonly ulong Owner, Epoch, Generation;
        internal Token(State* s) { Owner = (ulong)s; Epoch = s->Input.Epoch; Generation = s->Generation; }
        public bool IsInitialized => Owner != 0 && Epoch != 0 && Generation != 0;
    }

    /// <summary>Client progress and the exact copied physical completion are
    /// separate. AvailableBytes is512 only for a held successful IN, otherwise0.
    /// Physical may report a successful512B cancelled IN without granting any
    /// caller bytes. The outer owner must retain global B.Observation separately.</summary>
    public readonly struct Completion
    {
        public readonly Token Request;
        public readonly B.Completion Physical;
        public readonly ulong Sector;
        public readonly uint AvailableBytes;
        public readonly Status Result;
        public readonly B.Status BlockFailure;
        internal Completion(State* s, Status result)
        { Request = new(s); Physical = s->Physical; Sector = s->Sector; Result = result;
            AvailableBytes = result == Status.Ready ? SectorBytes : 0; BlockFailure = s->Failure; }
    }

    /// <summary>Copied current port geometry and identity for a typed filesystem
    /// admission. Success authenticates the original clean owner, including this
    /// port's pending/held IN. It grants no mapping, partition provenance or user
    /// copy authority. Busy/fault/retired inspection publishes no facts.</summary>
    public readonly struct Inspection
    {
        public readonly ulong Owner, Epoch, EngineOwner, EngineEpoch, DeviceOwner, DeviceEpoch, SectorCount, FirstLba, LastLba;
        public readonly bool RequestActive, ReadReady;
        internal Inspection(State* s)
        { Owner = (ulong)s; Epoch = s->Input.Epoch; EngineOwner = (ulong)s->Input.Engine; EngineEpoch = s->Input.EngineEpoch;
            DeviceOwner = (ulong)s->Input.Device; DeviceEpoch = s->Input.DeviceEpoch; SectorCount = s->Input.Device->SectorCount;
            FirstLba = s->Input.FirstLba; LastLba = s->Input.LastLba; RequestActive = s->HasIo; ReadReady = s->Phase == ReadyPhase; }
    }

    /// <summary>Zeroed noncopyable POD. Faults retain exact original ownership;
    /// no replacement device at the same address is adopted. An uncertain issued
    /// request retires only after the original owner is independently stopped.
    /// Retired local metadata grants no further retained-storage authority.</summary>
    public struct State
    {
        internal ulong Self, Generation, Sector;
        internal Admission Input;
        internal B.RequestToken Io;
        internal B.Completion Physical;
        internal B.Status Failure;
        internal byte Phase;
        internal bool HasIo, CancelRequested;
        public readonly bool RequestActive => HasIo;
        public readonly bool Ready => Phase == ReadyPhase;
        public readonly bool Retired => Phase == RetiredPhase;
        public readonly B.Status LastBlockFailure => Failure;
    }

    /// <summary>Validate a fresh port without I/O. Invalid storage/identity has
    /// no writes. Busy engine or block activity is not adopted. Numeric alias
    /// checks do not replace the outer supervisor's complete mapping inventory.</summary>
    public static Status Start(State* s, in Admission admission)
    {
        Admission a = admission;
        if (!Storage((ulong)s, (ulong)sizeof(State), 8) || !InputShape(a) ||
            !C.CanRetainReadRange(a.Engine, a.EngineEpoch, a.Device, a.DeviceEpoch, a.FirstLba, a.LastLba) ||
            !C.CanRetainStorage(a.Engine, a.EngineEpoch, (ulong)s, (ulong)sizeof(State)) ||
            !B.CanBorrowStorage(a.Device, a.DeviceEpoch, (ulong)s, (ulong)sizeof(State))) return Status.InvalidStorage;
        for (uint i = 0; i < sizeof(State); i++) if (((byte*)s)[i] != 0) return Status.InvalidState;
        if (a.Engine->RequestActive || a.Device->RequestPending) return Status.Busy;
        if (!C.CanAdmitStorage(a.Engine, a.EngineEpoch) || !B.CanAdmitStorage(a.Device, a.DeviceEpoch)) return Status.DeviceFailed;
        s->Self = (ulong)s; s->Input = a; s->Phase = IdlePhase; s->Failure = B.Status.Success; return Status.Success;
    }

    /// <summary>Inspect without I/O. Busy is an unavailable writer/foreign-read
    /// turn, never successful filesystem readiness or copy. Retired authenticates
    /// only exact local self/epoch retirement, without dereferencing released C/B
    /// backing or touching output; it grants no retained-storage authority. Other
    /// failures also leave output untouched. Nonretired outputs follow full-tree
    /// alias rules and are borrowed only through return.</summary>
    public static Status Inspect(State* s, ulong epoch, Inspection* result)
    {
        if (!Bound(s) || s->Input.Epoch != epoch) return Status.InvalidState;
        if (s->Phase == RetiredPhase) return Status.Retired;
        if (!Retained(s)) return Status.InvalidState;
        if (!External(s, (ulong)result, (ulong)sizeof(Inspection), 8)) return Status.InvalidStorage;
        if (s->Phase == FaultPhase) return Status.DeviceFailed;
        if (s->Input.Engine->RequestActive) return Status.Busy;
        if (!Clean(s)) return Status.DeviceFailed;
        if (s->HasIo ? !CurrentRead(s) : s->Input.Device->RequestPending) return s->HasIo ? Status.DeviceFailed : Status.Busy;
        *result = new(s); return Status.Success;
    }

    /// <summary>Submit at most one real IN. The engine must have no logical
    /// Working/Ready request and B must have no pending/held request. Busy does
    /// not poll or publish. Output aliases reject untouched; admitted rejections
    /// clear the token. Sample B.Observation after this call before another submit.</summary>
    public static Status Submit(State* s, ulong lba, Token* token)
    {
        if (!Retained(s)) return Status.InvalidState;
        if (!External(s, (ulong)token, (ulong)sizeof(Token), 8)) return Status.InvalidStorage;
        *token = default;
        if (s->Phase == FaultPhase) return Status.DeviceFailed;
        if (s->Phase != IdlePhase) return Status.Busy;
        if (lba < s->Input.FirstLba || lba > s->Input.LastLba) return Status.InvalidRange;
        // A normal writer is temporarily dirty between its OUT and final FLUSH.
        // Do not misclassify that exclusive logical turn as a port/device fault.
        if (s->Input.Engine->RequestActive) return Status.Busy;
        if (!Clean(s)) return Status.DeviceFailed;
        if (s->Input.Device->RequestPending) return Status.Busy;
        if (s->Generation == ulong.MaxValue) return Status.GenerationExhausted;
        s->Generation++; s->Sector = lba; s->Physical = default; s->CancelRequested = false;
        ulong before = s->Input.Device->Submitted;
        B.RequestToken io = default;
        var status = B.SubmitRead(s->Input.Device, lba, &io);
        if (status != B.Status.Success)
        {
            // Publication may precede a failed fence/notify and returned token.
            // Retain that ORIGINAL generation, never infer a completed request.
            if (s->Input.Device->Submitted != before) { s->Io = new(s->Input.Device); s->HasIo = true; }
            return Fault(s, status);
        }
        s->Io = io; s->HasIo = true; s->Phase = PendingPhase; *token = new(s); return Status.Success;
    }

    /// <summary>Poll only this port's exact IN generation/kind/LBA. A wrong
    /// client/token never invokes tokenless B.Poll. One call observes one
    /// completion, never submits the next sector. Held Ready is copied metadata
    /// only; final Copy separately revalidates the actual device and destination.</summary>
    public static Status Poll(State* s, Token token, Completion* result)
    {
        if (!Retained(s)) return Status.InvalidState;
        if (!External(s, (ulong)result, (ulong)sizeof(Completion), 8)) return Status.InvalidStorage;
        if (!Matches(s, token)) return Status.StaleToken;
        if (s->Phase == FaultPhase) { *result = new(s, Status.DeviceFailed); return Status.DeviceFailed; }
        if (!Clean(s) || s->Input.Engine->RequestActive || !CurrentRead(s))
        { var failure = Fault(s, B.Status.StaleToken); *result = new(s, failure); return failure; }
        if (s->Phase == ReadyPhase) { *result = new(s, Status.Ready); return Status.Ready; }
        B.Completion physical = default;
        var status = B.Poll(s->Input.Device, &physical);
        if (status == B.Status.Pending) { *result = new(s, Status.Pending); return Status.Pending; }
        if (PhysicalMatches(s, physical)) s->Physical = physical;
        if (status is not (B.Status.Ready or B.Status.Cancelled) || !PhysicalMatches(s, physical) ||
            physical.DeviceResult != B.Status.Success || physical.Bytes != SectorBytes || physical.Cancelled != s->CancelRequested)
        { var failure = Fault(s, status is B.Status.Ready or B.Status.Cancelled ? B.Status.InvalidCompletion : status); *result = new(s, failure); return failure; }
        if (status == B.Status.Cancelled)
        { *result = new(s, Status.Cancelled); End(s); return Status.Cancelled; }
        s->Phase = ReadyPhase; *result = new(s, Status.Ready); return Status.Ready;
    }

    /// <summary>Copy exactly512 bytes from the authenticated held IN, then
    /// revoke its token. No output pointer survives return. Every range is
    /// disjoint from all port/C/B/pool/DMA/MMIO/observer backing. Linux user
    /// mapping/fd generations and other provider buffers remain outer obligations.</summary>
    public static Status Copy(State* s, Token token, Span<byte> destination)
    {
        if (!Retained(s)) return Status.InvalidState;
        fixed (byte* output = destination)
        {
            if (destination.Length != SectorBytes || !External(s, (ulong)output, SectorBytes, 1)) return Status.InvalidStorage;
            if (!Matches(s, token)) return Status.StaleToken;
            if (s->Phase == FaultPhase) return Status.DeviceFailed;
            if (s->Phase != ReadyPhase) return Status.Pending;
            if (!Clean(s) || s->Input.Engine->RequestActive || !CurrentRead(s)) return Fault(s, B.Status.StaleToken);
            var status = B.CopyRead(s->Input.Device, s->Io, destination);
            if (status != B.Status.Success) return Fault(s, status);
            End(s); return Status.Success;
        }
    }

    /// <summary>Revoke only this caller's read. Pending cancellation retains
    /// the lease until Poll drains its actual completion. Held Ready cancellation
    /// revokes copy immediately. Physical facts/counters are never undone.</summary>
    public static Status Cancel(State* s, Token token)
    {
        if (!Retained(s)) return Status.InvalidState;
        if (!Matches(s, token)) return Status.StaleToken;
        if (s->Phase == FaultPhase) return Status.DeviceFailed;
        if (!Clean(s) || s->Input.Engine->RequestActive || !CurrentRead(s)) return Fault(s, B.Status.StaleToken);
        var status = B.Cancel(s->Input.Device, s->Io);
        if (status != B.Status.Success) return Fault(s, status);
        s->CancelRequested = true;
        if (s->Phase == ReadyPhase) { End(s); return Status.Cancelled; }
        return Status.Pending;
    }

    /// <summary>Retire local metadata only. Pending IN is cancelled and must
    /// drain; held IN may be revoked. An issued failed/uncertain request requires
    /// independently proved B retirement. No B/C Stop, reset, FLUSH or release
    /// is performed. The caller stops this port before releasing C backing.</summary>
    public static Status Stop(State* s)
    {
        if (!Bound(s)) return Status.InvalidState;
        if (s->Phase == RetiredPhase) return Status.Success;
        if (!Retained(s)) return Status.InvalidState;
        if (s->HasIo && !s->Input.Device->Retired)
        {
            if (s->Phase == FaultPhase) return Status.DeviceFailed;
            var status = Cancel(s, new(s));
            if (status != Status.Cancelled) return status;
        }
        s->HasIo = false; s->Io = default; s->Physical = default; s->Phase = RetiredPhase; return Status.Success;
    }

    // Structural extent authority only, including dirty/fault/original stopped
    // owners; never submission/copy or permission to release engine backing.
    internal static bool CanRetainStorage(State* s, ulong epoch, ulong address, ulong bytes) =>
        Retained(s) && s->Input.Epoch == epoch && External(s, address, bytes, 1);

    /// <summary>Compare copied original engine identity after the complete
    /// retained port/range/storage audit. The expected owner is numeric only.
    /// Dirty, faulted and original-stopped backing may match until local Stop;
    /// this grants no live I/O, process, filesystem or user-copy authority.
    /// The serialized owner keeps all retained backing mapped through return.
    /// No callbacks, allocations, output or state changes occur.</summary>
    internal static bool MatchesRetainedEngine(State* s, ulong expectedEpoch, ulong engineOwner, ulong engineEpoch) =>
        Retained(s) && s->Input.Epoch == expectedEpoch &&
        (ulong)s->Input.Engine == engineOwner && s->Input.EngineEpoch == engineEpoch;

    private static bool InputShape(Admission a) => Storage((ulong)a.Engine, (ulong)sizeof(C.State), 8) &&
        Storage((ulong)a.Device, (ulong)sizeof(B.State), 8) && a.Epoch != 0 && a.EngineEpoch != 0 && a.DeviceEpoch != 0 &&
        a.FirstLba != 0 && a.FirstLba <= a.LastLba;
    private static bool Bound(State* s) => Storage((ulong)s, (ulong)sizeof(State), 8) && s->Self == (ulong)s &&
        s->Phase is >= IdlePhase and <= RetiredPhase && InputShape(s->Input);
    private static bool Retained(State* s) => Bound(s) && s->Phase != RetiredPhase &&
        C.CanRetainReadRange(s->Input.Engine, s->Input.EngineEpoch, s->Input.Device, s->Input.DeviceEpoch, s->Input.FirstLba, s->Input.LastLba) &&
        C.CanRetainStorage(s->Input.Engine, s->Input.EngineEpoch, (ulong)s, (ulong)sizeof(State)) &&
        B.CanBorrowStorage(s->Input.Device, s->Input.DeviceEpoch, (ulong)s, (ulong)sizeof(State));
    private static bool External(State* s, ulong address, ulong bytes, ulong alignment) => Storage(address, bytes, alignment) &&
        !Overlap(address, bytes, (ulong)s, (ulong)sizeof(State)) &&
        C.CanRetainStorage(s->Input.Engine, s->Input.EngineEpoch, address, bytes) &&
        B.CanBorrowStorage(s->Input.Device, s->Input.DeviceEpoch, address, bytes);
    private static bool Clean(State* s) => C.CanAdmitStorage(s->Input.Engine, s->Input.EngineEpoch) &&
        B.CanAdmitStorage(s->Input.Device, s->Input.DeviceEpoch);
    private static bool Matches(State* s, Token token) => s->HasIo && token.Owner == (ulong)s && token.Epoch == s->Input.Epoch &&
        token.Generation != 0 && token.Generation == s->Generation && s->Phase is PendingPhase or ReadyPhase or FaultPhase;
    // B.Poll has no token argument. Authenticate its CURRENT request before
    // calling it, including held DMA. This is identity, not a duplicated lease check.
    private static bool CurrentRead(State* s) => s->HasIo && s->Io.Owner == (ulong)s->Input.Device && s->Io.Epoch == s->Input.DeviceEpoch &&
        s->Io.Generation != 0 && s->Io.Generation == s->Input.Device->Generation && s->Input.Device->RequestPending &&
        s->Input.Device->LastOperation == B.Operation.Read && s->Input.Device->LastSector == s->Sector;
    private static bool PhysicalMatches(State* s, B.Completion c) => c.Token.Owner == s->Io.Owner && c.Token.Epoch == s->Io.Epoch &&
        c.Token.Generation == s->Io.Generation && c.Kind == B.Operation.Read && c.Sector == s->Sector;
    private static Status Fault(State* s, B.Status status) { s->Failure = status; s->Phase = FaultPhase; return Status.DeviceFailed; }
    private static void End(State* s) { s->HasIo = false; s->Io = default; s->Phase = IdlePhase; s->CancelRequested = false; }
    private static bool Storage(ulong address, ulong bytes, ulong alignment) => address >= 4096 && address < Limit &&
        bytes != 0 && bytes <= Limit - address && (address & (alignment - 1)) == 0;
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => a < b + m && b < a + n;
}
