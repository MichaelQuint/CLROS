namespace OS.Kernel.Memory;
/// <summary>
/// Immutable request for one exact 4 KiB leaf mutation.
/// </summary>

public readonly struct X64PageMapRequest
{
    public const ulong StructureSize = 64;
    public const ushort StructureVersion = 1;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64PageMapOperation _operation;
    private readonly PageProtection _protection;
    private readonly PageCachePolicy _cachePolicy;
    private readonly X64HardwareStatePolicy _hardwareStatePolicy;
    private readonly byte _reserved0;
    private readonly uint _reserved1;
    private readonly OS.Boot.PhysicalAddress _rootTable;
    private readonly OS.Boot.VirtualAddress _virtualAddress;
    private readonly OS.Boot.PhysicalAddress _physicalAddress;
    private readonly X64PageTableEncodingPreconditions _preconditions;
    private X64PageMapRequest(X64PageMapOperation operation, OS.Boot.PhysicalAddress rootTable, OS.Boot.VirtualAddress virtualAddress, OS.Boot.PhysicalAddress physicalAddress, PageProtection protection, PageCachePolicy cachePolicy, X64HardwareStatePolicy hardwareStatePolicy, X64PageTableEncodingPreconditions preconditions)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _operation = operation;
        _protection = protection;
        _cachePolicy = cachePolicy;
        _hardwareStatePolicy = hardwareStatePolicy;
        _reserved0 = 0;
        _reserved1 = 0;
        _rootTable = rootTable;
        _virtualAddress = virtualAddress;
        _physicalAddress = physicalAddress;
        _preconditions = preconditions;
    }

    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == StructureVersion && _isInitialized == 1 && _reserved0 == 0 && _reserved1 == 0;
    public X64PageMapOperation Operation => _operation;
    public OS.Boot.PhysicalAddress RootTable => _rootTable;
    public OS.Boot.VirtualAddress VirtualAddress => _virtualAddress;
    public OS.Boot.PhysicalAddress PhysicalAddress => _physicalAddress;
    public PageProtection Protection => _protection;
    public PageCachePolicy CachePolicy => _cachePolicy;
    public X64HardwareStatePolicy HardwareStatePolicy => _hardwareStatePolicy;
    public X64PageTableEncodingPreconditions Preconditions => _preconditions;

    public static X64PageMapRequest CreateMap(OS.Boot.PhysicalAddress rootTable, OS.Boot.VirtualAddress virtualAddress, OS.Boot.PhysicalAddress physicalAddress, PageProtection protection, PageCachePolicy cachePolicy, X64PageTableEncodingPreconditions preconditions)
    {
        return new X64PageMapRequest(X64PageMapOperation.Map, rootTable, virtualAddress, physicalAddress, protection, cachePolicy, X64HardwareStatePolicy.Uninitialized, preconditions);
    }

    public static X64PageMapRequest CreateUnmap(OS.Boot.PhysicalAddress rootTable, OS.Boot.VirtualAddress virtualAddress, X64PageTableEncodingPreconditions preconditions)
    {
        return new X64PageMapRequest(X64PageMapOperation.Unmap, rootTable, virtualAddress, default, PageProtection.None, PageCachePolicy.Uninitialized, X64HardwareStatePolicy.Uninitialized, preconditions);
    }

    public static X64PageMapRequest CreateProtect(OS.Boot.PhysicalAddress rootTable, OS.Boot.VirtualAddress virtualAddress, PageProtection protection, X64HardwareStatePolicy hardwareStatePolicy, X64PageTableEncodingPreconditions preconditions)
    {
        return new X64PageMapRequest(X64PageMapOperation.Protect, rootTable, virtualAddress, default, protection, PageCachePolicy.Uninitialized, hardwareStatePolicy, preconditions);
    }

    public X64PageMapStatus Validate()
    {
        if (!IsInitialized)
        {
            return X64PageMapStatus.InvalidRequestState;
        }

        if (_operation != X64PageMapOperation.Map && _operation != X64PageMapOperation.Unmap && _operation != X64PageMapOperation.Protect)
        {
            return X64PageMapStatus.InvalidOperation;
        }

        if (!_rootTable.IsValid || !_rootTable.IsAligned(X64PageMap.PageSize))
        {
            return X64PageMapStatus.InvalidRootTable;
        }

        if (!_virtualAddress.IsValid)
        {
            return X64PageMapStatus.PageZeroRejected;
        }

        if (!_virtualAddress.IsAligned(X64PageMap.PageSize))
        {
            return X64PageMapStatus.MisalignedVirtualAddress;
        }

        if (_virtualAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong rawVirtual) != OS.Boot.AddressValueStatus.Success)
        {
            return X64PageMapStatus.InvalidVirtualAddress;
        }

        if (rawVirtual == 0)
        {
            return X64PageMapStatus.PageZeroRejected;
        }

        if (rawVirtual >= X64PageMap.LowCanonicalEndExclusive || rawVirtual > X64PageMap.LowCanonicalEndExclusive - X64PageMap.PageSize)
        {
            return X64PageMapStatus.NonCanonicalVirtualAddress;
        }

        X64PageTableEntryStatus preconditionStatus = _preconditions.Validate();
        if (preconditionStatus != X64PageTableEntryStatus.Success)
        {
            return X64PageMapStatus.InvalidRequestState;
        }

        if (_operation == X64PageMapOperation.Map)
        {
            if (!_physicalAddress.IsValid || !_physicalAddress.IsAligned(X64PageMap.PageSize))
            {
                return X64PageMapStatus.InvalidPhysicalAddress;
            }

            X64PageTableEntryResult encoded = X64PageTableEntry.TryEncode(new X64PageTableEntryRequest(X64PageTableLevel.Pte, X64PageTableEntryKind.Leaf, _physicalAddress, _protection, _cachePolicy), _preconditions);
            return encoded.Succeeded ? X64PageMapStatus.Success : MapEntryStatus(encoded.Status);
        }

        if (_physicalAddress.IsValid)
        {
            return X64PageMapStatus.InvalidPhysicalAddress;
        }

        if (_operation == X64PageMapOperation.Unmap)
        {
            if (_protection != PageProtection.None)
            {
                return X64PageMapStatus.InvalidProtection;
            }

            if (_cachePolicy != PageCachePolicy.Uninitialized)
            {
                return X64PageMapStatus.InvalidCachePolicy;
            }

            return _hardwareStatePolicy == X64HardwareStatePolicy.Uninitialized ? X64PageMapStatus.Success : X64PageMapStatus.InvalidHardwareStatePolicy;
        }

        if (_cachePolicy != PageCachePolicy.Uninitialized)
        {
            return X64PageMapStatus.InvalidCachePolicy;
        }

        if (_hardwareStatePolicy != X64HardwareStatePolicy.Clear && _hardwareStatePolicy != X64HardwareStatePolicy.Preserve)
        {
            return X64PageMapStatus.InvalidHardwareStatePolicy;
        }

        X64PageTableEntryResult protectionCheck = X64PageTableEntry.TryEncode(new X64PageTableEntryRequest(X64PageTableLevel.Pte, X64PageTableEntryKind.Leaf, _rootTable, _protection, PageCachePolicy.WriteBack), _preconditions);
        return protectionCheck.Succeeded ? X64PageMapStatus.Success : MapEntryStatus(protectionCheck.Status);
    }

    private static X64PageMapStatus MapEntryStatus(X64PageTableEntryStatus status)
    {
        if (status == X64PageTableEntryStatus.InvalidProtection || status == X64PageTableEntryStatus.WritableExecutableRejected)
        {
            return X64PageMapStatus.InvalidProtection;
        }

        if (status == X64PageTableEntryStatus.InvalidCachePolicy || status == X64PageTableEntryStatus.CachePolicyNotSupported)
        {
            return X64PageMapStatus.InvalidCachePolicy;
        }

        if (status == X64PageTableEntryStatus.InvalidPhysicalAddress || status == X64PageTableEntryStatus.MisalignedPhysicalAddress || status == X64PageTableEntryStatus.PhysicalAddressOutsideWidth)
        {
            return X64PageMapStatus.InvalidPhysicalAddress;
        }

        return X64PageMapStatus.InvalidRequestState;
    }
}
