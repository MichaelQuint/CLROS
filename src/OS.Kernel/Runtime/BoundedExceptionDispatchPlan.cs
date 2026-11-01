using System;

namespace OS.Kernel.Runtime;
/// <summary>First-pass typed search and second-pass exceptional cleanup policy for one bounded method.</summary>
/// <remarks>
/// Caller supplies every physical row from NativeAotEhClauseMetadata.ReadClause for the SAME
/// original, authenticated method contribution and one successful ExceptionTypeMatch result
/// at each typed row's index, over that row's type and the SAME retained exception. This policy
/// cannot authenticate provenance, pair receipts to types, retain a module/root or authorize
/// CPU transfer. All image, allocation, stack and compiler-shape authority must remain stable
/// through consumption and continuation. Control PC is the already checked return PC minus one.
/// Containing ranges must be ordered inner to outer; equal ranges admit typed catch siblings
/// only. Filters, boundary markers, cloned/equal-range cleanups and general collided/rethrow
/// starts are unsupported. The narrow normal-finally entry skips an independently authenticated
/// exited cleanup and all earlier clauses in both passes. Kind 1 is fault OR finally: this plan
/// applies only to exceptional unwind.
/// No successful partial plan is exposed, including after a matching catch in an invalid tail.
/// </remarks>

public static class BoundedExceptionDispatchPlan
{
    public const int MaximumClauses = 4;
    public const uint MaximumCleanups = 2;
    public static BoundedExceptionDispatchResult Select(ReadOnlySpan<NativeAotEhClauseResult> clauses, ReadOnlySpan<ExceptionTypeMatchResult> matches, uint protectedOffset) => SelectCore(clauses, matches, protectedOffset, false, 0);
    /// <summary>Searches after one exited normal finally without re-entering that clause or earlier clauses.</summary>
    /// <remarks>
    /// Caller pairs the complete rows/typed receipts to the SAME retained exception and method
    /// and independently authenticates the exited clause and restored parent context (for example
    /// with RecoverNormalFinallyParent). Numeric indices alone do not prove that pairing or
    /// invocation history. All rows, including skipped rows and the tail, retain full validation.
    /// Original indices are preserved. This is policy only, with no CPU transfer authority.
    /// </remarks>

    public static BoundedExceptionDispatchResult SelectAfterExitedFinally(ReadOnlySpan<NativeAotEhClauseResult> clauses, ReadOnlySpan<ExceptionTypeMatchResult> matches, uint protectedOffset, uint exitedClauseIndex) => SelectCore(clauses, matches, protectedOffset, true, exitedClauseIndex);
    private static BoundedExceptionDispatchResult SelectCore(ReadOnlySpan<NativeAotEhClauseResult> clauses, ReadOnlySpan<ExceptionTypeMatchResult> matches, uint protectedOffset, bool skipExited, uint exitedClauseIndex)
    {
        if (clauses.Length == 0)
            return new(BoundedExceptionDispatchStatus.Empty);
        if (clauses.Length > MaximumClauses)
            return new(BoundedExceptionDispatchStatus.CapacityExceeded);
        if (matches.Length != clauses.Length)
            return new(BoundedExceptionDispatchStatus.InvalidTypeMatch);
        if (skipExited && exitedClauseIndex >= (uint)clauses.Length)
            return new(BoundedExceptionDispatchStatus.InvalidStart);
        int selected = -1;
        // Validate the complete input before publishing any handler or cleanup address.
        for (int i = 0; i < clauses.Length; i++)
        {
            var row = clauses[i];
            if (!row.Succeeded || row.Index != (uint)i || row.Count != (uint)clauses.Length)
                return new(BoundedExceptionDispatchStatus.InvalidClause);
            if (row.IsTryGroupBoundary || row.FilterAddress != 0 || (row.Kind != NativeAotEhClauseKind.Typed && row.Kind != NativeAotEhClauseKind.FaultOrFinally))
                return new(BoundedExceptionDispatchStatus.UnsupportedClause);
            if (row.TryStartOffset >= row.TryEndOffset || row.HandlerAddress == 0)
                return new(BoundedExceptionDispatchStatus.InvalidClause);
            if ((row.Kind == NativeAotEhClauseKind.Typed) != (row.TypeAddress != 0))
                return new(BoundedExceptionDispatchStatus.InvalidClause);
            if (row.Kind == NativeAotEhClauseKind.Typed && !matches[i].Succeeded)
                return new(BoundedExceptionDispatchStatus.InvalidTypeMatch);
            for (int j = 0; j < i; j++)
            {
                var inner = clauses[j];
                if (inner.TryEndOffset <= row.TryStartOffset || row.TryEndOffset <= inner.TryStartOffset)
                    continue;
                bool equal = inner.TryStartOffset == row.TryStartOffset && inner.TryEndOffset == row.TryEndOffset;
                if (inner.TryStartOffset < row.TryStartOffset || inner.TryEndOffset > row.TryEndOffset || (equal && (inner.Kind != NativeAotEhClauseKind.Typed || row.Kind != NativeAotEhClauseKind.Typed)))
                    return new(BoundedExceptionDispatchStatus.InvalidNesting);
            }

            if (!row.ContainsOffset(protectedOffset) || (skipExited && (uint)i <= exitedClauseIndex))
                continue;
            if (selected < 0 && row.Kind == NativeAotEhClauseKind.Typed && matches[i].Matches)
                selected = i;
        }

        if (skipExited && clauses[(int)exitedClauseIndex].Kind != NativeAotEhClauseKind.FaultOrFinally)
            return new(BoundedExceptionDispatchStatus.InvalidStart);
        if (selected < 0)
            return new(BoundedExceptionDispatchStatus.NoMatchingCatch);
        uint count = 0;
        ulong first = 0, second = 0;
        var caught = clauses[selected];
        for (int i = 0; i < selected; i++)
        {
            if (skipExited && (uint)i <= exitedClauseIndex)
                continue;
            var cleanup = clauses[i];
            if (cleanup.Kind != NativeAotEhClauseKind.FaultOrFinally || !cleanup.ContainsOffset(protectedOffset))
                continue;
            if (cleanup.TryStartOffset < caught.TryStartOffset || cleanup.TryEndOffset > caught.TryEndOffset || (cleanup.TryStartOffset == caught.TryStartOffset && cleanup.TryEndOffset == caught.TryEndOffset))
                return new(BoundedExceptionDispatchStatus.InvalidNesting);
            if (++count > MaximumCleanups)
                return new(BoundedExceptionDispatchStatus.CapacityExceeded);
            if (cleanup.HandlerAddress == caught.HandlerAddress || cleanup.HandlerAddress == first)
                return new(BoundedExceptionDispatchStatus.InvalidClause);
            if (count == 1)
                first = cleanup.HandlerAddress;
            else
                second = cleanup.HandlerAddress;
        }

        return new((uint)selected, caught.HandlerAddress, count, first, second);
    }
}
