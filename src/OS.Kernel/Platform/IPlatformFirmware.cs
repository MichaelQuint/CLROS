namespace OS.Kernel.Platform;
public interface IPlatformFirmware
{
    FirmwareServicesMode Mode { get; }

    FirmwareCallResult RequireBootServices(string operation);
}
