using System;
using OS.Std.Binary;
using OS.Std.Hash;

namespace OS.Storage;

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
