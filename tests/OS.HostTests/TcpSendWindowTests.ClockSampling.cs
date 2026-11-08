using OS.Net;
using Tx = OS.Net.TcpSendWindow;
using Wire = TcpReceiveWindowTests;

internal static unsafe partial class TcpSendWindowTests
{
    [HostTest("tcp continuing idle pumps omit timer-only samples while finite lifetime pumps retain expiry checks")]
    private static void IdleClockSampling()
    {
        foreach (bool continuing in new[] { false, true })
        {
            using var f = new Fixture();
            var io = f.Operations();
            const ulong lifetime = 10_000_000_000;
            HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, continuing ? 0 : lifetime, continuing: continuing));
            HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(f.Owner, 30_000_000_000));
            uint before = f.Hardware->ClockCalls;
            byte[] output = new byte[1514];
            HostCheck.Equal(Tx.Status.NothingToSend, Tx.Pump(f.Owner, output, out _));
            HostCheck.Equal(Tx.Status.NothingToSend, Tx.PumpRetransmission(f.Owner, output, out _));
            HostCheck.Equal(Tx.Status.NothingToSend, Tx.PumpClose(f.Owner, output, out _));
            // Pump retains the shared owner's independent clock-health check;
            // only the adjacent retry/close timer-only calls may omit samples.
            HostCheck.Equal(continuing ? 1U : 3U, f.Hardware->ClockCalls - before);
            f.Hardware->Now = lifetime + 100;
            HostCheck.Equal(continuing ? Tx.Status.NothingToSend : Tx.Status.Timeout, Tx.Pump(f.Owner, output, out _));
            if (!continuing)
                continue;
            // A newly queued operation must use fresh time and retain the
            // copied-device callback's elapsed time in its RTO anchor.
            HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcd"u8, out _));
            f.Hardware->AdvanceOnSend = 2_000_000_000;
            before = f.Hardware->ClockCalls;
            HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out _));
            HostCheck.Equal(2U, f.Hardware->ClockCalls - before);
            HostCheck.Equal(f.Hardware->Now + 1_000_000_000, f.Owner->RetransmissionDeadlineNanoseconds);
        }
    }

    [HostTest("tcp original receive and ACK checks share one observation before callback-free parsing")]
    private static void ReceiveClockSampling()
    {
        using var f = new Fixture();
        var io = f.Operations();
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 100_000_000_000));
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(f.Owner, 30_000_000_000));
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcdefgh"u8, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, new byte[1514], out _));
        f.Hardware->Now += 200;
        ulong observed = f.Hardware->Now;
        f.Hardware->AdvanceOnClock = 100;
        uint before = f.Hardware->ClockCalls;
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 702, flags: 0x10), out _));
        HostCheck.Equal(1U, f.Hardware->ClockCalls - before);
        HostCheck.Equal(observed + f.Owner->RetransmissionTimeoutNanoseconds, f.Owner->RetransmissionDeadlineNanoseconds);
        HostCheck.Equal(2, f.Owner->InFlightBytes);
        f.Hardware->AdvanceOnClock = 0;
        f.Hardware->Now = observed - 1;
        HostCheck.Equal(Tx.Status.InvalidClock, f.Ack(704, out _));
    }

    [HostTest("tcp FIN retry shares only its initial observation and keeps original and post-transmit deadlines")]
    private static void FinClockSampling()
    {
        using var f = new Fixture();
        var io = f.Operations();
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 100_000_000_000));
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(f.Owner, 30_000_000_000));
        HostCheck.Equal(Tx.Status.Success, Tx.ShutdownWrite(f.Owner));
        byte[] output = new byte[1514];
        HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, output, out _));
        ulong deadline = f.Owner->RetransmissionDeadlineNanoseconds;
        f.Hardware->Now = deadline - 50;
        f.Hardware->AdvanceOnClock = 100;
        uint before = f.Hardware->ClockCalls;
        HostCheck.Equal(Tx.Status.NotDue, Tx.PumpClose(f.Owner, output, out _));
        HostCheck.Equal(1U, f.Hardware->ClockCalls - before);
        HostCheck.Equal(deadline, f.Owner->RetransmissionDeadlineNanoseconds);
        f.Hardware->AdvanceOnClock = 0;
        f.Hardware->AdvanceOnSend = 2_000_000_000;
        before = f.Hardware->ClockCalls;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, output, out _));
        HostCheck.Equal(2U, f.Hardware->ClockCalls - before);
        HostCheck.Equal(f.Hardware->Now + f.Owner->RetransmissionTimeoutNanoseconds, f.Owner->RetransmissionDeadlineNanoseconds);
    }
}
