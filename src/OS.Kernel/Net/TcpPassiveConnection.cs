namespace OS.Net;
/// <summary>
/// Owns one bounded passive handshake over the existing connection table and backlog.
/// A serialized kernel dispatcher supplies trusted mapped storage and selects the
/// endpoint; frame identity/checksum validation still runs here. No allocation,
/// sockets, data stream, timer interrupt or NIC driver is supplied. All callbacks
/// are synchronous/nonreentrant and must not mutate owner/table/backlog metadata.
/// </summary>

public static unsafe partial class TcpPassiveConnection
{
    /// <summary>Validate a SYN, reserve its tuple/sequence lifetime, and attempt
    /// the first reply. Inputs/outputs/state/table storage must be disjoint.
    /// WouldBlock retains the pending owner and original deadline; retry it with
    /// Pump or a duplicate SYN. No ACK can establish before a reply was accepted.
    /// Options bound the lifetime to60s and accepted initial/retry attempts to1..8.
    /// Invalid admission publishes no owner. Never pass unchecked guest pointers.</summary>

    public static Status Begin(State* state, TcpConnectionTable.Owner* table, TcpPassiveIpv4FramePlannerInfo info, ReadOnlySpan<byte> syn, Span<byte> output, Operations operations, out TcpPassiveIpv4FramePlanResult packet)
    {
        packet = default;
        if (!Storage(state, (ulong)sizeof(State)) || TcpConnectionTable.ReadOwned(table, default, out _) != TcpConnectionTable.OwnershipStatus.StaleHandle || !OutsideTable(table, (byte*)state, (ulong)sizeof(State)) || !Zero(state, (ulong)sizeof(State)))
            return Status.InvalidStorage;
        if (!Valid(operations))
            return Status.InvalidOptions;
        if (output.Length < TcpPassiveIpv4FramePlanner.HeaderBytes)
            return Status.OutputTooSmall;
        if (!FramesOutside(state, table, syn, output))
            return Status.InvalidStorage;
        if (!SupportedFrame(syn) || (info.LocalMac.Octet0 & 1) != 0 || (info.RemoteMac.Octet0 & 1) != 0)
            return Status.InvalidPacket;
        var candidate = TcpPassiveIpv4FramePlanner.Create(info);
        if (candidate.ReceiveSynFrameAndWriteSynAckFrame(syn, output, out packet) != TcpPassiveIpv4FramePlanStatus.Success)
            return packet.Status == TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall ? Status.OutputTooSmall : Status.InvalidPacket;
        if (!SynParameters(syn, out ushort peerMss, out ushort peerWindow))
            return Status.InvalidPacket;
        ulong now = operations.NowNanoseconds(operations.Context);
        if (now > ~0UL - operations.LifetimeNanoseconds)
            return Status.InvalidClock;
        var key = new TcpConnectionKey(info.LocalAddress, info.LocalPort, info.RemoteAddress, info.RemotePort);
        var sequence = new TcpConnectionSequenceMetadata(candidate.LocalNextSequenceNumber, candidate.RemoteNextSequenceNumber);
        if (TcpConnectionTable.ReserveOwned(table, key, sequence, out var handle) != TcpConnectionTable.OwnershipStatus.Success)
            return Status.TableRejected;
        state->Self = (ulong)state;
        state->Table = table;
        state->Handle = handle;
        state->Planner = candidate;
        state->LocalMac = info.LocalMac;
        state->RemoteMac = info.RemoteMac;
        state->Io = operations;
        state->Started = now;
        state->LastNow = now;
        state->Deadline = now + operations.LifetimeNanoseconds;
        state->NextRetry = now;
        state->Phase = 1;
        state->PeerMss = peerMss;
        state->PeerWindow = peerWindow;
        return Transmit(state, output.Slice(0, packet.BytesWritten), now, duplicate: false);
    }

    /// <summary>Reproduce an exact duplicate SYN's reply on a temporary planner.
    /// Rejection, short output and full TX preserve the authoritative handshake.
    /// A successful duplicate never renews its hard deadline or scheduled retry.</summary>

    public static Status ReceiveSyn(State* state, ReadOnlySpan<byte> syn, Span<byte> output, out TcpPassiveIpv4FramePlanResult packet)
    {
        packet = default;
        var status = Time(state, out ulong now);
        if (status != Status.Success)
            return status;
        if (output.Length < TcpPassiveIpv4FramePlanner.HeaderBytes)
            return Status.OutputTooSmall;
        if (!FramesOutside(state, state->Table, syn, output))
            return Status.InvalidStorage;
        if (!SupportedFrame(syn))
            return Status.InvalidPacket;
        var candidate = state->Planner;
        if (candidate.ReceiveSynFrameAndWriteSynAckFrame(syn, output, out packet) != TcpPassiveIpv4FramePlanStatus.Success)
            return packet.Status == TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall ? Status.OutputTooSmall : Status.InvalidPacket;
        if (!SynParameters(syn, out ushort peerMss, out _) || peerMss != state->PeerMss)
            return Status.InvalidPacket;
        return Transmit(state, output.Slice(0, packet.BytesWritten), now, duplicate: true);
    }

    /// <summary>Attempt one due SYN-ACK, with no loop or queued borrowed buffer.
    /// Only accepted initial/retry sends advance the bounded attempt count and
    /// retry schedule. Exhausting attempts still permits an ACK until the original
    /// hard deadline; expiry closes/releases pending table metadata and stops TX.</summary>

    public static Status Pump(State* state, Span<byte> output, out TcpPassiveIpv4FramePlanResult packet)
    {
        packet = default;
        var status = Time(state, out ulong now);
        if (status != Status.Success)
            return status;
        if (now < state->NextRetry)
            return Status.NotDue;
        if (state->Attempts >= state->Io.MaximumAttempts)
            return Status.RetryLimit;
        if (output.Length < TcpPassiveIpv4FramePlanner.HeaderBytes)
            return Status.OutputTooSmall;
        if (!FramesOutside(state, state->Table, ReadOnlySpan<byte>.Empty, output))
            return Status.InvalidStorage;
        if (state->Planner.WritePendingSynAckFrame(output, out packet) != TcpPassiveIpv4FramePlanStatus.Success)
            return packet.Status == TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall ? Status.OutputTooSmall : Status.InvalidPacket;
        return Transmit(state, output.Slice(0, packet.BytesWritten), now, duplicate: false);
    }

    /// <summary>Validate final ACK on a temporary copy and enqueue before a
    /// nonfallible establishment commit. Invalid ACK/backlog admission leaves the
    /// real planner/handle/deadline intact. Packet failure may describe its failed
    /// candidate; State remains authoritative. Backlog is1..64 disjoint entries,
    /// exclusively owned for this call. Output results are distinct caller locals.
    /// The queue stores endpoints/sequences, not generation-bearing handles.</summary>

    public static Status ReceiveAck(State* state, ReadOnlySpan<byte> frame, Span<TcpListenerBacklogEntry> backlog, out TcpPassiveIpv4FramePlanResult packet, out TcpPassiveListenerBacklogBridgeResult admission)
    {
        packet = default;
        admission = default;
        var status = Time(state, out _);
        if (status != Status.Success)
            return status;
        if (state->Attempts == 0)
            return Status.NotSent;
        if (backlog.Length is < 1 or > 64)
            return Status.InvalidStorage;
        fixed (TcpListenerBacklogEntry* entries = backlog)
        fixed (byte* bytes = frame)
        {
            ulong backlogBytes = (ulong)backlog.Length * (ulong)sizeof(TcpListenerBacklogEntry);
            if (!Storage(entries, backlogBytes) || !Outside(state, state->Table, (byte*)entries, backlogBytes) || !Outside(state, state->Table, bytes, (ulong)frame.Length) || Overlap((byte*)entries, backlogBytes, bytes, (ulong)frame.Length))
                return Status.InvalidStorage;
        }

        if (!SupportedFrame(frame))
            return Status.InvalidPacket;
        var candidate = state->Planner;
        if (candidate.ReceiveFinalAckFrame(frame, out packet) != TcpPassiveIpv4FramePlanStatus.Success)
            return Status.InvalidPacket;
        // Time validated the handle, pending entry and sequence metadata. No
        // callback/concurrent writer intervenes between validation and this commit.
        if (TcpPassiveListenerBacklogBridge.EnqueueEstablished(backlog, packet, out admission) != TcpPassiveListenerBacklogBridgeStatus.Success)
            return Status.BacklogRejected;
        var table = state->Table;
        var entry = table->Entries[state->Handle.Index];
        table->Entries[state->Handle.Index] = TcpConnectionTableEntry.Create(TcpConnectionLifecycleState.Established, entry.Key, entry.Sequence);
        state->Planner = candidate;
        state->Phase = 3;
        state->Io = default;
        state->PeerWindow = packet.TcpHeader.WindowSize;
        state->WindowSequence = packet.TcpHeader.SequenceNumber;
        state->WindowAcknowledgement = packet.TcpHeader.AcknowledgementNumber;
        return Status.Success;
    }

    /// <summary>Validate a peer reset for this serialized pending owner only.
    /// Accepts header-only RST or RST|ACK at the exact expected receive sequence;
    /// ACK, when set, must acknowledge an accepted SYN-ACK. Bare RST ignores its
    /// unused ACK field. Options, payload and other flags are unsupported; no
    /// challenge ACK is sent. Borrowed frame storage must be disjoint from owner
    /// and table metadata. Invalid frames leave all metadata unchanged, including
    /// time. A valid frame observes the existing clock/deadline rules, then retires
    /// this exact generation without TX/backlog changes or further retries.
    /// Established/faulted ownership belongs to its existing cleanup consumer.</summary>

    public static Status ReceiveReset(State* state, ReadOnlySpan<byte> frame)
    {
        var status = Validate(state);
        if (status != Status.Success)
            return status;
        fixed (byte* bytes = frame)
            if (!Outside(state, state->Table, bytes, (ulong)frame.Length))
                return Status.InvalidStorage;
        if (!SupportedFrame(frame) || EthernetHeader.TryParse(frame, out var ethernet, out var ipPacket) != PacketParseStatus.Success || ethernet.EtherType != EthernetHeader.EtherTypeIpv4 || ethernet.Destination != state->LocalMac || ethernet.Source != state->RemoteMac || Ipv4Header.TryParse(ipPacket, out var ip, out var segment) != PacketParseStatus.Success || ip.Protocol != 6 || TcpHeader.TryParse(segment, out var tcp, out var options, out var payload) != PacketParseStatus.Success || options.Length != 0 || payload.Length != 0 || (segment[12] & 15) != 0 || tcp.Flags is not (4 or 0x14) || tcp.SequenceNumber != state->Planner.RemoteNextSequenceNumber || (tcp.Flags == 0x14 && tcp.AcknowledgementNumber != state->Planner.LocalNextSequenceNumber))
            return Status.InvalidPacket;
        var key = state->Table->Entries[state->Handle.Index].Key;
        if (ip.Source != key.RemoteAddress || ip.Destination != key.LocalAddress || tcp.SourcePort != key.RemotePort || tcp.DestinationPort != key.LocalPort || Ipv4TransportChecksum.Validate(ip.Source, ip.Destination, 6, segment, out var checksum) != Ipv4TransportChecksumStatus.Success || !checksum.IsValid)
            return Status.InvalidPacket;
        if (tcp.Flags == 0x14 && state->Attempts == 0)
            return Status.NotSent;
        status = Time(state, out _);
        if (status != Status.Success)
            return status;
        return Retire(state);
    }

    /// <summary>Withdraw a pending/faulted handshake locally and release its
    /// table metadata. Send never borrows this storage; no packet/data lease is
    /// released or RST fabricated. Established ownership belongs to the later
    /// stream/close consumer and cannot be canceled by this operation.</summary>

    public static Status CancelPending(State* state)
    {
        var status = Validate(state, allowFault: true);
        if (status != Status.Success)
            return status;
        return Retire(state);
    }

    private static Status Time(State* state, out ulong now)
    {
        now = 0;
        var status = Validate(state);
        if (status != Status.Success)
            return status;
        now = state->Io.NowNanoseconds(state->Io.Context);
        if (now < state->LastNow)
            return Status.InvalidClock;
        state->LastNow = now;
        if (now < state->Deadline)
            return Status.Success;
        status = Retire(state);
        return status == Status.Success ? Status.Timeout : status;
    }

    private static Status Transmit(State* state, ReadOnlySpan<byte> frame, ulong now, bool duplicate)
    {
        byte sent;
        fixed (byte* bytes = frame)
            sent = state->Io.Send(state->Io.Context, bytes, (uint)frame.Length);
        if (sent == 0)
            return Status.WouldBlock;
        if (sent != 1)
        {
            state->Phase = 2;
            return Status.SendFailed;
        }

        bool schedule = !duplicate || state->Attempts == 0;
        if (schedule)
            state->Attempts++;
        else if (state->Duplicates != ~0UL)
            state->Duplicates++;
        // The copy is already accepted: preserve its accounting even if the
        // post-copy clock fails or the immutable pending lifetime has expired.
        ulong completed = state->Io.NowNanoseconds(state->Io.Context);
        if (completed < now)
        {
            state->Phase = 2;
            return Status.InvalidClock;
        }

        state->LastNow = completed;
        if (completed >= state->Deadline)
        {
            var status = Retire(state);
            return status == Status.Success ? Status.Timeout : status;
        }

        if (schedule)
        {
            ulong remaining = state->Deadline - completed;
            state->NextRetry = state->Io.RetryNanoseconds >= remaining ? state->Deadline : completed + state->Io.RetryNanoseconds;
        }

        return Status.Success;
    }

    private static Status Retire(State* state)
    {
        var table = state->Table;
        var handle = state->Handle;
        if (TcpConnectionTable.TransitionOwned(table, handle, TcpConnectionLifecycleState.SynReceived, TcpConnectionLifecycleState.Closed) != TcpConnectionTable.OwnershipStatus.Success || TcpConnectionTable.ReleaseOwned(table, handle) != TcpConnectionTable.OwnershipStatus.Success)
        {
            state->Phase = 2;
            return Status.TableRejected;
        }

        state->Planner = default;
        state->Io = default;
        state->Handle = default;
        state->Phase = 4;
        return Status.Success;
    }

    private static Status Validate(State* state, bool allowFault = false)
    {
        if (!Storage(state, (ulong)sizeof(State)) || state->Self != (ulong)state || (state->Phase != 1 && !(allowFault && state->Phase == 2)))
            return Status.InvalidState;
        if (TcpConnectionTable.ReadOwned(state->Table, state->Handle, out var entry) != TcpConnectionTable.OwnershipStatus.Success || entry.State != TcpConnectionLifecycleState.SynReceived || !Valid(state->Io) || state->Planner.State != TcpPassiveOpenHandshakeState.SynReceived || entry.Sequence.LocalNextSequenceNumber != state->Planner.LocalNextSequenceNumber || entry.Sequence.RemoteNextSequenceNumber != state->Planner.RemoteNextSequenceNumber || state->Deadline <= state->Started || state->Deadline - state->Started != state->Io.LifetimeNanoseconds || state->LastNow < state->Started || state->NextRetry < state->Started || state->NextRetry > state->Deadline || state->Attempts > state->Io.MaximumAttempts)
            return Status.TableRejected;
        return Status.Success;
    }

    private static bool Valid(Operations io) => io.NowNanoseconds != null && io.Send != null && io.LifetimeNanoseconds is > 0 and <= 60_000_000_000 && io.RetryNanoseconds > 0 && io.RetryNanoseconds <= io.LifetimeNanoseconds && io.MaximumAttempts is > 0 and <= 8;
    private static bool SynParameters(ReadOnlySpan<byte> frame, out ushort mss, out ushort window)
    {
        mss = 0;
        window = 0;
        int ipBytes = (frame[16] << 8) | frame[17];
        if (ipBytes < 40 || ipBytes > frame.Length - 14 || TcpHeader.TryParse(frame.Slice(34, ipBytes - 20), out var header, out var options, out _) != PacketParseStatus.Success || TcpHeader.TryReadMaximumSegmentSize(options, out mss) != PacketParseStatus.Success)
            return false;
        window = header.WindowSize;
        return true;
    }

    // Existing IPv4 parsing rejects options. This controller additionally rejects
    // reserved/fragment flags and nonzero fragment offsets; DF is supported.
    private static bool SupportedFrame(ReadOnlySpan<byte> frame) => frame.Length is >= 54 and <= 1514 && (frame[20] & 0xbf) == 0 && frame[21] == 0;
    private static bool FramesOutside(State* state, TcpConnectionTable.Owner* table, ReadOnlySpan<byte> input, Span<byte> output)
    {
        fixed (byte* a = input)
        fixed (byte* b = output)
            return input.Length <= 1514 && Outside(state, table, a, (ulong)input.Length) && Outside(state, table, b, (ulong)output.Length) && !Overlap(a, (ulong)input.Length, b, (ulong)output.Length);
    }

    private static bool Outside(State* state, TcpConnectionTable.Owner* table, byte* address, ulong bytes) => OutsideTable(table, address, bytes) && !Overlap((byte*)state, (ulong)sizeof(State), address, bytes);
    private static bool OutsideTable(TcpConnectionTable.Owner* table, byte* address, ulong bytes) => bytes == 0 || address != null && bytes <= ~0UL - (ulong)address && !Overlap((byte*)table, (ulong)sizeof(TcpConnectionTable.Owner), address, bytes) && !Overlap((byte*)table->Entries, (ulong)table->CapacityValue * (ulong)sizeof(TcpConnectionTableEntry), address, bytes) && !Overlap((byte*)table->Generations, (ulong)table->CapacityValue * 8, address, bytes);
    private static bool Overlap(byte* a, ulong count, byte* b, ulong other) => count != 0 && other != 0 && (ulong)a < (ulong)b + other && (ulong)b < (ulong)a + count;
    private static bool Storage(void* address, ulong count) => address != null && ((ulong)address & 7) == 0 && count <= ~0UL - (ulong)address;
    private static bool Zero(void* address, ulong count)
    {
        for (ulong i = 0; i < count; i++)
            if (((byte*)address)[i] != 0)
                return false;
        return true;
    }
}
