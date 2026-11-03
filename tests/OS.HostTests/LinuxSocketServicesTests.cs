using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Net;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Tx = OS.Net.TcpSendWindow;
using Rx = OS.Net.TcpReceiveWindow;
using Wire = TcpReceiveWindowTests;

internal static unsafe partial class LinuxSocketServicesTests
{
    private const ulong User = 0x100000000, Output = User + 24576;
    private const ulong MessageHeader = User + 4096, MessageVectors = User + 8192;
    private static readonly InspectSocket Inspect = typeof(Sockets).GetMethod("InspectRetainedStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<InspectSocket>();
    private static readonly RetainSocket Retain = typeof(Fds).GetMethod("SocketCanRetainStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<RetainSocket>();
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "MessageContext")]
    private static extern ref void* MessageContext(ref Sockets.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "MessageContextBytes")]
    private static extern ref uint MessageContextBytes(ref Sockets.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Entries")]
    private static extern ref TcpConnectionTableEntry* TableEntries(ref TcpConnectionTable.Owner state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Generations")]
    private static extern ref ulong* TableGenerations(ref TcpConnectionTable.Owner state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ObserveMessage")]
    private static extern ref delegate* unmanaged<void*, ulong, uint, ulong, ulong, ulong, ulong*, uint, uint, byte> MessageObserver(ref Sockets.State state);
    [UnmanagedCallersOnly]
    private static byte ObserveMessage(void* context, ulong number, uint fd, ulong header, ulong flags, ulong table, ulong* vectors, uint count, uint total)
    {
        MessageTrace* trace = (MessageTrace*)context;
        trace->Calls++;
        trace->Number = number;
        trace->Fd = fd;
        trace->Header = header;
        trace->Table = table;
        trace->Count = count;
        trace->Total = total;
        if (count != 0)
        {
            trace->FirstAddress = vectors[0];
            trace->FirstBytes = vectors[1];
        }

        return trace->Reject == 0 ? (byte)1 : (byte)0;
    }

    [UnmanagedCallersOnly]
    private static byte OtherObserver(void* context, ulong number, uint fd, ulong header, ulong flags, ulong table, ulong* vectors, uint count, uint total) => 1;
    private static void Message(Model model, ulong address, ulong bytes)
    {
        new Span<byte>(model.Files.Memory.At(MessageHeader), 56).Clear();
        ulong* header = (ulong*)model.Files.Memory.At(MessageHeader);
        header[2] = MessageVectors;
        header[3] = 1;
        ulong* vector = (ulong*)model.Files.Memory.At(MessageVectors);
        vector[0] = address;
        vector[1] = bytes;
    }

    private static uint AcceptBounded(Model model, TcpSendWindowTests.Fixture connection)
    {
        uint listener = model.Listen();
        HostCheck.True(Fds.Capture(model.Files.State, listener, out var token));
        HostCheck.True(Sockets.EnqueueAccepted(model.State, token, listener, connection.Receiver, connection.Owner, (uint)sizeof(TcpSendWindowTests.Hardware)));
        long fd = model.Call(288, listener, 0, 0, Fds.NonBlock);
        HostCheck.True(fd >= 0);
        return (uint)fd;
    }

    internal static TcpSendWindowTests.Fixture Connection()
    {
        var f = new TcpSendWindowTests.Fixture(capacity: 8, receiveCapacity: 8);
        var io = f.Operations();
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 1_000_000_000_000));
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(f.Owner, 30_000_000_000));
        return f;
    }

    internal static void AbortAndReap(Model m, TcpSendWindowTests.Fixture f)
    {
        if (f.Owner->Active)
            HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
        HostCheck.True(Sockets.ReapClosed(m.State, f.Owner));
        HostCheck.Equal(0, f.Table->Count);
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 8).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.True(new ReadOnlySpan<byte>(f.ReceiveBuffer, 8).IndexOfAnyExcept((byte)0) < 0);
    }

    [HostTest("m16-011 socket admission rejects aliases and dirty storage without publication", HostLane.Fast, 0)]
    private static void Admission()
    {
        using var files = new LinuxDescriptorServicesTests.Owner();
        files.Begin();
        Sockets.State state = default;
        HostCheck.True(!Sockets.Begin(&state, files.State, (Sockets.Endpoint*)files.Admission.Scratch, 4, new(10, 0, 2, 15), 8080));
        HostCheck.Equal(3U, files.State->ActiveDescriptors);
        HostCheck.True(Fds.Audit(files.State));
        Sockets.Endpoint* slots = stackalloc Sockets.Endpoint[4];
        new Span<byte>(slots, 4 * sizeof(Sockets.Endpoint)).Clear();
        ((byte*)slots)[0] = 1;
        HostCheck.True(!Sockets.Begin(&state, files.State, slots, 4, new(10, 0, 2, 15), 8080));
        HostCheck.True(Fds.End(files.State));
    }

    [HostTest("bounded message observer rejects invalid and descriptor-owned extents without publication or callbacks")]
    private static void BoundedObserverAdmission()
    {
        using var m = new Model();
        MessageTrace trace = default;
        byte[] state = new ReadOnlySpan<byte>(m.State, sizeof(Sockets.State)).ToArray();
        var a = m.Files.Admission;
        foreach ((ulong start, uint bytes) in new[]
        {
            (0UL, 8U),
            (1UL, 8U),
            ((ulong)&trace, 0U),
            ((ulong)&trace, 7U),
            ((ulong)&trace, Sockets.MaximumSharedContextBytes + 1),
            ((ulong)&trace + 1, 8U),
            (0x00007ffffffffff8UL, 16U),
            (ulong.MaxValue - 7, 16U),
            ((ulong)m.State, (uint)sizeof(Sockets.State)),
            ((ulong)m.Endpoints, 8U),
            ((ulong)m.Files.State, 8U),
            ((ulong)a.Slots, 8U),
            ((ulong)a.Descriptions, 8U),
            ((ulong)a.Scratch, 8U),
            ((ulong)m.Files.Memory.Memory.Pool, 8U),
            ((ulong)m.Files.Memory.Memory.Tables, 8U),
            ((ulong)a.Views, 8U)
        }

        )
        {
            HostCheck.True(!Sockets.AttachMessageObserver(m.State, (void*)start, bytes, &ObserveMessage));
            HostCheck.True(new ReadOnlySpan<byte>(m.State, sizeof(Sockets.State)).SequenceEqual(state));
            HostCheck.True(Fds.Audit(m.Files.State) && Inspect(m.State, m.Files.State));
            HostCheck.Equal(0U, trace.Calls);
        }

        HostCheck.True(!Sockets.AttachMessageObserver(m.State, &trace, (uint)sizeof(MessageTrace), null));
        HostCheck.True(Sockets.AttachMessageObserver(m.State, &trace, (uint)sizeof(MessageTrace), &ObserveMessage));
        HostCheck.True(!Sockets.AttachMessageObserver(m.State, &trace, (uint)sizeof(MessageTrace), &ObserveMessage));
        HostCheck.Equal(0U, trace.Calls);
        m.End();
        HostCheck.True(!m.State->HasMessageObserver);
    }

    [HostTest("bounded message observer rejects exact containing and crossing actual transport extents")]
    private static void BoundedObserverTransportAliases()
    {
        using var m = new Model();
        using var f = Connection();
        uint fd = AcceptBounded(m, f);
        var extents = new[]
        {
            ((ulong)f.Receiver, (uint)sizeof(Rx.Owner)),
            ((ulong)f.Owner, (uint)sizeof(Tx.Owner)),
            ((ulong)f.ReceiveBuffer, 8U),
            ((ulong)f.Buffer, 8U),
            ((ulong)f.Table, (uint)sizeof(TcpConnectionTable.Owner)),
            ((ulong)TableEntries(ref *f.Table), (uint)sizeof(TcpConnectionTableEntry)),
            ((ulong)TableGenerations(ref *f.Table), 8U),
            ((ulong)f.Hardware, (uint)sizeof(TcpSendWindowTests.Hardware))
        };
        byte[] state = new ReadOnlySpan<byte>(m.State, sizeof(Sockets.State)).ToArray();
        byte[] transport = new ReadOnlySpan<byte>(f.Owner, sizeof(Tx.Owner)).ToArray();
        uint sends = f.Hardware->Calls;
        foreach ((ulong start, uint bytes) in extents)
            foreach ((ulong address, uint count) in new[]
            {
                (start, bytes),
                (start - 8, bytes + 16),
                (start - 8, 16U),
                (start + bytes - 8, 16U)
            }

            )
            {
                HostCheck.True(!Sockets.AttachMessageObserver(m.State, (void*)address, count, &ObserveMessage));
                HostCheck.True(state.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(m.State, sizeof(Sockets.State))));
                HostCheck.True(transport.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Owner, sizeof(Tx.Owner))));
                HostCheck.Equal(sends, f.Hardware->Calls);
                HostCheck.True(Inspect(m.State, m.Files.State));
            }

        HostCheck.Equal(0L, m.Call(3, fd));
        AbortAndReap(m, f);
        m.End();
    }

    [HostTest("bounded message observer retains whole carrier until finish and refuses before send or receive mutation")]
    private static void BoundedObserverMessages()
    {
        using var m = new Model();
        using var f = Connection();
        uint fd = AcceptBounded(m, f);
        byte* allocation = (byte*)NativeMemory.AlignedAlloc(256, 16);
        try
        {
            new Span<byte>(allocation, 256).Clear();
            MessageTrace* trace = (MessageTrace*)(allocation + 64);
            trace->OpaqueChild = (ulong)(allocation + 224);
            allocation[224] = 0x7d;
            HostCheck.True(Sockets.AttachMessageObserver(m.State, trace, (uint)sizeof(MessageTrace), &ObserveMessage));
            HostCheck.True(Inspect(m.State, m.Files.State));
            HostCheck.True(!Retain(m.Files.State, (ulong)trace, (ulong)sizeof(MessageTrace)) && !Retain(m.Files.State, (ulong)trace - 8, (ulong)sizeof(MessageTrace) + 16) && !Retain(m.Files.State, (ulong)trace - 8, 16) && !Retain(m.Files.State, (ulong)trace + (ulong)sizeof(MessageTrace) - 8, 16));
            HostCheck.True(Retain(m.Files.State, (ulong)trace - 8, 8) && Retain(m.Files.State, (ulong)trace + (ulong)sizeof(MessageTrace), 8));
            // No transitive authority is invented for an opaque pointer value.
            HostCheck.True(Retain(m.Files.State, trace->OpaqueChild, 8));
            m.Files.Text(User, "abc");
            Message(m, User, 3);
            trace->Reject = 1;
            HostCheck.Equal(-5L, m.Call(46, fd, MessageHeader));
            HostCheck.Equal(0, f.Owner->BufferedBytes);
            trace->Reject = 0;
            HostCheck.Equal(3L, m.Call(46, fd, MessageHeader));
            HostCheck.Equal(46UL, trace->Number);
            HostCheck.Equal(fd, trace->Fd);
            HostCheck.Equal(MessageHeader, trace->Header);
            HostCheck.Equal(MessageVectors, trace->Table);
            HostCheck.Equal(User, trace->FirstAddress);
            HostCheck.Equal(3UL, trace->FirstBytes);
            HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, "xyz"u8, ack: 700, flags: 0x18), out _));
            Message(m, Output, 3);
            trace->Reject = 1;
            HostCheck.Equal(-5L, m.Call(47, fd, MessageHeader));
            HostCheck.Equal(3, f.Receiver->UnreadBytes);
            trace->Reject = 0;
            HostCheck.Equal(3L, m.Call(47, fd, MessageHeader));
            HostCheck.Equal(0, f.Receiver->UnreadBytes);
            HostCheck.Equal(4U, trace->Calls);
            HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 3).SequenceEqual("xyz"u8));
            HostCheck.Equal((byte)0x7d, allocation[224]);
            HostCheck.Equal(0L, m.Call(3, fd));
            AbortAndReap(m, f);
            HostCheck.True(!Retain(m.Files.State, (ulong)trace, (ulong)sizeof(MessageTrace)));
            m.End();
            HostCheck.True(!m.State->HasMessageObserver);
            HostCheck.Equal(0U, MessageContextBytes(ref *m.State));
        }
        finally
        {
            NativeMemory.AlignedFree(allocation);
        }
    }

    [HostTest("bounded message observer rejects changed pointer callback size and aliases with no effects after restoration")]
    private static void BoundedObserverLifetime()
    {
        using var m = new Model();
        using var f = Connection();
        uint fd = AcceptBounded(m, f);
        MessageTrace trace = default, other = default;
        HostCheck.True(Sockets.AttachMessageObserver(m.State, &trace, (uint)sizeof(MessageTrace), &ObserveMessage));
        byte[] owner = new ReadOnlySpan<byte>(f.Owner, sizeof(Tx.Owner)).ToArray();
        byte[] data = new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 16).ToArray();
        for (int changed = 0; changed < 5; changed++)
        {
            void* context = MessageContext(ref *m.State);
            uint bytes = MessageContextBytes(ref *m.State);
            var observer = MessageObserver(ref *m.State);
            try
            {
                if (changed == 0)
                    MessageContext(ref *m.State) = &other;
                else if (changed == 1)
                    MessageObserver(ref *m.State) = &OtherObserver;
                else if (changed == 2)
                    MessageContextBytes(ref *m.State) = 0;
                else if (changed == 3)
                    MessageContextBytes(ref *m.State) = Sockets.MaximumSharedContextBytes + 1;
                else
                    MessageContext(ref *m.State) = f.Buffer;
                HostCheck.True(!Fds.Audit(m.Files.State) && !Inspect(m.State, m.Files.State));
                HostCheck.True(!Retain(m.Files.State, (ulong)&other, (ulong)sizeof(MessageTrace)));
                HostCheck.True(owner.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Owner, sizeof(Tx.Owner))));
                HostCheck.True(data.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 16)));
                HostCheck.True(trace.Calls == 0 && other.Calls == 0);
            }
            finally
            {
                MessageContext(ref *m.State) = context;
                MessageContextBytes(ref *m.State) = bytes;
                MessageObserver(ref *m.State) = observer;
            }

            HostCheck.True(Fds.Audit(m.Files.State) && Inspect(m.State, m.Files.State));
        }

        m.Files.Text(User, "ok");
        Message(m, User, 2);
        HostCheck.Equal(2L, m.Call(46, fd, MessageHeader));
        HostCheck.Equal(1U, trace.Calls);
        HostCheck.Equal(0L, m.Call(3, fd));
        AbortAndReap(m, f);
        m.End();
    }

    [HostTest("bounded message observer follows genuine network admission and excludes all its retained extents")]
    private static void BoundedObserverNetwork()
    {
        using var m = new Model();
        LinuxEpollServices.State epoll = default;
        LinuxEpollServices.Port* ports = stackalloc LinuxEpollServices.Port[2];
        LinuxEpollServices.Registration* registrations = stackalloc LinuxEpollServices.Registration[16];
        new Span<byte>(ports, 2 * sizeof(LinuxEpollServices.Port)).Clear();
        new Span<byte>(registrations, 16 * sizeof(LinuxEpollServices.Registration)).Clear();
        HostCheck.True(LinuxEpollServices.Begin(&epoll, m.Files.State, ports, 2, registrations, 16));
        using var network = new LinuxSocketNetworkTests.PacketCarrier(m.State);
        HostCheck.True(network.Start());
        MessageFrameCarrier carrier = default;
        MessageTrace* trace = &carrier.Trace;
        byte[] state = new ReadOnlySpan<byte>(m.State, sizeof(Sockets.State)).ToArray();
        foreach ((ulong start, ulong bytes) in network.Extents().Concat(new[] { ((ulong)&epoll, (ulong)sizeof(LinuxEpollServices.State)), ((ulong)ports, 2UL * (ulong)sizeof(LinuxEpollServices.Port)), ((ulong)registrations, 16UL * (ulong)sizeof(LinuxEpollServices.Registration)) }))
        {
            HostCheck.True(!Sockets.AttachMessageObserver(m.State, (void*)start, (uint)bytes, &ObserveMessage));
            HostCheck.True(state.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(m.State, sizeof(Sockets.State))));
            HostCheck.True(network.Sends == 0 && network.Clocks == 0 && trace->Calls == 0);
        }

        HostCheck.True(Sockets.AttachMessageObserver(m.State, &carrier, (uint)sizeof(MessageFrameCarrier), &ObserveMessage));
        byte[] before = new ReadOnlySpan<byte>(&carrier, sizeof(MessageFrameCarrier)).ToArray(), frame = new byte[1514];
        HostCheck.Equal(LinuxSocketNetwork.Status.InvalidStorage, LinuxSocketNetwork.Receive(network.State, new ReadOnlySpan<byte>(&carrier, 8), frame));
        HostCheck.Equal(LinuxSocketNetwork.Status.InvalidStorage, LinuxSocketNetwork.Receive(network.State, ReadOnlySpan<byte>.Empty, new Span<byte>(&carrier, 1514)));
        HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(&carrier, sizeof(MessageFrameCarrier))));
        HostCheck.True(network.Sends == 0 && network.Clocks == 0 && trace->Calls == 0);
        uint listener = m.Listen(), sequence = network.Connect(), fd = (uint)m.Call(288, listener, 0, 0, Fds.NonBlock);
        HostCheck.True(fd < 12 && Inspect(m.State, m.Files.State));
        m.Files.Text(User, "ab");
        Message(m, User, 2);
        HostCheck.Equal(2L, m.Call(46, fd, MessageHeader));
        HostCheck.Equal(OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork.Status.Success, network.Receive(Wire.Packet(100, "cd"u8, sequence, flags: 0x18)));
        Message(m, Output, 2);
        HostCheck.Equal(2L, m.Call(47, fd, MessageHeader));
        HostCheck.Equal(2U, trace->Calls);
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 2).SequenceEqual("cd"u8));
        HostCheck.Equal(0L, m.Call(3, fd));
        HostCheck.Equal(0L, m.Call(3, listener));
        for (uint remaining = 0; remaining < m.Files.Admission.DescriptorCount; remaining++)
            if (Fds.Capture(m.Files.State, remaining, out _))
                HostCheck.Equal(0L, m.Call(3, remaining));
        network.Retire();
        HostCheck.True(!Retain(m.Files.State, (ulong)&carrier, (ulong)sizeof(MessageFrameCarrier)));
        m.End();
    }

    [HostTest("bounded message observer preserves legacy unbounded mode and the network-first ordering boundary")]
    private static void BoundedObserverCompatibility()
    {
        using (var m = new Model())
        {
            MessageTrace trace = default;
            HostCheck.True(Sockets.AttachMessageObserver(m.State, &trace, &ObserveMessage));
            HostCheck.True(Fds.Audit(m.Files.State) && !Inspect(m.State, m.Files.State));
            HostCheck.True(!Sockets.AttachMessageObserver(m.State, &trace, (uint)sizeof(MessageTrace), &ObserveMessage));
            HostCheck.Equal(0U, trace.Calls);
            m.End();
        }

        using (var m = new Model())
        {
            LinuxEpollServices.State epoll = default;
            LinuxEpollServices.Port* ports = stackalloc LinuxEpollServices.Port[2];
            LinuxEpollServices.Registration* registrations = stackalloc LinuxEpollServices.Registration[16];
            new Span<byte>(ports, 2 * sizeof(LinuxEpollServices.Port)).Clear();
            new Span<byte>(registrations, 16 * sizeof(LinuxEpollServices.Registration)).Clear();
            HostCheck.True(LinuxEpollServices.Begin(&epoll, m.Files.State, ports, 2, registrations, 16));
            using var network = new LinuxSocketNetworkTests.PacketCarrier(m.State);
            MessageTrace trace = default;
            HostCheck.True(Sockets.AttachMessageObserver(m.State, &trace, (uint)sizeof(MessageTrace), &ObserveMessage));
            HostCheck.True(!network.Start());
            HostCheck.True(network.Sends == 0 && network.Clocks == 0);
            HostCheck.Equal(TcpConnectionTable.OwnershipStatus.Success, TcpConnectionTable.RetireOwned(network.Table));
            m.End();
        }

        foreach (bool guarded in new[]
        {
            false,
            true
        }

        )
        {
            var catalog = HostSourceCatalog.Efi(OS.Tooling.EfiProfile.Create("Diagnostic",
                new Dictionary<string, bool> { [guarded ? "EnableM16027Probe" : "EnableM16011Probe"] = true }));
            string[] fields = catalog.Type(typeof(Sockets.State).FullName!).GetMembers()
                .OfType<Microsoft.CodeAnalysis.IFieldSymbol>().Select(field => field.Name).ToArray();
            HostCheck.Equal(guarded, fields.Contains("OriginalMessageContext"));
            HostCheck.Equal(guarded, fields.Contains("MessageContextBytes"));
            HostCheck.Equal(guarded, fields.Contains("OriginalObserveMessage"));
            HostCheck.Equal(guarded ? 2 : 1, catalog.Type(typeof(Sockets).FullName!).GetMembers("AttachMessageObserver")
                .OfType<Microsoft.CodeAnalysis.IMethodSymbol>().Count());
        }
    }

    [HostTest("m16-011 family probes distinguish unsupported socket type and preserve fd accounting", HostLane.Fast, 1)]
    private static void ProtocolAndCapacity()
    {
        using var m = new Model(capacity: 1);
        HostCheck.Equal(-94L, m.Call(41, 2, 2 | Fds.CloseOnExec, 0));
        HostCheck.Equal(-97L, m.Call(41, 10, 2 | Fds.CloseOnExec, 0));
        HostCheck.Equal(-97L, m.Call(41, 1, 2, 0));
        HostCheck.Equal(-93L, m.Call(41, 2, 1, 17));
        HostCheck.Equal(-22L, m.Call(41, 2, 0x100001, 6));
        HostCheck.Equal(3U, m.Files.State->ActiveDescriptors);
        uint fd = m.Socket();
        HostCheck.Equal(3U, fd);
        HostCheck.Equal(-105L, m.Call(41, 2, 1, 6));
        HostCheck.Equal(0L, m.Call(3, fd));
        HostCheck.Equal(fd, m.Socket());
        m.End();
    }

    [HostTest("m16-011 socket dup shares flags and last-close retains one endpoint", HostLane.Fast, 2)]
    private static void DescriptorNamespace()
    {
        using var m = new Model(capacity: 1);
        uint fd = m.Socket();
        HostCheck.Equal(2L | Fds.NonBlock, m.Call(72, fd, 3));
        HostCheck.Equal(1L, m.Call(72, fd, 1));
        HostCheck.True(Fds.Capture(m.Files.State, fd, out var stale));
        uint dup = (uint)m.Call(72, fd, 0, 0);
        HostCheck.Equal(4U, dup);
        HostCheck.Equal(0L, m.Call(72, dup, 4, 0x8802));
        HostCheck.Equal(2L | Fds.NonBlock, m.Call(72, fd, 3));
        HostCheck.Equal(0L, m.Call(3, fd));
        HostCheck.True(!Fds.IsCurrent(m.Files.State, stale));
        HostCheck.Equal(-105L, m.Call(41, 2, 1, 6));
        HostCheck.Equal(0L, m.Call(3, dup));
        HostCheck.Equal(fd, m.Socket());
        HostCheck.True(!Fds.IsCurrent(m.Files.State, stale));
        m.End();
    }

    [HostTest("m16-011 bind enforces exact manifest endpoint and preserves listener accounting", HostLane.Fast, 3)]
    private static void BindPolicy()
    {
        using var m = new Model();
        uint fd = m.Socket();
        m.Address(8081);
        HostCheck.Equal(-13L, m.Call(49, fd, User, 16));
        m.Address(last: 2);
        HostCheck.Equal(-13L, m.Call(49, fd, User, 16));
        m.Address(last: 15);
        HostCheck.Equal(-22L, m.Call(49, fd, User, 15));
        m.Files.Context->FailInputAt = m.Files.Context->InputCopies + 1;
        HostCheck.Equal(-14L, m.Call(49, fd, User, 16));
        m.Files.Context->FailInputAt = 0;
        HostCheck.Equal(0L, m.Call(49, fd, User, 16));
        HostCheck.Equal(0L, m.Call(50, fd, 4));
        HostCheck.Equal(-11L, m.Call(288, fd, 0, 0, Fds.NonBlock));
        uint second = m.Socket();
        m.Address();
        HostCheck.Equal(-98L, m.Call(49, second, User, 16));
        HostCheck.Equal(-88L, m.Call(50, 1, 4));
        HostCheck.Equal(-9L, m.Call(50, 31, 4));
        m.End();
    }

    [HostTest("m16-011 actual TCP accept preserves backlog on user-copy and descriptor exhaustion", HostLane.Fast, 4)]
    private static void AcceptFailures()
    {
        using var m = new Model(descriptors: 5);
        using var f = Connection();
        uint listener = m.Listen();
        HostCheck.True(Fds.Capture(m.Files.State, listener, out var token));
        HostCheck.True(Sockets.EnqueueAccepted(m.State, token, listener, f.Receiver, f.Owner));
        HostCheck.True(!Sockets.EnqueueAccepted(m.State, token, listener, f.Receiver, f.Owner));
        *(uint*)m.Files.Memory.At(Output) = 16;
        m.Files.Context->FailOutput = true;
        HostCheck.Equal(-14L, m.Call(288, listener, User, Output, Fds.NonBlock));
        HostCheck.Equal(1U, m.State->RetainedConnections);
        m.Files.Context->FailOutput = false;
        uint blocker = m.Socket();
        HostCheck.Equal(-24L, m.Call(288, listener, User, Output, Fds.NonBlock));
        HostCheck.Equal(0L, m.Call(3, blocker));
        uint accepted = (uint)m.Call(288, listener, User, Output, Fds.NonBlock);
        HostCheck.Equal(4U, accepted);
        HostCheck.Equal(16U, *(uint*)m.Files.Memory.At(Output));
        HostCheck.Equal((byte)0x9c, m.Files.Memory.At(User)[2]);
        HostCheck.Equal((byte)0x40, m.Files.Memory.At(User)[3]);
        HostCheck.Equal(0L, m.Call(3, accepted));
        AbortAndReap(m, f);
        m.End();
    }

    [HostTest("m16-011 TCP partial sends and copy-failed reads retain exact owned bytes", HostLane.Fast, 5)]
    private static void PartialIo()
    {
        using var m = new Model();
        using var f = Connection();
        uint listener = m.Listen(), fd = m.Accept(listener, f);
        m.Option(fd, 1, 7, 4);
        m.Option(fd, 6, 1, 1);
        m.Option(listener, 1, 2, 1);
        m.Files.Text(User, "abcdefghij");
        HostCheck.Equal(4L, m.Call(44, fd, User, 10, 0x4000));
        HostCheck.Equal(-11L, m.Call(44, fd, User, 1));
        HostCheck.Equal(4, f.Owner->BufferedBytes);
        byte[] output = new byte[1514];
        HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(704, out _));
        HostCheck.Equal(4L, m.Call(1, fd, User + 4, 6));
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, "xyz"u8, ack: 704, flags: 0x18), out _));
        m.Files.Context->FailOutput = true;
        HostCheck.Equal(-14L, m.Call(45, fd, Output, 2));
        HostCheck.Equal(3, f.Receiver->UnreadBytes);
        m.Files.Context->FailOutput = false;
        m.Files.Context->RequiredOutputSource = m.Files.Admission.Scratch;
        HostCheck.Equal(2L, m.Call(45, fd, Output, 2));
        HostCheck.Equal((byte)'x', m.Files.Memory.At(Output)[0]);
        HostCheck.Equal(1L, m.Call(0, fd, Output, 9));
        HostCheck.Equal((byte)'z', m.Files.Memory.At(Output)[0]);
        HostCheck.Equal(-11L, m.Call(45, fd, Output, 1));
        HostCheck.Equal(0L, m.Call(3, fd));
        HostCheck.Equal(1U, m.State->RetainedConnections);
        HostCheck.True(!Fds.End(m.Files.State));
        AbortAndReap(m, f);
        m.End();
    }

    [HostTest("m16-011 peer FIN drains data to EOF and reset remains distinct until last close", HostLane.Fast, 6)]
    private static void EofAndReset()
    {
        using (var m = new Model())
        using (var f = Connection())
        {
            uint fd = m.Accept(m.Listen(), f);
            HostCheck.Equal(Tx.Status.PeerClosed, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, "end"u8, ack: 700, flags: 0x19), out _));
            HostCheck.Equal(0L, m.Call(48, fd, 1));
            HostCheck.Equal(Tx.Status.Success, Tx.PumpClose(f.Owner, new byte[1514], out _));
            HostCheck.Equal(Tx.Status.ConnectionClosed, Tx.ReceiveFrame(f.Owner, Wire.Packet(104, ReadOnlySpan<byte>.Empty, ack: 701, flags: 0x10), out _));
            HostCheck.Equal(3L, m.Call(45, fd, Output, 8));
            HostCheck.Equal(0L, m.Call(45, fd, Output, 8));
            HostCheck.Equal(0L, m.Call(48, fd, 1));
            HostCheck.Equal(0L, m.Call(48, fd, 1));
            HostCheck.Equal(-32L, m.Call(44, fd, User, 1));
            HostCheck.Equal(0L, m.Call(3, fd));
            AbortAndReap(m, f);
            m.End();
        }

        using (var m = new Model())
        using (var f = Connection())
        {
            uint fd = m.Accept(m.Listen(), f), dup = (uint)m.Call(72, fd, 0, 0);
            HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, "R"u8, ack: 700, flags: 0x18), out _));
            HostCheck.Equal(Tx.Status.ConnectionReset, Tx.ReceiveFrame(f.Owner, Wire.Packet(101, ReadOnlySpan<byte>.Empty, ack: 700, flags: 0x14), out _));
            HostCheck.Equal(Rx.Status.TableRejected, Rx.Read(f.Receiver, new byte[1], out _));
            m.Files.Context->FailOutput = true;
            HostCheck.Equal(-14L, m.Call(45, fd, Output, 1));
            m.Files.Context->FailOutput = false;
            HostCheck.Equal(1, f.Receiver->UnreadBytes);
            HostCheck.Equal(1L, m.Call(45, fd, Output, 1));
            HostCheck.Equal((byte)'R', *m.Files.Memory.At(Output));
            HostCheck.Equal(0, f.Receiver->UnreadBytes);
            *(uint*)m.Files.Memory.At(Output) = 4;
            m.Files.Context->FailOutput = true;
            HostCheck.Equal(-14L, m.Call(55, fd, 1, 4, User, Output));
            m.Files.Context->FailOutput = false;
            HostCheck.Equal(-104L, m.Call(45, fd, Output, 1));
            HostCheck.Equal(-32L, m.Call(44, fd, User, 1));
            HostCheck.Equal(0L, m.Call(45, fd, Output, 1));
            HostCheck.Equal(0L, m.Call(55, fd, 1, 4, User, Output));
            HostCheck.Equal(0, *(int*)m.Files.Memory.At(User));
            HostCheck.True(!Sockets.ReapClosed(m.State, f.Owner));
            HostCheck.Equal(0L, m.Call(3, fd));
            HostCheck.True(!Sockets.ReapClosed(m.State, f.Owner));
            HostCheck.Equal(0L, m.Call(3, dup));
            AbortAndReap(m, f);
            m.End();
        }
    }

    [HostTest("m16-011 listener last close retains queued TCP for explicit closed scrub", HostLane.Fast, 7)]
    private static void ListenerClose()
    {
        using var m = new Model();
        using var f = Connection();
        uint listener = m.Listen();
        HostCheck.True(Fds.Capture(m.Files.State, listener, out var token));
        HostCheck.True(Sockets.EnqueueAccepted(m.State, token, listener, f.Receiver, f.Owner));
        HostCheck.Equal(0L, m.Call(3, listener));
        HostCheck.True(Fds.Audit(m.Files.State));
        HostCheck.True(!Sockets.EnqueueAccepted(m.State, token, listener, f.Receiver, f.Owner));
        HostCheck.True(f.Owner->WritesClosed);
        HostCheck.Equal(1U, m.State->RetainedConnections);
        uint next = m.Socket();
        m.Address();
        HostCheck.Equal(-98L, m.Call(49, next, User, 16));
        m.Option(next, 1, 2, 1);
        m.Address();
        HostCheck.Equal(0L, m.Call(49, next, User, 16));
        AbortAndReap(m, f);
        m.End();
    }

    [HostTest("m16-011 faulted read exposes no bytes and last-close aborts failed clock ownership", HostLane.Fast, 8)]
    private static void FaultedTransport()
    {
        using (var m = new Model())
        using (var f = Connection())
        {
            uint fd = m.Accept(m.Listen(), f);
            HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, "abc"u8, ack: 700, flags: 0x18), out _));
            f.Hardware->Result = 2;
            HostCheck.Equal(Rx.Status.SendFailed, Rx.PumpAck(f.Receiver, new byte[1514], out _));
            uint before = m.Files.Context->OutputCopies;
            HostCheck.Equal(-5L, m.Call(45, fd, Output, 3));
            HostCheck.Equal(before, m.Files.Context->OutputCopies);
            HostCheck.Equal(3, f.Receiver->UnreadBytes);
            HostCheck.Equal(0L, m.Call(3, fd));
            AbortAndReap(m, f);
            m.End();
        }

        using (var m = new Model())
        using (var f = Connection())
        {
            uint fd = m.Accept(m.Listen(), f);
            f.Hardware->Now = 1_000_000_000_100;
            HostCheck.Equal(0L, m.Call(3, fd));
            HostCheck.Equal(TcpConnectionCloseState.Closed, f.Owner->CloseState);
            HostCheck.True(Sockets.ReapClosed(m.State, f.Owner));
            HostCheck.Equal(0, f.Table->Count);
            m.End();
        }
    }

    [HostTest("m16-011 EFI socket references require the explicit TCP feature boundary", HostLane.Fast, 9)]
    private static void FeatureBoundary()
    {
        var off = HostSourceCatalog.Efi(OS.Tooling.EfiProfile.Create("Diagnostic",
            new Dictionary<string, bool> { ["EnableM16009Probe"] = true }));
        var on = HostSourceCatalog.Efi(OS.Tooling.EfiProfile.Create("Diagnostic",
            new Dictionary<string, bool> { ["EnableM16011Probe"] = true }));
        HostCheck.True(off.Compilation.GetDiagnostics().All(diagnostic => diagnostic.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        foreach (Type owner in new[] { typeof(LinuxDescriptorServices), typeof(LinuxThreadServices) })
        {
            HostCheck.True(off.TypeSources(owner.FullName!).Count != 0);
            HostCheck.True(!ReferencesSockets(off, owner));
            HostCheck.True(ReferencesSockets(on, owner));
        }
        HostCheck.True(off.Compilation.GetTypeByMetadataName(typeof(Sockets).FullName!) is null);

        static bool ReferencesSockets(OS.Tooling.CSharpSourceCatalog catalog, Type owner)
        {
            var socket = catalog.Compilation.GetTypeByMetadataName(typeof(Sockets).FullName!);
            return socket is not null && catalog.TypeSources(owner.FullName!).Any(document =>
                document.Syntax.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().Any(identifier =>
                    Microsoft.CodeAnalysis.SymbolEqualityComparer.Default.Equals(
                        catalog.Compilation.GetSemanticModel(document.Syntax).GetSymbolInfo(identifier).Symbol, socket)));
        }
    }

    [HostTest("m16-011 recvfrom peek preserves exact receive bytes window and ACK metadata", HostLane.Fast, 10)]
    private static void PeekPreservesWindow()
    {
        using var m = new Model();
        using var f = Connection();
        uint fd = m.Accept(m.Listen(), f);
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, "abcdef"u8, ack: 700, flags: 0x18), out _));
        HostCheck.Equal(Rx.Status.Success, Rx.PumpAck(f.Receiver, new byte[1514], out _));
        byte[] owner = new ReadOnlySpan<byte>(f.Receiver, sizeof(Rx.Owner)).ToArray();
        byte[] buffer = new ReadOnlySpan<byte>(f.ReceiveBuffer, 8).ToArray();
        m.Files.Context->RequiredOutputSource = m.Files.Admission.Scratch;
        HostCheck.Equal(2L, m.Call(45, fd, Output, 2, 2));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 2).SequenceEqual("ab"u8));
        HostCheck.Equal(6L, m.Call(45, fd, Output, 64, 0x42));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 6).SequenceEqual("abcdef"u8));
        HostCheck.True(new ReadOnlySpan<byte>(f.Receiver, sizeof(Rx.Owner)).SequenceEqual(owner));
        HostCheck.True(new ReadOnlySpan<byte>(f.ReceiveBuffer, 8).SequenceEqual(buffer));
        HostCheck.Equal(6, f.Receiver->UnreadBytes);
        HostCheck.Equal((ushort)2, f.Receiver->AvailableWindow);
        HostCheck.Equal(106U, f.Receiver->AcknowledgementNumber);
        HostCheck.True(!f.Receiver->HasPendingAck);
        HostCheck.Equal(Rx.Status.NothingToAck, Rx.PumpAck(f.Receiver, new byte[1514], out _));
        HostCheck.Equal(6L, m.Call(0, fd, Output, 8));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 6).SequenceEqual("abcdef"u8));
        HostCheck.Equal(0, f.Receiver->UnreadBytes);
        HostCheck.Equal((ushort)8, f.Receiver->AvailableWindow);
        HostCheck.True(f.Receiver->HasPendingAck);
        HostCheck.Equal(0L, m.Call(3, fd));
        AbortAndReap(m, f);
        m.End();
    }

    [HostTest("m16-011 peek rejects invalid outputs flags and message metadata without consuming TCP", HostLane.Fast, 11)]
    private static void PeekFailures()
    {
        using var m = new Model();
        using var f = Connection();
        uint fd = m.Accept(m.Listen(), f);
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, "xyz"u8, ack: 700, flags: 0x18), out _));
        byte[] owner = new ReadOnlySpan<byte>(f.Receiver, sizeof(Rx.Owner)).ToArray();
        new Span<byte>(m.Files.Memory.At(Output), 8).Fill(0xa5);
        uint copies = m.Files.Context->OutputCopies;
        HostCheck.Equal(-14L, m.Call(45, fd, User + 32768, 3, 2));
        HostCheck.Equal(copies, m.Files.Context->OutputCopies);
        m.Files.Context->FailOutput = true;
        HostCheck.Equal(-14L, m.Call(45, fd, Output, 3, 0x42));
        m.Files.Context->FailOutput = false;
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 8).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(-95L, m.Call(45, fd, Output, 3, 0x102));
        HostCheck.Equal(-95L, m.Call(45, fd, Output, 3, 2, User, Output));
        HostCheck.Equal(-22L, m.Call(45, fd, Output, Fds.MaximumIo + 1, 2));
        new Span<byte>(m.Files.Memory.At(User), 56).Clear();
        *(ulong*)m.Files.Memory.At(User) = Output; // Address-bearing stream messages remain unsupported.
        HostCheck.Equal(-95L, m.Call(47, fd, User, 2));
        HostCheck.Equal(0L, m.Call(45, fd, 0, 0, 2));
        HostCheck.True(new ReadOnlySpan<byte>(f.Receiver, sizeof(Rx.Owner)).SequenceEqual(owner));
        HostCheck.True(new ReadOnlySpan<byte>(f.ReceiveBuffer, 3).SequenceEqual("xyz"u8));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 8).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(3L, m.Call(45, fd, Output, 8));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 3).SequenceEqual("xyz"u8));
        HostCheck.Equal(0L, m.Call(3, fd));
        AbortAndReap(m, f);
        m.End();
    }

    [HostTest("m16-011 peek preserves nonblocking FIN and reset receive outcomes", HostLane.Fast, 12)]
    private static void PeekTerminalStates()
    {
        using (var m = new Model())
        using (var f = Connection())
        {
            uint fd = m.Accept(m.Listen(), f);
            HostCheck.Equal(-11L, m.Call(45, fd, Output, 1, 2));
            HostCheck.Equal(0L, m.Call(45, fd, 0, 0, 0x42));
            HostCheck.Equal(0L, m.Call(72, fd, 4, 2));
            HostCheck.Equal(-38L, m.Call(45, fd, Output, 1, 2));
            HostCheck.Equal(-11L, m.Call(45, fd, Output, 1, 0x42));
            HostCheck.Equal(Tx.Status.PeerClosed, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, "end"u8, ack: 700, flags: 0x19), out _));
            byte[] owner = new ReadOnlySpan<byte>(f.Receiver, sizeof(Rx.Owner)).ToArray();
            HostCheck.Equal(3L, m.Call(45, fd, Output, 8, 2));
            HostCheck.Equal(3L, m.Call(45, fd, Output, 8, 0x42));
            HostCheck.True(new ReadOnlySpan<byte>(f.Receiver, sizeof(Rx.Owner)).SequenceEqual(owner));
            HostCheck.Equal(3L, m.Call(45, fd, Output, 8));
            HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 3).SequenceEqual("end"u8));
            HostCheck.Equal(0L, m.Call(45, fd, Output, 8, 2));
            HostCheck.True(f.Receiver->EndOfStream);
            HostCheck.Equal(0L, m.Call(3, fd));
            AbortAndReap(m, f);
            m.End();
        }

        using (var m = new Model())
        using (var f = Connection())
        {
            uint fd = m.Accept(m.Listen(), f);
            HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, "R"u8, ack: 700, flags: 0x18), out _));
            HostCheck.Equal(Tx.Status.ConnectionReset, Tx.ReceiveFrame(f.Owner, Wire.Packet(101, ReadOnlySpan<byte>.Empty, ack: 700, flags: 0x14), out _));
            byte[] owner = new ReadOnlySpan<byte>(f.Receiver, sizeof(Rx.Owner)).ToArray();
            HostCheck.Equal(1L, m.Call(45, fd, Output, 1, 2));
            HostCheck.Equal(1L, m.Call(45, fd, Output, 1, 0x42));
            HostCheck.True(new ReadOnlySpan<byte>(f.Receiver, sizeof(Rx.Owner)).SequenceEqual(owner));
            HostCheck.Equal(1L, m.Call(45, fd, Output, 1));
            HostCheck.Equal((byte)'R', *m.Files.Memory.At(Output));
            HostCheck.Equal(-104L, m.Call(45, fd, Output, 1, 2));
            HostCheck.Equal(0L, m.Call(45, fd, Output, 1, 0x42));
            HostCheck.Equal(0L, m.Call(3, fd));
            AbortAndReap(m, f);
            m.End();
        }
    }
}
