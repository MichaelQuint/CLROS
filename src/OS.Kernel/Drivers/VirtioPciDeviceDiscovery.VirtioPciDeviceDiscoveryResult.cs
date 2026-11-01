namespace OS.Drivers;
public readonly struct VirtioPciDeviceDiscoveryResult
{
    private VirtioPciDeviceDiscoveryResult(VirtioPciDeviceDiscoveryStatus status, PciDeviceIdentity identity, VirtioPciDeviceKind deviceKind, byte virtioDeviceId, VirtioPciCapabilitySet capabilities, bool isInitialized)
    {
        Status = status;
        Identity = identity;
        DeviceKind = deviceKind;
        VirtioDeviceId = virtioDeviceId;
        Capabilities = capabilities;
        IsInitialized = isInitialized;
    }

    public VirtioPciDeviceDiscoveryStatus Status { get; }
    public PciDeviceIdentity Identity { get; }
    public VirtioPciDeviceKind DeviceKind { get; }
    public byte VirtioDeviceId { get; }
    public VirtioPciCapabilitySet Capabilities { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VirtioPciDeviceDiscoveryStatus.Success;

    public static VirtioPciDeviceDiscoveryResult Success(PciDeviceIdentity identity, VirtioPciDeviceKind deviceKind, byte virtioDeviceId, VirtioPciCapabilitySet capabilities)
    {
        return new VirtioPciDeviceDiscoveryResult(VirtioPciDeviceDiscoveryStatus.Success, identity, deviceKind, virtioDeviceId, capabilities, isInitialized: true);
    }

    public static VirtioPciDeviceDiscoveryResult Fail(VirtioPciDeviceDiscoveryStatus status, PciDeviceIdentity identity, byte virtioDeviceId, VirtioPciDeviceKind deviceKind)
    {
        return new VirtioPciDeviceDiscoveryResult(status, identity, deviceKind, virtioDeviceId, default, isInitialized: true);
    }
}
