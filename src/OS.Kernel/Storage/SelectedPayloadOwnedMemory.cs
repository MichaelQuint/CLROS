using System;
using OS.Std.Binary;
using OS.Std.Hash;

namespace OS.Storage;

public enum SelectedPayloadOwnedMemoryStatus : byte
{
    Uninitialized,
    Success,
    PackageValidationFailed,
    InvalidPayloadHeader,
    InvalidRange,
    DestinationTooSmall,
    OverlappingBuffers
}

public readonly struct SelectedPayloadOwnedMemoryResult
{
    internal SelectedPayloadOwnedMemoryResult(
        SelectedPayloadOwnedMemoryStatus status,
        LivePackageStatus packageStatus,
        uint bytesWritten = 0,
        uint entryOffset = 0,
        uint payloadCrc32 = 0)
    {
        Status = status;
        PackageStatus = packageStatus;
        BytesWritten = bytesWritten;
        EntryOffset = entryOffset;
        PayloadCrc32 = payloadCrc32;
    }

    public SelectedPayloadOwnedMemoryStatus Status { get; }
    public LivePackageStatus PackageStatus { get; }
    public uint BytesWritten { get; }
    public uint EntryOffset { get; }
    public uint PayloadCrc32 { get; }
    public bool Succeeded => Status == SelectedPayloadOwnedMemoryStatus.Success;
}

// Pure staging over retained caller-owned bytes. No storage reads, allocation,
// executable mapping or invocation. The caller supplies valid backing memory.
public static unsafe class SelectedPayloadOwnedMemory
{
    public static SelectedPayloadOwnedMemoryResult Stage(
        Span<byte> package,
        Span<byte> path,
        uint expectedCrc32,
        Span<byte> destination)
    {
        fixed (byte* packageBytes = package)
        fixed (byte* pathBytes = path)
        fixed (byte* destinationBytes = destination)
        {
            if (!TryEnd(packageBytes, package.Length, out ulong packageEnd)
                || !TryEnd(pathBytes, path.Length, out ulong pathEnd)
                || !TryEnd(destinationBytes, destination.Length, out ulong destinationEnd))
            {
                return new(SelectedPayloadOwnedMemoryStatus.InvalidRange, LivePackageStatus.Uninitialized);
            }

            // Validate the actual bytes on every call; no supplied receipt can
            // authorize a stale or forged content range.
            LivePackageResult selected = APPPKGDisk.Validate(package, path, expectedCrc32);
            if (!selected.Succeeded)
            {
                return new(SelectedPayloadOwnedMemoryStatus.PackageValidationFailed, selected.Status);
            }

            if (selected.ContentOffset > (uint)package.Length
                || selected.ContentLength > (uint)package.Length - selected.ContentOffset)
            {
                return new(SelectedPayloadOwnedMemoryStatus.InvalidRange, selected.Status);
            }

            byte* payload = packageBytes + selected.ContentOffset;
            if (selected.ContentLength < 32
                // OSAPPv0\0, matching NativeAppPayloadHeader's v0 format.
                || EndianPrimitives.ReadUInt64LittleEndian(payload) != 0x003076505041534FUL
                || EndianPrimitives.ReadUInt16LittleEndian(payload + 8) != 0
                || EndianPrimitives.ReadUInt16LittleEndian(payload + 10) != 32
                || EndianPrimitives.ReadUInt32LittleEndian(payload + 12) != 0
                || EndianPrimitives.ReadUInt64LittleEndian(payload + 24) != 0)
            {
                return new(SelectedPayloadOwnedMemoryStatus.InvalidPayloadHeader, selected.Status);
            }

            ulong entryOffset = EndianPrimitives.ReadUInt64LittleEndian(payload + 16);
            if (entryOffset < 32 || entryOffset >= selected.ContentLength)
            {
                return new(SelectedPayloadOwnedMemoryStatus.InvalidPayloadHeader, selected.Status);
            }

            if ((uint)destination.Length < selected.ContentLength)
            {
                return new(SelectedPayloadOwnedMemoryStatus.DestinationTooSmall, selected.Status);
            }

            if (Overlaps(packageBytes, packageEnd, pathBytes, pathEnd)
                || Overlaps(packageBytes, packageEnd, destinationBytes, destinationEnd)
                || Overlaps(pathBytes, pathEnd, destinationBytes, destinationEnd))
            {
                return new(SelectedPayloadOwnedMemoryStatus.OverlappingBuffers, selected.Status);
            }

            uint crc = Crc32.InitialState;
            for (uint index = 0; index < selected.ContentLength; index++)
                crc = Crc32.UpdateByte(crc, payload[index]);

            for (uint index = 0; index < selected.ContentLength; index++)
                destinationBytes[index] = payload[index];

            return new(
                SelectedPayloadOwnedMemoryStatus.Success,
                selected.Status,
                selected.ContentLength,
                (uint)entryOffset,
                Crc32.Finish(crc));
        }
    }

    private static bool TryEnd(byte* bytes, int length, out ulong end)
    {
        ulong start = (ulong)bytes;
        end = 0;
        if (length < 0 || (bytes == null && length != 0)
            || start > ulong.MaxValue - (uint)length)
            return false;

        end = start + (uint)length;
        return true;
    }

    private static bool Overlaps(byte* left, ulong leftEnd, byte* right, ulong rightEnd) =>
        (ulong)left < rightEnd && (ulong)right < leftEnd;
}
