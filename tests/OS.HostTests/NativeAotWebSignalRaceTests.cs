using OS.Tooling;

internal static class NativeAotWebSignalRaceTests
{
    // Copied synthetic scalar observations only, not an owned guest capture.
    private static readonly QemuTranscripts.WebSignalRaceSelection Selection=new(1,1,3,2,2);
    internal static QemuTranscripts.WebSignalRaceReport Run(QemuTranscripts.WebSignalRaceSelection? selection=null,
        bool omitRepark=false,bool wrongRecovery=false,bool leaveOpen=false,bool repeatPipe=false,Action<int>? originalWait=null,
        bool controls=false,bool extraSocket=false,bool missingAfter=false,bool lateRetirement=false,bool unaccepted=false,
        Action<int>? originalSocket=null,Action<NativeAotThreadingTranscriptTests.Vector,string>? phase=null,int pipeBase=7)
    {
        var replay=NativeAotWebReplayTests.InspectProbe(v=>
        {
            if(extraSocket)
            {
                string? retired=null;
                if(lateRetirement)
                {
                    retired=NativeAotWebTransportTests.Retire(0,2,41006,NativeAotWebTransportTests.Timeout+1);
                    HostCheck.True(v.Rows.Remove(retired));
                }
                if(missingAfter)
                {
                    int at=v.Rows.FindLastIndex(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal)&&r.Contains(" nr=45 ",StringComparison.Ordinal)&&r.Contains(" a0=7 ",StringComparison.Ordinal));
                    HostCheck.True(at>=0);string old=v.Rows[at];
                    v.Rows[at]=old.Replace("valueBits="+(NativeAotWebTransportTests.Report().Sockets.After.RequestHex.Length/2)+" ",
                        "valueBits="+unchecked((ulong)-11L).ToString(System.Globalization.CultureInfo.InvariantCulture)+" ",StringComparison.Ordinal);
                    HostCheck.True(v.Rows[at]!=old);
                }
                v.Rows.Add(NativeAotWebTransportTests.Fresh(7,1,42000,NativeAotWebTransportTests.Timeout+(lateRetirement?0UL:2UL)));
                if(!unaccepted)
                {originalSocket?.Invoke(v.Rows.Count);v.Call(288,7,[6,0x500700,0x500780,0x80000]);v.Call(45,1,[7,0x500800,1,0,0,0]);v.Call(44,1,[7,0x500800,1,0,0,0]);v.Call(3,0,[7]);}
                if(retired is not null)v.Rows.Add(retired);
            }
            phase?.Invoke(v,"before-probe");
            const ulong b=0x500800,frame=0x6ffffb78,stack=0x6ffffff0;
            ulong read=(ulong)pipeBase,write=read+1,epoll=read+2;
            v.Call(293,0,[0x500500,0x80800]);v.Call(291,(long)epoll,[0x80000]);v.Call(233,0,[epoll,1,read,0x500300]);
            v.Call(13,0,[12,0x500400,0,8]);var actor=v.Clone(3,3);v.Call(186,1);
            originalWait?.Invoke(v.Rows.Count);v.Call(232,0,[epoll,b,1,1],3,3,1_000_020);v.Drain(30);v.Supervisor(3,1,3);v.Select(actor);
            v.Call(186,3);v.Call(1,1,[write,b,1]);
            v.Rows.Add($"M16_012_WAKE identity={v.Main.Id} sequence=1 completion=1 state=1 clock=30");
            v.Call(234,0,[1,1,12]);v.Call(0,1,[read,b,1]);v.Irq(40,2,3);v.Select(v.Main);
            if(omitRepark)return;
            v.Rows.Add($"M16_012_REPARK identity={v.Main.Id} previous=1 sequence=2 deadline=1000020 clock=40");
            v.Rows.Add($"M16_012_WAKE identity={v.Main.Id} sequence=2 completion=5 state=5 clock=40");
            v.Main.Sequence=2;v.Supervisor(3,2,3);v.Select(actor);v.Irq(50,2,3);v.Select(v.Main);
            v.Rows.Add($"M16_012_COMPLETE identity={v.Main.Id} sequence=2 completion=5 valueBits={unchecked((ulong)-4L)} clock=50");
            v.Resume(-4);v.Main.Frame=frame;v.Main.Depth=1;v.Main.Mask=1UL<<11;v.Main.Rsp=frame-64;v.Call(186,1);
            v.Main.Rsp=frame+8;v.Main.Frame=v.Main.Depth=v.Main.Mask=0;v.Call(15,-4,action:5,returnedRsp:stack);v.Main.Rsp=stack;
            v.Wake(0x500200);v.Call(232,0,[epoll,b,1,1],3,3,1_000_050);v.Drain(60);v.Supervisor(3,2,3);
            v.Select(actor);v.Call(60,0,[0],4);v.Drain(60);v.Exit(4,1,3);
            // The original synthetic worker2 was parked on the woken futex.
            var callback=new NativeAotThreadingTranscriptTests.Thread(2,2)
                {Fs=0x10017e800,Rsp=0x10017dfc0,Bottom=0x100160000,Top=0x10017e000,Ordinal=1,Sequence=1};
            v.Select(callback);v.Resume(0);v.Call(186,2);v.Call(186,2);
            phase?.Invoke(v,"before-recovery-write");
            v.Call(1,1,[wrongRecovery?4:write,b,1]);
            v.Rows.Add($"M16_012_WAKE identity={v.Main.Id} sequence=3 completion=1 state=1 clock=60");
            v.Irq(70,2,2);v.Select(v.Main);
            v.Rows.Add($"M16_012_COMPLETE identity={v.Main.Id} sequence=3 completion=1 valueBits=1 clock=70");
            v.Resume(1);v.Call(0,1,[read,b,1]);
            if(!leaveOpen){v.Call(3,0,[epoll]);v.Call(3,0,[write]);v.Call(3,0,[read]);}
            if(repeatPipe){v.Call(293,0,[0x500500,0x80800]);v.Call(3,0,[write]);v.Call(3,0,[read]);}
            phase?.Invoke(v,"after-cleanup");
        },signalRace:selection??Selection,controls:controls);
        HostCheck.True(replay.SignalRace is not null);return replay.SignalRace!;
    }

    private static void Reject(Action action)
    {bool failed=false;try{action();}catch(ToolFailure){failed=true;}HostCheck.True(failed);}

    [HostTest("web race projection retains original row indices descriptor generations and optional native reaps")]
    private static void Projection()
    {
        int original=-1;var r=Run(originalWait:i=>original=i);HostCheck.Equal(0x100000001UL,r.Waiter.Identity);HostCheck.Equal(0x100000003UL,r.Actor.Identity);
        HostCheck.Equal(0x100000002UL,r.Callback.Identity);HostCheck.Equal(r.Callback.Identity,r.RecoveryWriter.Identity);
        HostCheck.True(r.Actor.CloneRecordIndex is not null && r.Actor.ExitRecordIndex is not null && r.Actor.ReapRecordIndex is not null);
        HostCheck.True(r.Waiter.ExitRecordIndex is null && r.Waiter.ReapRecordIndex is null);
        HostCheck.Equal(7,r.ReadDescriptor.Descriptor);HostCheck.Equal(8,r.WriteDescriptor.Descriptor);HostCheck.Equal(9,r.EpollDescriptor.Descriptor);
        HostCheck.Equal(3U,r.ReadDescriptor.Generation);HostCheck.Equal(1U,r.WriteDescriptor.Generation);
        HostCheck.True(r.Steps.Count>=17 && r.Steps.Zip(r.Steps.Skip(1)).All(p=>p.First.RecordIndex<p.Second.RecordIndex));
        HostCheck.Equal(original,r.Steps[0].RecordIndex);
        HostCheck.True(r.SocketCalls.Count==0);
        var array=r.Steps.ToArray();array[0]=default;HostCheck.True(r.Steps[0].Kind=="initial-wait");
    }

    [HostTest("web race copied selectors cannot invent process target callback or recovery writer identities")]
    private static void Selectors()
    {
        foreach(var s in new[]{Selection with{Pid=2},Selection with{WaiterTid=3},Selection with{ActorTid=1},
            Selection with{CallbackTid=99},Selection with{RecoveryWriterTid=3}})Reject(()=>Run(s));
    }

    [HostTest("web race requires completed repark interrupt recovery and explicit descriptor cleanup")]
    private static void Incomplete()
    {
        Reject(()=>Run(omitRepark:true));Reject(()=>Run(wrongRecovery:true));Reject(()=>Run(leaveOpen:true));
    }

    [HostTest("web race rejects multiple probe generations instead of silently choosing matching descriptor numbers")]
    private static void Generations()=>Reject(()=>Run(repeatPipe:true));

    [HostTest("web race replay admits later socket only after exact ordinary controls then completes actual copied race projection")]
    private static void LaterSocket()
    {
        int original=-1;var report=Run(controls:true,extraSocket:true,originalSocket:i=>original=i);
        HostCheck.True(report.Steps.Count>=17&&report.Selection==Selection&&report.Actor.ReapRecordIndex is not null);
        HostCheck.Equal(3,report.SocketCalls.Count);var accepted=report.SocketCalls[0];var received=report.SocketCalls[1];var sent=report.SocketCalls[2];
        HostCheck.True(accepted.CallRecordIndex==original&&accepted.Accept&&accepted.Number==288&&accepted.Bytes==0&&accepted.Offset==0&&
            accepted.Identity==0x100000001&&accepted.Ordinal>0&&accepted.Descriptor==7&&accepted.DescriptorGeneration>0&&accepted.OpenDescription>=0);
        HostCheck.True(accepted.Connection.RemotePort==42000&&accepted.Connection.LocalPort==8080&&accepted.Connection.Generation==1&&
            report.SocketCalls.All(c=>c.Connection==accepted.Connection&&c.DescriptorGeneration==accepted.DescriptorGeneration&&c.OpenDescription==accepted.OpenDescription));
        HostCheck.True(received.CallRecordIndex>accepted.CallRecordIndex&&sent.CallRecordIndex>received.CallRecordIndex&&
            received.Ordinal==accepted.Ordinal+1&&sent.Ordinal==received.Ordinal+1&&received.Number==45&&sent.Number==44&&
            !received.Peek&&!received.Response&&sent.Response&&received.Offset==0&&sent.Offset==0&&received.Bytes==1&&sent.Bytes==1&&
            accepted.Sent==0&&accepted.Received==0&&received.Sent==0&&received.Received==1&&sent.Sent==1&&sent.Received==1);
        bool immutable=false;try{((IList<QemuTranscripts.WebSignalRaceSocketCall>)report.SocketCalls).Clear();}catch(NotSupportedException){immutable=true;}HostCheck.True(immutable);
        Reject(()=>Run(controls:true,extraSocket:true,missingAfter:true));
        Reject(()=>Run(controls:true,extraSocket:true,lateRetirement:true));
        Reject(()=>Run(controls:true,extraSocket:true,unaccepted:true));
    }
}
