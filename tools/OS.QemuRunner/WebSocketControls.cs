using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OS.Tools.QemuRunner;

/// <summary>Finite external HTTP controls against the unchanged guest Kestrel.
/// One peer's return ACKs are withheld; another completes health before VLAN
/// injection and again on the same keep-alive connection after actual timeout
/// and drop rows. Owns both clients and its incremental serial reader until
/// return. Results require independent packet/runtime/native joins; no guest
/// acceptance is granted by this host orchestrator.</summary>
public static class WebSocketControls
{
    public readonly record struct Exchange(int SourcePort, DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc,
        long ElapsedMilliseconds, DateTimeOffset DateUtc, string RequestHex, string ResponseHex);
    public readonly record struct Events(string TimeoutRow, string DropRow, ulong TimeoutClock, ulong DropClock);
    public sealed record Report(Exchange Deadline, Exchange Before, Exchange After, Events Observations);

    /// <summary>Run only against the owned IPv4 loopback forward. A 45..120s
    /// budget includes all connects, HTTP and observed kernel events. Cancellation
    /// closes both clients; the caller must stop/join frames separately. Existing
    /// serial bytes are skipped, and truncation or unsupported responses throw.</summary>
    public static async Task<Report> RunAsync(int forwardPort, WebFrameControl frames, string serialPath,
        TimeSpan maximumDuration, CancellationToken cancellationToken = default)
    {
        if (forwardPort is <1 or >65535 || maximumDuration < TimeSpan.FromSeconds(45) || maximumDuration > TimeSpan.FromSeconds(120))
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(maximumDuration); CancellationToken token = lifetime.Token;
        using SerialEvents serial = new(serialPath);
        using TcpClient expired = NewClient(); using TcpClient healthy = NewClient();
        int badPort = ((IPEndPoint)expired.Client.LocalEndPoint!).Port;
        int goodPort = ((IPEndPoint)healthy.Client.LocalEndPoint!).Port;
        frames.SelectDeadlineClient(badPort);
        async Task<Exchange> Request(TcpClient client, int port, bool connect, bool keepAlive)
        {
            DateTimeOffset started = DateTimeOffset.UtcNow; Stopwatch timer = Stopwatch.StartNew();
            if (connect) await client.ConnectAsync(IPAddress.Loopback, forwardPort, token).ConfigureAwait(false);
            byte[] request = RequestBytes(forwardPort, keepAlive);
            await client.GetStream().WriteAsync(request, token).ConfigureAwait(false);
            // The expired peer intentionally cannot reach EOF: its ACKs never
            // drain the guest response. Content-Length still gives exact bytes.
            byte[] response = await ReadHealthResponseAsync(client.GetStream(), !keepAlive && client != expired, token).ConfigureAwait(false);
            timer.Stop(); DateTimeOffset completed = DateTimeOffset.UtcNow, date = ResponseDate(response);
            int headerEnd=response.AsSpan().IndexOf("\r\n\r\n"u8);
            if(Headers(response.AsSpan(0,headerEnd)).ContainsKey("Connection")==keepAlive)
                throw new InvalidDataException("Control connection lifetime.");
            if (date < started.AddSeconds(-5) || date > completed.AddSeconds(5)) throw new InvalidDataException("Control Date outside UTC interval.");
            return new(port, started, completed, timer.ElapsedMilliseconds, date, Convert.ToHexString(request), Convert.ToHexString(response));
        }
        Exchange bad = await Request(expired, badPort, connect:true, keepAlive:false).ConfigureAwait(false);
        Exchange before = await Request(healthy, goodPort, connect:true, keepAlive:true).ConfigureAwait(false);
        serial.Poll();
        if (serial.Rows.Any(row=>row.StartsWith("M07_027_TIMEOUT ",StringComparison.Ordinal)))
            throw new InvalidDataException("Timeout preceded healthy control.");
        await frames.InjectRejectedFrameAsync().WaitAsync(token).ConfigureAwait(false);
        Events observed;
        for (;;)
        {
            if (frames.Completion.IsCompleted) throw new InvalidDataException("Frame owner ended during HTTP controls.");
            serial.Poll(); Events? sample = ObserveEvents(serial.Rows, badPort);
            if (sample.HasValue) { observed = sample.Value; break; }
            await Task.Delay(25, token).ConfigureAwait(false);
        }
        Exchange after = await Request(healthy, goodPort, connect:false, keepAlive:false).ConfigureAwait(false);
        if (after.DateUtc <= before.DateUtc || frames.DroppedFrames.Count == 0 || frames.VlanFrames != 1)
            throw new InvalidDataException("Control progress incomplete.");
        return new(bad, before, after, observed);
    }

    private static TcpClient NewClient()
    {
        TcpClient client = new(AddressFamily.InterNetwork);
        try { client.NoDelay = true; client.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0)); return client; }
        catch { client.Dispose(); throw; }
    }

    /// <summary>Owned exact health request bytes. Keep-alive is used only for
    /// the first request on the retained healthy control connection.</summary>
    public static byte[] RequestBytes(int forwardPort, bool keepAlive)
    {
        if (forwardPort is <1 or >65535) throw new ArgumentOutOfRangeException(nameof(forwardPort));
        return Encoding.ASCII.GetBytes("GET /healthz HTTP/1."+(keepAlive?"1":"0")+"\r\nHost: 127.0.0.1:"+
            forwardPort.ToString(CultureInfo.InvariantCulture)+"\r\nConnection: "+(keepAlive?"keep-alive":"close")+"\r\nAccept: */*\r\n\r\n");
    }

    /// <summary>Read exactly one bounded Content-Length health response from a
    /// borrowed stream, optionally requiring EOF. No read may consume bytes from
    /// a later response. The caller must provide cancellation and retain stream
    /// ownership. Malformed/extra/truncated bytes throw; no source authority.</summary>
    public static async Task<byte[]> ReadHealthResponseAsync(Stream stream, bool requireEof, CancellationToken token)
    {
        byte[] bytes = new byte[8192]; int count = 0, extent = -1;
        while (extent < 0 || count < extent)
        {
            // Read the header byte-wise so a keep-alive response cannot consume
            // a subsequent response. Only its declared17 body bytes follow.
            int next = extent < 0 ? 1 : extent - count;
            if (extent < 0 && count >= 2048) throw new InvalidDataException("Control header too large.");
            if (count == bytes.Length) throw new InvalidDataException("Control response too large.");
            int read = await stream.ReadAsync(bytes.AsMemory(count, Math.Min(next, bytes.Length-count)),token).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("Truncated control response."); count += read;
            if (extent < 0 && count >= 4 && bytes.AsSpan(count-4,4).SequenceEqual("\r\n\r\n"u8))
            {
                if (count > 2048) throw new InvalidDataException("Control header too large.");
                var headers = Headers(bytes.AsSpan(0,count-4));
                if (headers.GetValueOrDefault("Content-Length") != "17") throw new InvalidDataException("Control content length.");
                extent = count + 17;
            }
        }
        int body = count-17;
        if (!bytes.AsSpan(body,17).SequenceEqual("OS_MINIMAL_API_OK"u8)) throw new InvalidDataException("Control health body.");
        if (requireEof && await stream.ReadAsync(new byte[1],token).ConfigureAwait(false) != 0) throw new InvalidDataException("Extra control bytes.");
        return bytes[..count];
    }

    private static Dictionary<string,string> Headers(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes) if (value is <32 or >126 && value is not (10 or 13)) throw new InvalidDataException("Control header encoding.");
        string[] lines = Encoding.ASCII.GetString(bytes).Split("\r\n",StringSplitOptions.None);
        if (lines[0] != "HTTP/1.1 200 OK") throw new InvalidDataException("Control status.");
        Dictionary<string,string> headers = new(StringComparer.Ordinal);
        foreach(string line in lines.Skip(1))
        {
            int colon=line.IndexOf(": ",StringComparison.Ordinal);
            if(colon<1 || !headers.TryAdd(line[..colon],line[(colon+2)..]))throw new InvalidDataException("Control duplicate/malformed header.");
        }
        bool close=headers.GetValueOrDefault("Connection")=="close";
        if(headers.Count!=(close?5:4) || headers.Keys.Any(key=>key is not("Date" or "Server" or "Content-Type" or "Content-Length" or "Connection")) ||
            headers.GetValueOrDefault("Server")!="Kestrel" || headers.GetValueOrDefault("Content-Type")!="text/plain" ||
            (!close && headers.ContainsKey("Connection")))throw new InvalidDataException("Control headers.");
        string? text=headers.GetValueOrDefault("Date");
        if(!DateTimeOffset.TryParseExact(text,"r",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var date) ||
            text!=date.ToString("r",CultureInfo.InvariantCulture))throw new InvalidDataException("Control Date.");
        return headers;
    }

    private static DateTimeOffset ResponseDate(byte[] response)
    {
        int end=response.AsSpan().IndexOf("\r\n\r\n"u8);
        return DateTimeOffset.ParseExact(Headers(response.AsSpan(0,end))["Date"],"r",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal);
    }

    /// <summary>Value-only exact event decoding. Missing events return null;
    /// malformed, duplicate, foreign-tuple or nonisolated events throw. Caller
    /// strings alone prove no actual device/clock/transport observation.</summary>
    public static Events? ObserveEvents(IReadOnlyList<string> rows,int badPort)
    {
        if(badPort is <1024 or >65535 || rows.Count>2)throw new InvalidDataException("Control event extent.");
        string? timeout=null,drop=null;ulong timeoutClock=0,dropClock=0;
        foreach(string row in rows)
        {
            bool isTimeout=row.StartsWith("M07_027_TIMEOUT ",StringComparison.Ordinal);
            string[] names=(isTimeout?"slot owner epoch generation index local localPort remote remotePort clock sharedFailed":
                "before after availableBefore availableAfter delivered clock running").Split(' ');
            string[] words=row.Split(' ');
            if(words.Length!=names.Length+1 || (!isTimeout && words[0]!="M07_028_DROP"))throw new InvalidDataException("Control event fields.");
            Dictionary<string,ulong> values=[];
            for(int i=0;i<names.Length;i++)
            {
                string prefix=names[i]+"=";string number=words[i+1].StartsWith(prefix,StringComparison.Ordinal)?words[i+1][prefix.Length..]:"";
                if(number.Length==0 || number.Length>1 && number[0]=='0' || !ulong.TryParse(number,NumberStyles.None,CultureInfo.InvariantCulture,out ulong value))
                    throw new InvalidDataException("Control event number.");
                values.Add(names[i],value);
            }
            ulong clock=values["clock"];
            if(clock==0 || clock>long.MaxValue)throw new InvalidDataException("Control event clock.");
            if(isTimeout)
            {
                if(timeout is not null || values["slot"]>=8 || values["index"]>=8 || values["owner"]==0 || values["epoch"]==0 || values["generation"]==0 ||
                    values["local"]!=167772687 || values["localPort"]!=8080 || values["remote"]!=167772674 || values["remotePort"]!=(ulong)badPort ||
                    values["sharedFailed"]!=0)throw new InvalidDataException("Control timeout owner.");
                timeout=row;timeoutClock=clock;
            }
            else
            {
                if(drop is not null || values["before"]!=0 || values["after"]!=1 || values["availableBefore"]>65535 ||
                    values["delivered"]>1 || values["availableAfter"]!=((values["availableBefore"]+1+values["delivered"])&65535) || values["running"]!=1)
                    throw new InvalidDataException("Control device drop.");
                drop=row;dropClock=clock;
            }
        }
        if(timeout is null || drop is null)return null;
        if(dropClock>=timeoutClock)throw new InvalidDataException("Control event order.");
        return new(timeout,drop,timeoutClock,dropClock);
    }

    private sealed class SerialEvents : IDisposable
    {
        private readonly FileStream file;
        private readonly StringBuilder fragment=new();
        private long read;
        internal readonly List<string> Rows=[];
        internal SerialEvents(string path){file=new(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);file.Position=file.Length;}
        internal void Poll()
        {
            if(file.Length<file.Position)throw new InvalidDataException("Control serial truncated.");
            byte[] bytes=new byte[4096];int count;
            while((count=file.Read(bytes))!=0)
            {
                if((read+=count)>8*1024*1024)throw new InvalidDataException("Control serial budget.");
                for(int i=0;i<count;i++)
                {
                    if(bytes[i]==10)
                    {
                        string line=fragment.ToString().TrimEnd('\r');fragment.Clear();
                        if(line.StartsWith("M07_027_TIMEOUT ",StringComparison.Ordinal) || line.StartsWith("M07_028_DROP ",StringComparison.Ordinal))Rows.Add(line);
                        if(Rows.Count>2)throw new InvalidDataException("Extra control events.");
                    }
                    else {fragment.Append((char)bytes[i]);if(fragment.Length>65536)throw new InvalidDataException("Control serial line budget.");}
                }
            }
        }
        public void Dispose()=>file.Dispose();
    }
}
