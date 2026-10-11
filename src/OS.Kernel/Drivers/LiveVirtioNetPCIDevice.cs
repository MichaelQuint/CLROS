using OS.Std.Binary;

namespace OS.Drivers;

public enum VirtioNetBindingStatus : byte
{
    Uninitialized, Success, InvalidSnapshot, InvalidBdf, ReadFailed, AbsentDevice,
    WrongIdentity, UnsupportedHeader, InvalidCapabilities, MissingCapabilities
}

public readonly struct VirtioNetPciBinding
{
    public readonly uint Identity;
    public readonly ushort OriginalCommand;
    public readonly byte Bus, Device, Function;
    public readonly VirtioPciCapabilitySet Capabilities;
    internal VirtioNetPciBinding(ushort command, byte bus, byte device, byte function, VirtioPciCapabilitySet capabilities)
    {
        Identity = LiveVirtioNetPCIDevice.ModernNetworkIdentity; OriginalCommand = command;
        Bus = bus; Device = device; Function = function; Capabilities = capabilities;
    }
}

/// <summary>One modern network device from an exact stable caller-owned PCI
/// snapshot. This preserves declarations and command state; the adapter must
/// independently prove exclusive ownership, BAR bounds/mappings and quiescence.
/// It does not establish MAC/configuration contents, queues or packet I/O.</summary>
public static unsafe class LiveVirtioNetPCIDevice
{
    public const uint ModernNetworkIdentity = 0x10411af4;
    public static VirtioNetBindingStatus Read(ref BoundedPciAccess access, byte bus, byte device, byte function,
        out VirtioNetPciBinding binding)
    {
        binding = default;
        if (device > 31 || function > 7) return VirtioNetBindingStatus.InvalidBdf;
        uint* words = stackalloc uint[64];
        var status = ECAMPageVendorDeviceHeader.ReadSnapshot(ref access, bus, device, function, words, 64);
        if (status == BoundedPciStatus.AbsentDevice) return VirtioNetBindingStatus.AbsentDevice;
        if (status != BoundedPciStatus.Success) return VirtioNetBindingStatus.ReadFailed;
        return Parse((byte*)words, 256, bus, device, function, out binding);
    }

    public static VirtioNetBindingStatus Parse(byte* config, uint bytes, byte bus, byte device, byte function,
        out VirtioNetPciBinding binding)
    {
        binding = default;
        if (device > 31 || function > 7) return VirtioNetBindingStatus.InvalidBdf;
        if (config == null || bytes != 256 || (ulong)config > ~0UL - 256) return VirtioNetBindingStatus.InvalidSnapshot;
        uint identity = EndianPrimitives.ReadUInt32LittleEndian(config);
        if ((identity & 0xffff) == 0xffff) return VirtioNetBindingStatus.AbsentDevice;
        if (identity != ModernNetworkIdentity) return VirtioNetBindingStatus.WrongIdentity;
        if ((config[0x0e] & 0x7f) != 0) return VirtioNetBindingStatus.UnsupportedHeader;
        var status = ModernVirtioPCICapabilitiesLiveConfigurationSpace.Parse(config, bytes, out var caps);
        if (status == VirtioCapabilityStatus.MissingRequired) return VirtioNetBindingStatus.MissingCapabilities;
        if (status != VirtioCapabilityStatus.Success) return VirtioNetBindingStatus.InvalidCapabilities;
        binding = new((ushort)EndianPrimitives.ReadUInt32LittleEndian(config + 4), bus, device, function, caps);
        return VirtioNetBindingStatus.Success;
    }
}
