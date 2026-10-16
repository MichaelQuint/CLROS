using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Finite external-peer measurement fixture requiring IPv4 TCP, socket options,
/// nonblocking I/O, monotonic time and sleeping. Host success is not guest acceptance.
/// </summary>
internal static class Program
{
    private const int ConnectionCount = 4;
    private const long DeadlineMilliseconds = 30_000;

    private static int Main(string[] args)
    {
        Console.WriteLine("TEST_START nativeaot.socket");
        int port = 8080;
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--port" ||
            !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out port) ||
            port is < 1 or > 65535))
        {
            Console.WriteLine("TEST_FAIL nativeaot.socket reason=arguments");
            return 2;
        }

        int index = -1;
        try
        {
            byte[] request = Encoding.ASCII.GetBytes("CLROS_SOCKET_REQUEST");
            byte[] reply = Encoding.ASCII.GetBytes("CLROS_SOCKET_REPLY");
            byte[] resetRequest = [0x52];
            byte[] payload = new byte[65_536];
            byte[] receiveBuffer = new byte[3];
            for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i % 251);

            using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Blocking = false;
            listener.Bind(new IPEndPoint(IPAddress.Any, port));
            listener.Listen(ConnectionCount);
            Console.WriteLine($"SOCKET_LISTEN port={port} connections={ConnectionCount} deadline_ms={DeadlineMilliseconds}");

            Counts total = default;
            for (index = 0; index < ConnectionCount; index++)
            {
                Stopwatch deadline = Stopwatch.StartNew();
                Counts current = default;
                using Socket peer = Accept(listener, deadline, ref current);
                peer.Blocking = false;
                peer.SendBufferSize = 4096;
                peer.NoDelay = true;

                ReceiveExpected(peer, index == 1 ? resetRequest : request, receiveBuffer, deadline, ref current);
                if (index != 0)
                    ReceiveTerminal(peer, index == 1, receiveBuffer, deadline, ref current);
                if (index != 1)
                    SendAll(peer, index == 0 ? payload : reply, deadline, ref current);
                if (index >= 2) peer.Shutdown(SocketShutdown.Send);

                Console.WriteLine($"SOCKET_CONNECTION index={index} {current}");
                total.Add(current);
            }

            Console.WriteLine($"TEST_PASS nativeaot.socket connections={ConnectionCount} {total}");
            return 0;
        }
        catch (SocketException error)
        {
            Console.WriteLine($"TEST_FAIL nativeaot.socket index={index} reason=socket error={error.SocketErrorCode}");
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"TEST_FAIL nativeaot.socket index={index} reason=deadline");
        }
        catch (InvalidOperationException error)
        {
            Console.WriteLine($"TEST_FAIL nativeaot.socket index={index} reason={error.Message}");
        }
        catch (Exception error)
        {
            Console.WriteLine($"TEST_FAIL nativeaot.socket index={index} reason=unexpected type={error.GetType().Name}");
        }
        return 1;
    }

    private static Socket Accept(Socket listener, Stopwatch deadline, ref Counts counts)
    {
        while (true)
        {
            CheckDeadline(deadline);
            try { return listener.Accept(); }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock)
            {
                counts.AcceptBlocked++;
                Thread.Sleep(1);
            }
        }
    }

    private static void ReceiveExpected(Socket peer, byte[] expected, byte[] buffer, Stopwatch deadline, ref Counts counts)
    {
        int offset = 0;
        while (offset < expected.Length)
        {
            CheckDeadline(deadline);
            try
            {
                int received = peer.Receive(buffer, 0, Math.Min(3, expected.Length - offset), SocketFlags.None);
                if (received == 0) throw new InvalidOperationException("request.eof");
                counts.Received += received;
                for (int i = 0; i < received; i++)
                    if (buffer[i] != expected[offset + i]) throw new InvalidOperationException("request.mismatch");
                offset += received;
            }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock)
            {
                counts.ReceiveBlocked++;
                Thread.Sleep(1);
            }
        }
    }

    private static void ReceiveTerminal(Socket peer, bool requireReset, byte[] buffer, Stopwatch deadline, ref Counts counts)
    {
        while (true)
        {
            CheckDeadline(deadline);
            try
            {
                int received = peer.Receive(buffer, 0, 1, SocketFlags.None);
                counts.Received += received;
                if (received != 0) throw new InvalidOperationException("request.extra-data");
                if (requireReset) throw new InvalidOperationException("reset.orderly-eof");
                counts.Eof++;
                return;
            }
            catch (SocketException error) when (requireReset && error.SocketErrorCode == SocketError.ConnectionReset)
            {
                counts.Reset++;
                return;
            }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock)
            {
                counts.ReceiveBlocked++;
                Thread.Sleep(1);
            }
        }
    }

    private static void SendAll(Socket peer, byte[] buffer, Stopwatch deadline, ref Counts counts)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            CheckDeadline(deadline);
            try
            {
                int remaining = buffer.Length - offset;
                int sent = peer.Send(buffer, offset, remaining, SocketFlags.None);
                if (sent == 0) throw new InvalidOperationException("send.zero");
                if (sent < remaining) counts.SendPartial++;
                offset += sent;
                counts.Sent += sent;
            }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock)
            {
                counts.SendBlocked++;
                Thread.Sleep(1);
            }
        }
    }

    private static void CheckDeadline(Stopwatch deadline)
    {
        if (deadline.ElapsedMilliseconds >= DeadlineMilliseconds) throw new TimeoutException();
    }

    private struct Counts
    {
        internal int Received, Sent, AcceptBlocked, ReceiveBlocked, SendBlocked, SendPartial, Eof, Reset;
        internal void Add(Counts value)
        {
            Received += value.Received; Sent += value.Sent; AcceptBlocked += value.AcceptBlocked;
            ReceiveBlocked += value.ReceiveBlocked; SendBlocked += value.SendBlocked;
            SendPartial += value.SendPartial; Eof += value.Eof; Reset += value.Reset;
        }
        public override readonly string ToString() =>
            $"received={Received} sent={Sent} accept_blocked={AcceptBlocked} receive_blocked={ReceiveBlocked} send_blocked={SendBlocked} send_partial={SendPartial} eof={Eof} reset={Reset}";
    }
}
