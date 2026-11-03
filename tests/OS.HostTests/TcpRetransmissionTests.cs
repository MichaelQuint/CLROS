using OS.Net;
using Tx = OS.Net.TcpSendWindow;
using Rx = OS.Net.TcpReceiveWindow;
using Fixture = TcpSendWindowTests.Fixture;
using Wire = TcpReceiveWindowTests;

internal static unsafe class TcpRetransmissionTests
{
    private const ulong Second = 1_000_000_000;

    [HostTest("net.tcp.retry starts only after accepted TX and keeps one segment outstanding", HostLane.Fast, 0)]
    private static void Admission()
    {
        using var f = new Fixture();
        byte[] output = new byte[1514];
        HostCheck.Equal(Tx.Status.Success, Configure(f, 100 * Second));
        HostCheck.Equal(Tx.Status.NothingToSend, Tx.PumpRetransmission(f.Owner, output, out _));
        Tx.Queue(f.Owner, "abcdefgh"u8, out _);
        f.Hardware->Result = 0;
        HostCheck.Equal(Tx.Status.WouldBlock, Tx.Pump(f.Owner, output, out _));
        HostCheck.Equal(0UL, f.Owner->RetransmissionDeadlineNanoseconds);
        HostCheck.Equal(700U, f.Owner->NextSequence);
        f.Hardware->Result = 1;
        f.Hardware->Now = 200;
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out _));
        HostCheck.Equal(Second + 200, f.Owner->RetransmissionDeadlineNanoseconds);
        HostCheck.Equal(Tx.Status.AwaitingAck, Tx.Pump(f.Owner, output, out _));
        uint calls = f.Hardware->Calls;
        f.Hardware->Now = Second + 199;
        HostCheck.Equal(Tx.Status.NotDue, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal(calls, f.Hardware->Calls);
    }

    [HostTest("net.tcp.retry recovers lost data with original sequence bytes and exponential backoff", HostLane.Fast, 1)]
    private static void Loss()
    {
        using var f = new Fixture();
        byte[] output = Start(f);
        f.Hardware->Now = Second + 100;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out var packet));
        TcpSendWindowTests.CheckData(output.AsSpan(0, packet.BytesWritten), 700, 100, 32, "abcd"u8);
        HostCheck.Equal(704U, f.Owner->NextSequence);
        HostCheck.Equal(700U, f.Owner->OldestUnacknowledgedSequence);
        HostCheck.Equal(8, f.Owner->BufferedBytes);
        HostCheck.Equal(4, f.Owner->InFlightBytes);
        HostCheck.Equal((byte)1, f.Owner->Retransmissions);
        HostCheck.Equal(2 * Second, f.Owner->RetransmissionTimeoutNanoseconds);
        HostCheck.Equal(3 * Second + 100, f.Owner->RetransmissionDeadlineNanoseconds);
        f.Hardware->Now = 3 * Second + 99;
        HostCheck.Equal(Tx.Status.NotDue, Tx.PumpRetransmission(f.Owner, output, out _));
        f.Hardware->Now++;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal(4 * Second, f.Owner->RetransmissionTimeoutNanoseconds);
        HostCheck.Equal(Tx.Status.Success, f.Ack(704, out int released));
        HostCheck.Equal(4, released);
        HostCheck.Equal(0UL, f.Owner->RetransmissionDeadlineNanoseconds);
        HostCheck.Equal(Tx.Status.NothingToSend, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out packet));
        TcpSendWindowTests.CheckData(output.AsSpan(0, packet.BytesWritten), 704, 100, 32, "efgh"u8);
    }

    [HostTest("net.tcp.retry duplicate reordered invalid and window ACKs cannot defer recovery", HostLane.Fast, 2)]
    private static void DuplicateAcks()
    {
        using var f = new Fixture();
        byte[] output = Start(f);
        ulong deadline = f.Owner->RetransmissionDeadlineNanoseconds;
        f.Hardware->Now = Second / 2;
        for (int i = 0; i < 20; i++)
            HostCheck.Equal(Tx.Status.DuplicateAck, f.Ack(700, out _));
        HostCheck.Equal(20UL, f.Owner->DuplicateAcknowledgements);
        HostCheck.Equal(Tx.Status.OldAck, f.Ack(699, out _));
        HostCheck.Equal(Tx.Status.InvalidAck, f.Ack(705, out _));
        HostCheck.Equal(Tx.Status.WindowUpdated, f.Ack(700, out _, window: 0));
        byte[] bad = Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 704, flags: 0x10);
        bad[50] ^= 1;
        HostCheck.Equal(Tx.Status.InvalidPacket, Tx.Acknowledge(f.Owner, bad, out _));
        HostCheck.Equal(deadline, f.Owner->RetransmissionDeadlineNanoseconds);
        f.Hardware->Now = deadline;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal(8, f.Owner->BufferedBytes);
    }

    [HostTest("net.tcp.retry partial ACK retries only retained suffix after a fresh RTO", HostLane.Fast, 3)]
    private static void PartialAck()
    {
        using var f = new Fixture();
        byte[] output = Start(f);
        f.Hardware->Now = Second / 2;
        HostCheck.Equal(Tx.Status.Success, f.Ack(702, out int released));
        HostCheck.Equal(2, released);
        HostCheck.Equal(Second + Second / 2, f.Owner->RetransmissionDeadlineNanoseconds);
        f.Hardware->Now = Second + Second / 2;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out var packet));
        TcpSendWindowTests.CheckData(output.AsSpan(0, packet.BytesWritten), 702, 100, 32, "cd"u8);
        HostCheck.Equal(704U, f.Owner->NextSequence);
        HostCheck.Equal(6, f.Owner->BufferedBytes);
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 2).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.Equal(Tx.Status.Success, f.Ack(704, out released));
        HostCheck.Equal(2, released);
        HostCheck.Equal(2 * Second, f.Owner->RetransmissionTimeoutNanoseconds); // Karn excludes ambiguous sample.
    }

    [HostTest("net.tcp.retry RTT estimator smooths original samples and excludes retransmitted data", HostLane.Fast, 4)]
    private static void Samples()
    {
        using var f = new Fixture();
        byte[] output = Start(f);
        f.Hardware->Now = Second + 100;
        HostCheck.Equal(Tx.Status.Success, f.Ack(704, out _));
        HostCheck.Equal(3 * Second, f.Owner->RetransmissionTimeoutNanoseconds);
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out _));
        f.Hardware->Now += 2 * Second;
        HostCheck.Equal(Tx.Status.Success, f.Ack(708, out _));
        // RTTVAR=(3*0.5+1)/4=.625s, SRTT=(7*1+2)/8=1.125s.
        HostCheck.Equal(3_625_000_000UL, f.Owner->RetransmissionTimeoutNanoseconds);
        Tx.Queue(f.Owner, "ijkl"u8, out _);
        Tx.Pump(f.Owner, output, out _);
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out _));
        f.Hardware->Now += Second;
        HostCheck.Equal(Tx.Status.Success, f.Ack(712, out _));
        HostCheck.Equal(7_250_000_000UL, f.Owner->RetransmissionTimeoutNanoseconds);
    }

    [HostTest("net.tcp.retry TX pressure and short output preserve retry and sequence state", HostLane.Fast, 5)]
    private static void Pressure()
    {
        using var f = new Fixture();
        byte[] output = Start(f);
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.OutputTooSmall, Tx.PumpRetransmission(f.Owner, output.AsSpan(0, 57), out _));
        f.Hardware->Result = 0;
        ulong deadline = f.Owner->RetransmissionDeadlineNanoseconds;
        for (int i = 0; i < 8; i++)
            HostCheck.Equal(Tx.Status.WouldBlock, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal((byte)0, f.Owner->Retransmissions);
        HostCheck.Equal(deadline, f.Owner->RetransmissionDeadlineNanoseconds);
        HostCheck.Equal(Second, f.Owner->RetransmissionTimeoutNanoseconds);
        HostCheck.Equal(704U, f.Owner->NextSequence);
        f.Hardware->Result = 1;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal((byte)1, f.Owner->Retransmissions);
    }

    [HostTest("net.tcp.retry bounded attempts timeout retains bytes until closed cleanup", HostLane.Fast, 6)]
    private static void RetryLimit()
    {
        using var f = new Fixture();
        byte[] output = Start(f, retries: 2);
        for (int i = 0; i < 2; i++)
        {
            f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
            HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out _));
        }
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        uint calls = f.Hardware->Calls;
        HostCheck.Equal(Tx.Status.Timeout, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.True(f.Owner->RequiresCleanup);
        HostCheck.Equal(calls, f.Hardware->Calls);
        HostCheck.Equal(8, f.Owner->BufferedBytes);
        HostCheck.Equal(4, f.Owner->InFlightBytes);
        HostCheck.Equal(Tx.Status.InvalidState, f.Ack(704, out _));
        f.Close();
        HostCheck.Equal(Tx.Status.Success, Tx.ReleaseClosed(f.Owner));
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 32).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(f.Receiver));
    }

    [HostTest("net.tcp.retry hard deadline bounds zero window TX pressure and late ACK", HostLane.Fast, 7)]
    private static void Lifetime()
    {
        using (var f = new Fixture(window: 0))
        {
            HostCheck.Equal(Tx.Status.Success, Configure(f, 2 * Second));
            Tx.Queue(f.Owner, "data"u8, out _);
            HostCheck.Equal(Tx.Status.WindowClosed, Tx.Pump(f.Owner, new byte[1514], out _));
            f.Hardware->Now += 2 * Second;
            HostCheck.Equal(Tx.Status.Timeout, Tx.Queue(f.Owner, "x"u8, out int accepted));
            HostCheck.Equal(0, accepted);
            HostCheck.Equal(4, f.Owner->BufferedBytes);
        }
        using (var f = new Fixture())
        {
            byte[] output = Start(f, lifetime: 2 * Second);
            f.Hardware->Result = 0;
            f.Hardware->Now = Second + 100;
            HostCheck.Equal(Tx.Status.WouldBlock, Tx.PumpRetransmission(f.Owner, output, out _));
            f.Hardware->Now = 2 * Second + 100;
            HostCheck.Equal(Tx.Status.Timeout, f.Ack(704, out int released));
            HostCheck.Equal(0, released);
            HostCheck.Equal(8, f.Owner->BufferedBytes);
        }
    }

    [HostTest("net.tcp.retry backwards and overflowing clocks fail without releasing owned data", HostLane.Fast, 8)]
    private static void Clocks()
    {
        using (var f = new Fixture())
        {
            byte[] output = Start(f);
            f.Hardware->Now--;
            HostCheck.Equal(Tx.Status.InvalidClock, Tx.PumpRetransmission(f.Owner, output, out _));
            HostCheck.True(f.Owner->RequiresCleanup);
            HostCheck.Equal(8, f.Owner->BufferedBytes);
        }
        using (var f = new Fixture())
        {
            f.Hardware->Now = ulong.MaxValue - Second;
            HostCheck.Equal(Tx.Status.InvalidClock, Configure(f, 2 * Second));
            HostCheck.True(f.Owner->Active);
            HostCheck.Equal(0UL, f.Owner->RetransmissionTimeoutNanoseconds);
        }
    }

    [HostTest("net.tcp.retry rejects invalid admission aliases copied owners and fatal TX", HostLane.Fast, 9)]
    private static void InvalidOwners()
    {
        using var f = new Fixture();
        byte[] output = new byte[1514];
        HostCheck.Equal(Tx.Status.InvalidState, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.InvalidOptions, Configure(f, Second));
        HostCheck.Equal(Tx.Status.InvalidOptions, Configure(f, 100 * Second, initialRtoNanoseconds: 1));
        HostCheck.Equal(Tx.Status.InvalidOptions, Configure(f, 100 * Second, maximumRtoNanoseconds: Second));
        HostCheck.Equal(Tx.Status.InvalidOptions, Configure(f, 100 * Second, maximumRetransmissions: 0));
        Start(f, output: output);
        HostCheck.Equal(Tx.Status.InvalidState, Configure(f, 100 * Second));
        Tx.Owner copy = *f.Owner;
        HostCheck.Equal(Tx.Status.InvalidState, Tx.PumpRetransmission(&copy, output, out _));
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.InvalidStorage, Tx.PumpRetransmission(f.Owner, new Span<byte>(f.Buffer, 60), out _));
        f.Hardware->Result = 2;
        HostCheck.Equal(Tx.Status.SendFailed, Tx.PumpRetransmission(f.Owner, output, out _));
        HostCheck.True(f.Owner->RequiresCleanup);
        HostCheck.Equal((byte)0, f.Owner->Retransmissions);
    }

    [HostTest("net.tcp.retry wrap and independent receiver reorder preserve original payload ownership", HostLane.Fast, 10)]
    private static void WrapAndReorder()
    {
        using var f = new Fixture(iss: uint.MaxValue - 2);
        byte[] output = Start(f);
        HostCheck.Equal(Tx.Status.Success, f.Ack(0, out int released));
        HostCheck.Equal(2, released);
        byte[] gap = Wire.Packet(102, "gap"u8, ack: 2, flags: 0x18);
        HostCheck.Equal(Rx.Status.OutOfOrder, Rx.Receive(f.Receiver, gap, out _));
        HostCheck.Equal(0, f.Receiver->UnreadBytes);
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out var packet));
        TcpSendWindowTests.CheckData(output.AsSpan(0, packet.BytesWritten), 0, 100, 32, "cd"u8);
        HostCheck.Equal(2U, f.Owner->NextSequence);
    }

    [HostTest("net.tcp.retry warmed send loss ACK cycle allocates no managed bytes", HostLane.Fast, 11)]
    private static void Allocation()
    {
        using var f = new Fixture();
        byte[] output = new byte[1514];
        byte[] ack = Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 700, flags: 0x10);
        HostCheck.Equal(Tx.Status.Success, Configure(f, 100_000 * Second));
        for (int i = 0; i < 2; i++)
            Cycle(f, output, ack);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
            Cycle(f, output, ack);
        HostCheck.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [HostTest("net.tcp.retry caps exponential RTO and performs at most one attempt per call", HostLane.Fast, 12)]
    private static void MaximumRto()
    {
        using var f = new Fixture();
        byte[] output = Start(f, retries: 16, lifetime: 1000 * Second);
        for (int i = 0; i < 9; i++)
        {
            f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
            uint calls = f.Hardware->Calls;
            HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out _));
            HostCheck.Equal(calls + 1, f.Hardware->Calls);
            HostCheck.Equal(Math.Min(1UL << (i + 1), 60UL) * Second, f.Owner->RetransmissionTimeoutNanoseconds);
            HostCheck.Equal(Tx.Status.NotDue, Tx.PumpRetransmission(f.Owner, output, out _));
            HostCheck.Equal(calls + 1, f.Hardware->Calls);
        }
    }

    [HostTest("net.tcp.retry serializes current receive ACK and window after new peer data", HostLane.Fast, 13)]
    private static void ReceiveProgress()
    {
        using var f = new Fixture();
        byte[] output = Start(f);
        byte[] incoming = Wire.Packet(100, "xy"u8, ack: 700, flags: 0x18, window: 32);
        HostCheck.Equal(Tx.Status.DuplicateAck, Tx.Acknowledge(f.Owner, incoming, out _));
        HostCheck.Equal(Rx.Status.Success, Rx.Receive(f.Receiver, incoming, out _));
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, output, out var packet));
        TcpSendWindowTests.CheckData(output.AsSpan(0, packet.BytesWritten), 700, 102, 30, "abcd"u8);
        HostCheck.Equal(2, f.Receiver->UnreadBytes);
        HostCheck.Equal(0UL, f.Owner->DuplicateAcknowledgements);
    }

    [HostTest("net.tcp.retry timed handshake loss uses three seconds until clean data RTT", HostLane.Fast, 14)]
    private static void HandshakeLoss()
    {
        using var f = new Fixture(retryHandshake: true);
        byte[] output = new byte[1514];
        HostCheck.Equal((byte)2, f.Handshake->SuccessfulAttempts);
        HostCheck.Equal(Tx.Status.InvalidOptions, Configure(f, 2 * Second));
        HostCheck.Equal(Tx.Status.Success, Configure(f, 100 * Second));
        HostCheck.Equal(3 * Second, f.Owner->RetransmissionTimeoutNanoseconds);
        Tx.Queue(f.Owner, "data"u8, out _);
        Tx.Pump(f.Owner, output, out _);
        f.Hardware->Now += Second / 10;
        HostCheck.Equal(Tx.Status.Success, f.Ack(704, out _));
        HostCheck.Equal(Second, f.Owner->RetransmissionTimeoutNanoseconds);
    }

    [HostTest("net.tcp.retry timestamp follows copied TX and preserves progress on late clock failure", HostLane.Fast, 15)]
    private static void AcceptedTime()
    {
        using (var f = new Fixture())
        {
            Configure(f, 100 * Second);
            Tx.Queue(f.Owner, "data"u8, out _);
            f.Hardware->AdvanceOnSend = Second / 2;
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
            HostCheck.Equal(Second + Second / 2 + 100, f.Owner->RetransmissionDeadlineNanoseconds);
            f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
            HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
            HostCheck.Equal(4 * Second + 100, f.Owner->RetransmissionDeadlineNanoseconds);
        }
        using (var f = new Fixture())
        {
            Configure(f, 2 * Second);
            Tx.Queue(f.Owner, "data"u8, out _);
            f.Hardware->AdvanceOnSend = 2 * Second;
            HostCheck.Equal(Tx.Status.Timeout, Tx.Pump(f.Owner, new byte[1514], out _));
            HostCheck.Equal(704U, f.Owner->NextSequence);
            HostCheck.Equal(4, f.Owner->InFlightBytes);
            HostCheck.True(f.Owner->RequiresCleanup);
        }
    }

    private static void Cycle(Fixture f, byte[] output, byte[] ack)
    {
        Tx.Queue(f.Owner, "abcd"u8, out _);
        Tx.Pump(f.Owner, output, out _);
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        Tx.PumpRetransmission(f.Owner, output, out _);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(ack.AsSpan(42, 4), f.Owner->NextSequence);
        ack[50] = 0;
        ack[51] = 0;
        ushort sum = (ushort)~Wire.Fold(Wire.Sum(ack.AsSpan(26, 8)) + 6U + (uint)ack.Length - 34 + Wire.Sum(ack.AsSpan(34)));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(ack.AsSpan(50, 2), sum);
        if (Tx.Acknowledge(f.Owner, ack, out int released) != Tx.Status.Success || released != 4)
            throw new InvalidOperationException("loss cycle failed");
    }

    private static byte[] Start(Fixture f, byte retries = 5, ulong lifetime = 100 * Second, byte[]? output = null)
    {
        output ??= new byte[1514];
        HostCheck.Equal(Tx.Status.Success, Configure(f, lifetime, maximumRetransmissions: retries));
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcdefgh"u8, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out _));
        return output;
    }

    private static Tx.Status Configure(Fixture f, ulong lifetimeNanoseconds,
        ulong initialRtoNanoseconds = Second, ulong maximumRtoNanoseconds = 60 * Second, byte maximumRetransmissions = 5)
    {
        var io = f.Operations();
        return Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, lifetimeNanoseconds,
            initialRtoNanoseconds, maximumRtoNanoseconds, maximumRetransmissions);
    }
}
