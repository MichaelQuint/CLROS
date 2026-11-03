using OS.Std.Text;

namespace OS.Kernel.Runtime;

public static class NativeAotClassConstructorRunnerPreflightDiagnosticFormatter
{
    public static NativeAotClassConstructorRunnerPreflightFormatResult FormatResult(in NativeAotClassConstructorRunnerPreflightResult result, Span<byte> destination)
    {
        NativeAotClassConstructorRunnerPreflightFormatStatus validation = Validate(in result);
        if (validation != NativeAotClassConstructorRunnerPreflightFormatStatus.Success)
        {
            return NativeAotClassConstructorRunnerPreflightFormatResult.Fail(validation, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return NativeAotClassConstructorRunnerPreflightFormatResult.Fail(NativeAotClassConstructorRunnerPreflightFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("NATIVEAOT_CCTOR_RUNNER_PREFLIGHT status="u8, destination, ref offset) || !TryAppendBytes(GetPreflightStatusName(result.Status), destination, ref offset) || !TryAppendBytes(" initialized="u8, destination, ref offset) || !TryAppendBool(result.IsInitialized, destination, ref offset) || !TryAppendBytes(" executesConstructors="u8, destination, ref offset) || !TryAppendBool(NativeAotClassConstructorRunnerPreflight.ExecutesConstructors, destination, ref offset) || !TryAppendBytes(" typeCount="u8, destination, ref offset) || !TryAppendInt64Decimal(result.TypeCount, destination, ref offset) || !TryAppendBytes(" targetMetadataCount="u8, destination, ref offset) || !TryAppendInt64Decimal(result.TargetMetadataCount, destination, ref offset) || !TryAppendBytes(" occupiedCount="u8, destination, ref offset) || !TryAppendInt64Decimal(result.OccupiedCount, destination, ref offset) || !TryAppendBytes(" capacity="u8, destination, ref offset) || !TryAppendInt64Decimal(result.Capacity, destination, ref offset) || !TryAppendBytes(" firstProblemIndex="u8, destination, ref offset) || !TryAppendInt64Decimal(result.FirstProblemIndex, destination, ref offset) || !TryAppendBytes(" firstProblemState="u8, destination, ref offset) || !TryAppendBytes(GetStateName(result.FirstProblemState), destination, ref offset) || !TryAppendBytes(" firstProblemFailureCode="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.FirstProblemFailureCode, destination, ref offset))
        {
            return NativeAotClassConstructorRunnerPreflightFormatResult.Fail(NativeAotClassConstructorRunnerPreflightFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        return new NativeAotClassConstructorRunnerPreflightFormatResult(NativeAotClassConstructorRunnerPreflightFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static NativeAotClassConstructorRunnerPreflightFormatResult QueryRequiredBytes(in NativeAotClassConstructorRunnerPreflightResult result)
    {
        NativeAotClassConstructorRunnerPreflightFormatStatus validation = Validate(in result);
        if (validation != NativeAotClassConstructorRunnerPreflightFormatStatus.Success)
        {
            return NativeAotClassConstructorRunnerPreflightFormatResult.Fail(validation, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new NativeAotClassConstructorRunnerPreflightFormatResult(NativeAotClassConstructorRunnerPreflightFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static NativeAotClassConstructorRunnerPreflightFormatStatus Validate(in NativeAotClassConstructorRunnerPreflightResult result)
    {
        if (!result.IsInitialized || GetPreflightStatusName(result.Status).Length == 0 || GetStateName(result.FirstProblemState).Length == 0 || result.Capacity < 0 || result.OccupiedCount < 0 || result.OccupiedCount > result.Capacity || result.FirstProblemIndex < -1 || result.FirstProblemIndex >= result.Capacity)
        {
            return NativeAotClassConstructorRunnerPreflightFormatStatus.InvalidResult;
        }

        return NativeAotClassConstructorRunnerPreflightFormatStatus.Success;
    }

    private static int GetRequiredBytes(in NativeAotClassConstructorRunnerPreflightResult result)
    {
        return "NATIVEAOT_CCTOR_RUNNER_PREFLIGHT status="u8.Length + GetPreflightStatusName(result.Status).Length + " initialized="u8.Length + GetBoolByteCount(result.IsInitialized) + " executesConstructors="u8.Length + GetBoolByteCount(NativeAotClassConstructorRunnerPreflight.ExecutesConstructors) + " typeCount="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.TypeCount) + " targetMetadataCount="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.TargetMetadataCount) + " occupiedCount="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.OccupiedCount) + " capacity="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.Capacity) + " firstProblemIndex="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.FirstProblemIndex) + " firstProblemState="u8.Length + GetStateName(result.FirstProblemState).Length + " firstProblemFailureCode="u8.Length + AsciiDecimalFormatter.CountUInt32Digits(result.FirstProblemFailureCode);
    }

    private static ReadOnlySpan<byte> GetPreflightStatusName(NativeAotClassConstructorRunnerPreflightStatus status)
    {
        return status switch
        {
            NativeAotClassConstructorRunnerPreflightStatus.Ready => "Ready"u8,
            NativeAotClassConstructorRunnerPreflightStatus.NotInitialized => "NotInitialized"u8,
            NativeAotClassConstructorRunnerPreflightStatus.InvalidTypeCount => "InvalidTypeCount"u8,
            NativeAotClassConstructorRunnerPreflightStatus.TypeCountExceedsCapacity => "TypeCountExceedsCapacity"u8,
            NativeAotClassConstructorRunnerPreflightStatus.TypeCountMismatch => "TypeCountMismatch"u8,
            NativeAotClassConstructorRunnerPreflightStatus.MissingTargetMetadata => "MissingTargetMetadata"u8,
            NativeAotClassConstructorRunnerPreflightStatus.TargetMetadataCountMismatch => "TargetMetadataCountMismatch"u8,
            NativeAotClassConstructorRunnerPreflightStatus.PendingOrReentrantConstructor => "PendingOrReentrantConstructor"u8,
            NativeAotClassConstructorRunnerPreflightStatus.FailedConstructor => "FailedConstructor"u8,
            NativeAotClassConstructorRunnerPreflightStatus.InvalidState => "InvalidState"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetStateName(NativeAotClassConstructorState state)
    {
        return state switch
        {
            NativeAotClassConstructorState.Uninitialized => "Uninitialized"u8,
            NativeAotClassConstructorState.Initializing => "Initializing"u8,
            NativeAotClassConstructorState.Initialized => "Initialized"u8,
            NativeAotClassConstructorState.Failed => "Failed"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static int GetBoolByteCount(bool value)
    {
        return value ? "true"u8.Length : "false"u8.Length;
    }

    private static bool TryAppendBytes(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendBool(bool value, Span<byte> destination, ref int offset)
    {
        return TryAppendBytes(value ? "true"u8 : "false"u8, destination, ref offset);
    }

    private static bool TryAppendInt64Decimal(long value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendUInt64Decimal(ulong value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }
}
