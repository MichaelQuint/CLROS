using Mirror = OS.Std.NoRuntime;

internal static unsafe class ArpPointerSpanTests
{
    [HostTest("ARP pointer views match BCL indexing slicing clearing and shared read-only storage", HostLane.Fast, 0)]
    private static void Views()
    {
        uint* data = stackalloc uint[8];
        for (int i = 0; i < 8; i++) data[i] = (uint)(100 + i);
        var span = new Mirror.Span<uint>(data,8);
        Mirror.ReadOnlySpan<uint> read = span;
        ReadOnlySpan<uint> reference = new(data,8);
        HostCheck.Equal(8,read.Length);
        for (int i = 0; i < 8; i++) HostCheck.Equal(reference[i],read[i]);
        span.Slice(2,3).Clear();
        for (int i = 0; i < 8; i++) HostCheck.Equal(i is >= 2 and < 5 ? 0U : (uint)(100+i),read[i]);
        span[7] = 900; HostCheck.Equal(900U,read.Slice(7)[0]);
        var empty = span.Slice(8,0); var emptyRead = read.Slice(8);
        fixed (uint* p = empty) fixed (uint* q = emptyRead)
            HostCheck.True(empty.Length == 0 && emptyRead.Length == 0 && p == null && q == null);
        fixed (uint* p = read.Slice(1,4)) HostCheck.True(p == data+1);
        HostCheck.True(default(Mirror.Span<uint>).Length == 0 && default(Mirror.ReadOnlySpan<uint>).Length == 0);
    }

    [HostTest("ARP pointer views reject invalid bounds in the host mirror without modifying storage", HostLane.Fast, 1)]
    private static void Bounds()
    {
        byte* data = stackalloc byte[4]; for (int i = 0; i < 4; i++) data[i] = 0xa5;
        var span = new Mirror.Span<byte>(data,4); Mirror.ReadOnlySpan<byte> read = span;
        for (int bad = 0; bad < 10; bad++)
        {
            bool rejected = false;
            try
            {
                switch (bad)
                {
                    case 0: _ = span[-1]; break; case 1: _ = span[4]; break;
                    case 2: _ = read[-1]; break; case 3: _ = read[4]; break;
                    case 4: _ = span.Slice(-1); break; case 5: _ = read.Slice(5); break;
                    case 6: _ = span.Slice(1,-1); break; case 7: _ = read.Slice(1,4); break;
                    case 8: _ = new Mirror.ReadOnlySpan<byte>(data,-1); break;
                    case 9: _ = read.Slice(int.MinValue); break;
                }
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            HostCheck.True(rejected);
        }
        for (int i = 0; i < 4; i++) HostCheck.Equal((byte)0xa5,data[i]);
    }
}
