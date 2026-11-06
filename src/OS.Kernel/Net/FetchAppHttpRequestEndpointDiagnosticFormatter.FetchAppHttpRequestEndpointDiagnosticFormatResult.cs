using OS.Std.Text;

namespace OS.Net;

public readonly struct FetchAppHttpRequestEndpointDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public FetchAppHttpRequestEndpointDiagnosticFormatResult(FetchAppHttpRequestEndpointDiagnosticFormatStatus status, FetchAppHttpRequestEndpointStatus endpointStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        EndpointStatus = endpointStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public FetchAppHttpRequestEndpointDiagnosticFormatStatus Status
    {
        get;
    }
    public FetchAppHttpRequestEndpointStatus EndpointStatus
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
    public bool Succeeded => IsInitialized && Status == FetchAppHttpRequestEndpointDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static FetchAppHttpRequestEndpointDiagnosticFormatResult Fail(FetchAppHttpRequestEndpointDiagnosticFormatStatus status, FetchAppHttpRequestEndpointStatus endpointStatus, int requiredBytes, int availableBytes)
    {
        return new FetchAppHttpRequestEndpointDiagnosticFormatResult(status, endpointStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
