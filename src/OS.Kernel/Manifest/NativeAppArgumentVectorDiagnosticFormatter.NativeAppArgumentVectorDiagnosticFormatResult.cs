using OS.Std.Text;

namespace OS.Manifest;

public readonly struct NativeAppArgumentVectorDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public NativeAppArgumentVectorDiagnosticFormatResult(NativeAppArgumentVectorDiagnosticFormatStatus status, NativeAppArgumentVectorStatus plannerStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        PlannerStatus = plannerStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public NativeAppArgumentVectorDiagnosticFormatStatus Status
    {
        get;
    }
    public NativeAppArgumentVectorStatus PlannerStatus
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
    public bool Succeeded => IsInitialized && Status == NativeAppArgumentVectorDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static NativeAppArgumentVectorDiagnosticFormatResult Fail(NativeAppArgumentVectorDiagnosticFormatStatus status, NativeAppArgumentVectorStatus plannerStatus, int requiredBytes, int availableBytes)
    {
        return new NativeAppArgumentVectorDiagnosticFormatResult(status, plannerStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
