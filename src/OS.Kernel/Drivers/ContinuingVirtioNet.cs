using OS.Kernel.Platform;

namespace OS.Drivers;

/// <summary>Retained size-eight split RX/TX ring engine for the admitted modern
/// no-offload, single-buffer network profile. One IRQ-off owner supplies stable,
/// exclusive state/metadata and coherent WB/RW/NX DMA backing. No call allocates,
/// polls indefinitely, configures a device, notifies MMIO or proves hardware
/// readiness. The live adapter owns negotiation, queue programming, real fences,
/// notification, monotonic service budgets and reset before releasing DMA.
/// Faults retain all backing and descriptor ownership for that reset path.</summary>
public static unsafe class ContinuingVirtioNet
{
    public const ushort QueueSize = 8;
    public const uint RingBytes = 256, TransmitRingOffset = VirtioNetQueue.TransmitRingOffset,
        BufferBytes = 1536, BuffersOffset = 4096, BackingBytes = 32768;
    public const int MaximumFrameBytes = 1514;
    public enum Status : byte
    {
        Uninitialized, Success, Empty, Full, ShortBuffer, InvalidStorage, InvalidState,
        InvalidFrame, InvalidCompletion, OwnershipFailed, OrderingFailed, NotQuiescent
    }

    /// <summary>Borrowed owner state, never copied or externally mutated while
    /// bound. Slots/tokens contain sixteen entries, RX first. Retain all storage
    /// through actual device reset/bus-master disable/fence and ReleaseAfterReset.
    /// Stopped state is retired; reuse requires a distinct fresh state/epoch.</summary>
    public struct State
    {
        internal ulong Self, Epoch;
        internal byte* Data;
        internal DmaAddressRange Range;
        internal VirtqueueSplitRingPlan Plan;
        internal VirtqueueDescriptorPoolState Rx, Tx;
        internal VirtqueueDescriptorToken* Tokens;
        internal void* Context;
        internal delegate* unmanaged<void*, byte> Fence;
        internal ushort RxAvailable, RxUsed, TxAvailable, TxUsed;
        internal byte TxMask, Phase;
        internal ulong Received, Reclaimed, Dropped;
        public readonly bool Faulted => Phase == 2;
        public readonly bool Retired => Phase == 3;
        public readonly ushort ReceiveAvailableIndex => RxAvailable;
        public readonly ushort ReceiveUsedIndex => RxUsed;
        public readonly ushort TransmitAvailableIndex => TxAvailable;
        public readonly ushort TransmitUsedIndex => TxUsed;
        public readonly ulong ReceivedFrames => Received;
        /// <summary>Unsupported packets whose validated receive buffers were
        /// recycled without publishing payload. Saturation faults before reuse.</summary>
        public readonly ulong DroppedFrames => Dropped;
        public readonly ulong ReclaimedTransmits => Reclaimed;
    }

    /// <summary>Prepare real descriptor/ring bytes and publish eight receive
    /// buffers into caller-borrowed DMA. The device must still be quiescent and
    /// its queues unbound. This does not transfer DMA-pool ownership or enable
    /// hardware. Invalid admission changes nothing; an ordering failure after
    /// preparation poisons the state and requires the normal reset cleanup.</summary>
    public static Status Initialize(State* state, DmaAddressRange range,
        VirtqueueDescriptorSlot* slots, uint slotCount, VirtqueueDescriptorToken* tokens, uint tokenCount, ulong epoch,
        void* context, delegate* unmanaged<void*, byte> fence)
    {
        ulong stateBytes = (ulong)sizeof(State), slotBytes = 16UL * (ulong)sizeof(VirtqueueDescriptorSlot),
            tokenBytes = 16UL * (ulong)sizeof(VirtqueueDescriptorToken);
        if (slotCount != 16 || tokenCount != 16 || !Storage(state, stateBytes) || !Storage(slots, slotBytes) || !Storage(tokens, tokenBytes) ||
            !range.IsInitialized || range.ByteLength != BackingBytes || epoch == 0 || fence == null ||
            range.CpuVirtualAddress == 0 || range.CpuVirtualAddress > ~0UL - BackingBytes ||
            range.DeviceAddress > ~0UL - BackingBytes ||
            Overlap((ulong)state, stateBytes, (ulong)slots, slotBytes) ||
            Overlap((ulong)state, stateBytes, (ulong)tokens, tokenBytes) ||
            Overlap((ulong)slots, slotBytes, (ulong)tokens, tokenBytes) ||
            Overlap((ulong)state, stateBytes, range.CpuVirtualAddress, BackingBytes) ||
            Overlap((ulong)slots, slotBytes, range.CpuVirtualAddress, BackingBytes) ||
            Overlap((ulong)tokens, tokenBytes, range.CpuVirtualAddress, BackingBytes)) return Status.InvalidStorage;
        for (ulong i = 0; i < stateBytes; i++) if (((byte*)state)[i] != 0) return Status.InvalidState;
        for (ulong i = 0; i < slotBytes; i++) if (((byte*)slots)[i] != 0) return Status.InvalidStorage;
        for (ulong i = 0; i < tokenBytes; i++) if (((byte*)tokens)[i] != 0) return Status.InvalidStorage;
        var plan = VirtqueueSplitRingPlan.Create(QueueSize);
        if (!plan.Succeeded || plan.Plan.EndOffset > RingBytes ||
            BuffersOffset + 16 * BufferBytes > BackingBytes) return Status.InvalidStorage;
        state->Self = (ulong)state; state->Epoch = epoch; state->Range = range;
        state->Data = (byte*)range.CpuVirtualAddress; state->Tokens = tokens;
        state->Context = context; state->Fence = fence; state->Plan = plan.Plan; state->Phase = 1;
        if (VirtqueueDescriptors.TryInitialize(&state->Rx, slots, QueueSize, epoch) != VirtqueueDescriptorStatus.Success ||
            VirtqueueDescriptors.TryInitialize(&state->Tx, slots + QueueSize, QueueSize, epoch) != VirtqueueDescriptorStatus.Success)
            return Fail(state, Status.OwnershipFailed);
        for (uint i = 0; i < BackingBytes; i++) state->Data[i] = 0;
        *(ushort*)(state->Data + state->Plan.AvailableRing.Offset) = 1; // NO_INTERRUPT: bounded polling owner.
        *(ushort*)(state->Data + TransmitRingOffset + state->Plan.AvailableRing.Offset) = 1;
        for (ushort i = 0; i < QueueSize; i++)
        {
            if (VirtqueueDescriptors.TryAllocate(&state->Rx, out tokens[i]) != VirtqueueDescriptorStatus.Success || tokens[i].Index != i)
                return Fail(state, Status.OwnershipFailed);
            Descriptor(state, false, i, BufferBytes);
            if (VirtqueueDescriptors.TryPublish(&state->Rx, tokens[i]) != VirtqueueDescriptorStatus.Success)
                return Fail(state, Status.OwnershipFailed);
            *(ushort*)(state->Data + state->Plan.AvailableRing.Offset + 4 + i * 2) = i;
        }
        if (!Barrier(state)) return Fail(state, Status.OrderingFailed);
        state->RxAvailable = QueueSize;
        *(ushort*)(state->Data + state->Plan.AvailableRing.Offset + 2) = QueueSize;
        return Barrier(state) ? Status.Success : Fail(state, Status.OrderingFailed);
    }

    /// <summary>Copy one Ethernet frame and publish a real TX descriptor.
    /// Success means queued memory, not transmission; the adapter must notify
    /// queue1 afterward. Full/invalid input leaves the queues untouched. Failed
    /// ordering retains the descriptor and requires reset; do not retry it.</summary>
    public static Status Enqueue(State* state, ReadOnlySpan<byte> frame)
    {
        if (!Valid(state)) return Status.InvalidState;
        if (frame.Length < 14 || frame.Length > MaximumFrameBytes) return Status.InvalidFrame;
        fixed (byte* source = frame)
            if (Aliases(state, (ulong)source, (ulong)frame.Length)) return Status.InvalidStorage;
        if (state->TxMask == 255) return Status.Full;
        var status = VirtqueueDescriptors.TryAllocate(&state->Tx, out var token);
        if (status != VirtqueueDescriptorStatus.Success) return Fail(state, Status.OwnershipFailed);
        ushort id = token.Index;
        if (id >= QueueSize || (state->TxMask & (1 << id)) != 0) return Fail(state, Status.OwnershipFailed);
        state->Tokens[QueueSize + id] = token;
        byte* buffer = Buffer(state, true, id);
        for (int i = 0; i < 12; i++) buffer[i] = 0;
        for (int i = 0; i < frame.Length; i++) buffer[12 + i] = frame[i];
        Descriptor(state, true, id, (uint)frame.Length + 12);
        if (VirtqueueDescriptors.TryPublish(&state->Tx, token) != VirtqueueDescriptorStatus.Success)
            return Fail(state, Status.OwnershipFailed);
        state->TxMask |= (byte)(1 << id);
        byte* available = state->Data + TransmitRingOffset + state->Plan.AvailableRing.Offset;
        *(ushort*)(available + 4 + (state->TxAvailable & 7) * 2) = id;
        if (!Barrier(state)) return Fail(state, Status.OrderingFailed);
        state->TxAvailable = unchecked((ushort)(state->TxAvailable + 1));
        *(ushort*)(available + 2) = state->TxAvailable;
        return Barrier(state) ? Status.Success : Fail(state, Status.OrderingFailed);
    }

    /// <summary>Reclaim at most budget1..8 actual used TX entries. Validate the
    /// complete observed batch before releasing any descriptor, including IDs,
    /// duplicate/stale ownership, zero device-written length and index distance.
    /// A protocol/order fault retains backing until reset.</summary>
    public static Status Reclaim(State* state, uint budget, out uint count)
        => Reclaim(state, budget, out count, out _);

    // The retained owner needs the validated, already-completed IDs left by a
    // small budget so it cannot time them out before the next bounded reclaim.
    internal static Status Reclaim(State* state, uint budget, out uint count, out byte observedRemaining)
    {
        count = 0; observedRemaining = 0;
        if (!Valid(state) || budget == 0 || budget > QueueSize) return Status.InvalidState;
        Status status = Pending(state, true, out ushort pending, out byte observed);
        if (status != Status.Success) return status;
        if (pending == 0) return Status.Empty;
        uint take = pending < budget ? pending : budget;
        if (state->Reclaimed > ~0UL - take) return Fail(state, Status.InvalidState);
        for (uint i = 0; i < take; i++)
        {
            ushort id = (ushort)*(uint*)UsedEntry(state, true, state->TxUsed);
            var token = state->Tokens[QueueSize + id];
            if (VirtqueueDescriptors.TryReclaim(&state->Tx, token) != VirtqueueDescriptorStatus.Success ||
                VirtqueueDescriptors.TryRelease(&state->Tx, token) != VirtqueueDescriptorStatus.Success)
                return Fail(state, Status.OwnershipFailed);
            state->TxMask &= (byte)~(1 << id);
            state->Tokens[QueueSize + id] = default;
            state->TxUsed = unchecked((ushort)(state->TxUsed + 1)); count++;
        }
        state->Reclaimed += count;
        observedRemaining = (byte)(observed & state->TxMask);
        return Status.Success;
    }

    /// <summary>Copy one complete no-offload single-buffer RX frame, then repost
    /// that same owned buffer. ShortBuffer returns required Ethernet bytes without
    /// consumption of that frame or output writes. Unsupported in-buffer content
    /// is dropped/recycled, at most eight buffers per call. A bounded merged
    /// discard waits for its complete chain; no merged payload is delivered.
    /// Empty/ShortBuffer can therefore follow replenishment. No borrowed DMA
    /// span escapes. Unsafe used records/counts and ordering faults poison state.
    /// Output may contain the frame if a later repost fails; consume it only on
    /// Success. The adapter must notify queue0 whenever the available index
    /// advances, including Empty/ShortBuffer after preceding drops.</summary>
    public static Status Receive(State* state, Span<byte> destination, out uint bytes)
    {
        bytes = 0;
        if (!Valid(state)) return Status.InvalidState;
        fixed (byte* target = destination)
            if (destination.Length != 0 && Aliases(state, (ulong)target, (ulong)destination.Length)) return Status.InvalidStorage;
        Status status = Pending(state, false, out ushort pending, out _);
        if (status != Status.Success) return status;
        if (pending == 0) return Status.Empty;
        byte* entry = UsedEntry(state, false, state->RxUsed);
        // Consume at most one queue's worth of policy rejects. A good packet
        // following them is delivered normally; no extra owner/adapter status
        // or unbounded polling is required.
        uint budget = QueueSize;
        while (budget != 0)
        {
            entry = UsedEntry(state, false, state->RxUsed);
            ushort id = (ushort)*(uint*)entry; uint length = *(uint*)(entry + 4);
            byte* buffer = Buffer(state, false, id);
            uint buffers = length < 12 ? 1U : (uint)(buffer[10] | buffer[11] << 8);
            bool invalidCount = buffers == 0;
            if (invalidCount) buffers = 1;
            // A merged packet cannot borrow beyond this owner's entire ring.
            // Wait for all of a bounded discard chain before recycling it.
            if (buffers > QueueSize) return Fail(state, Status.InvalidCompletion);
            if (buffers > pending || buffers > budget) return Status.Empty;
            var policy = VirtioNetHeaderChecksumPolicy.Validate(BoundedVirtioFeatures.NetSingleBufferReceive,
                new ReadOnlySpan<byte>(buffer, (int)length));
            if (invalidCount || !policy.Succeeded || length > MaximumFrameBytes + 12)
            {
                if (state->Dropped == ~0UL) return Fail(state, Status.InvalidState);
                for (uint i = 0; i < buffers; i++)
                {
                    ushort rejected = (ushort)*(uint*)UsedEntry(state, false, state->RxUsed);
                    status = RecycleReceive(state, rejected); if (status != Status.Success) return status;
                }
                state->Dropped++; budget -= buffers;
                status = Pending(state, false, out pending, out _);
                if (status != Status.Success) return status;
                if (pending == 0) return Status.Empty;
                continue;
            }
            bytes = length - 12;
            if (destination.Length < bytes) return Status.ShortBuffer;
            if (state->Received == ~0UL) return Fail(state, Status.InvalidState);
            for (int i = 0; i < bytes; i++) destination[i] = buffer[12 + i];
            status = RecycleReceive(state, id); if (status != Status.Success) return status;
            state->Received++; return Status.Success;
        }
        return Status.Empty;
    }

    private static Status RecycleReceive(State* state, ushort id)
    {
        var token = state->Tokens[id];
        if (VirtqueueDescriptors.TryReclaim(&state->Rx, token) != VirtqueueDescriptorStatus.Success ||
            VirtqueueDescriptors.TryPublish(&state->Rx, token) != VirtqueueDescriptorStatus.Success)
            return Fail(state, Status.OwnershipFailed);
        byte* available = state->Data + state->Plan.AvailableRing.Offset;
        *(ushort*)(available + 4 + (state->RxAvailable & 7) * 2) = id;
        if (!Barrier(state)) return Fail(state, Status.OrderingFailed);
        state->RxAvailable = unchecked((ushort)(state->RxAvailable + 1));
        *(ushort*)(available + 2) = state->RxAvailable;
        if (!Barrier(state)) return Fail(state, Status.OrderingFailed);
        state->RxUsed = unchecked((ushort)(state->RxUsed + 1));
        return Status.Success;
    }

    /// <summary>Release descriptor metadata and scrub borrowed DMA only after the
    /// trusted adapter actually resets the device, disables bus mastering and
    /// fences. The receipt records that decision; values alone grant no hardware
    /// authority. Failed proof/fence retains everything. The adapter still owns
    /// DMA-pool return/release and all mappings after successful retirement.</summary>
    public static Status ReleaseAfterReset(State* state, in VirtioNetQueueResult quiescence)
    {
        if (!Bound(state) || state->Phase == 3) return Status.InvalidState;
        if (!quiescence.ResetComplete || !quiescence.BusMasterDisabled ||
            quiescence.CleanupStatus != VirtioNetQueueStatus.Success) return Status.NotQuiescent;
        if (!Barrier(state)) return Status.OrderingFailed;
        for (int i = 0; i < 16; i++)
        {
            var token = state->Tokens[i]; if (!token.IsInitialized) continue;
            VirtqueueDescriptorPoolState* pool = i < QueueSize ? &state->Rx : &state->Tx;
            // Every initialized RX token is published. A failed TX publish may
            // leave a reserved token; reclaim rejects that without mutation.
            var reclaim = VirtqueueDescriptors.TryReclaim(pool, token);
            if (reclaim != VirtqueueDescriptorStatus.Success && reclaim != VirtqueueDescriptorStatus.InvalidOwnership)
                return Fail(state, Status.OwnershipFailed);
            if (VirtqueueDescriptors.TryRelease(pool, token) != VirtqueueDescriptorStatus.Success)
                return Fail(state, Status.OwnershipFailed);
            state->Tokens[i] = default;
        }
        for (uint i = 0; i < BackingBytes; i++) state->Data[i] = 0;
        state->TxMask = 0; state->Phase = 3;
        return Status.Success;
    }

    private static Status Pending(State* s, bool tx, out ushort pending, out byte seen)
    {
        pending = 0; seen = 0;
        if (!Barrier(s)) return Fail(s, Status.OrderingFailed);
        ushort producer = *(ushort*)(s->Data + (tx ? TransmitRingOffset : 0) + s->Plan.UsedRing.Offset + 2);
        pending = unchecked((ushort)(producer - (tx ? s->TxUsed : s->RxUsed)));
        if (pending > QueueSize) return Fail(s, Status.InvalidCompletion);
        if (!Barrier(s)) return Fail(s, Status.OrderingFailed);
        for (uint i = 0; i < pending; i++)
        {
            byte* entry = UsedEntry(s, tx, unchecked((ushort)((tx ? s->TxUsed : s->RxUsed) + i)));
            uint id = *(uint*)entry, length = *(uint*)(entry + 4);
            if (id >= QueueSize || (seen & (1 << (int)id)) != 0 ||
                (tx && ((s->TxMask & (1 << (int)id)) == 0 || length != 0)) ||
                (!tx && length > BufferBytes)) return Fail(s, Status.InvalidCompletion);
            seen |= (byte)(1 << (int)id);
        }
        return Status.Success;
    }

    private static void Descriptor(State* s, bool tx, ushort id, uint length)
    {
        byte* d = s->Data + (tx ? TransmitRingOffset : 0) + id * 16;
        *(ulong*)d = s->Range.DeviceAddress + BuffersOffset + (uint)(id + (tx ? QueueSize : 0)) * BufferBytes;
        *(uint*)(d + 8) = length; *(ushort*)(d + 12) = (ushort)(tx ? 0 : 2); *(ushort*)(d + 14) = 0;
    }
    private static byte* Buffer(State* s, bool tx, ushort id) => s->Data + BuffersOffset + (uint)(id + (tx ? QueueSize : 0)) * BufferBytes;
    private static byte* UsedEntry(State* s, bool tx, ushort ordinal) => s->Data + (tx ? TransmitRingOffset : 0) + s->Plan.UsedRing.Offset + 4 + (ordinal & 7) * 8;
    private static bool Barrier(State* s) => s->Fence(s->Context) == 1;
    private static Status Fail(State* s, Status status) { s->Phase = 2; return status; }
    private static bool Bound(State* s) => Storage(s, (ulong)sizeof(State)) && s->Self == (ulong)s && s->Epoch != 0 && s->Data != null && s->Tokens != null && s->Fence != null;
    private static bool Valid(State* s) => Bound(s) && s->Phase == 1;
    private static bool Storage(void* p, ulong bytes) => p != null && ((ulong)p & 7) == 0 && bytes <= ~0UL - (ulong)p;
    private static bool Overlap(ulong a, ulong bytes, ulong b, ulong other) => a < b + other && b < a + bytes;
    private static bool Aliases(State* s, ulong address, ulong bytes) => bytes > ~0UL - address ||
        Overlap(address, bytes, s->Range.CpuVirtualAddress, BackingBytes) ||
        Overlap(address, bytes, (ulong)s, (ulong)sizeof(State)) ||
        Overlap(address, bytes, (ulong)s->Tokens, 16UL * (ulong)sizeof(VirtqueueDescriptorToken)) ||
        Overlap(address, bytes, (ulong)s->Rx.Slots, 16UL * (ulong)sizeof(VirtqueueDescriptorSlot));
}
