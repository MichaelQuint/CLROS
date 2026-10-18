using System.Globalization;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Structurally checked external controls from one runner summary.
    /// These borrowed values require independent original serial, packet,
    /// runtime, process and source joins before they qualify a guest.</summary>
    public sealed record WebControlInspection(WebSocketControls.Report Sockets,WebFrameControl.Report Frames);

    private static WebControlInspection ReadWebControls(string socketsJson,string framesJson,int forward)
    {
        Need(socketsJson.Length is >0 and <=65536 && framesJson.Length is >0 and <=400000,"web_controls_extent");
        try
        {
            using var sockets=JsonDocument.Parse(socketsJson,new JsonDocumentOptions{MaxDepth=8});
            using var frames=JsonDocument.Parse(framesJson,new JsonDocumentOptions{MaxDepth=8});
            var root=sockets.RootElement;WebControlObject(root,"Deadline","Before","After","Observations");
            var bad=ReadWebExchange(root.GetProperty("Deadline"),forward,false);
            var before=ReadWebExchange(root.GetProperty("Before"),forward,true);
            var after=ReadWebExchange(root.GetProperty("After"),forward,false);
            Need(bad.SourcePort!=before.SourcePort && before.SourcePort==after.SourcePort &&
                bad.CompletedUtc<=before.StartedUtc && before.CompletedUtc<=after.StartedUtc &&
                after.CompletedUtc-bad.StartedUtc<=TimeSpan.FromSeconds(125) && after.DateUtc>before.DateUtc,
                "web_controls_progress");
            var events=root.GetProperty("Observations");
            WebControlObject(events,"TimeoutRow","DropRow","TimeoutClock","DropClock");
            string timeout=WebControlText(events,"TimeoutRow",1024),drop=WebControlText(events,"DropRow",1024);
            var observed=WebSocketControls.ObserveEvents([drop,timeout],bad.SourcePort);
            Need(observed.HasValue && observed.Value.TimeoutClock==(ulong)WebControlNumber(events,"TimeoutClock",1,long.MaxValue) &&
                observed.Value.DropClock==(ulong)WebControlNumber(events,"DropClock",1,long.MaxValue),"web_controls_event_clocks");
            var frame=frames.RootElement;
            WebControlObject(frame,"QemuOutPort","QemuInPort","Arguments","ForwardedFrames","VlanFrames","DroppedFrames",
                "GuestExitExpected","Stopped","Healthy");
            int outPort=(int)WebControlNumber(frame,"QemuOutPort",1024,65535),inPort=(int)WebControlNumber(frame,"QemuInPort",1024,65535);
            Need(new[]{outPort,inPort,forward,bad.SourcePort,before.SourcePort}.Distinct().Count()==5,"web_controls_ports");
            string[] arguments=["-chardev","socket,id=clroswebout,host=127.0.0.1,port="+outPort.ToString(CultureInfo.InvariantCulture),
                "-chardev","socket,id=clroswebin,host=127.0.0.1,port="+inPort.ToString(CultureInfo.InvariantCulture),
                "-object","filter-redirector,id=clroswebfilter,netdev=n0,queue=tx,indev=clroswebin,outdev=clroswebout"];
            var args=frame.GetProperty("Arguments");
            Need(args.ValueKind==JsonValueKind.Array && args.GetArrayLength()==arguments.Length &&
                args.EnumerateArray().Select(x=>x.ValueKind==JsonValueKind.String?x.GetString():null).SequenceEqual(arguments),"web_controls_arguments");
            long forwarded=WebControlNumber(frame,"ForwardedFrames",1,100000);
            Need(WebControlNumber(frame,"VlanFrames",1,1)==1 &&
                new[]{"GuestExitExpected","Stopped","Healthy"}.All(key=>frame.GetProperty(key).ValueKind==JsonValueKind.True),"web_controls_lifecycle");
            var withheld=frame.GetProperty("DroppedFrames");
            Need(withheld.ValueKind==JsonValueKind.Array && withheld.GetArrayLength() is >0 and <=128 &&
                forwarded+withheld.GetArrayLength()<=100000,"web_controls_withheld_count");
            List<WebFrameControl.DroppedFrame> drops=[];long ordinal=0;
            foreach(var entry in withheld.EnumerateArray())
            {
                WebControlObject(entry,"Ordinal","Hex");long next=WebControlNumber(entry,"Ordinal",1,forwarded+withheld.GetArrayLength());
                string hex=WebControlHex(entry,"Hex",54,1514);
                Need(next>ordinal,"web_controls_withheld_order");ordinal=next;drops.Add(new(next,hex));
            }
            return new(new(bad,before,after,observed!.Value),new(outPort,inPort,Array.AsReadOnly(arguments),forwarded,1,
                drops.AsReadOnly(),true,true,true));
        }
        catch(Exception error) when(error is JsonException or InvalidDataException or FormatException or OverflowException)
        { throw new ToolFailure("Invalid web controls: "+error.GetType().Name); }
    }

    private static WebSocketControls.Exchange ReadWebExchange(JsonElement row,int forward,bool keepAlive)
    {
        WebControlObject(row,"SourcePort","StartedUtc","CompletedUtc","ElapsedMilliseconds","DateUtc","RequestHex","ResponseHex");
        int port=(int)WebControlNumber(row,"SourcePort",1024,65535);
        DateTimeOffset start=WebControlUtc(row,"StartedUtc"),end=WebControlUtc(row,"CompletedUtc"),date=WebControlUtc(row,"DateUtc");
        long elapsed=WebControlNumber(row,"ElapsedMilliseconds",0,120000);
        Need(start<=end && Math.Abs((end-start).TotalMilliseconds-elapsed)<=5000,"web_controls_exchange_interval");
        string request=WebControlHex(row,"RequestHex",1,2048),response=WebControlHex(row,"ResponseHex",1,8192);
        byte[] expected=Encoding.ASCII.GetBytes("GET /healthz HTTP/1."+(keepAlive?"1":"0")+"\r\nHost: 127.0.0.1:"+
            forward.ToString(CultureInfo.InvariantCulture)+"\r\nConnection: "+(keepAlive?"keep-alive":"close")+"\r\nAccept: */*\r\n\r\n");
        Need(Convert.FromHexString(request).AsSpan().SequenceEqual(expected),"web_controls_request");
        var parsed=WebHttpResponse(Convert.FromHexString(response),"OS_MINIMAL_API_OK",-1,false,keepAlive);
        Need(parsed.DateUtc==date,"web_controls_response_date");
        const long tolerance=WebUtcToleranceSeconds*TimeSpan.TicksPerSecond;
        Need(date.UtcTicks>=start.UtcTicks-tolerance && date.UtcTicks<=end.UtcTicks+tolerance,"web_controls_utc_date");
        return new(port,start,end,elapsed,date,request,response);
    }

    private static void WebControlObject(JsonElement value,params string[] keys)
    {
        Need(value.ValueKind==JsonValueKind.Object,"web_controls_object");
        string[] actual=value.EnumerateObject().Select(p=>p.Name).ToArray();
        Need(actual.Length==keys.Length && actual.Distinct(StringComparer.Ordinal).Count()==keys.Length &&
            actual.All(key=>keys.Contains(key,StringComparer.Ordinal)),"web_controls_schema");
    }

    private static string WebControlText(JsonElement value,string key,int maximum)
    {
        var field=value.GetProperty(key);Need(field.ValueKind==JsonValueKind.String,"web_controls_string");
        string text=field.GetString()!;Need(text.Length is >0 && text.Length<=maximum,"web_controls_string_extent");return text;
    }

    private static long WebControlNumber(JsonElement value,string key,long minimum,long maximum)
    {
        var field=value.GetProperty(key);
        Need(field.ValueKind==JsonValueKind.Number && field.TryGetInt64(out _),"web_controls_integer");
        long number=field.GetInt64();
        Need(number>=minimum && number<=maximum && field.GetRawText()==number.ToString(CultureInfo.InvariantCulture),"web_controls_integer_range");return number;
    }

    private static DateTimeOffset WebControlUtc(JsonElement value,string key)
    {
        string text=WebControlText(value,key,40);
        Need((text.EndsWith('Z') || text.EndsWith("+00:00",StringComparison.Ordinal)) &&
            value.GetProperty(key).TryGetDateTimeOffset(out var date) && date.Offset==TimeSpan.Zero &&
            date> DateTimeOffset.UnixEpoch.AddSeconds(WebUtcToleranceSeconds) &&
            date<DateTimeOffset.MaxValue.AddSeconds(-WebUtcToleranceSeconds),"web_controls_utc");
        return value.GetProperty(key).GetDateTimeOffset();
    }

    private static string WebControlHex(JsonElement value,string key,int minimumBytes,int maximumBytes)
    {
        string text=WebControlText(value,key,maximumBytes*2);
        Need(text.Length>=minimumBytes*2 && text.Length%2==0 && text.All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F'),"web_controls_hex");return text;
    }
}
