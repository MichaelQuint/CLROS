namespace OS.Drivers;

using OS.Net;
using OS.Std.Text;

public readonly struct VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult(VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus status, VirtioNetReceiveRegisterWindowTcpDispatchStatus dispatchStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        DispatchStatus = dispatchStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus Status
    {
        get;
    }
    public VirtioNetReceiveRegisterWindowTcpDispatchStatus DispatchStatus
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
    public bool Succeeded => IsInitialized && Status == VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult Fail(VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus status, VirtioNetReceiveRegisterWindowTcpDispatchStatus dispatchStatus, int requiredBytes, int availableBytes)
    {
        return new VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult(status, dispatchStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
