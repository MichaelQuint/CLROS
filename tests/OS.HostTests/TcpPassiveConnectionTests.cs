using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Net;
using Passive = OS.Net.TcpPassiveConnection;
using Tcb = OS.Net.TcpConnectionTable;

internal static unsafe class TcpPassiveConnectionTests
{
    private const uint Iss = 0xa0b0c0d0, Irs = 0x10203040;

    [HostTest("net.tcp.passive.owner bad ACK preserves pending lifetime then good ACK establishes", HostLane.Fast, 0)]
    private static void AckRecovery()
    {
        using var f = new Fixture(); byte[] output = new byte[54];
        HostCheck.Equal(Passive.Status.Success, f.Begin(f.State, output));
        var handle = f.State->Connection; ulong deadline = f.State->DeadlineNanoseconds;
        HostCheck.Equal(1, f.Table->Count); CheckReply(f.LastFrame(), Iss, unchecked(Irs + 1));
        byte[][] bad = [Packet(Irs + 1, Iss + 2, 0x10), Packet(Irs + 2, Iss + 1, 0x10),
            Packet(Irs + 1, Iss + 1, 0x14), Packet(Irs + 1, Iss + 1, 0x10, port: 40001), new byte[53]];
        byte[] checksum = Packet(Irs + 1, Iss + 1, 0x10); checksum[50] ^= 1;
        byte[] foreign = Packet(Irs + 1, Iss + 1, 0x10); foreign[0] ^= 2;
        byte[] fragment = Packet(Irs + 1, Iss + 1, 0x10, fragment: 0x2000);
        foreach (byte[] frame in bad.Concat(new[] { checksum, foreign, fragment }))
        {
            HostCheck.Equal(Passive.Status.InvalidPacket, Passive.ReceiveAck(f.State, frame, f.Backlog(), out _, out _));
            HostCheck.True(f.State->Pending); HostCheck.Equal(deadline, f.State->DeadlineNanoseconds);
            CheckEntry(f.Table, handle, TcpConnectionLifecycleState.SynReceived, Iss + 1, Irs + 1);
            HostCheck.Equal(TcpListenerBacklogStatus.BacklogEmpty, TcpListenerBacklog.Accept(f.Backlog()).Status);
        }
        HostCheck.Equal(Passive.Status.Success, Passive.ReceiveAck(f.State, Packet(Irs + 1, Iss + 1, 0x10),
            f.Backlog(), out var received, out var admitted));
        HostCheck.True(received.Succeeded && admitted.Succeeded && f.State->Established);
        CheckEntry(f.Table, handle, TcpConnectionLifecycleState.Established, Iss + 1, Irs + 1);
        HostCheck.Equal(Passive.Status.InvalidState, Passive.CancelPending(f.State));
        HostCheck.Equal(Passive.Status.InvalidState, Passive.Pump(f.State, output, out _));
        var accepted = TcpListenerBacklog.Accept(f.Backlog()); HostCheck.True(accepted.Succeeded);
        HostCheck.Equal(Irs + 1, accepted.Connection.RemoteNextSequenceNumber);
    }

    [HostTest("net.tcp.passive.owner backlog pressure and duplicate tuple never establish by inference", HostLane.Fast, 1)]
    private static void BacklogPressure()
    {
        using var f = new Fixture(2); byte[] output = new byte[54];
        var occupied = new TcpListenerBacklogConnection(LocalAddress(), 8080, RemoteAddress(), 41000, 11, 12);
        HostCheck.True(TcpListenerBacklog.Enqueue(f.Backlog(), occupied).Succeeded);
        HostCheck.Equal(Passive.Status.Success, f.Begin(f.State, output));
        byte[] ack = Packet(Irs + 1, Iss + 1, 0x10);
        HostCheck.Equal(Passive.Status.BacklogRejected, Passive.ReceiveAck(f.State, ack, f.Backlog(), out _, out var result));
        HostCheck.Equal(TcpListenerBacklogStatus.BacklogFull, result.BacklogStatus);
        HostCheck.True(f.State->Pending); HostCheck.Equal(1100UL, f.State->DeadlineNanoseconds);
        HostCheck.Equal((ushort)41000, TcpListenerBacklog.Accept(f.Backlog()).Connection.RemotePort);
        // An endpoint match in the backlog is not proof of this table generation.
        var duplicate = new TcpListenerBacklogConnection(LocalAddress(), 8080, RemoteAddress(), 40000, Iss + 1, Irs + 1);
        HostCheck.True(TcpListenerBacklog.Enqueue(f.Backlog(), duplicate).Succeeded);
        HostCheck.Equal(Passive.Status.BacklogRejected, Passive.ReceiveAck(f.State, ack, f.Backlog(), out _, out result));
        HostCheck.Equal(TcpListenerBacklogStatus.DuplicateEndpoint, result.BacklogStatus);
        HostCheck.True(f.State->Pending && TcpListenerBacklog.Accept(f.Backlog()).Succeeded);
        HostCheck.Equal(Passive.Status.Success, Passive.ReceiveAck(f.State, ack, f.Backlog(), out _, out result));
        HostCheck.True(result.Succeeded && f.State->Established);
        HostCheck.Equal(Passive.Status.Success, f.Begin(f.Other, output, remotePort: 40001));
        byte[] otherAck = Packet(Irs + 1, Iss + 1, 0x10, port: 40001);
        HostCheck.Equal(Passive.Status.BacklogRejected, Passive.ReceiveAck(f.Other, otherAck, f.Backlog(), out _, out result));
        HostCheck.Equal(TcpListenerBacklogStatus.BacklogFull, result.BacklogStatus);
        CheckEntry(f.Table, f.Other->Connection, TcpConnectionLifecycleState.SynReceived, Iss + 1, Irs + 1);
        HostCheck.Equal(Passive.Status.InvalidState, Passive.ReceiveAck(f.State, ack, f.Backlog(), out _, out _));
        HostCheck.Equal((ushort)40000, TcpListenerBacklog.Accept(f.Backlog()).Connection.RemotePort);
        HostCheck.Equal(Passive.Status.Success, Passive.ReceiveAck(f.Other, otherAck, f.Backlog(), out _, out result));
        HostCheck.Equal((ushort)40001, TcpListenerBacklog.Accept(f.Backlog()).Connection.RemotePort);
        HostCheck.Equal(2, f.Table->Count);
        HostCheck.Equal(TcpListenerBacklogStatus.BacklogEmpty, TcpListenerBacklog.Accept(f.Backlog()).Status);
    }

    [HostTest("net.tcp.passive.owner duplicate SYN and due retry retain byte-identical wrapped sequences", HostLane.Fast, 2)]
    private static void DuplicateAndRetry()
    {
        using var f = new Fixture(); byte[] output = new byte[54], syn = Packet(uint.MaxValue, 0, 2);
        HostCheck.Equal(Passive.Status.Success, f.Begin(f.State, output, uint.MaxValue, uint.MaxValue));
        byte[] original = output.ToArray(); CheckReply(original, uint.MaxValue, 0);
        CheckEntry(f.Table, f.State->Connection, TcpConnectionLifecycleState.SynReceived, 0, 0);
        f.Hardware->Now = 150;
        HostCheck.Equal(Passive.Status.Success, Passive.ReceiveSyn(f.State, syn, output, out _));
        HostCheck.True(output.AsSpan().SequenceEqual(original));
        HostCheck.Equal((byte)1, f.State->SuccessfulAttempts); HostCheck.Equal(1UL, f.State->DuplicateReplies);
        HostCheck.Equal(200UL, f.State->NextRetryNanoseconds);
        HostCheck.Equal(Passive.Status.InvalidPacket, Passive.ReceiveSyn(f.State, Packet(uint.MaxValue - 1, 0, 2), output, out var rejected));
        HostCheck.Equal(TcpPassiveIpv4FramePlanStatus.WrongSequenceNumber, rejected.Status);
        HostCheck.True(f.State->Pending); HostCheck.Equal(2U, f.Hardware->Sends);
        f.Hardware->Now = 200;
        HostCheck.Equal(Passive.Status.Success, Passive.Pump(f.State, output, out _));
        HostCheck.True(output.AsSpan().SequenceEqual(original)); HostCheck.Equal((byte)2, f.State->SuccessfulAttempts);
        HostCheck.Equal(Passive.Status.NotDue, Passive.Pump(f.State, output, out _));
        f.Hardware->Now = 300; f.Hardware->SendResult = 0;
        HostCheck.Equal(Passive.Status.WouldBlock, Passive.Pump(f.State, output, out _));
        uint calls = f.Hardware->Calls;
        HostCheck.Equal(Passive.Status.OutputTooSmall, Passive.Pump(f.State, output.AsSpan(0, 53), out _));
        HostCheck.Equal(calls, f.Hardware->Calls); HostCheck.Equal(300UL, f.State->NextRetryNanoseconds);
        HostCheck.Equal((byte)2, f.State->SuccessfulAttempts);
        f.Hardware->Now = 350; f.Hardware->SendResult = 1;
        HostCheck.Equal(Passive.Status.Success, Passive.Pump(f.State, output, out _));
        f.Hardware->Now = 450;
        HostCheck.Equal(Passive.Status.RetryLimit, Passive.Pump(f.State, output, out _));
        f.Hardware->Now = 500;
        HostCheck.Equal(Passive.Status.Success, Passive.ReceiveSyn(f.State, syn, output, out _));
        HostCheck.Equal((byte)3, f.State->SuccessfulAttempts); HostCheck.Equal(450UL, f.State->NextRetryNanoseconds);
        HostCheck.Equal(1100UL, f.State->DeadlineNanoseconds);
        f.Hardware->Now = 1099;
        HostCheck.Equal(Passive.Status.Success, Passive.ReceiveAck(f.State, Packet(0, 0, 0x10), f.Backlog(), out _, out _));
        HostCheck.True(f.State->Established); CheckReply(f.LastFrame(), uint.MaxValue, 0);
    }

    [HostTest("net.tcp.passive.owner full initial TX cannot admit ACK or renew hard timeout", HostLane.Fast, 3)]
    private static void InitialPressureAndTimeout()
    {
        using var f = new Fixture(); byte[] output = new byte[54]; f.Hardware->SendResult = 0;
        HostCheck.Equal(Passive.Status.WouldBlock, f.Begin(f.State, output));
        HostCheck.True(f.State->Pending); var stale = f.State->Connection;
        HostCheck.Equal((byte)0, f.State->SuccessfulAttempts); HostCheck.Equal(1, f.Table->Count);
        f.Hardware->Now = 500;
        HostCheck.Equal(Passive.Status.NotSent, Passive.ReceiveAck(f.State, Packet(Irs + 1, Iss + 1, 0x10), f.Backlog(), out _, out _));
        HostCheck.Equal(Passive.Status.WouldBlock, Passive.Pump(f.State, output, out _));
        HostCheck.Equal(Passive.Status.WouldBlock, Passive.ReceiveSyn(f.State, Packet(Irs, 0, 2), output, out _));
        HostCheck.Equal(100UL, f.State->NextRetryNanoseconds); HostCheck.Equal(1100UL, f.State->DeadlineNanoseconds);
        uint calls = f.Hardware->Calls; f.Hardware->Now = 1100;
        HostCheck.Equal(Passive.Status.Timeout, Passive.ReceiveSyn(f.State, Packet(Irs, 0, 2), Span<byte>.Empty, out _));
        HostCheck.True(f.State->Retired && !f.State->Connection.IsInitialized);
        HostCheck.Equal(calls, f.Hardware->Calls); HostCheck.Equal(0, f.Table->Count);
        HostCheck.Equal(Tcb.OwnershipStatus.StaleHandle, Tcb.ReadOwned(f.Table, stale, out _));
        HostCheck.True(new ReadOnlySpan<byte>(f.Entries, sizeof(TcpConnectionTableEntry)).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.Equal(Passive.Status.InvalidStorage, f.Begin(f.State, output));
        f.Hardware->SendResult = 1;
        HostCheck.Equal(Passive.Status.Success, f.Begin(f.Other, output));
        HostCheck.Equal(Tcb.OwnershipStatus.StaleHandle, Tcb.ReadOwned(f.Table, stale, out _));
        HostCheck.Equal(Passive.Status.Success, Passive.CancelPending(f.Other));
    }

    [HostTest("net.tcp.passive.owner fatal TX retains cleanup and invalid clocks preserve metadata", HostLane.Fast, 4)]
    private static void Failures()
    {
        using var f = new Fixture(); byte[] output = new byte[54];
        f.Hardware->Now = ulong.MaxValue - 500;
        HostCheck.Equal(Passive.Status.InvalidClock, f.Begin(f.State, output)); HostCheck.Equal(0, f.Table->Count);
        f.Hardware->Now = 100; f.Hardware->SendResult = 2;
        HostCheck.Equal(Passive.Status.SendFailed, f.Begin(f.State, output));
        HostCheck.True(f.State->RequiresCleanup); HostCheck.Equal(1, f.Table->Count);
        HostCheck.Equal(Passive.Status.InvalidState, Passive.Pump(f.State, output, out _));
        HostCheck.Equal(Passive.Status.Success, Passive.CancelPending(f.State)); HostCheck.Equal(0, f.Table->Count);
        f.Hardware->SendResult = 1;
        HostCheck.Equal(Passive.Status.Success, f.Begin(f.Other, output));
        f.Hardware->Now = 99;
        HostCheck.Equal(Passive.Status.InvalidClock, Passive.ReceiveAck(f.Other, Packet(Irs + 1, Iss + 1, 0x10), f.Backlog(), out _, out _));
        HostCheck.True(f.Other->Pending); HostCheck.Equal(200UL, f.Other->NextRetryNanoseconds);
        f.Hardware->Now = 101;
        HostCheck.Equal(Passive.Status.Success, Passive.ReceiveAck(f.Other, Packet(Irs + 1, Iss + 1, 0x10), f.Backlog(), out _, out _));
    }

    [HostTest("net.tcp.passive.owner storage aliases copies stale generations and capacity fail closed", HostLane.Fast, 5)]
    private static void StorageAndIdentity()
    {
        using var f = new Fixture(); byte[] output = new byte[54], syn = Packet(Irs, 0, 2);
        HostCheck.Equal(Passive.Status.InvalidStorage, Passive.Begin(f.State, f.Table, Info(), syn, syn, f.Io(), out _));
        HostCheck.Equal(Passive.Status.InvalidStorage, f.Begin((Passive.State*)f.Table, output));
        HostCheck.Equal(Passive.Status.Success, f.Begin(f.State, output));
        Passive.State copy = *f.State;
        HostCheck.Equal(Passive.Status.InvalidState, Passive.Pump(&copy, output, out _));
        HostCheck.Equal(Passive.Status.InvalidStorage, Passive.ReceiveSyn(f.State, syn, new Span<byte>(f.State, 54), out _));
        HostCheck.Equal(Passive.Status.InvalidStorage, Passive.ReceiveSyn(f.State, new ReadOnlySpan<byte>(f.State, 54), output, out _));
        HostCheck.Equal(Passive.Status.InvalidStorage, Passive.ReceiveAck(f.State, Packet(Irs + 1, Iss + 1, 0x10),
            new Span<TcpListenerBacklogEntry>(f.State, 1), out _, out _));
        HostCheck.Equal(Passive.Status.TableRejected, f.Begin(f.Other, output)); // Same tuple, same live generation.
        HostCheck.Equal(Passive.Status.TableRejected, f.Begin(f.Other, output, remotePort: 40001)); // Table capacity is one.
        var old = f.State->Connection;
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.TransitionOwned(f.Table, old, TcpConnectionLifecycleState.SynReceived, TcpConnectionLifecycleState.Closed));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(f.Table, old));
        HostCheck.Equal(Passive.Status.Success, f.Begin(f.Other, output));
        uint calls = f.Hardware->Calls;
        HostCheck.Equal(Passive.Status.TableRejected, Passive.Pump(f.State, output, out _));
        HostCheck.Equal(calls, f.Hardware->Calls); HostCheck.True(f.Other->Pending);
        HostCheck.Equal(Passive.Status.Success, Passive.CancelPending(f.Other));
    }

    [HostTest("net.tcp.passive.owner rejects invalid options fragments and multicast before TX", HostLane.Fast, 6)]
    private static void AdmissionGuards()
    {
        using var f = new Fixture(); byte[] output = new byte[54], syn = Packet(Irs, 0, 2);
        for (int i = 0; i < 8; i++)
        {
            var io = f.Io();
            switch (i) { case 0: io.MaximumAttempts = 0; break; case 1: io.MaximumAttempts = 9; break;
                case 2: io.LifetimeNanoseconds = 60_000_000_001; break; case 3: io.RetryNanoseconds = 0; break;
                case 4: io.RetryNanoseconds = 1001; break; case 5: io.NowNanoseconds = null; break; case 6: io.Send = null; break;
                case 7: io.LifetimeNanoseconds = 0; break; }
            HostCheck.Equal(Passive.Status.InvalidOptions, Passive.Begin(f.State, f.Table, Info(), syn, output, io, out _));
        }
        foreach (ushort fragment in new ushort[] { 0x2000, 1, 0x8000 })
            HostCheck.Equal(Passive.Status.InvalidPacket, Passive.Begin(f.State, f.Table, Info(), Packet(Irs, 0, 2, fragment: fragment), output, f.Io(), out _));
        var multicast = new TcpPassiveIpv4FramePlannerInfo(new MacAddress(3, 0, 0, 0, 0, 1), RemoteMac(), LocalAddress(), RemoteAddress(), 8080, 40000, Iss);
        HostCheck.Equal(Passive.Status.InvalidPacket, Passive.Begin(f.State, f.Table, multicast, syn, output, f.Io(), out _));
        HostCheck.Equal(0U, f.Hardware->Calls); HostCheck.Equal(0, f.Table->Count);
    }

    [HostTest("net.tcp.passive.owner caller storage and warmed lifecycle allocate no managed bytes", HostLane.Fast, 7)]
    private static void Allocation()
    {
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<Passive.State>() && !RuntimeHelpers.IsReferenceOrContainsReferences<Passive.Operations>());
        using var f = new Fixture(); byte[] output = new byte[54], syn = Packet(Irs, 0, 2); var info = Info(); var io = f.Io();
        for (int i = 0; i < 2; i++) { *f.State = default; Passive.Begin(f.State, f.Table, info, syn, output, io, out _); Passive.CancelPending(f.State); }
        long before = GC.GetAllocatedBytesForCurrentThread(); bool success = true;
        for (int i = 0; i < 10_000; i++)
        {
            *f.State = default;
            success &= Passive.Begin(f.State, f.Table, info, syn, output, io, out _) == Passive.Status.Success;
            success &= Passive.CancelPending(f.State) == Passive.Status.Success;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(success); HostCheck.Equal(0L, allocated); HostCheck.Equal(0, f.Table->Count);
    }

    private struct Hardware { public ulong Now; public uint Calls, Sends; public byte SendResult; public fixed byte Frame[1514]; }
    private sealed class Fixture : IDisposable
    {
        private readonly byte* memory;
        public Passive.State* State => (Passive.State*)memory;
        public Passive.State* Other => (Passive.State*)(memory + 1024);
        public Tcb.Owner* Table => (Tcb.Owner*)(memory + 2048);
        public TcpConnectionTableEntry* Entries => (TcpConnectionTableEntry*)(memory + 3072);
        public Hardware* Hardware => (Hardware*)(memory + 4096);
        public Span<TcpListenerBacklogEntry> Backlog() => new(memory + 6144, 1);
        public Fixture(uint capacity = 1)
        {
            memory = (byte*)NativeMemory.AlignedAlloc(8192, 4096); NativeMemory.Clear(memory, 8192);
            HostCheck.True(sizeof(Passive.State) <= 1024 && sizeof(Hardware) <= 2048 && capacity * sizeof(TcpConnectionTableEntry) <= 512);
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.InitializeOwned(Table, Entries, capacity, (ulong*)(memory + 3584), capacity, 71));
            Hardware->Now = 100; Hardware->SendResult = 1;
        }
        public Passive.Operations Io() => new() { Context = Hardware, NowNanoseconds = &Clock, Send = &Send,
            LifetimeNanoseconds = 1000, RetryNanoseconds = 100, MaximumAttempts = 3 };
        public Passive.Status Begin(Passive.State* state, byte[] output, uint iss = Iss, uint irs = Irs, ushort remotePort = 40000) =>
            Passive.Begin(state, Table, Info(iss, remotePort), Packet(irs, 0, 2, remotePort), output, Io(), out _);
        public byte[] LastFrame() => new ReadOnlySpan<byte>(Hardware->Frame, 54).ToArray();
        public void Dispose() => NativeMemory.AlignedFree(memory);
    }
    private static ulong Clock(void* context) => ((Hardware*)context)->Now;
    private static byte Send(void* context, byte* frame, uint count)
    {
        var hardware = (Hardware*)context; hardware->Calls++;
        if (hardware->SendResult != 1) return hardware->SendResult;
        if (count != 54) return 2;
        for (int i = 0; i < count; i++) hardware->Frame[i] = frame[i]; hardware->Sends++; return 1;
    }
    private static TcpPassiveIpv4FramePlannerInfo Info(uint iss = Iss, ushort port = 40000) =>
        new(new MacAddress(2, 0, 0, 0, 0, 1), RemoteMac(), LocalAddress(), RemoteAddress(), 8080, port, iss, 0x3456, 51, 0x270);
    private static MacAddress RemoteMac() => new(0x52, 0x55, 0x0a, 0, 2, 2);
    private static Ipv4Address LocalAddress() => new(10, 0, 2, 15);
    private static Ipv4Address RemoteAddress() => new(10, 0, 2, 2);
    private static void CheckEntry(Tcb.Owner* table, Tcb.Handle handle, TcpConnectionLifecycleState state, uint local, uint remote)
    {
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReadOwned(table, handle, out var entry));
        HostCheck.Equal(state, entry.State); HostCheck.Equal(local, entry.Sequence.LocalNextSequenceNumber);
        HostCheck.Equal(remote, entry.Sequence.RemoteNextSequenceNumber);
    }
    // Independent byte vectors/checksums: no production packet writer or checksum helper.
    private static byte[] Packet(uint sequence, uint ack, byte flags, ushort port = 40000, ushort fragment = 0x4000)
    {
        byte[] frame = new byte[54]; new byte[] { 2, 0, 0, 0, 0, 1, 0x52, 0x55, 0x0a, 0, 2, 2, 8, 0 }.CopyTo(frame, 0);
        frame[14] = 0x45; U16(frame, 16, 40); U16(frame, 18, 0x7251); U16(frame, 20, fragment); frame[22] = 64; frame[23] = 6;
        new byte[] { 10, 0, 2, 2, 10, 0, 2, 15 }.CopyTo(frame, 26);
        U16(frame, 24, (ushort)~Sum(frame.AsSpan(14, 20)));
        U16(frame, 34, port); U16(frame, 36, 8080); U32(frame, 38, sequence); U32(frame, 42, ack);
        frame[46] = 0x50; frame[47] = flags; U16(frame, 48, 0x2000);
        U16(frame, 50, (ushort)~Fold(Sum(frame.AsSpan(26, 8)) + 6U + 20 + Sum(frame.AsSpan(34, 20))));
        return frame;
    }
    private static void CheckReply(byte[] frame, uint iss, uint next)
    {
        HostCheck.Equal(54, frame.Length); HostCheck.Equal((ushort)0xffff, Sum(frame.AsSpan(14, 20)));
        HostCheck.Equal((ushort)0xffff, Fold(Sum(frame.AsSpan(26, 8)) + 6U + 20 + Sum(frame.AsSpan(34, 20))));
        HostCheck.Equal(iss, BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(38, 4)));
        HostCheck.Equal(next, BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(42, 4)));
        HostCheck.Equal((byte)0x12, frame[47]); HostCheck.Equal((ushort)0x3456, BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(48, 2)));
    }
    private static ushort Sum(ReadOnlySpan<byte> bytes)
    { uint sum = 0; for (int i = 0; i < bytes.Length; i += 2) sum += (uint)((bytes[i] << 8) | bytes[i + 1]); return Fold(sum); }
    private static ushort Fold(uint sum) { while (sum > 0xffff) sum = (sum & 0xffff) + (sum >> 16); return (ushort)sum; }
    private static void U16(byte[] bytes, int at, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(at, 2), value);
    private static void U32(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(at, 4), value);
}
