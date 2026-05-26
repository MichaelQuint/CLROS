namespace OS.Kernel.Platform;

public sealed class FirmwareServicesGuard : IPlatformFirmware
{
    public FirmwareServicesMode Mode { get; private set; }

    public FirmwareCallResult RequireBootServices(string operation)
    {
        if (Mode == FirmwareServicesMode.BootServicesAvailable)
        {
            return FirmwareCallResult.Allowed(operation, Mode);
        }

        return FirmwareCallResult.Denied(operation, Mode);
    }

    public FirmwareCallResult EnterUefiGoneSimulation()
    {
        if (Mode == FirmwareServicesMode.BootServicesExited)
        {
            return FirmwareCallResult.Denied("uefi.gone.simulate", Mode);
        }

        Mode = FirmwareServicesMode.UefiGoneSimulated;
        return FirmwareCallResult.Allowed("uefi.gone.simulate", Mode);
    }

    public FirmwareCallResult MarkBootServicesExited()
    {
        Mode = FirmwareServicesMode.BootServicesExited;
        return FirmwareCallResult.Allowed("uefi.exit-boot-services", Mode);
    }
}
