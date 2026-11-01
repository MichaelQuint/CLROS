namespace OS.Manifest;
public readonly struct NativeAppArgumentVectorPlanResult
{
    private NativeAppArgumentVectorPlanResult(NativeAppArgumentVectorStatus status, NativeAppLaunchStatus launchStatus, bool isInitialized, NativeAppArgumentVector arguments, int errorOffset)
    {
        Status = status;
        LaunchStatus = launchStatus;
        IsInitialized = isInitialized;
        Arguments = arguments;
        ErrorOffset = errorOffset;
    }

    public NativeAppArgumentVectorStatus Status { get; }
    public NativeAppLaunchStatus LaunchStatus { get; }
    public bool IsInitialized { get; }
    public NativeAppArgumentVector Arguments { get; }
    public int ArgumentCount => Arguments.Count;
    public int ErrorOffset { get; }
    public bool Succeeded => IsInitialized && Status == NativeAppArgumentVectorStatus.Success;

    public static NativeAppArgumentVectorPlanResult Success(NativeAppArgumentVector arguments)
    {
        return new NativeAppArgumentVectorPlanResult(NativeAppArgumentVectorStatus.Success, NativeAppLaunchStatus.Success, isInitialized: true, arguments, -1);
    }

    public static NativeAppArgumentVectorPlanResult Fail(NativeAppArgumentVectorStatus status, NativeAppLaunchStatus launchStatus, int errorOffset)
    {
        return new NativeAppArgumentVectorPlanResult(status, launchStatus, isInitialized: true, default, errorOffset);
    }
}
