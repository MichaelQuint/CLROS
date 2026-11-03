using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Apps.NativeAotSocketVectors;
using OS.Tooling;

internal static partial class NativeAotSocketVectorsPeerTests
{
    [HostTest("socket vector external driver gates real receives then checks reset disposal bytes and later health")]
    private static void Wire() => WireAsync().GetAwaiter().GetResult();
    private static async Task WireAsync()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        using var appOutput = new Progress();
        using var peerOutput = new StringWriter(CultureInfo.InvariantCulture);
        Task<int> app = Task.Run(() => SocketVectorsFixture.Run(["--port", port.ToString(CultureInfo.InvariantCulture)], appOutput));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        Task? peer = null;
        try
        {
            await appOutput.Ready.Task.WaitAsync(deadline.Token);
            peer = NativeAotSocketVectorsPeer.RunPeerAsync(port, peerOutput, (index, total, token) => (index == 0 && total == 3 ? appOutput.Partial.Task : appOutput.ResetRequest.Task).WaitAsync(token), deadline.Token);
            await Task.WhenAll(app, peer);
            HostCheck.Equal(0, await app);
            NativeAotSocketVectorsPeer.InspectOutput(peerOutput.ToString());
            HostCheck.True(appOutput.Success);
            bool ungated = false;
            try
            {
                NativeAotSocketVectorsPeer.InspectOutput(peerOutput.ToString(), requireSendBackpressure: true);
            }
            catch (ToolFailure)
            {
                ungated = true;
            }

            HostCheck.True(ungated);
            foreach (string change in new[]
            {
                peerOutput.ToString().Replace("received=524288", "received=524287", StringComparison.Ordinal),
                peerOutput.ToString() + "extra\n",
                peerOutput.ToString().Replace("TEST_PASS", "TEST_FAIL", StringComparison.Ordinal)
            }

            )
            {
                bool failed = false;
                try
                {
                    NativeAotSocketVectorsPeer.InspectOutput(change);
                }
                catch (ToolFailure)
                {
                    failed = true;
                }

                HostCheck.True(failed);
            }

            foreach (string line in peerOutput.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
                Console.WriteLine("VECTOR_PEER_OUTPUT " + line.TrimEnd('\r'));
        }
        finally
        {
            deadline.Cancel();
            if (peer is not null)
                try
                {
                    await peer;
                }
                catch
                {
                }

            // The app owns its listener and bounded30s budget through completion.
            _ = await app;
        }
    }

    [HostTest("socket vector provisional gate needs complete joined non-peek receives on the accepted fd")]
    private static void Gate()
    {
        string[] rows = [Call(43, 5, 6, 1), Message(2, 64), Vector(2, 0, 17), Vector(2, 1, 47), Call(47, 6, 3, 2)];
        string Raw(IEnumerable<string> values) => string.Join("\r\n", values) + "\r\n";
        HostCheck.True(NativeAotSocketVectorsPeer.HasReceiveProgress(Raw(rows), 0, 3));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasReceiveProgress(Raw(rows), 0, 64));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasReceiveProgress(Raw(rows), 1, 3));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasReceiveProgress(Raw(rows.Where(row => !row.StartsWith("M16_013_", StringComparison.Ordinal))), 0, 3));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasReceiveProgress(Raw(rows).TrimEnd('\r', '\n'), 0, 3));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasReceiveProgress(Raw(rows.Select(row => row.Replace("nr=47 a0=6", "nr=47 a0=7", StringComparison.Ordinal))), 0, 3));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasReceiveProgress(Raw(rows.Select(row => row.Replace("header=8192 flags=0", "header=8192 flags=2", StringComparison.Ordinal).Replace("nr=47 a0=6 a1=8192 a2=0", "nr=47 a0=6 a1=8192 a2=2", StringComparison.Ordinal))), 0, 3));
        string[] complete = [..rows, Message(3, 61), Vector(3, 0, 14), Vector(3, 1, 47), Call(47, 6, 61, 3)];
        HostCheck.True(NativeAotSocketVectorsPeer.HasReceiveProgress(Raw(complete), 0, 64));
        string[] closed = [..rows, Call(3, 6, 0, 3)];
        HostCheck.True(!NativeAotSocketVectorsPeer.HasReceiveProgress(Raw(closed), 0, 3));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasReceiveProgress(Raw([..closed, Call(43, 5, 6, 4)]), 0, 3));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasReceiveProgress(Raw([..rows, Call(47, 6, 1, 3)]), 0, 3));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasReceiveProgress(Raw([..rows, Call(45, 6, 1, 3)]), 0, 3));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasReceiveProgress(Raw([..rows, Message(3, 1), Vector(3, 0, 1), Vector(3, 1, 0), Call(47, 6, 2, 3)]), 0, 3));
    }

    [HostTest("socket vector send gate requires joined live first-connection sendmsg EAGAIN")]
    private static void SendGate()
    {
        string Raw(IEnumerable<string> rows) => string.Join("\r\n", rows) + "\r\n";
        ulong blocked = unchecked((ulong)-11L);
        string[] rows = [Call(43, 5, 6, 1), Message(2, 64, 46), Vector(2, 0, 17), Vector(2, 1, 47), Call(46, 6, blocked, 2)];
        HostCheck.True(NativeAotSocketVectorsPeer.HasSendBackpressure(Raw(rows)));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasSendBackpressure(Raw(rows).TrimEnd('\r', '\n')));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasSendBackpressure(Raw(rows.Where(row => !row.StartsWith("M16_013_", StringComparison.Ordinal)))));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasSendBackpressure(Raw([..rows[..^1], Call(46, 6, 17, 2)])));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasSendBackpressure(Raw(rows.Select(row => row.Replace("nr=46", "nr=47", StringComparison.Ordinal)))));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasSendBackpressure(Raw(rows.Select(row => row.Replace("nr=46 a0=6", "nr=46 a0=7", StringComparison.Ordinal)))));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasSendBackpressure(Raw([..rows, Call(3, 6, 0, 3)])));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasSendBackpressure(Raw([..rows, Call(3, 6, 0, 3), Call(43, 5, 6, 4)])));
        HostCheck.True(!NativeAotSocketVectorsPeer.HasSendBackpressure(Raw(rows.Select(row => row.Replace("generation=1", "generation=0", StringComparison.Ordinal)))));
    }

    private static string Call(uint number, uint fd, ulong value, uint ordinal) => $"M16_008_CALL identity=4294967297 nr={number} a0={fd} a1={(number is 46 or 47 ? 8192 : 0)} a2=0 a3=0 a4=0 a5=0 rip=4194818 rsp=1879048176 flags=514 fs=0 gs=0 clock=10 valueBits={value} action=1 status=1 ordinal={ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={value}";
    private static string Message(uint ordinal, uint total, uint number = 47) => $"M16_013_MESSAGE identity=4294967297 ordinal={ordinal} nr={number} fd=6 generation=1 header=8192 flags=0 table=12288 vectors=2 total={total}";
    private static string Vector(uint ordinal, uint index, uint count) => $"M16_013_VECTOR identity=4294967297 ordinal={ordinal} index={index} address={16384 + index * 4096} bytes={count}";
}
