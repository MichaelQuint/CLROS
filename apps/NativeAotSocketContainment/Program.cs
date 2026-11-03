using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Finite three-peer containment fixture for a controlled external Ethernet peer.
/// Every socket is nonblocking; one independent sleeping thread witnesses progress.
/// The caller owns publication and guest/wire qualification. Host success alone
/// cannot establish that a guest isolated a TCP timeout or rejected an RX frame.
/// </summary>

internal static partial class Program
{
    private const int PeerCount = 3, Rounds = 15;
    private const long DeadlineMilliseconds = 100_000;
    private static int Main(string[] args)
    {
        Console.WriteLine("TEST_START nativeaot.socket.containment");
        int port = 8080;
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--port" || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535))
        {
            Console.WriteLine("TEST_FAIL nativeaot.socket.containment reason=arguments");
            return 2;
        }

        return Run(port);
    }

    private static int Run(int port)
    {
        Stopwatch clock = Stopwatch.StartNew();
        Progress progress = new();
        Thread worker = new(() =>
        {
            while (Volatile.Read(ref progress.Stop) == 0)
            {
                Interlocked.Increment(ref progress.Value);
                Thread.Sleep(250);
            }
        });
        ContainmentPeer?[] accepted = new ContainmentPeer?[PeerCount], roles = new ContainmentPeer?[PeerCount];
        int acceptedCount = 0, queued = 0, timeoutWorker = 0, blockedWorker = 0;
        long timeoutAt = -1;
        bool started = false, saturated = false, eof = false, shutdown = false, success = false;
        byte[] payload = new byte[65_536], one = new byte[1];
        for (int i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i % 251);
        try
        {
            using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Blocking = false;
            listener.Bind(new IPEndPoint(IPAddress.Any, port));
            listener.Listen(PeerCount);
            worker.Start();
            started = true;
            Console.WriteLine($"SOCKET_CONTAINMENT_LISTEN port={port} peers=3 rounds=15 interval_ms=5000 deadline_ms={DeadlineMilliseconds}");
            while (true)
            {
                if (clock.ElapsedMilliseconds >= DeadlineMilliseconds)
                    throw new TimeoutException();
                if (acceptedCount < PeerCount)
                {
                    try
                    {
                        Socket socket = listener.Accept();
                        accepted[acceptedCount++] = new(socket);
                        socket.Blocking = false;
                        socket.NoDelay = true;
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock)
                    {
                    }
                }

                for (int i = 0; i < acceptedCount; i++)
                {
                    ContainmentPeer peer = accepted[i]!;
                    if (peer.Role >= 0)
                        continue;
                    try
                    {
                        if (peer.Socket.Receive(one, SocketFlags.None) != 1)
                            throw new InvalidOperationException("role.eof");
                        int role = one[0];
                        if (role >= PeerCount || roles[role] is not null)
                            throw new InvalidOperationException("role.invalid");
                        peer.Role = role;
                        peer.Started = clock.ElapsedMilliseconds;
                        roles[role] = peer;
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock)
                    {
                    }
                }

                if (roles[0] is ContainmentPeer a && roles[1] is not null && roles[2] is not null && !shutdown)
                {
                    if (!saturated)
                    {
                        if (queued == payload.Length)
                            throw new InvalidOperationException("timeout.send.not-blocked");
                        try
                        {
                            int sent = a.Socket.Send(payload, queued, payload.Length - queued, SocketFlags.None);
                            if (sent <= 0)
                                throw new InvalidOperationException("timeout.send.zero");
                            queued += sent;
                        }
                        catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock)
                        {
                            if (queued < 32_768)
                                throw new InvalidOperationException("timeout.send.too-short");
                            saturated = true;
                            blockedWorker = Volatile.Read(ref progress.Value);
                        }
                    }
                    else if (timeoutAt < 0)
                    {
                        try
                        {
                            _ = a.Socket.Receive(one, SocketFlags.None);
                            throw new InvalidOperationException("timeout.receive.completed");
                        }
                        catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock)
                        {
                        }
                        catch (SocketException error) when (error.SocketErrorCode == SocketError.TimedOut)
                        {
                            long elapsed = clock.ElapsedMilliseconds - a.Started;
                            timeoutWorker = Volatile.Read(ref progress.Value);
                            if (elapsed < 60_000 || timeoutWorker <= blockedWorker)
                                throw new InvalidOperationException("timeout.early-or-no-progress");
                            timeoutAt = clock.ElapsedMilliseconds;
                            Console.WriteLine($"SOCKET_CONTAINMENT_TIMEOUT elapsed_ms={elapsed} queued={queued} worker={timeoutWorker}");
                        }
                    }
                    else if (!eof)
                    {
                        if (a.Socket.Receive(one, SocketFlags.None) != 0)
                            throw new InvalidOperationException("timeout.receive.not-eof");
                        eof = true;
                    }
                    else
                    {
                        one[0] = 0x5a;
                        try
                        {
                            _ = a.Socket.Send(one, SocketFlags.None);
                            throw new InvalidOperationException("timeout.send.not-shutdown");
                        }
                        catch (SocketException error) when (error.SocketErrorCode == SocketError.Shutdown)
                        {
                            shutdown = true;
                        }
                    }
                }

                for (int role = 1; role < PeerCount; role++)
                {
                    if (roles[role] is not ContainmentPeer peer || peer.Round == Rounds)
                        continue;
                    if (!peer.ReplyPending)
                    {
                        try
                        {
                            if (peer.Socket.Receive(one, SocketFlags.None) != 1 || one[0] != peer.Round)
                                throw new InvalidOperationException("healthy.round");
                            peer.ReplyPending = true;
                        }
                        catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock)
                        {
                            continue;
                        }
                    }

                    one[0] = (byte)(peer.Round ^ 0x80);
                    try
                    {
                        if (peer.Socket.Send(one, SocketFlags.None) != 1)
                            throw new InvalidOperationException("healthy.send");
                        peer.ReplyPending = false;
                        peer.Round++;
                        if (peer.Round == Rounds)
                        {
                            peer.Completed = clock.ElapsedMilliseconds;
                            peer.Worker = Volatile.Read(ref progress.Value);
                            long elapsed = peer.Completed - peer.Started;
                            if (elapsed < 70_000)
                                throw new InvalidOperationException("healthy.too-short");
                            Console.WriteLine($"SOCKET_CONTAINMENT_HEALTHY role={role} rounds=15 elapsed_ms={elapsed} worker={peer.Worker}");
                        }
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock)
                    {
                    }
                }

                if (shutdown && roles[1] is { Round: Rounds } b && roles[2] is { Round: Rounds } c)
                {
                    if (b.Completed <= timeoutAt || c.Completed <= timeoutAt || b.Worker <= timeoutWorker || c.Worker <= timeoutWorker)
                        throw new InvalidOperationException("healthy.no-post-timeout-progress");
                    success = true;
                    break;
                }

                // The guest syscall budget is finite. A100ms poll cadence keeps
                // this70-second case bounded without spending thousands of
                // unnecessary receive/sleep calls on the main worker.
                Thread.Sleep(100);
            }
        }
        catch (TimeoutException)
        {
            Console.WriteLine("TEST_FAIL nativeaot.socket.containment reason=deadline");
        }
        catch (SocketException error)
        {
            Console.WriteLine($"TEST_FAIL nativeaot.socket.containment reason=socket error={error.SocketErrorCode}");
        }
        catch (InvalidOperationException error)
        {
            Console.WriteLine($"TEST_FAIL nativeaot.socket.containment reason={error.Message}");
        }
        catch (Exception error)
        {
            Console.WriteLine($"TEST_FAIL nativeaot.socket.containment reason=unexpected type={error.GetType().Name}");
        }
        finally
        {
            Volatile.Write(ref progress.Stop, 1);
            if (started && !worker.Join(5_000))
            {
                Console.WriteLine("TEST_FAIL nativeaot.socket.containment reason=worker.join");
                Environment.Exit(70);
            }

            foreach (ContainmentPeer? peer in accepted)
                peer?.Socket.Dispose();
        }

        if (!success)
            return 1;
        if (clock.ElapsedMilliseconds >= DeadlineMilliseconds)
        {
            Console.WriteLine("TEST_FAIL nativeaot.socket.containment reason=deadline");
            return 1;
        }

        Console.WriteLine($"TEST_PASS nativeaot.socket.containment peers=3 timedout=1 queued={queued} eof=1 shutdown=1 worker_before={blockedWorker} worker_timeout={timeoutWorker} worker={Volatile.Read(ref progress.Value)} elapsed_ms={clock.ElapsedMilliseconds}");
        return 0;
    }
}
