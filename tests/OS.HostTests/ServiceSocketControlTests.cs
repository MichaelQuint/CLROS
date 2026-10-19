using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Tools.QemuRunner;

internal static class ServiceSocketControlTests
{
    [HostTest("service controls drive two pending resets lost reopen healthy isolation and byte-exact resumed close over owned mock IO")]
    private static async Task Complete()
    {
        var (control, transport, _) = await Exercise("none");
        HostCheck.True(control.Schema =="clros.nativeaot.service.controls.v1" && control.FirstSynAckOrdinal <control.SecondSynAckOrdinal &&
            control.FirstProbeOrdinal <control.SecondProbeOrdinal && control.Persist.Port !=control.Healthy.Port && control.Observations.Count >20);
        HostCheck.True(transport.Session.Healthy && transport.Session.Stopped && transport.ControlledPorts.Count ==3 && transport.Session.DroppedFrames.Count ==4);
        HostCheck.True(transport.Injections.Count(row => !row.Forwarded) ==1 && transport.Injections.Single(row => !row.Forwarded).Tag =="persist.reopen.lost" &&
            transport.Injections.Any(row => row.Tag =="persist.reopen.fresh"));
        HostCheck.True(Convert.FromHexString(control.Persist.ResponseHex).AsSpan().EndsWith("CLROS_SERVICE_HEALTH_OK"u8));
        // This is an owned host transport simulation, not a native or guest capture.
    }

    [HostTest("service controls reject wrong pending ACK multi-byte persist probes and wrong healthy response")]
    private static async Task Refusals()
    {
        foreach (string mode in new[] { "syn-ack", "probe", "body" })
        {
            bool refused = false; try { await Exercise(mode); } catch (InvalidDataException) { refused = true; }
            HostCheck.True(refused);
        }
    }

    [HostTest("service controls cancellation joins the writer before port reservation disposal and prevents reuse")]
    private static async Task Cancellation()
    {
        using var frames = new WebFrameControl(TimeSpan.FromSeconds(10), continuingService: true);
        using var controls = new ServiceSocketControls(); using var cancel = new CancellationTokenSource();
        Task<ServiceSocketControls.Report> running = controls.RunAsync(frames, Path.GetFullPath("missing-service-serial-" +Guid.NewGuid()),
            Path.GetFullPath("missing-service-pcap-" +Guid.NewGuid()), cancel.Token);
        cancel.Cancel(); bool cancelled = false; try { await running; } catch (OperationCanceledException) { cancelled = true; }
        HostCheck.True(cancelled && running.IsCompleted && frames.Completion.IsCompletedSuccessfully);
        bool repeated = false; try { _ = controls.RunAsync(frames, "x", "y"); } catch (InvalidOperationException) { repeated = true; }
        HostCheck.True(repeated); controls.Dispose(); controls.Dispose();
    }

    private sealed class Flow(uint client, uint server, bool zero)
    {
        internal readonly uint Client = unchecked(client +1), Server = unchecked(server +1);
        internal readonly bool Zero = zero;
        internal uint Next = unchecked(client +1);
        internal byte[] Response = [];
        internal int Probes;
    }

    internal static async Task<(ServiceSocketControls.Report, WebFrameControl.ServiceReport, byte[])> Exercise(string mode)
    {
        string folder = Path.Combine(Path.GetTempPath(), "clros-service-controls-" +Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try { return await Run(folder, mode); }
        finally
        {
            File.Delete(Path.Combine(folder, "serial.log")); File.Delete(Path.Combine(folder, "wire.pcap")); Directory.Delete(folder);
        }
    }

    private static async Task<(ServiceSocketControls.Report, WebFrameControl.ServiceReport, byte[])> Run(string folder, string mode)
    {
        string serial = Path.Combine(folder, "serial.log"), capture = Path.Combine(folder, "wire.pcap");
        ServiceSocketControls.Report report;
        WebFrameControl.ServiceReport frameReport;
        {
        File.WriteAllText(serial, "M16_011_PEER mac=90525193470466 sent=1 received=1 start=1 end=2\r\nM16_011_SLOT event=0 synthetic=gate-only\r\n");
        using var output = new FileStream(capture, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        writer.Write(Convert.FromHexString("D4C3B2A10200040000000000000000000000010001000000")); writer.Flush();
        uint time = 0;
        foreach (byte[] raw in NativeAotSocketIntegrationTests.Packets().Frames.Take(8)) Record(raw);
        void Record(byte[] raw)
        {
            writer.Write(1U); writer.Write(time++ * 1000); writer.Write((uint)raw.Length); writer.Write((uint)raw.Length); writer.Write(raw); writer.Flush();
        }
        using var frames = new WebFrameControl(TimeSpan.FromSeconds(10), continuingService: true);
        using var controls = new ServiceSocketControls(); using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(7));
        using TcpClient backend = new(), guest = new();
        await backend.ConnectAsync(IPAddress.Loopback, frames.QemuOutPort); await guest.ConnectAsync(IPAddress.Loopback, frames.QemuInPort);
        Task<ServiceSocketControls.Report> execution = controls.RunAsync(frames, serial, capture, limit.Token);
        Task simulator = Simulate();
        try
        {
            report = await execution;
            while (frames.DroppedFrames.Count <4) await Task.Delay(1, limit.Token);
            frames.ExpectGuestExit();
            frameReport = frames.FinishService();
        }
        finally
        {
            limit.Cancel(); frames.Stop();
            try { await simulator.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (Exception error) when (error is OperationCanceledException or EndOfStreamException or IOException) { }
        }

        async Task Simulate()
        {
            uint syns = 0; bool healthyClosed = false; Dictionary<int, Flow> flows = [];
            void Reply(int port, Flow flow, uint sequence, byte flags, string payload = "", uint? ack = null) =>
                Record(ContinuingTcpScenario.Write(new(true, (ushort)port, sequence, ack ??flow.Next, flags, 8192, payload), 0x52550a000202));
            for (;;)
            {
                byte[] header = new byte[4]; await guest.GetStream().ReadExactlyAsync(header, limit.Token);
                int size = checked((int)BinaryPrimitives.ReadUInt32BigEndian(header)); byte[] raw = new byte[size];
                await guest.GetStream().ReadExactlyAsync(raw, limit.Token); Record(raw);
                int port = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(34)); uint sequence = BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(38));
                uint ack = BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(42)); byte flags = raw[47]; ushort window = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(48));
                int payloadBytes = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(16)) -40;
                if (flags ==2)
                {
                    var flow = new Flow(sequence, 10000 +(++syns *1000), window ==0); flows.Add(port, flow);
                    Reply(port, flow, flow.Server -1, 0x12, ack: flow.Client +(mode =="syn-ack" ? 1U : 0U));
                    byte[] reset = ContinuingTcpScenario.Write(new(false, (ushort)port, flow.Client, 0, 4), 0x52550a000202);
                    byte[] length = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(length, (uint)reset.Length);
                    await backend.GetStream().WriteAsync(length, limit.Token); await backend.GetStream().WriteAsync(reset, limit.Token); continue;
                }
                Flow current = flows[port];
                if (flags ==4) { HostCheck.True(sequence ==current.Client && ack ==0); flows.Remove(port); continue; }
                if (payloadBytes !=0)
                {
                    HostCheck.True(Encoding.ASCII.GetString(raw, 54, payloadBytes) =="GET /healthz HTTP/1.1\r\nHost: clros.service\r\nConnection: close\r\n\r\n");
                    current.Next = unchecked(sequence +(uint)payloadBytes);
                    string body = mode =="body" && !current.Zero ? "CLROS_SERVICE_HEALTH_NO" : "CLROS_SERVICE_HEALTH_OK";
                    current.Response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: " +body.Length.ToString(CultureInfo.InvariantCulture) +
                        "\r\nConnection: close\r\nServer: Kestrel\r\nDate: " +DateTimeOffset.UtcNow.ToString("r", CultureInfo.InvariantCulture) +"\r\n\r\n" +body);
                    if (current.Zero)
                    { Reply(port, current, current.Server, 0x18, Encoding.ASCII.GetString(current.Response, 0, mode =="probe" ? 2 : 1)); current.Probes = 1; }
                    else
                    { Reply(port, current, current.Server, 0x18, Encoding.ASCII.GetString(current.Response)); Reply(port, current, unchecked(current.Server +(uint)current.Response.Length), 0x11); }
                }
                else if (current.Zero && current.Probes ==1 && window ==0 && ack ==unchecked(current.Server +1))
                { Reply(port, current, unchecked(current.Server +1), 0x18, Encoding.ASCII.GetString(current.Response, 1, 1)); current.Probes = 2; }
                else if (current.Zero && current.Probes ==2 && window !=0 && ack ==unchecked(current.Server +2))
                {
                    HostCheck.True(healthyClosed); current.Probes = 3;
                    Reply(port, current, unchecked(current.Server +2), 0x18, Encoding.ASCII.GetString(current.Response, 2, current.Response.Length -2));
                    Reply(port, current, unchecked(current.Server +(uint)current.Response.Length), 0x11);
                }
                else if (flags ==0x11)
                {
                    Reply(port, current, unchecked(current.Server +(uint)current.Response.Length +1), 0x10, ack: unchecked(sequence +1));
                    if (!current.Zero) healthyClosed = true;
                }
            }
        }
        }
        // Joining above stops the producer; the using scope closes its writer
        // before opening the immutable completed capture for inspection.
        return (report, frameReport, File.ReadAllBytes(capture));
    }
}
