using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Net;
using Rx = OS.Net.TcpReceiveWindow;
using Tx = OS.Net.TcpSendWindow;
using Tcb = OS.Net.TcpConnectionTable;
using Passive = OS.Net.TcpPassiveConnection;
using Wire = TcpReceiveWindowTests;

internal static unsafe partial class TcpSendWindowTests
{
    private const ulong Second = 1_000_000_000;
    [HostTest("m07-029 lost reopening update recovers queued exact bytes without an outstanding segment", HostLane.Fast, 14)]
    private static void PersistRecovery()
    {
        using var f = new Fixture(window: 0);
        byte[] output = new byte[1514];
        ConfigurePersist(f);
        Tx.Queue(f.Owner, "abc"u8, out _);
        HostCheck.Equal(Tx.Status.WindowClosed, Tx.Pump(f.Owner, output, out _));
        HostCheck.Equal(0, f.Owner->InFlightBytes);
        HostCheck.Equal(Tx.Status.NotDue, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal(Second + 100, f.Owner->PersistDeadlineNanoseconds);
        f.Hardware->Now = f.Owner->PersistDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out var packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 700, 100, 32, "a"u8);
        HostCheck.Equal(701U, f.Owner->NextSequence);
        HostCheck.Equal(1, f.Owner->InFlightBytes);
        HostCheck.Equal(3, f.Owner->BufferedBytes);
        // The receiver reopens, but its unsolicited update is lost. It returns
        // a fresh window only when the next real copied probe reaches it.
        f.Hardware->Now = f.Owner->PersistDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 700, 100, 32, "a"u8);
        HostCheck.Equal(Tx.Status.WindowUpdated, f.Ack(700, out _, window: 32));
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 700, 100, 32, "a"u8);
        HostCheck.Equal(0UL, f.Owner->PersistDeadlineNanoseconds);
        HostCheck.Equal(Tx.Status.Success, f.Ack(701, out int released));
        HostCheck.Equal(1, released);
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 701, 100, 32, "bc"u8);
        HostCheck.Equal(Tx.Status.Success, f.Ack(703, out released));
        HostCheck.Equal(2, released);
        HostCheck.Equal(0, f.Owner->BufferedBytes);
    }

    [HostTest("m07-029 persist duplicate ACKs retain deadlines and prolonged zero window stays live", HostLane.Fast, 15)]
    private static void PersistBackoff()
    {
        using var f = new Fixture(window: 0);
        byte[] output = new byte[1514];
        ConfigurePersist(f);
        Tx.Queue(f.Owner, "abc"u8, out _);
        HostCheck.Equal(Tx.Status.NotDue, Tx.PumpRetransmission(f.Owner, output, out _));
        ulong interval = Second;
        for (int i = 0; i < 20; i++)
        {
            ulong due = f.Owner->PersistDeadlineNanoseconds;
            f.Hardware->Now = due - 1;
            for (int j = 0; j < 3; j++)
                HostCheck.Equal(Tx.Status.DuplicateAck, f.Ack(700, out _, window: 0));
            HostCheck.Equal(due, f.Owner->PersistDeadlineNanoseconds);
            uint calls = f.Hardware->Calls;
            HostCheck.Equal(Tx.Status.NotDue, Tx.PumpRetransmission(f.Owner, output, out _));
            HostCheck.Equal(calls, f.Hardware->Calls);
            f.Hardware->Now = due;
            HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out var packet));
            CheckData(output.AsSpan(0, packet.BytesWritten), 700, 100, 32, "a"u8);
            interval = Math.Min(60 * Second, interval * 2);
            HostCheck.Equal(due + interval, f.Owner->PersistDeadlineNanoseconds);
            HostCheck.Equal((byte)0, f.Owner->Retransmissions);
        }

        HostCheck.True(f.Hardware->Now > 300 * Second && f.Owner->Active && !f.Owner->TimedOut);
        HostCheck.Equal(20UL, f.Owner->PersistProbes);
        HostCheck.Equal(701U, f.Owner->NextSequence);
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(f.Owner, 60 * Second));
        HostCheck.Equal(Tx.Status.ConnectionReset, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 701, flags: 0x14), out _));
        HostCheck.Equal(0UL, f.Owner->PersistDeadlineNanoseconds);
        uint resetCalls = f.Hardware->Calls;
        HostCheck.Equal(Tx.Status.InvalidState, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal(resetCalls, f.Hardware->Calls);
        HostCheck.Equal(Tx.Status.Success, Tx.ReleaseClosed(f.Owner));
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 32).IndexOfAnyExcept((byte)0) < 0);
    }

    [HostTest("m07-029 one-byte persist sequence wrap and zero-window ACK own exact retained ranges", HostLane.Fast, 16)]
    private static void PersistWrap()
    {
        using var f = new Fixture(window: 0, iss: uint.MaxValue - 1);
        byte[] output = new byte[1514];
        ConfigurePersist(f);
        Tx.Queue(f.Owner, "ab"u8, out _);
        Tx.PumpRetransmission(f.Owner, output, out _);
        f.Hardware->Now = f.Owner->PersistDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out var packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), uint.MaxValue, 100, 32, "a"u8);
        HostCheck.Equal(0U, f.Owner->NextSequence);
        HostCheck.Equal(Tx.Status.InvalidAck, f.Ack(1, out _, window: 0));
        HostCheck.Equal(Tx.Status.Success, f.Ack(0, out int released, window: 0));
        HostCheck.Equal(1, released);
        f.Hardware->Now = f.Owner->PersistDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 0, 100, 32, "b"u8);
        HostCheck.Equal(Tx.Status.Success, f.Ack(1, out released, window: 0));
        HostCheck.Equal(1, released);
        HostCheck.Equal(Tx.Status.NothingToSend, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal(0UL, f.Owner->PersistDeadlineNanoseconds);
    }

    [HostTest("m07-029 persist short full fatal and aliased TX preserve exact copy ownership", HostLane.Fast, 17)]
    private static void PersistPressure()
    {
        using var f = new Fixture(window: 0);
        byte[] output = new byte[1514];
        ConfigurePersist(f);
        Tx.Queue(f.Owner, "ab"u8, out _);
        Tx.PumpRetransmission(f.Owner, output, out _);
        f.Hardware->Now = f.Owner->PersistDeadlineNanoseconds;
        ulong due = f.Owner->PersistDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.OutputTooSmall, Tx.PumpRetransmission(f.Owner, output.AsSpan(0, 54), out _));
        HostCheck.Equal(Tx.Status.InvalidStorage, Tx.PumpRetransmission(f.Owner, new Span<byte>(f.Buffer, 55), out _));
        f.Hardware->Result = 0;
        HostCheck.Equal(Tx.Status.WouldBlock, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal(due, f.Owner->PersistDeadlineNanoseconds);
        HostCheck.Equal(700U, f.Owner->NextSequence);
        HostCheck.Equal(0, f.Owner->InFlightBytes);
        HostCheck.Equal(0UL, f.Owner->PersistProbes);
        f.Hardware->Result = 2;
        HostCheck.Equal(Tx.Status.SendFailed, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.True(f.Owner->RequiresCleanup && !f.Owner->TimedOut);
        HostCheck.Equal(700U, f.Owner->NextSequence);
        HostCheck.Equal(2, f.Owner->BufferedBytes);
    }

    [HostTest("m07-029 zero-window FIN wait probes without consuming sequence and explicit close budget still applies", HostLane.Fast, 18)]
    private static void PersistClose()
    {
        using var f = new Fixture(window: 0);
        byte[] output = new byte[1514];
        ConfigurePersist(f);
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(f.Owner, 5 * Second));
        HostCheck.Equal(Tx.Status.Success, Tx.ShutdownWrite(f.Owner));
        HostCheck.Equal(Tx.Status.WindowClosed, Tx.PumpClose(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.NotDue, Tx.PumpRetransmission(f.Owner, output, out _));
        f.Hardware->Now = f.Owner->PersistDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out var packet));
        CheckChecksums(output.AsSpan(0, packet.BytesWritten));
        HostCheck.Equal(54, packet.BytesWritten);
        HostCheck.Equal(699U, BinaryPrimitives.ReadUInt32BigEndian(output.AsSpan(38, 4)));
        HostCheck.Equal((byte)0x10, output[47]);
        HostCheck.Equal(700U, f.Owner->NextSequence);
        f.Hardware->Now = 5 * Second + 100;
        HostCheck.Equal(Tx.Status.Timeout, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.RetireTimedOut(f.Owner));
        HostCheck.Equal(0UL, f.Owner->PersistDeadlineNanoseconds);
    }

    private static void ConfigurePersist(Fixture f)
    {
        var io = f.Operations();
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 0, continuing: true));
    }

    [HostTest("m07-029 persist clock headroom regression and cancellation retain copied-byte ownership", HostLane.Fast, 19)]
    private static void PersistClockAndCancel()
    {
        for (int mode = 0; mode < 3; mode++)
        {
            using var f = new Fixture(window: 0);
            byte[] output = new byte[1514];
            ConfigurePersist(f);
            Tx.Queue(f.Owner, "ab"u8, out _);
            Tx.PumpRetransmission(f.Owner, output, out _);
            f.Hardware->Now = f.Owner->PersistDeadlineNanoseconds;
            if (mode == 0)
                f.Hardware->AdvanceOnSend = ulong.MaxValue - f.Hardware->Now - 30 * Second;
            HostCheck.Equal(mode == 0 ? Tx.Status.InvalidClock : Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out _));
            HostCheck.Equal(701U, f.Owner->NextSequence);
            HostCheck.Equal(1, f.Owner->InFlightBytes);
            if (mode == 1)
            {
                f.Hardware->Now = 100;
                HostCheck.Equal(Tx.Status.InvalidClock, Tx.PumpRetransmission(f.Owner, output, out _));
            }

            uint calls = f.Hardware->Calls;
            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
            HostCheck.Equal(0UL, f.Owner->PersistDeadlineNanoseconds);
            HostCheck.Equal(Tx.Status.InvalidState, Tx.PumpRetransmission(f.Owner, output, out _));
            HostCheck.Equal(calls, f.Hardware->Calls);
            HostCheck.Equal(Tx.Status.Success, Tx.ReleaseClosed(f.Owner));
            HostCheck.Equal(0UL, f.Owner->PersistProbes);
            HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 32).IndexOfAnyExcept((byte)0) < 0);
        }
    }

    [HostTest("m07-027 continuing timer admission and copied TX reject unrepresentable deadlines", HostLane.Fast, 13)]
    private static void ContinuingTimerBounds()
    {
        using var f = new Fixture();
        var io = f.Operations();
        HostCheck.Equal(Tx.Status.InvalidOptions, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 0));
        HostCheck.Equal(Tx.Status.InvalidOptions, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 100_000_000_000, continuing: true));
        f.Hardware->Now = ulong.MaxValue;
        HostCheck.Equal(Tx.Status.InvalidClock, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 0, continuing: true));
        HostCheck.True(f.Owner->Active && !f.Owner->TimedOut);
        f.Hardware->Now = ulong.MaxValue - 61_000_000_000;
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 0, continuing: true));
        HostCheck.Equal(Tx.Status.InvalidState, Tx.RetireTimedOut(f.Owner));
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "sent"u8, out int accepted));
        HostCheck.Equal(4, accepted);
        f.Hardware->AdvanceOnSend = 2_000_000_000;
        HostCheck.Equal(Tx.Status.InvalidClock, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(704U, f.Owner->NextSequence);
        HostCheck.Equal(4, f.Owner->InFlightBytes);
        HostCheck.True(f.Owner->RequiresCleanup && !f.Owner->TimedOut);
        HostCheck.Equal(Tx.Status.InvalidState, Tx.RetireTimedOut(f.Owner));
        f.Close();
        HostCheck.Equal(Tx.Status.Success, Tx.ReleaseClosed(f.Owner));
        HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(f.Receiver));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(f.Table, f.Handle));
    }

    [HostTest("net.tcp.send.owner copies bounded short writes and retains bytes until ACK", HostLane.Fast, 0)]
    private static void QueueAndRetain()
    {
        using var f = new Fixture(capacity: 8, window: 8);
        byte[] output = new byte[1514];
        byte[] input = Encoding.ASCII.GetBytes("abcdefghij");
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, input, out int accepted));
        HostCheck.Equal(8, accepted);
        input.AsSpan().Fill(0xa5);
        HostCheck.Equal(700U, f.Owner->NextSequence);
        HostCheck.Equal(Tx.Status.WouldBlock, Tx.Queue(f.Owner, "x"u8, out accepted));
        HostCheck.Equal(0, accepted);
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out var packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 700, 100, 32, "abcd"u8);
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 704, 100, 32, "efgh"u8);
        HostCheck.Equal(8, f.Owner->BufferedBytes);
        HostCheck.Equal(8, f.Owner->InFlightBytes);
        HostCheck.Equal(Tx.Status.NothingToSend, Tx.Pump(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(704, out int released));
        HostCheck.Equal(4, released);
        HostCheck.Equal(4, f.Owner->BufferedBytes);
        HostCheck.Equal(704U, f.Owner->OldestUnacknowledgedSequence);
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 4).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.Equal(Tx.Status.Success, f.Ack(708, out released));
        HostCheck.Equal(4, released);
        HostCheck.Equal(0, f.Owner->BufferedBytes);
        HostCheck.Equal(8, f.Owner->AvailableBytes);
    }

    [HostTest("net.tcp.send.owner TX pressure and short output never fabricate sequence progress", HostLane.Fast, 1)]
    private static void TxPressure()
    {
        using var f = new Fixture();
        byte[] output = new byte[1514];
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcdef"u8, out _));
        f.Hardware->Result = 0;
        HostCheck.Equal(Tx.Status.WouldBlock, Tx.Pump(f.Owner, output, out var serialized));
        HostCheck.True(serialized.Succeeded);
        HostCheck.Equal(700U, f.Owner->NextSequence);
        HostCheck.Equal(0, f.Owner->InFlightBytes);
        HostCheck.Equal(Tx.Status.OutputTooSmall, Tx.Pump(f.Owner, output.AsSpan(0, 54), out _));
        f.Hardware->Result = 1;
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output.AsSpan(0, 55), out var packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 700, 100, 32, "a"u8);
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 701, 100, 32, "bcde"u8);
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 705, 100, 32, "f"u8);
        HostCheck.Equal(706U, f.Owner->NextSequence);
        HostCheck.Equal(700U, f.Owner->OldestUnacknowledgedSequence);
    }

    [HostTest("net.tcp.send.owner segmentation obeys negotiated MSS MTU and absent IPv4 default", HostLane.Fast, 2)]
    private static void Limits()
    {
        byte[] data = Enumerable.Range(0, 600).Select(i => (byte)i).ToArray(), output = new byte[1514];
        using (var f = new Fixture(capacity: 1024, mss: 100, window: 1024, mtu: 68))
        {
            Tx.Queue(f.Owner, data, out _);
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out var packet));
            HostCheck.Equal(28, packet.PayloadBytes);
            HostCheck.Equal((ushort)68, packet.Ipv4Header.TotalLength);
            CheckData(output.AsSpan(0, packet.BytesWritten), 700, 100, 32, data.AsSpan(0, 28));
        }

        using (var f = new Fixture(capacity: 1024, mss: 17, window: 1024))
        {
            Tx.Queue(f.Owner, data, out _);
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out var packet));
            HostCheck.Equal(17, packet.PayloadBytes);
        }

        using (var f = new Fixture(capacity: 1024, mss: 0, window: 1024))
        {
            HostCheck.Equal((ushort)536, f.Owner->PeerMaximumSegmentSize);
            Tx.Queue(f.Owner, data, out _);
            Tx.Pump(f.Owner, output, out var packet);
            HostCheck.Equal(536, packet.PayloadBytes);
        }

        using (var f = new Fixture(capacity: 2048, mss: ushort.MaxValue, window: 4096))
        {
            byte[] large = new byte[1600];
            Tx.Queue(f.Owner, large, out _);
            Tx.Pump(f.Owner, output, out var packet);
            HostCheck.Equal(1460, packet.PayloadBytes);
            HostCheck.Equal(1514, packet.BytesWritten);
        }
    }

    [HostTest("net.tcp.send.owner partial duplicate old and future ACK retain correct byte ranges", HostLane.Fast, 3)]
    private static void PartialAcks()
    {
        using var f = new Fixture(capacity: 8);
        byte[] output = new byte[1514];
        Tx.Queue(f.Owner, "abcdefgh"u8, out _);
        Tx.Pump(f.Owner, output, out _);
        Tx.Pump(f.Owner, output, out _);
        HostCheck.Equal(Tx.Status.Success, f.Ack(703, out int released));
        HostCheck.Equal(3, released);
        HostCheck.Equal(Tx.Status.DuplicateAck, f.Ack(703, out released));
        HostCheck.Equal(0, released);
        HostCheck.Equal(Tx.Status.OldAck, f.Ack(702, out _));
        HostCheck.Equal(Tx.Status.InvalidAck, f.Ack(709, out _));
        HostCheck.True(f.Receiver->HasPendingAck);
        HostCheck.Equal(5, f.Owner->BufferedBytes);
        HostCheck.Equal(5, f.Owner->InFlightBytes);
        HostCheck.Equal(Tx.Status.Success, f.Ack(705, out released));
        HostCheck.Equal(2, released);
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "ijklmnop"u8, out int accepted));
        HostCheck.Equal(5, accepted);
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out var packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 708, 100, 32, "ijkl"u8);
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 712, 100, 32, "m"u8);
        HostCheck.Equal(Tx.Status.Success, f.Ack(713, out released));
        HostCheck.Equal(8, released);
        HostCheck.Equal(0, f.Owner->BufferedBytes);
    }

    [HostTest("net.tcp.send.owner zero window reopening and stale ordering preserve advertised right edge", HostLane.Fast, 4)]
    private static void Windows()
    {
        using var f = new Fixture(window: 0);
        byte[] output = new byte[1514];
        Tx.Queue(f.Owner, "abcdefgh"u8, out _);
        HostCheck.Equal(Tx.Status.WindowClosed, Tx.Pump(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.WindowUpdated, f.Ack(700, out _, window: 4));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.WindowClosed, Tx.Pump(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.WindowUpdated, f.Ack(700, out _, window: 0));
        HostCheck.Equal(Tx.Status.WindowClosed, Tx.Pump(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.WindowUpdated, f.Ack(700, out _, window: 8));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(704, out _, window: 4, sequence: 101));
        HostCheck.Equal(Tx.Status.Success, f.Ack(708, out _, window: 100, sequence: 100));
        HostCheck.Equal((ushort)4, f.Owner->PeerAdvertisedWindow);
        Tx.Queue(f.Owner, "ijkl"u8, out _);
        HostCheck.Equal(Tx.Status.WindowClosed, Tx.Pump(f.Owner, output, out _)); // Stale ACK cannot grow the absolute edge.
        HostCheck.Equal(Tx.Status.WindowUpdated, f.Ack(708, out _, window: 4, sequence: 101));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out var packet));
        CheckData(output.AsSpan(0, packet.BytesWritten), 708, 100, 32, "ijkl"u8);
    }

    [HostTest("net.tcp.send.owner partial ACK and retained bytes cross uint sequence wrap", HostLane.Fast, 5)]
    private static void Wrap()
    {
        using var f = new Fixture(iss: uint.MaxValue - 2);
        byte[] output = new byte[1514];
        Tx.Queue(f.Owner, "abcdef"u8, out _);
        Tx.Pump(f.Owner, output, out _);
        Tx.Pump(f.Owner, output, out _);
        HostCheck.Equal(4U, f.Owner->NextSequence);
        HostCheck.Equal(Tx.Status.Success, f.Ack(1, out int released));
        HostCheck.Equal(3, released);
        HostCheck.Equal(3, f.Owner->BufferedBytes);
        HostCheck.Equal(3, f.Owner->InFlightBytes);
        HostCheck.Equal(Tx.Status.DuplicateAck, f.Ack(1, out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(4, out released));
        HostCheck.Equal(3, released);
        HostCheck.Equal(4U, f.Owner->OldestUnacknowledgedSequence);
    }

    [HostTest("net.tcp.send.owner partial ACK piggyback interoperates with persistent receive window", HostLane.Fast, 6)]
    private static void ReceiveInterop()
    {
        using var f = new Fixture();
        byte[] output = new byte[1514];
        Tx.Queue(f.Owner, "abcdefgh"u8, out _);
        Tx.Pump(f.Owner, output, out _);
        Tx.Pump(f.Owner, output, out _);
        byte[] frame = Wire.Packet(100, "xy"u8, ack: 703, window: 32);
        HostCheck.Equal(Tx.Status.Success, Tx.Acknowledge(f.Owner, frame, out int released));
        HostCheck.Equal(3, released);
        HostCheck.Equal(Rx.Status.Success, Rx.Receive(f.Receiver, frame, out _));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReadOwned(f.Table, f.Handle, out var entry));
        HostCheck.Equal(703U, entry.Sequence.LocalAcknowledgedSequenceNumber);
        HostCheck.Equal(708U, entry.Sequence.LocalNextSequenceNumber);
        HostCheck.Equal(102U, entry.Sequence.RemoteNextSequenceNumber);
        HostCheck.Equal(Tx.Status.Success, f.Ack(708, out released, sequence: 102));
        HostCheck.Equal(5, released);
        byte[] read = new byte[2];
        HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Receiver, read, out _));
        HostCheck.Equal("xy", Encoding.ASCII.GetString(read));
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Receiver, output, out var ack));
        HostCheck.Equal(708U, ack.TcpHeader.SequenceNumber);
        HostCheck.Equal(102U, ack.TcpHeader.AcknowledgementNumber);
    }

    [HostTest("net.tcp.send SYN MSS parser and configured reply use original validated negotiation", HostLane.Fast, 7)]
    private static void Negotiation()
    {
        HostCheck.Equal(PacketParseStatus.Success, TcpHeader.TryReadMaximumSegmentSize(ReadOnlySpan<byte>.Empty, out ushort mss));
        HostCheck.Equal((ushort)536, mss);
        HostCheck.Equal(PacketParseStatus.Success, TcpHeader.TryReadMaximumSegmentSize(new byte[] { 1, 2, 4, 0, 5, 0, 0, 0 }, out mss));
        HostCheck.Equal((ushort)5, mss);
        HostCheck.Equal(PacketParseStatus.Success, TcpHeader.TryReadMaximumSegmentSize(new byte[] { 30, 4, 1, 2, 2, 4, 0, 3 }, out mss));
        HostCheck.Equal((ushort)3, mss);
        foreach (byte[] bad in new byte[][]
        {
            [2, 4, 0, 0],
            [2, 3, 1, 0],
            [30, 0, 0, 0],
            [30, 5, 0, 0],
            [2, 4, 0, 3, 2, 4, 0, 4],
            [1, 1, 1]
        }

        )
        {
            HostCheck.Equal(PacketParseStatus.InvalidLength, TcpHeader.TryReadMaximumSegmentSize(bad, out mss));
            HostCheck.Equal((ushort)0, mss);
        }

        using (var f = new Fixture())
        {
            HostCheck.Equal((ushort)4, f.Handshake->PeerMaximumSegmentSize);
            HostCheck.Equal((ushort)32, f.Handshake->PeerAdvertisedWindow);
            var reply = new ReadOnlySpan<byte>(f.Hardware->Last, f.Hardware->Length);
            HostCheck.Equal(58, reply.Length);
            CheckChecksums(reply);
            HostCheck.Equal((byte)0x60, reply[46]);
            HostCheck.True(reply.Slice(54, 4).SequenceEqual(new byte[] { 2, 4, 5, 0xb4 }));
        }

        using (var f = new Fixture(window: 32, finalWindow: 0))
        {
            HostCheck.Equal((ushort)0, f.Handshake->PeerAdvertisedWindow);
            Tx.Queue(f.Owner, "x"u8, out _);
            HostCheck.Equal(Tx.Status.WindowClosed, Tx.Pump(f.Owner, new byte[64], out _));
        }

        using (var f = new Fixture(setup: false))
        {
            byte[] output = new byte[58], bad = Wire.Packet(99, ReadOnlySpan<byte>.Empty, ack: 0, flags: 2, options: new byte[] { 2, 4, 0, 0 });
            HostCheck.Equal(Passive.Status.InvalidPacket, Passive.Begin(f.Handshake, f.Table, f.PassiveInfo(), bad, output, f.Operations(), out _));
            HostCheck.Equal(0, f.Table->Count);
            HostCheck.Equal(0U, f.Hardware->Calls);
            byte[] syn = Wire.Packet(99, ReadOnlySpan<byte>.Empty, ack: 0, flags: 2, options: new byte[] { 2, 4, 0, 4 });
            HostCheck.Equal(Passive.Status.InvalidPacket, Passive.Begin(f.Handshake, f.Table, f.PassiveInfo(localMss: 1461), syn, output, f.Operations(), out _));
            HostCheck.Equal(0, f.Table->Count);
            HostCheck.Equal(Passive.Status.OutputTooSmall, Passive.Begin(f.Handshake, f.Table, f.PassiveInfo(), syn, output.AsSpan(0, 57), f.Operations(), out _));
            HostCheck.Equal(0, f.Table->Count);
        }
    }

    [HostTest("net.tcp.send.owner attachment rejects bypass and cleanup works in either order", HostLane.Fast, 8)]
    private static void Ownership()
    {
        foreach (bool receiverFirst in new[]
        {
            false,
            true
        }

        )
        {
            using var f = new Fixture();
            Tx.Queue(f.Owner, "abc"u8, out _);
            Tx.Pump(f.Owner, new byte[64], out _);
            HostCheck.Equal(Tx.Status.TableRejected, f.Bind(f.Other, f.OtherBuffer));
            HostCheck.Equal(Tcb.OwnershipStatus.InvalidTransition, Tcb.UpdateOwned(f.Table, f.Handle, new(704, 100, 700)));
            HostCheck.Equal(Tcb.OwnershipStatus.InvalidTransition, Tcb.UpdateOwned(f.Table, f.Handle, new(703, 100, 701)));
            HostCheck.Equal(Tx.Status.TableRejected, Tx.ReleaseClosed(f.Owner));
            f.Close();
            HostCheck.Equal(Tcb.OwnershipStatus.AttachmentsRemain, Tcb.ReleaseOwned(f.Table, f.Handle));
            if (receiverFirst)
                HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(f.Receiver));
            else
                HostCheck.Equal(Tx.Status.Success, Tx.ReleaseClosed(f.Owner));
            HostCheck.Equal(Tcb.OwnershipStatus.AttachmentsRemain, Tcb.ReleaseOwned(f.Table, f.Handle));
            if (receiverFirst)
                HostCheck.Equal(Tx.Status.Success, Tx.ReleaseClosed(f.Owner));
            else
                HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(f.Receiver));
            HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 32).IndexOfAnyExcept((byte)0) < 0);
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(f.Table, f.Handle));
        }
    }

    [HostTest("net.tcp.send.owner copied aliased malformed and future packets cannot free retained bytes", HostLane.Fast, 9)]
    private static void Rejections()
    {
        using var f = new Fixture();
        Tx.Queue(f.Owner, "abcd"u8, out _);
        Tx.Pump(f.Owner, new byte[64], out _);
        Tx.Owner copy = *f.Owner;
        HostCheck.Equal(Tx.Status.InvalidState, Tx.Pump(&copy, new byte[64], out _));
        HostCheck.Equal(Tx.Status.InvalidStorage, Tx.Queue(f.Owner, new ReadOnlySpan<byte>(f.Buffer, 1), out _));
        HostCheck.Equal(Tx.Status.InvalidStorage, Tx.Queue(f.Owner, new ReadOnlySpan<byte>(f.ReceiveBuffer, 1), out _));
        Tx.Queue(f.Owner, "x"u8, out _);
        HostCheck.Equal(Tx.Status.InvalidStorage, Tx.Pump(f.Owner, new Span<byte>(f.Buffer, 64), out _));
        HostCheck.Equal(Tx.Status.InvalidStorage, Tx.Acknowledge(f.Owner, new ReadOnlySpan<byte>(f.Buffer, 54), out _));
        byte[] corrupt = Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 704, flags: 0x10);
        corrupt[50] ^= 1;
        foreach (byte[] bad in new[]
        {
            corrupt,
            Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 704, port: 40001, flags: 0x10),
            Wire.Packet(132, ReadOnlySpan<byte>.Empty, ack: 704, flags: 0x10),
            Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 704, flags: 0x14)
        }

        )
        {
            HostCheck.Equal(Tx.Status.InvalidPacket, Tx.Acknowledge(f.Owner, bad, out int released));
            HostCheck.Equal(0, released);
            bool outsideWindow = BinaryPrimitives.ReadUInt32BigEndian(bad.AsSpan(38, 4)) == 132;
            HostCheck.Equal(outsideWindow, f.Receiver->HasPendingAck);
            if (outsideWindow)
                HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Receiver, new byte[54], out _));
        }

        HostCheck.Equal(5, f.Owner->BufferedBytes);
        HostCheck.Equal(4, f.Owner->InFlightBytes);
        f.Close();
        Tx.ReleaseClosed(f.Owner);
        Rx.ReleaseClosed(f.Receiver);
        Tcb.ReleaseOwned(f.Table, f.Handle);
        *f.Owner = copy;
        HostCheck.Equal(Tx.Status.TableRejected, Tx.Pump(f.Owner, new byte[64], out _));
    }

    [HostTest("net.tcp.send.owner admission and fatal TX retain explicit closed ownership", HostLane.Fast, 10)]
    private static void AdmissionAndFatal()
    {
        using var f = new Fixture(bind: false);
        HostCheck.Equal(Tx.Status.InvalidStorage, Tx.Initialize(f.Owner, f.Receiver, f.Handshake, null, 32, 1500));
        HostCheck.Equal(Tx.Status.InvalidStorage, Tx.Initialize(f.Owner, f.Receiver, f.Handshake, f.ReceiveBuffer, 32, 1500));
        HostCheck.Equal(Tx.Status.InvalidOptions, Tx.Initialize(f.Owner, f.Receiver, f.Handshake, f.Buffer, 32, 67));
        HostCheck.Equal(Tx.Status.Success, f.Bind(f.Owner, f.Buffer));
        HostCheck.Equal(Tx.Status.NoData, Tx.Queue(f.Owner, ReadOnlySpan<byte>.Empty, out _));
        Tx.Queue(f.Owner, "abc"u8, out _);
        f.Hardware->Result = 2;
        HostCheck.Equal(Tx.Status.SendFailed, Tx.Pump(f.Owner, new byte[64], out _));
        HostCheck.True(f.Owner->RequiresCleanup);
        HostCheck.Equal(3, f.Owner->BufferedBytes);
        HostCheck.Equal(0, f.Owner->InFlightBytes);
        HostCheck.Equal(700U, f.Owner->NextSequence);
        f.Close();
        HostCheck.Equal(Tx.Status.Success, Tx.ReleaseClosed(f.Owner));
        HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(f.Receiver));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(f.Table, f.Handle));
    }

    [HostTest("net.tcp.send.owner warmed queue send validated ACK cycles allocate no managed bytes", HostLane.Fast, 11)]
    private static void Allocation()
    {
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<Tx.Owner>());
        using var f = new Fixture();
        byte[] output = new byte[64], ack = Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 701, flags: 0x10, window: 32);
        Tx.Queue(f.Owner, "z"u8, out _);
        Tx.Pump(f.Owner, output, out _);
        Tx.Acknowledge(f.Owner, ack, out _);
        uint next = 702;
        bool success = true;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
            success &= Tx.Queue(f.Owner, "z"u8, out _) == Tx.Status.Success;
            success &= Tx.Pump(f.Owner, output, out _) == Tx.Status.Success;
            Wire.U32(ack, 42, next++);
            Wire.TcpChecksum(ack);
            success &= Tx.Acknowledge(f.Owner, ack, out _) == Tx.Status.Success;
        }

        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(success);
        HostCheck.Equal(0L, bytes);
        HostCheck.Equal(0, f.Owner->BufferedBytes);
    }

    [HostTest("net.tcp.send configured MSS SYN ACK duplicates retries and short output preserve negotiation", HostLane.Fast, 12)]
    private static void PendingMssReplies()
    {
        using var f = new Fixture(setup: false);
        byte[] output = new byte[58];
        byte[] syn = Wire.Packet(99, ReadOnlySpan<byte>.Empty, ack: 0, flags: 2, options: new byte[] { 2, 4, 0, 4 });
        HostCheck.Equal(Passive.Status.Success, Passive.Begin(f.Handshake, f.Table, f.PassiveInfo(), syn, output, f.Operations(), out _));
        byte[] original = output.ToArray();
        f.Hardware->Now = 150;
        HostCheck.Equal(Passive.Status.Success, Passive.ReceiveSyn(f.Handshake, syn, output, out _));
        HostCheck.True(output.AsSpan().SequenceEqual(original));
        uint calls = f.Hardware->Calls;
        HostCheck.Equal(Passive.Status.InvalidPacket, Passive.ReceiveSyn(f.Handshake, Wire.Packet(99, ReadOnlySpan<byte>.Empty, ack: 0, flags: 2, options: new byte[] { 2, 4, 0, 5 }), output, out _));
        HostCheck.Equal(calls, f.Hardware->Calls);
        HostCheck.Equal((ushort)4, f.Handshake->PeerMaximumSegmentSize);
        HostCheck.Equal(Passive.Status.OutputTooSmall, Passive.ReceiveSyn(f.Handshake, syn, output.AsSpan(0, 57), out _));
        f.Hardware->Now = 200;
        HostCheck.Equal(Passive.Status.OutputTooSmall, Passive.Pump(f.Handshake, output.AsSpan(0, 57), out _));
        HostCheck.Equal((byte)1, f.Handshake->SuccessfulAttempts);
        HostCheck.Equal(1100UL, f.Handshake->DeadlineNanoseconds);
        HostCheck.Equal(Passive.Status.Success, Passive.Pump(f.Handshake, output, out _));
        HostCheck.True(output.AsSpan().SequenceEqual(original));
        HostCheck.Equal((byte)2, f.Handshake->SuccessfulAttempts);
        CheckChecksums(output);
        HostCheck.Equal(Passive.Status.Success, Passive.CancelPending(f.Handshake));
    }

    private static ulong Clock(void* context)
    {
        var hardware = (Hardware*)context;
        hardware->ClockCalls++;
        ulong now = hardware->Now;
        hardware->Now += hardware->AdvanceOnClock;
        return now;
    }
    private static byte Send(void* context, byte* bytes, uint length)
    {
        var hardware = (Hardware*)context;
        hardware->Calls++;
        hardware->Now += hardware->AdvanceOnSend;
        if (length > 1514)
            return 2;
        if (hardware->Result == 1)
        {
            hardware->Length = (int)length;
            for (int i = 0; i < length; i++)
                hardware->Last[i] = bytes[i];
        }

        return hardware->Result;
    }

    internal static void CheckData(ReadOnlySpan<byte> frame, uint local, uint remote, ushort window, ReadOnlySpan<byte> payload)
    {
        CheckChecksums(frame);
        HostCheck.Equal(local, BinaryPrimitives.ReadUInt32BigEndian(frame.Slice(38, 4)));
        HostCheck.Equal(remote, BinaryPrimitives.ReadUInt32BigEndian(frame.Slice(42, 4)));
        HostCheck.Equal(window, BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(48, 2)));
        HostCheck.Equal((byte)0x18, frame[47]);
        HostCheck.True(frame.Slice(54).SequenceEqual(payload));
    }

    private static void CheckChecksums(ReadOnlySpan<byte> frame)
    {
        HostCheck.Equal((ushort)0xffff, Wire.Sum(frame.Slice(14, 20)));
        HostCheck.Equal((ushort)0xffff, Wire.Fold(Wire.Sum(frame.Slice(26, 8)) + 6U + (uint)frame.Length - 34 + Wire.Sum(frame.Slice(34))));
        HostCheck.True(frame.Slice(0, 14).SequenceEqual(new byte[] { 0x52, 0x55, 0x0a, 0, 2, 2, 2, 0, 0, 0, 0, 1, 8, 0 }));
    }
}
