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
