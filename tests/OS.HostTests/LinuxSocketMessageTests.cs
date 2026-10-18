using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Net;
using Model = LinuxSocketServicesTests.Model;
using SocketCases = LinuxSocketServicesTests;
using Tx = OS.Net.TcpSendWindow;
using Wire = TcpReceiveWindowTests;

internal static unsafe class LinuxSocketMessageTests
{
    private const ulong User = 0x100000000, Header = User + 4096, Vectors = User + 8192;
    private const ulong First = User + 12288, Second = User + 16384;

    private struct Trace
    {
        internal uint Calls, Fd, Count, Total;
        internal ulong Number, Header, Flags, Table, FirstAddress, FirstBytes, SecondAddress, SecondBytes;
        internal bool Reject;
    }

    [UnmanagedCallersOnly]
    private static byte Observe(void* context, ulong number, uint fd, ulong header, ulong flags,
        ulong table, ulong* vectors, uint count, uint total)
    {
        Trace* trace = (Trace*)context;
        trace->Calls++; trace->Number = number; trace->Fd = fd; trace->Header = header;
        trace->Flags = flags; trace->Table = table; trace->Count = count; trace->Total = total;
        if (count > 0) { trace->FirstAddress = vectors[0]; trace->FirstBytes = vectors[1]; }
        if (count > 1) { trace->SecondAddress = vectors[2]; trace->SecondBytes = vectors[3]; }
        return trace->Reject ? (byte)0 : (byte)1;
    }

    private static void Message(Model model, params (ulong Address, ulong Bytes)[] vectors)
    {
        new Span<byte>(model.Files.Memory.At(Header), 56).Clear();
        ulong* header = (ulong*)model.Files.Memory.At(Header);
        header[2] = Vectors; header[3] = (ulong)vectors.Length;
        ulong* table = (ulong*)model.Files.Memory.At(Vectors);
        for (int i = 0; i < vectors.Length; i++)
        { table[i * 2] = vectors[i].Address; table[i * 2 + 1] = vectors[i].Bytes; }
    }

    [HostTest("m16-013 sendmsg snapshots vectors and returns only queued prefix under backpressure")]
    private static void SendPrefix()
    {
        using var model = new Model(); using var connection = SocketCases.Connection();
        uint fd = model.Accept(model.Listen(), connection); model.Option(fd, 1, 7, 4);
        model.Files.Text(First, "abc"); model.Files.Text(Second, "defghij");
        Message(model, (First, 3), (Second, 7));
        HostCheck.Equal(4L, model.Call(46, fd, Header, 0x4000));
        HostCheck.True(new ReadOnlySpan<byte>(connection.Buffer, 4).SequenceEqual("abcd"u8));
        HostCheck.Equal(-11L, model.Call(46, fd, Header)); HostCheck.Equal(4, connection.Owner->BufferedBytes);
        byte[] packet = new byte[1514]; HostCheck.Equal(Tx.Status.Success, Tx.Pump(connection.Owner, packet, out _));
        HostCheck.Equal(Tx.Status.Success, connection.Ack(704, out _));
        model.Files.Context->FailInputAt = model.Files.Context->InputCopies + 4;
        HostCheck.Equal(-14L, model.Call(46, fd, Header)); HostCheck.Equal(0, connection.Owner->BufferedBytes);
        HostCheck.Equal(0L, model.Call(3, fd)); SocketCases.AbortAndReap(model, connection); model.End();
    }

    [HostTest("m16-013 message metadata and complete vector extents reject without queued bytes")]
    private static void InvalidMetadata()
    {
        using var model = new Model(); using var connection = SocketCases.Connection();
        uint fd = model.Accept(model.Listen(), connection);
        foreach (string mutation in new[] { "null-header", "unmapped-header", "header-cross-hole", "null-vector", "vector-cross-hole",
            "count", "count-high", "length", "sum", "null-body", "body-hole", "body-wrap", "name", "name-length", "control", "control-length", "flags" })
        {
            Message(model, (First, 1), (Second, 1)); ulong* header = (ulong*)model.Files.Memory.At(Header);
            ulong* table = (ulong*)model.Files.Memory.At(Vectors); ulong address = Header, flags = 0; long expected = -14;
            switch (mutation)
            {
                case "null-header": address = 0; break;
                case "unmapped-header": address = User + 32768; break;
                case "header-cross-hole": address = User + 32768 - 32; break;
                case "null-vector": header[2] = 0; break;
                case "vector-cross-hole": header[2] = User + 32768 - 16; break;
                case "count": header[3] = 17; expected = -22; break;
                case "count-high": header[3] = 1UL << 32; expected = -22; break;
                case "length": table[1] = ulong.MaxValue; expected = -22; break;
                case "sum": table[1] = 65535; table[3] = 2; expected = -22; break;
                case "null-body": table[0] = 0; break;
                case "body-hole": table[0] = User + 32768; break;
                case "body-wrap": table[0] = ulong.MaxValue; table[1] = 2; break;
                case "name": header[0] = First; expected = -95; break;
                case "name-length": header[1] = 16; expected = -95; break;
                case "control": header[4] = First; expected = -95; break;
                case "control-length": header[5] = 1UL << 32; expected = -95; break;
                case "flags": flags = 1; expected = -95; break;
            }
            HostCheck.Equal(expected, model.Call(46, fd, address, flags), mutation);
            HostCheck.Equal(0, connection.Owner->BufferedBytes);
        }
        Message(model); HostCheck.Equal(0L, model.Call(46, fd, Header));
        Message(model, (0, 0)); HostCheck.Equal(0L, model.Call(46, fd, Header));
        HostCheck.Equal(-88L, model.Call(46, 1, Header));
        HostCheck.Equal(0L, model.Call(3, fd)); HostCheck.Equal(-9L, model.Call(46, fd, Header));
        SocketCases.AbortAndReap(model, connection); model.End();
    }

    [HostTest("m16-013 recvmsg scatters peek and retains unread data after later copyout faults")]
    private static void ReceiveCommit()
    {
        using var model = new Model(); using var connection = SocketCases.Connection();
        uint fd = model.Accept(model.Listen(), connection);
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(connection.Owner, Wire.Packet(100, "abcdef"u8, ack: 700, flags: 0x18), out _));
        Message(model, (First, 2), (Second, 4));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(model.Files.Memory.State, First, 4096, 1));
        HostCheck.Equal(-14L, model.Call(47, fd, Header)); HostCheck.Equal(6, connection.Receiver->UnreadBytes);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(model.Files.Memory.State, First, 4096, 3));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(model.Files.Memory.State, Header, 4096, 1));
        HostCheck.Equal(-14L, model.Call(47, fd, Header)); HostCheck.Equal(6, connection.Receiver->UnreadBytes);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(model.Files.Memory.State, Header, 4096, 3));
        HostCheck.Equal(6L, model.Call(47, fd, Header, 2)); HostCheck.Equal(6, connection.Receiver->UnreadBytes);
        HostCheck.True(new ReadOnlySpan<byte>(model.Files.Memory.At(First), 2).SequenceEqual("ab"u8));
        HostCheck.True(new ReadOnlySpan<byte>(model.Files.Memory.At(Second), 4).SequenceEqual("cdef"u8));
        foreach (uint fault in new[] { 2U, 3U, 4U, 5U })
        {
            model.Files.Context->FailOutputAt = model.Files.Context->OutputCopies + fault;
            HostCheck.Equal(-14L, model.Call(47, fd, Header)); HostCheck.Equal(6, connection.Receiver->UnreadBytes);
        }
        model.Files.Context->FailOutputAt = 0;
        HostCheck.Equal(6L, model.Call(47, fd, Header)); HostCheck.Equal(0, connection.Receiver->UnreadBytes);
        HostCheck.Equal(0UL, ((ulong*)model.Files.Memory.At(Header))[5]);
        HostCheck.Equal(0U, *(uint*)model.Files.Memory.At(Header + 48));
        HostCheck.Equal(-11L, model.Call(47, fd, Header));
        HostCheck.Equal(0L, model.Call(3, fd)); SocketCases.AbortAndReap(model, connection); model.End();
    }

    [HostTest("m16-013 mapped cross-page vectors and headers use current pages and reset policy")]
    private static void CrossPageAndReset()
    {
        using var model = new Model(); using var connection = SocketCases.Connection();
        uint fd = model.Accept(model.Listen(), connection);
        model.Files.Text(First, "abcd"); Message(model, (First, 2), (First + 2, 2));
        ulong crossHeader = User + 20480 - 24, crossVectors = User + 8192 - 16;
        new ReadOnlySpan<byte>(model.Files.Memory.At(Vectors), 32).CopyTo(new Span<byte>(model.Files.Memory.At(crossVectors), 32));
        ((ulong*)model.Files.Memory.At(Header))[2] = crossVectors;
        new ReadOnlySpan<byte>(model.Files.Memory.At(Header), 56).CopyTo(new Span<byte>(model.Files.Memory.At(crossHeader), 56));
        HostCheck.Equal(4L, model.Call(46, fd, crossHeader));
        HostCheck.Equal(-95L, model.Call(47, fd, Header, 0x100));
        HostCheck.Equal(Tx.Status.ConnectionReset, Tx.ReceiveFrame(connection.Owner,
            Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: 700, flags: 0x14), out _));
        HostCheck.Equal(-104L, model.Call(47, fd, Header)); HostCheck.Equal(0L, model.Call(47, fd, Header));
        HostCheck.Equal(-32L, model.Call(46, fd, Header));
        HostCheck.Equal(0L, model.Call(3, fd)); SocketCases.AbortAndReap(model, connection); model.End();
    }

    [HostTest("m16-013 receive payload may alias input metadata without restoring snapshotted pointers")]
    private static void AliasedMetadata()
    {
        using var model = new Model(); using var connection = SocketCases.Connection();
        uint fd = model.Accept(model.Listen(), connection);
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(connection.Owner, Wire.Packet(100, "abcdef"u8, ack: 700, flags: 0x18), out _));
        Message(model, (Header + 16, 4), (Second, 2));
        HostCheck.Equal(6L, model.Call(47, fd, Header)); HostCheck.Equal(0, connection.Receiver->UnreadBytes);
        HostCheck.True(new ReadOnlySpan<byte>(model.Files.Memory.At(Header + 16), 4).SequenceEqual("abcd"u8));
        HostCheck.True(new ReadOnlySpan<byte>(model.Files.Memory.At(Second), 2).SequenceEqual("ef"u8));
        HostCheck.Equal(0L, model.Call(3, fd)); SocketCases.AbortAndReap(model, connection); model.End();
    }

    [HostTest("m16-013 message observation borrows validated metadata before transport and detaches at finish")]
    private static void BorrowedObservation()
    {
        using var model = new Model(); using var connection = SocketCases.Connection();
        uint fd = model.Accept(model.Listen(), connection); Trace trace = new() { Reject = true };
        HostCheck.Equal(false, LinuxSocketServices.AttachMessageObserver(model.State, null, &Observe));
        HostCheck.Equal(false, LinuxSocketServices.AttachMessageObserver(model.State, &trace, null));
        HostCheck.Equal(false, model.State->HasMessageObserver);
        HostCheck.True(LinuxSocketServices.AttachMessageObserver(model.State, &trace, &Observe));
        HostCheck.Equal(false, LinuxSocketServices.AttachMessageObserver(model.State, &trace, &Observe));
        model.Files.Text(First, "ab"); model.Files.Text(Second, "c"); Message(model, (First, 2), (Second, 1));
        HostCheck.Equal(-5L, model.Call(46, fd, Header, 0x4000)); HostCheck.Equal(0, connection.Owner->BufferedBytes);
        HostCheck.Equal(1U, trace.Calls); HostCheck.Equal(46UL, trace.Number); HostCheck.Equal(fd, trace.Fd);
        HostCheck.Equal(Header, trace.Header); HostCheck.Equal(0x4000UL, trace.Flags); HostCheck.Equal(Vectors, trace.Table);
        HostCheck.Equal(2U, trace.Count); HostCheck.Equal(3U, trace.Total);
        HostCheck.Equal(First, trace.FirstAddress); HostCheck.Equal(2UL, trace.FirstBytes);
        HostCheck.Equal(Second, trace.SecondAddress); HostCheck.Equal(1UL, trace.SecondBytes);
        trace.Reject = false; HostCheck.Equal(3L, model.Call(46, fd, Header));
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(connection.Owner, Wire.Packet(100, "xyz"u8, ack: 700, flags: 0x18), out _));
        trace.Reject = true; HostCheck.Equal(-5L, model.Call(47, fd, Header)); HostCheck.Equal(3, connection.Receiver->UnreadBytes);
        trace.Reject = false; HostCheck.Equal(3L, model.Call(47, fd, Header)); HostCheck.Equal(0, connection.Receiver->UnreadBytes);
        HostCheck.Equal(4U, trace.Calls); HostCheck.Equal(47UL, trace.Number);
        HostCheck.Equal(0L, model.Call(3, fd)); SocketCases.AbortAndReap(model, connection); model.End();
        HostCheck.Equal(false, model.State->HasMessageObserver);
    }
}
