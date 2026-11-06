using OS.Boot;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

namespace OS.Kernel.Runtime;

public readonly struct NativeAotNormalFinallyResult
{
    internal NativeAotNormalFinallyResult(NativeAotNormalFinallyStatus status)
    {
        this = default;
        Status = status;
    }

    internal NativeAotNormalFinallyResult(X64UnwindContext parent, NativeAotEhMethodBounds bounds, uint protectedOffset, uint exitedClause)
    {
        Status = NativeAotNormalFinallyStatus.Success;
        Parent = parent;
        Bounds = bounds;
        ProtectedOffset = protectedOffset;
        ExitedClauseIndex = exitedClause;
    }

    public NativeAotNormalFinallyStatus Status
    {
        get;
    }
    public bool Succeeded => Status == NativeAotNormalFinallyStatus.Success;
    public X64UnwindContext Parent
    {
        get;
    }
    public NativeAotEhMethodBounds Bounds
    {
        get;
    }
    public uint ProtectedOffset
    {
        get;
    }
    public uint ExitedClauseIndex
    {
        get;
    }
}
