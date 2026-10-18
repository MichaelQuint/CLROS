using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Net;
using Network = OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Epoll = OS.Kernel.Compatibility.LinuxX64.LinuxEpollServices;
using Table = OS.Net.TcpConnectionTable;
using Wire = TcpReceiveWindowTests;

internal static unsafe class LinuxSocketNetworkTests
{
    private const ulong User = 0x100000000, Output = User + 0x4000;
    private struct Hardware { internal ulong Now; internal uint Calls, Length; internal byte Result; internal fixed byte Frame[1514]; }
    private sealed class Model : IDisposable
    {
        internal readonly LinuxDescriptorServicesTests.Owner Files = new(16, 16);
        internal readonly Sockets.State* Sockets;
        internal readonly Sockets.Endpoint* Endpoints;
        private readonly Epoll.State* Epoll;
        private readonly Epoll.Port* Ports;
        private readonly Epoll.Registration* Registrations;
        internal readonly Network.State* State;
        internal readonly Network.Slot* Slots;
        internal readonly Table.Owner* Table;
        private readonly TcpConnectionTableEntry* Entries;
        private readonly ulong* Generations;
        internal readonly byte* Buffers;
        internal readonly Hardware* Hardware;
        internal readonly byte[] Frame = new byte[1514];
        private readonly uint Count, ReceiveBytes, SendBytes;
        internal Model(uint count = 4, uint receiveBytes = 8192, uint sendBytes = 32768, bool compactTimeWait = false)
        {
            Count = count; ReceiveBytes = receiveBytes; SendBytes = sendBytes; Files.Begin();
            Sockets = (Sockets.State*)NativeMemory.AllocZeroed((nuint)sizeof(Sockets.State));
            Endpoints = (Sockets.Endpoint*)NativeMemory.AllocZeroed(8, (nuint)sizeof(Sockets.Endpoint));
            Epoll = (Epoll.State*)NativeMemory.AllocZeroed((nuint)sizeof(Epoll.State));
            Ports = (Epoll.Port*)NativeMemory.AllocZeroed(2, (nuint)sizeof(Epoll.Port));
            Registrations = (Epoll.Registration*)NativeMemory.AllocZeroed(16, (nuint)sizeof(Epoll.Registration));
            State = (Network.State*)NativeMemory.AllocZeroed((nuint)sizeof(Network.State));
            Slots = (Network.Slot*)NativeMemory.AllocZeroed(count, (nuint)sizeof(Network.Slot));
            Table = (Table.Owner*)NativeMemory.AllocZeroed((nuint)sizeof(Table.Owner));
            Entries = (TcpConnectionTableEntry*)NativeMemory.AllocZeroed(count, (nuint)sizeof(TcpConnectionTableEntry));
            Generations = (ulong*)NativeMemory.AllocZeroed(count, 8);
            Buffers = (byte*)NativeMemory.AllocZeroed(count, receiveBytes + sendBytes);
            Hardware = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware)); Hardware->Now = 1000; Hardware->Result = 1;
            HostCheck.True(LinuxSocketServices.Begin(Sockets, Files.State, Endpoints, 8, new(10, 0, 2, 15), 8080));
            HostCheck.True(LinuxEpollServices.Begin(Epoll, Files.State, Ports, 2, Registrations, 16));
            HostCheck.Equal(TcpConnectionTable.OwnershipStatus.Success, TcpConnectionTable.InitializeOwned(Table, Entries, count, Generations, count, 0x1234));
            HostCheck.True(Network.Begin(State, Sockets, Table, Slots, count, Buffers, receiveBytes, sendBytes,
                new(2, 0, 0, 0, 0, 1), new(0x52, 0x55, 0x0a, 0, 2, 2), new(10, 0, 2, 2), 700, Io, compactTimeWait));
        }
        internal TcpPassiveConnection.Operations Io => new() { Context = Hardware, NowNanoseconds = &Now, Send = &Send,
            LifetimeNanoseconds = 10_000_000_000, RetryNanoseconds = 1_000_000_000, MaximumAttempts = 3 };
        internal long Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0) => Fds.Dispatch(Files.State,
            new() { Number = number, Argument0 = a0, Argument1 = a1, Argument2 = a2, Argument3 = a3 });
        internal uint Listen(uint backlog = 4)
        {
            long fd = Call(41, 2, 1 | Fds.NonBlock, 6); HostCheck.True(fd >= 0);
            *(ulong*)Files.Memory.At(User) = 0x901f0002; *(ulong*)Files.Memory.At(User + 8) = 0;
            HostCheck.Equal(0L, Call(49, (uint)fd, User, 16)); HostCheck.Equal(0L, Call(50, (uint)fd, backlog)); return (uint)fd;
        }
        internal Network.Status Receive(byte[] frame) => Network.Receive(State, frame, Frame);
        internal void Pump(ulong now = 0)
        { if (now != 0) Hardware->Now = now; HostCheck.Equal(Network.Status.Success, Network.Pump(State, Frame)); }
        internal void Refresh() => HostCheck.True(LinuxEpollServices.Refresh(Epoll));
        internal uint Connect(ushort port = 40000, uint sequence = 99)
        {
            HostCheck.Equal(Network.Status.Success, Receive(Syn(port, sequence)));
            uint local = BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(Hardware->Frame + 38, 4)) + 1;
            HostCheck.Equal(Network.Status.Success, Receive(Wire.Packet(sequence + 1, ReadOnlySpan<byte>.Empty, local, port, flags: 0x10)));
            return local;
        }
        internal uint Accept(uint listener) { long fd = Call(288, listener, 0, 0, Fds.NonBlock); HostCheck.True(fd >= 0); return (uint)fd; }
        internal void Finish(bool abort = true)
        {
            for (uint fd = 0; fd < 32; fd++) if (Fds.Capture(Files.State, fd, out _)) HostCheck.Equal(0L, Call(3, fd));
            if (abort) HostCheck.True(Network.Abort(State));
            HostCheck.Equal(0U, State->RetainedConnections); HostCheck.Equal(0, Table->Count);
            HostCheck.True(Network.End(State)); HostCheck.Equal(TcpConnectionTable.OwnershipStatus.Success, TcpConnectionTable.RetireOwned(Table));
            HostCheck.True(Fds.End(Files.State)); HostCheck.True(new ReadOnlySpan<byte>(Buffers, (int)(Count * (ReceiveBytes + SendBytes))).IndexOfAnyExcept((byte)0) < 0);
        }
        public void Dispose()
        {
            NativeMemory.Free(Hardware); NativeMemory.Free(Buffers); NativeMemory.Free(Generations); NativeMemory.Free(Entries); NativeMemory.Free(Table);
            NativeMemory.Free(Slots); NativeMemory.Free(State); NativeMemory.Free(Registrations); NativeMemory.Free(Ports); NativeMemory.Free(Epoll);
            NativeMemory.Free(Endpoints); NativeMemory.Free(Sockets); Files.Dispose();
        }
    }
    private static ulong Now(void* context) => ((Hardware*)context)->Now;
    private static byte Send(void* context, byte* frame, uint bytes)
    {
        Hardware* h = (Hardware*)context; h->Calls++;
        if (bytes > 1514) return 2;
        if (h->Result == 1) { for (uint i = 0; i < bytes; i++) h->Frame[i] = frame[i]; h->Length = bytes; }
        return h->Result;
    }
    private static byte[] Syn(ushort port = 40000, uint sequence = 99) => Wire.Packet(sequence, ReadOnlySpan<byte>.Empty, 0, port,
        flags: 2, options: [2, 4, 5, 180]);

    [HostTest("m07-029 prolonged zero-window peer preserves independent socket progress and resumes exact bytes", HostLane.Fast, 19)]
    private static void PersistAndIndependentProgress()
    {
        using Model m = new(); uint listener = m.Listen(), first = m.Connect(), a = m.Accept(listener);
        uint second = m.Connect(40001, 199), b = m.Accept(listener);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, first, flags: 0x10, window: 0)));
        m.Files.Text(User, "recover"); HostCheck.Equal(7L, m.Call(44, a, User, 7)); m.Pump();
        HostCheck.Equal(Table.OwnershipStatus.Success, Table.FindOwned(m.Table, new(new(10, 0, 2, 15), 8080, new(10, 0, 2, 2), 40000), out var handle));
        HostCheck.Equal(Table.OwnershipStatus.Success, Table.ReadOwned(m.Table, handle, out var entry));
        HostCheck.Equal(first, entry.Sequence.LocalNextSequenceNumber);
        HostCheck.Equal(first, entry.Sequence.LocalAcknowledgedSequenceNumber);
        ulong due = 1_000_001_000, interval = 1_000_000_000;
        for (int i = 0; i < 12; i++)
        {
            m.Pump(due);
            HostCheck.Equal(55U, m.Hardware->Length);
            HostCheck.Equal(first, BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(m.Hardware->Frame + 38, 4)));
            HostCheck.Equal((byte)'r', m.Hardware->Frame[54]);
            HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, first, flags: 0x10, window: 0)));
            HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(200U + (uint)i, "x"u8, second, port: 40001)));
            HostCheck.Equal(1L, m.Call(45, b, Output, 1)); HostCheck.Equal((byte)'x', *m.Files.Memory.At(Output));
            m.Pump();
            interval = Math.Min(60_000_000_000, interval * 2); due += interval;
        }
        HostCheck.True(m.Hardware->Now > 300_000_000_000 && !m.State->RequiresShutdown);
        // The initial unsolicited reopening update was lost. The next probe
        // elicits an ACK that opens the window; no new application write occurs.
        m.Pump(due);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, first + 1, flags: 0x10, window: 8192)));
        m.Pump();
        HostCheck.True(new ReadOnlySpan<byte>(m.Hardware->Frame + 54, 6).SequenceEqual("ecover"u8));
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, first + 7, flags: 0x10, window: 8192)));
        HostCheck.Equal(Table.OwnershipStatus.Success, Table.ReadOwned(m.Table, handle, out entry));
        HostCheck.Equal(first + 7, entry.Sequence.LocalAcknowledgedSequenceNumber); HostCheck.Equal(2, m.Table->Count);
        m.Finish();
    }

    [HostTest("m07-032 validated pending reset returns capacity before timeout and cancels retries", HostLane.Fast, 20)]
    private static void PendingReset()
    {
        using Model m = new(count: 1); uint listener = m.Listen();
        HostCheck.Equal(Network.Status.Success, m.Receive(Syn()));
        HostCheck.Equal(Table.OwnershipStatus.Success, Table.FindOwned(m.Table, new(new(10, 0, 2, 15), 8080, new(10, 0, 2, 2), 40000), out var old));
        uint calls = m.Hardware->Calls;
        HostCheck.Equal(Network.Status.Ignored, m.Receive(Wire.Packet(101, ReadOnlySpan<byte>.Empty, 701, flags: 0x14)));
        HostCheck.Equal(Network.Status.Ignored, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, 702, flags: 0x14)));
        byte[] corrupt = Wire.Packet(100, ReadOnlySpan<byte>.Empty, 701, flags: 0x14); corrupt[50] ^= 1;
        HostCheck.Equal(Network.Status.Ignored, m.Receive(corrupt));
        HostCheck.Equal(1U, m.State->RetainedConnections); HostCheck.Equal(calls, m.Hardware->Calls);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, 701, flags: 0x14)));
        HostCheck.Equal(0U, m.State->RetainedConnections); HostCheck.Equal(0, m.Table->Count);
        HostCheck.Equal(Table.OwnershipStatus.StaleHandle, Table.ReadOwned(m.Table, old, out _));
        HostCheck.Equal(-11L, m.Call(288, listener, 0, 0, Fds.NonBlock));
        HostCheck.Equal(Network.Status.Ignored, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, 701, flags: 0x14)));
        m.Pump(1_000_001_000); HostCheck.Equal(calls, m.Hardware->Calls);
        uint local = m.Connect(40001, 199), fd = m.Accept(listener);
        HostCheck.True(m.Hardware->Now < 10_000_001_000);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(200, "next"u8, local, port: 40001)));
        HostCheck.Equal(4L, m.Call(45, fd, Output, 4));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 4).SequenceEqual("next"u8));
        HostCheck.True(!m.State->RequiresShutdown); m.Finish();
    }

    [HostTest("m07-032 pending reset preserves an established peer and listener-close cleanup", HostLane.Fast, 21)]
    private static void PendingResetIsolation()
    {
        using Model m = new(count: 2); uint listener = m.Listen(), first = m.Connect(), fd = m.Accept(listener);
        HostCheck.Equal(Network.Status.Success, m.Receive(Syn(40001, 199)));
        uint pendingNext = BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(m.Hardware->Frame + 38, 4)) + 1;
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(200, ReadOnlySpan<byte>.Empty, pendingNext, port: 40001, flags: 0x14)));
        HostCheck.Equal(1, m.Table->Count); HostCheck.Equal(1U, m.State->RetainedConnections);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, "live"u8, first)));
        HostCheck.Equal(4L, m.Call(45, fd, Output, 4));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 4).SequenceEqual("live"u8));
        HostCheck.Equal(Network.Status.Success, m.Receive(Syn(40002, 299)));
        HostCheck.Equal(0L, m.Call(3, listener)); m.Pump();
        HostCheck.Equal(1, m.Table->Count); HostCheck.True(!m.State->RequiresShutdown); m.Finish();
    }

    [HostTest("m07-030 twelve active closes reuse one bulk slot while compact tuples retain full TIME-WAIT", HostLane.Fast, 22)]
    private static void CompactTimeWaitChurn()
    {
        using Model m = new(count: 1, compactTimeWait: true); uint listener = m.Listen();
        for (ushort i = 0; i < 12; i++)
        {
            ushort port = (ushort)(40000 + i); uint remote = 99U + i * 100U;
            uint local = m.Connect(port, remote), fd = m.Accept(listener);
            HostCheck.Equal(0L, m.Call(48, fd, 1)); m.Pump();
            HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(remote + 1, ReadOnlySpan<byte>.Empty, local + 1, port, flags: 0x11)));
            HostCheck.Equal(0L, m.Call(3, fd)); m.Pump();
            HostCheck.Equal(0, m.Table->Count); HostCheck.Equal((uint)i + 1, m.State->CompactTimeWaitConnections);
            HostCheck.Equal((uint)i + 1, m.State->RetainedConnections);
            HostCheck.True(new ReadOnlySpan<byte>(m.Buffers, 40960).IndexOfAnyExcept((byte)0) < 0);
            HostCheck.Equal(Network.Status.Ignored, m.Receive(Syn(port, remote + 2000)));
        }
        HostCheck.True(m.Hardware->Now < 240_000_001_000 && !m.State->RequiresShutdown);
        m.Pump(240_000_000_999); HostCheck.Equal(12U, m.State->CompactTimeWaitConnections);
        HostCheck.True(!Network.End(m.State));
        m.Pump(240_000_001_000); HostCheck.Equal(0U, m.State->RetainedConnections); m.Finish(false);
    }

    [HostTest("m07-030 compact duplicate FIN keeps exact ACK through TX pressure and restarts only its deadline", HostLane.Fast, 23)]
    private static void CompactTimeWaitDuplicateFin()
    {
        using Model m = new(count: 1, compactTimeWait: true); uint listener = m.Listen(), local = m.Connect(), fd = m.Accept(listener);
        HostCheck.Equal(0L, m.Call(48, fd, 1)); m.Pump();
        byte[] fin = Wire.Packet(100, ReadOnlySpan<byte>.Empty, local + 1, flags: 0x11);
        HostCheck.Equal(Network.Status.Success, m.Receive(fin)); HostCheck.Equal(0L, m.Call(3, fd)); m.Pump();
        HostCheck.Equal(1U, m.State->CompactTimeWaitConnections);
        m.Hardware->Now = 1_000_001_000; m.Hardware->Result = 0;
        HostCheck.Equal(Network.Status.Success, m.Receive(fin));
        HostCheck.Equal(Network.Status.Ignored, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, local + 1, flags: 0x14)));
        HostCheck.Equal(Network.Status.Ignored, m.Receive(Wire.Packet(101, ReadOnlySpan<byte>.Empty, local + 1, flags: 0x11)));
        m.Hardware->Result = 1; m.Pump(2_000_001_000);
        HostCheck.Equal(54U, m.Hardware->Length); HostCheck.Equal((byte)0x10, m.Hardware->Frame[47]);
        HostCheck.Equal(local + 1, BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(m.Hardware->Frame + 38, 4)));
        HostCheck.Equal(101U, BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(m.Hardware->Frame + 42, 4)));
        m.Pump(240_000_001_000); HostCheck.Equal(1U, m.State->CompactTimeWaitConnections);
        m.Pump(241_000_000_999); HostCheck.Equal(1U, m.State->CompactTimeWaitConnections);
        m.Pump(241_000_001_000); HostCheck.Equal(0U, m.State->RetainedConnections); m.Finish(false);
    }

    [HostTest("m07-030 full compact table retains one bulk close without faulting another established peer", HostLane.Fast, 24)]
    private static void CompactTimeWaitCapacity()
    {
        using Model m = new(count: 2, compactTimeWait: true); uint listener = m.Listen();
        for (ushort i = 0; i < 65; i++)
        {
            ushort port = (ushort)(40000 + i); uint local = m.Connect(port), fd = m.Accept(listener);
            HostCheck.Equal(0L, m.Call(48, fd, 1)); m.Pump();
            HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, local + 1, port, flags: 0x11)));
            HostCheck.Equal(0L, m.Call(3, fd)); m.Pump();
        }
        HostCheck.Equal(64U, m.State->CompactTimeWaitConnections); HostCheck.Equal(1, m.Table->Count);
        uint healthy = m.Connect(40100, 199), b = m.Accept(listener);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(200, "alive"u8, healthy, port: 40100)));
        HostCheck.Equal(5L, m.Call(45, b, Output, 5)); HostCheck.True(!m.State->RequiresShutdown);
        HostCheck.Equal(Network.Status.Capacity, m.Receive(Syn(40101, 299)));
        m.Pump(240_000_001_000); m.Pump(); // Close then reap the full-table bulk fallback on the next bounded turn.
        HostCheck.Equal(0U, m.State->CompactTimeWaitConnections); HostCheck.Equal(1U, m.State->RetainedConnections);
        HostCheck.Equal(1, m.Table->Count); HostCheck.True(!m.State->RequiresShutdown); m.Finish();
    }

    [HostTest("m16-011 network admission and frame aliases fail without changing ownership", HostLane.Fast, 0)]
    private static void Admission()
    {
        using Model m = new(); Network.State copy = *m.State;
        HostCheck.Equal(Network.Status.InvalidState, Network.Pump(&copy, m.Frame));
        HostCheck.True(!Network.Begin(m.State, m.Sockets, m.Table, m.Slots, 4, m.Buffers, 8192, 32768,
            new(2, 0, 0, 0, 0, 1), new(0x52, 0x55, 0x0a, 0, 2, 2), new(10, 0, 2, 2), 700, m.Io));
        HostCheck.Equal(Network.Status.InvalidStorage, Network.Receive(m.State, new ReadOnlySpan<byte>(m.Buffers, 64), m.Frame));
        HostCheck.Equal(Network.Status.InvalidStorage, Network.Receive(m.State, Syn(), new Span<byte>(m.Buffers, 1514)));
        HostCheck.Equal(Network.Status.InvalidStorage, Network.Receive(m.State, Syn(), new Span<byte>(m.Files.Admission.Scratch, 1514)));
        HostCheck.Equal(Network.Status.InvalidStorage, Network.Receive(m.State, Syn(), new Span<byte>(m.Files.Memory.At(User), 1514)));
        HostCheck.Equal(Network.Status.InvalidStorage, Network.Receive(m.State, new ReadOnlySpan<byte>((byte*)m.Sockets, sizeof(Sockets.State)), m.Frame));
        HostCheck.Equal(0, m.Table->Count); HostCheck.True(!Network.End(&copy)); m.Finish();
    }

    [HostTest("m16-011 actual SYN ACK data and copied response traverse the Linux socket adapter", HostLane.Fast, 1)]
    private static void Exchange()
    {
        using Model m = new(); uint listener = m.Listen(), local = m.Connect(), fd = m.Accept(listener);
        HostCheck.Equal(1U, m.State->RetainedConnections);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, "hello"u8, local)));
        HostCheck.Equal(5L, m.Call(45, fd, Output, 64)); HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 5).SequenceEqual("hello"u8));
        HostCheck.Equal(-11L, m.Call(45, fd, Output, 1)); m.Files.Text(User, "response"); HostCheck.Equal(8L, m.Call(44, fd, User, 8));
        m.Pump(); HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(105, ReadOnlySpan<byte>.Empty, local + 8, flags: 0x10)));
        HostCheck.Equal(1, m.Table->Count); m.Finish();
    }

    [HostTest("m16-011 exact tuple and generation dispatch isolates reordered connections", HostLane.Fast, 2)]
    private static void TupleDispatch()
    {
        using Model m = new(); uint listener = m.Listen(); uint first = m.Connect(40000), second = m.Connect(40001, 199);
        uint a = m.Accept(listener), b = m.Accept(listener);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(200, "second"u8, second, port: 40001)));
        HostCheck.Equal(-11L, m.Call(45, a, Output, 32)); HostCheck.Equal(6L, m.Call(45, b, Output, 32));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 6).SequenceEqual("second"u8));
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, "first"u8, first)));
        HostCheck.Equal(5L, m.Call(45, a, Output, 32)); HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 5).SequenceEqual("first"u8)); m.Finish();
    }

    [HostTest("m16-011 malformed wrong-peer missing-listener and exhausted SYNs preserve live generations", HostLane.Fast, 3)]
    private static void Rejection()
    {
        using Model m = new(count: 1); HostCheck.Equal(Network.Status.Ignored, m.Receive(Syn())); HostCheck.Equal(0U, m.Hardware->Calls);
        uint listener = m.Listen(); byte[] corrupt = Syn(); corrupt[^1] ^= 1;
        HostCheck.Equal(Network.Status.Ignored, m.Receive(corrupt)); byte[] wrongMac = Syn(); wrongMac[6] ^= 2;
        HostCheck.Equal(Network.Status.Ignored, m.Receive(wrongMac)); HostCheck.Equal(0, m.Table->Count);
        HostCheck.Equal(Network.Status.Ignored, m.Receive(Syn(port: 0))); HostCheck.True(!m.State->RequiresShutdown);
        HostCheck.Equal(Network.Status.Success, m.Receive(Syn())); HostCheck.Equal(1, m.Table->Count);
        HostCheck.Equal(Table.OwnershipStatus.Success, Table.FindOwned(m.Table, new(new(10, 0, 2, 15), 8080, new(10, 0, 2, 2), 40000), out var original));
        HostCheck.Equal(Network.Status.Capacity, m.Receive(Syn(40001))); HostCheck.Equal(Table.OwnershipStatus.Success, Table.ReadOwned(m.Table, original, out _));
        HostCheck.Equal(0L, m.Call(3, listener)); m.Pump(); HostCheck.Equal(Table.OwnershipStatus.StaleHandle, Table.ReadOwned(m.Table, original, out _)); m.Finish();
    }

    [HostTest("m16-011 genuine pending TX backpressure retries and retires at the original deadline", HostLane.Fast, 4)]
    private static void PendingRetry()
    {
        using Model m = new(count: 1); _ = m.Listen(); m.Hardware->Result = 0;
        HostCheck.Equal(Network.Status.Success, m.Receive(Syn())); HostCheck.Equal(1U, m.State->RetainedConnections);
        HostCheck.Equal(Network.Status.Ignored, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, 701, flags: 0x10)));
        m.Hardware->Result = 1; m.Pump(2000); HostCheck.True(m.Hardware->Calls >= 2);
        m.Pump(1_000_002_000); m.Pump(2_000_002_000); m.Pump(3_000_002_000);
        HostCheck.Equal(1U, m.State->RetainedConnections); HostCheck.True(!m.State->RequiresShutdown);
        m.Pump(10_000_001_000); HostCheck.Equal(0U, m.State->RetainedConnections); HostCheck.Equal(0, m.Table->Count); m.Finish(false);
    }

    [HostTest("m16-011 full socket accept queue retains established transport until capacity returns", HostLane.Fast, 5)]
    private static void AcceptPressure()
    {
        using Model m = new(); uint listener = m.Listen(1); uint first = m.Connect(), second = m.Connect(40001, 199);
        HostCheck.Equal(2, m.Table->Count); uint a = m.Accept(listener); HostCheck.Equal(-11L, m.Call(288, listener, 0, 0, Fds.NonBlock));
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(200, "queued"u8, second, port: 40001)));
        m.Pump(); uint b = m.Accept(listener); HostCheck.Equal(6L, m.Call(45, b, Output, 32));
        HostCheck.Equal(-11L, m.Call(45, a, Output, 32)); HostCheck.True(first != second); m.Finish();
    }

    [HostTest("m16-011 actual peer FIN and last ACK close and scrub without abort", HostLane.Fast, 6)]
    private static void GracefulClose()
    {
        using Model m = new(); uint listener = m.Listen(), local = m.Connect(), fd = m.Accept(listener);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, "end"u8, local, flags: 0x19)));
        HostCheck.Equal(3L, m.Call(45, fd, Output, 32)); HostCheck.Equal(0L, m.Call(45, fd, Output, 32));
        HostCheck.Equal(0L, m.Call(48, fd, 1)); m.Pump();
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(104, ReadOnlySpan<byte>.Empty, local + 1, flags: 0x10)));
        HostCheck.Equal(0L, m.Call(3, fd)); m.Pump(); HostCheck.Equal(0U, m.State->RetainedConnections); m.Finish(false);
    }

    [HostTest("m16-011 reset remains owned until fd close and closed table generation is never reused by stale traffic", HostLane.Fast, 7)]
    private static void ResetAndReuse()
    {
        using Model m = new(count: 1); uint listener = m.Listen(), local = m.Connect(), fd = m.Accept(listener);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, local, flags: 0x14)));
        HostCheck.Equal(Network.Status.Ignored, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, local, flags: 0x14)));
        m.Pump(); HostCheck.Equal(1U, m.State->RetainedConnections); HostCheck.Equal(-104L, m.Call(45, fd, Output, 1));
        HostCheck.Equal(Table.OwnershipStatus.Success, Table.FindOwned(m.Table, new(new(10, 0, 2, 15), 8080, new(10, 0, 2, 2), 40000), out var original));
        HostCheck.Equal(0L, m.Call(3, fd)); m.Pump(); HostCheck.Equal(0U, m.State->RetainedConnections);
        uint fresh = m.Connect(sequence: 199); HostCheck.True(fresh != local);
        HostCheck.Equal(Table.OwnershipStatus.StaleHandle, Table.ReadOwned(m.Table, original, out _));
        HostCheck.Equal(Network.Status.Ignored, m.Receive(Wire.Packet(100, "old"u8, local)));
        uint replacement = m.Accept(listener); HostCheck.Equal(-11L, m.Call(45, replacement, Output, 32)); m.Finish();
    }

    [HostTest("m16-011 actual active FIN retains TIME-WAIT until the full monotonic 240 seconds", HostLane.Fast, 8)]
    private static void TimeWait()
    {
        using Model m = new(); uint listener = m.Listen(), local = m.Connect(), fd = m.Accept(listener);
        HostCheck.Equal(0L, m.Call(48, fd, 1)); m.Pump();
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, local + 1, flags: 0x11)));
        HostCheck.Equal(0L, m.Call(3, fd)); m.Pump(240_000_000_999); HostCheck.Equal(1U, m.State->RetainedConnections);
        HostCheck.True(!Network.End(m.State)); m.Pump(240_000_001_000); m.Pump();
        HostCheck.Equal(0U, m.State->RetainedConnections); m.Finish(false);
    }

    [HostTest("m16-011 pending listener dup survives original fd close without redirecting to a reused fd", HostLane.Fast, 9)]
    private static void ListenerDup()
    {
        using Model m = new(); uint listener = m.Listen();
        HostCheck.Equal(Network.Status.Success, m.Receive(Syn())); uint duplicate = (uint)m.Call(72, listener, 0);
        HostCheck.Equal(0L, m.Call(3, listener)); m.Pump(); HostCheck.Equal(1U, m.State->RetainedConnections);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, 701, flags: 0x10)));
        uint accepted = m.Accept(duplicate); HostCheck.Equal(listener, accepted); HostCheck.Equal(-11L, m.Call(45, accepted, Output, 1)); m.Finish();
    }

    [HostTest("m16-011 final ACK may establish after SYN-ACK attempt limit until the original deadline", HostLane.Fast, 10)]
    private static void FinalAckAfterAttempts()
    {
        using Model m = new(); uint listener = m.Listen(); HostCheck.Equal(Network.Status.Success, m.Receive(Syn()));
        m.Pump(1_000_001_000); m.Pump(2_000_001_000); m.Pump(3_000_001_000);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, 701, flags: 0x10)));
        _ = m.Accept(listener); HostCheck.True(!m.State->RequiresShutdown); m.Finish();
    }

    [HostTest("m16-011 fatal pending and established TX retain ownership until explicit detached abort", HostLane.Fast, 11)]
    private static void FatalCopy()
    {
        foreach (bool established in new[] { false, true })
        {
            using Model m = new(); uint listener = m.Listen();
            if (established)
            {
                _ = m.Connect(); uint fd = m.Accept(listener); m.Files.Text(User, "data"); HostCheck.Equal(4L, m.Call(44, fd, User, 4));
                m.Hardware->Result = 2; HostCheck.Equal(Network.Status.TransportFailure, Network.Pump(m.State, m.Frame));
            }
            else { m.Hardware->Result = 2; HostCheck.Equal(Network.Status.TransportFailure, m.Receive(Syn())); }
            HostCheck.True(m.State->RequiresShutdown); HostCheck.Equal(1U, m.State->RetainedConnections);
            HostCheck.True(!Network.Abort(m.State)); HostCheck.True(!Network.End(m.State)); m.Finish();
        }
    }

    [HostTest("m16-011 private queued reset reaps after listener close without re-aborting closed TCP", HostLane.Fast, 12)]
    private static void PrivateReset()
    {
        using Model m = new(); uint listener = m.Listen(1); _ = m.Connect(); uint local = m.Connect(40001, 199);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(200, ReadOnlySpan<byte>.Empty, local, port: 40001, flags: 0x14)));
        HostCheck.Equal(0L, m.Call(3, listener)); m.Pump(); HostCheck.True(!m.State->RequiresShutdown);
        HostCheck.Equal(1U, m.State->RetainedConnections); HostCheck.Equal(1, m.Table->Count); m.Finish();
    }

    [HostTest("m16-011 final handshake ACK payload reaches the accepted stream exactly once", HostLane.Fast, 13)]
    private static void HandshakePayload()
    {
        using Model m = new(); uint listener = m.Listen(); HostCheck.Equal(Network.Status.Success, m.Receive(Syn()));
        byte[] third = Wire.Packet(100, "third"u8, 701, flags: 0x10);
        HostCheck.Equal(Network.Status.Success, m.Receive(third)); uint fd = m.Accept(listener);
        HostCheck.Equal(5L, m.Call(45, fd, Output, 32));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 5).SequenceEqual("third"u8));
        var duplicate = m.Receive(third); HostCheck.True(duplicate is Network.Status.Success or Network.Status.Ignored);
        HostCheck.Equal(-11L, m.Call(45, fd, Output, 32)); m.Finish();
    }

    [HostTest("m16-011 fresh traffic keeps both accepted peers live beyond the first establishment deadline", HostLane.Fast, 14)]
    private static void FreshTrafficBeyondEstablishmentDeadline()
    {
        using Model m = new(); uint listener = m.Listen(), first = m.Connect(), a = m.Accept(listener);
        m.Hardware->Now = 20_000_000_000; uint second = m.Connect(40001, 199), b = m.Accept(listener);
        m.Hardware->Now = 29_000_000_000;
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, "fresh!"u8, first)));
        HostCheck.Equal(6L, m.Call(45, a, Output, 32));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 6).SequenceEqual("fresh!"u8));

        m.Pump(30_000_002_000);
        HostCheck.True(!m.State->RequiresShutdown); HostCheck.Equal(2U, m.State->RetainedConnections); HostCheck.Equal(2, m.Table->Count);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(106, "first!"u8, first)));
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(200, "second"u8, second, port: 40001)));
        HostCheck.Equal(6L, m.Call(45, a, Output, 32));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 6).SequenceEqual("first!"u8));
        HostCheck.Equal(6L, m.Call(45, b, Output, 32));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 6).SequenceEqual("second"u8));
        m.Files.Text(User, "reply"); HostCheck.Equal(5L, m.Call(44, a, User, 5)); HostCheck.Equal(5L, m.Call(44, b, User, 5));
        m.Pump();
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(112, ReadOnlySpan<byte>.Empty, first + 5, flags: 0x10)));
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(206, ReadOnlySpan<byte>.Empty, second + 5, port: 40001, flags: 0x10)));
        HostCheck.Equal(-11L, m.Call(45, a, Output, 1)); HostCheck.Equal(-11L, m.Call(45, b, Output, 1));
        HostCheck.True(!m.State->RequiresShutdown); HostCheck.Equal(2, m.Table->Count); m.Finish();
    }

    [HostTest("m07-027 retry exhaustion reports only its peer timeout and preserves healthy socket progress", HostLane.Fast, 15)]
    private static void RetryTimeoutIsLocal()
    {
        using Model m = new(); uint listener = m.Listen(), first = m.Connect(), a = m.Accept(listener);
        uint second = m.Connect(40001, 199), b = m.Accept(listener);
        uint epoll = (uint)m.Call(291); *(uint*)m.Files.Memory.At(User) = 5; *(ulong*)m.Files.Memory.At(User + 4) = 0xabc;
        HostCheck.Equal(0L, m.Call(233, epoll, 1, a, User));
        m.Refresh(); HostCheck.Equal(1L, m.Call(232, epoll, Output, 1, 0));
        m.Files.Text(User, "lost"); HostCheck.Equal(4L, m.Call(44, a, User, 4)); m.Pump();
        foreach (ulong seconds in new ulong[] { 1, 3, 7, 15, 31, 63 }) m.Pump(seconds * 1_000_000_000 + 1000);
        HostCheck.True(!m.State->RequiresShutdown);
        m.Refresh(); HostCheck.Equal(1L, m.Call(232, epoll, Output, 1, 0));
        HostCheck.Equal(29U, *(uint*)m.Files.Memory.At(Output));
        HostCheck.Equal(-110L, m.Call(45, a, Output, 1)); HostCheck.Equal(0L, m.Call(45, a, Output, 1));
        m.Refresh(); HostCheck.Equal(1L, m.Call(232, epoll, Output, 1, 0));
        HostCheck.Equal(21U, *(uint*)m.Files.Memory.At(Output));
        HostCheck.Equal(-32L, m.Call(44, a, User, 4));
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(200, "alive"u8, second, port: 40001)));
        HostCheck.Equal(5L, m.Call(45, b, Output, 8));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 5).SequenceEqual("alive"u8));
        HostCheck.Equal(0L, m.Call(3, a)); m.Pump(); HostCheck.Equal(1U, m.State->RetainedConnections);
        HostCheck.Equal(1, m.Table->Count);
        HostCheck.True(new ReadOnlySpan<byte>(m.Buffers, 8192 + 32768).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.Equal(Network.Status.Ignored, m.Receive(Wire.Packet(100, "stale"u8, first)));
        HostCheck.Equal(-9L, m.Call(45, a, Output, 1)); m.Finish();
    }

    [HostTest("m07-027 a send observing peer close expiry returns timeout without faulting other sockets", HostLane.Fast, 16)]
    private static void SyscallCloseTimeoutIsLocal()
    {
        using Model m = new(); uint listener = m.Listen(), first = m.Connect(), a = m.Accept(listener);
        uint second = m.Connect(40001, 199), b = m.Accept(listener);
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, first, flags: 0x11)));
        m.Hardware->Now = 30_000_002_000; m.Files.Text(User, "late");
        HostCheck.Equal(-110L, m.Call(44, a, User, 4)); m.Pump();
        HostCheck.True(!m.State->RequiresShutdown);
        HostCheck.Equal(-32L, m.Call(44, a, User, 4));
        HostCheck.Equal(Network.Status.Success, m.Receive(Wire.Packet(200, "alive"u8, second, port: 40001)));
        HostCheck.Equal(5L, m.Call(45, b, Output, 8)); m.Finish();
    }

    [HostTest("m07-027 timeout SO_ERROR is consumed only after successful application copy", HostLane.Fast, 18)]
    private static void TimeoutSocketErrorCopy()
    {
        using Model m = new(); uint listener = m.Listen(); m.Connect(); uint fd = m.Accept(listener);
        m.Files.Text(User, "lost"); HostCheck.Equal(4L, m.Call(44, fd, User, 4)); m.Pump();
        foreach (ulong seconds in new ulong[] { 1, 3, 7, 15, 31, 63 }) m.Pump(seconds * 1_000_000_000 + 1000);
        *(uint*)m.Files.Memory.At(User + 64) = 4;
        var call = new LinuxSyscallFrame { Number = 55, Argument0 = fd, Argument1 = 1, Argument2 = 4,
            Argument3 = 0xdead0000, Argument4 = User + 64 };
        HostCheck.Equal(-14L, Fds.Dispatch(m.Files.State, call));
        call.Argument3 = Output; HostCheck.Equal(0L, Fds.Dispatch(m.Files.State, call));
        HostCheck.Equal(110, *(int*)m.Files.Memory.At(Output));
        HostCheck.Equal(0L, Fds.Dispatch(m.Files.State, call)); HostCheck.Equal(0, *(int*)m.Files.Memory.At(Output));
        HostCheck.Equal(0L, m.Call(45, fd, Output, 1)); HostCheck.True(!m.State->RequiresShutdown); m.Finish();
    }

    [HostTest("m07-027 a reversed trusted clock still faults the shared owner and retains backing", HostLane.Fast, 17)]
    private static void CorruptClockRemainsFatal()
    {
        using Model m = new(); uint listener = m.Listen(); m.Connect(); m.Accept(listener); m.Connect(40001, 199); m.Accept(listener);
        m.Hardware->Now = 999;
        HostCheck.Equal(Network.Status.TransportFailure, Network.Pump(m.State, m.Frame));
        HostCheck.True(m.State->RequiresShutdown);
        HostCheck.Equal(2U, m.State->RetainedConnections); HostCheck.Equal(2, m.Table->Count); m.Finish();
    }
}
