namespace OS.Kernel.Memory;

/// <summary>
/// Identifies why CLROS owns a physical boot-time range.
/// Existing values are an append-only ABI and must never be renumbered or reused.
/// </summary>
public enum BootReservationKind : byte
{
    Uninitialized = 0,
    LoadedImage = 1,
    BootStackSwitchStub = 2,
    BootStackGuard = 3,
    BootStack = 4,
    OwnedMemoryMap = 5,
    AcpiSnapshot = 6,
    AcpiScratch = 7,
    DiagnosticState = 8,
    AllocatorArena = 9,
    PageTableArena = 10,
    Cr3TransitionStub = 11,
    BootHandoff = 12,
    FramebufferMetadata = 13,
    PageProtectionProbe = 14,
    LinuxElfPackage = 15,
    LinuxElfBacking = 16,
    LinuxElfUserTables = 17,
    LinuxSyscallCode = 18,
    LinuxSyscallData = 19
}

/// <summary>
/// Reports construction or structural validation of one boot reservation.
/// The zero-valued default is intentionally not success.
/// </summary>
public enum BootReservedRangeStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    UninitializedKind = 2,
    UnknownKind = 3,
    ReservedBitsSet = 4,
    InvalidPhysicalStart = 5,
    MisalignedPhysicalStart = 6,
    InvalidPageCount = 7,
    PageByteCountOverflow = 8,
    ByteLengthMismatch = 9,
    InvalidPhysicalEndExclusive = 10,
    MisalignedPhysicalEndExclusive = 11,
    AddressRangeOverflow = 12,
    PhysicalEndMismatch = 13
}

/// <summary>
/// Describes one nonzero, page-aligned, half-open physical range owned by CLROS.
/// This is distinct from BootMemoryReservedRange, which classifies unusable UEFI
/// descriptors and may legitimately describe physical page zero.
/// </summary>
public readonly struct BootReservedRange
{
    public const ulong PageSize = 4096;

    private const ulong MaximumUnsignedLong = ~0UL;

    private readonly BootReservationKind _kind;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly byte _reserved6;
    private readonly OS.Boot.PhysicalAddress _physicalStart;
    private readonly OS.Boot.PhysicalAddress _physicalEndExclusive;
    private readonly ulong _pageCount;
    private readonly ulong _byteLength;

    private BootReservedRange(
        BootReservationKind kind,
        OS.Boot.PhysicalAddress physicalStart,
        OS.Boot.PhysicalAddress physicalEndExclusive,
        ulong pageCount,
        ulong byteLength)
    {
        _kind = kind;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _reserved6 = 0;
        _physicalStart = physicalStart;
        _physicalEndExclusive = physicalEndExclusive;
        _pageCount = pageCount;
        _byteLength = byteLength;
    }

    public BootReservationKind Kind => _kind;

    public OS.Boot.PhysicalAddress PhysicalStart => _physicalStart;

    public OS.Boot.PhysicalAddress PhysicalEndExclusive => _physicalEndExclusive;

    public ulong PageCount => _pageCount;

    public ulong ByteLength => _byteLength;

    public bool IsValid => Validate() == BootReservedRangeStatus.Success;

    public static BootReservedRangeStatus TryCreate(
        BootReservationKind kind,
        OS.Boot.PhysicalAddress physicalStart,
        ulong pageCount,
        out BootReservedRange range)
    {
        range = default;

        BootReservedRangeStatus kindStatus = ValidateKind(kind);
        if (kindStatus != BootReservedRangeStatus.Success)
        {
            return kindStatus;
        }

        if (!physicalStart.IsValid)
        {
            return BootReservedRangeStatus.InvalidPhysicalStart;
        }

        if (!physicalStart.IsAligned(PageSize))
        {
            return BootReservedRangeStatus.MisalignedPhysicalStart;
        }

        if (pageCount == 0)
        {
            return BootReservedRangeStatus.InvalidPageCount;
        }

        if (pageCount > MaximumUnsignedLong / PageSize)
        {
            return BootReservedRangeStatus.PageByteCountOverflow;
        }

        ulong byteLength = pageCount * PageSize;
        OS.Boot.AddressValueStatus endStatus = physicalStart.TryGetEndExclusive(
            byteLength,
            out OS.Boot.PhysicalAddress physicalEndExclusive);
        if (endStatus != OS.Boot.AddressValueStatus.Success)
        {
            return endStatus == OS.Boot.AddressValueStatus.AddressOverflow
                ? BootReservedRangeStatus.AddressRangeOverflow
                : BootReservedRangeStatus.InvalidPhysicalStart;
        }

        if (!physicalEndExclusive.IsAligned(PageSize))
        {
            return BootReservedRangeStatus.MisalignedPhysicalEndExclusive;
        }

        range = new BootReservedRange(
            kind,
            physicalStart,
            physicalEndExclusive,
            pageCount,
            byteLength);
        return BootReservedRangeStatus.Success;
    }

    public bool Equals(BootReservedRange other)
    {
        return _kind == other._kind
            && _reserved0 == other._reserved0
            && _reserved1 == other._reserved1
            && _reserved2 == other._reserved2
            && _reserved3 == other._reserved3
            && _reserved4 == other._reserved4
            && _reserved5 == other._reserved5
            && _reserved6 == other._reserved6
            && _physicalStart.Equals(other._physicalStart)
            && _physicalEndExclusive.Equals(other._physicalEndExclusive)
            && _pageCount == other._pageCount
            && _byteLength == other._byteLength;
    }

    internal BootReservedRangeStatus Validate()
    {
        BootReservedRangeStatus kindStatus = ValidateKind(_kind);
        if (kindStatus != BootReservedRangeStatus.Success)
        {
            return kindStatus;
        }

        if (_reserved0 != 0
            || _reserved1 != 0
            || _reserved2 != 0
            || _reserved3 != 0
            || _reserved4 != 0
            || _reserved5 != 0
            || _reserved6 != 0)
        {
            return BootReservedRangeStatus.ReservedBitsSet;
        }

        if (!_physicalStart.IsValid)
        {
            return BootReservedRangeStatus.InvalidPhysicalStart;
        }

        if (!_physicalStart.IsAligned(PageSize))
        {
            return BootReservedRangeStatus.MisalignedPhysicalStart;
        }

        if (_pageCount == 0)
        {
            return BootReservedRangeStatus.InvalidPageCount;
        }

        if (_pageCount > MaximumUnsignedLong / PageSize)
        {
            return BootReservedRangeStatus.PageByteCountOverflow;
        }

        ulong expectedByteLength = _pageCount * PageSize;
        if (_byteLength != expectedByteLength)
        {
            return BootReservedRangeStatus.ByteLengthMismatch;
        }

        if (!_physicalEndExclusive.IsValid)
        {
            return BootReservedRangeStatus.InvalidPhysicalEndExclusive;
        }

        if (!_physicalEndExclusive.IsAligned(PageSize))
        {
            return BootReservedRangeStatus.MisalignedPhysicalEndExclusive;
        }

        OS.Boot.AddressValueStatus endStatus = _physicalStart.TryGetEndExclusive(
            expectedByteLength,
            out OS.Boot.PhysicalAddress expectedEndExclusive);
        if (endStatus != OS.Boot.AddressValueStatus.Success)
        {
            return endStatus == OS.Boot.AddressValueStatus.AddressOverflow
                ? BootReservedRangeStatus.AddressRangeOverflow
                : BootReservedRangeStatus.InvalidPhysicalStart;
        }

        return _physicalEndExclusive.Equals(expectedEndExclusive)
            ? BootReservedRangeStatus.Success
            : BootReservedRangeStatus.PhysicalEndMismatch;
    }

    private static BootReservedRangeStatus ValidateKind(BootReservationKind kind)
    {
        if (kind == BootReservationKind.Uninitialized)
        {
            return BootReservedRangeStatus.UninitializedKind;
        }

        return kind == BootReservationKind.LoadedImage
            || kind == BootReservationKind.BootStackSwitchStub
            || kind == BootReservationKind.BootStackGuard
            || kind == BootReservationKind.BootStack
            || kind == BootReservationKind.OwnedMemoryMap
            || kind == BootReservationKind.AcpiSnapshot
            || kind == BootReservationKind.AcpiScratch
            || kind == BootReservationKind.DiagnosticState
            || kind == BootReservationKind.AllocatorArena
            || kind == BootReservationKind.PageTableArena
            || kind == BootReservationKind.Cr3TransitionStub
            || kind == BootReservationKind.BootHandoff
            || kind == BootReservationKind.FramebufferMetadata
            || kind == BootReservationKind.PageProtectionProbe
            || kind == BootReservationKind.LinuxElfPackage
            || kind == BootReservationKind.LinuxElfBacking
            || kind == BootReservationKind.LinuxElfUserTables
            || kind == BootReservationKind.LinuxSyscallCode
            || kind == BootReservationKind.LinuxSyscallData
            ? BootReservedRangeStatus.Success
            : BootReservedRangeStatus.UnknownKind;
    }
}
