namespace OS.Boot;
/// <summary>
/// Binds the code allocation, data allocation, and owned memory-map
/// destination to caller-attested pre-hierarchy identity mappings. This
/// evidence is required before any physical reservation is compared with a
/// dereferenceable virtual address.
/// </summary>

public readonly struct M01StageOneAddressMappingEvidence
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 72;
    private const ulong RequiredAllocationTableAlignment = 8;
    private const ulong MaximumUnsignedLong = ~0UL;
    private readonly uint _size;
    private readonly uint _version;
    private readonly M01StageOneAddressMappingProvenance _provenance;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly byte _reserved6;
    private readonly PhysicalAddress _codePhysicalStart;
    private readonly VirtualAddress _codeVirtualStart;
    private readonly PhysicalAddress _dataPhysicalStart;
    private readonly VirtualAddress _dataVirtualStart;
    private readonly PhysicalAddress _ownedMapPhysicalStart;
    private readonly VirtualAddress _ownedMapVirtualStart;
    private readonly ulong _ownedMapByteLength;
    private M01StageOneAddressMappingEvidence(PhysicalAddress codePhysicalStart, VirtualAddress codeVirtualStart, PhysicalAddress dataPhysicalStart, VirtualAddress dataVirtualStart, PhysicalAddress ownedMapPhysicalStart, VirtualAddress ownedMapVirtualStart, ulong ownedMapByteLength)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _provenance = M01StageOneAddressMappingProvenance.CallerAttestedIdentityMappings;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _reserved6 = 0;
        _codePhysicalStart = codePhysicalStart;
        _codeVirtualStart = codeVirtualStart;
        _dataPhysicalStart = dataPhysicalStart;
        _dataVirtualStart = dataVirtualStart;
        _ownedMapPhysicalStart = ownedMapPhysicalStart;
        _ownedMapVirtualStart = ownedMapVirtualStart;
        _ownedMapByteLength = ownedMapByteLength;
    }

    public uint Size => _size;
    public uint Version => _version;
    public M01StageOneAddressMappingProvenance Provenance => _provenance;
    public PhysicalAddress CodePhysicalStart => _codePhysicalStart;
    public VirtualAddress CodeVirtualStart => _codeVirtualStart;
    public PhysicalAddress DataPhysicalStart => _dataPhysicalStart;
    public VirtualAddress DataVirtualStart => _dataVirtualStart;
    public PhysicalAddress OwnedMapPhysicalStart => _ownedMapPhysicalStart;
    public VirtualAddress OwnedMapVirtualStart => _ownedMapVirtualStart;
    public ulong OwnedMapByteLength => _ownedMapByteLength;

    public static unsafe M01StageOneAddressMappingEvidence CaptureCallerAttestedIdentity(BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, FirmwareMemoryMapRecord ownedMap)
    {
        ulong allocationTableAddress = (ulong)allocationRecords;
        ulong allocationTableByteLength = M01LoadedImageStackIntegration.RequiredAllocationRecordCount * (ulong)sizeof(BootMemoryRegionRecord);
        if (allocationRecords == null || allocationRecordCount != M01LoadedImageStackIntegration.RequiredAllocationRecordCount || allocationRecordCapacity < allocationRecordCount || (allocationTableAddress & (RequiredAllocationTableAlignment - 1)) != 0 || allocationTableAddress > MaximumUnsignedLong - allocationTableByteLength)
        {
            return default;
        }

        BootMemoryRegionRecord code = allocationRecords[M01LoadedImageStackIntegration.CodeAllocationRecordIndex];
        BootMemoryRegionRecord data = allocationRecords[M01LoadedImageStackIntegration.DataAllocationRecordIndex];
        if (code.PhysicalStart != code.VirtualStart || data.PhysicalStart != data.VirtualStart || ownedMap.BufferAddress == 0 || ownedMap.BufferSize == 0 || PhysicalAddress.TryCreateFromUefiBoundary(code.PhysicalStart, out PhysicalAddress codePhysicalStart) != AddressValueStatus.Success || VirtualAddress.TryCreateFromUefiBoundary(code.VirtualStart, out VirtualAddress codeVirtualStart) != AddressValueStatus.Success || PhysicalAddress.TryCreateFromUefiBoundary(data.PhysicalStart, out PhysicalAddress dataPhysicalStart) != AddressValueStatus.Success || VirtualAddress.TryCreateFromUefiBoundary(data.VirtualStart, out VirtualAddress dataVirtualStart) != AddressValueStatus.Success || PhysicalAddress.TryCreateFromUefiBoundary(ownedMap.BufferAddress, out PhysicalAddress ownedMapPhysicalStart) != AddressValueStatus.Success || VirtualAddress.TryCreateFromUefiBoundary(ownedMap.BufferAddress, out VirtualAddress ownedMapVirtualStart) != AddressValueStatus.Success)
        {
            return default;
        }

        return new M01StageOneAddressMappingEvidence(codePhysicalStart, codeVirtualStart, dataPhysicalStart, dataVirtualStart, ownedMapPhysicalStart, ownedMapVirtualStart, ownedMap.BufferSize);
    }

    public bool IsValidFor(BootMemoryRegionRecord code, BootMemoryRegionRecord data, FirmwareMemoryMapRecord ownedMap)
    {
        if (_size != StructureSize || _version != CurrentVersion || _provenance != M01StageOneAddressMappingProvenance.CallerAttestedIdentityMappings || _reserved0 != 0 || _reserved1 != 0 || _reserved2 != 0 || _reserved3 != 0 || _reserved4 != 0 || _reserved5 != 0 || _reserved6 != 0 || code.PhysicalStart != code.VirtualStart || data.PhysicalStart != data.VirtualStart || code.ByteLength == 0 || data.ByteLength == 0 || ownedMap.BufferAddress == 0 || ownedMap.BufferSize == 0 || _ownedMapByteLength != ownedMap.BufferSize || _codePhysicalStart.TryGetRawValueForUefiBoundary(out ulong codePhysicalStart) != AddressValueStatus.Success || _codeVirtualStart.TryGetRawValueForUefiBoundary(out ulong codeVirtualStart) != AddressValueStatus.Success || _dataPhysicalStart.TryGetRawValueForUefiBoundary(out ulong dataPhysicalStart) != AddressValueStatus.Success || _dataVirtualStart.TryGetRawValueForUefiBoundary(out ulong dataVirtualStart) != AddressValueStatus.Success || _ownedMapPhysicalStart.TryGetRawValueForUefiBoundary(out ulong mapPhysicalStart) != AddressValueStatus.Success || _ownedMapVirtualStart.TryGetRawValueForUefiBoundary(out ulong mapVirtualStart) != AddressValueStatus.Success || codePhysicalStart != code.PhysicalStart || codeVirtualStart != code.VirtualStart || dataPhysicalStart != data.PhysicalStart || dataVirtualStart != data.VirtualStart || mapPhysicalStart != ownedMap.BufferAddress || mapVirtualStart != ownedMap.BufferAddress || codePhysicalStart != codeVirtualStart || dataPhysicalStart != dataVirtualStart || mapPhysicalStart != mapVirtualStart || _codePhysicalStart.TryGetEndExclusive(code.ByteLength, out PhysicalAddress codePhysicalEnd) != AddressValueStatus.Success || _codeVirtualStart.TryGetEndExclusive(code.ByteLength, out VirtualAddress codeVirtualEnd) != AddressValueStatus.Success || _dataPhysicalStart.TryGetEndExclusive(data.ByteLength, out PhysicalAddress dataPhysicalEnd) != AddressValueStatus.Success || _dataVirtualStart.TryGetEndExclusive(data.ByteLength, out VirtualAddress dataVirtualEnd) != AddressValueStatus.Success || _ownedMapPhysicalStart.TryGetEndExclusive(_ownedMapByteLength, out PhysicalAddress mapPhysicalEnd) != AddressValueStatus.Success || _ownedMapVirtualStart.TryGetEndExclusive(_ownedMapByteLength, out VirtualAddress mapVirtualEnd) != AddressValueStatus.Success || codePhysicalEnd.TryGetRawValueForUefiBoundary(out ulong codePhysicalEndValue) != AddressValueStatus.Success || codeVirtualEnd.TryGetRawValueForUefiBoundary(out ulong codeVirtualEndValue) != AddressValueStatus.Success || dataPhysicalEnd.TryGetRawValueForUefiBoundary(out ulong dataPhysicalEndValue) != AddressValueStatus.Success || dataVirtualEnd.TryGetRawValueForUefiBoundary(out ulong dataVirtualEndValue) != AddressValueStatus.Success || mapPhysicalEnd.TryGetRawValueForUefiBoundary(out ulong mapPhysicalEndValue) != AddressValueStatus.Success || mapVirtualEnd.TryGetRawValueForUefiBoundary(out ulong mapVirtualEndValue) != AddressValueStatus.Success || codePhysicalEndValue != code.PhysicalEndExclusive || codePhysicalEndValue != codeVirtualEndValue || dataPhysicalEndValue != data.PhysicalEndExclusive || dataPhysicalEndValue != dataVirtualEndValue || mapPhysicalEndValue != mapVirtualEndValue)
        {
            return false;
        }

        return true;
    }
}
