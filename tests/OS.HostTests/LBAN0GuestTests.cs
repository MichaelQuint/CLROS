using System.Buffers.Binary;
using OS.Storage;

internal static unsafe class LBAN0GuestTests
{
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);

    [HostTest("block.lba0 reads through the live contract and validates protective MBR capacity")]
    private static void ReadAndValidate()
    {
        using var f = new BlockReadFixture(); f.State->ExpectedSector = 0; f.State->ProtectiveMbr = true;
        var device = f.CreateLiveDevice(); var info = LBAN0Guest.Info(ref device);
        var bytes = new Span<byte>(f.Output, 512);
        var result = LBAN0Guest.Validate(bytes, info.BlockCount, LBAN0Guest.Read(ref device, bytes));
        HostCheck.True(result.Succeeded && result.FirstLba == 1 && result.ProtectedSectors == uint.MaxValue);
        f.Check();
        foreach (ulong capacity in new ulong[] { 2, 131072, uint.MaxValue, (ulong)uint.MaxValue + 1, (ulong)uint.MaxValue + 2, ulong.MaxValue / 512 })
        {
            var mbr = Mbr(capacity); var original = mbr.ToArray();
            HostCheck.True(LBAN0Guest.Validate(mbr, capacity, Complete).Succeeded);
            HostCheck.True(mbr.AsSpan().SequenceEqual(original));
        }
    }

    [HostTest("block.lba0 rejects truncated invalid hybrid and uncompleted reads without mutation")]
    private static void Rejections()
    {
        const ulong capacity = 131072;
        foreach (int size in new[] { 0, 511, 513 }) HostCheck.Equal(Lba0Status.InvalidLength, LBAN0Guest.Validate(new byte[size], capacity, Complete).Status);
        foreach (ulong size in new ulong[] { 0, 1, ulong.MaxValue / 512 + 1 }) HostCheck.Equal(Lba0Status.InvalidCapacity, LBAN0Guest.Validate(Mbr(capacity), size, Complete).Status);
        foreach (var read in new[] { default(BlockReadResult), BlockReadResult.Fail(BlockDeviceStatus.IoError), BlockReadResult.Success(0, 0), BlockReadResult.Success(1, 511), BlockReadResult.Success(2, 512) })
            HostCheck.Equal(Lba0Status.ReadFailed, LBAN0Guest.Validate(Mbr(capacity), capacity, read).Status);
        foreach (int offset in new[] { 510, 511, 446, 450, 454, 455, 456, 457, 458, 459, 460, 461 })
        {
            var mbr = Mbr(capacity); mbr[offset] ^= 1; var original = mbr.ToArray();
            HostCheck.Equal(offset >= 510 ? Lba0Status.BadSignature : Lba0Status.BadProtectiveEntry, LBAN0Guest.Validate(mbr, capacity, Complete).Status);
            HostCheck.True(mbr.AsSpan().SequenceEqual(original));
        }
        for (int offset = 462; offset < 510; offset++)
        {
            var mbr = Mbr(capacity); mbr[offset] = 1;
            HostCheck.Equal(Lba0Status.HybridEntry, LBAN0Guest.Validate(mbr, capacity, Complete).Status);
        }
    }

    private static byte[] Mbr(ulong capacity)
    {
        var bytes = new byte[512]; bytes[450] = 0xee; bytes[510] = 0x55; bytes[511] = 0xaa;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(454), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(458), (uint)Math.Min(capacity - 1, uint.MaxValue));
        return bytes;
    }
}
