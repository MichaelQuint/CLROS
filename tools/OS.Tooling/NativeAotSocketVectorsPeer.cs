using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;
/// <summary>Independent bounded wire peer for the fixed BufferList fixture.
/// It owns its sockets through completion and retains no guest pointers. Output
/// and live progress gates grant no execution authority; final sealed native,
/// complete serial and packet validation remain required.</summary>

public static partial class NativeAotSocketVectorsPeer
{
    /// <summary>Own one fixed guest-peer child beside the supplied runner.
    /// Wait for its original accept attempt before launch, await both children
    /// on failure/cancellation, and retain a fresh peer log. Completion binds the
    /// canonical serial path and the same runner object; final guest joins remain
    /// required. The runner callback must honor cancellation and confirm exit.</summary>

    public static async Task<Completion> RunAsync(string root, string peerLog, string serial, Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serial);
        root = System.IO.Path.GetFullPath(root);
        serial = System.IO.Path.GetFullPath(serial, root);
        var execution = await NativeAotSocketPeerLifecycle.RunAsync(root, peerLog, runRunner, cancellation, token => NativeAotSocketPeer.WaitForAcceptAsync(serial, false, token), serial, vectors: true);
        return new(execution.Root, execution.Port, execution.Path, execution.Sha256, execution.Runner, execution.ReceiveGateSerial);
    }

    /// <summary>Wait for the original guest's accept attempt, then drive four
    /// peers. Serial observations only delay stimulus. Caller owns output and
    /// raw serial; cancellation closes sockets and propagates without a pass.</summary>

    public static async Task RunGuestPeerAsync(string root, int port, string serial, TextWriter output, CancellationToken cancellation = default)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        ArgumentNullException.ThrowIfNull(output);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        root = Path.GetFullPath(root);
        serial = Path.GetFullPath(serial, root);
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(serial)!, "socket_vectors_peer_serial");
        await NativeAotSocketPeer.WaitForAcceptAsync(serial, false, deadline.Token);
        await RunPeerAsync(port, output, (index, total, token) => NativeAotSocketPeer.WaitForSerialAsync(serial, text => HasReceiveProgress(text, index, total), token), deadline.Token, token => NativeAotSocketPeer.WaitForSerialAsync(serial, HasSendBackpressure, token));
    }

    /// <summary>Send exact requests, gate first3 bytes and consumed reset request,
    /// peek the first response byte without consuming it, then wait for the
    /// supplied send-backpressure gate (guest) or250ms (standalone Linux control)
    /// before verifying bytes with257-byte reads.
    /// The caller supplies actual app progress, owns output and must await this
    /// task. The two-minute deadline/cancellation propagates after socket disposal.
    /// Configured short I/O/hold is stimulus, not proof of blocked or partial sends.</summary>

    public static async Task RunPeerAsync(int port, TextWriter output, Func<int, uint, CancellationToken, Task> waitForReceive, CancellationToken cancellation = default, Func<CancellationToken, Task>? waitForSendBackpressure = null)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(waitForReceive);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        CancellationToken token = deadline.Token;
        output.WriteLine("TEST_START nativeaot.socket.vectors.peer");
        byte[] buffer = new byte[257];
        for (int index = 0; index < 4; index++)
        {
            using var peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
                ReceiveBufferSize = 4096
            };
            await peer.ConnectAsync(IPAddress.Loopback, port, token);
            byte[] request = new byte[64];
            for (int i = 0; i < request.Length; i++)
                request[i] = (byte)((index * 37 + i * 11) % 251);
            if (index == 0)
            {
                await Send(request.AsMemory(0, 3));
                await waitForReceive(0, 3, token);
                await Send(request.AsMemory(3, 17));
                await Send(request.AsMemory(20));
            }
            else
                await Send(request);
            if (index == 1)
            {
                await waitForReceive(1, 64, token);
                peer.LingerState = new(true, 0);
                output.WriteLine("SOCKET_VECTOR_PEER index=1 sent=64 received=0 close=reset");
                continue;
            }

            if (index == 2)
            {
                string close = "eof";
                try
                {
                    if (await peer.ReceiveAsync(buffer.AsMemory(0, 1), SocketFlags.None, token) != 0)
                        throw new ToolFailure("socket_vectors_peer_dispose_payload");
                }
                catch (SocketException error)when (error.SocketErrorCode == SocketError.ConnectionReset)
                {
                    close = "reset";
                }

                output.WriteLine("SOCKET_VECTOR_PEER index=2 sent=64 received=0 close=" + close + " disposition=disposed");
                continue;
            }

            peer.Shutdown(SocketShutdown.Send);
            if (index == 0)
            {
                int peeked = await peer.ReceiveAsync(buffer.AsMemory(0, 1), SocketFlags.Peek, token);
                if (peeked != 1 || buffer[0] != 0)
                    throw new ToolFailure("socket_vectors_peer_first_byte");
                if (waitForSendBackpressure is null)
                {
                    output.WriteLine("SOCKET_VECTOR_PEER_HOLD index=0 peeked=1 hold_ms=250");
                    await Task.Delay(250, token);
                }
                else
                {
                    await waitForSendBackpressure(token);
                    output.WriteLine("SOCKET_VECTOR_PEER_HOLD index=0 peeked=1 until=sendmsg_eagain");
                }
            }

            int expected = index == 0 ? 524288 : 64, received = 0;
            while (received < expected)
            {
                int count = await peer.ReceiveAsync(buffer.AsMemory(0, Math.Min(buffer.Length, expected - received)), SocketFlags.None, token);
                if (count == 0)
                    throw new ToolFailure("socket_vectors_peer_early_eof");
                for (int i = 0; i < count; i++)
                    if (buffer[i] != (byte)((received + i) % 251))
                        throw new ToolFailure("socket_vectors_peer_payload");
                received += count;
            }

            if (await peer.ReceiveAsync(buffer.AsMemory(0, 1), SocketFlags.None, token) != 0)
                throw new ToolFailure("socket_vectors_peer_trailing_payload");
            output.WriteLine($"SOCKET_VECTOR_PEER index={index} sent=64 received={received} close=eof");
            async Task Send(ReadOnlyMemory<byte> bytes)
            {
                while (!bytes.IsEmpty)
                {
                    int sent = await peer.SendAsync(bytes, SocketFlags.None, token);
                    if (sent <= 0)
                        throw new ToolFailure("socket_vectors_peer_send");
                    bytes = bytes[sent..];
                }
            }
        }

        token.ThrowIfCancellationRequested();
        output.WriteLine("TEST_PASS nativeaot.socket.vectors.peer");
    }

    /// <summary>Value-only provisional gate over complete current call/vector
    /// records. Match actual positive, non-peek receives to the requested accepted
    /// connection's live fd; partial/malformed snapshots return false. This does
    /// not validate mappings, full scheduling, native execution or packet authority.</summary>

    public static bool HasReceiveProgress(string text, int connection, uint total)
    {
        if (connection is < 0 or > 3 || total is 0 or > 64)
            return false;
        return HasProgress(text, connection, total, sendBackpressure: false);
    }

    /// <summary>Provisional stimulus gate requiring a complete joined sendmsg
    /// EAGAIN on the first accepted connection while its fd remains live.
    /// Receive EAGAIN, later/reused fds and partial/malformed snapshots do not
    /// release reads. Final immutable call/packet validation remains mandatory.</summary>

    public static bool HasSendBackpressure(string text) => HasProgress(text, 0, 0, sendBackpressure: true);
    private static bool HasProgress(string text, int connection, uint total, bool sendBackpressure)
    {
        int end = text.LastIndexOf('\n');
        if (end < 0)
            return false;
        string[] rows = text[..(end + 1)].Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.TrimEnd('\r')).ToArray();
        try
        {
            var calls = QemuTranscripts.ReadLinuxCallReturns(rows, allowEpoll: true).ToDictionary(call => call.RecordIndex);
            var messages = NativeAotSocketMessages.Read(rows, calls).ToDictionary(message => message.CallRecordIndex);
            var accepted = new Dictionary<ulong, int>();
            ulong[] received = new ulong[4];
            int count = 0;
            bool blocked = false;
            foreach (var call in calls.Values.OrderBy(call => call.RecordIndex))
            {
                var match = Regex.Match(rows[call.RecordIndex], @"^M16_008_CALL identity=\d+ nr=(\d+) a0=(\d+) ", RegexOptions.CultureInvariant);
                if (!match.Success || !ulong.TryParse(match.Groups[1].Value, out ulong number) || !ulong.TryParse(match.Groups[2].Value, out ulong fd))
                    return false;
                if (number is 43 or 288 && call.ValueBits is >= 3 and < 16)
                {
                    if (count == 4 || !accepted.TryAdd(call.ValueBits, count++))
                        return false;
                }
                else if (number == 3 && call.ValueBits == 0)
                    accepted.Remove(fd);
                else if (number == 46 && unchecked((long)call.ValueBits) == -11 && accepted.TryGetValue(fd, out int sender) && sender == 0)
                {
                    if (!messages.TryGetValue(call.RecordIndex, out var message) || message.Flags != 0 || message.Total == 0)
                        return false;
                    blocked = true;
                }
                else if (number is 45 or 47 && unchecked((long)call.ValueBits) > 0 && accepted.TryGetValue(fd, out int index))
                {
                    if (number != 47 || !messages.TryGetValue(call.RecordIndex, out var message) || call.ValueBits > 8192 || call.ValueBits > message.Total)
                        return false;
                    if ((message.Flags & 2) == 0)
                    {
                        if (call.ValueBits > 64 - received[index])
                            return false;
                        received[index] += call.ValueBits;
                    }
                }
            }

            return accepted.Values.Contains(connection) && (sendBackpressure ? blocked : received[connection] == total);
        }
        catch (ToolFailure)
        {
        }

        return false;
    }

    /// <summary>Require the exact finite peer output and the recorded EOF/reset
    /// disposal outcome. Supplied text cannot create an execution receipt.</summary>

    public static void InspectOutput(string text, bool requireSendBackpressure = false)
    {
        string before = "TEST_START nativeaot.socket.vectors.peer\n" + (requireSendBackpressure ? "SOCKET_VECTOR_PEER_HOLD index=0 peeked=1 until=sendmsg_eagain\n" : "SOCKET_VECTOR_PEER_HOLD index=0 peeked=1 hold_ms=250\n") + "SOCKET_VECTOR_PEER index=0 sent=64 received=524288 close=eof\n" + "SOCKET_VECTOR_PEER index=1 sent=64 received=0 close=reset\n";
        const string after = " disposition=disposed\nSOCKET_VECTOR_PEER index=3 sent=64 received=64 close=eof\nTEST_PASS nativeaot.socket.vectors.peer\n";
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (text != before + "SOCKET_VECTOR_PEER index=2 sent=64 received=0 close=eof" + after && text != before + "SOCKET_VECTOR_PEER index=2 sent=64 received=0 close=reset" + after)
            throw new ToolFailure("socket_vectors_peer_output");
    }
}
