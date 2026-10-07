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

internal static class M02PageFaultCr2ErrorCodeEfiIntegrationTests
{
    private const string Flag="EnableM02PageFaultCr2ErrorCodeProbe";
    private const string Ready="CLROS_M02_014_READY vector=14 hardwareErrorCode=true errorCode=0 cr2=0 pageZeroAbsent=true guardPagesAbsent=4 ist=2 rf=true ownedCr3=true hardwareStateCaptured=true faultTriggered=true terminalHalt=true postExitBootServices=false";
    private static readonly string[] Prerequisites=["EnableM01LoadedImageStackProbe","EnableM01LiveAllocatorProbe","EnableM01OwnedPagingProbe","EnableM02SafeStubProbe","EnableM02CheckedPortIoBindingProbe","EnableM02Com1OwnedPortIoProbe","EnableM02OwnedCom1ReadyProbe","EnableM02PanicSafeOwnedCom1Probe"];
    private static readonly string[] Conflicts=["EnableM02OwnedX64GdtEncodingProbe","EnableM02GuardedIstStackAllocationProbe","EnableM02OwnedGdtTssTerminalProbe","EnableM02NormalizedInterruptFramePageProbe","EnableM02OwnedIdtGatePopulationProbe","EnableM02IdtrContinuedExecutionProbe","EnableM02Ud2VectorStateProbe","EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","EnableM04ArithmeticConformanceProbe","EnableM04LayoutContractProbe","EnableM04ManagedHeapPageRangeProbe"];
    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.014.efi");
        Run("source closure and terminal ordering",TestProject);
        Run("compiled actual adapter controlled backing",TestAdapter);
        Run("strict numeric transcript and lifecycle",TestTranscript);
        Run("default-off profile prerequisites and conflicts",TestProfiles);
        Run("frozen source firewall",TestFirewall);
        Run("complete default and enabled actual NativeAOT artifact authority",TestArtifacts);
        Console.WriteLine("CLROS_M02_014_EFI_HOST_READY guestExecution=false faultTriggered=false postExitBootServices=false");
        Console.WriteLine("TEST_PASS m02.014.efi");
    }
    private static void Run(string name,Action action)
    {
        Console.WriteLine("TEST_START m02.014.efi "+name);
        try {action();Console.WriteLine("TEST_PASS m02.014.efi "+name);}
        catch(Exception e){Console.WriteLine("TEST_FAIL m02.014.efi "+name+" "+e.Message);throw;}
    }
    private static void TestProject()
    {
        string root=Root(); XDocument project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        var items=project.Descendants("Compile").ToArray();Require(items.Length==114,"114 explicit compile items");
        foreach(string name in new[]{"NullPointerProbeAuthority.cs","NullPageFaultProbePlan.cs","SeparateDisabledDefaultPageFaultCR2ErrorCode.cs","SeparateDisabledDefaultPageFaultPageTables.cs","SeparateDisabledDefaultPageFaultTransition.cs"})
            Require(items.Count(x=>x.Attribute("Include")?.Value=="..\\OS.Kernel\\Diagnostics\\"+name && x.Attribute("Link")?.Value=="Diagnostics\\"+name)==1,"exact new item "+name);
        Require(BootSequencePlanTests.HasNoStdlibBootAuthorityBoundary(),"guarded EFI boot/probe authority excludes host surfaces");
        foreach(var pair in new[]{("src/OS.Kernel/Diagnostics/SeparateDisabledDefaultPageFaultPageTables.cs","4ec0cfb1cd2a07f0834463c5fd2ea1d6882e3fc76e85b3d9e49607b47abbb3e2"),("src/OS.Kernel/Diagnostics/SeparateDisabledDefaultPageFaultTransition.cs","5a21080faa46414a30740c8ea5f972608333d10973f6988ceb1ec39f50508373"),("tests/OS.HostTests/SeparateDisabledDefaultPageFaultTransitionTests.cs","463db1a1108fb4a62917c6fb4408c6c08b21301b9ed62e94faf62fd8ae4daa8e")})
            Require(Hash(File.ReadAllText(Path.Combine(root,pair.Item1)).Replace("\r\n","\n",StringComparison.Ordinal))==pair.Item2,"frozen LF source "+pair.Item1);
        string authority=File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Diagnostics/NullPointerProbeAuthority.cs"));
        Require(authority.Contains("public const bool EnableNullPointerPanicProbe = false;",StringComparison.Ordinal) && !File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Diagnostics/Probes.cs")).Contains("public const bool EnableNullPointerPanicProbe",StringComparison.Ordinal),"unique false authority");
        XElement initial=project.Descendants(Flag).Single(x=>x.Attribute("Condition")!=null);Require(initial.Value=="false","default false");
        string entry=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));string adapter=Adapter(entry);
        Require(!adapter.Contains("TEST_START m02.014",StringComparison.Ordinal),"stream alone emits start");
        int audit=adapter.IndexOf(".Audit(",StringComparison.Ordinal),rebind=adapter.IndexOf("var fresh =",StringComparison.Ordinal),read=adapter.IndexOf("!ReadBack(c, d, fresh)",StringComparison.Ordinal),publish=adapter.IndexOf("X64InstructionPublication.Serialize()",StringComparison.Ordinal),enter=adapter.IndexOf("enter((byte*)",StringComparison.Ordinal);
        Require(audit>0 && rebind>audit && read>rebind && publish>read && enter>publish,"fresh backing audit/readback/publication/entry");
        Require(!adapter.Contains("s_m01InstructionPublicationEpochMask",StringComparison.Ordinal),"historical M01 epoch unchanged");
        int rangeAllocation=adapter.IndexOf("* ranges = stackalloc",StringComparison.Ordinal),pageAllocation=adapter.IndexOf("* pages = stackalloc",StringComparison.Ordinal),guard=adapter.IndexOf("if (!ScratchBuffers(",StringComparison.Ordinal),query=adapter.IndexOf("if (!Query(",StringComparison.Ordinal),zero=adapter.IndexOf("Zero((byte*)c",StringComparison.Ordinal);
        Require(rangeAllocation>0 && pageAllocation>rangeAllocation && guard>pageAllocation && query>guard && zero>query,"all caller scratch validated before first query-code/bulk write");
        Require(adapter.Contains("stackalloc OS.Kernel.Diagnostics.SeparatePageFaultMappingRange[6]",StringComparison.Ordinal)&&adapter.Contains("stackalloc OS.Kernel.Memory.X64PageTableImagePage[43]",StringComparison.Ordinal),"bounded caller stack scratch");
        int caller=entry.IndexOf("M02PageFaultAdapter.Run(s_console",StringComparison.Ordinal);Require(caller>entry.LastIndexOf("EfiEntry.WriteBootSequencePlan(",caller,StringComparison.Ordinal),"terminal diagnostic continuation");
    }
    private static void TestAdapter()
    {
        string root=Root(),dir=Scratch("adapter");
        string adapter=Adapter(File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"))).Replace("private static class M02PageFaultAdapter","internal static class M02PageFaultAdapter",StringComparison.Ordinal)
            .Replace("private static void FailBeforeEntry(","internal static void FailBeforeEntry(",StringComparison.Ordinal);
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
                        bool Scratch()=>EfiEntry.M02PageFaultAdapter.ScratchBuffers(scratchAddresses[0],scratchAddresses[1],scratchAddresses[2],scratchAddresses[3],scratchAddresses[4],c,d,t);
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
                        bool Prepare()=>EfiEntry.M02PageFaultAdapter.Prepare(c,d,t,Mode(),pat,ranges,pages,out _);
                        void Clear()=>new Span<byte>(bytes,56*4096).Clear();
                        Clear(); Check(Prepare(),"valid actual backing");
                        EfiEntry.M02PageFaultAdapter.FillRanges(ranges,c,d);
                        Check(ranges[0].PageCount==3 && ranges[1].PageCount==2,"six-range topology");
                        Check(ranges[2].Start==d+0x3000 && ranges[3].Start==d+0x5000 && ranges[4].Start==d+0x7000 && ranges[5].Start==d+0x9000,"guard exclusions");
                        Clear();Check(EfiEntry.M02PageFaultAdapter.Prepare(c,d,t,Mode(),pat,ranges,pages,out var request),"prepare request");
                        Check(request.Image.MappedPages==52 && request.Image.RangeCount==6 && request.Image.Request.ArenaPageCount==43,"exact image");
                        Check(EfiEntry.M02PageFaultAdapter.ReadBack(c,d,request),"actual complete readback");
                        foreach(int offset in new[]{0,14*16,4095,4096,4096+768,4096+2841,12287}) {
                            bytes[offset]^=1;Check(!EfiEntry.M02PageFaultAdapter.ReadBack(c,d,request),"code corruption "+offset);bytes[offset]^=1;
                        }
                        foreach(int offset in new[]{0,14*16,4095,4096,4096+24,4096+100,8191,8192,0x3000,0x5000,0x7000,0x9000,40959}) {
                            ((byte*)d)[offset]^=1;Check(!EfiEntry.M02PageFaultAdapter.ReadBack(c,d,request),"data corruption "+offset);((byte*)d)[offset]^=1;
                        }
                        ulong first=((ulong*)t)[0];((ulong*)t)[0]^=0x20;
                        Check(!SeparateDisabledDefaultPageFaultPageTables.Audit(request.Image.Request,ranges,6,6,pages,43,(ulong*)t,43*512).Succeeded,"fresh pristine audit rejects stale active bits");
                        ((ulong*)t)[0]=first;
                        for(int i=0;i<6;i++) { var saved=ranges[i];ranges[i]=default;Check(!SeparateDisabledDefaultPageFaultPageTables.Audit(request.Image.Request,ranges,6,6,pages,43,(ulong*)t,43*512).Succeeded,"each range authority");ranges[i]=saved; }
                        foreach(var bad in new[]{Mode(cr0:0x80010032),Mode(cr0:0x80000033),Mode(cr4:0),Mode(cr4:0x1020),Mode(cr4:0x20020),Mode(cr4:0x800020),Mode(cr4:0x1000020),Mode(efer:0x500),Mode(bits:35),Mode(bits:53),Mode(flags:0),Mode(flags:0x102),Mode(flags:0x402),Mode(leaf:0x80000007),default})
                            Check(!EfiEntry.M02PageFaultAdapter.Mode(bad,pat,out _),"bad actual mode");
                        foreach(ulong badPat in new[]{0UL,pat^1UL,pat^(7UL<<8)}) Check(!EfiEntry.M02PageFaultAdapter.Mode(Mode(),badPat,out _),"bad PAT");
                        Clear();byte[] before=new ReadOnlySpan<byte>(bytes,56*4096).ToArray();
                        foreach(int fault in Enumerable.Range(0,7)) {
                            var r=fault==0?null:fault==1?(SeparatePageFaultMappingRange*)c:fault==2?(SeparatePageFaultMappingRange*)((byte*)ranges+1):ranges;
                            var p=fault==3?null:fault==4?(X64PageTableImagePage*)t:fault==5?(X64PageTableImagePage*)ranges:fault==6?(X64PageTableImagePage*)ulong.MaxValue:pages;
                            Check(!EfiEntry.M02PageFaultAdapter.Prepare(c,d,t,Mode(),pat,r,p,out _),"scratch pre-write rejection");
                            Check(new ReadOnlySpan<byte>(bytes,56*4096).SequenceEqual(before),"rejection storage unchanged");
                        }
                        foreach(ulong address in new[]{0UL,1UL,0x0000800000000000UL,0x00007ffffffff000UL,ulong.MaxValue}) Check(!EfiEntry.M02PageFaultAdapter.Range(address,4096,48),"range boundary");
                        for(int flags=0;flags<8;flags++) {
                            Check(EfiEntry.M02PageFaultAdapter.CleanupPlan(c,(flags&1)!=0,d,(flags&2)!=0,t,(flags&4)!=0)==flags,"disjoint cleanup exact");
                            byte plan=EfiEntry.M02PageFaultAdapter.CleanupPlan(c,(flags&1)!=0,c,(flags&2)!=0,c,(flags&4)!=0);
                            Check(plan==(flags>=4?4:flags>=2?2:flags),"overlap release once");
                        }
                        Check(EfiEntry.M02PageFaultAdapter.CleanupPlan(ulong.MaxValue,true,d,true,t,true)==6,"wrapped old interval never freed twice");
                        EfiEntry.EfiBootServices services=default;services.FreePages=&Free;
                        foreach(bool failure in new[]{false,true})
                        for(int flags=0;flags<8;flags++) {
                            Freed.Clear();EfiEntry.Captured.Clear();FreeFailure=failure;
                            EfiEntry.M02PageFaultAdapter.FailBeforeEntry(null,null,&services,c,(flags&1)!=0,d,(flags&2)!=0,t,(flags&4)!=0);
                            List<(ulong,ulong)> expected=[];
                            if((flags&4)!=0)expected.Add((t,43));if((flags&2)!=0)expected.Add((d,10));if((flags&1)!=0)expected.Add((c,3));
                            Check(Freed.SequenceEqual(expected),"actual reverse cleanup exactly once");
                            Check(EfiEntry.Captured.ToString()=="M02_PAGE_FAULT_CR2_ERROR_CODE_FAILED\r\nTEST_FAIL m02.014 preparation_failed\r\n","cleanup failure cannot become success");
                        }
                        Freed.Clear();EfiEntry.Captured.Clear();
                        EfiEntry.M02PageFaultAdapter.FailBeforeEntry(null,null,&services,c,true,c,true,c,true);
                        Check(Freed.SequenceEqual(new[]{(c,43UL)}),"actual overlapping claims free once");
                        Console.WriteLine("TEST_PASS m02.014.efi.compiled-adapter guestExecution=false faultTriggered=false");
                        return 0;
                    } finally { NativeMemory.AlignedFree(bytes); }
                }
            }
            """);
        File.WriteAllText(Path.Combine(dir,"Adapter.csproj"),"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><ProjectReference Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj"))+"\"/><Compile Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS/Boot/X64InstructionPublication.cs"))+"\"/><Compile Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS/Runtime/RuntimeImportAttribute.cs"))+"\"/></ItemGroup></Project>");
        Result r=Process(root,"dotnet","run","--project",Path.Combine(dir,"Adapter.csproj"),"-c","Release");File.WriteAllText(Path.Combine(dir,"run.log"),r.Output);Require(r.Code==0,r.Output);
    }
    private static void TestTranscript()
    {
        string root=Root(),dir=Scratch("transcript");
        string valid="OVMF firmware\r\nOS_BOOT_OK\r\nTEST_PASS boot.sequence\r\nOS_SMOKE_PASS\r\nTEST_START m02.014\r\nM02_PAGE_FAULT_LAYOUT code=0x0000000000100000 data=0x0000000000200000 tables=0x0000000000300000 faultRip=0x0000000000101B19 codePages=3 dataPages=10 tablePages=43 mappedPages=52\r\nM02_PAGE_FAULT_MODE firmwareCs=0x0000000000000008 cr0=0x0000000080010033 firmwareCr3=0x0000000000400000 cr4Before=0x00000000000000A0 efer=0x0000000000000D00 pat=0x0007040600070406 physicalBits=0x0000000000000030\r\nM02_PAGE_FAULT_ROOT cr3=0x0000000000300000 expectedCr3=0x0000000000300000 cr4=0x0000000000000020 expectedCr4=0x0000000000000020\r\nM02_PAGE_FAULT_ABSENT role=zero linear=0x0000000000000000 levelsRead=0x0000000000000004 pml4e=0x0000000000301003 pdpte=0x0000000000302023 pde=0x0000000000303003 pte=0x0000000000000000\r\nM02_PAGE_FAULT_ABSENT role=df linear=0x0000000000202000 levelsRead=0x0000000000000004 pml4e=0x0000000000301003 pdpte=0x0000000000302023 pde=0x0000000000304003 pte=0x0000000000000000\r\nM02_PAGE_FAULT_ABSENT role=pf linear=0x0000000000204000 levelsRead=0x0000000000000004 pml4e=0x0000000000301003 pdpte=0x0000000000302023 pde=0x0000000000304003 pte=0x0000000000000000\r\nM02_PAGE_FAULT_ABSENT role=nmi linear=0x0000000000206000 levelsRead=0x0000000000000004 pml4e=0x0000000000301003 pdpte=0x0000000000302023 pde=0x0000000000304003 pte=0x0000000000000000\r\nM02_PAGE_FAULT_ABSENT role=transition linear=0x0000000000208000 levelsRead=0x0000000000000004 pml4e=0x0000000000301003 pdpte=0x0000000000302023 pde=0x0000000000304003 pte=0x0000000000000000\r\nM02_PAGE_FAULT_FRAME vector=0x000000000000000E hasErrorCode=0x0000000000000001 error=0x0000000000000000 cr2=0x0000000000000000 rip=0x0000000000101B19 cs=0x0000000000000008 rflags=0x0000000000010046 savedRsp=0x000000000020A000 savedSs=0x0000000000000010 handlerRsp=0x0000000000205F40 handlerSs=0x0000000000000000 cr3=0x0000000000300000\r\nCLROS_M02_014_READY vector=14 hardwareErrorCode=true errorCode=0 cr2=0 pageZeroAbsent=true guardPagesAbsent=4 ist=2 rf=true ownedCr3=true hardwareStateCaptured=true faultTriggered=true terminalHalt=true postExitBootServices=false\r\nTEST_PASS m02.014\r\n";
        List<(string Text,bool Accept)> cases=[(valid,true),(valid+"\r\n",true),(valid.Replace("firmwareCs=0x0000000000000008","firmwareCs=0x0000000000000038",StringComparison.Ordinal).Replace(" cs=0x0000000000000008"," cs=0x0000000000000038",StringComparison.Ordinal),true),(valid.Replace("firmwareCs=0x0000000000000008","firmwareCs=0x0000000000000038",StringComparison.Ordinal),false)];
        foreach(string token in new[]{"TEST_FAIL","OS_SMOKE_FAIL","CLROS_M02_014_HOST_READY","CLROS_M02_014_PAGE_IMAGE_HOST_READY","CLROS_M02_014_TRANSITION_HOST_READY","CLROS_M02_014_EFI_HOST_READY","M02_PAGE_FAULT_CR2_ERROR_CODE_FAILED","M02_PAGE_FAULT_LAYOUT"})
            for(int split=1;split<token.Length;split++)cases.Add((token[..split]+"\r\n"+token[split..]+"\r\n"+valid,false));
        foreach(string selector in new[]{"0000000000000000","000000000000000C","000000000000003B","0000000000010008"})
            cases.Add((valid.Replace("firmwareCs=0x0000000000000008","firmwareCs=0x"+selector,StringComparison.Ordinal).Replace(" cs=0x0000000000000008"," cs=0x"+selector,StringComparison.Ordinal),false));
        string absentPattern=@"levelsRead=0x[0-9A-F]{16} pml4e=0x[0-9A-F]{16} pdpte=0x[0-9A-F]{16} pde=0x[0-9A-F]{16} pte=0x[0-9A-F]{16}";
        const string rootAbsent="levelsRead=0x0000000000000001 pml4e=0x0000000000000000 pdpte=0x0000000000000000 pde=0x0000000000000000 pte=0x0000000000000000";
        cases.Add((Regex.Replace(valid,absentPattern,rootAbsent),false));
        string far=Regex.Replace(valid,@"0x[0-9A-F]{16}",m=> {
            ulong value=Convert.ToUInt64(m.Value[2..],16);
            return value>=0x100000 && value<0x400000 ? "0x"+(value+0x10000000000).ToString("X16",System.Globalization.CultureInfo.InvariantCulture):m.Value;
        });
        far=Regex.Replace(far,@"M02_PAGE_FAULT_ABSENT role=zero [^\r\n]*","M02_PAGE_FAULT_ABSENT role=zero linear=0x0000000000000000 "+rootAbsent);
        cases.Add((far,true));
        string[] lines=valid.Split("\r\n");
        for(int i=4;i<16;i++) {
            var changed=lines.ToList();changed.RemoveAt(i);cases.Add((string.Join("\r\n",changed),false));
            changed=lines.ToList();changed.Insert(i,lines[i]);cases.Add((string.Join("\r\n",changed),false));
            changed=lines.ToList();changed[i]+=" ";cases.Add((string.Join("\r\n",changed),false));
            changed=lines.ToList();changed[i]+="\0";cases.Add((string.Join("\r\n",changed),false));
            if(i<15){changed=lines.ToList();(changed[i],changed[i+1])=(changed[i+1],changed[i]);cases.Add((string.Join("\r\n",changed),false));}
        }
        var numeric=Regex.Matches(valid,@"0x[0-9A-F]{16}");Require(numeric.Count==57,"independent 57-field fixture");
        foreach(Match m in numeric) {
            string invalidValue = m.Index == numeric[5].Index || m.Index == numeric[8].Index ? "0x0000000000000000" : "0xFFFFFFFFFFFFFFFF";
            foreach(string replacement in new[]{invalidValue,"0x000000000000000g","0x0",m.Value+"0"}) cases.Add((valid[..m.Index]+replacement+valid[(m.Index+m.Length)..],false));
        }
        foreach(string suffix in new[]{"\r","\n"," ","firmware returned\r\n","TEST_FAIL late\r\n","\0"}) cases.Add((valid+suffix,false));
        cases.Add((valid.Replace("pml4e=0x0000000000301003","pml4e=0x0000000000301023",StringComparison.Ordinal),true));
        foreach(var mutation in new[]{("pml4e=0x0000000000301003","pml4e=0x0000000000304003",false),("pdpte=0x0000000000302023","pdpte=0x0000000000304023",false),("pml4e=0x0000000000301003","pml4e=0x0000000000301023",true)}) {
            int index=valid.IndexOf(mutation.Item1,StringComparison.Ordinal);
            cases.Add((valid[..index]+mutation.Item2+valid[(index+mutation.Item1.Length)..],mutation.Item3));
        }
        foreach(string bad in new[]{"0x0000000000301043","0x0000000000301083","0x8000000000301003","0x0000000000300003","0x0000000000400003"})cases.Add((valid.Replace("pml4e=0x0000000000301003","pml4e="+bad,StringComparison.Ordinal),false));
        cases.Add((valid.Replace("pte=0x0000000000000000","pte=0x0000000000200063",StringComparison.Ordinal),false));
        var records=cases.Select((x,i)=>new{Path=Path.Combine(dir,i+".log"),x.Accept}).ToArray();
        for(int i=0;i<cases.Count;i++)File.WriteAllText(records[i].Path,cases[i].Text,new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir,"cases.json"),JsonSerializer.Serialize(records));
        string script="param([string]$Root,[string]$Cases)\n$ErrorActionPreference='Stop'\n"+ExtractFunctions+"\n"+"""
            $names=@('Assert-M02PageFaultCr2ErrorCodeTranscript','Assert-M02PageFaultRunnerResult','Test-M02PageFaultCr2ErrorCodeOwnedMarker')
            LoadFunctions (Join-Path $Root 'scripts/run-qemu-smoke.ps1') $names
            $script:M02PageFaultCr2ErrorCodeReadyMarker='__READY__'
            $records=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($Cases))
            foreach($case in $records) {
                $accepted=$true;$reason=''
                try { Assert-M02PageFaultCr2ErrorCodeTranscript $case.Path } catch {$accepted=$false;$reason=$_.Exception.Message}
                if($accepted -ne $case.Accept){throw ($case.Path+' accepted='+$accepted+' '+$reason)}
            }
            foreach($code in @('0','1','-1','-2147483648','2147483647')) {Assert-M02PageFaultRunnerResult @('SERIAL_MARKER_DRAIN milliseconds=750',('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$code))}
            foreach($code in @('none','-0','01','+1','2147483648','-2147483649','99999999999999999999')) {
                $ok=$false;try{Assert-M02PageFaultRunnerResult @('SERIAL_MARKER_DRAIN milliseconds=750',('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$code));$ok=$true}catch{}
                if($ok){throw 'invalid native exit'}
            }
            foreach($line in @('QEMU_PROCESS_RESULT disposition=exited exitCode=0','QEMU_PROCESS_RESULT disposition=cleanup-failed exitCode=0','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1 ')) {
                $ok=$false;try{Assert-M02PageFaultRunnerResult @('SERIAL_MARKER_DRAIN milliseconds=750',$line);$ok=$true}catch{}
                if($ok){throw 'invalid lifecycle'}
            }
            Write-Output ('TEST_PASS m02.014.efi.transcript cases='+$records.Count)
            """.Replace("__READY__",Ready,StringComparison.Ordinal);
        Result r=PowerShell(dir,script,"-Root",root,"-Cases",Path.Combine(dir,"cases.json"));Require(r.Code==0,r.Output);
    }
    private static void TestProfiles()
    {
        string root=Root();Result baseline=Qemu();Save("default-dry",baseline);Require(baseline.Code==0&&!baseline.Output.Contains("TEST_START m02.014;",StringComparison.Ordinal),baseline.Output);
        Result enabled=Qemu("-BuildEfi","-"+Flag);Save("enabled-dry",enabled);Require(enabled.Code==0&&enabled.Output.Contains("M02_PAGE_FAULT_LAYOUT",StringComparison.Ordinal)&&enabled.Output.Contains("terminalProfile=true",StringComparison.Ordinal),enabled.Output);
        foreach(string script in new[]{"scripts/run-qemu-smoke.ps1","scripts/build-efi-shell.ps1","scripts/test-efi-artifact.ps1"}) {
            string arguments=script.Contains("run-qemu",StringComparison.Ordinal)?"-DryRun -BuildEfi":script.Contains("build-efi",StringComparison.Ordinal)?"-PreflightOnly -LogPath artifacts/issue517-tests/rejected.log":"-EfiArtifact missing.EFI";
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
                Result r=Process(root,"powershell.exe","-NoProfile","-Command","& '"+Path.Combine(root,script)+"' "+arguments+" -"+Flag+" -"+option+opposing);
                Save("conflict-"+option,r);Require(r.Code!=0&&r.Output.Contains("m02_page_fault_cr2_error_code_profile_conflict",StringComparison.Ordinal),r.Output);
            }
        }
        Result endian=Qemu("-BuildEfi","-"+Flag,"-EnableM04IndependentEndianConformanceProbe");Require(endian.Code==0,"optional independent endian "+endian.Output);
        foreach(string[] args in new[]{new[]{"-"+Flag},new[]{"-BuildEfi","-"+Flag,"-SkipImageBuild"},new[]{"-BuildEfi","-"+Flag,"-M01RawObservationLog","artifacts/forbidden.log"},new[]{"-RequireMarker",Ready}}) {Result r=Qemu(args);Save("invalid-profile",r);Require(r.Code!=0,r.Output);}
    }
    private static void TestFirewall()
    {
        string dir=Scratch("firewall");Result r=PowerShell(dir,"param([string]$Root)\n$ErrorActionPreference='Stop'\n"+ExtractFunctions+"\n"+"""
            LoadFunctions (Join-Path $Root 'scripts/build-efi-shell.ps1') @('Get-M01Utf8Sha256','Get-NostdlibEfiActiveSource','Test-M01ActiveSourceSurface')
            $name='..\OS.Kernel\Diagnostics\SeparateDisabledDefaultPageFaultPageTables.cs'
            $text=[IO.File]::ReadAllText((Join-Path $Root 'src/OS.Kernel/Diagnostics/SeparateDisabledDefaultPageFaultPageTables.cs')).Replace("`r`n","`n")
            foreach($value in @($text,$text.Replace("`n","`r`n"))) {if(-not(Test-M01ActiveSourceSurface $value $name).Succeeded){throw 'exact identity rejected'}}
            foreach($value in @(($text+' '),($text+"`r"),$text.Replace('4096','4095'),($text+"`n// changed"))) {if((Test-M01ActiveSourceSurface $value $name).Succeeded){throw 'changed content accepted'}}
            foreach($value in @($name.ToLowerInvariant(),($name+' '),$name.Replace('\','/'),$name.Replace('Diagnostics\','Diagnostics\.\'),$name.Insert(4,[string][char]0),'other.cs')) {if((Test-M01ActiveSourceSurface $text $value).Succeeded){throw 'wrong path accepted'}}
            Write-Output 'TEST_PASS m02.014.efi.firewall'
            ""","-Root",Root());Require(r.Code==0,r.Output);
    }
    private static void TestArtifacts()
    {
        string root=Root();
        foreach(bool enabled in new[]{false,true}) {
            string id="issue517-tests-"+Guid.NewGuid().ToString("N"),dir=Scratch(enabled?"artifact-enabled":"artifact-default");
            string artifact="artifacts/efi/"+id+".EFI";
            Result r=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File","scripts/build-efi-shell.ps1","-EfiOutputPath",artifact,"-LogPath",Path.Combine(dir,"publish.log"),..(enabled?new[]{"-"+Flag}:Array.Empty<string>())]);
            File.WriteAllText(Path.Combine(dir,"run.log"),r.Output);Require(r.Code==0&&r.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal)&&r.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),r.Output);
            string link=Path.ChangeExtension(Path.Combine(root,artifact),"link.json");
            if(!File.Exists(link))link=Path.Combine(root,artifact)+".link.json";
            using JsonDocument document=JsonDocument.Parse(File.ReadAllText(link));JsonElement evidence=document.RootElement;
            var inputs=evidence.GetProperty("sourceClosureInputs").EnumerateArray().ToArray();Require(inputs.Length==129,"129 raw authority inputs");
            foreach(var input in inputs)Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,input.GetProperty("path").GetString()!)))).Equals(input.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"raw source identity");
            var evaluation=evidence.GetProperty("postRestoreEvaluation");Require(evaluation.GetProperty("compileCount").GetInt32()==114,"114 evaluated files");
            var properties=evaluation.GetProperty("properties");var flags=properties.EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)).ToArray();Require(flags.Length==47,"complete 47-switch vector");
            foreach(var flag in flags)Require(flag.Value.GetString()==(enabled&&(Prerequisites.Contains(flag.Name,StringComparer.Ordinal)||flag.Name==Flag)?"true":"false"),"actual evaluated "+flag.Name);
            string assembly=Path.Combine(root,"src/OS/bin/Release/net10.0/win-x64/OS.dll");using var reader=new PEReader(File.OpenRead(assembly));Require(reader.GetMetadataReader().AssemblyReferences.Count==0,"actual no-stdlib metadata");
        }
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
    private static string Adapter(string text) => Between(text,"    private static class M02PageFaultAdapter","#endif");
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
    private static string Scratch(string name){string path=Path.Combine(Root(),"artifacts/issue517-tests",name,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    private static string Root(){for(DirectoryInfo? d=new(Environment.CurrentDirectory);d!=null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"CLROS.slnx")))return d.FullName;throw new DirectoryNotFoundException();}
    private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private readonly record struct Result(int Code,string Output);

}
