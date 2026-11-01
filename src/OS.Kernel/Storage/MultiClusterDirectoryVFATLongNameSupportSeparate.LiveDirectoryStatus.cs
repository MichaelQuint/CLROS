using System;
using OS.Std.Binary;

namespace OS.Storage;

public enum LiveDirectoryStatus : byte
{
    Uninitialized = 0, Entry = 1, Skipped, LongName, End,
    InvalidLength, InvalidState, InvalidSequence, InvalidChecksum, InvalidName, InvalidEntry
}
