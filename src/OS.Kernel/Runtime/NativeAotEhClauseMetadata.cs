using OS.Boot;
using Layout = OS.Kernel.Runtime.ObjectArrayStringEETypeMethodTableLayoutContracts;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

namespace OS.Kernel.Runtime;
/// <summary>
/// Original C# reader for the Windows NativeAOT EH format at Layout.RuntimeSourceCommit
/// (ILC 11.0.0 RC2 Win64): unwind trailer flags, optional associated-data RVA, EH RVA,
/// and compressed count/start/length-kind/handler, plus fixed type RVA or compressed filter.
/// Fault and finally share kind1; physical ordering and empty try-group markers are retained.
/// </summary>
/// <remarks>
/// Caller retains exclusively stable, readable original mapped image, TypeManager registration
/// and unwind snapshot for the entire call. Numeric admission is not a mapping probe or compiler
/// provenance proof. Exact method/EH object contributions must come from the SAME IMAGE's
/// ILC/COFF evidence, including folded aliases; a section tail, next symbol, root row end or
/// arbitrary readable interval is not that evidence. Neither PE nor this stream stores EhBytes.
/// No persistent EH snapshot, ownership transfer, rooting, invocation, search, exception object,
/// register restoration, stack write or GC-data parsing is provided. Cross-module targets,
/// chained unwind, reverse-PInvoke, unknown kinds/flags and split code are unsupported.
/// The bounded unsigned decoder follows the pinned permissive runtime reader, including
/// nonminimal widths. The compiler normally emits minimal widths; this is not LEB128.
/// </remarks>

public static unsafe partial class NativeAotEhClauseMetadata
{
    // CLROS policy limits, not NativeAOT encoding limits.
    public const uint MaximumClauses = 64;
    public const uint MaximumEhBytes = 4096;
    public static NativeAotEhClauseResult ReadClause(NativeAotLayoutProfile profile, EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, byte* moduleOwner, ulong moduleCapacity, ulong moduleHandle, byte* unwindSnapshot, ulong unwindCapacity, NativeAotEhMethodBounds bounds, uint index)
    {
        if (profile != NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian)
            return new(NativeAotEhClauseStatus.UnsupportedProfile);
        // Authenticate the supplied original owner before using any image payload.
        if (!TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(capture, mapping, moduleOwner, moduleCapacity, moduleHandle, 204).Succeeded || mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong address) != AddressValueStatus.Success)
            return new(NativeAotEhClauseStatus.ModuleRejected);
        Image image = new()
        {
            Capture = capture,
            Mapping = mapping,
            Owner = moduleOwner,
            Capacity = moduleCapacity,
            Handle = moduleHandle,
            Base = address,
            Bytes = capture.Range.ImageSize
        };
        if (!Range(image, bounds.RootMethodRva, bounds.CodeBytes, TypeManagerRegisteredRangeKind.ExecutableEntry))
            return new(NativeAotEhClauseStatus.BoundsRejected);
        var root = Unwind.Lookup(unwindSnapshot, unwindCapacity, address + bounds.RootMethodRva);
        if (!root.Succeeded || !Identity(image, root))
            return new(NativeAotEhClauseStatus.SnapshotRejected);
        if (root.BeginRva != bounds.RootMethodRva || root.EndRva - root.BeginRva > bounds.CodeBytes)
            return new(NativeAotEhClauseStatus.BoundsRejected);
        NativeAotEhClauseStatus status = Trailer(image, root, 0, out byte flags, out uint trailerRva);
        if (status != NativeAotEhClauseStatus.Success)
            return new(status);
        uint trailerBytes = 1U + ((flags & 16) != 0 ? 4U : 0U) + ((flags & 4) != 0 ? 4U : 0U);
        if (!Range(image, trailerRva, trailerBytes, TypeManagerRegisteredRangeKind.ImmutableMetadata))
            return new(NativeAotEhClauseStatus.MetadataRejected);
        if ((flags & 4) == 0)
            return new(bounds.EhRva == 0 && bounds.EhBytes == 0 ? NativeAotEhClauseStatus.NoEhInfo : NativeAotEhClauseStatus.BoundsRejected);
        uint discovered = U32((byte*)(address + trailerRva + 1 + ((flags & 16) != 0 ? 4U : 0U)));
        // The pinned manager reads an int32 image-relative value; negative RVAs are unsupported.
        if (discovered == 0 || discovered > 0x7FFFFFFFU || discovered != bounds.EhRva || bounds.EhBytes == 0)
            return new(NativeAotEhClauseStatus.BoundsRejected);
        if (bounds.EhBytes > MaximumEhBytes)
            return new(NativeAotEhClauseStatus.CapacityExceeded);
        if (!Range(image, bounds.EhRva, bounds.EhBytes, TypeManagerRegisteredRangeKind.ImmutableMetadata))
            return new(NativeAotEhClauseStatus.MetadataRejected);
        byte* payload = (byte*)(address + bounds.EhRva);
        uint cursor = 0;
        if (!Unsigned(payload, bounds.EhBytes, ref cursor, out uint count))
            return new(NativeAotEhClauseStatus.MetadataRejected);
        if (count > MaximumClauses)
            return new(NativeAotEhClauseStatus.CapacityExceeded);
        NativeAotEhClauseResult selected = default;
        uint lastTargetIndex = root.FunctionIndex;
        for (uint i = 0; i < count; i++)
        {
            if (!Unsigned(payload, bounds.EhBytes, ref cursor, out uint start) || !Unsigned(payload, bounds.EhBytes, ref cursor, out uint packed))
                return new(NativeAotEhClauseStatus.MetadataRejected);
            uint kind = packed & 3;
            if (kind == 3)
                return new(NativeAotEhClauseStatus.UnsupportedFormat);
            if (!Unsigned(payload, bounds.EhBytes, ref cursor, out uint handlerOffset))
                return new(NativeAotEhClauseStatus.MetadataRejected);
            ulong end = (ulong)start + (packed >> 2);
            if (end > bounds.CodeBytes)
                return new(NativeAotEhClauseStatus.BoundsRejected);
            bool boundary = start == 0 && packed == 1 && handlerOffset == 0;
            ulong handler = 0, filter = 0, type = 0;
            if (!boundary)
            {
                status = Target(image, unwindSnapshot, unwindCapacity, bounds, handlerOffset, 1, out handler, out uint handlerIndex);
                if (status != NativeAotEhClauseStatus.Success)
                    return new(status);
                if (handlerIndex > lastTargetIndex)
                    lastTargetIndex = handlerIndex;
            }

            if (kind == 0)
            {
                if (bounds.EhBytes - cursor < 4)
                    return new(NativeAotEhClauseStatus.MetadataRejected);
                uint typeRva = U32(payload + cursor);
                cursor += 4;
                if ((typeRva & 7) != 0 || !Range(image, typeRva, Layout.MethodTablePrefixBytes, TypeManagerRegisteredRangeKind.ImmutableMetadata))
                    return new(NativeAotEhClauseStatus.TargetRejected);
                type = address + typeRva;
            }
            else if (kind == 2)
            {
                if (!Unsigned(payload, bounds.EhBytes, ref cursor, out uint filterOffset))
                    return new(NativeAotEhClauseStatus.MetadataRejected);
                status = Target(image, unwindSnapshot, unwindCapacity, bounds, filterOffset, 2, out filter, out uint filterIndex);
                if (status != NativeAotEhClauseStatus.Success)
                    return new(status);
                if (filterIndex > lastTargetIndex)
                    lastTargetIndex = filterIndex;
            }

            if (i == index)
                selected = new(i, count, start, (uint)end, (NativeAotEhClauseKind)kind, handler, filter, type, boundary);
        }

        // Even an early requested row or an out-of-range index never bypasses late validation.
        if (cursor != bounds.EhBytes)
            return new(NativeAotEhClauseStatus.MetadataRejected);
        // The pinned manager associates funclets with the nearest preceding root in pdata.
        // One finite pass covers every referenced target, without repeating ancestry walks
        // for equal-range clauses or exposing a row before this final ownership check.
        for (uint i = root.FunctionIndex; i < lastTargetIndex;)
        {
            var function = Unwind.LookupFunction(unwindSnapshot, unwindCapacity, ++i);
            if (!function.Succeeded || !Identity(image, function))
                return new(NativeAotEhClauseStatus.SnapshotRejected);
            if (function.BeginRva < bounds.RootMethodRva || (ulong)function.EndRva > (ulong)bounds.RootMethodRva + bounds.CodeBytes)
                return new(NativeAotEhClauseStatus.TargetRejected);
            status = Trailer(image, function, 255, out byte funcletFlags, out _);
            if (status != NativeAotEhClauseStatus.Success)
                return new(status);
            if ((funcletFlags & 3) == 0)
                return new(NativeAotEhClauseStatus.TargetRejected);
            if (funcletFlags != (funcletFlags & 3))
                return new(NativeAotEhClauseStatus.UnsupportedFormat);
        }

        return index < count ? selected : new(NativeAotEhClauseStatus.IndexOutOfRange);
    }

    private static NativeAotEhClauseStatus Target(Image image, byte* snapshot, ulong capacity, NativeAotEhMethodBounds bounds, uint offset, byte kind, out ulong target, out uint functionIndex)
    {
        target = 0;
        functionIndex = 0;
        if (offset >= bounds.CodeBytes)
            return NativeAotEhClauseStatus.TargetRejected;
        ulong rva = (ulong)bounds.RootMethodRva + offset;
        if (!Range(image, rva, 1, TypeManagerRegisteredRangeKind.ExecutableEntry))
            return NativeAotEhClauseStatus.TargetRejected;
        var function = Unwind.Lookup(snapshot, capacity, image.Base + rva);
        if (!function.Succeeded || !Identity(image, function))
            return NativeAotEhClauseStatus.SnapshotRejected;
        if (function.BeginRva != rva || (ulong)function.EndRva > (ulong)bounds.RootMethodRva + bounds.CodeBytes)
            return NativeAotEhClauseStatus.TargetRejected;
        NativeAotEhClauseStatus status = Trailer(image, function, kind, out byte flags, out _);
        if (status != NativeAotEhClauseStatus.Success)
            return status;
        // The pinned writer places optional EH/associated data only on the root.
        if (flags != kind)
            return NativeAotEhClauseStatus.UnsupportedFormat;
        target = image.Base + rva;
        functionIndex = function.FunctionIndex;
        return NativeAotEhClauseStatus.Success;
    }

    private static NativeAotEhClauseStatus Trailer(Image image, X64UnwindMetadataResult function, byte expectedKind, out byte flags, out uint rva)
    {
        flags = 0;
        rva = 0;
        if (function.ChainDepth != 0 || (function.Flags & 4) != 0)
            return NativeAotEhClauseStatus.UnsupportedFormat;
        ulong size = 4UL + (ulong)function.CodeSlots * 2;
        if ((function.Flags & 3) != 0)
            size = ((size + 3) & ~3UL) + 4;
        ulong start = (ulong)function.UnwindRva + size;
        if (start > 0xFFFFFFFFUL || !Range(image, start, 1, TypeManagerRegisteredRangeKind.ImmutableMetadata))
            return NativeAotEhClauseStatus.MetadataRejected;
        flags = *(byte*)(image.Base + start);
        rva = (uint)start;
        if ((flags & ~31) != 0 || (flags & 8) != 0 || (flags & 3) == 3 || (expectedKind != 255 && (flags & 3) != expectedKind))
            return NativeAotEhClauseStatus.UnsupportedFormat;
        return NativeAotEhClauseStatus.Success;
    }

    private static bool Identity(Image image, X64UnwindMetadataResult result) => result.ImageBase == image.Base && result.ModuleOwner == (ulong)image.Owner && result.ModuleHandle == image.Handle;
    private static bool Range(Image image, ulong rva, ulong bytes, TypeManagerRegisteredRangeKind kind) => rva != 0 && bytes != 0 && rva < image.Bytes && bytes <= image.Bytes - rva && TypeManagerModuleMetadataLiveNativeAOTImage.TryValidateRegisteredRange(image.Capture, image.Mapping, image.Owner, image.Capacity, image.Handle, image.Base + rva, bytes, kind) == TypeManagerModuleStatus.Success;
    private static bool Unsigned(byte* data, uint length, ref uint cursor, out uint value)
    {
        value = 0;
        if (cursor >= length)
            return false;
        byte first = data[cursor];
        uint width = (first & 1) == 0 ? 1U : (first & 2) == 0 ? 2U : (first & 4) == 0 ? 3U : (first & 8) == 0 ? 4U : 5U;
        if (width > length - cursor)
            return false;
        byte* p = data + cursor;
        value = width == 1 ? (uint)(first >> 1) : width == 2 ? (uint)(first >> 2 | p[1] << 6) : width == 3 ? (uint)(first >> 3 | p[1] << 5 | p[2] << 13) : width == 4 ? (uint)(first >> 4 | p[1] << 4 | p[2] << 12 | p[3] << 20) : U32(p + 1);
        cursor += width;
        return true;
    }

    private static uint U32(byte* p) => (uint)p[0] | (uint)p[1] << 8 | (uint)p[2] << 16 | (uint)p[3] << 24;
}
