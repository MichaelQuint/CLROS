using OS.Drivers;

internal static class VirtioNetHeaderChecksumPolicyTests
{
    [HostTest("virtio.net.modern RX header requires VERSION_1 and one buffer without offload", HostLane.Fast, 0)]
    private static void Policy()
    {
        // Independent modern header + Ethernet ARP vector; legacy offset 10
        // would read a different EtherType and must not classify this as ARP.
        byte[] frame = new byte[54];
        frame[10] = 1;
        frame[12] = 2;
        frame[17] = 1;
        frame[18] = 0x52;
        frame[23] = 2;
        frame[24] = 8;
        frame[25] = 6;
        const ulong accepted = VirtioNetHeaderChecksumPolicy.AcceptedFeatures;
        HostCheck.Equal(0x100008000UL, accepted);
        var r = VirtioNetHeaderChecksumPolicy.Validate(accepted, frame);
        HostCheck.True(r.Succeeded && r.Frame.Metadata.EthernetFrameOffset == 12 &&
            r.Frame.Metadata.EthernetPayloadOffset == 26 && r.Frame.Metadata.EthernetFrameLength == 42);
        HostCheck.True(!default(VirtioNetHeaderChecksumPolicyResult).Succeeded);
        foreach (ulong features in new ulong[] { 0, 1UL << 15, 1UL << 32, accepted | 1, accepted | (1UL << 28), accepted | (1UL << 34), ulong.MaxValue })
            HostCheck.Equal(VirtioNetHeaderChecksumPolicyStatus.UnsupportedFeatures,
                VirtioNetHeaderChecksumPolicy.Validate(features, frame).Status);
        foreach (int length in new[] { 0, 9, 10, 11, 12, 25 })
            HostCheck.True(!VirtioNetHeaderChecksumPolicy.Validate(accepted, frame.AsSpan(0, length)).Succeeded);
        foreach (int offset in new[] { 0, 1, 2, 4, 6, 8, 10, 11 })
        {
            byte old = frame[offset];
            frame[offset] = offset == 10 ? (byte)0 : (byte)1;
            var bad = VirtioNetHeaderChecksumPolicy.Validate(accepted, frame);
            HostCheck.Equal(VirtioNetHeaderChecksumPolicyStatus.HeaderRejected, bad.Status);
            HostCheck.True(!VirtioNetReceiveFrameClassifier.ClassifyModern(frame).Succeeded);
            if (offset >= 10)
                HostCheck.Equal(VirtioNetHeaderStatus.UnsupportedReceiveBufferCount, bad.Frame.Status);
            frame[offset] = old;
        }
        frame[0] = 2;
        HostCheck.Equal(VirtioNetHeaderStatus.UnsupportedOffloadFlags,
            VirtioNetHeaderChecksumPolicy.Validate(accepted, frame).Frame.Status);
    }
}
