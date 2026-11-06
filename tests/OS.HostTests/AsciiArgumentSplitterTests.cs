using System.Text;
using OS.Std.Text;

internal static unsafe class AsciiArgumentSplitterTests
{
    private const byte Sentinel = 0xcc;
    private const uint SliceSentinel = 0xcccccccc;

    [HostTest("std.ascii arguments strips balanced quotes anywhere in tokens")]
    private static void QuotedArguments()
    {
        Expect("a\"b c\"d \"\"", ["ab cd", ""]);
        Expect("  alpha \"beta gamma\" delta  ", ["alpha", "beta gamma", "delta"]);
        Expect("a\"\"b \"c\"d \"\"\"\"", ["ab", "cd", ""]);
        Expect("\"\" \"\"", ["", ""]);
        Expect("\" \"", [" "]);
        Expect("x=y ''", ["x=y", "''"]);
    }

    [HostTest("std.ascii arguments accepts empty input and empty quoted arguments")]
    private static void EmptyArguments()
    {
        Expect("", []);
        Expect("     ", []);
        Expect("\"\"", [""]);
        HostCheck.Equal(AsciiArgumentStatus.Success,
            AsciiArgumentSplitter.Measure(null, 0, out uint count, out uint bytes));
        HostCheck.Equal(0u, count);
        HostCheck.Equal(0u, bytes);
        HostCheck.Equal(AsciiArgumentStatus.Success,
            AsciiArgumentSplitter.Split(null, 0, null, 0, null, 0, out count, out bytes));
        HostCheck.Equal(0u, count);
        HostCheck.Equal(0u, bytes);
    }

    [HostTest("std.ascii arguments rejects malformed syntax and nonprintable bytes atomically")]
    private static void InvalidSyntax()
    {
        foreach (string text in new[] { "\"", "a\"bc", "\"a\"b\"" })
            Reject(Encoding.ASCII.GetBytes(text), AsciiArgumentStatus.UnbalancedQuotes);
        foreach (string text in new[] { "a\\b", "\"a\\b\"" })
            Reject(Encoding.ASCII.GetBytes(text), AsciiArgumentStatus.BackslashNotSupported);
        foreach (byte value in new byte[] { 0, 9, 10, 13, 31, 127, 128, 255 })
            Reject([(byte)'a', value, (byte)'b'], AsciiArgumentStatus.InvalidCharacter);
    }

    [HostTest("std.ascii arguments validates null short and wrapping destination extents")]
    private static void CapacityAndExtentFailures()
    {
        byte[] source = "abc"u8.ToArray(), output = FilledBytes(16);
        AsciiArgumentSlice[] slices = FilledSlices(2);
        fixed (byte* p = source)
        fixed (byte* o = output)
        fixed (AsciiArgumentSlice* s = slices)
        {
            Failure(AsciiArgumentStatus.OutputTooSmall,
                AsciiArgumentSplitter.Split(p, 3, o, 3, s, 2, out uint count, out uint bytes), count, bytes);
            Failure(AsciiArgumentStatus.SlicesTooSmall,
                AsciiArgumentSplitter.Split(p, 3, o, 16, s, 0, out count, out bytes), count, bytes);
            Failure(AsciiArgumentStatus.InvalidOutput,
                AsciiArgumentSplitter.Split(p, 3, null, 4, s, 2, out count, out bytes), count, bytes);
            Failure(AsciiArgumentStatus.InvalidSlices,
                AsciiArgumentSplitter.Split(p, 3, o, 16, null, 1, out count, out bytes), count, bytes);
            Failure(AsciiArgumentStatus.InvalidInput,
                AsciiArgumentSplitter.Measure(null, 1, out count, out bytes), count, bytes);
            Failure(AsciiArgumentStatus.InvalidInput,
                AsciiArgumentSplitter.Split(null, 1, o, 16, s, 2, out count, out bytes), count, bytes);
            Failure(AsciiArgumentStatus.InvalidInput,
                AsciiArgumentSplitter.Measure((byte*)(ulong.MaxValue - 1), 3, out count, out bytes), count, bytes);
            Failure(AsciiArgumentStatus.InvalidInput,
                AsciiArgumentSplitter.Split((byte*)(ulong.MaxValue - 1), 3, o, 16, s, 2, out count, out bytes), count, bytes);
            Failure(AsciiArgumentStatus.InvalidOutput,
                AsciiArgumentSplitter.Split(p, 3, (byte*)(ulong.MaxValue - 1), 4, s, 2, out count, out bytes), count, bytes);
            Failure(AsciiArgumentStatus.InvalidSlices,
                AsciiArgumentSplitter.Split(p, 3, o, 16, (AsciiArgumentSlice*)(ulong.MaxValue - 3), 1, out count, out bytes), count, bytes);
        }
        HostCheck.True(source.AsSpan().SequenceEqual("abc"u8));
        Unchanged(output, slices);
    }

    [HostTest("std.ascii arguments rejects each pairwise storage alias before writes")]
    private static void Aliasing()
    {
        byte[] source = FilledBytes(64), output = FilledBytes(64);
        "abc"u8.CopyTo(source);
        byte[] original = source.ToArray();
        AsciiArgumentSlice[] slices = FilledSlices(4);
        fixed (byte* p = source)
        fixed (byte* o = output)
        fixed (AsciiArgumentSlice* s = slices)
        {
            Failure(AsciiArgumentStatus.OverlappingStorage,
                AsciiArgumentSplitter.Split(p, 3, p, 16, s, 4, out uint count, out uint bytes), count, bytes);
            Failure(AsciiArgumentStatus.OverlappingStorage,
                AsciiArgumentSplitter.Split(p, 3, p + 2, 16, s, 4, out count, out bytes), count, bytes);
            Failure(AsciiArgumentStatus.OverlappingStorage,
                AsciiArgumentSplitter.Split(p, 3, o, 16, (AsciiArgumentSlice*)p, 4, out count, out bytes), count, bytes);
            Failure(AsciiArgumentStatus.OverlappingStorage,
                AsciiArgumentSplitter.Split(p, 3, o, 16, (AsciiArgumentSlice*)o, 4, out count, out bytes), count, bytes);
            // The declared output tail overlaps slices even though four emitted bytes would not.
            Failure(AsciiArgumentStatus.OverlappingStorage,
                AsciiArgumentSplitter.Split(p, 3, o, 16, (AsciiArgumentSlice*)(o + 8), 1, out count, out bytes), count, bytes);
        }
        HostCheck.True(source.SequenceEqual(original));
        Unchanged(output, slices);

        fixed (byte* p = source)
        fixed (AsciiArgumentSlice* s = slices)
        {
            HostCheck.Equal(AsciiArgumentStatus.Success,
                AsciiArgumentSplitter.Split(p, 3, p + 3, 4, s, 1, out uint count, out uint bytes));
            HostCheck.Equal(1u, count);
            HostCheck.Equal(4u, bytes);
        }
        HostCheck.True(source.AsSpan(0, 3).SequenceEqual("abc"u8));
        HostCheck.True(source.AsSpan(3, 4).SequenceEqual("abc\0"u8));
        HostCheck.Equal(0u, slices[0].Offset);
        HostCheck.Equal(3u, slices[0].Length);
        for (int i = 7; i < source.Length; i++)
            HostCheck.Equal(Sentinel, source[i]);
    }

    [HostTest("std.ascii arguments accepts 256 bytes and 128 args with exact output capacity")]
    private static void MaximumBounds()
    {
        Expect(new string('x', 256), [new string('x', 256)], exactCapacity: true);
        string[] expected = new string[128];
        expected[0] = "aa";
        Array.Fill(expected, "a", 1, 127);
        string text = "aa" + string.Concat(Enumerable.Repeat(" a", 127));
        HostCheck.Equal(256, text.Length);
        Expect(text, expected, exactCapacity: true);
        Reject(Encoding.ASCII.GetBytes("a" + string.Concat(Enumerable.Repeat(" a", 128))),
            AsciiArgumentStatus.InputTooLong);
    }

    [HostTest("std.ascii arguments measure and split allocate zero managed bytes")]
    private static void AllocationFree()
    {
        byte[] source = "alpha beta"u8.ToArray(), output = new byte[32];
        AsciiArgumentSlice[] slices = new AsciiArgumentSlice[4];
        bool valid = true;
        long allocated;
        fixed (byte* p = source)
        fixed (byte* o = output)
        fixed (AsciiArgumentSlice* s = slices)
        {
            AsciiArgumentSplitter.Measure(p, 10, out _, out _);
            AsciiArgumentSplitter.Split(p, 10, o, 32, s, 4, out _, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
            {
                valid &= AsciiArgumentSplitter.Measure(p, 10, out uint count, out uint bytes) == AsciiArgumentStatus.Success;
                valid &= count == 2 && bytes == 11;
                valid &= AsciiArgumentSplitter.Split(p, 10, o, 32, s, 4, out count, out bytes) == AsciiArgumentStatus.Success;
                valid &= count == 2 && bytes == 11;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        HostCheck.True(valid);
        HostCheck.Equal(0L, allocated);
    }

    private static void Expect(string text, string[] expected, bool exactCapacity = false)
    {
        byte[] source = Encoding.ASCII.GetBytes(text), original = source.ToArray();
        uint expectedBytes = (uint)expected.Sum(token => token.Length + 1);
        byte[] output = FilledBytes((int)expectedBytes + 8);
        AsciiArgumentSlice[] slices = FilledSlices(expected.Length + 2);
        fixed (byte* p = source)
        fixed (byte* o = output)
        fixed (AsciiArgumentSlice* s = slices)
        {
            HostCheck.Equal(AsciiArgumentStatus.Success,
                AsciiArgumentSplitter.Measure(p, (uint)source.Length, out uint count, out uint bytes));
            HostCheck.Equal((uint)expected.Length, count);
            HostCheck.Equal(expectedBytes, bytes);
            HostCheck.Equal(AsciiArgumentStatus.Success,
                AsciiArgumentSplitter.Split(p, (uint)source.Length, o,
                    exactCapacity ? expectedBytes : (uint)output.Length, s,
                    exactCapacity ? (uint)expected.Length : (uint)slices.Length, out count, out bytes));
            HostCheck.Equal((uint)expected.Length, count);
            HostCheck.Equal(expectedBytes, bytes);
        }
        uint at = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            byte[] token = Encoding.ASCII.GetBytes(expected[i]);
            HostCheck.Equal(at, slices[i].Offset);
            HostCheck.Equal((uint)token.Length, slices[i].Length);
            HostCheck.True(output.AsSpan((int)at, token.Length).SequenceEqual(token));
            at += (uint)token.Length;
            HostCheck.Equal((byte)0, output[at++]);
        }
        for (uint i = at; i < output.Length; i++)
            HostCheck.Equal(Sentinel, output[i]);
        for (int i = expected.Length; i < slices.Length; i++)
        {
            HostCheck.Equal(SliceSentinel, slices[i].Offset);
            HostCheck.Equal(SliceSentinel, slices[i].Length);
        }
        HostCheck.True(source.SequenceEqual(original));
    }

    private static void Reject(byte[] source, AsciiArgumentStatus expected)
    {
        byte[] original = source.ToArray(), output = FilledBytes(512);
        AsciiArgumentSlice[] slices = FilledSlices(128);
        fixed (byte* p = source)
        fixed (byte* o = output)
        fixed (AsciiArgumentSlice* s = slices)
        {
            Failure(expected, AsciiArgumentSplitter.Measure(p, (uint)source.Length, out uint count, out uint bytes), count, bytes);
            Failure(expected, AsciiArgumentSplitter.Split(p, (uint)source.Length, o, 512, s, 128, out count, out bytes), count, bytes);
        }
        HostCheck.True(source.SequenceEqual(original));
        Unchanged(output, slices);
    }

    private static void Failure(AsciiArgumentStatus expected, AsciiArgumentStatus actual, uint count, uint bytes)
    {
        HostCheck.Equal(expected, actual);
        HostCheck.Equal(0u, count);
        HostCheck.Equal(0u, bytes);
    }

    private static byte[] FilledBytes(int count)
    {
        byte[] bytes = new byte[count];
        Array.Fill(bytes, Sentinel);
        return bytes;
    }

    private static AsciiArgumentSlice[] FilledSlices(int count)
    {
        AsciiArgumentSlice[] slices = new AsciiArgumentSlice[count];
        Array.Fill(slices, new AsciiArgumentSlice { Offset = SliceSentinel, Length = SliceSentinel });
        return slices;
    }

    private static void Unchanged(byte[] output, AsciiArgumentSlice[] slices)
    {
        foreach (byte value in output)
            HostCheck.Equal(Sentinel, value);
        foreach (AsciiArgumentSlice value in slices)
        {
            HostCheck.Equal(SliceSentinel, value.Offset);
            HostCheck.Equal(SliceSentinel, value.Length);
        }
    }
}
