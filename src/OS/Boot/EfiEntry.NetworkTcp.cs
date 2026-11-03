#if M07_026_PROBE
using OS.Drivers;
using OS.Kernel.Memory;
using OS.Net;
using NetDevice = OS.Drivers.ContinuingVirtioNetDevice;
using TcpTx = OS.Net.TcpSendWindow;
using TcpRx = OS.Net.TcpReceiveWindow;
using TcpPending = OS.Net.TcpPassiveConnection;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {

        private static NetworkTcpStorage s_networkTcp;

        private static bool RunNetworkTcp(Storage* boot, NetDevice.State* device, MacAddress peer,
            Span<byte> tx, Span<byte> rx)
        {
            fixed (NetworkTcpStorage* owner = &s_networkTcp)
            {
                if (sizeof(TcpConnectionTableEntry) * 2 + 16 > 256 || sizeof(TcpListenerBacklogEntry) > 128)
                    return false;
                owner->Boot = boot;
                owner->Device = device;
                owner->Peer = peer;
                var entries = (TcpConnectionTableEntry*)owner->TableBytes;
                ulong* generations = (ulong*)(entries + 2);
                if (TcpConnectionTable.InitializeOwned(&owner->Table, entries, 2, generations, 2, 226) != TcpConnectionTable.OwnershipStatus.Success)
                    return false;
                bool success = LinuxText(boot, "TEST_START m07.013\r\nTEST_START m07.014\r\nTEST_START m07.015\r\nTEST_START m07.016\r\nTEST_START m07.017\r\nTEST_START m07.018\r\n") &&
                    RunNetworkTcpCore(owner, tx, rx);
                if (!success)
                    CleanupNetworkTcp(owner);
                return success && owner->Failed == 0 && owner->Table.Retired && LinuxText(boot,
                    "CLROS_M07_013_READY\r\nTEST_PASS m07.013\r\nCLROS_M07_014_READY\r\nTEST_PASS m07.014\r\nCLROS_M07_015_READY\r\nTEST_PASS m07.015\r\nCLROS_M07_016_READY\r\nTEST_PASS m07.016\r\nCLROS_M07_017_READY\r\nTEST_PASS m07.017\r\nCLROS_M07_018_READY\r\nTEST_PASS m07.018\r\n");
            }
        }

        private static bool RunNetworkTcpCore(NetworkTcpStorage* owner, Span<byte> tx, Span<byte> rx)
        {
            var a = &owner->A;
            var b = &owner->B;
            var c = &owner->C;
            var d = &owner->D;
            var e = &owner->E;
            Span<TcpListenerBacklogEntry> backlog = new(owner->BacklogBytes, 1);
            if (!WaitNetworkTcp(owner, rx, out int length) ||
                BeginNetworkTcp(owner, a, 40000, 1000, rx.Slice(0, length), tx) != TcpPending.Status.Success ||
                !WaitNetworkTcp(owner, rx, out length, &a->Pending) ||
                TcpPending.ReceiveSyn(&a->Pending, rx.Slice(0, length), tx, out _) != TcpPending.Status.Success ||
                !WaitNetworkTcp(owner, rx, out length, &a->Pending) ||
                TcpPending.ReceiveAck(&a->Pending, rx.Slice(0, length), backlog, out _, out _) != TcpPending.Status.InvalidPacket ||
                !WaitNetworkTcp(owner, rx, out length, &a->Pending) ||
                TcpPending.ReceiveAck(&a->Pending, rx.Slice(0, length), backlog, out _, out _) != TcpPending.Status.Success ||
                a->Pending.SuccessfulAttempts != 2 || a->Pending.DuplicateReplies != 1 ||
                !WaitNetworkTcp(owner, rx, out length) ||
                BeginNetworkTcp(owner, c, 40000, 1000, rx.Slice(0, length), tx) != TcpPending.Status.TableRejected || owner->Table.Count != 1 ||
                !WriteNetworkTcpRow(owner, 0, a->Pending.SuccessfulAttempts, a->Pending.DuplicateReplies, a->Pending.PeerMaximumSegmentSize, 2))
                return false;
            if (!WaitNetworkTcp(owner, rx, out length) ||
                BeginNetworkTcp(owner, b, 40001, 2000, rx.Slice(0, length), tx) != TcpPending.Status.Success ||
                !WaitNetworkTcp(owner, rx, out length, &b->Pending) ||
                TcpPending.ReceiveAck(&b->Pending, rx.Slice(0, length), backlog, out _, out _) != TcpPending.Status.BacklogRejected ||
                !AcceptNetworkTcp(owner, a, 40000, 1000) ||
                !WaitNetworkTcp(owner, rx, out length, &b->Pending) ||
                TcpPending.ReceiveAck(&b->Pending, rx.Slice(0, length), backlog, out _, out _) != TcpPending.Status.Success ||
                !AcceptNetworkTcp(owner, b, 40001, 2000) ||
                !WaitNetworkTcp(owner, rx, out length) ||
                BeginNetworkTcp(owner, c, 40002, 3000, rx.Slice(0, length), tx) != TcpPending.Status.TableRejected ||
                owner->Table.Count != 2 || !WriteNetworkTcpRow(owner, 1, 2, 1, 1, (ulong)a->Sender.PeerAdvertisedWindow))
                return false;

            if (TcpTx.ShutdownWrite(&b->Sender) != TcpTx.Status.Success || TcpTx.PumpClose(&b->Sender, tx, out _) != TcpTx.Status.Success ||
                !ReceiveNetworkTcp(owner, b, rx, TcpTx.Status.PeerClosed) || b->Sender.CloseState != TcpConnectionCloseState.Closing ||
                TcpRx.PumpAck(&b->Receiver, tx, out _) != TcpRx.Status.Success ||
                !ReceiveNetworkTcp(owner, b, rx, TcpTx.Status.Success) || b->Sender.CloseState != TcpConnectionCloseState.TimeWait)
                return false;
            ulong firstWait = b->Sender.TimeWaitDeadlineNanoseconds;
            ulong waitStarted = NetworkTcpNow(owner);
            if (!ReceiveNetworkTcp(owner, b, rx, TcpTx.Status.PeerClosed) ||
                b->Sender.TimeWaitDeadlineNanoseconds < firstWait || TcpRx.PumpAck(&b->Receiver, tx, out _) != TcpRx.Status.Success ||
                b->Sender.TimeWaitDeadlineNanoseconds < waitStarted ||
                !WriteNetworkTcpRow(owner, 2, firstWait, b->Sender.TimeWaitDeadlineNanoseconds, waitStarted, 240000000000))
                return false;

            if (!ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.WouldBlock) || TcpRx.PumpAck(&a->Receiver, tx, out _) != TcpRx.Status.Success ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.Success) || TcpRx.PumpAck(&a->Receiver, tx, out _) != TcpRx.Status.Success ||
                !ReadNetworkTcpBytes(a, "abc") || TcpRx.PumpAck(&a->Receiver, tx, out _) != TcpRx.Status.Success ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.Success) || TcpRx.PumpAck(&a->Receiver, tx, out _) != TcpRx.Status.Success ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.Success) || TcpRx.PumpAck(&a->Receiver, tx, out _) != TcpRx.Status.Success ||
                !ReadNetworkTcpBytes(a, "defghijk") || TcpRx.PumpAck(&a->Receiver, tx, out _) != TcpRx.Status.Success ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.InvalidPacket) || TcpRx.PumpAck(&a->Receiver, tx, out _) != TcpRx.Status.Success ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.WouldBlock) || TcpRx.PumpAck(&a->Receiver, tx, out _) != TcpRx.Status.Success ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.PeerClosed) || TcpRx.PumpAck(&a->Receiver, tx, out _) != TcpRx.Status.Success ||
                a->Receiver.UnreadBytes != 4 || a->Receiver.EndOfStream || a->Sender.CloseState != TcpConnectionCloseState.CloseWait ||
                !WriteNetworkTcpRow(owner, 3, 11, 4, a->Receiver.AcknowledgementNumber, a->Receiver.AvailableWindow))
                return false;

            int queued = 0;
            if (!QueueNetworkTcpResponse(a, ref queued) || queued != 8 ||
                TcpTx.Queue(&a->Sender, tx.Slice(0, 1), out _) != TcpTx.Status.WouldBlock ||
                TcpTx.Pump(&a->Sender, tx, out _) != TcpTx.Status.Success ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.WindowUpdated) ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.DuplicateAck) ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.WindowUpdated) ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.Success, retry: true) || a->Sender.OldestUnacknowledgedSequence != 1003 ||
                !QueueNetworkTcpResponse(a, ref queued) ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.InvalidAck) ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.OldAck) ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.Success) ||
                TcpTx.Pump(&a->Sender, tx, out _) != TcpTx.Status.WindowClosed ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.WindowUpdated) || !QueueNetworkTcpResponse(a, ref queued) ||
                TcpTx.Pump(&a->Sender, tx, out _) != TcpTx.Status.Success ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.Success) ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.Success) || !QueueNetworkTcpResponse(a, ref queued))
                return false;
            while (a->Sender.OldestUnacknowledgedSequence != 1021)
            {
                if (TcpTx.Pump(&a->Sender, tx, out _) != TcpTx.Status.Success ||
                    !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.Success, retry: true) || !QueueNetworkTcpResponse(a, ref queued))
                    return false;
            }
            if (queued != 20 || a->Sender.BufferedBytes != 0 ||
                !WriteNetworkTcpRow(owner, 4, (ulong)queued, a->Sender.OldestUnacknowledgedSequence, a->Sender.DuplicateAcknowledgements, owner->CopiedFrames))
                return false;
            var oldA = a->Pending.Connection;
            if (TcpTx.ShutdownWrite(&a->Sender) != TcpTx.Status.Success || TcpTx.PumpClose(&a->Sender, tx, out _) != TcpTx.Status.Success ||
                !ReceiveNetworkTcp(owner, a, rx, TcpTx.Status.ConnectionClosed) || !ReadNetworkTcpBytes(a, "lmno") ||
                !a->Receiver.EndOfStream || TcpRx.Read(&a->Receiver, tx.Slice(0, 1), out _) != TcpRx.Status.EndOfStream || !ReapNetworkTcp(owner, a) ||
                !WriteNetworkTcpRow(owner, 5, 20, 15, (ulong)owner->Table.Count, 1))
                return false;

            if (!WaitNetworkTcp(owner, rx, out length) ||
                BeginNetworkTcp(owner, c, 40000, 0xfffffffc, rx.Slice(0, length), tx) != TcpPending.Status.Success ||
                !WaitNetworkTcp(owner, rx, out length, &c->Pending) ||
                TcpPending.ReceiveAck(&c->Pending, rx.Slice(0, length), backlog, out _, out _) != TcpPending.Status.Success ||
                !AcceptNetworkTcp(owner, c, 40000, 0xfffffffc) ||
                TcpConnectionTable.ReadOwned(&owner->Table, oldA, out _) != TcpConnectionTable.OwnershipStatus.StaleHandle ||
                !ReceiveNetworkTcp(owner, c, rx, TcpTx.Status.InvalidPacket) || c->Receiver.UnreadBytes != 0 ||
                !ReceiveNetworkTcp(owner, c, rx, TcpTx.Status.Success) || TcpRx.PumpAck(&c->Receiver, tx, out _) != TcpRx.Status.Success)
                return false;
            Span<byte> discard = stackalloc byte[8];
            const string discarded = "discard!";
            for (int i = 0; i < 8; i++)
                discard[i] = (byte)discarded[i];
            if (TcpTx.Queue(&c->Sender, discard, out int accepted) != TcpTx.Status.Success || accepted != 8 ||
                TcpTx.Pump(&c->Sender, tx, out _) != TcpTx.Status.Success ||
                !ReceiveNetworkTcp(owner, c, rx, TcpTx.Status.InvalidPacket) ||
                !ReceiveNetworkTcp(owner, c, rx, TcpTx.Status.InvalidPacket) || c->Receiver.HasPendingAck ||
                !ReceiveNetworkTcp(owner, c, rx, TcpTx.Status.InvalidPacket) || !c->Receiver.HasPendingAck ||
                TcpRx.PumpAck(&c->Receiver, tx, out _) != TcpRx.Status.Success ||
                !ReceiveNetworkTcp(owner, c, rx, TcpTx.Status.ConnectionReset) ||
                TcpRx.Read(&c->Receiver, discard, out _) != TcpRx.Status.TableRejected ||
                c->Receiver.UnreadBytes != 4 || c->Sender.BufferedBytes != 8 ||
                c->Receiver.AcknowledgementNumber != 2 || c->Sender.NextSequence != 1 ||
                !ReapNetworkTcp(owner, c) || !WriteNetworkTcpRow(owner, 6, 2, 3, 4, 8))
                return false;

            if (!WaitNetworkTcp(owner, rx, out length) ||
                BeginNetworkTcp(owner, d, 40003, 4000, rx.Slice(0, length), tx) != TcpPending.Status.Success ||
                !WaitNetworkTcp(owner, rx, out length, &d->Pending) ||
                TcpPending.ReceiveAck(&d->Pending, rx.Slice(0, length), backlog, out _, out _) != TcpPending.Status.Success ||
                !AcceptNetworkTcp(owner, d, 40003, 4000))
                return false;
            discard[0] = (byte)'l';
            discard[1] = (byte)'o';
            discard[2] = (byte)'s';
            discard[3] = (byte)'t';
            ulong timeoutStart = NetworkTcpNow(owner);
            if (TcpTx.Queue(&d->Sender, discard.Slice(0, 4), out accepted) != TcpTx.Status.Success || accepted != 4 ||
                TcpTx.Pump(&d->Sender, tx, out _) != TcpTx.Status.Success)
                return false;
            TcpTx.Status timed = TcpTx.Status.NotDue;
            for (uint poll = 0; poll < 1000000000; poll++)
            {
                if (!PollNetworkTcp(owner) || NetDevice.Receive(owner->Device, rx, out _) != NetDevice.Status.Empty)
                    return false;
                timed = TcpTx.PumpRetransmission(&d->Sender, tx, out _);
                if (timed == TcpTx.Status.Timeout)
                    break;
                if (timed is not (TcpTx.Status.Success or TcpTx.Status.NotDue))
                    return false;
            }
            ulong timeoutEnd = NetworkTcpNow(owner);
            if (timed != TcpTx.Status.Timeout || d->Sender.Retransmissions != 2 || d->Sender.BufferedBytes != 4 ||
                timeoutEnd < timeoutStart || timeoutEnd - timeoutStart < 7000000000 ||
                TcpTx.AbortConnection(&d->Sender) != TcpTx.Status.Success || !ReapNetworkTcp(owner, d) ||
                !WriteNetworkTcpRow(owner, 7, 2, timeoutStart, timeoutEnd, 1))
                return false;

            if (!WaitNetworkTcp(owner, rx, out length) ||
                BeginNetworkTcp(owner, e, 40004, 5000, rx.Slice(0, length), tx) != TcpPending.Status.Success)
                return false;
            var expiredHandle = e->Pending.Connection;
            ulong pendingDeadline = e->Pending.DeadlineNanoseconds;
            TcpPending.Status expired = TcpPending.Status.NotDue;
            for (uint poll = 0; poll < 1000000000; poll++)
            {
                if (!PollNetworkTcp(owner) || NetDevice.Receive(owner->Device, rx, out _) != NetDevice.Status.Empty)
                    return false;
                expired = TcpPending.Pump(&e->Pending, tx, out _);
                if (expired == TcpPending.Status.Timeout)
                    break;
                if (expired is not (TcpPending.Status.Success or TcpPending.Status.NotDue or TcpPending.Status.RetryLimit))
                    return false;
            }
            ulong pendingEnded = NetworkTcpNow(owner);
            if (expired != TcpPending.Status.Timeout || !e->Pending.Retired || e->Pending.SuccessfulAttempts != 3 || pendingEnded < pendingDeadline ||
                TcpConnectionTable.ReadOwned(&owner->Table, expiredHandle, out _) != TcpConnectionTable.OwnershipStatus.StaleHandle ||
                owner->Table.Count != 1 || !WriteNetworkTcpRow(owner, 8, 3, pendingDeadline, pendingEnded, 1))
                return false;

            ulong deadline = b->Sender.TimeWaitDeadlineNanoseconds;
            TcpTx.Status closed = TcpTx.Status.NotDue;
            for (uint poll = 0; poll < 1000000000; poll++)
            {
                if (!PollNetworkTcp(owner, true) || NetDevice.Receive(owner->Device, rx, out _) != NetDevice.Status.Empty)
                    return false;
                closed = TcpTx.PumpClose(&b->Sender, tx, out _);
                if (closed == TcpTx.Status.Success)
                    break;
                if (closed != TcpTx.Status.NotDue || owner->Table.Count != 1 || b->Sender.CloseState != TcpConnectionCloseState.TimeWait)
                    return false;
            }
            ulong completed = NetworkTcpNow(owner);
            if (closed != TcpTx.Status.Success || completed < deadline || !ReapNetworkTcp(owner, b) || owner->Table.Count != 0 ||
                TcpConnectionTable.RetireOwned(&owner->Table) != TcpConnectionTable.OwnershipStatus.Success ||
                !WriteNetworkTcpRow(owner, 9, deadline, completed, (ulong)owner->Table.Count, 5))
                return false;

            const string finalTag = "CLROS TCP closed";
            Span<byte> tag = stackalloc byte[finalTag.Length];
            for (int i = 0; i < tag.Length; i++)
                tag[i] = (byte)finalTag[i];
            var info = new Ipv4IcmpEchoFrameInfo(owner->Peer, new MacAddress(2, 0, 0, 0, 0, 1), new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2));
            tx.Clear();
            if (Ipv4IcmpEchoFrame.TryWriteEchoRequest(tx, info, 227, 0, tag, out int written) != PacketParseStatus.Success ||
                NetDevice.Send(owner->Device, tx.Slice(0, written < 60 ? 60 : written)) != NetDevice.Status.Success ||
                !WaitNetworkTcp(owner, rx, out length) ||
                Ipv4IcmpEchoFrame.TryParse(rx.Slice(0, length), out var replyInfo, out var echo, out var bytes) != PacketParseStatus.Success ||
                !echo.IsEchoReply || echo.Identifier != 227 || echo.SequenceNumber != 0 || bytes.Length != tag.Length ||
                replyInfo.SourceAddress != info.DestinationAddress || replyInfo.DestinationAddress != info.SourceAddress ||
                replyInfo.SourceMac != owner->Peer || replyInfo.DestinationMac != info.SourceMac)
                return false;
            for (int i = 0; i < tag.Length; i++)
                if (bytes[i] != tag[i])
                    return false;
            ulong drainStarted = NetworkTcpNow(owner);
            for (uint poll = 0; poll < 1000000 && owner->Device->ReclaimedTransmits != 33 + owner->CopiedFrames; poll++)
                if (!PollNetworkTcp(owner) || NetworkTcpNow(owner) - drainStarted >= 2000000000)
                    return false;
            if (owner->Device->ReclaimedTransmits != 33 + owner->CopiedFrames || owner->Device->ReceivedFrames != 32 + owner->IncomingFrames)
                return false;
            return owner->Failed == 0 && WriteNetworkTcpRow(owner, 10, owner->Controls, owner->CopiedFrames, owner->Device->ReceivedFrames, owner->Device->ReclaimedTransmits);
        }

        private static TcpPending.Status BeginNetworkTcp(NetworkTcpStorage* owner, NetworkTcpSlot* slot,
            ushort port, uint sequence, ReadOnlySpan<byte> syn, Span<byte> tx)
        {
            var info = new TcpPassiveIpv4FramePlannerInfo(new MacAddress(2, 0, 0, 0, 0, 1), owner->Peer,
                new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2), 8080, port, sequence, 8, maximumSegmentSize: 4);
            var io = new TcpPending.Operations
            {
                Context = owner,
                NowNanoseconds = &NetworkTcpNow,
                Send = &SendNetworkTcp,
                LifetimeNanoseconds = 10000000000,
                RetryNanoseconds = 1000000000,
                MaximumAttempts = 3
            };
            return TcpPending.Begin(&slot->Pending, &owner->Table, info, syn, tx, io, out _);
        }

        private static bool AcceptNetworkTcp(NetworkTcpStorage* owner, NetworkTcpSlot* slot, ushort port, uint sequence)
        {
            var accepted = TcpListenerBacklog.Accept(new Span<TcpListenerBacklogEntry>(owner->BacklogBytes, 1));
            var info = new TcpEstablishedStreamIpv4FramePlannerInfo(new MacAddress(2, 0, 0, 0, 0, 1), owner->Peer,
                new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2), 4, 8);
            return accepted.Succeeded && accepted.Connection.RemotePort == port && accepted.Connection.LocalNextSequenceNumber == sequence + 1 &&
                TcpRx.Initialize(&slot->Receiver, &owner->Table, slot->Pending.Connection, slot->ReceiveBytes, 8, info, owner, &SendNetworkTcp) == TcpRx.Status.Success &&
                TcpTx.Initialize(&slot->Sender, &slot->Receiver, &slot->Pending, slot->SendBytes, 8, 1500) == TcpTx.Status.Success &&
                TcpTx.ConfigureRetransmission(&slot->Sender, owner, &NetworkTcpNow, 30000000000, maximumRetransmissions: 2) == TcpTx.Status.Success &&
                TcpTx.ConfigureClose(&slot->Sender, 20000000000) == TcpTx.Status.Success && owner->Failed == 0;
        }

        private static bool ReceiveNetworkTcp(NetworkTcpStorage* owner, NetworkTcpSlot* slot, Span<byte> rx,
            TcpTx.Status expected, bool retry = false)
        {
            if (!WaitNetworkTcp(owner, rx, out int length, null, retry ? &slot->Sender : null))
                return false;
            var result = TcpTx.ReceiveFrame(&slot->Sender, rx.Slice(0, length), out _);
            if (result != expected || owner->Failed != 0)
                return false;
            owner->Controls++;
            return true;
        }

        private static bool WaitNetworkTcp(NetworkTcpStorage* owner, Span<byte> rx, out int length,
            TcpPending.State* pending = null, TcpTx.Owner* sender = null)
        {
            length = 0;
            ulong started = NetworkTcpNow(owner);
            if (owner->Failed != 0 || started > ~0UL - 8000000000)
                return false;
            Span<byte> output = stackalloc byte[1514];
            for (uint poll = 0; poll < 1000000000; poll++)
            {
                if (!PollNetworkTcp(owner))
                    return false;
                var received = NetDevice.Receive(owner->Device, rx, out uint count);
                if (received == NetDevice.Status.Success)
                {
                    owner->IncomingFrames++;
                    length = (int)count;
                    return true;
                }
                if (received != NetDevice.Status.Empty)
                    return false;
                if (pending != null && pending->Pending)
                {
                    var status = TcpPending.Pump(pending, output, out _);
                    if (status is not (TcpPending.Status.Success or TcpPending.Status.NotDue))
                        return false;
                }
                if (sender != null && sender->InFlightBytes != 0)
                {
                    var status = TcpTx.PumpRetransmission(sender, output, out _);
                    if (status is not (TcpTx.Status.Success or TcpTx.Status.NotDue))
                        return false;
                }
                ulong now = NetworkTcpNow(owner);
                if (owner->Failed != 0 || now < started || now - started >= 8000000000)
                    return false;
            }
            return false;
        }

        private static bool PollNetworkTcp(NetworkTcpStorage* owner, bool timeWait = false) =>
            owner->Failed == 0 && NetworkSchedulerPoll(owner->Boot, timeWait) && NetDevice.Poll(owner->Device, 8, out _) == NetDevice.Status.Success;

        private static ulong NetworkTcpNow(void* context)
        {
            var owner = (NetworkTcpStorage*)context;
            if (!ReadNetworkTime(owner->Boot, out ulong now))
            {
                owner->Failed = 1;
                return 0;
            }
            return now;
        }

        private static byte SendNetworkTcp(void* context, byte* frame, uint length)
        {
            var owner = (NetworkTcpStorage*)context;
            var sent = NetDevice.Send(owner->Device, new ReadOnlySpan<byte>(frame, (int)length));
            if (sent == NetDevice.Status.Success)
            {
                owner->CopiedFrames++;
                return 1;
            }
            if (sent == NetDevice.Status.Full)
                return 0;
            owner->Failed = 1;
            return 2;
        }

        private static bool ReadNetworkTcpBytes(NetworkTcpSlot* slot, string expected)
        {
            Span<byte> output = stackalloc byte[8];
            if (TcpRx.Read(&slot->Receiver, output.Slice(0, expected.Length), out var read) != TcpRx.Status.Success || read.BytesRead != expected.Length)
                return false;
            for (int i = 0; i < expected.Length; i++)
                if (output[i] != (byte)expected[i])
                    return false;
            return true;
        }

        private static bool QueueNetworkTcpResponse(NetworkTcpSlot* slot, ref int queued)
        {
            const string text = "ABCDEFGHIJKLMNOPQRST";
            if (queued == text.Length)
                return true;
            Span<byte> bytes = stackalloc byte[text.Length];
            for (int i = 0; i < text.Length; i++)
                bytes[i] = (byte)text[i];
            var result = TcpTx.Queue(&slot->Sender, bytes.Slice(queued), out int accepted);
            queued += accepted;
            return result is TcpTx.Status.Success or TcpTx.Status.WouldBlock;
        }

        private static bool ReapNetworkTcp(NetworkTcpStorage* owner, NetworkTcpSlot* slot)
        {
            var handle = slot->Pending.Connection;
            if (TcpTx.ReleaseClosed(&slot->Sender) != TcpTx.Status.Success || TcpRx.ReleaseClosed(&slot->Receiver) != TcpRx.Status.Success ||
                TcpConnectionTable.ReleaseOwned(&owner->Table, handle) != TcpConnectionTable.OwnershipStatus.Success ||
                TcpConnectionTable.ReleaseOwned(&owner->Table, handle) != TcpConnectionTable.OwnershipStatus.StaleHandle)
                return false;
            for (int i = 0; i < 8; i++)
                if (slot->SendBytes[i] != 0 || slot->ReceiveBytes[i] != 0)
                    return false;
            return slot->Sender.Retired && slot->Receiver.Retired;
        }

        private static void CleanupNetworkTcp(NetworkTcpStorage* owner)
        {
            for (int i = 0; i < 5; i++)
            {
                var slot = i == 0 ? &owner->A : i == 1 ? &owner->B : i == 2 ? &owner->C : i == 3 ? &owner->D : &owner->E;
                if (slot->Pending.Pending || slot->Pending.RequiresCleanup)
                    _ = TcpPending.CancelPending(&slot->Pending);
                else if (slot->Sender.Active || slot->Sender.RequiresCleanup)
                {
                    _ = TcpTx.AbortConnection(&slot->Sender);
                    _ = ReapNetworkTcp(owner, slot);
                }
                else if (slot->Pending.Established && !slot->Sender.Retired &&
                    TcpConnectionTable.ReadOwned(&owner->Table, slot->Pending.Connection, out var entry) == TcpConnectionTable.OwnershipStatus.Success)
                {
                    _ = TcpConnectionTable.TransitionOwned(&owner->Table, slot->Pending.Connection, entry.State, TcpConnectionLifecycleState.Closed);
                    if (slot->Receiver.Active)
                        _ = TcpRx.ReleaseClosed(&slot->Receiver);
                    _ = TcpConnectionTable.ReleaseOwned(&owner->Table, slot->Pending.Connection);
                }
            }
            if (owner->Table.Count == 0)
                _ = TcpConnectionTable.RetireOwned(&owner->Table);
        }

        private static bool WriteNetworkTcpRow(NetworkTcpStorage* owner, uint stage, ulong a, ulong b, ulong c, ulong d) =>
            LinuxText(owner->Boot, "M07_026_TCP stage=") && LinuxNumber(owner->Boot, stage) &&
            LinuxText(owner->Boot, " a=") && LinuxNumber(owner->Boot, a) && LinuxText(owner->Boot, " b=") && LinuxNumber(owner->Boot, b) &&
            LinuxText(owner->Boot, " c=") && LinuxNumber(owner->Boot, c) && LinuxText(owner->Boot, " d=") && LinuxNumber(owner->Boot, d) && LinuxText(owner->Boot, "\r\n");
    }
}
#endif
