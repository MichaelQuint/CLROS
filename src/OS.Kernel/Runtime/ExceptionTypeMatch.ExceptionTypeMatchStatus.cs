using OS.Boot;
using Layout = OS.Kernel.Runtime.ObjectArrayStringEETypeMethodTableLayoutContracts;

namespace OS.Kernel.Runtime;

public enum ExceptionTypeMatchStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    UnsupportedProfile = 2,
    ObjectRejected = 3,
    ModuleRejected = 4,
    MetadataRejected = 5,
    UnsupportedType = 6,
    HierarchyCycle = 7,
    DepthExceeded = 8
}
