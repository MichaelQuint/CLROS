using OS.Boot;

namespace OS.Manifest;
public readonly struct NativeAppLaunchPlanResult
{
    private NativeAppLaunchPlanResult(NativeAppLaunchStatus status, bool isInitialized, NativeAppLaunchPlan plan, BootCapabilities requiredBootCapabilities, BootCapabilities missingBootCapabilities, BootCapabilities unknownBootCapabilities, NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities, NativeAppLaunchRuntimeCapabilities missingRuntimeCapabilities)
    {
        Status = status;
        IsInitialized = isInitialized;
        Plan = plan;
        RequiredBootCapabilities = requiredBootCapabilities;
        MissingBootCapabilities = missingBootCapabilities;
        UnknownBootCapabilities = unknownBootCapabilities;
        RequiredRuntimeCapabilities = requiredRuntimeCapabilities;
        MissingRuntimeCapabilities = missingRuntimeCapabilities;
    }

    public NativeAppLaunchStatus Status { get; }
    public bool IsInitialized { get; }
    public NativeAppLaunchPlan Plan { get; }
    public BootCapabilities RequiredBootCapabilities { get; }
    public BootCapabilities MissingBootCapabilities { get; }
    public BootCapabilities UnknownBootCapabilities { get; }
    public NativeAppLaunchRuntimeCapabilities RequiredRuntimeCapabilities { get; }
    public NativeAppLaunchRuntimeCapabilities MissingRuntimeCapabilities { get; }
    public bool Succeeded => IsInitialized && Status == NativeAppLaunchStatus.Success;

    public static NativeAppLaunchPlanResult Success(NativeAppLaunchPlan plan, BootCapabilities requiredBootCapabilities, NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities)
    {
        return new NativeAppLaunchPlanResult(NativeAppLaunchStatus.Success, isInitialized: true, plan, requiredBootCapabilities, BootCapabilities.None, BootCapabilities.None, requiredRuntimeCapabilities, NativeAppLaunchRuntimeCapabilities.None);
    }

    public static NativeAppLaunchPlanResult Fail(NativeAppLaunchStatus status, BootCapabilities requiredBootCapabilities, BootCapabilities missingBootCapabilities, BootCapabilities unknownBootCapabilities, NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities, NativeAppLaunchRuntimeCapabilities missingRuntimeCapabilities)
    {
        return new NativeAppLaunchPlanResult(status, isInitialized: true, default, requiredBootCapabilities, missingBootCapabilities, unknownBootCapabilities, requiredRuntimeCapabilities, missingRuntimeCapabilities);
    }
}
