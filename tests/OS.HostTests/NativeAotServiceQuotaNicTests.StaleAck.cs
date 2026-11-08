using OS.Tooling;

internal static partial class NativeAotServiceQuotaNicTests
{
    [HostTest("NIC accounting joins exactly the validated original stale ACK and rejects offsets and counter drift")]
    private static void StaleAck()
    {
        var vector = Fixture();
        // The preceding incoming ACK advances to2002 at sequence1002. This
        // exact later ACK is behind it, without receive-sequence progress.
        const int index = 18;
        const long ordinal = index + 1;
        vector.Frames.Insert(index, NativeAotSocketIntegrationTests.Tcp(false, 43000, 1002, 2001, 0x10, []));
        var envelope = vector.Envelope with { Received = 9, Ignored = 4 };
        byte[] packets = NativeAotSocketIntegrationTests.Capture(vector.Frames);
        QemuTranscripts.ServiceNicAccountingInspection Inspect(long record = ordinal,
            QemuTranscripts.ServiceEnvelopeInspection? changed = null, byte[]? capture = null) =>
            QemuTranscripts.InspectNativeAotServiceStaleAckNicForTests(capture ?? packets, vector.Parent,
                changed ?? envelope, Raw(vector.Parent, changed ?? envelope), record);
        var result = Inspect();
        HostCheck.True(result is { Received: 9, Accepted: 4, Ignored: 4, QuietSyns: 2, TcpCopied: 3 });
        Reject(() => Read(vector, envelope: envelope, pcap: packets), "service_packet_nic_join");
        foreach (long wrong in new[] { -1L, 0, ordinal - 2, ordinal - 1, ordinal + 1, 999 })
            Reject(() => Inspect(wrong), wrong == 0 ? "service_packet_nic_join" : "service_packet_stale_ack_record");
        foreach (var changed in new[]
        {
            envelope with { Accepted = 5, Ignored = 3 },
            envelope with { Accepted = 3, Ignored = 5 }
        })
            Reject(() => Inspect(changed: changed), "service_packet_nic_join");
        foreach (var changed in new[]
        {
            NativeAotSocketIntegrationTests.Tcp(false, 43000, 1002, 2002, 0x10, []),
            NativeAotSocketIntegrationTests.Tcp(false, 43000, 1002, 2003, 0x10, []),
            NativeAotSocketIntegrationTests.Tcp(false, 43000, 1003, 2001, 0x10, []),
            NativeAotSocketIntegrationTests.Tcp(false, 43000, 1002, 2001, 0x18, "X"u8)
        })
        {
            var frames = vector.Frames.Select(frame => frame.ToArray()).ToList();
            frames[index] = changed;
            Reject(() => Inspect(capture: NativeAotSocketIntegrationTests.Capture(frames)), "service_packet_stale_ack_record");
        }
    }
}
