using OS.Storage;
using FileHash = OS.Storage.KnownFileSHAN256EquivalentKnownFixtureHash;

internal static unsafe class KnownFileSHAN256EquivalentKnownFixtureHashTests
{
    private const uint FixtureBytes = 1301, FixtureCrc = 0xe194d61d; // Independently computed with Python zlib.
    private static readonly BlockReadResult Complete = BlockReadResult.Success(1, 512);

    [HostTest("fat.file streams a three sector known CRC and excludes final padding")]
    private static void KnownBytes()
    {
        foreach (byte padding in new byte[] { 0, 0xa5, 0xff })
        {
            LiveFileChecksum state = default;
            HostCheck.True(!state.Succeeded);
            HostCheck.Equal(LiveFileStatus.Success, FileHash.Begin(Entry(), FixtureBytes, FixtureCrc, ref state));
            for (uint offset = 0; offset < 1536; offset += 512)
                HostCheck.Equal(LiveFileStatus.Success, FileHash.Append(Sector(offset, padding), Complete, offset, ref state));
            HostCheck.Equal(FixtureBytes, state.BytesRead);
            HostCheck.Equal(LiveFileStatus.Complete, FileHash.Finish(ref state));
            HostCheck.True(state.Succeeded);
            HostCheck.Equal(LiveFileStatus.InvalidState, FileHash.Append(new byte[512], Complete, FixtureBytes, ref state));
            HostCheck.Equal(LiveFileStatus.InvalidState, FileHash.Finish(ref state));
        }
        var empty = Entry();
        empty.FirstCluster = empty.ByteCount = 0;
        LiveFileChecksum zero = default;
        HostCheck.Equal(LiveFileStatus.Success, FileHash.Begin(empty, 0, 0, ref zero));
        HostCheck.Equal(LiveFileStatus.Complete, FileHash.Finish(ref zero));
    }

    [HostTest("fat.file rejects short failed reads wrong offsets and lengths without consuming state")]
    private static void BadReads()
    {
        LiveFileChecksum state = default;
        FileHash.Begin(Entry(), FixtureBytes, FixtureCrc, ref state);
        var before = state;
        foreach (var read in new[] { default(BlockReadResult), BlockReadResult.Fail(BlockDeviceStatus.IoError),
            BlockReadResult.Success(0, 0), BlockReadResult.Success(1, 511), BlockReadResult.Success(2, 512) })
        {
            HostCheck.Equal(LiveFileStatus.ReadFailed, FileHash.Append(new byte[512], read, 0, ref state));
            Same(before, state);
        }
        foreach (int length in new[] { 0, 511, 513 })
        {
            HostCheck.Equal(LiveFileStatus.InvalidLength, FileHash.Append(new byte[length], Complete, 0, ref state));
            Same(before, state);
        }
        HostCheck.Equal(LiveFileStatus.InvalidLength, FileHash.Append(new Span<byte>(null, 512), Complete, 0, ref state));
        Same(before, state);
        foreach (uint offset in new uint[] { 1, 512, uint.MaxValue })
        {
            HostCheck.Equal(LiveFileStatus.UnexpectedOffset, FileHash.Append(new byte[512], Complete, offset, ref state));
            Same(before, state);
        }
        HostCheck.Equal(LiveFileStatus.Truncated, FileHash.Finish(ref state));
        Same(before, state);
        FileHash.Append(Sector(0, 0), Complete, 0, ref state);
        before = state;
        HostCheck.Equal(LiveFileStatus.UnexpectedOffset, FileHash.Append(Sector(0, 0), Complete, 0, ref state));
        Same(before, state);
        HostCheck.Equal(LiveFileStatus.Truncated, FileHash.Finish(ref state));
        Same(before, state);
    }

    [HostTest("fat.file detects corrupted content or expected CRC and preserves failure state")]
    private static void Mismatch()
    {
        foreach (bool corruptBytes in new[] { false, true })
        {
            LiveFileChecksum state = default;
            FileHash.Begin(Entry(), FixtureBytes, FixtureCrc ^ (corruptBytes ? 0u : 1u), ref state);
            for (uint offset = 0; offset < 1536; offset += 512)
            {
                byte[] bytes = Sector(offset, 0);
                if (corruptBytes && offset == 1024)
                    bytes[276] ^= 1;
                FileHash.Append(bytes, Complete, offset, ref state);
            }
            var before = state;
            HostCheck.Equal(LiveFileStatus.ChecksumMismatch, FileHash.Finish(ref state));
            Same(before, state);
            HostCheck.True(!state.Succeeded);
        }
    }

    [HostTest("fat.file rejects invalid metadata and forged progress state")]
    private static void Admission()
    {
        LiveFileChecksum state = default;
        foreach (byte attributes in new byte[] { 8, 0x10, 0x40, 0x80 })
        {
            var entry = Entry();
            entry.Attributes = attributes;
            HostCheck.Equal(LiveFileStatus.InvalidEntry, FileHash.Begin(entry, FixtureBytes, FixtureCrc, ref state));
            Same(default, state);
        }
        foreach (uint cluster in new uint[] { 0, 1, 0x0ffffff0, uint.MaxValue })
        {
            var entry = Entry();
            entry.FirstCluster = cluster;
            HostCheck.Equal(LiveFileStatus.InvalidEntry, FileHash.Begin(entry, FixtureBytes, FixtureCrc, ref state));
            Same(default, state);
        }
        HostCheck.Equal(LiveFileStatus.InvalidEntry, FileHash.Begin(default, FixtureBytes, FixtureCrc, ref state));
        HostCheck.Equal(LiveFileStatus.InvalidEntry, FileHash.Begin(Entry(), FixtureBytes - 1, FixtureCrc, ref state));
        foreach (var bad in new[] { default(LiveFileChecksum), new LiveFileChecksum { Status = LiveFileStatus.Complete },
            new LiveFileChecksum { Status = LiveFileStatus.Success, ByteCount = 1, BytesRead = 2 },
            new LiveFileChecksum { Status = LiveFileStatus.Success, ByteCount = 512, BytesRead = 1 },
            new LiveFileChecksum { Status = LiveFileStatus.Success, ByteCount = 512, CrcState = 0 } })
        {
            state = bad;
            HostCheck.Equal(LiveFileStatus.InvalidState, FileHash.Append(new byte[512], Complete, 0, ref state));
            Same(bad, state);
            HostCheck.Equal(LiveFileStatus.InvalidState, FileHash.Finish(ref state));
            Same(bad, state);
        }
    }

    private static LiveDirectoryEntry Entry() => new() { Status = LiveDirectoryStatus.Entry, Attributes = 0x20, FirstCluster = 44, ByteCount = FixtureBytes };
    private static byte[] Sector(uint offset, byte padding)
    {
        byte[] bytes = new byte[512];
        Array.Fill(bytes, padding);
        for (uint i = 0; i < 512 && offset + i < FixtureBytes; i++)
            bytes[i] = (byte)(((offset + i) * 73 + 19) ^ ((offset + i) >> 3));
        return bytes;
    }
    private static void Same(LiveFileChecksum expected, LiveFileChecksum actual)
    {
        HostCheck.Equal(expected.Status, actual.Status);
        HostCheck.Equal(expected.ByteCount, actual.ByteCount);
        HostCheck.Equal(expected.BytesRead, actual.BytesRead);
        HostCheck.Equal(expected.CrcState, actual.CrcState);
        HostCheck.Equal(expected.ExpectedCrc, actual.ExpectedCrc);
    }
}
