namespace OS.Drivers;
public enum VirtioNetTransmitRegisterWindowReclaimStatus
{
    Success = 0,
    ReadinessFailed = 1,
    CompletionPlanFailed = 2,
    OwnershipReclaimFailed = 3
}
