namespace OS.Tools.QemuRunner;

public sealed partial class ServiceSocketControls
{
    private static async Task<Flight> ExerciseFlightAsync(Flow flow, Func<Task<Seen>> next,
        Func<uint, string, Task> ack, Action<Seen> copy, CancellationToken token)
    {
        async Task<Seen> Data()
        {
            for (; ; )
            {
                token.ThrowIfCancellationRequested();
                Seen packet = await next().ConfigureAwait(false);
                Need(packet.Ack == flow.Next && packet.Flags is 0x10 or 0x18, "control flight flags");
                if (packet.Payload.Length != 0)
                    return packet;
            }
        }

        Seen first = await Data().ConfigureAwait(false);
        Need(first.Sequence == flow.Server && first.Payload.Length == 32, "control flight initial window");
        // Deliberately withhold the first ACK until its original RTO retry.
        Seen timeout = await Data().ConfigureAwait(false);
        Need(timeout.Sequence == first.Sequence && timeout.Payload.AsSpan().SequenceEqual(first.Payload), "control flight timeout copy");
        copy(first);
        uint start = unchecked(first.Sequence + 32);
        await ack(start, "flight.timeout.ack").ConfigureAwait(false);
        Seen a = await Data().ConfigureAwait(false), b = await Data().ConfigureAwait(false);
        Need(a.Sequence == start && b.Sequence == unchecked(start + 32) && a.Payload.Length == 32 && b.Payload.Length == 32, "control flight two segments");
        // Deliver the later segment to reassembly first; its gap cannot advance
        // the cumulative ACK. The first segment is treated as lost.
        copy(b);
        Need(flow.Received == 32, "control flight reordered gap");
        for (int i = 1; i <= 3; i++)
            await ack(start, "flight.duplicate." + i).ConfigureAwait(false);
        async Task<Seen> Retry(uint sequence, ReadOnlyMemory<byte> expected)
        {
            for (int count = 0; count < 16; count++)
            {
                Seen packet = await Data().ConfigureAwait(false);
                if (packet.Sequence == sequence)
                {
                    Need(packet.Payload.AsSpan().SequenceEqual(expected.Span), "control flight exact retry");
                    return packet;
                }
                Need(unchecked(packet.Sequence - flow.Server) >= 64, "control flight unexpected prefix");
                copy(packet);
            }
            throw new IOException("control flight retry bound");
        }
        Seen fast = await Retry(start, a.Payload).ConfigureAwait(false);
        copy(a with { Payload = a.Payload.AsSpan(0, 16).ToArray() });
        Need(flow.Received == 48, "control flight partial gap");
        await ack(unchecked(start + 16), "flight.partial.ack").ConfigureAwait(false);
        await ack(start, "flight.stale.ack").ConfigureAwait(false);
        Seen suffix = await Retry(unchecked(start + 16), a.Payload.AsMemory(16)).ConfigureAwait(false);
        copy(suffix);
        Need(flow.Received >= 96, "control flight reassembly");
        await ack(unchecked(flow.Server + (uint)flow.Received), "flight.cumulative.ack").ConfigureAwait(false);
        return new(first.Ordinal, timeout.Ordinal, a.Ordinal, b.Ordinal, fast.Ordinal, suffix.Ordinal);
    }
}
