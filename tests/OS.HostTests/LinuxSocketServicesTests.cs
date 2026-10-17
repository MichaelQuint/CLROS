using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Net;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Tx = OS.Net.TcpSendWindow;
using Rx = OS.Net.TcpReceiveWindow;
using Wire = TcpReceiveWindowTests;

internal static unsafe class LinuxSocketServicesTests
{
    private const ulong User = 0x100000000, Output = User + 24576;
    private sealed class Model : IDisposable
    {
        internal readonly LinuxDescriptorServicesTests.Owner Files;
        internal readonly Sockets.State* State;
        internal readonly Sockets.Endpoint* Endpoints;
        internal Model(uint capacity = 4, uint descriptors = 12)
        {
            Files = new(descriptors, descriptors); Files.Begin();
            State = (Sockets.State*)NativeMemory.AlignedAlloc((nuint)sizeof(Sockets.State), 16); *State = default;
            Endpoints = (Sockets.Endpoint*)NativeMemory.AllocZeroed(capacity, (nuint)sizeof(Sockets.Endpoint));
            HostCheck.True(Sockets.Begin(State, Files.State, Endpoints, capacity, new(10, 0, 2, 15), 8080));
        }
        internal long Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0) =>
            Fds.Dispatch(Files.State, new() { Number = number, Argument0 = a0, Argument1 = a1, Argument2 = a2,
                Argument3 = a3, Argument4 = a4, Argument5 = a5 });
        internal uint Socket(uint flags = Fds.NonBlock | Fds.CloseOnExec)
        { long fd = Call(41, 2, 1 | flags, 6); HostCheck.True(fd >= 0); return (uint)fd; }
        internal void Address(ushort port = 8080, byte last = 0)
        {
            var bytes = new Span<byte>(Files.Memory.At(User), 16); bytes.Clear(); bytes[0] = 2;
            bytes[2] = (byte)(port >> 8); bytes[3] = (byte)port;
            if (last != 0) { bytes[4] = 10; bytes[6] = 2; bytes[7] = last; }
        }
        internal uint Listen(uint backlog = 4)
        {
            uint fd = Socket(); Address(); HostCheck.Equal(0L, Call(49, fd, User, 16));
            HostCheck.Equal(0L, Call(50, fd, backlog)); return fd;
        }
        internal uint Accept(uint listener, TcpSendWindowTests.Fixture connection)
        {
            HostCheck.True(Fds.Capture(Files.State, listener, out var token));
            HostCheck.True(Sockets.EnqueueAccepted(State, token, listener, connection.Receiver, connection.Owner));
            long fd = Call(288, listener, 0, 0, Fds.NonBlock | Fds.CloseOnExec); HostCheck.True(fd >= 0); return (uint)fd;
        }
        internal void Option(uint fd, ulong level, ulong option, int value)
        { *(int*)Files.Memory.At(User) = value; HostCheck.Equal(0L, Call(54, fd, level, option, User, 4)); }
        internal void End() { HostCheck.True(Fds.Audit(Files.State)); HostCheck.True(Fds.End(Files.State)); }
        public void Dispose() { NativeMemory.AlignedFree(State); NativeMemory.Free(Endpoints); Files.Dispose(); }
    }
    private static TcpSendWindowTests.Fixture Connection()
    {
        var f = new TcpSendWindowTests.Fixture(capacity: 8, receiveCapacity: 8);
        var io = f.Operations();
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(f.Owner, io.Context, io.NowNanoseconds, 1_000_000_000_000));
        HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(f.Owner, 30_000_000_000)); return f;
    }
    private static void AbortAndReap(Model m, TcpSendWindowTests.Fixture f)
    {
        if (f.Owner->Active) HostCheck.Equal(Tx.Status.Success, Tx.AbortConnection(f.Owner));
        HostCheck.True(Sockets.ReapClosed(m.State, f.Owner)); HostCheck.Equal(0, f.Table->Count);
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 8).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.True(new ReadOnlySpan<byte>(f.ReceiveBuffer, 8).IndexOfAnyExcept((byte)0) < 0);
    }

    [HostTest("m16-011 socket admission rejects aliases and dirty storage without publication", HostLane.Fast, 0)]
    private static void Admission()
    {
        using var files = new LinuxDescriptorServicesTests.Owner(); files.Begin();
        Sockets.State state = default;
        HostCheck.True(!Sockets.Begin(&state, files.State, (Sockets.Endpoint*)files.Admission.Scratch, 4, new(10, 0, 2, 15), 8080));
        HostCheck.Equal(3U, files.State->ActiveDescriptors); HostCheck.True(Fds.Audit(files.State));
        Sockets.Endpoint* slots = stackalloc Sockets.Endpoint[4]; new Span<byte>(slots, 4 * sizeof(Sockets.Endpoint)).Clear();
        ((byte*)slots)[0] = 1;
        HostCheck.True(!Sockets.Begin(&state, files.State, slots, 4, new(10, 0, 2, 15), 8080));
        HostCheck.True(Fds.End(files.State));
    }

    [HostTest("m16-011 family probes distinguish unsupported socket type and preserve fd accounting", HostLane.Fast, 1)]
    private static void ProtocolAndCapacity()
    {
        using var m = new Model(capacity: 1);
        HostCheck.Equal(-94L, m.Call(41, 2, 2 | Fds.CloseOnExec, 0));
        HostCheck.Equal(-97L, m.Call(41, 10, 2 | Fds.CloseOnExec, 0));
        HostCheck.Equal(-97L, m.Call(41, 1, 2, 0)); HostCheck.Equal(-93L, m.Call(41, 2, 1, 17));
        HostCheck.Equal(-22L, m.Call(41, 2, 0x100001, 6)); HostCheck.Equal(3U, m.Files.State->ActiveDescriptors);
        uint fd = m.Socket(); HostCheck.Equal(3U, fd); HostCheck.Equal(-105L, m.Call(41, 2, 1, 6));
        HostCheck.Equal(0L, m.Call(3, fd)); HostCheck.Equal(fd, m.Socket()); m.End();
    }

    [HostTest("m16-011 socket dup shares flags and last-close retains one endpoint", HostLane.Fast, 2)]
    private static void DescriptorNamespace()
    {
        using var m = new Model(capacity: 1);
        uint fd = m.Socket(); HostCheck.Equal(2L | Fds.NonBlock, m.Call(72, fd, 3)); HostCheck.Equal(1L, m.Call(72, fd, 1));
        HostCheck.True(Fds.Capture(m.Files.State, fd, out var stale));
        uint dup = (uint)m.Call(72, fd, 0, 0); HostCheck.Equal(4U, dup);
        HostCheck.Equal(0L, m.Call(72, dup, 4, 0x8802)); HostCheck.Equal(2L | Fds.NonBlock, m.Call(72, fd, 3));
        HostCheck.Equal(0L, m.Call(3, fd)); HostCheck.True(!Fds.IsCurrent(m.Files.State, stale));
        HostCheck.Equal(-105L, m.Call(41, 2, 1, 6)); HostCheck.Equal(0L, m.Call(3, dup));
        HostCheck.Equal(fd, m.Socket()); HostCheck.True(!Fds.IsCurrent(m.Files.State, stale)); m.End();
    }

    [HostTest("m16-011 bind enforces exact manifest endpoint and preserves listener accounting", HostLane.Fast, 3)]
    private static void BindPolicy()
    {
        using var m = new Model(); uint fd = m.Socket();
        m.Address(8081); HostCheck.Equal(-13L, m.Call(49, fd, User, 16));
        m.Address(last: 2); HostCheck.Equal(-13L, m.Call(49, fd, User, 16));
        m.Address(last: 15); HostCheck.Equal(-22L, m.Call(49, fd, User, 15));
        m.Files.Context->FailInputAt = m.Files.Context->InputCopies + 1;
        HostCheck.Equal(-14L, m.Call(49, fd, User, 16)); m.Files.Context->FailInputAt = 0;
        HostCheck.Equal(0L, m.Call(49, fd, User, 16)); HostCheck.Equal(0L, m.Call(50, fd, 4));
        HostCheck.Equal(-11L, m.Call(288, fd, 0, 0, Fds.NonBlock));
        uint second = m.Socket(); m.Address(); HostCheck.Equal(-98L, m.Call(49, second, User, 16));
        HostCheck.Equal(-88L, m.Call(50, 1, 4)); HostCheck.Equal(-9L, m.Call(50, 31, 4)); m.End();
    }

    [HostTest("m16-011 actual TCP accept preserves backlog on user-copy and descriptor exhaustion", HostLane.Fast, 4)]
    private static void AcceptFailures()
    {
        using var m = new Model(descriptors: 5); using var f = Connection(); uint listener = m.Listen();
        HostCheck.True(Fds.Capture(m.Files.State, listener, out var token));
        HostCheck.True(Sockets.EnqueueAccepted(m.State, token, listener, f.Receiver, f.Owner));
        HostCheck.True(!Sockets.EnqueueAccepted(m.State, token, listener, f.Receiver, f.Owner));
        *(uint*)m.Files.Memory.At(Output) = 16; m.Files.Context->FailOutput = true;
        HostCheck.Equal(-14L, m.Call(288, listener, User, Output, Fds.NonBlock));
        HostCheck.Equal(1U, m.State->RetainedConnections); m.Files.Context->FailOutput = false;
        uint blocker = m.Socket(); HostCheck.Equal(-24L, m.Call(288, listener, User, Output, Fds.NonBlock));
        HostCheck.Equal(0L, m.Call(3, blocker));
        uint accepted = (uint)m.Call(288, listener, User, Output, Fds.NonBlock);
        HostCheck.Equal(4U, accepted); HostCheck.Equal(16U, *(uint*)m.Files.Memory.At(Output));
        HostCheck.Equal((byte)0x9c, m.Files.Memory.At(User)[2]); HostCheck.Equal((byte)0x40, m.Files.Memory.At(User)[3]);
        HostCheck.Equal(0L, m.Call(3, accepted)); AbortAndReap(m, f); m.End();
    }

    [HostTest("m16-011 TCP partial sends and copy-failed reads retain exact owned bytes", HostLane.Fast, 5)]
    private static void PartialIo()
    {
        using var m = new Model(); using var f = Connection(); uint listener = m.Listen(), fd = m.Accept(listener, f);
        m.Option(fd, 1, 7, 4); m.Option(fd, 6, 1, 1); m.Option(listener, 1, 2, 1);
        m.Files.Text(User, "abcdefghij"); HostCheck.Equal(4L, m.Call(44, fd, User, 10, 0x4000));
        HostCheck.Equal(-11L, m.Call(44, fd, User, 1)); HostCheck.Equal(4, f.Owner->BufferedBytes);
        byte[] output = new byte[1514]; HostCheck.Equal(Tx.Status.Success, Tx.Pump(f.Owner, output, out _));
        HostCheck.Equal(Tx.Status.Success, f.Ack(704, out _)); HostCheck.Equal(4L, m.Call(1, fd, User + 4, 6));
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, "xyz"u8, ack: 704, flags: 0x18), out _));
        m.Files.Context->FailOutput = true; HostCheck.Equal(-14L, m.Call(45, fd, Output, 2));
        HostCheck.Equal(3, f.Receiver->UnreadBytes); m.Files.Context->FailOutput = false;
        m.Files.Context->RequiredOutputSource = m.Files.Admission.Scratch;
        HostCheck.Equal(2L, m.Call(45, fd, Output, 2)); HostCheck.Equal((byte)'x', m.Files.Memory.At(Output)[0]);
        HostCheck.Equal(1L, m.Call(0, fd, Output, 9)); HostCheck.Equal((byte)'z', m.Files.Memory.At(Output)[0]);
        HostCheck.Equal(-11L, m.Call(45, fd, Output, 1)); HostCheck.Equal(0L, m.Call(3, fd));
        HostCheck.Equal(1U, m.State->RetainedConnections); HostCheck.True(!Fds.End(m.Files.State));
        AbortAndReap(m, f); m.End();
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
            HostCheck.Equal(3L, m.Call(45, fd, Output, 8)); HostCheck.Equal(0L, m.Call(45, fd, Output, 8));
            HostCheck.Equal(0L, m.Call(48, fd, 1)); HostCheck.Equal(0L, m.Call(48, fd, 1));
            HostCheck.Equal(-32L, m.Call(44, fd, User, 1)); HostCheck.Equal(0L, m.Call(3, fd)); AbortAndReap(m, f); m.End();
        }
        using (var m = new Model())
        using (var f = Connection())
        {
            uint fd = m.Accept(m.Listen(), f), dup = (uint)m.Call(72, fd, 0, 0);
            HostCheck.Equal(Tx.Status.Success,Tx.ReceiveFrame(f.Owner,Wire.Packet(100,"R"u8,ack:700,flags:0x18),out _));
            HostCheck.Equal(Tx.Status.ConnectionReset, Tx.ReceiveFrame(f.Owner, Wire.Packet(101, ReadOnlySpan<byte>.Empty, ack: 700, flags: 0x14), out _));
            HostCheck.Equal(Rx.Status.TableRejected,Rx.Read(f.Receiver,new byte[1],out _));
            m.Files.Context->FailOutput=true;HostCheck.Equal(-14L,m.Call(45,fd,Output,1));m.Files.Context->FailOutput=false;
            HostCheck.Equal(1,f.Receiver->UnreadBytes);HostCheck.Equal(1L,m.Call(45,fd,Output,1));
            HostCheck.Equal((byte)'R',*m.Files.Memory.At(Output));HostCheck.Equal(0,f.Receiver->UnreadBytes);
            *(uint*)m.Files.Memory.At(Output) = 4; m.Files.Context->FailOutput = true;
            HostCheck.Equal(-14L, m.Call(55, fd, 1, 4, User, Output)); m.Files.Context->FailOutput = false;
            HostCheck.Equal(-104L, m.Call(45, fd, Output, 1)); HostCheck.Equal(-32L, m.Call(44, fd, User, 1));
            HostCheck.Equal(0L, m.Call(45, fd, Output, 1));
            HostCheck.Equal(0L, m.Call(55, fd, 1, 4, User, Output)); HostCheck.Equal(0, *(int*)m.Files.Memory.At(User));
            HostCheck.True(!Sockets.ReapClosed(m.State, f.Owner)); HostCheck.Equal(0L, m.Call(3, fd));
            HostCheck.True(!Sockets.ReapClosed(m.State, f.Owner)); HostCheck.Equal(0L, m.Call(3, dup)); AbortAndReap(m, f); m.End();
        }
    }

    [HostTest("m16-011 listener last close retains queued TCP for explicit closed scrub", HostLane.Fast, 7)]
    private static void ListenerClose()
    {
        using var m = new Model(); using var f = Connection(); uint listener = m.Listen();
        HostCheck.True(Fds.Capture(m.Files.State, listener, out var token));
        HostCheck.True(Sockets.EnqueueAccepted(m.State, token, listener, f.Receiver, f.Owner));
        HostCheck.Equal(0L, m.Call(3, listener)); HostCheck.True(Fds.Audit(m.Files.State));
        HostCheck.True(!Sockets.EnqueueAccepted(m.State, token, listener, f.Receiver, f.Owner));
        HostCheck.True(f.Owner->WritesClosed); HostCheck.Equal(1U, m.State->RetainedConnections);
        uint next = m.Socket(); m.Address(); HostCheck.Equal(-98L, m.Call(49, next, User, 16));
        m.Option(next, 1, 2, 1); m.Address(); HostCheck.Equal(0L, m.Call(49, next, User, 16));
        AbortAndReap(m, f); m.End();
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
            HostCheck.Equal(-5L, m.Call(45, fd, Output, 3)); HostCheck.Equal(before, m.Files.Context->OutputCopies);
            HostCheck.Equal(3, f.Receiver->UnreadBytes); HostCheck.Equal(0L, m.Call(3, fd));
            AbortAndReap(m, f); m.End();
        }
        using (var m = new Model())
        using (var f = Connection())
        {
            uint fd = m.Accept(m.Listen(), f); f.Hardware->Now = 1_000_000_000_100;
            HostCheck.Equal(0L, m.Call(3, fd)); HostCheck.Equal(TcpConnectionCloseState.Closed, f.Owner->CloseState);
            HostCheck.True(Sockets.ReapClosed(m.State, f.Owner)); HostCheck.Equal(0, f.Table->Count); m.End();
        }
    }

    [HostTest("m16-011 EFI socket references require the explicit TCP feature boundary", HostLane.Fast, 9)]
    private static void FeatureBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (string file in new[] { "LinuxDescriptorServices.cs", "LinuxThreadServices.Pipes.cs" })
        {
            string source = File.ReadAllText(Path.Combine(root, "src/OS.Kernel/Compatibility/LinuxX64", file));
            var off = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: ["NOSTDLIB_EFI"])).GetRoot();
            var on = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: ["NOSTDLIB_EFI", "CLROS_LINUX_SOCKETS"])).GetRoot();
            HostCheck.True(!off.DescendantNodes().OfType<IdentifierNameSyntax>().Any(node => node.Identifier.ValueText == "LinuxSocketServices"));
            HostCheck.True(on.DescendantNodes().OfType<IdentifierNameSyntax>().Any(node => node.Identifier.ValueText == "LinuxSocketServices"));
        }
        string sockets = File.ReadAllText(Path.Combine(root, "src/OS.Kernel/Compatibility/LinuxX64/LinuxSocketServices.cs"));
        var disabled = CSharpSyntaxTree.ParseText(sockets, new CSharpParseOptions(preprocessorSymbols: ["NOSTDLIB_EFI"])).GetCompilationUnitRoot();
        HostCheck.Equal(0, disabled.Members.Count);
    }

    [HostTest("m16-011 recvfrom peek preserves exact receive bytes window and ACK metadata", HostLane.Fast, 10)]
    private static void PeekPreservesWindow()
    {
        using var m = new Model(); using var f = Connection(); uint fd = m.Accept(m.Listen(), f);
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
        HostCheck.Equal(6, f.Receiver->UnreadBytes); HostCheck.Equal((ushort)2, f.Receiver->AvailableWindow);
        HostCheck.Equal(106U, f.Receiver->AcknowledgementNumber); HostCheck.True(!f.Receiver->HasPendingAck);
        HostCheck.Equal(Rx.Status.NothingToAck, Rx.PumpAck(f.Receiver, new byte[1514], out _));
        HostCheck.Equal(6L, m.Call(0, fd, Output, 8));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 6).SequenceEqual("abcdef"u8));
        HostCheck.Equal(0, f.Receiver->UnreadBytes); HostCheck.Equal((ushort)8, f.Receiver->AvailableWindow);
        HostCheck.True(f.Receiver->HasPendingAck);
        HostCheck.Equal(0L, m.Call(3, fd)); AbortAndReap(m, f); m.End();
    }

    [HostTest("m16-011 peek rejects invalid outputs flags and vector calls without consuming TCP", HostLane.Fast, 11)]
    private static void PeekFailures()
    {
        using var m = new Model(); using var f = Connection(); uint fd = m.Accept(m.Listen(), f);
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(f.Owner, Wire.Packet(100, "xyz"u8, ack: 700, flags: 0x18), out _));
        byte[] owner = new ReadOnlySpan<byte>(f.Receiver, sizeof(Rx.Owner)).ToArray();
        new Span<byte>(m.Files.Memory.At(Output), 8).Fill(0xa5);
        uint copies = m.Files.Context->OutputCopies;
        HostCheck.Equal(-14L, m.Call(45, fd, User + 32768, 3, 2));
        HostCheck.Equal(copies, m.Files.Context->OutputCopies);
        m.Files.Context->FailOutput = true;
        HostCheck.Equal(-14L, m.Call(45, fd, Output, 3, 0x42)); m.Files.Context->FailOutput = false;
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 8).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(-95L, m.Call(45, fd, Output, 3, 0x102));
        HostCheck.Equal(-95L, m.Call(45, fd, Output, 3, 2, User, Output));
        HostCheck.Equal(-22L, m.Call(45, fd, Output, Fds.MaximumIo + 1, 2));
        HostCheck.Equal(-38L, m.Call(47, fd, User, 2)); // recvmsg remains unsupported, including vector peek.
        HostCheck.Equal(0L, m.Call(45, fd, 0, 0, 2));
        HostCheck.True(new ReadOnlySpan<byte>(f.Receiver, sizeof(Rx.Owner)).SequenceEqual(owner));
        HostCheck.True(new ReadOnlySpan<byte>(f.ReceiveBuffer, 3).SequenceEqual("xyz"u8));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 8).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(3L, m.Call(45, fd, Output, 8));
        HostCheck.True(new ReadOnlySpan<byte>(m.Files.Memory.At(Output), 3).SequenceEqual("xyz"u8));
        HostCheck.Equal(0L, m.Call(3, fd)); AbortAndReap(m, f); m.End();
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
            HostCheck.Equal(0L, m.Call(3, fd)); AbortAndReap(m, f); m.End();
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
            HostCheck.Equal(1L, m.Call(45, fd, Output, 1)); HostCheck.Equal((byte)'R', *m.Files.Memory.At(Output));
            HostCheck.Equal(-104L, m.Call(45, fd, Output, 1, 2));
            HostCheck.Equal(0L, m.Call(45, fd, Output, 1, 0x42));
            HostCheck.Equal(0L, m.Call(3, fd)); AbortAndReap(m, f); m.End();
        }
    }
}
