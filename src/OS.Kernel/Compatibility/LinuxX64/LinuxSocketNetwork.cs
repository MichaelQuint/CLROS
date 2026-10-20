#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using System;
using OS.Net;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Pending = OS.Net.TcpPassiveConnection;
using Rx = OS.Net.TcpReceiveWindow;
using Tx = OS.Net.TcpSendWindow;
using Waits = OS.Net.TcpTimeWaitTable;

namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>Serialized packet/timer owner connecting an actual copied-frame NIC
/// to one process's passive Linux TCP sockets. Caller retains all mapped,
/// exclusive supervisor storage/callbacks through End; no allocation, NIC polling,
/// CPU transfer or guest pointer access occurs here. Exact tuple/generation
/// lookup selects retained owners. Pending, queued and fd-less closing lifetimes
/// consume bounded capacity; normal TIME-WAIT is never shortened. Invalid packets
/// are ignored, capacity pressure is explicit, and ownership failure retains
/// backing for diagnosis. No UDP, routing, arbitrary peer or synthetic RST exists.</summary>
public static unsafe class LinuxSocketNetwork
{
    public const uint MaximumConnections = 16;
    public enum Status : byte { Success, Ignored, Capacity, InvalidStorage, InvalidState, TransportFailure }

    /// <summary>Initially zero, noncopyable connection metadata. The private
    /// one-entry handshake bridge is consumed only after its original table
    /// generation establishes. Socket transfer retains both stream windows until
    /// the adapter permits exact closed reap. Never independently release them.</summary>
    public struct Slot
    {
        internal Pending.State Handshake;
        internal Rx.Owner Receiver;
        internal Tx.Owner Sender;
        internal TcpListenerBacklogEntry Backlog;
        internal Fds.Token Listener;
        internal ulong ListenerInode;
        internal uint ListenerFd, ListenerDescription, Phase;
        internal ulong LeaseGeneration;
    }

    /// <summary>One-use owner borrowing an empty initialized connection table,
    /// zeroed slots and contiguous per-slot RX then TX backing. Buffers, metadata,
    /// table storage and descriptor ownership are mutually disjoint. Admission
    /// precedes thread attachment; the thread owner checks these extents before
    /// borrowing any worker metadata. Public counts are observations only.</summary>
    public struct State
    {
        internal ulong Self;
        internal LinuxSocketServices.State* Sockets;
        internal TcpConnectionTable.Owner* Table;
        internal Slot* Slots;
        internal byte* Buffers;
        internal uint Count, ReceiveBytes, SendBytes, NextSequence, Initialized, Failed;
        internal uint MeasureLeases, Leased, PeakLeased;
        internal ulong LeaseAcquired, LeaseReturned;
        internal MacAddress LocalMac, PeerMac;
        internal Ipv4Address PeerAddress;
        internal Pending.Operations Io;
        internal TcpInitialSequence.Owner* Sequence;
        internal TcpInitialSequence.Lease SequenceLease;
        internal Waits.Owner TimeWait;
        internal uint CompactTimeWait;
        internal fixed ulong TimeWaitEntries[(int)Waits.MaximumEntries * 8];
        internal fixed ulong TimeWaitAcks[(int)Waits.MaximumEntries];
        public readonly uint CompactTimeWaitConnections => CompactTimeWait == 1 ? (uint)TimeWait.Count : 0;
        public readonly uint RetainedConnections
        {
            get { uint n = 0; if (Initialized == 1) { n = CompactTimeWaitConnections; for (uint i = 0; i < Count; i++) if (Slots[i].Phase != 0) n++; } return n; }
        }
        public readonly bool RequiresShutdown => Initialized == 1 && Failed != 0;
    }

    /// <summary>Attach sockets, then epoll, then this network owner, then threads.
    /// The actual configured NIC/peer MACs and IPv4 peer are required. All extents
    /// must be trusted mapped supervisor storage, inaccessible to the application.
    /// RX/TX capacities are 8..32768 bytes, aligned to8; initial sequence entropy
    /// belongs to the caller. Rejection changes nothing. The IO context/code
    /// remains retained and synchronous/nonreentrant; TX copies the whole frame.
    /// Explicit compact mode embeds64 bounded TIME-WAIT records and their ACK
    /// generations in State; fd-less drained bulk windows may then be scrubbed
    /// and reused while their original tuples remain protected. Optional sequence
    /// owner replaces the finite diagnostic counter, requires initialSequence=0,
    /// and is borrowed through End. It is disjoint from every retained extent,
    /// worker, packet and output; its startup/rekey quiet time ignores new SYNs.
    /// Optional lease measurement counts successful RX/TX owner bindings, not
    /// physical allocation or payload usage; it defaults off.</summary>
    public static bool Begin(State* state, LinuxSocketServices.State* sockets, TcpConnectionTable.Owner* table,
        Slot* slots, uint count, byte* buffers, uint receiveBytes, uint sendBytes,
        MacAddress localMac, MacAddress peerMac, Ipv4Address peerAddress, uint initialSequence, Pending.Operations io,
        bool compactTimeWait = false, TcpInitialSequence.Owner* sequence = null, bool measureLeases = false)
    {
        if (sizeof(Waits.Entry) > 64 || count is 0 or > MaximumConnections || receiveBytes is < 8 or > 32768 || sendBytes is < 8 or > 32768 ||
            ((receiveBytes | sendBytes) & 7) != 0 ||
            (sequence == null ? initialSequence == 0 || initialSequence > uint.MaxValue - 65536 : initialSequence != 0) ||
            !Extent(state, (ulong)sizeof(State)) || !Extent(sockets, (ulong)sizeof(LinuxSocketServices.State)) ||
            sockets->Self != (ulong)sockets || sockets->Initialized != 1 || !Fds.Audit(sockets->Descriptors) ||
            sockets->Descriptors->Sockets != sockets || sockets->Descriptors->ThreadOwner != 0 || sockets->Descriptors->Epoll == null ||
            sockets->Network != null || sockets->RetainedConnections != 0 || !Extent(table, (ulong)sizeof(TcpConnectionTable.Owner)) ||
            TcpConnectionTable.ReadOwned(table, default, out _) != TcpConnectionTable.OwnershipStatus.StaleHandle ||
            table->Count != 0 || table->Capacity < count || (localMac.Octet0 & 1) != 0 || (peerMac.Octet0 & 1) != 0 ||
            localMac == default || peerMac == default || peerAddress == default || peerAddress.Octet0 is 0 or >= 224 ||
            io.Context == null || io.NowNanoseconds == null || io.Send == null || io.LifetimeNanoseconds is 0 or > 60_000_000_000 ||
            io.RetryNanoseconds == 0 || io.RetryNanoseconds > io.LifetimeNanoseconds || io.MaximumAttempts is 0 or > 8) return false;
        ulong* starts = stackalloc ulong[6]; ulong* sizes = stackalloc ulong[6];
        Extents(state, table, slots, count, buffers, receiveBytes, sendBytes, starts, sizes);
        for (uint i = 0; i < 6; i++)
        {
            if (!Extent((void*)starts[i], sizes[i]) || !Fds.SocketOutside(sockets->Descriptors, (void*)starts[i], sizes[i])) return false;
            for (uint j = 0; j < i; j++) if (Overlap(starts[i], sizes[i], starts[j], sizes[j])) return false;
        }
        if (!Zero(state, sizes[0]) || !Zero(slots, sizes[1]) || !Zero(buffers, sizes[2])) return false;
        if (sequence != null)
        {
            if (!TcpInitialSequence.IsInitialized(sequence) || !Fds.SocketOutside(sockets->Descriptors, sequence, (uint)sizeof(TcpInitialSequence.Owner))) return false;
            for (uint i = 0; i < 6; i++)
                if (Overlap(starts[i], sizes[i], (ulong)sequence, (ulong)sizeof(TcpInitialSequence.Owner))) return false;
        }
        *state = new() { Self = (ulong)state, Sockets = sockets, Table = table, Slots = slots, Count = count,
            Buffers = buffers, ReceiveBytes = receiveBytes, SendBytes = sendBytes, LocalMac = localMac,
            PeerMac = peerMac, PeerAddress = peerAddress, NextSequence = initialSequence, Io = io, Initialized = 1,
            MeasureLeases = measureLeases ? 1U : 0U };
        if (compactTimeWait)
        {
            if (Waits.Initialize(&state->TimeWait, (Waits.Entry*)state->TimeWaitEntries, Waits.MaximumEntries,
                table->Epoch, io.NowNanoseconds(io.Context)) != Waits.Status.Success) { *state = default; return false; }
            state->CompactTimeWait = 1;
        }
        if (sequence != null)
        {
            var lease = TcpInitialSequence.Acquire(sequence, state, io.NowNanoseconds(io.Context));
            if (!lease.IsValid) { *state = default; return false; }
            state->Sequence = sequence; state->SequenceLease = lease;
        }
        sockets->Network = state; return true;
    }

    /// <summary>Dispatch one complete trusted Ethernet frame with distinct input
    /// and output backing aligned to8 and outside all retained owners, including
    /// descriptor metadata and application backing. Verify addresses/checksums
    /// before any tuple action. Only a real listener admits SYN. Successful ACK
    /// establishes genuine windows before socket transfer; payload/control frames
    /// always use the original retained transport. No frame pointer escapes.</summary>
    public static Status Receive(State* state, ReadOnlySpan<byte> frame, Span<byte> output)
    {
        if (!Current(state)) return Status.InvalidState;
        if (!Frames(state, frame, output)) return Status.InvalidStorage;
        if (EthernetHeader.TryParse(frame, out var ethernet, out var ipBytes) != PacketParseStatus.Success ||
            ethernet.Source != state->PeerMac || ethernet.Destination != state->LocalMac || ethernet.EtherType != 0x800 ||
            Ipv4Header.TryParse(ipBytes, out var ip, out var segment) != PacketParseStatus.Success || ip.Protocol != 6 ||
            ip.Source != state->PeerAddress || ip.Destination != state->Sockets->InterfaceAddress || (ip.FlagsAndFragmentOffset & ~0x4000) != 0 ||
            TcpHeader.TryParse(segment, out var tcp, out _, out var payload) != PacketParseStatus.Success || tcp.SourcePort == 0 || tcp.DestinationPort != state->Sockets->AllowedPort ||
            Ipv4TransportChecksum.Validate(ip.Source, ip.Destination, 6, segment, out var checksum) != Ipv4TransportChecksumStatus.Success || !checksum.IsValid)
            return Status.Ignored;
        var key = new TcpConnectionKey(ip.Destination, tcp.DestinationPort, ip.Source, tcp.SourcePort);
        if (state->CompactTimeWait != 0)
        {
            ulong now = state->Io.NowNanoseconds(state->Io.Context);
            if (Waits.Expire(&state->TimeWait, now) != Waits.Status.Success) return Fail(state);
            var waiting = Waits.Find(&state->TimeWait, key, out var retired, out var record);
            if (waiting == Waits.Status.Success)
            {
                if ((tcp.Flags & 0x11) != 0x11 || (tcp.Flags & ~0x19) != 0 ||
                    unchecked(tcp.SequenceNumber + (uint)payload.Length + 1) != record.RemoteNextSequenceNumber ||
                    unchecked(record.LocalNextSequenceNumber - tcp.AcknowledgementNumber) > 1) return Status.Ignored;
                if (Waits.Restart(&state->TimeWait, retired, now) != Waits.Status.Success) return Fail(state);
                state->TimeWaitAcks[retired.Index] = retired.Generation;
                return SendTimeWaitAck(state, retired, record, output);
            }
            if (waiting != Waits.Status.NotFound) return Fail(state);
        }
        var found = TcpConnectionTable.FindOwned(state->Table, key, out var handle);
        if (found == TcpConnectionTable.OwnershipStatus.Success)
        {
            Slot* slot = null;
            for (uint i = 0; i < state->Count; i++)
                if (state->Slots[i].Phase != 0 && Same(state->Slots[i].Handshake.Connection, handle)) { slot = state->Slots + i; break; }
            if (slot == null) return Fail(state);
            if (TcpConnectionTable.ReadOwned(state->Table, handle, out var held) != TcpConnectionTable.OwnershipStatus.Success) return Fail(state);
            if (held.State == TcpConnectionLifecycleState.Closed) return Status.Ignored;
            if (slot->Phase == 1)
            {
                if ((tcp.Flags & 4) != 0)
                    return PendingResult(state, slot, Pending.ReceiveReset(&slot->Handshake, frame));
                if ((tcp.Flags & 2) != 0)
                    return PendingResult(state, slot, Pending.ReceiveSyn(&slot->Handshake, frame, output, out _));
                var ack = Pending.ReceiveAck(&slot->Handshake, frame, new Span<TcpListenerBacklogEntry>(&slot->Backlog, 1), out _, out _);
                if (ack != Pending.Status.Success) return PendingResult(state, slot, ack);
                slot->Phase = 2;
                return Establish(state, slot, payload.Length == 0 ? default : frame);
            }
            var received = Tx.ReceiveFrame(&slot->Sender, frame, out _);
            if (received is Tx.Status.InvalidPacket or Tx.Status.InvalidAck or Tx.Status.OldAck) return Status.Ignored;
            return TransportResult(state, slot, received);
        }
        if (found != TcpConnectionTable.OwnershipStatus.NotFound) return Fail(state);
        if (tcp.Flags != 2 || !LinuxSocketServices.Listener(state->Sockets, out uint listenerFd, out var listener)) return Status.Ignored;
        uint index = 0; for (; index < state->Count && state->Slots[index].Phase != 0; index++) { }
        if (index == state->Count || state->Sequence == null && state->NextSequence > uint.MaxValue - 65536) return Status.Capacity;
        uint initial = state->NextSequence;
        if (state->Sequence != null)
        {
            var next = TcpInitialSequence.Next(state->Sequence, state->SequenceLease, key, state->Io.NowNanoseconds(state->Io.Context));
            if (next.Status == TcpInitialSequence.Status.Quiet) return Status.Ignored;
            if (next.Status != TcpInitialSequence.Status.Success) return Fail(state);
            initial = next.Sequence;
        }
        Slot* free = state->Slots + index;
        var info = new TcpPassiveIpv4FramePlannerInfo(state->LocalMac, state->PeerMac,
            state->Sockets->InterfaceAddress, state->PeerAddress, state->Sockets->AllowedPort, tcp.SourcePort,
            initial, (ushort)state->ReceiveBytes, maximumSegmentSize: 1460);
        var status = Pending.Begin(&free->Handshake, state->Table, info, frame, output, state->Io, out _);
        if (free->Handshake.Pending || free->Handshake.RequiresCleanup)
        {
            free->Phase = 1; free->Listener = listener; free->ListenerFd = listenerFd;
            free->ListenerDescription = state->Sockets->Descriptors->Owner.Slots[listenerFd].Description - 1;
            free->ListenerInode = Fds.Find(state->Sockets->Descriptors, listenerFd)->Inode;
            if (state->Sequence == null) state->NextSequence += 65536;
        }
        return PendingResult(state, free, status);
    }

    /// <summary>Perform bounded pending retry, copied send/ACK/retransmission,
    /// FIN/TIME-WAIT and exact closed reap work for every retained connection.
    /// Caller drains received NIC frames first and refreshes thread readiness
    /// after this call. Capacity/full TX preserves ownership. Failure never frees
    /// backing; run explicit CloseDescriptors/abort shutdown before End. Retry
    /// and close expiry retire only their connection. No implicit total or idle
    /// lifetime applies; the application/qualification owner supplies its budget.</summary>
    public static Status Pump(State* state, Span<byte> output)
    {
        if (!Current(state)) return Status.InvalidState;
        if (!Frames(state, default, output)) return Status.InvalidStorage;
        if (state->CompactTimeWait != 0)
        {
            if (Waits.Expire(&state->TimeWait, state->Io.NowNanoseconds(state->Io.Context)) != Waits.Status.Success) return Fail(state);
            for (ushort i = 0; i < Waits.MaximumEntries; i++)
            {
                if (state->TimeWaitAcks[i] == 0) continue;
                var handle = new Waits.Handle(&state->TimeWait, i, state->TimeWaitAcks[i]);
                var read = Waits.Read(&state->TimeWait, handle, out var record);
                if (read == Waits.Status.StaleHandle) { state->TimeWaitAcks[i] = 0; continue; }
                if (read != Waits.Status.Success || SendTimeWaitAck(state, handle, record, output) == Status.TransportFailure) return Fail(state);
            }
        }
        for (uint i = 0; i < state->Count; i++)
        {
            Slot* slot = state->Slots + i;
            if (slot->Phase == 0) continue;
            if (slot->Phase == 1)
            {
                if (!RefreshListener(state, slot))
                {
                    if (Pending.CancelPending(&slot->Handshake) != Pending.Status.Success) return Fail(state);
                    *slot = default; continue;
                }
                Status pending = PendingResult(state, slot, Pending.Pump(&slot->Handshake, output, out _));
                if (pending == Status.TransportFailure) return pending;
                continue;
            }
            if (slot->Phase == 2)
            {
                if (!RefreshListener(state, slot))
                {
                    if (TcpConnectionTable.ReadOwned(state->Table, slot->Handshake.Connection, out var abandoned) != TcpConnectionTable.OwnershipStatus.Success ||
                        abandoned.State != TcpConnectionLifecycleState.Closed && Tx.AbortConnection(&slot->Sender) != Tx.Status.Success ||
                        !ReapPrivate(state, slot)) return Fail(state);
                    continue;
                }
                if (LinuxSocketServices.EnqueueAccepted(state->Sockets, slot->Listener, slot->ListenerFd, &slot->Receiver, &slot->Sender)) slot->Phase = 3;
            }
            if (TcpConnectionTable.ReadOwned(state->Table, slot->Handshake.Connection, out var entry) != TcpConnectionTable.OwnershipStatus.Success) return Fail(state);
            if (entry.State == TcpConnectionLifecycleState.Closed)
            {
                if (slot->Phase == 2) { if (!ReapPrivate(state, slot)) return Fail(state); }
                else if (LinuxSocketServices.IsClosing(state->Sockets, &slot->Sender))
                {
                    if (!LinuxSocketServices.ReapClosed(state->Sockets, &slot->Sender) || !ReturnLease(state, slot)) return Fail(state);
                    *slot = default;
                }
                continue;
            }
            if (slot->Receiver.UnreadBytes != 0 && LinuxSocketServices.IsClosing(state->Sockets, &slot->Sender))
            {
                var reset = TransportResult(state, slot, Tx.ResetUnread(&slot->Sender, output, out _));
                if (reset == Status.TransportFailure) return reset;
                if (slot->Sender.CloseState == TcpConnectionCloseState.Reset)
                {
                    if (!LinuxSocketServices.ReapClosed(state->Sockets, &slot->Sender) || !ReturnLease(state, slot)) return Fail(state);
                    *slot = default;
                }
                continue;
            }
            var sent = TransportResult(state, slot, Tx.Pump(&slot->Sender, output, out _));
            if (sent == Status.TransportFailure) return sent;
            if (sent == Status.Ignored) continue;
            var retry = TransportResult(state, slot, Tx.PumpRetransmission(&slot->Sender, output, out _));
            if (retry == Status.TransportFailure) return retry;
            if (retry == Status.Ignored) continue;
            var ack = Rx.PumpAck(&slot->Receiver, output, out _);
            if (ack is not (Rx.Status.Success or Rx.Status.NothingToAck or Rx.Status.WouldBlock)) return Fail(state);
            if (TransportResult(state, slot, Tx.PumpClose(&slot->Sender, output, out _)) == Status.TransportFailure) return Status.TransportFailure;
            if (state->CompactTimeWait != 0 && slot->Sender.CloseState == TcpConnectionCloseState.TimeWait &&
                slot->Receiver.UnreadBytes == 0 && !slot->Receiver.HasPendingAck &&
                LinuxSocketServices.IsClosing(state->Sockets, &slot->Sender))
            {
                var transferred = Tx.TransferTimeWait(&slot->Sender, &state->TimeWait);
                if (transferred == Waits.Status.Success)
                {
                    if (!LinuxSocketServices.ReapClosed(state->Sockets, &slot->Sender) || !ReturnLease(state, slot)) return Fail(state);
                    *slot = default;
                }
                else if (transferred is not (Waits.Status.Full or Waits.Status.Expired or Waits.Status.GenerationExhausted)) return Fail(state);
            }
        }
        return Status.Success;
    }

    /// <summary>Failure/explicit process-abort cleanup only, after all actual
    /// threads detach and every fd closes. This abandons remaining transport
    /// without claiming FIN/TIME-WAIT completion or emitting a synthetic RST.
    /// Cancel exact pending generations and explicitly abort/reap closed windows;
    /// no backing is freed. Failure retains the owner for diagnosis. Normal
    /// qualification drains Pump through real close time before End instead.</summary>
    public static bool Abort(State* state)
    {
        if (!Current(state, true) || state->Sockets->Descriptors->ThreadOwner != 0 ||
            state->Sockets->Descriptors->ActiveDescriptors != 0) return false;
        for (uint i = 0; i < state->Count; i++)
        {
            Slot* slot = state->Slots + i;
            if (slot->Phase == 0) continue;
            if (slot->Phase == 1)
            {
                if (!slot->Handshake.Retired && Pending.CancelPending(&slot->Handshake) != Pending.Status.Success) return false;
                *slot = default; continue;
            }
            if (TcpConnectionTable.ReadOwned(state->Table, slot->Handshake.Connection, out var entry) != TcpConnectionTable.OwnershipStatus.Success) return false;
            if (entry.State != TcpConnectionLifecycleState.Closed && Tx.AbortConnection(&slot->Sender) != Tx.Status.Success) return false;
            if (slot->Phase == 2) { if (!ReapPrivate(state, slot)) return false; }
            else
            {
                if (!LinuxSocketServices.IsClosing(state->Sockets, &slot->Sender) ||
                    !LinuxSocketServices.ReapClosed(state->Sockets, &slot->Sender) || !ReturnLease(state, slot)) return false;
                *slot = default;
            }
        }
        if (state->CompactTimeWait != 0)
        {
            if (Waits.Discard(&state->TimeWait) != Waits.Status.Success) return false;
            for (uint i = 0; i < Waits.MaximumEntries; i++) state->TimeWaitAcks[i] = 0;
        }
        return state->Table->Count == 0;
    }

    private static Status Establish(State* state, Slot* slot, ReadOnlySpan<byte> initialPayloadFrame)
    {
        var accepted = TcpListenerBacklog.Accept(new Span<TcpListenerBacklogEntry>(&slot->Backlog, 1));
        if (!accepted.Succeeded || TcpConnectionTable.ReadOwned(state->Table, slot->Handshake.Connection, out var entry) != TcpConnectionTable.OwnershipStatus.Success ||
            accepted.Connection.LocalAddress != entry.Key.LocalAddress || accepted.Connection.LocalPort != entry.Key.LocalPort ||
            accepted.Connection.RemoteAddress != entry.Key.RemoteAddress || accepted.Connection.RemotePort != entry.Key.RemotePort ||
            accepted.Connection.LocalNextSequenceNumber != entry.Sequence.LocalNextSequenceNumber ||
            accepted.Connection.RemoteNextSequenceNumber != entry.Sequence.RemoteNextSequenceNumber) return Fail(state);
        uint index = (uint)(slot - state->Slots);
        byte* buffer = state->Buffers + index * (state->ReceiveBytes + state->SendBytes);
        var info = new TcpEstablishedStreamIpv4FramePlannerInfo(state->LocalMac, state->PeerMac,
            state->Sockets->InterfaceAddress, state->PeerAddress, 1460, (ushort)state->ReceiveBytes);
        if (Rx.Initialize(&slot->Receiver, state->Table, slot->Handshake.Connection, buffer, state->ReceiveBytes, info,
                state->Io.Context, state->Io.Send) != Rx.Status.Success ||
            Tx.Initialize(&slot->Sender, &slot->Receiver, &slot->Handshake, buffer + state->ReceiveBytes, state->SendBytes, 1500) != Tx.Status.Success ||
            Tx.ConfigureRetransmission(&slot->Sender, state->Io.Context, state->Io.NowNanoseconds, 0, continuing: true) != Tx.Status.Success ||
            Tx.ConfigureClose(&slot->Sender, 30_000_000_000) != Tx.Status.Success) return Fail(state);
        if (state->MeasureLeases != 0)
        {
            if (slot->LeaseGeneration != 0 || !LeaseOwners(state, slot) || state->Leased >= state->Count || state->LeaseAcquired == ~0UL)
                return Fail(state);
            slot->LeaseGeneration = slot->Handshake.Connection.Generation;
            state->Leased++; state->LeaseAcquired++;
            if (state->Leased > state->PeakLeased) state->PeakLeased = state->Leased;
        }
        Status result = Status.Success;
        if (initialPayloadFrame.Length != 0)
        {
            // The handshake advanced only SYN/ACK sequences. Its borrowed frame
            // may also contain data: consume it once through the real stream,
            // before any application can observe the accepted socket.
            var received = Tx.ReceiveFrame(&slot->Sender, initialPayloadFrame, out _);
            if (received is Tx.Status.InvalidPacket or Tx.Status.InvalidAck or Tx.Status.OldAck) result = Status.Ignored;
            else
            {
                result = TransportResult(state, slot, received);
                if (result != Status.Success) return result;
            }
        }
        if (RefreshListener(state, slot) && LinuxSocketServices.EnqueueAccepted(state->Sockets, slot->Listener, slot->ListenerFd, &slot->Receiver, &slot->Sender)) slot->Phase = 3;
        return result;
    }

    private static bool RefreshListener(State* state, Slot* slot)
    {
        if (Fds.IsCurrent(state->Sockets->Descriptors, slot->Listener)) return true;
        if (!LinuxSocketServices.Listener(state->Sockets, out uint fd, out var token)) return false;
        if (state->Sockets->Descriptors->Owner.Slots[fd].Description != slot->ListenerDescription + 1 ||
            Fds.Find(state->Sockets->Descriptors, fd)->Inode != slot->ListenerInode) return false;
        slot->Listener = token; slot->ListenerFd = fd; return true;
    }

    private static Status PendingResult(State* state, Slot* slot, Pending.Status result)
    {
        if ((result is Pending.Status.Success or Pending.Status.Timeout or Pending.Status.RetryLimit) && slot->Handshake.Retired)
        { *slot = default; return Status.Success; }
        if (result == Pending.Status.RetryLimit && slot->Handshake.Pending) return Status.Success;
        if (result is Pending.Status.Success or Pending.Status.WouldBlock or Pending.Status.NotDue) return Status.Success;
        if (result is Pending.Status.InvalidPacket or Pending.Status.NotSent or Pending.Status.BacklogRejected) return Status.Ignored;
        if (result == Pending.Status.TableRejected && slot->Phase == 0) return Status.Capacity;
        return Fail(state);
    }

    private static Status SendTimeWaitAck(State* state, Waits.Handle handle, Waits.Record record, Span<byte> output)
    {
        var info = new TcpEstablishedStreamIpv4FramePlannerInfo(state->LocalMac, state->PeerMac,
            record.Key.LocalAddress, record.Key.RemoteAddress, 1460, (ushort)state->ReceiveBytes);
        var stream = TcpEstablishedStream.Create(record.Key.LocalPort, record.Key.RemotePort,
            record.LocalNextSequenceNumber, record.RemoteNextSequenceNumber, Span<byte>.Empty);
        if (TcpEstablishedStreamIpv4FramePlanner.TryWriteAckFrame(ref stream, info, (ushort)state->ReceiveBytes, output, out var packet) !=
            TcpEstablishedStreamIpv4FramePlanStatus.Success) return Fail(state);
        byte sent;
        fixed (byte* bytes = output) sent = state->Io.Send(state->Io.Context, bytes, (uint)packet.BytesWritten);
        if (sent > 1) return Fail(state);
        if (sent == 1) state->TimeWaitAcks[handle.Index] = 0;
        return Status.Success;
    }
    private static bool ReapPrivate(State* state, Slot* slot)
    {
        var handle = slot->Handshake.Connection;
        if (Tx.ReleaseClosed(&slot->Sender) != Tx.Status.Success || Rx.ReleaseClosed(&slot->Receiver) != Rx.Status.Success ||
            TcpConnectionTable.ReleaseOwned(state->Table, handle) != TcpConnectionTable.OwnershipStatus.Success || !ReturnLease(state, slot)) return false;
        *slot = default; return true;
    }

    // Called only after the actual two window owners have released/scrubbed.
    private static bool ReturnLease(State* state, Slot* slot)
    {
        if (state->MeasureLeases == 0) return true;
        uint index = (uint)(slot - state->Slots);
        if (index >= state->Count || slot->LeaseGeneration == 0 || slot->LeaseGeneration != slot->Handshake.Connection.Generation ||
            state->Leased == 0 || state->LeaseReturned >= state->LeaseAcquired || slot->Receiver.Buffer != null || slot->Sender.Buffer != null ||
            !Zero(state->Buffers + index * (state->ReceiveBytes + state->SendBytes), state->ReceiveBytes + state->SendBytes)) return false;
        slot->LeaseGeneration = 0; state->Leased--; state->LeaseReturned++; return true;
    }

    private static bool LeaseOwners(State* state, Slot* slot)
    {
        uint index = (uint)(slot - state->Slots);
        if (index >= state->Count) return false;
        byte* buffer = state->Buffers + index * (state->ReceiveBytes + state->SendBytes);
        return slot->Receiver.Self == (ulong)(&slot->Receiver) && slot->Sender.Self == (ulong)(&slot->Sender) &&
            slot->Receiver.Table == state->Table && slot->Sender.Table == state->Table && slot->Sender.Receiver == &slot->Receiver &&
            Same(slot->Receiver.Handle, slot->Handshake.Connection) && Same(slot->Sender.Handle, slot->Handshake.Connection) &&
            slot->Receiver.Buffer == buffer && slot->Sender.Buffer == buffer + state->ReceiveBytes &&
            slot->Receiver.CapacityValue == state->ReceiveBytes && slot->Sender.CapacityValue == state->SendBytes;
    }

    /// <summary>Copied, serialized RX/TX borrower counts/bytes from this one-use
    /// owner. BackingBytes remains retained caller storage; current/peak bytes
    /// are successful window leases, not new physical pages or buffered data.
    /// Failed/disabled/inconsistent owners return false. Post-End succeeds only
    /// after actual release, empty slots/table and whole-buffer scrubbing.</summary>
    public readonly struct LeaseObservation
    {
        internal LeaseObservation(State* state)
        {
            Leases = state->Leased; PeakLeases = state->PeakLeased;
            Acquired = state->LeaseAcquired; Returned = state->LeaseReturned;
            ReceiveBytes = (ulong)Leases * state->ReceiveBytes; SendBytes = (ulong)Leases * state->SendBytes;
            PeakReceiveBytes = (ulong)PeakLeases * state->ReceiveBytes; PeakSendBytes = (ulong)PeakLeases * state->SendBytes;
            BackingBytes = (ulong)state->Count * (state->ReceiveBytes + state->SendBytes); Ended = state->Initialized == 2;
        }
        public readonly uint Leases, PeakLeases;
        public readonly ulong Acquired, Returned, ReceiveBytes, SendBytes, PeakReceiveBytes, PeakSendBytes, BackingBytes;
        public readonly bool Ended;
    }

    /// <summary>Read only under the original owner's serialization and retain
    /// all admitted storage through the call. Grants no lifetime authority.</summary>
    public static bool TryObserveLeases(State* state, out LeaseObservation observation)
    {
        observation = default;
        if (!Extent(state, (ulong)sizeof(State)) || state->Self != (ulong)state || state->MeasureLeases != 1 || state->Failed != 0 ||
            state->Initialized is not (1 or 2) || state->Count is 0 or > MaximumConnections ||
            state->ReceiveBytes is < 8 or > 32768 || state->SendBytes is < 8 or > 32768 ||
            ((state->ReceiveBytes | state->SendBytes) & 7) != 0 ||
            !Extent(state->Sockets, (ulong)sizeof(LinuxSocketServices.State)) || state->Sockets->Self != (ulong)state->Sockets ||
            !Extent(state->Slots, state->Count * (ulong)sizeof(Slot)) || !Extent(state->Table, (ulong)sizeof(TcpConnectionTable.Owner)) ||
            !Extent(state->Buffers, state->Count * (ulong)(state->ReceiveBytes + state->SendBytes)) || !LeaseCounts(state)) return false;
        if (state->Initialized == 1 ? !Current(state) : state->Sockets->Network != null || state->Table->Count != 0 ||
            state->Leased != 0 || !Zero(state->Slots, state->Count * (ulong)sizeof(Slot)) ||
            !Zero(state->Buffers, state->Count * (ulong)(state->ReceiveBytes + state->SendBytes))) return false;
        observation = new(state); return true;
    }

    private static bool LeaseCounts(State* state)
    {
        if (state->LeaseReturned > state->LeaseAcquired || state->LeaseAcquired - state->LeaseReturned != state->Leased ||
            state->Leased > state->PeakLeased || state->PeakLeased > state->Count) return false;
        uint actual = 0;
        for (uint i = 0; i < state->Count; i++)
        {
            Slot* slot = state->Slots + i;
            if (slot->LeaseGeneration == 0) { if (slot->Phase > 1) return false; continue; }
            if (slot->Phase is not (2 or 3) || slot->LeaseGeneration != slot->Handshake.Connection.Generation || !LeaseOwners(state, slot)) return false;
            actual++;
        }
        return actual == state->Leased;
    }
    private static Status TransportResult(State* state, Slot* slot, Tx.Status result)
    {
        if (Healthy(result)) return Status.Success;
        if (result == Tx.Status.Timeout && Tx.RetireTimedOut(&slot->Sender) == Tx.Status.Success) return Status.Ignored;
        return Fail(state);
    }
    private static bool Healthy(Tx.Status status) => status is Tx.Status.Success or Tx.Status.NoData or Tx.Status.NothingToSend or
        Tx.Status.WindowClosed or Tx.Status.WouldBlock or Tx.Status.AwaitingAck or Tx.Status.NotDue or Tx.Status.WaitingForDrain or
        Tx.Status.DuplicateAck or Tx.Status.WindowUpdated or Tx.Status.PeerClosed or Tx.Status.ConnectionClosed or Tx.Status.ConnectionReset;

    /// <summary>Detach only an empty owner after all pending and exact stream
    /// lifetimes have retired. Verify RX/TX scrubbing before release. The caller
    /// separately retires the empty table and stops NIC/DMA ownership; neither is
    /// implied here. A failed/faulted owner is retained, never erased.</summary>
    public static bool End(State* state)
    {
        if (!Current(state, true) || state->RetainedConnections != 0 || state->Table->Count != 0 ||
            !Zero(state->Buffers, state->Count * (ulong)(state->ReceiveBytes + state->SendBytes)) ||
            state->MeasureLeases != 0 && (!LeaseCounts(state) || state->Leased != 0)) return false;
        if (state->CompactTimeWait != 0)
        {
            if (Waits.Retire(&state->TimeWait) != Waits.Status.Success) return false;
            for (uint i = 0; i < Waits.MaximumEntries; i++) state->TimeWaitAcks[i] = 0;
        }
        if (state->Sequence != null && !TcpInitialSequence.Release(state->Sequence, state->SequenceLease)) return false;
        state->Sequence = null; state->SequenceLease = default;
        state->Sockets->Network = null; state->Initialized = 2; return true;
    }

    // Six base extents plus the optional secret are admitted before attachment.
    internal static void ExtentAt(State* state, uint index, out ulong start, out ulong bytes)
    {
        start = bytes = 0;
        if (state == null || index >= 7) return;
        if (index == 6)
        {
            if (state->Sequence != null) { start = (ulong)state->Sequence; bytes = (ulong)sizeof(TcpInitialSequence.Owner); }
            return;
        }
        ulong* starts = stackalloc ulong[6]; ulong* sizes = stackalloc ulong[6];
        Extents(state, state->Table, state->Slots, state->Count, state->Buffers, state->ReceiveBytes, state->SendBytes, starts, sizes);
        start = starts[index]; bytes = sizes[index];
    }
    private static void Extents(State* state, TcpConnectionTable.Owner* table, Slot* slots, uint count, byte* buffers,
        uint receiveBytes, uint sendBytes, ulong* starts, ulong* sizes)
    {
        starts[0] = (ulong)state; sizes[0] = (ulong)sizeof(State);
        starts[1] = (ulong)slots; sizes[1] = count * (ulong)sizeof(Slot);
        starts[2] = (ulong)buffers; sizes[2] = count * (ulong)(receiveBytes + sendBytes);
        starts[3] = (ulong)table; sizes[3] = (ulong)sizeof(TcpConnectionTable.Owner);
        starts[4] = (ulong)table->Entries; sizes[4] = (ulong)table->CapacityValue * (ulong)sizeof(TcpConnectionTableEntry);
        starts[5] = (ulong)table->Generations; sizes[5] = (ulong)table->CapacityValue * 8;
    }
    private static bool Frames(State* state, ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (output.Length < 1514) return false;
        fixed (byte* source = input) fixed (byte* target = output)
        {
            if (input.Length != 0 && (source == null || (ulong)source > ~0UL - (uint)input.Length) ||
                target == null || (ulong)target > ~0UL - (uint)output.Length ||
                Overlap((ulong)source, (uint)input.Length, (ulong)target, (uint)output.Length)) return false;
            if (input.Length != 0 && !Fds.SocketOutside(state->Sockets->Descriptors, source, (uint)input.Length) ||
                !Fds.SocketOutside(state->Sockets->Descriptors, target, (uint)output.Length)) return false;
            for (uint i = 0; i < 7; i++)
            {
                ExtentAt(state, i, out ulong start, out ulong bytes);
                if (Overlap(start, bytes, (ulong)source, (uint)input.Length) || Overlap(start, bytes, (ulong)target, (uint)output.Length)) return false;
            }
        }
        return true;
    }
    private static bool Current(State* state, bool allowFailed = false)
    {
        if (!Extent(state, (ulong)sizeof(State)) || state->Self != (ulong)state || state->Initialized != 1 || state->Failed > 1 || state->CompactTimeWait > 1 ||
            state->MeasureLeases > 1 || state->MeasureLeases == 0 && (state->Leased != 0 || state->PeakLeased != 0 || state->LeaseAcquired != 0 || state->LeaseReturned != 0) ||
            state->Failed != 0 && !allowFailed || state->Count is 0 or > MaximumConnections ||
            state->ReceiveBytes is < 8 or > 32768 || state->SendBytes is < 8 or > 32768 || ((state->ReceiveBytes | state->SendBytes) & 7) != 0 ||
            !Extent(state->Sockets, (ulong)sizeof(LinuxSocketServices.State)) || state->Sockets->Network != state ||
            !Extent(state->Table, (ulong)sizeof(TcpConnectionTable.Owner)) ||
            !Extent(state->Slots, state->Count * (ulong)sizeof(Slot)) ||
            !Extent(state->Buffers, state->Count * (ulong)(state->ReceiveBytes + state->SendBytes)) ||
            !Fds.Audit(state->Sockets->Descriptors)) return false;
        if (state->CompactTimeWait != 0 && Waits.Read(&state->TimeWait, default, out _) != Waits.Status.StaleHandle) return false;
        if (state->Sequence != null && !TcpInitialSequence.Current(state->Sequence, state->SequenceLease) ||
            state->Sequence == null && state->SequenceLease.IsValid) return false;
        uint held = 0;
        for (uint i = 0; i < state->Count; i++)
        {
            Slot* slot = state->Slots + i;
            if (state->MeasureLeases == 0 && slot->LeaseGeneration != 0) return false;
            if (slot->Phase == 0) continue;
            if (slot->Phase > 3 || TcpConnectionTable.ReadOwned(state->Table, slot->Handshake.Connection, out var entry) != TcpConnectionTable.OwnershipStatus.Success ||
                (slot->Phase == 1 ? entry.State != TcpConnectionLifecycleState.SynReceived :
                    entry.State is not (TcpConnectionLifecycleState.Established or TcpConnectionLifecycleState.Closed))) return false;
            held++;
        }
        return held == state->Table->Count && (state->MeasureLeases == 0 || LeaseCounts(state));
    }
    private static Status Fail(State* state) { state->Failed = 1; return Status.TransportFailure; }
    private static bool Same(TcpConnectionTable.Handle a, TcpConnectionTable.Handle b) =>
        a.OwnerAddress == b.OwnerAddress && a.Epoch == b.Epoch && a.Generation == b.Generation && a.Index == b.Index;
    private static bool Extent(void* pointer, ulong bytes) => pointer != null && ((ulong)pointer & 7) == 0 && bytes != 0 && bytes <= ~0UL - (ulong)pointer;
    private static bool Overlap(ulong a, ulong bytes, ulong b, ulong count) => bytes != 0 && count != 0 && a < b + count && b < a + bytes;
    private static bool Zero(void* pointer, ulong bytes) { for (ulong i = 0; i < bytes; i++) if (((byte*)pointer)[i] != 0) return false; return true; }
}
#endif
