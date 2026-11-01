using System;
using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Std.Hash;

namespace OS.Storage;

public enum LiveGptStatus : byte
{
    Uninitialized = 0, Success = 1, ReadFailed = 2, InvalidLength = 3,
    InvalidCapacity = 4, BadSignature = 5, UnsupportedRevision = 6,
    InvalidHeaderSize = 7, ReservedNonZero = 8, InvalidHeaderPosition = 9,
    InvalidUsableRange = 10, InvalidDiskGuid = 11, UnsupportedArrayLayout = 12,
    InvalidArrayPlacement = 13, HeaderCrcMismatch = 14, InvalidHeader = 15,
    ArrayCrcMismatch = 16, InvalidUnusedEntry = 17, InvalidPartitionGuid = 18,
    InvalidPartitionExtent = 19, DuplicatePartitionGuid = 20, OverlappingPartitions = 21
}
