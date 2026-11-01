using System;
using OS.Std.Hash;

namespace OS.Storage;

public enum LiveFileStatus : byte
{
    Uninitialized = 0, Success = 1, Complete, InvalidEntry, InvalidState,
    ReadFailed, InvalidLength, UnexpectedOffset, Truncated, ChecksumMismatch
}
