using OS.Boot;
using Layout = OS.Kernel.Runtime.ObjectArrayStringEETypeMethodTableLayoutContracts;

namespace OS.Kernel.Runtime;

public readonly struct ExceptionTypeMatchResult
{
    internal ExceptionTypeMatchResult(ExceptionTypeMatchStatus status, bool matches = false)
    {
        Status = status;
        Matches = status == ExceptionTypeMatchStatus.Success && matches;
    }

    public ExceptionTypeMatchStatus Status
    {
        get;
    }
    public bool Succeeded => Status == ExceptionTypeMatchStatus.Success;
    public bool Matches
    {
        get;
    }
}
