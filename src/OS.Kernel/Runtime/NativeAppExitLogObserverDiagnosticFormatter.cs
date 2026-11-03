using OS.Pal;
using OS.Std.Text;

namespace OS.Kernel.Runtime;

public static class NativeAppExitLogObserverDiagnosticFormatter
{
    public static NativeAppExitLogObserverDiagnosticFormatResult FormatResult(in NativeAppExitLogObserverResult result, Span<byte> destination)
    {
        NativeAppExitLogObserverDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != NativeAppExitLogObserverDiagnosticFormatStatus.Success)
        {
            return NativeAppExitLogObserverDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return NativeAppExitLogObserverDiagnosticFormatResult.Fail(NativeAppExitLogObserverDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("APP_EXIT_OBSERVER status="u8, destination, ref offset) || !TryAppendBytes(GetObserverStatusName(result.Status), destination, ref offset) || !TryAppendBytes(" recordsScanned="u8, destination, ref offset) || !TryAppendInt64Decimal(result.RecordsScanned, destination, ref offset) || !TryAppendBytes(" recordIndex="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.RecordIndex, destination, ref offset) || !TryAppendBytes(" streamId="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.StreamId, destination, ref offset) || !TryAppendBytes(" flags="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.Flags, destination, ref offset) || !TryAppendBytes(" sequence="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.Sequence, destination, ref offset) || !TryAppendBytes(" bytesRead="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.BytesRead, destination, ref offset) || !TryAppendBytes(" requiredBytes="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.RequiredBytes, destination, ref offset) || !TryAppendBytes(" logStatus="u8, destination, ref offset) || !TryAppendBytes(GetOSHostStatusName(result.LogStatus), destination, ref offset) || !TryAppendBytes(" parseAttempted="u8, destination, ref offset) || !TryAppendBool(result.ParseAttempted, destination, ref offset) || !TryAppendBytes(" parseStatus="u8, destination, ref offset) || !TryAppendBytes(GetParseStatusName(result.ParseStatus), destination, ref offset) || !TryAppendBytes(" exitCode="u8, destination, ref offset) || !TryAppendInt64Decimal(result.ExitCode, destination, ref offset) || !TryAppendBytes(" exitStatus="u8, destination, ref offset) || !TryAppendBytes(GetOSHostStatusName(result.ExitStatus), destination, ref offset) || !TryAppendBytes(" launcherFailure="u8, destination, ref offset) || !TryAppendBool(result.LauncherFailure, destination, ref offset))
        {
            return NativeAppExitLogObserverDiagnosticFormatResult.Fail(NativeAppExitLogObserverDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        return new NativeAppExitLogObserverDiagnosticFormatResult(NativeAppExitLogObserverDiagnosticFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static NativeAppExitLogObserverDiagnosticFormatResult QueryRequiredBytes(in NativeAppExitLogObserverResult result)
    {
        NativeAppExitLogObserverDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != NativeAppExitLogObserverDiagnosticFormatStatus.Success)
        {
            return NativeAppExitLogObserverDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new NativeAppExitLogObserverDiagnosticFormatResult(NativeAppExitLogObserverDiagnosticFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static NativeAppExitLogObserverDiagnosticFormatStatus Validate(in NativeAppExitLogObserverResult result)
    {
        if (!result.IsInitialized || result.RecordsScanned < 0 || result.ExitCode < NativeAppExitStatusFormatter.MinimumExitCode || result.ExitCode > NativeAppExitStatusFormatter.MaximumExitCode || GetObserverStatusName(result.Status).Length == 0 || GetParseStatusName(result.ParseStatus).Length == 0 || GetOSHostStatusName(result.LogStatus).Length == 0 || GetOSHostStatusName(result.ExitStatus).Length == 0)
        {
            return NativeAppExitLogObserverDiagnosticFormatStatus.InvalidResult;
        }

        return NativeAppExitLogObserverDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in NativeAppExitLogObserverResult result)
    {
        return "APP_EXIT_OBSERVER status="u8.Length + GetObserverStatusName(result.Status).Length + " recordsScanned="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.RecordsScanned) + " recordIndex="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.RecordIndex) + " streamId="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.StreamId) + " flags="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.Flags) + " sequence="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.Sequence) + " bytesRead="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.BytesRead) + " requiredBytes="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.RequiredBytes) + " logStatus="u8.Length + GetOSHostStatusName(result.LogStatus).Length + " parseAttempted="u8.Length + GetBoolByteCount(result.ParseAttempted) + " parseStatus="u8.Length + GetParseStatusName(result.ParseStatus).Length + " exitCode="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.ExitCode) + " exitStatus="u8.Length + GetOSHostStatusName(result.ExitStatus).Length + " launcherFailure="u8.Length + GetBoolByteCount(result.LauncherFailure);
    }

    private static ReadOnlySpan<byte> GetObserverStatusName(NativeAppExitLogObserverStatus status)
    {
        return status switch
        {
            NativeAppExitLogObserverStatus.Success => "Success"u8,
            NativeAppExitLogObserverStatus.NoExitMarker => "NoExitMarker"u8,
            NativeAppExitLogObserverStatus.MalformedExitMarker => "MalformedExitMarker"u8,
            NativeAppExitLogObserverStatus.InvalidLogTable => "InvalidLogTable"u8,
            NativeAppExitLogObserverStatus.InvalidRecord => "InvalidRecord"u8,
            NativeAppExitLogObserverStatus.RecordBufferTooSmall => "RecordBufferTooSmall"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetParseStatusName(NativeAppExitParseStatus status)
    {
        return status switch
        {
            NativeAppExitParseStatus.Success => "Success"u8,
            NativeAppExitParseStatus.InvalidPrefix => "InvalidPrefix"u8,
            NativeAppExitParseStatus.InvalidFieldOrder => "InvalidFieldOrder"u8,
            NativeAppExitParseStatus.MalformedCode => "MalformedCode"u8,
            NativeAppExitParseStatus.InvalidStatusToken => "InvalidStatusToken"u8,
            NativeAppExitParseStatus.ExtraFields => "ExtraFields"u8,
            NativeAppExitParseStatus.TruncatedFields => "TruncatedFields"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetOSHostStatusName(OSHostStatus status)
    {
        return status switch
        {
            OSHostStatus.Ok => "OSHOST_STATUS_OK"u8,
            OSHostStatus.InvalidArgument => "OSHOST_STATUS_INVALID_ARGUMENT"u8,
            OSHostStatus.NotSupported => "OSHOST_STATUS_NOT_SUPPORTED"u8,
            OSHostStatus.NotFound => "OSHOST_STATUS_NOT_FOUND"u8,
            OSHostStatus.AccessDenied => "OSHOST_STATUS_ACCESS_DENIED"u8,
            OSHostStatus.BufferTooSmall => "OSHOST_STATUS_BUFFER_TOO_SMALL"u8,
            OSHostStatus.OutOfMemory => "OSHOST_STATUS_OUT_OF_MEMORY"u8,
            OSHostStatus.WouldBlock => "OSHOST_STATUS_WOULD_BLOCK"u8,
            OSHostStatus.TimedOut => "OSHOST_STATUS_TIMED_OUT"u8,
            OSHostStatus.Interrupted => "OSHOST_STATUS_INTERRUPTED"u8,
            OSHostStatus.EndOfFile => "OSHOST_STATUS_END_OF_FILE"u8,
            OSHostStatus.ConnectionReset => "OSHOST_STATUS_CONNECTION_RESET"u8,
            OSHostStatus.ConnectionRefused => "OSHOST_STATUS_CONNECTION_REFUSED"u8,
            OSHostStatus.AddressInUse => "OSHOST_STATUS_ADDR_IN_USE"u8,
            OSHostStatus.AddressNotAvailable => "OSHOST_STATUS_ADDR_NOT_AVAILABLE"u8,
            OSHostStatus.NetworkDown => "OSHOST_STATUS_NETWORK_DOWN"u8,
            OSHostStatus.TryAgain => "OSHOST_STATUS_TRY_AGAIN"u8,
            OSHostStatus.IoError => "OSHOST_STATUS_IO_ERROR"u8,
            OSHostStatus.InternalError => "OSHOST_STATUS_INTERNAL_ERROR"u8,
            OSHostStatus.Closed => "OSHOST_STATUS_CLOSED"u8,
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
