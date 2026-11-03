using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct VirtioPciBarRegisterWindowBinding
{
    internal VirtioPciBarRegisterWindowBinding(VirtioPciBarAperture aperture, VirtioPciMappedBarWindow window, ulong aperturePhysicalBaseAddress, ulong apertureVirtualBaseAddress)
    {
        Aperture = aperture;
        Window = window;
        AperturePhysicalBaseAddress = aperturePhysicalBaseAddress;
        ApertureVirtualBaseAddress = apertureVirtualBaseAddress;
        IsBound = true;
    }

    public VirtioPciBarAperture Aperture
    {
        get;
    }
    public VirtioPciMappedBarWindow Window
    {
        get;
    }
    public bool IsBound
    {
        get;
    }
    public VirtioPciCapabilityType CapabilityType => Aperture.CapabilityType;
    public byte BarIndex => Aperture.BarIndex;
    public PciBarKind BarKind => Aperture.BarKind;
    public ulong BarPhysicalBaseAddress => Window.PhysicalBaseAddress;
    public ulong BarVirtualBaseAddress => Window.VirtualBaseAddress;
    public uint ApertureOffset => Aperture.Offset;
    public uint ApertureLength => Aperture.Length;
    public ulong ApertureEndOffset => Aperture.EndOffset;
    public ulong AperturePhysicalBaseAddress
    {
        get;
    }
    public ulong ApertureVirtualBaseAddress
    {
        get;
    }
    public ulong WindowLength => Window.Length;
    public bool IsMapped => Window.IsMapped;
    public bool IsMmio => Window.IsMmio;
    public bool IsDeviceMemory => Window.IsDeviceMemory;
    public bool IsUncached => Window.IsUncached;
}
