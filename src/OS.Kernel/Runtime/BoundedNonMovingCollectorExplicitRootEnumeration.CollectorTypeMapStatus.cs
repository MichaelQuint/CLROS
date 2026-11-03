using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;

public enum CollectorTypeMapStatus : byte
{
    Uninitialized,
    Success,
    UnsupportedProfile,
    InvalidWindow,
    InvalidMethodTable,
    ModuleRejected,
    UnsupportedType,
    InvalidDescriptor,
    UnsupportedSeriesCount,
    OverlappingSeries
}
