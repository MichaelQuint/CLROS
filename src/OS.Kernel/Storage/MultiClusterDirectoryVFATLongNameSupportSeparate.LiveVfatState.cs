using System;
using OS.Std.Binary;

namespace OS.Storage;

public struct LiveVfatState
{
    public byte Slots, Remaining, Checksum;
}
