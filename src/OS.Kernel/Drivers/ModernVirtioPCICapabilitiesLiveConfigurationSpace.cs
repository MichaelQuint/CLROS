namespace OS.Drivers;
// Metadata only: BAR admission, mapped bounds and device lifetime remain caller-owned.
public static unsafe class ModernVirtioPCICapabilitiesLiveConfigurationSpace
{
    public static VirtioCapabilityStatus Read(ref BoundedPciAccess access, byte bus, byte device, byte function, out VirtioPciCapabilitySet capabilities)
    {
        capabilities = default;
        uint* words = stackalloc uint[64];
        BoundedPciStatus status = ECAMPageVendorDeviceHeader.ReadSnapshot(ref access, bus, device, function, words, 64);
        if (status == BoundedPciStatus.AbsentDevice)
            return VirtioCapabilityStatus.AbsentDevice;
        if (status != BoundedPciStatus.Success)
            return VirtioCapabilityStatus.ReadFailed;
        return Parse((byte*)words, 256, out capabilities);
    }

    public static VirtioCapabilityStatus Parse(byte* config, uint length, out VirtioPciCapabilitySet capabilities)
    {
        capabilities = default;
        PciCapabilityWalkResult walk = PciCapabilityWalk.Validate(config, length);
        if (walk.Status == PciCapabilityWalkStatus.InvalidStorage || walk.Status == PciCapabilityWalkStatus.Truncated)
            return VirtioCapabilityStatus.InvalidSnapshot;
        if (walk.Status == PciCapabilityWalkStatus.AbsentDevice)
            return VirtioCapabilityStatus.AbsentDevice;
        if (walk.Status == PciCapabilityWalkStatus.UnsupportedHeader)
            return VirtioCapabilityStatus.UnsupportedHeader;
        if (!walk.Succeeded)
            return VirtioCapabilityStatus.InvalidChain;
        uint identity = OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(config);
        uint deviceId = identity >> 16;
        if ((identity & 0xffff) != 0x1af4 || deviceId < 0x1040 || deviceId > 0x107f)
            return VirtioCapabilityStatus.UnsupportedDevice;
        if ((config[0x0e] & 0x7f) != 0)
            return VirtioCapabilityStatus.UnsupportedHeader;
        if (walk.Count == 0)
            return VirtioCapabilityStatus.MissingRequired;
        byte* occupied = stackalloc byte[256];
        for (uint i = 0; i < 256; i++)
            occupied[i] = 0;
        VirtioPciCapabilityMetadata common = default, notify = default, isr = default, deviceConfig = default;
        uint found = 0;
        uint node = config[0x34];
        for (uint index = 0; index < walk.Count; index++)
        {
            bool vendor = config[node] == 9;
            uint extent = vendor ? config[node + 2] : 2U;
            if (vendor && (extent < 4 || extent > 256 - node))
                return VirtioCapabilityStatus.InvalidCapability;
            for (uint i = node; i < node + extent; i++)
            {
                if (occupied[i] != 0)
                    return VirtioCapabilityStatus.OverlappingCapabilities;
                occupied[i] = 1;
            }

            if (vendor)
            {
                byte type = config[node + 3];
                if (type >= 1 && type <= 4)
                {
                    if (extent < 5)
                        return VirtioCapabilityStatus.InvalidCapability;
                    byte bar = config[node + 4];
                    // Reserved types and BARs do not have payloads this profile may interpret.
                    if (bar <= 5)
                    {
                        if (extent < (type == 2 ? 20U : 16U))
                            return VirtioCapabilityStatus.InvalidCapability;
                        uint offset = OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(config + node + 8);
                        uint bytes = OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(config + node + 12);
                        uint minimum = type == 1 ? VirtioPciModernRegisterMap.CommonConfiguration.FirstLiveQueueSetupLength : type == 2 ? 2U : 1U;
                        uint alignment = type == 1 || type == 4 ? 4U : type == 2 ? 2U : 1U;
                        // The mapping planner checks the widened offset + length against the actual BAR.
                        if (bytes < minimum || (offset & (alignment - 1)) != 0)
                            return VirtioCapabilityStatus.InvalidCapability;
                        uint multiplier = type == 2 ? OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(config + node + 16) : 0;
                        if (multiplier != 0 && (multiplier < 2 || (multiplier & (multiplier - 1)) != 0))
                            return VirtioCapabilityStatus.InvalidCapability;
                        uint bit = 1U << type;
                        if ((found & bit) == 0)
                        {
                            var metadata = new VirtioPciCapabilityMetadata((VirtioPciCapabilityType)type, bar, offset, bytes, multiplier);
                            if (type == 1)
                                common = metadata;
                            else if (type == 2)
                                notify = metadata;
                            else if (type == 3)
                                isr = metadata;
                            else
                                deviceConfig = metadata;
                            found |= bit;
                        }
                    }
                }
            }

            node = config[node + 1];
        }

        if (found != 0x1e)
            return VirtioCapabilityStatus.MissingRequired;
        capabilities = VirtioPciCapabilitySet.Create(common, notify, isr, deviceConfig);
        return VirtioCapabilityStatus.Success;
    }
}
