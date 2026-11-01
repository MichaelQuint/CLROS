using System;

namespace OS.Kernel.Runtime;
public readonly struct BoundedExceptionFrameDispatchResult
{
    internal BoundedExceptionFrameDispatchResult(BoundedExceptionFrameDispatchStatus status, BoundedExceptionFrameSearchResult search, uint cleanupFrameIndex = 0)
    {
        this = default;
        Status = status;
        SearchStatus = search.Status;
        DispatchStatus = search.DispatchStatus;
        if (status == BoundedExceptionFrameDispatchStatus.Success)
        {
            CatchFrameIndex = search.CatchFrameIndex;
            CleanupFrameIndex = cleanupFrameIndex;
        }
    }

    public BoundedExceptionFrameDispatchStatus Status { get; }
    public BoundedExceptionFrameSearchStatus SearchStatus { get; }
    public BoundedExceptionDispatchStatus DispatchStatus { get; }
    public bool Succeeded => Status == BoundedExceptionFrameDispatchStatus.Success;
    public uint CatchFrameIndex { get; }
    public uint CleanupFrameIndex { get; }
}
