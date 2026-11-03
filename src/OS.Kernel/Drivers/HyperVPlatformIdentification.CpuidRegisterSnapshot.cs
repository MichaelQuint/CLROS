namespace OS.Drivers;

public readonly struct CpuidRegisterSnapshot
{
    public CpuidRegisterSnapshot(uint eax, uint ebx, uint ecx, uint edx)
    {
        Eax = eax;
        Ebx = ebx;
        Ecx = ecx;
        Edx = edx;
    }

    public uint Eax
    {
        get;
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
}
