using OS.Boot;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

namespace OS.Kernel.Runtime;

public readonly struct NativeAotEhMethodLookupResult
{
    internal NativeAotEhMethodLookupResult(NativeAotEhClauseStatus status)
    {
        this = default;
        Status = status;
    }

    internal NativeAotEhMethodLookupResult(NativeAotEhMethodBounds bounds, uint clauses)
    {
        this = default;
        Status = NativeAotEhClauseStatus.Success;
        Bounds = bounds;
        ClauseCount = clauses;
    }

    public NativeAotEhClauseStatus Status
    {
        get;
    }
    public bool Succeeded => Status == NativeAotEhClauseStatus.Success;
    public NativeAotEhMethodBounds Bounds
    {
        get;
    }
    public uint ClauseCount
    {
        get;
    }
}
