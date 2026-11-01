namespace OS.Kernel.Diagnostics;
public enum Com1OwnedPortIoConfigurationStatus : byte
{
    Success = 0,
    BackendNotReady = 1,
    ConfigurationWriteFailed = 2
}
#if !NOSTDLIB_EFI
#endif
