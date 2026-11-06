using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

/// <summary>
/// Binds numeric metadata for one modern block device from an exact, stable
/// caller-owned PCI snapshot. The adapter owns device/configuration lifetime;
/// this does not prove BAR mappings, quiescence, capacity contents or block I/O.
/// The original Command value is retained without changing device state.
/// </summary>
public static unsafe class LiveVirtioBlkPCIDevice
{
    public const uint ModernBlockIdentity = 0x10421af4;

    public static VirtioBlkBindingStatus Read(ref BoundedPciAccess access, byte bus,
        byte device, byte function, out VirtioBlkPciBinding binding)
    {
        binding = default;
        if (device > 31 || function > 7)
            return VirtioBlkBindingStatus.InvalidBdf;
        uint* words = stackalloc uint[64];
        var status = ECAMPageVendorDeviceHeader.ReadSnapshot(ref access, bus, device, function, words, 64);
        if (status == BoundedPciStatus.AbsentDevice)
            return VirtioBlkBindingStatus.AbsentDevice;
        if (status != BoundedPciStatus.Success)
            return VirtioBlkBindingStatus.ReadFailed;
        return Parse((byte*)words, 256, bus, device, function, out binding);
    }

    public static VirtioBlkBindingStatus Parse(byte* config, uint bytes, byte bus,
        byte device, byte function, out VirtioBlkPciBinding binding)
    {
        binding = default;
        if (device > 31 || function > 7)
            return VirtioBlkBindingStatus.InvalidBdf;
        if (config == null || bytes != 256 || (ulong)config > ~0UL - 256)
            return VirtioBlkBindingStatus.InvalidSnapshot;
        uint identity = EndianPrimitives.ReadUInt32LittleEndian(config);
        if ((identity & 0xffff) == 0xffff)
            return VirtioBlkBindingStatus.AbsentDevice;
        if (identity != ModernBlockIdentity)
            return VirtioBlkBindingStatus.WrongIdentity;
        if ((config[0x0e] & 0x7f) != 0)
            return VirtioBlkBindingStatus.UnsupportedHeader;
        var status = ModernVirtioPCICapabilitiesLiveConfigurationSpace.Parse(config, bytes, out var capabilities);
        if (status == VirtioCapabilityStatus.MissingRequired)
            return VirtioBlkBindingStatus.MissingCapabilities;
        if (status != VirtioCapabilityStatus.Success)
            return VirtioBlkBindingStatus.InvalidCapabilities;
        if (capabilities.DeviceConfiguration.Length < 8)
            return VirtioBlkBindingStatus.DeviceConfigurationTooShort;
        binding = new(identity, (ushort)EndianPrimitives.ReadUInt32LittleEndian(config + 4),
            bus, device, function, capabilities);
        return VirtioBlkBindingStatus.Success;
    }
}
