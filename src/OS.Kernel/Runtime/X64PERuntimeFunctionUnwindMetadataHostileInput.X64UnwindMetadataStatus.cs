using OS.Boot;

namespace OS.Kernel.Runtime;
public enum X64UnwindMetadataStatus : byte
{
    Uninitialized,
    Success,
    ModuleRejected,
    DirectoryAbsent,
    DirectoryRejected,
    FunctionRejected,
    UnwindRejected,
    UnsupportedFormat,
    ChainCycle,
    CapacityExceeded,
    StorageRejected,
    StorageOverlap,
    AlreadyBound,
    NotBound,
    MetadataChanged,
    NotFound
}
