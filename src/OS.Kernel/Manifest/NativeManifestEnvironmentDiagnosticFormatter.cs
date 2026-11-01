using OS.Std.Text;

namespace OS.Manifest;
public static class NativeManifestEnvironmentDiagnosticFormatter
{
    public static NativeManifestEnvironmentDiagnosticFormatResult FormatResult(in NativeManifestParseResult result, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        NativeManifestEnvironmentDiagnosticFormatStatus validationStatus = Validate(in result, source);
        if (validationStatus != NativeManifestEnvironmentDiagnosticFormatStatus.Success)
        {
            return NativeManifestEnvironmentDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return NativeManifestEnvironmentDiagnosticFormatResult.Fail(NativeManifestEnvironmentDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        GetFirstEnvironmentMetadata(in result, out bool firstKeyPresent, out int firstKeyOffset, out int firstKeyLength, out int firstValueOffset, out int firstValueLength);
        int offset = 0;
        if (!TryAppendBytes("NATIVE_MANIFEST_ENV status="u8, destination, ref offset) || !TryAppendBytes(GetParseStatusName(result.Status), destination, ref offset) || !TryAppendBytes(" initialized="u8, destination, ref offset) || !TryAppendBool(result.IsInitialized, destination, ref offset) || !TryAppendBytes(" environmentCount="u8, destination, ref offset) || !TryAppendInt64Decimal(result.Manifest.EnvironmentCount, destination, ref offset) || !TryAppendBytes(" maxEnvironmentCount="u8, destination, ref offset) || !TryAppendInt64Decimal(NativeManifestParser.MaximumEnvironmentCount, destination, ref offset) || !TryAppendBytes(" firstKeyPresent="u8, destination, ref offset) || !TryAppendBool(firstKeyPresent, destination, ref offset) || !TryAppendBytes(" firstKeyOffset="u8, destination, ref offset) || !TryAppendInt64Decimal(firstKeyOffset, destination, ref offset) || !TryAppendBytes(" firstKeyLength="u8, destination, ref offset) || !TryAppendInt64Decimal(firstKeyLength, destination, ref offset) || !TryAppendBytes(" firstValueOffset="u8, destination, ref offset) || !TryAppendInt64Decimal(firstValueOffset, destination, ref offset) || !TryAppendBytes(" firstValueLength="u8, destination, ref offset) || !TryAppendInt64Decimal(firstValueLength, destination, ref offset) || !TryAppendBytes(" processEnvironmentMaterialized="u8, destination, ref offset) || !TryAppendBool(false, destination, ref offset) || !TryAppendBytes(" hostedDotNetStarted="u8, destination, ref offset) || !TryAppendBool(false, destination, ref offset) || !TryAppendBytes(" kestrelStarted="u8, destination, ref offset) || !TryAppendBool(false, destination, ref offset))
        {
            return NativeManifestEnvironmentDiagnosticFormatResult.Fail(NativeManifestEnvironmentDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        return new NativeManifestEnvironmentDiagnosticFormatResult(NativeManifestEnvironmentDiagnosticFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static NativeManifestEnvironmentDiagnosticFormatResult QueryRequiredBytes(in NativeManifestParseResult result, ReadOnlySpan<byte> source)
    {
        NativeManifestEnvironmentDiagnosticFormatStatus validationStatus = Validate(in result, source);
        if (validationStatus != NativeManifestEnvironmentDiagnosticFormatStatus.Success)
        {
            return NativeManifestEnvironmentDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new NativeManifestEnvironmentDiagnosticFormatResult(NativeManifestEnvironmentDiagnosticFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static NativeManifestEnvironmentDiagnosticFormatStatus Validate(in NativeManifestParseResult result, ReadOnlySpan<byte> source)
    {
        if (!result.IsInitialized || !result.Succeeded || result.Status != NativeManifestParseStatus.Success || result.Manifest.EnvironmentCount < 0 || result.Manifest.EnvironmentCount > NativeManifestParser.MaximumEnvironmentCount || GetParseStatusName(result.Status).Length == 0)
        {
            return NativeManifestEnvironmentDiagnosticFormatStatus.InvalidResult;
        }

        for (int index = 0; index < result.Manifest.EnvironmentCount; index++)
        {
            NativeManifestEnvironmentEntry entry = result.Manifest.GetEnvironmentEntry(index);
            if (!entry.IsPresent || !IsValidField(source, entry.Key) || !IsValidField(source, entry.Value))
            {
                return NativeManifestEnvironmentDiagnosticFormatStatus.SourceRangeInvalid;
            }
        }

        return NativeManifestEnvironmentDiagnosticFormatStatus.Success;
    }

    private static bool IsValidField(ReadOnlySpan<byte> source, NativeManifestField field)
    {
        return field.IsPresent && field.Offset >= 0 && field.Length >= 0 && field.Offset <= source.Length && source.Length - field.Offset >= field.Length;
    }

    private static int GetRequiredBytes(in NativeManifestParseResult result)
    {
        GetFirstEnvironmentMetadata(in result, out bool firstKeyPresent, out int firstKeyOffset, out int firstKeyLength, out int firstValueOffset, out int firstValueLength);
        return "NATIVE_MANIFEST_ENV status="u8.Length + GetParseStatusName(result.Status).Length + " initialized="u8.Length + GetBoolByteCount(result.IsInitialized) + " environmentCount="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.Manifest.EnvironmentCount) + " maxEnvironmentCount="u8.Length + AsciiDecimalFormatter.CountInt32Digits(NativeManifestParser.MaximumEnvironmentCount) + " firstKeyPresent="u8.Length + GetBoolByteCount(firstKeyPresent) + " firstKeyOffset="u8.Length + AsciiDecimalFormatter.CountInt32Digits(firstKeyOffset) + " firstKeyLength="u8.Length + AsciiDecimalFormatter.CountInt32Digits(firstKeyLength) + " firstValueOffset="u8.Length + AsciiDecimalFormatter.CountInt32Digits(firstValueOffset) + " firstValueLength="u8.Length + AsciiDecimalFormatter.CountInt32Digits(firstValueLength) + " processEnvironmentMaterialized="u8.Length + GetBoolByteCount(false) + " hostedDotNetStarted="u8.Length + GetBoolByteCount(false) + " kestrelStarted="u8.Length + GetBoolByteCount(false);
    }

    private static void GetFirstEnvironmentMetadata(in NativeManifestParseResult result, out bool firstKeyPresent, out int firstKeyOffset, out int firstKeyLength, out int firstValueOffset, out int firstValueLength)
    {
        NativeManifestEnvironmentEntry first = result.Manifest.EnvironmentCount > 0 ? result.Manifest.GetEnvironmentEntry(0) : default;
        firstKeyPresent = first.IsPresent && first.Key.IsPresent;
        firstKeyOffset = firstKeyPresent ? first.Key.Offset : -1;
        firstKeyLength = firstKeyPresent ? first.Key.Length : 0;
        firstValueOffset = firstKeyPresent ? first.Value.Offset : -1;
        firstValueLength = firstKeyPresent ? first.Value.Length : 0;
    }

    private static ReadOnlySpan<byte> GetParseStatusName(NativeManifestParseStatus status)
    {
        return status switch
        {
            NativeManifestParseStatus.Success => "Success"u8,
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
}
