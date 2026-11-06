using OS.Boot;
using Layout = OS.Kernel.Runtime.ObjectArrayStringEETypeMethodTableLayoutContracts;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

namespace OS.Kernel.Runtime;

public enum NativeAotEhClauseKind : byte
{
    Typed = 0,
    FaultOrFinally = 1,
    Filter = 2
}
