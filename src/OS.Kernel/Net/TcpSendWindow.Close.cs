namespace OS.Net;

public static unsafe partial class TcpSendWindow
{
    /// <summary>Transfer an already-entered TIME-WAIT lifetime into an exclusive
    /// compact table before allowing bulk cleanup. The serialized network owner
    /// must have removed every fd reference; unread bytes or a pending receive
    /// ACK prevent transfer. Retain the tuple, sequences and exact current2MSL
    /// deadline before closing the original table entry. Full/expired admission
    /// leaves bulk ownership intact. Success still requires ordinary sender,
    /// receiver, socket and connection-table reap; no NIC cleanup is implied.</summary>
    public static TcpTimeWaitTable.Status TransferTimeWait(Owner* owner, TcpTimeWaitTable.Owner* target)
    {
        if (Validate(owner, out var entry) != Status.Success || owner->CloseEnabled == 0 ||
            owner->CloseStateValue != TcpConnectionCloseState.TimeWait || owner->Count != 0 || owner->Sent != 0 ||
            owner->FinPending != 0 || owner->Receiver->UnreadBytes != 0 || owner->Receiver->HasPendingAck)
            return TcpTimeWaitTable.Status.InvalidState;
        if (TcpTimeWaitTable.Read(target, default, out _) != TcpTimeWaitTable.Status.StaleHandle)
            return TcpTimeWaitTable.Status.InvalidState;
        if (!Outside(owner, (byte*)target, (ulong)sizeof(TcpTimeWaitTable.Owner)) ||
            !Outside(owner, (byte*)target->Entries, target->CapacityValue * (ulong)sizeof(TcpTimeWaitTable.Entry)))
            return TcpTimeWaitTable.Status.InvalidStorage;
        if (Time(owner, out ulong now) != Status.Success)
            return TcpTimeWaitTable.Status.InvalidClock;
        var retained = TcpTimeWaitTable.Retain(target, entry.Key, owner->Next, owner->Receiver->RemoteNext,
            owner->TimeWaitDeadline, owner->TimeWaitDuration, now, out _);
        if (retained != TcpTimeWaitTable.Status.Success)
            return retained;
        return CloseTable(owner, false) == Status.Success ? TcpTimeWaitTable.Status.Success : TcpTimeWaitTable.Status.InvalidState;
    }

    /// <summary>Enable FIN/reset/half-close on the existing timed connection
    /// before control dispatch. The same serialized owner, actual monotonic
    /// callback, receive backing and copied-TX callback remain retained. The
    /// fixed close budget starts on first shutdown/peer FIN; TIME-WAIT has its
    /// own at-least-240-second duration (RFC9293's two-minute MSL, doubled)
    /// and supersedes workload/close budgets.
    /// Invalid options change nothing. This is not a socket/descriptor API.
    /// Use ReceiveFrame for all packets after enabling, PumpClose as well as
    /// existing data/ACK pumps, and explicit AbortConnection after failures.</summary>
    public static Status ConfigureClose(Owner* owner, ulong closeTimeoutNanoseconds,
        ulong timeWaitNanoseconds = 240_000_000_000)
    {
        var status = Validate(owner, out _);
        if (status != Status.Success)
            return status;
        if (owner->CloseEnabled != 0 || owner->Timed == 0)
            return Status.InvalidState;
        if (closeTimeoutNanoseconds == 0 || closeTimeoutNanoseconds > ~0UL / 16 ||
            timeWaitNanoseconds < 240_000_000_000 || timeWaitNanoseconds > ~0UL / 16)
            return Status.InvalidOptions;
        owner->CloseLimit = closeTimeoutNanoseconds;
        owner->TimeWaitDuration = timeWaitNanoseconds;
        owner->CloseStateValue = TcpConnectionCloseState.Established;
        owner->CloseEnabled = 1;
        return Status.Success;
    }

    /// <summary>Stop accepting new application writes while retaining all
    /// previously queued bytes. Pump continues their normal send/ACK/retry
    /// lifecycle. PumpClose may send FIN only after every queued byte is ACKed.
    /// Repeated shutdown is rejected unchanged; peer half-close alone permits
    /// further writes. No FIN sequence is consumed until copied TX succeeds.</summary>
    public static Status ShutdownWrite(Owner* owner)
    {
        var status = Validate(owner, out _);
        if (status != Status.Success)
            return status;
        if (owner->CloseEnabled == 0 || owner->WriteShutdown != 0)
            return Status.InvalidState;
        status = Time(owner, out ulong now);
        if (status != Status.Success)
            return status;
        owner->WriteShutdown = 1;
        StartClose(owner, now);
        return Status.Success;
    }

    /// <summary>Dispatch one original Ethernet/IPv4 TCP frame. Validate its
    /// identities, checksums, flags, ACK and receive sequence before progress.
    /// Contiguous data+FIN respects the advertised window; EOF follows buffered
    /// reads, and duplicate FIN never consumes sequence twice. Peer FIN permits
    /// remaining local sends. Exact-sequence peer RST closes with data retained
    /// for scrub; in-window nonexact RST schedules a challenge ACK. TIME-WAIT
    /// ignores RST and restarts only for the exact original consumed FIN.
    /// No borrowed frame escapes this call; no guest pointer is accepted.</summary>
    public static Status ReceiveFrame(Owner* owner, ReadOnlySpan<byte> frame,
        out TcpEstablishedStreamIpv4FramePlanResult packet)
    {
        packet = default;
        var status = Validate(owner, out var entry);
        if (status != Status.Success)
            return status;
        if (owner->CloseEnabled == 0)
            return Status.InvalidState;
        fixed (byte* bytes = frame)
            if (!Outside(owner, bytes, (ulong)frame.Length))
                return Status.InvalidStorage;
        if (frame.Length is < 54 or > 1514 || (frame[20] & 0xbf) != 0 || frame[21] != 0)
            return Status.InvalidPacket;
        status = Time(owner, out ulong now);
        if (status != Status.Success)
            return status;
        var receiver = owner->Receiver;
        var stream = TcpEstablishedStream.Create(entry.Key.LocalPort, entry.Key.RemotePort, owner->Next, receiver->RemoteNext, Span<byte>.Empty);
        if (TcpEstablishedStreamIpv4FramePlanner.ValidateFrame(stream, receiver->Info, frame, out packet) != TcpEstablishedStreamIpv4FramePlanStatus.Success)
            return Status.InvalidPacket;
        var tcp = packet.TcpHeader;
        if ((tcp.Flags & 4) != 0)
        {
            if (owner->CloseStateValue == TcpConnectionCloseState.TimeWait || packet.PayloadBytes != 0 || (tcp.Flags & ~0x14) != 0)
                return Status.InvalidPacket;
            if (tcp.SequenceNumber != receiver->RemoteNext)
            {
                if (AcceptableSequence(tcp.SequenceNumber, 0, receiver->RemoteNext, receiver->AvailableWindow))
                    receiver->AckPending = 1;
                return Status.InvalidPacket;
            }
            return AbortCore(owner, true) == Status.Success ? Status.ConnectionReset : Status.TableRejected;
        }
        if (owner->CloseStateValue == TcpConnectionCloseState.TimeWait)
        {
            if ((tcp.Flags & 0x11) != 0x11 || (tcp.Flags & ~0x19) != 0 ||
                unchecked(tcp.SequenceNumber + (uint)packet.PayloadBytes + 1) != receiver->RemoteNext ||
                unchecked(owner->Next - tcp.AcknowledgementNumber) > 1)
                return Status.InvalidPacket;
            receiver->AckPending = 1;
            var restart = EnterTimeWait(owner, now);
            return restart == Status.Success ? Status.PeerClosed : restart;
        }
        status = AcknowledgeAfterTime(owner, entry, frame, out _, true, now);
        if (status is not (Status.Success or Status.DuplicateAck or Status.WindowUpdated or Status.OldAck))
            return status;
        if (owner->CloseStateValue == TcpConnectionCloseState.Closed)
            return Status.ConnectionClosed;
        bool hadFin = receiver->PeerClosed != 0;
        var received = TcpReceiveWindow.ReceiveCloseFrame(receiver, frame, out packet);
        if (received == TcpReceiveWindow.Status.PeerClosed)
        {
            if (!hadFin)
            {
                StartClose(owner, now);
                if (owner->CloseStateValue == TcpConnectionCloseState.Established)
                    owner->CloseStateValue = TcpConnectionCloseState.CloseWait;
                else if (owner->CloseStateValue == TcpConnectionCloseState.FinWait1)
                    owner->CloseStateValue = TcpConnectionCloseState.Closing;
                else if (owner->CloseStateValue == TcpConnectionCloseState.FinWait2)
                {
                    status = EnterTimeWait(owner, now);
                    if (status != Status.Success)
                        return status;
                }
            }
            return Status.PeerClosed;
        }
        return received switch
        {
            TcpReceiveWindow.Status.Success => Status.Success,
            TcpReceiveWindow.Status.NoPayload => status,
            TcpReceiveWindow.Status.Duplicate => Status.DuplicateAck,
            TcpReceiveWindow.Status.OutOfOrder or TcpReceiveWindow.Status.WindowFull => Status.WouldBlock,
            TcpReceiveWindow.Status.InvalidStorage => Status.InvalidStorage,
            _ => Status.InvalidPacket
        };
    }

    /// <summary>Perform at most one close action: send the first FIN after
    /// queued data drains, pump an outstanding FIN's existing RTO, or complete
    /// actual TIME-WAIT. Full/short output preserves state; copied FIN consumes
    /// exactly one sequence. Graceful completion retains unread peer bytes/EOF
    /// until explicit receiver cleanup. No table slot or backing is released.
    /// Failure retains ownership and requires AbortConnection before cleanup.</summary>
    public static Status PumpClose(Owner* owner, Span<byte> output,
        out TcpEstablishedStreamIpv4FramePlanResult packet)
    {
        packet = default;
        var status = Validate(owner, out var entry);
        if (status != Status.Success)
            return status;
        if (owner->CloseEnabled == 0)
            return Status.InvalidState;
        if (NoTimedWork(owner))
            return Status.NothingToSend;
        status = Time(owner, out ulong now);
        if (status != Status.Success)
            return status;
        if (owner->CloseStateValue == TcpConnectionCloseState.TimeWait)
        {
            if (now < owner->TimeWaitDeadline)
                return Status.NotDue;
            return CloseTable(owner, false);
        }
        if (owner->FinPending != 0)
            return PumpRetransmissionAfterTime(owner, entry, output, now, out packet);
        if (owner->WriteShutdown == 0)
            return Status.NothingToSend;
        if (owner->Count != 0 || owner->Sent != 0)
            return Status.WaitingForDrain;
        if (owner->CloseStateValue is not (TcpConnectionCloseState.Established or TcpConnectionCloseState.CloseWait))
            return Status.AwaitingAck;
        if (unchecked((int)(owner->WindowAck + owner->PeerWindow - owner->Next)) <= 0)
            return Status.WindowClosed;
        if (output.Length < 54)
            return Status.OutputTooSmall;
        fixed (byte* bytes = output)
            if (!Outside(owner, bytes, (ulong)output.Length))
                return Status.InvalidStorage;
        var receiver = owner->Receiver;
        var stream = TcpEstablishedStream.Create(entry.Key.LocalPort, entry.Key.RemotePort, owner->Next, receiver->RemoteNext, Span<byte>.Empty);
        if (TcpEstablishedStreamIpv4FramePlanner.TryWriteControlFrame(ref stream, receiver->Info, receiver->AvailableWindow, 0x11, output, out packet) != TcpEstablishedStreamIpv4FramePlanStatus.Success)
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
        owner->Next = unchecked(owner->Next + 1);
        owner->FinPending = 1;
        owner->CloseStateValue = owner->CloseStateValue == TcpConnectionCloseState.CloseWait ? TcpConnectionCloseState.LastAck : TcpConnectionCloseState.FinWait1;
        Commit(owner, entry);
        owner->Sampling = 0;
        owner->RetryCount = 0;
        status = Time(owner, out now);
        if (status != Status.Success)
            return status;
        owner->RetryAt = Deadline(now, owner->Rto);
        return Status.Success;
    }

    /// <summary>After the serialized socket owner removes the last fd, signal
    /// unread-data loss with RST|ACK at this exact connection's current sequences.
    /// Require write shutdown and retained unread bytes. Copied-TX pressure keeps
    /// all ownership for retry within the existing close deadline, including
    /// TIME-WAIT; Timeout requires RetireTimedOut and claims no transmitted reset.
    /// ConnectionReset means copied output and exact table closure, not delivery.
    /// Other failures retain backing; sender/receiver closed reap is still owned
    /// by the caller. Never use this for a live duplicate fd or SHUT_WR alone.</summary>
    public static Status ResetUnread(Owner* owner, Span<byte> output,
        out TcpEstablishedStreamIpv4FramePlanResult packet)
    {
        packet = default;
        var status = Validate(owner, out var entry);
        if (status != Status.Success)
            return status;
        if (owner->CloseEnabled == 0 || owner->WriteShutdown == 0 || owner->CloseDeadline == 0 ||
            owner->Receiver->UnreadBytes == 0)
            return Status.InvalidState;
        status = Time(owner, out ulong now);
        if (status != Status.Success)
            return status;
        // Ordinary TIME-WAIT bypasses the close budget; explicit unread abort
        // must still terminate a connection whose reset cannot enter copied TX.
        if (now >= owner->CloseDeadline)
        {
            owner->Phase = 2;
            owner->TimeoutError = 1;
            return Status.Timeout;
        }
        if (output.Length < 54)
            return Status.OutputTooSmall;
        fixed (byte* bytes = output)
            if (!Outside(owner, bytes, (ulong)output.Length))
                return Status.InvalidStorage;
        var receiver = owner->Receiver;
        var stream = TcpEstablishedStream.Create(entry.Key.LocalPort, entry.Key.RemotePort, owner->Next, receiver->RemoteNext, Span<byte>.Empty);
        if (TcpEstablishedStreamIpv4FramePlanner.TryWriteControlFrame(ref stream, receiver->Info, 0, 0x14, output, out packet) !=
            TcpEstablishedStreamIpv4FramePlanStatus.Success)
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
        status = AbortCore(owner, true);
        return status == Status.Success ? Status.ConnectionReset : status;
    }

    /// <summary>Explicitly abandon the exact held connection after a caller
    /// abort or failed send/clock/timeout. Accept active or faulted retained
    /// metadata, stop retries and close its table entry without releasing or
    /// scrubbing any buffer. Subsequent reads cannot return aborted bytes.
    /// Caller must perform sender/receiver ReleaseClosed and table ReleaseOwned
    /// exactly once. This emits no synthetic RST and proves no NIC/DMA teardown.</summary>
    public static Status AbortConnection(Owner* owner)
    {
        var status = Validate(owner, out _, allowFault: true);
        if (status != Status.Success)
            return status;
        return AbortCore(owner, false);
    }

    /// <summary>Retire only an actual timed-out retained connection, publishing
    /// reset-like transport closure with its distinct timeout observation. The
    /// serialized caller must have received Timeout from this exact owner.
    /// No frame is emitted or backing released; closed reap remains mandatory.
    /// Invalid/corrupt ownership is rejected without claiming peer retirement.</summary>
    public static Status RetireTimedOut(Owner* owner)
    {
        var status = Validate(owner, out _, allowFault: true);
        if (status != Status.Success)
            return status;
        if (owner->Phase != 2 || owner->TimeoutError != 1)
            return Status.InvalidState;
        return AbortCore(owner, true);
    }

    private static Status AbortCore(Owner* owner, bool reset)
    {
        var status = CloseTable(owner, reset);
        if (status == Status.Success)
            owner->Receiver->PeerClosed = 0;
        return status;
    }

    private static Status CloseTable(Owner* owner, bool reset)
    {
        var status = TcpConnectionTable.TransitionOwned(owner->Table, owner->Handle,
            TcpConnectionLifecycleState.Established, TcpConnectionLifecycleState.Closed);
        if (status != TcpConnectionTable.OwnershipStatus.Success)
            return Status.TableRejected;
        owner->CloseStateValue = reset ? TcpConnectionCloseState.Reset : TcpConnectionCloseState.Closed;
        if (!reset)
            owner->TimeoutError = 0;
        owner->RetryAt = 0;
        owner->PersistAt = 0;
        owner->PersistDelay = 0;
        owner->WriteShutdown = 1;
        owner->Phase = 2;
        return Status.Success;
    }

    private static void StartClose(Owner* owner, ulong now)
    {
        if (owner->CloseDeadline == 0)
            owner->CloseDeadline = Deadline(now, owner->CloseLimit);
    }

    private static Status EnterTimeWait(Owner* owner, ulong now)
    {
        // Saturating here could reclaim before the actual 2MSL duration. Fail
        // with the connection retained if this clock domain cannot represent it.
        if (owner->TimeWaitDuration > ~0UL - now)
        {
            owner->Phase = 2;
            return Status.InvalidClock;
        }
        owner->CloseStateValue = TcpConnectionCloseState.TimeWait;
        owner->PersistAt = 0;
        owner->PersistDelay = 0;
        owner->TimeWaitDeadline = now + owner->TimeWaitDuration;
        owner->RetryAt = 0;
        return Status.Success;
    }

    private static Status FinAcknowledged(Owner* owner, ulong now)
    {
        if (owner->CloseStateValue == TcpConnectionCloseState.FinWait1)
            owner->CloseStateValue = TcpConnectionCloseState.FinWait2;
        else if (owner->CloseStateValue == TcpConnectionCloseState.Closing)
            return EnterTimeWait(owner, now);
        else if (owner->CloseStateValue == TcpConnectionCloseState.LastAck)
            return CloseTable(owner, false);
        return Status.Success;
    }
}
