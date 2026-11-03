using System.Buffers.Binary;
using OS.Storage;

internal static unsafe partial class PrimaryGPTIndependentlyBoundsCRCTests
{
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);
    [HostTest("block.gpt.primary reads one sector and validates independent primary backup CRC vectors")]
    private static void ValidCopiesAndRead()
    {
        var device = new RecordingDevice();
        byte[] output = new byte[512];
        var read = PrimaryGPTIndependentlyBoundsCRC.Read(ref device, 1234, output);
        HostCheck.True(read.Succeeded && device.Lba == 1234 && device.Count == 1 && device.Bytes == 512 && output[0] == 0x5a);
        foreach (bool backup in new[]
        {
            false,
            true
        }

        )
        {
            var(header, entries) = GptTestData.Create(backup);
            byte[] originalHeader = header.ToArray(), originalEntries = entries.ToArray();
            var result = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, GptTestData.Capacity, Complete, backup);
            HostCheck.True(result.Succeeded && result.Capacity == GptTestData.Capacity && result.HeaderSize == 92);
            HostCheck.Equal(backup ? 131071UL : 1UL, result.CurrentLba);
            HostCheck.Equal(backup ? 1UL : 131071UL, result.AlternateLba);
            HostCheck.Equal(backup ? 131039UL : 2UL, result.EntryLba);
            // Independent Python zlib golden values for the explicitly encoded fixture.
            HostCheck.Equal(backup ? 0xbf2475fau : 0x3128b93fu, result.ComputedHeaderCrc);
            HostCheck.Equal(0xb8a9ec08u, result.ArrayCrc);
            HostCheck.Equal(LiveGptStatus.Success, PrimaryGPTIndependentlyBoundsCRC.ValidateArray(result, entries, out uint crc));
            HostCheck.Equal(0xb8a9ec08u, crc);
            HostCheck.True(header.AsSpan().SequenceEqual(originalHeader) && entries.AsSpan().SequenceEqual(originalEntries));
        }
    }

    [HostTest("block.gpt.primary rejects incomplete receipts exact length failures and invalid capacity")]
    private static void Admission()
    {
        var(header, _) = GptTestData.Create();
        HostCheck.True(!default(LiveGptHeaderResult).Succeeded && default(LiveGptStatus) != LiveGptStatus.Success);
        foreach (var read in new[]
        {
            default(BlockReadResult),
            BlockReadResult.Fail(BlockDeviceStatus.IoError),
            BlockReadResult.Success(0, 0),
            BlockReadResult.Success(1, 511),
            BlockReadResult.Success(2, 512)
        }

        )
            HostCheck.Equal(LiveGptStatus.ReadFailed, PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, GptTestData.Capacity, read).Status);
        foreach (int length in new[]
        {
            0,
            511,
            513
        }

        )
            HostCheck.Equal(LiveGptStatus.InvalidLength, PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(new byte[length], GptTestData.Capacity, Complete).Status);
        HostCheck.Equal(LiveGptStatus.InvalidLength, PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(new Span<byte>(null, 512), GptTestData.Capacity, Complete).Status);
        foreach (ulong capacity in new ulong[]
        {
            0,
            67,
            ulong.MaxValue / 512 + 1,
            ulong.MaxValue
        }

        )
            HostCheck.Equal(LiveGptStatus.InvalidCapacity, PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, capacity, Complete).Status);
    }

    [HostTest("block.gpt.primary rejects malformed header fields with independently refreshed CRC")]
    private static void HeaderFields()
    {
        foreach ((Action<byte[]> change, LiveGptStatus expected)in new (Action<byte[]>, LiveGptStatus)[]
        {
            (h => h[0] ^= 1, LiveGptStatus.BadSignature),
            (h => U32(h, 8, 0x10001), LiveGptStatus.UnsupportedRevision),
            (h => U32(h, 20, 1), LiveGptStatus.ReservedNonZero),
            (h => h[511] = 1, LiveGptStatus.ReservedNonZero),
            (h => U64(h, 24, 2), LiveGptStatus.InvalidHeaderPosition),
            (h => U64(h, 32, 1), LiveGptStatus.InvalidHeaderPosition),
            (h => U64(h, 40, 33), LiveGptStatus.InvalidUsableRange),
            (h => U64(h, 48, GptTestData.Capacity - 33), LiveGptStatus.InvalidUsableRange),
            (h => U64(h, 40, GptTestData.Capacity), LiveGptStatus.InvalidUsableRange),
            (h =>
            {
                U64(h, 56, 0);
                U64(h, 64, 0);
            }, LiveGptStatus.InvalidDiskGuid),
            (h => U32(h, 80, 127), LiveGptStatus.UnsupportedArrayLayout),
            (h => U32(h, 80, uint.MaxValue), LiveGptStatus.UnsupportedArrayLayout),
            (h => U32(h, 84, 256), LiveGptStatus.UnsupportedArrayLayout),
            (h => U64(h, 72, 1), LiveGptStatus.InvalidArrayPlacement),
            (h => U64(h, 72, 3), LiveGptStatus.InvalidArrayPlacement),
            (h => U64(h, 72, ulong.MaxValue), LiveGptStatus.InvalidArrayPlacement)
        }

        )
        {
            var(header, _) = GptTestData.Create();
            change(header);
            GptTestData.RehashHeader(header);
            byte[] before = header.ToArray();
            HostCheck.Equal(expected, PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, GptTestData.Capacity, Complete).Status);
            HostCheck.True(header.AsSpan().SequenceEqual(before));
        }

        foreach (uint size in new uint[]
        {
            0,
            91,
            513,
            uint.MaxValue
        }

        )
        {
            var(header, _) = GptTestData.Create();
            U32(header, 12, size);
            HostCheck.Equal(LiveGptStatus.InvalidHeaderSize, PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, GptTestData.Capacity, Complete).Status);
        }
    }

    [HostTest("block.gpt.primary accepts bounded header extensions capacity edges and nonadjacent arrays")]
    private static void PlacementBoundaries()
    {
        foreach (bool backup in new[]
        {
            false,
            true
        }

        )
        {
            var(header, _) = GptTestData.Create(backup);
            HostCheck.Equal(LiveGptStatus.InvalidHeaderPosition, PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, GptTestData.Capacity, Complete, !backup).Status);
            U64(header, 40, 35);
            U64(header, 48, GptTestData.Capacity - 36);
            U64(header, 72, backup ? GptTestData.Capacity - 34 : 3UL);
            GptTestData.RehashHeader(header);
            HostCheck.True(PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, GptTestData.Capacity, Complete, backup).Succeeded);
            U64(header, 72, backup ? GptTestData.Capacity - 32 : 4UL);
            GptTestData.RehashHeader(header);
            HostCheck.Equal(LiveGptStatus.InvalidArrayPlacement, PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, GptTestData.Capacity, Complete, backup).Status);
        }

        foreach (ulong capacity in new ulong[]
        {
            68,
            ulong.MaxValue / 512
        }

        )
        {
            var(header, _) = GptTestData.Create();
            U64(header, 32, capacity - 1);
            U64(header, 48, capacity - 34);
            GptTestData.RehashHeader(header);
            HostCheck.True(PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, capacity, Complete).Succeeded);
        }

        foreach (uint size in new uint[]
        {
            92,
            96,
            512
        }

        )
        {
            var(header, _) = GptTestData.Create();
            U32(header, 12, size);
            if (size > 92)
                header[size - 1] = 0xa5;
            GptTestData.RehashHeader(header);
            HostCheck.True(PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, GptTestData.Capacity, Complete).Succeeded);
        }
    }

    [HostTest("block.gpt.primary detects header and full array CRC failures without changing inputs")]
    private static void CrcFailures()
    {
        foreach (int offset in new[]
        {
            16,
            56
        }

        )
        {
            var(header, _) = GptTestData.Create();
            header[offset] ^= 1;
            byte[] before = header.ToArray();
            var result = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, GptTestData.Capacity, Complete);
            HostCheck.Equal(LiveGptStatus.HeaderCrcMismatch, result.Status);
            HostCheck.True(result.ExpectedHeaderCrc != result.ComputedHeaderCrc && header.AsSpan().SequenceEqual(before));
        }

        var(good, entries) = GptTestData.Create();
        var h = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(good, GptTestData.Capacity, Complete);
        entries[^1] = 1;
        byte[] original = entries.ToArray();
        HostCheck.Equal(LiveGptStatus.ArrayCrcMismatch, PrimaryGPTIndependentlyBoundsCRC.ValidateArray(h, entries, out uint actual));
        HostCheck.True(actual != h.ArrayCrc && entries.AsSpan().SequenceEqual(original));
    }

    [HostTest("block.gpt.primary validates every fixed entry GUID extent and unused byte")]
    private static void Entries()
    {
        foreach ((Action<byte[]> change, LiveGptStatus expected)in new (Action<byte[]>, LiveGptStatus)[]
        {
            (a => a[255] = 1, LiveGptStatus.InvalidUnusedEntry),
            (a => a.AsSpan(16, 16).Clear(), LiveGptStatus.InvalidPartitionGuid),
            (a => U64(a, 32, 33), LiveGptStatus.InvalidPartitionExtent),
            (a => U64(a, 40, GptTestData.Capacity - 33), LiveGptStatus.InvalidPartitionExtent),
            (a => U64(a, 32, ulong.MaxValue), LiveGptStatus.InvalidPartitionExtent),
            (a =>
            {
                a.AsSpan(0, 128).CopyTo(a.AsSpan(128));
                U64(a, 160, 34);
                U64(a, 168, 100);
            }, LiveGptStatus.DuplicatePartitionGuid),
            (a =>
            {
                a.AsSpan(0, 128).CopyTo(a.AsSpan(128));
                U64(a, 144, 99);
            }, LiveGptStatus.OverlappingPartitions)
        }

        )
        {
            var(header, entries) = GptTestData.Create();
            change(entries);
            GptTestData.RehashArray(header, entries);
            byte[] before = entries.ToArray();
            var h = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, GptTestData.Capacity, Complete);
            HostCheck.Equal(expected, PrimaryGPTIndependentlyBoundsCRC.ValidateArray(h, entries, out uint crc));
            HostCheck.True(crc == h.ArrayCrc && entries.AsSpan().SequenceEqual(before));
        }

        var(fullHeader, full) = GptTestData.Create();
        for (int i = 0; i < 128; i++)
        {
            if (i != 0)
                full.AsSpan(0, 128).CopyTo(full.AsSpan(i * 128));
            U64(full, i * 128 + 16, (ulong)i + 1);
            U64(full, i * 128 + 32, (ulong)i + 2048);
            U64(full, i * 128 + 40, (ulong)i + 2048);
            full[i * 128 + 48] = 0xff;
            full[i * 128 + 127] = 0xa5; // Opaque attributes/name bytes.
        }

        GptTestData.RehashArray(fullHeader, full);
        var all = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(fullHeader, GptTestData.Capacity, Complete);
        HostCheck.Equal(LiveGptStatus.Success, PrimaryGPTIndependentlyBoundsCRC.ValidateArray(all, full, out _));
        full.AsSpan().Clear();
        GptTestData.RehashArray(fullHeader, full);
        var empty = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(fullHeader, GptTestData.Capacity, Complete);
        HostCheck.Equal(LiveGptStatus.Success, PrimaryGPTIndependentlyBoundsCRC.ValidateArray(empty, full, out uint emptyCrc));
        HostCheck.Equal(0xab54d286u, emptyCrc);
    }

    [HostTest("block.gpt.primary rechecks mutable header bounds before validating the exact array")]
    private static void ArrayAdmission()
    {
        var(header, entries) = GptTestData.Create();
        var h = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(header, GptTestData.Capacity, Complete);
        foreach (int length in new[]
        {
            0,
            16383,
            16385
        }

        )
        {
            HostCheck.Equal(LiveGptStatus.InvalidLength, PrimaryGPTIndependentlyBoundsCRC.ValidateArray(h, new byte[length], out uint crc));
            HostCheck.Equal(0u, crc);
        }

        HostCheck.Equal(LiveGptStatus.InvalidLength, PrimaryGPTIndependentlyBoundsCRC.ValidateArray(h, new Span<byte>(null, 16384), out _));
        var mutations = new LiveGptHeaderResult[9];
        Array.Fill(mutations, h);
        mutations[0] = default;
        mutations[1].Capacity = ulong.MaxValue;
        mutations[2].HeaderSize = 513;
        mutations[3].CurrentLba = 2;
        mutations[4].FirstUsableLba = 0;
        mutations[5].LastUsableLba = ulong.MaxValue;
        mutations[6].DiskGuidLow = 0;
        mutations[6].DiskGuidHigh = 0;
        mutations[7].EntryLba = ulong.MaxValue;
        mutations[8].ComputedHeaderCrc ^= 1;
        foreach (var forged in mutations)
        {
            HostCheck.Equal(LiveGptStatus.InvalidHeader, PrimaryGPTIndependentlyBoundsCRC.ValidateArray(forged, entries, out uint crc));
            HostCheck.Equal(0u, crc);
        }
    }

    private static void U32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void U64(byte[] bytes, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);
}
