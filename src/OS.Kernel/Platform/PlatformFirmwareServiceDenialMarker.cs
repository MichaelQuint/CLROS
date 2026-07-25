using OS.Std.Text;

namespace OS.Kernel.Platform;

public enum PlatformFirmwareServiceDenialMarkerStatus
{
    Success = 0,
    InvalidResult = 1,
    AllowedCall = 2,
    InvalidOperation = 3,
    DestinationTooSmall = 4
}

public readonly struct PlatformFirmwareServiceDenialMarkerResult
{
    private PlatformFirmwareServiceDenialMarkerResult(
        bool initialized,
        PlatformFirmwareServiceDenialMarkerStatus status,
        int bytesWritten,
        int requiredBytes,
        FirmwareServicesMode mode,
        int operationBytes)
    {
        IsInitialized = initialized;
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Mode = mode;
        OperationBytes = operationBytes;
    }

    public bool IsInitialized { get; }

    public PlatformFirmwareServiceDenialMarkerStatus Status { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public FirmwareServicesMode Mode { get; }

    public int OperationBytes { get; }

    public bool Succeeded => IsInitialized
        && Status == PlatformFirmwareServiceDenialMarkerStatus.Success;

    public static PlatformFirmwareServiceDenialMarkerResult FromStatus(
        PlatformFirmwareServiceDenialMarkerStatus status,
        FirmwareServicesMode mode = FirmwareServicesMode.BootServicesAvailable,
        int operationBytes = 0,
        int requiredBytes = 0)
    {
        return new PlatformFirmwareServiceDenialMarkerResult(
            initialized: true,
            status,
            bytesWritten: 0,
            requiredBytes,
            mode,
            operationBytes);
    }

    public static PlatformFirmwareServiceDenialMarkerResult Success(
        int bytesWritten,
        int requiredBytes,
        FirmwareServicesMode mode,
        int operationBytes)
    {
        return new PlatformFirmwareServiceDenialMarkerResult(
            initialized: true,
            PlatformFirmwareServiceDenialMarkerStatus.Success,
            bytesWritten,
            requiredBytes,
            mode,
            operationBytes);
    }
}

public static class PlatformFirmwareServiceDenialMarker
{
    private static ReadOnlySpan<byte> Prefix => "UEFI_SERVICE_DENIED operation="u8;

    private static ReadOnlySpan<byte> ModeSeparator => " mode="u8;

    public static PlatformFirmwareServiceDenialMarkerResult QueryRequiredBytes(
        FirmwareCallResult result)
    {
        return TryFormatDeniedCall(result, Span<byte>.Empty, write: false);
    }

    public static PlatformFirmwareServiceDenialMarkerResult QueryRequiredBytes(
        string operation,
        FirmwareServicesMode mode)
    {
        return TryFormatDeniedCall(operation, mode, Span<byte>.Empty, write: false);
    }

    public static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedCall(
        FirmwareCallResult result,
        Span<byte> destination)
    {
        return TryFormatDeniedCall(result, destination, write: true);
    }

    public static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedCall(
        string operation,
        FirmwareServicesMode mode,
        Span<byte> destination)
    {
        return TryFormatDeniedCall(operation, mode, destination, write: true);
    }

    private static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedCall(
        FirmwareCallResult result,
        Span<byte> destination,
        bool write)
    {
        if (result.Succeeded)
        {
            return PlatformFirmwareServiceDenialMarkerResult.FromStatus(
                PlatformFirmwareServiceDenialMarkerStatus.AllowedCall,
                result.Mode);
        }

        return TryFormatDeniedCall(result.Operation, result.Mode, destination, write);
    }

    private static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedCall(
        string operation,
        FirmwareServicesMode mode,
        Span<byte> destination,
        bool write)
    {
        if (mode == FirmwareServicesMode.BootServicesAvailable)
        {
            return PlatformFirmwareServiceDenialMarkerResult.FromStatus(
                PlatformFirmwareServiceDenialMarkerStatus.InvalidResult,
                mode);
        }

        int operationBytes = CountOperationBytes(operation);
        if (operationBytes <= 0)
        {
            return PlatformFirmwareServiceDenialMarkerResult.FromStatus(
                PlatformFirmwareServiceDenialMarkerStatus.InvalidOperation,
                mode);
        }

        ReadOnlySpan<byte> modeBytes = GetModeBytes(mode);
        if (modeBytes.IsEmpty)
        {
            return PlatformFirmwareServiceDenialMarkerResult.FromStatus(
                PlatformFirmwareServiceDenialMarkerStatus.InvalidResult,
                mode,
                operationBytes);
        }

        int requiredBytes = Prefix.Length
            + operationBytes
            + ModeSeparator.Length
            + modeBytes.Length;

        if (!write)
        {
            return PlatformFirmwareServiceDenialMarkerResult.Success(
                bytesWritten: 0,
                requiredBytes,
                mode,
                operationBytes);
        }

        if (destination.Length < requiredBytes)
        {
            return PlatformFirmwareServiceDenialMarkerResult.FromStatus(
                PlatformFirmwareServiceDenialMarkerStatus.DestinationTooSmall,
                mode,
                operationBytes,
                requiredBytes);
        }

        int offset = 0;
        if (!TryAppendBytes(Prefix, destination, ref offset) ||
            !TryAppendOperation(operation, destination, ref offset) ||
            !TryAppendBytes(ModeSeparator, destination, ref offset) ||
            !TryAppendBytes(modeBytes, destination, ref offset))
        {
            return PlatformFirmwareServiceDenialMarkerResult.FromStatus(
                PlatformFirmwareServiceDenialMarkerStatus.DestinationTooSmall,
                mode,
                operationBytes,
                requiredBytes);
        }

        return PlatformFirmwareServiceDenialMarkerResult.Success(
            bytesWritten: requiredBytes,
            requiredBytes,
            mode,
            operationBytes);
    }

    private static ReadOnlySpan<byte> GetModeBytes(FirmwareServicesMode mode)
    {
        return mode switch
        {
            FirmwareServicesMode.UefiGoneSimulated => "UefiGoneSimulated"u8,
            FirmwareServicesMode.BootServicesExited => "BootServicesExited"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static int CountOperationBytes(string? operation)
    {
        if (string.IsNullOrEmpty(operation))
        {
            return 0;
        }

        int count = 0;
        for (int index = 0; index < operation.Length; index++)
        {
            char value = operation[index];
            if (!IsOperationByte(value))
            {
                return -1;
            }

            count++;
        }

        return count;
    }

    private static bool TryAppendBytes(ReadOnlySpan<byte> source, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendBytes(destination, offset, source);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendOperation(string operation, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendChars(destination, offset, operation);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool IsOperationByte(char value)
    {
        return value is >= '!' and <= '~';
    }
}
