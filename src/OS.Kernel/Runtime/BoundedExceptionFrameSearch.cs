using System;

namespace OS.Kernel.Runtime;

public enum BoundedExceptionFrameSearchStatus : byte
{
    Uninitialized, Success, Empty, CapacityExceeded, OutputTooSmall, InvalidFrame,
    InvalidSlice, InvalidClauseRange, DispatchRejected, DuplicateCleanup, NoMatchingCatch
}

/// <summary>One caller-authenticated physical frame and its contiguous decoded EH rows.</summary>
public readonly struct BoundedExceptionSearchFrame
{
    public BoundedExceptionSearchFrame(X64UnwindContext context, uint protectedOffset, uint codeBytes,
        uint rowOffset, uint rowCount)
    { Context = context; ProtectedOffset = protectedOffset; CodeBytes = codeBytes; RowOffset = rowOffset; RowCount = rowCount; }
    public X64UnwindContext Context { get; }
    public uint ProtectedOffset { get; }
    public uint CodeBytes { get; }
    public uint RowOffset { get; }
    public uint RowCount { get; }
}

public readonly struct BoundedExceptionCleanupEntry
{
    internal BoundedExceptionCleanupEntry(uint frameIndex, ulong handler, X64UnwindContext context)
    { FrameIndex = frameIndex; Handler = handler; Context = context; }
    public uint FrameIndex { get; }
    public ulong Handler { get; }
    public X64UnwindContext Context { get; }
}

public readonly struct BoundedExceptionFrameSearchResult
{
    internal BoundedExceptionFrameSearchResult(BoundedExceptionFrameSearchStatus status,
        BoundedExceptionDispatchStatus dispatchStatus = default)
    { this = default; Status = status; DispatchStatus = dispatchStatus; }
    internal BoundedExceptionFrameSearchResult(uint frameIndex, BoundedExceptionDispatchResult plan,
        X64UnwindContext context, uint cleanupCount)
    {
        Status = BoundedExceptionFrameSearchStatus.Success; DispatchStatus = BoundedExceptionDispatchStatus.Success;
        CatchFrameIndex = frameIndex; CatchClauseIndex = plan.CatchIndex; CatchHandler = plan.CatchHandler;
        CatchContext = context; CleanupCount = cleanupCount;
    }
    public BoundedExceptionFrameSearchStatus Status { get; }
    public BoundedExceptionDispatchStatus DispatchStatus { get; }
    public bool Succeeded => Status == BoundedExceptionFrameSearchStatus.Success;
    public uint CatchFrameIndex { get; }
    public uint CatchClauseIndex { get; }
    public ulong CatchHandler { get; }
    public X64UnwindContext CatchContext { get; }
    public uint CleanupCount { get; }
}

/// <summary>Bounded first-pass search and exceptional cleanup planning across physical frames.</summary>
/// <remarks>
/// The caller retains original module/root/index authority, authentic native contexts, every
/// physical row and successful typed-match receipts for the SAME live exception. Method start
/// is derived from Rip - 1 - ProtectedOffset, not authenticated here. RIP is byte-granular;
/// RSP must be nonzero, eight-byte aligned and strictly increasing. Zero rows denote no EH.
/// The existing per-method planner's unsupported topology and lifetime restrictions apply.
/// At most two containing cleanup targets per frame are admitted, including unmatched/tail
/// frames. Repeated handlers in distinct progressing frames are legitimate recursion.
/// Inputs must remain stable and be distinct from the caller-owned output. Only its first
/// eight entries are output; these are cleared on every call, including failure/no match.
/// This helper neither roots/authenticates an object or frame nor invokes/transfers to a target.
/// A no-match result never authorizes second-pass cleanup or an unhandled CPU transfer.
/// </remarks>
public static class BoundedExceptionFrameSearch
{
    public const int MaximumFrames = 4;
    public const int MaximumCleanupEntries = 8;
    public const uint MaximumCodeBytes = 0x7FFF_FFFF;

    public static BoundedExceptionFrameSearchResult Select(ReadOnlySpan<BoundedExceptionSearchFrame> frames,
        ReadOnlySpan<NativeAotEhClauseResult> rows, ReadOnlySpan<ExceptionTypeMatchResult> matches,
        Span<BoundedExceptionCleanupEntry> cleanups)
    {
        int clearCount = cleanups.Length < MaximumCleanupEntries ? cleanups.Length : MaximumCleanupEntries;
        for (int i = 0; i < clearCount; i++) cleanups[i] = default;
        if (cleanups.Length < MaximumCleanupEntries) return new(BoundedExceptionFrameSearchStatus.OutputTooSmall);
        if (frames.Length == 0) return new(BoundedExceptionFrameSearchStatus.Empty);
        if (frames.Length > MaximumFrames || rows.Length > MaximumFrames * BoundedExceptionDispatchPlan.MaximumClauses)
            return new(BoundedExceptionFrameSearchStatus.CapacityExceeded);
        if (matches.Length != rows.Length)
            return new(BoundedExceptionFrameSearchStatus.DispatchRejected, BoundedExceptionDispatchStatus.InvalidTypeMatch);

        int nextRow = 0, selectedFrame = -1;
        ulong previousRsp = 0;
        BoundedExceptionDispatchResult selectedPlan = default;
        // Validate every frame and every receipt, even after finding an inner catch.
        for (int i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            if (frame.Context.Rip == 0 || frame.Context.Rsp == 0 || (frame.Context.Rsp & 7) != 0 ||
                frame.Context.Rsp <= previousRsp || frame.CodeBytes == 0 || frame.CodeBytes > MaximumCodeBytes ||
                frame.ProtectedOffset >= frame.CodeBytes || frame.Context.Rip <= (ulong)frame.ProtectedOffset + 1)
                return new(BoundedExceptionFrameSearchStatus.InvalidFrame);
            ulong method = frame.Context.Rip - 1 - frame.ProtectedOffset;
            if (frame.CodeBytes > ulong.MaxValue - method) return new(BoundedExceptionFrameSearchStatus.InvalidFrame);
            ulong end = method + frame.CodeBytes;
            previousRsp = frame.Context.Rsp;
            if (frame.RowCount > BoundedExceptionDispatchPlan.MaximumClauses)
                return new(BoundedExceptionFrameSearchStatus.CapacityExceeded);
            if (frame.RowOffset != (uint)nextRow || frame.RowCount > (uint)(rows.Length - nextRow))
                return new(BoundedExceptionFrameSearchStatus.InvalidSlice);
            int count = (int)frame.RowCount;
            if (count != 0)
            {
                var plan = BoundedExceptionDispatchPlan.Select(rows.Slice(nextRow, count), matches.Slice(nextRow, count), frame.ProtectedOffset);
                if (!plan.Succeeded && plan.Status != BoundedExceptionDispatchStatus.NoMatchingCatch)
                    return new(plan.Status == BoundedExceptionDispatchStatus.CapacityExceeded
                        ? BoundedExceptionFrameSearchStatus.CapacityExceeded : BoundedExceptionFrameSearchStatus.DispatchRejected, plan.Status);
                uint cleanupCount = 0;
                ulong firstHandler = 0;
                for (int j = 0; j < count; j++)
                {
                    var row = rows[nextRow + j];
                    if (row.TryEndOffset > frame.CodeBytes || row.HandlerAddress < method || row.HandlerAddress >= end)
                        return new(BoundedExceptionFrameSearchStatus.InvalidClauseRange);
                    if (row.Kind != NativeAotEhClauseKind.FaultOrFinally || !row.ContainsOffset(frame.ProtectedOffset)) continue;
                    if (++cleanupCount > BoundedExceptionDispatchPlan.MaximumCleanups)
                        return new(BoundedExceptionFrameSearchStatus.CapacityExceeded);
                    if (row.HandlerAddress == firstHandler) return new(BoundedExceptionFrameSearchStatus.DuplicateCleanup);
                    firstHandler = row.HandlerAddress;
                }
                if (selectedFrame < 0 && plan.Succeeded) { selectedFrame = i; selectedPlan = plan; }
            }
            nextRow += count;
        }
        if (nextRow != rows.Length) return new(BoundedExceptionFrameSearchStatus.InvalidSlice);
        if (selectedFrame < 0) return new(BoundedExceptionFrameSearchStatus.NoMatchingCatch);

        uint written = 0;
        for (int i = 0; i <= selectedFrame; i++)
        {
            var frame = frames[i];
            if (i == selectedFrame)
            {
                if (selectedPlan.CleanupCount != 0)
                    cleanups[(int)written++] = new((uint)i, selectedPlan.FirstCleanup, frame.Context);
                if (selectedPlan.CleanupCount == 2)
                    cleanups[(int)written++] = new((uint)i, selectedPlan.SecondCleanup, frame.Context);
            }
            else for (int j = 0; j < (int)frame.RowCount; j++)
            {
                var row = rows[(int)frame.RowOffset + j];
                if (row.Kind == NativeAotEhClauseKind.FaultOrFinally && row.ContainsOffset(frame.ProtectedOffset))
                    cleanups[(int)written++] = new((uint)i, row.HandlerAddress, frame.Context);
            }
        }
        return new((uint)selectedFrame, selectedPlan, frames[selectedFrame].Context, written);
    }
}
