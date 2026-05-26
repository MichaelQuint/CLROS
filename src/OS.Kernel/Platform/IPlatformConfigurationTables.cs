using OS.Boot;

namespace OS.Kernel.Platform;

public interface IPlatformConfigurationTables
{
    PlatformConfigurationTableScanResult ScanAcpiRsdp();
}

public interface IPlatformConfigurationTableSource
{
    PlatformConfigurationTableMetadata GetMetadata();
}

public static class PlatformConfigurationTableOperations
{
    public const string ScanAcpiRsdp = "ConfigurationTable.ScanAcpiRsdp";
}

public enum PlatformConfigurationTableScanStatus
{
    Unknown = 0,
    Success = 1,
    FirmwareServicesUnavailable = 2,
    AcpiRsdpScanFailed = 3
}

public readonly struct PlatformConfigurationTableMetadata
{
    public PlatformConfigurationTableMetadata(ulong tableAddress, ulong tableCount)
    {
        TableAddress = tableAddress;
        TableCount = tableCount;
    }

    public ulong TableAddress { get; }

    public ulong TableCount { get; }
}

public readonly struct PlatformConfigurationTableScanResult
{
    private PlatformConfigurationTableScanResult(
        bool succeeded,
        PlatformConfigurationTableScanStatus status,
        FirmwareServicesMode mode,
        PlatformConfigurationTableMetadata metadata,
        bool acpiScanAttempted,
        EfiConfigurationTableScanStatus acpiScanStatus,
        EfiAcpiTableKind tableKind,
        ulong rsdpAddress)
    {
        Succeeded = succeeded;
        Status = status;
        Mode = mode;
        Metadata = metadata;
        AcpiScanAttempted = acpiScanAttempted;
        AcpiScanStatus = acpiScanStatus;
        TableKind = tableKind;
        RsdpAddress = rsdpAddress;
    }

    public bool Succeeded { get; }

    public PlatformConfigurationTableScanStatus Status { get; }

    public FirmwareServicesMode Mode { get; }

    public string Operation => PlatformConfigurationTableOperations.ScanAcpiRsdp;

    public PlatformConfigurationTableMetadata Metadata { get; }

    public bool AcpiScanAttempted { get; }

    public EfiConfigurationTableScanStatus AcpiScanStatus { get; }

    public EfiAcpiTableKind TableKind { get; }

    public ulong RsdpAddress { get; }

    public static unsafe PlatformConfigurationTableScanResult ScanAcpiRsdp(
        FirmwareCallResult guardResult,
        PlatformConfigurationTableMetadata metadata)
    {
        if (!guardResult.Succeeded)
        {
            return new PlatformConfigurationTableScanResult(
                false,
                PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable,
                guardResult.Mode,
                default,
                acpiScanAttempted: false,
                EfiConfigurationTableScanStatus.NotFound,
                EfiAcpiTableKind.None,
                0);
        }

        EfiConfigurationTableScanResult scanResult = EfiConfigurationTableScanner.Scan(
            (void*)metadata.TableAddress,
            metadata.TableCount);

        if (scanResult.Succeeded)
        {
            return new PlatformConfigurationTableScanResult(
                true,
                PlatformConfigurationTableScanStatus.Success,
                guardResult.Mode,
                metadata,
                acpiScanAttempted: true,
                scanResult.Status,
                scanResult.TableKind,
                scanResult.RsdpAddress);
        }

        return new PlatformConfigurationTableScanResult(
            false,
            PlatformConfigurationTableScanStatus.AcpiRsdpScanFailed,
            guardResult.Mode,
            metadata,
            acpiScanAttempted: true,
            scanResult.Status,
            scanResult.TableKind,
            scanResult.RsdpAddress);
    }
}

public sealed class GuardedPlatformConfigurationTables : IPlatformConfigurationTables
{
    private readonly IPlatformFirmware _firmware;
    private readonly IPlatformConfigurationTableSource _source;

    public GuardedPlatformConfigurationTables(
        IPlatformFirmware firmware,
        IPlatformConfigurationTableSource source)
    {
        _firmware = firmware;
        _source = source;
    }

    public PlatformConfigurationTableScanResult ScanAcpiRsdp()
    {
        FirmwareCallResult guardResult = _firmware.RequireBootServices(
            PlatformConfigurationTableOperations.ScanAcpiRsdp);

        if (!guardResult.Succeeded)
        {
            return PlatformConfigurationTableScanResult.ScanAcpiRsdp(guardResult, default);
        }

        return PlatformConfigurationTableScanResult.ScanAcpiRsdp(
            guardResult,
            _source.GetMetadata());
    }
}
