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
#if STRING_CORE_TEST
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

internal static unsafe class StringCoreTestProgram
{
    private static int Main()
    {
        Run("mixed independent headers UTF16 terminators and padding", Mixed);
        Run("String and Object token prefix rejection", Rejected);
        Run("actual before-bind full-width length and exhaustion statuses", Failures);
        Run("full-capacity storage and zero managed allocation", Geometry);
        Console.WriteLine("TEST_PASS string.core.tests"); return 0;
    }
    private static void Run(string name,Action test)
    { Console.WriteLine("TEST_START string.core "+name);try{test();Console.WriteLine("TEST_PASS string.core "+name);}catch(Exception e){Console.WriteLine("TEST_FAIL string.core "+name+" "+e);throw;} }
    private static void Need(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static void W32(byte* p,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(p,4),value);
    private static void W64(byte* p,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(p,8),value);
    private static int Length(ulong id)=>id is 1 or 3?0:id is 2 or 4?1:id==7?2:3;
    private static ulong Size(ulong id)=>id is 1 or 3 or 4?24UL:32UL;
    private static ulong Scalar(ulong id)=>id==1?0x1111111111111111UL:id==2?0x5AUL:id==4?0x41UL:id==5?0x03A903A903A9UL:id==7?0xD800D800UL:0;
    private static ulong Offset(ulong id)=>id==1?8UL:id==2?16UL:12UL;
    private static ulong Width(ulong id)=>id==1?8UL:id==2?1UL:(ulong)Length(id)*2;
    private static void Sequence(F f,bool oracle)
    {
        ulong used=0;
        for(ulong id=1;id<=7;id++)
        {
            ulong size=Size(id);int length=Length(id);new Span<byte>(f.Page+used,(int)size).Fill(0xBC);
            byte[] before=oracle?f.Snapshot():[];var result=f.Strings(id,length);
            Need(result.Succeeded && result.Leaf.UsedBefore==used && result.Leaf.UsedAfter==used+size &&
                result.Leaf.AllocationCount==id && result.Leaf.ObjectReference==(ulong)f.Page+used+8 &&
                result.Observation.ZeroBytes==size-(id==1?8UL:12UL) && result.Observation.ZeroOr==0,"actual result id="+id);
            if(oracle)
            {
                byte[] expected=new byte[size];BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8),f.StringToken(id));
                if(id!=1)BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(16),length);
                Need(new ReadOnlySpan<byte>(f.Page+used,(int)size).SequenceEqual(expected),"independent all header/payload/terminator/padding bytes");
                f.Only(before,(0x3100,80),(0x3200,96),(0x4000,6672),(0x8000+(int)used,(int)size));
                if(id>=3)Need(result.Observation.RelatedObjectTable==f.Token(4) && result.Observation.RelatedObjectPrefix0==((24UL<<32)|0x50000000UL),"separate full related Object prefix");
            }
            ulong scalar=Scalar(id),offset=Offset(id),width=Width(id),reference=result.Leaf.ObjectReference;
            for(ulong i=0;i<width;i++)((byte*)reference)[offset+i]=(byte)(scalar>>(int)(8*i));
            Need(f.Done(id,reference,length,scalar)==RhpAdmissionStatus.Success,"actual readback id="+id);
            if(id>=3)Need(((byte*)reference)[12+2*length]==0 && ((byte*)reference)[13+2*length]==0,"actual zero terminator");
            used+=size;
        }
        Need(used==200 && f.Bump->UsedBytes==200 && f.Bump->AllocationCount==7 && f.Control->Completed==7 &&
            f.Check()==RhpAdmissionStatus.Success && f.Retained().IsReady && !f.Retained().ContentsAreZeroNow,"one mixed200/7 state");
    }
    private static void Mixed()
    {
        using F f=new();Need(f.Prepare()==RhpAdmissionStatus.Success && f.Bind().Succeeded,"original retained authority and binder");Sequence(f,true);
        // Header, scalar, terminator and final padding corruption must each reject without writes.
        foreach(int offset in new[]{0,8,16,40,48,64,72,76,96,100,120,126,156,166,192,199})
        {
            f.Page[offset]^=1;byte[] before=f.Snapshot();Need(f.Strings(3,0).Status==RhpAdmissionStatus.PriorObjectChanged,"prior byte corruption "+offset);f.Unchanged(before);f.Page[offset]^=1;
        }
        Need(f.Bind().Status==RhpNewFastStatus.AlreadyInitialized,"no second state/binder");
    }
    private static void Rejected()
    {
        for(int variant=0;variant<11;variant++)
        {
            using F f=new();Need(f.Prepare()==RhpAdmissionStatus.Success,"prepare");
            ulong token=f.Token(3),expected=token,related=f.Token(4);
            switch(variant)
            {
                case 0:token=ulong.MaxValue-7;break;
                case 1:related=ulong.MaxValue-7;break;
                case 2:W64((byte*)token+8,f.Token(5));break;
                case 3:W32((byte*)token,0xE0020002);break;
                case 4:W32((byte*)token+4,24);break;
                case 5:W32((byte*)related+4,32);break;
                case 6:W64((byte*)related+8,token);break;
                case 7:related=token;W64((byte*)token+8,token);break;
                case 8:W32(f.Image+0x1AC,0xC0000040);break;
                case 9:f.Control->InProgress=1;break;
                case 10:related=(ulong)f.Records+6664;W64((byte*)token+8,related);break;
            }
            byte[] before=f.Snapshot();var r=RhpNewFastAllocationCore.AllocateStrings(f.Access,f.Bump,f.Control,f.Records,token,expected,3,5,0,related);
            Need(!r.Succeeded && r.Status!=RhpAdmissionStatus.AllocationRejected,"pre-read rejection "+variant);f.Unchanged(before);
        }
    }
    private static void Failures()
    {
        using F unbound=new();Need(unbound.Prepare()==RhpAdmissionStatus.Success,"beforebind prepare");
        byte[] page=unbound.PageSnapshot(),state=unbound.BumpSnapshot();var r=unbound.Strings(3,0);
        Need(r.Status==RhpAdmissionStatus.AllocationRejected && r.Leaf.Status==RhpNewFastStatus.NotInitialized,"actual leaf5");unbound.PageUnchanged(page);unbound.BumpUnchanged(state);
        using F invalid=new();Need(invalid.Prepare()==RhpAdmissionStatus.Success && invalid.Bind().Succeeded,"invalid-length bind");
        page=invalid.PageSnapshot();state=invalid.BumpSnapshot();r=invalid.Strings(3,0x100000001L);
        Need(r.Status==RhpAdmissionStatus.AllocationRejected && r.Leaf.Status==RhpNewFastStatus.InvalidLength,"actual full-width leaf18");invalid.PageUnchanged(page);invalid.BumpUnchanged(state);
        using F f=new();Need(f.Prepare()==RhpAdmissionStatus.Success && f.Bind().Succeeded,"bind");Sequence(f,true);
        for(int i=0;i<161;i++){r=f.Strings(3,0);Need(r.Succeeded && r.Leaf.BytesConsumed==24,"legitimate empty String filler");}
        r=f.Strings(3,3);Need(r.Succeeded && r.Leaf.BytesConsumed==32,"last length3 filler");
        for(int i=0;i<6;i++)((byte*)r.Leaf.ObjectReference)[12+i]=(byte)(0x005A005A005AUL>>(8*i));
        Need(f.Bump->UsedBytes==4096 && f.Bump->AllocationCount==169 && f.Control->Completed==7 && f.Check()==RhpAdmissionStatus.Success,"same cursor full page, no observation overflow");
        for(int i=0;i<161;i++)
        {
            byte[] expected=new byte[24];BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8),f.Token(3));
            Need(new ReadOnlySpan<byte>(f.Page+200+i*24,24).SequenceEqual(expected),"every filler byte");
        }
        byte[] last=new byte[32];BinaryPrimitives.WriteUInt64LittleEndian(last.AsSpan(8),f.Token(3));BinaryPrimitives.WriteInt32LittleEndian(last.AsSpan(16),3);
        last[20]=last[22]=last[24]=0x5A;Need(new ReadOnlySpan<byte>(f.Page+4064,32).SequenceEqual(last),"full final filler bytes");
        page=f.PageSnapshot();state=f.BumpSnapshot();r=f.Strings(3,1);
        Need(r.Status==RhpAdmissionStatus.AllocationRejected && r.Leaf.Status==RhpNewFastStatus.Exhausted,"actual leaf15");f.PageUnchanged(page);f.BumpUnchanged(state);
    }
    private static void Geometry()
    {
        Need(sizeof(RhpNewFastHeapAccess)==104 && sizeof(RhpNewFastBumpState)==80 && sizeof(RhpNewFastResult)==88 &&
            sizeof(RhpAllocationControl)==96 && sizeof(RhpObjectObservation)==256 && sizeof(RhpObservationStorage)==6672,"actual complete POD geometry");
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<RhpObservationStorage>(),"POD observation storage");
        using F f=new();byte[] before=f.Snapshot();
        var alias=(RhpObservationStorage*)(f.Page-6672+8);
        Need(RhpNewFastAllocationCore.Prepare(f.Original,f.Access,f.Bump,f.Control,alias,f.Image,16384)==RhpAdmissionStatus.StorageOverlap,"last8 alias admission");f.Unchanged(before);
        for(int pass=0;pass<2;pass++)
        {
            f.Reset();f.Prefixes();Need(f.Prepare()==RhpAdmissionStatus.Success && f.Bind().Succeeded,"reset fixture");
            long start=GC.GetAllocatedBytesForCurrentThread();Sequence(f,false);long end=GC.GetAllocatedBytesForCurrentThread();
            // The independent sequence labels allocate strings; measure the actual filler core separately.
            start=GC.GetAllocatedBytesForCurrentThread();var r=f.Strings(3,0);end=GC.GetAllocatedBytesForCurrentThread();
            Need(r.Succeeded && end==start,"zero CLR allocation by actual String POD core");
        }
        Console.WriteLine("STRING_CORE_POD access=104 bump=80 result=88 control=96 observation=256 records=6672 used=200 count=7");
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
        internal F(){Need(Storage!=null,"native storage");Reset();Prefixes();}
        internal void Prefixes()
        {
            PrefixBytes(1,0x50000000,24,0);PrefixBytes(2,0xE0020001,24,Token(5));
            PrefixBytes(3,0xD0000002,22,Token(4));PrefixBytes(4,0x50000000,24,0);PrefixBytes(5,0x14000000,24,0);
        }
        internal void PrefixBytes(ulong id,uint flags,uint size,ulong related)
        { W32((byte*)Token(id),flags);W32((byte*)Token(id)+4,size);W64((byte*)Token(id)+8,related); }
        internal ulong StringToken(ulong id)=>Token(id>=3?3UL:id);
        internal RhpAllocationAttempt Strings(ulong id,long length)=>RhpNewFastAllocationCore.AllocateStrings(Access,Bump,Control,Records,
            StringToken(id),StringToken(id),id,id==1?1UL:id==2?6UL:5UL,length,id==1?0:id==2?Token(5):Token(4));
        internal RhpAdmissionStatus Done(ulong id,ulong reference,int length,ulong scalar)=>
            RhpNewFastAllocationCore.CompleteStrings(Access,Bump,Control,Records,id,reference,length,scalar,scalar);

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
internal static class M04StringAllocationEfiIntegrationTests
{
    private const string Flag="EnableM04StringAllocationProbe";
    private static readonly string[] Chain=["EnableM01LoadedImageStackProbe","EnableM01LiveAllocatorProbe","EnableM04IndependentEndianConformanceProbe",
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","EnableM04ArithmeticConformanceProbe","EnableM04LayoutContractProbe","EnableM04ManagedHeapPageRangeProbe","EnableM04RhpNewFastProbe","EnableM04SzArrayAllocationProbe"];

    [HostTest("m04.009.efi actual POD core and bounded String metadata", HostLane.Native, 1)]
    private static void TestCore()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Scratch(root,"core");
        string includes=string.Join('\n',new[]{"src/OS/Runtime/RhpNewFastAllocation.cs","tests/OS.HostTests/M04StringAllocationEfiIntegrationTests.cs"}
            .Select(x=>"<Compile Include=\""+SecurityElement.Escape(Path.Combine(root,x))+"\"/>"));
        File.WriteAllText(Path.Combine(directory,"Core.csproj"),"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
            <OutputType>Exe</OutputType><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>STRING_CORE_TEST</DefineConstants><NoWarn>0649</NoWarn></PropertyGroup><ItemGroup>
            """+includes+"<ProjectReference Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj"))+"\"/></ItemGroup></Project>");
        Result result=Process(root,"dotnet",["run","--project",Path.Combine(directory,"Core.csproj"),"-c","Release","--nologo"]);
        File.WriteAllText(Path.Combine(directory,"process.log"),result.Output);File.WriteAllText(Path.Combine(directory,"exit.txt"),result.Code.ToString());
        Need(result.Code==0 && result.Output.Contains("TEST_PASS string.core.tests",StringComparison.Ordinal),result.Output);
    }

    [HostTest("m04.009.efi source admission and strict firewall", HostLane.Fast, 2)]
private static void TestFirewall()
{
    const string name = @"..\OS.Kernel\Runtime\GuestTestStringAllocationCharacterStorage.cs";
    string text = File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS.Kernel/Runtime/GuestTestStringAllocationCharacterStorage.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
    foreach (string good in new[] { text, text.Replace("\n", "\r\n", StringComparison.Ordinal) })
        Need(OS.Tooling.EfiSourceAdmission.Inspect(good, name).Succeeded, "reviewed source admission");
    foreach (string bad in new[] { "using System; public class X {}", "public class X { object value = new object(); }" })
        Need(!OS.Tooling.EfiSourceAdmission.Inspect(bad, name).Succeeded, "generic surface remains closed");

}

    [HostTest("m04.009.efi source profile and original handoff", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root=HostToolProcess.FindRepositoryRoot();XDocument project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        string[] items=project.Descendants("Compile").Select(x=>x.Attribute("Include")!.Value+"|"+(x.Attribute("Link")?.Value??"")).ToArray();
        Need(items.Length==OS.Tooling.EfiContracts.CompileItems.Count && items[82]=="..\\OS.Kernel\\Runtime\\GuestTestStringAllocationCharacterStorage.cs|Runtime\\GuestTestStringAllocationCharacterStorage.cs" &&
            items[83]=="..\\OS.Kernel\\Diagnostics\\StringAllocationProbeAuthority.cs|Diagnostics\\StringAllocationProbeAuthority.cs","two exact String includes");
        foreach(var pair in new[]{(Flag,"M04_STRING_ALLOCATION_PROBE"),("EnableM04StringAllocationBeforeBindProbe","M04_STRING_ALLOCATION_BEFORE_BIND_PROBE"),("EnableM04StringAllocationExhaustionProbe","M04_STRING_ALLOCATION_EXHAUSTION_PROBE"),("EnableM04StringAllocationInvalidLengthProbe","M04_STRING_ALLOCATION_INVALID_LENGTH_PROBE")})
        {
            var value=project.Descendants(pair.Item1).Single();Need(value.Value=="false" && value.Attribute("Condition")?.Value=="'$("+pair.Item1+")' == ''","default false");
            Need(project.Descendants("PropertyGroup").Single(x=>x.Attribute("Condition")?.Value=="'$("+pair.Item1+")' == 'true'").Element("DefineConstants")?.Value=="$(DefineConstants);"+pair.Item2,"exact selected compiler symbol");
        }
        foreach(string required in Chain)Need(project.Descendants("Error").Any(x=>x.Attribute("Condition")?.Value=="'$("+Flag+")' == 'true' and '$("+required+")' != 'true'"),"explicit prerequisite "+required);
        HostCheck.Registered(typeof(GuestTestStringAllocationCharacterStorageTests));
        HostCheck.Registered(typeof(GuestTestRhpNewFastTests));
        foreach(var file in new[]{("src/OS.Kernel/Runtime/GuestTestStringAllocationCharacterStorage.cs","0676ab5b69838bd12575a9d3c2059f8c39872cfa67c5b9fd0c37227568822aba"),
            ("src/OS.Kernel/Runtime/GuestTestRhpNewFast.cs",OS.Tooling.EfiContracts.AllocationSourceSha256),
            ("src/OS/Boot/EfiEntry.cs",OS.Tooling.EfiContracts.EfiEntrySourceSha256)})
            Need(Hash(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(root,file.Item1)).Replace("\r\n","\n",StringComparison.Ordinal)))==file.Item2,"reviewed host/entry source "+file.Item1);
        string adapter=File.ReadAllText(Path.Combine(root,"src/OS/Runtime/RhpNewFastAllocation.cs"));
        Need(adapter.Contains("GuestTestStringAllocationCharacterStorage.TryAllocate(*access, bump, 80,",StringComparison.Ordinal) &&
            adapter.Contains("NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian, prefix, token, length)",StringComparison.Ordinal) &&
            adapter.Contains("expectedElement == token",StringComparison.Ordinal) && adapter.Contains("token != expectedToken",StringComparison.Ordinal) &&
            adapter.Contains("RejectedAllocationUnchanged(bump, bumpBefore, access->Backing, records->BeforeAttempt)",StringComparison.Ordinal) &&
            !adapter.Contains("Serialize(",StringComparison.Ordinal),"actual shared allocator and metadata/rejection/data publication boundary");
        Need(Regex.Matches(adapter,@"private static RhpNewFastBumpState s_bump;").Count==1 && Regex.Matches(adapter,@"GuestTestRhpNewFast\.InitializeOnce\(").Count==1,"sole original state/binder");
        foreach(string forbidden in new[]{"FreeOnePage(","FreePages(","RhBox(","new object[","throw ","System.Reflection","System.Console"})Need(!adapter.Contains(forbidden,StringComparison.Ordinal),"no scope expansion");
    }
    [HostTest("m04.009.efi explicit profile prerequisites and conflicts", HostLane.Native, 4)]
    private static void TestProfiles()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Scratch(root,"profiles");
        string[] negatives=["EnableM04StringAllocationBeforeBindProbe","EnableM04StringAllocationExhaustionProbe","EnableM04StringAllocationInvalidLengthProbe"];
        string[] all=Chain.Append(Flag).ToArray();
        string[] conflicts=["EnableM01OwnedPagingProbe",..XDocument.Load(Path.Combine(root,"src/OS/OS.csproj")).Descendants()
            .Where(x=>x.Name.LocalName.StartsWith("EnableM02",StringComparison.Ordinal)).Select(x=>x.Name.LocalName).Distinct(StringComparer.Ordinal),
            "EnableUefiGoneSimulationProbe","EnableExitBootServicesFreshMapProbe","EnableAcpiPhysicalDiscoveryProbe","EnableM04RhpNewFastBeforeBindFailureProbe","EnableM04RhpNewFastExhaustionFailureProbe","EnableM04SzArrayAllocationBeforeBindProbe","EnableM04SzArrayAllocationExhaustionProbe","EnableM03MemoryMapCaptureProbe","EnableM04CheckedReferenceBarrierProbe","EnableM03FirmwareEntropySeedProbe","EnableM04TypeManagerModuleRegistrationProbe"];
        Need(Chain.Length==9 && conflicts.Length==27,"all exact prerequisites/conflicts");
        var conditions=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj")).Descendants("Error").Select(x=>x.Attribute("Condition")!.Value).ToHashSet(StringComparer.Ordinal);
        foreach(string flag in negatives.Prepend(Flag))foreach(string conflict in conflicts)
        {
            Need(conditions.Contains("'$("+flag+")' == 'true' and '$("+conflict+")' == 'true'") &&
                conditions.Contains("'$("+conflict+")' == 'true' and '$("+flag+")' == 'true'"),"both reciprocal conflicts "+flag+"/"+conflict);
        }
        List<string[]> invalid=[..Chain.Select(missing=>all.Where(x=>x!=missing).ToArray()),..conflicts.Select(x=>all.Append(x).ToArray()),..negatives.Select(x=>new[]{x})];
        for(int i=0;i<negatives.Length;i++)for(int j=i+1;j<negatives.Length;j++)invalid.Add([..all,negatives[i],negatives[j]]);
        int id=0;
        foreach(string[] flags in invalid)foreach(bool runner in new[]{false,true})
        {
            Result r=Command([..(runner ? new[] { "qemu", "smoke" } : new[] { "efi", "build" }), ..(runner?new[]{"-BootPolicy", "Diagnostic", "-DryRun","-BuildEfi"}:new[]{"-PreflightOnly","-LogPath",Path.Combine(directory,"preflight-"+id+".log")}), ..flags.Select(x=>"-"+x)]);
            Save(Path.Combine(directory,(id++).ToString()),r);Need(r.Code!=0 && (r.Output.Contains("m04_",StringComparison.Ordinal) || (flags.Contains("EnableM03FirmwareEntropySeedProbe",StringComparer.Ordinal) && r.Output.Contains("m03_firmware_entropy_profile_conflict property=EnableM01LoadedImageStackProbe",StringComparison.Ordinal)) || (flags.Contains("EnableM03MemoryMapCaptureProbe",StringComparer.Ordinal) && r.Output.Contains("m03_memory_map_capture_profile_conflict property=EnableM01LoadedImageStackProbe",StringComparison.Ordinal))),"explicit profile rejection "+string.Join(',',flags)+" "+r.Output);
        }
        foreach(string[] flags in negatives.Select(x=>all.Append(x).ToArray()).Prepend(all))
        {
            Result r=Command(["qemu", "smoke", "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", ..flags.Select(x=>"-"+x)]);
            Save(Path.Combine(directory,(id++).ToString()),r);Need(r.Code==0,r.Output);
            Need(r.Output.Contains("TEST_FAIL m04.009",StringComparison.Ordinal),"negative profile keeps generic failure classification");
        }
    }
    [HostTest("m04.009.efi actual default and four String artifacts", HostLane.Native, 5)]
    private static void TestArtifacts()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        string[] all=Chain.Append(Flag).ToArray();
        foreach(var lane in new[]{("default",Array.Empty<string>()),("positive",all),("before-bind",all.Append("EnableM04StringAllocationBeforeBindProbe").ToArray()),("exhaustion",all.Append("EnableM04StringAllocationExhaustionProbe").ToArray()),("invalid-length",all.Append("EnableM04StringAllocationInvalidLengthProbe").ToArray())})
        {
            string directory=Scratch(root,"artifact-"+lane.Item1),artifact=Path.Combine(root,"artifacts/efi/issue528-tests-"+Guid.NewGuid().ToString("N")+".EFI");
            Result r=Command(["efi", "build", "-EfiOutputPath", artifact, "-LogPath", Path.Combine(directory,"publish.log"), ..lane.Item2.Select(x=>"-"+x)]);
            Save(directory,r);
            Need(r.Code==0 && r.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal) && r.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),r.Output);
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(artifact+suffix,Path.Combine(directory,"image.EFI"+suffix));
            foreach(var file in new[]{(OS.Tooling.EfiContracts.ManagedAssemblyRelativePath("Release"),"OS.dll"),("src/OS/obj/Release/net11.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net11.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(root,file.Item1),Path.Combine(directory,file.Item2));
            using var document=System.Text.Json.JsonDocument.Parse(File.ReadAllText(artifact+".link.json"));var e=document.RootElement;
            var inputs=e.GetProperty("sourceClosureInputs").EnumerateArray().ToArray();Need(inputs.Length==OS.Tooling.EfiEvidence.SourceClosurePaths(root).Count,"complete raw source inventory");
            foreach(var input in inputs)Need(Hash(File.ReadAllBytes(Path.Combine(root,input.GetProperty("path").GetString()!)))==input.GetProperty("sha256").GetString(),"actual raw source unchanged");
            string[] expectedCompileItems = OS.Tooling.EfiProjectAdmission.ExpectedCompileItems(
                OS.Tooling.EfiProfile.Create(null, lane.Item2.ToDictionary(flag => flag, _ => true, StringComparer.Ordinal)));
            var evaluation=e.GetProperty("postRestoreEvaluation");
            Need(evaluation.GetProperty("compileCount").GetInt32()==expectedCompileItems.Length,"complete evaluated compile inventory");
            Need(evaluation.GetProperty("compileSha256").GetString()==OS.Tooling.EfiEvidence.HashLines(expectedCompileItems),"ordered evaluated compile inventory");
            var flags=evaluation.GetProperty("properties").EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)).ToArray();
            Need(flags.Length==OS.Tooling.EfiContracts.DiagnosticProbes.Count,"complete52 evaluated flags");
            foreach(var f in flags)Need(f.Value.GetString()==(lane.Item2.Contains(f.Name,StringComparer.Ordinal)?"true":"false"),"actual requested/evaluated profile "+f.Name);
            TestMetadata(directory,lane.Item1!="default");
            if(lane.Item1!="default")TestLinkedRoutes(root,directory,lane.Item1);
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
        foreach(string owner in enabled?new[]{"System.Runtime.RhpNewFastRuntime","System.Runtime.RuntimeImports"}:new[]{"System.Runtime.RhpNewFastRuntime"})
        {
            var named=methods.Where(h=>md.GetString(md.GetMethodDefinition(h).Name)=="RhNewString" && FullType(md.GetMethodDefinition(h).GetDeclaringType())==owner).ToArray();
            Need(named.Length==1,"sole named helper for exact owner "+owner);var method=md.GetMethodDefinition(named[0]);
            Need((method.Attributes&System.Reflection.MethodAttributes.Static)!=0 && (method.Attributes&System.Reflection.MethodAttributes.MemberAccessMask)==System.Reflection.MethodAttributes.Assembly && method.GetGenericParameters().Count==0,"internal static nongeneric helper");
            var sig=md.GetBlobReader(method.Signature);Need(sig.ReadByte()==0 && sig.ReadCompressedInteger()==2 && sig.ReadByte()==0x0E && sig.ReadByte()==0x0F && sig.ReadByte()==0x11,"String(pointer-to-valuetype,nint) signature");
            int coded=sig.ReadCompressedInteger();Need((coded&3)==0 && FullType(System.Reflection.Metadata.Ecma335.MetadataTokens.TypeDefinitionHandle(coded>>2))=="Internal.Runtime.MethodTable" && sig.ReadByte()==0x18 && sig.RemainingBytes==0,"actual MethodTable* and full native signed length");
            bool import=owner.EndsWith("RuntimeImports",StringComparison.Ordinal);
            if(import)Need((method.ImplAttributes&System.Reflection.MethodImplAttributes.InternalCall)!=0 && method.RelativeVirtualAddress==0,"actual intrinsic runtime import");
            else Need((method.ImplAttributes&System.Reflection.MethodImplAttributes.NoInlining)!=0 && method.RelativeVirtualAddress!=0,"real NoInlining export body");
            int payloads=0;
            foreach(var h in methods)foreach(var ah in md.GetMethodDefinition(h).GetCustomAttributes())
            {
                var attr=md.GetCustomAttribute(ah);if(AttributeType(attr)!=(import?"System.Runtime.RuntimeImportAttribute":"System.Runtime.RuntimeExportAttribute"))continue;
                var blob=md.GetBlobReader(attr.Value);Need(blob.ReadUInt16()==1,"attribute prolog");string? value=blob.ReadSerializedString();
                if(import){Need(value=="*","actual import module");value=blob.ReadSerializedString();}
                Need(blob.ReadUInt16()==0 && blob.RemainingBytes==0,"finite exact attribute payload");
                if(value=="RhNewString"){payloads++;Need(h==named[0],"sole payload belongs to exact helper");}
            }
            Need(payloads==1,"sole RhNewString attribute payload per import/export");
            byte[] il=method.RelativeVirtualAddress==0?[]:pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
            Need(import || il.Length>0,"initialized export IL");
            evidence.Add(new{Name="RhNewString",Owner=owner,Rva=method.RelativeVirtualAddress,Signature=Convert.ToHexString(md.GetBlobBytes(method.Signature)),IL=Convert.ToHexString(il),Payloads=payloads});
        }
        Need(md.TypeDefinitions.Any(h=>FullType(h)=="System.Runtime.RuntimeImports")==enabled,"no unsupported import dependency in disabled closures");
        var text=md.GetTypeDefinition(md.TypeDefinitions.Single(h=>FullType(h)=="System.String"));var fields=text.GetFields().ToArray();
        Need(fields.Length==2 && md.GetString(md.GetFieldDefinition(fields[0]).Name)=="_stringLength" && md.GetBlobBytes(md.GetFieldDefinition(fields[0]).Signature).AsSpan().SequenceEqual(new byte[]{6,8}) &&
            md.GetString(md.GetFieldDefinition(fields[1]).Name)=="_firstChar" && md.GetBlobBytes(md.GetFieldDefinition(fields[1]).Signature).AsSpan().SequenceEqual(new byte[]{6,3}),"exact String int/Char fields in source order");
        foreach(string name in new[]{".ctor","Ctor","get_Length","get_Chars","FastAllocateString"})
        {
            var h=text.GetMethods().Single(h=>md.GetString(md.GetMethodDefinition(h).Name)==name);var m=md.GetMethodDefinition(h);
            byte[] il=m.RelativeVirtualAddress==0?[]:pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()!;
            if(name==".ctor")Need((m.ImplAttributes&System.Reflection.MethodImplAttributes.InternalCall)!=0 && il.Length==0 && md.GetBlobBytes(m.Signature).AsSpan().SequenceEqual(new byte[]{0x20,2,1,3,8}),"genuine internal-call String(char,int) constructor");
            else Need(il.Length>0,"actual String method body "+name);
            if(name=="Ctor")Need(md.GetBlobBytes(m.Signature).AsSpan().SequenceEqual(new byte[]{0,2,0x0E,3,8}) && (m.ImplAttributes&System.Reflection.MethodImplAttributes.NoInlining)!=0,"matching static String Ctor remapping contract");
            evidence.Add(new{Name=name,Owner="System.String",Rva=m.RelativeVirtualAddress,Signature=Convert.ToHexString(md.GetBlobBytes(m.Signature)),IL=Convert.ToHexString(il),Instructions=DecodeIl(il)});
        }
        Need(md.TypeDefinitions.Any(h=>FullType(h)=="OS.Runtime.RhpNewFastAllocation")==enabled,"actual enabled adapter presence/default absence");
        if(enabled)
        {
            var adapter=md.TypeDefinitions.Single(h=>FullType(h)=="OS.Runtime.RhpNewFastAllocation");
            foreach(string name in new[]{"NewString","RawString","StringLength","StringCharacter","StringUnits","AllocateString","RunStrings"})
            {
                var h=md.GetTypeDefinition(adapter).GetMethods().Single(h=>md.GetString(md.GetMethodDefinition(h).Name)==name);var m=md.GetMethodDefinition(h);
                byte[] il=pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()!;var instructions=DecodeIl(il);
                if(name=="NewString")
                {
                    var call=instructions.Single(x=>x.Name=="newobj");var target=System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(call.Operand);
                    Need(target.Kind==HandleKind.MethodDefinition && FullType(md.GetMethodDefinition((MethodDefinitionHandle)target).GetDeclaringType())=="System.String" && md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)target).Name)==".ctor","genuine compiler-created String constructor");
                }
                evidence.Add(new{Name=name,Owner="OS.Runtime.RhpNewFastAllocation",Rva=m.RelativeVirtualAddress,IL=Convert.ToHexString(il),Instructions=instructions});
            }
        }
        File.WriteAllText(Path.Combine(directory,"managed-string-metadata.json"),System.Text.Json.JsonSerializer.Serialize(new{Assembly="OS",AssemblyReferences=0,Enabled=enabled,Mvid=md.GetGuid(md.GetModuleDefinition().Mvid),Methods=evidence,GuestAccepted=false},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
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

    private readonly record struct IlInstruction(int Offset,string Name,int Operand);
    private static void TestLinkedRoutes(string root,string directory,string lane)
    {
        byte[] image=File.ReadAllBytes(Path.Combine(directory,"image.EFI"));using var reader=new PEReader(new MemoryStream(image));var header=reader.PEHeaders.PEHeader!;
        Need(header.Magic==PEMagic.PE32Plus && reader.PEHeaders.SectionHeaders.Length is >0 and <=8,"bounded x64 image sections");
        byte[] At(ulong rva,int bytes)
        {
            var sections=reader.PEHeaders.SectionHeaders.Where(s=>s.VirtualAddress>=0 && s.VirtualSize>=0 && s.SizeOfRawData>=0 && rva>=(ulong)s.VirtualAddress && rva+(ulong)bytes<=(ulong)s.VirtualAddress+(ulong)s.VirtualSize && rva+(ulong)bytes<=(ulong)s.VirtualAddress+(ulong)s.SizeOfRawData).ToArray();
            Need(sections.Length==1,"one initialized native extent");var s=sections[0];return image.AsSpan(checked(s.PointerToRawData+(int)(rva-(ulong)s.VirtualAddress)),bytes).ToArray();
        }
        var symbols=Regex.Matches(File.ReadAllText(Path.Combine(directory,"image.EFI.link.map")),@"^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(\S+)\s+([0-9A-Fa-f]{16})\s+",RegexOptions.Multiline).ToDictionary(m=>m.Groups[1].Value,m=>Convert.ToUInt64(m.Groups[2].Value,16)-header.ImageBase,StringComparer.Ordinal);
        string llvm=HostToolProcess.FindLlvmObjdump();
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
        ulong mt=Token("String"),related=Token("Object");
        string mtName=symbols.Single(s=>s.Value==mt&&s.Key.StartsWith("??_7",StringComparison.Ordinal)).Key;
        string objectName=symbols.Single(s=>s.Value==related&&s.Key.StartsWith("??_7",StringComparison.Ordinal)).Key;
        byte[] raw=At(mt,24),objectPrefix=At(related,24);
        Need(BinaryPrimitives.ReadUInt32LittleEndian(raw)==0xD0000002 && BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4))==22 &&
            BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(8))==header.ImageBase+related,"actual complete String shape/related Object");
        Need(RelocatedElement(mtName,out byte[] coffPrefix)==objectName && dir64.Count(x=>x==mt+8)==1,"String prefix exact COFF ADDR64/PE DIR64/related Object");
        BinaryPrimitives.WriteUInt64LittleEndian(coffPrefix.AsSpan(8),checked(BinaryPrimitives.ReadUInt64LittleEndian(coffPrefix.AsSpan(8))+header.ImageBase+related));
        Need(raw.AsSpan().SequenceEqual(coffPrefix),"all24 relocated String prefix bytes");
        var os=Symbol(objectName);Need(os.Section>0,"defined related Object");
        int osh=20+BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(16))+(os.Section-1)*40;
        int objectFile=checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(osh+20))+os.Value));
        Need(BinaryPrimitives.ReadUInt32LittleEndian(objectPrefix.AsSpan(4))==24 && BinaryPrimitives.ReadUInt64LittleEndian(objectPrefix.AsSpan(8))==0 &&
            objectPrefix.AsSpan().SequenceEqual(coff.AsSpan(objectFile,24)) && !dir64.Any(x=>x>=related&&x<related+24),"all24 distinct Object prefix bytes/zero related pointer");
        uint orp=BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(osh+24));int orn=BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(osh+32));
        for(int i=0;i<orn;i++){uint off=BinaryPrimitives.ReadUInt32LittleEndian(coff.AsSpan(checked((int)orp+i*10)));Need(off<os.Value||off>=os.Value+24,"no unaccounted Object prefix relocation");}
        foreach(ulong token in new[]{mt,related})
        {
            var section=reader.PEHeaders.SectionHeaders.Single(s=>token>=(ulong)s.VirtualAddress&&token+24<=(ulong)s.VirtualAddress+(ulong)s.VirtualSize);
            uint c=(uint)section.SectionCharacteristics;Need((c&0x40000000)!=0&&(c&0xA2000000)==0,"actual initialized readonly nondiscardable prefix section");
        }
        string exportBody=Body("RhNewString");Need(exportBody.Contains(prefix+"AllocateString",StringComparison.Ordinal),"real String leaf-bound export");
        List<object> code=[];
        string runName=symbols.ContainsKey(prefix+"RunStrings")?"RunStrings":"Run";
        foreach(string name in new[]{"AllocateString","AllocateStringRoute",runName})
        {
            string symbol=prefix+name;Need(symbols.ContainsKey(symbol),"actual native String method "+name);
            code.Add(new{Name=name,Rva=symbols[symbol],Body=Body(symbol)});
        }
        if(lane=="invalid-length")
        {
            string body=Body(prefix+"RawString");Need(body.Contains("RhNewString",StringComparison.Ordinal) && Regex.IsMatch(body,@"\bmov\s+rdx, rcx"),"actual full-width nint forwarded to RDX");
            var operand=Regex.Matches(body,@"\blea\s+rcx, \[rip\][^\r\n]*\r?\n\s*[0-9a-f]+:\s+IMAGE_REL_AMD64_REL32\s+(\S+)");
            Need(operand.Count==1 && operand[0].Groups[1].Value==mtName,"direct helper genuine String token");
            string run=Body(prefix+runName);Need(Regex.IsMatch(run,@"\bmovabs\s+rcx, (?:0x100000001|4294967297)\b") && run.Contains(prefix+"RawString",StringComparison.Ordinal),"actual 64-bit invalid-length constant at call site");
            code.Add(new{Name="RawString",Rva=symbols[prefix+"RawString"],Body=body});
        }
        else
        {
            string ctor=symbols.Keys.Single(n=>string.Equals(n,"String__Ctor",StringComparison.Ordinal));string cb=Body(ctor),newBody=Body(prefix+"NewString");
            Need(newBody.Contains(ctor,StringComparison.Ordinal),"genuine new String maps to static Ctor");
            int call=cb.IndexOf("<RhNewString>",StringComparison.Ordinal);
            Need(call>=0 && Regex.IsMatch(cb[..call],@"\blea\s+rcx, \[rip\][^\r\n]*\r?\n\s*[0-9a-f]+:\s+IMAGE_REL_AMD64_REL32\s+"+Regex.Escape(mtName)),"Ctor authentic MT before helper");
            Need(Regex.IsMatch(cb[..call],@"\b(?:mov|movsxd)\s+(?:edx|rdx),") && Regex.IsMatch(cb[(call+1)..],@"\bmov\s+word ptr \["),"actual count route and post-helper UTF16 stores");
            Need(Regex.IsMatch(cb,@"\b(?:add|lea)\b[^\r\n]*(?:0xc|0xC|12)\b") && cb.Contains("ret",StringComparison.Ordinal),"actual reference+12 character base and return continuation");
            foreach(string name in lane=="before-bind"?Array.Empty<string>():new[]{"StringLength","StringCharacter"})
            {
                string b=Body(prefix+name);Need(Regex.IsMatch(b,name=="StringLength"?@"\bmov\s+eax, dword ptr \[rcx \+ 0x8\]":@"\bmovzx\s+eax, word ptr \[.*0xc\]"),"actual compiled String field offset "+name);
                code.Add(new{Name=name,Rva=symbols[prefix+name],Body=b});
            }
            code.Add(new{Name="Ctor",Rva=symbols[ctor],Body=cb});
        }
        File.WriteAllText(Path.Combine(directory,"linked-string-routes.json"),System.Text.Json.JsonSerializer.Serialize(new{PreferredBase=header.ImageBase,Lane=lane,
            StringSymbol=mtName,StringRva=mt,RelatedObjectSymbol=objectName,RelatedObjectRva=related,StringPrefix=Convert.ToHexString(raw),ObjectPrefix=Convert.ToHexString(objectPrefix),
            Dir64Rva=mt+8,ExportRva=symbols["RhNewString"],Code=code,GuestAccepted=false},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
    [HostTest("m04.009.efi strongest full transcript and negatives", HostLane.Fast, 3)]
    private static void TestTranscript()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Scratch(root,"transcript");
        Result dry=Command(["qemu", "smoke", "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", ..Chain.Append(Flag).Select(x=>"-"+x)]);
        Save(Path.Combine(directory,"dryrun"),dry);Need(dry.Code==0,dry.Output);
        string[] chain=dry.Output.Replace("\r","").Split('\n').Single(x=>x.StartsWith("ORDERED_MARKERS ",StringComparison.Ordinal))[16..].Split(';');
        List<string> lines=["TEST_START efi.main",..chain.Take(6),"UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 3","UEFI_MEMORY_MAP_OWNED","BOOT_HANDOFF_READY","UEFI_MEMORY_MAP_NORMALIZED","BOOT_MEMORY_MAP_ACCEPTED","BOOT_MEMORY_REGIONS_READY","BOOT_MEMORY_REGION_COUNT 3","BOOT_PAGE_ALLOCATOR_READY status=Ready regions=3 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0","BOOT_ACPI_RSDP_HANDOFF_READY","UEFI_ACPI_RSDP_READY",..chain.Skip(6)];
        const string identity="allocatorState=0x0000000000100000 nodes=0x0000000000100040 heapState=0x0000000000101000 ledger=0x0000000000102000 parentIndex=4 parentStart=0x0000000000900000 parentEnd=0x0000000000902000 backing=0x0000000000900000 retainedStart=0x0000000000900000 retainedEnd=0x0000000000901000";
        lines[lines.IndexOf("M04_HEAP_PAGE_BOUND")]+=" "+identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_RETAINED")]+=" checkpoint=beforeBootPlan "+identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_INITIALIZED")]+=" pages=1 bytes=4096 freePagesBefore=2 freeBytesBefore=8192 allocatedPagesBefore=0 allocatedBytesBefore=0 freePagesAfter=1 freeBytesAfter=4096 allocatedPagesAfter=1 allocatedBytesAfter=4096 readBytes=4096 contentsZero=true";
        lines[lines.IndexOf("M04_HEAP_PAGE_CURRENT")]+=" freePages=1 freeBytes=4096 allocatedPages=1 allocatedBytes=4096 readBytes=4096 contentsZero=true";
        lines[45]="M04_STRING_PREP image=0x0000000001000000 imageBytes=65536 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 control=0x0000000000103200 controlBytes=96 records=0x0000000000104000 recordsBytes=6672";
        lines[46]="M04_STRING_BIND image=0x0000000001000000 imageBytes=65536 allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 ledgerBytes=200 parentIndex=4 backing=0x0000000000900000 backingBytes=8192 heap=0x0000000000101000 heapBytes=216 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 status=1 used=0 count=0";
        uint[] sizes=[24,32,24,24,32,32,32],lengths=[0,1,0,1,3,3,2],offsets=[8,16,12,12,12,12,12],widths=[8,1,0,2,6,6,4];
        ulong[] scalars=[0x1111111111111111,0x5A,0,0x41,0x03A903A903A9,0,0xD800D800];ulong used=0;
        static string H(ulong x)=>"0x"+x.ToString("X16");
        for(int id=1;id<=7;id++) {
            bool str=id>=3,array=id==2;uint size=sizes[id-1];ulong mt=0x1001100UL+(ulong)Math.Min(id-1,2)*32,allocation=0x900000+used;
            ulong element=array?0x1002000UL:0,related=str?0x1002100UL:0;
            ulong element0=array?(24UL<<32)|0x14000000UL:0,related0=str?(24UL<<32)|0x50000000UL:0;
            uint flags=str?0xD0000002U:array?0xE0020001U:0x50000000U;
            lines[45+id*2]=$"M04_STRING_OBJECT id={id} route={(str?"compiled_string":array?"compiled_byte_array":"compiled_fixed")} mt={H(mt)} prefix0={H(((ulong)(str?22:24)<<32)|flags)} prefix1={H(str?related:element)} prefix2={H(0x1234567800020001)} flags={H(flags)} base={size} allocation={H(allocation)} reference={H(allocation+8)} end={H(allocation+size)} usedBefore={used} usedAfter={used+size} count={id} sync={H(0)} observedMt={H(mt)} zeroBytes={size-(id==1?8U:12U)} zeroOr={H(0)} length={lengths[id-1]} element={H(element)} element0={H(element0)} element1={H(0)} element2={H(0)} relatedObject={H(related)} relatedObject0={H(related0)} relatedObject1={H(0)} relatedObject2={H(0)}";
            lines[46+id*2]=$"M04_STRING_READBACK id={id} length={lengths[id-1]} scalarOffset={offsets[id-1]} scalarWidth={widths[id-1]} scalar={H(scalars[id-1])} terminator={H(str?allocation+8+12+2*lengths[id-1]:0)} terminatorValue={H(0)} referenceSlotBytes={size-16-(id==1?0U:4U)-widths[id-1]} referenceSlotOr={H(0)} constructorCount={(id<=2?1:id-1)} priorCount={id} priorMismatchBytes=0 canaryMismatchBytes=0";used+=size;
        }
        lines[61]="M04_STRING_RETAIN allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 parentIndex=4 backing=0x0000000000900000 heap=0x0000000000101000 bump=0x0000000000103100 heapStart=0x0000000000900000 heapEnd=0x0000000000901000 initialPages=2 freePages=1 allocatedPages=1 readBytes=4096 contentsZero=false used=200 count=7 status=1";
        Need(lines.Count==72 && lines[44]=="TEST_START m04.009" && lines[64]=="TEST_START boot.sequence","full72 positions");
        string good=string.Join("\r\n",lines)+"\r\n";
        string acceptedPreamble=Encoding.ASCII.GetString(Convert.FromBase64String("G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg=="));
        List<(string Text,bool Accept)> cases=[(good,true),(acceptedPreamble+good,true),("arbitrary firmware\r\n"+good,false),(good+"\r\n",false)];
        for(int i=0;i<lines.Count;i++)
        {
            foreach(string replacement in new[]{"",lines[i]+" "," "+lines[i],lines[i]+" diagnostic",lines[i]+"\0",lines[i].ToLowerInvariant(),lines[i]+"\r\n"+lines[i]})
                cases.Add((good.Replace(lines[i]+"\r\n",replacement+"\r\n",StringComparison.Ordinal),false));
        }
        foreach(string bad in new[]{"TE\r\nST_FA\r\nIL firmware\r\n","OS_SMO\r\nKE_FAIL\r\n","M\0\uFEFF04_RHP_BIND\r\n"})cases.Add((bad+good,false));
        foreach(string tail in new[]{"OVMF menu\r\n","partial","\r","OS_SMOKE_PASS\r\n","TEST_FAIL m04.009 exhausted\r\n"})cases.Add((good+tail,false));
        cases.Add((good.Replace("\r\n","\n",StringComparison.Ordinal),false));cases.Add((good[..^1],false));
        foreach((string from,string to) in new[]{("scalarWidth=1","scalarWidth=8"),("length=3","length=2"),("element=0x0000000001002000","element=0x0000000001002100"),("element0=0x0000001814000000","element0=0x0000001828000000"),("accessBytes=104","accessBytes=103"),("controlBytes=96","controlBytes=95"),("recordsBytes=6672","recordsBytes=4096"),("records=0x0000000000104000","records=0x0000000000103100"),("control=0x0000000000103200","control=0x0000000000101000"),("backingBytes=8192","backingBytes=4096"),("bump=0x0000000000103100","bump=0x0000000000103000"),("imageBytes=65536","imageBytes=18446744073709551616"),("zeroBytes=16","zeroBytes=15"),("relatedObject=0x0000000001002100","relatedObject=0x0000000001002000"),("relatedObject0=0x0000001850000000","relatedObject0=0x0000002050000000"),("terminatorValue=0x0000000000000000","terminatorValue=0x0000000000000001"),("constructorCount=6","constructorCount=7"),("usedAfter=24","usedAfter=25"),("scalarOffset=16","scalarOffset=8"),("priorCount=7","priorCount=6"),("contentsZero=false","contentsZero=true"),("count=7 status=1","count=8 status=1"),("flags=0x0000000050000000","flags=0x0000000051000000")})
            {Need(good.Contains(from,StringComparison.Ordinal),"mutation applies: "+from);cases.Add((good.Replace(from,to,StringComparison.Ordinal),false));}
        var manifest=cases.Select((c,i)=>new{Path=Path.Combine(directory,i+".log"),c.Accept}).ToArray();
        for(int i=0;i<cases.Count;i++)File.WriteAllText(manifest[i].Path,cases[i].Text,new UTF8Encoding(false));
        string malformed=Path.Combine(directory,"malformed-utf8.log");File.WriteAllBytes(malformed,[0xC0,0xAF,..Encoding.ASCII.GetBytes(good)]);
        manifest=[..manifest,new {Path=malformed,Accept=false}];
        string json=Path.Combine(directory,"cases.json");File.WriteAllText(json,System.Text.Json.JsonSerializer.Serialize(manifest));
        foreach(var entry in manifest)
        {
            bool accepted = true;
            try { OS.Tooling.QemuTranscripts.AssertM04StringAllocationTranscript(entry.Path); }
            catch (OS.Tooling.ToolFailure) { accepted = false; }
            Need(accepted == entry.Accept, "whole transcript control " + entry.Path + " expected=" + entry.Accept);
        }
        EfiTranscriptControls.RunnerExitCodes();
        Console.WriteLine("TRANSCRIPT_CONTROLS m04.009 cases=" + manifest.Length);
    }
    private static string Between(string text,string start,string end){int i=text.IndexOf(start,StringComparison.Ordinal);Need(i>=0,start);int j=text.IndexOf(end,i,StringComparison.Ordinal);Need(j>=i,end);return text[i..j];}

    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void Save(string directory,Result r){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"process.log"),r.Output);File.WriteAllText(Path.Combine(directory,"exit.txt"),r.Code.ToString());}

    private static Result Process(string root,string executable,string[] arguments)
    {
        ProcessStartInfo start=new(executable){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};

        foreach(string a in arguments)start.ArgumentList.Add(a);
        string evidence=Scratch(root,"child-command");DateTime begun=DateTime.UtcNow;
        File.WriteAllText(Path.Combine(evidence,"command.json"),System.Text.Json.JsonSerializer.Serialize(new {Executable=executable,Arguments=arguments,WorkingDirectory=root,StartedUtc=begun}));
        using System.Diagnostics.Process p=System.Diagnostics.Process.Start(start)!;
        Task<string> stdout=p.StandardOutput.ReadToEndAsync(),stderr=p.StandardError.ReadToEndAsync();
        bool timeout=!p.WaitForExit(900000);
        if(timeout){p.Kill(entireProcessTree:true);p.WaitForExit();}
        Task.WaitAll(stdout,stderr);
        File.WriteAllText(Path.Combine(evidence,"stdout.log"),stdout.Result,new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(evidence,"stderr.log"),stderr.Result,new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(evidence,"result.json"),System.Text.Json.JsonSerializer.Serialize(new {NativeExit=p.ExitCode,TimedOut=timeout,StartedUtc=begun,EndedUtc=DateTime.UtcNow}));
        if(timeout)throw new TimeoutException(executable+" evidence="+evidence);
        return new(p.ExitCode,stdout.Result+stderr.Result);
    }

    private static string Scratch(string root,string name){string p=Path.Combine(root,"artifacts/issue528-tests",name,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}

    private static void Need(bool x,string reason){if(!x)throw new InvalidOperationException(reason);}

    private static Result Command(params string[] args) { var result = HostToolProcess.RunTool(args); return new(result.ExitCode, result.Output + result.Error); }
    private readonly record struct Result(int Code,string Output);
}
#endif
