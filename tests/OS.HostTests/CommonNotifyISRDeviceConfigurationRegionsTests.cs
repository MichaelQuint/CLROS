using OS.Drivers;

internal static unsafe class CommonNotifyISRDeviceConfigurationRegionsTests
{
    private static VirtioPciCapabilitySet Capabilities(uint commonLength = 4096, uint notifyOffset = 0x3000,
        byte bar = 4, uint deviceOffset = 0x2000, uint commonOffset = 0) => VirtioPciCapabilitySet.Create(
            new(VirtioPciCapabilityType.CommonConfiguration, bar, commonOffset, commonLength, 0),
            new(VirtioPciCapabilityType.NotifyConfiguration, bar, notifyOffset, 4096, 4),
            new(VirtioPciCapabilityType.IsrStatus, bar, 0x1000, 4096, 0),
            new(VirtioPciCapabilityType.DeviceConfiguration, bar, deviceOffset, 4096, 0));

    private static void Bars(PciSizedBar* bars)
    {
        for (int i = 0; i < 6; i++) bars[i] = default;
        HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(
            0x0000000c, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
    }

    [HostTest("virtio.block.regions admits complete capacity field without widening network contract")]
    private static void BlockCapacityExtent()
    {
        PciSizedBar* bars = stackalloc PciSizedBar[6]; Bars(bars);
        VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
        var caps = Capabilities();
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4));
        HostCheck.Equal(8U, regions[3].Bytes);
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, regions, 4));
        HostCheck.Equal(1U, regions[3].Bytes);
        foreach (uint bytes in new uint[] { 1, 7 })
        {
            for (int i = 0; i < 4; i++) regions[i] = default;
            var shortCaps = VirtioPciCapabilitySet.Create(caps.CommonConfiguration, caps.NotifyConfiguration, caps.IsrStatus,
                new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, bytes, 0));
            HostCheck.Equal(VirtioRegionStatus.InvalidCapability,
                CommonNotifyISRDeviceConfigurationRegions.PlanBlock(shortCaps, bars, 6, regions, 4));
            for (int i = 0; i < 4; i++) HostCheck.True(!regions[i].IsValid);
        }
    }

    [HostTest("virtio.regions retains high physical BAR and distinct virtual binding", HostLane.Fast, 0)]
    private static bool HighAddressAndDistinctMapping()
    {
        PciSizedBar* bars = stackalloc PciSizedBar[6]; Bars(bars);
        VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(), bars, 6, regions, 4));
        ulong[] offsets = [0, 0x3000, 0x1000, 0x2000];
        uint[] needed = [56, 2, 1, 1];
        for (int i = 0; i < 4; i++)
        {
            HostCheck.Equal(0xc000000000UL + offsets[i], regions[i].PhysicalStart);
            HostCheck.Equal(1UL, regions[i].PageCount);
            HostCheck.Equal(needed[i], regions[i].Bytes);
            HostCheck.Equal(VirtioRegionStatus.Success,
                CommonNotifyISRDeviceConfigurationRegions.Bind(regions[i], 0x400000UL + (ulong)i * 4096, out var mapped));
            HostCheck.True(mapped.IsValid);
            HostCheck.Equal(regions[i].PhysicalStart, mapped.PhysicalStart);
            HostCheck.Equal(0x400000UL + (ulong)i * 4096, mapped.VirtualStart);
        }
        return true;
    }

    [HostTest("virtio.regions rejects extents overlaps nonmemory BAR and partial outputs", HostLane.Fast, 1)]
    private static bool InvalidPlans()
    {
        HostCheck.True(default(VirtioRegionStatus) != VirtioRegionStatus.Success);
        PciSizedBar* bars = stackalloc PciSizedBar[6]; Bars(bars);
        VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
        for (int i = 0; i < 4; i++) regions[i] = default;
        HostCheck.Equal(VirtioRegionStatus.InvalidStorage, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(), null, 6, regions, 4));
        HostCheck.Equal(VirtioRegionStatus.InvalidStorage, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(), bars, 5, regions, 4));
        HostCheck.Equal(VirtioRegionStatus.InvalidStorage, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(), bars, 6, regions, 3));
        HostCheck.Equal(VirtioRegionStatus.MissingCapability, CommonNotifyISRDeviceConfigurationRegions.Plan(default, bars, 6, regions, 4));
        HostCheck.Equal(VirtioRegionStatus.InvalidCapability, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(55), bars, 6, regions, 4));
        HostCheck.Equal(VirtioRegionStatus.OutsideBar, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(notifyOffset: 0x4000), bars, 6, regions, 4));
        HostCheck.Equal(VirtioRegionStatus.OutsideBar, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(notifyOffset: 0xfffff000), bars, 6, regions, 4));
        HostCheck.Equal(VirtioRegionStatus.Overlap, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(deviceOffset: 0x1000), bars, 6, regions, 4));
        HostCheck.Equal(VirtioRegionStatus.InvalidCapability, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(deviceOffset: 0x2001), bars, 6, regions, 4));
        HostCheck.Equal(VirtioRegionStatus.InvalidBar, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(bar: 5), bars, 6, regions, 4));
        HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(
            0xc001, 0, 0xffffc001, 0, 4, out bars[4]));
        HostCheck.Equal(VirtioRegionStatus.InvalidBar, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(), bars, 6, regions, 4));
        for (int i = 0; i < 4; i++) HostCheck.True(!regions[i].IsValid);
        return true;
    }

    [HostTest("virtio.regions original map excludes ram duplicate and malformed MMIO extents", HostLane.Fast, 2)]
    private static bool MemoryAuthority()
    {
        PciSizedBar* bars = stackalloc PciSizedBar[6]; Bars(bars);
        VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(), bars, 6, regions, 4));
        ulong* rows = stackalloc ulong[10]; new Span<ulong>(rows, 10).Clear();
        rows[0] = 7; rows[1] = 0; rows[3] = 256; rows[5] = 11; rows[6] = 0xc000000000; rows[8] = 4;
        OS.Boot.FirmwareMemoryMapRecord map = new() { BufferAddress = (ulong)rows, BufferSize = 80, MapSize = 80,
            DescriptorSize = 40, DescriptorCount = 2, DescriptorVersion = 1, Flags = 1 };
        for (int i = 0; i < 4; i++) HostCheck.True(CommonNotifyISRDeviceConfigurationRegions.Admit(regions[i], map));
        foreach (ulong type in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, uint.MaxValue })
        { rows[5] = type; HostCheck.True(!CommonNotifyISRDeviceConfigurationRegions.Admit(regions[0], map)); }
        rows[5] = 0; HostCheck.True(CommonNotifyISRDeviceConfigurationRegions.Admit(regions[0], map));
        rows[8] = ulong.MaxValue; HostCheck.True(!CommonNotifyISRDeviceConfigurationRegions.Admit(regions[0], map));
        rows[8] = 4; rows[6]++; HostCheck.True(!CommonNotifyISRDeviceConfigurationRegions.Admit(regions[0], map));
        rows[6] = 0xd000000000; HostCheck.True(CommonNotifyISRDeviceConfigurationRegions.Admit(regions[0], map));
        rows[0] = 11; rows[1] = rows[6] = 0xc000000000; rows[3] = 4;
        HostCheck.True(!CommonNotifyISRDeviceConfigurationRegions.Admit(regions[0], map));
        map.Flags = 0; HostCheck.True(!CommonNotifyISRDeviceConfigurationRegions.Admit(regions[0], map));
        return true;
    }

    [HostTest("mmio.pages admission rejects malformed bounds and partial original descriptors", HostLane.Fast, 5)]
    private static void MmioPages()
    {
        ulong* rows = stackalloc ulong[5]; new Span<ulong>(rows, 5).Clear();
        rows[0] = 11; rows[1] = 0xfee00000; rows[3] = 1;
        OS.Boot.FirmwareMemoryMapRecord map = new() { BufferAddress = (ulong)rows, BufferSize = 40, MapSize = 40,
            DescriptorSize = 40, DescriptorCount = 1, DescriptorVersion = 1, Flags = 1 };
        bool Admit(ulong address, ulong pages) => CommonNotifyISRDeviceConfigurationRegions.AdmitMmioPages(address, pages, map);
        HostCheck.True(Admit(0xfee00000, 1));
        HostCheck.True(Admit(0xfec00000, 1)); // A hole still requires separate ACPI/PCI authority.
        HostCheck.True(!Admit(0xfee00000, 2));
        HostCheck.True(!Admit(0xfedff000, 2));
        foreach (ulong address in new ulong[] { 0, 0xff000, 0xfee00001, 0x800000000000, ulong.MaxValue - 4095 })
            HostCheck.True(!Admit(address, 1));
        foreach (ulong pages in new ulong[] { 0, 17, ulong.MaxValue }) HostCheck.True(!Admit(0xfee00000, pages));
        rows[0] = 7; HostCheck.True(!Admit(0xfee00000, 1));
        map.Flags = 0; HostCheck.True(!Admit(0xfec00000, 1));
    }

    [HostTest("virtqueue.regions expanded notification binding validates full address policy", HostLane.Fast, 4)]
    private static void NotificationPageBounds()
    {
        PciSizedBar* bars = stackalloc PciSizedBar[6]; Bars(bars);
        VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(), bars, 6, regions, 4));
        foreach (uint bytes in new uint[] { 0, 1, 4097, uint.MaxValue })
        {
            HostCheck.Equal(VirtioRegionStatus.InvalidCapability,
                CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], bytes, 0x400000, out var invalid));
            HostCheck.True(!invalid.IsValid);
        }
        HostCheck.Equal(VirtioRegionStatus.InvalidAddress,
            CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 4096, 0x400001, out _));
        HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(
            0xffffc00c, 0x7fff, 0xffffc00c, 0xffffffff, 4, out bars[4]));
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(Capabilities(), bars, 6, regions, 4));
        // The two-byte negotiation window is valid; expanding it to the entire
        // last lower-half page violates the existing strict mapped-end policy.
        HostCheck.Equal(VirtioRegionStatus.Success,
            CommonNotifyISRDeviceConfigurationRegions.Bind(regions[1], 0x400000, out var original));
        HostCheck.True(original.IsValid);
        HostCheck.Equal(VirtioRegionStatus.InvalidAddress,
            CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 4096, 0x400000, out var expanded));
        HostCheck.True(!expanded.IsValid && expanded.Bytes == 0);
        HostCheck.Equal(VirtioRegionStatus.Success,
            CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 4094, 0x400000, out expanded));
        HostCheck.True(expanded.IsValid && expanded.Bytes == 4094);
    }

    [HostTest("virtio.regions binding rejects default unaligned overflow and preserves offset", HostLane.Fast, 3)]
    private static bool BindingBounds()
    {
        PciSizedBar* bars = stackalloc PciSizedBar[6]; Bars(bars);
        VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(
            Capabilities(commonLength: 56, commonOffset: 4), bars, 6, regions, 4));
        var region = regions[0];
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(region, 0x200000, out var mapped));
        HostCheck.Equal(0x200004UL, mapped.VirtualStart);
        HostCheck.Equal(0xc000000004UL, mapped.PhysicalStart);
        HostCheck.Equal(VirtioRegionStatus.InvalidAddress, CommonNotifyISRDeviceConfigurationRegions.Bind(default, 0x200000, out mapped));
        HostCheck.True(!mapped.IsValid);
        foreach (ulong address in new ulong[] { 0, 1, 0x200001, 0x0000800000000000, ulong.MaxValue - 4095 })
        {
            HostCheck.Equal(VirtioRegionStatus.InvalidAddress, CommonNotifyISRDeviceConfigurationRegions.Bind(region, address, out mapped));
            HostCheck.True(!mapped.IsValid);
        }
        return true;
    }
}
