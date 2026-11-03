using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct PciConfigurationHeaderResult
{
    private PciConfigurationHeaderResult(bool isInitialized, PciConfigurationHeaderStatus status, PciType0ConfigurationHeader header)
    {
        IsInitialized = isInitialized;
        Status = status;
        Header = header;
    }

    public bool IsInitialized
    {
        get;
    }
    public PciConfigurationHeaderStatus Status
    {
        get;
    }
    public PciType0ConfigurationHeader Header
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == PciConfigurationHeaderStatus.Success;

    public static PciConfigurationHeaderResult Success(PciType0ConfigurationHeader header)
    {
        return new PciConfigurationHeaderResult(true, PciConfigurationHeaderStatus.Success, header);
    }

    public static PciConfigurationHeaderResult Fail(PciConfigurationHeaderStatus status)
    {
        return new PciConfigurationHeaderResult(true, status, default);
    }
}
