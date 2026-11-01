using OS.Std.Text;

namespace OS.Pal;
public readonly struct OSHostStatusDomainNameFormatResult
{
    private const uint InitializedFlag = 1;
    public readonly OSHostStatus Status;
    public readonly OSHostAbiStatusDomain Domain;
    public readonly uint BytesWritten;
    public readonly uint RequiredBytes;
    public readonly uint Flags;
    public OSHostStatusDomainNameFormatResult(OSHostStatus status, OSHostAbiStatusDomain domain, uint bytesWritten, uint requiredBytes, uint flags = InitializedFlag)
    {
        Status = status;
        Domain = domain;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Flags = flags;
    }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && RequiredBytes != 0 && BytesWritten == RequiredBytes;

    public static OSHostStatusDomainNameFormatResult FromStatus(OSHostStatus status, OSHostAbiStatusDomain domain)
    {
        return new OSHostStatusDomainNameFormatResult(status, domain, bytesWritten: 0, requiredBytes: 0);
    }
}
