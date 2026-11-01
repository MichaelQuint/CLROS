using OS.Std.Binary;

namespace OS.Storage;

public static class VirtioBlockFeatureBits
{
    public const int ReadOnlyBit = 5;
    public const ulong ReadOnly = 1UL << ReadOnlyBit;
}
