using OS.Drivers;

internal static class BARPrefetchTypeRegressionTestsDerivedMOSAFailuresTests
{
    // Independent vectors inspired by MOSA PCIDeviceConfiguration.cs: (bar & 8) == 1
    // loses prefetch; treating every slot as 32-bit loses the upper half. No source reuse.
    [HostTest("m05.009 prefetch bit and memory type are independent")]
    private static void Types()
    {
        Check(0x80000000, 0, 0xfffff000, 0, 0, PciSizedBarKind.Memory32, 0x80000000, 4096, false);
        Check(0x80000008, 0, 0xfffff008, 0, 1, PciSizedBarKind.Memory32, 0x80000000, 4096, true);
        Check(0x20000004, 3, 0xffe00004, 0xffffffff, 2, PciSizedBarKind.Memory64, 0x320000000, 0x200000, false);
        Check(0x2000000c, 3, 0xffe0000c, 0xffffffff, 4, PciSizedBarKind.Memory64, 0x320000000, 0x200000, true);
        Check(0xc001, 0, 0xffffff01, 0, 5, PciSizedBarKind.Io32, 0xc000, 256, false);
        Check(0, 0, 0, 0, 0, PciSizedBarKind.Absent, 0, 0, false);
        Check(0, 0, 0xfffff000, 0, 0, PciSizedBarKind.Memory32, 0, 4096, false);
    }

    [HostTest("m05.009 rejects reserved types masks pairing and wrapped extents")]
    private static void Invalid()
    {
        foreach (uint low in new uint[] { 2, 6, 10, 14, 3 })
            HostCheck.True(BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(low, 0, low, 0, 0, out _) == PciBarProbeStatus.UnsupportedType);
        HostCheck.True(BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(4, 1, 0xfffff004, uint.MaxValue, 5, out _) == PciBarProbeStatus.UnsupportedType);
        Reject(0x80000008, 0, 0xfffff000, 0, PciBarProbeStatus.InvalidMask);
        Reject(0x80000000, 0, 0, 0, PciBarProbeStatus.InvalidMask);
        Reject(0x80000000, 0, 0xffff5000, 0, PciBarProbeStatus.InvalidMask);
        Reject(0x80000010, 0, 0xfffff000, 0, PciBarProbeStatus.InvalidExtent);
        Reject(0xfffff004, uint.MaxValue, 0xfffff004, uint.MaxValue, PciBarProbeStatus.InvalidExtent);
        Reject(4, 0, 4, 0, PciBarProbeStatus.InvalidMask);
        HostCheck.True(BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0, 0, 0, 0, 6, out _) == PciBarProbeStatus.InvalidInput);
    }

    private static void Check(uint low, uint high, uint maskLow, uint maskHigh, uint slot,
        PciSizedBarKind kind, ulong address, ulong bytes, bool prefetch)
    {
        HostCheck.True(BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(low, high, maskLow, maskHigh, slot, out var bar) == PciBarProbeStatus.Success);
        HostCheck.True(bar.Kind == kind && bar.Base == address && bar.Bytes == bytes && bar.Prefetchable == prefetch && bar.IsAssigned == (address != 0 && bytes != 0));
    }
    private static void Reject(uint low, uint high, uint maskLow, uint maskHigh, PciBarProbeStatus status)
    {
        HostCheck.True(BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(low, high, maskLow, maskHigh, 0, out var bar) == status);
        HostCheck.True(bar.Kind == PciSizedBarKind.Uninitialized && !bar.IsAssigned);
    }
}
