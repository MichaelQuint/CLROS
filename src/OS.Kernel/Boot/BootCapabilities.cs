namespace OS.Boot;

[Flags]
public enum BootCapabilities
{
    None = 0,
    TextOutput = 1,
    Shutdown = 2,
    MemoryMap = 4,
    AcpiRsdp = 8
}
