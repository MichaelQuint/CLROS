using OS.Std.Binary;
using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Storage;

public enum GptPartitionTableCrcStatus
{
    NotValidated = 0,
    Validated = 1,
    HeaderMismatch = 2,
    PartitionEntryArrayMismatch = 3
}
