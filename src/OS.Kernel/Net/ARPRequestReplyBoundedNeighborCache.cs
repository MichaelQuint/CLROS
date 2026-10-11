using OS.Drivers;

namespace OS.Net;

public struct ARPRequestReplyBoundedNeighborCacheResult
{
    public EthernetFrameReceiveResult Receive;
    public VirtioNetHeaderChecksumPolicyResult Policy;
    public ArpNeighborResolutionResult Request, Reply;
    public ArpNeighborCacheMutationResult Cache;
    public readonly bool Succeeded => Receive.Succeeded && Policy.Succeeded &&
        Request.Succeeded && Reply.Succeeded && Cache.Succeeded;
}

/// <summary>One live request/reply, then a bounded caller-owned cache update.
/// No cache mutation occurs before checked receive/quiescence, header policy
/// and matching ARP peer/local identities. Ticks are supplied by the caller.</summary>
public static unsafe class ARPRequestReplyBoundedNeighborCache
{
    public static ARPRequestReplyBoundedNeighborCacheResult Run(VirtioMappedRegion common,
        VirtioMappedRegion notify, uint multiplier, DmaBufferPool* dma, VirtqueuePollingAccess access,
        uint pollLimit, uint resetLimit, ulong negotiatedFeatures, MacAddress localMac,
        Ipv4Address localAddress, Ipv4Address targetAddress, ref ArpNeighborCache cache,
        ulong currentTick, Span<byte> receiveBuffer)
    {
        ARPRequestReplyBoundedNeighborCacheResult result = default;
        if (negotiatedFeatures != VirtioNetHeaderChecksumPolicy.AcceptedFeatures)
        { result.Policy.Status = VirtioNetHeaderChecksumPolicyStatus.UnsupportedFeatures; return result; }
        ArpNeighborResolutionTransaction transaction =
            ArpNeighborResolutionTransaction.Create(localMac, localAddress, targetAddress);
        Span<byte> frame = stackalloc byte[60]; frame.Clear();
        if (transaction.TryWriteRequest(frame, out result.Request) != ArpNeighborResolutionStatus.Success) return result;
        result.Receive = EthernetFrameItExistingClassifier.Run(common, notify, multiplier, dma, access,
            pollLimit, resetLimit, frame, receiveBuffer);
        if (!result.Receive.Succeeded) return result;
        ReadOnlySpan<byte> received = receiveBuffer.Slice(0, (int)result.Receive.CapturedBytes);
        result.Policy = VirtioNetHeaderChecksumPolicy.Validate(negotiatedFeatures, received);
        if (!result.Policy.Succeeded) return result;
        if (transaction.ReceiveReply(received.Slice(VirtioNetHeader.ModernHeaderLength),
            out result.Reply) != ArpNeighborResolutionStatus.Success) return result;
        cache.InsertOrUpdate(targetAddress, result.Reply.ResolvedMac, currentTick, out result.Cache);
        return result;
    }
}
