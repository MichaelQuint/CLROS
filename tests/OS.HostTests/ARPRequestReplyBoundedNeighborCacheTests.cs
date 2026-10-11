using OS.Drivers;
using OS.Net;

internal static unsafe class ARPRequestReplyBoundedNeighborCacheTests
{
    [HostTest("virtio.net.ARP live adapter caches only matching no-offload reply", HostLane.Fast, 0)]
    private static void ReplyCache()
    {
        Span<ArpNeighborCacheEntry> entries = stackalloc ArpNeighborCacheEntry[1];
        for (int bad = -1; bad < 9; bad++)
        {
            using var f = new QueueTests.Fixture(); EthernetFrameItExistingClassifierTests.Configure(f);
            byte[] reply = EthernetFrameItExistingClassifierTests.Reply();
            byte[] output = EthernetFrameItExistingClassifierTests.Output();
            // Independent offsets: operation, sender IPv4, target IPv4/MAC,
            // Ethernet destination/source, checksum flag, num_buffers, cache full.
            int[] offsets = [33,40,53,44,17,18,0,10];
            if (bad >= 0 && bad < offsets.Length) reply[offsets[bad]] ^= 1;
            var cache = ArpNeighborCache.Create(entries);
            if (bad == 8) cache.InsertOrUpdate(new(10,0,2,9), new(2,0,0,0,0,9), 1, out _);
            f.State->Sending = true; f.State->Receiving = true;
            ARPRequestReplyBoundedNeighborCacheResult r;
            fixed (byte* source = reply) fixed (byte* destination = output)
            {
                f.State->ReceiveSource = source; f.State->OutputSentinel = destination;
                r = ARPRequestReplyBoundedNeighborCache.Run(f.Common, f.Notify, 4, f.Pool, f.Access, 3, 3,
                    1UL << 32, new(2,0,0,0,0,1), new(10,0,2,15), new(10,0,2,2), ref cache, 100, output);
            }
            HostCheck.Equal(bad == -1, r.Succeeded);
            if (bad == -1)
            {
                HostCheck.Equal(ArpNeighborCacheStatus.Success, cache.TryLookup(new(10,0,2,2), out var found));
                HostCheck.Equal(new MacAddress(0x52,0x55,10,0,2,2), found.Entry.MacAddress);
                HostCheck.Equal(100UL, found.Entry.LastUpdatedTick); HostCheck.Equal(42, r.Request.BytesWritten);
            }
            else
            {
                HostCheck.Equal(bad == 8 ? 1 : 0, cache.Count);
                HostCheck.Equal(ArpNeighborCacheStatus.NotFound, cache.TryLookup(new(10,0,2,2), out _));
                if (bad == 8) HostCheck.Equal(ArpNeighborCacheStatus.CapacityExceeded, r.Cache.Status);
            }
            HostCheck.True(r.Receive.Queue.DmaReleased && !f.State->OutputChangedBeforeQuiescence);
            QueueTests.Clean(f);
        }
        HostCheck.True(!default(ARPRequestReplyBoundedNeighborCacheResult).Succeeded);
    }
}
