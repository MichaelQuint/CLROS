using OS.Boot;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

namespace OS.Kernel.Runtime;

public enum NativeAotNormalFinallyStatus : byte
{
    Uninitialized,
    Success,
    UnsupportedProfile,
    ModuleRejected,
    FuncletRejected,
    UnwindRejected,
    ParentRejected,
    CatalogRejected,
    ClauseRejected,
    AmbiguousClause
}
