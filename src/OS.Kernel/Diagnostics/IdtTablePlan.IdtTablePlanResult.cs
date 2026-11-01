namespace OS.Kernel.Diagnostics;
public readonly struct IdtTablePlanResult
{
    private IdtTablePlanResult(bool succeeded, IdtTablePlanStatus status, byte vector, IdtGateDescriptorStatus descriptorStatus, int descriptorCount, int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        Vector = vector;
        DescriptorStatus = descriptorStatus;
        DescriptorCount = descriptorCount;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded { get; }
    public IdtTablePlanStatus Status { get; }
    public byte Vector { get; }
    public IdtGateDescriptorStatus DescriptorStatus { get; }
    public int DescriptorCount { get; }
    public int RequiredBytes { get; }

    public static IdtTablePlanResult Success()
    {
        return new IdtTablePlanResult(true, IdtTablePlanStatus.Ok, 0, IdtGateDescriptorStatus.Ok, IdtTablePlan.EntryCount, IdtTablePlan.EncodedByteLength);
    }

    public static IdtTablePlanResult Fail(IdtTablePlanStatus status, byte vector = 0, IdtGateDescriptorStatus descriptorStatus = IdtGateDescriptorStatus.Ok, int descriptorCount = 0, int requiredBytes = 0)
    {
        return new IdtTablePlanResult(false, status, vector, descriptorStatus, descriptorCount, requiredBytes);
    }
}
