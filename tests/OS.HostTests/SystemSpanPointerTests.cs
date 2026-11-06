internal static unsafe class SystemSpanPointerTests
{
    [HostTest("span.pointer mirror matches BCL length pinning default and empty behavior")]
    private static void Parity()
    {
        byte* data = stackalloc byte[8];
        data[0] = 0x42;
        data[7] = 0x96;
        var mirror = new OS.Std.NoRuntime.Span<byte>(data, 8);
        var bcl = new Span<byte>(data, 8);
        HostCheck.Equal(bcl.Length, mirror.Length);
        fixed (byte* actual = mirror)
        fixed (byte* expected = bcl)
        {
            HostCheck.True(actual == expected);
            actual[7] = 0x17;
            HostCheck.Equal((byte)0x17, bcl[7]);
        }
        var empty = new OS.Std.NoRuntime.Span<byte>(data, 0);
        var absent = default(OS.Std.NoRuntime.Span<byte>);
        fixed (byte* a = empty)
        fixed (byte* b = absent)
            HostCheck.True(a == null && b == null && empty.Length == 0 && absent.Length == 0);
        var emptyProperty = OS.Std.NoRuntime.Span<byte>.Empty;
        var readOnlyEmpty = OS.Std.NoRuntime.ReadOnlySpan<byte>.Empty;
        fixed (byte* a = emptyProperty)
        fixed (byte* b = readOnlyEmpty)
            HostCheck.True(a == null && b == null && emptyProperty.Length == 0 && readOnlyEmpty.Length == 0);
        ushort* words = stackalloc ushort[2];
        words[1] = 0x1234;
        var typed = new OS.Std.NoRuntime.Span<ushort>(words, 2);
        fixed (ushort* p = typed)
            HostCheck.True(p == words && p[1] == 0x1234 && typed.Length == 2);
        var nullNonempty = new OS.Std.NoRuntime.Span<byte>(null, 1);
        fixed (byte* p = nullNonempty)
            HostCheck.True(p == null && nullNonempty.Length == 1);
    }

    [HostTest("span.pointer mirror rejects negative length and allocates nothing on valid operations")]
    private static void BoundsAndAllocation()
    {
        bool rejected = false;
        try
        {
            _ = new OS.Std.NoRuntime.Span<byte>(null, -1);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
        byte* data = stackalloc byte[4];
        for (int i = 0; i < 32; i++)
            Touch(data);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
            Touch(data);
        HostCheck.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    private static void Touch(byte* data)
    {
        var value = new OS.Std.NoRuntime.Span<byte>(data, 4);
        fixed (byte* p = value)
            p[value.Length - 1] = 0x23;
    }
}
