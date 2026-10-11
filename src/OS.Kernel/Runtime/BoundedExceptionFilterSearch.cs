using System;

namespace OS.Kernel.Runtime;

public enum BoundedExceptionFilterSearchStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    FilterRequired = 2,
    Empty = 3,
    CapacityExceeded = 4,
    InvalidClause = 5,
    UnsupportedClause = 6,
    InvalidTopology = 7,
    InvalidReceipt = 8,
    NoMatch = 9
}

/// <summary>A caller-supplied result of one synchronous original filter invocation.</summary>
/// <remarks>Default means pending. Numeric fields do not authenticate invocation or exception identity.</remarks>
public readonly struct BoundedExceptionFilterReceipt
{
    public BoundedExceptionFilterReceipt(uint clauseIndex, ulong filterAddress, int returnValue)
    { HasValue = true; ClauseIndex = clauseIndex; FilterAddress = filterAddress; ReturnValue = returnValue; }
    public bool HasValue { get; }
    public uint ClauseIndex { get; }
    public ulong FilterAddress { get; }
    public int ReturnValue { get; }
}

public readonly struct BoundedExceptionFilterSearchResult
{
    internal BoundedExceptionFilterSearchResult(BoundedExceptionFilterSearchStatus status)
    { this = default; Status = status; }
    internal BoundedExceptionFilterSearchResult(BoundedExceptionFilterSearchStatus status, uint index, ulong target)
    {
        this = default; Status = status; ClauseIndex = index;
        if (status == BoundedExceptionFilterSearchStatus.FilterRequired) FilterAddress = target;
        else if (status == BoundedExceptionFilterSearchStatus.Success) HandlerAddress = target;
    }
    public BoundedExceptionFilterSearchStatus Status { get; }
    public bool Succeeded => Status == BoundedExceptionFilterSearchStatus.Success;
    public bool RequiresFilter => Status == BoundedExceptionFilterSearchStatus.FilterRequired;
    public uint ClauseIndex { get; }
    public ulong FilterAddress { get; }
    public ulong HandlerAddress { get; }
}

/// <summary>Ordered first-pass decisions for at most four same-range compiler filter rows.</summary>
/// <remarks>
/// Caller supplies every physical row decoded from the SAME authenticated original method and
/// binds completed receipts to actual calls with the SAME retained exception and parent context.
/// Module, object root, stack and compiler target authority must remain stable. This helper neither
/// authenticates that authority nor invokes filters or transfers control. Only filter siblings over
/// one nonempty half-open range are supported; typed catches, cleanups and boundary markers reject.
/// Receipts form a completed prefix ending at the first true result. Zero is false; any signed
/// nonzero result is true. Every tail row and receipt is checked before publishing a target.
/// </remarks>
public static class BoundedExceptionFilterSearch
{
    public const int MaximumClauses = 4;

    public static BoundedExceptionFilterSearchResult Select(ReadOnlySpan<NativeAotEhClauseResult> clauses,
        ReadOnlySpan<BoundedExceptionFilterReceipt> receipts, uint protectedOffset)
    {
        if (clauses.Length == 0) return new(BoundedExceptionFilterSearchStatus.Empty);
        if (clauses.Length > MaximumClauses) return new(BoundedExceptionFilterSearchStatus.CapacityExceeded);
        if (receipts.Length != clauses.Length) return new(BoundedExceptionFilterSearchStatus.InvalidReceipt);

        for (int i = 0; i < clauses.Length; i++)
        {
            var row = clauses[i];
            if (!row.Succeeded || row.Index != (uint)i || row.Count != (uint)clauses.Length)
                return new(BoundedExceptionFilterSearchStatus.InvalidClause);
            if (row.IsTryGroupBoundary || row.Kind != NativeAotEhClauseKind.Filter)
                return new(BoundedExceptionFilterSearchStatus.UnsupportedClause);
            if (row.TryStartOffset >= row.TryEndOffset || row.HandlerAddress == 0 || row.FilterAddress == 0 || row.TypeAddress != 0)
                return new(BoundedExceptionFilterSearchStatus.InvalidClause);
            if (row.TryStartOffset != clauses[0].TryStartOffset || row.TryEndOffset != clauses[0].TryEndOffset)
                return new(BoundedExceptionFilterSearchStatus.InvalidTopology);
        }

        int pending = -1, selected = -1;
        for (int i = 0; i < receipts.Length; i++)
        {
            var receipt = receipts[i];
            if (receipt.HasValue)
            {
                if (pending >= 0 || selected >= 0 || receipt.ClauseIndex != (uint)i ||
                    receipt.FilterAddress != clauses[i].FilterAddress)
                    return new(BoundedExceptionFilterSearchStatus.InvalidReceipt);
                if (receipt.ReturnValue != 0) selected = i;
            }
            else
            {
                if (receipt.ClauseIndex != 0 || receipt.FilterAddress != 0 || receipt.ReturnValue != 0)
                    return new(BoundedExceptionFilterSearchStatus.InvalidReceipt);
                if (pending < 0 && selected < 0) pending = i;
            }
        }
        if (!clauses[0].ContainsOffset(protectedOffset)) return new(BoundedExceptionFilterSearchStatus.NoMatch);
        if (selected >= 0) return new(BoundedExceptionFilterSearchStatus.Success,
            (uint)selected, clauses[selected].HandlerAddress);
        if (pending >= 0) return new(BoundedExceptionFilterSearchStatus.FilterRequired,
            (uint)pending, clauses[pending].FilterAddress);
        return new(BoundedExceptionFilterSearchStatus.NoMatch);
    }
}
