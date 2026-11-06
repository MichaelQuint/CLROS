namespace OS.Boot;
/// <summary>
/// Preserves address domains while binding the M01-003 physical image range
/// to an explicitly attested pre-hierarchy virtual identity mapping.
/// </summary>

public readonly struct M01LoadedImageVirtualMappingEvidence
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 56;
    private readonly uint _size;
    private readonly uint _version;
    private readonly M01LoadedImageVirtualMappingProvenance _provenance;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly byte _reserved6;
    private readonly PhysicalAddress _physicalImageBase;
    private readonly PhysicalAddress _physicalImageEndExclusive;
    private readonly VirtualAddress _virtualImageBase;
    private readonly VirtualAddress _virtualImageEndExclusive;
    private readonly ulong _imageByteLength;
    private M01LoadedImageVirtualMappingEvidence(PhysicalAddress physicalImageBase, PhysicalAddress physicalImageEndExclusive, VirtualAddress virtualImageBase, VirtualAddress virtualImageEndExclusive, ulong imageByteLength)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01LoadedImageVirtualMappingProvenance.CallerAttestedIdentityMapping;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _reserved6 = 0;
        _physicalImageBase = physicalImageBase;
        _physicalImageEndExclusive = physicalImageEndExclusive;
        _virtualImageBase = virtualImageBase;
        _virtualImageEndExclusive = virtualImageEndExclusive;
        _imageByteLength = imageByteLength;
    }

    public uint Size => _size;
    public uint Version => _version;
    public M01LoadedImageVirtualMappingProvenance Provenance => _provenance;
    public PhysicalAddress PhysicalImageBase => _physicalImageBase;
    public PhysicalAddress PhysicalImageEndExclusive => _physicalImageEndExclusive;
    public VirtualAddress VirtualImageBase => _virtualImageBase;
    public VirtualAddress VirtualImageEndExclusive => _virtualImageEndExclusive;
    public ulong ImageByteLength => _imageByteLength;

    public static M01LoadedImageVirtualMappingEvidence CaptureCallerAttestedIdentity(EfiLoadedImageRangeCapturePlan loadedImage)
    {
        if (!loadedImage.Succeeded || loadedImage.Range.ImageBase.TryGetRawValueForUefiBoundary(out ulong physicalBase) != AddressValueStatus.Success || loadedImage.Range.ImageEndExclusive.TryGetRawValueForUefiBoundary(out ulong physicalEnd) != AddressValueStatus.Success || VirtualAddress.TryCreateFromUefiBoundary(physicalBase, out VirtualAddress virtualBase) != AddressValueStatus.Success || VirtualAddress.TryCreateFromUefiBoundary(physicalEnd, out VirtualAddress virtualEnd) != AddressValueStatus.Success)
        {
            return default;
        }

        return new M01LoadedImageVirtualMappingEvidence(loadedImage.Range.ImageBase, loadedImage.Range.ImageEndExclusive, virtualBase, virtualEnd, loadedImage.Range.ImageSize);
    }

    public bool IsValidFor(EfiLoadedImageRangeCapturePlan loadedImage)
    {
        if (_size != StructureSize || _version != CurrentVersion || _provenance != M01LoadedImageVirtualMappingProvenance.CallerAttestedIdentityMapping || _reserved0 != 0 || _reserved1 != 0 || _reserved2 != 0 || _reserved3 != 0 || _reserved4 != 0 || _reserved5 != 0 || _reserved6 != 0 || !loadedImage.Succeeded || !_physicalImageBase.Equals(loadedImage.Range.ImageBase) || !_physicalImageEndExclusive.Equals(loadedImage.Range.ImageEndExclusive) || _imageByteLength != loadedImage.Range.ImageSize || !_virtualImageBase.IsValid || !_virtualImageEndExclusive.IsValid || _virtualImageBase.TryGetEndExclusive(_imageByteLength, out VirtualAddress expectedVirtualEnd) != AddressValueStatus.Success || !expectedVirtualEnd.Equals(_virtualImageEndExclusive) || _physicalImageBase.TryGetRawValueForUefiBoundary(out ulong physicalBase) != AddressValueStatus.Success || _physicalImageEndExclusive.TryGetRawValueForUefiBoundary(out ulong physicalEnd) != AddressValueStatus.Success || _virtualImageBase.TryGetRawValueForUefiBoundary(out ulong virtualBase) != AddressValueStatus.Success || _virtualImageEndExclusive.TryGetRawValueForUefiBoundary(out ulong virtualEnd) != AddressValueStatus.Success)
        {
            return false;
        }

        return physicalBase == virtualBase && physicalEnd == virtualEnd;
    }
}
