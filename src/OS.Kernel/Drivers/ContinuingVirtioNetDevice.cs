using RingEngine = global::OS.Drivers.ContinuingVirtioNet;

namespace OS.Drivers;
/// <summary>Exclusive retained modern-net queue owner over the existing PCI,
/// MMIO, DMA and ring contracts. One IRQ-off BSP caller retains all mappings,
/// metadata and validated native callbacks. The device must already have exactly
/// VERSION_1+MRG_RXBUF negotiated, status11 and exclusive ownership. Numeric
/// regions/callbacks do not establish that authority. No interrupts, offload,
/// multi-buffer assembly or packet-protocol processing is supplied here.</summary>

public static unsafe partial class ContinuingVirtioNetDevice
{
    /// <summary>Allocate eight pages, prepare all RX buffers, program/read back
    /// both queues using the accepted setup helper, enable bus mastering and
    /// DRIVER_OK, then actually notify RX. Success means a configured running
    /// device, not a received/transmitted packet. Invalid admission has no side
    /// effects. Partial failures preserve their reason and attempt bounded cleanup.</summary>

    public static Status Start(State* state, in Admission input)
    {
        if (input.NegotiatedFeatures != BoundedVirtioFeatures.NetSingleBufferReceive)
            return Status.UnsupportedFeatures;
        if (!Admit(state, input))
            return Status.InvalidAccess;
        state->Self = (ulong)state;
        state->Input = input;
        state->PoolEpoch = input.Pool->Epoch;
        state->Phase = 1;
        Status result = StartCore(state);
        if (result == Status.Success)
        {
            state->Phase = 2;
            state->Failure = Status.Success;
            return result;
        }

        _ = Fault(state, result);
        _ = Stop(state); // Cleanup has its own retained receipt; never hide the original failure.
        return result;
    }

    private static Status StartCore(State* s)
    {
        Admission a = s->Input;
        if (a.Mmio.BusMaster(a.Mmio.Context, 0) != 1)
            return Status.SetupFailed;
        s->Queue.Status = VirtioNetQueue.Status(a.Common, a.Mmio, 11);
        if (s->Queue.Status != VirtioNetQueueStatus.Success)
            return Status.SetupFailed;
        ushort queues = 0;
        if (a.Mmio.Read16(a.Mmio.Context, a.Common.VirtualStart + 18, &queues) != 1)
            return Status.ReadFailed;
        if (queues < 2)
            return Status.SetupFailed;
        if (PageAlignedDmaBuffers.TryAllocate(a.Pool, RingEngine.BackingBytes / 4096, out s->Allocation) != DmaBufferStatus.Success)
            return Status.DmaRejected;
        s->Queue.CpuAddress = s->Allocation.Range.CpuVirtualAddress;
        s->Queue.DeviceAddress = s->Allocation.Range.DeviceAddress;
        s->RingResult = RingEngine.Initialize(&s->Ring, s->Allocation.Range, a.Slots, a.SlotCount, a.Tokens, a.TokenCount, a.Epoch, a.Mmio.Context, a.Mmio.Fence);
        s->RingBound = s->Ring.Self == (ulong)(&s->Ring);
        if (s->RingResult != RingEngine.Status.Success)
            return Status.RingFailed;
        if (PageAlignedDmaBuffers.TryGiveToDevice(a.Pool, s->Allocation.Token) != DmaBufferStatus.Success)
            return Status.DmaRejected;
        s->DeviceOwned = true;
        var plan = s->Ring.Plan;
        s->Queue.Status = VirtioNetQueue.Configure(a.Common, a.Notify, a.NotifyMultiplier, a.Mmio, plan, 0, ref s->Queue.Receive, ref s->Queue);
        if (s->Queue.Status != VirtioNetQueueStatus.Success)
            return Status.SetupFailed;
        s->Queue.Status = VirtioNetQueue.Configure(a.Common, a.Notify, a.NotifyMultiplier, a.Mmio, plan, 1, ref s->Queue.Transmit, ref s->Queue);
        if (s->Queue.Status != VirtioNetQueueStatus.Success)
            return Status.SetupFailed;
        s->Queue.Receive.Published = true;
        s->Queue.Receive.AvailableIndex = RingEngine.QueueSize;
        if (a.Mmio.BusMaster(a.Mmio.Context, 1) != 1)
            return Status.SetupFailed;
        if (!VirtioNetQueue.Fence(a.Mmio, ref s->Queue))
            return Status.OrderingFailed;
        if (a.Mmio.Write8(a.Mmio.Context, a.Common.VirtualStart + 20, 15) != 1)
            return Status.WriteFailed;
        s->Queue.Status = VirtioNetQueue.Status(a.Common, a.Mmio, 15);
        if (s->Queue.Status != VirtioNetQueueStatus.Success)
            return Status.SetupFailed;
        if (a.Mmio.Write16(a.Mmio.Context, s->Queue.Receive.Notify, 0) != 1)
            return Status.WriteFailed;
        return ReadClock(s, out _);
    }

    /// <summary>Queue one Ethernet frame and perform its real queue1 notification.
    /// Full/invalid caller input publishes nothing. Failure after publication may
    /// already have exposed the frame to hardware: stop the faulted owner and do
    /// not retry the frame. No TCP ACK or network-delivery claim is made.</summary>

    public static Status Send(State* s, ReadOnlySpan<byte> frame)
    {
        if (!Running(s))
            return Status.InvalidState;
        if (frame.Length < 14 || frame.Length > RingEngine.MaximumFrameBytes)
            return Status.InvalidFrame;
        fixed (byte* p = frame)
            if (!CallerBuffer(s, (ulong)p, (ulong)frame.Length))
                return Status.InvalidStorage;
        if (s->Ring.TxMask == 255)
            return Status.Full;
        Status clock = ReadClock(s, out ulong now);
        if (clock != Status.Success)
            return Fault(s, clock);
        if (now > ~0UL - s->Input.CompletionTimeoutNanos)
            return Fault(s, Status.ClockFailed);
        byte before = s->Ring.TxMask;
        s->RingResult = RingEngine.Enqueue(&s->Ring, frame);
        if (s->RingResult != RingEngine.Status.Success)
            return RingStatus(s);
        byte added = (byte)(s->Ring.TxMask & ~before);
        int id = 0;
        while (id < 8 && (added & (1 << id)) == 0)
            id++;
        if (id == 8 || added != 1 << id)
            return Fault(s, Status.RingFailed);
        s->Deadlines[id] = now + s->Input.CompletionTimeoutNanos;
        s->Queue.Transmit.Published = true;
        s->Queue.Transmit.AvailableIndex = s->Ring.TransmitAvailableIndex;
        if (s->Input.Mmio.Write16(s->Input.Mmio.Context, s->Queue.Transmit.Notify, 1) != 1)
            return Fault(s, Status.WriteFailed);
        return Status.Success;
    }

    /// <summary>One bounded pump: check actual status, reclaim at most1..8 used
    /// TX entries, then check each remaining TX against its original deadline.
    /// Completed entries win over timeout on this observation; continuous traffic
    /// cannot renew an older descriptor's deadline. Healthy idle success with
    /// count0 is not packet proof. Failures retain DMA until Stop succeeds.</summary>

    public static Status Poll(State* s, uint budget, out uint reclaimed)
    {
        reclaimed = 0;
        if (!Running(s) || budget == 0 || budget > 8)
            return Status.InvalidState;
        s->Queue.Status = VirtioNetQueue.Status(s->Input.Common, s->Input.Mmio, 15);
        if (s->Queue.Status != VirtioNetQueueStatus.Success)
            return Fault(s, Status.ReadFailed);
        s->RingResult = RingEngine.Reclaim(&s->Ring, budget, out reclaimed, out byte observedRemaining);
        if (s->RingResult is not (RingEngine.Status.Success or RingEngine.Status.Empty))
            return RingStatus(s);
        Status clock = ReadClock(s, out ulong now);
        if (clock != Status.Success)
            return Fault(s, clock);
        for (int i = 0; i < 8; i++)
        {
            if ((s->Ring.TxMask & (1 << i)) == 0)
                s->Deadlines[i] = 0;
            else if ((observedRemaining & (1 << i)) == 0 && now >= s->Deadlines[i])
                return Fault(s, Status.Timeout);
        }

        return Status.Success;
    }

    /// <summary>Copy a checked RX frame and actually notify queue0 after repost.
    /// ShortBuffer preserves the valid used entry and reports required bytes.
    /// Empty/ShortBuffer can follow safe content drops; notify every actual
    /// replenishment before returning either nonfatal outcome. Discard
    /// output on every other failure, including notification failure after copy.
    /// Faulted state must be stopped before any further packet operation.</summary>

    public static Status Receive(State* s, Span<byte> destination, out uint bytes)
    {
        bytes = 0;
        if (!Running(s))
            return Status.InvalidState;
        fixed (byte* p = destination)
            if (destination.Length != 0 && !CallerBuffer(s, (ulong)p, (ulong)destination.Length))
                return Status.InvalidStorage;
        s->RingResult = RingEngine.Receive(&s->Ring, destination, out bytes);
        if (s->RingResult is not (RingEngine.Status.Success or RingEngine.Status.Empty or RingEngine.Status.ShortBuffer))
            return RingStatus(s);
        if (s->Queue.Receive.AvailableIndex != s->Ring.ReceiveAvailableIndex)
        {
            s->Queue.Receive.AvailableIndex = s->Ring.ReceiveAvailableIndex;
            if (s->Input.Mmio.Write16(s->Input.Mmio.Context, s->Queue.Receive.Notify, 0) != 1)
                return Fault(s, Status.WriteFailed);
        }

        return s->RingResult == RingEngine.Status.Success ? Status.Success : RingStatus(s);
    }

    /// <summary>Bounded actual reset/readback, bus-master disable and fence must
    /// precede descriptor release/scrub, pool return and release. Retry is safe
    /// after partial cleanup: completed release stages are not repeated. Any
    /// failed stage retains the remaining ownership and mapped backing.</summary>

    public static Status Stop(State* s)
    {
        if (!Bound(s) || s->Phase == 4)
            return Status.InvalidState;
        s->Phase = 3;
        s->Queue.ResetComplete = false;
        s->Queue.BusMasterDisabled = false;
        VirtioNetQueue.Quiesce(s->Input.Common, s->Input.Mmio, s->Input.ResetLimit, ref s->Queue);
        if (s->Queue.CleanupStatus != VirtioNetQueueStatus.Success)
            return Status.ResetFailed;
        if (s->RingBound && !s->RingReleased)
        {
            s->RingResult = RingEngine.ReleaseAfterReset(&s->Ring, s->Queue);
            if (s->RingResult != RingEngine.Status.Success)
                return Status.RingFailed;
            s->RingReleased = true;
            s->Queue.Receive.DescriptorReleased = s->Queue.Transmit.DescriptorReleased = true;
        }

        if (s->Allocation.IsInitialized && !s->Queue.DmaReleased)
        {
            if (!s->RingBound)
                for (ulong i = 0; i < s->Allocation.Range.ByteLength; i++)
                    ((byte*)s->Allocation.Range.CpuVirtualAddress)[i] = 0;
            if (s->DeviceOwned && !s->ReturnedToCpu)
            {
                if (PageAlignedDmaBuffers.TryReturnToCpu(s->Input.Pool, s->Allocation.Token) != DmaBufferStatus.Success)
                    return Status.DmaRejected;
                s->ReturnedToCpu = true;
            }

            if (PageAlignedDmaBuffers.TryRelease(s->Input.Pool, s->Allocation.Token) != DmaBufferStatus.Success)
                return Status.DmaRejected;
        }

        s->Queue.DmaReleased = true;
        s->Phase = 4;
        return Status.Success;
    }

    private static Status ReadClock(State* s, out ulong now)
    {
        now = 0;
        ulong value = 0;
        if (s->Input.Clock.Read(s->Input.Clock.Context, &value) != 1)
            return Status.ClockFailed;
        if (value < s->LastClock)
            return Status.ClockRegressed;
        now = value;
        s->LastClock = value;
        return Status.Success;
    }

    private static Status RingStatus(State* s) => s->Ring.Faulted ? Fault(s, Status.RingFailed) : s->RingResult switch
    {
        RingEngine.Status.Full => Status.Full,
        RingEngine.Status.Empty => Status.Empty,
        RingEngine.Status.ShortBuffer => Status.ShortBuffer,
        RingEngine.Status.InvalidFrame => Status.InvalidFrame,
        RingEngine.Status.InvalidStorage => Status.InvalidStorage,
        _ => Fault(s, Status.RingFailed)};
    private static Status Fault(State* s, Status reason)
    {
        s->Phase = 3;
        s->Failure = reason;
        return reason;
    }

    private static bool Bound(State* s) => Storage(s, (ulong)sizeof(State)) && s->Self == (ulong)s && s->Phase is >= 1 and <= 4 && s->Input.Pool != null && s->Input.Pool->Self == (ulong)s->Input.Pool && s->Input.Pool->Epoch == s->PoolEpoch;
    private static bool Running(State* s) => Bound(s) && s->Phase == 2;
    private static bool CallerBuffer(State* s, ulong start, ulong bytes) => bytes <= ~0UL - start && !Overlap(start, bytes, (ulong)s, (ulong)sizeof(State)) && !Overlap(start, bytes, (ulong)s->Input.Pool, (ulong)sizeof(DmaBufferPool)) && !Overlap(start, bytes, (ulong)s->Input.Pool->Slots, s->Input.Pool->Count * (ulong)sizeof(DmaBufferSlot));
    private static bool Admit(State* s, in Admission a)
    {
        if (!VirtioNetQueue.Valid(a.Common, a.Notify, a.NotifyMultiplier, a.Mmio, a.ResetLimit) || a.Clock.Read == null || a.Epoch == 0 || a.CompletionTimeoutNanos is 0 or > 60000000000UL || a.SlotCount != 16 || a.TokenCount != 16 || !Storage(a.Pool, (ulong)sizeof(DmaBufferPool)) || a.Pool->Self != (ulong)a.Pool || a.Pool->Epoch == 0 || a.Pool->Count is 0 or > 64 || !a.Pool->Window.IsInitialized || a.Pool->Window.ByteLength != a.Pool->Count * 4096UL)
            return false;
        ulong* starts = stackalloc ulong[]
        {
            (ulong)s,
            (ulong)a.Slots,
            (ulong)a.Tokens,
            (ulong)a.Pool,
            (ulong)a.Pool->Slots
        };
        ulong* sizes = stackalloc ulong[]
        {
            (ulong)sizeof(State),
            16UL * (ulong)sizeof(VirtqueueDescriptorSlot),
            16UL * (ulong)sizeof(VirtqueueDescriptorToken),
            (ulong)sizeof(DmaBufferPool),
            a.Pool->Count * (ulong)sizeof(DmaBufferSlot)
        };
        for (int i = 0; i < 5; i++)
        {
            if (!Storage((void*)starts[i], sizes[i]) || Overlap(starts[i], sizes[i], a.Pool->Window.CpuVirtualBase, a.Pool->Window.ByteLength))
                return false;
            for (int j = 0; j < i; j++)
                if (Overlap(starts[i], sizes[i], starts[j], sizes[j]))
                    return false;
        }

        for (int i = 0; i < 3; i++)
            for (ulong b = 0; b < sizes[i]; b++)
                if (((byte*)starts[i])[b] != 0)
                    return false;
        return !RegionAlias(a.Common, a.Pool->Window) && !RegionAlias(a.Notify, a.Pool->Window) && !Overlap(a.Common.VirtualStart, a.Common.Bytes, a.Notify.VirtualStart, a.Notify.Bytes) && !Overlap(a.Common.PhysicalStart, a.Common.Bytes, a.Notify.PhysicalStart, a.Notify.Bytes);
    }

    private static bool RegionAlias(VirtioMappedRegion region, OS.Kernel.Platform.DmaAddressWindow window) => Overlap(region.VirtualStart, region.Bytes, window.CpuVirtualBase, window.ByteLength) || Overlap(region.PhysicalStart, region.Bytes, window.PhysicalBase, window.ByteLength);
    private static bool Storage(void* p, ulong bytes) => p != null && ((ulong)p & 7) == 0 && bytes != 0 && bytes <= ~0UL - (ulong)p;
    private static bool Overlap(ulong a, ulong bytes, ulong b, ulong other) => a < b + other && b < a + bytes;
}
