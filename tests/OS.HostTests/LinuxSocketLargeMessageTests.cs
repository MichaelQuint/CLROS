using OS.Kernel.Compatibility.LinuxX64;
using Model = LinuxSocketServicesTests.Model;
using SocketCases = LinuxSocketServicesTests;
using Tx = OS.Net.TcpSendWindow;
using Wire = TcpReceiveWindowTests;

internal static unsafe class LinuxSocketLargeMessageTests
{
    private const ulong User = 0x100000000, Header = User + 4096, Vectors = User + 8192;
    private const ulong First = User + 12288, Second = User + 16384;

    [HostTest("m16-026 seventeen connected message vectors queue only the exact available four byte prefix")]
    private static void SeventeenVectors()
    {
        using var model = new Model();
        using var connection = SocketCases.Connection();
        uint fd = model.Accept(model.Listen(), connection);
        model.Option(fd, 1, 7, 4);
        model.Files.Text(First, "abcdefghijklmnopq");
        Message(model, Enumerable.Range(0, 17).Select(i => (First + (ulong)i, 1UL)).ToArray());
        HostCheck.Equal(4L, model.Call(46, fd, Header, 0x4000));
        HostCheck.Equal(4, connection.Owner->BufferedBytes);
        HostCheck.True(new ReadOnlySpan<byte>(connection.Buffer, 4).SequenceEqual("abcd"u8));
        Finish(model, connection, fd);
    }

    [HostTest("m16-026 connected message aggregate above sixty four KiB retains bounded scratch and send prefix")]
    private static void LargeAggregate()
    {
        using var model = new Model();
        using var connection = SocketCases.Connection();
        uint fd = model.Accept(model.Listen(), connection);
        model.Option(fd, 1, 7, 4);
        for (int i = 0; i < 8192; i++)
            model.Files.Memory.At(First)[i] = unchecked((byte)(31 + i * 17));
        Message(model, Enumerable.Repeat((First, 8192UL), 16).ToArray());
        HostCheck.Equal(4L, model.Call(46, fd, Header));
        HostCheck.Equal(4, connection.Owner->BufferedBytes);
        HostCheck.True(new ReadOnlySpan<byte>(connection.Buffer, 4).SequenceEqual(new byte[] { 31, 48, 65, 82 }));
        Finish(model, connection, fd);
    }

    [HostTest("m16-026 connected message accepts 1024 vectors and one MiB but rejects count and aggregate overflow without mutation")]
    private static void MessageBoundaries()
    {
        using var model = new Model();
        using var connection = SocketCases.Connection();
        model.Files.Context->MaximumInputBytes = 8192;
        uint fd = model.Accept(model.Listen(), connection);
        model.Option(fd, 1, 7, 4);
        Message(model, Enumerable.Repeat((0UL, 0UL), 1024).ToArray());
        byte[] empty = new ReadOnlySpan<byte>(connection.Owner, sizeof(Tx.Owner)).ToArray();
        HostCheck.Equal(0L, model.Call(46, fd, Header));
        HostCheck.True(empty.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(connection.Owner, sizeof(Tx.Owner))));

        model.Files.Text(First, "abcd");
        Message(model, Enumerable.Repeat((First, 1024UL), 1024).ToArray());
        // The complete table overlaps First. Its final bytes, rather than the
        // earlier text, are the actual user payload presented to sendmsg.
        byte[] expected = new ReadOnlySpan<byte>(model.Files.Memory.At(First), 4).ToArray();
        byte[] table = new ReadOnlySpan<byte>(model.Files.Memory.At(Vectors), 16384).ToArray();
        HostCheck.Equal(4L, model.Call(46, fd, Header));
        HostCheck.True(expected.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(connection.Buffer, 4)));
        HostCheck.True(table.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(model.Files.Memory.At(Vectors), 16384)));
        byte[] owner = new ReadOnlySpan<byte>(connection.Owner, sizeof(Tx.Owner)).ToArray();
        byte[] buffer = new ReadOnlySpan<byte>(connection.Buffer, 8).ToArray();
        HostCheck.True(LinuxDescriptorServices.Capture(model.Files.State, fd, out var token));
        foreach (bool tooMany in new[] { true, false })
        {
            Message(model, Enumerable.Repeat((First, 1024UL), tooMany ? 1025 : 1024).ToArray());
            if (!tooMany)
                ((ulong*)model.Files.Memory.At(Vectors))[2047] = 1025;
            byte[] metadata = new ReadOnlySpan<byte>(model.Files.Memory.At(Header), 56).ToArray();
            byte[] input = new ReadOnlySpan<byte>(model.Files.Memory.At(Vectors), tooMany ? 16400 : 16384).ToArray();
            HostCheck.Equal(-22L, model.Call(46, fd, Header));
            HostCheck.True(owner.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(connection.Owner, sizeof(Tx.Owner))));
            HostCheck.True(buffer.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(connection.Buffer, 8)));
            HostCheck.True(LinuxDescriptorServices.IsCurrent(model.Files.State, token));
            HostCheck.True(metadata.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(model.Files.Memory.At(Header), 56)));
            HostCheck.True(input.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(model.Files.Memory.At(Vectors), input.Length)));
            HostCheck.Equal(4, connection.Owner->BufferedBytes);
        }
        Finish(model, connection, fd);
    }

    [HostTest("m16-026 late invalid seventeenth vector faults before enqueue copyout or receive consumption")]
    private static void LateVectorFault()
    {
        using var model = new Model();
        using var connection = SocketCases.Connection();
        uint fd = model.Accept(model.Listen(), connection);
        model.Option(fd, 1, 7, 4);
        var vectors = Enumerable.Range(0, 17).Select(i => (First + (ulong)i, 1UL)).ToArray();
        vectors[16] = (User + 32768, 1);
        new Span<byte>(model.Files.Memory.At(First), 17).Fill(0xa5);
        Message(model, vectors);
        byte[] owner = new ReadOnlySpan<byte>(connection.Owner, sizeof(Tx.Owner)).ToArray();
        byte[] sendBuffer = new ReadOnlySpan<byte>(connection.Buffer, 8).ToArray();
        HostCheck.Equal(-14L, model.Call(46, fd, Header));
        HostCheck.True(owner.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(connection.Owner, sizeof(Tx.Owner))));
        HostCheck.True(sendBuffer.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(connection.Buffer, 8)));
        HostCheck.Equal(0, connection.Owner->BufferedBytes);

        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(connection.Owner, Wire.Packet(100, "abcdef"u8, ack: 700, flags: 0x18), out _));
        byte[] receiver = new ReadOnlySpan<byte>(connection.Receiver, sizeof(OS.Net.TcpReceiveWindow.Owner)).ToArray();
        byte[] receiveBuffer = new ReadOnlySpan<byte>(connection.ReceiveBuffer, 8).ToArray();
        uint copies = model.Files.Context->OutputCopies;
        HostCheck.Equal(-14L, model.Call(47, fd, Header));
        HostCheck.Equal(6, connection.Receiver->UnreadBytes);
        HostCheck.Equal(copies, model.Files.Context->OutputCopies);
        HostCheck.True(receiver.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(connection.Receiver, sizeof(OS.Net.TcpReceiveWindow.Owner))));
        HostCheck.True(receiveBuffer.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(connection.ReceiveBuffer, 8)));
        HostCheck.True(new ReadOnlySpan<byte>(model.Files.Memory.At(First), 17).IndexOfAnyExcept((byte)0xa5) < 0);
        vectors[16] = (First + 16, 1);
        Message(model, vectors);
        HostCheck.Equal(6L, model.Call(47, fd, Header));
        HostCheck.Equal(0, connection.Receiver->UnreadBytes);
        HostCheck.True(new ReadOnlySpan<byte>(model.Files.Memory.At(First), 6).SequenceEqual("abcdef"u8));
        Finish(model, connection, fd);
    }

    [HostTest("m16-026 large recvmsg scatters only received bytes and clears only defined output header fields")]
    private static void LargeReceivePrefix()
    {
        using var model = new Model();
        using var connection = SocketCases.Connection();
        uint fd = model.Accept(model.Listen(), connection);
        new Span<byte>(model.Files.Memory.At(First), 16).Fill(0xa5);
        new Span<byte>(model.Files.Memory.At(Second), 16).Fill(0x5a);
        var vectors = Enumerable.Repeat((First, 8192UL), 17).ToArray();
        vectors[0] = (First, 2);
        vectors[1] = (Second, 8192);
        Message(model, vectors);
        *(uint*)model.Files.Memory.At(Header + 12) = 0xa1b2c3d4;
        *(uint*)model.Files.Memory.At(Header + 48) = 0xdeadbeef;
        *(uint*)model.Files.Memory.At(Header + 52) = 0x11223344;
        new Span<byte>(model.Files.Memory.At(Header + 56), 16).Fill(0x7c);
        byte[] header = new ReadOnlySpan<byte>(model.Files.Memory.At(Header), 56).ToArray();
        header.AsSpan(8, 4).Clear();
        header.AsSpan(40, 8).Clear();
        header.AsSpan(48, 4).Clear();
        byte[] table = new ReadOnlySpan<byte>(model.Files.Memory.At(Vectors), 272).ToArray();
        HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(connection.Owner, Wire.Packet(100, "abcdef"u8, ack: 700, flags: 0x18), out _));
        HostCheck.Equal(6L, model.Call(47, fd, Header));
        HostCheck.Equal(0, connection.Receiver->UnreadBytes);
        HostCheck.True(new ReadOnlySpan<byte>(model.Files.Memory.At(First), 2).SequenceEqual("ab"u8));
        HostCheck.True(new ReadOnlySpan<byte>(model.Files.Memory.At(Second), 4).SequenceEqual("cdef"u8));
        HostCheck.True(new ReadOnlySpan<byte>(model.Files.Memory.At(First + 2), 14).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.True(new ReadOnlySpan<byte>(model.Files.Memory.At(Second + 4), 12).IndexOfAnyExcept((byte)0x5a) < 0);
        HostCheck.True(header.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(model.Files.Memory.At(Header), 56)));
        HostCheck.True(table.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(model.Files.Memory.At(Vectors), 272)));
        HostCheck.True(new ReadOnlySpan<byte>(model.Files.Memory.At(Header + 56), 16).IndexOfAnyExcept((byte)0x7c) < 0);
        Finish(model, connection, fd);
    }

    [HostTest("m16-026 513 and 1024 vector messages use bounded input callbacks and retain the last vector")]
    private static void BoundedMetadataCopies()
    {
        foreach (int count in new[] { 513, 1024 })
            foreach (ulong number in new[] { 46UL, 47UL })
            {
                using var model = new Model();
                using var connection = SocketCases.Connection();
                uint fd = model.Accept(model.Listen(), connection);
                model.Files.Context->MaximumInputBytes = 8192;
                const ulong data = User + 28672;
                var vectors = new (ulong Address, ulong Bytes)[count];
                vectors[^1] = (data, 1);
                Message(model, vectors);
                *model.Files.Memory.At(data) = (byte)'Q';
                if (number == 47)
                {
                    *model.Files.Memory.At(data) = 0xa5;
                    HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(connection.Owner,
                        Wire.Packet(100, "Q"u8, ack: 700, flags: 0x18), out _));
                }
                HostCheck.Equal(1L, model.Call(number, fd, Header));
                if (number == 46)
                    HostCheck.Equal((byte)'Q', *connection.Buffer);
                else
                {
                    HostCheck.Equal((byte)'Q', *model.Files.Memory.At(data));
                    HostCheck.Equal(0, connection.Receiver->UnreadBytes);
                }
                Finish(model, connection, fd);
            }
    }

    [HostTest("m16-026 second metadata chunk failure preserves transport user memory and descriptor ownership")]
    private static void LaterMetadataCopyFailure()
    {
        foreach (ulong number in new[] { 46UL, 47UL })
        {
            using var model = new Model();
            using var connection = SocketCases.Connection();
            uint fd = model.Accept(model.Listen(), connection);
            model.Files.Context->MaximumInputBytes = 8192;
            const ulong data = User + 28672;
            var vectors = new (ulong Address, ulong Bytes)[1024];
            vectors[^1] = (data, 1);
            Message(model, vectors);
            *model.Files.Memory.At(data) = 0xa5;
            HostCheck.Equal(Tx.Status.Success, Tx.ReceiveFrame(connection.Owner,
                Wire.Packet(100, "Q"u8, ack: 700, flags: 0x18), out _));
            HostCheck.True(LinuxDescriptorServices.Capture(model.Files.State, fd, out var token));
            byte[] owner = new ReadOnlySpan<byte>(connection.Owner, sizeof(Tx.Owner)).ToArray();
            byte[] receiver = new ReadOnlySpan<byte>(connection.Receiver, sizeof(OS.Net.TcpReceiveWindow.Owner)).ToArray();
            byte[] user = new ReadOnlySpan<byte>(model.Files.Memory.At(Header), 28672 - 4096 + 1).ToArray();
            uint outputs = model.Files.Context->OutputCopies;
            model.Files.Context->InputCopies = 0;
            model.Files.Context->FailInputAt = 3;
            HostCheck.Equal(-14L, model.Call(number, fd, Header));
            HostCheck.Equal(3U, model.Files.Context->InputCopies);
            HostCheck.Equal(outputs, model.Files.Context->OutputCopies);
            HostCheck.True(owner.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(connection.Owner, sizeof(Tx.Owner))));
            HostCheck.True(receiver.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(connection.Receiver, sizeof(OS.Net.TcpReceiveWindow.Owner))));
            HostCheck.True(user.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(model.Files.Memory.At(Header), user.Length)));
            HostCheck.True(LinuxDescriptorServices.IsCurrent(model.Files.State, token));
            model.Files.Context->FailInputAt = 0;
            HostCheck.Equal(1L, model.Call(number, fd, Header));
            Finish(model, connection, fd);
        }
    }

    private static void Message(Model model, params (ulong Address, ulong Bytes)[] vectors)
    {
        new Span<byte>(model.Files.Memory.At(Header), 56).Clear();
        ulong* header = (ulong*)model.Files.Memory.At(Header);
        header[2] = Vectors;
        header[3] = (ulong)vectors.Length;
        ulong* table = (ulong*)model.Files.Memory.At(Vectors);
        for (int i = 0; i < vectors.Length; i++)
        {
            table[i * 2] = vectors[i].Address;
            table[i * 2 + 1] = vectors[i].Bytes;
        }
    }

    private static void Finish(Model model, TcpSendWindowTests.Fixture connection, uint fd)
    {
        HostCheck.Equal(0L, model.Call(3, fd));
        SocketCases.AbortAndReap(model, connection);
        model.End();
    }
}
