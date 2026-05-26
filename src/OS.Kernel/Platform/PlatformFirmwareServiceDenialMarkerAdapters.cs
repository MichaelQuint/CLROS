namespace OS.Kernel.Platform;

public static class PlatformFirmwareServiceDenialMarkerAdapters
{
    public static PlatformFirmwareServiceDenialMarkerResult QueryRequiredBytes(
        PlatformConsoleResult result)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformConsoleStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            Span<byte>.Empty,
            write: false);
    }

    public static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedCall(
        PlatformConsoleResult result,
        Span<byte> destination)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformConsoleStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            destination,
            write: true);
    }

    public static PlatformFirmwareServiceDenialMarkerResult QueryRequiredBytes(
        PlatformMemoryMapResult result)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformMemoryMapStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            Span<byte>.Empty,
            write: false);
    }

    public static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedCall(
        PlatformMemoryMapResult result,
        Span<byte> destination)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformMemoryMapStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            destination,
            write: true);
    }

    public static PlatformFirmwareServiceDenialMarkerResult QueryRequiredBytes(
        PlatformConfigurationTableScanResult result)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            Span<byte>.Empty,
            write: false);
    }

    public static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedCall(
        PlatformConfigurationTableScanResult result,
        Span<byte> destination)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            destination,
            write: true);
    }

    public static PlatformFirmwareServiceDenialMarkerResult QueryRequiredBytes(
        PlatformTimerResult result)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformTimerStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            Span<byte>.Empty,
            write: false);
    }

    public static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedCall(
        PlatformTimerResult result,
        Span<byte> destination)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformTimerStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            destination,
            write: true);
    }

    public static PlatformFirmwareServiceDenialMarkerResult QueryRequiredBytes(
        PlatformRandomResult result)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformRandomStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            Span<byte>.Empty,
            write: false);
    }

    public static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedCall(
        PlatformRandomResult result,
        Span<byte> destination)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformRandomStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            destination,
            write: true);
    }

    public static PlatformFirmwareServiceDenialMarkerResult QueryRequiredBytes(
        PlatformPhysicalMemoryResult result)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformPhysicalMemoryStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            Span<byte>.Empty,
            write: false);
    }

    public static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedCall(
        PlatformPhysicalMemoryResult result,
        Span<byte> destination)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformPhysicalMemoryStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            destination,
            write: true);
    }

    public static PlatformFirmwareServiceDenialMarkerResult QueryRequiredBytes(
        PlatformFramebufferResult result)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformFramebufferStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            Span<byte>.Empty,
            write: false);
    }

    public static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedCall(
        PlatformFramebufferResult result,
        Span<byte> destination)
    {
        return TryFormatDeniedPlatformResult(
            result.Succeeded,
            result.Status == PlatformFramebufferStatus.FirmwareServicesUnavailable,
            result.Mode,
            result.Operation,
            destination,
            write: true);
    }

    private static PlatformFirmwareServiceDenialMarkerResult TryFormatDeniedPlatformResult(
        bool succeeded,
        bool firmwareServicesUnavailable,
        FirmwareServicesMode mode,
        string operation,
        Span<byte> destination,
        bool write)
    {
        if (succeeded)
        {
            return PlatformFirmwareServiceDenialMarkerResult.FromStatus(
                PlatformFirmwareServiceDenialMarkerStatus.AllowedCall,
                mode);
        }

        if (!firmwareServicesUnavailable)
        {
            return PlatformFirmwareServiceDenialMarkerResult.FromStatus(
                PlatformFirmwareServiceDenialMarkerStatus.InvalidResult,
                mode);
        }

        return write
            ? PlatformFirmwareServiceDenialMarker.TryFormatDeniedCall(operation, mode, destination)
            : PlatformFirmwareServiceDenialMarker.QueryRequiredBytes(operation, mode);
    }
}
