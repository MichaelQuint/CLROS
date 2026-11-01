namespace OS.Kernel.Memory;
/// <summary>
/// Records the caller's observed prerequisites for encoding present mappings.
/// It does not read CR0, CR4, EFER, CPUID, or IA32_PAT and is not by itself
/// evidence that those observations are live.
/// </summary>

public readonly struct X64PageTableEncodingPreconditions
{
    public const ulong StructureSize = 24;
    private readonly X64PatMappingToken _patMapping;
    private readonly byte _isInitialized;
    private readonly byte _physicalAddressWidth;
    private readonly byte _nxeEnabled;
    private readonly byte _writeProtectEnabled;
    private readonly byte _fiveLevelPagingEnabled;
    private readonly byte _oneGiBPagesSupported;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private X64PageTableEncodingPreconditions(byte physicalAddressWidth, bool nxeEnabled, bool writeProtectEnabled, bool fiveLevelPagingEnabled, bool oneGiBPagesSupported, X64PatMappingToken patMapping)
    {
        _patMapping = patMapping;
        _isInitialized = 1;
        _physicalAddressWidth = physicalAddressWidth;
        _nxeEnabled = nxeEnabled ? (byte)1 : (byte)0;
        _writeProtectEnabled = writeProtectEnabled ? (byte)1 : (byte)0;
        _fiveLevelPagingEnabled = fiveLevelPagingEnabled ? (byte)1 : (byte)0;
        _oneGiBPagesSupported = oneGiBPagesSupported ? (byte)1 : (byte)0;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized => _isInitialized == 1;
    public byte PhysicalAddressWidth => _physicalAddressWidth;
    public bool NxeEnabled => _nxeEnabled == 1;
    public bool WriteProtectEnabled => _writeProtectEnabled == 1;
    public bool FiveLevelPagingEnabled => _fiveLevelPagingEnabled == 1;
    public bool OneGiBPagesSupported => _oneGiBPagesSupported == 1;
    public X64PatMappingToken PatMapping => _patMapping;

    public X64PageTableEntryStatus Validate()
    {
        if (_isInitialized == 0)
        {
            return X64PageTableEntryStatus.PreconditionsNotInitialized;
        }

        if (_isInitialized != 1 || _nxeEnabled > 1 || _writeProtectEnabled > 1 || _fiveLevelPagingEnabled > 1 || _oneGiBPagesSupported > 1 || _reserved0 != 0 || _reserved1 != 0)
        {
            return X64PageTableEntryStatus.InvalidPreconditionState;
        }

        if (_physicalAddressWidth < X64PageTableEntry.MinimumPhysicalAddressWidth || _physicalAddressWidth > X64PageTableEntry.MaximumPhysicalAddressWidth)
        {
            return X64PageTableEntryStatus.UnsupportedPhysicalAddressWidth;
        }

        if (_fiveLevelPagingEnabled == 1)
        {
            return X64PageTableEntryStatus.FiveLevelPagingNotSupported;
        }

        if (_nxeEnabled == 0)
        {
            return X64PageTableEntryStatus.NxeNotEnabled;
        }

        if (_writeProtectEnabled == 0)
        {
            return X64PageTableEntryStatus.WriteProtectNotEnabled;
        }

        return _patMapping.Validate();
    }

    public static X64PageTableEncodingPreconditions CreateObserved(byte physicalAddressWidth, bool nxeEnabled, bool writeProtectEnabled, bool fiveLevelPagingEnabled, bool oneGiBPagesSupported, X64PatMappingToken patMapping)
    {
        return new X64PageTableEncodingPreconditions(physicalAddressWidth, nxeEnabled, writeProtectEnabled, fiveLevelPagingEnabled, oneGiBPagesSupported, patMapping);
    }
}
