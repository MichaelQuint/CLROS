using OS.Std.Text;

namespace OS.Kernel.Runtime;

public static class NativeAotClassConstructorWorkItemPlanDiagnosticFormatter
{
    public static NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult FormatResult(in NativeAotClassConstructorWorkItemPlanResult result, Span<byte> destination)
    {
        NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus validation = Validate(in result);
        if (validation != NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.Success)
        {
            return NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult.Fail(validation, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult.Fail(NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("NATIVEAOT_CCTOR_WORK_ITEMS status="u8, destination, ref offset) || !TryAppendBytes(GetPlanStatusName(result.Status), destination, ref offset) || !TryAppendBytes(" initialized="u8, destination, ref offset) || !TryAppendBool(result.IsInitialized, destination, ref offset) || !TryAppendBytes(" preflight="u8, destination, ref offset) || !TryAppendBytes(GetPreflightStatusName(result.PreflightStatus), destination, ref offset) || !TryAppendBytes(" typeCount="u8, destination, ref offset) || !TryAppendInt64Decimal(result.TypeCount, destination, ref offset) || !TryAppendBytes(" targetMetadataCount="u8, destination, ref offset) || !TryAppendInt64Decimal(result.TargetMetadataCount, destination, ref offset) || !TryAppendBytes(" plannedCount="u8, destination, ref offset) || !TryAppendInt64Decimal(result.PlannedCount, destination, ref offset) || !TryAppendBytes(" skippedInitializedCount="u8, destination, ref offset) || !TryAppendInt64Decimal(result.SkippedInitializedCount, destination, ref offset) || !TryAppendBytes(" requiredWorkItemCapacity="u8, destination, ref offset) || !TryAppendInt64Decimal(result.RequiredWorkItemCapacity, destination, ref offset) || !TryAppendBytes(" availableWorkItemCapacity="u8, destination, ref offset) || !TryAppendInt64Decimal(result.AvailableWorkItemCapacity, destination, ref offset) || !TryAppendBytes(" firstProblemIndex="u8, destination, ref offset) || !TryAppendInt64Decimal(result.FirstProblemIndex, destination, ref offset) || !TryAppendBytes(" firstProblemState="u8, destination, ref offset) || !TryAppendBytes(GetStateName(result.FirstProblemState), destination, ref offset) || !TryAppendBytes(" firstProblemFailureCode="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.FirstProblemFailureCode, destination, ref offset) || !TryAppendBytes(" executesConstructors="u8, destination, ref offset) || !TryAppendBool(NativeAotClassConstructorWorkItemPlanner.ExecutesConstructors, destination, ref offset) || !TryAppendBytes(" usesReflection="u8, destination, ref offset) || !TryAppendBool(NativeAotClassConstructorWorkItemPlanner.UsesReflection, destination, ref offset) || !TryAppendBytes(" usesExceptionControlFlow="u8, destination, ref offset) || !TryAppendBool(NativeAotClassConstructorWorkItemPlanner.UsesExceptionControlFlow, destination, ref offset) || !TryAppendBytes(" usesManagedDelegates="u8, destination, ref offset) || !TryAppendBool(NativeAotClassConstructorWorkItemPlanner.UsesManagedDelegates, destination, ref offset) || !TryAppendBytes(" discoversMetadata="u8, destination, ref offset) || !TryAppendBool(NativeAotClassConstructorWorkItemPlanner.DiscoversMetadata, destination, ref offset))
        {
            return NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult.Fail(NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        return new NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult(NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult QueryRequiredBytes(in NativeAotClassConstructorWorkItemPlanResult result)
    {
        NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus validation = Validate(in result);
        if (validation != NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.Success)
        {
            return NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult.Fail(validation, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new NativeAotClassConstructorWorkItemPlanDiagnosticFormatResult(NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus Validate(in NativeAotClassConstructorWorkItemPlanResult result)
    {
        if (!result.IsInitialized || GetPlanStatusName(result.Status).Length == 0 || GetPreflightStatusName(result.PreflightStatus).Length == 0 || GetStateName(result.FirstProblemState).Length == 0 || result.TypeCount < 0 || result.TargetMetadataCount < 0 || result.PlannedCount < 0 || result.SkippedInitializedCount < 0 || result.RequiredWorkItemCapacity < 0 || result.AvailableWorkItemCapacity < 0 || result.FirstProblemIndex < -1 || result.PlannedCount + result.SkippedInitializedCount > result.TypeCount || result.RequiredWorkItemCapacity < result.PlannedCount)
        {
            return NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.InvalidResult;
        }

        if (result.Status == NativeAotClassConstructorWorkItemPlanStatus.Ready && !result.Succeeded)
        {
            return NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.InvalidResult;
        }

        return NativeAotClassConstructorWorkItemPlanDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in NativeAotClassConstructorWorkItemPlanResult result)
    {
        return "NATIVEAOT_CCTOR_WORK_ITEMS status="u8.Length + GetPlanStatusName(result.Status).Length + " initialized="u8.Length + GetBoolByteCount(result.IsInitialized) + " preflight="u8.Length + GetPreflightStatusName(result.PreflightStatus).Length + " typeCount="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.TypeCount) + " targetMetadataCount="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.TargetMetadataCount) + " plannedCount="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.PlannedCount) + " skippedInitializedCount="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.SkippedInitializedCount) + " requiredWorkItemCapacity="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.RequiredWorkItemCapacity) + " availableWorkItemCapacity="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.AvailableWorkItemCapacity) + " firstProblemIndex="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.FirstProblemIndex) + " firstProblemState="u8.Length + GetStateName(result.FirstProblemState).Length + " firstProblemFailureCode="u8.Length + AsciiDecimalFormatter.CountUInt32Digits(result.FirstProblemFailureCode) + " executesConstructors="u8.Length + GetBoolByteCount(NativeAotClassConstructorWorkItemPlanner.ExecutesConstructors) + " usesReflection="u8.Length + GetBoolByteCount(NativeAotClassConstructorWorkItemPlanner.UsesReflection) + " usesExceptionControlFlow="u8.Length + GetBoolByteCount(NativeAotClassConstructorWorkItemPlanner.UsesExceptionControlFlow) + " usesManagedDelegates="u8.Length + GetBoolByteCount(NativeAotClassConstructorWorkItemPlanner.UsesManagedDelegates) + " discoversMetadata="u8.Length + GetBoolByteCount(NativeAotClassConstructorWorkItemPlanner.DiscoversMetadata);
    }

    private static ReadOnlySpan<byte> GetPlanStatusName(NativeAotClassConstructorWorkItemPlanStatus status)
    {
        return status switch
        {
            NativeAotClassConstructorWorkItemPlanStatus.Ready => "Ready"u8,
            NativeAotClassConstructorWorkItemPlanStatus.NotInitialized => "NotInitialized"u8,
            NativeAotClassConstructorWorkItemPlanStatus.PreflightNotReady => "PreflightNotReady"u8,
            NativeAotClassConstructorWorkItemPlanStatus.InvalidTypeCount => "InvalidTypeCount"u8,
            NativeAotClassConstructorWorkItemPlanStatus.TargetMetadataCountMismatch => "TargetMetadataCountMismatch"u8,
            NativeAotClassConstructorWorkItemPlanStatus.TypeCountMismatch => "TypeCountMismatch"u8,
            NativeAotClassConstructorWorkItemPlanStatus.MissingTargetMetadata => "MissingTargetMetadata"u8,
            NativeAotClassConstructorWorkItemPlanStatus.MissingTargetToken => "MissingTargetToken"u8,
            NativeAotClassConstructorWorkItemPlanStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            NativeAotClassConstructorWorkItemPlanStatus.PendingOrReentrantConstructor => "PendingOrReentrantConstructor"u8,
            NativeAotClassConstructorWorkItemPlanStatus.FailedConstructor => "FailedConstructor"u8,
            NativeAotClassConstructorWorkItemPlanStatus.InvalidState => "InvalidState"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
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
