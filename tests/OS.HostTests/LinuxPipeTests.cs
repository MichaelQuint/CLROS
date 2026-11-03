using System.Runtime.CompilerServices;
using OS.Kernel.Compatibility.LinuxX64;

internal static unsafe class LinuxPipeTests
{
    [HostTest("m16-007 pipe retained storage admission rejects malformed and overlapping ranges", HostLane.Fast, 0)]
    private static void Admission()
    {
        byte* retained = stackalloc byte[4096];
        byte* replacement = stackalloc byte[4096];
        new Span<byte>(retained, 4096).Fill(0xa5);
        new Span<byte>(replacement, 4096).Fill(0x6d);
        LinuxPipe pipe = default;
        HostCheck.True(!pipe.Initialized && pipe.Capacity == 0 && pipe.BufferedBytes == 0 &&
            pipe.ReaderCount == 0 && pipe.WriterCount == 0);
        HostCheck.Equal(-9L, pipe.Peek(null, 0, true));
        HostCheck.Equal(-9L, pipe.Write(null, 0, true, true));
        HostCheck.True(!pipe.Consume(0) && !pipe.DuplicateReader() && !pipe.DuplicateWriter() &&
            !pipe.CloseReader() && !pipe.CloseWriter() && !pipe.Release());
        HostCheck.True(!pipe.Initialize(null, 4096));
        foreach (uint capacity in new uint[] { 0, 2048, 4095, 4097, 65537, 131072 })
            HostCheck.True(!pipe.Initialize(retained, capacity));
        HostCheck.True(!pipe.Initialize((byte*)(ulong.MaxValue - 4095), 4096));
        HostCheck.True(!pipe.Initialize((byte*)&pipe, 4096));
        HostCheck.True(new ReadOnlySpan<byte>(retained, 4096).IndexOfAnyExcept((byte)0xa5) == -1);
        HostCheck.True(pipe.Initialize(retained, 4096));
        HostCheck.True(pipe.Initialized && pipe.Capacity == 4096 && pipe.BufferedBytes == 0 &&
            pipe.ReaderCount == 1 && pipe.WriterCount == 1);
        HostCheck.True(new ReadOnlySpan<byte>(retained, 4096).IndexOfAnyExcept((byte)0) == -1);
        byte[] original = Snapshot(&pipe);
        HostCheck.True(!pipe.Initialize(replacement, 4096));
        HostCheck.True(original.AsSpan().SequenceEqual(Snapshot(&pipe)) &&
            new ReadOnlySpan<byte>(replacement, 4096).IndexOfAnyExcept((byte)0x6d) == -1);
    }

    [HostTest("m16-007 pipe admits every supported power of two and scrubs released ownership", HostLane.Fast, 1)]
    private static void CapacitiesAndRelease()
    {
        byte[] retained = new byte[65536];
        fixed (byte* buffer = retained)
        {
            foreach (uint capacity in new uint[] { 4096, 8192, 16384, 32768, 65536 })
            {
                retained.AsSpan().Fill(0x7e);
                LinuxPipe pipe = default;
                HostCheck.True(pipe.Initialize(buffer, capacity));
                HostCheck.Equal(capacity, pipe.Capacity);
                HostCheck.True(retained.AsSpan(0, (int)capacity).IndexOfAnyExcept((byte)0) == -1);
                HostCheck.True(retained.AsSpan((int)capacity).IndexOfAnyExcept((byte)0x7e) == -1);
                byte value = 0x91;
                HostCheck.Equal(1L, pipe.Write(&value, 1, true, true));
                HostCheck.True(!pipe.Release() && pipe.CloseReader() && !pipe.Release());
                HostCheck.True(pipe.CloseWriter() && pipe.Release());
                HostCheck.True(!pipe.Initialized && pipe.Capacity == 0 && pipe.BufferedBytes == 0 &&
                    pipe.ReaderCount == 0 && pipe.WriterCount == 0);
                HostCheck.True(retained.AsSpan(0, (int)capacity).IndexOfAnyExcept((byte)0) == -1);
                HostCheck.True(retained.AsSpan((int)capacity).IndexOfAnyExcept((byte)0x7e) == -1);
                HostCheck.True(!pipe.Release() && !pipe.CloseReader() && !pipe.CloseWriter() &&
                    !pipe.DuplicateReader() && !pipe.DuplicateWriter());
                HostCheck.True(pipe.Initialize(buffer, capacity));
                HostCheck.True(pipe.CloseReader() && pipe.CloseWriter() && pipe.Release());
            }
        }
    }

    [HostTest("m16-007 pipe peeks transactionally then drains data before writer EOF", HostLane.Fast, 2)]
    private static void PeekAndEof()
    {
        byte* retained = stackalloc byte[4096];
        byte* output = stackalloc byte[16];
        byte* input = stackalloc byte[7] { 0x10, 0x31, 0x52, 0x73, 0x94, 0xb5, 0xd6 };
        LinuxPipe pipe = default;
        HostCheck.True(pipe.Initialize(retained, 4096));
        HostCheck.Equal(-11L, pipe.Peek(output, 16, true));
        HostCheck.Equal(7L, pipe.Write(input, 7, true, false));
        new Span<byte>(output, 16).Fill(0xee);
        HostCheck.Equal(4L, pipe.Peek(output, 4, true));
        HostCheck.True(new ReadOnlySpan<byte>(output, 4).SequenceEqual(new byte[] { 0x10, 0x31, 0x52, 0x73 }));
        HostCheck.Equal(7U, pipe.BufferedBytes); // Failed external copy-out can retry the same prefix.
        HostCheck.Equal(7L, pipe.Peek(output, 16, true));
        HostCheck.True(new ReadOnlySpan<byte>(output, 7).SequenceEqual(new byte[] { 0x10, 0x31, 0x52, 0x73, 0x94, 0xb5, 0xd6 }));
        HostCheck.True(new ReadOnlySpan<byte>(output + 7, 9).IndexOfAnyExcept((byte)0xee) == -1);
        byte[] original = Snapshot(&pipe);
        HostCheck.True(!pipe.Consume(8));
        HostCheck.True(original.AsSpan().SequenceEqual(Snapshot(&pipe)));
        HostCheck.True(pipe.Consume(3));
        HostCheck.Equal(4U, pipe.BufferedBytes);
        HostCheck.True(new ReadOnlySpan<byte>(retained, 3).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.True(pipe.DuplicateWriter() && pipe.CloseWriter());
        HostCheck.Equal(1U, pipe.WriterCount);
        HostCheck.Equal(4L, pipe.Peek(output, 16, true));
        HostCheck.True(new ReadOnlySpan<byte>(output, 4).SequenceEqual(new byte[] { 0x73, 0x94, 0xb5, 0xd6 }));
        HostCheck.True(pipe.CloseWriter() && !pipe.DuplicateWriter() && !pipe.CloseWriter());
        HostCheck.Equal(4L, pipe.Peek(output, 16, true));
        HostCheck.True(pipe.Consume(4));
        new Span<byte>(output, 16).Fill(0xc7);
        HostCheck.Equal(0L, pipe.Peek(output, 16, true));
        HostCheck.True(new ReadOnlySpan<byte>(output, 16).IndexOfAnyExcept((byte)0xc7) == -1);
        HostCheck.Equal(-9L, pipe.Write(input, 7, true, true));
        HostCheck.True(pipe.CloseReader() && pipe.Release());
    }

    [HostTest("m16-007 pipe atomic 4096-byte writes and larger prefixes preserve every byte", HostLane.Fast, 3)]
    private static void AtomicAndPartialWrites()
    {
        byte* retained = stackalloc byte[8192];
        byte* input = stackalloc byte[12288];
        byte* output = stackalloc byte[8192];
        for (uint i = 0; i < 12288; i++)
            input[i] = (byte)(i * 29 + (i >> 8) * 13 + 7);
        LinuxPipe pipe = default;
        HostCheck.True(pipe.Initialize(retained, 8192));
        HostCheck.Equal(5000L, pipe.Write(input, 5000, true, true));
        byte[] original = Snapshot(&pipe);
        byte[] bytes = new ReadOnlySpan<byte>(retained, 8192).ToArray();
        HostCheck.Equal(-11L, pipe.Write(input + 5000, 4096, true, true));
        HostCheck.True(original.AsSpan().SequenceEqual(Snapshot(&pipe)) &&
            bytes.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(retained, 8192)));
        HostCheck.Equal(3192L, pipe.Write(input + 5000, 5000, true, true));
        HostCheck.Equal(8192U, pipe.BufferedBytes);
        HostCheck.Equal(-11L, pipe.Write(input, 1, true, true));
        HostCheck.Equal(-11L, pipe.Write(input, 5000, true, true));
        HostCheck.Equal(8192L, pipe.Peek(output, 8192, true));
        HostCheck.True(new ReadOnlySpan<byte>(output, 8192).SequenceEqual(new ReadOnlySpan<byte>(input, 8192)));
        HostCheck.True(pipe.Consume(4096));
        HostCheck.Equal(4096L, pipe.Write(input + 8192, 4096, true, true));
        HostCheck.Equal(8192L, pipe.Peek(output, 8192, true));
        HostCheck.True(new ReadOnlySpan<byte>(output, 8192).SequenceEqual(new ReadOnlySpan<byte>(input + 4096, 8192)));
        HostCheck.True(pipe.Consume(8192));
        HostCheck.Equal(4096L, pipe.Write(input, 4096, true, true));
        HostCheck.Equal(4096L, pipe.Peek(output, 8192, true));
        HostCheck.True(new ReadOnlySpan<byte>(output, 4096).SequenceEqual(new ReadOnlySpan<byte>(input, 4096)));
        HostCheck.True(pipe.Consume(4096) && pipe.CloseReader() && pipe.CloseWriter() && pipe.Release());
    }

    [HostTest("m16-007 pipe wraparound follows an independent byte stream across repeated cycles", HostLane.Fast, 4)]
    private static void Wraparound()
    {
        byte* retained = stackalloc byte[4096];
        byte* input = stackalloc byte[6000];
        byte* output = stackalloc byte[6000];
        LinuxPipe pipe = default;
        HostCheck.True(pipe.Initialize(retained, 4096));
        var expected = new Queue<byte>();
        uint written = 0, read = 0;
        for (uint round = 0; round < 24; round++)
        {
            uint request = round % 3 == 0 ? 5000U : 1U + round * 83;
            for (uint i = 0; i < request; i++)
                input[i] = StreamByte(written + i);
            long result = pipe.Write(input, request, true, true);
            if (result > 0)
            {
                for (uint i = 0; i < result; i++)
                    expected.Enqueue(StreamByte(written + i));
                written += (uint)result;
            }
            else
                HostCheck.Equal(-11L, result);
            HostCheck.Equal((uint)expected.Count, pipe.BufferedBytes);
            HostCheck.Equal((long)expected.Count, pipe.Peek(output, 6000, true));
            HostCheck.True(new ReadOnlySpan<byte>(output, expected.Count).SequenceEqual(expected.ToArray()));
            uint take = (uint)Math.Min(expected.Count, 700 + (int)round * 19);
            for (uint i = 0; i < take; i++)
                HostCheck.Equal(StreamByte(read + i), expected.Dequeue());
            HostCheck.True(pipe.Consume(take));
            read += take;
        }
        HostCheck.True(written > 5 * 4096 && read > 4 * 4096);
        HostCheck.Equal((long)expected.Count, pipe.Peek(output, 6000, true));
        HostCheck.True(new ReadOnlySpan<byte>(output, expected.Count).SequenceEqual(expected.ToArray()));
        HostCheck.True(pipe.Consume((uint)expected.Count));
        HostCheck.Equal(0U, pipe.BufferedBytes);
        HostCheck.True(new ReadOnlySpan<byte>(retained, 4096).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.True(pipe.CloseReader() && pipe.CloseWriter() && pipe.Release());
    }

    [HostTest("m16-007 pipe rejects bad copy ranges without changing bytes or ownership", HostLane.Fast, 5)]
    private static void BadCopyRanges()
    {
        byte* retained = stackalloc byte[4096];
        byte* input = stackalloc byte[32];
        byte* output = stackalloc byte[32];
        new Span<byte>(input, 32).Fill(0x51);
        new Span<byte>(output, 32).Fill(0x76);
        LinuxPipe pipe = default;
        HostCheck.True(pipe.Initialize(retained, 4096));
        HostCheck.Equal(32L, pipe.Write(input, 32, true, true));
        byte[] original = Snapshot(&pipe);
        byte[] bytes = new ReadOnlySpan<byte>(retained, 4096).ToArray();
        HostCheck.Equal(-14L, pipe.Peek(null, 1, true));
        HostCheck.Equal(-14L, pipe.Write(null, 1, true, true));
        HostCheck.Equal(-14L, pipe.Peek((byte*)(ulong.MaxValue - 1), 2, true));
        HostCheck.Equal(-14L, pipe.Write((byte*)(ulong.MaxValue - 1), 2, true, true));
        HostCheck.Equal(-14L, pipe.Peek(retained, 1, true));
        HostCheck.Equal(-14L, pipe.Write(retained + 4095, 1, true, true));
        HostCheck.Equal(-14L, pipe.Peek(retained - 1, 2, true));
        HostCheck.Equal(-14L, pipe.Write(retained - 1, 2, true, true));
        HostCheck.Equal(-14L, pipe.Peek((byte*)&pipe, 1, true));
        HostCheck.Equal(-14L, pipe.Write((byte*)&pipe, 1, true, true));
        HostCheck.True(original.AsSpan().SequenceEqual(Snapshot(&pipe)) &&
            bytes.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(retained, 4096)) &&
            new ReadOnlySpan<byte>(output, 32).IndexOfAnyExcept((byte)0x76) == -1);
        HostCheck.Equal(32L, pipe.Peek(output, 32, true));
        HostCheck.True(new ReadOnlySpan<byte>(output, 32).IndexOfAnyExcept((byte)0x51) == -1);
    }

    [HostTest("m16-007 pipe zero lengths blocking rejection SIGPIPE policy and balanced references", HostLane.Fast, 6)]
    private static void UnsupportedAndReferences()
    {
        byte* retained = stackalloc byte[4096];
        byte value = 0x85;
        LinuxPipe pipe = default;
        HostCheck.True(pipe.Initialize(retained, 4096));
        HostCheck.Equal(0L, pipe.Peek(null, 0, false));
        HostCheck.Equal(0L, pipe.Write(null, 0, false, false));
        HostCheck.True(pipe.Consume(0));
        byte[] original = Snapshot(&pipe);
        HostCheck.Equal(-38L, pipe.Peek(null, 1, false));
        HostCheck.Equal(-38L, pipe.Write(null, 1, false, false));
        HostCheck.True(original.AsSpan().SequenceEqual(Snapshot(&pipe)));
        HostCheck.True(pipe.DuplicateReader() && pipe.DuplicateReader() && pipe.DuplicateWriter());
        HostCheck.True(pipe.ReaderCount == 3 && pipe.WriterCount == 2);
        HostCheck.True(pipe.CloseReader() && pipe.CloseReader());
        HostCheck.Equal(1L, pipe.Write(&value, 1, true, false));
        HostCheck.True(pipe.CloseReader() && !pipe.CloseReader() && !pipe.DuplicateReader());
        HostCheck.Equal(-9L, pipe.Peek(null, 0, true));
        HostCheck.True(!pipe.Consume(0) && !pipe.Consume(1));
        original = Snapshot(&pipe);
        HostCheck.Equal(-38L, pipe.Write(&value, 1, true, false));
        HostCheck.Equal(-32L, pipe.Write(&value, 1, true, true));
        HostCheck.Equal(-38L, pipe.Write(&value, 1, false, true));
        HostCheck.Equal(0L, pipe.Write(null, 0, false, false));
        HostCheck.True(original.AsSpan().SequenceEqual(Snapshot(&pipe)) && retained[0] == value);
        HostCheck.True(pipe.CloseWriter() && pipe.CloseWriter() && !pipe.CloseWriter() && !pipe.DuplicateWriter());
        HostCheck.Equal(-9L, pipe.Write(null, 0, true, true));
        HostCheck.True(pipe.Release());
        HostCheck.True(new ReadOnlySpan<byte>(retained, 4096).IndexOfAnyExcept((byte)0) == -1);
    }

    [HostTest("m16-007 pipe exhausted references and corrupted indices fail without mutation", HostLane.Fast, 7)]
    private static void ExhaustionAndBadState()
    {
        byte* retained = stackalloc byte[4096];
        byte value = 0x3d;
        LinuxPipe pipe = default;
        HostCheck.True(pipe.Initialize(retained, 4096));
        Readers(ref pipe) = uint.MaxValue;
        Writers(ref pipe) = uint.MaxValue;
        byte[] original = Snapshot(&pipe);
        HostCheck.True(!pipe.DuplicateReader() && !pipe.DuplicateWriter());
        HostCheck.True(original.AsSpan().SequenceEqual(Snapshot(&pipe)));
        HostCheck.True(pipe.CloseReader() && pipe.CloseWriter());
        HostCheck.True(pipe.ReaderCount == uint.MaxValue - 1 && pipe.WriterCount == uint.MaxValue - 1);
        Readers(ref pipe) = 1;
        Writers(ref pipe) = 1;
        HostCheck.Equal(1L, pipe.Write(&value, 1, true, true));
        ReadIndex(ref pipe) = 4096;
        original = Snapshot(&pipe);
        byte[] bytes = new ReadOnlySpan<byte>(retained, 4096).ToArray();
        HostCheck.Equal(-9L, pipe.Peek(&value, 1, true));
        HostCheck.Equal(-9L, pipe.Write(&value, 1, true, true));
        HostCheck.True(!pipe.Consume(1) && !pipe.DuplicateReader() && !pipe.DuplicateWriter() &&
            !pipe.CloseReader() && !pipe.CloseWriter() && !pipe.Release());
        HostCheck.True(original.AsSpan().SequenceEqual(Snapshot(&pipe)) &&
            bytes.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(retained, 4096)));
        ReadIndex(ref pipe) = 0;
        Buffered(ref pipe) = 0; // Write index is still one: inconsistent ring.
        original = Snapshot(&pipe);
        HostCheck.Equal(-9L, pipe.Write(&value, 1, true, true));
        HostCheck.True(!pipe.Consume(0) && original.AsSpan().SequenceEqual(Snapshot(&pipe)));
    }

    [HostTest("m16-007 pipe production operations allocate no managed storage", HostLane.Fast, 8)]
    private static void NoAllocation()
    {
        byte* retained = stackalloc byte[4096];
        byte value = 0x28, output = 0;
        LinuxPipe pipe = default;
        HostCheck.True(pipe.Initialize(retained, 4096));
        // Warm each operation before measuring the production-only sequence.
        HostCheck.Equal(1L, pipe.Write(&value, 1, true, true));
        HostCheck.Equal(1L, pipe.Peek(&output, 1, true));
        HostCheck.True(pipe.Consume(1) && pipe.DuplicateReader() && pipe.CloseReader() &&
            pipe.DuplicateWriter() && pipe.CloseWriter());
        long before = GC.GetAllocatedBytesForCurrentThread();
        long write = pipe.Write(&value, 1, true, true), peek = pipe.Peek(&output, 1, true);
        bool consumed = pipe.Consume(1), reader = pipe.DuplicateReader(), readerClosed = pipe.CloseReader(),
            writer = pipe.DuplicateWriter(), writerClosed = pipe.CloseWriter();
        bool lastReader = pipe.CloseReader(), lastWriter = pipe.CloseWriter(), released = pipe.Release();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(write == 1 && peek == 1 && output == value && consumed && reader && readerClosed &&
            writer && writerClosed && lastReader && lastWriter && released && allocated == 0);
    }

    private static byte[] Snapshot(LinuxPipe* pipe) => new ReadOnlySpan<byte>(pipe, sizeof(LinuxPipe)).ToArray();
    private static byte StreamByte(uint sequence) => (byte)(sequence * 17 + (sequence >> 8) * 31 + 0x43);

    // Deliberately inject unreachable states to check rejection/overflow; no
    // production accessors or public mutation hooks are added for these tests.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_readers")]
    private static extern ref uint Readers(ref LinuxPipe pipe);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_writers")]
    private static extern ref uint Writers(ref LinuxPipe pipe);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_readIndex")]
    private static extern ref uint ReadIndex(ref LinuxPipe pipe);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_buffered")]
    private static extern ref uint Buffered(ref LinuxPipe pipe);
}
