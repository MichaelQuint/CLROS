using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct EnaPciResourceSnapshot
{
    public EnaPciResourceSnapshot(int sourceIndex, PciEcamScannedDevice device, PciBarDescriptorSet bars, PciBarApertureSet apertures, PciMsixCapabilitySnapshot msix, byte registerBarIndex)
    {
        SourceIndex = sourceIndex;
        Device = device;
        Bars = bars;
        Apertures = apertures;
        Msix = msix;
        RegisterBarIndex = registerBarIndex;
    }

    public int SourceIndex { get; }
    public PciEcamScannedDevice Device { get; }
    public PciBarDescriptorSet Bars { get; }
    public PciBarApertureSet Apertures { get; }
    public PciMsixCapabilitySnapshot Msix { get; }
    public byte RegisterBarIndex { get; }
}
