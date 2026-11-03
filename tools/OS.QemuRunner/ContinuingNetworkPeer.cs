using System.Buffers.Binary;

namespace OS.Tools.QemuRunner;

/// <summary>
/// Independent wire peer for the finite M07-026 ARP/ICMP fixture. Owns its loopback
/// transport and asynchronous worker through Stop/Dispose; a serialized runner
/// must require successful Completion before accepting serial success. Unknown
/// Ethernet protocols are ignored, malformed fixture traffic fails, and at most
/// 256 base frames are read. Optional TCP uses the independent finite wire corpus;
/// no kernel codecs or external network are used.
/// </summary>
public sealed class ContinuingNetworkPeer : IDisposable
{
    private static readonly byte[] GuestMac = [2, 0, 0, 0, 0, 1], PeerMac = [2, 0, 0, 0, 0, 2];
    private static readonly byte[] GuestIp = [10, 0, 2, 15], PeerIp = [10, 0, 2, 2];
    private static readonly byte[] EchoTag = "CLROS continuing network"u8.ToArray();
    private static readonly byte[] ClosedTag = "CLROS TCP closed"u8.ToArray();
    private readonly QemuEthernetPeer transport;
    private readonly CancellationTokenSource stop = new();
    private readonly TaskCompletionSource tcpReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly bool runTcp;
    private int arpReplies, echoReplies;
    private int tcpPackets;
    private bool disposed;

    /// <summary>Starts the sole listener/worker with the transport's 1ms..5min lifetime.</summary>
    public ContinuingNetworkPeer(TimeSpan maximumLifetime, bool runTcp = false)
    {
        this.runTcp = runTcp;
        transport = new(maximumLifetime);
        Completion = RunAsync();
    }

    public string NetdevArgument => transport.NetdevArgument;
    public Task Completion
    {
        get;
    }
    public int ArpReplies => Volatile.Read(ref arpReplies);
    public int EchoReplies => Volatile.Read(ref echoReplies);
    public int TcpPackets => Volatile.Read(ref tcpPackets);

    /// <summary>Release the TCP start gate only after the original continuing IP row is observed.
    /// This coordinates delivery; captured packets and guest checks still determine acceptance.</summary>
    public void AllowTcp() => tcpReady.TrySetResult();

    private async Task RunAsync()
    {
        await transport.AcceptConnectionAsync(stop.Token).ConfigureAwait(false);
        byte[] input = new byte[QemuEthernetPeer.MaximumFrameBytes], output = new byte[QemuEthernetPeer.MaximumFrameBytes];
        for (int count = 0; count < 256; count++)
        {
            int length = await transport.ReadFrameAsync(input, stop.Token).ConfigureAwait(false);
            int reply = BuildReply(input.AsSpan(0, length), output, out int sequence);
            if (reply == 0)
                continue;
            if (sequence >= 0 && sequence != EchoReplies)
                throw new InvalidDataException("Unexpected echo sequence.");
            await transport.WriteFrameAsync(output.AsMemory(0, reply), stop.Token).ConfigureAwait(false);
            if (sequence < 0)
                Interlocked.Increment(ref arpReplies);
            else
            {
                if (Interlocked.Increment(ref echoReplies) == 12)
                {
                    if (ArpReplies != 21)
                        throw new InvalidDataException("Unexpected original/continuing ARP request count.");
                    if (runTcp)
                        await RunTcpAsync(input, output).ConfigureAwait(false);
                    return;
                }
            }
        }
        throw new InvalidDataException("Continuing fixture frame bound exhausted.");
    }

    private async Task RunTcpAsync(byte[] input, byte[] output)
    {
        await tcpReady.Task.WaitAsync(TimeSpan.FromSeconds(10), stop.Token).ConfigureAwait(false);
        foreach (var packet in ContinuingTcpScenario.Packets)
        {
            if (packet.DelayMilliseconds != 0)
                await Task.Delay(packet.DelayMilliseconds, stop.Token).ConfigureAwait(false);
            if (packet.FromGuest)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(8));
                int length = await transport.ReadFrameAsync(input, deadline.Token).ConfigureAwait(false);
                ContinuingTcpScenario.Validate(input.AsSpan(0, length), packet, 0x020000000002);
            }
            else
                await transport.WriteFrameAsync(ContinuingTcpScenario.Write(packet, 0x020000000002), stop.Token).ConfigureAwait(false);
            Interlocked.Increment(ref tcpPackets);
        }
        // The real guest keeps B's table generation for 2MSL while other cases run.
        // Its final ICMP request appears only after actual TIME-WAIT and TCP cleanup.
        using var finalDeadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        finalDeadline.CancelAfter(TimeSpan.FromSeconds(260));
        int finalLength = await transport.ReadFrameAsync(input, finalDeadline.Token).ConfigureAwait(false);
        int reply = BuildReply(input.AsSpan(0, finalLength), output, out int sequence, final: true);
        if (reply == 0 || sequence != 0)
            throw new InvalidDataException("Missing final TCP cleanup echo.");
        await transport.WriteFrameAsync(output.AsMemory(0, reply), stop.Token).ConfigureAwait(false);
    }

    private static int BuildReply(ReadOnlySpan<byte> input, Span<byte> output, out int sequence, bool final = false)
    {
        sequence = -1;
        ushort type = BinaryPrimitives.ReadUInt16BigEndian(input[12..]);
        if (type != 0x0806 && type != 0x0800)
            return 0;
        if (!input.Slice(6, 6).SequenceEqual(GuestMac))
            throw new InvalidDataException("Unexpected fixture source MAC.");
        output.Clear();
        GuestMac.CopyTo(output);
        PeerMac.CopyTo(output[6..]);
        BinaryPrimitives.WriteUInt16BigEndian(output[12..], type);
        if (type == 0x0806)
        {
            ReadOnlySpan<byte> header = [0, 1, 8, 0, 6, 4, 0, 1], broadcast = [255, 255, 255, 255, 255, 255], zero = [0, 0, 0, 0, 0, 0];
            if (input.Length < 42 || !input.Slice(14, 8).SequenceEqual(header) || !input[..6].SequenceEqual(broadcast) ||
                !input.Slice(22, 6).SequenceEqual(GuestMac) || !input.Slice(28, 4).SequenceEqual(GuestIp) ||
                !input.Slice(32, 6).SequenceEqual(zero) || !input.Slice(38, 4).SequenceEqual(PeerIp))
                throw new InvalidDataException("Invalid fixture ARP request.");
            input.Slice(14, 28).CopyTo(output[14..]);
            BinaryPrimitives.WriteUInt16BigEndian(output[20..], 2);
            PeerMac.CopyTo(output[22..]);
            PeerIp.CopyTo(output[28..]);
            GuestMac.CopyTo(output[32..]);
            GuestIp.CopyTo(output[38..]);
            return 60;
        }
        if (input.Length < 34)
            throw new InvalidDataException("Truncated fixture IPv4 header.");
        if (input[23] != 1)
            return 0;
        int total = BinaryPrimitives.ReadUInt16BigEndian(input[16..]);
        if (input[14] != 0x45 || total < 28 || total > input.Length - 14 ||
            (BinaryPrimitives.ReadUInt16BigEndian(input[20..]) & 0xbfff) != 0 ||
            !input.Slice(0, 6).SequenceEqual(PeerMac) || !input.Slice(26, 4).SequenceEqual(GuestIp) ||
            !input.Slice(30, 4).SequenceEqual(PeerIp) || Checksum(input.Slice(14, 20)) != 0 ||
            Checksum(input.Slice(34, total - 20)) != 0)
            throw new InvalidDataException("Invalid fixture IPv4/ICMP packet.");
        ReadOnlySpan<byte> echo = input.Slice(34, total - 20);
        if (echo[0] != 8 || echo[1] != 0 || BinaryPrimitives.ReadUInt16BigEndian(echo[4..]) != (final ? 227 : 226) ||
            !echo[8..].SequenceEqual(final ? ClosedTag : EchoTag))
            throw new InvalidDataException("Unexpected fixture echo.");
        sequence = BinaryPrimitives.ReadUInt16BigEndian(echo[6..]);
        input.Slice(14, total).CopyTo(output[14..]);
        PeerIp.CopyTo(output[26..]);
        GuestIp.CopyTo(output[30..]);
        output[24] = output[25] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(output[24..], Checksum(output.Slice(14, 20)));
        output[34] = output[36] = output[37] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(output[36..], Checksum(output.Slice(34, total - 20)));
        return Math.Max(60, total + 14);
    }

    private static ushort Checksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        for (int i = 0; i < bytes.Length; i += 2)
            sum += (uint)((bytes[i] << 8) | (i + 1 < bytes.Length ? bytes[i + 1] : 0));
        while (sum > 65535)
            sum = (sum & 65535) + (sum >> 16);
        return (ushort)~sum;
    }

    /// <summary>Cancels I/O, closes the transport and observes/joins the worker within two seconds.
    /// True means joined; Completion still records cancellation or protocol failure. Call serially.</summary>
    public bool Stop()
    {
        if (!disposed)
        {
            stop.Cancel();
            transport.Dispose();
        }
        try
        {
            return Completion.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException) { return Completion.IsCompleted; }
    }

    /// <summary>Releases all host resources; idempotent. The runner calls Stop and checks its result first.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        _ = Stop();
        disposed = true;
        stop.Dispose();
    }
}
