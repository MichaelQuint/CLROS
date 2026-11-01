namespace OS.Pal;
using OS.Std.Text;

public readonly struct OSHostExportCatalogDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public readonly OSHostStatus Status;
    public readonly OSHostExportCatalogStatus CatalogStatus;
    public readonly uint BytesWritten;
    public readonly uint RequiredBytes;
    public readonly uint AvailableBytes;
    public readonly uint Flags;
    public OSHostExportCatalogDiagnosticFormatResult(OSHostStatus status, OSHostExportCatalogStatus catalogStatus, uint bytesWritten, uint requiredBytes, uint availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        CatalogStatus = catalogStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && RequiredBytes != 0 && BytesWritten == RequiredBytes;

    public static OSHostExportCatalogDiagnosticFormatResult FromStatus(OSHostStatus status, OSHostExportCatalogStatus catalogStatus, uint availableBytes = 0)
    {
        return new OSHostExportCatalogDiagnosticFormatResult(status, catalogStatus, bytesWritten: 0, requiredBytes: 0, availableBytes);
    }
}
