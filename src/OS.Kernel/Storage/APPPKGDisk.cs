using System;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Std.Text;

namespace OS.Storage;

/// <summary>Bounded OSPKGv0 metadata and selected file validation over stable
/// caller-owned disk bytes. CRC32 is supplied by the reviewed fixture/caller;
/// the v0 package has no cryptographic authentication or embedded hash.</summary>
public static unsafe class APPPKGDisk
{
    public const uint MaximumBytes = 8192;
    public const ushort MaximumFiles = 16;

    public static LivePackageResult Validate(Span<byte> package, Span<byte> selectedPath, uint expectedCrc32)
    {
        uint size = (uint)package.Length;
        if (size < 32 || size > MaximumBytes) return Fail(LivePackageStatus.InvalidLength);
        fixed (byte* bytes = package)
        fixed (byte* path = selectedPath)
        {
            if (bytes == null || (ulong)bytes > ulong.MaxValue - size) return Fail(LivePackageStatus.InvalidLength);
            if (!AsciiPointerMatcher.IsRelativePath(path, (uint)selectedPath.Length))
                return Fail(LivePackageStatus.InvalidPath);
            if (!AsciiPointerMatcher.Matches(bytes, 8, 8, 0x003076474b50534fUL) ||
                EndianPrimitives.ReadUInt16LittleEndian(bytes + 10) != 32 ||
                EndianPrimitives.ReadUInt16LittleEndian(bytes + 14) != 32 ||
                EndianPrimitives.ReadUInt64LittleEndian(bytes + 24) != 0)
                return Fail(LivePackageStatus.InvalidHeader);
            if (EndianPrimitives.ReadUInt16LittleEndian(bytes + 8) != 0)
                return Fail(LivePackageStatus.UnsupportedVersion);
            ushort files = EndianPrimitives.ReadUInt16LittleEndian(bytes + 12);
            uint table = EndianPrimitives.ReadUInt32LittleEndian(bytes + 16);
            uint tableLength = EndianPrimitives.ReadUInt32LittleEndian(bytes + 20);
            if (files == 0 || files > MaximumFiles || tableLength != (uint)files * 32 ||
                table < 32 || !Inside(size, table, tableLength)) return Fail(LivePackageStatus.InvalidTable);
            LivePackageResult result = default; bool found = false;
            for (uint i = 0; i < files; i++)
            {
                byte* entry = bytes + table + i * 32;
                uint pathOffset = EndianPrimitives.ReadUInt32LittleEndian(entry);
                uint pathLength = EndianPrimitives.ReadUInt16LittleEndian(entry + 4);
                ulong contentOffset = EndianPrimitives.ReadUInt64LittleEndian(entry + 8);
                ulong contentLength = EndianPrimitives.ReadUInt64LittleEndian(entry + 16);
                if (EndianPrimitives.ReadUInt16LittleEndian(entry + 6) != 0 ||
                    EndianPrimitives.ReadUInt64LittleEndian(entry + 24) != 0 ||
                    !Inside(size, pathOffset, pathLength) || !Inside(size, contentOffset, contentLength))
                    return Fail(LivePackageStatus.InvalidEntry);
                if (Metadata(pathOffset, pathLength, table, tableLength) ||
                    Metadata(contentOffset, contentLength, table, tableLength) ||
                    Overlaps(pathOffset, pathLength, contentOffset, contentLength))
                    return Fail(LivePackageStatus.Overlap);
                if (!AsciiPointerMatcher.IsRelativePath(bytes + pathOffset, pathLength))
                    return Fail(LivePackageStatus.InvalidPath);
                for (uint j = 0; j < i; j++)
                {
                    byte* prior = bytes + table + j * 32;
                    uint priorPath = EndianPrimitives.ReadUInt32LittleEndian(prior);
                    uint priorLength = EndianPrimitives.ReadUInt16LittleEndian(prior + 4);
                    ulong priorContent = EndianPrimitives.ReadUInt64LittleEndian(prior + 8);
                    ulong priorContentLength = EndianPrimitives.ReadUInt64LittleEndian(prior + 16);
                    if (Equal(bytes + pathOffset, pathLength, bytes + priorPath, priorLength))
                        return Fail(LivePackageStatus.DuplicatePath);
                    if (Overlaps(pathOffset, pathLength, priorPath, priorLength) ||
                        Overlaps(pathOffset, pathLength, priorContent, priorContentLength) ||
                        Overlaps(contentOffset, contentLength, priorPath, priorLength) ||
                        Overlaps(contentOffset, contentLength, priorContent, priorContentLength))
                        return Fail(LivePackageStatus.Overlap);
                }
                if (Equal(bytes + pathOffset, pathLength, path, (uint)selectedPath.Length))
                {
                    found = true; result.ContentOffset = (uint)contentOffset; result.ContentLength = (uint)contentLength;
                }
            }
            if (!found) return Fail(LivePackageStatus.MissingFile);
            uint crc = Crc32.InitialState;
            for (uint i = 0; i < size; i++) crc = Crc32.UpdateByte(crc, bytes[i]);
            crc = Crc32.Finish(crc);
            if (crc != expectedCrc32) return Fail(LivePackageStatus.ChecksumMismatch);
            result.Status = LivePackageStatus.Success; result.FileCount = files; result.PackageCrc32 = crc;
            return result;
        }
    }

    private static bool Inside(uint size, ulong offset, ulong length) => offset <= size && length <= size - offset;
    private static bool Metadata(ulong offset, ulong length, uint table, uint tableLength) =>
        Overlaps(offset, length, 0, 32) || Overlaps(offset, length, table, tableLength);
    // Arguments have already been checked against the bounded package length.
    private static bool Overlaps(ulong a, ulong al, ulong b, ulong bl) => al != 0 && bl != 0 && a < b + bl && b < a + al;
    private static bool Equal(byte* a, uint al, byte* b, uint bl)
    {
        if (al != bl) return false;
        for (uint i = 0; i < al; i++) if (a[i] != b[i]) return false;
        return true;
    }
    private static LivePackageResult Fail(LivePackageStatus status) => new LivePackageResult { Status = status };
}
