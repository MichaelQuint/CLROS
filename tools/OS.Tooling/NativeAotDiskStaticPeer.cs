using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace OS.Tooling;

/// <summary>Own a bounded five-response development peer and its original
/// runner. Preserve fresh partial failures, join child cleanup and record a
/// stopped endpoint. Complete native/image/packet/file validation remains the
/// enclosing original consumer's obligation; this is not Blazor acceptance.</summary>
public static class NativeAotDiskStaticPeer
{
    public sealed record Response(int Ordinal,string Method,string Path,int Status,string[] RequestHeaders,
        string[] ResponseHeaders,int Bytes,string Sha256,string BodyFile,long Started,long Finished);

    public sealed class Completion
    {
        internal Completion(int port,string directory,string hash,ProcessResult runner)
        {Port=port;Directory=directory;ReceiptSha256=hash;Runner=runner;}
        public int Port {get;}
        public string Directory {get;}
        public string ReceiptSha256 {get;}
        public ProcessResult Runner {get;}
    }

    /// <summary>Start exactly one caller-supplied cancellable runner and a
    /// single pooled HTTP/1.1 connection after actual guest readiness. The owner
    /// retains no live child on return or throw. At most five64KiB bodies are
    /// copied to a fresh artifacts directory; no redirects, proxy, decompression
    /// or external content can satisfy the requests. A competing bind, missing
    /// readiness, wrong response, timeout or incomplete cleanup fails.</summary>
    public static async Task<Completion> RunAsync(string root,string serial,
        Func<int,CancellationToken,Task<ProcessResult>> runRunner,CancellationToken cancellation)
    {
        root=Path.GetFullPath(root);serial=Path.GetFullPath(serial,root);
        string directory=serial+".disk-http";
        if(!directory.StartsWith(Path.Combine(root,"artifacts")+Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) || Path.Exists(directory))
            throw new ToolFailure("disk_http_fresh_artifacts_required");
        AuthorityFiles.RequirePlainPath(root,Path.GetDirectoryName(directory)!,"disk_http_path");Directory.CreateDirectory(directory);
        int port;
        using(TcpListener reservation=new(IPAddress.Loopback,0))
        {reservation.Start();port=((IPEndPoint)reservation.LocalEndpoint).Port;}
        using var owned=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        Task<ProcessResult> runner=runRunner(port,owned.Token);Task? peer=null;
        try
        {
            peer=Observe();
            if(await Task.WhenAny(peer,runner)==runner)
            {
                cancellation.ThrowIfCancellationRequested();
                throw new ToolFailure("disk_http_runner_exited_before_peer");
            }
            await peer;var result=await runner;
            QemuTranscripts.RequireCompleteProcess(result,"disk_http_runner_incomplete");
            if(!result.Succeeded)throw new ToolFailure("disk_http_runner_failed");
            using(Socket stopped=new(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp))
            {
                using var refusal=CancellationTokenSource.CreateLinkedTokenSource(cancellation);refusal.CancelAfter(TimeSpan.FromSeconds(5));
                bool refused=false;
                try{await stopped.ConnectAsync(IPAddress.Loopback,port,refusal.Token);}
                catch(SocketException error) when(error.SocketErrorCode==SocketError.ConnectionRefused){refused=true;}
                if(!refused)throw new ToolFailure("disk_http_stopped_endpoint_live");
                using FileStream output=new(Path.Combine(directory,"stopped.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                await JsonSerializer.SerializeAsync(output,new{Port=port,EndpointRefused=true,RunnerExit=result.ExitCode},cancellationToken:cancellation);output.Flush(true);
            }
            return new(port,directory,NativeAotWebPayload.HashFile(Path.Combine(directory,"http.json")),result);
        }
        finally
        {
            owned.Cancel();if(peer is not null)try{await peer;}catch{}
            try{_=await runner;}catch{}
        }

        async Task Observe()
        {
            using var startup=CancellationTokenSource.CreateLinkedTokenSource(owned.Token);startup.CancelAfter(TimeSpan.FromSeconds(240));
            await NativeAotSocketPeer.WaitForSerialAsync(serial,text=>text.Contains("M10_018_LISTENING ",StringComparison.Ordinal),startup.Token);
            using var matrix=CancellationTokenSource.CreateLinkedTokenSource(owned.Token);matrix.CancelAfter(TimeSpan.FromSeconds(DiskStaticHttpContract.MatrixTimeoutSeconds));
            using HttpClient client=new(new SocketsHttpHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.None,UseProxy=false,MaxConnectionsPerServer=1})
            {BaseAddress=new Uri("http://127.0.0.1:"+port),Timeout=TimeSpan.FromSeconds(60),DefaultRequestVersion=HttpVersion.Version11,DefaultVersionPolicy=HttpVersionPolicy.RequestVersionExact};
            List<Response> rows=[];bool complete=false;
            try
            {
                foreach(string path in DiskStaticHttpContract.Paths)
                {
                    int ordinal=rows.Count;using var request=new HttpRequestMessage(HttpMethod.Get,path);
                    string[] headers=request.Headers.Select(h=>h.Key+": "+string.Join(",",h.Value)).ToArray();long started=Stopwatch.GetTimestamp();
                    using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,matrix.Token);
                    using var source=await response.Content.ReadAsStreamAsync(matrix.Token);using MemoryStream body=new();byte[] buffer=new byte[8192];
                    for(int read;(read=await source.ReadAsync(buffer,matrix.Token))!=0;)
                    {if(body.Length+read>DiskStaticHttpContract.MaximumBodyBytes)throw new ToolFailure("disk_http_body_capacity");body.Write(buffer,0,read);}
                    long finished=Stopwatch.GetTimestamp();byte[] bytes=body.ToArray();string name=ordinal.ToString("D3",CultureInfo.InvariantCulture)+".body";
                    using(FileStream output=new(Path.Combine(directory,name),FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                    {await output.WriteAsync(bytes,matrix.Token);output.Flush(true);}
                    List<string> responseHeaders=[];int headerCharacters=0;
                    foreach(var header in response.Headers.Concat(response.Content.Headers))
                    {
                        string line=header.Key+": "+string.Join(",",header.Value);
                        if(responseHeaders.Count==32 || line.Length is 0 or >1024 ||
                            line.Any(c=>c is <' ' or >'~') || (headerCharacters+=line.Length)>4096)
                            throw new ToolFailure("disk_http_header_capacity");
                        responseHeaders.Add(line);
                    }
                    rows.Add(new(ordinal,"GET",path,(int)response.StatusCode,headers,responseHeaders.ToArray(),
                        bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)),name,started,finished));
                    if(response.StatusCode!=HttpStatusCode.OK)throw new ToolFailure("disk_http_status "+path);
                }
                complete=true;
            }
            finally
            {
                using FileStream receipt=new(Path.Combine(directory,"http.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                await JsonSerializer.SerializeAsync(receipt,new{Schema=DiskStaticHttpContract.Schema,Scope=DiskStaticHttpContract.Scope,
                    Port=port,Complete=complete,ClockFrequency=Stopwatch.Frequency,Responses=rows},cancellationToken:CancellationToken.None);receipt.Flush(true);
            }
        }
    }
}
