using OS.Boot;
using Layout = OS.Kernel.Runtime.ObjectArrayStringEETypeMethodTableLayoutContracts;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

namespace OS.Kernel.Runtime;

public enum NativeAotEhClauseStatus : byte
{
    Uninitialized,
    Success,
    UnsupportedProfile,
    ModuleRejected,
    SnapshotRejected,
    BoundsRejected,
    UnsupportedFormat,
    MetadataRejected,
    CapacityExceeded,
    TargetRejected,
    NoEhInfo,
    IndexOutOfRange
}
