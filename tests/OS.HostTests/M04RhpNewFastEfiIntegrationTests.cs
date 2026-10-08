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
#if RHP_CORE_TEST
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

// Compiles the actual private POD core. No CLR object address or privileged path is invoked.
internal static unsafe class RhpCoreTestProgram
{
    private delegate bool RejectionReadback(RhpNewFastBumpState* bump,byte* bumpBefore,byte* backing,byte* backingBefore);
    private static int Main()
    {
        Run("original authority and byte observations", Allocation);
        Run("bounded PE and exact token", ImageBounds);
        Run("storage rejection and all-byte preservation", Rejections);
        Run("actual leaf rejection and exhaustion", Exhaustion);
        Run("POD and zero managed allocation", Pod);
        Console.WriteLine("TEST_PASS rhp.core.tests");
        return 0;
    }
    private static void Run(string name, Action test)
    {
        Console.WriteLine("TEST_START rhp.core " + name);
        try { test(); Console.WriteLine("TEST_PASS rhp.core " + name); }
        catch (Exception ex) { Console.WriteLine("TEST_FAIL rhp.core " + name + " " + ex.Message); throw; }
    }
    private static void Need(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }

    private static void Allocation()
    {
        using F f = new();
        byte[] before = f.Snapshot();
        Need(f.Prepare() == RhpAdmissionStatus.Success, "prepare actual authority");
        f.Only(before, (0x3000,104),(0x3200,96),(0x4000,6672));
        before = f.Snapshot();
        Need(f.Bind().Succeeded, "bind actual allocator");
        f.Only(before, (0x3100,80));
        Need(f.Bind().Status == RhpNewFastStatus.AlreadyInitialized, "repeat actual bind rejects");
        ulong used = 0;
        for (ulong id = 1; id <= 10; id++)
        {
            uint size = f.Size(id);
            // The actual allocator must clear dirty selected bytes without touching earlier objects.
            new Span<byte>(f.Page + used, (int)size).Fill(0xBC);
            before = f.Snapshot();
            RhpAllocationAttempt r = f.Allocate(id);
            Need(r.Succeeded && r.Leaf.MappedAllocationStart == (ulong)f.Page + used &&
                r.Leaf.ObjectReference == (ulong)f.Page + used + 8 && r.Leaf.BytesConsumed == size &&
                r.Leaf.UsedBefore == used && r.Leaf.UsedAfter == used + size && r.Leaf.AllocationCount == id &&
                r.Observation.ZeroOr == 0 && r.Observation.ZeroBytes == size - 8 &&
                r.Observation.Sync == 0 && r.Observation.ObservedMt == f.Token(id), "independent result and observed bytes id=" + id);
            byte[] expected = new byte[size];
            BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8,8), f.Token(id));
            Need(new ReadOnlySpan<byte>(f.Page + used, (int)size).SequenceEqual(expected), "complete independent object byte oracle");
            f.Only(before,(0x3100,80),(0x3200,96),(0x4000,6672),(0x8000+(int)used,(int)size));
            Need(f.Complete(id, r.Leaf.ObjectReference) == RhpAdmissionStatus.Success, "typed-observation seam complete");
            used += size;
        }
        Need(used == 272 && f.Bump->AllocationCount == 10 && f.Control->Completed == 10, "literal representative fixture extent");
        Need(f.Check() == RhpAdmissionStatus.Success && f.Retained().IsReady && !f.Retained().ContentsAreZeroNow,
            "ownership retained with object content");
        f.Page[16] ^= 1;
        before = f.Snapshot();
        Need(f.Allocate(1).Status == RhpAdmissionStatus.PriorObjectChanged, "prior scalar change rejected before next allocation");
        f.Unchanged(before);
    }

    private static void ImageBounds()
    {
        using F f = new();
        Need(f.Prefix(f.Token(1),f.Token(1)), "valid loaded PE prefix");
        Need(!f.Prefix(ulong.MaxValue - 3,f.Token(1)), "unknown token rejected without dereference");
        Need(!f.Prefix(0,0), "zero token");
        byte[] pristine = new ReadOnlySpan<byte>(f.Image,16384).ToArray();
        (int Offset, uint Value)[] invalid = [
            (0,0),(60,0xFFFFFFF0),(0x80,0),(0x84,0x00008664),(0x84,0x00098664),
            (0x84,0x0001014C),(0x94,0),(0x98,0x10B),(0xD0,0x4001),(0xD4,0x100),
            (0x190,0xFFFFF000),(0x194,0x300),(0x194,0x3FF0),(0x198,0x110),
            (0x1AC,0x00000040),(0x1AC,0xC0000040),(0x1AC,0x60000040),(0x1AC,0x42000040)
        ];
        foreach (var mutation in invalid)
        {
            pristine.CopyTo(new Span<byte>(f.Image,16384));
            W32(f.Image + mutation.Offset,mutation.Value);
            Need(!f.Prefix(f.Token(1),f.Token(1)), "PE mutation rejected at " + mutation.Offset.ToString("X"));
        }
        pristine.CopyTo(new Span<byte>(f.Image,16384));
        W32(f.Image+0x198,0x2000); Need(f.Prefix(f.Token(1),f.Token(1)), "raw size may exceed virtual size");
        W32(f.Image+0x198,0x800); Need(f.Prefix(f.Token(1),f.Token(1)), "virtual size may exceed raw size outside chosen prefix");
        Need(!f.Prefix((ulong)f.Image+0x17F0,(ulong)f.Image+0x17F0), "whole prefix must fit initialized raw extent");
        pristine.CopyTo(new Span<byte>(f.Image,16384));
        W32(f.Image+0x84,0x00028664); // Two nonempty sections may not overlap, even away from chosen token.
        new ReadOnlySpan<byte>(f.Image+0x188,40).CopyTo(new Span<byte>(f.Image+0x1B0,40));
        Need(!f.Prefix(f.Token(1),f.Token(1)), "ambiguous overlapping sections");
        W32(f.Image+0x1BC,0x2000); Need(f.Prefix(f.Token(1),f.Token(1)), "disjoint second section");
        W32(f.Image+0x84,0x00088664); Need(f.Prefix(f.Token(1),f.Token(1)), "eight bounded sections including empty sections");
        W32(f.Image+0x84,0x00098664); Need(!f.Prefix(f.Token(1),f.Token(1)), "ninth section fails closed");
    }

    private static void Rejections()
    {
        using F f = new();
        // Invoke the actual private rejection reader; exercise every bump byte,
        // including authority fields outside UsedBytes/AllocationCount.
        var check=typeof(RhpNewFastAllocationCore).GetMethod("RejectedAllocationUnchanged",
            System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!
            .CreateDelegate<RejectionReadback>();
        byte* bumpBefore=stackalloc byte[80];byte* pageBefore=stackalloc byte[4096];
        new ReadOnlySpan<byte>((byte*)f.Bump,80).CopyTo(new Span<byte>(bumpBefore,80));
        new ReadOnlySpan<byte>(f.Page,4096).CopyTo(new Span<byte>(pageBefore,4096));
        Need(check(f.Bump,bumpBefore,f.Page,pageBefore),"complete rejection snapshot accepts unchanged bytes");
        for(int i=0;i<80;i++)
        {
            ((byte*)f.Bump)[i]^=1;
            Need(!check(f.Bump,bumpBefore,f.Page,pageBefore),"bump rejection readback byte "+i);
            ((byte*)f.Bump)[i]^=1;
        }
        foreach(int i in new[]{0,7,8,79,4095})
        {
            f.Page[i]^=1;Need(!check(f.Bump,bumpBefore,f.Page,pageBefore),"page rejection readback byte "+i);f.Page[i]^=1;
        }
        for (int mutation = 0; mutation < 11; mutation++)
        {
            f.Reset(); RhpNewFastHeapAccess a=f.Original;
            RhpNewFastHeapAccess* access=f.Access; RhpNewFastBumpState* bump=f.Bump;
            RhpAllocationControl* control=f.Control; RhpObservationStorage* records=f.Records;
            switch(mutation)
            {
                case 0: a.BackingCapacity=4096;break;
                case 1: a.ReservationCount=4;break;
                case 2: a.NodeCapacity=2;break;
                case 3: a.HeapCapacity=232;break;
                case 4: a.ParentIndex=5;break;
                case 5: access=(RhpNewFastHeapAccess*)f.Control;break;
                case 6: bump=(RhpNewFastBumpState*)f.Page;break;
                case 7: records=(RhpObservationStorage*)((byte*)f.Control+8);break;
                case 8: control=(RhpAllocationControl*)((byte*)f.Control+1);break;
                case 9: a.AllocatorCapacity=0;break;
                case 10: records=(RhpObservationStorage*)(ulong.MaxValue-7);break;
            }
            byte[] before=f.Snapshot();
            Need(RhpNewFastAllocationCore.Prepare(a,access,bump,control,records,f.Image,16384)!=RhpAdmissionStatus.Success,"invalid numeric storage " + mutation);
            f.Unchanged(before);
        }
        foreach(int slot in new[]{0x3000,0x3100,0x3200,0x4000,0x4000+6671})
        {
            f.Reset();f.Storage[slot]=1;byte[] before=f.Snapshot();
            Need(f.Prepare()==RhpAdmissionStatus.AlreadyPrepared,"every scratch prefix must start zero");f.Unchanged(before);
        }
        f.Reset();f.Page[4095]=1;byte[] dirty=f.Snapshot();
        Need(f.Prepare()==RhpAdmissionStatus.RetainedRejected,"whole-page zero check");f.Unchanged(dirty);
        f.Reset();Need(f.Prepare()==RhpAdmissionStatus.Success && f.Bind().Succeeded,"prepared rejection fixture");
        for(int mutation=0;mutation<5;mutation++)
        {
            ulong oldBefore=f.Control->CanaryBefore,oldAfter=f.Records->CanaryAfter;
            ulong oldHeap=f.Original.Heap->SelfAddress; // No writes to private host leaf fields.
            if(mutation==0)f.Control->InProgress=1;
            if(mutation==1)f.Control->CanaryBefore^=1;
            if(mutation==2)f.Records->CanaryAfter^=1;
            if(mutation==3)f.Access->BackingCapacity=4096;
            if(mutation==4)f.Control->Completed=11;
            byte[] before=f.Snapshot();Need(!f.Allocate(1).Succeeded,"reentry/canary/authority rejection");f.Unchanged(before);
            f.Control->InProgress=0;f.Control->CanaryBefore=oldBefore;f.Records->CanaryAfter=oldAfter;
            f.Access->BackingCapacity=8192;f.Control->Completed=0;
            Need(f.Original.Heap->SelfAddress==oldHeap,"original heap never relocated");
        }
        byte[] snapshot=f.Snapshot();
        Need(f.Allocate(1,token:1).Status==RhpAdmissionStatus.UnknownToken,"finite exact token first");f.Unchanged(snapshot);
        Need(f.Allocate(2).Status==RhpAdmissionStatus.InvalidFixture,"fixture order");f.Unchanged(snapshot);
        *f.Original.Reservations=default;
        snapshot=f.Snapshot();Need(f.Allocate(1).Status==RhpAdmissionStatus.RetainedRejected,"actual live parent ledger revalidation");f.Unchanged(snapshot);
    }

    private static void Exhaustion()
    {
        using F f=new();Need(f.Prepare()==RhpAdmissionStatus.Success,"before bind setup");
        byte[] page=f.PageSnapshot(),bump=f.BumpSnapshot();
        RhpAllocationAttempt beforeBind=f.Allocate(1);
        Need(beforeBind.Status==RhpAdmissionStatus.AllocationRejected && beforeBind.Leaf.Status==RhpNewFastStatus.NotInitialized,
            "actual frozen leaf status5 before bind");
        f.PageUnchanged(page);f.BumpUnchanged(bump);
        Need(f.Bind().Succeeded,"bind after controlled rejected attempt");
        for(ulong id=1;id<=10;id++){var r=f.Allocate(id);Need(r.Succeeded && f.Complete(id,r.Leaf.ObjectReference)==RhpAdmissionStatus.Success,"fixture complete");}
        while(24<=4096-f.Bump->UsedBytes)
        {
            var r=f.Allocate(1);Need(r.Succeeded,"filler actual allocation");
            W64((byte*)r.Leaf.ObjectReference+8,5555);
        }
        page=f.PageSnapshot();bump=f.BumpSnapshot();
        var exhausted=f.Allocate(1);
        Need(exhausted.Status==RhpAdmissionStatus.AllocationRejected && exhausted.Leaf.Status==RhpNewFastStatus.Exhausted &&
            f.Bump->UsedBytes==4088 && f.Bump->AllocationCount==169,"actual frozen leaf status15 at exact remaining8");
        f.PageUnchanged(page);f.BumpUnchanged(bump);
        Need(f.Check()==RhpAdmissionStatus.Success,"ten prior object records remain intact after exhaustion");
    }

    private static void Pod()
    {
        Need(sizeof(RhpNewFastHeapAccess)==104 && sizeof(RhpNewFastBumpState)==80 && sizeof(RhpAllocationControl)==96 &&
            sizeof(RhpObjectObservation)==256 && sizeof(RhpObservationStorage)==6672,"exact POD storage sizes");
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<RhpAllocationControl>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<RhpObservationStorage>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<RhpAllocationAttempt>(),"no hidden managed references");
        using F f=new();Need(f.Prepare()==RhpAdmissionStatus.Success && f.Bind().Succeeded,"zero allocation fixture");
        for(int i=0;i<20;i++)Need(f.Check()==RhpAdmissionStatus.Success,"warm check");
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<100;i++)
        {
            if(f.Check()!=RhpAdmissionStatus.Success || !f.Prefix(f.Token(1),f.Token(1)))throw new InvalidOperationException("unexpected warm failure");
        }
        long after=GC.GetAllocatedBytesForCurrentThread();Need(after==before,"zero allocation retained/prefix core");
        before=GC.GetAllocatedBytesForCurrentThread();
        for(ulong id=1;id<=10;id++)
        {
            var allocated=f.Allocate(id);
            if(!allocated.Succeeded || f.Complete(id,allocated.Leaf.ObjectReference)!=RhpAdmissionStatus.Success)
                throw new InvalidOperationException("unexpected allocation core failure");
        }
        after=GC.GetAllocatedBytesForCurrentThread();Need(after==before,"zero managed allocation in actual allocation core");
        Console.WriteLine("RHP_CORE_POD access=104 bump=80 control=96 observation=256 records=6672 allocationDelta="+(after-before));
    }

    private static void W32(byte* p,uint x)=>BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(p,4),x);
    private static void W64(byte* p,ulong x)=>BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(p,8),x);
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
        internal F(){Need(Storage!=null,"native storage");Reset();}
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
internal static class M04RhpNewFastEfiIntegrationTests
{
    private const string Flag="EnableM04RhpNewFastProbe";
    private static readonly string[] Chain=["EnableM01LoadedImageStackProbe","EnableM01LiveAllocatorProbe","EnableM04IndependentEndianConformanceProbe",
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","EnableM04ArithmeticConformanceProbe","EnableM04LayoutContractProbe","EnableM04ManagedHeapPageRangeProbe"];
    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("source profile and original handoff",TestSource);
        Run("actual POD core and bounded PE",TestCore);
        Run("exact frozen leaf source firewall",TestFirewall);
        Run("strongest full transcript and negatives",TestTranscript);
        Run("explicit profile prerequisites and conflicts",TestProfiles);
        Run("actual default and three allocation artifacts",TestArtifacts);
    }
    private static void Run(string name,Action test)
    {
        Console.WriteLine("TEST_START m04.007.efi "+name);
        try{test();Console.WriteLine("TEST_PASS m04.007.efi "+name);}
        catch(Exception ex){Console.WriteLine("TEST_FAIL m04.007.efi "+name+" "+ex.Message);throw;}
    }
    private static void TestCore()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Scratch(root,"core");
        string includes=string.Join('\n',new[]{"src/OS/Runtime/RhpNewFastAllocation.cs","tests/OS.HostTests/M04RhpNewFastEfiIntegrationTests.cs"}
            .Select(x=>"<Compile Include=\""+SecurityElement.Escape(Path.Combine(root,x))+"\"/>"));
        File.WriteAllText(Path.Combine(directory,"Core.csproj"),"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion>
            <OutputType>Exe</OutputType><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>RHP_CORE_TEST</DefineConstants><NoWarn>0649</NoWarn></PropertyGroup><ItemGroup>
            """+includes+"<ProjectReference Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj"))+"\"/></ItemGroup></Project>");
        Result result=Process(root,"dotnet",["run","--project",Path.Combine(directory,"Core.csproj"),"-c","Release","--nologo"]);
        File.WriteAllText(Path.Combine(directory,"process.log"),result.Output);File.WriteAllText(Path.Combine(directory,"exit.txt"),result.Code.ToString());
        Need(result.Code==0 && result.Output.Contains("TEST_PASS rhp.core.tests",StringComparison.Ordinal),result.Output);
    }
    private static void TestSource()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        XDocument p=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        string[] compile=p.Descendants("Compile").Select(x=>x.Attribute("Include")!.Value+"|"+(x.Attribute("Link")?.Value??"")).ToArray();
        Need(compile.Length==122 && compile[80]=="..\\OS.Kernel\\Runtime\\GuestTestRhpNewFast.cs|Runtime\\GuestTestRhpNewFast.cs" && compile[81]=="..\\OS.Kernel\\Runtime\\GuestTestArrayAllocationLengthOverflowChecks.cs|Runtime\\GuestTestArrayAllocationLengthOverflowChecks.cs" && compile[84]=="Runtime\\RhpNewFastAllocation.cs|","exact appended closure");
        foreach(string flag in new[]{Flag,"EnableM04RhpNewFastBeforeBindFailureProbe","EnableM04RhpNewFastExhaustionFailureProbe"})
        {
            XElement value=p.Descendants(flag).Single();
            Need(value.Value=="false" && value.Attribute("Condition")?.Value=="'$("+flag+")' == ''","default false "+flag);
        }
        foreach(string required in Chain)Need(p.Descendants("Error").Any(x=>x.Attribute("Condition")?.Value=="'$("+Flag+")' == 'true' and '$("+required+")' != 'true'"),"explicit prerequisite "+required);
        string leaf=File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Runtime/GuestTestRhpNewFast.cs")).Replace("\r\n","\n",StringComparison.Ordinal);
        Need(Hash(Encoding.UTF8.GetBytes(leaf))=="7a14fa23e28548fa94d30e5569f786a1ce4c5bbe2375ac5b23a493d06f20ebd5","frozen allocation leaf");
        string entry=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));
        int zero=entry.IndexOf("if (!M01StageTwoAdapter.ValidateManagedHeapBeforeBootPlan",StringComparison.Ordinal);
        int handoff=entry.IndexOf("M01StageTwoAdapter.RunRhpNewFastAfterZeroCheckpoint();",StringComparison.Ordinal);
        Need(zero>=0 && handoff>zero && entry.IndexOf("EfiEntry.WriteBootSequencePlan(",zero,StringComparison.Ordinal)>handoff,"zero then allocation then boot sequence");
        string adapter=File.ReadAllText(Path.Combine(root,"src/OS/Runtime/RhpNewFastAllocation.cs"));
        Need(adapter.Contains("GuestTestRhpNewFast.TryAllocate(*access, bump, 80,",StringComparison.Ordinal) &&
            adapter.Contains("if (token == 0 || token != expectedToken",StringComparison.Ordinal) &&
            !adapter.Contains("Serialize(",StringComparison.Ordinal),"actual frozen call and no data-only publication");
        int snapshot=adapter.IndexOf("byte* bumpBefore = stackalloc byte[80];",StringComparison.Ordinal);
        int allocation=adapter.IndexOf("GuestTestRhpNewFast.TryAllocate(*access, bump, 80,",StringComparison.Ordinal);
        int rejection=adapter.IndexOf("!RejectedAllocationUnchanged(bump, bumpBefore, access->Backing, records->BeforeAttempt)",StringComparison.Ordinal);
        Need(snapshot>=0&&snapshot<allocation&&rejection>allocation&&
            adapter.Contains("for (ulong i = 0; i < 80; i++) bumpBefore[i] = ((byte*)bump)[i];",StringComparison.Ordinal),"all80-byte snapshot brackets the actual leaf");
        foreach(string forbidden in new[]{"FreeOnePage(","FreePages(","RhBox(","new object[","throw ","System.Reflection","System.Console"})
            Need(!adapter.Contains(forbidden,StringComparison.Ordinal),"no expanded runtime "+forbidden);
    }
    private static void TestFirewall()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Scratch(root,"firewall");
        string ps=Path.Combine(directory,"Test.ps1");
        File.WriteAllText(ps,"""
            param([string]$Root)
            $ErrorActionPreference='Stop';$t=$null;$e=$null
            $ast=[System.Management.Automation.Language.Parser]::ParseFile((Join-Path $Root 'scripts/build-efi-shell.ps1'),[ref]$t,[ref]$e)
            foreach($name in @('Get-M01Utf8Sha256','Get-NostdlibEfiActiveSource','Test-M01ActiveSourceSurface')) {
                $f=$ast.Find({param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name},$true)
                . ([scriptblock]::Create($f.Extent.Text))
            }
            $name='..\OS.Kernel\Runtime\GuestTestRhpNewFast.cs'
            $text=[IO.File]::ReadAllText((Join-Path $Root 'src/OS.Kernel/Runtime/GuestTestRhpNewFast.cs')).Replace("`r`n","`n")
            foreach($good in @($text,$text.Replace("`n","`r`n"))) { if(-not (Test-M01ActiveSourceSurface $good $name).Succeeded){throw 'exact reviewed source rejected'} }
            foreach($bad in @(($text+' '), ($text+"`r"),($text+"`n// extra"),$text.Replace('4096','4095'))) { if((Test-M01ActiveSourceSurface $bad $name).Succeeded){throw 'changed content admitted'} }
            foreach($bad in @($name.ToLowerInvariant(),($name+' '),$name.Replace('\','/'),$name.Replace('Runtime\','Runtime\.\'),$name.Insert(4,[string][char]0),'..\OS.Kernel\Runtime\Other.cs')) { if((Test-M01ActiveSourceSurface $text $bad).Succeeded){throw ('path alias admitted ['+$bad+']')} }
            if((Test-M01ActiveSourceSurface 'using System; public class X { }' '..\OS.Kernel\Other.cs').Succeeded){throw 'generic scanner weakened'}
            Write-Output 'TEST_PASS m04.007.efi.firewall'
            """);
        Result result=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",ps,"-Root",root]);
        Save(directory,result);Need(result.Code==0,result.Output);
    }
    private static void TestTranscript()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Scratch(root,"transcript");
        string source=File.ReadAllText(Path.Combine(root,"scripts/run-qemu-smoke.ps1"));
        Need(source.Contains("QEMU_SCRIPT_M04_RHP_NEW_FAST_TRANSCRIPT exactWholeLines=true lines=78 objects=10 compiledNew=4 direct=6 terminal=true",StringComparison.Ordinal),"reported exact transcript line count");
        string defs=Between(source,"$script:M04IndependentEndianConformanceReadyMarker =","function Invoke-NativeCommand {");
        string functions=Between(source,"function Assert-M04ManagedHeapPageRangeTranscript {","function Assert-M04LayoutContractTranscript {");
        Result dry=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/run-qemu-smoke.ps1"),"-BootPolicy", "Diagnostic", "-DryRun","-BuildEfi",..Chain.Append(Flag).Select(x=>"-"+x)]);
        Save(Path.Combine(directory,"dryrun"),dry);Need(dry.Code==0,dry.Output);
        string[] chain=dry.Output.Replace("\r","").Split('\n').Single(x=>x.StartsWith("ORDERED_MARKERS ",StringComparison.Ordinal))[16..].Split(';');
        List<string> lines=["TEST_START efi.main",..chain.Take(6),"UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 3","UEFI_MEMORY_MAP_OWNED","BOOT_HANDOFF_READY","UEFI_MEMORY_MAP_NORMALIZED","BOOT_MEMORY_MAP_ACCEPTED","BOOT_MEMORY_REGIONS_READY","BOOT_MEMORY_REGION_COUNT 3","BOOT_PAGE_ALLOCATOR_READY status=Ready regions=3 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0","BOOT_ACPI_RSDP_HANDOFF_READY","UEFI_ACPI_RSDP_READY",..chain.Skip(6)];
        const string identity="allocatorState=0x0000000000100000 nodes=0x0000000000100040 heapState=0x0000000000101000 ledger=0x0000000000102000 parentIndex=4 parentStart=0x0000000000900000 parentEnd=0x0000000000902000 backing=0x0000000000900000 retainedStart=0x0000000000900000 retainedEnd=0x0000000000901000";
        lines[lines.IndexOf("M04_HEAP_PAGE_BOUND")]+=" "+identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_RETAINED")]+=" checkpoint=beforeBootPlan "+identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_INITIALIZED")]+=" pages=1 bytes=4096 freePagesBefore=2 freeBytesBefore=8192 allocatedPagesBefore=0 allocatedBytesBefore=0 freePagesAfter=1 freeBytesAfter=4096 allocatedPagesAfter=1 allocatedBytesAfter=4096 readBytes=4096 contentsZero=true";
        lines[lines.IndexOf("M04_HEAP_PAGE_CURRENT")]+=" freePages=1 freeBytes=4096 allocatedPages=1 allocatedBytes=4096 readBytes=4096 contentsZero=true";
        lines[45]="M04_RHP_PREP image=0x0000000001000000 imageBytes=65536 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 control=0x0000000000103200 controlBytes=96 records=0x0000000000104000 recordsBytes=6672";
        lines[46]="M04_RHP_BIND image=0x0000000001000000 imageBytes=65536 allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 ledgerBytes=200 parentIndex=4 backing=0x0000000000900000 backingBytes=8192 heap=0x0000000000101000 heapBytes=216 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 status=1 used=0 count=0";
        uint[] sizes=[24,32,32,32,24,24,24,32,24,24];
        uint[] flags=[0x50000000,0x51000000,0x53000000,0x52000000,0x20000080,0x20000080,0x40000000,0x41000000,0x43000000,0x4A000200];
        ulong used=0;
        static string H(ulong x)=>"0x"+x.ToString("X16");
        for(int id=1;id<=10;id++)
        {
            uint size=sizes[id-1];ulong mt=0x1001100UL+(ulong)(id-1)*32,allocation=0x900000+used;
            lines[45+id*2]=$"M04_RHP_OBJECT id={id} route={(id<=4?"new":"direct")} mt={H(mt)} prefix0={H(((ulong)size<<32)|flags[id-1])} prefix1={H(0x1002000)} prefix2={H(0x1234567800020001)} flags={H(flags[id-1])} base={size} allocation={H(allocation)} reference={H(allocation+8)} end={H(allocation+size)} usedBefore={used} usedAfter={used+size} count={id} sync={H(0)} observedMt={H(mt)} zeroBytes={size-8} zeroOr={H(0)}";
            lines[46+id*2]=$"M04_RHP_READBACK id={id} scalarOffset={(id==1?8:id<=4?16:0)} scalar={H(id<=4?(ulong)id*0x1111111111111111:0)} referenceSlotBytes={size-16-(id<=4?8:0)} referenceSlotOr={H(0)} priorCount={id} priorMismatchBytes=0 canaryMismatchBytes=0";
            used+=size;
        }
        lines[67]="M04_RHP_RETAIN allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 parentIndex=4 backing=0x0000000000900000 heap=0x0000000000101000 bump=0x0000000000103100 heapStart=0x0000000000900000 heapEnd=0x0000000000901000 initialPages=2 freePages=1 allocatedPages=1 readBytes=4096 contentsZero=false used=272 count=10 status=1";
        Need(lines.Count==78 && lines[44]=="TEST_START m04.007.rhp_new_fast" && lines[70]=="TEST_START boot.sequence","full78 positions");
        string good=string.Join("\r\n",lines)+"\r\n";
        List<(string Text,bool Accept)> cases=[(good,true),("\u001b[2J\u001b[HUEFI Interactive Shell\r\n"+good,true),(good+"\r\n",true)];
        for(int i=0;i<lines.Count;i++)
        {
            foreach(string replacement in new[]{"",lines[i]+" "," "+lines[i],lines[i]+" diagnostic",lines[i]+"\0",lines[i].ToLowerInvariant(),lines[i]+"\r\n"+lines[i]})
                cases.Add((good.Replace(lines[i]+"\r\n",replacement+"\r\n",StringComparison.Ordinal),false));
        }
        foreach(string bad in new[]{"TE\r\nST_FA\r\nIL firmware\r\n","OS_SMO\r\nKE_FAIL\r\n","M\0\uFEFF04_RHP_BIND\r\n"})cases.Add((bad+good,false));
        foreach(string tail in new[]{"OVMF menu\r\n","partial","\r","OS_SMOKE_PASS\r\n","TEST_FAIL m04.007.rhp_new_fast exhausted\r\n"})cases.Add((good+tail,false));
        cases.Add((good.Replace("\r\n","\n",StringComparison.Ordinal),false));cases.Add((good[..^1],false));
        foreach((string from,string to) in new[]{("accessBytes=104","accessBytes=103"),("controlBytes=96","controlBytes=95"),("recordsBytes=6672","recordsBytes=4096"),("records=0x0000000000104000","records=0x0000000000103100"),("control=0x0000000000103200","control=0x0000000000101000"),("backingBytes=8192","backingBytes=4096"),("bump=0x0000000000103100","bump=0x0000000000103000"),("imageBytes=65536","imageBytes=18446744073709551616"),("zeroBytes=16","zeroBytes=15"),("usedAfter=24","usedAfter=25"),("scalarOffset=16","scalarOffset=8"),("priorCount=10","priorCount=9"),("contentsZero=false","contentsZero=true"),("count=10 status=1","count=11 status=1"),("flags=0x0000000050000000","flags=0x0000000051000000")})
            cases.Add((good.Replace(from,to,StringComparison.Ordinal),false));
        var manifest=cases.Select((c,i)=>new{Path=Path.Combine(directory,i+".log"),c.Accept}).ToArray();
        for(int i=0;i<cases.Count;i++)File.WriteAllText(manifest[i].Path,cases[i].Text,new UTF8Encoding(false));
        string json=Path.Combine(directory,"cases.json");File.WriteAllText(json,System.Text.Json.JsonSerializer.Serialize(manifest));
        string ps=Path.Combine(directory,"Test.ps1");File.WriteAllText(ps,"$ErrorActionPreference='Stop'\n"+defs+functions+"\n$cases=[IO.File]::ReadAllText('"+json+"')|ConvertFrom-Json\n"+"""
            foreach($case in $cases){$accepted=$true;$reason='';try{Assert-M04RhpNewFastTranscript $case.Path}catch{$accepted=$false;$reason=$_.Exception.Message};if($accepted -ne $case.Accept){throw ($case.Path+' accepted='+$accepted+' reason='+$reason)}}
            foreach($exitCode in @('0','-1','2147483647','-2147483648')){Assert-M04ManagedHeapRunnerResult @('SERIAL_MARKER_DRAIN milliseconds=750',('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exitCode))}
            foreach($exitCode in @('none','2147483648','-2147483649','-0','01','+1')){$rejected=$false;try{Assert-M04ManagedHeapRunnerResult @('SERIAL_MARKER_DRAIN milliseconds=750',('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exitCode))}catch{$rejected=$true};if(-not $rejected){throw 'invalid exit admitted'}}
            Write-Output ('TEST_PASS m04.007.efi.transcript cases='+$cases.Count)
            """);
        Result result=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",ps]);Save(directory,result);Need(result.Code==0,result.Output);
    }
    private static void TestProfiles()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Scratch(root,"profiles");
        string before="EnableM04RhpNewFastBeforeBindFailureProbe",exhaust="EnableM04RhpNewFastExhaustionFailureProbe";
        string[] all=Chain.Append(Flag).ToArray();
        string[] conflicts=["EnableM01OwnedPagingProbe",..XDocument.Load(Path.Combine(root,"src/OS/OS.csproj")).Descendants()
            .Where(x=>x.Name.LocalName.StartsWith("EnableM02",StringComparison.Ordinal)).Select(x=>x.Name.LocalName).Distinct(StringComparer.Ordinal),
            "EnableUefiGoneSimulationProbe","EnableExitBootServicesFreshMapProbe","EnableAcpiPhysicalDiscoveryProbe","EnableM03MemoryMapCaptureProbe","EnableM03FirmwareEntropySeedProbe","EnableM04TypeManagerModuleRegistrationProbe"];
        Need(conflicts.Length==22,"all unrelated profile conflicts");
        List<string[]> invalid=[..Chain.Select(missing=>all.Where(x=>x!=missing).ToArray()),..conflicts.Select(x=>all.Append(x).ToArray()),[before],[exhaust],[..all,before,exhaust]];
        int id=0;
        foreach(string[] flags in invalid)
        foreach(bool runner in new[]{false,true})
        {
            Result r=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts",runner?"run-qemu-smoke.ps1":"build-efi-shell.ps1"),
                ..(runner?new[]{"-BootPolicy", "Diagnostic", "-DryRun","-BuildEfi"}:new[]{"-PreflightOnly","-LogPath",Path.Combine(directory,"preflight-"+id+".log")}),..flags.Select(x=>"-"+x)]);
            Save(Path.Combine(directory,(id++).ToString()),r);
            Need(r.Code!=0 && (flags.Contains("EnableM04TypeManagerModuleRegistrationProbe",StringComparer.Ordinal) ? r.Output.Contains("m04_type_manager_profile_conflict property=EnableM01LiveAllocatorProbe",StringComparison.Ordinal) : flags.Contains("EnableM03FirmwareEntropySeedProbe",StringComparer.Ordinal) ? r.Output.Contains("m03_firmware_entropy_profile_conflict property=EnableM01LiveAllocatorProbe",StringComparison.Ordinal) : flags.Contains("EnableM03MemoryMapCaptureProbe",StringComparer.Ordinal) ? r.Output.Contains("m03_memory_map_capture_profile_conflict property=EnableM01LoadedImageStackProbe",StringComparison.Ordinal) : r.Output.Contains("m04_rhp_new_fast",StringComparison.Ordinal)),"explicit profile rejection "+string.Join(',',flags)+" "+r.Output);
        }
        foreach(string[] flags in new[]{all,all.Append(before).ToArray(),all.Append(exhaust).ToArray()})
        {
            Result r=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/run-qemu-smoke.ps1"),"-BootPolicy", "Diagnostic", "-DryRun","-BuildEfi",..flags.Select(x=>"-"+x)]);
            Save(Path.Combine(directory,(id++).ToString()),r);Need(r.Code==0,r.Output);
            Need(r.Output.Contains("TEST_FAIL m04.007.rhp_new_fast",StringComparison.Ordinal),"negative profile does not weaken generic failure inventory");
        }
    }
    private static void TestArtifacts()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        string[] all=Chain.Append(Flag).ToArray();
        foreach(var lane in new[]{("default",Array.Empty<string>()),("positive",all),("before-bind",all.Append("EnableM04RhpNewFastBeforeBindFailureProbe").ToArray()),("exhaustion",all.Append("EnableM04RhpNewFastExhaustionFailureProbe").ToArray())})
        {
            string directory=Scratch(root,"artifact-"+lane.Item1),artifact=Path.Combine(root,"artifacts/efi/issue519-tests-"+Guid.NewGuid().ToString("N")+".EFI");
            Result r=Process(root,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/build-efi-shell.ps1"),"-EfiOutputPath",artifact,"-LogPath",Path.Combine(directory,"publish.log"),..lane.Item2.Select(x=>"-"+x)]);
            Save(directory,r);
            Need(r.Code==0 && r.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal) && r.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),r.Output);
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(artifact+suffix,Path.Combine(directory,"image.EFI"+suffix));
            foreach(var file in new[]{("src/OS/bin/Release/net10.0/win-x64/OS.dll","OS.dll"),("src/OS/obj/Release/net10.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net10.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(root,file.Item1),Path.Combine(directory,file.Item2));
            using var document=System.Text.Json.JsonDocument.Parse(File.ReadAllText(artifact+".link.json"));var e=document.RootElement;
            var inputs=e.GetProperty("sourceClosureInputs").EnumerateArray().ToArray();Need(inputs.Length==138,"actual138 raw sources");
            foreach(var input in inputs)Need(Hash(File.ReadAllBytes(Path.Combine(root,input.GetProperty("path").GetString()!)))==input.GetProperty("sha256").GetString(),"actual raw source unchanged");
            var evaluation=e.GetProperty("postRestoreEvaluation");Need(evaluation.GetProperty("compileCount").GetInt32()==122,"actual122 compile items");
            var flags=evaluation.GetProperty("properties").EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)).ToArray();
            Need(flags.Length==52,"complete52 evaluated flags");
            foreach(var f in flags)Need(f.Value.GetString()==(lane.Item2.Contains(f.Name,StringComparer.Ordinal)?"true":"false"),"actual requested/evaluated profile "+f.Name);
            using(var pe=new PEReader(File.OpenRead(Path.Combine(directory,"OS.dll"))))
            {
                var md=pe.GetMetadataReader();Need(md.AssemblyReferences.Count==0 && md.GetString(md.GetAssemblyDefinition().Name)=="OS","actual no-stdlib assembly");
                var exports=md.TypeDefinitions.SelectMany(t=>md.GetTypeDefinition(t).GetMethods()).Where(m=>md.GetString(md.GetMethodDefinition(m).Name)=="RhpNewFast").ToArray();
                Need(exports.Length==1,"sole RhpNewFast metadata method");
                var export=md.GetMethodDefinition(exports[0]);Need((export.ImplAttributes&System.Reflection.MethodImplAttributes.NoInlining)!=0,"actual export NoInlining");
                Need(export.RelativeVirtualAddress!=0,"actual export body");
            }
            if(lane.Item1=="positive")TestLinkedRoutes(root,directory);
            File.WriteAllText(Path.Combine(directory,"identities.json"),System.Text.Json.JsonSerializer.Serialize(Directory.GetFiles(directory).Select(p=>new{Path=Path.GetFileName(p),Sha256=Hash(File.ReadAllBytes(p))}),new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        }
    }
    private static void TestLinkedRoutes(string root,string directory,int routeCount=10)
    {
        byte[] image=File.ReadAllBytes(Path.Combine(directory,"image.EFI"));
        using var reader=new PEReader(new MemoryStream(image));var header=reader.PEHeaders.PEHeader!;
        Need(header.Magic==PEMagic.PE32Plus && reader.PEHeaders.SectionHeaders.Length is >0 and <=8,"bounded actual x64 PE sections");
        byte[] At(ulong rva,int bytes)
        {
            var sections=reader.PEHeaders.SectionHeaders.Where(s=>rva>=(ulong)s.VirtualAddress && rva+(ulong)bytes<=(ulong)s.VirtualAddress+(ulong)s.VirtualSize && rva+(ulong)bytes<=(ulong)s.VirtualAddress+(ulong)s.SizeOfRawData).ToArray();
            Need(sections.Length==1,"unique initialized extent");var s=sections[0];return image.AsSpan(checked(s.PointerToRawData+(int)(rva-(ulong)s.VirtualAddress)),bytes).ToArray();
        }
        var symbols=Regex.Matches(File.ReadAllText(Path.Combine(directory,"image.EFI.link.map")),@"^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(\S+)\s+([0-9A-Fa-f]{16})\s+",RegexOptions.Multiline)
            .ToDictionary(m=>m.Groups[1].Value,m=>Convert.ToUInt64(m.Groups[2].Value,16)-header.ImageBase,StringComparer.Ordinal);
        Need(symbols.ContainsKey("RhpNewFast"),"named actual native export");
        // LLVM provides decoded instructions/relocations. Never search arbitrary bytes for E8.
        string llvm=HostToolProcess.FindLlvmObjdump();
        Result dis=Process(root,llvm,["--disassemble","--reloc","--x86-asm-syntax=intel",Path.Combine(directory,"OS.obj")]);Save(Path.Combine(directory,"disassembly"),dis);Need(dis.Code==0,dis.Output);
        File.WriteAllText(Path.Combine(directory,"disassembler.json"),System.Text.Json.JsonSerializer.Serialize(new{Path=llvm,Sha256=Hash(File.ReadAllBytes(llvm))}));
        const string prefix="OS_OS_Runtime_RhpNewFastAllocation__";
        string[] types=["Plain","Reference","GenericReference","GenericValue","Int32","Enum","Value","ReferenceValue","GenericStruct","Nullable"];
        string[] calls=["NewPlain","NewReference","NewGenericReference","NewGenericValue","AllocateInt32","AllocateEnum","AllocateValue","AllocateReferenceValue","AllocateGenericValue","AllocateNullable"];
        uint[] sizes=[24,32,32,32,24,24,24,32,24,24],flags=[0x50000000,0x51000000,0x53000000,0x52000000,0x20000080,0x20000080,0x40000000,0x41000000,0x43000000,0x4A000200];
        List<object> rows=[];HashSet<ulong> tokens=[];
        Need(routeCount is 0 or 1 or 10,"bounded source-profile route inventory");
        for(int i=0;i<routeCount;i++)
        {
            string getter=prefix+"Type"+types[i],caller=prefix+calls[i];ulong rva=symbols[getter];byte[] code=At(rva,8);
            Need(code.AsSpan(0,3).SequenceEqual(new byte[]{0x48,0x8D,0x05}) && code[7]==0xC3,"actual intrinsic RIP-relative getter "+getter);
            ulong mt=checked((ulong)((long)rva+7+BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(3,4))));Need(tokens.Add(mt),"ten distinct authentic identities");
            byte[] raw=At(mt,24);Need(BinaryPrimitives.ReadUInt32LittleEndian(raw)==flags[i] && BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4))==sizes[i],"independent native prefix shape "+i);
            var section=reader.PEHeaders.SectionHeaders.Single(s=>mt>=(ulong)s.VirtualAddress&&mt+24<=(ulong)s.VirtualAddress+(ulong)s.VirtualSize);
            uint characteristics=(uint)section.SectionCharacteristics;Need((characteristics&0x40000000)!=0&&(characteristics&0xA2000000)==0,"initialized readable nonwritable nondiscardable prefix");
            string mtName=symbols.Single(s=>s.Value==mt&&s.Key.StartsWith("??_7",StringComparison.Ordinal)).Key;
            var body=Regex.Match(dis.Output,@"^[0-9a-f]+ <"+Regex.Escape(caller)+@">:\r?\n(.*?)(?=^[0-9a-f]+ <|\z)",RegexOptions.Multiline|RegexOptions.Singleline);Need(body.Success,"decoded caller "+caller);
            var operand=Regex.Matches(body.Value,@"\blea\s+rcx, \[rip\][^\r\n]*\r?\n\s*[0-9a-f]+:\s+IMAGE_REL_AMD64_REL32\s+(\S+)");
            Need(operand.Count==1 && string.Equals(operand[0].Groups[1].Value,mtName,StringComparison.Ordinal),"caller token relocation equals actual Of<T> identity "+caller);
            var exportCalls=Regex.Matches(body.Value,@"\bcall\s+0x[0-9a-f]+ <RhpNewFast>");
            Need(exportCalls.Count==1 && exportCalls[0].Index>operand[0].Index,"actual token precedes exactly one export call "+caller);
            int offset=0;string constructor="";
            if(i<4)
            {
                byte[] load=At(symbols[prefix+"Read"+types[i]],5);offset=i==0?8:16;
                Need(load.AsSpan(0,3).SequenceEqual(new byte[]{0x48,0x8B,0x41})&&load[3]==offset&&load[4]==0xC3,"independent scalar reader offset");
                var ctors=Regex.Matches(body.Value,@"\bcall\s+0x[0-9a-f]+ <(.+___ctor)>");Need(ctors.Count==1,"one actual constructor after allocation");constructor=ctors[0].Groups[1].Value;
                Need(body.Value.IndexOf("<RhpNewFast>",StringComparison.Ordinal)<ctors[0].Index,"export before constructor");
                Need(symbols.ContainsKey(constructor),"linked constructor identity");
            }
            rows.Add(new{Id=i+1,Getter=getter,GetterRva=rva,Caller=caller,CallerRva=symbols[caller],CallerTokenRelocation=operand[0].Groups[1].Value,MethodTable=mtName,MethodTableRva=mt,RawPrefix=Convert.ToHexString(raw),BaseSize=sizes[i],Flags=flags[i],ScalarOffset=offset,Constructor=constructor});
        }
        File.WriteAllText(Path.Combine(directory,"linked-ten-routes.json"),System.Text.Json.JsonSerializer.Serialize(new{PreferredBase=header.ImageBase,ExportRva=symbols["RhpNewFast"],Rows=rows},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
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
    private static string Scratch(string root,string name){string p=Path.Combine(root,"artifacts/issue519-tests",name,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
    private static void Need(bool x,string reason){if(!x)throw new InvalidOperationException(reason);}
    private readonly record struct Result(int Code,string Output);
}
#endif
