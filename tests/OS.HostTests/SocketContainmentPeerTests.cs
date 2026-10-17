using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using OS.Tools.QemuRunner;

internal static class SocketContainmentPeerTests
{
    [HostTest("qemu.socket-containment lifetime and owned cancellation", HostLane.Fast, 0)]
    private static void Lifetime()
    {
        foreach (int seconds in new[] { 119, 296 })
        {
            bool rejected = false;
            try { using var bad = new SocketContainmentPeer(TimeSpan.FromSeconds(seconds)); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            HostCheck.True(rejected);
        }
        using var peer = new SocketContainmentPeer(TimeSpan.FromSeconds(120));
        HostCheck.True(peer.NetdevArgument == "socket,id=n0,connect=127.0.0.1:" + peer.Port);
        HostCheck.True(peer.Stop() && peer.Completion.IsCompleted && !peer.Completion.IsCompletedSuccessfully);
    }

    [HostTest("qemu.socket-containment checked guest wire parser and corruption controls", HostLane.Fast, 1)]
    private static void WireParser()
    {
        byte[] syn = ContinuingTcpScenario.Write(new(true, 41002, 0xfffffffe, 30001, 0x12, 8192, Mss: true), 0x020000000002);
        var parsed = SocketContainmentPeer.ParseGuest(syn);
        HostCheck.Equal((ushort)41002, parsed.Port); HostCheck.Equal(0xfffffffeU, parsed.Sequence);
        HostCheck.Equal(30001U, parsed.Acknowledgement); HostCheck.Equal((byte)0x12, parsed.Flags);
        HostCheck.Equal((ushort)8192, parsed.Window); HostCheck.Equal(0, parsed.Payload.Length);
        byte[] data = ContinuingTcpScenario.Write(new(true, 41001, 5, 20003, 0x18, 8192, "payload"), 0x020000000002);
        var payload = SocketContainmentPeer.ParseGuest(data);
        HostCheck.True(payload.Payload.AsSpan().SequenceEqual("payload"u8));
        payload.Payload[0] = 0;
        HostCheck.True(SocketContainmentPeer.ParseGuest(data).Payload.AsSpan().SequenceEqual("payload"u8));
        for (int length = 0; length < 54; length++) Reject(data[..Math.Min(length, data.Length)]);
        foreach (int offset in new[] { 0, 6, 12, 14, 16, 20, 23, 24, 26, 30, 34, 36, 46, 50, 54 })
        {
            byte[] changed = (byte[])data.Clone(); changed[offset] ^= 1; Reject(changed);
        }
        Reject(ContinuingTcpScenario.Write(new(true, 41003, 1, 2, 0x10), 0x020000000002));
        Reject(ContinuingTcpScenario.Write(new(true, 41000, 1, 2, 0x18, Mss: true), 0x020000000002));
        byte[] padded = ContinuingTcpScenario.Write(new(true, 41000, 1, 2, 0x10), 0x020000000002);
        padded[^1] = 1; Reject(padded);
    }

    [HostTest("qemu.socket-containment handshakes tolerate earlier peer ACK and reject wrong healthy bytes", HostLane.Fast, 2)]
    private static void Handshakes() => HandshakesAsync().GetAwaiter().GetResult();

    private static async Task HandshakesAsync()
    {
        using var peer = new SocketContainmentPeer(TimeSpan.FromSeconds(120));
        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, peer.Port);
        NetworkStream stream = client.GetStream();
        byte[] firmware = new byte[60]; firmware[12] = 0x86; firmware[13] = 0xdd;
        await Write(stream, firmware);
        byte[] anchor = new byte[60];
        Convert.FromHexString("02000000000202000000000188B5").CopyTo(anchor, 0);
        "CLROS-M07"u8.CopyTo(anchor.AsSpan(14)); await Write(stream, anchor);
        byte[] bootArp = new byte[60], bootReply = new byte[60];
        Convert.FromHexString("FFFFFFFFFFFF020000000001080600010800060400010200000000010A00020F0000000000000A000202").CopyTo(bootArp, 0);
        Convert.FromHexString("020000000001020000000002080600010800060400020200000000020A0002020200000000010A00020F").CopyTo(bootReply, 0);
        for (int i = 0; i < 2; i++)
        {
            await Write(stream, bootArp);
            HostCheck.True((await Read(stream)).AsSpan().SequenceEqual(bootReply));
        }
        for (int role = 0; role < 3; role++)
        {
            ushort port = (ushort)(41000 + role); uint local = (uint)(10000 + role * 10000), guest = (uint)(900 + role * 1000);
            byte[] syn = await Read(stream);
            if (role == 0) HostCheck.Equal(2, peer.ArpReplies);
            HostCheck.True(syn.AsSpan().SequenceEqual(ContinuingTcpScenario.Write(new(false, port, local, 0, 2, 4096), 0x020000000002)));
            if (role == 0)
            {
                byte[] arp = new byte[60];
                Convert.FromHexString("FFFFFFFFFFFF020000000001080600010800060400010200000000010A00020F0000000000000A000202").CopyTo(arp, 0);
                await Write(stream, arp);
                byte[] reply = new byte[60];
                Convert.FromHexString("020000000001020000000002080600010800060400020200000000020A0002020200000000010A00020F").CopyTo(reply, 0);
                HostCheck.True((await Read(stream)).AsSpan().SequenceEqual(reply));
            }
            if (role == 1)
            {
                await Write(stream, ContinuingTcpScenario.Write(new(true, 41000, 900, 10001, 0x12, 8192), 0x020000000002));
                HostCheck.True((await Read(stream)).AsSpan().SequenceEqual(ContinuingTcpScenario.Write(new(false, 41000, 10002, 901, 0x10, 4096), 0x020000000002)));
            }
            await Write(stream, ContinuingTcpScenario.Write(new(true, port, guest, local + 1, 0x12, 8192), 0x020000000002));
            HostCheck.True((await Read(stream)).AsSpan().SequenceEqual(ContinuingTcpScenario.Write(new(false, port, local + 1, guest + 1, 0x10, 4096), 0x020000000002)));
            HostCheck.True((await Read(stream)).AsSpan().SequenceEqual(ContinuingTcpScenario.Write(new(false, port, local + 1, guest + 1, 0x18, 4096, ((char)role).ToString()), 0x020000000002)));
            // This ACK can arrive after the next peer's SYN has been sent.
            await Write(stream, ContinuingTcpScenario.Write(new(true, port, guest + 1, local + 2, 0x10, 8192), 0x020000000002));
        }
        for (int role = 1; role < 3; role++)
        {
            byte[] request = await Read(stream);
            HostCheck.Equal((ushort)(41000 + role), BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(34)));
            HostCheck.Equal((byte)0, request[54]);
        }
        await Write(stream, ContinuingTcpScenario.Write(new(true, 41001, 1901, 20003, 0x18, 8192, "x"), 0x020000000002));
        bool rejected = false;
        try { await peer.Completion.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (InvalidDataException) { rejected = true; }
        HostCheck.True(rejected && peer.Stop() && peer.HealthyReplies == 0);
    }

    [HostTest("qemu.socket-containment diagnostic VLAN framing is explicit and bounded", HostLane.Fast, 3)]
    private static void VlanFraming() => VlanFramingAsync().GetAwaiter().GetResult();

    private static async Task VlanFramingAsync()
    {
        using var peer = new QemuEthernetPeer(TimeSpan.FromSeconds(10), allowTaggedDiagnostic: true);
        using TcpClient client = new(); Task accept = peer.AcceptConnectionAsync();
        await client.ConnectAsync(IPAddress.Loopback, peer.Port); await accept;
        NetworkStream stream = client.GetStream();
        byte[] vlan = new byte[1518]; vlan[12] = 0x81;
        bool rejected = false;
        try { await peer.WriteFrameAsync(new byte[1518]); } catch (ArgumentOutOfRangeException) { rejected = true; }
        HostCheck.True(rejected && peer.FramesWritten == 0);
        await peer.WriteFrameAsync(vlan);
        byte[] wire = new byte[1522]; await stream.ReadExactlyAsync(wire);
        HostCheck.True(wire.AsSpan(0, 4).SequenceEqual(new byte[] { 0, 0, 5, 238 }) && wire.AsSpan(4).SequenceEqual(vlan));
        await stream.WriteAsync(wire);
        byte[] buffer = new byte[1518]; HostCheck.Equal(1518, await peer.ReadFrameAsync(buffer));
        HostCheck.True(buffer.AsSpan().SequenceEqual(vlan));
        wire[16] = 8; await stream.WriteAsync(wire);
        rejected = false;
        try { await peer.ReadFrameAsync(buffer); } catch (InvalidDataException) { rejected = true; }
        HostCheck.True(rejected && peer.FramesRead == 1);
    }

    [HostTest("qemu.socket-containment CLI selects one captured backend and rejects conflicts", HostLane.Fast, 4)]
    private static void Selection() => SelectionAsync().GetAwaiter().GetResult();

    private static async Task SelectionAsync()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string image = Path.Combine(root, "artifacts", "socket-containment-dryrun.raw");
        string capture = image + ".pcap";
        string[] fixture = ["--dry-run", "--image", image, "--qemu", "qemu-test", "--socket-containment-peer",
            "--virtio-negotiation-device", "--arp-receive-fixture", "--ethernet-capture", capture];
        foreach (string seconds in new[] { "120", "295" })
        {
            var result = await Run([.. fixture, "--timeout", seconds]);
            HostCheck.True(result.Code == 0 && result.Text.Contains("-netdev socket,id=n0,connect=127.0.0.1:<ephemeral>", StringComparison.Ordinal) &&
                result.Text.Contains("filter-dump,id=clroscapture,netdev=n0,file=" + capture, StringComparison.Ordinal) &&
                !result.Text.Contains("hostfwd=", StringComparison.Ordinal) && result.Text.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal));
        }
        foreach (string[] extra in new string[][] { ["--timeout", "119"], ["--timeout", "296"],
            ["--timeout", "120", "--socket-containment-peer"], ["--timeout", "120", "--continuing-network-peer"],
            ["--timeout", "120", "--socket-forward-port", "20080"] })
            HostCheck.True((await Run([.. fixture, .. extra])).Code != 0);
        HostCheck.True((await Run(["--dry-run", "--socket-containment-peer", "--timeout", "120"])).Code != 0);
        HostCheck.True(!File.Exists(image) && !File.Exists(capture));

        async Task<(int Code, string Text)> Run(string[] args)
        {
            ProcessStartInfo start = HostToolProcess.CreateDotnetToolStartInfo(root, "OS.QemuRunner");
            foreach (string arg in args) start.ArgumentList.Add(arg);
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Runner did not start.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
            return (process.ExitCode, await stdout + await stderr);
        }
    }

    private static void Reject(byte[] bytes)
    {
        bool rejected = false;
        try { _ = SocketContainmentPeer.ParseGuest(bytes); } catch (InvalidDataException) { rejected = true; }
        HostCheck.True(rejected);
    }

    private static async Task<byte[]> Read(NetworkStream stream)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        byte[] prefix = new byte[4]; await stream.ReadExactlyAsync(prefix, deadline.Token);
        uint length = BinaryPrimitives.ReadUInt32BigEndian(prefix); HostCheck.True(length is >= 14 and <= 1518);
        byte[] data = new byte[length]; await stream.ReadExactlyAsync(data, deadline.Token); return data;
    }

    private static async Task Write(NetworkStream stream, byte[] frame)
    {
        byte[] prefix = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(prefix, (uint)frame.Length);
        await stream.WriteAsync(prefix); await stream.WriteAsync(frame);
    }
}
