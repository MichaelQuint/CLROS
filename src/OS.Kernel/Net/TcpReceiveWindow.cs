namespace OS.Net;
/// <summary>
/// Persistent receive ownership over an established TCP table generation. One
/// serialized kernel dispatcher retains trusted mapped owner/buffer/table storage;
/// unchecked guest pointers and aliased output results are unsupported. Receive
/// commits bytes once and schedules a cumulative ACK. Drain the current RX batch
/// before PumpAck so queued packets share an ACK. No NIC, socket, out-of-order
/// queue, send retransmission or timer is supplied. Callbacks must not reenter or
/// mutate owner/table metadata and must copy the whole frame before returning1.
/// </summary>

public static unsafe partial class TcpReceiveWindow
{
    /// <summary>Bind an established handle to one zeroed receiver and 1..32768
    /// zeroed bytes, disjoint from all metadata. Info's initial window must match
    /// capacity and its IP endpoints must match the table; caller uses this same
    /// capacity in the SYN-ACK. A second binding cannot steal the receiver. Send
    /// returns1 for accepted copied output,0 for full, otherwise fatal. Invalid
    /// admission changes nothing and allocates no memory.</summary>

    public static Status Initialize(Owner* owner, TcpConnectionTable.Owner* table, TcpConnectionTable.Handle handle, byte* buffer, uint capacity, TcpEstablishedStreamIpv4FramePlannerInfo info, void* context, delegate*<void*, byte*, uint, byte> send)
    {
        if (!Storage(owner, (ulong)sizeof(Owner)) || capacity is 0 or > 32768 || buffer == null || capacity > ~0UL - (ulong)buffer)
            return Status.InvalidStorage;
        if (TcpConnectionTable.ReadOwned(table, handle, out var entry) != TcpConnectionTable.OwnershipStatus.Success || entry.State != TcpConnectionLifecycleState.Established || entry.ReceiveOwnerAddress != 0)
            return Status.TableRejected;
        if (!OutsideTable(table, (byte*)owner, (ulong)sizeof(Owner)) || !OutsideTable(table, buffer, capacity) || Overlap((byte*)owner, (ulong)sizeof(Owner), buffer, capacity) || !Zero(owner, (ulong)sizeof(Owner)) || !Zero(buffer, capacity))
            return Status.InvalidStorage;
        if (send == null || info.WindowSize != capacity || info.MaximumPayloadLength is < 1 or > 1460 || info.LocalAddress != entry.Key.LocalAddress || info.RemoteAddress != entry.Key.RemoteAddress || !Unicast(info.LocalMac) || !Unicast(info.RemoteMac))
            return Status.InvalidOptions;
        owner->Self = (ulong)owner;
        owner->Table = table;
        owner->Handle = handle;
        owner->Buffer = buffer;
        owner->CapacityValue = (int)capacity;
        owner->Info = info;
        owner->RemoteNext = entry.Sequence.RemoteNextSequenceNumber;
        owner->Context = context;
        owner->Send = send;
        owner->Phase = 1;
        table->Entries[handle.Index].ReceiveOwnerAddress = (ulong)owner;
        return Status.Success;
    }

    /// <summary>Validate a frame and retain only new contiguous bytes within
    /// free capacity. Duplicate/gap/full inputs schedule ACK without delivering
    /// bytes; pure acceptable ACKs return NoPayload without an ACK loop. Old
    /// overlap and window excess are trimmed, gaps are dropped. Original packet
    /// offsets remain in result, but consumers read accepted bytes from Read.
    /// Input is distinct from owner/table/private receive storage. No TX occurs.</summary>

    public static Status Receive(Owner* owner, ReadOnlySpan<byte> frame, out TcpEstablishedStreamIpv4FramePlanResult packet) => ReceiveCore(owner, frame, out packet, false);
    internal static Status ReceiveCloseFrame(Owner* owner, ReadOnlySpan<byte> frame, out TcpEstablishedStreamIpv4FramePlanResult packet) => ReceiveCore(owner, frame, out packet, true);
    private static Status ReceiveCore(Owner* owner, ReadOnlySpan<byte> frame, out TcpEstablishedStreamIpv4FramePlanResult packet, bool allowFin)
    {
        packet = default;
        var status = Validate(owner, out var entry);
        if (status != Status.Success)
            return status;
        fixed (byte* input = frame)
            if (!Outside(owner, input, (ulong)frame.Length))
                return Status.InvalidStorage;
        if (frame.Length is < 54 or > 1514 || (frame[20] & 0xbf) != 0 || frame[21] != 0)
            return Status.InvalidPacket;
        var stream = Borrow(owner, entry);
        uint before = owner->RemoteNext;
        ushort window = owner->AvailableWindow;
        if (owner->PeerClosed != 0)
        {
            if (TcpEstablishedStreamIpv4FramePlanner.ValidateFrame(stream, owner->Info, frame, out packet) != TcpEstablishedStreamIpv4FramePlanStatus.Success || (packet.TcpHeader.Flags & 0x10) == 0 || (packet.TcpHeader.Flags & ~(allowFin ? 0x19 : 0x18)) != 0)
                return Status.InvalidPacket;
            if (allowFin && (packet.TcpHeader.Flags & 1) != 0 && unchecked(packet.TcpHeader.SequenceNumber + (uint)packet.PayloadBytes + 1) == owner->RemoteNext)
            {
                owner->AckPending = 1;
                return Status.PeerClosed;
            }

            if ((packet.TcpHeader.Flags & 1) != 0)
            {
                owner->AckPending = 1;
                return Status.InvalidPacket;
            }

            if (packet.PayloadBytes != 0)
            {
                owner->AckPending = 1;
                return Status.InvalidPacket;
            }

            return Status.NoPayload;
        }

        var parsed = allowFin ? TcpEstablishedStreamIpv4FramePlanner.ReceiveFinWindowFrame(ref stream, owner->Info, frame, out packet) : TcpEstablishedStreamIpv4FramePlanner.ReceiveWindowFrame(ref stream, owner->Info, frame, out packet);
        if (parsed == TcpEstablishedStreamIpv4FramePlanStatus.Success)
        {
            owner->Offset = stream.ReceiveOffset;
            owner->Unread = stream.UnreadBytes;
            owner->RemoteNext = stream.RemoteNextSequenceNumber;
            status = Status.Success;
            owner->AckPending = 1;
        }
        else
        {
            if (parsed != TcpEstablishedStreamIpv4FramePlanStatus.TcpStreamFailed)
                return Status.InvalidPacket;
            status = packet.StreamStatus switch
            {
                TcpEstablishedStreamStatus.DuplicateSegment => Status.Duplicate,
                TcpEstablishedStreamStatus.OutOfOrderSegment => Status.OutOfOrder,
                TcpEstablishedStreamStatus.ReceiveBufferTooSmall => Status.WindowFull,
                TcpEstablishedStreamStatus.NoPayload => Status.NoPayload,
                _ => Status.InvalidPacket
            };
        }

        // On a successful trimmed receive PayloadBytes counts delivered bytes;
        // FIN is positioned after the original wire payload, including overlap.
        uint fin = unchecked(packet.TcpHeader.SequenceNumber + (uint)(packet.TcpSegmentLength - packet.TcpHeader.HeaderLength));
        if (allowFin && (packet.TcpHeader.Flags & 1) != 0 && (status is Status.Success or Status.Duplicate or Status.NoPayload) && fin == owner->RemoteNext && unchecked(fin - before) < window)
        {
            owner->RemoteNext = unchecked(owner->RemoteNext + 1);
            owner->PeerClosed = 1;
            owner->AckPending = 1;
            status = Status.PeerClosed;
        }

        if (status is Status.Success or Status.PeerClosed)
            owner->Table->Entries[owner->Handle.Index] = entry.WithMetadata(entry.State, new TcpConnectionSequenceMetadata(entry.Sequence.LocalNextSequenceNumber, owner->RemoteNext, entry.Sequence.LocalAcknowledgedSequenceNumber));
        if (status is Status.Duplicate or Status.OutOfOrder or Status.WindowFull)
            owner->AckPending = 1;
        return status;
    }

    /// <summary>Copy bounded unread bytes once, scrub consumed storage and
    /// schedule a window update. No data/short destination consumes nothing.
    /// Destination and result are distinct caller storage outside all ownership.
    /// Sequence progress acknowledges receipt, not application consumption.
    /// retainedClosed permits unread bytes from an actually closed, retained
    /// table handle after the caller identifies its terminal error; it creates
    /// no FIN/EOF and grants no authority over released or stale storage.</summary>

    public static Status Read(Owner* owner, Span<byte> destination, out TcpEstablishedStreamReadResult result, bool retainedClosed = false)
    {
        result = default;
        bool allowClosed = Storage(owner, (ulong)sizeof(Owner)) && (owner->PeerClosed == 1 || retainedClosed);
        var status = Validate(owner, out var entry, allowClosed);
        if (status != Status.Success)
            return status;
        if (owner->PeerClosed != 0 && owner->Unread == 0)
            return Status.EndOfStream;
        fixed (byte* output = destination)
            if (!Outside(owner, output, (ulong)destination.Length))
                return Status.InvalidStorage;
        var stream = Borrow(owner, entry);
        int offset = owner->Offset;
        var read = stream.Read(destination, out result);
        if (read != TcpEstablishedStreamStatus.Success)
            return read == TcpEstablishedStreamStatus.NoData ? Status.NoData : Status.OutputTooSmall;
        for (int i = 0; i < result.BytesRead; i++)
            owner->Buffer[offset + i] = 0;
        owner->Offset = stream.ReceiveOffset;
        owner->Unread = stream.UnreadBytes;
        owner->AckPending = 1;
        return Status.Success;
    }

    /// <summary>After draining a bounded RX batch, attempt one ACK for the
    /// latest sequence/free window. Full/short output retains pending progress;
    /// only actual copied TX admission clears it. Fatal TX retains buffer/table
    /// ownership for explicit closed cleanup. No frame pointer escapes Send.</summary>

    public static Status PumpAck(Owner* owner, Span<byte> output, out TcpEstablishedStreamIpv4FramePlanResult packet)
    {
        packet = default;
        var status = Validate(owner, out var entry);
        if (status != Status.Success)
            return status;
        if (owner->AckPending == 0)
            return Status.NothingToAck;
        if (output.Length < TcpEstablishedStreamIpv4FramePlanner.HeaderBytes)
            return Status.OutputTooSmall;
        fixed (byte* bytes = output)
            if (!Outside(owner, bytes, (ulong)output.Length))
                return Status.InvalidStorage;
        var stream = Borrow(owner, entry);
        if (TcpEstablishedStreamIpv4FramePlanner.TryWriteAckFrame(ref stream, owner->Info, (ushort)(owner->CapacityValue - owner->Unread), output, out packet) != TcpEstablishedStreamIpv4FramePlanStatus.Success)
            return Status.InvalidPacket;
        byte sent;
        fixed (byte* bytes = output)
            sent = owner->Send(owner->Context, bytes, (uint)packet.BytesWritten);
        if (sent == 0)
            return Status.WouldBlock;
        if (sent != 1)
        {
            owner->Phase = 2;
            return Status.SendFailed;
        }

        owner->AckPending = 0;
        return Status.Success;
    }

    /// <summary>After the close/abort consumer marks this exact table lifetime
    /// Closed and stops receive delivery, scrub the whole private buffer and
    /// detach it. This explicitly discards unread data. Only then may the caller
    /// release the table entry or backing lease. No NIC/DMA quiescence is asserted.</summary>

    public static Status ReleaseClosed(Owner* owner)
    {
        var status = Validate(owner, out var entry, closed: true);
        if (status != Status.Success)
            return status;
        if (entry.State != TcpConnectionLifecycleState.Closed)
            return Status.InvalidState;
        for (int i = 0; i < owner->CapacityValue; i++)
            owner->Buffer[i] = 0;
        owner->Table->Entries[owner->Handle.Index].ReceiveOwnerAddress = 0;
        owner->Buffer = null;
        owner->Context = null;
        owner->Send = null;
        owner->Info = default;
        owner->Handle = default;
        owner->Unread = 0;
        owner->Offset = 0;
        owner->AckPending = 0;
        owner->RemoteNext = 0;
        owner->Phase = 3;
        return Status.Success;
    }

    internal static Status Validate(Owner* owner, out TcpConnectionTableEntry entry, bool closed = false)
    {
        entry = default;
        if (!Storage(owner, (ulong)sizeof(Owner)) || owner->Self != (ulong)owner || (owner->Phase != 1 && !(closed && owner->Phase == 2)))
            return Status.InvalidState;
        if (TcpConnectionTable.ReadOwned(owner->Table, owner->Handle, out entry) != TcpConnectionTable.OwnershipStatus.Success || entry.ReceiveOwnerAddress != (ulong)owner || entry.State != TcpConnectionLifecycleState.Established && !(closed && entry.State == TcpConnectionLifecycleState.Closed))
            return Status.TableRejected;
        if (owner->Buffer == null || owner->CapacityValue is < 1 or > 32768 || owner->Offset < 0 || owner->Offset > owner->CapacityValue || owner->Unread < 0 || owner->Unread > owner->CapacityValue - owner->Offset || owner->RemoteNext != entry.Sequence.RemoteNextSequenceNumber || owner->AckPending > 1 || owner->PeerClosed > 1 || owner->Send == null || !OutsideTable(owner->Table, (byte*)owner, (ulong)sizeof(Owner)) || !OutsideTable(owner->Table, owner->Buffer, (ulong)owner->CapacityValue) || Overlap((byte*)owner, (ulong)sizeof(Owner), owner->Buffer, (ulong)owner->CapacityValue))
            return Status.InvalidState;
        return Status.Success;
    }

    private static TcpEstablishedStream Borrow(Owner* owner, TcpConnectionTableEntry entry) => TcpEstablishedStream.Restore(entry.Key.LocalPort, entry.Key.RemotePort, entry.Sequence.LocalNextSequenceNumber, owner->RemoteNext, new Span<byte>(owner->Buffer, owner->CapacityValue), owner->Offset, owner->Unread, entry.Sequence.LocalAcknowledgedSequenceNumber);
    private static bool Unicast(MacAddress mac) => !mac.IsZero && (mac.Octet0 & 1) == 0;
    private static bool Outside(Owner* owner, byte* address, ulong bytes) => OutsideTable(owner->Table, address, bytes) && !Overlap((byte*)owner, (ulong)sizeof(Owner), address, bytes) && !Overlap(owner->Buffer, (ulong)owner->CapacityValue, address, bytes);
    private static bool OutsideTable(TcpConnectionTable.Owner* table, byte* address, ulong bytes) => bytes == 0 || address != null && bytes <= ~0UL - (ulong)address && !Overlap((byte*)table, (ulong)sizeof(TcpConnectionTable.Owner), address, bytes) && !Overlap((byte*)table->Entries, (ulong)table->CapacityValue * (ulong)sizeof(TcpConnectionTableEntry), address, bytes) && !Overlap((byte*)table->Generations, (ulong)table->CapacityValue * 8, address, bytes);
    private static bool Overlap(byte* a, ulong size, byte* b, ulong other) => size != 0 && other != 0 && (ulong)a < (ulong)b + other && (ulong)b < (ulong)a + size;
    private static bool Storage(void* address, ulong bytes) => address != null && ((ulong)address & 7) == 0 && bytes <= ~0UL - (ulong)address;
    private static bool Zero(void* address, ulong bytes)
    {
        for (ulong i = 0; i < bytes; i++)
            if (((byte*)address)[i] != 0)
                return false;
        return true;
    }
}
