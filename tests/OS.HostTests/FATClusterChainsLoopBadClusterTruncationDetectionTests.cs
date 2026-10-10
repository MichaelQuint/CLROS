using System.Buffers.Binary;
using OS.Storage;
using Fat = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;

internal static unsafe class FATClusterChainsLoopBadClusterTruncationDetectionTests
{
    private const ulong Capacity = 100000, First = 2048, Last = 72047;
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);

    [HostTest("fat.live independently encoded BPB and cluster geometry preserve inputs")]
    private static void Geometry()
    {
        byte[] bpb = Bpb(), before = bpb.ToArray();
        LiveFat32Volume v = default;
        HostCheck.True(!v.Succeeded && default(LiveFat32Status) != LiveFat32Status.Success);
        HostCheck.Equal(LiveFat32Status.Success, Fat.ParseBpb(bpb, Capacity, First, Last, Complete, ref v));
        HostCheck.True(v.Succeeded && v.FirstFatLba == 2080 && v.FirstDataLba == 3280 && v.ClusterCount == 68768 && v.RootCluster == 2);
        ulong lba = 99;
        HostCheck.Equal(LiveFat32Status.Success, Fat.ClusterFirstLba(v, 2, ref lba)); HostCheck.Equal(3280UL, lba);
        HostCheck.Equal(LiveFat32Status.Success, Fat.ClusterFirstLba(v, 68769, ref lba)); HostCheck.Equal(Last, lba);
        foreach (uint cluster in new uint[] { 0, 1, 68770, uint.MaxValue })
        {
            HostCheck.Equal(LiveFat32Status.InvalidCluster, Fat.ClusterFirstLba(v, cluster, ref lba)); HostCheck.Equal(Last, lba);
        }
        HostCheck.True(bpb.AsSpan().SequenceEqual(before));
        // Numeric edge vectors need only a BPB, not multi-gigabyte disk buffers.
        foreach ((uint clusters, byte spc) in new[] { (65525u, (byte)128), (Fat.MaximumClusters, (byte)1) })
        {
            uint fat = (clusters + 2 + 127) / 128, total = 32 + 2 * fat + clusters * spc;
            bpb = Bpb(); bpb[13] = spc; U32(bpb, 36, fat); U32(bpb, 32, total);
            ulong last = First + total - 1;
            HostCheck.Equal(LiveFat32Status.Success, Fat.ParseBpb(bpb, last + 1, First, last, Complete, ref v));
            HostCheck.Equal(clusters, v.ClusterCount);
            HostCheck.Equal(LiveFat32Status.Success, Fat.ClusterFirstLba(v, clusters + 1, ref lba));
            HostCheck.Equal(last - spc + 1, lba);
        }
    }

    [HostTest("fat.live BPB admission rejects short receipts malformed geometry and overflow atomically")]
    private static void BpbRejections()
    {
        var sentinel = Volume(); byte[] valid = Bpb();
        foreach (var read in new[] { default(BlockReadResult), BlockReadResult.Fail(BlockDeviceStatus.IoError),
            BlockReadResult.Success(0, 512), BlockReadResult.Success(1, 511), BlockReadResult.Success(2, 512) })
        {
            var output = sentinel;
            HostCheck.Equal(LiveFat32Status.ReadFailed, Fat.ParseBpb(valid, Capacity, First, Last, read, ref output));
            HostCheck.True(output.Equals(sentinel));
        }
        foreach (int length in new[] { 0, 511, 513 })
        {
            var output = sentinel;
            HostCheck.Equal(LiveFat32Status.InvalidLength, Fat.ParseBpb(new byte[length], Capacity, First, Last, Complete, ref output));
            HostCheck.True(output.Equals(sentinel));
        }
        foreach (ulong pointer in new ulong[] { 0, ulong.MaxValue - 255 })
        {
            var output = sentinel;
            HostCheck.Equal(LiveFat32Status.InvalidLength, Fat.ParseBpb(new Span<byte>((void*)pointer, 512), Capacity, First, Last, Complete, ref output));
            HostCheck.True(output.Equals(sentinel));
        }
        foreach ((ulong cap, ulong first, ulong last) in new[] { (0UL, First, Last), (ulong.MaxValue, First, Last),
            (Capacity, 0UL, Last), (Capacity, Last, First), (Capacity, First, Capacity), (Capacity, First, Last - 1) })
        {
            var output = sentinel;
            HostCheck.True(Fat.ParseBpb(valid, cap, first, last, Complete, ref output) != LiveFat32Status.Success && output.Equals(sentinel));
        }
        foreach (Action<byte[]> change in new Action<byte[]>[]
        {
            b => b[510] = 0, b => U16(b, 11, 4096), b => b[13] = 0, b => b[13] = 3, b => b[13] = 129,
            b => b[13] = 2, b => U16(b, 14, 0), b => b[16] = 1, b => U16(b, 17, 1),
            b => U16(b, 19, 1), b => U16(b, 22, 1), b => U32(b, 32, 0), b => U32(b, 32, uint.MaxValue),
            b => U32(b, 36, 0), b => U32(b, 36, uint.MaxValue), b => U32(b, 36, 1),
            b => U16(b, 40, 0x80), b => U16(b, 40, 1), b => U16(b, 42, 1), b => b[52] = 1,
            b => U32(b, 44, 1), b => U32(b, 44, 68770)
        })
        {
            byte[] bpb = Bpb(); change(bpb); byte[] before = bpb.ToArray(); var output = sentinel;
            HostCheck.True(Fat.ParseBpb(bpb, Capacity, First, Last, Complete, ref output) != LiveFat32Status.Success);
            HostCheck.True(output.Equals(sentinel) && bpb.AsSpan().SequenceEqual(before));
        }
    }

    [HostTest("fat.live FAT entries bind exact primary sector classify terminal values and ignore high bits")]
    private static void Entries()
    {
        var v = Volume();
        foreach (uint cluster in new uint[] { 127, 128, 68769 })
        {
            byte[] sector = new byte[512]; U32(sector, (int)(cluster % 128) * 4, 0xf0000003);
            uint next = 99;
            HostCheck.Equal(LiveFat32Status.Success, Fat.ReadFatEntry(v, cluster, v.FirstFatLba + cluster / 128, sector, Complete, ref next));
            HostCheck.Equal(3u, next);
        }
        foreach ((uint raw, LiveFat32Status status) in new[]
        {
            (0u, LiveFat32Status.FreeCluster), (1u, LiveFat32Status.ReservedCluster),
            (0x0ffffff0u, LiveFat32Status.ReservedCluster), (0x0ffffff6u, LiveFat32Status.ReservedCluster),
            (0x0ffffff7u, LiveFat32Status.BadCluster), (0x0ffffff8u, LiveFat32Status.EndOfChain),
            (uint.MaxValue, LiveFat32Status.EndOfChain), (68770u, LiveFat32Status.InvalidCluster)
        })
        {
            byte[] sector = new byte[512]; U32(sector, 8, raw); byte[] before = sector.ToArray(); uint next = 99;
            HostCheck.Equal(status, Fat.ReadFatEntry(v, 2, v.FirstFatLba, sector, Complete, ref next));
            HostCheck.True(next == 99 && sector.AsSpan().SequenceEqual(before));
        }
        foreach (ulong lba in new[] { v.FirstFatLba - 1, v.FirstFatLba + 1, v.FirstFatLba + v.FatSectors })
        {
            uint next = 99;
            HostCheck.Equal(LiveFat32Status.UnexpectedFatSector, Fat.ReadFatEntry(v, 2, lba, new byte[512], Complete, ref next));
            HostCheck.Equal(99u, next);
        }
        uint unchanged = 99;
        HostCheck.Equal(LiveFat32Status.ReadFailed, Fat.ReadFatEntry(v, 2, v.FirstFatLba, new byte[512], BlockReadResult.Success(1, 511), ref unchanged));
        HostCheck.Equal(LiveFat32Status.InvalidLength, Fat.ReadFatEntry(v, 2, v.FirstFatLba, new byte[511], Complete, ref unchanged));
        HostCheck.Equal(LiveFat32Status.InvalidLength, Fat.ReadFatEntry(v, 2, v.FirstFatLba, new Span<byte>(null, 512), Complete, ref unchanged));
        HostCheck.Equal(99u, unchanged);
    }

    [HostTest("fat.live visited scratch detects loops bounds exhaustion and aliased count without partial writes")]
    private static void Visits()
    {
        var v = Volume(); uint[] history = [88, 89, 90]; uint count = 0;
        foreach (uint cluster in new uint[] { 2, 5, 9 }) HostCheck.Equal(LiveFat32Status.Success, Fat.Visit(v, cluster, history, ref count));
        HostCheck.True(count == 3 && history.AsSpan().SequenceEqual(new uint[] { 2, 5, 9 }));
        uint[] before = history.ToArray();
        HostCheck.Equal(LiveFat32Status.Loop, Fat.Visit(v, 5, history, ref count));
        HostCheck.Equal(LiveFat32Status.ScratchLimit, Fat.Visit(v, 6, history, ref count));
        HostCheck.Equal(LiveFat32Status.InvalidCluster, Fat.Visit(v, 68770, history, ref count));
        HostCheck.True(count == 3 && history.AsSpan().SequenceEqual(before));
        count = 4; HostCheck.Equal(LiveFat32Status.InvalidHistory, Fat.Visit(v, 6, history, ref count)); HostCheck.Equal(4u, count);
        count = 0;
        HostCheck.Equal(LiveFat32Status.ScratchLimit, Fat.Visit(v, 6, default, ref count));
        HostCheck.Equal(LiveFat32Status.ScratchLimit, Fat.Visit(v, 6, new uint[4097], ref count));
        HostCheck.Equal(LiveFat32Status.InvalidLength, Fat.Visit(v, 6, new Span<uint>(null, 3), ref count));
        HostCheck.Equal(0u, count);
        uint* alias = stackalloc uint[3]; alias[0] = 1; alias[1] = 88; alias[2] = 89;
        HostCheck.Equal(LiveFat32Status.InvalidHistory, Fat.Visit(v, 6, new Span<uint>(alias, 3), ref alias[0]));
        HostCheck.True(alias[0] == 1 && alias[1] == 88 && alias[2] == 89);
        history[0] = 0; count = 1;
        HostCheck.Equal(LiveFat32Status.InvalidHistory, Fat.Visit(v, 6, history, ref count));
        HostCheck.True(count == 1 && history[0] == 0 && history[1] == 5);
    }

    [HostTest("fat.live mutable volume revalidation and completed length checks fail closed")]
    private static void VolumeAndLength()
    {
        foreach (int field in new[] { 0, 1, 2, 3, 4, 5, 6 })
        {
            var v = Volume();
            switch (field)
            {
                case 0: v = default; break; case 1: v.FirstFatLba++; break; case 2: v.FirstDataLba++; break;
                case 3: v.ClusterCount++; break; case 4: v.TotalSectors = ulong.MaxValue; break;
                case 5: v.Capacity = ulong.MaxValue; break; case 6: v.SectorsPerCluster = uint.MaxValue; break;
            }
            ulong lba = 99; uint next = 99, count = 0; uint[] history = [77];
            HostCheck.Equal(LiveFat32Status.InvalidVolume, Fat.ClusterFirstLba(v, 2, ref lba));
            HostCheck.Equal(LiveFat32Status.InvalidVolume, Fat.ReadFatEntry(v, 2, 2080, new byte[512], Complete, ref next));
            HostCheck.Equal(LiveFat32Status.InvalidVolume, Fat.Visit(v, 2, history, ref count));
            HostCheck.True(lba == 99 && next == 99 && count == 0 && history[0] == 77);
        }
        HostCheck.Equal(LiveFat32Status.Success, Fat.CheckLength(3, 3));
        HostCheck.Equal(LiveFat32Status.Truncated, Fat.CheckLength(2, 3));
        HostCheck.Equal(LiveFat32Status.Truncated, Fat.CheckLength(0, 1));
        HostCheck.Equal(LiveFat32Status.Overlong, Fat.CheckLength(4, 3));
        HostCheck.Equal(LiveFat32Status.InvalidHistory, Fat.CheckLength(0, 0));
        HostCheck.Equal(LiveFat32Status.ScratchLimit, Fat.CheckLength(1, 4097));
        HostCheck.Equal(LiveFat32Status.ScratchLimit, Fat.CheckLength(4097, 1));
    }

    private static LiveFat32Volume Volume()
    {
        LiveFat32Volume volume = default;
        HostCheck.Equal(LiveFat32Status.Success, Fat.ParseBpb(Bpb(), Capacity, First, Last, Complete, ref volume));
        return volume;
    }
    private static byte[] Bpb()
    {
        byte[] bytes = new byte[512];
        U16(bytes, 11, 512); bytes[13] = 1; U16(bytes, 14, 32); bytes[16] = 2; bytes[21] = 0xf8;
        U32(bytes, 28, (uint)First); U32(bytes, 32, 70000); U32(bytes, 36, 600); U32(bytes, 44, 2);
        U16(bytes, 48, 1); U16(bytes, 50, 6); bytes[510] = 0x55; bytes[511] = 0xaa;
        return bytes;
    }
    private static void U16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void U32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
