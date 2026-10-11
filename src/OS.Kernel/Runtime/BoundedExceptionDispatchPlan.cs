using System;

namespace OS.Kernel.Runtime;

public enum BoundedExceptionDispatchStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    Empty = 2,
    CapacityExceeded = 3,
    InvalidClause = 4,
    UnsupportedClause = 5,
    InvalidTypeMatch = 6,
    InvalidNesting = 7,
    NoMatchingCatch = 8
}

public readonly struct BoundedExceptionDispatchResult
{
    internal BoundedExceptionDispatchResult(BoundedExceptionDispatchStatus status)
    { this = default; Status = status; }
    internal BoundedExceptionDispatchResult(uint index, ulong handler, uint cleanups, ulong first, ulong second)
    {
        Status = BoundedExceptionDispatchStatus.Success;
        CatchIndex = index; CatchHandler = handler; CleanupCount = cleanups;
        FirstCleanup = first; SecondCleanup = second;
    }
    public BoundedExceptionDispatchStatus Status { get; }
    public bool Succeeded => Status == BoundedExceptionDispatchStatus.Success;
    public uint CatchIndex { get; }
    public ulong CatchHandler { get; }
    public uint CleanupCount { get; }
    public ulong FirstCleanup { get; }
    public ulong SecondCleanup { get; }
}

/// <summary>First-pass typed search and second-pass exceptional cleanup policy for one bounded method.</summary>
/// <remarks>
/// Caller supplies every physical row from NativeAotEhClauseMetadata.ReadClause for the SAME
/// original, authenticated method contribution and one successful ExceptionTypeMatch result
/// at each typed row's index, over that row's type and the SAME retained exception. This policy
/// cannot authenticate provenance, pair receipts to types, retain a module/root or authorize
/// CPU transfer. All image, allocation, stack and compiler-shape authority must remain stable
/// through consumption and continuation. Control PC is the already checked return PC minus one.
/// Containing ranges must be ordered inner to outer; equal ranges admit typed catch siblings
/// only. Filters, boundary markers, cloned/equal-range cleanups and collided/rethrow starts
/// are unsupported. Kind 1 is fault OR finally: this plan applies only to exceptional unwind.
/// No successful partial plan is exposed, including after a matching catch in an invalid tail.
/// </remarks>
public static class BoundedExceptionDispatchPlan
{
    public const int MaximumClauses = 4;
    public const uint MaximumCleanups = 2;

    public static BoundedExceptionDispatchResult Select(ReadOnlySpan<NativeAotEhClauseResult> clauses,
        ReadOnlySpan<ExceptionTypeMatchResult> matches, uint protectedOffset)
    {
        if (clauses.Length == 0) return new(BoundedExceptionDispatchStatus.Empty);
        if (clauses.Length > MaximumClauses) return new(BoundedExceptionDispatchStatus.CapacityExceeded);
        if (matches.Length != clauses.Length) return new(BoundedExceptionDispatchStatus.InvalidTypeMatch);

        int selected = -1;
        // Validate the complete input before publishing any handler or cleanup address.
        for (int i = 0; i < clauses.Length; i++)
        {
            var row = clauses[i];
            if (!row.Succeeded || row.Index != (uint)i || row.Count != (uint)clauses.Length)
                return new(BoundedExceptionDispatchStatus.InvalidClause);
            if (row.IsTryGroupBoundary || row.FilterAddress != 0 ||
                (row.Kind != NativeAotEhClauseKind.Typed && row.Kind != NativeAotEhClauseKind.FaultOrFinally))
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
                if (inner.TryEndOffset <= row.TryStartOffset || row.TryEndOffset <= inner.TryStartOffset) continue;
                bool equal = inner.TryStartOffset == row.TryStartOffset && inner.TryEndOffset == row.TryEndOffset;
                if (inner.TryStartOffset < row.TryStartOffset || inner.TryEndOffset > row.TryEndOffset ||
                    (equal && (inner.Kind != NativeAotEhClauseKind.Typed || row.Kind != NativeAotEhClauseKind.Typed)))
                    return new(BoundedExceptionDispatchStatus.InvalidNesting);
            }
            if (!row.ContainsOffset(protectedOffset)) continue;
            if (selected < 0 && row.Kind == NativeAotEhClauseKind.Typed && matches[i].Matches) selected = i;
        }
        if (selected < 0) return new(BoundedExceptionDispatchStatus.NoMatchingCatch);

        uint count = 0;
        ulong first = 0, second = 0;
        var caught = clauses[selected];
        for (int i = 0; i < selected; i++)
        {
            var cleanup = clauses[i];
            if (cleanup.Kind != NativeAotEhClauseKind.FaultOrFinally || !cleanup.ContainsOffset(protectedOffset)) continue;
            if (cleanup.TryStartOffset < caught.TryStartOffset || cleanup.TryEndOffset > caught.TryEndOffset ||
                (cleanup.TryStartOffset == caught.TryStartOffset && cleanup.TryEndOffset == caught.TryEndOffset))
                return new(BoundedExceptionDispatchStatus.InvalidNesting);
            if (++count > MaximumCleanups) return new(BoundedExceptionDispatchStatus.CapacityExceeded);
            if (cleanup.HandlerAddress == caught.HandlerAddress || cleanup.HandlerAddress == first)
                return new(BoundedExceptionDispatchStatus.InvalidClause);
            if (count == 1) first = cleanup.HandlerAddress;
            else second = cleanup.HandlerAddress;
        }
        return new((uint)selected, caught.HandlerAddress, count, first, second);
    }
}
