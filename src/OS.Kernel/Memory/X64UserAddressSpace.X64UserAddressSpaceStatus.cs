using OS.Boot;

namespace OS.Kernel.Memory;

public enum X64UserAddressSpaceStatus : byte
{
    Uninitialized,
    Ready,
    InvalidInput,
    InvalidRange,
    OverlappingRanges,
    InvalidEncoding,
    InsufficientTables,
    DirtyTables,
    AuditFailed
}
