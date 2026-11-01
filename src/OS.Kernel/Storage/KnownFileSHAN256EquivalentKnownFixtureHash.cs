using System;
using OS.Std.Hash;

namespace OS.Storage;

/// <summary>Streaming repository CRC32 of file bytes, excluding final-sector
/// padding. The caller owns validated cluster order and supplies exact read
/// receipts. Metadata is numeric state, not device authority. Failure leaves
/// state unchanged; CRC32 detects corruption and is not authentication.</summary>
public static unsafe class KnownFileSHAN256EquivalentKnownFixtureHash
{
    public const uint FixtureBytes = 1301, FixtureCrc32 = 0xe194d61d;

    public static LiveFileStatus Begin(LiveDirectoryEntry entry, uint expectedBytes,
        uint expectedCrc, ref LiveFileChecksum state)
    {
        if (!entry.Succeeded || (entry.Attributes & 0xd8) != 0 || entry.ByteCount != expectedBytes ||
            (entry.ByteCount == 0 ? entry.FirstCluster != 0 : entry.FirstCluster < 2 || entry.FirstCluster >= 0x0ffffff0))
            return LiveFileStatus.InvalidEntry;
        state.Status = LiveFileStatus.Success; state.ByteCount = expectedBytes;
        state.BytesRead = 0; state.CrcState = Crc32.InitialState; state.ExpectedCrc = expectedCrc;
        return LiveFileStatus.Success;
    }

    public static LiveFileStatus Append(Span<byte> sector, BlockReadResult read,
        uint fileOffset, ref LiveFileChecksum state)
    {
        if (!Valid(state) || state.BytesRead == state.ByteCount) return LiveFileStatus.InvalidState;
        if (!read.Succeeded || read.BlocksRead != 1 || read.BytesRead != 512) return LiveFileStatus.ReadFailed;
        if (sector.Length != 512) return LiveFileStatus.InvalidLength;
        if (fileOffset != state.BytesRead) return LiveFileStatus.UnexpectedOffset;
        fixed (byte* bytes = sector)
        {
            if (bytes == null || (ulong)bytes > ulong.MaxValue - 512) return LiveFileStatus.InvalidLength;
            uint count = state.ByteCount - state.BytesRead;
            if (count > 512) count = 512;
            uint crc = state.CrcState;
            for (uint i = 0; i < count; i++) crc = Crc32.UpdateByte(crc, bytes[i]);
            state.CrcState = crc; state.BytesRead += count;
            return LiveFileStatus.Success;
        }
    }

    public static LiveFileStatus Finish(ref LiveFileChecksum state)
    {
        if (!Valid(state)) return LiveFileStatus.InvalidState;
        if (state.BytesRead != state.ByteCount) return LiveFileStatus.Truncated;
        if (Crc32.Finish(state.CrcState) != state.ExpectedCrc) return LiveFileStatus.ChecksumMismatch;
        state.Status = LiveFileStatus.Complete;
        return LiveFileStatus.Complete;
    }

    private static bool Valid(LiveFileChecksum state) => state.Status == LiveFileStatus.Success &&
        state.BytesRead <= state.ByteCount && (state.BytesRead == state.ByteCount || state.BytesRead % 512 == 0) &&
        (state.BytesRead != 0 || state.CrcState == Crc32.InitialState);
}
