using OS.Drivers;

internal static class UnsupportedDeviceFeaturesFailedFEATURESOKStateTests
{
    [HostTest("virtio.features selects only version1 and ignores optional offers", HostLane.Fast, 0)]
    private static bool SelectsBoundedProfile()
    {
        HostCheck.Equal(0, (int)default(VirtioFeatureStatus));
        HostCheck.True(default(VirtioFeatureStatus) != VirtioFeatureStatus.Success);
        HostCheck.Equal(0x0000000100000000UL, BoundedVirtioFeatures.Version1);
        foreach (ulong offered in new ulong[] { 0x0000000100000000, 0x8000000100000001, ulong.MaxValue })
        {
            HostCheck.Equal(VirtioFeatureStatus.Success,
                BoundedVirtioFeatures.Select(offered, 0x0000000100000000, out ulong selected));
            HostCheck.Equal(0x0000000100000000UL, selected);
        }
        return true;
    }

    [HostTest("virtio.features rejects unsupported requests and missing required bit with empty output", HostLane.Fast, 1)]
    private static bool RejectsInvalidSelection()
    {
        foreach (ulong requested in new ulong[] { 0, 1, 0x0000000100000001, 0x8000000000000000, ulong.MaxValue })
        {
            ulong selected = ulong.MaxValue;
            HostCheck.Equal(VirtioFeatureStatus.UnsupportedRequested,
                BoundedVirtioFeatures.Select(ulong.MaxValue, requested, out selected));
            HostCheck.Equal(0UL, selected);
        }
        foreach (ulong offered in new ulong[] { 0, 0xffffffff, 0xfffffffeffffffff })
        {
            ulong selected = ulong.MaxValue;
            HostCheck.Equal(VirtioFeatureStatus.MissingVersion1,
                BoundedVirtioFeatures.Select(offered, 0x0000000100000000, out selected));
            HostCheck.Equal(0UL, selected);
        }
        HostCheck.Equal(VirtioFeatureStatus.UnsupportedRequested,
            BoundedVirtioFeatures.Select(0, 0, out ulong empty));
        return HostCheck.Result(empty == 0);
    }

    [HostTest("virtio.features status requires exact acknowledgement and rejects failure reset or reserved bits", HostLane.Fast, 2)]
    private static bool ExactStatus()
    {
        HostCheck.Equal(VirtioFeatureStatus.Success, BoundedVirtioFeatures.ValidateFeaturesOk(0x0b));
        HostCheck.Equal(VirtioFeatureStatus.FeaturesRejected, BoundedVirtioFeatures.ValidateFeaturesOk(0x03));
        foreach (byte status in new byte[] { 0x40, 0x80, 0xc0, 0x43, 0x8b, 0xff })
            HostCheck.Equal(VirtioFeatureStatus.DeviceFailed, BoundedVirtioFeatures.ValidateFeaturesOk(status));
        foreach (byte status in new byte[] { 0, 1, 2, 4, 8, 9, 10, 12, 15, 0x10, 0x20, 0x1b, 0x2b, 0x3f })
            HostCheck.Equal(VirtioFeatureStatus.InvalidStatus, BoundedVirtioFeatures.ValidateFeaturesOk(status));
        return true;
    }
}
