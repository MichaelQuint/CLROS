using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;

public readonly struct AcpiRsdpSnapshotResult
{
    private AcpiRsdpSnapshotResult(AcpiRsdpSnapshotStatus status, ulong physicalAddress, int bytesRead, int requiredBytes, bool extendedReadAttempted, PlatformPhysicalMemoryStatus physicalReadStatus, AcpiRsdpValidationStatus validationStatus, AcpiRsdpRevisionKind revisionKind, byte revision, uint declaredLength, ulong rsdtAddress, ulong xsdtAddress)
    {
        Status = status;
        PhysicalAddress = physicalAddress;
        BytesRead = bytesRead;
        RequiredBytes = requiredBytes;
        ExtendedReadAttempted = extendedReadAttempted;
        PhysicalReadStatus = physicalReadStatus;
        ValidationStatus = validationStatus;
        RevisionKind = revisionKind;
        Revision = revision;
        DeclaredLength = declaredLength;
        RsdtAddress = rsdtAddress;
        XsdtAddress = xsdtAddress;
    }

    public AcpiRsdpSnapshotStatus Status
    {
        get;
    }
    public bool Succeeded => Status == AcpiRsdpSnapshotStatus.Success;
    public ulong PhysicalAddress
    {
        get;
    }
    public int BytesRead
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public bool ExtendedReadAttempted
    {
        get;
    }
    public PlatformPhysicalMemoryStatus PhysicalReadStatus
    {
        get;
    }
    public AcpiRsdpValidationStatus ValidationStatus
    {
        get;
    }
    public AcpiRsdpRevisionKind RevisionKind
    {
        get;
    }
    public byte Revision
    {
        get;
    }
    public uint DeclaredLength
    {
        get;
    }
    public ulong RsdtAddress
    {
        get;
    }
    public ulong XsdtAddress
    {
        get;
    }

    public static AcpiRsdpSnapshotResult Success(ulong physicalAddress, int bytesRead, bool extendedReadAttempted, AcpiRsdpValidationResult validation, ulong rsdtAddress, ulong xsdtAddress)
    {
        return new AcpiRsdpSnapshotResult(AcpiRsdpSnapshotStatus.Success, physicalAddress, bytesRead, validation.RequiredLength > 0 ? ToInt32Length(validation.RequiredLength) : bytesRead, extendedReadAttempted, PlatformPhysicalMemoryStatus.Success, validation.Status, validation.RevisionKind, validation.Revision, validation.Length, rsdtAddress, xsdtAddress);
    }

    public static AcpiRsdpSnapshotResult InvalidPhysicalAddress()
    {
        return Fail(AcpiRsdpSnapshotStatus.InvalidPhysicalAddress);
    }

    public static AcpiRsdpSnapshotResult PhysicalMemoryUnavailable(ulong physicalAddress)
    {
        return new AcpiRsdpSnapshotResult(AcpiRsdpSnapshotStatus.PhysicalMemoryUnavailable, physicalAddress, 0, 0, extendedReadAttempted: false, PlatformPhysicalMemoryStatus.SourceUnavailable, AcpiRsdpValidationStatus.Success, AcpiRsdpRevisionKind.None, revision: 0, declaredLength: 0, rsdtAddress: 0, xsdtAddress: 0);
    }

    public static AcpiRsdpSnapshotResult ScratchTooSmall(ulong physicalAddress, int requiredBytes)
    {
        return new AcpiRsdpSnapshotResult(AcpiRsdpSnapshotStatus.ScratchTooSmall, physicalAddress, 0, requiredBytes, extendedReadAttempted: false, PlatformPhysicalMemoryStatus.Unknown, AcpiRsdpValidationStatus.Success, AcpiRsdpRevisionKind.None, revision: 0, declaredLength: 0, rsdtAddress: 0, xsdtAddress: 0);
    }

    public static AcpiRsdpSnapshotResult PhysicalReadFailed(ulong physicalAddress, PlatformPhysicalMemoryResult readResult, bool extendedReadAttempted)
    {
        AcpiRsdpSnapshotStatus status = extendedReadAttempted ? AcpiRsdpSnapshotStatus.ExtendedReadFailed : AcpiRsdpSnapshotStatus.PhysicalReadFailed;
        return new AcpiRsdpSnapshotResult(status, physicalAddress, readResult.BytesRead, readResult.RequiredBytes, extendedReadAttempted, readResult.Status, AcpiRsdpValidationStatus.Success, AcpiRsdpRevisionKind.None, revision: 0, declaredLength: 0, rsdtAddress: 0, xsdtAddress: 0);
    }

    public static AcpiRsdpSnapshotResult ValidationFailed(ulong physicalAddress, AcpiRsdpValidationResult validation, bool extendedReadAttempted)
    {
        AcpiRsdpSnapshotStatus status = extendedReadAttempted ? AcpiRsdpSnapshotStatus.ExtendedValidationFailed : AcpiRsdpSnapshotStatus.BaseValidationFailed;
        return new AcpiRsdpSnapshotResult(status, physicalAddress, 0, ToInt32Length(validation.RequiredLength), extendedReadAttempted, PlatformPhysicalMemoryStatus.Success, validation.Status, validation.RevisionKind, validation.Revision, validation.Length, rsdtAddress: 0, xsdtAddress: 0);
    }

    private static AcpiRsdpSnapshotResult Fail(AcpiRsdpSnapshotStatus status)
    {
        return new AcpiRsdpSnapshotResult(status, 0, 0, 0, extendedReadAttempted: false, PlatformPhysicalMemoryStatus.Unknown, AcpiRsdpValidationStatus.Success, AcpiRsdpRevisionKind.None, revision: 0, declaredLength: 0, rsdtAddress: 0, xsdtAddress: 0);
    }

    private static int ToInt32Length(uint length)
    {
        return length > int.MaxValue ? int.MaxValue : (int)length;
    }
}
