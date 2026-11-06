using OS.Std.Text;

namespace OS.Manifest;

public readonly struct NativeManifestEnvironmentDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public NativeManifestEnvironmentDiagnosticFormatResult(NativeManifestEnvironmentDiagnosticFormatStatus status, NativeManifestParseStatus parseStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        ParseStatus = parseStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public NativeManifestEnvironmentDiagnosticFormatStatus Status
    {
        get;
    }
    public NativeManifestParseStatus ParseStatus
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public int AvailableBytes
    {
        get;
    }
    public uint Flags
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == NativeManifestEnvironmentDiagnosticFormatStatus.Success && ParseStatus == NativeManifestParseStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static NativeManifestEnvironmentDiagnosticFormatResult Fail(NativeManifestEnvironmentDiagnosticFormatStatus status, NativeManifestParseStatus parseStatus, int requiredBytes, int availableBytes)
    {
        return new NativeManifestEnvironmentDiagnosticFormatResult(status, parseStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
