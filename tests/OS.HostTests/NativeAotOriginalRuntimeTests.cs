using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Manifest;
using OS.Tooling;
using Vector=NativeAotThreadingTranscriptTests.Vector;
using Thread=NativeAotThreadingTranscriptTests.Thread;

internal static class NativeAotOriginalRuntimeTests
{
    private const string App="NativeAotRuntimeSmoke";
    private const string Stdout="TEST_START nativeaot.runtime\nNATIVEAOT_RUNTIME_EH_OK\n"+
        "NATIVEAOT_RUNTIME_GC_THREADS_TLS_OK\nNATIVEAOT_RUNTIME_TASK_TIMER_OK\nTEST_PASS nativeaot.runtime\n";
    private static readonly string[] Sources=["global.json","Directory.Build.props","Directory.Build.targets","tools/Directory.Build.props",
        "tools/OS.Tooling/NativeAotPayload.cs","apps/NativeAotRuntimeSmoke/Program.cs","apps/NativeAotRuntimeSmoke/NativeAotRuntimeSmoke.csproj"];

    [HostTest("nativeaot.original-runtime binds original seven raw producer inputs and rejects source additions and overrides")]
    private static void SourceAuthority()
    {
        string root=HostToolProcess.FindRepositoryRoot(),copy=Directory.CreateTempSubdirectory("clros-original-runtime-").FullName;
        try
        {
            foreach(string relative in Sources)
            {
                string path=Path.Combine(copy,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.Copy(Path.Combine(root,relative),path);
            }
            NativeAotRuntimePackage.RequireCurrentSources(root,App);NativeAotRuntimePackage.RequireCurrentSources(copy,App);
            foreach(string relative in Sources)
            {
                string path=Path.Combine(copy,relative);byte[] original=File.ReadAllBytes(path);File.AppendAllText(path,"\n");
                Reject(()=>NativeAotRuntimePackage.RequireCurrentSources(copy,App),"source_identity");File.WriteAllBytes(path,original);
            }
            string extra=Path.Combine(copy,"apps",App,"Extra.cs");File.WriteAllText(extra,"extra");
            Reject(()=>NativeAotRuntimePackage.RequireCurrentSources(copy,App),"source_inventory");File.Delete(extra);
            File.WriteAllText(Path.Combine(copy,"NuGet.Config"),"override");
            Reject(()=>NativeAotRuntimePackage.RequireCurrentSources(copy,App),"source_override");
        }
        finally{Directory.Delete(copy,true);}
    }

    [HostTest("nativeaot.original-runtime canonical distinct policy admits no matrix substitution or synthetic instruction authority")]
    private static void PackageAuthority()
    {
        string root=HostToolProcess.FindRepositoryRoot(),text=NativeAotRuntimePackage.Manifest(App);
        byte[] manifest=Encoding.ASCII.GetBytes(text);var view=NativeManifestParser.ParseLinuxElf(manifest);
        HostCheck.True(view.Succeeded && view.Threads==16 && view.MemoryBytes==67108864 && view.StackBytes==1572864 && view.Descriptors==16);
        HostCheck.True(text.Contains("app.path=apps/NativeAotRuntimeSmoke\n",StringComparison.Ordinal) &&
            text.Contains("digest=4d60bbc72bf797db5e8f449867816a64ac99fbf93a5736d1dd1d31287cda7553\n",StringComparison.Ordinal));
        HostCheck.True(!NativeAotRuntimePayload.Applications.Contains(App,StringComparer.Ordinal));
        Reject(()=>NativeAotRuntimePayload.ValidateOutput(App,Stdout),"app"); // The later producer remains unchanged.
        Reject(()=>NativeAotThreadingPackage.Manifest(App),"threading_package_app");
        byte[] elf=new byte[1511248];LinuxThreadProofFixture.CreateElf("Threads").CopyTo(elf,0);
        int digest=text.IndexOf("digest=",StringComparison.Ordinal)+7;
        byte[] counterfeit=Encoding.ASCII.GetBytes(text[..digest]+Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant()+text[(digest+64)..]);
        byte[] package=new byte[64+manifest.Length+elf.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(package,LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(8),1);BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(12),1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16),64);BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20),(uint)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24),(ulong)elf.Length);counterfeit.CopyTo(package,64);elf.CopyTo(package,64+manifest.Length);
        HostCheck.True(LinuxElfPackage.Validate(package).Succeeded,"independently valid counterfeit");
        Reject(()=>NativeAotRuntimePackage.RequirePackage(App,package),"identity");
        manifest.CopyTo(package,64);Reject(()=>NativeAotRuntimePackage.RequirePackage(App,package),"identity");
        Reject(()=>NativeAotRuntimeProgram.Create(root,App,package,"400200: 90\tnop\n"),"runtime_package_identity");
        Reject(()=>NativeAotRuntimePackage.RequirePackage("NativeAotRuntimeMatrix",package),"identity");
    }

    [HostTest("nativeaot.original-runtime missing original qualification cannot publish or overwrite an output")]
    private static void MissingEvidence()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Path.Combine(root,"artifacts","original-runtime-missing-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);string destination=Path.Combine(directory,"app.pkg");
        try
        {
            Reject(()=>NativeAotRuntimePackage.Write(root,directory,App,destination),"runtime_evidence_path");
            HostCheck.True(!Directory.EnumerateFileSystemEntries(directory).Any());File.WriteAllBytes(destination,[1,2,3]);
            Reject(()=>NativeAotRuntimePackage.Write(root,directory,App,destination),"output_exists");
            HostCheck.True(File.ReadAllBytes(destination).AsSpan().SequenceEqual(new byte[]{1,2,3}));
        }
        finally{Directory.Delete(directory,true);}
    }

    [HostTest("nativeaot.original-runtime exact five rows survive arbitrary raw stdout segmentation and CRLF")]
    private static void Output()
    {
        foreach(string text in new[]{Stdout,Stdout.Replace("\n","\r\n",StringComparison.Ordinal)})
        {
            NativeAotThreadingOutput.ValidateRuntimeSmokeOutput(text);
            using MemoryStream raw=new();byte[] bytes=Encoding.UTF8.GetBytes(text);
            foreach(byte value in bytes)
            {raw.Write("M16_005_WRITE fd=1 bytes=1 data="u8);raw.WriteByte(value);raw.Write("\r\n"u8);}
            var capture=NativeAotThreadingOutput.Read(App,raw.ToArray());HostCheck.Equal(text,capture.Stdout);
            HostCheck.True(capture.WriteLengths.Count==bytes.Length && capture.WriteLengths.All(n=>n==1));
        }
        foreach(string bad in new[]{Stdout[..^1],Stdout+"\n",Stdout.Replace("NATIVEAOT_RUNTIME_EH_OK\n","",StringComparison.Ordinal),
            Stdout.Replace("NATIVEAOT_RUNTIME_EH_OK","NATIVEAOT_RUNTIME_EH_OK\nNATIVEAOT_RUNTIME_EH_OK",StringComparison.Ordinal),
            Stdout.Replace("nativeaot.runtime","nativeaot.runtime-matrix",StringComparison.Ordinal),
            Stdout.Replace("NATIVEAOT_RUNTIME_GC_THREADS_TLS_OK\nNATIVEAOT_RUNTIME_TASK_TIMER_OK","NATIVEAOT_RUNTIME_TASK_TIMER_OK\nNATIVEAOT_RUNTIME_GC_THREADS_TLS_OK",StringComparison.Ordinal)})
            Reject(()=>NativeAotThreadingOutput.ValidateRuntimeSmokeOutput(bad),"runtime_smoke_output");
    }

    // Complete independent synthetic kernel observations, never a guest capture
    // or a way to obtain the sealed package/instruction factory authority.
    private sealed class SmokeVector
    {
        internal readonly Vector Core=new(true);
        internal readonly StringBuilder Text=new();
        internal SmokeVector(bool earlyTimer=false)
        {
            var v=Core.Start();v.Rows[1]=v.Rows[1].Replace("entropyBytes=32","entropyBytes=256",StringComparison.Ordinal);
            v.Rows[2]=v.Rows[2].Replace("packageBytes=1577712","packageBytes=1511248",StringComparison.Ordinal);
            v.Rows[4]=v.Rows[4].Replace("workers=8","workers=16",StringComparison.Ordinal);
            v.Rows.Insert(5,"M16_008_BUDGET started=0 epochs=8192 steps=16384 nanos=300000000000");
            Write("TEST_START nativeaot.runtime");Write("NATIVEAOT_RUNTIME_EH_OK");
            Thread final=v.Clone(2,2),signal=v.Clone(3,3),worker=v.Clone(4,4),pool=v.Clone(5,5),timer=v.Clone(6,6);
            v.Irq(10,6,6);v.Select(final);v.Wait(0x500200);v.Drain(20);v.Supervisor(3,5,6);
            v.Select(signal);v.Call(0,0,[3,0x500700,1],3,2);v.Drain(30);v.Supervisor(3,4,6);
            v.Select(worker);v.Wait(0x500210);v.Drain(40);v.Supervisor(3,3,6);
            v.Select(pool);v.Call(186,5);v.Wait(0x500214);v.Drain(50);v.Supervisor(3,2,6);
            v.Select(timer);v.Wait(0x500218,100);v.Drain(60);v.Supervisor(3,1,6);v.Select(v.Main);
            if(earlyTimer)FireTimer(6);
            v.Wake(0x500210);v.Wait(0x500234);v.Drain(v.Clock);v.Supervisor(3,1,6);
            v.Select(worker);v.Resume(0);v.Wake(0x500234);v.Call(60,0,[0],4);v.Drain(v.Clock);v.Exit(4,1,6);
            v.Select(v.Main);v.Resume(0);Write("NATIVEAOT_RUNTIME_GC_THREADS_TLS_OK");
            if(!earlyTimer)FireTimer(5);
            Write("NATIVEAOT_RUNTIME_TASK_TIMER_OK");Write("TEST_PASS nativeaot.runtime");
            v.Call(231,0,[0],2);v.Drain(v.Clock);v.Exit(2,4,5);
            int ready=3,active=4;foreach(var t in new[]{final,signal,pool,timer}){v.Select(t);v.Exit(4,ready--,active--);}
            v.Rows.Add("M16_008_BUDGET_END nanos=100");v.End(6);

            void FireTimer(int active)
            {
                v.Wait(0x500234);v.Drain(v.Clock);v.Supervisor(3,0,active);v.Tick(100,1);
                v.Select(timer);v.Resume(-110);v.Wake(0x500234);v.Wait(0x500218);v.Drain(100);v.Supervisor(3,1,active);
                v.Select(v.Main);v.Resume(0);
            }
        }
        internal void Write(string line)
        {
            string data=line+"\n";Text.Append(data);int bytes=Encoding.UTF8.GetByteCount(data);Core.Lengths.Add(bytes);
            Core.Rows.Add($"M16_005_WRITE fd=1 bytes={bytes} data={line}");Core.Call(1,bytes,[1,0x500400,(uint)bytes]);
        }
        internal QemuTranscripts.ThreadingProtocolReport Inspect(string[]? rows=null)=>
            QemuTranscripts.InspectNativeAotRuntimeProtocolForTests(App,rows??Core.Rows.ToArray(),NativeAotThreadingTranscriptTests.Elf(),
                r=>r==0x400202,r=>r==0x400202,Core.Lengths,Text.ToString(),8192,12288);
        internal byte[] Raw()
        {
            using MemoryStream raw=new();int offset=0,index=0;
            foreach(string row in Core.Rows)
            {
                string value=row;
                if(row.StartsWith("M16_005_WRITE ",StringComparison.Ordinal))
                {int count=Core.Lengths[index++];value=row[..(row.IndexOf(" data=",StringComparison.Ordinal)+6)]+Text.ToString(offset,count);offset+=count;}
                raw.Write(Encoding.UTF8.GetBytes(value+"\r\n"));
            }
            return raw.ToArray();
        }
    }

    [HostTest("nativeaot.original-runtime complete leaf requires normal worker cleanup and later timed completion without inventing activation signals")]
    private static void CompleteLeaf()
    {
        SmokeVector vector=new();var report=vector.Inspect();
        HostCheck.True(report.Clones==5 && report.Reaped==6 && report.SignalReturns==0 && report.Ticks==1 && report.Irqs==1);
        var raw=vector.Raw();var capture=NativeAotThreadingOutput.Read(App,raw);HostCheck.Equal(Stdout,capture.Stdout);
        HostCheck.Equal(string.Join("\r\n",vector.Core.Rows)+"\r\n",capture.NormalizedTail);
        Reject(()=>QemuTranscripts.ValidateNativeAotRuntimeTail("",[],null!,raw,8192,12288),"runtime_program_missing");
        Reject(()=>new SmokeVector(true).Inspect(),"runtime_smoke_timer_kernel_evidence");
        foreach(var change in new[]{("workers=16","workers=8"),("entropyBytes=256","entropyBytes=32"),
            ("packageBytes=1511248","packageBytes=1577616"),("M16_008_BUDGET_END nanos=100","M16_008_BUDGET_END nanos=99"),
            (" context=zero stack=zero slot=zero"," context=zero stack=dirty slot=zero")})
            Reject(()=>vector.Inspect(vector.Core.Rows.Select(r=>r.Replace(change.Item1,change.Item2,StringComparison.Ordinal)).ToArray()));
    }

    [HostTest("nativeaot.original-runtime coalesced split phase output preserves event order and cannot invent worker cleanup")]
    private static void CoalescedLeaf()
    {
        SmokeVector b=new();var v=b.Core;string stdout=b.Text.ToString();int bytes=Encoding.UTF8.GetByteCount(stdout);
        string template=v.Rows.First(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) && r.Contains(" nr=1 a0=1 ",StringComparison.Ordinal));
        v.Rows.RemoveAll(r=>r.StartsWith("M16_005_WRITE ",StringComparison.Ordinal) ||
            r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) && r.Contains(" nr=1 a0=1 ",StringComparison.Ordinal));
        int at=v.Rows.FindIndex(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) && r.Contains(" nr=231 ",StringComparison.Ordinal));
        v.Lengths.Clear();int offset=0;
        foreach(int count in new[]{1,bytes/2,bytes-bytes/2-1})
        {
            string part=stdout.Substring(offset,count).Replace("\n","",StringComparison.Ordinal);offset+=count;
            v.Rows.Insert(at++,$"M16_005_WRITE fd=1 bytes={count} data={part}");string call=template;
            foreach(var field in new[]{("a2",(ulong)count),("valueBits",(ulong)count),("returnValueBits",(ulong)count),("clock",100UL)})
                call=Regex.Replace(call,@"\b"+field.Item1+"=[0-9]+",field.Item1+"="+field.Item2.ToString(CultureInfo.InvariantCulture));
            v.Rows.Insert(at++,call);v.Lengths.Add(count);
        }
        Dictionary<string,int> ordinals=[];
        for(int i=0;i<v.Rows.Count;i++)if(v.Rows[i].StartsWith("M16_008_CALL ",StringComparison.Ordinal))
        {
            string identity=Regex.Match(v.Rows[i],@"\bidentity=([0-9]+)").Groups[1].Value;
            int ordinal=ordinals.GetValueOrDefault(identity)+1;ordinals[identity]=ordinal;
            v.Rows[i]=Regex.Replace(v.Rows[i],@"\bordinal=[0-9]+","ordinal="+ordinal.ToString(CultureInfo.InvariantCulture));
        }
        HostCheck.Equal(6,b.Inspect().Reaped);HostCheck.Equal(Stdout,NativeAotThreadingOutput.Read(App,b.Raw()).Stdout);
        // Full valid stdout placed before any worker can have exited must fail
        // on the phase evidence, not merely on a later truncated transcript.
        Vector empty=new(true);empty.Start();empty.Rows[1]=empty.Rows[1].Replace("entropyBytes=32","entropyBytes=256",StringComparison.Ordinal);
        empty.Rows[2]=empty.Rows[2].Replace("packageBytes=1577712","packageBytes=1511248",StringComparison.Ordinal);
        empty.Rows[4]=empty.Rows[4].Replace("workers=8","workers=16",StringComparison.Ordinal);
        empty.Rows.Insert(5,"M16_008_BUDGET started=0 epochs=8192 steps=16384 nanos=300000000000");
        for(int i=0;i<4;i++)empty.Call(186,1);
        empty.Rows.Add($"M16_005_WRITE fd=1 bytes={bytes} data="+stdout.Replace("\n","",StringComparison.Ordinal));
        empty.Call(1,bytes,[1,0x500400,(uint)bytes]);
        Reject(()=>QemuTranscripts.InspectNativeAotRuntimeProtocolForTests(App,empty.Rows.ToArray(),NativeAotThreadingTranscriptTests.Elf(),
            _=>true,_=>true,[bytes],Stdout,8192,12288),"runtime_smoke_join_ownership");
    }

    private static void Reject(Action action,string? reason=null)
    {
        string? failure=null;
        try{action();}catch(Exception error)when(error is InvalidDataException or ArgumentException or IOException or ToolFailure){failure=error.Message;}
        HostCheck.True(failure is not null && (reason is null || failure.Contains(reason,StringComparison.Ordinal)),"expected="+reason+" actual="+failure);
    }
}
