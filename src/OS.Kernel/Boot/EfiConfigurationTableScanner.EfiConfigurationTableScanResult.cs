namespace OS.Boot;

public readonly struct EfiConfigurationTableScanResult
{
    private EfiConfigurationTableScanResult(bool succeeded, EfiConfigurationTableScanStatus status, EfiAcpiTableKind tableKind, ulong rsdpAddress)
    {
        Succeeded = succeeded;
        Status = status;
        TableKind = tableKind;
        RsdpAddress = rsdpAddress;
    }

    public bool Succeeded
    {
        get;
    }
    public EfiConfigurationTableScanStatus Status
    {
        get;
    }
    public EfiAcpiTableKind TableKind
    {
        get;
    }
    public ulong RsdpAddress
    {
        get;
    }

    public static EfiConfigurationTableScanResult Success(EfiAcpiTableKind tableKind, ulong rsdpAddress)
    {
        return new EfiConfigurationTableScanResult(true, EfiConfigurationTableScanStatus.Ok, tableKind, rsdpAddress);
    }

    public static EfiConfigurationTableScanResult Fail(EfiConfigurationTableScanStatus status)
    {
        return new EfiConfigurationTableScanResult(false, status, EfiAcpiTableKind.None, 0);
    }
}
