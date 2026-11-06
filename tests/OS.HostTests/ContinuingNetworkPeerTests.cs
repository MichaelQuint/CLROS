using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using OS.Net;
using OS.Tools.QemuRunner;

internal static class ContinuingNetworkPeerTests
{
    [HostTest("qemu.continuing-peer independently replies to original ARP and twelve guest-codec echoes", HostLane.Fast, 0)]
    private static void Exchange() => ExchangeAsync().GetAwaiter().GetResult();

    private static async Task ExchangeAsync()
    {
        using var peer = new ContinuingNetworkPeer(TimeSpan.FromSeconds(10));
        using TcpClient client = await Connect(peer);
        NetworkStream stream = client.GetStream();
        byte[] arp = Arp(), expected = new byte[60];
        Convert.FromHexString("020000000001020000000002080600010800060400020200000000020A0002020200000000010A00020F").CopyTo(expected, 0);
        byte[] unrelated = new byte[60];
        unrelated[12] = 0x88;
        unrelated[13] = 0xb5;
        await Write(stream, unrelated);
        for (int i = 0; i < 21; i++)
        {
            await Write(stream, arp);
            HostCheck.True((await Read(stream)).AsSpan().SequenceEqual(expected));
        }
        for (ushort i = 0; i < 12; i++)
        {
            byte[] request = Echo(i);
            await Write(stream, request);
            byte[] response = await Read(stream);
            HostCheck.Equal(PacketParseStatus.Success, Ipv4IcmpEchoFrame.TryParse(response, out var info, out var echo, out var payload));
            HostCheck.True(echo.IsEchoReply && echo.Identifier == 226 && echo.SequenceNumber == i &&
                info.SourceMac == new MacAddress(2, 0, 0, 0, 0, 2) && info.DestinationMac == new MacAddress(2, 0, 0, 0, 0, 1) &&
                info.SourceAddress == new Ipv4Address(10, 0, 2, 2) && info.DestinationAddress == new Ipv4Address(10, 0, 2, 15) &&
                payload.SequenceEqual("CLROS continuing network"u8));
        }
        await peer.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        HostCheck.True(peer.Stop() && peer.Completion.IsCompletedSuccessfully);
        HostCheck.Equal(21, peer.ArpReplies);
        HostCheck.Equal(12, peer.EchoReplies);
    }

    [HostTest("qemu.continuing-peer rejects checksum fragmentation wrong sequence and ARP identity", HostLane.Fast, 1)]
    private static void RejectedPackets() => RejectedPacketsAsync().GetAwaiter().GetResult();

    private static async Task RejectedPacketsAsync()
    {
        for (int control = 0; control < 5; control++)
        {
            using var peer = new ContinuingNetworkPeer(TimeSpan.FromSeconds(10));
            using TcpClient client = await Connect(peer);
            byte[] frame = control == 3 ? Arp() : Echo(control == 2 ? (ushort)1 : (ushort)0);
            if (control == 0)
                frame[36] ^= 1;
            if (control == 1)
                frame[20] = 0x20;
            if (control == 3)
                frame[32] = 1;
            if (control == 4)
                frame = frame[..33];
            await Write(client.GetStream(), frame);
            bool rejected = false;
            try
            {
                await peer.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            HostCheck.True(rejected && peer.Stop());
            HostCheck.Equal(0, peer.ArpReplies);
            HostCheck.Equal(0, peer.EchoReplies);
        }
    }

    [HostTest("qemu.continuing-peer completion requires original ARP parent and shutdown joins pending I/O", HostLane.Fast, 2)]
    private static void Lifetime() => LifetimeAsync().GetAwaiter().GetResult();

    private static async Task LifetimeAsync()
    {
        using (var peer = new ContinuingNetworkPeer(TimeSpan.FromSeconds(10)))
            HostCheck.True(peer.Stop() && peer.Completion.IsCompleted && !peer.Completion.IsCompletedSuccessfully);
        using (var peer = new ContinuingNetworkPeer(TimeSpan.FromSeconds(10)))
        {
            using TcpClient client = await Connect(peer);
            HostCheck.True(peer.Stop() && peer.Completion.IsCompleted && !peer.Completion.IsCompletedSuccessfully);
        }
        using (var peer = new ContinuingNetworkPeer(TimeSpan.FromSeconds(10)))
        {
            using TcpClient client = await Connect(peer);
            for (ushort i = 0; i < 12; i++)
            {
                await Write(client.GetStream(), Echo(i));
                _ = await Read(client.GetStream());
            }
            bool rejected = false;
            try
            {
                await peer.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            HostCheck.True(rejected && peer.Stop());
        }
    }

    private static async Task<TcpClient> Connect(ContinuingNetworkPeer peer)
    {
        var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(IPAddress.Loopback, int.Parse(peer.NetdevArgument[(peer.NetdevArgument.LastIndexOf(':') + 1)..]));
        return client;
    }

    [HostTest("qemu.continuing-peer gates TCP and rejects a wrong original-codec SYN-ACK", HostLane.Fast, 3)]
    private static void TcpInterop() => TcpInteropAsync().GetAwaiter().GetResult();

    private static async Task TcpInteropAsync()
    {
        for (int control = 0; control < 2; control++)
        {
            using var peer = new ContinuingNetworkPeer(TimeSpan.FromSeconds(10), runTcp: true);
            using TcpClient client = await Connect(peer);
            var stream = client.GetStream();
            for (int i = 0; i < 21; i++)
            {
                await Write(stream, Arp());
                _ = await Read(stream);
            }
            for (ushort i = 0; i < 12; i++)
            {
                await Write(stream, Echo(i));
                _ = await Read(stream);
            }
            HostCheck.True(!peer.Completion.IsCompleted && peer.TcpPackets == 0);
            if (control == 0)
            {
                HostCheck.True(peer.Stop() && peer.Completion.IsCompleted && !peer.Completion.IsCompletedSuccessfully);
                continue;
            }
            peer.AllowTcp();
            byte[] syn = await Read(stream), reply = SynAck(syn, 40000, 1000);
            await Write(stream, reply);
            HostCheck.True((await Read(stream)).AsSpan().SequenceEqual(syn));
            await Write(stream, reply);
            _ = await Read(stream); // Deliberate invalid final ACK.
            await Write(stream, reply); // Original-codec retry; this host test makes no elapsed-time claim.
            _ = await Read(stream); // Valid final ACK.
            HostCheck.True((await Read(stream)).AsSpan().SequenceEqual(syn));
            byte[] other = await Read(stream);
            await Write(stream, SynAck(other, 40001, 2001)); // Correct checksum/tuple, wrong authoritative ISS.
            bool rejected = false;
            try
            {
                await peer.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            HostCheck.True(rejected && peer.Stop() && peer.TcpPackets == 9);
        }
    }

    private static byte[] SynAck(byte[] syn, ushort port, uint sequence)
    {
        var info = new TcpPassiveIpv4FramePlannerInfo(new MacAddress(2, 0, 0, 0, 0, 1), new MacAddress(2, 0, 0, 0, 0, 2),
            new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2), 8080, port, sequence, 8, maximumSegmentSize: 4);
        var planner = TcpPassiveIpv4FramePlanner.Create(info);
        byte[] output = new byte[1514];
        HostCheck.Equal(TcpPassiveIpv4FramePlanStatus.Success, planner.ReceiveSynFrameAndWriteSynAckFrame(syn, output, out var packet));
        return output[..packet.BytesWritten];
    }

    private static byte[] Arp()
    {
        byte[] frame = new byte[60];
        Convert.FromHexString("FFFFFFFFFFFF020000000001080600010800060400010200000000010A00020F0000000000000A000202").CopyTo(frame, 0);
        return frame;
    }

    private static byte[] Echo(ushort sequence)
    {
        byte[] frame = new byte[128];
        var info = new Ipv4IcmpEchoFrameInfo(new MacAddress(2, 0, 0, 0, 0, 2), new MacAddress(2, 0, 0, 0, 0, 1),
            new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2));
        HostCheck.Equal(PacketParseStatus.Success, Ipv4IcmpEchoFrame.TryWriteEchoRequest(frame, info, 226, sequence,
            "CLROS continuing network"u8, out int length));
        return frame[..Math.Max(60, length)];
    }

    private static async Task Write(NetworkStream stream, byte[] frame)
    {
        byte[] wire = new byte[frame.Length + 4];
        BinaryPrimitives.WriteUInt32BigEndian(wire, (uint)frame.Length);
        frame.CopyTo(wire, 4);
        await stream.WriteAsync(wire);
    }

    private static async Task<byte[]> Read(NetworkStream stream)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        byte[] prefix = new byte[4];
        await stream.ReadExactlyAsync(prefix, deadline.Token);
        uint length = BinaryPrimitives.ReadUInt32BigEndian(prefix);
        HostCheck.True(length is >= 14 and <= 1514);
        byte[] frame = new byte[(int)length];
        await stream.ReadExactlyAsync(frame, deadline.Token);
        return frame;
    }
}
