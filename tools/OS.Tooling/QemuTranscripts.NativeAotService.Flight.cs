using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // The caller has already joined every original PCAP row, checksum and
    // literal injection in order. Recompute the flight proof from those rows,
    // not the driver's completion flag or a filtered/reindexed substitute.
    private static void ServiceFlightPackets(ServiceControlPacket[] healthy, ServiceControlPacket[] incoming,
        ServiceSocketControls.Flight flight, WebFrameControl.ServiceReport frames)
    {
        ServiceControlPacket At(long ordinal)
        {
            var matches = healthy.Where(packet => packet.Ordinal == ordinal && packet.Outgoing && packet.Payload.Length != 0).ToArray();
            Need(matches.Length == 1, "service_flight_ordinal");
            return matches[0];
        }
        var forwarded = frames.Injections.Where(item => item.Forwarded).ToArray();
        ServiceControlPacket Injection(string tag)
        {
            int index = Array.FindIndex(forwarded, item => item.Tag == tag);
            Need(index >= 0 && index < incoming.Length, "service_flight_injection");
            var packet = incoming[index];
            Need(packet.Port == healthy[0].Port && packet.Flags == 0x10 && packet.Window == 8192 && packet.Payload.Length == 0, "service_flight_ack_shape");
            return packet;
        }
        var first = At(flight.First);
        var timeout = At(flight.Timeout);
        var a = At(flight.NextFirst);
        var b = At(flight.NextSecond);
        var fast = At(flight.Fast);
        var suffix = At(flight.Suffix);
        Need(first.Ordinal < timeout.Ordinal && timeout.Ordinal < a.Ordinal && a.Ordinal < b.Ordinal && b.Ordinal < fast.Ordinal && fast.Ordinal < suffix.Ordinal, "service_flight_order");
        byte[] syn = healthy[0].Raw;
        int tcp = 14 + (syn[14] & 15) * 4;
        Need(!healthy[0].Outgoing && healthy[0].Flags == 2 && (syn[tcp + 12] >> 4) == 6 && syn.AsSpan(tcp + 20, 4).SequenceEqual(new byte[] { 2, 4, 0, 32 }), "service_flight_mss");
        Need(first.Sequence == unchecked(healthy[1].Sequence + 1) && first.Payload.Length == 32 &&
            timeout.Sequence == first.Sequence && timeout.Payload.AsSpan().SequenceEqual(first.Payload) &&
            timeout.Microseconds - first.Microseconds >= 1_000_000 &&
            !healthy.Any(packet => !packet.Outgoing && packet.Ordinal < timeout.Ordinal && unchecked(packet.Ack - first.Sequence) is > 0 and < 0x80000000), "service_flight_timeout");
        uint start = unchecked(first.Sequence + 32);
        var timeoutAck = Injection("flight.timeout.ack");
        Need(timeoutAck.Ordinal > timeout.Ordinal && timeoutAck.Ordinal < a.Ordinal && timeoutAck.Ack == start &&
            a.Sequence == start && a.Payload.Length == 32 && b.Sequence == unchecked(start + 32) && b.Payload.Length == 32 &&
            !healthy.Any(packet => !packet.Outgoing && packet.Ordinal > a.Ordinal && packet.Ordinal < b.Ordinal && unchecked(packet.Ack - start) is > 0 and < 0x80000000), "service_flight_two_unacked");
        long previous = b.Ordinal;
        for (int i = 1; i <= 3; i++)
        {
            var duplicate = Injection("flight.duplicate." + i);
            Need(duplicate.Ordinal > previous && duplicate.Ordinal < fast.Ordinal && duplicate.Ack == start && duplicate.Sequence == a.Ack, "service_flight_duplicate");
            previous = duplicate.Ordinal;
        }
        Need(fast.Sequence == a.Sequence && fast.Payload.AsSpan().SequenceEqual(a.Payload) && fast.Microseconds - a.Microseconds < 2_000_000, "service_flight_fast");
        var partial = Injection("flight.partial.ack");
        var stale = Injection("flight.stale.ack");
        var cumulative = Injection("flight.cumulative.ack");
        Need(fast.Ordinal < partial.Ordinal && partial.Ordinal < stale.Ordinal && stale.Ordinal < suffix.Ordinal && suffix.Ordinal < cumulative.Ordinal &&
            partial.Ack == unchecked(start + 16) && stale.Ack == start && partial.Sequence == a.Ack && stale.Sequence == a.Ack &&
            suffix.Sequence == partial.Ack && suffix.Payload.AsSpan().SequenceEqual(a.Payload.AsSpan(16)) &&
            suffix.Microseconds - partial.Microseconds >= 2_000_000 && unchecked(cumulative.Ack - start) >= 64, "service_flight_suffix");
    }
}
