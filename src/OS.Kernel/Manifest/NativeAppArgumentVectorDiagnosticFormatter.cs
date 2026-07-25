using OS.Std.Text;

namespace OS.Manifest;

public enum NativeAppArgumentVectorDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}

public readonly struct NativeAppArgumentVectorDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;

    public NativeAppArgumentVectorDiagnosticFormatResult(
        NativeAppArgumentVectorDiagnosticFormatStatus status,
        NativeAppArgumentVectorStatus plannerStatus,
        int bytesWritten,
        int requiredBytes,
        int availableBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        PlannerStatus = plannerStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public NativeAppArgumentVectorDiagnosticFormatStatus Status { get; }

    public NativeAppArgumentVectorStatus PlannerStatus { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public int AvailableBytes { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == NativeAppArgumentVectorDiagnosticFormatStatus.Success &&
        RequiredBytes > 0 &&
        BytesWritten == RequiredBytes &&
        RequiredBytes <= AvailableBytes;

    public static NativeAppArgumentVectorDiagnosticFormatResult Fail(
        NativeAppArgumentVectorDiagnosticFormatStatus status,
        NativeAppArgumentVectorStatus plannerStatus,
        int requiredBytes,
        int availableBytes)
    {
        return new NativeAppArgumentVectorDiagnosticFormatResult(
            status,
            plannerStatus,
            bytesWritten: 0,
            requiredBytes,
            availableBytes);
    }
}

public static class NativeAppArgumentVectorDiagnosticFormatter
{
    public static NativeAppArgumentVectorDiagnosticFormatResult FormatResult(
        in NativeAppArgumentVectorPlanResult result,
        Span<byte> destination)
    {
        NativeAppArgumentVectorDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != NativeAppArgumentVectorDiagnosticFormatStatus.Success)
        {
            return NativeAppArgumentVectorDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return NativeAppArgumentVectorDiagnosticFormatResult.Fail(
                NativeAppArgumentVectorDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        GetFirstArgumentMetadata(
            in result,
            out bool firstArgumentPresent,
            out int firstArgumentOffset,
            out int firstArgumentLength);

        int offset = 0;
        if (!TryAppendBytes("APP_ARGS_VECTOR status="u8, destination, ref offset) ||
            !TryAppendBytes(GetPlannerStatusName(result.Status), destination, ref offset) ||
            !TryAppendBytes(" initialized="u8, destination, ref offset) ||
            !TryAppendBool(result.IsInitialized, destination, ref offset) ||
            !TryAppendBytes(" argumentCount="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.ArgumentCount, destination, ref offset) ||
            !TryAppendBytes(" maxArgumentCount="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(
                NativeAppArgumentVectorPlanner.MaximumArgumentCount,
                destination,
                ref offset) ||
            !TryAppendBytes(" maxArgumentLength="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(
                NativeAppArgumentVectorPlanner.MaximumArgumentLength,
                destination,
                ref offset) ||
            !TryAppendBytes(" firstArgumentPresent="u8, destination, ref offset) ||
            !TryAppendBool(firstArgumentPresent, destination, ref offset) ||
            !TryAppendBytes(" firstArgumentOffset="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(firstArgumentOffset, destination, ref offset) ||
            !TryAppendBytes(" firstArgumentLength="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(firstArgumentLength, destination, ref offset) ||
            !TryAppendBytes(" appExecuted="u8, destination, ref offset) ||
            !TryAppendBool(false, destination, ref offset) ||
            !TryAppendBytes(" kestrelStarted="u8, destination, ref offset) ||
            !TryAppendBool(false, destination, ref offset))
        {
            return NativeAppArgumentVectorDiagnosticFormatResult.Fail(
                NativeAppArgumentVectorDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        return new NativeAppArgumentVectorDiagnosticFormatResult(
            NativeAppArgumentVectorDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: offset,
            requiredBytes,
            availableBytes: destination.Length);
    }

    public static NativeAppArgumentVectorDiagnosticFormatResult QueryRequiredBytes(
        in NativeAppArgumentVectorPlanResult result)
    {
        NativeAppArgumentVectorDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != NativeAppArgumentVectorDiagnosticFormatStatus.Success)
        {
            return NativeAppArgumentVectorDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                availableBytes: 0);
        }

        return new NativeAppArgumentVectorDiagnosticFormatResult(
            NativeAppArgumentVectorDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: 0,
            requiredBytes: GetRequiredBytes(in result),
            availableBytes: 0);
    }

    private static NativeAppArgumentVectorDiagnosticFormatStatus Validate(
        in NativeAppArgumentVectorPlanResult result)
    {
        if (!result.IsInitialized ||
            GetPlannerStatusName(result.Status).Length == 0 ||
            result.ArgumentCount < 0 ||
            result.ArgumentCount > NativeAppArgumentVectorPlanner.MaximumArgumentCount)
        {
            return NativeAppArgumentVectorDiagnosticFormatStatus.InvalidResult;
        }

        if (result.Status == NativeAppArgumentVectorStatus.Success && !result.Succeeded)
        {
            return NativeAppArgumentVectorDiagnosticFormatStatus.InvalidResult;
        }

        if (result.ArgumentCount == 0)
        {
            return NativeAppArgumentVectorDiagnosticFormatStatus.Success;
        }

        NativeAppArgument firstArgument = result.Arguments.GetAt(0);
        if (!firstArgument.IsPresent ||
            firstArgument.Value.Offset < 0 ||
            firstArgument.Value.Length < 0 ||
            firstArgument.Value.Length > NativeAppArgumentVectorPlanner.MaximumArgumentLength)
        {
            return NativeAppArgumentVectorDiagnosticFormatStatus.InvalidResult;
        }

        return NativeAppArgumentVectorDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in NativeAppArgumentVectorPlanResult result)
    {
        GetFirstArgumentMetadata(
            in result,
            out bool firstArgumentPresent,
            out int firstArgumentOffset,
            out int firstArgumentLength);

        return "APP_ARGS_VECTOR status="u8.Length
            + GetPlannerStatusName(result.Status).Length
            + " initialized="u8.Length
            + GetBoolByteCount(result.IsInitialized)
            + " argumentCount="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.ArgumentCount)
            + " maxArgumentCount="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(
                NativeAppArgumentVectorPlanner.MaximumArgumentCount)
            + " maxArgumentLength="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(
                NativeAppArgumentVectorPlanner.MaximumArgumentLength)
            + " firstArgumentPresent="u8.Length
            + GetBoolByteCount(firstArgumentPresent)
            + " firstArgumentOffset="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(firstArgumentOffset)
            + " firstArgumentLength="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(firstArgumentLength)
            + " appExecuted="u8.Length
            + GetBoolByteCount(false)
            + " kestrelStarted="u8.Length
            + GetBoolByteCount(false);
    }

    private static void GetFirstArgumentMetadata(
        in NativeAppArgumentVectorPlanResult result,
        out bool firstArgumentPresent,
        out int firstArgumentOffset,
        out int firstArgumentLength)
    {
        NativeAppArgument firstArgument = result.ArgumentCount > 0
            ? result.Arguments.GetAt(0)
            : default;

        firstArgumentPresent = firstArgument.IsPresent;
        firstArgumentOffset = firstArgumentPresent ? firstArgument.Value.Offset : -1;
        firstArgumentLength = firstArgumentPresent ? firstArgument.Value.Length : 0;
    }

    private static ReadOnlySpan<byte> GetPlannerStatusName(NativeAppArgumentVectorStatus status)
    {
        return status switch
        {
            NativeAppArgumentVectorStatus.Success => "Success"u8,
            NativeAppArgumentVectorStatus.LaunchPlanFailed => "LaunchPlanFailed"u8,
            NativeAppArgumentVectorStatus.InvalidArgumentsField => "InvalidArgumentsField"u8,
            NativeAppArgumentVectorStatus.TooManyArguments => "TooManyArguments"u8,
            NativeAppArgumentVectorStatus.ArgumentTooLong => "ArgumentTooLong"u8,
            NativeAppArgumentVectorStatus.UnterminatedQuote => "UnterminatedQuote"u8,
            NativeAppArgumentVectorStatus.QuoteNotDelimited => "QuoteNotDelimited"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static int GetBoolByteCount(bool value)
    {
        return value ? "true"u8.Length : "false"u8.Length;
    }

    private static bool TryAppendBytes(
        ReadOnlySpan<byte> value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendBool(
        bool value,
        Span<byte> destination,
        ref int offset)
    {
        return TryAppendBytes(value ? "true"u8 : "false"u8, destination, ref offset);
    }

    private static bool TryAppendInt64Decimal(
        long value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }
}
