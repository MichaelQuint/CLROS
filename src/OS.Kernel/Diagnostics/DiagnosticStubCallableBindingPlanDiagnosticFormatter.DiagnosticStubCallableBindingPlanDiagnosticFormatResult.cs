using OS.Kernel.Runtime;
using OS.Std.Text;

namespace OS.Kernel.Diagnostics;
public readonly struct DiagnosticStubCallableBindingPlanDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public DiagnosticStubCallableBindingPlanDiagnosticFormatResult(DiagnosticStubCallableBindingPlanDiagnosticFormatStatus status, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public DiagnosticStubCallableBindingPlanDiagnosticFormatStatus Status { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == DiagnosticStubCallableBindingPlanDiagnosticFormatStatus.Success && RequiredBytes != 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static DiagnosticStubCallableBindingPlanDiagnosticFormatResult Fail(DiagnosticStubCallableBindingPlanDiagnosticFormatStatus status, int requiredBytes, int availableBytes)
    {
        return new DiagnosticStubCallableBindingPlanDiagnosticFormatResult(status, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
