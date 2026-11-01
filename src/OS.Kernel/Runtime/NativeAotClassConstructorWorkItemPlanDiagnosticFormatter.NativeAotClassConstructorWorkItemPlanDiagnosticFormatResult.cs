using OS.Std.Text;

namespace OS.Kernel.Runtime;
public readonly struct NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult(NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus status, NativeAotClassConstructorWorkItemPlanStatus planStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        PlanStatus = planStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus Status { get; }
    public NativeAotClassConstructorWorkItemPlanStatus PlanStatus { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.Success && RequiredBytes > 0 && (BytesWritten == 0 || (BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes));

    public static NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult Fail(NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus status, NativeAotClassConstructorWorkItemPlanStatus planStatus, int requiredBytes, int availableBytes)
    {
        return new NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult(status, planStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
