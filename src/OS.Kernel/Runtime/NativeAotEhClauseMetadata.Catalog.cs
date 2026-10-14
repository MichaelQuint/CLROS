using OS.Boot;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

namespace OS.Kernel.Runtime;

public readonly struct NativeAotEhMethodLookupResult
{
    internal NativeAotEhMethodLookupResult(NativeAotEhClauseStatus status) { this = default; Status = status; }
    internal NativeAotEhMethodLookupResult(NativeAotEhMethodBounds bounds, uint clauses)
    { this = default; Status = NativeAotEhClauseStatus.Success; Bounds = bounds; ClauseCount = clauses; }
    public NativeAotEhClauseStatus Status { get; }
    public bool Succeeded => Status == NativeAotEhClauseStatus.Success;
    public NativeAotEhMethodBounds Bounds { get; }
    public uint ClauseCount { get; }
}

public static unsafe partial class NativeAotEhClauseMetadata
{
    public const uint CatalogMagic = 0x43454843;
    public const uint MaximumCatalogMethods = 32;

    /// <summary>Resolves exact reviewed compiler bounds for an instruction in one contiguous contribution.</summary>
    /// <remarks>
    /// The caller retains the stable original image, module owner and unwind snapshot and
    /// independently reviewed SAME IMAGE compiler-generated catalog and exact catalog extent.
    /// Readonly admission does not prove compiler provenance. No length is inferred from an
    /// EH cursor, neighboring symbol or runtime-function row. Every catalog row and complete
    /// EH stream is checked, including rows after the requested method. Folded/overlapping
    /// contributions, split code and unsupported ReadClause shapes fail closed. This lookup
    /// does not acquire lifetime, root objects, search handlers or transfer CPU control.
    /// </remarks>
    public static NativeAotEhMethodLookupResult LookupMethod(NativeAotLayoutProfile profile,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        byte* moduleOwner, ulong moduleCapacity, ulong moduleHandle,
        byte* unwindSnapshot, ulong unwindCapacity, uint catalogRva, uint catalogBytes, ulong instruction)
    {
        if (profile != NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian)
            return new(NativeAotEhClauseStatus.UnsupportedProfile);
        if (!TypeManagerModuleMetadataLiveNativeAOTImage.TryGetSection(capture, mapping,
                moduleOwner, moduleCapacity, moduleHandle, 204).Succeeded ||
            mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong address) != AddressValueStatus.Success)
            return new(NativeAotEhClauseStatus.ModuleRejected);
        Image image = new() { Capture=capture, Mapping=mapping, Owner=moduleOwner,
            Capacity=moduleCapacity, Handle=moduleHandle, Base=address, Bytes=capture.Range.ImageSize };
        if (catalogBytes < 32 || catalogBytes > 16 + MaximumCatalogMethods * 16)
            return new(NativeAotEhClauseStatus.CapacityExceeded);
        if ((catalogRva & 3) != 0 || !Range(image,catalogRva,catalogBytes,TypeManagerRegisteredRangeKind.ImmutableMetadata))
            return new(NativeAotEhClauseStatus.MetadataRejected);
        if (instruction < address || !Range(image,instruction-address,1,TypeManagerRegisteredRangeKind.ExecutableEntry))
            return new(NativeAotEhClauseStatus.TargetRejected);
        byte* catalog=(byte*)(address+catalogRva);
        uint count=U32(catalog+8);
        if (count == 0 || count > MaximumCatalogMethods) return new(NativeAotEhClauseStatus.CapacityExceeded);
        if (U32(catalog)!=CatalogMagic || U32(catalog+4)!=1 || U32(catalog+12)!=catalogBytes ||
            catalogBytes!=16+count*16) return new(NativeAotEhClauseStatus.MetadataRejected);
        NativeAotEhMethodLookupResult selected = new(NativeAotEhClauseStatus.IndexOutOfRange);
        for(uint i=0;i<count;i++)
        {
            byte* row=catalog+16+i*16;
            NativeAotEhMethodBounds bounds=new(U32(row),U32(row+4),U32(row+8),U32(row+12));
            if (bounds.CodeBytes==0 || bounds.EhRva==0 || bounds.EhBytes==0 ||
                CatalogOverlap(bounds.EhRva,bounds.EhBytes,catalogRva,catalogBytes))
                return new(NativeAotEhClauseStatus.BoundsRejected);
            for(uint j=0;j<i;j++)
            {
                byte* earlier=catalog+16+j*16;
                if(CatalogOverlap(bounds.RootMethodRva,bounds.CodeBytes,U32(earlier),U32(earlier+4)) ||
                    CatalogOverlap(bounds.EhRva,bounds.EhBytes,U32(earlier+8),U32(earlier+12)))
                    return new(NativeAotEhClauseStatus.BoundsRejected);
            }
            var clause=ReadClause(profile,capture,mapping,moduleOwner,moduleCapacity,moduleHandle,
                unwindSnapshot,unwindCapacity,bounds,0);
            if(!clause.Succeeded && clause.Status!=NativeAotEhClauseStatus.IndexOutOfRange) return new(clause.Status);
            // Cover unreferenced funclets as well: an instruction inside a declared
            // span must never be attributed across an intervening method root.
            var root=Unwind.Lookup(unwindSnapshot,unwindCapacity,address+bounds.RootMethodRva);
            if(!root.Succeeded || !Identity(image,root)) return new(NativeAotEhClauseStatus.SnapshotRejected);
            uint frames=1;
            for(uint k=root.FunctionIndex+1;k<root.FunctionCount;k++)
            {
                var next=Unwind.LookupFunction(unwindSnapshot,unwindCapacity,k);
                if(!next.Succeeded || !Identity(image,next)) return new(NativeAotEhClauseStatus.SnapshotRejected);
                if((ulong)next.BeginRva>=(ulong)bounds.RootMethodRva+bounds.CodeBytes) break;
                if(++frames>65) return new(NativeAotEhClauseStatus.CapacityExceeded);
                if((ulong)next.EndRva>(ulong)bounds.RootMethodRva+bounds.CodeBytes) return new(NativeAotEhClauseStatus.BoundsRejected);
                var status=Trailer(image,next,255,out byte flags,out _);
                if(status!=NativeAotEhClauseStatus.Success) return new(status);
                if(flags is not (1 or 2)) return new(NativeAotEhClauseStatus.TargetRejected);
            }
            ulong rva=instruction-address;
            if(rva>=bounds.RootMethodRva && rva<(ulong)bounds.RootMethodRva+bounds.CodeBytes)
            {
                var function=Unwind.Lookup(unwindSnapshot,unwindCapacity,instruction);
                if(!function.Succeeded || !Identity(image,function)) return new(NativeAotEhClauseStatus.SnapshotRejected);
                if(function.BeginRva<bounds.RootMethodRva || (ulong)function.EndRva>(ulong)bounds.RootMethodRva+bounds.CodeBytes)
                    return new(NativeAotEhClauseStatus.BoundsRejected);
                selected=new(bounds,clause.Count);
            }
        }
        return selected;
    }
    private static bool CatalogOverlap(uint start,uint bytes,uint other,uint otherBytes) =>
        (ulong)start<(ulong)other+otherBytes && (ulong)other<(ulong)start+bytes;
}
