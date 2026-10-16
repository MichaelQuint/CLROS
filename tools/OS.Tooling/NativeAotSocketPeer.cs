using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

/// <summary>Own the unchanged independent socket peer as a bounded child beside
/// one runner. Both children are awaited on failure/cancellation. A fresh raw
/// peer log and confirmed child exit are required; no guest authority is granted
/// without the sealed program and matching packet/parent evidence.</summary>
public static class NativeAotSocketPeer
{
    /// <summary>Immutable identity of this owned peer execution and runner
    /// result. Only RunAsync can construct it; derived output cannot do so.</summary>
    public sealed class Completion
    {
        internal Completion(string root,int port,string path,string hash,ProcessResult runner)
        {Root=root;Port=port;Path=path;Sha256=hash;Runner=runner;}
        internal string Root {get;}
        public int Port {get;}
        public string Path {get;}
        public string Sha256 {get;}
        public ProcessResult Runner {get;}
        internal void Verify(AuthorityFiles files)
        {
            var file=files.Open(System.IO.Path.GetDirectoryName(Path)!,Path,"original socket peer","socket_peer_log",65536,singleLink:true);
            byte[] raw=file.ReadBytes(65536,"socket_peer_log");
            if(Convert.ToHexString(SHA256.HashData(raw))!=Sha256)throw new ToolFailure("socket_peer_log_identity");
            InspectOutput(new UTF8Encoding(false,true).GetString(raw));
        }
    }

    /// <summary>Value-only check of the original four-client peer's complete
    /// output. Canonical literal byte counts and EOF/reset results are mandatory;
    /// supplied text alone never constructs an execution Completion.</summary>
    public static void InspectOutput(string text)
    {
        const string expected="TEST_START nativeaot.socket.peer\n"+
            "SOCKET_PEER_CONNECTION index=0 sent=20 received=65536 close=eof\n"+
            "SOCKET_PEER_CONNECTION index=1 sent=1 close=reset\n"+
            "SOCKET_PEER_CONNECTION index=2 sent=20 received=18 close=eof\n"+
            "SOCKET_PEER_CONNECTION index=3 sent=20 received=18 close=eof\nTEST_PASS nativeaot.socket.peer\n";
        if(text.Replace("\r\n","\n",StringComparison.Ordinal)!=expected)throw new ToolFailure("socket_peer_output");
    }

    /// <summary>Reserve an ephemeral loopback forward port, release the brief
    /// reservation, and start the unchanged peer beside the supplied runner.
    /// The runner must honor cancellation and return a confirmed process result.
    /// A competing bind fails through normal child checks. The caller owns the
    /// fresh peer log afterward; failures retain captured peer bytes for diagnosis.
    /// This method never builds, rewrites or substitutes the original payload.</summary>
    public static async Task<Completion> RunAsync(string root,string peerLog,
        Func<int,CancellationToken,Task<ProcessResult>> runRunner,CancellationToken cancellation=default,
        Func<CancellationToken,Task>? beforePeer=null)
    {
        ArgumentNullException.ThrowIfNull(runRunner);root=System.IO.Path.GetFullPath(root);
        peerLog=System.IO.Path.GetFullPath(peerLog,root);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(peerLog)!);
        AuthorityFiles.RequirePlainPath(root,System.IO.Path.GetDirectoryName(peerLog)!,"socket_peer_log_path");
        using FileStream log=new(peerLog,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        AuthorityFiles.RequirePlainPath(root,peerLog,"socket_peer_log_path");
        int port;using(var reservation=new TcpListener(IPAddress.Loopback,0))
        {reservation.Start();port=((IPEndPoint)reservation.LocalEndpoint).Port;}
        using CancellationTokenSource owned=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        using AuthorityFiles inputs=new();string tool=HostTools.ManagedTool(root,"OS.Tooling");
        _=inputs.Open(System.IO.Path.GetDirectoryName(tool)!,tool,"socket peer executable","socket_peer_executable",32*1024*1024,singleLink:true);
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
                    throw new ToolFailure("socket_runner_exited_before_peer",$"exit={early.ExitCode} terminationConfirmed={early.TerminationConfirmed} failure={early.FailureReason}");
                }
                await ready;
            }
            peer=ProcessRunner.RunAsync(new("dotnet",[tool,"nativeaot","socket","peer","--port",port.ToString(System.Globalization.CultureInfo.InvariantCulture)],root)
                {Timeout=TimeSpan.FromSeconds(130),OutputCharacterLimit=65536},owned.Token);
            Task<ProcessResult> first=await Task.WhenAny(peer,runner);
            if(!(await first).Succeeded)owned.Cancel();
            ProcessResult result=await runner;if(!result.Succeeded)owned.Cancel();peerResult=await peer;
            QemuTranscripts.RequireCompleteProcess(result,"socket_runner_incomplete");
            QemuTranscripts.RequireCompleteProcess(peerResult,"socket_peer_incomplete");
            if(!result.Succeeded||!peerResult.Succeeded||peerResult.StandardError.Length!=0)throw new ToolFailure("socket_peer_or_runner_failed");
            InspectOutput(peerResult.StandardOutput);inputs.VerifyUnchanged();
            byte[] bytes=Encoding.UTF8.GetBytes(peerResult.StandardOutput);
            return new(root,port,peerLog,Convert.ToHexString(SHA256.HashData(bytes)),result);
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

    // This provisional live read only delays external stimulus. The final
    // immutable transcript still supplies every authority/lifecycle check.
    internal static async Task WaitForAcceptAsync(string serial,bool asynchronous,CancellationToken cancellation)
    {
        using CancellationTokenSource deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(120));
        while(true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            if(File.Exists(serial))
            {
                using FileStream input=new(serial,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                if(input.Length>8*1024*1024)throw new ToolFailure("socket_live_serial_extent");
                using StreamReader reader=new(input,Encoding.UTF8,false,4096);
                string text=await reader.ReadToEndAsync(deadline.Token);
                if(text.Contains("TEST_FAIL",StringComparison.Ordinal)||text.Contains("M16_008_STOP ",StringComparison.Ordinal))
                    throw new ToolFailure("socket_guest_failed_before_peer");
                int attempt=text.IndexOf(" nr=288 ",StringComparison.Ordinal);
                if(attempt<0)attempt=text.IndexOf(" nr=43 ",StringComparison.Ordinal);
                if(attempt>=0)
                {
                    int end=text.IndexOf("\r\n",attempt,StringComparison.Ordinal);
                    if(end>=0 && text[attempt..end].Contains(" valueBits=18446744073709551605 action=1 status=1 ",StringComparison.Ordinal) &&
                        (!asynchronous || text.IndexOf("M16_008_SUPERVISOR identity=4294967297 action=3 ",end,StringComparison.Ordinal)>=0))return;
                }
            }
            await Task.Delay(50,deadline.Token);
        }
    }
}
