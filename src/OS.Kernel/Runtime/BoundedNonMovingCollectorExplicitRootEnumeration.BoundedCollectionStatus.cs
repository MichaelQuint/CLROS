using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;

public enum BoundedCollectionStatus : byte
{
    Uninitialized,
    Success,
    UnsupportedProfile,
    InvalidRoots,
    StorageOverlap,
    OwnerRejected,
    ModuleRejected,
    MetadataRejected,
    InvalidReference,
    StaticRootRequired,
    StaticRootRejected,
    WorkLimitExceeded
}
