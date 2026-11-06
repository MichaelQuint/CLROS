namespace OS.Kernel.Memory;
/// <summary>
/// Caller-owned evaluated state. This record preserves raw control-register
/// observations and the validated root/arena facts without executing a
/// privileged instruction.
/// </summary>

public readonly struct X64PagingModePreflightResult
{
    public const ulong StructureSize = 96;
    private readonly OS.Boot.PhysicalAddress _ownedRoot;
    private readonly OS.Boot.PhysicalAddress _tableArenaStart;
    private readonly OS.Boot.PhysicalAddress _tableArenaEndExclusive;
    private readonly ulong _cr0;
    private readonly ulong _rawFirmwareCr3;
    private readonly ulong _rawCr4;
    private readonly ulong _ia32Efer;
    private readonly ulong _rflags;
    private readonly ulong _physicalAddressMask;
    private readonly byte _isInitialized;
    private readonly X64PagingModePreflightStatus _status;
    private readonly byte _physicalAddressWidth;
    private readonly byte _probeEnabled;
    private readonly byte _pagingEnabled;
    private readonly byte _writeProtectEnabled;
    private readonly byte _paeEnabled;
    private readonly byte _longModeEnabled;
    private readonly byte _longModeActive;
    private readonly byte _nxeEnabled;
    private readonly byte _la57Enabled;
    private readonly byte _pcideEnabled;
    private readonly byte _cpuidAddressLeafAvailable;
    private readonly byte _ownedRootAligned;
    private readonly byte _ownedRootInsideArena;
    private readonly byte _reserved0;
    private readonly ulong _reserved1;
    internal X64PagingModePreflightResult(X64PagingModePreflightStatus status, OS.Boot.PhysicalAddress ownedRoot, OS.Boot.PhysicalAddress tableArenaStart, OS.Boot.PhysicalAddress tableArenaEndExclusive, ulong cr0, ulong rawFirmwareCr3, ulong rawCr4, ulong ia32Efer, ulong rflags, ulong physicalAddressMask, byte physicalAddressWidth, bool probeEnabled, bool pagingEnabled, bool writeProtectEnabled, bool paeEnabled, bool longModeEnabled, bool longModeActive, bool nxeEnabled, bool la57Enabled, bool pcideEnabled, bool cpuidAddressLeafAvailable, bool ownedRootAligned, bool ownedRootInsideArena)
    {
        _ownedRoot = ownedRoot;
        _tableArenaStart = tableArenaStart;
        _tableArenaEndExclusive = tableArenaEndExclusive;
        _cr0 = cr0;
        _rawFirmwareCr3 = rawFirmwareCr3;
        _rawCr4 = rawCr4;
        _ia32Efer = ia32Efer;
        _rflags = rflags;
        _physicalAddressMask = physicalAddressMask;
        _isInitialized = 1;
        _status = status;
        _physicalAddressWidth = physicalAddressWidth;
        _probeEnabled = probeEnabled ? (byte)1 : (byte)0;
        _pagingEnabled = pagingEnabled ? (byte)1 : (byte)0;
        _writeProtectEnabled = writeProtectEnabled ? (byte)1 : (byte)0;
        _paeEnabled = paeEnabled ? (byte)1 : (byte)0;
        _longModeEnabled = longModeEnabled ? (byte)1 : (byte)0;
        _longModeActive = longModeActive ? (byte)1 : (byte)0;
        _nxeEnabled = nxeEnabled ? (byte)1 : (byte)0;
        _la57Enabled = la57Enabled ? (byte)1 : (byte)0;
        _pcideEnabled = pcideEnabled ? (byte)1 : (byte)0;
        _cpuidAddressLeafAvailable = cpuidAddressLeafAvailable ? (byte)1 : (byte)0;
        _ownedRootAligned = ownedRootAligned ? (byte)1 : (byte)0;
        _ownedRootInsideArena = ownedRootInsideArena ? (byte)1 : (byte)0;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized
    {
        get
        {
            bool canonical = _isInitialized == 1 && _status >= X64PagingModePreflightStatus.Success && _status <= X64PagingModePreflightStatus.OwnedRootOutsidePhysicalWidth && _probeEnabled <= 1 && _pagingEnabled <= 1 && _writeProtectEnabled <= 1 && _paeEnabled <= 1 && _longModeEnabled <= 1 && _longModeActive <= 1 && _nxeEnabled <= 1 && _la57Enabled <= 1 && _pcideEnabled <= 1 && _cpuidAddressLeafAvailable <= 1 && _ownedRootAligned <= 1 && _ownedRootInsideArena <= 1 && _reserved0 == 0 && _reserved1 == 0;
            if (!canonical)
            {
                return false;
            }

            return _status != X64PagingModePreflightStatus.Success || HasSuccessfulState();
        }
    }

    public bool Succeeded => IsInitialized && _status == X64PagingModePreflightStatus.Success;
    public X64PagingModePreflightStatus Status => _status;
    public OS.Boot.PhysicalAddress OwnedRoot => _ownedRoot;
    public OS.Boot.PhysicalAddress TableArenaStart => _tableArenaStart;
    public OS.Boot.PhysicalAddress TableArenaEndExclusive => _tableArenaEndExclusive;
    public ulong Cr0 => _cr0;
    public ulong RawFirmwareCr3 => _rawFirmwareCr3;
    public ulong RawCr4 => _rawCr4;
    public ulong Ia32Efer => _ia32Efer;
    public ulong Rflags => _rflags;
    public ulong PhysicalAddressMask => _physicalAddressMask;
    public byte PhysicalAddressWidth => _physicalAddressWidth;
    public bool ProbeEnabled => _probeEnabled == 1;
    public bool PagingEnabled => _pagingEnabled == 1;
    public bool WriteProtectEnabled => _writeProtectEnabled == 1;
    public bool PaeEnabled => _paeEnabled == 1;
    public bool LongModeEnabled => _longModeEnabled == 1;
    public bool LongModeActive => _longModeActive == 1;
    public bool NxeEnabled => _nxeEnabled == 1;
    public bool La57Enabled => _la57Enabled == 1;
    public bool PcideEnabled => _pcideEnabled == 1;
    public bool CpuidAddressLeafAvailable => _cpuidAddressLeafAvailable == 1;
    public bool OwnedRootAligned => _ownedRootAligned == 1;
    public bool OwnedRootInsideArena => _ownedRootInsideArena == 1;

    private bool HasSuccessfulState()
    {
        if (_physicalAddressWidth < X64PagingModePreflightStubPlan.MinimumPhysicalAddressWidth || _physicalAddressWidth > X64PagingModePreflightStubPlan.MaximumPhysicalAddressWidth)
        {
            return false;
        }

        ulong expectedPhysicalMask = (1UL << _physicalAddressWidth) - 1;
        if (_physicalAddressMask != expectedPhysicalMask || _probeEnabled != 1 || _cpuidAddressLeafAvailable != 1 || _pagingEnabled != 1 || _writeProtectEnabled != 1 || _paeEnabled != 1 || _longModeEnabled != 1 || _longModeActive != 1 || _nxeEnabled != 1 || _la57Enabled != 0 || _pcideEnabled != 0 || _ownedRootAligned != 1 || _ownedRootInsideArena != 1)
        {
            return false;
        }

        if ((_cr0 & X64PagingModePreflightStubPlan.Cr0PagingBit) == 0 || (_cr0 & X64PagingModePreflightStubPlan.Cr0WriteProtectBit) == 0 || (_rawCr4 & X64PagingModePreflightStubPlan.Cr4PaeBit) == 0 || (_rawCr4 & X64PagingModePreflightStubPlan.Cr4La57Bit) != 0 || (_rawCr4 & X64PagingModePreflightStubPlan.Cr4PcideBit) != 0 || (_ia32Efer & X64PagingModePreflightStubPlan.EferLongModeEnableBit) == 0 || (_ia32Efer & X64PagingModePreflightStubPlan.EferLongModeActiveBit) == 0 || (_ia32Efer & X64PagingModePreflightStubPlan.EferNoExecuteEnableBit) == 0 || (_rflags & X64PagingModePreflightStubPlan.RflagsArchitecturalOneBit) == 0)
        {
            return false;
        }

        ulong allowedCr3 = (expectedPhysicalMask & ~0xfffUL) | X64PagingModePreflightStubPlan.Cr3AllowedLowBits;
        ulong firmwareRoot = _rawFirmwareCr3 & (expectedPhysicalMask & ~0xfffUL);
        if ((_rawFirmwareCr3 & ~allowedCr3) != 0 || firmwareRoot == 0)
        {
            return false;
        }

        if (_ownedRoot.TryGetRawValueForHardwareRegisterBoundary(out ulong root) != OS.Boot.AddressValueStatus.Success || _tableArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong arenaStart) != OS.Boot.AddressValueStatus.Success || _tableArenaEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong arenaEnd) != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        if ((root & (X64PagingModePreflightStubPlan.PageSize - 1)) != 0 || (arenaStart & (X64PagingModePreflightStubPlan.PageSize - 1)) != 0 || (arenaEnd & (X64PagingModePreflightStubPlan.PageSize - 1)) != 0 || arenaEnd <= arenaStart || (arenaStart & ~expectedPhysicalMask) != 0 || ((arenaEnd - 1) & ~expectedPhysicalMask) != 0 || (root & ~expectedPhysicalMask) != 0 || root < arenaStart || root >= arenaEnd || root > ~0UL - X64PagingModePreflightStubPlan.PageSize || root + X64PagingModePreflightStubPlan.PageSize > arenaEnd)
        {
            return false;
        }

        return true;
    }
}
