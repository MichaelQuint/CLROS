namespace OS.Boot;

public enum EfiConfigurationTableScanStatus
{
    Ok = 0,
    NotFound = 1,
    InvalidPointer = 2,
    InvalidConfigurationTableCount = 3,
    InvalidConfigurationTableAddress = 4
}
#if !NOSTDLIB_EFI
#endif
