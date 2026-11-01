using System;
using System.Runtime.CompilerServices;
using OS.Std.Binary;

namespace OS.Storage;

public enum Lba0Status : byte
{
    Uninitialized, Success, InvalidLength, InvalidCapacity, BadSignature,
    BadProtectiveEntry, HybridEntry, ReadFailed
}
