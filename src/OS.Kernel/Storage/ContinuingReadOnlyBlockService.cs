using OS.Drivers;

namespace OS.Storage;

/// <summary>Exclusive polling-only modern Virtio block owner. One serialized
/// supervisor caller retains the state, callbacks, UC MMIO and coherent WB DMA
/// backing until Stop succeeds. Negotiation and actual mapping/device authority
/// belong to the adapter. One IN request is outstanding at a time: legacy calls
/// select512 bytes, explicit reads select at most4 sectors in the same DMA page. No
/// interrupts, writes, hidden allocation, synchronous wait or caller pointer is
/// retained. Poll performs one observation so other services can keep running.</summary>
public static unsafe partial class ContinuingReadOnlyBlockService
{
    public const uint MaximumReadSectors = VirtioBlkDmaLayout.MaximumReadSectors;
    private const byte StartingPhase = 1, IdlePhase = 2, PendingPhase = 3,
        ReadyPhase = 4, FaultPhase = 5, RetiredPhase = 6;
    private const ulong MaximumAddress = 0x0000800000000000UL;

    /// <summary>Configure/read back queue0 once, retain one4KiB DMA lease and
    /// enable bus mastering/DRIVER_OK. Invalid admission has no effects. Partial
    /// setup failures preserve their reason and attempt bounded Stop; inspect
    /// RequiresCleanup before releasing any adapter resource.</summary>
    public static Status Start(State* state, in Admission input)
    {
        if (RejectReentry(state)) return Status.ObservationRejected;
        Admission a = input;
        if (a.NegotiatedFeatures != BoundedVirtioFeatures.Version1 &&
            a.NegotiatedFeatures != (BoundedVirtioFeatures.Version1 | BoundedVirtioFeatures.BlockReadOnly))
            return Status.UnsupportedFeatures;
        if (!Admit(state, a)) return Status.InvalidAccess;
        state->Self = (ulong)state; state->PoolEpoch = a.Pool->Epoch;
        state->Input = a; state->Phase = StartingPhase;
        Status result = StartCore(state);
        if (result == Status.Success) { state->Phase = IdlePhase; state->Failure = Status.Success; return result; }
        _ = Fault(state, result);
        _ = Stop(state);
        return result;
    }

    private static Status StartCore(State* s)
    {
        Admission a = s->Input;
        Status status = ReadClock(s, out _); if (status != Status.Success) return status;
        status = ReadCapacity(s); if (status != Status.Success) return status;
        if (s->Capacity != a.ExpectedCapacity) return Status.ConfigurationChanged;
        s->Queue.Status = VirtioBlkQueueN0.Configure(a.Common, a.Pool, a.Access.Queue,
            ref s->Allocation, ref s->DeviceOwned, ref s->Queue, a.Notify, a.Device);
        if (s->Queue.Status != VirtioBlkQueueStatus.Success) return Status.SetupFailed;
        ushort offset = 0;
        if (a.Access.Queue.Read16(a.Access.Queue.Context, a.Common.VirtualStart + 30, &offset) != 1) return Status.ReadFailed;
        ulong displacement = (ulong)offset * a.NotifyMultiplier;
        if (displacement > a.Notify.Bytes - 2u) return Status.InvalidNotify;
        s->NotifyAddress = a.Notify.VirtualStart + displacement;
        if (a.Access.BusMasterOn(a.Access.Queue.Context) != 1) return Status.BusMasterFailed;
        if (!Fence(s)) return Status.OrderingFailed;
        if (a.Access.Queue.Write8(a.Access.Queue.Context, a.Common.VirtualStart + 20, 15) != 1) return Status.WriteFailed;
        return DeviceStatus(s);
    }

    /// <summary>Publish one read and actually notify queue0. Busy/range/generation
    /// rejection publishes nothing. A failure after publication faults the owner:
    /// the device may have seen the request, so retain backing and Stop. The
    /// original deadline is never renewed by Poll. Supply aligned, mapped,
    /// exclusive ordinary RAM for the token, disjoint from all retained storage.
    /// It is borrowed only for this call. Invalid owner/output leaves it untouched;
    /// admitted output is cleared before any other rejection or publication.
    /// Observer rejection after notification retains published DMA but no token.</summary>
    public static Status Submit(State* s, ulong sector, ReadToken* token)
        => Submit(s, sector, 1, token);

    /// <summary>Publish one contiguous1..4-sector IN request in the existing
    /// retained DMA page. Legacy Submit selects1. Invalid count or capacity span
    /// returns SectorOutOfRange without publication; no deadline or ownership rule
    /// changes. Token storage and callback lifetimes follow the legacy contract.</summary>
    public static Status Submit(State* s, ulong sector, uint sectors, ReadToken* token)
    {
        if (RejectReentry(s)) return Status.ObservationRejected;
        if (!Running(s)) return Status.InvalidState;
        if (!MetadataOutput(s, (ulong)token, (ulong)sizeof(ReadToken))) return Status.InvalidStorage;
        *token = default;
        if (s->Phase != IdlePhase) return Status.Busy;
        if (sectors is 0 or > MaximumReadSectors || sector >= s->Capacity || sectors > s->Capacity - sector)
            return Status.SectorOutOfRange;
        uint bytes = sectors * VirtioBlkDmaLayout.DataBytes;
        if (s->RequestGeneration == ~0UL) return Status.GenerationExhausted;
        Status status = CheckUnchanged(s); if (status != Status.Success) return Fault(s, status);
        status = ReadClock(s, out ulong now); if (status != Status.Success) return Fault(s, status);
        if (now > ~0UL - s->Input.CompletionTimeoutNanos) return Fault(s, Status.ClockFailed);
        byte* data = (byte*)s->Queue.Layout.Range.CpuVirtualAddress;
        ulong address = s->Queue.Layout.Range.DeviceAddress;
        *(uint*)(data + VirtioBlkDmaLayout.RequestOffset) = 0;
        *(uint*)(data + VirtioBlkDmaLayout.RequestOffset + 4) = 0;
        *(ulong*)(data + VirtioBlkDmaLayout.RequestOffset + 8) = sector;
        data[VirtioBlkDmaLayout.StatusOffset] = 0xff;
        for (uint i = 0; i < bytes; i++) data[VirtioBlkDmaLayout.DataOffset + i] = 0;
        Descriptor(data, address + VirtioBlkDmaLayout.RequestOffset, 16, 1, 1);
        Descriptor(data + 16, address + VirtioBlkDmaLayout.DataOffset, bytes, 3, 2);
        Descriptor(data + 32, address + VirtioBlkDmaLayout.StatusOffset, 1, 2, 0);
        ushort* available = (ushort*)(data + s->Queue.Layout.Rings.AvailableRing.Offset);
        available[2 + (s->AvailableIndex & 7)] = 0;
        if (!Fence(s)) return Fault(s, Status.OrderingFailed);
        s->RequestGeneration++; s->RequestSector = sector; s->RequestSectors = (byte)sectors;
        s->Deadline = now + s->Input.CompletionTimeoutNanos;
        s->Discard = false; s->Phase = PendingPhase;
        s->AvailableIndex = unchecked((ushort)(s->AvailableIndex + 1));
        available[1] = s->AvailableIndex; s->SubmittedCount++;
        if (!Fence(s)) return Fault(s, Status.OrderingFailed);
        var a = s->Input.Access.Queue;
        if (a.Write16(a.Context, s->NotifyAddress, 0) != 1) return Fault(s, Status.WriteFailed);
        if (!Observe(s, ObservationKind.Submitted, s->SubmittedCount, bytes, null)) return Fault(s, Status.ObservationRejected);
        *token = Token(s); return Status.Success;
    }

    /// <summary>One bounded status/index observation and, when present, one used
    /// element. Valid completion wins timeout on this call. Ready is repeatable
    /// until Copy/Cancel; cancellation drains without exposing bytes. Unexpected
    /// indices/heads, short/error status, config churn and clock failure fault the
    /// owner without releasing DMA. Virtio supplies head IDs, not wire generation
    /// tags; this contract assumes the admitted device follows the protocol.
    /// Completion output is aligned, mapped exclusive ordinary RAM, disjoint from
    /// retained storage, and borrowed only through return. Invalid owner/output
    /// leaves it untouched; other outcomes clear it unless they publish a result.
    /// The observer runs once per newly validated entry, never for repeated Ready.</summary>
    public static Status Poll(State* s, Completion* completion)
    {
        if (RejectReentry(s)) return Status.ObservationRejected;
        if (!Running(s)) return Status.InvalidState;
        if (!MetadataOutput(s, (ulong)completion, (ulong)sizeof(Completion))) return Status.InvalidStorage;
        *completion = default;
        if ((s->Phase is PendingPhase or ReadyPhase) && (s->RequestSectors == 0 || s->RequestSectors > MaximumReadSectors))
            return Fault(s, Status.InvalidCompletion);
        uint bytes = s->RequestBytes;
        if (s->PollCount != ~0UL) s->PollCount++;
        Status status = DeviceStatus(s); if (status != Status.Success) return Fault(s, status);
        var a = s->Input.Access.Queue;
        ulong cpu = s->Queue.Layout.Range.CpuVirtualAddress;
        ulong used = cpu + s->Queue.Layout.Rings.UsedRing.Offset;
        ushort index = 0;
        if (a.Read16(a.Context, used + 2, &index) != 1) return Fault(s, Status.ReadFailed);
        ushort delta = unchecked((ushort)(index - s->UsedConsumed));
        if (delta != 0)
        {
            if (s->Phase != PendingPhase || delta != 1) return Fault(s, Status.InvalidCompletion);
            if (!Fence(s)) return Fault(s, Status.OrderingFailed);
            uint id = 0, length = 0; byte deviceStatus = 0xff;
            ulong element = used + 4 + (ulong)(s->UsedConsumed & 7) * 8;
            if (a.Read32(a.Context, element, &id) != 1 || a.Read32(a.Context, element + 4, &length) != 1 ||
                a.Read8(a.Context, cpu + VirtioBlkDmaLayout.StatusOffset, &deviceStatus) != 1) return Fault(s, Status.ReadFailed);
            var checkedCompletion = UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, id, length, deviceStatus, s->RequestSectors);
            if (checkedCompletion != VirtioBlkReadStatus.Success) return Fault(s, CompletionStatus(checkedCompletion));
            status = DeviceStatus(s); if (status != Status.Success) return Fault(s, status);
            status = ReadClock(s, out _); if (status != Status.Success) return Fault(s, status);
            s->UsedConsumed = index; s->CompletedCount++; s->Deadline = 0;
            if (!Observe(s, ObservationKind.CompletionValidated, s->CompletedCount, s->Discard ? 0u : bytes, null))
                return Fault(s, Status.ObservationRejected);
            *completion = new(Token(s), s->RequestSector, s->Discard ? 0u : bytes);
            if (s->Discard)
            {
                s->CancelledCount++; s->Discard = false; s->Phase = IdlePhase; return Status.Cancelled;
            }
            s->Phase = ReadyPhase; return Status.Ready;
        }
        status = ReadClock(s, out ulong now); if (status != Status.Success) return Fault(s, status);
        if (s->Phase == PendingPhase) return now >= s->Deadline ? Fault(s, Status.Timeout) : Status.Pending;
        if (s->Phase == ReadyPhase) { *completion = new(Token(s), s->RequestSector, bytes); return Status.Ready; }
        return Status.Idle;
    }

    /// <summary>Copy the exact retained request length to exclusive ordinary RAM, then
    /// revoke the token and admit the next read. Short/aliased output preserves
    /// Ready for retry. All status/config/index/fence checks precede the copy.
    /// CopyReady observation precedes destination writes; its sequence becomes
    /// committed only when State.Copied advances. Caller memory is never retained;
    /// failure, including observer rejection, copies no bytes.</summary>
    public static Status Copy(State* s, ReadToken token, Span<byte> destination)
    {
        if (RejectReentry(s)) return Status.ObservationRejected;
        if (!Running(s)) return Status.InvalidState;
        if (!Matches(s, token) || s->Phase == IdlePhase) return Status.StaleToken;
        if (s->Discard) return Status.Cancelled;
        if (s->Phase != ReadyPhase) return Status.Pending;
        if (s->RequestSectors == 0 || s->RequestSectors > MaximumReadSectors) return Fault(s, Status.InvalidCompletion);
        uint bytes = s->RequestBytes;
        if ((uint)destination.Length < bytes) return Status.ShortBuffer;
        fixed (byte* output = destination)
        {
            if (!CallerBuffer(s, (ulong)output, bytes)) return Status.InvalidStorage;
            Status status = CheckUnchanged(s); if (status != Status.Success) return Fault(s, status);
            if (!Fence(s)) return Fault(s, Status.OrderingFailed);
            byte* source = (byte*)(s->Queue.Layout.Range.CpuVirtualAddress + VirtioBlkDmaLayout.DataOffset);
            if (!Observe(s, ObservationKind.CopyReady, s->CopiedCount + 1, bytes, source)) return Fault(s, Status.ObservationRejected);
            uint i = 0;
            if ((((ulong)output | (ulong)source) & 7) == 0)
                for (; i < (bytes & ~7U); i += 8) *(ulong*)(output + i) = *(ulong*)(source + i);
            for (; i < bytes; i++) output[i] = source[i];
        }
        s->CopiedCount++; s->Phase = IdlePhase; return Status.Success;
    }

    /// <summary>Revoke output permission. Pending DMA still owns its descriptors
    /// and bounce buffer until Poll drains it or Stop establishes quiescence.
    /// Repeated cancellation of that pending token is harmless; it is still Busy.</summary>
    public static Status Cancel(State* s, ReadToken token)
    {
        if (RejectReentry(s)) return Status.ObservationRejected;
        if (!Running(s)) return Status.InvalidState;
        if (!Matches(s, token) || s->Phase == IdlePhase) return Status.StaleToken;
        if (s->Phase == PendingPhase) s->Discard = true;
        else { s->CancelledCount++; s->Phase = IdlePhase; }
        return Status.Success;
    }

    /// <summary>Bounded actual reset/readback, bus-master disable/readback and
    /// fence precede scrub and exact DMA return/release. Uncertain cleanup keeps
    /// resources quarantined. Retry requires fresh quiescence, preserves completed
    /// release stages and never reports a healthy device. Retired Stop is an
    /// idempotent no-op. Restart needs fresh zero state and a unique lifetime epoch.</summary>
    public static Status Stop(State* s)
    {
        if (RejectReentry(s)) return Status.ObservationRejected;
        if (!Bound(s)) return Status.InvalidState;
        if (s->Phase == RetiredPhase) return Status.Success;
        s->Phase = FaultPhase; s->Discard = true;
        s->Queue.ResetComplete = false; s->Queue.BusMasterDisabled = false;
        s->Queue.CleanupStatus = VirtioBlkQueueStatus.ResetFailed;
        var a = s->Input.Access.Queue; byte value = 0xff;
        if (a.Write8(a.Context, s->Input.Common.VirtualStart + 20, 0) == 1)
            for (uint i = 0; i < s->Input.ResetLimit; i++)
            {
                s->Queue.ResetReads++;
                if (a.Read8(a.Context, s->Input.Common.VirtualStart + 20, &value) != 1) break;
                if (value == 0) { s->Queue.ResetComplete = true; break; }
            }
        s->Queue.BusMasterDisabled = a.BusMasterOff(a.Context) == 1;
        if (!s->Queue.ResetComplete) return CleanupFailure(s, Status.ResetFailed);
        if (!s->Queue.BusMasterDisabled) { s->Queue.CleanupStatus = VirtioBlkQueueStatus.BusMasterFailed; return CleanupFailure(s, Status.ResetFailed); }
        if (!Fence(s)) { s->Queue.CleanupStatus = VirtioBlkQueueStatus.OrderingFailed; return CleanupFailure(s, Status.ResetFailed); }
        s->Queue.CleanupStatus = VirtioBlkQueueStatus.Success;
        if (s->Allocation.IsInitialized && !s->Queue.DmaReleased)
        {
            if (s->DeviceOwned && !s->ReturnedToCpu)
            {
                if (PageAlignedDmaBuffers.TryReturnToCpu(s->Input.Pool, s->Allocation.Token) != DmaBufferStatus.Success)
                { s->Queue.CleanupStatus = VirtioBlkQueueStatus.DmaRejected; return CleanupFailure(s, Status.DmaRejected); }
                s->ReturnedToCpu = true;
            }
            if (!s->Scrubbed)
            {
                byte* bytes = (byte*)s->Allocation.Range.CpuVirtualAddress;
                for (ulong i = 0; i < s->Allocation.Range.ByteLength; i++) bytes[i] = 0;
                s->Scrubbed = true;
            }
            if (PageAlignedDmaBuffers.TryRelease(s->Input.Pool, s->Allocation.Token) != DmaBufferStatus.Success)
            { s->Queue.CleanupStatus = VirtioBlkQueueStatus.DmaRejected; return CleanupFailure(s, Status.DmaRejected); }
        }
        s->Queue.DmaReleased = true; s->Phase = RetiredPhase; return Status.Success;
    }

    private static Status ReadCapacity(State* s)
    {
        var a = s->Input.Access.Queue;
        for (uint i = 0; i < 16; i++)
        {
            byte before = 0, after = 0; uint low = 0, high = 0;
            if (a.Read8(a.Context, s->Input.Common.VirtualStart + 21, &before) != 1 ||
                a.Read32(a.Context, s->Input.Device.VirtualStart, &low) != 1 ||
                a.Read32(a.Context, s->Input.Device.VirtualStart + 4, &high) != 1 ||
                a.Read8(a.Context, s->Input.Common.VirtualStart + 21, &after) != 1) return Status.ReadFailed;
            if (before != after) continue;
            s->Capacity = ((ulong)high << 32) | low; s->ConfigGeneration = after;
            return s->Capacity == 0 || s->Capacity > ~0UL / 512 ? Status.InvalidCapacity : Status.Success;
        }
        return Status.ConfigurationChanged;
    }

    private static Status DeviceStatus(State* s)
    {
        var a = s->Input.Access.Queue; byte value = 0, generation = 0;
        if (a.Read8(a.Context, s->Input.Common.VirtualStart + 20, &value) != 1) return Status.ReadFailed;
        if (value != 15) return Status.StatusRejected;
        if (a.Read8(a.Context, s->Input.Common.VirtualStart + 21, &generation) != 1) return Status.ReadFailed;
        return generation == s->ConfigGeneration ? Status.Success : Status.ConfigurationChanged;
    }

    private static Status CheckUnchanged(State* s)
    {
        Status status = DeviceStatus(s); if (status != Status.Success) return status;
        var a = s->Input.Access.Queue; ushort index = 0;
        if (a.Read16(a.Context, s->Queue.Layout.Range.CpuVirtualAddress + s->Queue.Layout.Rings.UsedRing.Offset + 2, &index) != 1)
            return Status.ReadFailed;
        return index == s->UsedConsumed ? Status.Success : Status.InvalidCompletion;
    }

    private static Status ReadClock(State* s, out ulong now)
    {
        now = 0; ulong value = 0;
        if (s->Input.Clock.Read(s->Input.Clock.Context, &value) != 1) return Status.ClockFailed;
        if (value < s->LastClock) return Status.ClockRegressed;
        now = value; s->LastClock = value; return Status.Success;
    }
    private static Status CompletionStatus(VirtioBlkReadStatus status) => status switch
    { VirtioBlkReadStatus.DeviceIoError => Status.DeviceIoError, VirtioBlkReadStatus.UnsupportedRequest => Status.UnsupportedRequest, _ => Status.InvalidCompletion };
    private static void Descriptor(byte* p, ulong address, uint length, ushort flags, ushort next)
    { *(ulong*)p = address; *(uint*)(p + 8) = length; *(ushort*)(p + 12) = flags; *(ushort*)(p + 14) = next; }
    private static bool Fence(State* s)
    { var a = s->Input.Access.Queue; if (a.Fence(a.Context) != 1) return false; s->Queue.Fences++; return true; }
    private static bool Observe(State* s, ObservationKind kind, ulong sequence, uint bytes, byte* payload)
    {
        var access = s->Input.Observation;
        if (access.Observe == null) return true;
        Observation observation = new(s, kind, sequence, bytes);
        s->ObservationReentered = false; s->Observing = true;
        byte accepted = access.Observe(access.Context, &observation, payload);
        s->Observing = false;
        return accepted == 1 && !s->ObservationReentered;
    }
    // Reject before output clearing or hardware effects. A nested rejection is
    // sticky until the outer observer returns, even if it ignores that result.
    private static bool RejectReentry(State* s)
    {
        if (!Storage((ulong)s, (ulong)sizeof(State)) || s->Self != (ulong)s || !s->Observing) return false;
        s->ObservationReentered = true; return true;
    }
    private static Status Fault(State* s, Status reason) { s->Phase = FaultPhase; s->Failure = reason; return reason; }
    private static Status CleanupFailure(State* s, Status reason)
    { if (s->Failure is Status.Uninitialized or Status.Success) s->Failure = reason; return reason; }
    private static ReadToken Token(State* s) => new((ulong)s, s->Input.Epoch, s->RequestGeneration);
    private static bool Matches(State* s, ReadToken token) => token.IsInitialized && token.Owner == (ulong)s &&
        token.Epoch == s->Input.Epoch && token.Generation == s->RequestGeneration;
    private static bool Bound(State* s) => Storage((ulong)s, (ulong)sizeof(State)) && s->Self == (ulong)s &&
        s->Phase is >= StartingPhase and <= RetiredPhase && (s->Phase == RetiredPhase ||
        (s->Input.Pool != null && s->Input.Pool->Self == (ulong)s->Input.Pool && s->Input.Pool->Epoch == s->PoolEpoch));
    private static bool Running(State* s) => Bound(s) && s->Running;
    private static bool MetadataOutput(State* s, ulong start, ulong bytes) => Storage(start, bytes) && CallerBuffer(s, start, bytes);
    private static bool CallerBuffer(State* s, ulong start, ulong bytes) => Storage(start, bytes, 1) &&
        !Overlap(start, bytes, (ulong)s, (ulong)sizeof(State)) &&
        !Overlap(start, bytes, (ulong)s->Input.Pool, (ulong)sizeof(DmaBufferPool)) &&
        !Overlap(start, bytes, (ulong)s->Input.Pool->Slots, s->Input.Pool->Count * (ulong)sizeof(DmaBufferSlot)) &&
        !Overlap(start, bytes, s->Input.Pool->Window.CpuVirtualBase, s->Input.Pool->Window.ByteLength) &&
        !OverlapsRegion(start, bytes, s->Input.Common) && !OverlapsRegion(start, bytes, s->Input.Notify) &&
        !OverlapsRegion(start, bytes, s->Input.Device);

    private static bool Admit(State* s, in Admission a)
    {
        var q = a.Access.Queue;
        if (!a.Common.IsValid || a.Common.Bytes < 56 || (a.Common.VirtualStart & 3) != 0 ||
            !a.Notify.IsValid || a.Notify.Bytes < 2 || (a.Notify.VirtualStart & 1) != 0 ||
            !a.Device.IsValid || a.Device.Bytes < 8 || (a.Device.VirtualStart & 3) != 0 ||
            (a.NotifyMultiplier != 0 && (a.NotifyMultiplier < 2 || (a.NotifyMultiplier & (a.NotifyMultiplier - 1)) != 0)) ||
            a.ResetLimit is 0 or > 1024 || a.Epoch == 0 || a.ExpectedCapacity == 0 || a.ExpectedCapacity > ~0UL / 512 ||
            a.CompletionTimeoutNanos is 0 or > 60000000000UL || a.Clock.Read == null ||
            q.Read8 == null || q.Write8 == null || q.Read16 == null || q.Write16 == null || q.Read32 == null || q.Write32 == null ||
            q.Fence == null || q.BusMasterOff == null || a.Access.BusMasterOn == null ||
            a.Access.PrepareInterrupt != null || a.Access.WaitInterrupt != null || a.Access.StopInterrupt != null ||
            !Storage((ulong)s, (ulong)sizeof(State)) || !Storage((ulong)a.Pool, (ulong)sizeof(DmaBufferPool))) return false;
        if (a.Pool->Self != (ulong)a.Pool || a.Pool->Epoch == 0 || a.Pool->Count is 0 or > 64 ||
            !a.Pool->Window.IsInitialized || a.Pool->Window.ByteLength != a.Pool->Count * 4096UL ||
            !Storage((ulong)a.Pool->Slots, a.Pool->Count * (ulong)sizeof(DmaBufferSlot))) return false;
        ulong* starts = stackalloc ulong[] { (ulong)s, (ulong)a.Pool, (ulong)a.Pool->Slots };
        ulong* lengths = stackalloc ulong[] { (ulong)sizeof(State), (ulong)sizeof(DmaBufferPool), a.Pool->Count * (ulong)sizeof(DmaBufferSlot) };
        for (int i = 0; i < 3; i++)
        {
            if (Overlap(starts[i], lengths[i], a.Pool->Window.CpuVirtualBase, a.Pool->Window.ByteLength) ||
                OverlapsRegion(starts[i], lengths[i], a.Common) || OverlapsRegion(starts[i], lengths[i], a.Notify) ||
                OverlapsRegion(starts[i], lengths[i], a.Device)) return false;
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
