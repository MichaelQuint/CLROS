using System;
using OS.Boot;

namespace OS.Kernel.Runtime;

public enum BoundedExceptionCallerWindowStatus : byte
{
    Uninitialized, Success, CapacityExceeded, OutputTooSmall, StorageOverlap,
    InvalidFrame, ModuleRejected, SnapshotRejected, CatalogRejected, ClauseRejected, UnwindRejected
}

public readonly struct BoundedExceptionCallerWindowResult
{
    internal BoundedExceptionCallerWindowResult(BoundedExceptionCallerWindowStatus status)
    { this = default; Status = status; }
    internal BoundedExceptionCallerWindowResult(uint frames, uint rows)
    { Status = BoundedExceptionCallerWindowStatus.Success; FrameCount = frames; RowCount = rows; }
    public BoundedExceptionCallerWindowStatus Status { get; }
    public bool Succeeded => Status == BoundedExceptionCallerWindowStatus.Success;
    public uint FrameCount { get; }
    public uint RowCount { get; }
}

/// <summary>Assembles one finite physical caller window for the existing bounded search planner.</summary>
/// <remarks>
/// The caller retains the genuine initial context, readable stack, original mapped image,
/// module owner, unwind snapshot and independently reviewed same-image compiler catalog
/// under exclusive stable access. These PODs do not acquire lifetime or prove provenance.
/// Actual RIP is retained for unwinding and continuation; RIP minus one selects the protected
/// call site only. EH lengths come exclusively from the catalog. A no-EH runtime-function
/// extent proves membership only and never supplies an inferred EH contribution length.
/// Only the requested frame slots and first min(rows.Length, MaximumRows) row slots are
/// outputs. Image-resident outputs require separate registered MutableData admission;
/// they must never overlap retained owners, snapshots or readable stack. Distinct storage
/// is checked before writing: overlapping/unadmitted outputs
/// remain untouched. Admitted outputs are cleared on every call and again on late failure;
/// failure result payloads are zero. Output tails remain untouched. No type matching,
/// handler invocation, rooting, collection, processor capture or CPU transfer occurs here.
/// </remarks>
public static unsafe class BoundedExceptionCallerWindow
{
    public const int MaximumRows = BoundedExceptionFrameSearch.MaximumFrames * BoundedExceptionDispatchPlan.MaximumClauses;

    public static BoundedExceptionCallerWindowResult Build(NativeAotLayoutProfile profile,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        byte* moduleOwner, ulong moduleCapacity, ulong moduleHandle,
        byte* unwindSnapshot, ulong unwindCapacity, uint catalogRva, uint catalogBytes,
        byte* stack, ulong stackBytes, X64UnwindContext initial, uint frameCount,
        Span<BoundedExceptionSearchFrame> frames, Span<NativeAotEhClauseResult> rows)
    {
        if (frameCount == 0 || frameCount > BoundedExceptionFrameSearch.MaximumFrames)
            return new(BoundedExceptionCallerWindowStatus.CapacityExceeded);
        if (frames.Length < frameCount) return new(BoundedExceptionCallerWindowStatus.OutputTooSmall);
        int rowCapacity = rows.Length < MaximumRows ? rows.Length : MaximumRows;
        if (mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong imageBase) != AddressValueStatus.Success)
            return new(BoundedExceptionCallerWindowStatus.ModuleRejected);
        fixed (BoundedExceptionSearchFrame* frameStorage = frames)
        fixed (NativeAotEhClauseResult* rowStorage = rows)
        {
            ulong frameBytes = frameCount * (ulong)sizeof(BoundedExceptionSearchFrame);
            ulong rowBytes = (ulong)rowCapacity * (ulong)sizeof(NativeAotEhClauseResult);
            if (!Range((ulong)frameStorage, frameBytes) || (rowBytes != 0 && !Range((ulong)rowStorage, rowBytes)) ||
                Overlap((ulong)frameStorage, frameBytes, (ulong)rowStorage, rowBytes) ||
                !OutputAdmitted((ulong)frameStorage, frameBytes, capture, mapping, imageBase,
                    moduleOwner, moduleCapacity, moduleHandle, unwindSnapshot, unwindCapacity, stack, stackBytes) ||
                !OutputAdmitted((ulong)rowStorage, rowBytes, capture, mapping, imageBase,
                    moduleOwner, moduleCapacity, moduleHandle, unwindSnapshot, unwindCapacity, stack, stackBytes))
                return new(BoundedExceptionCallerWindowStatus.StorageOverlap);
        }
        Clear(frames, frameCount, rows, rowCapacity);
        BoundedExceptionCallerWindowStatus failure = Assemble(profile, capture, mapping, moduleOwner, moduleCapacity,
            moduleHandle, unwindSnapshot, unwindCapacity, catalogRva, catalogBytes, stack, stackBytes,
            initial, frameCount, frames, rows, rowCapacity, imageBase, out uint rowCount);
        if (failure == BoundedExceptionCallerWindowStatus.Success) return new(frameCount, rowCount);
        Clear(frames, frameCount, rows, rowCapacity);
        return new(failure);
    }

    private static BoundedExceptionCallerWindowStatus Assemble(NativeAotLayoutProfile profile,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        byte* owner, ulong ownerBytes, ulong handle, byte* snapshot, ulong snapshotBytes,
        uint catalogRva, uint catalogBytes, byte* stack, ulong stackBytes, X64UnwindContext context,
        uint count, Span<BoundedExceptionSearchFrame> frames, Span<NativeAotEhClauseResult> rows,
        int rowCapacity, ulong imageBase, out uint rowCount)
    {
        rowCount = 0;
        ulong stackStart = (ulong)stack;
        if (!Range(stackStart, stackBytes) || (stackStart & 7) != 0 ||
            Overlap(stackStart, stackBytes, imageBase, capture.Range.ImageSize) ||
            Overlap(stackStart, stackBytes, (ulong)owner, ownerBytes) ||
            Overlap(stackStart, stackBytes, (ulong)snapshot, snapshotBytes))
            return BoundedExceptionCallerWindowStatus.InvalidFrame;
        ulong previousRsp = 0;
        for (uint i = 0; i < count; i++)
        {
            if (context.Rip <= imageBase || context.Rsp <= previousRsp || (context.Rsp & 7) != 0 ||
                context.Rsp < stackStart || context.Rsp - stackStart >= stackBytes)
                return BoundedExceptionCallerWindowStatus.InvalidFrame;
            var function = X64PERuntimeFunctionUnwindMetadataHostileInput.Lookup(snapshot, snapshotBytes, context.Rip);
            var protectedFunction = X64PERuntimeFunctionUnwindMetadataHostileInput.Lookup(snapshot, snapshotBytes, context.Rip - 1);
            if (!function.Succeeded || !protectedFunction.Succeeded || function.ModuleOwner != (ulong)owner ||
                function.ModuleHandle != handle || function.ImageBase != imageBase ||
                protectedFunction.FunctionIndex != function.FunctionIndex || function.ChainDepth != 0 || (function.Flags & 4) != 0)
                return BoundedExceptionCallerWindowStatus.SnapshotRejected;
            if (TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(capture, mapping, owner, ownerBytes,
                handle, imageBase + function.BeginRva, function.EndRva - function.BeginRva,
                TypeManagerRegisteredRangeKind.ExecutableEntry) != TypeManagerModuleStatus.Success)
                return BoundedExceptionCallerWindowStatus.ModuleRejected;
            var method = NativeAotEhClauseMetadata.LookupMethod(profile, capture, mapping, owner, ownerBytes, handle,
                snapshot, snapshotBytes, catalogRva, catalogBytes, context.Rip - 1);
            uint methodRva, codeBytes, clauses;
            if (method.Succeeded)
            {
                methodRva = method.Bounds.RootMethodRva; codeBytes = method.Bounds.CodeBytes; clauses = method.ClauseCount;
                if (clauses > BoundedExceptionDispatchPlan.MaximumClauses)
                    return BoundedExceptionCallerWindowStatus.CapacityExceeded;
                if (clauses > (uint)rowCapacity - rowCount) return BoundedExceptionCallerWindowStatus.OutputTooSmall;
                for (uint j = 0; j < clauses; j++)
                {
                    var row = NativeAotEhClauseMetadata.ReadClause(profile, capture, mapping, owner, ownerBytes, handle,
                        snapshot, snapshotBytes, method.Bounds, j);
                    if (!row.Succeeded || row.Count != clauses || row.Index != j)
                        return BoundedExceptionCallerWindowStatus.ClauseRejected;
                    rows[(int)(rowCount + j)] = row;
                }
            }
            else if (method.Status == NativeAotEhClauseStatus.IndexOutOfRange && (function.Flags & 3) == 0)
            {
                methodRva = function.BeginRva; codeBytes = function.EndRva - function.BeginRva; clauses = 0;
                // Check the NativeAOT root trailer as well as Windows unwind flags.
                // Zero EH bounds forbid decoding an unlisted EH contribution; this
                // function extent is used only to authenticate a no-EH root.
                var noEh = NativeAotEhClauseMetadata.ReadClause(profile, capture, mapping, owner, ownerBytes, handle,
                    snapshot, snapshotBytes, new(methodRva, codeBytes, 0, 0), 0);
                if (noEh.Status != NativeAotEhClauseStatus.NoEhInfo)
                    return BoundedExceptionCallerWindowStatus.CatalogRejected;
            }
            else return BoundedExceptionCallerWindowStatus.CatalogRejected;
            ulong offset = context.Rip - 1 - imageBase - methodRva;
            if (codeBytes == 0 || codeBytes > BoundedExceptionFrameSearch.MaximumCodeBytes || offset >= codeBytes)
                return BoundedExceptionCallerWindowStatus.InvalidFrame;
            frames[(int)i] = new(context, (uint)offset, codeBytes, rowCount, clauses);
            rowCount += clauses;
            previousRsp = context.Rsp;
            if (i + 1 < count)
            {
                var step = X64FrameUnwind.UnwindOne(profile, capture, mapping, owner, ownerBytes, handle,
                    snapshot, snapshotBytes, stack, stackBytes, context);
                if (!step.Succeeded) return BoundedExceptionCallerWindowStatus.UnwindRejected;
                context = step.Caller;
            }
        }
        return BoundedExceptionCallerWindowStatus.Success;
    }

    private static void Clear(Span<BoundedExceptionSearchFrame> frames, uint count,
        Span<NativeAotEhClauseResult> rows, int rowCount)
    {
        for (int i = 0; i < count; i++) frames[i] = default;
        for (int i = 0; i < rowCount; i++) rows[i] = default;
    }
    private static bool OutputAdmitted(ulong start, ulong bytes, EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, ulong imageBase, byte* owner, ulong ownerBytes, ulong handle,
        byte* snapshot, ulong snapshotBytes, byte* stack, ulong stackBytes)
    {
        if (bytes == 0) return true;
        if (Overlap(start, bytes, (ulong)owner, ownerBytes) || Overlap(start, bytes, (ulong)snapshot, snapshotBytes) ||
            Overlap(start, bytes, (ulong)stack, stackBytes)) return false;
        return !Overlap(start, bytes, imageBase, capture.Range.ImageSize) ||
            TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(capture, mapping, owner, ownerBytes,
                handle, start, bytes, TypeManagerRegisteredRangeKind.MutableData) == TypeManagerModuleStatus.Success;
    }
    private static bool Range(ulong start, ulong bytes) => start != 0 && bytes != 0 && bytes <= ulong.MaxValue - start;
    private static bool Overlap(ulong start, ulong bytes, ulong other, ulong otherBytes) =>
        bytes != 0 && otherBytes != 0 && (!Range(start, bytes) || !Range(other, otherBytes) ||
        (start < other + otherBytes && other < start + bytes));
}
