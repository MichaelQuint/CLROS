using OS.Std.Text;

namespace OS.Net;

public readonly struct TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult(TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus status, TcpConnectionCloseIpv4FramePlanStatus frameStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        FrameStatus = frameStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus Status
    {
        get;
    }
    public TcpConnectionCloseIpv4FramePlanStatus FrameStatus
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
    public bool Succeeded => IsInitialized && Status == TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult Fail(TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus status, TcpConnectionCloseIpv4FramePlanStatus frameStatus, int requiredBytes, int availableBytes)
    {
        return new TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult(status, frameStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
