using OS.Std.Binary;
using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Storage;

public static partial class GptPartitionTableParser
{
    private enum GptPartitionTableKind
    {
        Primary = 0,
        Backup = 1
    }
}
