namespace OS.Boot;

public readonly struct EfiConfigurationTableGuid
{
    public EfiConfigurationTableGuid(uint data1, ushort data2, ushort data3, byte data4, byte data5, byte data6, byte data7, byte data8, byte data9, byte data10, byte data11)
    {
        Data1 = data1;
        Data2 = data2;
        Data3 = data3;
        Data4 = data4;
        Data5 = data5;
        Data6 = data6;
        Data7 = data7;
        Data8 = data8;
        Data9 = data9;
        Data10 = data10;
        Data11 = data11;
    }

    public uint Data1
    {
        get;
    }
    public ushort Data2
    {
        get;
    }
    public ushort Data3
    {
        get;
    }
    public byte Data4
    {
        get;
    }
    public byte Data5
    {
        get;
    }
    public byte Data6
    {
        get;
    }
    public byte Data7
    {
        get;
    }
    public byte Data8
    {
        get;
    }
    public byte Data9
    {
        get;
    }
    public byte Data10
    {
        get;
    }
    public byte Data11
    {
        get;
    }
}
#if !NOSTDLIB_EFI
#endif
