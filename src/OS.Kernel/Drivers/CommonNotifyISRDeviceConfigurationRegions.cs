namespace OS.Drivers;

public static unsafe class CommonNotifyISRDeviceConfigurationRegions
{
    public const uint RegionCount = 4;
    // PCI assignment is positive device authority. The original owned EFI map
    // rules out RAM; firmware may omit MMIO. Later allocations must separately
    // be disjoint when the adapter installs these UC/RW/NX pages.
    public static bool Admit(VirtioConfigurationRegion region, OS.Boot.FirmwareMemoryMapRecord map) => region.IsValid && AdmitMmioPages(region.PageStart, region.PageCount, map);
    // Shared EFI-map exclusion for numerically declared ACPI/PCI MMIO. The
    // caller separately proves the original device declaration and installed UC
    // mapping. Absence from the EFI map alone is never device authority.
    public static bool AdmitMmioPages(ulong address, ulong pages, OS.Boot.FirmwareMemoryMapRecord map)
    {
        if (address < 0x100000 || (address & 4095) != 0 || pages == 0 || pages > 16 || address > 0x0000800000000000UL - pages * 4096 || !map.HasOwnedCopy() || map.DescriptorVersion != 1 || (map.BufferAddress & 7) != 0 || map.DescriptorSize < 40 || map.DescriptorSize > 256 || (map.DescriptorSize & 7) != 0 || map.DescriptorCount == 0 || map.DescriptorCount > 4096 || map.MapSize != map.DescriptorSize * map.DescriptorCount || map.MapSize > map.BufferSize || map.BufferAddress > ~0UL - map.BufferSize)
            return false;
        ulong bytes = pages * 4096;
        if (address < map.BufferAddress + map.BufferSize && map.BufferAddress < address + bytes)
            return false;
        uint covering = 0;
        for (ulong i = 0; i < map.DescriptorCount; i++)
        {
            byte* descriptor = (byte*)(map.BufferAddress + i * map.DescriptorSize);
            uint type = OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(descriptor);
            ulong start = OS.Std.Binary.EndianPrimitives.ReadUInt64LittleEndian(descriptor + 8);
            ulong descriptorPages = OS.Std.Binary.EndianPrimitives.ReadUInt64LittleEndian(descriptor + 24);
            if ((start & 4095) != 0 || descriptorPages == 0 || descriptorPages > (~0UL - start) / 4096)
                return false;
            ulong end = start + descriptorPages * 4096;
            if (address >= end || start >= address + bytes)
                continue;
            if ((type != 0 && type != 11) || start > address || end < address + bytes || ++covering != 1)
                return false;
        }

        return true;
    }

    // common, notify, ISR and device, in that order. Map only the portion used
    // by this VERSION_1-only profile; larger capability lengths remain valid.
    public static VirtioRegionStatus Plan(VirtioPciCapabilitySet capabilities, PciSizedBar* bars, uint barCount, VirtioConfigurationRegion* regions, uint capacity) => PlanCore(capabilities, bars, barCount, regions, capacity, false);
    /// <summary>Same bounded mappings with the complete eight-byte block capacity
    /// field admitted. A stable config-generation read remains a separate step.</summary>

    public static VirtioRegionStatus PlanBlock(VirtioPciCapabilitySet capabilities, PciSizedBar* bars, uint barCount, VirtioConfigurationRegion* regions, uint capacity) => PlanCore(capabilities, bars, barCount, regions, capacity, true);
    private static VirtioRegionStatus PlanCore(VirtioPciCapabilitySet capabilities, PciSizedBar* bars, uint barCount, VirtioConfigurationRegion* regions, uint capacity, bool block)
    {
        if (bars == null || regions == null || barCount != 6 || capacity < RegionCount || ((ulong)bars & 7) != 0 || ((ulong)regions & 7) != 0 || (ulong)bars > ~0UL - 6UL * (ulong)sizeof(PciSizedBar) || (ulong)regions > ~0UL - RegionCount * (ulong)sizeof(VirtioConfigurationRegion))
            return VirtioRegionStatus.InvalidStorage;
        if (!capabilities.HasCommonConfiguration || !capabilities.HasNotifyConfiguration || !capabilities.HasIsrStatus || !capabilities.HasDeviceConfiguration)
            return VirtioRegionStatus.MissingCapability;
        VirtioConfigurationRegion* pending = stackalloc VirtioConfigurationRegion[4];
        for (uint i = 0; i < RegionCount; i++)
        {
            var cap = i == 0 ? capabilities.CommonConfiguration : i == 1 ? capabilities.NotifyConfiguration : i == 2 ? capabilities.IsrStatus : capabilities.DeviceConfiguration;
            uint needed = i == 0 ? VirtioPciModernRegisterMap.CommonConfiguration.FirstLiveQueueSetupLength : i == 1 ? 2u : i == 3 && block ? 8u : 1u;
            uint alignment = i == 0 || i == 3 ? 4u : i == 1 ? 2u : 1u;
            if ((uint)cap.Type != i + 1 || cap.Bar >= 6 || cap.Length < needed || (cap.Offset & (alignment - 1)) != 0 || (i == 1 && cap.NotifyOffsetMultiplier != 0 && (cap.NotifyOffsetMultiplier < 2 || (cap.NotifyOffsetMultiplier & (cap.NotifyOffsetMultiplier - 1)) != 0)))
                return VirtioRegionStatus.InvalidCapability;
            var bar = bars[cap.Bar];
            if ((bar.Kind != PciSizedBarKind.Memory32 && bar.Kind != PciSizedBarKind.Memory64) || !bar.IsAssigned || (bar.Bytes & (bar.Bytes - 1)) != 0 || (bar.Base & (bar.Bytes - 1)) != 0 || bar.Base > ~0UL - bar.Bytes || (bar.Kind == PciSizedBarKind.Memory32 && bar.Base + bar.Bytes > 0x100000000UL))
                return VirtioRegionStatus.InvalidBar;
            if ((ulong)cap.Offset + cap.Length > bar.Bytes)
                return VirtioRegionStatus.OutsideBar;
            pending[i] = new(bar.Base + cap.Offset, needed);
            if (!pending[i].IsValid)
                return VirtioRegionStatus.InvalidAddress;
            for (uint j = 0; j < i; j++)
                if (pending[i].PageStart < pending[j].PageStart + pending[j].PageCount * 4096 && pending[j].PageStart < pending[i].PageStart + pending[i].PageCount * 4096)
                    return VirtioRegionStatus.Overlap;
        }

        // No partial declaration is exposed when any region fails admission.
        for (uint i = 0; i < RegionCount; i++)
            regions[i] = pending[i];
        return VirtioRegionStatus.Success;
    }

    /// <summary>Numeric binding for notification offsets within one already
    /// installed UC/RW/NX page. Pass the capability length admitted by Plan;
    /// the adapter must authenticate the entire page and its lifetime.</summary>

    public static VirtioRegionStatus BindNotificationPage(VirtioConfigurationRegion region, uint capabilityLength, ulong virtualPage, out VirtioMappedRegion mapped)
    {
        mapped = default;
        if (!region.IsValid || region.Bytes != 2 || region.PageCount != 1 || capabilityLength < 2 || capabilityLength > 4096 - (region.PhysicalStart & 4095))
            return VirtioRegionStatus.InvalidCapability;
        var status = Bind(region, virtualPage, out var first);
        if (status != VirtioRegionStatus.Success)
            return status;
        var expanded = new VirtioMappedRegion(first.PhysicalStart, first.VirtualStart, capabilityLength);
        if (!expanded.IsValid)
            return VirtioRegionStatus.InvalidAddress;
        mapped = expanded;
        return VirtioRegionStatus.Success;
    }

    public static VirtioRegionStatus Bind(VirtioConfigurationRegion region, ulong virtualPage, out VirtioMappedRegion mapped)
    {
        mapped = default;
        if (!region.IsValid || virtualPage < 4096 || (virtualPage & 4095) != 0 || virtualPage >= 0x0000800000000000UL - region.PageCount * 4096)
            return VirtioRegionStatus.InvalidAddress;
        mapped = new(region.PhysicalStart, virtualPage + (region.PhysicalStart & 4095), region.Bytes);
        return VirtioRegionStatus.Success;
    }
}
