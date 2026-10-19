namespace OS.Net;

/// <summary>
/// Bounded retained send ownership for one established TCP generation. A serialized
/// kernel dispatcher retains trusted mapped owner, table, receiver, buffers and
/// callback/code backing through closed cleanup. No unchecked guest pointers or
/// aliased result outputs are supported. Queue copies application bytes; Pump
/// advances SND.NXT only after copied TX admission; Acknowledge frees bytes only
/// after original frame/sequence/ACK validation. Optional timed operation keeps
/// one segment in flight and borrows a caller-supplied monotonic clock. No NIC,
/// socket, congestion-window growth or fast retransmit is supplied. Timed pumps
/// include separately backed-off zero-window probes without a retry-count timeout.
/// </summary>
public static unsafe partial class TcpSendWindow
{
    public enum Status : byte
    {
        Uninitialized, Success, InvalidStorage, InvalidState, InvalidOptions,
        TableRejected, InvalidPacket, InvalidAck, OldAck, DuplicateAck, WindowUpdated,
        NoData, NothingToSend, WindowClosed, OutputTooSmall, WouldBlock, SendFailed,
        AwaitingAck, NotDue, Timeout, InvalidClock, WriteClosed, WaitingForDrain,
        PeerClosed, ConnectionClosed, ConnectionReset
    }

    /// <summary>Initially zero, noncopyable metadata. Retained bytes include
    /// both transmitted/unacknowledged and queued/unsent data. Observations are
    /// not socket authority. The receiver backing remains retained even if it is
    /// cleaned first; both attachments must detach before table release.</summary>
    public struct Owner
    {
        internal ulong Self;
        internal TcpConnectionTable.Owner* Table;
        internal TcpConnectionTable.Handle Handle;
        internal TcpReceiveWindow.Owner* Receiver;
        internal byte* Buffer;
        internal int CapacityValue, Offset, Count, Sent;
        internal uint Una, Next, WindowSequence, WindowAck;
        internal ushort PeerWindow, PeerMss, Mtu;
        internal void* Context;
        internal delegate*<void*, byte*, uint, byte> Send;
        internal void* ClockContext;
        internal delegate*<void*, ulong> Clock;
        internal ulong LastNow, HardDeadline, RetryAt, Rto, MinimumRto, MaximumRto;
        internal ulong SampleAt, SmoothedRtt, RttVariation, DuplicateAcks;
        internal ulong PersistAt, PersistDelay, PersistCount;
        internal uint SampleEnd;
        internal byte Timed, RetryCount, RetryLimit, Sampling, HasRtt, HandshakeRetried;
        internal byte CloseEnabled, WriteShutdown, FinPending;
        internal TcpConnectionCloseState CloseStateValue;
        internal ulong CloseLimit, CloseDeadline, TimeWaitDuration, TimeWaitDeadline;
        internal byte Phase, TimeoutError;
        public readonly bool Active => Phase == 1;
        public readonly bool RequiresCleanup => Phase == 2;
        /// <summary>Actual retry or configured deadline exhaustion. The owner
        /// retains all backing until explicit timeout retirement and closed reap.</summary>
        public readonly bool TimedOut => TimeoutError == 1;
        public readonly bool Retired => Phase == 3;
        public readonly int BufferedBytes => Count;
        public readonly int InFlightBytes => Sent;
        public readonly int AvailableBytes => CapacityValue - Count;
        public readonly uint OldestUnacknowledgedSequence => Una;
        public readonly uint NextSequence => Next;
        public readonly ushort PeerMaximumSegmentSize => PeerMss;
        public readonly ushort PeerAdvertisedWindow => PeerWindow;
        public readonly ulong RetransmissionDeadlineNanoseconds => RetryAt;
        public readonly ulong RetransmissionTimeoutNanoseconds => Rto;
        public readonly byte Retransmissions => RetryCount;
        public readonly ulong DuplicateAcknowledgements => DuplicateAcks;
        public readonly ulong PersistDeadlineNanoseconds => PersistAt;
        public readonly ulong PersistProbes => PersistCount;
        public readonly TcpConnectionCloseState CloseState => CloseStateValue;
        public readonly bool WritesClosed => WriteShutdown != 0;
        public readonly bool FinOutstanding => FinPending != 0;
        public readonly ulong TimeWaitDeadlineNanoseconds => TimeWaitDeadline;
    }

    /// <summary>Bind one fresh established handshake and its active receiver,
    /// before payload dispatch. MSS/window come from actual validated handshake
    /// packets. Copy no data yet. Buffer is1..32768 zeroed bytes, disjoint from
    /// all metadata/receive storage; MTU is68..1500. No existing sender may be
    /// replaced. Invalid admission changes nothing. TX callback is borrowed from
    /// the receiver and must copy before return, never reenter/mutate metadata.</summary>
    public static Status Initialize(Owner* owner, TcpReceiveWindow.Owner* receiver,
        TcpPassiveConnection.State* handshake, byte* buffer, uint capacity, ushort mtu)
    {
        if (!Storage(owner, (ulong)sizeof(Owner)) || !Storage(handshake, (ulong)sizeof(TcpPassiveConnection.State)) ||
            capacity is 0 or > 32768 || buffer == null || capacity > ~0UL - (ulong)buffer) return Status.InvalidStorage;
        if (TcpReceiveWindow.Validate(receiver, out var entry) != TcpReceiveWindow.Status.Success ||
            entry.SendOwnerAddress != 0 || handshake->Self != (ulong)handshake || !handshake->Established ||
            handshake->Table != receiver->Table || !Same(handshake->Handle, receiver->Handle) ||
            entry.Sequence.LocalNextSequenceNumber != entry.Sequence.LocalAcknowledgedSequenceNumber ||
            entry.Sequence.RemoteNextSequenceNumber != handshake->Planner.RemoteNextSequenceNumber || receiver->Unread != 0)
            return Status.TableRejected;
        if (mtu is < 68 or > 1500 || handshake->PeerMss == 0 ||
            handshake->WindowAcknowledgement != entry.Sequence.LocalNextSequenceNumber) return Status.InvalidOptions;
        if (!OutsideReceiver(receiver, (byte*)owner, (ulong)sizeof(Owner)) || !OutsideReceiver(receiver, buffer, capacity) ||
            Overlap((byte*)owner, (ulong)sizeof(Owner), buffer, capacity) ||
            Overlap((byte*)handshake, (ulong)sizeof(TcpPassiveConnection.State), (byte*)owner, (ulong)sizeof(Owner)) ||
            Overlap((byte*)handshake, (ulong)sizeof(TcpPassiveConnection.State), buffer, capacity) ||
            !Zero(owner, (ulong)sizeof(Owner)) || !Zero(buffer, capacity)) return Status.InvalidStorage;
        owner->Self = (ulong)owner; owner->Table = receiver->Table; owner->Handle = receiver->Handle; owner->Receiver = receiver;
        owner->Buffer = buffer; owner->CapacityValue = (int)capacity; owner->Mtu = mtu;
        owner->Una = entry.Sequence.LocalAcknowledgedSequenceNumber; owner->Next = entry.Sequence.LocalNextSequenceNumber;
        owner->PeerMss = handshake->PeerMss; owner->PeerWindow = handshake->PeerWindow;
        owner->WindowSequence = handshake->WindowSequence; owner->WindowAck = handshake->WindowAcknowledgement;
        owner->Context = receiver->Context; owner->Send = receiver->Send; owner->Phase = 1;
        owner->HandshakeRetried = handshake->SuccessfulAttempts > 1 ? (byte)1 : (byte)0;
        owner->Table->Entries[owner->Handle.Index].SendOwnerAddress = (ulong)owner;
        return Status.Success;
    }

    /// <summary>Enable the bounded timed path before any transmission. One
    /// segment may be outstanding; callers must use PumpRetransmission as well
    /// as Pump and Acknowledge. The trusted supplied clock/context must remain
    /// valid through closed cleanup and never reenter. Initial RTO is at least
    /// one second, its cap at least sixty seconds, retries1..16, and the fixed
    /// lifetime bounds all calls including TX pressure/zero windows. Explicit
    /// continuing mode requires a zero lifetime and leaves the total application
    /// budget to its enclosing owner; retry and configured close limits remain.
    /// Zero-window persist uses a separate bounded exponential timer; a closed
    /// peer window alone never exhausts the retransmission count. Invalid
    /// configuration changes nothing. This is a serialized passive transport,
    /// without general congestion control or fast recovery.</summary>
    public static Status ConfigureRetransmission(Owner* owner, void* clockContext,
        delegate*<void*, ulong> nowNanoseconds, ulong lifetimeNanoseconds,
        ulong initialRtoNanoseconds = 1_000_000_000, ulong maximumRtoNanoseconds = 60_000_000_000,
        byte maximumRetransmissions = 5, bool continuing = false)
    {
        var status = Validate(owner, out _); if (status != Status.Success) return status;
        if (owner->Timed != 0 || owner->Sent != 0) return Status.InvalidState;
        if (initialRtoNanoseconds < 1_000_000_000 || maximumRtoNanoseconds < 60_000_000_000 ||
            maximumRtoNanoseconds < initialRtoNanoseconds || maximumRtoNanoseconds > (~0UL / 16) ||
            (continuing ? lifetimeNanoseconds != 0 : lifetimeNanoseconds <= initialRtoNanoseconds || lifetimeNanoseconds > (~0UL / 16)) ||
            maximumRetransmissions is 0 or > 16 || nowNanoseconds == null) return Status.InvalidOptions;
        ulong initial = owner->HandshakeRetried != 0 && initialRtoNanoseconds < 3_000_000_000 ? 3_000_000_000 : initialRtoNanoseconds;
        if (!continuing && lifetimeNanoseconds <= initial) return Status.InvalidOptions;
        ulong now = nowNanoseconds(clockContext);
        if ((continuing ? maximumRtoNanoseconds : lifetimeNanoseconds) > ~0UL - now) return Status.InvalidClock;
        owner->LastNow = now; owner->HardDeadline = continuing ? 0 : now + lifetimeNanoseconds;
        owner->Rto = initial; owner->MinimumRto = initialRtoNanoseconds;
        owner->MaximumRto = maximumRtoNanoseconds; owner->RetryLimit = maximumRetransmissions;
        owner->ClockContext = clockContext; owner->Clock = nowNanoseconds;
        owner->Timed = 1; return Status.Success;
    }

    /// <summary>Copy at most free capacity and report an explicit short write.
    /// Accepted bytes remain owned through ACK or explicit closed discard; the
    /// input is borrowed only for this call. No sequence/TX progress occurs here.
    /// Empty/full/invalid inputs accept nothing and return a distinct status.</summary>
    public static Status Queue(Owner* owner, ReadOnlySpan<byte> input, out int accepted)
    {
        accepted = 0;
        var status = Validate(owner, out _); if (status != Status.Success) return status;
        if (owner->WriteShutdown != 0) return Status.WriteClosed;
        status = Time(owner, out _); if (status != Status.Success) return status;
        if (input.Length == 0) return Status.NoData;
        fixed (byte* bytes = input)
            if (!Outside(owner, bytes, (ulong)input.Length)) return Status.InvalidStorage;
        int count = owner->CapacityValue - owner->Count;
        if (count == 0) return Status.WouldBlock;
        if (count > input.Length) count = input.Length;
        if (owner->Offset + owner->Count + count > owner->CapacityValue)
        {
            for (int i = 0; i < owner->Count; i++) owner->Buffer[i] = owner->Buffer[owner->Offset + i];
            owner->Offset = 0;
        }
        for (int i = 0; i < count; i++) owner->Buffer[owner->Offset + owner->Count + i] = input[i];
        owner->Count += count; accepted = count; return Status.Success;
    }

    /// <summary>Attempt one new segment, bounded by queued bytes, MTU, actual
    /// peer MSS, latest advertised right edge and output capacity. Only Send=1
    /// commits SND.NXT/in-flight progress. Full output/TX retains queued state.
    /// Timed operation permits one outstanding segment; retry pumping is separate.</summary>
    public static Status Pump(Owner* owner, Span<byte> output, out TcpEstablishedStreamIpv4FramePlanResult packet)
    {
        packet = default;
        var status = Validate(owner, out var entry); if (status != Status.Success) return status;
        status = Time(owner, out ulong now); if (status != Status.Success) return status;
        if (owner->FinPending != 0 || owner->Timed != 0 && owner->Sent != 0) return Status.AwaitingAck;
        int count = owner->Count - owner->Sent;
        if (count == 0) return Status.NothingToSend;
        uint right = unchecked(owner->WindowAck + owner->PeerWindow);
        int window = unchecked((int)(right - owner->Next));
        if (window <= 0) return Status.WindowClosed;
        if (output.Length <= TcpEstablishedStreamIpv4FramePlanner.HeaderBytes) return Status.OutputTooSmall;
        if (count > window) count = window;
        if (count > owner->PeerMss) count = owner->PeerMss;
        if (count > owner->Mtu - 40) count = owner->Mtu - 40;
        if (count > output.Length - 54) count = output.Length - 54;
        fixed (byte* bytes = output)
            if (!Outside(owner, bytes, (ulong)output.Length)) return Status.InvalidStorage;
        var receiver = owner->Receiver;
        var original = receiver->Info;
        var info = new TcpEstablishedStreamIpv4FramePlannerInfo(original.LocalMac, original.RemoteMac,
            original.LocalAddress, original.RemoteAddress, count, receiver->AvailableWindow, original.TimeToLive, original.Identification);
        var stream = TcpEstablishedStream.Create(entry.Key.LocalPort, entry.Key.RemotePort, owner->Next,
            entry.Sequence.RemoteNextSequenceNumber, Span<byte>.Empty);
        if (TcpEstablishedStreamIpv4FramePlanner.TryWritePayloadFrame(ref stream, info,
            new ReadOnlySpan<byte>(owner->Buffer + owner->Offset + owner->Sent, count), output, out packet) != TcpEstablishedStreamIpv4FramePlanStatus.Success)
            return Status.InvalidPacket;
        byte sent;
        fixed (byte* bytes = output) sent = owner->Send(owner->Context, bytes, (uint)packet.BytesWritten);
        if (sent == 0) return Status.WouldBlock;
        if (sent != 1) { owner->Phase = 2; return Status.SendFailed; }
        owner->Sent += count; owner->Next = stream.LocalNextSequenceNumber;
        Commit(owner, entry);
        if (owner->Timed != 0)
        {
            // Timestamp after copied TX: time spent in Send must not shorten RTO.
            // Actual sequence progress remains committed if the clock then fails.
            status = Time(owner, out now); if (status != Status.Success) return status;
            owner->RetryAt = Deadline(now, owner->Rto); owner->RetryCount = 0;
            owner->SampleAt = now; owner->SampleEnd = owner->Next; owner->Sampling = 1;
        }
        return Status.Success;
    }

    /// <summary>Attempt at most one due copy of the oldest unacknowledged
    /// bytes or owned FIN, using the current cumulative receive ACK/window. Never advances
    /// SND.NXT or releases data. Short/full TX retains timer and retry count;
    /// accepted TX backs off RTO and cancels ambiguous RTT sampling (Karn).
    /// Duplicate ACKs do not delay this timer. A zero offered window instead
    /// probes one octet with a separate capped backoff and no retry exhaustion.
    /// Ordinary retry exhaustion, explicit hard expiry, clock
    /// regression or fatal TX requires explicit closed cleanup; storage remains
    /// attached. Call from the same serialized dispatcher, without polling loops.</summary>
    public static Status PumpRetransmission(Owner* owner, Span<byte> output,
        out TcpEstablishedStreamIpv4FramePlanResult packet)
    {
        packet = default;
        var status = Validate(owner, out var entry); if (status != Status.Success) return status;
        if (owner->Timed == 0) return Status.InvalidState;
        status = Time(owner, out ulong now); if (status != Status.Success) return status;
        if (owner->PeerWindow == 0 && (owner->Count != 0 || owner->FinPending != 0 ||
            owner->WriteShutdown != 0 && owner->CloseStateValue is TcpConnectionCloseState.Established or TcpConnectionCloseState.CloseWait))
            return PumpPersist(owner, entry, output, now, out packet);
        owner->PersistAt = 0; owner->PersistDelay = 0;
        if (owner->Sent == 0 && owner->FinPending == 0) return Status.NothingToSend;
        if (now < owner->RetryAt) return Status.NotDue;
        if (owner->RetryCount == owner->RetryLimit) { owner->Phase = 2; owner->TimeoutError = 1; return Status.Timeout; }
        // Preserve the original segment extent: a shorter retry can make a later
        // combined retry transmit its tail less than one RTO after the last copy.
        int count = owner->Sent;
        if (output.Length < 54 + count) return Status.OutputTooSmall;
        fixed (byte* bytes = output)
            if (!Outside(owner, bytes, (ulong)output.Length)) return Status.InvalidStorage;
        var receiver = owner->Receiver; var original = receiver->Info;
        var info = new TcpEstablishedStreamIpv4FramePlannerInfo(original.LocalMac, original.RemoteMac,
            original.LocalAddress, original.RemoteAddress, count, receiver->AvailableWindow, original.TimeToLive, original.Identification);
        var stream = TcpEstablishedStream.Create(entry.Key.LocalPort, entry.Key.RemotePort, owner->Una,
            entry.Sequence.RemoteNextSequenceNumber, Span<byte>.Empty);
        var serialized = owner->FinPending != 0
            ? TcpEstablishedStreamIpv4FramePlanner.TryWriteControlFrame(ref stream, info, receiver->AvailableWindow, 0x11, output, out packet)
            : TcpEstablishedStreamIpv4FramePlanner.TryWritePayloadFrame(ref stream, info,
                new ReadOnlySpan<byte>(owner->Buffer + owner->Offset, count), output, out packet);
        if (serialized != TcpEstablishedStreamIpv4FramePlanStatus.Success)
            return Status.InvalidPacket;
        byte sent;
        fixed (byte* bytes = output) sent = owner->Send(owner->Context, bytes, (uint)packet.BytesWritten);
        if (sent == 0) return Status.WouldBlock;
        if (sent != 1) { owner->Phase = 2; return Status.SendFailed; }
        owner->RetryCount++; owner->Sampling = 0;
        owner->Rto = owner->Rto > owner->MaximumRto / 2 ? owner->MaximumRto : owner->Rto * 2;
        status = Time(owner, out now); if (status != Status.Success) return status;
        owner->RetryAt = Deadline(now, owner->Rto); return Status.Success;
    }

    // RFC9293 3.8.6.1: send one retained/new octet even through a zero offered
    // window. A newly copied octet owns sequence space until ACKed; a rejected
    // copy owns none. Persist never consumes the ordinary retry budget or an RTT
    // sample. Explicit enclosing lifetime/close/cancellation policy still applies.
    private static Status PumpPersist(Owner* owner, TcpConnectionTableEntry entry,
        Span<byte> output, ulong now, out TcpEstablishedStreamIpv4FramePlanResult packet)
    {
        packet = default;
        if (owner->PersistDelay == 0)
        {
            owner->PersistDelay = owner->Rto;
            owner->PersistAt = owner->RetryAt != 0 ? owner->RetryAt : Deadline(now, owner->PersistDelay);
        }
        if (now < owner->PersistAt) return Status.NotDue;
        int count = owner->Count == 0 ? 0 : 1;
        if (output.Length < 54 + count) return Status.OutputTooSmall;
        fixed (byte* bytes = output)
            if (!Outside(owner, bytes, (ulong)output.Length)) return Status.InvalidStorage;
        var receiver = owner->Receiver; var original = receiver->Info;
        var info = new TcpEstablishedStreamIpv4FramePlannerInfo(original.LocalMac, original.RemoteMac,
            original.LocalAddress, original.RemoteAddress, 1, receiver->AvailableWindow, original.TimeToLive, original.Identification);
        uint sequence = count != 0 || owner->FinPending != 0 ? owner->Una : unchecked(owner->Next - 1);
        var stream = TcpEstablishedStream.Create(entry.Key.LocalPort, entry.Key.RemotePort, sequence,
            entry.Sequence.RemoteNextSequenceNumber, Span<byte>.Empty);
        var serialized = count != 0
            ? TcpEstablishedStreamIpv4FramePlanner.TryWritePayloadFrame(ref stream, info,
                new ReadOnlySpan<byte>(owner->Buffer + owner->Offset, 1), output, out packet)
            : TcpEstablishedStreamIpv4FramePlanner.TryWriteControlFrame(ref stream, info, receiver->AvailableWindow,
                owner->FinPending != 0 ? (byte)0x11 : (byte)0x10, output, out packet);
        if (serialized != TcpEstablishedStreamIpv4FramePlanStatus.Success) return Status.InvalidPacket;
        byte sent;
        fixed (byte* bytes = output) sent = owner->Send(owner->Context, bytes, (uint)packet.BytesWritten);
        if (sent == 0) return Status.WouldBlock;
        if (sent != 1) { owner->Phase = 2; return Status.SendFailed; }
        if (count != 0 && owner->Sent == 0)
        {
            owner->Sent = 1; owner->Next = unchecked(owner->Next + 1);
            Commit(owner, entry); owner->RetryAt = Deadline(now, owner->Rto);
        }
        owner->Sampling = 0;
        if (owner->PersistCount != ~0UL) owner->PersistCount++;
        owner->PersistDelay = owner->PersistDelay > owner->MaximumRto / 2 ? owner->MaximumRto : owner->PersistDelay * 2;
        var status = Time(owner, out now); if (status != Status.Success) return status;
        owner->PersistAt = Deadline(now, owner->PersistDelay); return Status.Success;
    }

    /// <summary>Process original ACK metadata before receive-payload dispatch
    /// of the same frame. Validate checksum/endpoints/flags and current receive
    /// sequence window first. Release only ACKed transmitted bytes; future ACKs
    /// release nothing and schedule a corrective cumulative ACK. Duplicate/old
    /// ACKs never free data. Ordered SEQ/ACK window updates preserve the last
    /// advertised absolute right edge when stale updates are ignored.</summary>
    public static Status Acknowledge(Owner* owner, ReadOnlySpan<byte> frame, out int released)
        => AcknowledgeCore(owner, frame, out released, false);

    private static Status AcknowledgeCore(Owner* owner, ReadOnlySpan<byte> frame, out int released, bool controls)
    {
        released = 0;
        var status = Validate(owner, out var entry); if (status != Status.Success) return status;
        status = Time(owner, out ulong now); if (status != Status.Success) return status;
        fixed (byte* bytes = frame)
            if (!Outside(owner, bytes, (ulong)frame.Length)) return Status.InvalidStorage;
        if (frame.Length is < 54 or > 1514 || (frame[20] & 0xbf) != 0 || frame[21] != 0) return Status.InvalidPacket;
        var receiver = owner->Receiver;
        var stream = TcpEstablishedStream.Create(entry.Key.LocalPort, entry.Key.RemotePort, owner->Next,
            entry.Sequence.RemoteNextSequenceNumber, Span<byte>.Empty);
        if (TcpEstablishedStreamIpv4FramePlanner.ValidateFrame(stream, receiver->Info, frame, out var packet) != TcpEstablishedStreamIpv4FramePlanStatus.Success)
            return Status.InvalidPacket;
        var tcp = packet.TcpHeader;
        bool fin = controls && owner->CloseEnabled != 0 && (tcp.Flags & 1) != 0;
        if ((tcp.Flags & 0x10) == 0 || (tcp.Flags & ~(fin ? 0x19 : 0x18)) != 0) return Status.InvalidPacket;
        bool repeatedFin = fin && receiver->PeerClosed != 0 && unchecked(tcp.SequenceNumber + (uint)packet.PayloadBytes + 1) == receiver->RemoteNext;
        if (!repeatedFin && !AcceptableSequence(tcp.SequenceNumber, packet.PayloadBytes + (fin ? 1 : 0), entry.Sequence.RemoteNextSequenceNumber, receiver->AvailableWindow))
        { receiver->AckPending = 1; return Status.InvalidPacket; }
        uint advance = unchecked(tcp.AcknowledgementNumber - owner->Una);
        if (advance > (uint)owner->Sent + owner->FinPending)
        {
            if (unchecked((int)advance) < 0) return Status.OldAck;
            receiver->AckPending = 1; return Status.InvalidAck;
        }
        bool windowChanged = false;
        if (After(tcp.SequenceNumber, owner->WindowSequence) || tcp.SequenceNumber == owner->WindowSequence && !After(owner->WindowAck, tcp.AcknowledgementNumber))
        {
            windowChanged = owner->PeerWindow != tcp.WindowSize || owner->WindowSequence != tcp.SequenceNumber || owner->WindowAck != tcp.AcknowledgementNumber;
            owner->PeerWindow = tcp.WindowSize; owner->WindowSequence = tcp.SequenceNumber; owner->WindowAck = tcp.AcknowledgementNumber;
        }
        if (advance == 0)
        {
            if (!windowChanged && packet.PayloadBytes == 0 && owner->Sent != 0 && owner->DuplicateAcks != ~0UL) owner->DuplicateAcks++;
            return windowChanged ? Status.WindowUpdated : Status.DuplicateAck;
        }
        bool finAck = owner->FinPending != 0 && advance == 1;
        uint dataAdvance = finAck ? 0 : advance;
        for (uint i = 0; i < dataAdvance; i++) owner->Buffer[owner->Offset + i] = 0;
        owner->Offset += (int)dataAdvance; owner->Count -= (int)dataAdvance; owner->Sent -= (int)dataAdvance;
        if (owner->Count == 0) owner->Offset = 0;
        owner->Una = tcp.AcknowledgementNumber; released = (int)dataAdvance;
        if (finAck) owner->FinPending = 0;
        if (owner->Timed != 0)
        {
            if (owner->Sampling != 0 && tcp.AcknowledgementNumber == owner->SampleEnd)
            { Sample(owner, now - owner->SampleAt); owner->Sampling = 0; }
            owner->RetryAt = owner->Sent == 0 && owner->FinPending == 0 ? 0 : Deadline(now, owner->Rto);
        }
        Commit(owner, entry);
        if (finAck) return FinAcknowledged(owner, now);
        return Status.Success;
    }

    /// <summary>After the exact table lifetime is Closed, explicitly discard
    /// queued/unacknowledged data, scrub private storage and detach. Retain both
    /// owner/receiver backing through cleanup in either order. Table release is
    /// blocked until both attachments detach. This emits no FIN/RST or DMA proof.</summary>
    public static Status ReleaseClosed(Owner* owner)
    {
        var status = Validate(owner, out _, closed: true); if (status != Status.Success) return status;
        for (int i = 0; i < owner->CapacityValue; i++) owner->Buffer[i] = 0;
        owner->Table->Entries[owner->Handle.Index].SendOwnerAddress = 0;
        owner->Buffer = null; owner->Receiver = null; owner->Context = null; owner->Send = null;
        owner->Clock = null; owner->ClockContext = null; owner->RetryAt = 0; owner->Timed = 0;
        owner->PersistAt = 0; owner->PersistDelay = 0; owner->PersistCount = 0;
        owner->FinPending = 0; owner->CloseEnabled = 0; owner->TimeWaitDeadline = 0;
        owner->Handle = default; owner->Offset = 0; owner->Count = 0; owner->Sent = 0; owner->Phase = 3;
        return Status.Success;
    }

    private static void Commit(Owner* owner, TcpConnectionTableEntry entry) =>
        owner->Table->Entries[owner->Handle.Index] = entry.WithMetadata(entry.State,
            new TcpConnectionSequenceMetadata(owner->Next, entry.Sequence.RemoteNextSequenceNumber, owner->Una));

    private static Status Time(Owner* owner, out ulong now)
    {
        now = 0; if (owner->Timed == 0) return Status.Success;
        now = owner->Clock(owner->ClockContext);
        if (now < owner->LastNow) { owner->Phase = 2; return Status.InvalidClock; }
        owner->LastNow = now;
        if (owner->CloseEnabled != 0 && owner->CloseStateValue == TcpConnectionCloseState.TimeWait) return Status.Success;
        if (owner->HardDeadline == 0)
        {
            ulong headroom = owner->MaximumRto;
            if (owner->CloseEnabled != 0 && owner->CloseDeadline == 0 && owner->CloseLimit > headroom) headroom = owner->CloseLimit;
            if (headroom > ~0UL - now) { owner->Phase = 2; return Status.InvalidClock; }
        }
        if ((owner->HardDeadline == 0 || now < owner->HardDeadline) &&
            (owner->CloseDeadline == 0 || now < owner->CloseDeadline)) return Status.Success;
        owner->Phase = 2; owner->TimeoutError = 1; return Status.Timeout;
    }

    private static ulong Deadline(ulong now, ulong delay) => delay > ~0UL - now ? ~0UL : now + delay;

    private static void Sample(Owner* owner, ulong sample)
    {
        // RFC6298 integer nanoseconds; bounds on lifetime/RTO keep products safe.
        if (owner->HasRtt == 0)
        { owner->SmoothedRtt = sample; owner->RttVariation = sample / 2; owner->HasRtt = 1; }
        else
        {
            ulong difference = owner->SmoothedRtt > sample ? owner->SmoothedRtt - sample : sample - owner->SmoothedRtt;
            owner->RttVariation = (3 * owner->RttVariation + difference) / 4;
            owner->SmoothedRtt = (7 * owner->SmoothedRtt + sample) / 8;
        }
        ulong variation = owner->RttVariation * 4;
        ulong rto = owner->SmoothedRtt + (variation == 0 ? 1UL : variation);
        owner->Rto = rto < owner->MinimumRto ? owner->MinimumRto : rto > owner->MaximumRto ? owner->MaximumRto : rto;
    }

    internal static Status Validate(Owner* owner, out TcpConnectionTableEntry entry, bool closed = false, bool allowFault = false)
    {
        entry = default;
        if (!Storage(owner, (ulong)sizeof(Owner)) || owner->Self != (ulong)owner ||
            (owner->Phase != 1 && !((closed || allowFault) && owner->Phase == 2))) return Status.InvalidState;
        if (TcpConnectionTable.ReadOwned(owner->Table, owner->Handle, out entry) != TcpConnectionTable.OwnershipStatus.Success ||
            entry.SendOwnerAddress != (ulong)owner || entry.State != (closed ? TcpConnectionLifecycleState.Closed : TcpConnectionLifecycleState.Established))
            return Status.TableRejected;
        var receiver = owner->Receiver;
        if (!Storage(receiver, (ulong)sizeof(TcpReceiveWindow.Owner))) return Status.InvalidState;
        if (closed && receiver->Retired)
        {
            if (receiver->Self != (ulong)receiver || receiver->Table != owner->Table || entry.ReceiveOwnerAddress != 0) return Status.InvalidState;
        }
        else if (TcpReceiveWindow.Validate(receiver, out _, closed || allowFault) != TcpReceiveWindow.Status.Success ||
            receiver->Table != owner->Table || !Same(receiver->Handle, owner->Handle)) return Status.TableRejected;
        if (owner->Buffer == null || owner->CapacityValue is < 1 or > 32768 || owner->Offset < 0 || owner->Offset > owner->CapacityValue ||
            owner->Count < 0 || owner->Count > owner->CapacityValue - owner->Offset || owner->Sent < 0 || owner->Sent > owner->Count ||
            unchecked(owner->Next - owner->Una) != (uint)owner->Sent + owner->FinPending || owner->Una != entry.Sequence.LocalAcknowledgedSequenceNumber ||
            owner->Next != entry.Sequence.LocalNextSequenceNumber || owner->PeerMss == 0 || owner->Mtu is < 68 or > 1500 || owner->Send == null ||
            owner->TimeoutError > 1 || owner->TimeoutError != 0 && owner->Phase != 2 ||
            owner->Timed > 1 || owner->Timed != 0 && (owner->Clock == null || owner->MinimumRto < 1_000_000_000 ||
                owner->Rto < owner->MinimumRto || owner->Rto > owner->MaximumRto || owner->RetryLimit is 0 or > 16 ||
                owner->RetryCount > owner->RetryLimit || owner->Sent > owner->PeerMss || owner->Sent > owner->Mtu - 40) ||
            (owner->PersistDelay == 0 ? owner->PersistAt != 0 : owner->Timed == 0 ||
                owner->PersistDelay < owner->MinimumRto || owner->PersistDelay > owner->MaximumRto || owner->PersistAt == 0) ||
            owner->CloseEnabled > 1 || owner->WriteShutdown > 1 || owner->FinPending > 1 ||
            owner->FinPending != 0 && (owner->CloseEnabled == 0 || owner->WriteShutdown == 0 || owner->Sent != 0 || owner->Count != 0 ||
                !closed && owner->CloseStateValue is not (TcpConnectionCloseState.FinWait1 or TcpConnectionCloseState.Closing or TcpConnectionCloseState.LastAck)) ||
            !OutsideReceiver(receiver, (byte*)owner, (ulong)sizeof(Owner)) || !OutsideReceiver(receiver, owner->Buffer, (ulong)owner->CapacityValue) ||
            Overlap((byte*)owner, (ulong)sizeof(Owner), owner->Buffer, (ulong)owner->CapacityValue)) return Status.InvalidState;
        return Status.Success;
    }

    private static bool AcceptableSequence(uint sequence, int bytes, uint next, ushort window)
    {
        uint start = unchecked(sequence - next);
        if (bytes == 0) return window == 0 ? start == 0 : start < window;
        return window != 0 && (start < window || unchecked(sequence + (uint)bytes - 1 - next) < window);
    }
    private static bool After(uint a, uint b) => unchecked((int)(a - b)) > 0;
    private static bool Same(TcpConnectionTable.Handle a, TcpConnectionTable.Handle b) =>
        a.OwnerAddress == b.OwnerAddress && a.Epoch == b.Epoch && a.Generation == b.Generation && a.Index == b.Index;
    private static bool Outside(Owner* owner, byte* address, ulong bytes) => OutsideReceiver(owner->Receiver, address, bytes) &&
        !Overlap((byte*)owner, (ulong)sizeof(Owner), address, bytes) && !Overlap(owner->Buffer, (ulong)owner->CapacityValue, address, bytes);
    private static bool OutsideReceiver(TcpReceiveWindow.Owner* receiver, byte* address, ulong bytes) =>
        OutsideTable(receiver->Table, address, bytes) && !Overlap((byte*)receiver, (ulong)sizeof(TcpReceiveWindow.Owner), address, bytes) &&
        (receiver->Buffer == null || !Overlap(receiver->Buffer, (ulong)receiver->CapacityValue, address, bytes));
    private static bool OutsideTable(TcpConnectionTable.Owner* table, byte* address, ulong bytes) => bytes == 0 ||
        address != null && bytes <= ~0UL - (ulong)address && !Overlap((byte*)table, (ulong)sizeof(TcpConnectionTable.Owner), address, bytes) &&
        !Overlap((byte*)table->Entries, (ulong)table->CapacityValue * (ulong)sizeof(TcpConnectionTableEntry), address, bytes) &&
        !Overlap((byte*)table->Generations, (ulong)table->CapacityValue * 8, address, bytes);
    private static bool Overlap(byte* a, ulong bytes, byte* b, ulong other) => bytes != 0 && other != 0 && (ulong)a < (ulong)b + other && (ulong)b < (ulong)a + bytes;
    private static bool Storage(void* address, ulong bytes) => address != null && ((ulong)address & 7) == 0 && bytes <= ~0UL - (ulong)address;
    private static bool Zero(void* address, ulong bytes) { for (ulong i = 0; i < bytes; i++) if (((byte*)address)[i] != 0) return false; return true; }
}
