using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

/// <summary>Own an independent socket peer as a bounded child beside
/// one runner. Both children are awaited on failure/cancellation. A fresh raw
/// peer log and confirmed child exit are required; no guest authority is granted
/// without the sealed program and matching packet/parent evidence.</summary>
public static class NativeAotSocketPeer
{
    /// <summary>Immutable identity of this owned peer execution and runner
    /// result. Only RunAsync can construct it; derived output cannot do so.</summary>
    public sealed class Completion
    {
        internal Completion(string root,int port,string path,string hash,ProcessResult runner,string receiveGateSerial)
        {Root=root;Port=port;Path=path;Sha256=hash;Runner=runner;ReceiveGateSerial=receiveGateSerial;}
        internal string Root {get;}
        public int Port {get;}
        public string Path {get;}
        public string Sha256 {get;}
        internal string ReceiveGateSerial {get;}
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
    /// reservation, and start the literal peer beside the supplied runner.
    /// The runner must honor cancellation and return a confirmed process result.
    /// A competing bind fails through normal child checks. The caller owns the
    /// fresh peer log afterward; failures retain captured peer bytes for diagnosis.
    /// Optional receiveGateSerial selects the async external-stimulus gate and
    /// is retained in the completion for the final exact profile/path join.
    /// This method never builds, rewrites or substitutes the original payload.</summary>
    public static async Task<Completion> RunAsync(string root,string peerLog,
        Func<int,CancellationToken,Task<ProcessResult>> runRunner,CancellationToken cancellation=default,
        Func<CancellationToken,Task>? beforePeer=null,string? receiveGateSerial=null)
    {
        var execution=await NativeAotSocketPeerLifecycle.RunAsync(root,peerLog,runRunner,cancellation,beforePeer,receiveGateSerial,vectors:false);
        return new(execution.Root,execution.Port,execution.Path,execution.Sha256,execution.Runner,execution.ReceiveGateSerial);
    }

    /// <summary>Independent loopback stimulus for the slow emulated async guest.
    /// Hold the first literal request until its accepted socket reports EAGAIN
    /// and that caller parks. The live serial read only controls stimulus; it
    /// supplies no final evidence authority. Own four clients under a two-minute
    /// deadline, preserve the original byte/reset/EOF protocol and fail on any
    /// missing response. The unchanged publisher and guest ELF are not modified.</summary>
    public static async Task RunReceiveGatedPeerAsync(string root,int port,string serial,TextWriter output)
    {
        if(port is <1 or >65535)throw new ToolFailure("socket_peer_port");
        root=System.IO.Path.GetFullPath(root);serial=System.IO.Path.GetFullPath(serial,root);
        AuthorityFiles.RequirePlainPath(root,System.IO.Path.GetDirectoryName(serial)!,"socket_peer_serial_path");
        using CancellationTokenSource deadline=new(TimeSpan.FromMinutes(2));CancellationToken token=deadline.Token;
        output.WriteLine("TEST_START nativeaot.socket.peer");
        byte[] request="CLROS_SOCKET_REQUEST"u8.ToArray(),reply="CLROS_SOCKET_REPLY"u8.ToArray(),buffer=new byte[4096];
        for(int index=0;index<4;index++)
        {
            using Socket socket=new(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
            socket.NoDelay=true;socket.ReceiveBufferSize=4096;
            await socket.ConnectAsync(IPAddress.Loopback,port,token);
            if(index==0)await WaitForSerialAsync(serial,HasPendingReceive,token);
            if(index==1)
            {
                await SendAll(new byte[]{0x52});await Task.Delay(100,token);
                socket.LingerState=new(true,0);output.WriteLine("SOCKET_PEER_CONNECTION index=1 sent=1 close=reset");continue;
            }
            await SendAll(request.AsMemory(0,5));await SendAll(request.AsMemory(5));
            if(index==0)await Task.Delay(250,token);else socket.Shutdown(SocketShutdown.Send);
            int expected=index==0?65536:reply.Length,received=0;
            while(received<expected)
            {
                int count=await socket.ReceiveAsync(buffer.AsMemory(0,Math.Min(buffer.Length,expected-received)),SocketFlags.None,token);
                if(count==0)throw new ToolFailure("socket_peer_early_eof");
                for(int i=0;i<count;i++)if(buffer[i]!=(index==0?(byte)((received+i)%251):reply[received+i]))throw new ToolFailure("socket_peer_payload");
                received+=count;
            }
            if(await socket.ReceiveAsync(buffer.AsMemory(0,1),SocketFlags.None,token)!=0)throw new ToolFailure("socket_peer_trailing_payload");
            output.WriteLine($"SOCKET_PEER_CONNECTION index={index} sent=20 received={received} close=eof");
            async Task SendAll(ReadOnlyMemory<byte> bytes)
            {
                while(!bytes.IsEmpty){int count=await socket.SendAsync(bytes,SocketFlags.None,token);if(count<=0)throw new ToolFailure("socket_peer_send");bytes=bytes[count..];}
            }
        }
        output.WriteLine("TEST_PASS nativeaot.socket.peer");
    }

    /// <summary>Value-only predicate for delaying live external stimulus. It
    /// recognizes the first accepted fd's failed receive and the same caller's
    /// later park; malformed or incomplete observations return false. This is
    /// not a transcript validator and supplies no guest acceptance authority.</summary>
    public static bool HasPendingReceive(string text)
    {
        ulong? fd=null,caller=null;
        foreach(string raw in text.Split('\n'))
        {
            string row=raw.TrimEnd('\r');
            if(row.StartsWith("M16_008_CALL ",StringComparison.Ordinal))
            {
                var call=Regex.Match(row,@"^M16_008_CALL identity=(\d+) nr=(43|45|288) a0=(\d+) .* valueBits=(\d+) action=1 status=1 ",RegexOptions.CultureInvariant);
                if(!call.Success || !ulong.TryParse(call.Groups[1].Value,out ulong identity) || !ulong.TryParse(call.Groups[3].Value,out ulong descriptor) ||
                    !ulong.TryParse(call.Groups[4].Value,out ulong value))continue;
                if(fd is null && (call.Groups[2].Value is "43" or "288") && value is >=3 and <16)fd=value;
                else if(fd==descriptor && call.Groups[2].Value=="45")caller=value==unchecked((ulong)-11L)?identity:null;
            }
            else if(caller.HasValue && row.StartsWith("M16_008_SUPERVISOR identity="+caller.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+" action=3 ",StringComparison.Ordinal))return true;
        }
        return false;
    }

    internal static async Task WaitForSerialAsync(string serial,Func<string,bool> ready,CancellationToken cancellation)
    {
        while(true)
        {
            cancellation.ThrowIfCancellationRequested();
            if(File.Exists(serial))
            {
                using FileStream input=new(serial,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                if(input.Length>8*1024*1024)throw new ToolFailure("socket_live_serial_extent");
                using StreamReader reader=new(input,Encoding.UTF8,false,4096);
                string text=await reader.ReadToEndAsync(cancellation);
                if(text.Contains("TEST_FAIL",StringComparison.Ordinal)||text.Contains("M16_008_STOP ",StringComparison.Ordinal))throw new ToolFailure("socket_guest_failed_before_peer");
                if(ready(text))return;
            }
            await Task.Delay(50,cancellation);
        }
    }

    // This provisional live read only delays external stimulus. The final
    // immutable transcript still supplies every authority/lifecycle check.
    internal static async Task WaitForAcceptAsync(string serial,bool asynchronous,CancellationToken cancellation)
    {
        using CancellationTokenSource deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(120));
        await WaitForSerialAsync(serial,text=>
        {
            int attempt=text.IndexOf(" nr=288 ",StringComparison.Ordinal);
            if(attempt<0)attempt=text.IndexOf(" nr=43 ",StringComparison.Ordinal);
            if(attempt<0)return false;
            int end=text.IndexOf("\r\n",attempt,StringComparison.Ordinal);
            return end>=0 && text[attempt..end].Contains(" valueBits=18446744073709551605 action=1 status=1 ",StringComparison.Ordinal) &&
                (!asynchronous || text.IndexOf("M16_008_SUPERVISOR identity=4294967297 action=3 ",end,StringComparison.Ordinal)>=0);
        },deadline.Token);
    }
}
