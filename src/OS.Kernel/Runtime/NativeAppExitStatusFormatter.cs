using OS.Pal;
using OS.Std.Text;

namespace OS.Kernel.Runtime;

public static class NativeAppExitStatusFormatter
{
    public const int MinimumExitCode = 0;
    public const int MaximumExitCode = 255;
    public const int MaximumMarkerBytes = 96;
    private const string Prefix = "APP_EXIT code=";
    private const string StatusPrefix = " status=";
    private static ReadOnlySpan<byte> AppExitMarker => "APP_EXIT"u8;
    private static ReadOnlySpan<byte> AppExitCodePrefix => "APP_EXIT code="u8;
    private static ReadOnlySpan<byte> AppExitStatusPrefix => " status="u8;

    public static NativeAppExitStatusCreateResult CreateNormalExit(int exitCode, OSHostStatus status)
    {
        return Create(exitCode, status, launcherFailure: false);
    }

    public static NativeAppExitStatusCreateResult CreateLauncherFailure(OSHostStatus status)
    {
        if (status == OSHostStatus.Ok)
        {
            return NativeAppExitStatusCreateResult.Fail(NativeAppExitMarkerStatus.InvalidStatus);
        }

        return Create(MaximumExitCode, status, launcherFailure: true);
    }

    public static NativeAppExitMarkerResult TryFormat(NativeAppExitStatusRecord record, Span<byte> destination)
    {
        NativeAppExitMarkerStatus validationStatus = Validate(record);
        if (validationStatus != NativeAppExitMarkerStatus.Success)
        {
            return new NativeAppExitMarkerResult(validationStatus, 0, 0, OSHostStatus.InvalidArgument);
        }

        string statusName = OSHostStatusNames.GetConstantName(record.Status);
        int requiredBytes = Prefix.Length + AsciiDecimalFormatter.CountInt32Digits(record.ExitCode) + StatusPrefix.Length + statusName.Length;
        if (requiredBytes > MaximumMarkerBytes)
        {
            return new NativeAppExitMarkerResult(NativeAppExitMarkerStatus.MarkerTooLong, 0, requiredBytes, OSHostStatus.BufferTooSmall);
        }

        if (destination.Length < requiredBytes)
        {
            return new NativeAppExitMarkerResult(NativeAppExitMarkerStatus.DestinationTooSmall, 0, requiredBytes, OSHostStatus.BufferTooSmall);
        }

        int offset = 0;
        if (!TryAppendChars(Prefix, destination, ref offset) || !TryAppendUInt64Decimal((ulong)record.ExitCode, destination, ref offset) || !TryAppendChars(StatusPrefix, destination, ref offset) || !TryAppendChars(statusName, destination, ref offset))
        {
            return new NativeAppExitMarkerResult(NativeAppExitMarkerStatus.DestinationTooSmall, 0, requiredBytes, OSHostStatus.BufferTooSmall);
        }

        return new NativeAppExitMarkerResult(NativeAppExitMarkerStatus.Success, offset, requiredBytes, OSHostStatus.Ok);
    }

    public static NativeAppExitMarkerResult WriteToLog(NativeAppExitStatusRecord record, OSHostAppLogTable logTable)
    {
        Span<byte> marker = stackalloc byte[MaximumMarkerBytes];
        NativeAppExitMarkerResult format = TryFormat(record, marker);
        if (!format.Succeeded)
        {
            return format;
        }

        OSHostAppLogWriteResult write = logTable.Write(OSHostAppLogConstants.StreamStdout, marker[..format.BytesWritten], OSHostAppLogConstants.FlagMachineReadable);
        return write.Succeeded ? new NativeAppExitMarkerResult(NativeAppExitMarkerStatus.Success, (int)write.BytesWritten, format.RequiredBytes, write.Status) : new NativeAppExitMarkerResult(MapLogStatus(write.Status), 0, format.RequiredBytes, write.Status);
    }

    public static NativeAppExitParseResult TryParse(ReadOnlySpan<byte> line)
    {
        if (!OS.Std.MemoryExtensions.StartsWith(line, AppExitMarker))
        {
            return NativeAppExitParseResult.Fail(NativeAppExitParseStatus.InvalidPrefix);
        }

        if (line.Length == AppExitMarker.Length)
        {
            return NativeAppExitParseResult.Fail(NativeAppExitParseStatus.TruncatedFields);
        }

        if (line[AppExitMarker.Length] != (byte)' ')
        {
            return NativeAppExitParseResult.Fail(NativeAppExitParseStatus.InvalidPrefix);
        }

        if (!OS.Std.MemoryExtensions.StartsWith(line, AppExitCodePrefix))
        {
            return NativeAppExitParseResult.Fail(NativeAppExitParseStatus.InvalidFieldOrder);
        }

        int codeStart = AppExitCodePrefix.Length;
        if (codeStart == line.Length)
        {
            return NativeAppExitParseResult.Fail(NativeAppExitParseStatus.TruncatedFields);
        }

        int codeEnd = codeStart;
        while (codeEnd < line.Length && line[codeEnd] != (byte)' ')
        {
            codeEnd++;
        }

        if (!TryParseExitCode(line[codeStart..codeEnd], out int exitCode))
        {
            return NativeAppExitParseResult.Fail(NativeAppExitParseStatus.MalformedCode);
        }

        if (codeEnd == line.Length)
        {
            return NativeAppExitParseResult.Fail(NativeAppExitParseStatus.TruncatedFields);
        }

        ReadOnlySpan<byte> statusField = line[codeEnd..];
        if (!OS.Std.MemoryExtensions.StartsWith(statusField, AppExitStatusPrefix))
        {
            return NativeAppExitParseResult.Fail(NativeAppExitParseStatus.InvalidFieldOrder);
        }

        int statusStart = codeEnd + AppExitStatusPrefix.Length;
        if (statusStart == line.Length)
        {
            return NativeAppExitParseResult.Fail(NativeAppExitParseStatus.TruncatedFields);
        }

        ReadOnlySpan<byte> statusToken = line[statusStart..];
        if (ContainsAsciiSpace(statusToken))
        {
            return NativeAppExitParseResult.Fail(NativeAppExitParseStatus.ExtraFields);
        }

        if (!TryParseStatusToken(statusToken, out OSHostStatus status))
        {
            return NativeAppExitParseResult.Fail(NativeAppExitParseStatus.InvalidStatusToken);
        }

        bool launcherFailure = exitCode == MaximumExitCode && status != OSHostStatus.Ok;
        NativeAppExitStatusRecord record = new NativeAppExitStatusRecord(exitCode, status, launcherFailure);
        return NativeAppExitParseResult.Success(record, line.Length);
    }

    private static NativeAppExitStatusCreateResult Create(int exitCode, OSHostStatus status, bool launcherFailure)
    {
        if (!IsValidExitCode(exitCode))
        {
            return NativeAppExitStatusCreateResult.Fail(NativeAppExitMarkerStatus.InvalidExitCode);
        }

        if (!OSHostStatusNames.IsDefined((int)status))
        {
            return NativeAppExitStatusCreateResult.Fail(NativeAppExitMarkerStatus.InvalidStatus);
        }

        return NativeAppExitStatusCreateResult.Success(new NativeAppExitStatusRecord(exitCode, status, launcherFailure));
    }

    private static NativeAppExitMarkerStatus Validate(NativeAppExitStatusRecord record)
    {
        if (!IsValidExitCode(record.ExitCode))
        {
            return NativeAppExitMarkerStatus.InvalidExitCode;
        }

        return OSHostStatusNames.IsDefined((int)record.Status) ? NativeAppExitMarkerStatus.Success : NativeAppExitMarkerStatus.InvalidStatus;
    }

    private static bool IsValidExitCode(int exitCode)
    {
        return exitCode is >= MinimumExitCode and <= MaximumExitCode;
    }

    private static NativeAppExitMarkerStatus MapLogStatus(OSHostStatus status)
    {
        return status == OSHostStatus.BufferTooSmall ? NativeAppExitMarkerStatus.DestinationTooSmall : NativeAppExitMarkerStatus.MarkerTooLong;
    }

    private static bool TryParseExitCode(ReadOnlySpan<byte> value, out int exitCode)
    {
        exitCode = 0;
        if (value.Length == 0)
        {
            return false;
        }

        int parsed = 0;
        for (int index = 0; index < value.Length; index++)
        {
            byte digit = value[index];
            if (digit is < (byte)'0' or > (byte)'9')
            {
                return false;
            }

            parsed = (parsed * 10) + (digit - (byte)'0');
            if (parsed > MaximumExitCode)
            {
                return false;
            }
        }

        exitCode = parsed;
        return true;
    }

    private static bool TryParseStatusToken(ReadOnlySpan<byte> token, out OSHostStatus status)
    {
        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_OK"u8))
        {
            status = OSHostStatus.Ok;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_INVALID_ARGUMENT"u8))
        {
            status = OSHostStatus.InvalidArgument;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_NOT_SUPPORTED"u8))
        {
            status = OSHostStatus.NotSupported;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_NOT_FOUND"u8))
        {
            status = OSHostStatus.NotFound;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_ACCESS_DENIED"u8))
        {
            status = OSHostStatus.AccessDenied;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_BUFFER_TOO_SMALL"u8))
        {
            status = OSHostStatus.BufferTooSmall;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_OUT_OF_MEMORY"u8))
        {
            status = OSHostStatus.OutOfMemory;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_WOULD_BLOCK"u8))
        {
            status = OSHostStatus.WouldBlock;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_TIMED_OUT"u8))
        {
            status = OSHostStatus.TimedOut;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_INTERRUPTED"u8))
        {
            status = OSHostStatus.Interrupted;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_END_OF_FILE"u8))
        {
            status = OSHostStatus.EndOfFile;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_CONNECTION_RESET"u8))
        {
            status = OSHostStatus.ConnectionReset;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_CONNECTION_REFUSED"u8))
        {
            status = OSHostStatus.ConnectionRefused;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_ADDR_IN_USE"u8) || OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_ADDRESS_IN_USE"u8))
        {
            status = OSHostStatus.AddressInUse;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_ADDR_NOT_AVAILABLE"u8) || OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_ADDRESS_NOT_AVAILABLE"u8))
        {
            status = OSHostStatus.AddressNotAvailable;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_NETWORK_DOWN"u8))
        {
            status = OSHostStatus.NetworkDown;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_TRY_AGAIN"u8))
        {
            status = OSHostStatus.TryAgain;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_IO_ERROR"u8))
        {
            status = OSHostStatus.IoError;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_INTERNAL_ERROR"u8))
        {
            status = OSHostStatus.InternalError;
            return true;
        }

        if (OS.Std.MemoryExtensions.SequenceEqual(token, "OSHOST_STATUS_CLOSED"u8))
        {
            status = OSHostStatus.Closed;
            return true;
        }

        status = default;
        return false;
    }

    private static bool ContainsAsciiSpace(ReadOnlySpan<byte> value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] == (byte)' ')
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryAppendChars(ReadOnlySpan<char> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendChars(destination, offset, value);
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
