using OS.Manifest;

namespace OS.Kernel.Runtime;

public readonly struct NativeAppPayloadMappingPlanResult
{
    private NativeAppPayloadMappingPlanResult(NativeAppPayloadMappingPlanStatus status, NativeAppLaunchDescriptorStatus descriptorStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus, bool isInitialized, NativeAppPayloadMappingPlan plan)
    {
        Status = status;
        DescriptorStatus = descriptorStatus;
        PayloadHeaderStatus = payloadHeaderStatus;
        IsInitialized = isInitialized;
        Plan = plan;
    }

    public NativeAppPayloadMappingPlanStatus Status
    {
        get;
    }
    public NativeAppLaunchDescriptorStatus DescriptorStatus
    {
        get;
    }
    public NativeAppPayloadHeaderStatus PayloadHeaderStatus
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public NativeAppPayloadMappingPlan Plan
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NativeAppPayloadMappingPlanStatus.Success;

    public static NativeAppPayloadMappingPlanResult Success(NativeAppPayloadMappingPlan plan)
    {
        return new NativeAppPayloadMappingPlanResult(NativeAppPayloadMappingPlanStatus.Success, NativeAppLaunchDescriptorStatus.Success, NativeAppPayloadHeaderStatus.Success, isInitialized: true, plan);
    }

    public static NativeAppPayloadMappingPlanResult Fail(NativeAppPayloadMappingPlanStatus status, NativeAppLaunchDescriptorStatus descriptorStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus)
    {
        return new NativeAppPayloadMappingPlanResult(status, descriptorStatus, payloadHeaderStatus, isInitialized: true, default);
    }
}
