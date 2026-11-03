namespace OS.Boot;

public enum EfiAcpiTableKind
{
    None = 0,
    Acpi10 = 1,
    Acpi20 = 2
}
#if !NOSTDLIB_EFI
#endif
