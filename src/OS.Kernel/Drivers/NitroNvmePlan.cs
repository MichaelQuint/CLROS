using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct NitroNvmePlan
{
    internal NitroNvmePlan(int sourceIndex, PciEcamScannedDevice device, ushort subsystemVendorId, ushort subsystemId, PciBarResourcePlan registerBar, PciMsixCapabilitySnapshot msix, NitroNvmeNamespaceIdentity namespaceIdentity)
    {
        SourceIndex = sourceIndex;
        Device = device;
        SubsystemVendorId = subsystemVendorId;
        SubsystemId = subsystemId;
        RegisterBar = registerBar;
        Msix = msix;
        NamespaceIdentity = namespaceIdentity;
    }

    public int SourceIndex { get; }
    public PciEcamScannedDevice Device { get; }
    public ushort SubsystemVendorId { get; }
    public ushort SubsystemId { get; }
    public PciBarResourcePlan RegisterBar { get; }
    public PciMsixCapabilitySnapshot Msix { get; }
    public NitroNvmeNamespaceIdentity NamespaceIdentity { get; }
}
