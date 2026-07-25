namespace OS.Manifest;

public enum NativeAppArgumentVectorStatus
{
    Success = 0,
    LaunchPlanFailed,
    InvalidArgumentsField,
    TooManyArguments,
    ArgumentTooLong,
    UnterminatedQuote,
    QuoteNotDelimited
}

public readonly struct NativeAppArgument
{
    public NativeAppArgument(NativeManifestField value)
    {
        Value = value;
        IsPresent = true;
    }

    public NativeManifestField Value { get; }

    public bool IsPresent { get; }

    public bool TryGetValue(ReadOnlySpan<byte> manifestSource, out ReadOnlySpan<byte> value)
    {
        return Value.TryGetSlice(manifestSource, out value);
    }
}

public struct NativeAppArgumentVector
{
    private NativeAppArgument _argument0;
    private NativeAppArgument _argument1;
    private NativeAppArgument _argument2;
    private NativeAppArgument _argument3;
    private NativeAppArgument _argument4;
    private NativeAppArgument _argument5;
    private NativeAppArgument _argument6;
    private NativeAppArgument _argument7;
    private NativeAppArgument _argument8;
    private NativeAppArgument _argument9;
    private NativeAppArgument _argument10;
    private NativeAppArgument _argument11;
    private NativeAppArgument _argument12;
    private NativeAppArgument _argument13;
    private NativeAppArgument _argument14;
    private NativeAppArgument _argument15;

    public int Count { get; private set; }

    public bool TryAdd(NativeAppArgument argument)
    {
        if (Count >= NativeAppArgumentVectorPlanner.MaximumArgumentCount)
        {
            return false;
        }

        SetAt(Count, argument);
        Count++;
        return true;
    }

    public NativeAppArgument GetAt(int index)
    {
        return index switch
        {
            0 => _argument0,
            1 => _argument1,
            2 => _argument2,
            3 => _argument3,
            4 => _argument4,
            5 => _argument5,
            6 => _argument6,
            7 => _argument7,
            8 => _argument8,
            9 => _argument9,
            10 => _argument10,
            11 => _argument11,
            12 => _argument12,
            13 => _argument13,
            14 => _argument14,
            15 => _argument15,
            _ => default
        };
    }

    private void SetAt(int index, NativeAppArgument argument)
    {
        switch (index)
        {
            case 0:
                _argument0 = argument;
                break;
            case 1:
                _argument1 = argument;
                break;
            case 2:
                _argument2 = argument;
                break;
            case 3:
                _argument3 = argument;
                break;
            case 4:
                _argument4 = argument;
                break;
            case 5:
                _argument5 = argument;
                break;
            case 6:
                _argument6 = argument;
                break;
            case 7:
                _argument7 = argument;
                break;
            case 8:
                _argument8 = argument;
                break;
            case 9:
                _argument9 = argument;
                break;
            case 10:
                _argument10 = argument;
                break;
            case 11:
                _argument11 = argument;
                break;
            case 12:
                _argument12 = argument;
                break;
            case 13:
                _argument13 = argument;
                break;
            case 14:
                _argument14 = argument;
                break;
            case 15:
                _argument15 = argument;
                break;
        }
    }
}

public readonly struct NativeAppArgumentVectorPlanResult
{
    private NativeAppArgumentVectorPlanResult(
        NativeAppArgumentVectorStatus status,
        NativeAppLaunchStatus launchStatus,
        bool isInitialized,
        NativeAppArgumentVector arguments,
        int errorOffset)
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
        return new NativeAppArgumentVectorPlanResult(
            NativeAppArgumentVectorStatus.Success,
            NativeAppLaunchStatus.Success,
            isInitialized: true,
            arguments,
            -1);
    }

    public static NativeAppArgumentVectorPlanResult Fail(
        NativeAppArgumentVectorStatus status,
        NativeAppLaunchStatus launchStatus,
        int errorOffset)
    {
        return new NativeAppArgumentVectorPlanResult(
            status,
            launchStatus,
            isInitialized: true,
            default,
            errorOffset);
    }
}

public static class NativeAppArgumentVectorPlanner
{
    public const int MaximumArgumentCount = 16;
    public const int MaximumArgumentLength = 128;

    public static NativeAppArgumentVectorPlanResult Create(
        ReadOnlySpan<byte> manifestSource,
        NativeAppLaunchPlanResult launchPlan)
    {
        if (!launchPlan.Succeeded)
        {
            return NativeAppArgumentVectorPlanResult.Fail(
                NativeAppArgumentVectorStatus.LaunchPlanFailed,
                launchPlan.Status,
                -1);
        }

        NativeManifestField argumentsField = launchPlan.Plan.Arguments;
        if (!argumentsField.TryGetSlice(manifestSource, out ReadOnlySpan<byte> arguments))
        {
            return NativeAppArgumentVectorPlanResult.Fail(
                NativeAppArgumentVectorStatus.InvalidArgumentsField,
                NativeAppLaunchStatus.Success,
                -1);
        }

        NativeAppArgumentVector vector = default;
        int index = 0;
        while (index < arguments.Length)
        {
            while (index < arguments.Length && arguments[index] == (byte)' ')
            {
                index++;
            }

            if (index >= arguments.Length)
            {
                break;
            }

            if (vector.Count >= MaximumArgumentCount)
            {
                return NativeAppArgumentVectorPlanResult.Fail(
                    NativeAppArgumentVectorStatus.TooManyArguments,
                    NativeAppLaunchStatus.Success,
                    argumentsField.Offset + index);
            }

            int argumentStart;
            int argumentLength;
            if (arguments[index] == (byte)'"')
            {
                index++;
                argumentStart = index;

                while (index < arguments.Length && arguments[index] != (byte)'"')
                {
                    index++;
                }

                if (index >= arguments.Length)
                {
                    return NativeAppArgumentVectorPlanResult.Fail(
                        NativeAppArgumentVectorStatus.UnterminatedQuote,
                        NativeAppLaunchStatus.Success,
                        argumentsField.Offset + argumentStart - 1);
                }

                argumentLength = index - argumentStart;
                index++;
                if (index < arguments.Length && arguments[index] != (byte)' ')
                {
                    return NativeAppArgumentVectorPlanResult.Fail(
                        NativeAppArgumentVectorStatus.QuoteNotDelimited,
                        NativeAppLaunchStatus.Success,
                        argumentsField.Offset + index);
                }
            }
            else
            {
                argumentStart = index;
                while (index < arguments.Length && arguments[index] != (byte)' ')
                {
                    if (arguments[index] == (byte)'"')
                    {
                        return NativeAppArgumentVectorPlanResult.Fail(
                            NativeAppArgumentVectorStatus.QuoteNotDelimited,
                            NativeAppLaunchStatus.Success,
                            argumentsField.Offset + index);
                    }

                    index++;
                }

                argumentLength = index - argumentStart;
            }

            if (argumentLength > MaximumArgumentLength)
            {
                return NativeAppArgumentVectorPlanResult.Fail(
                    NativeAppArgumentVectorStatus.ArgumentTooLong,
                    NativeAppLaunchStatus.Success,
                    argumentsField.Offset + argumentStart);
            }

            if (!vector.TryAdd(new NativeAppArgument(
                    new NativeManifestField(argumentsField.Offset + argumentStart, argumentLength))))
            {
                return NativeAppArgumentVectorPlanResult.Fail(
                    NativeAppArgumentVectorStatus.TooManyArguments,
                    NativeAppLaunchStatus.Success,
                    argumentsField.Offset + argumentStart);
            }
        }

        return NativeAppArgumentVectorPlanResult.Success(vector);
    }
}
