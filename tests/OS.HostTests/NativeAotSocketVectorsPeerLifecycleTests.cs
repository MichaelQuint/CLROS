using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using OS.Tooling;

internal static class NativeAotSocketVectorsPeerLifecycleTests
{
    private static readonly Action<NativeAotSocketVectorsPeer.Completion,AuthorityFiles> Verify =
        typeof(NativeAotSocketVectorsPeer.Completion).GetMethod("Verify",BindingFlags.Instance|BindingFlags.NonPublic)!
            .CreateDelegate<Action<NativeAotSocketVectorsPeer.Completion,AuthorityFiles>>();

    // Real separate peer children; synthetic serial and TCP runner below grant no guest authority.
    [HostTest("vector peer orchestration seals its own fresh log serial root and same runner object")]
    private static void OwnedPeer()=>OwnedPeerAsync().GetAwaiter().GetResult();
    private static async Task OwnedPeerAsync()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=DirectoryFor(root);
        string serial=Path.Combine(directory,"guest.serial.log"),log=Path.Combine(directory,"peer.log");
        var expected=new ProcessResult(0,"runner-stub","",false,false,false,TimeSpan.Zero);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var result=await NativeAotSocketVectorsPeer.RunAsync(root,Path.GetRelativePath(root,log),Path.GetRelativePath(root,serial),
            (port,token)=>Stub(port,serial,expected,token),deadline.Token);
        HostCheck.True(ReferenceEquals(expected,result.Runner) && result.Port is >0 and <=65535);
        HostCheck.Equal(log,result.Path);HostCheck.Equal(serial,result.SerialPath);
        HostCheck.Equal(root,(string)typeof(NativeAotSocketVectorsPeer.Completion)
            .GetProperty("Root",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(result)!);
        byte[] raw=File.ReadAllBytes(log);HostCheck.Equal(Convert.ToHexString(SHA256.HashData(raw)),result.Sha256);
        NativeAotSocketVectorsPeer.InspectOutput(Encoding.UTF8.GetString(raw));
        using(var files=new AuthorityFiles()){Verify(result,files);files.VerifyUnchanged();}
        HostCheck.True(typeof(NativeAotSocketVectorsPeer.Completion).IsSealed &&
            typeof(NativeAotSocketVectorsPeer.Completion).GetConstructors().Length==0 &&
            !typeof(NativeAotSocketPeer.Completion).IsAssignableFrom(typeof(NativeAotSocketVectorsPeer.Completion)));
        Reject(()=>NativeAotSocketPeer.InspectOutput(Encoding.UTF8.GetString(raw)),"socket_peer_output");
        int starts=0;bool fresh=false;
        try { _=await NativeAotSocketVectorsPeer.RunAsync(root,log,serial,(_,_)=>{starts++;return Task.FromResult(expected);},deadline.Token); }
        catch(IOException){fresh=true;}
        HostCheck.True(fresh && starts==0);
        File.AppendAllText(log,"extra\n");
        Reject(()=>{using var files=new AuthorityFiles();Verify(result,files);},"socket_vectors_peer_log_identity");
    }

    [HostTest("vector peer orchestration retains an empty log when runner exits before accept readiness")]
    private static void EarlyRunner()=>EarlyRunnerAsync().GetAwaiter().GetResult();
    private static async Task EarlyRunnerAsync()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=DirectoryFor(root);
        string serial=Path.Combine(directory,"guest.serial.log"),log=Path.Combine(directory,"peer.log");
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string? failure=null;
        try { _=await NativeAotSocketVectorsPeer.RunAsync(root,log,serial,(_,_)=>
            Task.FromResult(new ProcessResult(2,"","",false,false,false,TimeSpan.Zero)),deadline.Token); }
        catch(ToolFailure error){failure=error.Message;}
        HostCheck.True(failure?.StartsWith("socket_vectors_runner_exited_before_peer exit=2 terminationConfirmed=True",StringComparison.Ordinal)==true);
        HostCheck.True(new FileInfo(log).Length==0 && !File.Exists(serial));
    }

    [HostTest("vector peer orchestration cancels and awaits a child waiting on receive progress when runner fails")]
    private static void FailedRunner()=>FailedRunnerAsync().GetAwaiter().GetResult();
    private static async Task FailedRunnerAsync()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=DirectoryFor(root);
        string serial=Path.Combine(directory,"guest.serial.log"),log=Path.Combine(directory,"peer.log");
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));bool exited=false;string? failure=null;
        try
        {
            _=await NativeAotSocketVectorsPeer.RunAsync(root,log,serial,async(port,token)=>
            {
                try{return await Stub(port,serial,new ProcessResult(2,"","",false,false,false,TimeSpan.Zero),token,stopAtGate:true);}
                finally{exited=true;}
            },deadline.Token);
        }
        catch(ToolFailure error){failure=error.Message;}
        HostCheck.True(exited && failure?.StartsWith("socket_vectors_peer_",StringComparison.Ordinal)==true);
        HostCheck.True(File.Exists(log) && !File.ReadAllText(log).Contains("TEST_PASS",StringComparison.Ordinal));
    }

    [HostTest("vector peer orchestration awaits both owned children after external cancellation")]
    private static void Cancelled()=>CancelledAsync().GetAwaiter().GetResult();
    private static async Task CancelledAsync()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=DirectoryFor(root);
        string serial=Path.Combine(directory,"guest.serial.log"),log=Path.Combine(directory,"peer.log");
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var connected=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);bool exited=false,cancelled=false;
        Task<NativeAotSocketVectorsPeer.Completion> run=NativeAotSocketVectorsPeer.RunAsync(root,log,serial,async(port,token)=>
        {
            try{return await Stub(port,serial,new ProcessResult(0,"","",false,false,false,TimeSpan.Zero),token,stopAtGate:true,connected:connected);}
            finally{exited=true;}
        },deadline.Token);
        try
        {
            await connected.Task.WaitAsync(deadline.Token);deadline.Cancel();
            try{_=await run;}catch(OperationCanceledException){cancelled=true;}
            HostCheck.True(cancelled && exited && File.Exists(log) && !File.ReadAllText(log).Contains("TEST_PASS",StringComparison.Ordinal));
        }
        finally{deadline.Cancel();try{_=await run;}catch{}}
    }

    private static string DirectoryFor(string root)
    {
        string directory=Path.Combine(root,"artifacts","tests","socket-vectors-lifecycle-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);return directory;
    }

    private static async Task<ProcessResult> Stub(int port,string serial,ProcessResult result,CancellationToken token,
        bool stopAtGate=false,TaskCompletionSource? connected=null)
    {
        using var listener=new TcpListener(IPAddress.Loopback,port);listener.Start();uint ordinal=0,generation=0;
        await File.WriteAllTextAsync(serial,Call(288,6,-11),token);
        for(int index=0;index<4;index++)
        {
            using(var peer=await listener.AcceptSocketAsync(token))
            {
                generation++;await Append(Call(288,6,7));
                byte[] request=new byte[64];int received=0;
                await Read(index==0?3:64);
                if(stopAtGate)
                {
                    if(connected is not null){connected.TrySetResult();await Task.Delay(Timeout.Infinite,token);}
                    return result;
                }
                await Message(17,47,received);
                if(index==0){await Read(64);await Message(14,47,61);}
                if(index==1)
                {
                    bool reset=false;try{_=await peer.ReceiveAsync(new byte[1],SocketFlags.None,token);}
                    catch(SocketException error)when(error.SocketErrorCode==SocketError.ConnectionReset){reset=true;}
                    HostCheck.True(reset);
                }
                else if(index!=2)
                {
                    HostCheck.Equal(0,await peer.ReceiveAsync(new byte[1],SocketFlags.None,token));
                    int length=index==0?65536:64;byte[] response=new byte[length];
                    for(int i=0;i<length;i++)response[i]=(byte)(i%251);
                    for(int sent=0;sent<length;){int bytes=await peer.SendAsync(response.AsMemory(sent),SocketFlags.None,token);HostCheck.True(bytes>0);sent+=bytes;}
                    peer.Shutdown(SocketShutdown.Send);
                }
                async Task Read(int end)
                {
                    while(received<end)
                    {
                        int bytes=await peer.ReceiveAsync(request.AsMemory(received,end-received),SocketFlags.None,token);HostCheck.True(bytes>0);
                        for(int i=0;i<bytes;i++)HostCheck.Equal((byte)((index*37+(received+i)*11)%251),request[received+i]);
                        received+=bytes;
                    }
                }
            }
            await Append(Call(3,7,0));
        }
        return result;

        Task Append(string value)=>File.AppendAllTextAsync(serial,value,token);
        string Call(uint number,uint fd,long returned,ulong argument1=0)
        {
            ordinal++;ulong value=unchecked((ulong)returned);
            return $"M16_008_CALL identity=4294967297 nr={number} a0={fd} a1={argument1} a2=0 a3=0 a4=0 a5=0 rip=4194818 rsp=1879048176 flags=514 fs=0 gs=0 clock=10 valueBits={value} action=1 status=1 ordinal={ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={value}\r\n";
        }
        Task Message(uint first,uint second,int received)
        {
            uint next=ordinal+1;
            return Append($"M16_013_MESSAGE identity=4294967297 ordinal={next} nr=47 fd=7 generation={generation} header=8192 flags=0 table=12288 vectors=2 total={first+second}\r\n"+
                $"M16_013_VECTOR identity=4294967297 ordinal={next} index=0 address=16384 bytes={first}\r\n"+
                $"M16_013_VECTOR identity=4294967297 ordinal={next} index=1 address={16384+first} bytes={second}\r\n"+Call(47,7,received,8192));
        }
    }

    private static void Reject(Action action,string reason)
    {
        string? failure=null;try{action();}catch(ToolFailure error){failure=error.Reason;}
        HostCheck.Equal(reason,failure??"accepted");
    }
}
