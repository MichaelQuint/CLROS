namespace OS.Drivers;
using OS.Std.Binary;

public readonly struct EnaAdminAcqEntry
{
    internal EnaAdminAcqEntry(EnaAdminAcqCommonDescriptor commonDescriptor)
    {
        IsInitialized = true;
        CommonDescriptor = commonDescriptor;
    }

    public bool IsInitialized { get; }
    public EnaAdminAcqCommonDescriptor CommonDescriptor { get; }
}
