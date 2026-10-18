using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace OS.Apps.NativeAotSocketVectors;

/// <summary>Finite IPv4 passive BufferList fixture. It requires nonblocking TCP,
/// reset delivery, monotonic time and real async receive disposal completion.
/// Run owns every socket/task through completion; the caller owns output.
/// Numeric failures return nonzero. Managed host success grants no guest evidence.</summary>
public static class SocketVectorsFixture
{
    private const int DeadlineMilliseconds = 30_000, MaximumOperations = 100_000;

    /// <summary>Run four serialized peers on --port (default8080). Explicit
    /// --policy first checks the CLROS-only manifest bind and descriptor limits.
    /// Connections0/3
    /// half-close after64 request bytes and receive524288/64 response bytes;1 resets
    /// after its request;2 sends no more data while a pending receive is disposed.
    /// Each request byte is(index*37+offset*11)%251; reply byte is offset%251.
    /// Output must remain writable for this synchronous call. No pointers escape.</summary>
    public static int Run(string[] args, TextWriter output)
    {
        output.WriteLine("TEST_START nativeaot.socket.vectors");
        int port = 8080, index = -1, reason = 0, error = 0, exit = 1;
        bool policy = args.Length == 1 && args[0] == "--policy";
        if (!policy && args.Length != 0 && (args.Length != 2 || args[0] != "--port" ||
            !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535))
        {
            output.WriteLine("TEST_FAIL nativeaot.socket.vectors index=-1 reason=1 error=0");
            output.WriteLine("APP_EXIT nativeaot.socket.vectors 2"); return 2;
        }
        try
        {
            // Only the declared CLROS guest supplies this bind/fd policy.
            // All negative owners close before normal four-peer progress begins.
            if (policy && !SocketPolicyFixture.Run(output)) throw new Failure(20);
            using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Blocking = false; listener.Bind(new IPEndPoint(IPAddress.Any, port)); listener.Listen(4);
            output.WriteLine($"SOCKET_VECTOR_LISTEN port={port} connections=4 deadline_ms={DeadlineMilliseconds} max_operations={MaximumOperations}");
            Counts total = new();
            for (index = 0; index < 4; index++)
            {
                Budget budget = new(); Counts current = new();
                using Socket peer = Accept(listener, budget, current);
                peer.Blocking = false; peer.SendBufferSize = 4096; peer.NoDelay = true;
                ReceiveRequest(peer, index, budget, current, output);
                output.WriteLine($"SOCKET_VECTOR_REQUEST index={index} bytes=64");
                if (index == 2) DisposePendingReceive(peer, budget, current, output);
                else
                {
                    ReceiveTerminal(peer, index == 1, budget, current);
                    if (index != 1)
                    {
                        SendResponse(peer, index == 0 ? 524_288 : 64, budget, current);
                        peer.Shutdown(SocketShutdown.Send);
                    }
                }
                output.WriteLine($"SOCKET_VECTOR_CONNECTION index={index} {current}"); total.Add(current);
            }
            output.WriteLine($"TEST_PASS nativeaot.socket.vectors connections=4 received={total.Received} sent={total.Sent} reset={total.Reset} pending={total.Pending} aborted={total.Aborted}");
            exit = 0;
        }
        catch (Failure failure) { reason = failure.Code; }
        catch (SocketException failure) { reason = 90; error = (int)failure.SocketErrorCode; }
        catch (Exception) { reason = 99; }
        if (exit != 0) output.WriteLine($"TEST_FAIL nativeaot.socket.vectors index={index} reason={reason} error={error}");
        output.WriteLine($"APP_EXIT nativeaot.socket.vectors {exit}"); return exit;
    }

    private static Socket Accept(Socket listener, Budget budget, Counts counts)
    {
        while (true)
        {
            budget.Check();
            try { return listener.Accept(); }
            catch (SocketException failure) when (failure.SocketErrorCode == SocketError.WouldBlock)
            { counts.AcceptBlocked++; Thread.Sleep(1); }
        }
    }

    private static void ReceiveRequest(Socket peer, int index, Budget budget, Counts counts, TextWriter output)
    {
        byte[] bytes = new byte[74]; Array.Fill(bytes, (byte)0xcc);
        List<ArraySegment<byte>> buffers = [new(bytes, 5, 17), new(bytes, 22, 47)];
        int received = 0;
        while (received != 64)
        {
            budget.Check();
            try
            {
                counts.ReceiveCalls++;
                int count = peer.Receive(buffers, SocketFlags.None);
                if (count <= 0 || count > 64 - received) throw new Failure(10);
                if (count < 64 - received) counts.ReceivePartial++;
                for (int i = 0; i < count; i++)
                    if (bytes[5 + received + i] != (byte)((index * 37 + (received + i) * 11) % 251)) throw new Failure(11);
                received += count; counts.Received += count; Advance(buffers, count);
                output.WriteLine($"SOCKET_VECTOR_RECEIVE index={index} bytes={count} total={received} remaining={64 - received}");
            }
            catch (SocketException failure) when (failure.SocketErrorCode == SocketError.WouldBlock)
            { counts.ReceiveBlocked++; Thread.Sleep(1); }
        }
        for (int i = 0; i < 5; i++) if (bytes[i] != 0xcc || bytes[69 + i] != 0xcc) throw new Failure(12);
    }

    private static void ReceiveTerminal(Socket peer, bool reset, Budget budget, Counts counts)
    {
        byte[] bytes = new byte[5];
        List<ArraySegment<byte>> buffers = [new(bytes, 1, 1), new(bytes, 3, 1)];
        while (true)
        {
            budget.Check();
            try
            {
                counts.ReceiveCalls++;
                if (peer.Receive(buffers, SocketFlags.None) != 0) throw new Failure(13);
                if (reset) throw new Failure(14);
                return;
            }
            catch (SocketException failure) when (reset && failure.SocketErrorCode == SocketError.ConnectionReset)
            { counts.Reset++; return; }
            catch (SocketException failure) when (failure.SocketErrorCode == SocketError.WouldBlock)
            { counts.ReceiveBlocked++; Thread.Sleep(1); }
        }
    }

    private static void SendResponse(Socket peer, int length, Budget budget, Counts counts)
    {
        byte[] bytes = new byte[length + 14];
        for (int i = 0; i < length; i++) bytes[7 + i] = (byte)(i % 251);
        // Every native call stays within the adapter's reviewed64KiB aggregate.
        for (int offset = 0; offset < length; offset += 65_536)
        {
            int batch = Math.Min(65_536, length - offset), split = batch / 3, sent = 0;
            List<ArraySegment<byte>> buffers = [new(bytes, 7 + offset, split), new(bytes, 7 + offset + split, batch - split)];
            while (sent != batch)
            {
                budget.Check();
                try
                {
                    counts.SendCalls++;
                    int count = peer.Send(buffers, SocketFlags.None);
                    if (count <= 0 || count > batch - sent) throw new Failure(15);
                    if (count < batch - sent) counts.SendPartial++;
                    sent += count; counts.Sent += count; Advance(buffers, count);
                }
                catch (SocketException failure) when (failure.SocketErrorCode == SocketError.WouldBlock)
                { counts.SendBlocked++; Thread.Sleep(1); }
            }
        }
    }

    private static void DisposePendingReceive(Socket peer, Budget budget, Counts counts, TextWriter output)
    {
        byte[] bytes = new byte[12];
        List<ArraySegment<byte>> buffers = [new(bytes, 1, 3), new(bytes, 6, 3)];
        budget.Check(); counts.ReceiveCalls++;
        Task<int> pending = peer.ReceiveAsync(buffers, SocketFlags.None);
        if (pending.IsCompleted) throw new Failure(20);
        counts.Pending++; output.WriteLine("SOCKET_VECTOR_PENDING index=2 pending=1");
        peer.Dispose();
        while (!pending.IsCompleted) { budget.Check(); Thread.Sleep(1); }
        try { _ = pending.GetAwaiter().GetResult(); throw new Failure(21); }
        catch (SocketException failure) when (failure.SocketErrorCode == SocketError.OperationAborted) { counts.Aborted++; }
        catch (ObjectDisposedException) { counts.Aborted++; }
    }

    private static void Advance(List<ArraySegment<byte>> buffers, int bytes)
    {
        while (bytes != 0)
        {
            ArraySegment<byte> first = buffers[0];
            if (bytes >= first.Count) { bytes -= first.Count; buffers.RemoveAt(0); }
            else { buffers[0] = new(first.Array!, first.Offset + bytes, first.Count - bytes); return; }
        }
    }

    private sealed class Budget
    {
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private int operations;
        internal void Check()
        { if (++operations > MaximumOperations || elapsed.ElapsedMilliseconds >= DeadlineMilliseconds) throw new Failure(2); }
    }

    private sealed class Failure(int code) : Exception { internal int Code => code; }

    private sealed class Counts
    {
        internal int Received, Sent, AcceptBlocked, ReceiveCalls, ReceivePartial, ReceiveBlocked, SendCalls, SendPartial, SendBlocked, Reset, Pending, Aborted;
        internal void Add(Counts other)
        { Received += other.Received; Sent += other.Sent; Reset += other.Reset; Pending += other.Pending; Aborted += other.Aborted; }
        public override string ToString() => $"received={Received} sent={Sent} accept_blocked={AcceptBlocked} receive_calls={ReceiveCalls} receive_partial={ReceivePartial} receive_blocked={ReceiveBlocked} send_calls={SendCalls} send_partial={SendPartial} send_blocked={SendBlocked} reset={Reset} pending={Pending} aborted={Aborted}";
    }
}
