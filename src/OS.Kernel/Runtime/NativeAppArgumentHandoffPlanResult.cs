using OS.Manifest;

namespace OS.Kernel.Runtime;
public readonly struct NativeAppArgumentHandoffPlanResult
{
    private NativeAppArgumentHandoffPlanResult(NativeAppArgumentHandoffPlanStatus status, NativeAppInvocationPlanStatus invocationStatus, NativeAppArgumentVectorStatus argumentVectorStatus, bool isInitialized, int argumentCount, int parsedArgumentBytes, int rawLaunchArgumentByteLength)
    {
        Status = status;
        InvocationStatus = invocationStatus;
        ArgumentVectorStatus = argumentVectorStatus;
        IsInitialized = isInitialized;
        ArgumentCount = argumentCount;
        ParsedArgumentBytes = parsedArgumentBytes;
        RawLaunchArgumentByteLength = rawLaunchArgumentByteLength;
    }

    public NativeAppArgumentHandoffPlanStatus Status { get; }
    public NativeAppInvocationPlanStatus InvocationStatus { get; }
    public NativeAppArgumentVectorStatus ArgumentVectorStatus { get; }
    public bool IsInitialized { get; }
    public int ArgumentCount { get; }
    public int ParsedArgumentBytes { get; }
    public int RawLaunchArgumentByteLength { get; }
    public bool ExecutesApp => false;
    public bool DefinesFinalAbi => false;
    public bool Succeeded => IsInitialized && Status == NativeAppArgumentHandoffPlanStatus.Success;

    public static NativeAppArgumentHandoffPlanResult Success(NativeAppInvocationPlanStatus invocationStatus, NativeAppArgumentVectorStatus argumentVectorStatus, int argumentCount, int parsedArgumentBytes, int rawLaunchArgumentByteLength)
    {
        return new NativeAppArgumentHandoffPlanResult(NativeAppArgumentHandoffPlanStatus.Success, invocationStatus, argumentVectorStatus, isInitialized: true, argumentCount, parsedArgumentBytes, rawLaunchArgumentByteLength);
    }

    public static NativeAppArgumentHandoffPlanResult Fail(NativeAppArgumentHandoffPlanStatus status, NativeAppInvocationPlanStatus invocationStatus, NativeAppArgumentVectorStatus argumentVectorStatus, int argumentCount, int parsedArgumentBytes, int rawLaunchArgumentByteLength)
    {
        return new NativeAppArgumentHandoffPlanResult(status, invocationStatus, argumentVectorStatus, isInitialized: true, argumentCount, parsedArgumentBytes, rawLaunchArgumentByteLength);
    }
}
