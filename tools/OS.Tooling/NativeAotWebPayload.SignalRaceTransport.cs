using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using OS.Tooling.Native;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Run the unchanged bounded controller against one owned IPv4
    /// loopback forward with actual HTTP/1.1 stream observations. Owns three
    /// persistent role handlers, their sockets and every started I/O until joined.
    /// Creates fresh selected/attempt reports and a separate .wire.json companion;
    /// partial failures retain raw successful reads/attempted writes after close
    /// and join before the original error propagates. A failed join grants no
    /// final snapshot. Snapshot/write errors preserve any controller error too.
    /// Stream IDs/endpoints/ticks confer no NAT tuple, PCAP, native process or
    /// guest authority. HTTP framing and the owned runner join remain separate.</summary>
    public static Task<string> CaptureObservedSignalRaceAsync(int port,string expectedHealthBody,
        string observationPath,CancellationToken cancellation=default)=>CaptureObservedSignalRaceCoreAsync(port,expectedHealthBody,observationPath,cancellation,null);

    private static async Task<string> CaptureObservedSignalRaceCoreAsync(int port,string expectedHealthBody,
        string observationPath,CancellationToken cancellation,Action<string,CreatedRaceReport,CreatedRaceReport,CreatedRaceReport>? retainCreated)
    {
        string endpoint="http://127.0.0.1:"+port.ToString(CultureInfo.InvariantCulture);RaceInputs(endpoint,expectedHealthBody);
        string path=Path.GetFullPath(observationPath);AuthorityFiles.RequirePlainPath(Path.GetDirectoryName(path)!,Path.GetDirectoryName(path)!,"web_signalrace_output_path");
        using var output=new FileStream(path+".wire.json",FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        var wire=new NativeAotServiceQuotaPeer.WireCapture(port,signalRace:true);wire.BindClock(Stopwatch.StartNew());
        string? selected=null;Exception? failure=null;CreatedRaceReport? selectedFile=null,attemptFile=null,wireFile=null;
        try
        {
            using var client=new HttpClient(new RaceRoutingHandler(wire,port),disposeHandler:true)
            {Timeout=Timeout.InfiniteTimeSpan,DefaultRequestVersion=HttpVersion.Version11,DefaultVersionPolicy=HttpVersionPolicy.RequestVersionExact};
            selected=await CaptureSignalRaceCoreAsync(client,endpoint,expectedHealthBody,path,cancellation,wire,
                retainCreated is null?null:(first,second)=>{selectedFile=first;attemptFile=second;});
        }
        catch(Exception error){failure=error;}
        try
        {
            await wire.StopAsync();var trace=wire.Snapshot(failure is null?"signalrace-run":"signalrace-partial");
            byte[] bytes=Encoding.UTF8.GetBytes(SerializeSignalRaceWire(trace));await output.WriteAsync(bytes);await output.FlushAsync();
            if(failure is null&&retainCreated is not null)
            {
                wireFile=CreatedRaceReport.Retain(output,bytes);retainCreated(selected!,selectedFile!,attemptFile!,wireFile);
                selectedFile=null;attemptFile=null;wireFile=null;
            }
        }
        catch(Exception error){if(failure is not null)throw new AggregateException(failure,error);throw;}
        finally{selectedFile?.Dispose();attemptFile?.Dispose();wireFile?.Dispose();}
        if(failure is not null)ExceptionDispatchInfo.Capture(failure).Throw();return selected!;
    }

    // Keep actual creation identities alive across writer closure and the first
    // immutable AuthorityFiles open. The bridge grants read access while the
    // creator still excludes other writers, and never permits delete sharing.
    internal sealed class CreatedRaceReport : IDisposable
    {
        private readonly FileStream bridge;
        private readonly FileIdentity identity;
        private readonly string hash;
        private CreatedRaceReport(FileStream bridge,FileIdentity identity,string hash)
        {this.bridge=bridge;this.identity=identity;this.hash=hash;}
        internal static CreatedRaceReport Retain(FileStream creator,byte[] written)
        {
            var identity=NativeFile.GetIdentity(creator.SafeFileHandle);FileStream? bridge=null;
            try
            {
                RaceNeed(identity.LinkCount==1&&identity.Length==(ulong)written.Length,"created_report_shape");
                bridge=new(creator.Name,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                var held=NativeFile.GetIdentity(bridge.SafeFileHandle);
                RaceNeed(held.Key==identity.Key&&held.LinkCount==1&&held.Length==identity.Length,"created_report_identity");
                var result=new CreatedRaceReport(bridge,identity,Convert.ToHexString(SHA256.HashData(written)));bridge=null;return result;
            }
            finally{bridge?.Dispose();}
        }
        internal void Verify(AuthorityFile file)
        {
            var held=NativeFile.GetIdentity(bridge.SafeFileHandle);
            RaceNeed(file.InitialIdentity.Key==identity.Key&&held.Key==identity.Key&&held.LinkCount==1&&
                file.InitialIdentity.LinkCount==1&&held.Length==identity.Length&&file.Length==(long)identity.Length&&
                file.Sha256.Equals(hash,StringComparison.OrdinalIgnoreCase),"created_report_changed");
        }
        public void Dispose()=>bridge.Dispose();
    }

    internal sealed record OwnedRaceCapture(string SelectedJson,CreatedRaceReport Selected,CreatedRaceReport Attempts,CreatedRaceReport Wire) : IDisposable
    {
        public void Dispose(){Selected.Dispose();Attempts.Dispose();Wire.Dispose();}
    }

    internal static async Task<OwnedRaceCapture> CaptureOwnedObservedSignalRaceAsync(int port,string health,string path,CancellationToken cancellation)
    {
        OwnedRaceCapture? result=null;
        try
        {
            _=await CaptureObservedSignalRaceCoreAsync(port,health,path,cancellation,
                (text,selected,attempts,wire)=>result=new(text,selected,attempts,wire));
            return result??throw new InvalidDataException("web_signalrace_created_reports_missing");
        }
        catch{result?.Dispose();throw;}
    }

    private sealed class RaceRoutingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string,HttpMessageInvoker> handlers=new(StringComparer.Ordinal);
        private readonly int port;
        internal RaceRoutingHandler(NativeAotServiceQuotaPeer.WireCapture wire,int port)
        {
            this.port=port;
            foreach(string role in new[]{"control","churn","race"})
            {
                string ownedRole=role;
                var handler=new SocketsHttpHandler{UseProxy=false,AllowAutoRedirect=false,UseCookies=false,MaxConnectionsPerServer=1,
                    ConnectTimeout=TimeSpan.FromSeconds(10),MaxResponseHeadersLength=8};
                handler.ConnectCallback=async(context,token)=>
                {
                    RaceNeed(context.DnsEndPoint.Host=="127.0.0.1"&&context.DnsEndPoint.Port==port,"wire_connect_target");
                    return await wire.ConnectAsync(ownedRole,token);
                };
                handlers.Add(role,new(handler,disposeHandler:true));
            }
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            RaceNeed(request.RequestUri is { } uri&&uri.Scheme=="http"&&uri.Host=="127.0.0.1"&&uri.Port==port&&uri.UserInfo==""&&uri.Query==""&&uri.Fragment=="","wire_request_target");
            request.Version=HttpVersion.Version11;request.VersionPolicy=HttpVersionPolicy.RequestVersionExact;
            string role=NativeAotServiceQuotaPeer.SignalRaceWireRole(request.Method.Method,request.RequestUri!.AbsolutePath);
            return handlers[role].SendAsync(request,token);
        }
        protected override void Dispose(bool disposing)
        {if(disposing)foreach(var handler in handlers.Values)handler.Dispose();base.Dispose(disposing);}
    }
}
