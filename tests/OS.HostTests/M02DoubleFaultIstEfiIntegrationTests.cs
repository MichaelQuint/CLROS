using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class M02DoubleFaultIstEfiIntegrationTests
{
    private const string Flag="EnableM02DoubleFaultIstProbe";
    private const string Ready="CLROS_M02_015_READY vector=8 hardwareErrorCode=true errorCode=0 ist=1 pageZeroAbsent=true guardPagesAbsent=4 savedState=diagnosticOnly terminalHalt=true postExitBootServices=false";
    private static readonly string[] Prerequisites=["EnableM01LoadedImageStackProbe","EnableM01LiveAllocatorProbe","EnableM01OwnedPagingProbe","EnableM02SafeStubProbe","EnableM02CheckedPortIoBindingProbe","EnableM02Com1OwnedPortIoProbe","EnableM02OwnedCom1ReadyProbe","EnableM02PanicSafeOwnedCom1Probe"];
    private static readonly string[] Conflicts=["EnableM02OwnedX64GdtEncodingProbe","EnableM02GuardedIstStackAllocationProbe","EnableM02OwnedGdtTssTerminalProbe","EnableM02NormalizedInterruptFramePageProbe","EnableM02OwnedIdtGatePopulationProbe","EnableM02IdtrContinuedExecutionProbe","EnableM02Ud2VectorStateProbe","EnableM02PageFaultCr2ErrorCodeProbe","EnableM04IndependentEndianConformanceProbe","EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","EnableM04ArithmeticConformanceProbe","EnableM04LayoutContractProbe","EnableM04ManagedHeapPageRangeProbe","EnableM04RhpNewFastProbe","EnableM04RhpNewFastBeforeBindFailureProbe","EnableM04RhpNewFastExhaustionFailureProbe","EnableUefiGoneSimulationProbe","EnableExitBootServicesFreshMapProbe","EnableAcpiPhysicalDiscoveryProbe"];
    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.015.efi");
        Run("source closure and terminal ordering",TestProject);
        Run("compiled actual adapter controlled backing",TestAdapter);
        Run("strict numeric transcript and lifecycle",TestTranscript);
        Run("default-off profile prerequisites and conflicts",TestProfiles);
        Run("frozen source firewall",TestFirewall);
        Run("complete default and enabled actual NativeAOT artifact authority",TestArtifacts);
        Console.WriteLine("CLROS_M02_015_EFI_HOST_READY guestExecution=false faultTriggered=false postExitBootServices=false");
        Console.WriteLine("TEST_PASS m02.015.efi");
    }
    private static void Run(string name,Action action)
    {
        Console.WriteLine("TEST_START m02.015.efi "+name);
        try {action();Console.WriteLine("TEST_PASS m02.015.efi "+name);}
        catch(Exception e){Console.WriteLine("TEST_FAIL m02.015.efi "+name+" "+e.Message);throw;}
    }
    private static void TestProject()
    {
        string root=Root(); XDocument project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        var items=project.Descendants("Compile").ToArray();Require(items.Length==112,"112 explicit compile items");
        foreach(string name in new[]{"DoubleFaultIstProbeAuthority.cs","DoubleFaultISTPathSilentRebootOrTriple.cs"})
            Require(items.Count(x=>x.Attribute("Include")?.Value=="..\\OS.Kernel\\Diagnostics\\"+name && x.Attribute("Link")?.Value=="Diagnostics\\"+name)==1,"exact new item "+name);
        Require(BootSequencePlanTests.HasNoStdlibBootAuthorityBoundary(),"guarded EFI boot/probe authority excludes host surfaces");
        foreach(var pair in new[]{("src/OS.Kernel/Diagnostics/DoubleFaultISTPathSilentRebootOrTriple.cs","d70fdeacca27629b919dc2fe4491ae323fdabdd8ae0ad6af5a3d74077874ed7f"),("tests/OS.HostTests/DoubleFaultISTPathSilentRebootOrTripleTests.cs","27cf4f9aacea17b79fbca6b65c86cae0838816e6fb74e42178cda11464c2cd30"),("src/OS.Kernel/Diagnostics/SeparateDisabledDefaultPageFaultPageTables.cs","4ec0cfb1cd2a07f0834463c5fd2ea1d6882e3fc76e85b3d9e49607b47abbb3e2"),("src/OS.Kernel/Diagnostics/SeparateDisabledDefaultPageFaultTransition.cs","5a21080faa46414a30740c8ea5f972608333d10973f6988ceb1ec39f50508373"),("tests/OS.HostTests/SeparateDisabledDefaultPageFaultTransitionTests.cs","463db1a1108fb4a62917c6fb4408c6c08b21301b9ed62e94faf62fd8ae4daa8e")})
            Require(Hash(File.ReadAllText(Path.Combine(root,pair.Item1)).Replace("\r\n","\n",StringComparison.Ordinal))==pair.Item2,"frozen LF source "+pair.Item1);
        string authority=File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Diagnostics/DoubleFaultIstProbeAuthority.cs"));
        Require(authority.Contains("public const bool EnableDoubleFaultIstProbe = false;",StringComparison.Ordinal) && !File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Diagnostics/Probes.cs")).Contains("public const bool EnableDoubleFaultIstProbe",StringComparison.Ordinal),"unique false authority");
        XElement initial=project.Descendants(Flag).Single(x=>x.Attribute("Condition")!=null);Require(initial.Value=="false","default false");
        Require(project.Descendants().Count(x=>x.Name.LocalName.StartsWith("Enable",StringComparison.Ordinal) && x.Attribute("Condition")?.Value=="'$("+x.Name.LocalName+")' == ''" && x.Value=="false")==43,"43 explicit XML false defaults");
        string[] ordered=items.Select(x=>x.Attribute("Include")!.Value).ToArray();int predecessor=Array.FindIndex(ordered,x=>x.EndsWith("\\SeparateDisabledDefaultPageFaultTransition.cs",StringComparison.Ordinal));
        Require(predecessor>=0 && ordered[predecessor+1].EndsWith("\\DoubleFaultIstProbeAuthority.cs",StringComparison.Ordinal) && ordered[predecessor+2].EndsWith("\\DoubleFaultISTPathSilentRebootOrTriple.cs",StringComparison.Ordinal)
            && ordered[predecessor+5].EndsWith("\\InvalidOpcodeVectorAuthority.cs",StringComparison.Ordinal),"exact two insertion positions");

        string entry=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));string adapter=Adapter(entry);
        Require(!adapter.Contains("TEST_START m02.015",StringComparison.Ordinal),"stream alone emits start");
        int audit=adapter.IndexOf(".Audit(",StringComparison.Ordinal),rebind=adapter.IndexOf("var fresh =",StringComparison.Ordinal),read=adapter.IndexOf("!ReadBack(c, d, fresh)",StringComparison.Ordinal),publish=adapter.IndexOf("X64InstructionPublication.Serialize()",StringComparison.Ordinal),enter=adapter.IndexOf("enter((byte*)",StringComparison.Ordinal);
        Require(audit>0 && rebind>audit && read>rebind && publish>read && enter>publish,"fresh backing audit/readback/publication/entry");
        Require(!adapter.Contains("s_m01InstructionPublicationEpochMask",StringComparison.Ordinal),"historical M01 epoch unchanged");
        int rangeAllocation=adapter.IndexOf("* ranges = stackalloc",StringComparison.Ordinal),pageAllocation=adapter.IndexOf("* pages = stackalloc",StringComparison.Ordinal),guard=adapter.IndexOf("if (!ScratchBuffers(",StringComparison.Ordinal),query=adapter.IndexOf("if (!Query(",StringComparison.Ordinal),zero=adapter.IndexOf("Zero((byte*)c",StringComparison.Ordinal);
        Require(rangeAllocation>0 && pageAllocation>rangeAllocation && guard>pageAllocation && query>guard && zero>query,"all caller scratch validated before first query-code/bulk write");
        Require(adapter.Contains("stackalloc OS.Kernel.Diagnostics.SeparatePageFaultMappingRange[6]",StringComparison.Ordinal)&&adapter.Contains("stackalloc OS.Kernel.Memory.X64PageTableImagePage[43]",StringComparison.Ordinal),"bounded caller stack scratch");
        int caller=entry.IndexOf("M02DoubleFaultIstAdapter.Run(s_console",StringComparison.Ordinal);Require(caller>entry.LastIndexOf("EfiEntry.WriteBootSequencePlan(",caller,StringComparison.Ordinal),"terminal diagnostic continuation");
        bool Flow(string text) {
            string run=Between(text,"internal static void Run(","internal static bool Range(");
            int attempt=run.IndexOf("s_state = 2;",StringComparison.Ordinal);
            return attempt>=0 && run.Contains("if (s_state != 0) { Failure(console, serial); Spin(); }",StringComparison.Ordinal)
                && run.Contains("X64InstructionPublication.Serialize() != X64InstructionPublication.ExpectedSuccessCookie",StringComparison.Ordinal)
                && run.Contains("(c + Page + OS.Kernel.Diagnostics.DoubleFaultISTPathSilentRebootOrTriple.EntryOffset)",StringComparison.Ordinal)
                && run.Contains("enter((byte*)(d + Page));",StringComparison.Ordinal)
                && !run[attempt..].Contains("FreePages",StringComparison.Ordinal) && !run[attempt..].Contains("FailBeforeEntry",StringComparison.Ordinal)
                && run[attempt..].Contains("while (true) { }",StringComparison.Ordinal);
        }
        Require(Flow(adapter),"closed terminal boundary");
        foreach(var mutation in new[]{("Serialize() !=","Serialize() =="),("DoubleFaultISTPathSilentRebootOrTriple.EntryOffset","DoubleFaultISTPathSilentRebootOrTriple.TriggerOffset"),("enter((byte*)(d + Page));","enter((byte*)d);"),("while (true) { }","services->FreePages(c,3);"),("if (s_state != 0)","if (s_state == 0)")})
            Require(!Flow(adapter.Replace(mutation.Item1,mutation.Item2,StringComparison.Ordinal)),"terminal flow mutation "+mutation.Item1);
        string prepare=Between(adapter,"internal static bool Prepare(","internal static bool ScratchBuffers(");
        int cursor=0;
        foreach(string operation in new[]{".PrepareContext(",".MaterializeArmedIdt(",".AuditArmedIdt(",".EmitStream(",".ValidateStreamReadBack("}) {
            int at=prepare.IndexOf(operation,cursor,StringComparison.Ordinal);Require(at>=cursor,"actual DF operation order "+operation);cursor=at+operation.Length;
        }
        Require(!adapter.Contains("NullPageFaultProbePlan",StringComparison.Ordinal) && !adapter.Contains("PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack",StringComparison.Ordinal),"no PF intent or all-present armed-table audit");

    }
    private static void TestAdapter()
    {
        string root=Root(),dir=Scratch("adapter");
        string adapter=Adapter(File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"))).Replace("private static class M02DoubleFaultIstAdapter","internal static class M02DoubleFaultIstAdapter",StringComparison.Ordinal)
            .Replace("private static void FailBeforeEntry(","internal static void FailBeforeEntry(",StringComparison.Ordinal)
            .Replace("private static bool SamePrepared(","internal static bool SamePrepared(",StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(dir,"Adapter.cs"),"""
            namespace OS.Boot;
            public static unsafe class EfiEntry
            {
                public struct EfiSimpleTextOutputProtocol { }
                public struct EfiSerialIoProtocol { }
                public struct EfiBootServices { public delegate* unmanaged<uint,uint,ulong,ulong*,ulong> AllocatePages; public delegate* unmanaged<ulong,ulong,ulong> FreePages; }
                internal static readonly System.Text.StringBuilder Captured = new();
                private static void Write(EfiSimpleTextOutputProtocol* c,EfiSerialIoProtocol* s,byte b)=>Captured.Append((char)b);
            """+adapter+"\n}");
        File.WriteAllText(Path.Combine(dir,"Program.cs"),"""
            using System.Runtime.InteropServices;
            using OS.Boot;
            using OS.Kernel.Memory;
            using OS.Kernel.Diagnostics;
            unsafe static class Program
            {
                static void Check(bool value,string reason) { if(!value)throw new Exception(reason); }
                static readonly List<(ulong Address,ulong Pages)> Freed = [];
                static bool FreeFailure;
                [System.Runtime.InteropServices.UnmanagedCallersOnly]
                static ulong Free(ulong address,ulong pages) { Freed.Add((address,pages));return FreeFailure ? 1UL : 0UL; }
                static X64PagingModePreflightObservation Mode(ulong cr0=0x80010033,ulong cr4=0xa0,ulong efer=0xd00,uint bits=48,ulong flags=2,uint leaf=0x80000008)
                    =>X64PagingModePreflightObservation.CreateObserved(cr0,0x400000,cr4,efer,leaf,bits,flags);
                static int Main()
                {
                    byte* bytes=(byte*)NativeMemory.AlignedAlloc(56*4096,4096);
                    try {
                        ulong c=(ulong)bytes,d=c+3*4096,t=d+10*4096;
                        SeparatePageFaultMappingRange* ranges=stackalloc SeparatePageFaultMappingRange[6];
                        X64PageTableImagePage* pages=stackalloc X64PageTableImagePage[43];
                        const ulong pat=0x0007040600070406;
                        byte* temp=stackalloc byte[4096];ulong scratchBase=((ulong)temp+7)&~7UL;
                        ulong[] scratchAddresses=[scratchBase,scratchBase+56,scratchBase+152,scratchBase+160,scratchBase+352];
                        ulong[] scratchSizes=[56,96,8,6UL*(ulong)sizeof(SeparatePageFaultMappingRange),43UL*(ulong)sizeof(X64PageTableImagePage)];
                        Check(scratchSizes[3]==192 && scratchSizes[4]==1032,"actual range/page POD extents");
                        bool Scratch()=>EfiEntry.M02DoubleFaultIstAdapter.ScratchBuffers(scratchAddresses[0],scratchAddresses[1],scratchAddresses[2],scratchAddresses[3],scratchAddresses[4],c,d,t);
                        new Span<byte>(temp,4096).Fill(0x5a);new Span<byte>(bytes,56*4096).Fill(0x6b);
                        Check(Scratch(),"five exact disjoint temporary extents");
                        for(int i=0;i<5;i++) {
                            ulong original=scratchAddresses[i];
                            foreach(ulong bad in new[]{0UL,original+1,ulong.MaxValue-7,c,d,t,c+3*4096-8,d+10*4096-8,t+43*4096-8}) {
                                scratchAddresses[i]=bad;Check(!Scratch(),"each scratch invalid extent/owned overlap");
                            }
                            scratchAddresses[i]=original;
                            for(int j=0;j<5;j++)if(i!=j) {
                                scratchAddresses[i]=scratchAddresses[j];Check(!Scratch(),"pairwise alias");
                                scratchAddresses[i]=scratchAddresses[j]+scratchSizes[j]-8;Check(!Scratch(),"last eight-byte overlap");
                                scratchAddresses[i]=original;
                            }
                        }
                        Check(new ReadOnlySpan<byte>(temp,4096).IndexOfAnyExcept((byte)0x5a)<0 && new ReadOnlySpan<byte>(bytes,56*4096).IndexOfAnyExcept((byte)0x6b)<0,"scratch guard read-only on all failures");
                        bool Prepare()=>EfiEntry.M02DoubleFaultIstAdapter.Prepare(c,d,t,Mode(),pat,ranges,pages,out _);
                        void Clear()=>new Span<byte>(bytes,56*4096).Clear();
                        Clear(); Check(Prepare(),"valid actual backing");
                        EfiEntry.M02DoubleFaultIstAdapter.FillRanges(ranges,c,d);
                        Check(ranges[0].PageCount==3 && ranges[1].PageCount==2,"six-range topology");
                        Check(ranges[2].Start==d+0x3000 && ranges[3].Start==d+0x5000 && ranges[4].Start==d+0x7000 && ranges[5].Start==d+0x9000,"guard exclusions");
                        Clear();Check(EfiEntry.M02DoubleFaultIstAdapter.Prepare(c,d,t,Mode(),pat,ranges,pages,out var request),"prepare request");
                        Check(request.Image.MappedPages==52 && request.Image.RangeCount==6 && request.Image.Request.ArenaPageCount==43,"exact image");
                        Check(EfiEntry.M02DoubleFaultIstAdapter.ReadBack(c,d,request),"actual complete readback");
                        Check(c>uint.MaxValue,"actual controlled backing above 4 GiB");
                        Check(((byte*)d)[14*16+5]==0x0e && ((byte*)d)[8*16+5]==0x8e,"one armed nonpresent PF gate; DF remains present");
                        Check(!PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack((byte*)d,4096,d,c).Succeeded,"unchanged all-present validator rejects armed table");
                        var buffers=new DoubleFaultIstBuffers((byte*)(d+4096),4096,(byte*)(c+4096),8192,(byte*)c,4096,(byte*)d,4096);
                        var read=DoubleFaultISTPathSilentRebootOrTriple.ValidateStreamReadBack(buffers,request);
                        Check(EfiEntry.M02DoubleFaultIstAdapter.TransitionMatches(read,DoubleFaultIstOperation.ValidateStreamReadBack,0,8192),"actual readback result contract");
                        Check(!EfiEntry.M02DoubleFaultIstAdapter.TransitionMatches(default,DoubleFaultIstOperation.ValidateStreamReadBack,0,8192),"default result");
                        foreach(var op in Enum.GetValues<DoubleFaultIstOperation>())if(op!=DoubleFaultIstOperation.ValidateStreamReadBack)
                            Check(!EfiEntry.M02DoubleFaultIstAdapter.TransitionMatches(read,op,0,8192),"wrong operation");
                        foreach(string property in new[]{"Operation","Status","BytesWritten","BytesCompared","FirstMismatchOffset","ContextFingerprint","StreamFingerprint","TableFingerprint","SlotFingerprint"}) {
                            var bad=read; int offset=(int)Marshal.OffsetOf<DoubleFaultIstResult>("<"+property+">k__BackingField");
                            if(property.EndsWith("Fingerprint",StringComparison.Ordinal)) new Span<byte>((byte*)&bad+offset,8).Clear();
                            else ((byte*)&bad)[offset]^=1;
                            Check(!EfiEntry.M02DoubleFaultIstAdapter.TransitionMatches(bad,DoubleFaultIstOperation.ValidateStreamReadBack,0,8192),"each result contract field "+property);
                        }
                        foreach(string property in new[]{"ContextFingerprint","TableFingerprint","SlotFingerprint"}) {
                            var bad=read; int offset=(int)Marshal.OffsetOf<DoubleFaultIstResult>("<"+property+">k__BackingField");((byte*)&bad)[offset]^=1;
                            Check(!EfiEntry.M02DoubleFaultIstAdapter.SamePrepared(read,bad),"nonzero fingerprint association "+property);
                        }
                        foreach(var bad in new[]{default(DoubleFaultIstRequest),new DoubleFaultIstRequest(d,c,t,Mode(),pat,request.Image),new DoubleFaultIstRequest(c,d,t+4096,Mode(),pat,request.Image),new DoubleFaultIstRequest(c,d,t,Mode(),pat,default),new DoubleFaultIstRequest(c,d,t,Mode(),pat,request.Image,executionRequested:true)})
                            Check(!EfiEntry.M02DoubleFaultIstAdapter.ReadBack(c,d,bad),"missing/swapped/stale request association");
                        Check(!EfiEntry.M02DoubleFaultIstAdapter.ImageMatches(request.Image,t+4096),"wrong root summary");
                        // Same-count summaries carry no C/D identity. A fresh audit of exact ranges supplies it.
                        ranges[0]=new(c+0x100000,3,PageProtection.Read|PageProtection.Execute);
                        Check(!SeparateDisabledDefaultPageFaultPageTables.Audit(request.Image.Request,ranges,6,6,pages,43,(ulong*)t,43*512).Succeeded,"same-count different-layout summary is insufficient");
                        EfiEntry.M02DoubleFaultIstAdapter.FillRanges(ranges,c,d);

                        foreach(int offset in new[]{0,14*16,4095,4096,4096+768,4096+2698,12287}) {
                            bytes[offset]^=1;Check(!EfiEntry.M02DoubleFaultIstAdapter.ReadBack(c,d,request),"code corruption "+offset);bytes[offset]^=1;
                        }
                        foreach(int offset in new[]{0,14*16,4095,4096,4096+24,4096+100,8191,8192,0x3000,0x5000,0x7000,0x9000,40959}) {
                            ((byte*)d)[offset]^=1;Check(!EfiEntry.M02DoubleFaultIstAdapter.ReadBack(c,d,request),"data corruption "+offset);((byte*)d)[offset]^=1;
                        }
                        ulong first=((ulong*)t)[0];((ulong*)t)[0]^=0x20;
                        Check(!SeparateDisabledDefaultPageFaultPageTables.Audit(request.Image.Request,ranges,6,6,pages,43,(ulong*)t,43*512).Succeeded,"fresh pristine audit rejects stale active bits");
                        ((ulong*)t)[0]=first;
                        for(int i=0;i<6;i++) { var saved=ranges[i];ranges[i]=default;Check(!SeparateDisabledDefaultPageFaultPageTables.Audit(request.Image.Request,ranges,6,6,pages,43,(ulong*)t,43*512).Succeeded,"each range authority");ranges[i]=saved; }
                        foreach(var bad in new[]{Mode(cr0:0x80010032),Mode(cr0:0x80000033),Mode(cr4:0),Mode(cr4:0x1020),Mode(cr4:0x20020),Mode(cr4:0x800020),Mode(cr4:0x1000020),Mode(efer:0x500),Mode(bits:35),Mode(bits:53),Mode(flags:0),Mode(flags:0x102),Mode(flags:0x402),Mode(leaf:0x80000007),default})
                            Check(!EfiEntry.M02DoubleFaultIstAdapter.Mode(bad,pat,out _),"bad actual mode");
                        foreach(ulong badPat in new[]{0UL,pat^1UL,pat^(7UL<<8)}) Check(!EfiEntry.M02DoubleFaultIstAdapter.Mode(Mode(),badPat,out _),"bad PAT");
                        Clear();byte[] before=new ReadOnlySpan<byte>(bytes,56*4096).ToArray();
                        foreach(int fault in Enumerable.Range(0,7)) {
                            var r=fault==0?null:fault==1?(SeparatePageFaultMappingRange*)c:fault==2?(SeparatePageFaultMappingRange*)((byte*)ranges+1):ranges;
                            var p=fault==3?null:fault==4?(X64PageTableImagePage*)t:fault==5?(X64PageTableImagePage*)ranges:fault==6?(X64PageTableImagePage*)ulong.MaxValue:pages;
                            Check(!EfiEntry.M02DoubleFaultIstAdapter.Prepare(c,d,t,Mode(),pat,r,p,out _),"scratch pre-write rejection");
                            Check(new ReadOnlySpan<byte>(bytes,56*4096).SequenceEqual(before),"rejection storage unchanged");
                        }
                        foreach(ulong address in new[]{0UL,1UL,0x0000800000000000UL,0x00007ffffffff000UL,ulong.MaxValue}) Check(!EfiEntry.M02DoubleFaultIstAdapter.Range(address,4096,48),"range boundary");
                        for(int flags=0;flags<8;flags++) {
                            Check(EfiEntry.M02DoubleFaultIstAdapter.CleanupPlan(c,(flags&1)!=0,d,(flags&2)!=0,t,(flags&4)!=0)==flags,"disjoint cleanup exact");
                            byte plan=EfiEntry.M02DoubleFaultIstAdapter.CleanupPlan(c,(flags&1)!=0,c,(flags&2)!=0,c,(flags&4)!=0);
                            Check(plan==(flags>=4?4:flags>=2?2:flags),"overlap release once");
                        }
                        Check(EfiEntry.M02DoubleFaultIstAdapter.CleanupPlan(ulong.MaxValue,true,d,true,t,true)==6,"wrapped old interval never freed twice");
                        EfiEntry.EfiBootServices services=default;services.FreePages=&Free;
                        foreach(bool failure in new[]{false,true})
                        for(int flags=0;flags<8;flags++) {
                            Freed.Clear();EfiEntry.Captured.Clear();FreeFailure=failure;
                            EfiEntry.M02DoubleFaultIstAdapter.FailBeforeEntry(null,null,&services,c,(flags&1)!=0,d,(flags&2)!=0,t,(flags&4)!=0);
                            List<(ulong,ulong)> expected=[];
                            if((flags&4)!=0)expected.Add((t,43));if((flags&2)!=0)expected.Add((d,10));if((flags&1)!=0)expected.Add((c,3));
                            Check(Freed.SequenceEqual(expected),"actual reverse cleanup exactly once");
                            Check(EfiEntry.Captured.ToString()=="M02_DOUBLE_FAULT_FAILED\r\nTEST_FAIL m02.015 preparation_failed\r\n","cleanup failure cannot become success");
                        }
                        Freed.Clear();EfiEntry.Captured.Clear();
                        EfiEntry.M02DoubleFaultIstAdapter.FailBeforeEntry(null,null,&services,c,true,c,true,c,true);
                        Check(Freed.SequenceEqual(new[]{(c,43UL)}),"actual overlapping claims free once");
                        Console.WriteLine("TEST_PASS m02.015.efi.compiled-adapter guestExecution=false faultTriggered=false");
                        return 0;
                    } finally { NativeMemory.AlignedFree(bytes); }
                }
            }
            """);
        File.WriteAllText(Path.Combine(dir,"Adapter.csproj"),"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><ProjectReference Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj"))+"\"/><Compile Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS/Boot/X64InstructionPublication.cs"))+"\"/><Compile Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS/Runtime/RuntimeImportAttribute.cs"))+"\"/></ItemGroup></Project>");
        Result r=Process(root,"dotnet","run","--project",Path.Combine(dir,"Adapter.csproj"),"-c","Release");File.WriteAllText(Path.Combine(dir,"run.log"),r.Output);Require(r.Code==0,r.Output);
    }
    private static string Transcript(int zeroLevels=4)
    {
        ulong c=zeroLevels switch {1=>0x10000000000UL,2=>0x40000000UL,3=>0x200000UL,_=>0x100000UL},d=c+0x100000,t=c+0x200000;
        List<ulong> fields=[c,d,t,c+4096+2698,8,0x80010033,0x400000,0xa0,0xd00,0x0007040600070406,48,t,t,0x20,0x20];
        Dictionary<(ulong,int),ulong> children=[];ulong next=t+4096;
        foreach(ulong linear in new[]{0UL,d+0x2000,d+0x4000,d+0x6000,d+0x8000}) {
            int levels=linear==0?zeroLevels:4;fields.Add(linear);fields.Add((ulong)levels);ulong table=t;
            for(int level=0;level<4;level++) {
                if(level>=levels-1){fields.Add(0);continue;}
                int index=(int)((linear>>(39-level*9))&511);
                if(!children.TryGetValue((table,index),out ulong child)){child=next;next+=4096;children.Add((table,index),child);}
                fields.Add(child|3);table=child;
            }
        }
        fields.AddRange([8,1,0,0,0x12345678,0x38,0x46,d+0xa000,0x10,d+0x4000-192,0,t,8]);
        fields.AddRange([39,d+4096+24,24,4095,d,0x8b,d+0x4000]);
        foreach(var gate in new[]{(Vector:8,Ist:1,Attributes:0x8e),(Vector:14,Ist:2,Attributes:0x0e)}) {
            ulong target=c+(ulong)(gate.Vector*16);
            fields.Add((target&65535)|(8UL<<16)|((ulong)gate.Ist<<32)|((ulong)gate.Attributes<<40)|((target&0xffff0000)<<32));
            fields.Add(target>>32);
        }
        for(ulong i=0;i<15;i++)fields.Add(0x1020304050607080+i);
        Require(fields.Count==84,"independent field extent");
        const string schema="TEST_START m02.015\r\nM02_DF_LAYOUT code=@ data=@ tables=@ faultRip=@ codePages=3 dataPages=10 tablePages=43 mappedPages=52\r\nM02_DF_MODE firmwareCs=@ cr0=@ firmwareCr3=@ cr4Before=@ efer=@ pat=@ physicalBits=@\r\nM02_DF_ROOT cr3=@ expectedCr3=@ cr4=@ expectedCr4=@\r\nM02_DF_ABSENT role=zero linear=@ levelsRead=@ pml4e=@ pdpte=@ pde=@ pte=@\r\nM02_DF_ABSENT role=df linear=@ levelsRead=@ pml4e=@ pdpte=@ pde=@ pte=@\r\nM02_DF_ABSENT role=pf linear=@ levelsRead=@ pml4e=@ pdpte=@ pde=@ pte=@\r\nM02_DF_ABSENT role=nmi linear=@ levelsRead=@ pml4e=@ pdpte=@ pde=@ pte=@\r\nM02_DF_ABSENT role=transition linear=@ levelsRead=@ pml4e=@ pdpte=@ pde=@ pte=@\r\nM02_DF_FRAME vector=@ hasErrorCode=@ error=@ cr2=@ rip=@ cs=@ rflags=@ savedRsp=@ savedSs=@ handlerRsp=@ handlerSs=@ cr3=@ handlerCs=@\r\nM02_DF_DESCRIPTORS gdtrLimit=@ gdtrBase=@ tr=@ idtrLimit=@ idtrBase=@ tssAccess=@ ist1=@ dfGateLow=@ dfGateHigh=@ pfGateLow=@ pfGateHigh=@\r\nM02_DF_GPRS rax=@ rbx=@ rcx=@ rdx=@ rbp=@ rsi=@ rdi=@ r8=@ r9=@ r10=@ r11=@ r12=@ r13=@ r14=@ r15=@\r\nCLROS_M02_015_READY vector=8 hardwareErrorCode=true errorCode=0 ist=1 pageZeroAbsent=true guardPagesAbsent=4 savedState=diagnosticOnly terminalHalt=true postExitBootServices=false\r\nTEST_PASS m02.015\r\n";
        int field=0;string tail=Regex.Replace(schema,"@",_=>"0x"+fields[field++].ToString("X16",System.Globalization.CultureInfo.InvariantCulture));
        Require(Encoding.ASCII.GetByteCount(tail)==2643,"exact raw tail bytes");
        return "TEST_START efi.main\r\nEFI_CONSOLE_READY\r\nUEFI_MEMORY_MAP_READY\r\nUEFI_MEMORY_MAP_DESCRIPTOR_COUNT 2\r\nUEFI_MEMORY_MAP_OWNED\r\nBOOT_HANDOFF_READY\r\nUEFI_MEMORY_MAP_NORMALIZED\r\nBOOT_MEMORY_MAP_ACCEPTED\r\nBOOT_MEMORY_REGIONS_READY\r\nBOOT_MEMORY_REGION_COUNT 2\r\nBOOT_PAGE_ALLOCATOR_READY status=Ready regions=2 conventionalPages=10 conventionalBytes=40960 freeNodes=2 freePages=10 freeBytes=40960 pageSize=4096 statusCode=0\r\nBOOT_ACPI_RSDP_HANDOFF_READY\r\nUEFI_ACPI_RSDP_READY\r\nTEST_START m01.stage1\r\nM01_LOADED_IMAGE_CAPTURED\r\nM01_STACK_SWITCH_STUB_READY\r\nM01_STACK_ARENA_ALLOCATED\r\nM01_STACK_GUARD_RESERVED\r\nM01_STAGE1_MAP_RECAPTURED\r\nM01_STAGE1_RESERVATIONS_VALIDATED\r\nM01_OWNED_STACK_READY\r\nM01_OWNED_STACK_SWITCHED\r\nM01_RSP_CONTAINED\r\nTEST_PASS m01.stage1\r\nTEST_START m01.stage2\r\nM01_ALLOCATOR_ARENA_ALLOCATED\r\nM01_STAGE2_MAP_RECAPTURED\r\nM01_RESERVATIONS_APPLIED\r\nM01_LIVE_ALLOCATED_4K\r\nM01_LIVE_PAGE_WITHIN_ARENA\r\nM01_LIVE_FREED_4K\r\nM01_DOUBLE_FREE_REJECTED\r\nTEST_PASS m01.stage2\r\nTEST_START m01.stage3\r\nM01_CR3_STUB_PAGE_ALLOCATED\r\nM01_PROTECTION_PROBE_PAGE_ALLOCATED\r\nM01_PAGE_TABLE_STATE_ARENA_ALLOCATED\r\nM01_STAGE3_MAP_RECAPTURED\r\nTEST_START m02.017\r\nM02_STUB_PAGE_MATERIALIZED pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=982E5B7E3EE58E56\r\nM02_STUB_PAGE_READBACK_VERIFIED fnv1a64=982E5B7E3EE58E56\r\nM01_CR3_STUB_MATERIALIZED\r\nM01_PAGE_TABLES_BUILT\r\nM01_PAGING_MODE_VALIDATED\r\nM01_PRE_CR3\r\nM01_FIRMWARE_CR3_RESTORED\r\nM01_RFLAGS_RESTORED\r\nM01_PGE_CYCLE_VERIFIED\r\nM01_OWNED_CR3_CANARY_VERIFIED\r\nM01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED\r\nM01_POST_CR3\r\nM01_RW_TO_RX_VERIFIED\r\nM01_TLB_INVALIDATION_COMPLETE\r\nM01_RX_PROBE_EXECUTED\r\nM01_PAGE_ZERO_ABSENT\r\nM02_SAFE_PROBE_EXECUTED offset=0 return=0x4F534F4B\r\nCLROS_M02_017_READY pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=982E5B7E3EE58E56 temporaryOwnedRootRx=true portIoExecuted=false\r\nTEST_PASS m02.017\r\nCLROS_M02_003_READY bindingReady=true out8AddressChecked=true in8AddressChecked=true guestInvocation=false portIoExecuted=false\r\nCLROS_M02_004_READY configurationWrites=7 bindingReady=true guestPortIo=true guestIn8=false markerTransport=uefiSerial postExitBootServices=false\r\nOWNED_COM1_READY\r\nCLROS_M02_005_READY marker=OWNED_COM1_READY markerBytes=16 lineEndingBytes=2 configurationWrites=7 lineStatusPolls=18 ownedWriter=true ownedMarkerTransport=com1PortIo evidenceMarkerTransport=uefiSerial guestPortIo=true guestIn8=true postExitBootServices=false\r\nCLROS_M02_006_READY panicFormatter=bounded ownedCom1Transport=true callerScratch=true statusCoded=true firmwareFallback=false\r\nCLROS_M02_006_EVIDENCE markerBytes=125 serialBytes=127 configurationWrites=7 lineStatusPolls=127 markerTransport=com1PortIo evidenceTransport=uefiSerial guestPortIo=true guestIn8=true panicFormatter=linked panicReporter=ownedCom1 callerScratch=true statusCoded=true firmwareFallback=false postExitBootServices=false\r\nTEST_PASS m01.stage3\r\nCLROS_M01_LIVE_BOOT_MEMORY_READY\r\nTEST_START boot.sequence\r\nBOOT_PHASE PhaseA.Audit START\r\nBOOT_PHASE PhaseA.Audit PASS\r\nBOOT_PHASE PhaseA.Diagnostics START\r\nBOOT_PHASE PhaseA.Diagnostics PASS\r\nOS_BOOT_OK\r\nTEST_PASS boot.sequence\r\nOS_SMOKE_PASS\r\n"+tail;
    }
    private static void TestTranscript()
    {
        string root=Root(),dir=Scratch("transcript"),valid=Transcript();
        byte[] acceptedPreamble=Convert.FromBase64String("G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg==");
        Require(Convert.ToHexString(SHA256.HashData(acceptedPreamble))=="BB50E913C752428AE49AE6C20879196F507926CC685CAD83986494244299D124","fixed accepted firmware identity");
        List<(byte[] Bytes,bool Accept)> cases=[];
        void Add(string text,bool accept)=>cases.Add((Encoding.UTF8.GetBytes(text),accept));
        Add(valid,true);cases.Add(([..acceptedPreamble,..Encoding.ASCII.GetBytes(valid)],true));
        for(int i=1;i<=4;i++)Add(Transcript(i),true);
        Add("OVMF arbitrary\r\n"+valid,false);Add("\0"+valid,false);cases.Add(([0xc0,0xaf,..Encoding.ASCII.GetBytes(valid)],false));
        byte[] changedPrefix=(byte[])acceptedPreamble.Clone();changedPrefix[0]^=1;cases.Add(([..changedPrefix,..Encoding.ASCII.GetBytes(valid)],false));
        Add(valid.Replace("firmwareCs=0x0000000000000008","firmwareCs=0x0000000000000038",StringComparison.Ordinal),true);
        foreach(string selector in new[]{"0000000000000000","000000000000000C","000000000000003B","0000000000010008"})
            Add(valid.Replace("firmwareCs=0x0000000000000008","firmwareCs=0x"+selector,StringComparison.Ordinal),false);
        foreach(string token in new[]{"TEST_FAIL","OS_SMOKE_FAIL","CLROS_M02_015_HOST_READY","CLROS_M02_015_EFI_HOST_READY","M02_DOUBLE_FAULT_FAILED","M02_DF_LAYOUT"})
            for(int split=1;split<token.Length;split++)Add(token[..split]+"\r\n"+token[split..]+"\r\n"+valid,false);
        string[] lines=valid.Split("\r\n");Require(lines.Length==89,"88 owned complete records");
        for(int i=0;i<88;i++) {
            var changed=lines.ToList();changed.RemoveAt(i);Add(string.Join("\r\n",changed),false);
            changed=lines.ToList();changed.Insert(i,lines[i]);Add(string.Join("\r\n",changed),false);
            foreach(string noise in new[]{" ","\0","\u007f","\u200b"}){changed=lines.ToList();changed[i]+=noise;Add(string.Join("\r\n",changed),false);}
            if(i<87){changed=lines.ToList();(changed[i],changed[i+1])=(changed[i+1],changed[i]);Add(string.Join("\r\n",changed),false);}
        }
        var numeric=Regex.Matches(valid,@"0x[0-9A-F]{16}");Require(numeric.Count==84,"independent 84-field fixture");
        for(int i=0;i<numeric.Count;i++) {
            Match m=numeric[i];bool diagnostic=i is >=48 and <=53 or >=69;
            foreach(string replacement in new[]{"0x000000000000000g","0x0",m.Value+"0"})Add(valid[..m.Index]+replacement+valid[(m.Index+m.Length)..],false);
            string value=i is 5 or 8?"0x0000000000000000":"0xFFFFFFFFFFFFFFFF";
            Add(valid[..m.Index]+value+valid[(m.Index+m.Length)..],diagnostic);
            if(diagnostic)Add(valid[..m.Index]+"0x0000000000000000"+valid[(m.Index+m.Length)..],true);
        }
        foreach(string suffix in new[]{"\r\n","\r","\n"," ","firmware returned\r\n","TEST_FAIL late\r\n","\0"})Add(valid+suffix,false);
        foreach(string delimiter in new[]{"\n","\r"})Add(valid.Replace("\r\n",delimiter,StringComparison.Ordinal),false);
        foreach(var mutation in new[]{("pml4e=0x0000000000301003","pml4e=0x0000000000304003",false),("pdpte=0x0000000000302003","pdpte=0x0000000000304003",false),("pml4e=0x0000000000301003","pml4e=0x0000000000301023",true)}) {
            int index=valid.IndexOf(mutation.Item1,StringComparison.Ordinal);Require(index>=0,"independent shared-slot case");
            Add(valid[..index]+mutation.Item2+valid[(index+mutation.Item1.Length)..],mutation.Item3);
        }
        const string rootAbsent="levelsRead=0x0000000000000001 pml4e=0x0000000000000000 pdpte=0x0000000000000000 pde=0x0000000000000000 pte=0x0000000000000000";
        Add(Regex.Replace(valid,@"levelsRead=0x[0-9A-F]{16} pml4e=0x[0-9A-F]{16} pdpte=0x[0-9A-F]{16} pde=0x[0-9A-F]{16} pte=0x[0-9A-F]{16}",rootAbsent),false);
        foreach(string bad in new[]{"0x0000000000301043","0x0000000000301083","0x8000000000301003","0x0000000000300003","0x0000000000400003"})Add(valid.Replace("pml4e=0x0000000000301003","pml4e="+bad,StringComparison.Ordinal),false);
        Add(valid.Replace("pte=0x0000000000000000","pte=0x0000000000200063",StringComparison.Ordinal),false);
        foreach(var mutation in new[]{("DESCRIPTOR_COUNT 2","DESCRIPTOR_COUNT 3"),("REGION_COUNT 2","REGION_COUNT 0"),("freeBytes=40960","freeBytes=40961"),("freeNodes=2","freeNodes=3"),("pageSize=4096","pageSize=8192"),("statusCode=0","statusCode=1")})Add(valid.Replace(mutation.Item1,mutation.Item2,StringComparison.Ordinal),false);
        var records=cases.Select((x,i)=>new{Path=Path.Combine(dir,i+".log"),x.Accept}).ToArray();
        for(int i=0;i<cases.Count;i++)File.WriteAllBytes(records[i].Path,cases[i].Bytes);
        File.WriteAllText(Path.Combine(dir,"cases.json"),JsonSerializer.Serialize(records));
        string script="param([string]$Root,[string]$Cases)\n$ErrorActionPreference='Stop'\n"+ExtractFunctions+"\n"+"""
            LoadFunctions (Join-Path $Root 'scripts/run-qemu-smoke.ps1') @('Assert-M02DoubleFaultIstTranscript','Assert-M02DoubleFaultIstPrefix','Assert-M02DoubleFaultIstRunnerResult','Test-M02DoubleFaultIstOwnedMarker')
            $script:M02DoubleFaultIstReadyMarker='__READY__'
            $records=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($Cases))
            foreach($case in $records) {
                $accepted=$true;$reason=''
                try { Assert-M02DoubleFaultIstTranscript $case.Path } catch {$accepted=$false;$reason=$_.Exception.Message}
                if($accepted -ne $case.Accept){throw ($case.Path+' accepted='+$accepted+' '+$reason)}
            }
            # The runner prints marker inventories before lifecycle records. Failure words there are data.
            $normal=@('TEST_START qemu.smoke','IMAGE_ARTIFACT controlled.img','SERIAL_LOG controlled.log','OVMF_CODE controlled.fd','WORKLOAD_PROFILE SingleMinimalApi','MARKER_PRESETS none','REQUIRED_MARKERS OS_BOOT_OK|TEST_PASS m02.015','ORDERED_MARKERS OS_BOOT_OK|TEST_PASS m02.015','UNIQUE_MARKERS OS_BOOT_OK|TEST_PASS m02.015','FORBIDDEN_MARKERS OS_SMOKE_FAIL|TEST_FAIL|M02_DOUBLE_FAULT_FAILED','SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=0','TEST_PASS qemu.smoke')
            Assert-M02DoubleFaultIstRunnerResult $normal
            foreach($failure in @('TEST_FAIL qemu.smoke timeout','OS_SMOKE_FAIL late','QEMU_PROCESS_CLEANUP_FAILED timeout_ms=2000',('TE'+[char]0+'ST_FAIL late'))) {
                $ok=$false;try{Assert-M02DoubleFaultIstRunnerResult @($normal+$failure);$ok=$true}catch{};if($ok){throw 'failure record accepted'}
            }
            foreach($code in @('0','1','-1','-2147483648','2147483647')) {Assert-M02DoubleFaultIstRunnerResult @('SERIAL_MARKER_DRAIN milliseconds=750',('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$code))}
            foreach($code in @('none','-0','01','+1','2147483648','-2147483649','99999999999999999999')) {
                $ok=$false;try{Assert-M02DoubleFaultIstRunnerResult @('SERIAL_MARKER_DRAIN milliseconds=750',('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$code));$ok=$true}catch{}
                if($ok){throw 'invalid native exit'}
            }
            foreach($bad in @(
                @('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=0'),
                @('SERIAL_MARKER_DRAIN milliseconds=750'),
                @('SERIAL_MARKER_DRAIN milliseconds=750','SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=0'),
                @('SERIAL_MARKER_DRAIN milliseconds=749','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=0'),
                @('SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=exited exitCode=0'),
                @('SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=cleanup-failed exitCode=0'),
                @('SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1 '),
                @('SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=0','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=0'),
                @('SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=0','TEST_FAIL qemu.smoke timeout')) ) {
                $ok=$false;try{Assert-M02DoubleFaultIstRunnerResult $bad;$ok=$true}catch{};if($ok){throw 'invalid lifecycle'}
            }
            Write-Output ('TEST_PASS m02.015.efi.transcript cases='+$records.Count)
            """.Replace("__READY__",Ready,StringComparison.Ordinal);
        Result r=PowerShell(dir,script,"-Root",root,"-Cases",Path.Combine(dir,"cases.json"));Require(r.Code==0,r.Output);
    }
    private static void TestProfiles()
    {
        string root=Root();Result baseline=Qemu();Save("default-dry",baseline);Require(baseline.Code==0&&!baseline.Output.Contains("TEST_START m02.015;",StringComparison.Ordinal),baseline.Output);
        Result enabled=Qemu("-BuildEfi","-"+Flag);Save("enabled-dry",enabled);Require(enabled.Code==0&&enabled.Output.Contains("M02_DF_LAYOUT",StringComparison.Ordinal)&&enabled.Output.Contains("terminalProfile=true",StringComparison.Ordinal),enabled.Output);
        foreach(string script in new[]{"scripts/run-qemu-smoke.ps1","scripts/build-efi-shell.ps1","scripts/test-efi-artifact.ps1"}) {
            string arguments=script.Contains("run-qemu",StringComparison.Ordinal)?"-DryRun -BuildEfi":script.Contains("build-efi",StringComparison.Ordinal)?"-PreflightOnly -LogPath artifacts/issue521-tests/rejected.log":"-EfiArtifact missing.EFI";
            foreach(string negative in new[]{"EnableM04RhpNewFastBeforeBindFailureProbe","EnableM04RhpNewFastExhaustionFailureProbe"}) {
                Result r=Process(root,"powershell.exe","-NoProfile","-Command","& '"+Path.Combine(root,script)+"' "+arguments+" -"+Flag+" -"+negative);
                Save("standalone-negative-"+negative,r);Require(r.Code!=0&&r.Output.Contains("m04_rhp_new_fast",StringComparison.Ordinal),r.Output);
            }
            foreach(string option in Prerequisites) {
                Result r=Process(root,"powershell.exe","-NoProfile","-Command","& '"+Path.Combine(root,script)+"' "+arguments+" -"+Flag+" -"+option+":$false");
                Save("explicit-false-"+option,r);Require(r.Code!=0&&r.Output.Contains("explicit_false_prerequisite",StringComparison.Ordinal),r.Output);
            }
            foreach(string option in Conflicts) {
                // Supply the opposing M04 profile's own prerequisites so its earlier
                // prerequisite check cannot mask the intended profile collision.
                string[] m04=["EnableM04IndependentEndianConformanceProbe","EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","EnableM04ArithmeticConformanceProbe","EnableM04LayoutContractProbe","EnableM04ManagedHeapPageRangeProbe"];
                int index=Array.IndexOf(m04,option);
                string opposing=index<0?string.Empty:" "+string.Join(' ',m04.Take(index).Select(x=>"-"+x));
                if(index==4)opposing+=" -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe";
                if(option.Contains("RhpNewFast",StringComparison.Ordinal)) opposing=" "+string.Join(' ',m04.Select(x=>"-"+x))+" -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe"+(option=="EnableM04RhpNewFastProbe"?string.Empty:" -EnableM04RhpNewFastProbe");
                Result r=Process(root,"powershell.exe","-NoProfile","-Command","& '"+Path.Combine(root,script)+"' "+arguments+" -"+Flag+" -"+option+opposing);
                Save("conflict-"+option,r);Require(r.Code!=0&&(r.Output.Contains("m02_double_fault_ist_profile_conflict",StringComparison.Ordinal)||r.Output.Contains("m04_rhp_new_fast_conflicting_profile",StringComparison.Ordinal)||r.Output.Contains("m02_page_fault_cr2_error_code_profile_conflict",StringComparison.Ordinal)),r.Output);
            }
        }

        foreach(string[] args in new[]{new[]{"-"+Flag},new[]{"-BuildEfi","-"+Flag,"-SkipImageBuild"},new[]{"-BuildEfi","-"+Flag,"-M01RawObservationLog","artifacts/forbidden.log"},new[]{"-RequireMarker",Ready}}) {Result r=Qemu(args);Save("invalid-profile",r);Require(r.Code!=0,r.Output);}
    }
    private static void TestFirewall()
    {
        string dir=Scratch("firewall");Result r=PowerShell(dir,"param([string]$Root)\n$ErrorActionPreference='Stop'\n"+ExtractFunctions+"\n"+"""
            LoadFunctions (Join-Path $Root 'scripts/build-efi-shell.ps1') @('Get-M01Utf8Sha256','Get-NostdlibEfiActiveSource','Test-M01ActiveSourceSurface')
            $name='..\OS.Kernel\Diagnostics\DoubleFaultISTPathSilentRebootOrTriple.cs'
            $text=[IO.File]::ReadAllText((Join-Path $Root 'src/OS.Kernel/Diagnostics/DoubleFaultISTPathSilentRebootOrTriple.cs'))
            if(-not(Test-M01ActiveSourceSurface $text $name).Succeeded){throw 'ordinary reviewed source rejected'}
            foreach($bad in @('using System.Text;', 'class Bad { object M() => new object(); }', 'class Bad { void M() { throw null; } }')) {
                if((Test-M01ActiveSourceSurface ($text+"`n"+$bad) $name).Succeeded){throw 'generic firewall bypass'}
            }
            Write-Output 'TEST_PASS m02.015.efi.firewall'
            ""","-Root",Root());Require(r.Code==0,r.Output);
    }
    private static void TestArtifacts()
    {
        string root=Root();
        foreach(bool enabled in new[]{false,true}) {
            string id="issue521-tests-"+Guid.NewGuid().ToString("N"),dir=Scratch(enabled?"artifact-enabled":"artifact-default");
            string artifact="artifacts/efi/"+id+".EFI";
            Result r=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File","scripts/build-efi-shell.ps1","-EfiOutputPath",artifact,"-LogPath",Path.Combine(dir,"publish.log"),..(enabled?new[]{"-"+Flag}:Array.Empty<string>())]);
            File.WriteAllText(Path.Combine(dir,"run.log"),r.Output);Require(r.Code==0&&r.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal)&&r.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),r.Output);
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(Path.Combine(root,artifact)+suffix,Path.Combine(dir,"image.EFI"+suffix));
            foreach(var file in new[]{("src/OS/bin/Release/net10.0/win-x64/OS.dll","OS.dll"),("src/OS/obj/Release/net10.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net10.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(root,file.Item1),Path.Combine(dir,file.Item2));
            string link=Path.ChangeExtension(Path.Combine(root,artifact),"link.json");
            if(!File.Exists(link))link=Path.Combine(root,artifact)+".link.json";
            using JsonDocument document=JsonDocument.Parse(File.ReadAllText(link));JsonElement evidence=document.RootElement;
            var inputs=evidence.GetProperty("sourceClosureInputs").EnumerateArray().ToArray();Require(inputs.Length==126,"126 raw authority inputs");
            foreach(var input in inputs)Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,input.GetProperty("path").GetString()!)))).Equals(input.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"raw source identity");
            var evaluation=evidence.GetProperty("postRestoreEvaluation");Require(evaluation.GetProperty("compileCount").GetInt32()==112,"112 evaluated files");
            var properties=evaluation.GetProperty("properties");var flags=properties.EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)).ToArray();Require(flags.Length==46,"complete 46-switch vector");
            foreach(var flag in flags)Require(flag.Value.GetString()==(enabled&&(Prerequisites.Contains(flag.Name,StringComparer.Ordinal)||flag.Name==Flag)?"true":"false"),"actual evaluated "+flag.Name);
            using var reader=new PEReader(File.OpenRead(Path.Combine(dir,"OS.dll")));var metadata=reader.GetMetadataReader();
            Require(metadata.AssemblyReferences.Count==0 && metadata.GetString(metadata.GetAssemblyDefinition().Name)=="OS","actual no-stdlib metadata");
            var adapters=metadata.TypeDefinitions.Where(h=>metadata.GetString(metadata.GetTypeDefinition(h).Name)=="M02DoubleFaultIstAdapter").ToArray();
            Require(adapters.Length==(enabled?1:0),"compiled default/DF dispatch isolation");
            if(enabled) {
                var methods=metadata.GetTypeDefinition(adapters[0]).GetMethods().ToDictionary(h=>metadata.GetString(metadata.GetMethodDefinition(h).Name),StringComparer.Ordinal);
                string[] runCalls=Calls(reader,metadata,methods["Run"]),prepareCalls=Calls(reader,metadata,methods["Prepare"]);
                int audit=Array.FindIndex(runCalls,x=>x=="SeparateDisabledDefaultPageFaultPageTables.Audit");
                int read=Array.FindIndex(runCalls,audit+1,x=>x=="M02DoubleFaultIstAdapter.ReadBack");
                int serialize=Array.FindIndex(runCalls,read+1,x=>x=="X64InstructionPublication.Serialize");
                int calli=Array.FindIndex(runCalls,serialize+1,x=>x=="calli");
                Require(audit>=0 && read>audit && serialize>read && calli>serialize,"actual decoded IL fresh audit/readback/publication/indirect entry");
                int cursor=0;foreach(string operation in new[]{"PrepareContext","MaterializeArmedIdt","AuditArmedIdt","EmitStream","ValidateStreamReadBack"}) {
                    int at=Array.FindIndex(prepareCalls,cursor,x=>x=="DoubleFaultISTPathSilentRebootOrTriple."+operation);Require(at>=cursor,"actual DF leaf call "+operation);cursor=at+1;
                }
                File.WriteAllText(Path.Combine(dir,"managed-adapter.json"),JsonSerializer.Serialize(new{GuestExecution=false,NativeAbiAccepted=false,RunCalls=runCalls,PrepareCalls=prepareCalls,Methods=methods.Select(x=>new{Name=x.Key,Rva=metadata.GetMethodDefinition(x.Value).RelativeVirtualAddress})},new JsonSerializerOptions{WriteIndented=true}));
            }
            File.WriteAllText(Path.Combine(dir,"identities.json"),JsonSerializer.Serialize(Directory.GetFiles(dir).Select(p=>new{Path=Path.GetFileName(p),Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))}),new JsonSerializerOptions{WriteIndented=true}));
        }
    }
    // Decode only method-body instruction boundaries. Host metadata inspection never invokes this assembly.
    private static string[] Calls(PEReader pe,MetadataReader md,MethodDefinitionHandle method)
    {
        var opcodes=typeof(System.Reflection.Emit.OpCodes).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)
            .Where(f=>f.FieldType==typeof(System.Reflection.Emit.OpCode)).Select(f=>(System.Reflection.Emit.OpCode)f.GetValue(null)!).ToDictionary(o=>(ushort)o.Value);
        byte[] il=pe.GetMethodBody(md.GetMethodDefinition(method).RelativeVirtualAddress).GetILBytes()!;List<string> calls=[];
        string TypeName(EntityHandle h)=>h.Kind==HandleKind.TypeDefinition?md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)h).Name):h.Kind==HandleKind.TypeReference?md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Name):h.Kind.ToString();
        for(int at=0;at<il.Length;) {
            ushort value=il[at++];if(value==0xfe)value=(ushort)(0xfe00|il[at++]);Require(opcodes.TryGetValue(value,out var op),"known IL opcode");
            int bytes=op.OperandType switch {
                System.Reflection.Emit.OperandType.InlineNone=>0,
                System.Reflection.Emit.OperandType.ShortInlineBrTarget or System.Reflection.Emit.OperandType.ShortInlineI or System.Reflection.Emit.OperandType.ShortInlineVar=>1,
                System.Reflection.Emit.OperandType.InlineVar=>2,
                System.Reflection.Emit.OperandType.InlineI8 or System.Reflection.Emit.OperandType.InlineR=>8,
                System.Reflection.Emit.OperandType.InlineSwitch=>checked(4+4*BitConverter.ToInt32(il,at)),_=>4};
            Require(bytes<=il.Length-at,"bounded IL operand");
            if(op==System.Reflection.Emit.OpCodes.Calli)calls.Add("calli");
            if(op==System.Reflection.Emit.OpCodes.Call || op==System.Reflection.Emit.OpCodes.Callvirt || op==System.Reflection.Emit.OpCodes.Newobj) {
                var h=System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(BitConverter.ToInt32(il,at));
                if(h.Kind==HandleKind.MethodSpecification)h=md.GetMethodSpecification((MethodSpecificationHandle)h).Method;
                if(h.Kind==HandleKind.MethodDefinition){var m=md.GetMethodDefinition((MethodDefinitionHandle)h);calls.Add(TypeName(m.GetDeclaringType())+"."+md.GetString(m.Name));}
                else {Require(h.Kind==HandleKind.MemberReference,"method operand");var m=md.GetMemberReference((MemberReferenceHandle)h);calls.Add(TypeName(m.Parent)+"."+md.GetString(m.Name));}
            }
            at+=bytes;
        }
        return calls.ToArray();
    }
    private const string ExtractFunctions="""
        function LoadFunctions([string]$Path,[string[]]$Names) {
            $ast=[Management.Automation.Language.Parser]::ParseFile($Path,[ref]$null,[ref]$null)
            foreach($name in $Names) {
                $f=$ast.Find({param($a) $a -is [Management.Automation.Language.FunctionDefinitionAst] -and $a.Name -ceq $name},$true)
                if($null -eq $f){throw ('missing '+$name)}
                . ([scriptblock]::Create($f.Extent.Text.Replace(('function '+$name+' '),('function global:'+$name+' '))))
            }
        }
        """;
    private static Result PowerShell(string dir,string text,params string[] args){string ps=Path.Combine(dir,"Test.ps1");File.WriteAllText(ps,text);Result r=Process(Root(),"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",ps,..args]);File.WriteAllText(Path.Combine(dir,"run.log"),r.Output);return r;}
    private static string Adapter(string text) => Between(text,"    private static class M02DoubleFaultIstAdapter","#endif");
    private static string Between(string text,string start,string end) {int i=text.IndexOf(start,StringComparison.Ordinal);Require(i>=0,start);return text[i..text.IndexOf(end,i,StringComparison.Ordinal)];}
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static Result Qemu(params string[] args)=>Process(Root(),"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(Root(),"scripts/run-qemu-smoke.ps1"),"-DryRun",..args]);
    private static string[] Markers(string output,string prefix)=>output.Replace("\r","").Split('\n').Single(x=>x.StartsWith(prefix,StringComparison.Ordinal))[prefix.Length..].Split(';');
    private static Result Process(string root,string exe,params string[] args)
    {
        ProcessStartInfo start=new(exe){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        if (string.Equals(exe,"powershell.exe",StringComparison.OrdinalIgnoreCase))
        {
            string modules=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","Modules");
            string inherited=Environment.GetEnvironmentVariable("PSModulePath")??string.Empty;
            start.Environment["PSModulePath"]=modules+(inherited.Length==0?string.Empty:Path.PathSeparator+inherited);
        }
        foreach(string arg in args)start.ArgumentList.Add(arg);
        using System.Diagnostics.Process process=System.Diagnostics.Process.Start(start)!;
        Task<string> stdout=process.StandardOutput.ReadToEndAsync(),stderr=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(600000)){process.Kill(true);throw new TimeoutException(exe);}
        Task.WaitAll(stdout,stderr);return new(process.ExitCode,stdout.Result+stderr.Result);
    }
    private static void Save(string name,Result r)=>File.WriteAllText(Path.Combine(Scratch(name),"run.log"),r.Output);
    private static string Scratch(string name){string path=Path.Combine(Root(),"artifacts/issue521-tests",name,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    private static string Root(){for(DirectoryInfo? d=new(Environment.CurrentDirectory);d!=null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"CLROS.slnx")))return d.FullName;throw new DirectoryNotFoundException();}
    private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private readonly record struct Result(int Code,string Output);

}
