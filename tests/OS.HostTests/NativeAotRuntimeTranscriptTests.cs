using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OS.Tooling;
using Vector=NativeAotThreadingTranscriptTests.Vector;
using Thread=NativeAotThreadingTranscriptTests.Thread;

internal static class NativeAotRuntimeTranscriptTests
{
    // Independent complete observations, not guest captures or sealed authority.
    private sealed class BroadVector
    {
        internal readonly Vector Core=new(true);
        internal readonly StringBuilder Stdout=new();
        internal readonly string App;
        internal BroadVector(string app)
        {
            App=app;Core.Start();
            Core.Rows[1]=Core.Rows[1].Replace("entropyBytes=32","entropyBytes=256",StringComparison.Ordinal);
            int size=app=="NativeAotThreadPoolStress"?1627408:app=="NativeAotRuntimeMatrix"?1577616:1576912;
            Core.Rows[2]=Core.Rows[2].Replace("packageBytes=1577712","packageBytes="+size.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal);
            Core.Rows[4]=Core.Rows[4].Replace("workers=8","workers=16",StringComparison.Ordinal);
            for(int i=0;i<7;i++)Core.Call(318,16,[0x500600,16,0]); // Exercise the actual larger entropy contract.
        }
        internal void Write(string line)
            =>WriteBytes(line+"\n");
        internal void WriteBytes(string text)
        {
            int bytes=Encoding.UTF8.GetByteCount(text);Stdout.Append(text);Core.Lengths.Add(bytes);
            string data=text.Replace("\r","",StringComparison.Ordinal).Replace("\n","",StringComparison.Ordinal);
            Core.Rows.Add($"M16_005_WRITE fd=1 bytes={bytes} data={data}");Core.Call(1,bytes,[1,0x500400,(uint)bytes]);
        }
        internal QemuTranscripts.ThreadingProtocolReport Inspect(string[]? rows=null,string? text=null)=>
            QemuTranscripts.InspectNativeAotRuntimeProtocolForTests(App,rows??Core.Rows.ToArray(),NativeAotThreadingTranscriptTests.Elf(),
                r=>r==0x400202,r=>r==0x400202,Core.Lengths,text??Stdout.ToString(),8192,12288);
    }

    private static void Activation(BroadVector b,Thread cpu,ulong time,int active)
    {
        var v=b.Core;v.Call(200,0,[(uint)cpu.Tid,35]);v.Wait(0x500230);v.Drain(time);v.Supervisor(3,1,active);
        v.Select(cpu);cpu.Frame=cpu.Top-1160;cpu.Depth=1;cpu.Mask=1UL<<34;cpu.Rsp=cpu.Frame-64;
        v.Call(186,cpu.Tid);v.Wake(0x500230);cpu.Rsp=cpu.Frame+8;cpu.Depth=cpu.Frame=cpu.Mask=0;
        v.Call(15,0,action:5,returnedRsp:cpu.Top-64);cpu.Rsp=cpu.Top-64;v.Irq(time+1,2,active);
        v.Select(v.Main);v.Resume(0);
    }

    private static BroadVector Suspension()
    {
        BroadVector b=new("NativeAotSuspensionStress");var v=b.Core;b.Write("TEST_START nativeaot.gc-stress");
        Thread final=v.Clone(2,2),signal=v.Clone(3,3);
        for(int round=1;round<=3;round++)
        {
            Thread[] workers=new Thread[5];for(int i=0;i<5;i++)workers[i]=v.Clone(4+i,4+(round-1)*5+i,(uint)round);
            ulong basis=(uint)(round-1)*1_000_000_000UL;v.Irq(basis+10,round==1?8:6,8);
            if(round==1)
            {
                v.Select(final);v.Wait(0x500200);v.Drain(20);v.Supervisor(3,7,8);
                v.Select(signal);v.Call(0,0,[3,0x500700,1],3,2);v.Drain(30);v.Supervisor(3,6,8);
            }
            v.Select(workers[0]);v.Irq(basis+40,6,8);
            for(int i=1;i<5;i++)
            {
                v.Select(workers[i]);v.Wait(0x500240+(uint)i*4,i==2?basis+1_000_000_000:0);
                v.Drain(basis+40+(uint)i);v.Supervisor(3,6-i,8);
            }
            v.Select(v.Main);
            for(int epoch=1;epoch<=3;epoch++)
            {
                Activation(b,workers[0],basis+40+(uint)epoch*10,8);
                int before=(round-1)*3+epoch-1,progress=epoch*10;
                b.Write($"NATIVEAOT_GC_STRESS_COLLECT lifetime={round} epoch={epoch} before={before} after={before+1} progressBefore={progress} progressReturned={progress+1} progressAfter={progress+2} waiting=3");
            }
            foreach(int i in new[]{1,3,4})v.Wake(0x500240+(uint)i*4);
            v.Wait(0x500270);v.Drain(basis+80);v.Supervisor(3,4,8);
            int ready=3,active=8;
            foreach(int i in new[]{0,1,3,4})
            {
                v.Select(workers[i]);if(i!=0)v.Resume(0);v.Call(60,0,[0],4);v.Drain(basis+80);v.Exit(4,ready--,active--);
            }
            v.Tick(basis+1_000_000_000,1);v.Select(workers[2]);v.Resume(-110);v.Wake(0x500270);
            v.Call(60,0,[0],4);v.Drain(v.Clock);v.Exit(4,1,4);v.Select(v.Main);v.Resume(0);
            b.Write($"NATIVEAOT_GC_STRESS_JOIN lifetime={round} started=5 joined=5 completed=5 roots=5 interior=5 tls=5 sleepStarts=1 exitRequests=1");
        }
        b.Write("TEST_PASS nativeaot.gc-stress");v.Call(231,0,[0],2);v.Drain(v.Clock);v.Exit(2,2,3);
        v.Select(final);v.Exit(4,1,2);v.Select(signal);v.Exit(4,0,1);
        v.Rows.Add("M16_008_BUDGET_END nanos=3000000001");v.End(18);return b;
    }

    private static BroadVector Matrix(bool lateNativeExit=false,bool wakeTimers=false,bool clockEvidence=true)
    {
        BroadVector b=new("NativeAotRuntimeMatrix");var v=b.Core;b.Write("TEST_START nativeaot.runtime-matrix");
        Thread[] background=new Thread[6];for(int i=0;i<6;i++)background[i]=v.Clone(2+i,2+i);
        for(int round=1;round<=3;round++)
        {
            Thread cpu=v.Clone(8,8+(round-1)*2,(uint)round),gate=v.Clone(9,9+(round-1)*2,(uint)round);
            ulong basis=(uint)(round-1)*1_000_000_000UL;v.Irq(basis+10,round==1?9:3,9);
            if(round==1)for(int i=0;i<6;i++)
            {
                v.Select(background[i]);
                if(i==1)v.Call(0,0,[3,0x500700,1],3,2);
                else v.Wait(0x500280+(uint)i*4,i==2 && !wakeTimers?100_000_000UL:0);
                v.Drain(20+(uint)i);v.Supervisor(3,8-i,9);
            }
            v.Select(cpu);v.Irq(basis+40,3,9);v.Select(gate);v.Wait(0x500260);v.Drain(basis+41);v.Supervisor(3,2,9);v.Select(v.Main);
            b.Write($"NATIVEAOT_MATRIX_EH round={round} filters=1 catches=2 cleanups=2 order=12 identity=true");
            b.Write($"NATIVEAOT_MATRIX_CCTOR round={round} attempts=2 total={round*2} runs=1 typed=true innerIdentity=true");
            b.Write($"NATIVEAOT_MATRIX_DELEGATES round={round} closed={round+50} closure={round+47} calls=2 actions=3 order=122 sum=19");
            Activation(b,cpu,basis+50,9);Activation(b,cpu,basis+60,9);
            v.Wake(0x500260);v.Wait(0x500270);v.Drain(basis+80);v.Supervisor(3,2,9);
            v.Select(cpu);v.Call(60,0,[0],4);v.Drain(basis+80);v.Exit(4,1,9);
            v.Select(gate);v.Resume(0);v.Wake(0x500270);
            if(lateNativeExit)v.Irq(basis+80,2,8);
            else {v.Call(60,0,[0],4);v.Drain(basis+80);v.Exit(4,1,8);}
            v.Select(v.Main);v.Resume(0);int before=round*3-2,progress=round*100;
            b.Write($"NATIVEAOT_MATRIX_GC round={round} collections=2 before={before} after={before+2} workers=2 joined=2 freshTls=2 roots=2 interior=2 tls=2 progressBefore={progress} progressReturned={progress+10} progressAfter={progress+20} finalizedMask={(1<<round)-1} duplicateFinalizers=0");
            if(lateNativeExit)
            {
                v.Irq(basis+80,2,8);v.Select(gate);v.Call(60,0,[0],4);v.Drain(basis+80);v.Exit(4,1,8);v.Select(v.Main);
            }
            Thread timer=background[2];
            for(int timerRound=1;timerRound<=2;timerRound++)
            {
                ulong now=basis+(uint)timerRound*100_000_000UL;
                if(wakeTimers)
                {
                    v.Irq(now-40_000_000,1,7);v.Select(v.Main);
                    if(clockEvidence)v.Call(228,0,[6,0x500600]);
                    v.Wake(0x500288);v.Wait(0x500234);v.Drain(now);v.Supervisor(3,1,7);
                    v.Select(timer);v.Resume(0);if(clockEvidence)v.Call(228,0,[6,0x500600]);
                    v.Wake(0x500234);v.Wait(0x500288);v.Drain(now);v.Supervisor(3,1,7);v.Select(v.Main);v.Resume(0);
                    continue;
                }
                v.Wait(0x500234);v.Drain(v.Clock);v.Supervisor(3,0,7);v.Tick(now,1);
                v.Select(timer);v.Resume(-110);v.Wake(0x500234);
                ulong next=timerRound==1?basis+200_000_000:round==3?0:basis+1_100_000_000;
                v.Wait(0x500288,next);v.Drain(now);v.Supervisor(3,1,7);v.Select(v.Main);v.Resume(0);
            }
            b.Write($"NATIVEAOT_MATRIX_ASYNC round={round} tasks=4 completed=4 checksum=30 workers=2 canceledBefore=1 canceledDuring=1 workCallbacks=0 delayBefore={basis+50_000_000} delayAfter={basis+100_000_000} timerBefore={basis+120_000_000} fired={basis+200_000_000} drained={basis+210_000_000} after={basis+250_000_000} frequency=1000000000 callbacks=1 stale=0 disposed=true");
        }
        b.Write("TEST_PASS nativeaot.runtime-matrix");v.Call(231,0,[0],2);v.Drain(v.Clock);v.Exit(2,6,7);
        int ready=5,active=6;foreach(var t in background){v.Select(t);v.Exit(4,ready--,active--);}
        v.Rows.Add("M16_008_BUDGET_END nanos=2300000000");v.End(13);return b;
    }

    [HostTest("nativeaot.runtime complete GC stress and matrix leaves consume real activation frames deadline wins fresh generations and reaps")]
    private static void Collections()
    {
        var gc=Suspension().Inspect();HostCheck.True(gc.Clones==17 && gc.Reaped==18 && gc.SignalReturns==9 && gc.Ticks==3);
        var matrix=Matrix().Inspect();HostCheck.True(matrix.Clones==12 && matrix.Reaped==13 && matrix.SignalReturns==6 && matrix.Ticks==6);
    }

    [HostTest("nativeaot.runtime timestamped matrix phases accept due-timer clock wake resume intervals without inventing deadlines")]
    private static void DueMatrixTimers()
    {
        var due=Matrix(wakeTimers:true);var report=due.Inspect();
        HostCheck.Equal(0,report.Ticks);HostCheck.Equal(13,report.Reaped);
        var missing=Matrix(wakeTimers:true,clockEvidence:false);
        Reject(missing,missing.Core.Rows.ToArray(),reason:"runtime_timer_kernel_evidence");
        const string before="delayBefore=50000000",outside="delayBefore=70000000";
        string text=due.Stdout.ToString().Replace(before,outside,StringComparison.Ordinal);
        var rows=due.Core.Rows.Select(r=>r.Replace(before,outside,StringComparison.Ordinal)).ToArray();
        Reject(due,rows,text,reason:"runtime_timer_kernel_evidence");
    }

    [HostTest("nativeaot.runtime phase checks require kernel collection and deadline evidence even when all app output is valid")]
    private static void MissingKernelEvidence()
    {
        foreach(var complete in new[]{Suspension(),Pool(),Matrix()})
        {
            BroadVector empty=new(complete.App);empty.WriteBytes(complete.Stdout.ToString());
            string expected=complete.App=="NativeAotThreadPoolStress"?"runtime_timer_kernel_evidence":"runtime_collection_kernel_evidence";
            bool rejected=false;try{empty.Inspect();}catch(ToolFailure failure){rejected=failure.Message==expected;}
            HostCheck.True(rejected,"complete output cannot invent kernel phase evidence");
        }
    }

    [HostTest("nativeaot.runtime coalesced phase rows and arbitrary write splits retain distinct signal deadline and reap observations")]
    private static void Coalesced()
    {
        foreach(var b in new[]{Suspension(),Pool(),Matrix(),Matrix(wakeTimers:true)})
        {
            var v=b.Core;string text=b.Stdout.ToString();int bytes=Encoding.UTF8.GetByteCount(text);
            string original=v.Rows.First(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) && r.Contains(" nr=1 a0=1 ",StringComparison.Ordinal));
            v.Rows.RemoveAll(r=>r.StartsWith("M16_005_WRITE ",StringComparison.Ordinal) ||
                r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) && r.Contains(" nr=1 a0=1 ",StringComparison.Ordinal));
            int at=v.Rows.FindIndex(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) && r.Contains(" nr=231 ",StringComparison.Ordinal));
            byte[] data=Encoding.UTF8.GetBytes(text);int[] lengths=[1,bytes/2,bytes-bytes/2-1];int offset=0;
            v.Lengths.Clear();
            foreach(int count in lengths)
            {
                string part=Encoding.UTF8.GetString(data,offset,count).Replace("\r","",StringComparison.Ordinal).Replace("\n","",StringComparison.Ordinal);offset+=count;
                v.Rows.Insert(at++,$"M16_005_WRITE fd=1 bytes={count} data={part}");
                string call=original;
                foreach(var value in new[]{("a2",(ulong)count),("valueBits",(ulong)count),("returnValueBits",(ulong)count),("clock",v.Clock)})
                    call=Regex.Replace(call,@"\b"+value.Item1+"=[0-9]+",value.Item1+"="+value.Item2.ToString(CultureInfo.InvariantCulture));
                v.Rows.Insert(at++,call);v.Lengths.Add(count);
            }
            Dictionary<string,int> ordinals=[];
            for(int i=0;i<v.Rows.Count;i++)if(v.Rows[i].StartsWith("M16_008_CALL ",StringComparison.Ordinal))
            {
                string identity=Regex.Match(v.Rows[i],@"\bidentity=([0-9]+)").Groups[1].Value;
                int ordinal=ordinals.GetValueOrDefault(identity)+1;ordinals[identity]=ordinal;
                v.Rows[i]=Regex.Replace(v.Rows[i],@"\bordinal=[0-9]+","ordinal="+ordinal.ToString(CultureInfo.InvariantCulture));
            }
            HostCheck.True(b.Inspect().Reaped>=6);
        }
    }

    private static BroadVector Pool()
    {
        BroadVector b=new("NativeAotThreadPoolStress");var v=b.Core;b.Write("TEST_START nativeaot.tp-stress");
        Thread final=v.Clone(2,2),signal=v.Clone(3,3),timer=v.Clone(4,4),first=v.Clone(5,5),second=v.Clone(6,6);
        v.Irq(10,6,6);v.Select(final);v.Wait(0x500200);v.Drain(20);v.Supervisor(3,5,6);
        v.Select(signal);v.Call(0,0,[3,0x500700,1],3,2);v.Drain(30);v.Supervisor(3,4,6);
        v.Select(timer);v.Wait(0x500210,100_000_000);v.Drain(40);v.Supervisor(3,3,6);
        v.Select(first);v.Wait(0x500214);v.Drain(50);v.Supervisor(3,2,6);
        v.Select(second);v.Wait(0x500218);v.Drain(60);v.Supervisor(3,1,6);v.Select(v.Main);
        for(int round=1;round<=3;round++)
        {
            b.Write($"NATIVEAOT_TP_STRESS_WORK lifetime={round} tasks=8 completed=8 checksum=204 activeMax=2 inFlight=0 mask=255 workers=2 threadPool=true");
            b.Write($"NATIVEAOT_TP_STRESS_CANCEL lifetime={round} before=2 during=2 workCallbacks=0 recovered=1");
            b.Write($"NATIVEAOT_TP_STRESS_RACE lifetime={round} outcome=result winners=1 duplicates=2 continuations=3 inline=0 value=43");
            ulong now=(uint)round*100_000_000UL;
            v.Wait(0x500230);v.Drain(v.Clock);v.Supervisor(3,0,6);v.Tick(now,1);
            v.Select(timer);v.Resume(-110);v.Wake(0x500230);v.Wait(0x500210,round==3?0:now+100_000_000);
            v.Drain(now);v.Supervisor(3,1,6);v.Select(v.Main);v.Resume(0);
            b.Write($"NATIVEAOT_TP_STRESS_TIMER lifetime={round} before={now-50_000_000} fired={now} drained={now+20_000_000} after={now+50_000_000} frequency=1000000000 callbacks=1 completed=1 stale=0 never=0 pendingDrain=true");
        }
        b.Write("TEST_PASS nativeaot.tp-stress");v.Call(231,0,[0],2);v.Drain(v.Clock);v.Exit(2,5,6);
        Thread[] rest=[final,signal,timer,first,second];int ready=4,active=5;
        foreach(var t in rest){v.Select(t);v.Exit(4,ready--,active--);}
        v.Rows.Add("M16_008_BUDGET_END nanos=350000000");v.End(6);return b;
    }

    [HostTest("nativeaot.runtime outer framing preserves original inner bytes and rejects wrong duplicate missing or trailing profile records")]
    private static void Envelope()
    {
        foreach (var b in new[] { Suspension(), Pool(), Matrix() })
        {
            var m = NativeAotRuntimeSelection.Markers(b.App);
            byte[] inner = Raw(b);
            string text = "PARENT\r\n" + m.Start + "\r\n" + Encoding.UTF8.GetString(inner) + m.Ready + "\r\n" + m.Pass + "\r\n";
            string normalized = QemuTranscripts.InspectNativeAotRuntimeEnvelopeForTests(b.App, Encoding.UTF8.GetBytes(text));
            HostCheck.True(normalized.StartsWith("PARENT\r\n" + m.Start + "\r\nTEST_START m16-009\r\n", StringComparison.Ordinal));
            HostCheck.True(normalized.EndsWith("TEST_PASS m16-009\r\n" + m.Ready + "\r\n" + m.Pass + "\r\n", StringComparison.Ordinal));
            foreach (string broken in new[] { text.Replace(m.Start + "\r\n", "", StringComparison.Ordinal),
                text.Replace(m.Start, m.Start + "\r\n" + m.Start, StringComparison.Ordinal),
                text.Replace(m.Ready, "CLROS_M16_009_READY", StringComparison.Ordinal),
                text.Replace(m.Pass + "\r\n", "", StringComparison.Ordinal), text + "trailing\r\n" })
            {
                bool rejected = false;
                try { QemuTranscripts.InspectNativeAotRuntimeEnvelopeForTests(b.App, Encoding.UTF8.GetBytes(broken)); }
                catch (ToolFailure) { rejected = true; }
                HostCheck.True(rejected);
            }
        }
    }

    [HostTest("nativeaot.runtime complete ThreadPool leaf binds larger entropy budget three genuine timer deadlines and actual cleanup")]
    private static void Complete()
    {
        var b=Pool();var report=b.Inspect();HostCheck.True(report.Clones==5 && report.Reaped==6 && report.Ticks==3 && report.Irqs==1);
        byte[] raw=Raw(b);
        var capture=NativeAotThreadingOutput.Read(b.App,raw);HostCheck.Equal(b.Stdout.ToString(),capture.Stdout);
        HostCheck.True(capture.NormalizedTail==string.Join("\r\n",b.Core.Rows)+"\r\n");
        bool rejected=false;try{QemuTranscripts.ValidateNativeAotRuntimeTail("",[],null!,raw,8192,12288);}catch(ToolFailure){rejected=true;}
        HostCheck.True(rejected,"value-only rows cannot replace sealed program authority");
    }
    [HostTest("nativeaot.runtime managed matrix joins may precede native wrapper exits but every real normal reap is mandatory")]
    private static void LateNativeMatrixExit()
    {
        var b=Matrix(lateNativeExit:true);var report=b.Inspect();
        HostCheck.True(report.Reaped==13 && report.SignalReturns==6);
        int at=b.Core.Rows.FindIndex(r=>r.StartsWith("M16_008_REAP old=4294967305 ",StringComparison.Ordinal));
        HostCheck.True(at>=0,"late native reap selector");
        Reject(b,b.Core.Rows.Where((_,i)=>i!=at).ToArray());
    }

    private static byte[] Raw(BroadVector b)
    {
        using MemoryStream raw=new();
        foreach(string row in b.Core.Rows)
        {
            raw.Write(Encoding.UTF8.GetBytes(row));
            if(row.StartsWith("M16_005_WRITE ",StringComparison.Ordinal))raw.WriteByte(10);
            raw.Write("\r\n"u8);
        }
        return raw.ToArray();
    }

    [HostTest("nativeaot.runtime rejects focused policy substitution unbacked timer output invented clocks and expired complete time")]
    private static void Mutations()
    {
        var b=Pool();_=b.Inspect();
        foreach(var mutation in new[] { ("entropyBytes=256","entropyBytes=32"), ("workers=16","workers=8"),
            ("epochs=8192","epochs=128"), ("steps=16384","steps=512"), ("started=0","started=100000000"),
            ("M16_008_BUDGET_END nanos=350000000","M16_008_BUDGET_END nanos=300000000000"),
            ("M16_008_BUDGET_END nanos=350000000","M16_008_BUDGET_END nanos=300000000"),
            ("clock=100000000 valueBits=1","clock=100000001 valueBits=1") })
        {
            var rows=b.Core.Rows.Select(r=>r.Replace(mutation.Item1,mutation.Item2,StringComparison.Ordinal)).ToArray();
            HostCheck.True(!rows.SequenceEqual(b.Core.Rows),"mutation selector");Reject(b,rows);
        }
        int at=b.Core.Rows.FindIndex(r=>r.StartsWith("M16_008_TICK ",StringComparison.Ordinal));
        var forged=b.Core.Rows.ToArray();forged[at]=Regex.Replace(forged[at],@"nanos=[0-9]+","nanos=99999999");Reject(b,forged);
        var text=b.Stdout.ToString().Replace("frequency=1000000000","frequency=1000000001",StringComparison.Ordinal);
        Reject(b,b.Core.Rows.ToArray(),text);
        Reject(b,b.Core.Rows.Where(r=>!r.StartsWith("M16_008_BUDGET ",StringComparison.Ordinal)).ToArray());
        Reject(b,b.Core.Rows.Where(r=>!r.StartsWith("M16_008_BUDGET_END ",StringComparison.Ordinal)).ToArray());
    }
    private static void Reject(BroadVector b,string[] rows,string? text=null,string? reason=null)
    {
        bool rejected=false;try{b.Inspect(rows,text);}catch(ToolFailure failure){rejected=reason is null || failure.Reason==reason;}
        HostCheck.True(rejected,"broader runtime mutation admitted or wrong rejection");
    }
}
