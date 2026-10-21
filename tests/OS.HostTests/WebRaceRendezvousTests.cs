using System.IO.Pipes;
using System.Text;
using OS.Tools.QemuRunner;

internal static class WebRaceRendezvousTests
{
    private const int Port = 20080, Pid = 1234;
    private static readonly WebRaceRendezvous.Digests Hashes = new(new('A', 64), new('B', 64), new('C', 64));

    [HostTest("web race rendezvous holds actual runner pipe until ordered producer completion")]
    private static async Task OrderedCompletion()
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var producer = WebRaceRendezvous.CreateProducer();
        Task<WebRaceRendezvous.Ready> ready = producer.WaitForReadyAsync(Port, limit.Token);
        Task<WebRaceRendezvous.Report> runner = WebRaceRendezvous.WaitInRunnerAsync(producer.PipeName, Port, Pid, TimeSpan.FromSeconds(5), cancellation: limit.Token);
        try
        {
            HostCheck.Equal(new WebRaceRendezvous.Ready(Port, Pid), await ready);
            HostCheck.True(!runner.IsCompleted);
            await Reject(() => producer.WaitForReadyAsync(Port, limit.Token));
            await Reject(() => producer.CompleteAsync(Hashes with { WireSha256 = new('c', 64) }, limit.Token));
            HostCheck.True(!runner.IsCompleted);
            await producer.CompleteAsync(Hashes, limit.Token);
            HostCheck.Equal(new WebRaceRendezvous.Report(Port, Pid, Hashes), await runner);
            await Reject(() => producer.CompleteAsync(Hashes, limit.Token));
        }
        finally { limit.Cancel(); await Join(runner); await Join(ready); }
    }

    [HostTest("web race rendezvous validates strict names bounds and handshake order before IPC")]
    private static async Task Inputs()
    {
        foreach (string name in new[] { "", "clros-web-race-", "clros-web-race-" + new string('A', 32), "clros-web-race-" + new string('a', 33), "../clros-web-race-" + new string('a', 32), null! })
            await Reject(() => { WebRaceRendezvous.ValidatePipeName(name); return Task.CompletedTask; });
        string valid = Name(); WebRaceRendezvous.ValidatePipeName(valid);
        foreach (var input in new[] { (0, Pid, TimeSpan.FromSeconds(1)), (Port, 0, TimeSpan.FromSeconds(1)), (Port, Pid, TimeSpan.Zero), (Port, Pid, TimeSpan.FromSeconds(46)) })
            await Reject(() => WebRaceRendezvous.WaitInRunnerAsync(valid, input.Item1, input.Item2, input.Item3));
        await using var producer = WebRaceRendezvous.CreateProducer();
        await Reject(() => producer.CompleteAsync(Hashes, CancellationToken.None));
    }

    [HostTest("web race rendezvous runner refuses malformed bounded digest frames and EOF")]
    private static async Task DoneFrames()
    {
        string good = "DONE_V1 " + new string('A', 64) + " " + new string('B', 64) + " " + new string('C', 64) + "\r\n";
        foreach (string frame in new[] { "", good.Replace("DONE_V1", "DONE_V2"), good.Replace(new string('A', 64), new string('a', 64)), good.Replace(" ", "  "), good.Replace("\r\n", "\n"), new string('X', 512), "DONE_V1\0\r\n" })
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            string name = Name(); using var server = Server(name);
            Task<WebRaceRendezvous.Report> runner = WebRaceRendezvous.WaitInRunnerAsync(name, Port, Pid, TimeSpan.FromSeconds(5), cancellation: limit.Token);
            try
            {
                await server.WaitForConnectionAsync(limit.Token);
                HostCheck.Equal($"READY_V1 {Port} {Pid}\r\n", await Line(server, limit.Token));
                if (frame.Length != 0) await server.WriteAsync(Encoding.ASCII.GetBytes(frame), limit.Token);
                else server.Dispose();
                await Reject(() => runner); HostCheck.True(runner.IsCompleted);
            }
            finally { limit.Cancel(); server.Dispose(); await Join(runner); }
        }
    }

    [HostTest("web race rendezvous producer refuses wrong port noncanonical READY overlong and EOF")]
    private static async Task ReadyFrames()
    {
        foreach (string frame in new[] { "", "READY_V1 20081 1234\r\n", "READY_V1 020080 1234\r\n", "READY_V1 20080 0\r\n", "READY_V2 20080 1234\r\n", "READY_V1 20080 1234\n", new string('R', 512) })
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var producer = WebRaceRendezvous.CreateProducer();
            Task<WebRaceRendezvous.Ready> waiting = producer.WaitForReadyAsync(Port, limit.Token);
            using var client = new NamedPipeClientStream(".", producer.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await client.ConnectAsync(limit.Token);
                if (frame.Length != 0) await client.WriteAsync(Encoding.ASCII.GetBytes(frame), limit.Token);
                else client.Dispose();
                await Reject(() => waiting); HostCheck.True(waiting.IsCompleted);
            }
            finally { limit.Cancel(); client.Dispose(); await Join(waiting); }
        }
    }

    [HostTest("web race rendezvous cancellation and disposal join pending accept connect and read")]
    private static async Task Cancellation()
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var producer = WebRaceRendezvous.CreateProducer())
        {
            Task<WebRaceRendezvous.Ready> accept = producer.WaitForReadyAsync(Port, limit.Token);
            await producer.DisposeAsync(); await Reject(() => accept); HostCheck.True(accept.IsCompleted);
        }
        using (var cancelled = new CancellationTokenSource())
        {
            Task<WebRaceRendezvous.Report> connect = WebRaceRendezvous.WaitInRunnerAsync(Name(), Port, Pid, TimeSpan.FromSeconds(5), cancellation: cancelled.Token);
            cancelled.Cancel(); await Reject(() => connect); HostCheck.True(connect.IsCompleted);
        }
        await using (var producer = WebRaceRendezvous.CreateProducer())
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(limit.Token);
            Task<WebRaceRendezvous.Ready> ready = producer.WaitForReadyAsync(Port, limit.Token);
            Task<WebRaceRendezvous.Report> read = WebRaceRendezvous.WaitInRunnerAsync(producer.PipeName, Port, Pid, TimeSpan.FromSeconds(5), cancellation: cancel.Token);
            try { _ = await ready; cancel.Cancel(); await Reject(() => read); HostCheck.True(read.IsCompleted); }
            finally { cancel.Cancel(); await Join(read); await Join(ready); }
        }
    }

    [HostTest("web race rendezvous health failure closes actual pipe and preserves primary exception")]
    private static async Task HealthAbort()
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string name = Name(); using var server = Server(name); bool fail = false;
        var original = new InvalidOperationException("independent health refusal");
        Task<WebRaceRendezvous.Report> runner = WebRaceRendezvous.WaitInRunnerAsync(name, Port, Pid, TimeSpan.FromSeconds(5),
            () => Volatile.Read(ref fail) ? throw original : true, limit.Token);
        try
        {
            await server.WaitForConnectionAsync(limit.Token); _ = await Line(server, limit.Token); Volatile.Write(ref fail, true);
            Exception? observed = null; try { _ = await runner; } catch (Exception error) { observed = error; }
            HostCheck.True(ReferenceEquals(original, observed) && runner.IsCompleted);
            try { HostCheck.Equal(0, await server.ReadAsync(new byte[1], limit.Token)); } catch (IOException) { }
        }
        finally { limit.Cancel(); server.Dispose(); await Join(runner); }
    }

    private static string Name() => "clros-web-race-" + Guid.NewGuid().ToString("N");
    private static NamedPipeServerStream Server(string name) => new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.FirstPipeInstance, 512, 512);
    private static async Task<string> Line(Stream stream, CancellationToken token)
    {
        var bytes = new List<byte>(); byte[] one = new byte[1];
        while (bytes.Count < 512)
        {
            HostCheck.Equal(1, await stream.ReadAsync(one, token)); bytes.Add(one[0]);
            if (one[0] == (byte)'\n') return Encoding.ASCII.GetString(bytes.ToArray());
        }
        throw new InvalidDataException("Independent test frame bound.");
    }
    private static async Task Reject(Func<Task> action)
    {
        bool rejected = false;
        try { await action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or InvalidDataException or IOException or OperationCanceledException) { rejected = true; }
        HostCheck.True(rejected);
    }
    private static async Task Join(Task task) { try { await task.WaitAsync(TimeSpan.FromSeconds(10)); } catch when (task.IsCompleted) { } }
}
