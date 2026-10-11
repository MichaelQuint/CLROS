using System;

namespace OS.Kernel.Runtime;

public enum BoundedExceptionFrameDispatchStatus : byte
{
    Uninitialized, Success, SearchRejected, UnsupportedPlan
}

/// <summary>Sequential x64 POD: targets at 0/8 and 240-byte contexts at 16/256; total 496 bytes.</summary>
public readonly struct BoundedExceptionFrameTransfer
{
    internal BoundedExceptionFrameTransfer(ulong catchHandler, ulong cleanupHandler,
        X64UnwindContext catchCaller, X64UnwindContext cleanupCaller)
    { CatchHandler = catchHandler; CleanupHandler = cleanupHandler; CatchCaller = catchCaller; CleanupCaller = cleanupCaller; }

    public readonly ulong CatchHandler, CleanupHandler;
    public readonly X64UnwindContext CatchCaller, CleanupCaller;
}

public readonly struct BoundedExceptionFrameDispatchResult
{
    internal BoundedExceptionFrameDispatchResult(BoundedExceptionFrameDispatchStatus status,
        BoundedExceptionFrameSearchResult search, uint cleanupFrameIndex = 0)
    {
        this = default;
        Status = status; SearchStatus = search.Status; DispatchStatus = search.DispatchStatus;
        if (status == BoundedExceptionFrameDispatchStatus.Success)
        { CatchFrameIndex = search.CatchFrameIndex; CleanupFrameIndex = cleanupFrameIndex; }
    }

    public BoundedExceptionFrameDispatchStatus Status { get; }
    public BoundedExceptionFrameSearchStatus SearchStatus { get; }
    public BoundedExceptionDispatchStatus DispatchStatus { get; }
    public bool Succeeded => Status == BoundedExceptionFrameDispatchStatus.Success;
    public uint CatchFrameIndex { get; }
    public uint CleanupFrameIndex { get; }
}

/// <summary>Admits only one earlier physical-frame cleanup followed by an outer typed catch.</summary>
/// <remarks>
/// Composes the existing complete-table search; it does not authenticate its inputs or execute
/// either target. The caller retains original module, rooted exception, real context and mapping
/// authority/lifetime. Inputs, scratch and transfer must be distinct and remain stable during
/// admission. The search's frame/row/topology limits apply. Only the first eight scratch entries
/// are output; every rejection clears them and defaults the transfer. The caller's later scratch
/// tail is untouched. Success is a bounded plan, not CPU-transfer or exception-lifetime evidence.
/// </remarks>
public static class BoundedExceptionFrameDispatch
{
    public static BoundedExceptionFrameDispatchResult Select(ReadOnlySpan<BoundedExceptionSearchFrame> frames,
        ReadOnlySpan<NativeAotEhClauseResult> rows, ReadOnlySpan<ExceptionTypeMatchResult> matches,
        Span<BoundedExceptionCleanupEntry> scratch, out BoundedExceptionFrameTransfer transfer)
    {
        transfer = default;
        var search = BoundedExceptionFrameSearch.Select(frames, rows, matches, scratch);
        if (!search.Succeeded) return new(BoundedExceptionFrameDispatchStatus.SearchRejected, search);

        if (search.CleanupCount != 1 || scratch[0].FrameIndex >= search.CatchFrameIndex)
        {
            // The successful search may already have published entries: unsupported plans
            // must not leave a usable cleanup receipt beside the default transfer.
            for (int i = 0; i < BoundedExceptionFrameSearch.MaximumCleanupEntries; i++) scratch[i] = default;
            return new(BoundedExceptionFrameDispatchStatus.UnsupportedPlan, search);
        }

        var cleanup = scratch[0];
        transfer = new(search.CatchHandler, cleanup.Handler, search.CatchContext, cleanup.Context);
        return new(BoundedExceptionFrameDispatchStatus.Success, search, cleanup.FrameIndex);
    }
}
