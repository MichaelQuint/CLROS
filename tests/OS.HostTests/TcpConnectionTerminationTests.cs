using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Net;
using Tx = OS.Net.TcpSendWindow;
using Rx = OS.Net.TcpReceiveWindow;
using Tcb = OS.Net.TcpConnectionTable;
using Passive = OS.Net.TcpPassiveConnection;
using Fixture = TcpSendWindowTests.Fixture;
using Wire = TcpReceiveWindowTests;

internal static unsafe class TcpConnectionTerminationTests
{
    private const ulong Second = 1_000_000_000;

    [HostTest("net.tcp.termination active close drains copied writes before one FIN and clocked TimeWait", HostLane.Fast, 0)]
    private static void ActiveClose()
    {
        using var f = new Fixture();
        Configure(f);
        byte[] output = new byte[1514];
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "abcdefgh"u8, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.ShutdownWrite(f.Owner));
        HostCheck.Equal(Tx.Status.WriteClosed, Tx.Queue(f.Owner, "x"u8, out int copied));
        HostCheck.Equal(0, copied);
        HostCheck.Equal(Tx.Status.WaitingForDrain, Tx.PumpClose(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out _));
        Ack(f, 704);
        HostCheck.Equal(Tx.Status.WaitingForDrain, Tx.PumpClose(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out _));
        Ack(f, 708);
        HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, output, out var packet));
        CheckControl(output, packet, 708, 100, 0x11);
        HostCheck.True(f.Owner->FinOutstanding);
        HostCheck.Equal(709U, f.Owner->NextSequence);
        HostCheck.Equal(Tx.Status.InvalidAck, Receive(f, 100, 710, 0x10));
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 709, 0x11));
        HostCheck.Equal(TcpConnectionCloseState.TimeWait, f.Owner->CloseState);
        HostCheck.True(!f.Owner->FinOutstanding);
        HostCheck.True(f.Receiver->EndOfStream);
        HostCheck.Equal(Tx.Status.NotDue, Tx.PumpClose(f.Owner, output, out _));
        f.Hardware->Now = f.Owner->TimeWaitDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, Span<byte>.Empty, out _));
        Reap(f);
    }

    [HostTest("net.tcp.termination peer data FIN keeps reads and local writes through passive close", HostLane.Fast, 1)]
    private static void PassiveHalfClose()
    {
        using var f = new Fixture();
        Configure(f);
        byte[] output = new byte[1514], read = new byte[16];
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 700, 0x19, "hello"u8));
        HostCheck.Equal(TcpConnectionCloseState.CloseWait, f.Owner->CloseState);
        HostCheck.Equal(106U, f.Receiver->AcknowledgementNumber);
        HostCheck.True(!f.Receiver->EndOfStream);
        HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Receiver, read.AsSpan(0, 2), out var part));
        HostCheck.Equal(2, part.BytesRead);
        HostCheck.True(read.AsSpan(0, 2).SequenceEqual("he"u8));
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, "okay"u8, out _));
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out _));
        Ack(f, 704, sequence: 106);
        HostCheck.Equal(Tx.Status.Success, Tx.ShutdownWrite(f.Owner));
        HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, output, out var packet));
        CheckControl(output, packet, 704, 106, 0x11);
        HostCheck.Equal(TcpConnectionCloseState.LastAck, f.Owner->CloseState);
        HostCheck.Equal(Tx.Status.ConnectionClosed, Receive(f, 106, 705, 0x10));
        HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Receiver, read, out part));
        HostCheck.Equal(3, part.BytesRead);
        HostCheck.True(read.AsSpan(0, 3).SequenceEqual("llo"u8));
        HostCheck.Equal(Rx.Status.EndOfStream, Rx.Read(f.Receiver, read, out _));
        Reap(f, receiverFirst: true);
    }

    [HostTest("net.tcp.termination consumes FIN only after original payload fits receive window", HostLane.Fast, 2)]
    private static void FinWindow()
    {
        using var f = new Fixture(receiveCapacity: 4);
        Configure(f);
        byte[] read = new byte[8];
        HostCheck.Equal(Tx.Status.Success, Receive(f, 100, 700, 0x19, "abcdef"u8));
        HostCheck.Equal(104U, f.Receiver->AcknowledgementNumber);
        HostCheck.Equal(4, f.Receiver->UnreadBytes);
        HostCheck.Equal(TcpConnectionCloseState.Established, f.Owner->CloseState);
        HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Receiver, read, out var part));
        HostCheck.Equal(4, part.BytesRead);
        HostCheck.True(read.AsSpan(0, 4).SequenceEqual("abcd"u8));
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 700, 0x19, "abcdef"u8));
        HostCheck.Equal(107U, f.Receiver->AcknowledgementNumber);
        HostCheck.Equal(2, f.Receiver->UnreadBytes);
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 700, 0x19, "abcdef"u8));
        HostCheck.Equal(107U, f.Receiver->AcknowledgementNumber);
        HostCheck.Equal(2, f.Receiver->UnreadBytes);
        HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Receiver, read, out part));
        HostCheck.Equal(2, part.BytesRead);
        HostCheck.True(read.AsSpan(0, 2).SequenceEqual("ef"u8));
        HostCheck.True(f.Receiver->EndOfStream);
        HostCheck.Equal(Tx.Status.InvalidPacket, Receive(f, 107, 700, 0x18, "late"u8));
        HostCheck.Equal(0, f.Receiver->UnreadBytes);
    }

    [HostTest("net.tcp.termination simultaneous FIN and duplicate FIN keep exact sequences", HostLane.Fast, 3)]
    private static void Simultaneous()
    {
        using var f = new Fixture();
        Configure(f);
        byte[] output = new byte[1514];
        Tx.ShutdownWrite(f.Owner);
        HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 700, 0x11));
        HostCheck.Equal(TcpConnectionCloseState.Closing, f.Owner->CloseState);
        HostCheck.True(f.Owner->FinOutstanding);
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 700, 0x11));
        HostCheck.Equal(101U, f.Receiver->AcknowledgementNumber);
        HostCheck.Equal(Tx.Status.Success, Receive(f, 101, 701, 0x10));
        HostCheck.Equal(TcpConnectionCloseState.TimeWait, f.Owner->CloseState);
        ulong oldDeadline = f.Owner->TimeWaitDeadlineNanoseconds;
        f.Hardware->Now += Second;
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 700, 0x11));
        HostCheck.Equal(oldDeadline + Second, f.Owner->TimeWaitDeadlineNanoseconds);
        HostCheck.Equal(701U, f.Owner->NextSequence);
        HostCheck.Equal(101U, f.Receiver->AcknowledgementNumber);
    }

    [HostTest("net.tcp.termination FIN TX pressure loss retry exhaustion retains cleanup ownership", HostLane.Fast, 4)]
    private static void FinLoss()
    {
        using var f = new Fixture();
        Configure(f, retries: 2);
        byte[] output = new byte[1514];
        Tx.ShutdownWrite(f.Owner);
        HostCheck.Equal(Tx.Status.OutputTooSmall, Tx.PumpClose(f.Owner, output.AsSpan(0, 53), out _));
        f.Hardware->Result = 0;
        HostCheck.Equal(Tx.Status.WouldBlock, Tx.PumpClose(f.Owner, output, out _));
        HostCheck.Equal(700U, f.Owner->NextSequence);
        HostCheck.True(!f.Owner->FinOutstanding);
        f.Hardware->Result = 1;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, output, out _));
        for (int i = 0; i < 2; i++)
        {
            f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
            HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, output, out var packet));
            CheckControl(output, packet, 700, 100, 0x11);
            HostCheck.Equal(701U, f.Owner->NextSequence);
            HostCheck.Equal(0, f.Owner->BufferedBytes);
        }
        f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Timeout, Tx.PumpClose(f.Owner, output, out _));
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidTransition, Tcb.ReleaseOwned(f.Table, f.Handle));
        HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
        Reap(f);
    }

    [HostTest("net.tcp.termination exact peer reset challenges nonexact sequence and retains queued data", HostLane.Fast, 5)]
    private static void PeerReset()
    {
        using var f = new Fixture();
        Configure(f);
        byte[] output = new byte[1514];
        HostCheck.Equal(Tx.Status.Success, Receive(f, 100, 700, 0x18, "in"u8));
        Tx.Queue(f.Owner, "abcdefgh"u8, out _);
        Tx.Pump(f.Owner, output, out _);
        HostCheck.Equal(Tx.Status.InvalidPacket, Receive(f, 103, 0, 4));
        HostCheck.True(f.Receiver->HasPendingAck);
        byte[] bad = Wire.Packet(102, ReadOnlySpan<byte>.Empty, ack: 0, flags: 4);
        bad[50] ^= 1;
        HostCheck.Equal(Tx.Status.InvalidPacket, Tx.ReceiveFrame(f.Owner, bad, out _));
        HostCheck.Equal(TcpConnectionCloseState.Established, f.Owner->CloseState);
        HostCheck.Equal(Tx.Status.ConnectionReset, Receive(f, 102, 0, 4));
        HostCheck.Equal(TcpConnectionCloseState.Reset, f.Owner->CloseState);
        HostCheck.Equal(8, f.Owner->BufferedBytes);
        HostCheck.Equal(2, f.Receiver->UnreadBytes);
        HostCheck.Equal(Rx.Status.TableRejected, Rx.Read(f.Receiver, output, out _));
        Reap(f);
    }

    [HostTest("net.tcp.termination close deadline and clock failure require explicit abort before scrub", HostLane.Fast, 6)]
    private static void CloseFailures()
    {
        using (var f = new Fixture(window: 0))
        {
            Configure(f, closeLimit: 2 * Second);
            Tx.ShutdownWrite(f.Owner);
            HostCheck.Equal(Tx.Status.WindowClosed, Tx.PumpClose(f.Owner, new byte[1514], out _));
            f.Hardware->Now += 2 * Second;
            HostCheck.Equal(Tx.Status.Timeout, Tx.PumpClose(f.Owner, new byte[1514], out _));
            HostCheck.True(f.Owner->RequiresCleanup);
            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
            Reap(f);
        }
        using (var f = new Fixture())
        {
            Configure(f);
            Tx.ShutdownWrite(f.Owner);
            f.Hardware->Now--;
            HostCheck.Equal(Tx.Status.InvalidClock, Tx.PumpClose(f.Owner, new byte[1514], out _));
            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
            Reap(f);
        }
    }

    [HostTest("net.tcp.termination rejects aliases copied owners invalid options and repeat shutdown", HostLane.Fast, 7)]
    private static void Admission()
    {
        using var f = new Fixture();
        byte[] output = new byte[1514];
        HostCheck.Equal(Tx.Status.InvalidState, Tx.ConfigureClose(f.Owner, Second));
        var io = f.Operations();
        Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 1000 * Second);
        HostCheck.Equal(Tx.Status.InvalidOptions, Tx.ConfigureClose(f.Owner, 0));
        HostCheck.Equal(Tx.Status.InvalidOptions, Tx.ConfigureClose(f.Owner, Second, Second));
        HostCheck.Equal(Tx.Status.InvalidOptions, Tx.ConfigureClose(f.Owner, Second, 120 * Second));
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(f.Owner, 30 * Second));
        HostCheck.Equal(Tx.Status.InvalidState, Tx.ConfigureClose(f.Owner, 30 * Second));
        Tx.Owner copy = *f.Owner;
        HostCheck.Equal(Tx.Status.InvalidState, Tx.ReceiveFrame(&copy, output, out _));
        HostCheck.Equal(Tx.Status.InvalidStorage, Tx.ReceiveFrame(f.Owner, new ReadOnlySpan<byte>(f.Buffer, 54), out _));
        HostCheck.Equal(Tx.Status.Success, Tx.ShutdownWrite(f.Owner));
        HostCheck.Equal(Tx.Status.InvalidState, Tx.ShutdownWrite(f.Owner));
        HostCheck.Equal(Tx.Status.InvalidStorage, Tx.PumpClose(f.Owner, new Span<byte>(f.Buffer, 54), out _));
        HostCheck.Equal(700U, f.Owner->NextSequence);
    }

    [HostTest("net.tcp.termination FIN ACK pressure keeps EOF and current cumulative ACK pending", HostLane.Fast, 8)]
    private static void AckPressure()
    {
        using var f = new Fixture();
        Configure(f);
        byte[] output = new byte[1514];
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 700, 0x11));
        f.Hardware->Result = 0;
        HostCheck.Equal(Rx.Status.WouldBlock, Rx.PumpAck(f.Receiver, output, out _));
        HostCheck.True(f.Receiver->HasPendingAck);
        HostCheck.True(f.Receiver->EndOfStream);
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 700, 0x11));
        f.Hardware->Result = 1;
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Receiver, output, out var packet));
        CheckControl(output, packet, 700, 101, 0x10);
        HostCheck.True(!f.Receiver->HasPendingAck);
    }

    [HostTest("net.tcp.termination TimeWait ignores reset and restarts callback deadline beyond workload timeout", HostLane.Fast, 9)]
    private static void TimeWait()
    {
        using var f = new Fixture();
        Configure(f, lifetime: 10 * Second, closeLimit: 2 * Second);
        byte[] output = new byte[1514];
        Tx.ShutdownWrite(f.Owner);
        Tx.PumpClose(f.Owner, output, out _);
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 701, 0x11));
        ulong original = f.Owner->TimeWaitDeadlineNanoseconds;
        f.Hardware->Now = 5 * Second;
        HostCheck.Equal(Tx.Status.InvalidPacket, Receive(f, 101, 0, 4));
        HostCheck.Equal(TcpConnectionCloseState.TimeWait, f.Owner->CloseState);
        f.Hardware->Now = original - Second;
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 701, 0x11));
        HostCheck.Equal(f.Hardware->Now + 240 * Second, f.Owner->TimeWaitDeadlineNanoseconds);
        f.Hardware->Now = original;
        HostCheck.Equal(Tx.Status.NotDue, Tx.PumpClose(f.Owner, output, out _));
        f.Hardware->Now = f.Owner->TimeWaitDeadlineNanoseconds;
        HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, output, out _));
        Reap(f);
    }

    [HostTest("net.tcp.termination FIN sequence wraps and post-TX clock expiry retains actual progress", HostLane.Fast, 10)]
    private static void FinWrapAndClock()
    {
        using (var f = new Fixture(iss: uint.MaxValue - 1))
        {
            Configure(f);
            Tx.ShutdownWrite(f.Owner);
            byte[] output = new byte[1514];
            HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, output, out var packet));
            CheckControl(output, packet, uint.MaxValue, 100, 0x11);
            HostCheck.Equal(0U, f.Owner->NextSequence);
            HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 100, 0, 0x11));
            HostCheck.Equal(TcpConnectionCloseState.TimeWait, f.Owner->CloseState);
        }
        using (var f = new Fixture())
        {
            Configure(f, closeLimit: 2 * Second);
            Tx.ShutdownWrite(f.Owner);
            f.Hardware->AdvanceOnSend = 2 * Second;
            HostCheck.Equal(Tx.Status.Timeout, Tx.PumpClose(f.Owner, new byte[1514], out _));
            HostCheck.Equal(701U, f.Owner->NextSequence);
            HostCheck.True(f.Owner->FinOutstanding);
            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
            Reap(f);
        }
    }

    [HostTest("net.tcp.termination stale generations and old wire FIN cannot target recycled connection", HostLane.Fast, 11)]
    private static void Recycled()
    {
        using var f = new Fixture();
        Configure(f);
        var old = f.Handle;
        var info = new TcpEstablishedStreamIpv4FramePlannerInfo(new MacAddress(2, 0, 0, 0, 0, 1), new MacAddress(0x52, 0x55, 0x0a, 0, 2, 2),
            new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2), 1460, 32, 51, 0x272);
        Tx.AbortConnection(f.Owner);
        Reap(f);
        byte* memory = (byte*)NativeMemory.AlignedAlloc(16384, 4096);
        NativeMemory.Clear(memory, 16384);
        try
        {
            var handshake = (Passive.State*)memory;
            var rx = (Rx.Owner*)(memory + 512);
            var tx = (Tx.Owner*)(memory + 1024);
            byte[] output = new byte[1514];
            var io = f.Operations();
            HostCheck.Equal(Passive.Status.Success, Passive.Begin(handshake, f.Table, f.PassiveInfo(1699),
                Wire.Packet(199, ReadOnlySpan<byte>.Empty, ack: 0, flags: 2, window: 32), output, io, out _));
            HostCheck.Equal(Passive.Status.Success, Passive.ReceiveAck(handshake,
                Wire.Packet(200, ReadOnlySpan<byte>.Empty, ack: 1700, flags: 0x10, window: 32), new Span<TcpListenerBacklogEntry>(memory + 1536, 1), out _, out _));
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReadOwned(f.Table, handshake->Connection, out _));
            HostCheck.Equal(Rx.Status.Success, Rx.Initialize(rx, f.Table, handshake->Connection, memory + 4096, 32, info, io.Context, io.Send));
            HostCheck.Equal(Tx.Status.Success, Tx.Initialize(tx, rx, handshake, memory + 8192, 32, 1500));
            HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(tx, io.Context, io.NowNanoseconds, 1000 * Second));
            HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(tx, 30 * Second));
            byte[] oldFin = Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 701, flags: 0x11, window: 32);
            HostCheck.Equal(Tx.Status.InvalidState, Tx.ReceiveFrame(f.Owner, oldFin, out _));
            HostCheck.Equal(Tx.Status.InvalidPacket, Tx.ReceiveFrame(tx, oldFin, out _));
            HostCheck.Equal(200U, rx->AcknowledgementNumber);
            HostCheck.Equal(1700U, tx->NextSequence);
            HostCheck.Equal(Tcb.OwnershipStatus.StaleHandle, Tcb.ReadOwned(f.Table, old, out _));
            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(tx));
            var handle = handshake->Connection;
            HostCheck.Equal(Tx.Status.Success, Tx.ReleaseClosed(tx));
            HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(rx));
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(f.Table, handle));
        }
        finally
        {
            NativeMemory.AlignedFree(memory);
        }
    }

    [HostTest("net.tcp.termination warmed duplicate FIN and copied ACK dispatch allocates no managed bytes", HostLane.Fast, 12)]
    private static void Allocation()
    {
        using var f = new Fixture();
        Configure(f);
        byte[] output = new byte[1514];
        byte[] fin = Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 700, flags: 0x11, window: 32);
        Cycle(f, fin, output);
        Cycle(f, fin, output);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            Cycle(f, fin, output);
        HostCheck.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [HostTest("net.tcp.termination old cumulative ACK may carry new data FIN without releasing sends", HostLane.Fast, 13)]
    private static void OldAckData()
    {
        using var f = new Fixture();
        Configure(f);
        byte[] output = new byte[1514];
        Tx.Queue(f.Owner, "abcd"u8, out _);
        Tx.Pump(f.Owner, output, out _);
        Ack(f, 704);
        HostCheck.Equal(Tx.Status.Success, Receive(f, 100, 700, 0x18, "xy"u8));
        HostCheck.Equal(102U, f.Receiver->AcknowledgementNumber);
        HostCheck.Equal(704U, f.Owner->OldestUnacknowledgedSequence);
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, 102, 700, 0x11));
        HostCheck.Equal(103U, f.Receiver->AcknowledgementNumber);
        HostCheck.Equal(2, f.Receiver->UnreadBytes);
        HostCheck.Equal(0, f.Owner->BufferedBytes);
        HostCheck.Equal(704U, f.Owner->NextSequence);
        HostCheck.Equal(Tx.Status.InvalidAck, Receive(f, 103, 705, 0x18, "bad"u8));
        HostCheck.Equal(2, f.Receiver->UnreadBytes);
    }

    [HostTest("net.tcp.termination fatal FIN or ACK TX retains ownership for explicit abort", HostLane.Fast, 14)]
    private static void FatalTx()
    {
        using (var f = new Fixture())
        {
            Configure(f);
            Tx.ShutdownWrite(f.Owner);
            f.Hardware->Result = 2;
            HostCheck.Equal(Tx.Status.SendFailed, Tx.PumpClose(f.Owner, new byte[1514], out _));
            HostCheck.Equal(700U, f.Owner->NextSequence);
            HostCheck.True(!f.Owner->FinOutstanding);
            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
            Reap(f);
        }
        using (var f = new Fixture())
        {
            Configure(f);
            Tx.ShutdownWrite(f.Owner);
            byte[] output = new byte[1514];
            Tx.PumpClose(f.Owner, output, out _);
            f.Hardware->Now = f.Owner->RetransmissionDeadlineNanoseconds;
            f.Hardware->Result = 2;
            HostCheck.Equal(Tx.Status.SendFailed, Tx.PumpClose(f.Owner, output, out _));
            HostCheck.True(f.Owner->FinOutstanding);
            HostCheck.Equal(701U, f.Owner->NextSequence);
            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
            Reap(f);
        }
        using (var f = new Fixture())
        {
            Configure(f);
            Receive(f, 100, 700, 0x11);
            f.Hardware->Result = 2;
            HostCheck.Equal(Rx.Status.SendFailed, Rx.PumpAck(f.Receiver, new byte[1514], out _));
            HostCheck.True(f.Receiver->RequiresCleanup);
            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
            Reap(f, receiverFirst: true);
        }
    }

    [HostTest("net.tcp.termination original handshake peer FIN wraps receive sequence through zero", HostLane.Fast, 15)]
    private static void RemoteWrap()
    {
        using var f = new Fixture(setup: false);
        byte[] output = new byte[1514];
        var io = f.Operations();
        HostCheck.Equal(Passive.Status.Success, Passive.Begin(f.Handshake, f.Table, f.PassiveInfo(),
            Wire.Packet(uint.MaxValue - 1, ReadOnlySpan<byte>.Empty, ack: 0, flags: 2, window: 32), output, io, out _));
        byte* backlog = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        NativeMemory.Clear(backlog, 4096);
        try
        {
            HostCheck.Equal(Passive.Status.Success, Passive.ReceiveAck(f.Handshake,
                Wire.Packet(uint.MaxValue, ReadOnlySpan<byte>.Empty, ack: 700, flags: 0x10, window: 32),
                new Span<TcpListenerBacklogEntry>(backlog, 1), out _, out _));
        }
        finally
        {
            NativeMemory.AlignedFree(backlog);
        }
        var info = new TcpEstablishedStreamIpv4FramePlannerInfo(new MacAddress(2, 0, 0, 0, 0, 1), new MacAddress(0x52, 0x55, 0x0a, 0, 2, 2),
            new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2), 1460, 32, 51, 0x272);
        HostCheck.Equal(Rx.Status.Success, Rx.Initialize(f.Receiver, f.Table, f.Handle, f.ReceiveBuffer, 32, info, io.Context, io.Send));
        HostCheck.Equal(Tx.Status.Success, f.Bind(f.Owner, f.Buffer));
        Configure(f);
        HostCheck.Equal(Tx.Status.PeerClosed, Receive(f, uint.MaxValue, 700, 0x11));
        HostCheck.Equal(0U, f.Receiver->AcknowledgementNumber);
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Receiver, output, out var packet));
        CheckControl(output, packet, 700, 0, 0x10);
        Tx.ShutdownWrite(f.Owner);
        Tx.PumpClose(f.Owner, output, out _);
        HostCheck.Equal(Tx.Status.ConnectionClosed, Receive(f, 0, 701, 0x10));
        Reap(f);
    }

    [HostTest("net.tcp.termination unrepresentable TimeWait never saturates into early completion", HostLane.Fast, 16)]
    private static void TimeWaitOverflow()
    {
        using var f = new Fixture();
        f.Hardware->Now = ulong.MaxValue - 10 * Second;
        Configure(f, lifetime: 5 * Second, closeLimit: 2 * Second);
        Tx.ShutdownWrite(f.Owner);
        HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, new byte[1514], out _));
        HostCheck.Equal(Tx.Status.InvalidClock, Receive(f, 100, 701, 0x11));
        HostCheck.True(f.Owner->RequiresCleanup);
        HostCheck.Equal(0UL, f.Owner->TimeWaitDeadlineNanoseconds);
        HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
        Reap(f);
    }

    private static void Cycle(Fixture f, byte[] fin, byte[] output)
    {
        if (Tx.ReceiveFrame(f.Owner, fin, out _) != Tx.Status.PeerClosed || Rx.PumpAck(f.Receiver, output, out _) != Rx.Status.Success)
            throw new InvalidOperationException("duplicate FIN/ACK dispatch failed");
    }

    [HostTest("net.tcp.termination separates admitted left overlap from right-edge clipping", HostLane.Fast, 17)]
    private static void OverlapAndClipping()
    {
        using var f = new Fixture(receiveCapacity: 8);
        Configure(f);
        byte[] read = new byte[8];
        HostCheck.Equal(Tx.Status.Success, Receive(f, 100, 700, 0x18, "abcdefgh"u8));
        HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Receiver, read.AsSpan(0, 3), out var part));
        HostCheck.True(part.BytesRead == 3 && read.AsSpan(0, 3).SequenceEqual("abc"u8));
        HostCheck.Equal((ushort)3, f.Receiver->AvailableWindow);
        HostCheck.Equal(Tx.Status.InvalidPacket, Receive(f, 104, 700, 0x18, "efghijkl"u8));
        HostCheck.True(f.Receiver->UnreadBytes == 5 && f.Receiver->AcknowledgementNumber == 108);
        HostCheck.Equal(Tx.Status.Success, Receive(f, 104, 700, 0x18, "efghij"u8));
        HostCheck.True(f.Receiver->AvailableWindow == 1 && f.Receiver->AcknowledgementNumber == 110);
        HostCheck.Equal(Tx.Status.Success, Receive(f, 110, 700, 0x18, "kl"u8));
        HostCheck.True(f.Receiver->AvailableWindow == 0 && f.Receiver->AcknowledgementNumber == 111);
        HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Receiver, read, out part));
        HostCheck.True(part.BytesRead == 8 && read.AsSpan().SequenceEqual("defghijk"u8));
        HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
        Reap(f);
    }

    private static void Configure(Fixture f, byte retries = 5, ulong lifetime = 1000 * Second, ulong closeLimit = 30 * Second)
    {
        var io = f.Operations();
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, lifetime, maximumRetransmissions: retries));
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(f.Owner, closeLimit));
    }

    private static Tx.Status Receive(Fixture f, uint sequence, uint ack, byte flags, ReadOnlySpan<byte> payload = default) =>
        Tx.ReceiveFrame(f.Owner, Wire.Packet(sequence, payload, ack: ack, flags: flags, window: 32), out _);

    private static void Ack(Fixture f, uint ack, uint sequence = 100) => HostCheck.Equal(Tx.Status.Success, Receive(f, sequence, ack, 0x10));

    private static void CheckControl(byte[] output, TcpEstablishedStreamIpv4FramePlanResult packet, uint sequence, uint ack, byte flags)
    {
        HostCheck.Equal(54, packet.BytesWritten);
        HostCheck.Equal(flags, output[47]);
        HostCheck.Equal(sequence, BinaryPrimitives.ReadUInt32BigEndian(output.AsSpan(38, 4)));
        HostCheck.Equal(ack, BinaryPrimitives.ReadUInt32BigEndian(output.AsSpan(42, 4)));
        HostCheck.Equal((ushort)0xffff, Wire.Sum(output.AsSpan(14, 20)));
        HostCheck.Equal((ushort)0xffff, Wire.Fold(Wire.Sum(output.AsSpan(26, 8)) + 6U + 20U + Wire.Sum(output.AsSpan(34, 20))));
    }

    private static void Reap(Fixture f, bool receiverFirst = false)
    {
        var handle = f.Handle;
        HostCheck.Equal(Tcb.OwnershipStatus.AttachmentsRemain, Tcb.ReleaseOwned(f.Table, handle));
        if (receiverFirst)
            HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(f.Receiver));
        HostCheck.Equal(Tx.Status.Success, Tx.ReleaseClosed(f.Owner));
        if (!receiverFirst)
            HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(f.Receiver));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(f.Table, handle));
        HostCheck.Equal(Tcb.OwnershipStatus.StaleHandle, Tcb.ReleaseOwned(f.Table, handle));
        HostCheck.Equal(Tx.Status.InvalidState, Tx.ReleaseClosed(f.Owner));
        HostCheck.Equal(Rx.Status.InvalidState, Rx.ReleaseClosed(f.Receiver));
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 32).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.True(new ReadOnlySpan<byte>(f.ReceiveBuffer, f.Receiver->ReceiveCapacity).IndexOfAnyExcept((byte)0) < 0);
    }
}
