using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using OS.Tools.QemuRunner;

internal static class WebFrameControlTests
{
    private static byte[] Frame(ushort port, byte flags, ReadOnlySpan<byte> payload = default)
    {
        byte[] bytes = new byte[54 + payload.Length];
        Convert.FromHexString("02000000000102000000000208004500000000000000400600000A0002020A00020F").CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(16), (ushort)(40 + payload.Length));
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(34), port); BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(36), 8080);
        bytes[46] = 0x50; bytes[47] = flags; payload.CopyTo(bytes.AsSpan(54)); return bytes;
    }
    private static async Task Send(NetworkStream stream, byte[] frame)
    {
        byte[] header = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(header, (uint)frame.Length);
        await stream.WriteAsync(header); await stream.WriteAsync(frame);
    }
    private static async Task<byte[]> Read(NetworkStream stream)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        byte[] header = new byte[4]; await stream.ReadExactlyAsync(header, deadline.Token);
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(header)); HostCheck.True(count is >=14 and <=1518);
        byte[] frame = new byte[count]; await stream.ReadExactlyAsync(frame, deadline.Token); return frame;
    }

    [HostTest("web frame control preserves unrelated bytes handshake and data while withholding only the armed peer")]
    private static async Task Isolation()
    {
        using var gate = new WebFrameControl(TimeSpan.FromSeconds(10));
        using TcpClient source = new(); using TcpClient target = new();
        await source.ConnectAsync(IPAddress.Loopback, gate.QemuOutPort); await target.ConnectAsync(IPAddress.Loopback, gate.QemuInPort);
        var input = source.GetStream(); var output = target.GetStream();
        gate.SelectDeadlineClient(50000);
        byte[][] forwarded = [Frame(50000, 0x10), Frame(50000, 0x18, "GET /h"u8), Frame(50001, 0x10), Frame(50000, 2), Frame(50000, 0x18, "ealthz"u8)];
        for (int i=0;i<forwarded.Length;i++)
        {
            if(i==2) { await Send(input,Frame(50000,0x10)); await Send(input,Frame(50000,0x11)); await Send(input,Frame(50000,0x14)); }
            await Send(input,forwarded[i]); HostCheck.True((await Read(output)).AsSpan().SequenceEqual(forwarded[i]));
        }
        using(var wait=new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            while(gate.ForwardedFrames<5)await Task.Delay(1,wait.Token);
        HostCheck.Equal(3,gate.DroppedFrames.Count); HostCheck.Equal(5L,gate.ForwardedFrames);
        HostCheck.Equal(Convert.ToHexString(Frame(50000,0x10)),gate.DroppedFrames[0].Hex);
        HostCheck.True(gate.DroppedFrames.Select(f=>f.Ordinal).SequenceEqual(new long[]{3,4,5}));
        Task injected = gate.InjectRejectedFrameAsync(); byte[] vlan = await Read(output); await injected;
        HostCheck.Equal(1518,vlan.Length); HostCheck.True(vlan.AsSpan(0,18).SequenceEqual(Convert.FromHexString("020000000001020000000002810000010800")));
        HostCheck.True(vlan.AsSpan(18).IndexOfAnyExcept((byte)0)<0); HostCheck.Equal(1,gate.VlanFrames);
        bool repeat=false;try{gate.SelectDeadlineClient(50001);}catch(InvalidOperationException){repeat=true;}HostCheck.True(repeat);
        repeat=false;try{_ = gate.InjectRejectedFrameAsync();}catch(InvalidOperationException){repeat=true;}HostCheck.True(repeat);
        HostCheck.True(gate.Arguments[^1].Contains("netdev=n0,queue=tx,indev=clroswebin,outdev=clroswebout",StringComparison.Ordinal));
        HostCheck.True(gate.Stop()); HostCheck.True(gate.Completion.IsCompletedSuccessfully);
    }

    [HostTest("web frame control joins pending accepts and retains malformed input failure")]
    private static async Task FailureAndCleanup()
    {
        using(var unopened=new WebFrameControl(TimeSpan.FromSeconds(10)))HostCheck.True(unopened.Stop());
        using var gate=new WebFrameControl(TimeSpan.FromSeconds(10));
        using TcpClient source=new();using TcpClient target=new();
        await source.ConnectAsync(IPAddress.Loopback,gate.QemuOutPort);await target.ConnectAsync(IPAddress.Loopback,gate.QemuInPort);
        byte[] header=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(header,1515);await source.GetStream().WriteAsync(header);
        try{await gate.Completion.WaitAsync(TimeSpan.FromSeconds(3));}catch(InvalidDataException){}
        HostCheck.True(gate.Completion.IsFaulted);HostCheck.True(gate.Stop());
    }

    [HostTest("web frame control distinguishes expected child exit from early backend closure")]
    private static async Task ChildExit()
    {
        foreach(bool expected in new[]{false,true})
        {
            using var gate=new WebFrameControl(TimeSpan.FromSeconds(10));
            using TcpClient source=new();using TcpClient target=new();
            await source.ConnectAsync(IPAddress.Loopback,gate.QemuOutPort);await target.ConnectAsync(IPAddress.Loopback,gate.QemuInPort);
            if(expected)gate.ExpectGuestExit();source.Dispose();
            if(!expected)try{await gate.Completion.WaitAsync(TimeSpan.FromSeconds(3));}catch(EndOfStreamException){}
            var result=gate.Finish();HostCheck.True(result.Stopped);HostCheck.Equal(expected,result.Healthy);HostCheck.Equal(expected,result.GuestExitExpected);
        }
    }

    [HostTest("service frame owner isolates reserved raw tuples and serializes exact injections with one deliberate loss")]
    private static async Task ServiceInjections()
    {
        using var gate = new WebFrameControl(TimeSpan.FromSeconds(10), continuingService: true);
        gate.SelectServiceClients(50000, 50001, 50002);
        using TcpClient source = new(); using TcpClient target = new();
        await source.ConnectAsync(IPAddress.Loopback, gate.QemuOutPort); await target.ConnectAsync(IPAddress.Loopback, gate.QemuInPort);
        var input = source.GetStream(); var output = target.GetStream(); byte[] ordinary = Frame(50003, 0x10);
        await Send(input, ordinary); HostCheck.True((await Read(output)).AsSpan().SequenceEqual(ordinary));
        await Send(input, Frame(50000, 0x14));
        using (var limit = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            while (gate.DroppedFrames.Count !=1) await Task.Delay(1, limit.Token);
        byte[] injected = Frame(50001, 0x18, "A"u8);
        Task first = gate.InjectServiceFrameAsync(injected, "request"); injected[^1] = (byte)'B';
        HostCheck.True((await Read(output)).AsSpan().SequenceEqual(Frame(50001, 0x18, "A"u8))); await first;
        await gate.InjectServiceFrameAsync(Frame(50001, 0x10), "reopen.lost", forward: false);
        byte[] fresh = Frame(50001, 0x18, "F"u8); Task last = gate.InjectServiceFrameAsync(fresh, "reopen.fresh");
        HostCheck.True((await Read(output)).AsSpan().SequenceEqual(fresh)); await last;
        await Send(input, ordinary); HostCheck.True((await Read(output)).AsSpan().SequenceEqual(ordinary));
        var report = gate.FinishService();
        HostCheck.True(report.Session.Stopped && report.Session.Healthy && report.Session.VlanFrames ==0 && report.Session.DroppedFrames.Count ==1);
        HostCheck.True(report.ControlledPorts.SequenceEqual(new[] { 50000, 50001, 50002 }) && report.Injections.Count ==3 &&
            report.Injections.All(row => row.AfterBackendOrdinal ==2) && !report.Injections[1].Forwarded && report.Injections[2].Forwarded);
        HostCheck.Equal(Convert.ToHexString(Frame(50001, 0x18, "A"u8)), report.Injections[0].Hex);
    }

    [HostTest("service frame owner rejects mixed modes unselected and overlapping injections and joins cancellation")]
    private static async Task ServiceGuards()
    {
        void Refuse(Action action) { bool refused = false; try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { refused = true; } HostCheck.True(refused); }
        using (var legacy = new WebFrameControl(TimeSpan.FromSeconds(10)))
            Refuse(() => legacy.InjectServiceFrameAsync(Frame(50000, 0x10), "x"));
        Refuse(() => { using var invalid = new WebFrameControl(TimeSpan.FromSeconds(901), continuingService: true); });
        using (var maximum = new WebFrameControl(TimeSpan.FromSeconds(900), continuingService: true)) HostCheck.True(maximum.Stop());
        using var gate = new WebFrameControl(TimeSpan.FromSeconds(10), continuingService: true);
        Refuse(() => gate.InjectServiceFrameAsync(Frame(50000, 0x10), "x"));
        Refuse(() => gate.SelectServiceClients(50000, 50000, 50002)); gate.SelectServiceClients(50000, 50001, 50002);
        Refuse(() => gate.SelectServiceClients(50003, 50004, 50005)); Refuse(() => gate.SelectDeadlineClient(50000)); Refuse(() => gate.InjectRejectedFrameAsync());
        Refuse(() => gate.InjectServiceFrameAsync(Frame(50003, 0x10), "x")); Refuse(() => gate.InjectServiceFrameAsync(Frame(50000, 0x10), "bad tag"));
        Task pending = gate.InjectServiceFrameAsync(Frame(50000, 0x10), "pending");
        Refuse(() => gate.InjectServiceFrameAsync(Frame(50001, 0x10), "overlap")); HostCheck.True(gate.Stop());
        bool cancelled = false; try { await pending; } catch (OperationCanceledException) { cancelled = true; }
        HostCheck.True(cancelled && gate.Completion.IsCompletedSuccessfully); Refuse(() => gate.InjectServiceFrameAsync(Frame(50000, 0x10), "retired"));
    }
}
