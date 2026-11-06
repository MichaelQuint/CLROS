using System;
using OS.Std.Binary;

namespace OS.Storage;

public enum LiveFat32Status : byte
{
    Uninitialized = 0, Success = 1, ReadFailed, InvalidLength, InvalidPartition,
    InvalidBpb, UnsupportedGeometry, InvalidFatBounds, InvalidCluster,
    UnexpectedFatSector, FreeCluster, ReservedCluster, BadCluster, EndOfChain,
    Loop, ScratchLimit, Truncated, Overlong, InvalidVolume, InvalidHistory
}
