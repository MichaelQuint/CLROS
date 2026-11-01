namespace OS.Net;
using OS.Std.Text;

public readonly struct TcpReceiveDispatchDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public TcpReceiveDispatchDiagnosticFormatResult(TcpReceiveDispatchDiagnosticFormatStatus status, TcpReceiveDispatchStatus dispatchStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        DispatchStatus = dispatchStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public TcpReceiveDispatchDiagnosticFormatStatus Status { get; }
    public TcpReceiveDispatchStatus DispatchStatus { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == TcpReceiveDispatchDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static TcpReceiveDispatchDiagnosticFormatResult Fail(TcpReceiveDispatchDiagnosticFormatStatus status, TcpReceiveDispatchStatus dispatchStatus, int requiredBytes, int availableBytes)
    {
        return new TcpReceiveDispatchDiagnosticFormatResult(status, dispatchStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
