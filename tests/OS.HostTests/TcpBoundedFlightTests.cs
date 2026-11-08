using System.Runtime.InteropServices;
using OS.Net;
using Tx = OS.Net.TcpSendWindow;
using Rx = OS.Net.TcpReceiveWindow;
using Table = OS.Net.TcpConnectionTable;
using Fixture = TcpSendWindowTests.Fixture;
using Wire = TcpReceiveWindowTests;

internal static unsafe class TcpBoundedFlightTests
{
    private const ulong Second = 1_000_000_000;

    private static Fixture Start(int capacity = 32, ushort window = ushort.MaxValue, uint iss = 699)
    {
        var f = new Fixture(capacity: capacity, receiveCapacity: 32, window: window, iss: iss);
        var io = f.Operations();
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 1000 * Second));
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(f.Owner, 30 * Second));
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureBoundedFlight(f.Owner));
        return f;
    }

    private static void Warm(Fixture f, int acknowledgements = 7)
    {
        for (int i = 0; i < acknowledgements; i++)
        {
            HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "warm"u8, out _));
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
            HostCheck.Equal(Tx.Status.Success, f.Ack(f.Owner->NextSequence, out int released, window: ushort.MaxValue));
            HostCheck.Equal(4, released);
        }
    }

    private static void Last(Fixture f, uint sequence, ReadOnlySpan<byte> payload)
        => TcpSendWindowTests.CheckData(new ReadOnlySpan<byte>(f.Hardware->Last, f.Hardware->Length), sequence, 100, 32, payload);

    [HostTest("tcp bounded flight retains32KiB with eight exact segments and cumulative plus partial ACK release")]
    private static void EightSegments()
    {
        using var f = Start(capacity: 32768);
        HostCheck.True(sizeof(Tx.Owner) <= 512 && sizeof(OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork.Slot) <= 1024);
        Warm(f);
        uint start = f.Owner->NextSequence;
        byte[] data = Enumerable.Range(0, 32768).Select(i => unchecked((byte)(i * 29))).ToArray();
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, data, out int accepted));
        HostCheck.Equal(32768, accepted);
        ulong deadline = 0;
        for (int i = 0; i < 8; i++)
        {
            f.Hardware->Now += 1000;
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
            Last(f, start + (uint)i * 4, data.AsSpan(i * 4, 4));
            if (i == 0)
                deadline = f.Owner->RetransmissionDeadlineNanoseconds;
            HostCheck.Equal(deadline, f.Owner->RetransmissionDeadlineNanoseconds);
        }
        HostCheck.Equal((byte)8, f.Owner->OutstandingSegments);
        HostCheck.Equal(32, f.Owner->InFlightBytes);
        uint calls = f.Hardware->Calls;
        HostCheck.Equal(Tx.Status.AwaitingAck, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(calls, f.Hardware->Calls);
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 16, out int released, window: ushort.MaxValue));
        HostCheck.Equal(16, released);
        HostCheck.Equal((byte)4, f.Owner->OutstandingSegments);
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 18, out released, window: ushort.MaxValue));
        HostCheck.Equal(2, released);
        HostCheck.Equal((byte)4, f.Owner->OutstandingSegments);
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        Last(f, start + 18, data.AsSpan(18, 2));
        HostCheck.Equal(32750, f.Owner->BufferedBytes);
    }

    [HostTest("tcp bounded flight timeout retransmits only the exact oldest suffix and applies loss cwnd backoff and Karn")]
    private static void TimeoutAndPartialAck()
    {
        using var f = Start();
        Warm(f, 3);
        uint start = f.Owner->NextSequence;
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcdefghijklmnop"u8, out _));
        for (int i = 0; i < 4; i++)
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        Last(f, start, "abcd"u8);
        HostCheck.Equal(4U, f.Owner->CongestionWindowBytes);
        HostCheck.Equal(8U, f.Owner->SlowStartThresholdBytes);
        HostCheck.Equal(2 * Second, f.Owner->RetransmissionTimeoutNanoseconds);
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "qrst"u8, out _));
        HostCheck.Equal(Tx.Status.AwaitingAck, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 6, out int released, window: ushort.MaxValue));
        HostCheck.Equal(6, released);
        HostCheck.Equal(2 * Second, f.Owner->RetransmissionTimeoutNanoseconds);
        HostCheck.Equal((byte)3, f.Owner->OutstandingSegments);
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        Last(f, start + 6, "gh"u8);
        HostCheck.Equal(4 * Second, f.Owner->RetransmissionTimeoutNanoseconds);
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 16, out released, window: ushort.MaxValue));
        HostCheck.Equal(10, released);
        HostCheck.Equal(4 * Second, f.Owner->RetransmissionTimeoutNanoseconds);
        HostCheck.Equal((byte)0, f.Owner->OutstandingSegments);
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        f.Hardware->Now += 100;
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 20, out _, window: ushort.MaxValue));
        HostCheck.Equal(Second, f.Owner->RetransmissionTimeoutNanoseconds);
    }

    [HostTest("tcp bounded flight resumes clean RTT samples after retransmission horizon without draining the stream")]
    private static void StreamingKarn()
    {
        using var f = Start();
        Warm(f, 3);
        uint start = f.Owner->NextSequence;
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, new byte[24], out _));
        for (int i = 0; i < 4; i++)
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 12, out _, window: ushort.MaxValue));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 16, out _, window: ushort.MaxValue));
        HostCheck.Equal(4, f.Owner->InFlightBytes);
        HostCheck.Equal(2 * Second, f.Owner->RetransmissionTimeoutNanoseconds);
        f.Hardware->Now += 100;
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        f.Hardware->Now += 100;
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 24, out _, window: ushort.MaxValue));
        HostCheck.Equal(Second, f.Owner->RetransmissionTimeoutNanoseconds);
    }

    [HostTest("tcp bounded flight three duplicates trigger exact fast retransmission with pressure retention and Reno recovery")]
    private static void FastRecovery()
    {
        using var f = Start();
        Warm(f, 3);
        uint start = f.Owner->NextSequence;
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcdefghijklmnop"u8, out _));
        for (int i = 0; i < 4; i++)
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        for (int i = 0; i < 3; i++)
            HostCheck.Equal(Tx.Status.DuplicateAck, f.Ack(start, out _, window: ushort.MaxValue));
        ulong deadline = f.Owner->RetransmissionDeadlineNanoseconds;
        f.Hardware->Result = 0;
        HostCheck.Equal(Tx.Status.WouldBlock, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        HostCheck.Equal(16U, f.Owner->CongestionWindowBytes);
        HostCheck.Equal(deadline, f.Owner->RetransmissionDeadlineNanoseconds);
        HostCheck.Equal(16, f.Owner->InFlightBytes);
        f.Hardware->Result = 1;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        Last(f, start, "abcd"u8);
        HostCheck.Equal(8U, f.Owner->SlowStartThresholdBytes);
        HostCheck.Equal(20U, f.Owner->CongestionWindowBytes);
        HostCheck.Equal(Second, f.Owner->RetransmissionTimeoutNanoseconds);
        HostCheck.Equal((byte)0, f.Owner->Retransmissions);
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "qrstuvwx"u8, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        Last(f, start + 16, "qrst"u8);
        HostCheck.Equal(Tx.Status.DuplicateAck, f.Ack(start, out _, window: ushort.MaxValue));
        HostCheck.Equal(24U, f.Owner->CongestionWindowBytes);
        f.Hardware->Now += 2 * Second;
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 4, out _, window: ushort.MaxValue));
        HostCheck.Equal(8U, f.Owner->CongestionWindowBytes);
        HostCheck.Equal(Second, f.Owner->RetransmissionTimeoutNanoseconds);
        HostCheck.Equal(16, f.Owner->InFlightBytes);
    }

    [HostTest("tcp bounded flight receive window and copied-frame backpressure never consume unaccepted ownership")]
    private static void WindowAndPressure()
    {
        using var f = Start(window: 3);
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcdefgh"u8, out _));
        f.Hardware->Result = 0;
        uint next = f.Owner->NextSequence;
        HostCheck.Equal(Tx.Status.WouldBlock, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(next, f.Owner->NextSequence);
        HostCheck.Equal((byte)0, f.Owner->OutstandingSegments);
        HostCheck.Equal(0UL, f.Owner->RetransmissionDeadlineNanoseconds);
        f.Hardware->Result = 1;
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        Last(f, next, "abc"u8);
        HostCheck.Equal(Tx.Status.WindowClosed, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(3, f.Owner->InFlightBytes);
        HostCheck.Equal((byte)1, f.Owner->OutstandingSegments);
    }

    [HostTest("tcp bounded flight persists a retained octet through zero windows without ring growth or ordinary retry exhaustion")]
    private static void ZeroWindow()
    {
        using var f = Start(window: 0);
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcdefgh"u8, out _));
        uint start = f.Owner->NextSequence;
        HostCheck.Equal(Tx.Status.WindowClosed, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.NotDue, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        f.Hardware->Now = f.Owner->PersistDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        Last(f, start, "a"u8);
        HostCheck.Equal((byte)1, f.Owner->OutstandingSegments);
        f.Hardware->Now = f.Owner->PersistDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        Last(f, start, "a"u8);
        HostCheck.Equal((byte)1, f.Owner->OutstandingSegments);
        HostCheck.Equal((byte)0, f.Owner->Retransmissions);
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 1, out _, window: ushort.MaxValue));
        HostCheck.Equal((byte)0, f.Owner->OutstandingSegments);
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        Last(f, start + 1, "bcde"u8);
    }

    [HostTest("tcp bounded flight cumulative and partial ACKs retain exact segment identity across sequence wrap")]
    private static void SequenceWrap()
    {
        using var f = Start(iss: 0xffffffeb);
        Warm(f, 3);
        uint start = f.Owner->NextSequence;
        HostCheck.Equal(0xfffffff8U, start);
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcdefghijklmnop"u8, out _));
        for (int i = 0; i < 4; i++)
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(8U, f.Owner->NextSequence);
        HostCheck.Equal(Tx.Status.Success, f.Ack(0xfffffffe, out int released, window: ushort.MaxValue));
        HostCheck.Equal(6, released);
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        Last(f, 0xfffffffe, "gh"u8);
        HostCheck.Equal(Tx.Status.Success, f.Ack(8, out released, window: ushort.MaxValue));
        HostCheck.Equal(10, released);
        HostCheck.Equal((byte)0, f.Owner->OutstandingSegments);
        HostCheck.Equal(0, f.Owner->BufferedBytes);
    }

    [HostTest("tcp bounded flight FIN follows complete data ACK and closed release scrubs payload and ring for reuse")]
    private static void CloseAndCleanup()
    {
        using var f = Start();
        Warm(f, 3);
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcdefghijklmnop"u8, out _));
        for (int i = 0; i < 4; i++)
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.Success, Tx.ShutdownWrite(f.Owner));
        HostCheck.Equal(Tx.Status.WaitingForDrain, Tx.PumpClose(f.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(f.Owner->NextSequence, out _, window: ushort.MaxValue));
        HostCheck.Equal((byte)0, f.Owner->OutstandingSegments);
        HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, new byte[1514], out _));
        HostCheck.True(f.Owner->FinOutstanding);
        HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
        var handle = f.Handle;
        HostCheck.Equal(Tx.Status.Success, Tx.ReleaseClosed(f.Owner));
        HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(f.Receiver));
        HostCheck.Equal(Table.OwnershipStatus.Success, Table.ReleaseOwned(f.Table, handle));
        HostCheck.Equal((byte)0, f.Owner->OutstandingSegments);
        HostCheck.Equal(0U, f.Owner->CongestionWindowBytes);
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 32).IndexOfAnyExcept((byte)0) < 0);
    }

    [HostTest("tcp bounded flight malformed retained ring rejects before callbacks payload or ACK mutation")]
    private static void CorruptRing()
    {
        using var f = Start();
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcd"u8, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        ushort* length = (ushort*)((byte*)f.Owner + (int)Marshal.OffsetOf<Tx.Owner>("FlightLengths"));
        ushort original = *length;
        uint calls = f.Hardware->Calls, clocks = f.Hardware->ClockCalls;
        try
        {
            *length = 5;
            HostCheck.Equal(Tx.Status.InvalidState, Tx.Pump(f.Owner, new byte[1514], out _));
            HostCheck.Equal(Tx.Status.InvalidState, Tx.Queue(f.Owner, "x"u8, out int accepted));
            HostCheck.Equal(0, accepted);
            HostCheck.Equal(Tx.Status.InvalidState, f.Ack(f.Owner->NextSequence, out _));
            HostCheck.Equal(calls, f.Hardware->Calls);
            HostCheck.Equal(clocks, f.Hardware->ClockCalls);
            HostCheck.Equal(4, f.Owner->InFlightBytes);
        }
        finally
        {
            *length = original;
        }
        HostCheck.Equal(Tx.Status.Success, f.Ack(f.Owner->NextSequence, out _, window: ushort.MaxValue));
    }

    [HostTest("tcp bounded flight congestion avoidance counts ACKed bytes and resists ACK division")]
    private static void CongestionAvoidance()
    {
        using var f = Start();
        Warm(f, 3);
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, new byte[16], out _));
        for (int i = 0; i < 4; i++)
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(f.Owner->NextSequence, out _, window: ushort.MaxValue));
        HostCheck.Equal(8U, f.Owner->CongestionWindowBytes);
        for (int i = 0; i < 2; i++)
        {
            HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcd"u8, out _));
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
            HostCheck.Equal(Tx.Status.Success, f.Ack(f.Owner->NextSequence, out _, window: ushort.MaxValue));
            HostCheck.Equal(i == 0 ? 8U : 12U, f.Owner->CongestionWindowBytes);
        }
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, new byte[8], out _));
        for (int i = 0; i < 2; i++)
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        uint start = f.Owner->OldestUnacknowledgedSequence;
        for (uint i = 1; i <= 8; i++)
        {
            HostCheck.Equal(Tx.Status.Success, f.Ack(start + i, out _, window: ushort.MaxValue));
            HostCheck.Equal(12U, f.Owner->CongestionWindowBytes);
        }
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcd"u8, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(f.Owner->NextSequence, out _, window: ushort.MaxValue));
        HostCheck.Equal(16U, f.Owner->CongestionWindowBytes);
    }

    [HostTest("tcp bounded flight old and future reordered ACKs cannot release or alias outstanding segments")]
    private static void ReorderedAcks()
    {
        using var f = Start();
        Warm(f, 3);
        uint start = f.Owner->NextSequence;
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, new byte[16], out _));
        for (int i = 0; i < 4; i++)
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.InvalidAck, f.Ack(start + 17, out int released, window: ushort.MaxValue));
        HostCheck.Equal(0, released);
        HostCheck.Equal((byte)4, f.Owner->OutstandingSegments);
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 8, out released, window: ushort.MaxValue));
        HostCheck.Equal(8, released);
        ulong deadline = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.OldAck, f.Ack(start + 4, out released, window: ushort.MaxValue));
        HostCheck.Equal(0, released);
        HostCheck.Equal(deadline, f.Owner->RetransmissionDeadlineNanoseconds);
        HostCheck.Equal((byte)2, f.Owner->OutstandingSegments);
        HostCheck.Equal(Tx.Status.Success, f.Ack(start + 16, out released, window: ushort.MaxValue));
        HostCheck.Equal(8, released);
        HostCheck.Equal((byte)0, f.Owner->OutstandingSegments);
    }

    [HostTest("tcp bounded flight ring generations wrap through retained buffer compaction without retransmission aliasing")]
    private static void RingWrapAndCompaction()
    {
        using var f = Start();
        Warm(f);
        byte[] original = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, original, out _));
        var expected = new Queue<byte[]>(Enumerable.Range(0, 8).Select(i => original.AsSpan(i * 4, 4).ToArray()));
        Array.Fill(original, (byte)0xa5);
        for (int i = 0; i < 8; i++)
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        for (int i = 0; i < 20; i++)
        {
            HostCheck.Equal(Tx.Status.Success, f.Ack(f.Owner->OldestUnacknowledgedSequence + 4, out int released, window: ushort.MaxValue));
            HostCheck.Equal(4, released);
            expected.Dequeue();
            byte[] next = [(byte)i, 0x4a, 0x9c, 0xe7];
            expected.Enqueue(next.ToArray());
            uint sequence = f.Owner->NextSequence;
            HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, next, out _));
            Array.Fill(next, (byte)0xff);
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
            Last(f, sequence, expected.Last());
            HostCheck.Equal((byte)8, f.Owner->OutstandingSegments);
            HostCheck.Equal(32, f.Owner->InFlightBytes);
        }
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpRetransmission(f.Owner, new byte[1514], out _));
        Last(f, f.Owner->OldestUnacknowledgedSequence, expected.Peek());
    }

    [HostTest("tcp bounded flight restart window includes an actual transmission timestamp of zero")]
    private static void IdleRestartAtZero()
    {
        using var f = new Fixture(window: ushort.MaxValue);
        f.Hardware->Now = 0;
        var io = f.Operations();
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 1000 * Second));
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureBoundedFlight(f.Owner));
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcd"u8, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(f.Owner->NextSequence, out _, window: ushort.MaxValue));
        HostCheck.Equal(8U, f.Owner->CongestionWindowBytes);
        f.Hardware->Now = Second + 1;
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcdefgh"u8, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        HostCheck.Equal(4U, f.Owner->CongestionWindowBytes);
        HostCheck.Equal(Tx.Status.AwaitingAck, Tx.Pump(f.Owner, new byte[1514], out _));
    }
}
