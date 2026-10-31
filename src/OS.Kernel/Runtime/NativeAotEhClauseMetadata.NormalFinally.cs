using OS.Boot;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

namespace OS.Kernel.Runtime;

public enum NativeAotNormalFinallyStatus : byte
{
    Uninitialized, Success, UnsupportedProfile, ModuleRejected, FuncletRejected,
    UnwindRejected, ParentRejected, CatalogRejected, ClauseRejected, AmbiguousClause
}

public readonly struct NativeAotNormalFinallyResult
{
    internal NativeAotNormalFinallyResult(NativeAotNormalFinallyStatus status)
    { this = default; Status = status; }
    internal NativeAotNormalFinallyResult(X64UnwindContext parent, NativeAotEhMethodBounds bounds,
        uint protectedOffset, uint exitedClause)
    { Status = NativeAotNormalFinallyStatus.Success; Parent = parent; Bounds = bounds;
        ProtectedOffset = protectedOffset; ExitedClauseIndex = exitedClause; }
    public NativeAotNormalFinallyStatus Status { get; }
    public bool Succeeded => Status == NativeAotNormalFinallyStatus.Success;
    public X64UnwindContext Parent { get; }
    public NativeAotEhMethodBounds Bounds { get; }
    public uint ProtectedOffset { get; }
    public uint ExitedClauseIndex { get; }
}

public static unsafe partial class NativeAotEhClauseMetadata
{
    /// <summary>Recovers one plain finally funclet's same-root normal caller under retained authority.</summary>
    /// <remarks>
    /// Caller retains the genuine instruction-boundary context, readable stack, original mapped
    /// image/module/snapshot and independently reviewed same-image compiler catalog under stable
    /// exclusive access. Fault and finally share one encoded kind; independently reviewed compiler
    /// and call-site evidence must identify a normal finally. This is a bounded shape check,
    /// not proof of how a funclet was invoked.
    /// Catch/filter, Windows handler/chain flags, ambiguous handler aliases and non-root callers
    /// fail closed. Own-frame unwind restores GP/XMM values into a local copy. Actual parent RIP
    /// is preserved; only RIP-1 selects the protected byte. Every failure has a zero payload and
    /// leaves all inputs untouched. No root acquisition, allocation, matcher, handler, stack write
    /// or CPU transfer occurs; success does not authorize general collided unwind or rethrow.
    /// </remarks>
    public static NativeAotNormalFinallyResult RecoverNormalFinallyParent(NativeAotLayoutProfile profile,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        byte* moduleOwner, ulong moduleCapacity, ulong moduleHandle,
        byte* unwindSnapshot, ulong unwindCapacity, uint catalogRva, uint catalogBytes,
        byte* stack, ulong stackBytes, X64UnwindContext current)
    {
        if (profile != NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian)
            return new(NativeAotNormalFinallyStatus.UnsupportedProfile);
        if (!TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(capture, mapping,
                moduleOwner, moduleCapacity, moduleHandle, 204).Succeeded ||
            mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong address) != AddressValueStatus.Success)
            return new(NativeAotNormalFinallyStatus.ModuleRejected);
        Image image = new() { Capture = capture, Mapping = mapping, Owner = moduleOwner,
            Capacity = moduleCapacity, Handle = moduleHandle, Base = address, Bytes = capture.Range.ImageSize };
        if (current.Rip == 0) return new(NativeAotNormalFinallyStatus.FuncletRejected);
        var funclet = Unwind.Lookup(unwindSnapshot, unwindCapacity, current.Rip);
        var callByte = Unwind.Lookup(unwindSnapshot, unwindCapacity, current.Rip - 1);
        if (!funclet.Succeeded || !callByte.Succeeded || !Identity(image, funclet) || !Identity(image, callByte) ||
            funclet.FunctionIndex != callByte.FunctionIndex || funclet.BeginRva != callByte.BeginRva ||
            funclet.EndRva != callByte.EndRva || funclet.Flags != 0 ||
            !Range(image, funclet.BeginRva, funclet.EndRva - funclet.BeginRva, TypeManagerRegisteredRangeKind.ExecutableEntry) ||
            Trailer(image, funclet, 1, out byte flags, out _) != NativeAotEhClauseStatus.Success || flags != 1)
            return new(NativeAotNormalFinallyStatus.FuncletRejected);
        var unwind = X64FrameUnwind.UnwindOne(profile, capture, mapping, moduleOwner, moduleCapacity,
            moduleHandle, unwindSnapshot, unwindCapacity, stack, stackBytes, current);
        if (!unwind.Succeeded) return new(NativeAotNormalFinallyStatus.UnwindRejected);
        var parent = unwind.Caller;
        if (parent.Rip == 0) return new(NativeAotNormalFinallyStatus.ParentRejected);
        var root = Unwind.Lookup(unwindSnapshot, unwindCapacity, parent.Rip);
        var protectedByte = Unwind.Lookup(unwindSnapshot, unwindCapacity, parent.Rip - 1);
        if (!root.Succeeded || !protectedByte.Succeeded || !Identity(image, root) || !Identity(image, protectedByte) ||
            root.FunctionIndex != protectedByte.FunctionIndex || root.BeginRva != protectedByte.BeginRva ||
            root.EndRva != protectedByte.EndRva ||
            Trailer(image, root, 0, out _, out _) != NativeAotEhClauseStatus.Success)
            return new(NativeAotNormalFinallyStatus.ParentRejected);
        var method = LookupMethod(profile, capture, mapping, moduleOwner, moduleCapacity, moduleHandle,
            unwindSnapshot, unwindCapacity, catalogRva, catalogBytes, parent.Rip - 1);
        if (!method.Succeeded) return new(NativeAotNormalFinallyStatus.CatalogRejected);
        if (method.Bounds.RootMethodRva != root.BeginRva ||
            method.ClauseCount == 0 || method.ClauseCount > BoundedExceptionDispatchPlan.MaximumClauses)
            return new(NativeAotNormalFinallyStatus.ParentRejected);
        uint exited = ~0U;
        for (uint i = 0; i < method.ClauseCount; i++)
        {
            var row = ReadClause(profile, capture, mapping, moduleOwner, moduleCapacity, moduleHandle,
                unwindSnapshot, unwindCapacity, method.Bounds, i);
            if (!row.Succeeded) return new(NativeAotNormalFinallyStatus.ClauseRejected);
            if (row.HandlerAddress != address + funclet.BeginRva) continue;
            if (row.Kind != NativeAotEhClauseKind.FaultOrFinally || row.IsTryGroupBoundary)
                return new(NativeAotNormalFinallyStatus.ClauseRejected);
            if (exited != ~0U) return new(NativeAotNormalFinallyStatus.AmbiguousClause);
            exited = i;
        }
        if (exited == ~0U) return new(NativeAotNormalFinallyStatus.ClauseRejected);
        return new(parent, method.Bounds, (uint)(parent.Rip - 1 - address - method.Bounds.RootMethodRva), exited);
    }
}
