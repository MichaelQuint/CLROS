namespace OS.Drivers;

public interface IPciConfigDwordAccessor
{
    PciConfigDwordAccessStatus Write32(PciEcamConfigurationAddressResult address, uint value);
    PciConfigDwordReadResult Read32(PciEcamConfigurationAddressResult address);
}
