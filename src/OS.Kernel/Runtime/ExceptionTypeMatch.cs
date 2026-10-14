using OS.Boot;
using Layout = OS.Kernel.Runtime.ObjectArrayStringEETypeMethodTableLayoutContracts;

namespace OS.Kernel.Runtime;

public enum ExceptionTypeMatchStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    UnsupportedProfile = 2,
    ObjectRejected = 3,
    ModuleRejected = 4,
    MetadataRejected = 5,
    UnsupportedType = 6,
    HierarchyCycle = 7,
    DepthExceeded = 8
}

public readonly struct ExceptionTypeMatchResult
{
    internal ExceptionTypeMatchResult(ExceptionTypeMatchStatus status, bool matches = false)
    {
        Status = status;
        Matches = status == ExceptionTypeMatchStatus.Success && matches;
    }

    public ExceptionTypeMatchStatus Status { get; }
    public bool Succeeded => Status == ExceptionTypeMatchStatus.Success;
    public bool Matches { get; }
}

/// <summary>
/// Bounded class-identity predicate, not an exception runtime export. Semantics
/// derive from Runtime.Base TypeCast.IsInstanceOfException and Common
/// MethodTable.NonArrayBaseType at Layout.RuntimeSourceCommit (ILC 11.0.0 RC2 x64).
/// Only canonical, non-generic fixed classes admitted by the layout contract,
/// with no extended flags, are supported. Implemented interfaces and optional
/// vtable/dispatch tails are not traversed; interface targets are unsupported.
/// </summary>
/// <remarks>
/// Caller retains the original mapped image, registration storage, heap owners,
/// backing and allocation receipt, with exclusive stable access for the call.
/// Type identities must originate from compiler metadata in that image. Range
/// admission cannot authenticate arbitrary bytes as compiler-emitted types, prove
/// OS mapping, prevent forged POD authority, or detect metadata changed before
/// this call. No snapshot, rooting, ownership transfer or lifetime extension is
/// supplied. Cross-module hierarchies and general assignability are unsupported.
/// Success does not establish that either class derives from System.Exception.
/// Collecting-mode receipts are checked against the current allocation identity,
/// rejecting swept/replaced receipts even after address/MT reuse. Noncollecting
/// ownership has no free operation; external reset/free/reuse is outside its
/// contract. Neither mode keeps an object alive after this call.
/// The related-type field is the direct absolute pointer read by the pinned
/// NonArrayBaseType accessor, including when other optional fields use relative
/// pointers. No imported indirection cell or tagged/relative encoding is decoded;
/// each nonzero pointer must itself admit a same-image immutable class prefix.
/// </remarks>
public static unsafe class ExceptionTypeMatch
{
    public const int MaximumHierarchyDepth = 32;

    /// <summary>
    /// After profile admission, null returns an admitted nonmatch without reading
    /// any authority or target. A nonnull reference must equal the original live
    /// allocation receipt. Both target and object chains are validated through
    /// their null base (at most 32 types each), even after identity matches, so a
    /// later malformed ancestor cannot expose a partial successful result.
    /// </summary>
    public static ExceptionTypeMatchResult Match(NativeAotLayoutProfile profile,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        byte* moduleOwner, ulong moduleCapacity, ulong moduleHandle,
        RhpNewFastHeapAccess heap, RhpNewFastBumpState* allocator, ulong allocatorCapacity,
        RhpNewFastResult receipt, ulong objectReference, ulong targetType)
    {
        if (profile != NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian)
            return new(ExceptionTypeMatchStatus.UnsupportedProfile);
        if (objectReference == 0) return new(ExceptionTypeMatchStatus.Success);
        if (objectReference != receipt.ObjectReference ||
            GuestTestRhpNewFast.TryValidateRetainedAllocation(heap, allocator, allocatorCapacity, receipt)
                != RhpNewFastStatus.Success)
            return new(ExceptionTypeMatchStatus.ObjectRejected);

        ExceptionTypeMatchStatus status = Walk(capture, mapping, moduleOwner, moduleCapacity,
            moduleHandle, targetType, 0, 0, out _);
        if (status != ExceptionTypeMatchStatus.Success) return new(status);

        // Retained-allocation validation read the actual object MT slot only
        // after checking its backing, and proved it equals this receipt token.
        status = Walk(capture, mapping, moduleOwner, moduleCapacity, moduleHandle,
            receipt.MethodTableToken, targetType, receipt.BytesConsumed, out bool matches);
        return new(status, matches);
    }

    private static ExceptionTypeMatchStatus Walk(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong capacity, ulong handle,
        ulong current, ulong target, ulong allocationBytes, out bool matches)
    {
        matches = false;
        if (current == 0) return ExceptionTypeMatchStatus.MetadataRejected;
        ulong* seen = stackalloc ulong[MaximumHierarchyDepth];
        int count = 0;
        bool found = false;
        while (current != 0)
        {
            for (int i = 0; i < count; i++)
                if (seen[i] == current) return ExceptionTypeMatchStatus.HierarchyCycle;
            if (count == MaximumHierarchyDepth) return ExceptionTypeMatchStatus.DepthExceeded;
            ExceptionTypeMatchStatus status = ReadClass(capture, mapping, owner, capacity,
                handle, current, out NativeAotMethodTablePrefix prefix);
            if (status != ExceptionTypeMatchStatus.Success) return status;
            if (count == 0 && allocationBytes != 0 && prefix.BaseSize != allocationBytes)
                return ExceptionTypeMatchStatus.ObjectRejected;
            seen[count++] = current;
            if (current == target) found = true;
            current = prefix.RelatedTypeToken;
        }
        matches = found;
        return ExceptionTypeMatchStatus.Success;
    }

    private static ExceptionTypeMatchStatus ReadClass(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong capacity, ulong handle,
        ulong address, out NativeAotMethodTablePrefix prefix)
    {
        prefix = default;
        if ((address & 7) != 0) return ExceptionTypeMatchStatus.MetadataRejected;
        TypeManagerModuleStatus range = TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(
            capture, mapping, owner, capacity, handle, address, Layout.MethodTablePrefixBytes,
            TypeManagerRegisteredRangeKind.ImmutableMetadata);
        if (range != TypeManagerModuleStatus.Success)
            return range == TypeManagerModuleStatus.RangeRejected || range == TypeManagerModuleStatus.RangePermissionRejected
                ? ExceptionTypeMatchStatus.MetadataRejected : ExceptionTypeMatchStatus.ModuleRejected;

        // The whole 24-byte prefix has read-only, non-executable image authority
        // before any field is read. RelatedTypeToken is an absolute base pointer
        // only for the admitted canonical class shape, never a relative pointer.
        byte* p = (byte*)address;
        NativeAotMethodTablePrefix candidate = new(U32(p), U32(p + 4), U64(p + 8),
            U16(p + 16), U16(p + 18), U32(p + 20));
        NativeAotLayoutResult layout = Layout.Evaluate(NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian,
            candidate, 0, ulong.MaxValue);
        if (layout.Status == NativeAotLayoutStatus.UnsupportedShape ||
            layout.Status == NativeAotLayoutStatus.UnsupportedTypeFeatures)
            return ExceptionTypeMatchStatus.UnsupportedType;
        if (!layout.Succeeded) return ExceptionTypeMatchStatus.MetadataRejected;
        if (layout.Shape != NativeAotLayoutShape.FixedObject || (candidate.Flags & 0xFFFF) != 0)
            return ExceptionTypeMatchStatus.UnsupportedType;
        prefix = candidate;
        return ExceptionTypeMatchStatus.Success;
    }

    private static ushort U16(byte* p) => (ushort)(p[0] | p[1] << 8);
    private static uint U32(byte* p) => (uint)(p[0] | p[1] << 8 | p[2] << 16 | p[3] << 24);
    private static ulong U64(byte* p) => U32(p) | (ulong)U32(p + 4) << 32;
}
