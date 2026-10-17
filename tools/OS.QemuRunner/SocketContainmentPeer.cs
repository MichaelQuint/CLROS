using System.Buffers.Binary;
using System.Diagnostics;

namespace OS.Tools.QemuRunner;

/// <summary>
/// Finite loopback Ethernet peer for NativeAotSocketContainment. Three passive
/// guest sockets receive roles0/1/2. Role0 receives no data acknowledgements;
/// roles1/2 exchange fifteen checked bytes over at least70 seconds, including
/// traffic after one1518-byte VLAN injection. Owns one reader, one serialized
/// writer and all tasks until Stop/Dispose. No external network, general TCP,
/// congestion control or guest-acceptance authority is provided by this peer.
/// </summary>
public sealed class SocketContainmentPeer : IDisposable
{
    private const ulong PeerMac = 0x020000000002;
    private readonly QemuEthernetPeer transport;
    private readonly CancellationTokenSource stop = new();
    private readonly byte[] input = new byte[QemuEthernetPeer.MaximumTaggedFrameBytes];
    private readonly Connection[] connections = [new(0), new(1), new(2)];
    private Task<int>? pendingRead;
    private bool disposed;
    private int frames, arpReplies, headTransmissions, healthyReplies, vlanFrames;
    private long firstHead = -1, lastHead;

    private sealed class Connection(int role)
    {
        internal readonly ushort Port = (ushort)(41000 + role);
        internal readonly uint InitialNext = (uint)(10001 + role * 10000);
        internal uint LocalNext = (uint)(10001 + role * 10000), GuestNext, GuestStart;
        internal bool Responded, FinSeen;
        internal int LastRound = -1;
    }

    /// <summary>Starts the sole worker for a120..295-second diagnostic lifetime.
    /// Completion must succeed and Stop must join before serial success is accepted.</summary>
    public SocketContainmentPeer(TimeSpan maximumLifetime)
    {
        if (maximumLifetime < TimeSpan.FromSeconds(120) || maximumLifetime > TimeSpan.FromSeconds(295))
            throw new ArgumentOutOfRangeException(nameof(maximumLifetime));
        transport = new(maximumLifetime, allowTaggedDiagnostic: true);
        Completion = RunAsync();
    }

    public string NetdevArgument => transport.NetdevArgument;
    public int Port => transport.Port;
    public Task Completion { get; }
    public int HeadTransmissions => Volatile.Read(ref headTransmissions);
    public int HealthyReplies => Volatile.Read(ref healthyReplies);
    public int VlanFrames => Volatile.Read(ref vlanFrames);
    public int ArpReplies => Volatile.Read(ref arpReplies);

    private async Task RunAsync()
    {
        await transport.AcceptConnectionAsync(stop.Token).ConfigureAwait(false);
        await BootPreludeAsync().ConfigureAwait(false);
        for (int role = 0; role < connections.Length; role++)
            await EstablishAsync(connections[role], role).ConfigureAwait(false);
        Stopwatch clock = Stopwatch.StartNew();
        int round = 0; bool sent = false; long firstRoundCompleted = 0;
        while (round < 15)
        {
            if (clock.ElapsedMilliseconds >= 100_000) throw new InvalidDataException("Socket containment peer deadline.");
            if (!sent && clock.ElapsedMilliseconds >= firstRoundCompleted + round * 5000L)
            {
                if (round == 7)
                {
                    await transport.WriteFrameAsync(VlanFrame(), stop.Token).ConfigureAwait(false);
                    Interlocked.Increment(ref vlanFrames);
                }
                for (int role = 1; role < 3; role++)
                {
                    Connection c = connections[role]; c.Responded = false;
                    await SendAsync(c, 0x18, ((char)round).ToString()).ConfigureAwait(false);
                    c.LocalNext++;
                }
                sent = true;
            }
            pendingRead ??= transport.ReadFrameAsync(input, stop.Token).AsTask();
            if (await Task.WhenAny(pendingRead, Task.Delay(25, stop.Token)).ConfigureAwait(false) != pendingRead) continue;
            int length = await pendingRead.ConfigureAwait(false); pendingRead = null;
            if (await ReplyArpAsync(length).ConfigureAwait(false)) continue;
            Packet packet = ParseGuest(input.AsSpan(0, length));
            Connection peer = connections[packet.Port - 41000];
            if (packet.Flags == 0x12 && packet.Sequence == unchecked(peer.GuestStart - 1) &&
                packet.Acknowledgement == peer.InitialNext && packet.Payload.Length == 0)
            {
                await SendAsync(peer, 0x10).ConfigureAwait(false); continue;
            }
            if (packet.Flags == 0x11 && packet.Port != 41000 && peer.LastRound == 14 && packet.Payload.Length == 0 &&
                packet.Acknowledgement == peer.LocalNext && packet.Sequence == unchecked(peer.GuestNext - (peer.FinSeen ? 1U : 0U)))
            {
                // The app can dispose one healthy socket after its last send
                // while the other final response is still in transit. ACK only
                // that exact final FIN; reset both peers after the case joins.
                if (!peer.FinSeen) { peer.GuestNext++; peer.FinSeen = true; }
                await SendAsync(peer, 0x10).ConfigureAwait(false); continue;
            }
            if (packet.Acknowledgement < peer.InitialNext || packet.Acknowledgement > peer.LocalNext || packet.Flags is not (0x10 or 0x18))
                throw new InvalidDataException("Unexpected socket containment control.");
            if (packet.Payload.Length == 0) continue;
            if (packet.Port == 41000)
            {
                uint offset = unchecked(packet.Sequence - peer.GuestStart);
                if (offset > 32768 || packet.Payload.Length > 32768 - offset)
                    throw new InvalidDataException("Expired peer data range.");
                for (int i = 0; i < packet.Payload.Length; i++)
                    if (packet.Payload[i] != (byte)((offset + i) % 251)) throw new InvalidDataException("Expired peer data mismatch.");
                if (offset == 0)
                {
                    Interlocked.Increment(ref headTransmissions);
                    if (firstHead < 0) firstHead = clock.ElapsedMilliseconds;
                    lastHead = clock.ElapsedMilliseconds;
                }
                continue; // Deliberately never ACK role0 data, including retries.
            }
            if (packet.Payload.Length != 1) throw new InvalidDataException("Unexpected healthy peer data.");
            bool fresh = false;
            if (packet.Sequence == unchecked(peer.GuestNext - 1))
            {
                // A genuine lost ACK may repeat the previous response.
                int previous = peer.LastRound;
                if (previous < 0 || packet.Payload[0] != (byte)(previous ^ 0x80)) throw new InvalidDataException("Healthy retry mismatch.");
            }
            else
            {
                if (!sent || peer.Responded || packet.Sequence != peer.GuestNext || packet.Payload[0] != (byte)(round ^ 0x80))
                    throw new InvalidDataException("Healthy peer sequence/payload mismatch.");
                peer.GuestNext++; peer.Responded = true; peer.LastRound = round; fresh = true; Interlocked.Increment(ref healthyReplies);
            }
            await SendAsync(peer, 0x10).ConfigureAwait(false);
            if (fresh && connections[1].Responded && connections[2].Responded)
            {
                if (round == 0) firstRoundCompleted = clock.ElapsedMilliseconds;
                round++; sent = false;
            }
        }
        if (HeadTransmissions < 6 || lastHead - firstHead < 30_000 || HealthyReplies != 30 || VlanFrames != 1 || clock.ElapsedMilliseconds < 70_000)
            throw new InvalidDataException("Socket containment wire witnesses incomplete.");
        // Exact-sequence RST avoids adding a240-second TIME-WAIT wait to this
        // timeout/drop case. Normal close/TIME-WAIT remain separate ancestors.
        for (int role = 1; role < 3; role++) await SendAsync(connections[role], 0x14).ConfigureAwait(false);
    }

    private async Task BootPreludeAsync()
    {
        bool owned = false;
        while (ArpReplies < 2)
        {
            int length = await transport.ReadFrameAsync(input, stop.Token).ConfigureAwait(false);
            if (!owned)
            {
                // Ignore firmware traffic until the exact owned M07 anchor.
                // The parent ARP fixture and socket peer resolution each need
                // one real reply before the first application SYN is useful.
                if (++frames > 4096) throw new InvalidDataException("Socket containment frame bound.");
                ReadOnlySpan<byte> header = [2, 0, 0, 0, 0, 2, 2, 0, 0, 0, 0, 1, 0x88, 0xb5];
                owned = length == 60 && input.AsSpan(0, 14).SequenceEqual(header) &&
                    input.AsSpan(14, 9).SequenceEqual("CLROS-M07"u8) && !input.AsSpan(23, 37).ContainsAnyExcept((byte)0);
                continue;
            }
            if (!await ReplyArpAsync(length).ConfigureAwait(false)) throw new InvalidDataException("Missing socket containment boot ARP.");
        }
    }

    private async Task EstablishAsync(Connection peer, int role)
    {
        Stopwatch deadline = Stopwatch.StartNew(); long nextSyn = 0;
        while (deadline.ElapsedMilliseconds < 30_000)
        {
            if (deadline.ElapsedMilliseconds >= nextSyn)
            {
                await SendAsync(peer, 2, sequence: unchecked(peer.LocalNext - 1)).ConfigureAwait(false);
                nextSyn = deadline.ElapsedMilliseconds + 500;
            }
            pendingRead ??= transport.ReadFrameAsync(input, stop.Token).AsTask();
            if (await Task.WhenAny(pendingRead, Task.Delay(25, stop.Token)).ConfigureAwait(false) != pendingRead) continue;
            int length = await pendingRead.ConfigureAwait(false); pendingRead = null;
            if (await ReplyArpAsync(length).ConfigureAwait(false)) continue;
            Packet packet = ParseGuest(input.AsSpan(0, length));
            if (packet.Port != peer.Port)
            {
                Connection earlier = connections[packet.Port - 41000];
                if (packet.Port < peer.Port && packet.Flags == 0x12 && packet.Payload.Length == 0 &&
                    packet.Sequence == unchecked(earlier.GuestStart - 1) && packet.Acknowledgement == earlier.InitialNext)
                {
                    await SendAsync(earlier, 0x10).ConfigureAwait(false); continue;
                }
                if (packet.Port > peer.Port || packet.Payload.Length != 0 ||
                    packet.Acknowledgement != earlier.LocalNext || packet.Sequence != earlier.GuestNext || packet.Flags != 0x10)
                    throw new InvalidDataException("Unexpected handshake peer.");
                continue;
            }
            if (packet.Flags is 4 or 0x14) continue; // Listener may not yet exist.
            if (packet.Flags != 0x12 || packet.Acknowledgement != peer.LocalNext || packet.Payload.Length != 0)
                throw new InvalidDataException("Invalid containment SYN-ACK.");
            peer.GuestStart = peer.GuestNext = unchecked(packet.Sequence + 1);
            await SendAsync(peer, 0x10).ConfigureAwait(false);
            await SendAsync(peer, 0x18, ((char)role).ToString()).ConfigureAwait(false);
            peer.LocalNext++; return;
        }
        throw new InvalidDataException("Socket containment handshake deadline.");
    }

    private ValueTask SendAsync(Connection peer, byte flags, string payload = "", uint? sequence = null) =>
        transport.WriteFrameAsync(ContinuingTcpScenario.Write(new(false, peer.Port, sequence ?? peer.LocalNext,
            flags == 2 ? 0 : peer.GuestNext, flags, 4096, payload), PeerMac), stop.Token);

    private async ValueTask<bool> ReplyArpAsync(int length)
    {
        if (++frames > 4096) throw new InvalidDataException("Socket containment frame bound.");
        if (BinaryPrimitives.ReadUInt16BigEndian(input.AsSpan(12)) != 0x0806) return false;
        byte[] expected = Convert.FromHexString("FFFFFFFFFFFF020000000001080600010800060400010200000000010A00020F0000000000000A000202");
        if (length != 60 || !input.AsSpan(0, expected.Length).SequenceEqual(expected) || input.AsSpan(expected.Length, length - expected.Length).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Invalid containment ARP request.");
        byte[] reply = new byte[60];
        Convert.FromHexString("020000000001020000000002080600010800060400020200000000020A0002020200000000010A00020F").CopyTo(reply, 0);
        await transport.WriteFrameAsync(reply, stop.Token).ConfigureAwait(false);
        Interlocked.Increment(ref arpReplies); return true;
    }

    /// <summary>A validated guest TCP observation. Payload is a fresh caller-owned
    /// array; its presence is wire evidence only, not guest descriptor authority.</summary>
    public readonly record struct Packet(ushort Port, uint Sequence, uint Acknowledgement, byte Flags, ushort Window, byte[] Payload);

    /// <summary>Validate one captured guest IPv4/TCP frame for the three fixed
    /// endpoints, including both checksums, lengths and padding. Malformed or
    /// unsupported frames throw InvalidDataException; no kernel codec is used.</summary>
    public static Packet ParseGuest(ReadOnlySpan<byte> frame)
    {
        ReadOnlySpan<byte> ethernet = [2, 0, 0, 0, 0, 2, 2, 0, 0, 0, 0, 1, 8, 0];
        ReadOnlySpan<byte> addresses = [10, 0, 2, 15, 10, 0, 2, 2];
        if (frame.Length < 54 || frame.Length > 1514 || !frame[..14].SequenceEqual(ethernet) || frame[14] != 0x45 || frame[23] != 6)
            throw new InvalidDataException("Containment Ethernet/IPv4 header.");
        int total = BinaryPrimitives.ReadUInt16BigEndian(frame[16..]);
        if (total < 40 || total > 1500 || frame.Length < 14 + total || frame.Length > Math.Max(60, 14 + total) ||
            (BinaryPrimitives.ReadUInt16BigEndian(frame[20..]) & 0xbfff) != 0 || Checksum(frame.Slice(14, 20)) != 0 ||
            !frame.Slice(26, 8).SequenceEqual(addresses) || frame[(14 + total)..].ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Containment IPv4 extent/checksum.");
        ReadOnlySpan<byte> tcp = frame.Slice(34, total - 20);
        int header = (tcp[12] >> 4) * 4;
        ushort port = BinaryPrimitives.ReadUInt16BigEndian(tcp[2..]);
        if (header < 20 || header > tcp.Length || (tcp[12] & 15) != 0 || tcp[18] != 0 || tcp[19] != 0 ||
            BinaryPrimitives.ReadUInt16BigEndian(tcp) != 8080 || port is < 41000 or > 41002 ||
            TcpChecksum(frame.Slice(26, 8), tcp) != 0)
            throw new InvalidDataException("Containment TCP extent/tuple/checksum.");
        if (header != 20 && (tcp[13] != 0x12 || header != 24 || tcp[20] != 2 || tcp[21] != 4 || BinaryPrimitives.ReadUInt16BigEndian(tcp[22..]) == 0))
            throw new InvalidDataException("Unsupported containment TCP options.");
        return new(port, BinaryPrimitives.ReadUInt32BigEndian(tcp[4..]), BinaryPrimitives.ReadUInt32BigEndian(tcp[8..]),
            tcp[13], BinaryPrimitives.ReadUInt16BigEndian(tcp[14..]), tcp[header..].ToArray());
    }

    private static byte[] VlanFrame()
    {
        byte[] bytes = new byte[1518];
        Convert.FromHexString("020000000001020000000002810000010800").CopyTo(bytes, 0);
        return bytes;
    }

    private static ushort TcpChecksum(ReadOnlySpan<byte> addresses, ReadOnlySpan<byte> tcp)
    {
        uint sum = (uint)(~Checksum(addresses) & 0xffff) + 6 + (uint)tcp.Length + (uint)(~Checksum(tcp) & 0xffff);
        while (sum > 65535) sum = (sum & 65535) + (sum >> 16);
        return (ushort)~sum;
    }

    private static ushort Checksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        for (int i = 0; i < bytes.Length; i += 2) sum += (uint)((bytes[i] << 8) | (i + 1 < bytes.Length ? bytes[i + 1] : 0));
        while (sum > 65535) sum = (sum & 65535) + (sum >> 16);
        return (ushort)~sum;
    }

    /// <summary>Cancel I/O, close transport and join the sole worker within two
    /// seconds. True means joined; Completion retains any protocol failure.</summary>
    public bool Stop()
    {
        if (!disposed) { stop.Cancel(); transport.Dispose(); }
        try { return Completion.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { return Completion.IsCompleted; }
    }

    /// <summary>Release the owned session; idempotent. Call Stop before accepting a run.</summary>
    public void Dispose()
    {
        if (disposed) return;
        _ = Stop(); disposed = true; stop.Dispose();
    }
}
