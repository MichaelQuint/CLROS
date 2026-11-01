using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockRequestType : uint
{
    Read = 0,
    Write = 1
}
