using OS.Std.Memory;

internal static unsafe class BoundedFileSectorCopyTests
{
    [HostTest("std.file.sector copy rejects count aliases into source or destination before writes")]
    private static void CounterAliases()
    {
        uint* memory = stackalloc uint[150];
        for (int i = 0; i < 150; i++) memory[i] = 0xa5a5a5a5;
        memory[0] = 0;
        Span<byte> aliased = new(memory, 600);
        byte[] other = Enumerable.Repeat((byte)0xcc, 600).ToArray();
        HostCheck.Equal(FileSectorCopyStatus.Overlap, BoundedFileSectorCopy.Copy(other.AsSpan(0, 512), 512, 600, aliased, ref memory[0]));
        HostCheck.Equal(0u, memory[0]);
        for (int i = 1; i < 150; i++) HostCheck.Equal(0xa5a5a5a5u, memory[i]);
        HostCheck.Equal(FileSectorCopyStatus.Overlap, BoundedFileSectorCopy.Copy(new Span<byte>(memory, 512), 512, 600, other, ref memory[0]));
        HostCheck.Equal(0u, memory[0]); HostCheck.True(other.All(b => b == 0xcc));
    }

    [HostTest("std.file.sector copy excludes final padding and advances only completed byte counts")]
    private static void Copy()
    {
        byte[] sector = Enumerable.Range(0, 512).Select(i => (byte)i).ToArray();
        byte[] target = Enumerable.Repeat((byte)0xcc, 520).ToArray(); uint written = 0;
        HostCheck.Equal(FileSectorCopyStatus.Success, BoundedFileSectorCopy.Copy(sector, 512, 513, target, ref written));
        HostCheck.Equal(512u, written);
        HostCheck.True(target.AsSpan(0, 512).SequenceEqual(sector));
        sector[0] = 0xaa;
        HostCheck.Equal(FileSectorCopyStatus.Success, BoundedFileSectorCopy.Copy(sector, 512, 513, target, ref written));
        HostCheck.Equal(513u, written); HostCheck.Equal((byte)0xaa, target[512]);
        HostCheck.True(target.AsSpan(513).ToArray().All(b => b == 0xcc));
        HostCheck.Equal(FileSectorCopyStatus.InvalidOffset, BoundedFileSectorCopy.Copy(sector, 512, 513, target, ref written));
    }
    [HostTest("std.file.sector copy rejects short failed aliased and unsafe buffers without changing state")]
    private static void Reject()
    {
        byte[] sector = Enumerable.Repeat((byte)7, 512).ToArray(), target = Enumerable.Repeat((byte)0xcc, 600).ToArray();
        foreach (uint read in new uint[] { 0, 511, 513, uint.MaxValue })
        {
            uint written = 0;
            HostCheck.Equal(FileSectorCopyStatus.IncompleteRead, BoundedFileSectorCopy.Copy(sector, read, 600, target, ref written));
            HostCheck.Equal(0u, written); HostCheck.True(target.All(b => b == 0xcc));
        }
        foreach (uint length in new uint[] { 0, 601, uint.MaxValue })
        {
            uint written = 0;
            HostCheck.Equal(FileSectorCopyStatus.InvalidLength, BoundedFileSectorCopy.Copy(sector, 512, length, target, ref written));
            HostCheck.Equal(0u, written); HostCheck.True(target.All(b => b == 0xcc));
        }
        uint count = 0;
        HostCheck.Equal(FileSectorCopyStatus.InvalidLength, BoundedFileSectorCopy.Copy(new Span<byte>(null, 512), 512, 1, target, ref count));
        HostCheck.Equal(FileSectorCopyStatus.InvalidLength, BoundedFileSectorCopy.Copy(sector, 512, 1, new Span<byte>(null, 1), ref count));
        Span<byte> wrapping = new((void*)unchecked((nuint)(ulong.MaxValue - 8)), 512);
        HostCheck.Equal(FileSectorCopyStatus.InvalidLength, BoundedFileSectorCopy.Copy(wrapping, 512, 1, target, ref count));
        HostCheck.Equal(FileSectorCopyStatus.InvalidLength, BoundedFileSectorCopy.Copy(sector, 512, 1, wrapping, ref count));
        HostCheck.Equal(FileSectorCopyStatus.Overlap, BoundedFileSectorCopy.Copy(target.AsSpan(0, 512), 512, 1, target, ref count));
        foreach (uint offset in new uint[] { 1, 600, uint.MaxValue })
        {
            count = offset;
            HostCheck.Equal(FileSectorCopyStatus.InvalidOffset, BoundedFileSectorCopy.Copy(sector, 512, 600, target, ref count));
            HostCheck.Equal(offset, count); HostCheck.True(target.All(b => b == 0xcc));
        }
    }
}
