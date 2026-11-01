namespace OS.Manifest;
public static class NativeAppArgumentVectorPlanner
{
    public const int MaximumArgumentCount = 16;
    public const int MaximumArgumentLength = 128;
    public static NativeAppArgumentVectorPlanResult Create(ReadOnlySpan<byte> manifestSource, NativeAppLaunchPlanResult launchPlan)
    {
        if (!launchPlan.Succeeded)
        {
            return NativeAppArgumentVectorPlanResult.Fail(NativeAppArgumentVectorStatus.LaunchPlanFailed, launchPlan.Status, -1);
        }

        NativeManifestField argumentsField = launchPlan.Plan.Arguments;
        if (!argumentsField.TryGetSlice(manifestSource, out ReadOnlySpan<byte> arguments))
        {
            return NativeAppArgumentVectorPlanResult.Fail(NativeAppArgumentVectorStatus.InvalidArgumentsField, NativeAppLaunchStatus.Success, -1);
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
                return NativeAppArgumentVectorPlanResult.Fail(NativeAppArgumentVectorStatus.TooManyArguments, NativeAppLaunchStatus.Success, argumentsField.Offset + index);
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
                    return NativeAppArgumentVectorPlanResult.Fail(NativeAppArgumentVectorStatus.UnterminatedQuote, NativeAppLaunchStatus.Success, argumentsField.Offset + argumentStart - 1);
                }

                argumentLength = index - argumentStart;
                index++;
                if (index < arguments.Length && arguments[index] != (byte)' ')
                {
                    return NativeAppArgumentVectorPlanResult.Fail(NativeAppArgumentVectorStatus.QuoteNotDelimited, NativeAppLaunchStatus.Success, argumentsField.Offset + index);
                }
            }
            else
            {
                argumentStart = index;
                while (index < arguments.Length && arguments[index] != (byte)' ')
                {
                    if (arguments[index] == (byte)'"')
                    {
                        return NativeAppArgumentVectorPlanResult.Fail(NativeAppArgumentVectorStatus.QuoteNotDelimited, NativeAppLaunchStatus.Success, argumentsField.Offset + index);
                    }

                    index++;
                }

                argumentLength = index - argumentStart;
            }

            if (argumentLength > MaximumArgumentLength)
            {
                return NativeAppArgumentVectorPlanResult.Fail(NativeAppArgumentVectorStatus.ArgumentTooLong, NativeAppLaunchStatus.Success, argumentsField.Offset + argumentStart);
            }

            if (!vector.TryAdd(new NativeAppArgument(new NativeManifestField(argumentsField.Offset + argumentStart, argumentLength))))
            {
                return NativeAppArgumentVectorPlanResult.Fail(NativeAppArgumentVectorStatus.TooManyArguments, NativeAppLaunchStatus.Success, argumentsField.Offset + argumentStart);
            }
        }

        return NativeAppArgumentVectorPlanResult.Success(vector);
    }
}
