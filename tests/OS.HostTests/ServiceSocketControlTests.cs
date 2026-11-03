using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Tools.QemuRunner;

internal static partial class ServiceSocketControlTests
{
    [HostTest("service controls drive pending resets fd-less unread reset lost reopen and healthy isolation over owned mock IO")]
    private static async Task Complete()
    {
        var (control, transport, _) = await Exercise("none");
        HostCheck.True(control.Schema == "clros.nativeaot.service.controls.v2" && control.FirstSynAckOrdinal < control.SecondSynAckOrdinal && control.FirstProbeOrdinal < control.SecondProbeOrdinal && control.Persist.Port != control.Healthy.Port && control.Observations.Count > 20);
        HostCheck.True(transport.Session.Healthy && transport.Session.Stopped && transport.ControlledPorts.Count == 3 && transport.Session.DroppedFrames.Count == 5);
        HostCheck.True(transport.Injections.Count(row => !row.Forwarded) == 1 && transport.Injections.Single(row => !row.Forwarded).Tag == "persist.reopen.lost" && transport.Injections.Any(row => row.Tag == "persist.reopen.fresh"));
        HostCheck.True(control.Unread.Http.Port == control.PendingPort && control.SecondSynAckOrdinal < control.Unread.LateOrdinal && control.Unread.LateOrdinal < control.Unread.ResetOrdinal && control.Unread.ResetOrdinal < control.FirstProbeOrdinal && control.Unread.Http.CompletedUtc <= control.Persist.StartedUtc && control.Unread.Http.CompletedUtc <= control.Healthy.StartedUtc && control.Unread.CloseRow.StartsWith("M07_034_CLOSE slot=0 owner=4096 epoch=1 generation=3 index=0 ", StringComparison.Ordinal) && control.Unread.CloseRow.EndsWith(" unread=0", StringComparison.Ordinal));
        HostCheck.True(Encoding.ASCII.GetString(Convert.FromHexString(control.Unread.Http.RequestHex)) == "GET /healthz HTTP/1.1\r\nHost: clros.service\r\nConnection: close\r\n\r\n");
        byte[] late = Convert.FromHexString(transport.Injections.Single(row => row.Tag == "unread.late").Hex);
        HostCheck.True(late.AsSpan(54).SequenceEqual("CLROS_UNREAD_LATE"u8));
        HostCheck.True(Convert.FromHexString(control.Persist.ResponseHex).AsSpan().EndsWith("CLROS_SERVICE_HEALTH_OK"u8));
        // This is an owned host transport simulation, not a native or guest capture.
    }

    [HostTest("service controls reject wrong pending ACK reset sequence reset ACK duplicate close multi-byte probes and wrong response")]
    private static async Task Refusals()
    {
        foreach (string mode in new[]
        {
            "syn-ack",
            "probe",
            "body",
            "reset-sequence",
            "reset-ack",
            "close-duplicate"
        }

        )
        {
            bool refused = false;
            try
            {
                await Exercise(mode);
            }
            catch (InvalidDataException)
            {
                refused = true;
            }

            HostCheck.True(refused);
        }
    }

    [HostTest("service controls await the actual last-close record and cancel when it never arrives")]
    private static async Task MissingClose()
    {
        bool cancelled = false;
        try
        {
            await Exercise("close-missing");
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        HostCheck.True(cancelled);
    }

    [HostTest("service controls cancellation joins the writer before port reservation disposal and prevents reuse")]
    private static async Task Cancellation()
    {
        using var frames = new WebFrameControl(TimeSpan.FromSeconds(10), continuingService: true);
        using var controls = new ServiceSocketControls();
        using var cancel = new CancellationTokenSource();
        Task<ServiceSocketControls.Report> running = controls.RunAsync(frames, Path.GetFullPath("missing-service-serial-" + Guid.NewGuid()), Path.GetFullPath("missing-service-pcap-" + Guid.NewGuid()), cancel.Token);
        cancel.Cancel();
        bool cancelled = false;
        try
        {
            await running;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        HostCheck.True(cancelled && running.IsCompleted && frames.Completion.IsCompletedSuccessfully);
        bool repeated = false;
        try
        {
            _ = controls.RunAsync(frames, "x", "y");
        }
        catch (InvalidOperationException)
        {
            repeated = true;
        }

        HostCheck.True(repeated);
        controls.Dispose();
        controls.Dispose();
    }

    internal static async Task<(ServiceSocketControls.Report, WebFrameControl.ServiceReport, byte[])> Exercise(string mode)
    {
        string folder = Path.Combine(Path.GetTempPath(), "clros-service-controls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            return await Run(folder, mode);
        }
        finally
        {
            File.Delete(Path.Combine(folder, "serial.log"));
            File.Delete(Path.Combine(folder, "wire.pcap"));
            Directory.Delete(folder);
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
            writer.Write(Convert.FromHexString("D4C3B2A10200040000000000000000000000010001000000"));
            writer.Flush();
            uint time = 0;
            foreach (byte[] raw in NativeAotSocketIntegrationTests.Packets().Frames.Take(8))
                Record(raw);
            void Record(byte[] raw)
            {
                writer.Write(1U);
                writer.Write(time++ * 1000);
                writer.Write((uint)raw.Length);
                writer.Write((uint)raw.Length);
                writer.Write(raw);
                writer.Flush();
            }

            using var frames = new WebFrameControl(TimeSpan.FromSeconds(10), continuingService: true);
            using var controls = new ServiceSocketControls();
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(mode == "close-missing" ? 1 : 7));
            using TcpClient backend = new(), guest = new();
            await backend.ConnectAsync(IPAddress.Loopback, frames.QemuOutPort);
            await guest.ConnectAsync(IPAddress.Loopback, frames.QemuInPort);
            Task<ServiceSocketControls.Report> execution = controls.RunAsync(frames, serial, capture, limit.Token);
            Task simulator = Simulate();
            try
            {
                report = await execution;
                while (frames.DroppedFrames.Count < 5)
                    await Task.Delay(1, limit.Token);
                frames.ExpectGuestExit();
                frameReport = frames.FinishService();
            }
            finally
            {
                limit.Cancel();
                frames.Stop();
                try
                {
                    await simulator.WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch (Exception error) when (error is OperationCanceledException or EndOfStreamException or IOException)
                {
                }
            }

            async Task Simulate()
            {
                uint syns = 0;
                bool healthyClosed = false;
                Dictionary<int, Flow> flows = [];
                void Reply(int port, Flow flow, uint sequence, byte flags, string payload = "", uint? ack = null) => Record(ContinuingTcpScenario.Write(new(true, (ushort)port, sequence, ack ?? flow.Next, flags, 8192, payload), 0x52550a000202));
                for (; ; )
                {
                    byte[] header = new byte[4];
                    await guest.GetStream().ReadExactlyAsync(header, limit.Token);
                    int size = checked((int)BinaryPrimitives.ReadUInt32BigEndian(header));
                    byte[] raw = new byte[size];
                    await guest.GetStream().ReadExactlyAsync(raw, limit.Token);
                    Record(raw);
                    int port = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(34));
                    uint sequence = BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(38));
                    uint ack = BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(42));
                    byte flags = raw[47];
                    ushort window = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(48));
                    int payloadBytes = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(16)) - 40;
                    if (flags == 2)
                    {
                        var flow = new Flow(sequence, 10000 + (++syns * 1000), window == 0);
                        flows.Add(port, flow);
                        Reply(port, flow, flow.Server - 1, 0x12, ack: flow.Client + (mode == "syn-ack" ? 1U : 0U));
                        byte[] reset = ContinuingTcpScenario.Write(new(false, (ushort)port, flow.Client, 0, 4), 0x52550a000202);
                        byte[] length = new byte[4];
                        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)reset.Length);
                        await backend.GetStream().WriteAsync(length, limit.Token);
                        await backend.GetStream().WriteAsync(reset, limit.Token);
                        continue;
                    }

                    Flow current = flows[port];
                    if (flags == 4)
                    {
                        HostCheck.True(sequence == current.Client && ack == 0);
                        flows.Remove(port);
                        continue;
                    }

                    if (payloadBytes != 0)
                    {
                        string payload = Encoding.ASCII.GetString(raw, 54, payloadBytes);
                        if (current.Unread && payload == "CLROS_UNREAD_LATE")
                        {
                            uint serverEnd = unchecked(current.Server + (uint)current.Response.Length + 1);
                            HostCheck.True(sequence == current.Next && ack == serverEnd && flags == 0x18 && window == 8192);
                            current.Next = unchecked(current.Next + (uint)payloadBytes);
                            Reply(port, current, unchecked(serverEnd + (mode == "reset-sequence" ? 1U : 0U)), 0x14, ack: unchecked(current.Next + (mode == "reset-ack" ? 1U : 0U)));
                            flows.Remove(port);
                            continue;
                        }

                        HostCheck.True(payload == "GET /healthz HTTP/1.1\r\nHost: clros.service\r\nConnection: close\r\n\r\n");
                        current.Next = unchecked(sequence + (uint)payloadBytes);
                        string body = mode == "body" && !current.Zero ? "CLROS_SERVICE_HEALTH_NO" : "CLROS_SERVICE_HEALTH_OK";
                        current.Response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\nConnection: close\r\nServer: Kestrel\r\nDate: " + DateTimeOffset.UtcNow.ToString("r", CultureInfo.InvariantCulture) + "\r\n\r\n" + body);
                        if (current.Zero)
                        {
                            Reply(port, current, current.Server, 0x18, Encoding.ASCII.GetString(current.Response, 0, mode == "probe" ? 2 : 1));
                            current.Probes = 1;
                        }
                        else
                        {
                            Reply(port, current, current.Server, 0x18, Encoding.ASCII.GetString(current.Response));
                            Reply(port, current, unchecked(current.Server + (uint)current.Response.Length), 0x11);
                            if (current.Unread && mode != "close-missing")
                            {
                                string row = $"M07_034_CLOSE slot=0 owner=4096 epoch=1 generation=3 index=0 local=167772687 localPort=8080 remote=167772674 remotePort={port} clock=1000000000 localNext={unchecked(current.Server + (uint)current.Response.Length + 1)} remoteNext={current.Next} unread=0\r\n";
                                File.AppendAllText(serial, row);
                                if (mode == "close-duplicate")
                                    File.AppendAllText(serial, row);
                            }
                        }
                    }
                    else if (current.Zero && current.Probes == 1 && window == 0 && ack == unchecked(current.Server + 1))
                    {
                        Reply(port, current, unchecked(current.Server + 1), 0x18, Encoding.ASCII.GetString(current.Response, 1, 1));
                        current.Probes = 2;
                        if (mode == "repeat-persist-probe")
                            Reply(port, current, unchecked(current.Server + 1), 0x18, Encoding.ASCII.GetString(current.Response, 1, 1));
                    }
                    else if (current.Zero && current.Probes == 2 && window != 0 && ack == unchecked(current.Server + 2))
                    {
                        HostCheck.True(healthyClosed);
                        current.Probes = 3;
                        if (mode == "duplicate-reopen-ack")
                        {
                            Reply(port, current, unchecked(current.Server + 2), 0x10);
                            // ReceiveFin ignores pure ACKs. Replaying the already
                            // probed prefix also exercises the byte-identical ACK.
                            Reply(port, current, current.Server, 0x18, Encoding.ASCII.GetString(current.Response, 0, 2));
                        }

                        Reply(port, current, unchecked(current.Server + 2), 0x18, Encoding.ASCII.GetString(current.Response, 2, current.Response.Length - 2));
                        Reply(port, current, unchecked(current.Server + (uint)current.Response.Length), 0x11);
                    }
                    else if (flags == 0x11)
                    {
                        HostCheck.True(!current.Unread);
                        Reply(port, current, unchecked(current.Server + (uint)current.Response.Length + 1), 0x10, ack: unchecked(sequence + 1));
                        if (!current.Zero)
                            healthyClosed = true;
                    }
                }
            }
        }

        // Joining above stops the producer; the using scope closes its writer
        // before opening the immutable completed capture for inspection.
        return (report, frameReport, File.ReadAllBytes(capture));
    }
}
