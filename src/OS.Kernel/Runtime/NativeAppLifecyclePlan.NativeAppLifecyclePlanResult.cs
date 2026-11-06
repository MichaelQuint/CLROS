using OS.Boot;
using OS.Manifest;
using OS.Pal;

namespace OS.Kernel.Runtime;

public readonly struct NativeAppLifecyclePlanResult
{
    private NativeAppLifecyclePlanResult(NativeAppLifecyclePlanStatus status, NativeAppInvocationPlanStatus invocationStatus, OSHostStatus logStatus, bool isInitialized, NativeAppLifecyclePlan plan)
    {
        Status = status;
        InvocationStatus = invocationStatus;
        LogStatus = logStatus;
        IsInitialized = isInitialized;
        Plan = plan;
    }

    public NativeAppLifecyclePlanStatus Status
    {
        get;
    }
    public NativeAppInvocationPlanStatus InvocationStatus
    {
        get;
    }
    public OSHostStatus LogStatus
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public NativeAppLifecyclePlan Plan
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NativeAppLifecyclePlanStatus.Success;

    public static NativeAppLifecyclePlanResult Success(NativeAppInvocationPlanStatus invocationStatus, OSHostStatus logStatus, NativeAppLifecyclePlan plan)
    {
        return new NativeAppLifecyclePlanResult(NativeAppLifecyclePlanStatus.Success, invocationStatus, logStatus, isInitialized: true, plan);
    }

    public static NativeAppLifecyclePlanResult Fail(NativeAppLifecyclePlanStatus status, NativeAppInvocationPlanStatus invocationStatus, OSHostStatus logStatus)
    {
        return new NativeAppLifecyclePlanResult(status, invocationStatus, logStatus, isInitialized: true, default);
    }
}
