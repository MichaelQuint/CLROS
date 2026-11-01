using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct EnaPciResourcePlan
{
    internal EnaPciResourcePlan(int sourceIndex, EnaPciDeviceKind deviceKind, PciEcamScannedDevice device, PciBarResourcePlan registerBar, PciMsixCapabilitySnapshot msix, PciMsixRegionPlan msixTable, PciMsixRegionPlan msixPendingBitArray)
    {
        SourceIndex = sourceIndex;
        DeviceKind = deviceKind;
        Device = device;
        RegisterBar = registerBar;
        Msix = msix;
        MsixTable = msixTable;
        MsixPendingBitArray = msixPendingBitArray;
    }

    public int SourceIndex { get; }
    public EnaPciDeviceKind DeviceKind { get; }
    public PciEcamScannedDevice Device { get; }
    public PciBarResourcePlan RegisterBar { get; }
    public PciMsixCapabilitySnapshot Msix { get; }
    public PciMsixRegionPlan MsixTable { get; }
    public PciMsixRegionPlan MsixPendingBitArray { get; }
}
