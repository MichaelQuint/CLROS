using OS.Std.Binary;

namespace OS.Boot;
public readonly struct AcpiRsdpValidationResult
{
    private AcpiRsdpValidationResult(bool succeeded, AcpiRsdpValidationStatus status, AcpiRsdpRevisionKind revisionKind, byte revision, uint length, uint requiredLength)
    {
        Succeeded = succeeded;
        Status = status;
        RevisionKind = revisionKind;
        Revision = revision;
        Length = length;
        RequiredLength = requiredLength;
    }

    public bool Succeeded { get; }
    public AcpiRsdpValidationStatus Status { get; }
    public AcpiRsdpRevisionKind RevisionKind { get; }
    public byte Revision { get; }
    public uint Length { get; }
    public uint RequiredLength { get; }

    public static AcpiRsdpValidationResult Success(AcpiRsdpRevisionKind revisionKind, byte revision, uint length)
    {
        return new AcpiRsdpValidationResult(true, AcpiRsdpValidationStatus.Success, revisionKind, revision, length, length);
    }

    public static AcpiRsdpValidationResult Fail(AcpiRsdpValidationStatus status, uint requiredLength = 0, byte revision = 0, uint length = 0)
    {
        return new AcpiRsdpValidationResult(false, status, AcpiRsdpRevisionKind.None, revision, length, requiredLength);
    }
}
