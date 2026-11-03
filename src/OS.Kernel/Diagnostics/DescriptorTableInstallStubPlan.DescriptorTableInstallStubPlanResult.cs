using OS.Std.Binary;
using OS.Std.Memory;

namespace OS.Kernel.Diagnostics;

public readonly struct DescriptorTableInstallStubPlanResult
{
    private DescriptorTableInstallStubPlanResult(bool succeeded, DescriptorTableInstallStubPlanStatus status, DescriptorTableInstructionKind operationKind, int bytesWritten, int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        OperationKind = operationKind;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded
    {
        get;
    }
    public DescriptorTableInstallStubPlanStatus Status
    {
        get;
    }
    public DescriptorTableInstructionKind OperationKind
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

    public static DescriptorTableInstallStubPlanResult Success(DescriptorTableInstructionKind operationKind, int bytesWritten)
    {
        return new DescriptorTableInstallStubPlanResult(true, DescriptorTableInstallStubPlanStatus.Ok, operationKind, bytesWritten, bytesWritten);
    }

    public static DescriptorTableInstallStubPlanResult Fail(DescriptorTableInstallStubPlanStatus status, DescriptorTableInstructionKind operationKind, int requiredBytes)
    {
        return new DescriptorTableInstallStubPlanResult(false, status, operationKind, 0, requiredBytes);
    }
}
