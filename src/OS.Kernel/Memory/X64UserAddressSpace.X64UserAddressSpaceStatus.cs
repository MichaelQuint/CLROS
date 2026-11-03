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
#if M10_004_PROBE

#elif M16_025_PROBE

#else
#endif
