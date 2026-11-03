using System;

namespace OS.Kernel.Runtime;

public readonly struct BoundedExceptionFrameSearchResult
{
    internal BoundedExceptionFrameSearchResult(BoundedExceptionFrameSearchStatus status, BoundedExceptionDispatchStatus dispatchStatus = default)
    {
        this = default;
        Status = status;
        DispatchStatus = dispatchStatus;
    }

    internal BoundedExceptionFrameSearchResult(uint frameIndex, BoundedExceptionDispatchResult plan, X64UnwindContext context, uint cleanupCount)
    {
        Status = BoundedExceptionFrameSearchStatus.Success;
        DispatchStatus = BoundedExceptionDispatchStatus.Success;
        CatchFrameIndex = frameIndex;
        CatchClauseIndex = plan.CatchIndex;
        CatchHandler = plan.CatchHandler;
        CatchContext = context;
        CleanupCount = cleanupCount;
    }

    public BoundedExceptionFrameSearchStatus Status
    {
        get;
    }
    public BoundedExceptionDispatchStatus DispatchStatus
    {
        get;
    }
    public bool Succeeded => Status == BoundedExceptionFrameSearchStatus.Success;
    public uint CatchFrameIndex
    {
        get;
    }
    public uint CatchClauseIndex
    {
        get;
    }
    public ulong CatchHandler
    {
        get;
    }
    public X64UnwindContext CatchContext
    {
        get;
    }
    public uint CleanupCount
    {
        get;
    }
}
