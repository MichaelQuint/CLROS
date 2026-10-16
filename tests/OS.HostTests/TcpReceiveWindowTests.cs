using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Net;
using Rx = OS.Net.TcpReceiveWindow;
using Tcb = OS.Net.TcpConnectionTable;

internal static unsafe class TcpReceiveWindowTests
{
    private const uint Local = 700, Remote = 100;

    [HostTest("net.tcp.receive.owner aggregates ACK and reads each buffered byte once", HostLane.Fast, 0)]
    private static void InOrder()
    {
        using var f = new Fixture(); byte[] ack = new byte[54];
        HostCheck.Equal(Rx.Status.Success, f.Receive(Remote, "abc"));
        HostCheck.Equal(Rx.Status.Success, f.Receive(Remote + 3, "de"));
        HostCheck.Equal(0U, f.Hardware->Calls); HostCheck.Equal(5, f.Owner->UnreadBytes);
        HostCheck.True(f.Owner->HasPendingAck);
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Owner, ack, out _)); CheckAck(ack, Local, Remote + 5, 3);
        HostCheck.Equal(Rx.Status.NothingToAck, Rx.PumpAck(f.Owner, ack, out _)); HostCheck.Equal(1U, f.Hardware->Calls);
        byte[] output = new byte[2];
        HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Owner, output, out var read));
        HostCheck.Equal(2, read.BytesRead); HostCheck.Equal("ab", Encoding.ASCII.GetString(output));
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 2).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Owner, ack, out _)); CheckAck(ack, Local, Remote + 5, 5);
        output = new byte[8]; HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Owner, output, out read));
        HostCheck.Equal(3, read.BytesRead); HostCheck.Equal("cde", Encoding.ASCII.GetString(output, 0, 3));
        HostCheck.Equal(Rx.Status.NoData, Rx.Read(f.Owner, output, out _));
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Owner, ack, out _)); CheckAck(ack, Local, Remote + 5, 8);
    }

    [HostTest("net.tcp.receive.owner trims old overlap and window excess while dropping gaps", HostLane.Fast, 1)]
    private static void OverlapAndGap()
    {
        using var f = new Fixture();
        HostCheck.Equal(Rx.Status.Success, f.Receive(Remote, "abcd"));
        byte[] overlap = Packet(Remote + 2, "cdefghijk"u8);
        HostCheck.Equal(Rx.Status.Success, Rx.Receive(f.Owner, overlap, out var received));
        HostCheck.Equal(4, received.PayloadBytes); HostCheck.Equal(Remote + 8, f.Owner->AcknowledgementNumber);
        HostCheck.Equal((ushort)0, f.Owner->AvailableWindow);
        byte[] output = new byte[8]; HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Owner, output, out _));
        HostCheck.Equal("abcdefgh", Encoding.ASCII.GetString(output));
        HostCheck.Equal(Rx.Status.Duplicate, f.Receive(Remote + 2, "cdef"));
        HostCheck.Equal(Rx.Status.OutOfOrder, f.Receive(Remote + 10, "kl")); HostCheck.Equal(0, f.Owner->UnreadBytes);
        HostCheck.Equal(Rx.Status.Success, f.Receive(Remote + 8, "ij"));
        HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Owner, output, out var read));
        HostCheck.Equal(2, read.BytesRead); HostCheck.Equal("ij", Encoding.ASCII.GetString(output, 0, 2));
        HostCheck.Equal(Remote + 10, f.Owner->AcknowledgementNumber); // The earlier gap was not queued.
    }

    [HostTest("net.tcp.receive.owner zero window probes and partial reopening retain cumulative ACK", HostLane.Fast, 2)]
    private static void ZeroWindow()
    {
        using var f = new Fixture(4); byte[] ack = new byte[54];
        HostCheck.Equal(Rx.Status.Success, Rx.Receive(f.Owner, Packet(Remote, "abcdef"u8), out var received));
        HostCheck.Equal(4, received.PayloadBytes); HostCheck.Equal(Remote + 4, f.Owner->AcknowledgementNumber);
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Owner, ack, out _)); CheckAck(ack, Local, Remote + 4, 0);
        HostCheck.Equal(Rx.Status.Duplicate, f.Receive(Remote + 3, "d"));
        HostCheck.Equal(Rx.Status.WindowFull, f.Receive(Remote + 4, "e"));
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Owner, ack, out _)); CheckAck(ack, Local, Remote + 4, 0);
        HostCheck.Equal(Rx.Status.NoPayload, Rx.Receive(f.Owner, Packet(Remote + 4, ReadOnlySpan<byte>.Empty, flags: 0x10), out _));
        HostCheck.True(!f.Owner->HasPendingAck); // An acceptable ACK never starts an ACK loop.
        HostCheck.Equal(Rx.Status.Duplicate, Rx.Receive(f.Owner, Packet(Remote + 3, ReadOnlySpan<byte>.Empty, flags: 0x10), out _));
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Owner, ack, out _));
        byte[] output = new byte[1]; HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Owner, output, out _));
        HostCheck.Equal((byte)'a', output[0]);
        HostCheck.Equal(Rx.Status.Success, Rx.Receive(f.Owner, Packet(Remote + 4, "ef"u8), out received));
        HostCheck.Equal(1, received.PayloadBytes); HostCheck.Equal(Remote + 5, f.Owner->AcknowledgementNumber);
        output = new byte[4]; HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Owner, output, out _));
        HostCheck.Equal("bcde", Encoding.ASCII.GetString(output));
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Owner, ack, out _)); CheckAck(ack, Local, Remote + 5, 4);
    }

    [HostTest("net.tcp.receive.owner overlap and cumulative ACK cross uint sequence wrap", HostLane.Fast, 3)]
    private static void Wrap()
    {
        using var f = new Fixture(remote: uint.MaxValue - 1); byte[] output = new byte[8], ack = new byte[54];
        HostCheck.Equal(Rx.Status.Success, f.Receive(uint.MaxValue - 1, "abcd"));
        HostCheck.Equal(2U, f.Owner->AcknowledgementNumber);
        HostCheck.Equal(Rx.Status.Duplicate, f.Receive(uint.MaxValue - 1, "ab"));
        HostCheck.Equal(Rx.Status.Success, f.Receive(uint.MaxValue, "bcdef"));
        HostCheck.Equal(4U, f.Owner->AcknowledgementNumber);
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Owner, ack, out _)); CheckAck(ack, Local, 4, 2);
        HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Owner, output, out var read));
        HostCheck.Equal(6, read.BytesRead); HostCheck.Equal("abcdef", Encoding.ASCII.GetString(output, 0, 6));
    }

    [HostTest("net.tcp.receive.owner full TX retains received data and latest window update", HostLane.Fast, 4)]
    private static void AckPressure()
    {
        using var f = new Fixture(); byte[] ack = new byte[54];
        HostCheck.Equal(Rx.Status.Success, f.Receive(Remote, "abc")); f.Hardware->SendResult = 0;
        HostCheck.Equal(Rx.Status.WouldBlock, Rx.PumpAck(f.Owner, ack, out var serialized));
        HostCheck.True(serialized.Succeeded && f.Owner->HasPendingAck); HostCheck.Equal(3, f.Owner->UnreadBytes);
        HostCheck.Equal(Rx.Status.Duplicate, f.Receive(Remote, "abc"));
        HostCheck.Equal(Rx.Status.Success, f.Receive(Remote + 3, "de"));
        byte[] read = new byte[2]; HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Owner, read, out _));
        uint calls = f.Hardware->Calls;
        HostCheck.Equal(Rx.Status.OutputTooSmall, Rx.PumpAck(f.Owner, ack.AsSpan(0, 53), out _));
        HostCheck.Equal(calls, f.Hardware->Calls);
        f.Hardware->SendResult = 1;
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Owner, ack, out _)); CheckAck(ack, Local, Remote + 5, 5);
        HostCheck.Equal(3, f.Owner->UnreadBytes); HostCheck.True(!f.Owner->HasPendingAck);
        read = new byte[8]; HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Owner, read, out var result));
        HostCheck.Equal(3, result.BytesRead); HostCheck.Equal("cde", Encoding.ASCII.GetString(read, 0, 3));
    }

    [HostTest("net.tcp.receive.owner rejects invalid packet identities checksums flags and fragments", HostLane.Fast, 5)]
    private static void InvalidPackets()
    {
        using var f = new Fixture();
        byte[] checksum = Packet(Remote, "x"u8); checksum[54] ^= 1;
        byte[] foreignMac = Packet(Remote, "x"u8); foreignMac[0] ^= 2;
        byte[] foreignIp = Packet(Remote, "x"u8); foreignIp[26] ^= 1;
        byte[] fragment = Packet(Remote, "x"u8, fragment: 0x2000);
        foreach (byte[] frame in new[] { checksum, foreignMac, foreignIp, fragment, Packet(Remote, "x"u8, port: 40001),
            Packet(Remote, "x"u8, ack: Local + 1), Packet(Remote, "x"u8, flags: 0x11), new byte[53], new byte[1515] })
        {
            HostCheck.Equal(Rx.Status.InvalidPacket, Rx.Receive(f.Owner, frame, out _));
            HostCheck.Equal(Remote, f.Owner->AcknowledgementNumber); HostCheck.Equal(0, f.Owner->UnreadBytes);
            HostCheck.True(!f.Owner->HasPendingAck);
        }
        HostCheck.Equal(0U, f.Hardware->Calls);
    }

    [HostTest("net.tcp.receive.owner unique binding and attachment survive table metadata updates", HostLane.Fast, 6)]
    private static void Attachment()
    {
        using var f = new Fixture();
        HostCheck.Equal(Rx.Status.TableRejected, f.Bind(f.Other, f.OtherBuffer));
        var legacy = new Span<TcpConnectionTableEntry>(f.Entries, 1);
        var key = new TcpConnectionKey(new Ipv4Address(10, 0, 2, 15), 8080, new Ipv4Address(10, 0, 2, 2), 40000);
        HostCheck.Equal(TcpConnectionTableStatus.InvalidEntryState, Tcb.MarkClosing(legacy, key).Status);
        HostCheck.Equal(Tcb.OwnershipStatus.InvalidTransition, Tcb.UpdateOwned(f.Table, f.Handle, new(Local, Remote + 1)));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.UpdateOwned(f.Table, f.Handle, new(Local + 5, Remote)));
        HostCheck.Equal(Rx.Status.Success, Rx.Receive(f.Owner, Packet(Remote, "abc"u8, ack: Local + 5), out _));
        byte[] ack = new byte[54]; HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Owner, ack, out _)); CheckAck(ack, Local + 5, Remote + 3, 5);
        HostCheck.Equal(Rx.Status.InvalidState, Rx.ReleaseClosed(f.Owner));
        f.Close();
        HostCheck.Equal(Tcb.OwnershipStatus.AttachmentsRemain, Tcb.ReleaseOwned(f.Table, f.Handle));
        HostCheck.Equal(TcpConnectionTableStatus.InvalidEntryState, Tcb.AddEstablished(legacy, key, new(Local, Remote)).Status);
        HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(f.Owner));
        HostCheck.True(f.Owner->Retired); HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 8).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(f.Table, f.Handle));
        HostCheck.Equal(Rx.Status.InvalidState, Rx.Read(f.Owner, ack, out _));
        HostCheck.Equal(Tcb.OwnershipStatus.StaleHandle, Tcb.ReadOwned(f.Table, f.Handle, out _));
    }

    [HostTest("net.tcp.receive.owner copied aliases stale and foreign generations cannot affect live receiver", HostLane.Fast, 7)]
    private static void IdentityAndStorage()
    {
        using var f = new Fixture(); byte[] output = new byte[54]; Rx.Owner copy = *f.Owner;
        HostCheck.Equal(Rx.Status.InvalidState, Rx.PumpAck(&copy, output, out _));
        HostCheck.Equal(Rx.Status.InvalidStorage, Rx.Receive(f.Owner, new ReadOnlySpan<byte>(f.Buffer, 54), out _));
        HostCheck.Equal(Rx.Status.InvalidStorage, Rx.Read(f.Owner, new Span<byte>(f.Owner, 1), out _));
        HostCheck.Equal(Rx.Status.Success, f.Receive(Remote, "x"));
        HostCheck.Equal(Rx.Status.InvalidStorage, Rx.PumpAck(f.Owner, new Span<byte>(f.Buffer, 54), out _));
        f.Close(); HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(f.Owner));
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(f.Table, f.Handle));
        f.Reserve(Remote); HostCheck.Equal(Rx.Status.Success, f.Bind(f.Other, f.OtherBuffer));
        *f.Owner = copy; // Same mapped address, but the old generation is retired.
        HostCheck.Equal(Rx.Status.TableRejected, Rx.PumpAck(f.Owner, output, out _));
        HostCheck.True(f.Other->Active); HostCheck.Equal(0, f.Other->UnreadBytes);
        using var foreign = new Fixture();
        HostCheck.Equal(Rx.Status.TableRejected, Rx.Initialize(f.Owner, f.Table, foreign.Handle, f.Buffer, 8, f.Info(), f.Hardware, &Send));
    }

    [HostTest("net.tcp.receive.owner fatal ACK retains buffer until explicit closed scrub", HostLane.Fast, 8)]
    private static void FatalAck()
    {
        using var f = new Fixture(); byte[] ack = new byte[54];
        HostCheck.Equal(Rx.Status.Success, f.Receive(Remote, "abc")); f.Hardware->SendResult = 2;
        HostCheck.Equal(Rx.Status.SendFailed, Rx.PumpAck(f.Owner, ack, out _));
        HostCheck.True(f.Owner->RequiresCleanup && f.Owner->HasPendingAck); HostCheck.Equal(3, f.Owner->UnreadBytes);
        HostCheck.Equal(Rx.Status.InvalidState, Rx.Read(f.Owner, ack, out _));
        f.Close(); HostCheck.Equal(Tcb.OwnershipStatus.AttachmentsRemain, Tcb.ReleaseOwned(f.Table, f.Handle));
        HostCheck.Equal(Rx.Status.Success, Rx.ReleaseClosed(f.Owner));
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 8).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReleaseOwned(f.Table, f.Handle));
    }

    [HostTest("net.tcp.receive.owner admission rejects nonzero aliased mismatched or absent storage", HostLane.Fast, 9)]
    private static void Admission()
    {
        using var f = new Fixture(bind: false);
        HostCheck.Equal(Rx.Status.InvalidStorage, Rx.Initialize(f.Owner, f.Table, f.Handle, null, 8, f.Info(), f.Hardware, &Send));
        HostCheck.Equal(Rx.Status.InvalidStorage, Rx.Initialize(f.Owner, f.Table, f.Handle, f.Buffer, 32769, f.Info(), f.Hardware, &Send));
        HostCheck.Equal(Rx.Status.InvalidStorage, Rx.Initialize(f.Owner, f.Table, f.Handle, (byte*)f.Owner, 8, f.Info(), f.Hardware, &Send));
        f.Buffer[0] = 1; HostCheck.Equal(Rx.Status.InvalidStorage, f.Bind(f.Owner, f.Buffer)); f.Buffer[0] = 0;
        HostCheck.Equal(Rx.Status.InvalidOptions, Rx.Initialize(f.Owner, f.Table, f.Handle, f.Buffer, 8, f.Info(7), f.Hardware, &Send));
        HostCheck.Equal(Rx.Status.InvalidOptions, Rx.Initialize(f.Owner, f.Table, f.Handle, f.Buffer, 8, f.Info(), f.Hardware, null));
        HostCheck.Equal(Rx.Status.Success, f.Bind(f.Owner, f.Buffer));
        HostCheck.Equal(Rx.Status.NoData, Rx.Read(f.Owner, Span<byte>.Empty, out _));
        HostCheck.Equal(Rx.Status.Success, f.Receive(Remote, "abc"));
        HostCheck.Equal(Rx.Status.OutputTooSmall, Rx.Read(f.Owner, Span<byte>.Empty, out _));
        HostCheck.Equal(3, f.Owner->UnreadBytes);
    }

    [HostTest("net.tcp.receive.owner persistent metadata and warmed receive read ACK allocate no managed bytes", HostLane.Fast, 10)]
    private static void Allocation()
    {
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<Rx.Owner>() && !RuntimeHelpers.IsReferenceOrContainsReferences<TcpConnectionTableEntry>());
        using var f = new Fixture(); byte[] frame = Packet(Remote, "z"u8), ack = new byte[54], output = new byte[1];
        Rx.Receive(f.Owner, frame, out _); Rx.Read(f.Owner, output, out _); Rx.PumpAck(f.Owner, ack, out _);
        uint next = Remote + 1; bool success = true; long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
            U32(frame, 38, next++); TcpChecksum(frame);
            success &= Rx.Receive(f.Owner, frame, out _) == Rx.Status.Success;
            success &= Rx.Read(f.Owner, output, out _) == Rx.Status.Success;
            success &= Rx.PumpAck(f.Owner, ack, out _) == Rx.Status.Success;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(success); HostCheck.Equal(0L, bytes); HostCheck.Equal((byte)'z', output[0]);
    }

    [HostTest("net.tcp.receive pure ACK writer preserves sequence state and rejects short output", HostLane.Fast, 11)]
    private static void AckWriter()
    {
        using var f = new Fixture(); Span<byte> buffer = stackalloc byte[8];
        var stream = TcpEstablishedStream.Create(8080, 40000, Local, Remote, buffer);
        byte[] output = Enumerable.Repeat((byte)0xa5, 54).ToArray();
        HostCheck.Equal(TcpEstablishedStreamIpv4FramePlanStatus.FrameBufferTooSmall,
            TcpEstablishedStreamIpv4FramePlanner.TryWriteAckFrame(ref stream, f.Info(), 0, output.AsSpan(0, 53), out _));
        HostCheck.True(output.AsSpan().IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(TcpEstablishedStreamIpv4FramePlanStatus.Success,
            TcpEstablishedStreamIpv4FramePlanner.TryWriteAckFrame(ref stream, f.Info(), 0, output, out var result));
        HostCheck.Equal(0, result.PayloadBytes); HostCheck.Equal(Local, stream.LocalNextSequenceNumber);
        HostCheck.Equal(Remote, stream.RemoteNextSequenceNumber); CheckAck(output, Local, Remote, 0);
    }

    [HostTest("net.tcp.receive.owner compacts unread bytes without duplicating released prefix", HostLane.Fast, 12)]
    private static void Compaction()
    {
        using var f = new Fixture(); byte[] output = new byte[4];
        HostCheck.Equal(Rx.Status.Success, f.Receive(Remote, "abcdef"));
        HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Owner, output, out _));
        HostCheck.Equal("abcd", Encoding.ASCII.GetString(output));
        HostCheck.Equal(Rx.Status.Success, f.Receive(Remote + 6, "ghijkl"));
        output = new byte[8]; HostCheck.Equal(Rx.Status.Success, Rx.Read(f.Owner, output, out _));
        HostCheck.Equal("efghijkl", Encoding.ASCII.GetString(output));
        HostCheck.Equal(Rx.Status.NoData, Rx.Read(f.Owner, output, out _));
        HostCheck.Equal(Remote + 12, f.Owner->AcknowledgementNumber);
    }

    private struct Hardware { public byte SendResult; public uint Calls; public fixed byte Last[54]; }
    private sealed class Fixture : IDisposable
    {
        private readonly byte* memory; private readonly int capacity;
        public Rx.Owner* Owner => (Rx.Owner*)memory;
        public Rx.Owner* Other => (Rx.Owner*)(memory + 512);
        public Tcb.Owner* Table => (Tcb.Owner*)(memory + 1024);
        public TcpConnectionTableEntry* Entries => (TcpConnectionTableEntry*)(memory + 1152);
        public byte* Buffer => memory + 2048;
        public byte* OtherBuffer => memory + 2560;
        public Hardware* Hardware => (Hardware*)(memory + 3072);
        public Tcb.Handle Handle { get; private set; }
        public Fixture(int capacity = 8, uint remote = Remote, bool bind = true)
        {
            this.capacity = capacity; memory = (byte*)NativeMemory.AlignedAlloc(8192, 4096); NativeMemory.Clear(memory, 8192);
            HostCheck.True(sizeof(Rx.Owner) <= 512 && sizeof(TcpConnectionTableEntry) <= 128);
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.InitializeOwned(Table, Entries, 1, (ulong*)(memory + 1536), 1, 123));
            Reserve(remote); Hardware->SendResult = 1;
            if (bind) HostCheck.Equal(Rx.Status.Success, Bind(Owner, Buffer));
        }
        public void Reserve(uint remote)
        {
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReserveOwned(Table,
                new(new Ipv4Address(10, 0, 2, 15), 8080, new Ipv4Address(10, 0, 2, 2), 40000), new(Local, remote), out var handle));
            Handle = handle; HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.TransitionOwned(Table, Handle, TcpConnectionLifecycleState.SynReceived, TcpConnectionLifecycleState.Established));
        }
        public Rx.Status Bind(Rx.Owner* owner, byte* buffer) => Rx.Initialize(owner, Table, Handle, buffer, (uint)capacity, Info(), Hardware, &Send);
        public TcpEstablishedStreamIpv4FramePlannerInfo Info(int window = -1) => new(new MacAddress(2, 0, 0, 0, 0, 1), new MacAddress(0x52, 0x55, 0x0a, 0, 2, 2),
            new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2), 1460, (ushort)(window < 0 ? capacity : window), 51, 0x271);
        public Rx.Status Receive(uint sequence, string payload) => Rx.Receive(Owner, Packet(sequence, Encoding.ASCII.GetBytes(payload)), out _);
        public void Close() => HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.TransitionOwned(Table, Handle, TcpConnectionLifecycleState.Established, TcpConnectionLifecycleState.Closed));
        public void Dispose() => NativeMemory.AlignedFree(memory);
    }
    private static byte Send(void* context, byte* bytes, uint length)
    {
        var hardware = (Hardware*)context; hardware->Calls++;
        if (length != 54) return 2;
        if (hardware->SendResult == 1) for (int i = 0; i < length; i++) hardware->Last[i] = bytes[i];
        return hardware->SendResult;
    }
    // Independent wire vectors and one's-complement checksum implementation.
    internal static byte[] Packet(uint sequence, ReadOnlySpan<byte> payload, uint ack = Local, ushort port = 40000,
        byte flags = 0x18, ushort fragment = 0x4000, ushort window = 0x2000, ReadOnlySpan<byte> options = default)
    {
        if (options.Length > 40 || (options.Length & 3) != 0) throw new ArgumentOutOfRangeException(nameof(options));
        byte[] bytes = new byte[54 + options.Length + payload.Length]; new byte[] { 2, 0, 0, 0, 0, 1, 0x52, 0x55, 0x0a, 0, 2, 2, 8, 0 }.CopyTo(bytes, 0);
        bytes[14] = 0x45; U16(bytes, 16, (ushort)(40 + options.Length + payload.Length)); U16(bytes, 18, 0x2345); U16(bytes, 20, fragment); bytes[22] = 64; bytes[23] = 6;
        new byte[] { 10, 0, 2, 2, 10, 0, 2, 15 }.CopyTo(bytes, 26); U16(bytes, 24, (ushort)~Sum(bytes.AsSpan(14, 20)));
        U16(bytes, 34, port); U16(bytes, 36, 8080); U32(bytes, 38, sequence); U32(bytes, 42, ack); bytes[46] = (byte)(((20 + options.Length) / 4) << 4); bytes[47] = flags;
        U16(bytes, 48, window); options.CopyTo(bytes.AsSpan(54)); payload.CopyTo(bytes.AsSpan(54 + options.Length)); TcpChecksum(bytes); return bytes;
    }
    internal static void TcpChecksum(byte[] bytes)
    { U16(bytes, 50, 0); U16(bytes, 50, (ushort)~Fold(Sum(bytes.AsSpan(26, 8)) + 6U + (uint)bytes.Length - 34 + Sum(bytes.AsSpan(34)))); }
    private static void CheckAck(byte[] bytes, uint local, uint remote, ushort window)
    {
        HostCheck.Equal(54, bytes.Length); HostCheck.Equal((ushort)0xffff, Sum(bytes.AsSpan(14, 20)));
        HostCheck.Equal((ushort)0xffff, Fold(Sum(bytes.AsSpan(26, 8)) + 6U + 20 + Sum(bytes.AsSpan(34))));
        HostCheck.Equal(local, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(38, 4)));
        HostCheck.Equal(remote, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(42, 4)));
        HostCheck.Equal(window, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(48, 2))); HostCheck.Equal((byte)0x10, bytes[47]);
        HostCheck.True(bytes.AsSpan(0, 14).SequenceEqual(new byte[] { 0x52, 0x55, 0x0a, 0, 2, 2, 2, 0, 0, 0, 0, 1, 8, 0 }));
    }
    internal static ushort Sum(ReadOnlySpan<byte> bytes)
    { uint sum = 0; for (int i = 0; i < bytes.Length; i += 2) sum += (uint)((bytes[i] << 8) | (i + 1 < bytes.Length ? bytes[i + 1] : 0)); return Fold(sum); }
    internal static ushort Fold(uint sum) { while (sum > 0xffff) sum = (sum & 0xffff) + (sum >> 16); return (ushort)sum; }
    private static void U16(byte[] bytes, int at, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(at, 2), value);
    internal static void U32(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(at, 4), value);
}
