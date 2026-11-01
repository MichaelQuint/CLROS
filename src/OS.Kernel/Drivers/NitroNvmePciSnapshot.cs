using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct NitroNvmePciSnapshot
{
    public NitroNvmePciSnapshot(int sourceIndex, PciEcamScannedDevice device, ushort subsystemVendorId, ushort subsystemId, PciBarDescriptorSet bars, PciBarApertureSet apertures, PciMsixCapabilitySnapshot msix, byte registerBarIndex)
    {
        SourceIndex = sourceIndex;
        Device = device;
        SubsystemVendorId = subsystemVendorId;
        SubsystemId = subsystemId;
        Bars = bars;
        Apertures = apertures;
        Msix = msix;
        RegisterBarIndex = registerBarIndex;
    }

    public int SourceIndex { get; }
    public PciEcamScannedDevice Device { get; }
    public ushort SubsystemVendorId { get; }
    public ushort SubsystemId { get; }
    public PciBarDescriptorSet Bars { get; }
    public PciBarApertureSet Apertures { get; }
    public PciMsixCapabilitySnapshot Msix { get; }
    public byte RegisterBarIndex { get; }
}
