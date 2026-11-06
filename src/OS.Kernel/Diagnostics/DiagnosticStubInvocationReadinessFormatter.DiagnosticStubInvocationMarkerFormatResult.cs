using OS.Kernel.Runtime;
using OS.Std.Text;

namespace OS.Kernel.Diagnostics;

public readonly struct DiagnosticStubInvocationMarkerFormatResult
{
    private const uint InitializedFlag = 1;
    public DiagnosticStubInvocationMarkerFormatResult(DiagnosticStubInvocationMarkerFormatStatus status, int bytesWritten, int requiredBytes, uint flags = InitializedFlag)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Flags = flags;
    }

    public DiagnosticStubInvocationMarkerFormatStatus Status
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
    public uint Flags
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == DiagnosticStubInvocationMarkerFormatStatus.Success && RequiredBytes != 0 && BytesWritten == RequiredBytes;
}
