using System;
using OS.Std.Binary;

namespace OS.Storage;

/// <summary>One physical directory entry at a time, including VFAT sequences
/// spanning sectors/clusters. Caller supplies stable, separate entry and 260-word
/// name scratch. Only Entry publishes metadata/name length. Failures clear the
/// sequence; scratch is unspecified until the next successful long-name entry.
/// This parser performs no I/O, path lookup, OEM conversion or case folding.</summary>
public static unsafe class MultiClusterDirectoryVFATLongNameSupportSeparate
{
    public const int EntryBytes = 32, NameScratchWords = 260;

    public static LiveDirectoryStatus Consume(Span<byte> entry, ref LiveVfatState state,
        Span<ushort> name, out LiveDirectoryEntry result)
    {
        result = default;
        if (entry.Length != EntryBytes || name.Length != NameScratchWords)
            return Fail(ref state, LiveDirectoryStatus.InvalidLength);
        fixed (byte* bytes = entry)
        fixed (ushort* words = name)
        {
            if (bytes == null || words == null || (ulong)bytes > ulong.MaxValue - EntryBytes ||
                (ulong)words > ulong.MaxValue - NameScratchWords * 2UL ||
                ((ulong)bytes < (ulong)words + NameScratchWords * 2UL && (ulong)words < (ulong)bytes + EntryBytes))
                return Fail(ref state, LiveDirectoryStatus.InvalidLength);
            if (state.Slots > 20 || (state.Slots == 0 ? state.Remaining != 0 || state.Checksum != 0 : state.Remaining >= state.Slots))
                return Fail(ref state, LiveDirectoryStatus.InvalidState);
            if (bytes[0] == 0 || bytes[0] == 0xe5)
            {
                if (state.Slots != 0)
                    return Fail(ref state, LiveDirectoryStatus.InvalidSequence);
                return bytes[0] == 0 ? LiveDirectoryStatus.End : LiveDirectoryStatus.Skipped;
            }
            if (bytes[11] == 0x0f)
            {
                uint ordinal = (uint)(bytes[0] & 0x1f);
                bool last = (bytes[0] & 0x40) != 0;
                if ((bytes[0] & 0xa0) != 0 || ordinal == 0 || ordinal > 20 || bytes[12] != 0 ||
                    EndianPrimitives.ReadUInt16LittleEndian(bytes + 26) != 0 ||
                    (last ? state.Slots != 0 : state.Slots == 0 || ordinal != state.Remaining || bytes[13] != state.Checksum))
                    return Fail(ref state, LiveDirectoryStatus.InvalidSequence);
                if (last)
                {
                    state.Slots = (byte)ordinal;
                    state.Checksum = bytes[13];
                }
                for (uint i = 0; i < 13; i++)
                {
                    uint offset = i < 5 ? 1 + i * 2 : i < 11 ? 14 + (i - 5) * 2 : 28 + (i - 11) * 2;
                    words[(ordinal - 1) * 13 + i] = EndianPrimitives.ReadUInt16LittleEndian(bytes + offset);
                }
                state.Remaining = (byte)(ordinal - 1);
                return LiveDirectoryStatus.LongName;
            }
            if (state.Slots != 0 && state.Remaining != 0)
                return Fail(ref state, LiveDirectoryStatus.InvalidSequence);
            byte attributes = bytes[11];
            if ((attributes & 0xc0) != 0 || (attributes & 0x18) == 0x18)
                return Fail(ref state, LiveDirectoryStatus.InvalidEntry);
            if ((attributes & 8) != 0)
            {
                if (state.Slots != 0)
                    return Fail(ref state, LiveDirectoryStatus.InvalidSequence);
                return LiveDirectoryStatus.Skipped;
            }
            uint cluster = ((uint)EndianPrimitives.ReadUInt16LittleEndian(bytes + 20) << 16) |
                EndianPrimitives.ReadUInt16LittleEndian(bytes + 26);
            uint size = EndianPrimitives.ReadUInt32LittleEndian(bytes + 28);
            bool directory = (attributes & 0x10) != 0;
            bool dot = bytes[0] == '.';
            if (dot)
            {
                uint start = bytes[1] == '.' ? 2u : 1u;
                for (uint i = start; i < 11; i++)
                    if (bytes[i] != ' ')
                        return Fail(ref state, LiveDirectoryStatus.InvalidEntry);
                if (!directory || size != 0 || state.Slots != 0 || cluster == 1 ||
                    (start == 1 && cluster == 0) || cluster >= 0x0ffffff0)
                    return Fail(ref state, LiveDirectoryStatus.InvalidEntry);
                return LiveDirectoryStatus.Skipped;
            }
            if (!ShortNameValid(bytes) || cluster == 1 || cluster >= 0x0ffffff0 ||
                ((directory || size != 0) && cluster < 2) || (directory && size != 0))
                return Fail(ref state, LiveDirectoryStatus.InvalidEntry);
            ushort length = 0;
            if (state.Slots != 0)
            {
                byte checksum = 0;
                for (int i = 0; i < 11; i++)
                    checksum = (byte)(((checksum & 1) << 7) + (checksum >> 1) + bytes[i]);
                if (checksum != state.Checksum)
                    return Fail(ref state, LiveDirectoryStatus.InvalidChecksum);
                uint capacity = (uint)state.Slots * 13;
                bool terminated = false, highSurrogate = false;
                for (uint i = 0; i < capacity; i++)
                {
                    ushort c = words[i];
                    if (terminated)
                    {
                        if (c != 0xffff)
                            return Fail(ref state, LiveDirectoryStatus.InvalidName);
                        continue;
                    }
                    if (c == 0)
                    {
                        terminated = true;
                        continue;
                    }
                    if (c == 0xffff || Forbidden(c) || length == 255 ||
                        (highSurrogate ? c < 0xdc00 || c > 0xdfff : c >= 0xdc00 && c <= 0xdfff))
                        return Fail(ref state, LiveDirectoryStatus.InvalidName);
                    highSurrogate = c >= 0xd800 && c <= 0xdbff;
                    length++;
                }
                // Minimal slot count and no traversal/normalization aliases.
                if (length == 0 || highSurrogate || length <= (state.Slots - 1) * 13 ||
                    words[length - 1] == '.' || words[length - 1] == ' ')
                    return Fail(ref state, LiveDirectoryStatus.InvalidName);
            }
            result.Status = LiveDirectoryStatus.Entry;
            result.ShortNameLow = EndianPrimitives.ReadUInt64LittleEndian(bytes);
            result.ShortNameHigh = (uint)(bytes[8] | bytes[9] << 8 | bytes[10] << 16);
            result.FirstCluster = cluster;
            result.ByteCount = size;
            result.Attributes = attributes;
            result.NameLength = length;
            state = default;
            return LiveDirectoryStatus.Entry;
        }
    }

    public static LiveDirectoryStatus Finish(LiveVfatState state) =>
        state.Slots == 0 && state.Remaining == 0 && state.Checksum == 0 ? LiveDirectoryStatus.End : LiveDirectoryStatus.InvalidSequence;

    private static LiveDirectoryStatus Fail(ref LiveVfatState state, LiveDirectoryStatus status)
    {
        state = default;
        return status;
    }

    private static bool Forbidden(ushort c) => c < 32 || c == '"' || c == '\\' || c == '/' ||
        c == ':' || c == '|' || c == '<' || c == '>' || c == '*' || c == '?';

    private static bool ShortNameValid(byte* bytes)
    {
        if (bytes[0] == ' ')
            return false;
        bool padding = false;
        for (int i = 0; i < 11; i++)
        {
            if (i == 8)
                padding = false;
            byte c = bytes[i];
            if (c == ' ')
            {
                padding = true;
                continue;
            }
            if (padding || (Forbidden(c) && !(i == 0 && c == 5)) || c == '.' || c == '+' ||
                c == ',' || c == ';' || c == '=' || c == '[' || c == ']')
                return false;
        }
        return true;
    }
}
