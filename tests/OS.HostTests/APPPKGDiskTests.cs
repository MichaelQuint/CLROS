using System.Buffers.Binary;
using System.Text;
using OS.Std.Hash;
using OS.Storage;

internal static unsafe class APPPKGDiskTests
{
    private static readonly byte[] Path = "bin/hello.app"u8.ToArray();
    internal static byte[] Package()
    {
        byte[] bytes = new byte[132];
        "OSPKGv0\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(36), (ushort)Path.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(40), 96);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(48), 36);
        Path.CopyTo(bytes, 64);
        "OSAPPv0\0"u8.CopyTo(bytes.AsSpan(96));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(106), 32);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(112), 32);
        bytes[128] = 0x31;
        bytes[129] = 0xc0;
        bytes[130] = 0xc3;
        bytes[131] = 0x90;
        return bytes;
    }
    [HostTest("m06.package validates bounded OSPKGv0 metadata against existing host parser")]
    private static void ValidCase()
    {
        var bytes = Package();
        var result = APPPKGDisk.Validate(bytes, Path, Crc32.Compute(bytes));
        var host = OsPackageParser.FindFile(bytes, Path);
        HostCheck.True(result.Succeeded && host.Succeeded && !default(LivePackageResult).Succeeded);
        HostCheck.Equal(1, (int)result.FileCount);
        HostCheck.Equal((uint)host.Entry.ContentOffset, result.ContentOffset);
        HostCheck.Equal((uint)host.Entry.ContentLength, result.ContentLength);
        HostCheck.Equal(Crc32.Compute(bytes), result.PackageCrc32);
        HostCheck.Equal(LivePackageStatus.ChecksumMismatch, APPPKGDisk.Validate(bytes, Path, Crc32.Compute(bytes) ^ 1).Status);
        bytes[131] ^= 1;
        HostCheck.Equal(LivePackageStatus.ChecksumMismatch, APPPKGDisk.Validate(bytes, Path, Crc32.Compute(Package())).Status);
    }
    [HostTest("m06.package rejects hostile header table entry ranges overlap and path metadata")]
    private static void Mutations()
    {
        var cases = new (int Offset, ulong Value, int Width, LivePackageStatus Status)[]
        {
            (0, 0, 1, LivePackageStatus.InvalidHeader), (8, 1, 2, LivePackageStatus.UnsupportedVersion),
            (10, 31, 2, LivePackageStatus.InvalidHeader), (12, 0, 2, LivePackageStatus.InvalidTable),
            (12, 17, 2, LivePackageStatus.InvalidTable), (14, 31, 2, LivePackageStatus.InvalidHeader),
            (16, uint.MaxValue, 4, LivePackageStatus.InvalidTable), (20, 0, 4, LivePackageStatus.InvalidTable),
            (24, 1, 8, LivePackageStatus.InvalidHeader), (32, uint.MaxValue, 4, LivePackageStatus.InvalidEntry),
            (36, 0, 2, LivePackageStatus.InvalidPath), (38, 1, 2, LivePackageStatus.InvalidEntry),
            (40, ulong.MaxValue, 8, LivePackageStatus.InvalidEntry), (48, ulong.MaxValue, 8, LivePackageStatus.InvalidEntry),
            (56, 1, 8, LivePackageStatus.InvalidEntry), (32, 24, 4, LivePackageStatus.Overlap),
            (40, 64, 8, LivePackageStatus.Overlap)
        };
        foreach (var c in cases)
        {
            byte[] bytes = Package();
            if (c.Width == 1)
                bytes[c.Offset] = (byte)c.Value;
            if (c.Width == 2)
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(c.Offset), (ushort)c.Value);
            if (c.Width == 4)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(c.Offset), (uint)c.Value);
            if (c.Width == 8)
                BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(c.Offset), c.Value);
            var result = APPPKGDisk.Validate(bytes, Path, Crc32.Compute(bytes));
            HostCheck.Equal(c.Status, result.Status);
            HostCheck.True(!result.Succeeded && result.ContentOffset == 0 && result.ContentLength == 0);
        }
        foreach (string path in new[] { "", "../app", "bin/../app", "bin/./app", "bin//app", "/app", "C:/app", "bin\\app", "app/" })
            HostCheck.Equal(LivePackageStatus.InvalidPath, APPPKGDisk.Validate(Package(), Encoding.ASCII.GetBytes(path), 0).Status);
        HostCheck.Equal(LivePackageStatus.MissingFile, APPPKGDisk.Validate(Package(), "missing"u8.ToArray(), 0).Status);
        HostCheck.Equal(LivePackageStatus.InvalidLength, APPPKGDisk.Validate(new byte[31], Path, 0).Status);
        HostCheck.Equal(LivePackageStatus.InvalidLength, APPPKGDisk.Validate(new byte[8193], Path, 0).Status);
        HostCheck.Equal(LivePackageStatus.InvalidLength, APPPKGDisk.Validate(new Span<byte>(null, 32), Path, 0).Status);
        HostCheck.Equal(LivePackageStatus.InvalidLength, APPPKGDisk.Validate(new Span<byte>((void*)unchecked((nuint)(ulong.MaxValue - 8)), 32), Path, 0).Status);
    }
    [HostTest("m06.package detects duplicate paths and all pairwise entry intersections")]
    private static void Multiple()
    {
        // Two complete metadata entries with independent path/content regions.
        byte[] bytes = new byte[176];
        Package().AsSpan(0, 32).CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 64);
        void Entry(int pos, uint po, ulong co)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(pos), po);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(pos + 4), 1);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(pos + 8), co);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(pos + 16), 1);
        }
        Entry(32, 96, 128);
        Entry(64, 112, 144);
        bytes[96] = (byte)'a';
        bytes[112] = (byte)'b';
        bytes[128] = (byte)'x';
        bytes[144] = (byte)'y';
        HostCheck.True(APPPKGDisk.Validate(bytes, "a"u8.ToArray(), Crc32.Compute(bytes)).Succeeded);
        bytes[112] = (byte)'a';
        HostCheck.Equal(LivePackageStatus.DuplicatePath, APPPKGDisk.Validate(bytes, "a"u8.ToArray(), 0).Status);
        bytes[112] = (byte)'b';
        foreach (var c in new[] { (64, 96UL, 4, LivePackageStatus.DuplicatePath),
            (64, 128UL, 4, LivePackageStatus.Overlap), (72, 96UL, 8, LivePackageStatus.Overlap),
            (72, 128UL, 8, LivePackageStatus.Overlap) })
        {
            byte[] changed = (byte[])bytes.Clone();
            if (c.Item3 == 4)
                BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(c.Item1), (uint)c.Item2);
            else
                BinaryPrimitives.WriteUInt64LittleEndian(changed.AsSpan(c.Item1), c.Item2);
            HostCheck.Equal(c.Item4, APPPKGDisk.Validate(changed, "a"u8.ToArray(), Crc32.Compute(changed)).Status);
        }
    }
}
