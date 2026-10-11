using OS.Drivers;

internal static unsafe class MinimalNoOffloadNoGSONoMergeableBufferTests
{
    [HostTest("virtio.net.features drops every optional offered bit and requires modern transport")]
    private static void Selection()
    {
        const ulong version = 1UL << 32;
        for (int bit = 0; bit < 64; bit++)
        {
            HostCheck.Equal(VirtioFeatureStatus.Success, MinimalNoOffloadNoGSONoMergeableBuffer.Select(version | (1UL << bit), out var selected));
            HostCheck.Equal(version, selected);
        }
        HostCheck.Equal(VirtioFeatureStatus.Success, MinimalNoOffloadNoGSONoMergeableBuffer.Select(ulong.MaxValue, out var all));
        HostCheck.Equal(version, all);
        foreach (ulong offered in new[] { 0UL, ulong.MaxValue ^ version })
        {
            HostCheck.Equal(VirtioFeatureStatus.MissingVersion1, MinimalNoOffloadNoGSONoMergeableBuffer.Select(offered, out var selected));
            HostCheck.Equal(0UL, selected);
        }
        var rejected = MinimalNoOffloadNoGSONoMergeableBuffer.Negotiate(default, 1, null, null, null, null, null);
        HostCheck.True(!rejected.Succeeded);
    }
}
