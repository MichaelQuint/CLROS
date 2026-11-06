using OS.Std.Text;

namespace OS.Storage;

public readonly struct VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;

    public VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult(
        VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus status,
        VirtioBlockPackageFileStagingPreflightStatus preflightStatus,
        int bytesWritten,
        int requiredBytes,
        int availableBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        PreflightStatus = preflightStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus Status
    {
        get;
    }

    public VirtioBlockPackageFileStagingPreflightStatus PreflightStatus
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

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.Success &&
        RequiredBytes > 0 &&
        (BytesWritten == 0 ||
            (BytesWritten == RequiredBytes &&
            RequiredBytes <= AvailableBytes));

    public static VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult Fail(
        VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus status,
        VirtioBlockPackageFileStagingPreflightStatus preflightStatus,
        int requiredBytes,
        int availableBytes)
    {
        return new VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult(
            status,
            preflightStatus,
            bytesWritten: 0,
            requiredBytes,
            availableBytes);
    }
}
