using System;

namespace OS.Kernel.Runtime;
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
    public static BoundedExceptionFrameDispatchResult Select(ReadOnlySpan<BoundedExceptionSearchFrame> frames, ReadOnlySpan<NativeAotEhClauseResult> rows, ReadOnlySpan<ExceptionTypeMatchResult> matches, Span<BoundedExceptionCleanupEntry> scratch, out BoundedExceptionFrameTransfer transfer)
    {
        transfer = default;
        var search = BoundedExceptionFrameSearch.Select(frames, rows, matches, scratch);
        if (!search.Succeeded)
            return new(BoundedExceptionFrameDispatchStatus.SearchRejected, search);
        if (search.CleanupCount != 1 || scratch[0].FrameIndex >= search.CatchFrameIndex)
        {
            // The successful search may already have published entries: unsupported plans
            // must not leave a usable cleanup receipt beside the default transfer.
            for (int i = 0; i < BoundedExceptionFrameSearch.MaximumCleanupEntries; i++)
                scratch[i] = default;
            return new(BoundedExceptionFrameDispatchStatus.UnsupportedPlan, search);
        }

        var cleanup = scratch[0];
        transfer = new(search.CatchHandler, cleanup.Handler, search.CatchContext, cleanup.Context);
        return new(BoundedExceptionFrameDispatchStatus.Success, search, cleanup.FrameIndex);
    }
}
