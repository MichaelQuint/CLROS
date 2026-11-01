using OS.Std.Binary;

namespace OS.Drivers;
public enum MadtPollingStatus : byte
{
    Uninitialized,
    Success,
    InvalidStorage,
    InvalidHeader,
    ChecksumFailed,
    InvalidRecord,
    UnsupportedTopology,
    DuplicateRecord,
    InvalidAddress,
    MissingProcessor,
    MissingIoApic,
    BspMismatch,
    TooManyRecords
}
