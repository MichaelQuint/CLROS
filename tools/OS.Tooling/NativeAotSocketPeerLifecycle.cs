using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

// Shared owned-child mechanics only. Public completions and output admission
// remain separate for the unchanged socket peer and the dedicated vector peer.
internal static class NativeAotSocketPeerLifecycle
{
    internal sealed record Execution(string Root,int Port,string Path,string Sha256,ProcessResult Runner,string ReceiveGateSerial);

    internal static async Task<Execution> RunAsync(string root,string peerLog,
        Func<int,CancellationToken,Task<ProcessResult>> runRunner,CancellationToken cancellation,
        Func<CancellationToken,Task>? beforePeer,string? receiveGateSerial,bool vectors)
    {
        ArgumentNullException.ThrowIfNull(runRunner);root=Path.GetFullPath(root);
        peerLog=Path.GetFullPath(peerLog,root);
        string gate=receiveGateSerial is null?"":Path.GetFullPath(receiveGateSerial,root);
        string prefix=vectors?"socket_vectors":"socket";
        Directory.CreateDirectory(Path.GetDirectoryName(peerLog)!);
        AuthorityFiles.RequirePlainPath(root,Path.GetDirectoryName(peerLog)!,prefix+"_peer_log_path");
        using FileStream log=new(peerLog,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        AuthorityFiles.RequirePlainPath(root,peerLog,prefix+"_peer_log_path");
        int port;using(var reservation=new TcpListener(IPAddress.Loopback,0))
        {reservation.Start();port=((IPEndPoint)reservation.LocalEndpoint).Port;}
        using CancellationTokenSource owned=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        using AuthorityFiles inputs=new();string tool=HostTools.ManagedTool(root,"OS.Tooling");
        _=inputs.Open(Path.GetDirectoryName(tool)!,tool,"socket peer executable",prefix+"_peer_executable",32*1024*1024,singleLink:true);
        Task<ProcessResult>? peer=null,runner=null;ProcessResult? peerResult=null;Task? ready=null;
        try
        {
            runner=runRunner(port,owned.Token);
            if(beforePeer is not null)
            {
                ready=beforePeer(owned.Token);
                if(await Task.WhenAny(ready,runner)==runner)
                {
                    var early=await runner;
                    throw new ToolFailure(prefix+"_runner_exited_before_peer",$"exit={early.ExitCode} terminationConfirmed={early.TerminationConfirmed} failure={early.FailureReason}");
                }
                await ready;
            }
            string command=vectors?"socket-vectors":"socket";
            string[] arguments=gate.Length==0?[tool,"nativeaot",command,"peer","--port",port.ToString(System.Globalization.CultureInfo.InvariantCulture)]:
                [tool,"nativeaot",command,"guest-peer","--port",port.ToString(System.Globalization.CultureInfo.InvariantCulture),"--serial",gate];
            peer=ProcessRunner.RunAsync(new("dotnet",arguments,root)
                {Timeout=TimeSpan.FromSeconds(130),OutputCharacterLimit=65536},owned.Token);
            Task<ProcessResult> first=await Task.WhenAny(peer,runner);
            if(!(await first).Succeeded)owned.Cancel();
            ProcessResult result=await runner;if(!result.Succeeded)owned.Cancel();peerResult=await peer;
            QemuTranscripts.RequireCompleteProcess(result,prefix+"_runner_incomplete");
            QemuTranscripts.RequireCompleteProcess(peerResult,prefix+"_peer_incomplete");
            if(!result.Succeeded||!peerResult.Succeeded||peerResult.StandardError.Length!=0)throw new ToolFailure(prefix+"_peer_or_runner_failed");
            if(vectors)NativeAotSocketVectorsPeer.InspectOutput(peerResult.StandardOutput,requireSendBackpressure:true);
            else NativeAotSocketPeer.InspectOutput(peerResult.StandardOutput);
            inputs.VerifyUnchanged();byte[] bytes=Encoding.UTF8.GetBytes(peerResult.StandardOutput);
            return new(root,port,peerLog,Convert.ToHexString(SHA256.HashData(bytes)),result,gate);
        }
        finally
        {
            owned.Cancel();
            if(ready is not null)try{await ready;}catch{}
            if(runner is not null)try{_=await runner;}catch{}
            if(peer is not null)try{peerResult=await peer;}catch{}
            if(peerResult is not null)
            {
                await log.WriteAsync(Encoding.UTF8.GetBytes(peerResult.StandardOutput),CancellationToken.None);
                if(peerResult.StandardError.Length!=0)
                {
                    using FileStream error=new(peerLog+".stderr.log",FileMode.CreateNew,FileAccess.Write,FileShare.Read);
                    await error.WriteAsync(Encoding.UTF8.GetBytes(peerResult.StandardError),CancellationToken.None);
                }
            }
        }
    }
}
