using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OS.Tooling;

// Deterministic HTTP-handler controls exercise owned logical requests and
// retained bytes only. They establish no real socket, signal or guest result.
internal static class NativeAotWebSignalRaceAttemptTests
{
    private const string Endpoint="http://127.0.0.1:23458",Health="OS_MINIMAL_API_OK";
    private sealed class BodyStream(byte[] prefix,string terminal,CancellationTokenSource? stop=null) : Stream
    {
        private int offset;
        internal bool Closed;
        public override bool CanRead=>true;
        public override bool CanSeek=>false;
        public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();
        public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] buffer,int start,int count)=>throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();
            if(offset<prefix.Length){int count=Math.Min(buffer.Length,prefix.Length-offset);prefix.AsMemory(offset,count).CopyTo(buffer);offset+=count;return count;}
            if(terminal=="eof")return 0;
            if(terminal=="fault")throw new IOException("secret-rich I/O message must not enter evidence");
            if(terminal=="cancel")stop!.Cancel();
            await Task.Delay(Timeout.Infinite,token);throw new InvalidOperationException("Unexpected pending stream completion");
        }
        protected override void Dispose(bool disposing){Closed=true;base.Dispose(disposing);}
        public override void Flush()=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
    private sealed class Peer(string mode,CancellationTokenSource stop) : HttpMessageHandler
    {
        internal int Calls;
        internal bool ChurnJoined;
        internal BodyStream? Stream;
        private bool churn;
        private static HttpResponseMessage Reply(string body)=>new(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"text/plain")};
        private HttpResponseMessage StreamReply(byte[] bytes,string terminal,string media="text/plain")
        {
            Stream=new(bytes,terminal,stop);var content=new StreamContent(Stream);content.Headers.ContentType=new MediaTypeHeaderValue(media);
            return new(HttpStatusCode.OK){Content=content};
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Calls++;string path=request.RequestUri!.AbsolutePath;
            if(mode is "headers-pending" or "join-fault")
            {
                if(request.Method==HttpMethod.Post)
                {
                    if(path=="/__clros/churn")
                    {churn=true;return mode=="headers-pending"?Task.FromResult(StreamReply([65],"wait")):Hold(token);}
                    return Task.FromException<HttpResponseMessage>(new HttpRequestException("secret-rich request message"));
                }
                if(path=="/healthz")return Task.FromResult(Reply(Health));
                if(path=="/__clros/signalrace/status")return Task.FromResult(Reply("CLROS_SIGNAL_RACE_STATUS phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0"));
                int active=churn?1:0;
                return Task.FromResult(Reply($"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={active} started={active} completed=0 canceled=0 failed=0 rejected=0 timers={active} workers={active} collections={active} heldBytes={active*65536} peakHeldBytes={active*65536}"));
            }
            if(mode=="before-headers")return Task.FromException<HttpResponseMessage>(new HttpRequestException("secret-rich request message"));
            if(mode=="status")return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable){Content=new StringContent("rejected status body",Encoding.UTF8,"text/plain")});
            return Task.FromResult(mode switch
            {
                "partial"=>StreamReply([65,255],"fault"),"utf8"=>StreamReply([65,255],"eof"),
                "cancel"=>StreamReply([65,255],"cancel"),"overflow"=>StreamReply(new byte[4097],"eof"),
                "media"=>StreamReply([65],"eof","application/json"),_=>throw new InvalidOperationException("Unexpected mode")
            });
        }
        private async Task<HttpResponseMessage> Hold(CancellationToken token)
        {try{await Task.Delay(Timeout.Infinite,token);throw new InvalidOperationException("Unexpected completion");}finally{ChurnJoined=true;}}
    }
    private static string Output()
    {string directory=Path.GetFullPath(Path.Combine("artifacts","host-tests","race-attempt-"+Guid.NewGuid().ToString("N")));Directory.CreateDirectory(directory);return Path.Combine(directory,"observations.json");}
    private static async Task<(string Failure,string Json,JsonDocument Log)> CaptureFailure(Peer peer,string path,CancellationToken token)
    {
        using var client=new HttpClient(peer);string failure="";
        try{await NativeAotWebPayload.CaptureSignalRaceAsync(client,Endpoint,Health,path,token);}
        catch(Exception error)when(error is InvalidDataException or IOException or HttpRequestException or OperationCanceledException or DecoderFallbackException){failure=error is InvalidDataException?error.Message:error.GetType().Name;}
        HostCheck.True(failure.Length>0);string selected=File.ReadAllText(path),json=File.ReadAllText(path+".attempts.json");
        var summary=NativeAotWebPayload.InspectSignalRaceAttempts(json,selected,Endpoint,Health);HostCheck.True(!summary.SemanticComplete);
        HostCheck.True(!json.Contains("secret-rich",StringComparison.Ordinal));return(failure,json,JsonDocument.Parse(json));
    }

    [HostTest("web race attempt log retains partial fault cancellation invalid UTF8 and overflow bytes")]
    private static async Task PartialBodies()
    {
        foreach(string mode in new[]{"partial","utf8","cancel","overflow"})
        {
            using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(10));var peer=new Peer(mode,stop);
            var result=await CaptureFailure(peer,Output(),stop.Token);using var log=result.Log;var row=log.RootElement.GetProperty("attempts")[0];
            HostCheck.Equal(1,peer.Calls);HostCheck.True(peer.Stream!.Closed);HostCheck.Equal(0,log.RootElement.GetProperty("selections").GetArrayLength());
            HostCheck.Equal(mode=="cancel"?"cancelled":"faulted",row.GetProperty("outcome").GetString());
            HostCheck.True(Convert.FromBase64String(row.GetProperty("bodyBase64").GetString()!).SequenceEqual(mode=="overflow"?new byte[4096]:new byte[]{65,255}));
            HostCheck.Equal(mode=="utf8",row.GetProperty("bodyComplete").GetBoolean());
            HostCheck.Equal(mode switch{"partial"=>"IOException","utf8"=>"DecoderFallbackException","cancel"=>"cancelled",_=>"web_signalrace_response_size"},row.GetProperty("error").GetString());
        }
    }

    [HostTest("web race attempt log retains rejected status content type and pre-header failures")]
    private static async Task RejectedResponses()
    {
        foreach(string mode in new[]{"status","media","before-headers"})
        {
            using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(10));var peer=new Peer(mode,stop);
            var result=await CaptureFailure(peer,Output(),stop.Token);using var log=result.Log;var row=log.RootElement.GetProperty("attempts")[0];
            HostCheck.Equal(1,peer.Calls);HostCheck.Equal(0,log.RootElement.GetProperty("selections").GetArrayLength());
            HostCheck.Equal(mode!="before-headers",row.GetProperty("headers").GetBoolean());
            HostCheck.Equal(mode switch{"status"=>"web_signalrace_churn_status","media"=>"web_signalrace_content_type",_=>"HttpRequestException"},result.Failure);
            HostCheck.Equal(mode=="status"?"response":"faulted",row.GetProperty("outcome").GetString());
            HostCheck.Equal(mode=="status"?"rejected status body":"",Encoding.UTF8.GetString(Convert.FromBase64String(row.GetProperty("bodyBase64").GetString()!)));
            if(mode=="media")HostCheck.True(peer.Stream!.Closed);
        }
    }

    [HostTest("web race response headers end pending overlap even while body read remains blocked")]
    private static async Task HeadersEndOverlap()
    {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(10));var peer=new Peer("headers-pending",stop);
        var result=await CaptureFailure(peer,Output(),stop.Token);using var log=result.Log;
        HostCheck.Equal("web_signalrace_lost_overlap",result.Failure);HostCheck.Equal(4,peer.Calls);HostCheck.True(peer.Stream!.Closed);
        var row=log.RootElement.GetProperty("attempts")[3];HostCheck.True(row.GetProperty("headers").GetBoolean());
        HostCheck.Equal("cancelled",row.GetProperty("outcome").GetString());HostCheck.Equal("QQ==",row.GetProperty("bodyBase64").GetString());
        HostCheck.Equal(3,log.RootElement.GetProperty("selections").GetArrayLength());
    }

    [HostTest("web race primary failure survives joining another faulted owned request")]
    private static async Task PrimaryFailure()
    {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(10));var peer=new Peer("join-fault",stop);
        var result=await CaptureFailure(peer,Output(),stop.Token);using var log=result.Log;
        HostCheck.Equal("web_signalrace_lost_overlap",result.Failure);HostCheck.True(peer.ChurnJoined);
        HostCheck.Equal(result.Failure,log.RootElement.GetProperty("failure").GetString());
        var rows=log.RootElement.GetProperty("attempts");HostCheck.Equal(6,rows.GetArrayLength());
        HostCheck.Equal("HttpRequestException",rows[5].GetProperty("error").GetString());
        HostCheck.Equal("cancelled",rows[3].GetProperty("outcome").GetString());
    }

    [HostTest("web race companion output collision refuses before any HTTP attempt")]
    private static async Task FreshOutput()
    {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(10));var peer=new Peer("before-headers",stop);using var client=new HttpClient(peer);
        string path=Output();File.WriteAllText(path+".attempts.json","retained original");bool refused=false;
        try{await NativeAotWebPayload.CaptureSignalRaceAsync(client,Endpoint,Health,path,stop.Token);}catch(IOException){refused=true;}
        HostCheck.True(refused);HostCheck.Equal(0,peer.Calls);HostCheck.Equal("retained original",File.ReadAllText(path+".attempts.json"));
    }
}
