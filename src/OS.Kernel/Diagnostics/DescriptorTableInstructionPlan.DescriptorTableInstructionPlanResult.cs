using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public readonly struct DescriptorTableInstructionPlanResult
{
    private DescriptorTableInstructionPlanResult(bool succeeded, DescriptorTableInstructionPlanStatus status, DescriptorTableInstructionKind operationKind, int bytesWritten, int requiredBytes)
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
    public DescriptorTableInstructionPlanStatus Status
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

    public static DescriptorTableInstructionPlanResult Success(DescriptorTableInstructionKind operationKind, int bytesWritten)
    {
        return new DescriptorTableInstructionPlanResult(true, DescriptorTableInstructionPlanStatus.Ok, operationKind, bytesWritten, bytesWritten);
    }

    public static DescriptorTableInstructionPlanResult Fail(DescriptorTableInstructionPlanStatus status, DescriptorTableInstructionKind operationKind, int requiredBytes)
    {
        return new DescriptorTableInstructionPlanResult(false, status, operationKind, 0, requiredBytes);
    }
}
