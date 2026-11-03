using OS.Std.Text;

namespace OS.Kernel.Runtime;

public readonly struct NativeAotClassConstructorStateDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public NativeAotClassConstructorStateDiagnosticFormatResult(NativeAotClassConstructorStateDiagnosticFormatStatus status, NativeAotClassConstructorStateTableStatus tableStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        TableStatus = tableStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public NativeAotClassConstructorStateDiagnosticFormatStatus Status
    {
        get;
    }
    public NativeAotClassConstructorStateTableStatus TableStatus
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
    public bool Succeeded => IsInitialized && Status == NativeAotClassConstructorStateDiagnosticFormatStatus.Success && RequiredBytes > 0 && (BytesWritten == 0 || (BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes));

    public static NativeAotClassConstructorStateDiagnosticFormatResult Fail(NativeAotClassConstructorStateDiagnosticFormatStatus status, NativeAotClassConstructorStateTableStatus tableStatus, int requiredBytes, int availableBytes)
    {
        return new NativeAotClassConstructorStateDiagnosticFormatResult(status, tableStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
