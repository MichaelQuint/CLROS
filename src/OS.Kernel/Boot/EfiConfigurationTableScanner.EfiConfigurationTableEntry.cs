namespace OS.Boot;

public readonly struct EfiConfigurationTableEntry
{
    public EfiConfigurationTableEntry(EfiConfigurationTableGuid vendorGuid, ulong vendorTable)
    {
        VendorGuid = vendorGuid;
        VendorTable = vendorTable;
    }

    public EfiConfigurationTableGuid VendorGuid
    {
        get;
    }
    public ulong VendorTable
    {
        get;
    }
}
#if !NOSTDLIB_EFI
#endif
