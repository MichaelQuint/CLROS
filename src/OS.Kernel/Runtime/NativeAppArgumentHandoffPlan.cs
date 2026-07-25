using OS.Manifest;

namespace OS.Kernel.Runtime;

public enum NativeAppArgumentHandoffPlanStatus
{
    Success = 0,
    InvocationPlanNotInitialized = 1,
    ArgumentVectorNotInitialized = 2,
    InvocationPlanFailed = 3,
    ArgumentVectorFailed = 4,
    ImpossibleArgumentCount = 5,
    InvalidArgumentMetadata = 6,
    ParsedArgumentBytesMismatch = 7,
    ParsedArgumentBytesOverflow = 8
}

public readonly struct NativeAppArgumentHandoffPlanResult
{
    private NativeAppArgumentHandoffPlanResult(
        NativeAppArgumentHandoffPlanStatus status,
        NativeAppInvocationPlanStatus invocationStatus,
        NativeAppArgumentVectorStatus argumentVectorStatus,
        bool isInitialized,
        int argumentCount,
        int parsedArgumentBytes,
        int rawLaunchArgumentByteLength)
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

    public static NativeAppArgumentHandoffPlanResult Success(
        NativeAppInvocationPlanStatus invocationStatus,
        NativeAppArgumentVectorStatus argumentVectorStatus,
        int argumentCount,
        int parsedArgumentBytes,
        int rawLaunchArgumentByteLength)
    {
        return new NativeAppArgumentHandoffPlanResult(
            NativeAppArgumentHandoffPlanStatus.Success,
            invocationStatus,
            argumentVectorStatus,
            isInitialized: true,
            argumentCount,
            parsedArgumentBytes,
            rawLaunchArgumentByteLength);
    }

    public static NativeAppArgumentHandoffPlanResult Fail(
        NativeAppArgumentHandoffPlanStatus status,
        NativeAppInvocationPlanStatus invocationStatus,
        NativeAppArgumentVectorStatus argumentVectorStatus,
        int argumentCount,
        int parsedArgumentBytes,
        int rawLaunchArgumentByteLength)
    {
        return new NativeAppArgumentHandoffPlanResult(
            status,
            invocationStatus,
            argumentVectorStatus,
            isInitialized: true,
            argumentCount,
            parsedArgumentBytes,
            rawLaunchArgumentByteLength);
    }
}

public static class NativeAppArgumentHandoffPlanner
{
    public const bool ExecutesApp = false;
    public const bool DefinesFinalAbi = false;
    public const bool AllocatesStack = false;
    public const bool StartsHostedDotNet = false;

    public static NativeAppArgumentHandoffPlanResult Compose(
        NativeAppInvocationPlanResult invocation,
        NativeAppArgumentVectorPlanResult argumentVector)
    {
        int argumentCount = argumentVector.ArgumentCount;
        int rawArgumentBytes = invocation.Plan.ArgumentByteLength;

        if (!invocation.IsInitialized)
        {
            return NativeAppArgumentHandoffPlanResult.Fail(
                NativeAppArgumentHandoffPlanStatus.InvocationPlanNotInitialized,
                invocation.Status,
                argumentVector.Status,
                argumentCount,
                parsedArgumentBytes: 0,
                rawArgumentBytes);
        }

        if (!argumentVector.IsInitialized)
        {
            return NativeAppArgumentHandoffPlanResult.Fail(
                NativeAppArgumentHandoffPlanStatus.ArgumentVectorNotInitialized,
                invocation.Status,
                argumentVector.Status,
                argumentCount,
                parsedArgumentBytes: 0,
                rawArgumentBytes);
        }

        if (!invocation.Succeeded)
        {
            return NativeAppArgumentHandoffPlanResult.Fail(
                NativeAppArgumentHandoffPlanStatus.InvocationPlanFailed,
                invocation.Status,
                argumentVector.Status,
                argumentCount,
                parsedArgumentBytes: 0,
                rawArgumentBytes);
        }

        if (!argumentVector.Succeeded)
        {
            return NativeAppArgumentHandoffPlanResult.Fail(
                NativeAppArgumentHandoffPlanStatus.ArgumentVectorFailed,
                invocation.Status,
                argumentVector.Status,
                argumentCount,
                parsedArgumentBytes: 0,
                rawArgumentBytes);
        }

        if (argumentCount < 0 || argumentCount > NativeAppArgumentVectorPlanner.MaximumArgumentCount)
        {
            return NativeAppArgumentHandoffPlanResult.Fail(
                NativeAppArgumentHandoffPlanStatus.ImpossibleArgumentCount,
                invocation.Status,
                argumentVector.Status,
                argumentCount,
                parsedArgumentBytes: 0,
                rawArgumentBytes);
        }

        if (rawArgumentBytes < 0 || rawArgumentBytes > NativeManifestParser.MaximumArgsLength)
        {
            return NativeAppArgumentHandoffPlanResult.Fail(
                NativeAppArgumentHandoffPlanStatus.ParsedArgumentBytesMismatch,
                invocation.Status,
                argumentVector.Status,
                argumentCount,
                parsedArgumentBytes: 0,
                rawArgumentBytes);
        }

        NativeAppArgumentHandoffPlanStatus parsedStatus =
            TryCountParsedArgumentBytes(argumentVector.Arguments, argumentCount, out int parsedArgumentBytes);
        if (parsedStatus != NativeAppArgumentHandoffPlanStatus.Success)
        {
            return NativeAppArgumentHandoffPlanResult.Fail(
                parsedStatus,
                invocation.Status,
                argumentVector.Status,
                argumentCount,
                parsedArgumentBytes,
                rawArgumentBytes);
        }

        if (parsedArgumentBytes > rawArgumentBytes)
        {
            return NativeAppArgumentHandoffPlanResult.Fail(
                NativeAppArgumentHandoffPlanStatus.ParsedArgumentBytesMismatch,
                invocation.Status,
                argumentVector.Status,
                argumentCount,
                parsedArgumentBytes,
                rawArgumentBytes);
        }

        return NativeAppArgumentHandoffPlanResult.Success(
            invocation.Status,
            argumentVector.Status,
            argumentCount,
            parsedArgumentBytes,
            rawArgumentBytes);
    }

    private static NativeAppArgumentHandoffPlanStatus TryCountParsedArgumentBytes(
        NativeAppArgumentVector arguments,
        int argumentCount,
        out int parsedArgumentBytes)
    {
        parsedArgumentBytes = 0;

        for (int index = 0; index < argumentCount; index++)
        {
            NativeAppArgument argument = arguments.GetAt(index);
            if (!argument.IsPresent)
            {
                return NativeAppArgumentHandoffPlanStatus.ImpossibleArgumentCount;
            }

            if (argument.Value.Length < 0 ||
                argument.Value.Length > NativeAppArgumentVectorPlanner.MaximumArgumentLength)
            {
                return NativeAppArgumentHandoffPlanStatus.InvalidArgumentMetadata;
            }

            if (parsedArgumentBytes > int.MaxValue - argument.Value.Length)
            {
                parsedArgumentBytes = 0;
                return NativeAppArgumentHandoffPlanStatus.ParsedArgumentBytesOverflow;
            }

            parsedArgumentBytes += argument.Value.Length;
        }

        return NativeAppArgumentHandoffPlanStatus.Success;
    }
}
