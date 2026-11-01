using System;

namespace OS.Kernel.Runtime;
public readonly struct BoundedExceptionFilterSearchResult
{
    internal BoundedExceptionFilterSearchResult(BoundedExceptionFilterSearchStatus status)
    {
        this = default;
        Status = status;
    }

    internal BoundedExceptionFilterSearchResult(BoundedExceptionFilterSearchStatus status, uint index, ulong target)
    {
        this = default;
        Status = status;
        ClauseIndex = index;
        if (status == BoundedExceptionFilterSearchStatus.FilterRequired)
            FilterAddress = target;
        else if (status == BoundedExceptionFilterSearchStatus.Success)
            HandlerAddress = target;
    }

    public BoundedExceptionFilterSearchStatus Status { get; }
    public bool Succeeded => Status == BoundedExceptionFilterSearchStatus.Success;
    public bool RequiresFilter => Status == BoundedExceptionFilterSearchStatus.FilterRequired;
    public uint ClauseIndex { get; }
    public ulong FilterAddress { get; }
    public ulong HandlerAddress { get; }
}
