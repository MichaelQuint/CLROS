using OS.Drivers;

namespace OS.Storage;

/// <summary>Exclusive polling-only modern Virtio IN/OUT/FLUSH owner. One serialized
/// supervisor caller retains state, callbacks, UC MMIO and coherent WB DMA until
/// Stop succeeds. This independent host-tested leaf activates no boot device.
/// Each write borrows exactly512 input bytes synchronously; no caller pointer is
/// retained. A completed OUT is not durable: only a validated subsequent FLUSH
/// advances the flush-acknowledged frontier. Actual persistence still depends on
/// the adapter's truthful device/cache contract and independent restart evidence.</summary>
public static unsafe class WritableBlockSession
{
    public const ulong FlushFeature = 1UL << 9;
    public const ulong Features = (1UL << 32) | FlushFeature;
    public enum Status : byte
    {
        Uninitialized, Success, InvalidAccess, UnsupportedFeatures, ReadOnlyDevice,
        UnsupportedFlush, InvalidState, SetupFailed, InvalidCapacity, ConfigurationChanged,
        InvalidNotify, ReadFailed, WriteFailed, StatusRejected, OrderingFailed,
        BusMasterFailed, DmaRejected, ClockFailed, ClockRegressed, GenerationExhausted,
        SectorOutOfRange, Busy, Idle, Pending, Cancelled, StaleToken, InvalidStorage,
        InvalidCompletion, DeviceIoError, UnsupportedRequest, Timeout, ResetFailed, Ready
    }
    public enum Operation : byte { Write, Flush, Read }

    /// <summary>Trusted monotonic nanoseconds, exactly1 on success. The adapter
    /// retains nonthrowing code/context and the actual mapping/device authority.</summary>
    public struct ClockAccess
    {
        public void* Context;
        public delegate* unmanaged<void*, ulong*, byte> Read;
    }

    /// <summary>Borrowed authority after successful VERSION_1+FLUSH negotiation,
    /// status11, firmware DMA quiescence and MSI-X disabled. RO must not be
    /// offered. Epoch is unique for this zero-state lifetime; pool/state/slots,
    /// ordinary DMA and MMIO remain exclusive and disjoint with no hidden aliases.
    /// Timeout bounds one request and is never renewed by Poll. IRQ callbacks
    /// must remain null. This receipt alone grants no hardware authority.</summary>
    public struct Admission
    {
        public VirtioMappedRegion Common, Notify, Device;
        public uint NotifyMultiplier, ResetLimit;
        public DmaBufferPool* Pool;
        public ulong Epoch, ExpectedCapacity, CompletionTimeoutNanos;
        public VirtioNegotiationResult Negotiation;
        public VirtioBlkReadAccess Access;
        public ClockAccess Clock;
    }

    /// <summary>Copied identity only. The exact retained owner, epoch and
    /// generation authenticate cancellation; OUT/FLUSH completion revokes the
    /// token. Successful IN retains it until CopyRead or cancellation.</summary>
    public readonly struct RequestToken
    {
        public readonly ulong Owner, Epoch, Generation;
        internal RequestToken(State* s) { Owner = (ulong)s; Epoch = s->Input.Epoch; Generation = s->Generation; }
        public bool IsInitialized => Owner != 0 && Epoch != 0 && Generation != 0;
    }

    /// <summary>Copied terminal request facts. DeviceResult is the observed
    /// wire outcome; Cancelled is a separate caller disposition and cannot undo
    /// completed writes or successful flush acknowledgements. Bytes is512 for a
    /// successful IN/OUT and0 for FLUSH/errors. IN bytes remain borrowed DMA
    /// until CopyRead; completion alone is not a delivered-byte claim.
    /// A fault preserves exact failure and
    /// requires retirement before an externally owned retry; no replay occurs.</summary>
    public readonly struct Completion
    {
        public readonly RequestToken Token;
        public readonly Operation Kind;
        public readonly ulong Sector;
        public readonly uint Bytes;
        public readonly Status DeviceResult;
        public readonly bool Cancelled;
        internal Completion(State* s, Status result)
        {
            Token = new(s); Kind = s->Kind; Sector = s->Sector;
            Bytes = result == Status.Success && Kind != Operation.Flush ? 512U : 0;
            DeviceResult = result; Cancelled = s->CancelRequested;
        }
    }

    /// <summary>Stable zero-initialized noncopyable owner. One device-owned DMA
    /// lease remains retained between completions. Fault/uncertain Stop never
    /// frees it. Stop only establishes quiescence, scrubs and returns the lease;
    /// it never flushes or advances durability. Dirty/retry/first-error facts
    /// survive retirement. Restart requires a fresh zero owner and unique epoch.</summary>
    public struct State
    {
        internal ulong Self, PoolEpoch, LastClock, Generation, Sector, Deadline, FlushTarget;
        internal Admission Input;
        internal DmaBufferAllocation Allocation;
        internal VirtioBlkQueueResult Queue;
        internal ulong Capacity, NotifyAddress;
        internal ulong SubmittedCount, CompletedCount, SubmittedWriteBytes, CompletedWriteBytes, FlushedWriteBytes;
        internal ushort AvailableIndex, UsedConsumed;
        internal byte ConfigGeneration, Phase;
        internal Operation Kind;
        internal bool DeviceOwned, ReturnedToCpu, Scrubbed, CancelRequested, Retry;
        internal Status Failure;
        public readonly bool Running => Phase is IdlePhase or PendingPhase or ReadReadyPhase;
        /// <summary>Queue ownership is busy through both an in-flight request
        /// and an unconsumed read result. Legacy writer/mutation admission must
        /// not treat retained ReadReady DMA as an idle device.</summary>
        public readonly bool RequestPending => Phase is PendingPhase or ReadReadyPhase;
        public readonly bool ReadPending => Phase == PendingPhase && Kind == Operation.Read;
        public readonly bool ReadReady => Phase == ReadReadyPhase;
        public readonly bool Retired => Phase == RetiredPhase;
        public readonly bool RequiresCleanup => Self != 0 && (Phase is >= StartingPhase and <= FaultPhase || Phase == ReadReadyPhase);
        public readonly Status LastFailure => Failure;
        public readonly ulong SectorCount => Capacity;
        public readonly ulong Submitted => SubmittedCount;
        public readonly ulong Completed => CompletedCount;
        public readonly ulong SubmittedBytes => SubmittedWriteBytes;
        public readonly ulong CompletedBytes => CompletedWriteBytes;
        public readonly ulong DurableBytes => FlushedWriteBytes;
        public readonly bool Dirty => SubmittedWriteBytes != FlushedWriteBytes;
        public readonly bool RetryRequired => Retry;
        public readonly Operation LastOperation => Kind;
        public readonly ulong LastSector => Sector;
        public readonly VirtioBlkQueueResult Receipt => Queue;
    }
    private const byte StartingPhase = 1, IdlePhase = 2, PendingPhase = 3, FaultPhase = 4, RetiredPhase = 5, ReadReadyPhase = 6;
    private const ulong MaximumAddress = 0x0000800000000000UL;

    /// <summary>Perform bounded modern writable-block feature negotiation with
    /// no queue/DMA/DRIVER_OK action. RO offered devices and absent FLUSH reject;
    /// no writethrough fallback or durability is inferred. The adapter holds
    /// exclusive valid common-config mapping and nonthrowing callbacks throughout.
    /// Failure attempts FAILED without replacing its original error.</summary>
    public static VirtioNegotiationResult Negotiate(VirtioMappedRegion common, uint resetLimit, VirtioBlkQueueAccess a)
    {
        if (!common.IsValid || common.Bytes < 56 || (common.VirtualStart & 3) != 0 || resetLimit is 0 or > 1024 ||
            a.Read8 == null || a.Write8 == null || a.Read32 == null || a.Write32 == null)
            return new(VirtioNegotiationStatus.InvalidAccess, 0, 0, 0, false, false);
        ulong c = common.VirtualStart, offered = 0, selected = 0; byte observed = 255;
        var failure = a.Write8(a.Context, c + 20, 0) == 1 ? VirtioNegotiationStatus.Success : VirtioNegotiationStatus.WriteFailed;
        bool reset = false;
        for (uint i = 0; failure == VirtioNegotiationStatus.Success && i < resetLimit; i++)
        {
            if (a.Read8(a.Context, c + 20, &observed) != 1) { failure = VirtioNegotiationStatus.ReadFailed; break; }
            if (observed == 0) { reset = true; break; }
        }
        if (failure == VirtioNegotiationStatus.Success && !reset) failure = VirtioNegotiationStatus.ResetTimeout;
        for (uint stage = 0; stage < 2; stage++)
        {
            if (failure != VirtioNegotiationStatus.Success) break;
            failure = AdvanceStatus(c, a, stage == 0 ? (byte)1 : (byte)3, out observed);
        }
        for (uint i = 0; failure == VirtioNegotiationStatus.Success && i < 2; i++)
        {
            uint value = 0;
            if (a.Write32(a.Context, c, i) != 1) failure = VirtioNegotiationStatus.WriteFailed;
            else if (a.Read32(a.Context, c + 4, &value) != 1) failure = VirtioNegotiationStatus.ReadFailed;
            else offered |= (ulong)value << (int)(32 * i);
        }
        if (failure == VirtioNegotiationStatus.Success &&
            ((offered & Features) != Features || (offered & (1UL << 5)) != 0)) failure = VirtioNegotiationStatus.UnsupportedFeatures;
        if (failure == VirtioNegotiationStatus.Success) selected = Features;
        for (uint i = 0; failure == VirtioNegotiationStatus.Success && i < 2; i++)
            if (a.Write32(a.Context, c + 8, i) != 1 || a.Write32(a.Context, c + 12, (uint)(Features >> (int)(32 * i))) != 1)
                failure = VirtioNegotiationStatus.WriteFailed;
        if (failure == VirtioNegotiationStatus.Success) failure = AdvanceStatus(c, a, 11, out observed);
        if (failure == VirtioNegotiationStatus.Success) return new(failure, offered, Features, observed, false, false);
        byte current = 0;
        bool attempted = reset && a.Read8(a.Context, c + 20, &current) == 1 && (current & 0x34) == 0;
        bool written = attempted && a.Write8(a.Context, c + 20, (byte)(current | 0x80)) == 1;
        return new(failure, offered, selected, observed, attempted, written);
    }

    private static VirtioNegotiationStatus AdvanceStatus(ulong c, VirtioBlkQueueAccess a, byte next, out byte observed)
    {
        observed = 0; byte prior = 0, value = 0;
        if (a.Read8(a.Context, c + 20, &prior) != 1) return VirtioNegotiationStatus.ReadFailed;
        if ((prior & 0xc0) != 0) return VirtioNegotiationStatus.DeviceFailed;
        if (prior != (next == 1 ? 0 : next == 3 ? 1 : 3)) return VirtioNegotiationStatus.StatusRejected;
        if (a.Write8(a.Context, c + 20, next) != 1) return VirtioNegotiationStatus.WriteFailed;
        if (a.Read8(a.Context, c + 20, &value) != 1) return VirtioNegotiationStatus.ReadFailed;
        observed = value;
        if ((value & 0xc0) != 0) return VirtioNegotiationStatus.DeviceFailed;
        return value == next ? VirtioNegotiationStatus.Success : next == 11 && value == 3 ?
            VirtioNegotiationStatus.FeaturesRejected : VirtioNegotiationStatus.StatusRejected;
    }

    /// <summary>Configure queue0 once and retain its exact DMA lease. Invalid
    /// admission has no effects. Partial setup failure attempts bounded Stop;
    /// inspect RequiresCleanup before releasing any adapter backing.</summary>
    public static Status Start(State* s, in Admission input)
    {
        Admission a = input;
        if ((a.Negotiation.Offered & (1UL << 5)) != 0) return Status.ReadOnlyDevice;
        if ((a.Negotiation.Offered & FlushFeature) == 0) return Status.UnsupportedFlush;
        if (!a.Negotiation.Succeeded || a.Negotiation.Selected != Features || (a.Negotiation.Offered & Features) != Features)
            return Status.UnsupportedFeatures;
        if (!Admit(s, a)) return Status.InvalidAccess;
        s->Self = (ulong)s; s->PoolEpoch = a.Pool->Epoch; s->Input = a; s->Phase = StartingPhase;
        Status result = ReadClock(s, out _);
        if (result == Status.Success) result = ReadCapacity(s);
        if (result == Status.Success && s->Capacity != a.ExpectedCapacity) result = Status.ConfigurationChanged;
        if (result == Status.Success)
        {
            s->Queue.Status = VirtioBlkQueueN0.Configure(a.Common, a.Pool, a.Access.Queue,
                ref s->Allocation, ref s->DeviceOwned, ref s->Queue, a.Notify, a.Device);
            if (s->Queue.Status != VirtioBlkQueueStatus.Success) result = Status.SetupFailed;
        }
        if (result == Status.Success)
        {
            ushort offset = 0;
            if (a.Access.Queue.Read16(a.Access.Queue.Context, a.Common.VirtualStart + 30, &offset) != 1) result = Status.ReadFailed;
            else if ((ulong)offset * a.NotifyMultiplier > a.Notify.Bytes - 2u) result = Status.InvalidNotify;
            else s->NotifyAddress = a.Notify.VirtualStart + (ulong)offset * a.NotifyMultiplier;
        }
        if (result == Status.Success && a.Access.BusMasterOn(a.Access.Queue.Context) != 1) result = Status.BusMasterFailed;
        if (result == Status.Success && !Fence(s)) result = Status.OrderingFailed;
        if (result == Status.Success && a.Access.Queue.Write8(a.Access.Queue.Context, a.Common.VirtualStart + 20, 15) != 1) result = Status.WriteFailed;
        if (result == Status.Success) result = DeviceStatus(s);
        if (result == Status.Success) { s->Phase = IdlePhase; s->Failure = Status.Success; return result; }
        Fault(s, result); _ = Stop(s); return result;
    }

    /// <summary>Copy exactly512 caller bytes into exclusive DMA, publish OUT,
    /// and notify once. The source and metadata output are borrowed only until
    /// return and must not alias state/pool/DMA/MMIO or each other. Invalid
    /// output/source leaves both untouched. Other admitted rejections clear the
    /// output token. Failure after publication may have reached the device:
    /// retain backing, dirty/retry facts and Stop; never assume cancellation undo.</summary>
    public static Status SubmitWrite(State* s, ulong sector, ReadOnlySpan<byte> source, RequestToken* token) =>
        Submit(s, Operation.Write, sector, source, token);

    /// <summary>Publish a real no-data FLUSH after preceding OUT completion.
    /// Even a clean session sends FLUSH; no synthetic success is returned. A
    /// successful validated completion covers only the captured completed-byte
    /// frontier. The metadata output follows SubmitWrite lifetime/alias rules.</summary>
    public static Status SubmitFlush(State* s, RequestToken* token) => Submit(s, Operation.Flush, 0, default, token);

    /// <summary>Publish one actual512-byte IN using the SAME exclusive queue.
    /// No output pointer is retained. Successful Poll retains ReadReady DMA;
    /// CopyRead or Cancel must consume it before another operation. Read never
    /// advances write/durable byte frontiers. Token alias rules match writes.</summary>
    public static Status SubmitRead(State* s, ulong sector, RequestToken* token) => Submit(s, Operation.Read, sector, default, token);

    private static Status Submit(State* s, Operation kind, ulong sector, ReadOnlySpan<byte> source, RequestToken* token)
    {
        if (!Running(s)) return Status.InvalidState;
        if (!MetadataOutput(s, (ulong)token, (ulong)sizeof(RequestToken))) return Status.InvalidStorage;
        fixed (byte* bytes = source)
        {
            if (kind == Operation.Write && (source.Length != 512 || !CallerBuffer(s, (ulong)bytes, 512) ||
                Overlap((ulong)bytes, 512, (ulong)token, (ulong)sizeof(RequestToken)))) return Status.InvalidStorage;
            *token = default;
            if (s->Phase != IdlePhase) return Status.Busy;
            if (kind != Operation.Flush && sector >= s->Capacity) return Status.SectorOutOfRange;
            if (s->Generation == ulong.MaxValue || s->SubmittedCount == ulong.MaxValue ||
                kind == Operation.Write && s->SubmittedWriteBytes > ulong.MaxValue - 512) return Status.GenerationExhausted;
            if (!DmaBound(s)) return Fault(s, Status.DmaRejected);
            Status result = CheckUnchanged(s); if (result != Status.Success) return Fault(s, result);
            result = ReadClock(s, out ulong now); if (result != Status.Success) return Fault(s, result);
            if (now > ulong.MaxValue - s->Input.CompletionTimeoutNanos) return Fault(s, Status.ClockFailed);
            byte* data = (byte*)s->Queue.Layout.Range.CpuVirtualAddress; ulong device = s->Queue.Layout.Range.DeviceAddress;
            *(uint*)(data + 256) = kind == Operation.Write ? 1u : kind == Operation.Flush ? 4u : 0u;
            *(uint*)(data + 260) = 0; *(ulong*)(data + 264) = sector;
            data[272] = 255;
            Descriptor(data, device + 256, 16, 1, 1);
            if (kind != Operation.Flush)
            {
                for (uint i = 0; i < 512; i++) data[512 + i] = kind == Operation.Write ? bytes[i] : (byte)0;
                Descriptor(data + 16, device + 512, 512, kind == Operation.Write ? (ushort)1 : (ushort)3, 2);
                Descriptor(data + 32, device + 272, 1, 2, 0);
            }
            else { Descriptor(data + 16, device + 272, 1, 2, 0); Descriptor(data + 32, 0, 0, 0, 0); }
            ushort* available = (ushort*)(data + s->Queue.Layout.Rings.AvailableRing.Offset);
            available[2 + (s->AvailableIndex & 7)] = 0;
            if (!Fence(s)) return Fault(s, Status.OrderingFailed);
            s->Generation++; s->Sector = sector; s->Kind = kind; s->Deadline = now + s->Input.CompletionTimeoutNanos;
            s->FlushTarget = s->CompletedWriteBytes; s->CancelRequested = false; s->Phase = PendingPhase;
            s->AvailableIndex = unchecked((ushort)(s->AvailableIndex + 1)); available[1] = s->AvailableIndex;
            s->SubmittedCount++; if (kind == Operation.Write) s->SubmittedWriteBytes += 512;
            if (!Fence(s)) return Fault(s, Status.OrderingFailed);
            var a = s->Input.Access.Queue;
            if (a.Write16(a.Context, s->NotifyAddress, 0) != 1) return Fault(s, Status.WriteFailed);
            *token = new(s); return Status.Success;
        }
    }

    /// <summary>Observe at most one used entry with acquire fence and exact
    /// head/status/length checks. IN success writes513 bytes (data+status), IN
    /// errors may write1..513; OUT/FLUSH write only status1. Valid completion
    /// wins the original deadline. IN Ready is retained until copy/cancel.
    /// Successful OUT advances completed bytes only; successful FLUSH advances
    /// durable bytes. Malformed/error/timeout/config failures quarantine DMA and
    /// preserve dirty/retry/error facts. No completion frees the DMA lease.
    /// Output is exclusive aligned ordinary RAM, borrowed only through return;
    /// validate its bounds/aliases before clearing or publishing any metadata.</summary>
    public static Status Poll(State* s, Completion* completion)
    {
        if (!Running(s)) return Status.InvalidState;
        if (!MetadataOutput(s, (ulong)completion, (ulong)sizeof(Completion))) return Status.InvalidStorage;
        *completion = default;
        if (!DmaBound(s)) return Fault(s, Status.DmaRejected);
        Status result = DeviceStatus(s); if (result != Status.Success) return Fault(s, result);
        var a = s->Input.Access.Queue; ushort index = 0;
        ulong used = s->Queue.Layout.Range.CpuVirtualAddress + s->Queue.Layout.Rings.UsedRing.Offset;
        if (a.Read16(a.Context, used + 2, &index) != 1) return Fault(s, Status.ReadFailed);
        ushort delta = unchecked((ushort)(index - s->UsedConsumed));
        if (delta != 0)
        {
            if (s->Phase != PendingPhase || delta != 1) return Fault(s, Status.InvalidCompletion);
            if (!Fence(s)) return Fault(s, Status.OrderingFailed);
            uint id = 0, length = 0; byte status = 255; ulong item = used + 4 + (ulong)(s->UsedConsumed & 7) * 8;
            if (a.Read32(a.Context, item, &id) != 1 || a.Read32(a.Context, item + 4, &length) != 1 ||
                a.Read8(a.Context, s->Queue.Layout.Range.CpuVirtualAddress + 272, &status) != 1) return Fault(s, Status.ReadFailed);
            if (id != 0 || status > 2 || (s->Kind == Operation.Read ? length is 0 or > 513 || status == 0 && length != 513 : length != 1))
                return Fault(s, Status.InvalidCompletion);
            result = DeviceStatus(s); if (result != Status.Success) return Fault(s, result);
            result = ReadClock(s, out _); if (result != Status.Success) return Fault(s, result);
            s->UsedConsumed = index; s->CompletedCount++; s->Deadline = 0;
            result = status == 0 ? Status.Success : status == 1 ? Status.DeviceIoError : Status.UnsupportedRequest;
            *completion = new(s, result);
            if (result != Status.Success) return Fault(s, result);
            if (s->Kind == Operation.Write) s->CompletedWriteBytes += 512;
            else if (s->Kind == Operation.Flush) s->FlushedWriteBytes = s->FlushTarget;
            s->Phase = s->Kind == Operation.Read && !s->CancelRequested ? ReadReadyPhase : IdlePhase;
            return s->CancelRequested ? Status.Cancelled : s->Phase == ReadReadyPhase ? Status.Ready : Status.Success;
        }
        if (s->Phase == ReadReadyPhase) { *completion = new(s, Status.Success); return Status.Ready; }
        result = ReadClock(s, out ulong now); if (result != Status.Success) return Fault(s, result);
        return s->Phase == IdlePhase ? Status.Idle : now >= s->Deadline ? Fault(s, Status.Timeout) : Status.Pending;
    }

    /// <summary>Record caller cancellation without retracting published DMA.
    /// Keep polling or Stop. Cancellation is idempotent for the live token and
    /// never changes physical completion/durability counters or frees backing.</summary>
    public static Status Cancel(State* s, RequestToken token)
    {
        if (!Running(s)) return Status.InvalidState;
        if (s->Phase is not (PendingPhase or ReadReadyPhase) || !token.IsInitialized || token.Owner != (ulong)s ||
            token.Epoch != s->Input.Epoch || token.Generation != s->Generation) return Status.StaleToken;
        s->CancelRequested = true; if (s->Phase == ReadReadyPhase) s->Phase = IdlePhase; return Status.Success;
    }

    /// <summary>Copy exactly512 validated IN bytes to exclusive ordinary RAM,
    /// then revoke read ownership. Invalid length/alias preserves Ready for
    /// retry and writes nothing. Current DMA lease/config/status/used index and
    /// acquire fence are checked before copy; no new expiry or fallible callback
    /// follows it. Destination is borrowed only until return.</summary>
    public static Status CopyRead(State* s, RequestToken token, Span<byte> destination)
    {
        if (!Running(s)) return Status.InvalidState;
        if (!token.IsInitialized || token.Owner != (ulong)s || token.Epoch != s->Input.Epoch || token.Generation != s->Generation ||
            s->Kind != Operation.Read || s->Phase == IdlePhase) return Status.StaleToken;
        if (s->Phase != ReadReadyPhase) return Status.Pending;
        fixed (byte* output = destination)
        {
            if (destination.Length != 512 || !CallerBuffer(s, (ulong)output, 512)) return Status.InvalidStorage;
            if (!DmaBound(s)) return Fault(s, Status.DmaRejected);
            Status status = CheckUnchanged(s); if (status != Status.Success) return Fault(s, status);
            if (!Fence(s)) return Fault(s, Status.OrderingFailed);
            byte* source = (byte*)s->Queue.Layout.Range.CpuVirtualAddress + 512;
            for (uint i = 0; i < 512; i++) output[i] = source[i];
        }
        s->Phase = IdlePhase; return Status.Success;
    }

    /// <summary>Bounded actual reset/readback, bus-master disable/readback and
    /// fence must precede scrub and exact DMA return/release. Stop never implies
    /// FLUSH or persistence. Uncertain cleanup retains all backing for retry;
    /// completed cleanup is idempotent and preserves the original failure.</summary>
    public static Status Stop(State* s)
    {
        if (!Bound(s)) return Status.InvalidState;
        if (s->Phase == RetiredPhase) return Status.Success;
        if (s->Phase == PendingPhase || s->Dirty) s->Retry = true;
        s->Phase = FaultPhase; s->Queue.ResetComplete = false; s->Queue.BusMasterDisabled = false;
        s->Queue.CleanupStatus = VirtioBlkQueueStatus.ResetFailed;
        var a = s->Input.Access.Queue; byte value = 255;
        if (a.Write8(a.Context, s->Input.Common.VirtualStart + 20, 0) == 1)
            for (uint i = 0; i < s->Input.ResetLimit; i++)
            { s->Queue.ResetReads++; if (a.Read8(a.Context, s->Input.Common.VirtualStart + 20, &value) != 1) break;
              if (value == 0) { s->Queue.ResetComplete = true; break; } }
        s->Queue.BusMasterDisabled = a.BusMasterOff(a.Context) == 1;
        if (!s->Queue.ResetComplete) return CleanupFailure(s, Status.ResetFailed);
        if (!s->Queue.BusMasterDisabled) { s->Queue.CleanupStatus = VirtioBlkQueueStatus.BusMasterFailed; return CleanupFailure(s, Status.ResetFailed); }
        if (!Fence(s)) { s->Queue.CleanupStatus = VirtioBlkQueueStatus.OrderingFailed; return CleanupFailure(s, Status.ResetFailed); }
        s->Queue.CleanupStatus = VirtioBlkQueueStatus.Success;
        if (s->Allocation.IsInitialized && !s->Queue.DmaReleased)
        {
            // Quiescence alone does not authenticate which pool lease can be
            // scrubbed/released. Reject changed range/slot authority as well.
            if (!DmaBound(s, s->DeviceOwned && !s->ReturnedToCpu ? DmaBufferOwnership.Device : DmaBufferOwnership.Cpu))
            { s->Queue.CleanupStatus = VirtioBlkQueueStatus.DmaRejected; return CleanupFailure(s, Status.DmaRejected); }
            if (s->DeviceOwned && !s->ReturnedToCpu)
            { if (PageAlignedDmaBuffers.TryReturnToCpu(s->Input.Pool, s->Allocation.Token) != DmaBufferStatus.Success)
              { s->Queue.CleanupStatus = VirtioBlkQueueStatus.DmaRejected; return CleanupFailure(s, Status.DmaRejected); }
              s->ReturnedToCpu = true; }
            if (!s->Scrubbed)
            { for (ulong i = 0; i < s->Allocation.Range.ByteLength; i++) ((byte*)s->Allocation.Range.CpuVirtualAddress)[i] = 0; s->Scrubbed = true; }
            if (PageAlignedDmaBuffers.TryRelease(s->Input.Pool, s->Allocation.Token) != DmaBufferStatus.Success)
            { s->Queue.CleanupStatus = VirtioBlkQueueStatus.DmaRejected; return CleanupFailure(s, Status.DmaRejected); }
        }
        s->Queue.DmaReleased = true; s->Phase = RetiredPhase; return Status.Success;
    }

    private static Status Fault(State* s, Status reason)
    { if (s->Phase == PendingPhase || s->Dirty) s->Retry = true; s->Phase = FaultPhase; if (s->Failure is Status.Uninitialized or Status.Success) s->Failure = reason; return reason; }
    private static Status CleanupFailure(State* s, Status reason)
    { if (s->Failure is Status.Uninitialized or Status.Success) s->Failure = reason; return reason; }
    private static Status ReadCapacity(State* s)
    {
        var a = s->Input.Access.Queue;
        for (uint i = 0; i < 16; i++)
        {
            byte before = 0, after = 0; uint low = 0, high = 0;
            if (a.Read8(a.Context, s->Input.Common.VirtualStart + 21, &before) != 1 || a.Read32(a.Context, s->Input.Device.VirtualStart, &low) != 1 ||
                a.Read32(a.Context, s->Input.Device.VirtualStart + 4, &high) != 1 || a.Read8(a.Context, s->Input.Common.VirtualStart + 21, &after) != 1) return Status.ReadFailed;
            if (before != after) continue;
            s->Capacity = ((ulong)high << 32) | low; s->ConfigGeneration = after;
            return s->Capacity == 0 || s->Capacity > ulong.MaxValue / 512 ? Status.InvalidCapacity : Status.Success;
        }
        return Status.ConfigurationChanged;
    }
    private static Status DeviceStatus(State* s)
    {
        var a = s->Input.Access.Queue; byte value = 0, generation = 0;
        if (a.Read8(a.Context, s->Input.Common.VirtualStart + 20, &value) != 1 || a.Read8(a.Context, s->Input.Common.VirtualStart + 21, &generation) != 1) return Status.ReadFailed;
        return value != 15 ? Status.StatusRejected : generation != s->ConfigGeneration ? Status.ConfigurationChanged : Status.Success;
    }
    private static Status CheckUnchanged(State* s)
    {
        Status result = DeviceStatus(s); if (result != Status.Success) return result;
        var a = s->Input.Access.Queue; ushort index = 0;
        if (a.Read16(a.Context, s->Queue.Layout.Range.CpuVirtualAddress + s->Queue.Layout.Rings.UsedRing.Offset + 2, &index) != 1) return Status.ReadFailed;
        return index == s->UsedConsumed ? Status.Success : Status.InvalidCompletion;
    }
    private static Status ReadClock(State* s, out ulong now)
    {
        now = 0; ulong value = 0;
        if (s->Input.Clock.Read(s->Input.Clock.Context, &value) != 1) return Status.ClockFailed;
        if (value < s->LastClock) return Status.ClockRegressed;
        now = s->LastClock = value; return Status.Success;
    }
    private static void Descriptor(byte* p, ulong address, uint length, ushort flags, ushort next)
    { *(ulong*)p = address; *(uint*)(p + 8) = length; *(ushort*)(p + 12) = flags; *(ushort*)(p + 14) = next; }
    private static bool Fence(State* s)
    { var a = s->Input.Access.Queue; if (a.Fence(a.Context) != 1) return false; s->Queue.Fences++; return true; }
    private static bool Bound(State* s) => Storage((ulong)s, (ulong)sizeof(State)) && s->Self == (ulong)s &&
        s->Phase is >= StartingPhase and <= ReadReadyPhase && (s->Phase == RetiredPhase ||
        (Storage((ulong)s->Input.Pool, (ulong)sizeof(DmaBufferPool)) && s->Input.Pool->Self == (ulong)s->Input.Pool &&
        s->Input.Pool->Epoch == s->PoolEpoch && s->Input.Pool->Count is > 0 and <= 64 &&
        Storage((ulong)s->Input.Pool->Slots, s->Input.Pool->Count * (ulong)sizeof(DmaBufferSlot))));
    private static bool Running(State* s) => Bound(s) && s->Running;
    // Read-only admission fact for retained providers. No clock/MMIO check or
    // I/O permission follows; each actual operation still revalidates hardware.
    internal static bool CanAdmitStorage(State* s, ulong expectedEpoch) =>
        Running(s) && s->Input.Epoch == expectedEpoch && !s->Dirty && DmaBound(s);
    private static bool DmaBound(State* s, DmaBufferOwnership ownership = DmaBufferOwnership.Device)
    {
        var p = s->Input.Pool; var t = s->Allocation.Token;
        if (!s->Allocation.IsInitialized || t.Pool != (ulong)p || t.Epoch != p->Epoch || t.Slot >= p->Count) return false;
        var slot = p->Slots + t.Slot;
        if (slot->Generation != t.Generation || slot->Owner != ownership || slot->Pages != 1 ||
            slot->FirstPage >= p->Count || (p->Used & (1UL << (int)slot->FirstPage)) == 0 ||
            p->Window.TrySlice((ulong)slot->FirstPage * 4096, 4096, out var range) != OS.Kernel.Platform.DmaAddressStatus.Success) return false;
        var original = s->Allocation.Range;
        return range.CpuVirtualAddress == original.CpuVirtualAddress && range.PhysicalAddress == original.PhysicalAddress &&
            range.DeviceAddress == original.DeviceAddress && range.ByteLength == original.ByteLength;
    }
    private static bool MetadataOutput(State* s, ulong start, ulong bytes) => Storage(start, bytes) && CallerBuffer(s, start, bytes);
    private static bool CallerBuffer(State* s, ulong start, ulong bytes) => Storage(start, bytes, 1) &&
        !Overlap(start, bytes, (ulong)s, (ulong)sizeof(State)) && !Overlap(start, bytes, (ulong)s->Input.Pool, (ulong)sizeof(DmaBufferPool)) &&
        !Overlap(start, bytes, (ulong)s->Input.Pool->Slots, s->Input.Pool->Count * (ulong)sizeof(DmaBufferSlot)) &&
        !Overlap(start, bytes, s->Input.Pool->Window.CpuVirtualBase, s->Input.Pool->Window.ByteLength) &&
        !OverlapsRegion(start, bytes, s->Input.Common) && !OverlapsRegion(start, bytes, s->Input.Notify) && !OverlapsRegion(start, bytes, s->Input.Device);
    private static bool Admit(State* s, in Admission a)
    {
        var q = a.Access.Queue;
        if (!a.Common.IsValid || a.Common.Bytes < 56 || (a.Common.VirtualStart & 3) != 0 ||
            !a.Notify.IsValid || a.Notify.Bytes < 2 || (a.Notify.VirtualStart & 1) != 0 || !a.Device.IsValid || a.Device.Bytes < 8 || (a.Device.VirtualStart & 3) != 0 ||
            (a.NotifyMultiplier != 0 && (a.NotifyMultiplier < 2 || (a.NotifyMultiplier & (a.NotifyMultiplier - 1)) != 0)) ||
            a.ResetLimit is 0 or > 1024 || a.Epoch == 0 || a.ExpectedCapacity == 0 || a.ExpectedCapacity > ulong.MaxValue / 512 ||
            a.CompletionTimeoutNanos is 0 or > 60000000000UL || a.Clock.Read == null || q.Read8 == null || q.Write8 == null || q.Read16 == null ||
            q.Write16 == null || q.Read32 == null || q.Write32 == null || q.Fence == null || q.BusMasterOff == null || a.Access.BusMasterOn == null ||
            a.Access.PrepareInterrupt != null || a.Access.WaitInterrupt != null || a.Access.StopInterrupt != null ||
            !Storage((ulong)s, (ulong)sizeof(State)) || !Storage((ulong)a.Pool, (ulong)sizeof(DmaBufferPool))) return false;
        if (a.Pool->Self != (ulong)a.Pool || a.Pool->Epoch == 0 || a.Pool->Count is 0 or > 64 || !a.Pool->Window.IsInitialized ||
            a.Pool->Window.ByteLength != a.Pool->Count * 4096UL || !Storage((ulong)a.Pool->Slots, a.Pool->Count * (ulong)sizeof(DmaBufferSlot))) return false;
        ulong* starts = stackalloc ulong[] { (ulong)s, (ulong)a.Pool, (ulong)a.Pool->Slots };
        ulong* lengths = stackalloc ulong[] { (ulong)sizeof(State), (ulong)sizeof(DmaBufferPool), a.Pool->Count * (ulong)sizeof(DmaBufferSlot) };
        for (int i = 0; i < 3; i++)
        {
            if (Overlap(starts[i], lengths[i], a.Pool->Window.CpuVirtualBase, a.Pool->Window.ByteLength) ||
                OverlapsRegion(starts[i], lengths[i], a.Common) || OverlapsRegion(starts[i], lengths[i], a.Notify) || OverlapsRegion(starts[i], lengths[i], a.Device)) return false;
            for (int j = 0; j < i; j++) if (Overlap(starts[i], lengths[i], starts[j], lengths[j])) return false;
        }
        if (RegionAlias(a.Common, a.Pool->Window) || RegionAlias(a.Notify, a.Pool->Window) || RegionAlias(a.Device, a.Pool->Window) ||
            RegionOverlap(a.Common, a.Notify) || RegionOverlap(a.Common, a.Device) || RegionOverlap(a.Notify, a.Device)) return false;
        for (ulong i = 0; i < (ulong)sizeof(State); i++) if (((byte*)s)[i] != 0) return false;
        return true;
    }
    private static bool RegionAlias(VirtioMappedRegion r, OS.Kernel.Platform.DmaAddressWindow w) =>
        Overlap(r.VirtualStart, r.Bytes, w.CpuVirtualBase, w.ByteLength) || Overlap(r.PhysicalStart, r.Bytes, w.PhysicalBase, w.ByteLength);
    private static bool RegionOverlap(VirtioMappedRegion a, VirtioMappedRegion b) =>
        Overlap(a.VirtualStart, a.Bytes, b.VirtualStart, b.Bytes) || Overlap(a.PhysicalStart, a.Bytes, b.PhysicalStart, b.Bytes);
    private static bool OverlapsRegion(ulong start, ulong bytes, VirtioMappedRegion r) => Overlap(start, bytes, r.VirtualStart, r.Bytes);
    private static bool Storage(ulong start, ulong bytes, ulong alignment = 8) => start >= 4096 && (start & (alignment - 1)) == 0 &&
        bytes != 0 && start < MaximumAddress && bytes <= MaximumAddress - start;
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => a < b + m && b < a + n;
}
