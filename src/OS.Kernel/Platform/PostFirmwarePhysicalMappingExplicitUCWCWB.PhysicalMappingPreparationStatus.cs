using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Platform;
public enum PhysicalMappingPreparationStatus : byte
{
    Uninitialized = 0,
    Prepared = 1,
    InvalidUsage = 2,
    InvalidRange = 3,
    InvalidInheritedEntry = 4,
    RangeOutsideLargePage = 5,
    InvalidTableAddress = 6,
    TableOverlapsMappedRange = 7,
    InvalidDestination = 8,
    UnsupportedCachePolicy = 9,
    InvalidTableArena = 10,
    MalformedTable = 11,
    NonIdentityMapping = 12,
    SharedOrCyclicTable = 13,
    UnmappedTableStorage = 14,
    NoFreeTablePage = 15,
    TargetNotLargePage = 16,
    NonzeroUnusedTable = 17,
    UnsupportedCacheEnvironment = 18,
    InvalidMtrrSnapshot = 19,
    MtrrRangeNotWriteBack = 20
}
