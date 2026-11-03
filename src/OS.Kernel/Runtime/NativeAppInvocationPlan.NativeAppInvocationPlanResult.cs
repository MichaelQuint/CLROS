using OS.Boot;
using OS.Manifest;

namespace OS.Kernel.Runtime;

public readonly struct NativeAppInvocationPlanResult
{
    private NativeAppInvocationPlanResult(NativeAppInvocationPlanStatus status, NativeAppExecutableMemoryPlanStatus executableMemoryStatus, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppLaunchDescriptorStatus descriptorStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus, NativeAppLaunchStatus launchStatus, bool isInitialized, NativeAppInvocationPlan plan)
    {
        Status = status;
        ExecutableMemoryStatus = executableMemoryStatus;
        PayloadMappingStatus = payloadMappingStatus;
        DescriptorStatus = descriptorStatus;
        PayloadHeaderStatus = payloadHeaderStatus;
        LaunchStatus = launchStatus;
        IsInitialized = isInitialized;
        Plan = plan;
    }

    public NativeAppInvocationPlanStatus Status
    {
        get;
    }
    public NativeAppExecutableMemoryPlanStatus ExecutableMemoryStatus
    {
        get;
    }
    public NativeAppPayloadMappingPlanStatus PayloadMappingStatus
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
    public NativeAppLaunchStatus LaunchStatus
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public NativeAppInvocationPlan Plan
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NativeAppInvocationPlanStatus.Success;

    public static NativeAppInvocationPlanResult Success(NativeAppExecutableMemoryPlanStatus executableMemoryStatus, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppLaunchDescriptorStatus descriptorStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus, NativeAppLaunchStatus launchStatus, NativeAppInvocationPlan plan)
    {
        return new NativeAppInvocationPlanResult(NativeAppInvocationPlanStatus.Success, executableMemoryStatus, payloadMappingStatus, descriptorStatus, payloadHeaderStatus, launchStatus, isInitialized: true, plan);
    }

    public static NativeAppInvocationPlanResult Fail(NativeAppInvocationPlanStatus status, NativeAppExecutableMemoryPlanStatus executableMemoryStatus, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppLaunchDescriptorStatus descriptorStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus, NativeAppLaunchStatus launchStatus)
    {
        return new NativeAppInvocationPlanResult(status, executableMemoryStatus, payloadMappingStatus, descriptorStatus, payloadHeaderStatus, launchStatus, isInitialized: true, default);
    }
}
