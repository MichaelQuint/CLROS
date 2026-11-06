namespace OS.Drivers;
/// <summary>Bounded modern RX policy: VERSION_1+MRG_RXBUF, one buffer, no checksum
/// or segmentation offload. IPv4/UDP/TCP payload checksums remain the software
/// protocol dispatcher's responsibility; ARP has no checksum field.</summary>

public static class VirtioNetHeaderChecksumPolicy
{
    public const ulong AcceptedFeatures = BoundedVirtioFeatures.NetSingleBufferReceive;
    public static VirtioNetHeaderChecksumPolicyResult Validate(ulong negotiatedFeatures, ReadOnlySpan<byte> receiveBuffer)
    {
        VirtioNetHeaderChecksumPolicyResult r = default;
        if (negotiatedFeatures != AcceptedFeatures)
        {
            r.Status = VirtioNetHeaderChecksumPolicyStatus.UnsupportedFeatures;
            return r;
        }

        r.Frame = VirtioNetHeader.TryParseModernReceivedFrameNoOffload(receiveBuffer);
        r.Status = r.Frame.Succeeded ? VirtioNetHeaderChecksumPolicyStatus.Success : VirtioNetHeaderChecksumPolicyStatus.HeaderRejected;
        return r;
    }
}
