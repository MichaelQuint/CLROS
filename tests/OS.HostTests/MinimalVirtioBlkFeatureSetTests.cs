using OS.Drivers;
using OS.Storage;

internal static class MinimalVirtioBlkFeatureSetTests
{
    [HostTest("virtio.block.features selects only version1 and offered read-only")]
    private static void Selection()
    {
        foreach (ulong offered in new ulong[] { 1UL << 32, (1UL << 32) | 32, ulong.MaxValue })
        {
            HostCheck.Equal(VirtioFeatureStatus.Success, MinimalVirtioBlkFeatureSet.Select(offered, out var selected));
            HostCheck.Equal((1UL << 32) | (offered & 32), selected);
        }
        foreach (ulong offered in new ulong[] { 0, 32, 0xfffffffeffffffff })
        {
            HostCheck.Equal(VirtioFeatureStatus.MissingVersion1, MinimalVirtioBlkFeatureSet.Select(offered, out var selected));
            HostCheck.Equal(0UL, selected);
        }
        HostCheck.Equal(VirtioFeatureStatus.Success, BoundedVirtioFeatures.Select(ulong.MaxValue, 1UL << 32, out var network));
        HostCheck.Equal(1UL << 32, network);
        HostCheck.Equal(VirtioFeatureStatus.UnsupportedRequested,
            BoundedVirtioFeatures.Select(ulong.MaxValue, (1UL << 32) | 32, out network));
        HostCheck.Equal(0UL, network);
    }
}
