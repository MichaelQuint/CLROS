using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
#if ARRAY_CORE_TEST
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

// Actual adapter POD core only: these are owned buffers, never CLR object addresses.
internal static unsafe class ArrayCoreTestProgram
{
    private static int Main()
    {
        Run("mixed exact headers and scalar storage",Mixed);
        Run("authentic token and bounded prefix rejection",Rejected);
        Run("actual before-bind and mixed exhaustion rejection",Exhaustion);
        Run("scratch extent and zero managed allocation",Geometry);
        Console.WriteLine("TEST_PASS array.core.tests"); return 0;
    }
    private static void Run(string name,Action test)
    { Console.WriteLine("TEST_START array.core "+name);try{test();Console.WriteLine("TEST_PASS array.core "+name);}catch(Exception e){Console.WriteLine("TEST_FAIL array.core "+name+" "+e);throw;} }
    private static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static void W32(byte* p,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(p,4),value);
    private static void W64(byte* p,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(p,8),value);
    private static int Length(ulong id)=>id==3?3:id==5?1:0;
    private static ulong Size(ulong id)=>id==3?48UL:id==4||id==5?32UL:24UL;
    private static ulong Scalar(ulong id)=>id==1?0x1111111111111111UL:id==3?0x3333333333333333UL:id==4?0x2222222222222222UL:id==5?0x5AUL:0;
    private static ulong Offset(ulong id)=>id==1?8UL:id==3?24UL:id==4||id==5?16UL:0;
    private static void Sequence(F f,bool oracle)
    {
        ulong used=0;
        for(ulong id=1;id<=5;id++)
        {
            ulong size=Size(id);int length=Length(id);bool array=id==2||id==3||id==5;
            new Span<byte>(f.Page+used,(int)size).Fill(0xBC);
            byte[] before=oracle?f.Snapshot():[];
            var result=f.Mixed(id,length);
            Need(result.Succeeded && result.Leaf.UsedBefore==used && result.Leaf.UsedAfter==used+size &&
                result.Leaf.AllocationCount==id && result.Observation.Reference==(ulong)f.Page+used+8 &&
                result.Observation.ZeroBytes==size-(array?12UL:8UL) && result.Observation.ZeroOr==0,"mixed result id="+id);
            if(oracle)
            {
                byte[] expected=new byte[size];BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8),f.MixedToken(id));
                if(array)BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(16),length);
                Need(new ReadOnlySpan<byte>(f.Page+used,(int)size).SequenceEqual(expected),"all independent header/padding/payload bytes");
                f.Only(before,(0x3100,80),(0x3200,96),(0x4000,6672),(0x8000+(int)used,(int)size));
                if(array)Need(result.Observation.ElementTable==f.Token(id==3?7UL:6UL) &&
                    result.Observation.ElementPrefix0==((24UL<<32)|(id==3?0x28000000UL:0x14000000UL)),"related element prefix");
            }
            ulong reference=result.Leaf.ObjectReference,scalar=Scalar(id),offset=Offset(id);
            if(id==5)((byte*)reference)[offset]=(byte)scalar;else if(id!=2)W64((byte*)reference+offset,scalar);
            Need(f.Done(id,reference,length,scalar)==RhpAdmissionStatus.Success,"actual scalar readback id="+id);
            used+=size;
        }
        Need(used==160 && f.Bump->UsedBytes==160 && f.Bump->AllocationCount==5 && f.Control->Completed==5 &&
            f.Check()==RhpAdmissionStatus.Success && f.Retained().IsReady && !f.Retained().ContentsAreZeroNow,"single mixed state160/5 retained");
    }
    private static void Mixed()
    {
        using F f=new();Need(f.Prepare()==RhpAdmissionStatus.Success && f.Bind().Succeeded,"one original bind");Sequence(f,true);
        foreach(int offset in new[]{16,40,68,88,136,144,159})
        {
            f.Page[offset]^=1;byte[] before=f.Snapshot();Need(f.Mixed(1,0).Status==RhpAdmissionStatus.PriorObjectChanged,"prior byte corruption");f.Unchanged(before);f.Page[offset]^=1;
        }
        Need(f.Bind().Status==RhpNewFastStatus.AlreadyInitialized,"no second binder");
    }
    private static void Rejected()
    {
        for(int variant=0;variant<10;variant++)
        {
            using F f=new();Need(f.Prepare()==RhpAdmissionStatus.Success,"prepare");
            ulong token=f.MixedToken(2),expected=token,element=f.Token(6);int length=0;
            switch(variant)
            {
                case 0:token=ulong.MaxValue-7;break;
                case 1:element=ulong.MaxValue-7;break;
                case 2:W64((byte*)token+8,f.Token(7));break;
                case 3:W32((byte*)token,0xE0020008);break;
                case 4:W32((byte*)token+4,32);break;
                case 5:W32((byte*)element,0x28000000);break;
                case 6:W32((byte*)element+4,32);break;
                case 7:W32(f.Image+0x1AC,0xC0000040);break;
                case 8:length=1;break;
                case 9:f.Control->InProgress=1;break;
            }
            byte[] before=f.Snapshot();var r=RhpNewFastAllocationCore.AllocateMixed(f.Access,f.Bump,f.Control,f.Records,token,expected,2,3,length,element);
            Need(!r.Succeeded && r.Status!=RhpAdmissionStatus.AllocationRejected,"pre-read/fixture rejection "+variant);f.Unchanged(before);
        }
    }
    private static void Exhaustion()
    {
        using F unbound=new();Need(unbound.Prepare()==RhpAdmissionStatus.Success,"beforebind prepare");
        byte[] page=unbound.PageSnapshot(),state=unbound.BumpSnapshot();var rejection=unbound.Mixed(2,0);
        Need(rejection.Status==RhpAdmissionStatus.AllocationRejected && rejection.Leaf.Status==RhpNewFastStatus.NotInitialized,"genuine leaf status5");
        unbound.PageUnchanged(page);unbound.BumpUnchanged(state);
        using F f=new();Need(f.Prepare()==RhpAdmissionStatus.Success && f.Bind().Succeeded,"bind");Sequence(f,true);
        for(int pair=0;pair<82;pair++)
        {
            var fixedResult=f.Mixed(1,0);Need(fixedResult.Succeeded,"legitimate fixed fill");W64((byte*)fixedResult.Leaf.ObjectReference+8,0x5555555555555555);
            var arrayResult=f.Mixed(2,0);Need(arrayResult.Succeeded,"legitimate byte zero fill");
            Need(f.Check()==RhpAdmissionStatus.Success,"five prior objects persist");
        }
        Need(f.Bump->UsedBytes==4096 && f.Bump->AllocationCount==169 && f.Control->Completed==5,"no extra cursor/observation slot");
        for(int offset=160;offset<4096;offset+=48)
        {
            byte[] expected=new byte[48];BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8),f.Token(1));
            BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(16),0x5555555555555555);BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(32),f.Token(2));
            Need(new ReadOnlySpan<byte>(f.Page+offset,48).SequenceEqual(expected),"all legitimate fill bytes");
        }
        page=f.PageSnapshot();state=f.BumpSnapshot();rejection=f.Mixed(3,1);
        Need(rejection.Status==RhpAdmissionStatus.AllocationRejected && rejection.Leaf.Status==RhpNewFastStatus.Exhausted,"actual pointer-size array leaf15");f.PageUnchanged(page);f.BumpUnchanged(state);
    }
    private static void Geometry()
    {
        Need(sizeof(RhpNewFastHeapAccess)==104 && sizeof(RhpNewFastBumpState)==80 && sizeof(RhpAllocationControl)==96 &&
            sizeof(RhpObjectObservation)==256 && sizeof(RhpObservationStorage)==6672,"exact shared POD geometry");
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<RhpObservationStorage>(),"no object references in observations");
        using F f=new();byte[] before=f.Snapshot();
        var alias=(RhpObservationStorage*)(f.Page-6672+8);
        Need(RhpNewFastAllocationCore.Prepare(f.Original,f.Access,f.Bump,f.Control,alias,f.Image,16384)==RhpAdmissionStatus.StorageOverlap,"last eight bytes overlap full backing");f.Unchanged(before);
        Need(f.Prepare()==RhpAdmissionStatus.Success && f.Bind().Succeeded,"prepare");
        // Warm actual core, reset only fixture-owned storage, then measure allocator calls alone.
        for(int i=0;i<2;i++)
        {
            f.Reset();f.PrefixBytes(2,0xE0020001,24,f.Token(6));f.PrefixBytes(6,0x14000000,24,0);f.Prepare();f.Bind();
            var r=f.Mixed(1,0);W64((byte*)r.Leaf.ObjectReference+8,1);f.Done(1,r.Leaf.ObjectReference,0,1);
            long start=GC.GetAllocatedBytesForCurrentThread();r=f.Mixed(2,0);var done=f.Done(2,r.Leaf.ObjectReference,0,0);long end=GC.GetAllocatedBytesForCurrentThread();
            Need(r.Succeeded && done==RhpAdmissionStatus.Success && end==start,"zero CLR allocation in actual shared array core");
        }
        Console.WriteLine("ARRAY_CORE_POD access=104 bump=80 control=96 observation=256 records=6672 used=160 count=5");
    }
    private sealed class F:IDisposable
    {
        internal readonly byte* Storage=(byte*)NativeMemory.AlignedAlloc(98304,4096);
        internal RhpNewFastHeapAccess Original;
        internal RhpNewFastHeapAccess* Access=>(RhpNewFastHeapAccess*)(Storage+0x3000);
        internal RhpNewFastBumpState* Bump=>(RhpNewFastBumpState*)(Storage+0x3100);
        internal RhpAllocationControl* Control=>(RhpAllocationControl*)(Storage+0x3200);
        internal RhpObservationStorage* Records=>(RhpObservationStorage*)(Storage+0x4000);
        internal byte* Page=>Storage+0x8000;
        internal byte* Image=>Storage+0xC000;
        internal F(){Need(Storage!=null,"native storage");Reset();
            PrefixBytes(2,0xE0020001,24,Token(6)); PrefixBytes(3,0xE0020008,24,Token(7));
            PrefixBytes(6,0x14000000,24,0); PrefixBytes(7,0x28000000,24,0);
        }
        internal void PrefixBytes(ulong id,uint flags,uint size,ulong related)
        { W32((byte*)Token(id),flags);W32((byte*)Token(id)+4,size);W64((byte*)Token(id)+8,related); }
        internal ulong MixedToken(ulong id)=>Token(id==5?2UL:id);
        internal RhpAllocationAttempt Mixed(ulong id,int length)=>RhpNewFastAllocationCore.AllocateMixed(Access,Bump,Control,Records,
            MixedToken(id),MixedToken(id),id,id==1||id==4?1UL:id==3?4UL:3UL,length,id==1||id==4?0:Token(id==3?7UL:6UL));
        internal RhpAdmissionStatus Done(ulong id,ulong reference,int length,ulong scalar)=>
            RhpNewFastAllocationCore.CompleteMixed(Access,Bump,Control,Records,id,reference,length,scalar,scalar);

        internal void Reset()
        {
            new Span<byte>(Storage,98304).Fill(0xA5);
            Original=new RhpNewFastHeapAccess{Allocator=(BootPageAllocatorState*)(Storage+0x100),AllocatorCapacity=1,
                Nodes=(BootPageFreeListNode*)(Storage+0x200),NodeCount=1,NodeCapacity=1,
                Reservations=(BootReservedRange*)(Storage+0x1000),ReservationCount=5,ReservationCapacity=5,ParentIndex=4,
                Heap=(ManagedHeapPageRangeState*)(Storage+0x2000),HeapCapacity=216,Backing=Page,BackingCapacity=8192};
            *Original.Allocator=new BootPageAllocatorState(true,4096,0,1,2,8192,0,0);
            Original.Nodes[0]=new BootPageFreeListNode(0,0,0x400000,0x402000,2,8192,-1);
            for(int i=0;i<5;i++)
            {
                PhysicalAddress.TryCreateFromUefiBoundary(i==4?0x400000UL:0x100000UL+(ulong)i*0x10000,out var physical);
                Need(BootReservedRange.TryCreate(i==4?BootReservationKind.AllocatorArena:BootReservationKind.LoadedImage,
                    physical,i==4?2UL:1UL,out Original.Reservations[i])==BootReservedRangeStatus.Success,"flat fixture ledger");
            }
            new Span<byte>(Original.Heap,216).Clear();new Span<byte>(Access,104).Clear();new Span<byte>(Bump,80).Clear();
            new Span<byte>(Control,96).Clear();new Span<byte>(Records,6672).Clear();new Span<byte>(Image,16384).Clear();
            var reserved=ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(Original.Allocator,1,Original.Nodes,1,1,
                Original.Reservations,5,5,4,Page,8192,Original.Heap,216);Need(reserved.IsReady,"actual original reserve");
            W32(Image,0x5A4D);W32(Image+60,0x80);W32(Image+0x80,0x4550);W32(Image+0x84,0x00018664);
            W32(Image+0x94,0xF0);W32(Image+0x98,0x20B);W32(Image+0xD0,0x4000);W32(Image+0xD4,0x400);
            W32(Image+0x190,0x1000);W32(Image+0x194,0x1000);W32(Image+0x198,0x1000);W32(Image+0x1AC,0x40000040);
            for(ulong id=1;id<=10;id++)
            {
                uint flags=id<=4?0x50000000U:id is 5 or 6?0x20000000U:0x40000000U;
                if(id==2 || id==3 || id==8 || id==9) flags|=0x01000000;
                if(id==3 || id==4 || id==9)flags|=0x00800000;
                if(id==10)flags=0x48000300; // Nullable element18 and permitted encoded value offset.
                W32((byte*)Token(id),flags);W32((byte*)Token(id)+4,Size(id));
                W64((byte*)Token(id)+8,0xFEDCBA9876543210);W64((byte*)Token(id)+16,0x1234567800020001);
            }
        }
        internal uint Size(ulong id)=>id is 2 or 3 or 4 or 8?32U:24U;
        internal ulong Token(ulong id)=>(ulong)Image+0x1100+(id-1)*32;
        internal RhpAdmissionStatus Prepare()=>RhpNewFastAllocationCore.Prepare(Original,Access,Bump,Control,Records,Image,16384);
        internal RhpNewFastResult Bind()=>RhpNewFastAllocationCore.Bind(Access,Bump,Control,Records);
        internal RhpAllocationAttempt Allocate(ulong id,ulong token=0)=>RhpNewFastAllocationCore.Allocate(Access,Bump,Control,Records,
            token==0?Token(id):token,Token(id),id,id<=4?1UL:2UL);
        internal RhpAdmissionStatus Complete(ulong id,ulong reference)
        {
            ulong scalar=id<=4?id*1111:0,offset=id<=4?(id==1?8UL:16UL):0;
            if(id<=4)W64((byte*)reference+offset,scalar);
            return RhpNewFastAllocationCore.Complete(Access,Bump,Control,Records,id,reference,offset,scalar,scalar);
        }
        internal RhpAdmissionStatus Check()=>RhpNewFastAllocationCore.CheckCompleted(Access,Bump,Control,Records);
        internal ManagedHeapPageRangeResult Retained()=>RhpNewFastAllocationCore.Retained(Original);
        internal bool Prefix(ulong token,ulong expected)=>RhpNewFastAllocationCore.TryReadPrefix(Image,16384,token,expected,out _,out _,out _,out _);
        internal byte[] Snapshot()=>new ReadOnlySpan<byte>(Storage,98304).ToArray();
        internal byte[] PageSnapshot()=>new ReadOnlySpan<byte>(Page,4096).ToArray();
        internal byte[] BumpSnapshot()=>new ReadOnlySpan<byte>(Bump,80).ToArray();
        internal void Unchanged(byte[] x)=>Need(new ReadOnlySpan<byte>(Storage,98304).SequenceEqual(x),"all supplied bytes unchanged");
        internal void PageUnchanged(byte[] x)=>Need(new ReadOnlySpan<byte>(Page,4096).SequenceEqual(x),"all4096 actual page bytes unchanged");
        internal void BumpUnchanged(byte[] x)=>Need(new ReadOnlySpan<byte>(Bump,80).SequenceEqual(x),"all80 actual bump bytes unchanged");
        internal void Only(byte[] before,params(int Start,int Count)[] allowed)
        {
            for(int i=0;i<before.Length;i++)if(before[i]!=Storage[i] && !allowed.Any(x=>i>=x.Start && i<x.Start+x.Count))
                throw new InvalidOperationException("outside allowed mutation at "+i);
        }
        public void Dispose()=>NativeMemory.AlignedFree(Storage);
    }
}
#else
internal static class M04ArrayAllocationEfiIntegrationTests
{
    private const string Flag="EnableM04SzArrayAllocationProbe";
    private static readonly string[] Chain=["EnableM01LoadedImageStackProbe","EnableM01LiveAllocatorProbe","EnableM04IndependentEndianConformanceProbe",
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","EnableM04ArithmeticConformanceProbe","EnableM04LayoutContractProbe","EnableM04ManagedHeapPageRangeProbe","EnableM04RhpNewFastProbe"];
    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("source profile and original handoff",TestSource);
        Run("actual POD core and bounded PE",TestCore);
        Run("ordinary wrapper admission and strict existing firewall",TestFirewall);
        Run("strongest full transcript and negatives",TestTranscript);
        Run("explicit profile prerequisites and conflicts",TestProfiles);
        Run("actual default and three array artifacts",TestArtifacts);
    }
    private static void Run(string name,Action test)
    {
        Console.WriteLine("TEST_START m04.008.efi "+name);
        try{test();Console.WriteLine("TEST_PASS m04.008.efi "+name);}
        catch(Exception ex){Console.WriteLine("TEST_FAIL m04.008.efi "+name+" "+ex.Message);throw;}
    }
    private static void TestCore()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Scratch(root,"core");
        string includes=string.Join('\n',new[]{"src/OS/Runtime/RhpNewFastAllocation.cs","tests/OS.HostTests/M04ArrayAllocationEfiIntegrationTests.cs"}
            .Select(x=>"<Compile Include=\""+SecurityElement.Escape(Path.Combine(root,x))+"\"/>"));
        File.WriteAllText(Path.Combine(directory,"Core.csproj"),"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion>
            <OutputType>Exe</OutputType><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>ARRAY_CORE_TEST</DefineConstants><NoWarn>0649</NoWarn></PropertyGroup><ItemGroup>
            """+includes+"<ProjectReference Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj"))+"\"/></ItemGroup></Project>");
        Result result=Process(root,"dotnet",["run","--project",Path.Combine(directory,"Core.csproj"),"-c","Release","--nologo"]);
        File.WriteAllText(Path.Combine(directory,"process.log"),result.Output);File.WriteAllText(Path.Combine(directory,"exit.txt"),result.Code.ToString());
        Need(result.Code==0 && result.Output.Contains("TEST_PASS array.core.tests",StringComparison.Ordinal),result.Output);
    }
    private static void TestSource()
    {
        string root=HostToolProcess.FindRepositoryRoot();XDocument project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        string[] items=project.Descendants("Compile").Select(x=>x.Attribute("Include")!.Value+"|"+(x.Attribute("Link")?.Value??"")).ToArray();
        Need(items.Length==95 && items[80]=="..\\OS.Kernel\\Runtime\\GuestTestArrayAllocationLengthOverflowChecks.cs|Runtime\\GuestTestArrayAllocationLengthOverflowChecks.cs","one real array wrapper include");
        foreach(var pair in new[]{(Flag,"M04_SZ_ARRAY_ALLOCATION_PROBE"),("EnableM04SzArrayAllocationBeforeBindProbe","M04_SZ_ARRAY_ALLOCATION_BEFORE_BIND_PROBE"),("EnableM04SzArrayAllocationExhaustionProbe","M04_SZ_ARRAY_ALLOCATION_EXHAUSTION_PROBE")}) {
            var value=project.Descendants(pair.Item1).Single();Need(value.Value=="false" && value.Attribute("Condition")?.Value=="'$("+pair.Item1+")' == ''","default false");
            Need(project.Descendants("PropertyGroup").Single(x=>x.Attribute("Condition")?.Value=="'$("+pair.Item1+")' == 'true'").Element("DefineConstants")?.Value=="$(DefineConstants);"+pair.Item2,"exact selected compiler symbol");
        }
        foreach(string required in Chain)Need(project.Descendants("Error").Any(x=>x.Attribute("Condition")?.Value=="'$("+Flag+")' == 'true' and '$("+required+")' != 'true'"),"explicit prerequisite "+required);
        foreach(var file in new[]{("GuestTestArrayAllocationLengthOverflowChecks.cs","3181b70d4a9bad1dd957ecba9e9d6c860275780cc26b61961019f9341c1138a9"),("GuestTestRhpNewFast.cs","da93a400753bb57a8d17ee8a07dbd6b0b0c8949d1c70c23f44c22a902d7cd14a")})
            Need(Hash(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Runtime",file.Item1)).Replace("\r\n","\n",StringComparison.Ordinal)))==file.Item2,"frozen host source");
        string shim=File.ReadAllText(Path.Combine(root,"src/OS/Runtime/MinimalRuntime.cs")),adapter=File.ReadAllText(Path.Combine(root,"src/OS/Runtime/RhpNewFastAllocation.cs"));
        Need(shim.Contains("private int _numComponents;",StringComparison.Ordinal) && shim.Contains("public int Length => _numComponents;",StringComparison.Ordinal),"minimal actual compiler Array contract");
        Need(adapter.Contains("GuestTestArrayAllocationLengthOverflowChecks.TryAllocate(*access, bump, 80,",StringComparison.Ordinal) &&
            adapter.Contains("prefix.RelatedTypeToken != expectedElement",StringComparison.Ordinal) &&
            adapter.Contains("token != expectedToken",StringComparison.Ordinal) &&
            adapter.Contains("RejectedAllocationUnchanged(bump, bumpBefore, access->Backing, records->BeforeAttempt)",StringComparison.Ordinal) &&
            !adapter.Contains("Serialize(",StringComparison.Ordinal),"actual shared allocator and metadata/rejection/data publication boundary");
        Need(Regex.Matches(adapter,@"private static RhpNewFastBumpState s_bump;").Count==1 &&
            Regex.Matches(adapter,@"GuestTestRhpNewFast\.InitializeOnce\(").Count==1,"one state and binder");
        foreach(string forbidden in new[]{"FreeOnePage(","FreePages(","RhBox(","new object[","throw ","System.Reflection","System.Console"})Need(!adapter.Contains(forbidden,StringComparison.Ordinal),"no runtime scope expansion");
    }
    private static void TestFirewall()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Scratch(root,"firewall"),ps=Path.Combine(directory,"Test.ps1");
        File.WriteAllText(ps,"""
            param([string]$Root)
            $ErrorActionPreference='Stop';$t=$null;$e=$null
            $ast=[System.Management.Automation.Language.Parser]::ParseFile((Join-Path $Root 'scripts/build-efi-shell.ps1'),[ref]$t,[ref]$e)
            if($e.Count){throw 'parse'}
            foreach($name in @('Get-M01Utf8Sha256','Get-NostdlibEfiActiveSource','Test-M01ActiveSourceSurface')) {
                $f=$ast.Find({param($n)$n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name},$true);. ([scriptblock]::Create($f.Extent.Text))
            }
            $name='..\OS.Kernel\Runtime\GuestTestArrayAllocationLengthOverflowChecks.cs'
            $text=[IO.File]::ReadAllText((Join-Path $Root 'src/OS.Kernel/Runtime/GuestTestArrayAllocationLengthOverflowChecks.cs'))
            foreach($good in @($text,$text.Replace("`r`n","`n"))) {if(-not (Test-M01ActiveSourceSurface $good $name).Succeeded){throw 'ordinary scalar wrapper rejected'}}
            foreach($bad in @('using System; public class X {}','public class X { object value = new object(); }')) {if((Test-M01ActiveSourceSurface $bad $name).Succeeded){throw 'generic surface expanded'}}
            Write-Output 'TEST_PASS m04.008.efi.firewall'
            """);
        Result r=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",ps,"-Root",root]);Save(directory,r);Need(r.Code==0,r.Output);
    }
    private static void TestTranscript()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Scratch(root,"transcript");
        string source=File.ReadAllText(Path.Combine(root,"scripts/run-qemu-smoke.ps1"));
        Need(source.Contains("QEMU_SCRIPT_M04_SZ_ARRAY_TRANSCRIPT exactWholeLines=true lines=68 objects=5 used=160 count=5 terminal=true",StringComparison.Ordinal),"reported exact transcript line count");
        string defs=Between(source,"$script:M04IndependentEndianConformanceReadyMarker =","function Invoke-NativeCommand {");
        string functions=Between(source,"function Assert-M04ManagedHeapPageRangeTranscript {","function Assert-M04LayoutContractTranscript {");
        Result dry=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/run-qemu-smoke.ps1"),"-DryRun","-BuildEfi",..Chain.Append(Flag).Select(x=>"-"+x)]);
        Save(Path.Combine(directory,"dryrun"),dry);Need(dry.Code==0,dry.Output);
        string[] chain=dry.Output.Replace("\r","").Split('\n').Single(x=>x.StartsWith("ORDERED_MARKERS ",StringComparison.Ordinal))[16..].Split(';');
        List<string> lines=["TEST_START efi.main",..chain.Take(6),"UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 3","UEFI_MEMORY_MAP_OWNED","BOOT_HANDOFF_READY","UEFI_MEMORY_MAP_NORMALIZED","BOOT_MEMORY_MAP_ACCEPTED","BOOT_MEMORY_REGIONS_READY","BOOT_MEMORY_REGION_COUNT 3","BOOT_PAGE_ALLOCATOR_READY status=Ready regions=3 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0","BOOT_ACPI_RSDP_HANDOFF_READY","UEFI_ACPI_RSDP_READY",..chain.Skip(6)];
        const string identity="allocatorState=0x0000000000100000 nodes=0x0000000000100040 heapState=0x0000000000101000 ledger=0x0000000000102000 parentIndex=4 parentStart=0x0000000000900000 parentEnd=0x0000000000902000 backing=0x0000000000900000 retainedStart=0x0000000000900000 retainedEnd=0x0000000000901000";
        lines[lines.IndexOf("M04_HEAP_PAGE_BOUND")]+=" "+identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_RETAINED")]+=" checkpoint=beforeBootPlan "+identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_INITIALIZED")]+=" pages=1 bytes=4096 freePagesBefore=2 freeBytesBefore=8192 allocatedPagesBefore=0 allocatedBytesBefore=0 freePagesAfter=1 freeBytesAfter=4096 allocatedPagesAfter=1 allocatedBytesAfter=4096 readBytes=4096 contentsZero=true";
        lines[lines.IndexOf("M04_HEAP_PAGE_CURRENT")]+=" freePages=1 freeBytes=4096 allocatedPages=1 allocatedBytes=4096 readBytes=4096 contentsZero=true";
        lines[45]="M04_ARRAY_PREP image=0x0000000001000000 imageBytes=65536 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 control=0x0000000000103200 controlBytes=96 records=0x0000000000104000 recordsBytes=6672";
        lines[46]="M04_ARRAY_BIND image=0x0000000001000000 imageBytes=65536 allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 ledgerBytes=200 parentIndex=4 backing=0x0000000000900000 backingBytes=8192 heap=0x0000000000101000 heapBytes=216 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 status=1 used=0 count=0";
        uint[] sizes=[24,24,48,32,32],lengths=[0,0,3,0,1],offsets=[8,0,24,16,16],widths=[8,0,8,8,1];
        uint[] flags=[0x50000000,0xE0020001,0xE0020008,0x51000000,0xE0020001];
        ulong[] scalars=[0x1111111111111111,0,0x3333333333333333,0x2222222222222222,0x5A];ulong used=0;
        static string H(ulong x)=>"0x"+x.ToString("X16");
        for(int id=1;id<=5;id++) {
            bool array=id is 2 or 3 or 5;uint size=sizes[id-1];ulong mt=0x1001100UL+(ulong)(id==5?1:id-1)*32,allocation=0x900000+used;
            ulong element=array?(id==3?0x1002100UL:0x1002000UL):0;
            ulong element0=array?(24UL<<32)|(id==3?0x28000000UL:0x14000000UL):0;
            lines[45+id*2]=$"M04_ARRAY_OBJECT id={id} route={(array?id==3?"compiled_long_array":"compiled_byte_array":"compiled_fixed")} mt={H(mt)} prefix0={H(((ulong)(id==4?32:24)<<32)|flags[id-1])} prefix1={H(element)} prefix2={H(0x1234567800020001)} flags={H(flags[id-1])} base={size} allocation={H(allocation)} reference={H(allocation+8)} end={H(allocation+size)} usedBefore={used} usedAfter={used+size} count={id} sync={H(0)} observedMt={H(mt)} zeroBytes={size-(array?12:8)} zeroOr={H(0)} length={lengths[id-1]} element={H(element)} element0={H(element0)} element1={H(0)} element2={H(0)}";
            lines[46+id*2]=$"M04_ARRAY_READBACK id={id} length={lengths[id-1]} scalarOffset={offsets[id-1]} scalarWidth={widths[id-1]} scalar={H(scalars[id-1])} referenceSlotBytes={size-16-(array?4:0)-widths[id-1]} referenceSlotOr={H(0)} priorCount={id} priorMismatchBytes=0 canaryMismatchBytes=0";used+=size;
        }
        lines[57]="M04_ARRAY_RETAIN allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 parentIndex=4 backing=0x0000000000900000 heap=0x0000000000101000 bump=0x0000000000103100 heapStart=0x0000000000900000 heapEnd=0x0000000000901000 initialPages=2 freePages=1 allocatedPages=1 readBytes=4096 contentsZero=false used=160 count=5 status=1";
        Need(lines.Count==68 && lines[44]=="TEST_START m04.008.sz_array" && lines[60]=="TEST_START boot.sequence","full68 positions");
        string good=string.Join("\r\n",lines)+"\r\n";
        List<(string Text,bool Accept)> cases=[(good,true),("\u001b[2J\u001b[HUEFI Interactive Shell\r\n"+good,true),(good+"\r\n",true)];
        for(int i=0;i<lines.Count;i++)
        {
            foreach(string replacement in new[]{"",lines[i]+" "," "+lines[i],lines[i]+" diagnostic",lines[i]+"\0",lines[i].ToLowerInvariant(),lines[i]+"\r\n"+lines[i]})
                cases.Add((good.Replace(lines[i]+"\r\n",replacement+"\r\n",StringComparison.Ordinal),false));
        }
        foreach(string bad in new[]{"TE\r\nST_FA\r\nIL firmware\r\n","OS_SMO\r\nKE_FAIL\r\n","M\0\uFEFF04_RHP_BIND\r\n"})cases.Add((bad+good,false));
        foreach(string tail in new[]{"OVMF menu\r\n","partial","\r","OS_SMOKE_PASS\r\n","TEST_FAIL m04.008.sz_array exhausted\r\n"})cases.Add((good+tail,false));
        cases.Add((good.Replace("\r\n","\n",StringComparison.Ordinal),false));cases.Add((good[..^1],false));
        foreach((string from,string to) in new[]{("scalarWidth=1","scalarWidth=8"),("length=3","length=2"),("element=0x0000000001002000","element=0x0000000001002100"),("element0=0x0000001814000000","element0=0x0000001828000000"),("accessBytes=104","accessBytes=103"),("controlBytes=96","controlBytes=95"),("recordsBytes=6672","recordsBytes=4096"),("records=0x0000000000104000","records=0x0000000000103100"),("control=0x0000000000103200","control=0x0000000000101000"),("backingBytes=8192","backingBytes=4096"),("bump=0x0000000000103100","bump=0x0000000000103000"),("imageBytes=65536","imageBytes=18446744073709551616"),("zeroBytes=16","zeroBytes=15"),("usedAfter=24","usedAfter=25"),("scalarOffset=16","scalarOffset=8"),("priorCount=5","priorCount=4"),("contentsZero=false","contentsZero=true"),("count=5 status=1","count=6 status=1"),("flags=0x0000000050000000","flags=0x0000000051000000")})
            cases.Add((good.Replace(from,to,StringComparison.Ordinal),false));
        var manifest=cases.Select((c,i)=>new{Path=Path.Combine(directory,i+".log"),c.Accept}).ToArray();
        for(int i=0;i<cases.Count;i++)File.WriteAllText(manifest[i].Path,cases[i].Text,new UTF8Encoding(false));
        string json=Path.Combine(directory,"cases.json");File.WriteAllText(json,System.Text.Json.JsonSerializer.Serialize(manifest));
        string ps=Path.Combine(directory,"Test.ps1");File.WriteAllText(ps,"$ErrorActionPreference='Stop'\n"+defs+functions+"\n$cases=[IO.File]::ReadAllText('"+json+"')|ConvertFrom-Json\n"+"""
            foreach($case in $cases){$accepted=$true;$reason='';try{Assert-M04SzArrayAllocationTranscript $case.Path}catch{$accepted=$false;$reason=$_.Exception.Message};if($accepted -ne $case.Accept){throw ($case.Path+' accepted='+$accepted+' reason='+$reason)}}
            foreach($exitCode in @('0','-1','2147483647','-2147483648')){Assert-M04ManagedHeapRunnerResult @('SERIAL_MARKER_DRAIN milliseconds=750',('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exitCode))}
            foreach($exitCode in @('none','2147483648','-2147483649','-0','01','+1')){$rejected=$false;try{Assert-M04ManagedHeapRunnerResult @('SERIAL_MARKER_DRAIN milliseconds=750',('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exitCode))}catch{$rejected=$true};if(-not $rejected){throw 'invalid exit admitted'}}
            Write-Output ('TEST_PASS m04.008.efi.transcript cases='+$cases.Count)
            """);
        Result result=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",ps]);Save(directory,result);Need(result.Code==0,result.Output);
    }
    private static void TestProfiles()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Scratch(root,"profiles");
        string before="EnableM04SzArrayAllocationBeforeBindProbe",exhaust="EnableM04SzArrayAllocationExhaustionProbe";
        string[] all=Chain.Append(Flag).ToArray();
        string[] conflicts=["EnableM01OwnedPagingProbe",..XDocument.Load(Path.Combine(root,"src/OS/OS.csproj")).Descendants()
            .Where(x=>x.Name.LocalName.StartsWith("EnableM02",StringComparison.Ordinal)).Select(x=>x.Name.LocalName).Distinct(StringComparer.Ordinal),
            "EnableUefiGoneSimulationProbe","EnableExitBootServicesFreshMapProbe","EnableAcpiPhysicalDiscoveryProbe","EnableM04RhpNewFastBeforeBindFailureProbe","EnableM04RhpNewFastExhaustionFailureProbe","EnableM03MemoryMapCaptureProbe","EnableM04CheckedReferenceBarrierProbe","EnableM03FirmwareEntropySeedProbe","EnableM04TypeManagerModuleRegistrationProbe"];
        Need(conflicts.Length==25,"all unrelated profile conflicts");
        List<string[]> invalid=[..Chain.Select(missing=>all.Where(x=>x!=missing).ToArray()),..conflicts.Select(x=>all.Append(x).ToArray()),[before],[exhaust],[..all,before,exhaust]];
        int id=0;
        foreach(string[] flags in invalid)
        foreach(bool runner in new[]{false,true})
        {
            Result r=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts",runner?"run-qemu-smoke.ps1":"build-efi-shell.ps1"),
                ..(runner?new[]{"-DryRun","-BuildEfi"}:new[]{"-PreflightOnly","-LogPath",Path.Combine(directory,"preflight-"+id+".log")}),..flags.Select(x=>"-"+x)]);
            Save(Path.Combine(directory,(id++).ToString()),r);
            Need(r.Code!=0 && (r.Output.Contains("m04_",StringComparison.Ordinal) || (flags.Contains("EnableM03FirmwareEntropySeedProbe",StringComparer.Ordinal) && r.Output.Contains("m03_firmware_entropy_profile_conflict property=EnableM01LiveAllocatorProbe",StringComparison.Ordinal)) || (flags.Contains("EnableM03MemoryMapCaptureProbe",StringComparer.Ordinal) && r.Output.Contains("m03_memory_map_capture_profile_conflict property=EnableM01LoadedImageStackProbe",StringComparison.Ordinal))),"explicit profile rejection "+string.Join(',',flags)+" "+r.Output);
        }
        foreach(string[] flags in new[]{all,all.Append(before).ToArray(),all.Append(exhaust).ToArray()})
        {
            Result r=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/run-qemu-smoke.ps1"),"-DryRun","-BuildEfi",..flags.Select(x=>"-"+x)]);
            Save(Path.Combine(directory,(id++).ToString()),r);Need(r.Code==0,r.Output);
            Need(r.Output.Contains("TEST_FAIL m04.008.sz_array",StringComparison.Ordinal),"negative profile does not weaken generic failure inventory");
        }
    }
    private static void TestArtifacts()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        string[] all=Chain.Append(Flag).ToArray();
        foreach(var lane in new[]{("default",Array.Empty<string>()),("positive",all),("before-bind",all.Append("EnableM04SzArrayAllocationBeforeBindProbe").ToArray()),("exhaustion",all.Append("EnableM04SzArrayAllocationExhaustionProbe").ToArray())})
        {
            string directory=Scratch(root,"artifact-"+lane.Item1),artifact=Path.Combine(root,"artifacts/efi/issue523-tests-"+Guid.NewGuid().ToString("N")+".EFI");
            Result r=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/build-efi-shell.ps1"),"-EfiOutputPath",artifact,"-LogPath",Path.Combine(directory,"publish.log"),..lane.Item2.Select(x=>"-"+x)]);
            Save(directory,r);
            Need(r.Code==0 && r.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal) && r.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),r.Output);
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(artifact+suffix,Path.Combine(directory,"image.EFI"+suffix));
            foreach(var file in new[]{("src/OS/bin/Release/net10.0/win-x64/OS.dll","OS.dll"),("src/OS/obj/Release/net10.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net10.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(root,file.Item1),Path.Combine(directory,file.Item2));
            using var document=System.Text.Json.JsonDocument.Parse(File.ReadAllText(artifact+".link.json"));var e=document.RootElement;
            var inputs=e.GetProperty("sourceClosureInputs").EnumerateArray().ToArray();Need(inputs.Length==109,"actual109 raw sources");
            foreach(var input in inputs)Need(Hash(File.ReadAllBytes(Path.Combine(root,input.GetProperty("path").GetString()!)))==input.GetProperty("sha256").GetString(),"actual raw source unchanged");
            var evaluation=e.GetProperty("postRestoreEvaluation");Need(evaluation.GetProperty("compileCount").GetInt32()==95,"actual95 compile items");
            var flags=evaluation.GetProperty("properties").EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)).ToArray();
            Need(flags.Length==41,"complete41 evaluated flags");
            foreach(var f in flags)Need(f.Value.GetString()==(lane.Item2.Contains(f.Name,StringComparer.Ordinal)?"true":"false"),"actual requested/evaluated profile "+f.Name);
            TestMetadata(directory,lane.Item1!="default");
            if(lane.Item1!="default")TestLinkedRoutes(root,directory,lane.Item1=="before-bind");
            File.WriteAllText(Path.Combine(directory,"identities.json"),System.Text.Json.JsonSerializer.Serialize(Directory.GetFiles(directory).Select(p=>new{Path=Path.GetFileName(p),Sha256=Hash(File.ReadAllBytes(p))}),new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        }
    }
    private static void TestMetadata(string directory,bool enabled)
    {
        using var pe=new PEReader(File.OpenRead(Path.Combine(directory,"OS.dll")));var md=pe.GetMetadataReader();
        Need(md.AssemblyReferences.Count==0 && md.GetString(md.GetAssemblyDefinition().Name)=="OS","actual zero-reference OS");
        string FullType(TypeDefinitionHandle h){var t=md.GetTypeDefinition(h);return md.GetString(t.Namespace)+"."+md.GetString(t.Name);}
        string AttributeType(CustomAttribute attribute)
        {
            var parent=attribute.Constructor.Kind==HandleKind.MethodDefinition
                ? (EntityHandle)md.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType()
                : md.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
            if(parent.Kind==HandleKind.TypeDefinition)return FullType((TypeDefinitionHandle)parent);
            if(parent.Kind==HandleKind.TypeReference){var t=md.GetTypeReference((TypeReferenceHandle)parent);return md.GetString(t.Namespace)+"."+md.GetString(t.Name);}
            return "";
        }
        var methods=md.TypeDefinitions.SelectMany(t=>md.GetTypeDefinition(t).GetMethods()).ToArray();List<object> evidence=[];
        foreach(string name in new[]{"RhpNewArrayFast","RhpNewPtrArrayFast"})
        {
            var named=methods.Where(h=>md.GetString(md.GetMethodDefinition(h).Name)==name).ToArray();Need(named.Length==1,"sole named helper "+name);
            var method=md.GetMethodDefinition(named[0]);Need(FullType(method.GetDeclaringType())=="System.Runtime.RhpNewFastRuntime","exact export owner");
            Need((method.Attributes&System.Reflection.MethodAttributes.Static)!=0 && (method.Attributes&System.Reflection.MethodAttributes.MemberAccessMask)==System.Reflection.MethodAttributes.Assembly && method.GetGenericParameters().Count==0,"internal static nongeneric helper");
            Need((method.ImplAttributes&System.Reflection.MethodImplAttributes.NoInlining)!=0 && method.RelativeVirtualAddress!=0,"real NoInlining body");
            var sig=md.GetBlobReader(method.Signature);Need(sig.ReadByte()==0 && sig.ReadCompressedInteger()==2 && sig.ReadByte()==0x1C && sig.ReadByte()==0x0F && sig.ReadByte()==0x11,"object(pointer-to-valuetype,int) signature");
            int coded=sig.ReadCompressedInteger();Need((coded&3)==0 && FullType(System.Reflection.Metadata.Ecma335.MetadataTokens.TypeDefinitionHandle(coded>>2))=="Internal.Runtime.MethodTable" && sig.ReadByte()==8 && sig.RemainingBytes==0,"actual MethodTable* and signed Int32");
            int exports=0;
            foreach(var h in methods)foreach(var ah in md.GetMethodDefinition(h).GetCustomAttributes())
            {
                var attr=md.GetCustomAttribute(ah);if(AttributeType(attr)!="System.Runtime.RuntimeExportAttribute")continue;
                var blob=md.GetBlobReader(attr.Value);Need(blob.ReadUInt16()==1,"attribute prolog");string? value=blob.ReadSerializedString();
                Need(blob.ReadUInt16()==0 && blob.RemainingBytes==0,"finite export attribute payload");
                if(value==name){exports++;Need(h==named[0],"sole payload on named export");}
            }
            Need(exports==1,"sole RuntimeExport payload "+name);
            byte[] il=pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;Need(il.Length>0,"initialized IL body");
            evidence.Add(new{Name=name,Rva=method.RelativeVirtualAddress,Signature=Convert.ToHexString(md.GetBlobBytes(method.Signature)),IL=Convert.ToHexString(il),ExportPayloads=exports});
        }
        var array=md.GetTypeDefinition(md.TypeDefinitions.Single(h=>FullType(h)=="System.Array"));var fields=array.GetFields().ToArray();
        Need(fields.Length==1 && md.GetString(md.GetFieldDefinition(fields[0]).Name)=="_numComponents" && md.GetBlobBytes(md.GetFieldDefinition(fields[0]).Signature).AsSpan().SequenceEqual(new byte[]{6,8}),"exact first signed length field");
        Need(array.GetMethods().Any(h=>md.GetString(md.GetMethodDefinition(h).Name)=="get_Length"),"length getter exists");
        Need(md.TypeDefinitions.Any(h=>FullType(h)=="OS.Runtime.RhpNewFastAllocation")==enabled,"actual enabled adapter presence/default absence");
        if(enabled)
        {
            var adapter=md.TypeDefinitions.Single(h=>FullType(h)=="OS.Runtime.RhpNewFastAllocation");
            foreach(string name in new[]{"NewBytes","NewLongs","ByteLength","LongLength","StoreByte","ReadByte","StoreLong","ReadLong","AllocateArray","RunMixed"})
            {
                var h=md.GetTypeDefinition(adapter).GetMethods().Single(h=>md.GetString(md.GetMethodDefinition(h).Name)==name);var m=md.GetMethodDefinition(h);
                byte[] il=pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()!;
                var instructions=DecodeIl(il);string expected=name.StartsWith("New",StringComparison.Ordinal)?"newarr":name.EndsWith("Length",StringComparison.Ordinal)?"ldlen":name=="StoreByte"?"stelem.i1":name=="StoreLong"?"stelem.i8":name=="ReadByte"?"ldelem.u1":name=="ReadLong"?"ldelem.i8":"";
                if(expected.Length!=0)Need(instructions.Any(x=>x.Name==expected),"genuine compiler IL "+name);
                evidence.Add(new{Name=name,Rva=m.RelativeVirtualAddress,IL=Convert.ToHexString(il),Instructions=instructions});
            }
        }
        File.WriteAllText(Path.Combine(directory,"managed-array-metadata.json"),System.Text.Json.JsonSerializer.Serialize(new{Assembly="OS",AssemblyReferences=0,Enabled=enabled,Mvid=md.GetGuid(md.GetModuleDefinition().Mvid),Methods=evidence,GuestAccepted=false},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
    private readonly record struct IlInstruction(int Offset,string Name,int Operand);
    private static IlInstruction[] DecodeIl(byte[] bytes)
    {
        var codes=typeof(System.Reflection.Emit.OpCodes).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Where(f=>f.FieldType==typeof(System.Reflection.Emit.OpCode)).Select(f=>(System.Reflection.Emit.OpCode)f.GetValue(null)!).ToDictionary(o=>(ushort)o.Value);
        List<IlInstruction> result=[];int p=0;
        while(p<bytes.Length)
        {
            int start=p;ushort code=bytes[p++];if(code==0xFE)code=(ushort)(0xFE00|bytes[p++]);var op=codes[code];int operand=0;
            int n=op.OperandType switch {System.Reflection.Emit.OperandType.InlineNone=>0,System.Reflection.Emit.OperandType.ShortInlineBrTarget or System.Reflection.Emit.OperandType.ShortInlineI or System.Reflection.Emit.OperandType.ShortInlineVar=>1,System.Reflection.Emit.OperandType.InlineVar=>2,System.Reflection.Emit.OperandType.InlineI8 or System.Reflection.Emit.OperandType.InlineR=>8,System.Reflection.Emit.OperandType.InlineSwitch=>4+4*BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(p)),_=>4};
            Need(n>=0 && p<=bytes.Length-n,"bounded IL decode");if(n==4)operand=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(p));result.Add(new(start,op.Name!,operand));p+=n;
        }
        return result.ToArray();
    }
    private static void TestLinkedRoutes(string root,string directory,bool beforeBind)
    {
        byte[] image=File.ReadAllBytes(Path.Combine(directory,"image.EFI"));using var reader=new PEReader(new MemoryStream(image));var header=reader.PEHeaders.PEHeader!;
        Need(header.Magic==PEMagic.PE32Plus && reader.PEHeaders.SectionHeaders.Length is >0 and <=8,"bounded x64 image sections");
        byte[] At(ulong rva,int bytes)
        {
            var sections=reader.PEHeaders.SectionHeaders.Where(s=>s.VirtualAddress>=0 && s.VirtualSize>=0 && s.SizeOfRawData>=0 && rva>=(ulong)s.VirtualAddress && rva+(ulong)bytes<=(ulong)s.VirtualAddress+(ulong)s.VirtualSize && rva+(ulong)bytes<=(ulong)s.VirtualAddress+(ulong)s.SizeOfRawData).ToArray();
            Need(sections.Length==1,"one initialized native extent");var s=sections[0];return image.AsSpan(checked(s.PointerToRawData+(int)(rva-(ulong)s.VirtualAddress)),bytes).ToArray();
        }
        var symbols=Regex.Matches(File.ReadAllText(Path.Combine(directory,"image.EFI.link.map")),@"^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(\S+)\s+([0-9A-Fa-f]{16})\s+",RegexOptions.Multiline).ToDictionary(m=>m.Groups[1].Value,m=>Convert.ToUInt64(m.Groups[2].Value,16)-header.ImageBase,StringComparer.Ordinal);
        string llvm=Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Microsoft Visual Studio"),"llvm-objdump.exe",SearchOption.AllDirectories).Where(p=>p.Contains("\\x64\\",StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).First();
        Result dis=Process(root,llvm,["--disassemble","--reloc","--x86-asm-syntax=intel",Path.Combine(directory,"OS.obj")]);Save(Path.Combine(directory,"disassembly"),dis);Need(dis.Code==0,dis.Output);
        File.WriteAllText(Path.Combine(directory,"disassembler.json"),System.Text.Json.JsonSerializer.Serialize(new{Path=llvm,Sha256=Hash(File.ReadAllBytes(llvm))}));
        string Body(string name){var m=Regex.Match(dis.Output,@"^[0-9a-f]+ <"+Regex.Escape(name)+@">:\r?\n(.*?)(?=^[0-9a-f]+ <|\z)",RegexOptions.Multiline|RegexOptions.Singleline);Need(m.Success,"decoded native method "+name);return m.Value;}
        const string prefix="OS_OS_Runtime_RhpNewFastAllocation__";
        ulong Token(string name)
        {
            ulong rva=symbols[prefix+"Type"+name];byte[] code=At(rva,8);Need(code.AsSpan(0,3).SequenceEqual(new byte[]{0x48,0x8D,0x05}) && code[7]==0xC3,"actual Of getter");
            return checked((ulong)((long)rva+7+BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(3))));
        }
        var dir64=new List<ulong>();var relocation=header.BaseRelocationTableDirectory;byte[] blocks=At((ulong)relocation.RelativeVirtualAddress,relocation.Size);
        for(int p=0;p<blocks.Length;){uint page=BinaryPrimitives.ReadUInt32LittleEndian(blocks.AsSpan(p)),size=BinaryPrimitives.ReadUInt32LittleEndian(blocks.AsSpan(p+4));Need(size>=8 && size%2==0 && size<=blocks.Length-p,"bounded reloc block");for(int j=8;j<size;j+=2){ushort e=BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(p+j));if((e>>12)==10)dir64.Add(page+(ulong)(e&4095));else Need((e>>12)==0,"supported actual PE relocation");}p+=(int)size;}
        byte[] coff=File.ReadAllBytes(Path.Combine(directory,"OS.obj"));uint table=BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(8)),count=BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(12));int strings=checked((int)(table+count*18));
        string SymbolName(int entry){if(BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(entry))!=0)return Encoding.ASCII.GetString(coff,entry,8).TrimEnd('\0');int p=checked(strings+(int)BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(entry+4)));int end=Array.IndexOf(coff,(byte)0,p);Need(end>=p,"COFF string");return Encoding.ASCII.GetString(coff,p,end-p);}
        (uint Value,int Section) Symbol(string name){for(uint i=0;i<count;){int p=checked((int)(table+i*18));string n=SymbolName(p);if(n==name)return(BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(p+8)),BinaryPrimitives.ReadInt16LittleEndian(coff.AsSpan(p+12)));i+=(uint)(1+coff[p+17]);}throw new InvalidOperationException("COFF symbol missing "+name);}
        string RelocatedElement(string name,out byte[] raw)
        {
            var symbol=Symbol(name);Need(symbol.Section>0,"defined array MT");int section=20+BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(16))+(symbol.Section-1)*40;
            uint rawStart=BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(section+20));raw=coff.AsSpan(checked((int)(rawStart+symbol.Value)),24).ToArray();
            uint relocStart=BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(section+24));int n=BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(section+32));List<string> found=[];
            for(int i=0;i<n;i++){int p=checked((int)relocStart+i*10);uint offset=BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(p));if(offset<symbol.Value||offset>=symbol.Value+24)continue;Need(offset==symbol.Value+8 && BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(p+8))==1,"exact array prefix ADDR64 field");uint target=BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(p+4));Need(target<count,"relocation target");found.Add(SymbolName(checked((int)(table+target*18))));}
            Need(found.Count==1,"sole related-element prefix relocation");return found[0];
        }
        List<object> rows=[];
        foreach(var route in new[]{("Bytes","Byte","NewBytes","RhpNewArrayFast",0xE0020001U),("Longs","Long","NewLongs","RhpNewPtrArrayFast",0xE0020008U)})
        {
            ulong mt=Token(route.Item1),element=Token(route.Item2);string mtName=symbols.Single(s=>s.Value==mt&&s.Key.StartsWith("??_7",StringComparison.Ordinal)).Key;
            string elementName=symbols.Single(s=>s.Value==element&&s.Key.StartsWith("??_7",StringComparison.Ordinal)).Key;byte[] raw=At(mt,24),ep=At(element,24);
            Need(BinaryPrimitives.ReadUInt32LittleEndian(raw)==route.Item5 && BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4))==24 && BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(8))==header.ImageBase+element,"actual array shape/element pointer");
            Need(RelocatedElement(mtName,out byte[] coffPrefix)==elementName && dir64.Count(x=>x==mt+8)==1,"same array COFF ADDR64 to exact PE DIR64/element");
            BinaryPrimitives.WriteUInt64LittleEndian(coffPrefix.AsSpan(8),checked(BinaryPrimitives.ReadUInt64LittleEndian(coffPrefix.AsSpan(8))+header.ImageBase+element));Need(raw.AsSpan().SequenceEqual(coffPrefix),"all24 relocated array prefix bytes");
            foreach(ulong token in new[]{mt,element}){var section=reader.PEHeaders.SectionHeaders.Single(s=>token>=(ulong)s.VirtualAddress&&token+24<=(ulong)s.VirtualAddress+(ulong)s.VirtualSize);uint c=(uint)section.SectionCharacteristics;Need((c&0x40000000)!=0&&(c&0xA2000000)==0,"bounded initialized readonly metadata section");}
            string caller=prefix+route.Item3;
            if(symbols.ContainsKey(caller))
            {
                string body=Body(caller);var operand=Regex.Matches(body,@"\blea\s+rcx, \[rip\][^\r\n]*\r?\n\s*[0-9a-f]+:\s+IMAGE_REL_AMD64_REL32\s+(\S+)");
                Need(operand.Count==1 && operand[0].Groups[1].Value==mtName,"actual compiler token equals Of array token");
                var call=Regex.Matches(body,@"\bcall\s+0x[0-9a-f]+ <"+route.Item4+">");Need(call.Count==1 && call[0].Index>operand[0].Index && Regex.IsMatch(body,@"\bmovsxd\s+rdx, ecx"),"genuine newarr ABI signed length/token/helper order");
            }
            else Need(beforeBind && route.Item1=="Longs","only unreachable beforebind long caller may be removed");
            string exportBody=Body(route.Item4);Need(exportBody.Contains(prefix+"AllocateArray",StringComparison.Ordinal),"real leaf-bound export native route");
            rows.Add(new{Kind=route.Item1,Caller=caller,CallerPresent=symbols.ContainsKey(caller),Export=route.Item4,ExportRva=symbols[route.Item4],MethodTable=mtName,MethodTableRva=mt,Element=elementName,ElementRva=element,Prefix=Convert.ToHexString(raw),ElementPrefix=Convert.ToHexString(ep),Dir64Rva=mt+8});
        }
        File.WriteAllText(Path.Combine(directory,"linked-array-routes.json"),System.Text.Json.JsonSerializer.Serialize(new{PreferredBase=header.ImageBase,Rows=rows,BeforeBind=beforeBind,GuestAccepted=false},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }

    private static string Between(string text,string start,string end){int i=text.IndexOf(start,StringComparison.Ordinal);Need(i>=0,start);int j=text.IndexOf(end,i,StringComparison.Ordinal);Need(j>=i,end);return text[i..j];}
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Save(string directory,Result r){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"process.log"),r.Output);File.WriteAllText(Path.Combine(directory,"exit.txt"),r.Code.ToString());}
    private static Result Process(string root,string executable,string[] arguments)
    {
        ProcessStartInfo start=new(executable){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        if(string.Equals(executable,"powershell.exe",StringComparison.OrdinalIgnoreCase))
        {
            string modules=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","Modules");
            string inherited=Environment.GetEnvironmentVariable("PSModulePath")??string.Empty;
            start.Environment["PSModulePath"]=modules+(inherited.Length==0?string.Empty:Path.PathSeparator+inherited);
        }
        foreach(string a in arguments)start.ArgumentList.Add(a);
        using System.Diagnostics.Process p=System.Diagnostics.Process.Start(start)!;
        Task<string> stdout=p.StandardOutput.ReadToEndAsync(),stderr=p.StandardError.ReadToEndAsync();
        if(!p.WaitForExit(900000)){p.Kill(entireProcessTree:true);throw new TimeoutException(executable);}
        Task.WaitAll(stdout,stderr);return new(p.ExitCode,stdout.Result+stderr.Result);
    }
    private static string Scratch(string root,string name){string p=Path.Combine(root,"artifacts/issue523-tests",name,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
    private static void Need(bool x,string reason){if(!x)throw new InvalidOperationException(reason);}
    private readonly record struct Result(int Code,string Output);
}
#endif
