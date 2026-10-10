using System;
using OS.Std.Text;

namespace OS.Storage;

public enum LiveManifestStatus : byte
{
    Uninitialized = 0, Success, InvalidLength, InvalidCharacter, MalformedLine,
    UnsupportedField, DuplicateField, MissingField, InvalidValue, InvalidPath
}

public struct LiveManifestResult
{
    public LiveManifestStatus Status;
    public uint ByteCount, AppPathOffset, AppPathLength, AppNameOffset, AppNameLength, ArgsOffset, ArgsLength, ErrorOffset;
    public byte NetworkMode, LogMode;
    public readonly bool Succeeded => Status == LiveManifestStatus.Success;
}

/// <summary>Allocation-free validation of a bounded disk-backed native v0
/// manifest. Admits required fields and optional package/native-aot kind fields;
/// environment/listen fields require a future guest parser extension. Source
/// offsets refer to caller-owned stable bytes. No storage I/O or app activation.</summary>
public static unsafe class NativeManifestDisk
{
    public const uint MaximumBytes = 4096;

    public static LiveManifestResult Validate(Span<byte> source)
    {
        if (source.Length == 0 || (uint)source.Length > MaximumBytes) return Fail(LiveManifestStatus.InvalidLength, 0);
        fixed (byte* bytes = source)
        {
            if (bytes == null || (ulong)bytes > ulong.MaxValue - (uint)source.Length)
                return Fail(LiveManifestStatus.InvalidLength, 0);
            LiveManifestResult result = default;
            uint seen = 0, length = (uint)source.Length, offset = 0;
            while (offset < length)
            {
                uint start = offset, split = length;
                while (offset < length && bytes[offset] != '\n')
                {
                    byte b = bytes[offset];
                    if (b == '=' && split == length) split = offset;
                    if (b < 32 || b > 126)
                    {
                        if (b != '\r' || offset + 1 >= length || bytes[offset + 1] != '\n')
                            return Fail(LiveManifestStatus.InvalidCharacter, offset);
                    }
                    offset++;
                }
                uint end = offset;
                if (end > start && bytes[end - 1] == '\r') end--;
                if (end == start || split >= end || split == start) return Fail(LiveManifestStatus.MalformedLine, start);
                uint keyLength = split - start, valueOffset = split + 1, valueLength = end - valueOffset;
                byte* key = bytes + start; byte* value = bytes + valueOffset;
                uint flag;
                if (AsciiPointerMatcher.Matches(key, keyLength, 6, 0x74616d726f66UL, 0x0UL, 0x0UL)) flag = 1;
                else if (AsciiPointerMatcher.Matches(key, keyLength, 7, 0x6e6f6973726576UL, 0x0UL, 0x0UL)) flag = 2;
                else if (AsciiPointerMatcher.Matches(key, keyLength, 8, 0x656d616e2e707061UL, 0x0UL, 0x0UL)) flag = 4;
                else if (AsciiPointerMatcher.Matches(key, keyLength, 8, 0x687461702e707061UL, 0x0UL, 0x0UL)) flag = 8;
                else if (AsciiPointerMatcher.Matches(key, keyLength, 4, 0x73677261UL, 0x0UL, 0x0UL)) flag = 16;
                else if (AsciiPointerMatcher.Matches(key, keyLength, 7, 0x6b726f7774656eUL, 0x0UL, 0x0UL)) flag = 32;
                else if (AsciiPointerMatcher.Matches(key, keyLength, 3, 0x676f6cUL, 0x0UL, 0x0UL)) flag = 64;
                else if (AsciiPointerMatcher.Matches(key, keyLength, 8, 0x646e696b2e707061UL, 0x0UL, 0x0UL)) flag = 128;
                else if (AsciiPointerMatcher.Matches(key, keyLength, 12, 0x2e656d69746e7572UL, 0x646e696bUL, 0x0UL)) flag = 256;
                else return Fail(LiveManifestStatus.UnsupportedField, start);
                if ((seen & flag) != 0) return Fail(LiveManifestStatus.DuplicateField, start);
                seen |= flag;
                if (flag == 1 && !AsciiPointerMatcher.Matches(value, valueLength, 18, 0x766974616e2d736fUL, 0x6566696e616d2d65UL, 0x7473UL) ||
                    flag == 2 && !AsciiPointerMatcher.Matches(value, valueLength, 1, 0x30UL, 0x0UL, 0x0UL) ||
                    flag == 128 && !AsciiPointerMatcher.Matches(value, valueLength, 7, 0x6567616b636170UL, 0x0UL, 0x0UL) ||
                    flag == 256 && !AsciiPointerMatcher.Matches(value, valueLength, 10, 0x612d65766974616eUL, 0x746fUL, 0x0UL))
                    return Fail(LiveManifestStatus.InvalidValue, valueOffset);
                if (flag == 4)
                {
                    if (valueLength == 0 || valueLength > 64) return Fail(LiveManifestStatus.InvalidValue, valueOffset);
                    result.AppNameOffset = valueOffset; result.AppNameLength = valueLength;
                }
                if (flag == 8)
                {
                    if (valueLength == 0 || valueLength > 128 || !AsciiPointerMatcher.IsRelativePath(value, valueLength))
                        return Fail(LiveManifestStatus.InvalidPath, valueOffset);
                    result.AppPathOffset = valueOffset; result.AppPathLength = valueLength;
                }
                if (flag == 16)
                {
                    if (valueLength > 256) return Fail(LiveManifestStatus.InvalidValue, valueOffset);
                    result.ArgsOffset = valueOffset; result.ArgsLength = valueLength;
                }
                if (flag == 32)
                {
                    if (AsciiPointerMatcher.Matches(value, valueLength, 4, 0x656e6f6eUL, 0x0UL, 0x0UL)) result.NetworkMode = 0;
                    else if (AsciiPointerMatcher.Matches(value, valueLength, 4, 0x70636864UL, 0x0UL, 0x0UL)) result.NetworkMode = 1;
                    else if (AsciiPointerMatcher.Matches(value, valueLength, 6, 0x636974617473UL, 0x0UL, 0x0UL)) result.NetworkMode = 2;
                    else return Fail(LiveManifestStatus.InvalidValue, valueOffset);
                }
                if (flag == 64)
                {
                    if (AsciiPointerMatcher.Matches(value, valueLength, 4, 0x656e6f6eUL, 0x0UL, 0x0UL)) result.LogMode = 0;
                    else if (AsciiPointerMatcher.Matches(value, valueLength, 6, 0x6c6169726573UL, 0x0UL, 0x0UL)) result.LogMode = 1;
                    else return Fail(LiveManifestStatus.InvalidValue, valueOffset);
                }
                if (offset < length) offset++;
            }
            if ((seen & 127) != 127) return Fail(LiveManifestStatus.MissingField, length);
            result.ByteCount = length; result.Status = LiveManifestStatus.Success;
            return result;
        }
    }

    private static LiveManifestResult Fail(LiveManifestStatus status, uint offset)
        => new LiveManifestResult { Status = status, ErrorOffset = offset };
}
