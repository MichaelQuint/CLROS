using OS.Drivers;

internal static class VirtqueuePowerTwoConstraintsIndexWidthNegotiatedMaximumTests
{
    [HostTest("virtqueue.size admits exact powers of two before narrowing", HostLane.Fast, 0)]
    private static bool AdmitsSizes()
    {
        HostCheck.True(default(VirtqueueSizeStatus) != VirtqueueSizeStatus.Success);
        HostCheck.Equal(32768u, BoundedVirtqueueSize.Maximum);
        for (uint value = 1; value <= 32768; value *= 2)
        {
            HostCheck.Equal(VirtqueueSizeStatus.Success, BoundedVirtqueueSize.Validate(value, 32768, out ushort size));
            HostCheck.Equal((ushort)value, size);
            HostCheck.Equal(VirtqueueSizeStatus.Success, BoundedVirtqueueSize.Validate(value, value, out size));
            HostCheck.Equal((ushort)value, size);
        }
        return true;
    }

    [HostTest("virtqueue.size rejects absent malformed and insufficient device maxima", HostLane.Fast, 1)]
    private static bool RejectsSizes()
    {
        foreach (uint value in new uint[] { 0, 3, 7, 32769, 65535, 65536, 65544, uint.MaxValue })
        {
            HostCheck.Equal(VirtqueueSizeStatus.InvalidRequested, BoundedVirtqueueSize.Validate(value, 32768, out ushort size));
            HostCheck.Equal((ushort)0, size);
        }
        foreach (uint value in new uint[] { 3, 7, 32769, 65535, 65536, uint.MaxValue })
        {
            HostCheck.Equal(VirtqueueSizeStatus.InvalidMaximum, BoundedVirtqueueSize.Validate(8, value, out ushort size));
            HostCheck.Equal((ushort)0, size);
        }
        HostCheck.Equal(VirtqueueSizeStatus.Unavailable, BoundedVirtqueueSize.Validate(8, 0, out ushort absent));
        HostCheck.Equal((ushort)0, absent);
        HostCheck.Equal(VirtqueueSizeStatus.ExceedsMaximum, BoundedVirtqueueSize.Validate(16, 8, out ushort tooLarge));
        HostCheck.Equal((ushort)0, tooLarge);
        return true;
    }

    [HostTest("virtqueue.size checks descriptor ids and bounded wrapping index distance", HostLane.Fast, 2)]
    private static bool ChecksIndices()
    {
        HostCheck.Equal(VirtqueueSizeStatus.Success, BoundedVirtqueueSize.TryDescriptor(32767, 32768, out ushort index));
        HostCheck.Equal((ushort)32767, index);
        foreach (uint value in new uint[] { 8, 65536, 65537, uint.MaxValue })
        {
            HostCheck.Equal(VirtqueueSizeStatus.InvalidIndex, BoundedVirtqueueSize.TryDescriptor(value, 8, out index));
            HostCheck.Equal((ushort)0, index);
        }
        HostCheck.Equal(VirtqueueSizeStatus.InvalidRequested, BoundedVirtqueueSize.TryDescriptor(0, 0, out index));
        HostCheck.Equal(VirtqueueSizeStatus.InvalidRequested, BoundedVirtqueueSize.CountPending(0, 0, 3, out ushort count));
        HostCheck.Equal((ushort)0, count);
        foreach (var pair in new (ushort Consumed, ushort Produced, ushort Expected)[]
            { (0, 0, 0), (0, 8, 8), (65535, 0, 1), (65530, 2, 8), (65535, 32767, 32768) })
        {
            ushort size = pair.Expected > 8 ? (ushort)32768 : (ushort)8;
            HostCheck.Equal(VirtqueueSizeStatus.Success, BoundedVirtqueueSize.CountPending(pair.Consumed, pair.Produced, size, out count));
            HostCheck.Equal(pair.Expected, count);
        }
        foreach (var pair in new (ushort Consumed, ushort Produced)[] { (0, 9), (1, 0), (65530, 3) })
        {
            HostCheck.Equal(VirtqueueSizeStatus.InvalidProgress, BoundedVirtqueueSize.CountPending(pair.Consumed, pair.Produced, 8, out count));
            HostCheck.Equal((ushort)0, count);
        }
        return true;
    }
}
