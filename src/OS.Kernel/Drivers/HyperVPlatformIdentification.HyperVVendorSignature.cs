namespace OS.Drivers;

public readonly struct HyperVVendorSignature
{
    public HyperVVendorSignature(uint ebx, uint ecx, uint edx)
    {
        Ebx = ebx;
        Ecx = ecx;
        Edx = edx;
    }

    public uint Ebx
    {
        get;
    }
    public uint Ecx
    {
        get;
    }
    public uint Edx
    {
        get;
    }
    public bool IsMicrosoftHyperV => Ebx == HyperVPlatformIdentification.MicrosoftHyperVVendorEbx && Ecx == HyperVPlatformIdentification.MicrosoftHyperVVendorEcx && Edx == HyperVPlatformIdentification.MicrosoftHyperVVendorEdx;

    public bool Equals(HyperVVendorSignature other)
    {
        return Ebx == other.Ebx && Ecx == other.Ecx && Edx == other.Edx;
    }
}
