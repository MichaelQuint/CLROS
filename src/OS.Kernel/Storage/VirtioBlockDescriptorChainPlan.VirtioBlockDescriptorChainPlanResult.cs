using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockDescriptorChainPlanResult
{
    private VirtioBlockDescriptorChainPlanResult(
        VirtioBlockDescriptorChainPlanStatus status,
        VirtioBlockReadTransactionPlanStatus transactionPlanStatus,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioBlockDescriptorChainPlan plan)
    {
        IsInitialized = true;
        Status = status;
        TransactionPlanStatus = transactionPlanStatus;
        RequestPlanStatus = requestPlanStatus;
        QueuePlanStatus = queuePlanStatus;
        Plan = plan;
    }

    public bool IsInitialized
    {
        get;
    }

    public VirtioBlockDescriptorChainPlanStatus Status
    {
        get;
    }

    public VirtioBlockReadTransactionPlanStatus TransactionPlanStatus
    {
        get;
    }

    public VirtioBlockRequestPlanStatus RequestPlanStatus
    {
        get;
    }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus
    {
        get;
    }

    public VirtioBlockDescriptorChainPlan Plan
    {
        get;
    }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockDescriptorChainPlanStatus.Success;

    public static VirtioBlockDescriptorChainPlanResult Success(
        VirtioBlockDescriptorChainPlan plan,
        VirtioBlockReadTransactionPlanStatus transactionPlanStatus,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus)
    {
        return new VirtioBlockDescriptorChainPlanResult(
            VirtioBlockDescriptorChainPlanStatus.Success,
            transactionPlanStatus,
            requestPlanStatus,
            queuePlanStatus,
            plan);
    }

    public static VirtioBlockDescriptorChainPlanResult Fail(
        VirtioBlockDescriptorChainPlanStatus status,
        VirtioBlockReadTransactionPlanStatus transactionPlanStatus,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus)
    {
        return new VirtioBlockDescriptorChainPlanResult(
            status,
            transactionPlanStatus,
            requestPlanStatus,
            queuePlanStatus,
            default);
    }
}
