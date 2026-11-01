using OS.Boot;

namespace OS.Kernel.Runtime;
public enum CompilerInterfaceDispatchStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    StorageRejected = 2,
    AlreadyBound = 3,
    NotBound = 4,
    StateChanged = 5,
    ModuleRejected = 6,
    AllocatorRejected = 7,
    CellRejected = 8,
    CellChanged = 9,
    UnsupportedCell = 10,
    NullReceiver = 11,
    ReceiverRejected = 12,
    MetadataRejected = 13,
    MetadataChanged = 14,
    UnsupportedType = 15,
    MapRejected = 16,
    SlotRejected = 17,
    TargetRejected = 18,
    IncompatibleInterface = 19,
    AmbiguousImplementation = 20,
    CapacityExceeded = 21,
    CacheChanged = 22
}
