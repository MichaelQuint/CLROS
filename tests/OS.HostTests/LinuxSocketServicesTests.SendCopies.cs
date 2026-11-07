using OS.Kernel.Compatibility.LinuxX64;
using Tx = OS.Net.TcpSendWindow;

internal static unsafe partial class LinuxSocketServicesTests
{
    [HostTest("socket scalar send copies only1460 of8192 offered bytes after complete validation")]
    private static void ScalarCopyCapacity()
    {
        using var m = new Model();
        using var f = Connection(capacity: 2048, mss: 1460, window: 8192);
        uint fd = m.Accept(m.Listen(), f);
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, new byte[588], out int queued));
        HostCheck.Equal(588, queued);
        for (int i = 0; i < 8192; i++)
            m.Files.Memory.At(User)[i] = (byte)(i * 17);
        ulong copied = m.Files.Context->InputBytes;
        HostCheck.Equal(1460L, m.Call(44, fd, User, 8192));
        HostCheck.Equal(1460UL, m.Files.Context->InputBytes - copied);
        HostCheck.Equal(2048, f.Owner->BufferedBytes);
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer + 588, 1460).SequenceEqual(new ReadOnlySpan<byte>(m.Files.Memory.At(User), 1460)));
        copied = m.Files.Context->InputBytes;
        HostCheck.Equal(-11L, m.Call(44, fd, ulong.MaxValue, 8192));
        HostCheck.Equal(0L, m.Call(44, fd, ulong.MaxValue, 0));
        HostCheck.Equal(-95L, m.Call(44, fd, User, 1, 1));
        HostCheck.Equal(copied, m.Files.Context->InputBytes);
        FinishSendCopy(m, f, fd);
    }

    [HostTest("socket scalar send faults invalid original tails beyond both queue capacity and scratch without payload copies")]
    private static void ScalarCopyInvalidTail()
    {
        using var m = new Model();
        using var f = Connection();
        uint fd = m.Accept(m.Listen(), f);
        ulong copied = m.Files.Context->InputBytes;
        foreach ((ulong address, ulong bytes) in new[] { (User + 28672, 8192UL), (Output, 8193UL), (ulong.MaxValue - 3, 8UL) })
        {
            HostCheck.Equal(-14L, m.Call(44, fd, address, bytes));
            HostCheck.Equal(copied, m.Files.Context->InputBytes);
            HostCheck.Equal(0, f.Owner->BufferedBytes);
        }
        HostCheck.Equal(-9L, m.Call(44, 31, ulong.MaxValue, 8192));
        FinishSendCopy(m, f, fd);
    }

    [HostTest("socket fragmented message copies only available payload after complete metadata snapshot and vector validation")]
    private static void MessageCopyCapacity()
    {
        using var m = new Model();
        using var f = Connection(capacity: 2048, mss: 1460, window: 8192);
        uint fd = m.Accept(m.Listen(), f);
        HostCheck.Equal(Tx.Status.Success, Tx.Queue(f.Owner, new byte[588], out _));
        const ulong payload = User + 12288;
        for (int i = 0; i < 8192; i++)
            m.Files.Memory.At(payload)[i] = (byte)(i * 29);
        Message(m, payload, 500);
        ((ulong*)m.Files.Memory.At(MessageHeader))[3] = 3;
        ulong* vectors = (ulong*)m.Files.Memory.At(MessageVectors);
        vectors[2] = ulong.MaxValue;
        vectors[3] = 0;
        vectors[4] = payload + 500;
        vectors[5] = 7692;
        ulong copied = m.Files.Context->InputBytes;
        HostCheck.Equal(1460L, m.Call(46, fd, MessageHeader));
        HostCheck.Equal(56UL + 48 + 1460, m.Files.Context->InputBytes - copied);
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer + 588, 1460).SequenceEqual(new ReadOnlySpan<byte>(m.Files.Memory.At(payload), 1460)));
        copied = m.Files.Context->InputBytes;
        HostCheck.Equal(-11L, m.Call(46, fd, MessageHeader));
        HostCheck.Equal(56UL + 48, m.Files.Context->InputBytes - copied);
        vectors[5] = 32768;
        copied = m.Files.Context->InputBytes;
        HostCheck.Equal(-14L, m.Call(46, fd, MessageHeader));
        HostCheck.Equal(56UL + 48, m.Files.Context->InputBytes - copied);
        HostCheck.Equal(2048, f.Owner->BufferedBytes);
        FinishSendCopy(m, f, fd);
    }

    [HostTest("socket message invalid tail and failed payload callbacks leave original retained bytes unchanged")]
    private static void MessageCopyFailures()
    {
        using var m = new Model();
        using var f = Connection();
        uint fd = m.Accept(m.Listen(), f);
        m.Files.Text(User + 12288, "abcdefgh");
        Message(m, User + 12288, 3);
        ((ulong*)m.Files.Memory.At(MessageHeader))[3] = 2;
        ulong* vectors = (ulong*)m.Files.Memory.At(MessageVectors);
        vectors[2] = User + 32768;
        vectors[3] = 1;
        ulong copied = m.Files.Context->InputBytes;
        HostCheck.Equal(-14L, m.Call(46, fd, MessageHeader));
        HostCheck.Equal(88UL, m.Files.Context->InputBytes - copied);
        HostCheck.Equal(0, f.Owner->BufferedBytes);
        vectors[2] = User + 12291;
        vectors[3] = 5;
        m.Files.Context->FailInputAt = m.Files.Context->InputCopies + 4;
        HostCheck.Equal(-14L, m.Call(46, fd, MessageHeader));
        HostCheck.Equal(0, f.Owner->BufferedBytes);
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 8).IndexOfAnyExcept((byte)0) < 0);
        m.Files.Context->FailInputAt = 0;
        HostCheck.Equal(8L, m.Call(46, fd, MessageHeader));
        HostCheck.True(new ReadOnlySpan<byte>(f.Buffer, 8).SequenceEqual("abcdefgh"u8));
        FinishSendCopy(m, f, fd);
    }

    private static void FinishSendCopy(Model m, TcpSendWindowTests.Fixture f, uint fd)
    {
        HostCheck.Equal(0L, m.Call(3, fd));
        AbortAndReap(m, f);
        m.End();
    }
}
