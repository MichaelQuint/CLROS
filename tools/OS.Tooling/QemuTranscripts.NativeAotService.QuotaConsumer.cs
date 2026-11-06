namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Inspect the eight original output snapshots and actual borrowed
    /// pre-run package through one privately owned observed Completion. Compose
    /// the strict quota parent, complete scheduler/syscall/memory/GC/cleanup
    /// replay, runner22, packet/NIC accounting, unique host/guest HTTP byte join,
    /// original socket slices and retained-map/progress overlap. Verify every
    /// held output and borrowed input before and after, including on failure.
    /// Nothing runs; evidence bytes come from held records/snapshots. QemuSmoke
    /// must keep its original current-source/native locks through final readback.
    /// Only its explicit quota route supplies these original owners and locks.</summary>

    internal static ServiceQuotaOwnedInspection InspectOwnedServiceQuota(string root, QemuSmokeOptions options, QemuSmokePlan plan, NativeAotServiceProgram program, ServiceRunnerContext context, ServiceQuotaOwnedEvidence evidence) => evidence.Inspect<ServiceQuotaOwnedInspection>(held =>
    {
        byte[] raw = held.Serial.ToArray(), packets = held.Packets.ToArray();
        var peer = held.Completion;
        var capture = ValidateNativeAotServiceQuotaCapture(root, held.ReadPackage(), program, raw);
        var runner = InspectNativeAotServiceQuotaRunnerForTests(options, plan, peer.Runner, raw, held.Summary.ToArray(), context, packets, capture.Parent.FrameAnchor);
        Need(peer.Port == context.ForwardPort && peer.PeerStartedUtc >= context.StartedUtc && peer.PeerStartedUtc <= runner.EndUtc && peer.PeerCompletedUtc > peer.PeerStartedUtc, "service_quota_owned_peer_bounds");
        // Peer completion may follow expected runner22 while its pending I/O
        // observes the stopped endpoint. Never require a guest/host clock join
        // or equate that host failure with the independently replayed cleanup.
        uint[] ports = capture.Leaf.Transport.FreshLifetimes.Select(lifetime => checked((uint)lifetime.RemotePort)).ToArray();
        var streams = ReadServiceQuotaStreamJoin(held.Wire, packets, capture.Parent.FrameAnchor, capture.Parent.Arp, ports);
        var temporal = ReadServiceQuotaTemporal(streams, capture.Leaf.SocketIo);
        int start = raw.AsSpan().IndexOf("TEST_START m16-011\r\n"u8);
        Need(start >= FirmwarePrefixBytes, "service_quota_owned_leaf");
        var framed = NativeAotThreadingOutput.ReadService(raw.AsSpan(start));
        var overlap = ReadServiceQuotaOverlap(framed, capture.Leaf.Memory, streams.Host, temporal, peer.Report);
        var nic = ReadServiceQuotaNic(packets, capture.Parent, capture.Envelope, raw);
        Need(capture.Envelope.Fresh == (ulong)ports.Length && streams.Guest.Streams.Count == ports.Length && temporal.SocketObservations == capture.Leaf.SocketIo.Count && temporal.RequestBytesConsumed == capture.Leaf.SocketReceived && temporal.ResponseBytesAccepted == capture.Leaf.SocketSent, "service_quota_owned_totals");
        return new(capture, runner, streams, temporal, overlap, nic);
    });
}
