using OS.Drivers;

namespace OS.Net;
public struct ARPRequestReplyBoundedNeighborCacheResult
{
    public EthernetFrameReceiveResult Receive;
    public VirtioNetHeaderChecksumPolicyResult Policy;
    public ArpNeighborResolutionResult Request, Reply;
    public ArpNeighborCacheMutationResult Cache;
    public readonly bool Succeeded => Receive.Succeeded && Policy.Succeeded && Request.Succeeded && Reply.Succeeded && Cache.Succeeded;
}
