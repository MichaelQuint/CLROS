using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using OS.Tools.QemuRunner;

internal static class QemuEthernetPeerTests
{
    [HostTest("qemu.ethernet-peer bounded listener and preflight guards", HostLane.Fast, 0)]
    private static void Guards() => GuardsAsync().GetAwaiter().GetResult();

    private static async Task GuardsAsync()
    {
        foreach (TimeSpan duration in new[] { TimeSpan.Zero, TimeSpan.FromMinutes(5) + TimeSpan.FromMilliseconds(1) })
        {
            bool rejected = false;
            try { using var invalid = new QemuEthernetPeer(duration); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            HostCheck.True(rejected);
        }
        using var peer = NewPeer();
        HostCheck.True(peer.Port is > 0 and <= 65535);
        HostCheck.Equal("socket,id=n0,connect=127.0.0.1:" + peer.Port, peer.NetdevArgument);
        await Reject<InvalidOperationException>(() => peer.ReadFrameAsync(new byte[1514]).AsTask());
        await Reject<InvalidOperationException>(() => peer.WriteFrameAsync(new byte[14]).AsTask());
        using TcpClient remote = await Connect(peer);
        byte[] payload = Frame(14, 0x35);
        await remote.GetStream().WriteAsync(Packet(payload));
        await Reject<ArgumentException>(() => peer.ReadFrameAsync(new byte[1513]).AsTask());
        await Reject<ArgumentOutOfRangeException>(() => peer.WriteFrameAsync(new byte[13]).AsTask());
        await Reject<ArgumentOutOfRangeException>(() => peer.WriteFrameAsync(new byte[1515]).AsTask());
        byte[] buffer = new byte[1514];
        HostCheck.Equal(14, await peer.ReadFrameAsync(buffer));
        HostCheck.True(buffer.AsSpan(0, 14).SequenceEqual(payload));
        await peer.WriteFrameAsync(payload);
        byte[] wire = new byte[18];
        await remote.GetStream().ReadExactlyAsync(wire);
        HostCheck.True(wire.AsSpan().SequenceEqual(Packet(payload))); // Invalid writes emitted no prefix.
        HostCheck.Equal(1L, peer.FramesRead); HostCheck.Equal(1L, peer.FramesWritten);
    }

    [HostTest("qemu.ethernet-peer fragmented and concatenated frames with duplex output", HostLane.Fast, 1)]
    private static void Framing() => FramingAsync().GetAwaiter().GetResult();

    private static async Task FramingAsync()
    {
        using var peer = NewPeer(); using TcpClient remote = await Connect(peer);
        NetworkStream stream = remote.GetStream();
        byte[] minimum = Frame(14, 0x83), maximum = Frame(1514, 0x29), buffer = new byte[1514];
        Task<int> receive = peer.ReadFrameAsync(buffer).AsTask();
        byte[] wire = Packet(maximum);
        for (int i = 0; i < 4; i++) await stream.WriteAsync(wire.AsMemory(i, 1));
        await stream.WriteAsync(wire.AsMemory(4, 9));
        await peer.WriteFrameAsync(minimum); // Reading and writing may proceed independently.
        byte[] reverse = new byte[18]; await stream.ReadExactlyAsync(reverse);
        HostCheck.True(reverse.AsSpan().SequenceEqual(Packet(minimum)));
        await stream.WriteAsync(wire.AsMemory(13));
        HostCheck.Equal(1514, await receive);
        HostCheck.True(buffer.AsSpan().SequenceEqual(maximum));
        byte[] combined = [.. Packet(minimum), .. Packet(maximum)];
        await stream.WriteAsync(combined);
        HostCheck.Equal(14, await peer.ReadFrameAsync(buffer));
        HostCheck.True(buffer.AsSpan(0, 14).SequenceEqual(minimum));
        HostCheck.Equal(1514, await peer.ReadFrameAsync(buffer));
        HostCheck.True(buffer.AsSpan().SequenceEqual(maximum));
        await peer.WriteFrameAsync(maximum);
        reverse = new byte[1518]; await stream.ReadExactlyAsync(reverse);
        HostCheck.True(reverse.AsSpan().SequenceEqual(Packet(maximum)));
        HostCheck.Equal(3L, peer.FramesRead); HostCheck.Equal(2L, peer.FramesWritten);
    }

    [HostTest("qemu.ethernet-peer rejects wire lengths without allocation or resynchronization", HostLane.Fast, 2)]
    private static void InvalidLengths() => InvalidLengthsAsync().GetAwaiter().GetResult();

    private static async Task InvalidLengthsAsync()
    {
        foreach (uint length in new uint[] { 0, 13, 1515, uint.MaxValue })
        {
            using var peer = NewPeer(); using TcpClient remote = await Connect(peer);
            Task<int> receive = peer.ReadFrameAsync(new byte[1514]).AsTask();
            byte[] header = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(header, length);
            await remote.GetStream().WriteAsync(header);
            await Reject<InvalidDataException>(async () => { await receive; });
            HostCheck.Equal(0L, peer.FramesRead);
            await Reject<IOException>(() => peer.ReadFrameAsync(new byte[1514]).AsTask());
            await Reject<IOException>(() => peer.WriteFrameAsync(new byte[14]).AsTask());
            await Reject<IOException>(() => peer.AcceptConnectionAsync());
        }
    }

    [HostTest("qemu.ethernet-peer truncated prefix and payload retire both directions", HostLane.Fast, 3)]
    private static void Truncation() => TruncationAsync().GetAwaiter().GetResult();

    private static async Task TruncationAsync()
    {
        foreach (int count in new[] { 0, 3, 17 })
        {
            using var peer = NewPeer(); using TcpClient remote = await Connect(peer);
            Task<int> receive = peer.ReadFrameAsync(new byte[1514]).AsTask();
            await remote.GetStream().WriteAsync(Packet(Frame(14, 0x92)).AsMemory(0, count));
            remote.Client.Shutdown(SocketShutdown.Send);
            await Reject<EndOfStreamException>(async () => { await receive; });
            HostCheck.Equal(0L, peer.FramesRead);
            await Reject<IOException>(() => peer.WriteFrameAsync(new byte[14]).AsTask());
        }
    }

    [HostTest("qemu.ethernet-peer cancellation ends pending accept and partial read", HostLane.Fast, 4)]
    private static void Cancellation() => CancellationAsync().GetAwaiter().GetResult();

    private static async Task CancellationAsync()
    {
        using (var peer = NewPeer())
        using (var cancel = new CancellationTokenSource())
        {
            Task accept = peer.AcceptConnectionAsync(cancel.Token); cancel.Cancel();
            await Reject<OperationCanceledException>(() => accept);
            await Reject<IOException>(() => peer.AcceptConnectionAsync());
        }
        using (var peer = NewPeer())
        using (TcpClient remote = await Connect(peer))
        using (var cancel = new CancellationTokenSource())
        {
            Task<int> receive = peer.ReadFrameAsync(new byte[1514], cancel.Token).AsTask();
            await remote.GetStream().WriteAsync(new byte[] { 0, 0 }); cancel.Cancel();
            await Reject<OperationCanceledException>(async () => { await receive; });
            HostCheck.Equal(0L, peer.FramesRead);
            await Reject<IOException>(() => peer.ReadFrameAsync(new byte[1514]).AsTask());
        }
    }

    [HostTest("qemu.ethernet-peer real lifetime bounds accept and connected read", HostLane.Fast, 5)]
    private static void Lifetime() => LifetimeAsync().GetAwaiter().GetResult();

    private static async Task LifetimeAsync()
    {
        using (var peer = new QemuEthernetPeer(TimeSpan.FromSeconds(1)))
            await Reject<OperationCanceledException>(() => peer.AcceptConnectionAsync());
        using (var peer = new QemuEthernetPeer(TimeSpan.FromSeconds(1)))
        using (TcpClient remote = await Connect(peer))
        {
            await Reject<OperationCanceledException>(() => peer.ReadFrameAsync(new byte[1514]).AsTask());
            await Reject<IOException>(() => peer.WriteFrameAsync(new byte[14]).AsTask());
        }
    }

    [HostTest("qemu.ethernet-peer single accept overlapping read and disposal ownership", HostLane.Fast, 6)]
    private static void Ownership() => OwnershipAsync().GetAwaiter().GetResult();

    private static async Task OwnershipAsync()
    {
        using (var peer = NewPeer())
        using (TcpClient remote = await Connect(peer))
        {
            await Reject<InvalidOperationException>(() => peer.AcceptConnectionAsync());
            byte[] buffer = new byte[1514]; Task<int> receive = peer.ReadFrameAsync(buffer).AsTask();
            await Reject<InvalidOperationException>(() => peer.ReadFrameAsync(new byte[1514]).AsTask());
            await remote.GetStream().WriteAsync(Packet(Frame(14, 0x57)));
            HostCheck.Equal(14, await receive); HostCheck.Equal(1L, peer.FramesRead);
            receive = peer.ReadFrameAsync(buffer).AsTask(); peer.Dispose(); peer.Dispose();
            await Closed(async () => { await receive; });
            await Reject<ObjectDisposedException>(() => peer.ReadFrameAsync(buffer).AsTask());
        }
        using (var peer = NewPeer())
        {
            Task accept = peer.AcceptConnectionAsync();
            await Reject<InvalidOperationException>(() => peer.AcceptConnectionAsync());
            peer.Dispose(); await Closed(() => accept);
        }
    }

    private static QemuEthernetPeer NewPeer() => new(TimeSpan.FromSeconds(10));

    private static async Task<TcpClient> Connect(QemuEthernetPeer peer)
    {
        var remote = new TcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            Task accept = peer.AcceptConnectionAsync(timeout.Token);
            await remote.ConnectAsync(IPAddress.Loopback, peer.Port, timeout.Token);
            await accept;
            return remote;
        }
        catch { remote.Dispose(); throw; }
    }

    private static byte[] Frame(int size, byte seed)
    {
        byte[] bytes = new byte[size];
        for (int i = 0; i < size; i++) bytes[i] = unchecked((byte)(seed + i * 3));
        return bytes;
    }

    private static byte[] Packet(byte[] frame)
    {
        byte[] wire = new byte[frame.Length + 4];
        // Independent bytes for lengths 14 and 1514, rather than reusing the production serializer.
        if (frame.Length == 14) wire[3] = 14;
        else if (frame.Length == 1514) { wire[2] = 5; wire[3] = 234; }
        else throw new ArgumentOutOfRangeException(nameof(frame));
        frame.CopyTo(wire, 4); return wire;
    }

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        bool rejected = false;
        try { await action().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (T) { rejected = true; }
        HostCheck.True(rejected, "Expected " + typeof(T).Name);
    }

    private static async Task Closed(Func<Task> action)
    {
        bool closed = false;
        try { await action().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException) { closed = true; }
        HostCheck.True(closed, "Disposal must interrupt pending socket I/O.");
    }
}
