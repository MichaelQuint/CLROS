using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Reflection.Metadata;
using System.Text;
using System.Xml.Linq;
#if CHECKED_REFERENCE_CORE_TEST
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

internal static unsafe class CheckedReferenceCoreProgram
{
    private static int Main()
    {
        Run("six allocations and complete23call POD sequence", Sequence);
        Run("preparation excludes each complete retained prefix", PrefixAliases);
        Run("legacy completed scalar geometry retains default rejection", LegacyRoute);
        Run("per-call patterned full-span rejection atomicity", Rejections);
        Run("Pair scalar completion is separate from two stores", PairCompletion);
        Run("warmed callback core has zero CLR allocations", NoAllocations);
        Run("executing-stack admission and five read-only checkpoints", StackSentinels);
        Console.WriteLine("TEST_PASS reference.core.tests"); return 0;
    }
    private static void Run(string name, Action action)
    {
        Console.WriteLine("TEST_START reference.core " + name);
        try { action(); Console.WriteLine("TEST_PASS reference.core " + name); }
        catch (Exception e) { Console.WriteLine("TEST_FAIL reference.core " + name + " " + e); throw; }
    }
    private static void Need(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static void W32(byte* at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(at, 4), value);
    private static void W64(byte* at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(at, 8), value);
    private static ulong Read(byte* at) => BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(at, 8));
    private const ulong Cookie = 0x179BCDE02468ACE0, PairTag = 0x1928374655AABBCC;
    private static ulong RoleTag(ulong id) => 0x1111222233334400 + id;
    private static ulong BlockTag(ulong id) => 0x3141592653589700 + id;

    private static void Sequence()
    {
        Need(sizeof(CheckedReferenceState)==432 && sizeof(CheckedReferenceGeometry)==56 && sizeof(RhpObservationStorage)==6672,"literal POD sizes");
        using F f = new(); f.Six(); f.Arm();
        Need(f.Bump->UsedBytes == 576 && f.Bump->AllocationCount == 6 && f.Control->ConstructorCount == 6, "independent total576/6");
        f.InitialStores(); f.Scalars();
        f.PairHelpers(); W64((byte*)f.Reference(4) + 40, PairTag);
        Need(f.Pair(Cookie ^ PairTag), "separate Pair cookie and scalar completion");
        byte[] before = f.Snapshot();
        Need(f.Apply(4, f.Reference(6) + 8, f.Reference(5) + 8, 192) == 1, "one real host bulk leaf");
        f.Only(before, (0x6000, sizeof(CheckedReferenceState)), (0x4000 + 8, 4096), (0x8000 + 384, 192));
        Need(RhpNewFastAllocationCore.CompleteReferences(f.Access, f.Bump, f.Control, f.Records, f.State, Cookie ^ BlockTag(7)), "complete23calls");
        for (ulong i = 0; i < 8; i++)
        {
            byte* p = (byte*)f.Reference(6) + 8 + i * 24;
            Need(Read(p) == f.Value(2 + i * 2) && Read(p + 8) == f.Value(3 + i * 2) && Read(p + 16) == BlockTag(i), "independent full Block words");
        }
        Need(Read((byte*)f.Reference(4) + 24) == f.Reference(1) && Read((byte*)f.Reference(4) + 32) == f.Reference(2) &&
            Read((byte*)f.Reference(4) + 40) == PairTag && f.State->Calls == 23 && f.State->Stage == 9, "Pair and final phase");
        Console.WriteLine("REFERENCE_CORE_POD allocations=6 used=576 calls=23 sourcePattern=true numericOnly=true");
    }

    private static void StackSentinels()
    {
        Need(sizeof(CheckedReferenceStackState)==96 && RhpNewFastAllocationCore.ReferenceStackBytes==135168 &&
            RhpNewFastAllocationCore.ReferenceStackBudget==16384,"independent stack POD/extent/ceiling");
        using (F f = new()) using (S s = new(f))
        {
            f.Six(); f.Arm();
            byte[] original=s.Snapshot();
            Need(s.Arm(),"mapped arm");s.OnlyWords(original);
            byte[] protectedState=f.Snapshot(),protectedStack=s.Snapshot();
            Need(!s.Arm(),"one-shot arm");f.Unchanged(protectedState);s.Unchanged(protectedStack);
            Need(!s.Check(),"premature check cannot skip18calls");f.Unchanged(protectedState);s.Unchanged(protectedStack);
            f.InitialStores();Need(s.Check()&&s.State->Checks==1,"18calls");
            Need(!s.Check(),"duplicate checkpoint rejected");
            Need(f.Apply(1,f.Reference(4)+8,f.Reference(1),8)==1&&s.Check(),"19calls");
            Need(f.Apply(2,f.Reference(3)+8,f.Reference(2),8)==1&&s.Check(),"20calls");
            f.PairHelpers();W64((byte*)f.Reference(4)+40,PairTag);
            Need(f.Pair(Cookie^PairTag)&&s.Check(),"22calls after actual Pair completion");
            Need(f.Apply(4,f.Reference(6)+8,f.Reference(5)+8,192)==1 &&
                RhpNewFastAllocationCore.CompleteReferences(f.Access,f.Bump,f.Control,f.Records,f.State,Cookie^BlockTag(7)) && s.Check(),"23calls final check");
            Need(s.State->Phase==2 && s.State->Checks==5 && s.State->Armed0==0x195A86B4C327D0EF &&
                s.State->Armed1==0x62C79E1035AB48D6 && s.State->Final0==s.State->Armed0 && s.State->Final1==s.State->Armed1,"actual readback words");
            Need(!s.Check(),"sixth check rejected");s.OnlyWords(original);
        }
        // Only the new numeric arguments vary. Invalid extents are never dereferenced.
        for(int id=0;id<20;id++)using(F f=new())using(S s=new(f))
        {
            f.Six();f.Arm();ulong authority=s.Authority,start=s.Start,end=s.End,rsp=s.Rsp,witness=s.Witness;
            CheckedReferenceStackState* state=s.State;
            switch(id)
            {
                case 0:authority=0;break;case 1:authority=ulong.MaxValue-7;break;
                case 2:start=0;break;case 3:start++;end++;break;case 4:start=ulong.MaxValue-4095;end=0;break;
                case 5:end-=8;break;case 6:rsp++;break;case 7:witness=end;break;
                case 8:witness=start+16+16384-8;break;case 9:witness++;break;
                case 10:state=(CheckedReferenceStackState*)(start+8);break;
                case 11:state=(CheckedReferenceStackState*)(f.Token(1)+16);break;
                case 12:authority=start;break;case 13:f.State->Busy=1;break;case 14:f.State->Stage=1;break;
                case 15:state=(CheckedReferenceStackState*)f.State;break;
                case 16:state=(CheckedReferenceStackState*)(s.Authority+760);break;
                case 17:start=(ulong)f.Image;end=start+135168;rsp=end-40;witness=end-4096;break;
                case 18:start=(ulong)f.Page;end=start+135168;rsp=end-40;witness=end-4096;break;
                case 19:state=(CheckedReferenceStackState*)(f.Token(5)-88);break;
            }
            byte[] before=f.Snapshot(),stackBefore=s.Snapshot();
            Need(!RhpNewFastAllocationCore.ArmReferenceStack(f.Access,f.Bump,f.Control,f.Records,f.State,state,
                authority,start,end,rsp,witness),"rejected stack admission "+id);f.Unchanged(before);s.Unchanged(stackBefore);
        }
        for(int id=0;id<10;id++)using(F f=new())using(S s=new(f))
        {
            f.Six();f.Arm();Need(s.Arm(),"arm changed-authority fixture");f.InitialStores();
            ulong authority=s.Authority,start=s.Start,end=s.End,rsp=s.Rsp,witness=s.Witness;
            switch(id)
            {
                case 0:authority+=8;break;case 1:start+=4096;end+=4096;rsp+=4096;break;
                case 2:rsp-=8;break;case 3:witness-=8;break;case 4:s.State->Self+=8;break;
                case 5:s.State->Checks=5;break;case 6:s.State->Phase=3;break;
                case 7:s.State->Armed0^=1;break;case 8:f.State->Stage=4;break;case 9:f.State->Busy=1;break;
            }
            byte[] before=f.Snapshot(),stackBefore=s.Snapshot();
            Need(!RhpNewFastAllocationCore.CheckReferenceStack(f.Access,f.Bump,f.Control,f.Records,f.State,s.State,
                authority,start,end,rsp,witness),"retained state/phase check "+id);f.Unchanged(before);s.Unchanged(stackBefore);
        }
        for(int mask=1;mask<=3;mask++)using(F f=new())using(S s=new(f))
        {
            f.Six();f.Arm();Need(s.Arm(),"corruption arm");f.InitialStores();
            if((mask&1)!=0)*(ulong*)s.Start^=1;if((mask&2)!=0)*(ulong*)(s.Start+8)^=1;
            byte[] stackBefore=s.Snapshot();Need(!s.Check()&&s.State->Phase==3,"corruption terminal state");
            Need(!s.Check()&&!s.Arm(),"no retry or reseed after corruption");s.Unchanged(stackBefore);
        }
        using(F f=new())using(S s=new(f))
        {
            f.Six();f.Arm();s.Witness=s.Start+16+16384;
            Need(s.Arm(),"exact end-exclusive budget boundary admitted");
        }
        Console.WriteLine("REFERENCE_STACK_POD stateBytes=96 usableBytes=135168 ceiling=16384 words=2 checks=5 numericOnly=true");
    }
    private sealed class S : IDisposable
    {
        private const int Bytes=147456;
        private readonly F f;
        private readonly byte* storage=(byte*)NativeMemory.AlignedAlloc(Bytes,4096);
        internal CheckedReferenceStackState* State=>(CheckedReferenceStackState*)(f.Storage+0x7000);
        internal ulong Authority=>(ulong)f.Storage+0x6800;
        internal ulong Start=>(ulong)storage+4096;
        internal ulong End=>Start+135168;
        internal ulong Rsp=>End-40;
        internal ulong Witness;
        internal S(F fixture)
        {f=fixture;Need(storage!=null,"mapped fake stack");new Span<byte>(storage,Bytes).Fill(0xD7);new Span<byte>(State,96).Clear();Witness=End-4096;}
        internal bool Arm()=>RhpNewFastAllocationCore.ArmReferenceStack(f.Access,f.Bump,f.Control,f.Records,f.State,State,Authority,Start,End,Rsp,Witness);
        internal bool Check()=>RhpNewFastAllocationCore.CheckReferenceStack(f.Access,f.Bump,f.Control,f.Records,f.State,State,Authority,Start,End,Rsp,Witness);
        internal byte[] Snapshot()=>new ReadOnlySpan<byte>(storage,Bytes).ToArray();
        internal void Unchanged(byte[] bytes)=>Need(new ReadOnlySpan<byte>(storage,Bytes).SequenceEqual(bytes),"mapped stack unchanged");
        internal void OnlyWords(byte[] bytes)
        {for(int i=0;i<Bytes;i++)if(i<4096||i>=4112)Need(storage[i]==bytes[i],"only admitted16 bytes written");}
        public void Dispose()=>NativeMemory.AlignedFree(storage);
    }

    private static void PrefixAliases()
    {
        // All pointers refer to this fixture's mapped readable/writable allocation.
        // Zero trailing prefix bytes make the original late check insufficient.
        for (ulong role = 1; role <= 6; role++)
        {
            using F f = new(); f.Six();
            ulong token = f.Token(role);
            Need(Read((byte*)token + 16) == 0, "trailing prefix zero");
            foreach (long relative in new long[] { -(sizeof(CheckedReferenceState) - 8), -8, 0, 8, 16 })
            {
                byte[] before = f.Snapshot();
                var alias = (CheckedReferenceState*)(token + (ulong)relative);
                Need(!f.PrepareState(alias), "state overlaps exact prefix role=" + role + " offset=" + relative); f.Unchanged(before);
            }
        }
        using F good = new(); good.Six();
        // A distinct mapped image area is allowed; this is not a whole-image exclusion.
        var imageState = (CheckedReferenceState*)(good.Image + 0x3000);
        Need(good.PrepareState(imageState), "non-overlapping zero static-like image storage admitted");
    }

    private static void LegacyRoute()
    {
        using F f = new(); f.Start();
        var result = RhpNewFastAllocationCore.Allocate(f.Access, f.Bump, f.Control, f.Records, f.Token(1), f.Token(1), 1, 1);
        Need(result.Succeeded, "old fixed route"); W64((byte*)result.Leaf.ObjectReference + 8, 0x123456789ABCDEF0);
        Need(RhpNewFastAllocationCore.Complete(f.Access, f.Bump, f.Control, f.Records, 1, result.Leaf.ObjectReference, 8,
            0x123456789ABCDEF0, 0x123456789ABCDEF0) == RhpAdmissionStatus.Success, "old complete");
        f.Records->Object0.Route = 7; f.Records->Object0.ScalarWidth = 0; f.Records->Object0.ScalarOffset = 0; f.Records->Object0.Scalar = 0;
        W64((byte*)result.Leaf.ObjectReference + 8, 0);
        byte[] before = f.Snapshot();
        Need(RhpNewFastAllocationCore.CheckCompleted(f.Access, f.Bump, f.Control, f.Records) == RhpAdmissionStatus.PriorObjectChanged, "route number cannot waive old scalar predicate"); f.Unchanged(before);
        using F reference = new(); reference.Six(); reference.Records->Object0.Route = 1;
        before = reference.Snapshot(); Need(!reference.PrepareState(reference.State), "reference mode requires every route7"); reference.Unchanged(before);
    }

    private static void Rejections()
    {
        for (int variant = 0; variant < 20; variant++)
        {
            using F f = new(); f.Six(); f.Arm();
            ulong operation = 2, destination = f.Reference(3) + 24, operand = f.Reference(1), bytes = 8;
            switch (variant)
            {
                case 0: operation = 1; break;
                case 1: operation = 0; break;
                case 2: operation = ulong.MaxValue; break;
                case 3: destination++; break;
                case 4: destination = (ulong)f.Page + 4096; break;
                case 5: destination = ulong.MaxValue - 7; break;
                case 6: operand = f.Reference(2); break; // Valid retained value, wrong exact next role.
                case 7: operand = f.Reference(3); break; // Owned object, not an admitted value.
                case 8: operand = 0; break; // Null is not expected at initial index0.
                case 9: bytes = 0; break;
                case 10: bytes = 16; break;
                case 11: bytes = ulong.MaxValue; break;
                case 12: f.State->Busy = 1; break;
                case 13: f.State->Armed = 0; break;
                case 14: f.State->Stage = 8; break;
                case 15: f.State->FrozenAccess.NodeCapacity++; break;
                case 16: f.State->CanaryAfter ^= 1; break;
                case 17: f.Page[4095] ^= 1; break;
                case 18: f.Page[8] ^= 1; break;
                case 19: W64((byte*)f.Token(3) + 16, 1); break;
            }
            byte[] before = f.Snapshot(); Need(f.Apply(operation, destination, operand, bytes) == 0, "rejection " + variant); f.Unchanged(before);
        }
        using F pair = new(); pair.Six(); pair.Arm(); pair.InitialStores(); pair.Scalars();
        foreach (var args in new (ulong D, ulong S, ulong N)[] {
            (ulong.MaxValue-7,pair.Reference(3)+24,8), (pair.Reference(4)+24,ulong.MaxValue-7,8),
            (pair.Reference(4)+24,pair.Reference(3)+24,ulong.MaxValue), (pair.Reference(4)+32,pair.Reference(3)+24,8) })
        { byte[] before = pair.Snapshot(); Need(pair.Apply(3,args.D,args.S,args.N)==0,"byref numeric overflow/wrong next window"); pair.Unchanged(before); }
    }

    private static void PairCompletion()
    {
        using F f = new(); f.Six(); f.Arm(); f.InitialStores(); f.Scalars(); f.PairHelpers();
        byte[] before = f.Snapshot(); Need(!f.Pair(Cookie ^ PairTag), "no early completion before scalar copy"); f.Unchanged(before);
        W64((byte*)f.Reference(4) + 40, PairTag);
        before = f.Snapshot(); Need(!f.Pair(Cookie), "wrong return cookie"); f.Unchanged(before);
        f.Page[4095] ^= 1; before = f.Snapshot(); Need(!f.Pair(Cookie ^ PairTag), "unrelated byte cannot be absorbed"); f.Unchanged(before); f.Page[4095] ^= 1;
        Need(f.Pair(Cookie ^ PairTag), "precise scalar-only interval accepted");
        before = f.Snapshot(); Need(!f.Pair(Cookie ^ PairTag), "cannot repeat completion"); f.Unchanged(before);
    }

    private static void NoAllocations()
    {
        for (int pass = 0; pass < 2; pass++)
        {
            using F f = new(); f.Six(); f.Arm();
            ulong destination = f.Reference(3) + 24, value = f.Reference(1);
            long before = GC.GetAllocatedBytesForCurrentThread();
            uint result = f.Apply(2, destination, value, 8);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Need(result == 1 && after == before, "warmed POD core no managed allocation");
        }
    }

    private sealed class F : IDisposable
    {
        internal readonly byte* Storage = (byte*)NativeMemory.AlignedAlloc(98304, 4096);
        internal RhpNewFastHeapAccess Original;
        internal RhpNewFastHeapAccess* Access => (RhpNewFastHeapAccess*)(Storage + 0x3000);
        internal RhpNewFastBumpState* Bump => (RhpNewFastBumpState*)(Storage + 0x3100);
        internal RhpAllocationControl* Control => (RhpAllocationControl*)(Storage + 0x3200);
        internal RhpObservationStorage* Records => (RhpObservationStorage*)(Storage + 0x4000);
        internal CheckedReferenceState* State => (CheckedReferenceState*)(Storage + 0x6000);
        internal byte* Page => Storage + 0x8000;
        internal byte* Image => Storage + 0xC000;
        internal static CheckedReferenceGeometry Geometry => new() { CellReference=8,CellTag=16,CellPayload=24,PairFirst=0,PairLast=8,PairTag=16,BlockPayload=8 };
        internal F()
        {
            Need(Storage != null, "mapped fixture allocation"); new Span<byte>(Storage,98304).Fill(0xA5);
            Original = new() { Allocator=(BootPageAllocatorState*)(Storage+0x100),AllocatorCapacity=1,
                Nodes=(BootPageFreeListNode*)(Storage+0x200),NodeCount=1,NodeCapacity=1,
                Reservations=(BootReservedRange*)(Storage+0x1000),ReservationCount=5,ReservationCapacity=5,ParentIndex=4,
                Heap=(ManagedHeapPageRangeState*)(Storage+0x2000),HeapCapacity=216,Backing=Page,BackingCapacity=8192 };
            *Original.Allocator = new BootPageAllocatorState(true,4096,0,1,2,8192,0,0);
            Original.Nodes[0] = new BootPageFreeListNode(0,0,0x400000,0x402000,2,8192,-1);
            for(int i=0;i<5;i++)
            {
                OS.Boot.PhysicalAddress.TryCreateFromUefiBoundary(i==4?0x400000UL:0x100000UL+(ulong)i*0x10000,out var physical);
                Need(BootReservedRange.TryCreate(i==4?BootReservationKind.AllocatorArena:BootReservationKind.LoadedImage,
                    physical,i==4?2UL:1UL,out Original.Reservations[i])==BootReservedRangeStatus.Success,"original reservation fixture");
            }
            new Span<byte>(Original.Heap,216).Clear(); new Span<byte>(Access,104).Clear(); new Span<byte>(Bump,80).Clear();
            new Span<byte>(Control,96).Clear(); new Span<byte>(Records,6672).Clear(); new Span<byte>(State,sizeof(CheckedReferenceState)).Clear(); new Span<byte>(Image,16384).Clear();
            Need(ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(Original.Allocator,1,Original.Nodes,1,1,
                Original.Reservations,5,5,4,Page,8192,Original.Heap,216).IsReady,"one original reserve");
            W32(Image,0x5A4D);W32(Image+60,0x80);W32(Image+0x80,0x4550);W32(Image+0x84,0x00018664);
            W32(Image+0x94,0xF0);W32(Image+0x98,0x20B);W32(Image+0xD0,0x4000);W32(Image+0xD4,0x400);
            W32(Image+0x190,0x2000);W32(Image+0x194,0x1000);W32(Image+0x198,0x2000);W32(Image+0x1AC,0x40000040);
            W32((byte*)Token(1),0x50000000);W32((byte*)Token(1)+4,24);
            W32((byte*)Token(3),0x51000000);W32((byte*)Token(3)+4,56);
            W32((byte*)Token(5),0x51000000);W32((byte*)Token(5)+4,208);
        }
        internal ulong Token(ulong id) => (ulong)Image + (id<=2?0x1100UL:id<=4?0x1800UL:0x1F00UL);
        internal ulong Reference(ulong id) => (&Records->Object0)[id-1].Reference;
        internal ulong Value(ulong index) => index%3==2?0:index%3==0?Reference(1):Reference(2);
        internal void Start()
        {
            Need(RhpNewFastAllocationCore.Prepare(Original,Access,Bump,Control,Records,Image,16384)==RhpAdmissionStatus.Success,"prepare");
            Need(RhpNewFastAllocationCore.Bind(Access,Bump,Control,Records).Succeeded,"bind");
        }
        internal void Six()
        {
            Start(); ulong used=0;
            for(ulong id=1;id<=6;id++)
            {
                ulong size=id<=2?24UL:id<=4?56UL:208UL;
                new Span<byte>(Page+used,(int)size).Fill(0xBC);
                var result=RhpNewFastAllocationCore.AllocateReferences(Access,Bump,Control,Records,Token(id),Token(id),id);
                Need(result.Succeeded && result.Leaf.UsedAfter==used+size && result.Leaf.AllocationCount==id,"allocation result");
                byte[] expected=new byte[size];BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8),Token(id));
                Need(new ReadOnlySpan<byte>(Page+used,(int)size).SequenceEqual(expected),"independent entire allocation bytes");
                ulong reference=result.Leaf.ObjectReference,scalar=id<=4?RoleTag(id):0,address=id<=2?reference+8:id<=4?reference+16:0;
                if(id<=4)W64((byte*)address,scalar);Control->ConstructorCount++;
                Need(RhpNewFastAllocationCore.CompleteReferenceAllocation(Access,Bump,Control,Records,id,reference,address,scalar)==RhpAdmissionStatus.Success,"completion"); used+=size;
            }
            // A patterned unused tail makes every subsequent whole-page oracle meaningful.
            new Span<byte>(Page+576,4096-576).Fill(0xA7);
        }
        internal bool PrepareState(CheckedReferenceState* state) => RhpNewFastAllocationCore.PrepareReferences(Access,Bump,Control,Records,state,Geometry);
        internal void Arm()
        {
            Need(PrepareState(State),"prepare six roles"); W64((byte*)Reference(3)+40,PairTag);
            for(ulong i=0;i<8;i++)W64((byte*)Reference(5)+24+i*24,BlockTag(i));
            Need(RhpNewFastAllocationCore.CompleteReferenceScalars(Access,Bump,Control,Records,State),"exact scalar setup");
        }
        internal uint Apply(ulong operation,ulong destination,ulong operand,ulong bytes) => RhpNewFastAllocationCore.ApplyReference(Access,Bump,Control,Records,State,operation,destination,operand,bytes);
        internal void InitialStores()
        {
            for(ulong i=0;i<18;i++)
            {
                ulong destination=i<2?Reference(3)+24+i*8:Reference(5)+8+(i-2)/2*24+(i%2)*8;
                byte[] before=Snapshot();Need(Apply(2,destination,Value(i),8)==1,"initial exact slot");
                Only(before,(0x6000,sizeof(CheckedReferenceState)),(0x4000+8,4096),(0x8000+(int)(destination-(ulong)Page),8));
                Need(Read((byte*)destination)==Value(i) && State->Calls==i+1,"scalar literal readback");
            }
        }
        internal void Scalars()
        {
            Need(Apply(1,Reference(4)+8,Reference(1),8)==1,"field store");
            Need(Apply(2,Reference(3)+8,Reference(2),8)==1,"checked store");
        }
        internal void PairHelpers()
        { Need(Apply(3,Reference(4)+24,Reference(3)+24,8)==1,"Pair first helper");Need(Apply(3,Reference(4)+32,Reference(3)+32,8)==1,"Pair second helper"); }
        internal bool Pair(ulong cookie) => RhpNewFastAllocationCore.CompleteReferencePair(Access,Bump,Control,Records,State,cookie);
        internal byte[] Snapshot()=>new ReadOnlySpan<byte>(Storage,98304).ToArray();
        internal void Unchanged(byte[] before)=>Need(new ReadOnlySpan<byte>(Storage,98304).SequenceEqual(before),"all98304 mapped bytes unchanged");
        internal void Only(byte[] before,params(int At,int Count)[] allowed)
        { for(int i=0;i<before.Length;i++)if(Storage[i]!=before[i] && !allowed.Any(x=>i>=x.At && i-x.At<x.Count))throw new InvalidOperationException("unexpected byte "+i); }
        public void Dispose()=>NativeMemory.AlignedFree(Storage);
    }
}
#else
internal static class M04CheckedReferenceBarrierEfiIntegrationTests
{

    private static void Need(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    [HostTest("m04.010.efi source authority and retained host contracts", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root = HostToolProcess.FindRepositoryRoot(); var p = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        const string flag = "EnableM04CheckedReferenceBarrierProbe";
        Need(p.Descendants(flag).Single().Value == "false", "default false");
        Need(p.Descendants("PropertyGroup").Single(x => x.Attribute("Condition")?.Value == "'$(" + flag + ")' == 'true'")
            .Element("DefineConstants")?.Value == "$(DefineConstants);M04_CHECKED_REFERENCE_BARRIER_PROBE", "literal symbol");
        string leaf = Path.Combine(root, "src/OS.Kernel/Runtime/CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.cs");
        Need(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(leaf))) ==
            "8F40B7416297C73EE252D25573EF84755C48B27A7C3E60F498C708D6C3C68021", "original host leaf unchanged");
        string entry = File.ReadAllText(Path.Combine(root, "src/OS/Boot/EfiEntry.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string getter = Between(entry, "        internal static bool TryGetOriginalReferenceStack(", "        internal static bool TryGetFinalSwitchPlan(");
        foreach (string token in new[]{"fixed (M01LoadedImageStackStaticState* state = &s_staticState)",
            "IsFinalStageOneSuccess(state, M01LoadedImageStackStaticState.RequiredStorageBytes)",
            "record.StorageAddress != (ulong)&state->SwitchStateStorage","!record.ObservedRsp.Equals(plan.ExpectedContinuationStackPointer)",
            "!plan.StackLayout.BackingStart.Equals(state->StackLayoutStorage.BackingStart)","high - low != UsableStackPageCount * PageSize"})
            Need(getter.Contains(token, StringComparison.Ordinal), "original active stack source " + token);
        Need(entry.Contains("#if M04_CHECKED_REFERENCE_BARRIER_PROBE || M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE || M04_X64_UNWIND_METADATA_PROBE\n        internal static bool TryGetOriginalReferenceStack(", StringComparison.Ordinal), "getter limited to the three reviewed stack-observing profiles");
        string adapter = File.ReadAllText(Path.Combine(root, "src/OS/Runtime/CheckedReferenceBarrierAdapter.cs"));
        Need(adapter.Contains("witness - (start + 16) < ReferenceStackBudget", StringComparison.Ordinal) &&
            adapter.Contains("ReferenceStackBudget = 16384", StringComparison.Ordinal), "end-exclusive strip clearance remains exact16KiB");
    }
    [HostTest("m04.010.efi actual POD core roles and rejection atomicity", HostLane.Native, 1)]
    private static void TestCore()
    {
        string root = HostToolProcess.FindRepositoryRoot(); string path = Path.Combine(root, "artifacts/issue537-tests/core", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path);
        string includes = string.Join('\n', new[] { "src/OS/Runtime/RhpNewFastAllocation.cs", "src/OS/Runtime/CheckedReferenceBarrierAdapter.cs", "tests/OS.HostTests/M04CheckedReferenceBarrierEfiIntegrationTests.cs" }
            .Select(x => "<Compile Include=\"" + SecurityElement.Escape(Path.Combine(root, x)) + "\"/>"));
        string project = Path.Combine(path, "Core.csproj");
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion>
            <OutputType>Exe</OutputType><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>CHECKED_REFERENCE_CORE_TEST</DefineConstants><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup><ItemGroup>
            """ + includes + "<ProjectReference Include=\"" + SecurityElement.Escape(Path.Combine(root, "src/OS.Kernel/OS.Kernel.csproj")) + "\"/></ItemGroup></Project>", new UTF8Encoding(false));
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string arg in new[] { "run", "--project", project, "-c", "Release", "--no-launch-profile", "--disable-build-servers", "-p:UseSharedCompilation=false" }) start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"; start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        File.WriteAllText(Path.Combine(path, "command.json"), System.Text.Json.JsonSerializer.Serialize(new { File = "dotnet", Arguments = start.ArgumentList.ToArray() }));
        using Process process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        bool timedOut = !process.WaitForExit(180000);
        if (timedOut) { process.Kill(true); process.WaitForExit(); }
        Task.WaitAll(stdout, stderr); File.WriteAllText(Path.Combine(path, "stdout.log"), stdout.Result); File.WriteAllText(Path.Combine(path, "stderr.log"), stderr.Result); File.WriteAllText(Path.Combine(path, "exit.txt"), process.ExitCode.ToString());
        File.WriteAllText(Path.Combine(path, "result.json"), System.Text.Json.JsonSerializer.Serialize(new { TimedOut = timedOut, ExitCode = process.ExitCode }));
        if (timedOut) throw new TimeoutException("complete reference core; evidence=" + path);
        Need(process.ExitCode == 0 && stderr.Result.Length == 0 && stdout.Result.Split('\n').Count(x => x.TrimEnd('\r') == "TEST_PASS reference.core.tests") == 1, stdout.Result + stderr.Result);
    }
    private const string Flag = "EnableM04CheckedReferenceBarrierProbe";
    private static readonly string[] Chain = ["EnableM01LoadedImageStackProbe","EnableM01LiveAllocatorProbe","EnableM04IndependentEndianConformanceProbe",
        "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","EnableM04ArithmeticConformanceProbe","EnableM04LayoutContractProbe","EnableM04ManagedHeapPageRangeProbe","EnableM04RhpNewFastProbe"];
    private static string Scratch(string name)
    { string p = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/issue537-tests", name, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
    private readonly record struct Result(int Code, string Output, string Error, bool TimedOut);
    private static Result Execute(string executable, string[] arguments, string directory, int timeout = 900000)
    {
        Directory.CreateDirectory(directory); string root = HostToolProcess.FindRepositoryRoot();
        ProcessStartInfo start = new(executable) { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string arg in arguments) start.ArgumentList.Add(arg);
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"; start.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        var begun = DateTime.UtcNow; File.WriteAllText(Path.Combine(directory, "command.json"), System.Text.Json.JsonSerializer.Serialize(new { Executable = executable, Arguments = arguments, WorkingDirectory = root, StartedUtc = begun }));
        using Process p = Process.Start(start)!; var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
        bool timedOut = !p.WaitForExit(timeout); if (timedOut) { p.Kill(true); p.WaitForExit(); }
        Task.WaitAll(stdout, stderr);
        File.WriteAllText(Path.Combine(directory, "stdout.log"), stdout.Result); File.WriteAllText(Path.Combine(directory, "stderr.log"), stderr.Result);
        File.WriteAllText(Path.Combine(directory, "result.json"), System.Text.Json.JsonSerializer.Serialize(new { ExitCode = p.ExitCode, TimedOut = timedOut, StartedUtc = begun, EndedUtc = DateTime.UtcNow }));
        return new(p.ExitCode, stdout.Result, stderr.Result, timedOut);
    }
    private static void Passed(Result r) => Need(!r.TimedOut && r.Code == 0 && r.Error.Length == 0, r.Output + r.Error);
    private static string[] ToolArguments(string command, params string[] args) => [.. command.Split(' '), .. args];
    private static Result Command(string[] args, string directory)
    {
        Directory.CreateDirectory(directory);
        var result = HostToolProcess.RunTool(args);
        File.WriteAllText(Path.Combine(directory, "stdout.log"), result.Output);
        File.WriteAllText(Path.Combine(directory, "stderr.log"), result.Error);
        return new(result.ExitCode, result.Output, result.Error, false);
    }
    private static string Between(string source, string first, string last)
    { int a = source.IndexOf(first, StringComparison.Ordinal); Need(a >= 0, first); int b = source.IndexOf(last, a, StringComparison.Ordinal); Need(b > a, last); return source[a..b]; }
    private static string Hash(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    [HostTest("m04.010.efi compile-time profile prerequisites and conflicts", HostLane.Fast, 5)]
    private static void TestProfileSource() => _ = ProfileConflicts();

    private static string[] ProfileConflicts()
    {
        string root = HostToolProcess.FindRepositoryRoot(); var project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        string[] flags = project.Descendants("Error").SelectMany(x => System.Text.RegularExpressions.Regex.Matches(x.Attribute("Condition")?.Value ?? "", @"\$\((Enable[^)]+)\)").Select(m => m.Groups[1].Value)).Distinct(StringComparer.Ordinal).ToArray();
        Need(flags.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count && Chain.Length == 8, "complete profile inventory/eight prerequisites");
        string[] conflicts = flags.Except(Chain.Append(Flag), StringComparer.Ordinal).ToArray(); Need(conflicts.Length == flags.Length - Chain.Length - 1, "complete unrelated profile inventory");
        var conditions = project.Descendants("Error").Select(x => x.Attribute("Condition")!.Value).ToHashSet(StringComparer.Ordinal);
        Need(conditions.Contains("'$(EnableM04SeededCollectorChurnProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"), "churn inherits GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"), "constructors inherit GC prerequisites and conflicts");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04InterfaceDispatchProbe)' == 'true' and '$(EnableM04ClassConstructorExecutionProbe)' != 'true'") == 1, "dispatch inherits constructor and GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM03FirmwareAdapterRevocationProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'"), "revocation inherits first-exit prerequisites and conflicts");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04X64UnwindMetadataProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' != 'true'") == 1, "unwind inherits original TypeManager prerequisites and conflicts");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM03OwnedSubsystemSurvivalProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'") == 1, "exact owned inherited prerequisite EnableM03OwnedSubsystemSurvivalProbe");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM03OwnedSubsystemPanicProbe)' == 'true' and '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true'") == 1, "exact owned inherited prerequisite EnableM03OwnedSubsystemPanicProbe");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04SimpleThrowCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "simple catch inherits exact GC prerequisites and conflicts");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04SingleFinallyCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "single finally inherits exact GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM04RethrowProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"), "rethrow inherits GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM05PhysicalMappingProbe)' == 'true' and ('$(ClrosBootPolicy)' != 'Diagnostic' or '$(EnableM03FirstRealExitBootServicesProbe)' != 'true' or '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true')"), "mapping inherits owned first-exit prerequisites");
        Need(conditions.Count(x=>x=="'$(EnableM04NestedFinallyCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'")==1,"nested finally inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x=>x=="'$(EnableM04UnhandledExceptionProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'")==1,"unhandled exception inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x=>x=="'$(EnableM04FrameDispatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'")==1,"frame dispatch inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x=>x=="'$(EnableM04ConstructorFailureProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'")==1,"constructor failure inherits exact GC prerequisites and conflicts");
        foreach (string conflict in conflicts.Where(x => !OS.Tooling.EfiContracts.FirstExitDependentProbes.Contains(x) && x != "EnableM04X64UnwindMetadataProbe" && x != "EnableM04SeededCollectorChurnProbe" && x != "EnableM04ClassConstructorExecutionProbe" && x != "EnableM04InterfaceDispatchProbe" && x != "EnableM04SimpleThrowCatchProbe" && x != "EnableM04SingleFinallyCatchProbe" && x != "EnableM04RethrowProbe"&& x != "EnableM04NestedFinallyCatchProbe"&& x != "EnableM04UnhandledExceptionProbe" && x != "EnableM04FrameDispatchProbe" && x != "EnableM04ConstructorFailureProbe")) Need((conflict == "EnableM03FirstRealExitBootServicesProbe" || conditions.Contains("'$(" + Flag + ")' == 'true' and '$(" + conflict + ")' == 'true'")) && conditions.Contains("'$(" + conflict + ")' == 'true' and '$(" + Flag + ")' == 'true'"), "both conflict directions " + conflict);
        foreach (string required in Chain) Need(conditions.Contains("'$(" + Flag + ")' == 'true' and '$(" + required + ")' != 'true'"), "prerequisite " + required);
        return conflicts;
    }

    [HostTest("m04.010.efi full profile prerequisites conflicts and default isolation", HostLane.Native, 3)]
    private static void TestProfiles()
    {
        string directory = Scratch("profiles"); string[] conflicts = ProfileConflicts();
        int id = 0; string[] enabled = [.. Chain, Flag];
        foreach (string[] invalid in Chain.Select(m => enabled.Where(x => x != m).ToArray()).Concat(conflicts.Select(c => enabled.Append(c).ToArray())))
            foreach (string script in new[] { "efi build", "qemu smoke", "efi verify" })
            {
                string[] mode = script.EndsWith("qemu smoke", StringComparison.Ordinal) ? ["-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi"] : script.EndsWith("efi build", StringComparison.Ordinal) ? ["-PreflightOnly"] : ["-EfiArtifact", "missing.EFI"];
                Result r = Command(ToolArguments(script, [.. mode, .. invalid.Select(x => "-" + x)]), Path.Combine(directory, (id++).ToString()));
                Need(!r.TimedOut && r.Code != 0 && (EfiProfileTestControls.MissingParentReason(invalid) is string parentReason ? r.Output.Contains(parentReason, StringComparison.Ordinal) : invalid.Contains("EnableM04GcStaticRootMaterializationProbe", StringComparer.Ordinal) ? r.Output.Contains("m04_gc_static_profile_conflict property=EnableM04CheckedReferenceBarrierProbe", StringComparison.Ordinal) : invalid.Contains("EnableM03FirstRealExitBootServicesProbe", StringComparer.Ordinal) ? r.Output.Contains("m03_first_exit_requires property=EnableAcpiPhysicalDiscoveryProbe", StringComparison.Ordinal) : invalid.Contains("EnableM03TypedExitBootServicesCallProbe", StringComparer.Ordinal) ? r.Output.Contains("m03_typed_ebs_profile_conflict property=EnableM01LoadedImageStackProbe", StringComparison.Ordinal) : invalid.Contains("EnableM04TypeManagerModuleRegistrationProbe", StringComparer.Ordinal) ? r.Output.Contains("m04_type_manager_profile_conflict property=EnableM01LiveAllocatorProbe", StringComparison.Ordinal) : invalid.Contains("EnableM03FirmwareEntropySeedProbe", StringComparer.Ordinal) ? r.Output.Contains("m03_firmware_entropy_profile_conflict property=EnableM01LoadedImageStackProbe", StringComparison.Ordinal) : r.Output.Contains("m04_checked_reference_profile_", StringComparison.Ordinal)), "first checked reference guard " + r.Output + r.Error);
            }
        foreach (string[] wrong in new[] { new[] { "-BootPolicy", "Diagnostic", "-DryRun" }, new[] { "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", "-SkipImageBuild" }, new[] { "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", "-M01RawObservationLog", "forbidden.log" } })
        { Result r = Command(ToolArguments("qemu smoke", [.. wrong, .. enabled.Select(x => "-" + x)]), Path.Combine(directory, (id++).ToString())); Need(!r.TimedOut && r.Code != 0 && r.Output.Contains("m04_checked_reference_requires_build_and_original_capture", StringComparison.Ordinal), r.Output); }
        foreach (string[] selected in new[] { Array.Empty<string>(), enabled })
        { Result r = Command(ToolArguments("qemu smoke", ["-BootPolicy", "Diagnostic", "-DryRun", .. (selected.Length == 0 ? Array.Empty<string>() : new[] { "-BuildEfi" }), .. selected.Select(x => "-" + x)]), Path.Combine(directory, (id++).ToString())); Passed(r); Need(r.Output.Contains("CLROS_M04_010_NATIVE_READY", StringComparison.Ordinal) == (selected.Length != 0), "default profile remains absent"); }
    }
    [HostTest("m04.010.efi whole reference transcript and finite rejected mutations", HostLane.Fast, 2)]
    private static void TestTranscript()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch("transcript");
        Result dry = Command(ToolArguments("qemu smoke", ["-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", .. Chain.Append(Flag).Select(x => "-" + x)]), Path.Combine(directory, "dryrun")); Passed(dry);
        string[] Vector(string name) => dry.Output.Replace("\r", "").Split('\n').Single(x => x.StartsWith(name + " ", StringComparison.Ordinal))[(name.Length + 1)..].Split(';');
        string[] required = Vector("REQUIRED_MARKERS"), ordered = Vector("ORDERED_MARKERS"), unique = Vector("UNIQUE_MARKERS"), forbidden = Vector("FORBIDDEN_MARKERS");
        List<string> lines = ["TEST_START efi.main", .. ordered.Take(6), "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 3", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY", "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT 3", "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=3 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY", .. ordered.Skip(6)];
        const string identity = "allocatorState=0x0000000000100000 nodes=0x0000000000100040 heapState=0x0000000000101000 ledger=0x0000000000102000 parentIndex=4 parentStart=0x0000000000900000 parentEnd=0x0000000000902000 backing=0x0000000000900000 retainedStart=0x0000000000900000 retainedEnd=0x0000000000901000";
        lines[lines.IndexOf("M04_HEAP_PAGE_BOUND")] += " " + identity; lines[lines.IndexOf("M04_HEAP_PAGE_RETAINED")] += " checkpoint=beforeBootPlan " + identity;
        lines[lines.IndexOf("M04_HEAP_PAGE_INITIALIZED")] += " pages=1 bytes=4096 freePagesBefore=2 freeBytesBefore=8192 allocatedPagesBefore=0 allocatedBytesBefore=0 freePagesAfter=1 freeBytesAfter=4096 allocatedPagesAfter=1 allocatedBytesAfter=4096 readBytes=4096 contentsZero=true";
        lines[lines.IndexOf("M04_HEAP_PAGE_CURRENT")] += " freePages=1 freeBytes=4096 allocatedPages=1 allocatedBytes=4096 readBytes=4096 contentsZero=true";
        Need(lines.Count == 71 && lines[44] == "TEST_START m04.010.references" && lines[63] == "TEST_START boot.sequence", "literal71 positions inherited44 plus19 and8");
        lines[45] = "M04_REFERENCE_PREP image=0x0000000001000000 imageBytes=65536 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 control=0x0000000000103200 controlBytes=96 records=0x0000000000104000 recordsBytes=6672";
        lines[46] = "M04_REFERENCE_BIND image=0x0000000001000000 imageBytes=65536 allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 ledgerBytes=200 parentIndex=4 backing=0x0000000000900000 backingBytes=8192 heap=0x0000000000101000 heapBytes=216 access=0x0000000000103000 accessBytes=104 bump=0x0000000000103100 bumpBytes=80 status=1 used=0 count=0";
        static string H(ulong x) => "0x" + x.ToString("X16"); ulong used = 0;
        for (int id = 1; id <= 6; id++)
        {
            ulong size = id <= 2 ? 24UL : id <= 4 ? 56UL : 208UL, flags = id <= 2 ? 0x50000000UL : 0x51000000UL, mt = 0x1001100UL + (ulong)((id - 1) / 2) * 32, allocation = 0x900000 + used, offset = id <= 2 ? 8UL : id <= 4 ? 16UL : 0, scalar = id <= 4 ? 0x1111222233334400UL + (ulong)id : 0;
            lines[46 + id] = $"M04_REFERENCE_OBJECT id={id} mt={H(mt)} prefix0={H(size << 32 | flags)} prefix1={H(0)} prefix2={H(0x1234567800020001)} flags={H(flags)} base={size} allocation={H(allocation)} reference={H(allocation + 8)} end={H(allocation + size)} usedBefore={used} usedAfter={used + size} count={id} sync={H(0)} observedMt={H(mt)} zeroBytes={size - 8} zeroOr={H(0)} scalarOffset={offset} scalar={H(scalar)}"; used += size;
        }
        Need(used == 576, "independent literal host geometry total");
        lines[53] = "M04_REFERENCE_GEOMETRY state=0x0000000000106000 stateBytes=432 cellReference=8 cellTag=16 cellPayload=24 pairFirst=0 pairLast=8 pairTag=16 blockPayload=8";
        ulong cookie = 0x179BCDE02468ACE0; var operations = new (ulong Kind, ulong Calls, ulong Cookie)[] { (2, 18, cookie), (1, 19, cookie ^ 0x1111222233334404UL), (2, 20, cookie), (3, 22, cookie ^ 0x1928374655AABBCCUL), (4, 23, cookie ^ 0x3141592653589707UL) };
        for (int i = 0; i < 5; i++) lines[54 + i] = $"M04_REFERENCE_OPERATION kind={operations[i].Kind} calls={operations[i].Calls} cookie={H(operations[i].Cookie)}";
        lines[59] = "M04_REFERENCE_RETAIN allocator=0x0000000000100000 nodes=0x0000000000100040 ledger=0x0000000000102000 parentIndex=4 backing=0x0000000000900000 heap=0x0000000000101000 bump=0x0000000000103100 heapStart=0x0000000000900000 heapEnd=0x0000000000901000 initialPages=2 freePages=1 allocatedPages=1 readBytes=4096 contentsZero=false used=576 count=6 status=1";
        lines[60] = "M04_REFERENCE_STACK start=0x0000000002000000 end=0x0000000002021000 initialRsp=0x0000000002020FD8 witness=0x0000000002020000 sentinel0=0x0000000002000000 sentinel1=0x0000000002000008 expected0=0x195A86B4C327D0EF expected1=0x62C79E1035AB48D6 armed0=0x195A86B4C327D0EF armed1=0x62C79E1035AB48D6 final0=0x195A86B4C327D0EF final1=0x62C79E1035AB48D6 checks=5 words=2 preserved=true";
        lines[61] = "CLROS_M04_010_NATIVE_READY allocations=6 usedBytes=576 calls=23 initialStores=18 fieldStores=1 checkedStores=1 pairHelpers=2 bulkCalls=1 pairComplete=true blockComplete=true exclusiveNonCollecting=true gate4=Fail";
        string good = string.Join("\r\n", lines) + "\r\n";
        // Feed the actual dry-run vectors to the production streamer, separately from the numeric raw parser below.
        const string ready = "CLROS_M04_010_NATIVE_READY allocations=6", oldPartial = ready + " usedBytes=";
        foreach (string[] vector in new[] { required, ordered, unique })
            Need(vector.Count(x => x == ready) == 1 && !vector.Contains(oldPartial, StringComparer.Ordinal), "actual complete-token READY in every runner vector");
        OS.Tools.QemuRunner.SerialMarkerCompletionResult Stream(string text, bool oldAnchor = false)
        {
            string[] Select(string[] vector) => oldAnchor ? vector.Select(x => x == ready ? oldPartial : x).ToArray() : vector;
            var state = new OS.Tools.QemuRunner.SerialMarkerStateMachine(Select(required), forbidden, orderedMarkers: Select(ordered), uniqueMarkers: Select(unique));
            Need(state.Feed(Encoding.ASCII.GetBytes(text)).Succeeded, "actual streamer consumes transcript");
            return state.Complete();
        }
        var accepted = Stream(good);
        Need(accepted.Passed && accepted.LinesProcessed == 71 && accepted.RequiredSeenCount == required.Union(ordered, StringComparer.Ordinal).Union(unique, StringComparer.Ordinal).Count()
            && accepted.OrderedSeenCount == ordered.Length && accepted.UniqueSeenCount == unique.Length && accepted.SuccessTerminalCount == 1, "actual vectors admit full71 transcript exactly once");
        var partial = Stream(good, true);
        Need(!partial.Passed && partial.MissingRequiredMarkers!.Contains(oldPartial, StringComparer.Ordinal)
            && partial.UniqueSeenCount == unique.Length - 1 && partial.OutOfOrderOccurrenceCount > 0, "old partial-token READY fails actual required/ordered/unique matching");
        string readyLine = lines[61] + "\r\n";
        var missing = Stream(good.Replace(readyLine, "", StringComparison.Ordinal));
        Need(!missing.Passed && missing.MissingRequiredMarkers!.Contains(ready, StringComparer.Ordinal) && missing.UniqueSeenCount == unique.Length - 1, "missing READY rejected by real streamer");
        var duplicate = Stream(good.Replace(readyLine, readyLine + readyLine, StringComparison.Ordinal));
        Need(!duplicate.Passed && duplicate.DuplicateUniqueMarkerHits!.Contains(ready, StringComparer.Ordinal) && duplicate.DuplicateUniqueOccurrenceCount == 1, "duplicate READY rejected by real streamer");
        string[] swappedReady = lines.ToArray(); (swappedReady[60], swappedReady[61]) = (swappedReady[61], swappedReady[60]);
        var outOfOrder = Stream(string.Join("\r\n", swappedReady) + "\r\n");
        Need(!outOfOrder.Passed && outOfOrder.OutOfOrderMarkerHits!.Contains(ready, StringComparer.Ordinal) && outOfOrder.OutOfOrderOccurrenceCount > 0, "early READY rejected by real streamer");
        const string stackMarker = "M04_REFERENCE_STACK";
        foreach (string[] vector in new[] { required, ordered, unique })
            Need(vector.Count(x => x == stackMarker) == 1, "STACK appears once in each actual runner vector");
        string stackLine = lines[60] + "\r\n";
        var missingStack = Stream(good.Replace(stackLine, "", StringComparison.Ordinal));
        Need(!missingStack.Passed && missingStack.MissingRequiredMarkers!.Contains(stackMarker, StringComparer.Ordinal) && missingStack.UniqueSeenCount == unique.Length - 1, "missing STACK rejected by real streamer");
        var duplicateStack = Stream(good.Replace(stackLine, stackLine + stackLine, StringComparison.Ordinal));
        Need(!duplicateStack.Passed && duplicateStack.DuplicateUniqueMarkerHits!.Contains(stackMarker, StringComparer.Ordinal) && duplicateStack.DuplicateUniqueOccurrenceCount == 1, "duplicate STACK rejected by real streamer");
        var lateStack = Stream(good.Replace(stackLine, "", StringComparison.Ordinal).Replace(readyLine, readyLine + stackLine, StringComparison.Ordinal));
        Need(!lateStack.Passed && lateStack.MissingRequiredMarkers!.Count == 0 && lateStack.UniqueSeenCount == unique.Length && lateStack.OutOfOrderMarkerHits!.Contains(ready, StringComparer.Ordinal) && lateStack.OutOfOrderOccurrenceCount > 0, "STACK after READY rejected by real streamer order");
        string acceptedPreamble = Encoding.ASCII.GetString(Convert.FromBase64String("G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg=="));
        Need(Encoding.ASCII.GetByteCount(acceptedPreamble) == OS.Tooling.QemuContracts.FirmwarePrefixBytes && Hash(Encoding.ASCII.GetBytes(acceptedPreamble)) == OS.Tooling.QemuContracts.FirmwarePrefixSha256, "reviewed firmware preamble");
        List<(string Text, bool Accept)> cases = [(good, true), (acceptedPreamble + good, true), (acceptedPreamble.Replace("Boot0001", "Boot0002", StringComparison.Ordinal) + good, false)];
        for (int i = 0; i < lines.Count; i++) foreach (string replacement in new[] { "", lines[i] + " ", " " + lines[i], lines[i] + " diagnostic", lines[i] + "\0", lines[i].ToLowerInvariant(), lines[i] + "\r\n" + lines[i] })
            cases.Add((string.Join("\r\n", lines.Select((line, n) => n == i ? replacement : line)) + "\r\n", false));
        for (int i = 44; i < 62; i++) { var swapped = lines.ToArray(); (swapped[i], swapped[i + 1]) = (swapped[i + 1], swapped[i]); cases.Add((string.Join("\r\n", swapped) + "\r\n", false)); }
        foreach (string suffix in new[] { "\r\n", "partial", "OVMF menu\r\n", "TEST_FAIL m04.010.references bad\r\n" }) cases.Add((good + suffix, false));
        cases.Add((good.Replace("\r\n", "\n", StringComparison.Ordinal), false)); cases.Add((good[..^1], false)); cases.Add(("arbitrary firmware\r\n" + good, false));
        foreach ((string a, string b) in new[] { ("recordsBytes=6672", "recordsBytes=6352"), ("stateBytes=432", "stateBytes=424"), ("state=0x0000000000106000", "state=0x0000000001001110"), ("state=0x0000000000106000", "state=0xFFFFFFFFFFFFFFF8"), ("cellPayload=24", "cellPayload=32"), ("pairLast=8", "pairLast=0"), ("blockPayload=8", "blockPayload=16"), ("calls=22", "calls=21"), ("flags=0x0000000051000000", "flags=0x0000000050000000"), ("zeroBytes=16", "zeroBytes=15"), ("contentsZero=false", "contentsZero=true"), ("used=576", "used=575"), ("exclusiveNonCollecting=true", "exclusiveNonCollecting=false") })
        { Need(good.Contains(a, StringComparison.Ordinal), "control applies " + a); cases.Add((good.Replace(a, b, StringComparison.Ordinal), false)); }
        foreach ((string a, string b) in new[] { ("start=0x0000000002000000", "start=0x0000000002000001"), ("end=0x0000000002021000", "end=0x0000000002020000"), ("initialRsp=0x0000000002020FD8", "initialRsp=0x0000000002020FD0"), ("witness=0x0000000002020000", "witness=0x0000000002004008"), ("sentinel1=0x0000000002000008", "sentinel1=0x0000000002000010"), ("expected0=0x195A86B4C327D0EF", "expected0=0x195A86B4C327D0EE"), ("armed0=0x195A86B4C327D0EF", "armed0=0x195A86B4C327D0EE"), ("armed1=0x62C79E1035AB48D6", "armed1=0x62C79E1035AB48D7"), ("final0=0x195A86B4C327D0EF", "final0=0x195A86B4C327D0EE"), ("final1=0x62C79E1035AB48D6", "final1=0x62C79E1035AB48D7"), ("checks=5", "checks=4"), ("words=2", "words=1"), ("preserved=true", "preserved=false") })
        { Need(good.Contains(a, StringComparison.Ordinal), "stack control applies " + a); cases.Add((good.Replace(a, b, StringComparison.Ordinal), false)); }
        var manifest = cases.Select((c, i) => new { Path = Path.Combine(directory, i + ".log"), c.Accept }).ToArray(); for (int i = 0; i < cases.Count; i++) File.WriteAllText(manifest[i].Path, cases[i].Text, new UTF8Encoding(false));
        string malformed = Path.Combine(directory, "malformed.log"); File.WriteAllBytes(malformed, [0xC0, 0xAF, .. Encoding.ASCII.GetBytes(good)]); manifest = [.. manifest, new { Path = malformed, Accept = false }];
        string json = Path.Combine(directory, "cases.json"); File.WriteAllText(json, System.Text.Json.JsonSerializer.Serialize(manifest));
        foreach (var entry in manifest)
        {
            bool acceptedCase = true;
            try { OS.Tooling.QemuTranscripts.AssertM04CheckedReferenceBarrierTranscript(entry.Path); }
            catch (OS.Tooling.ToolFailure) { acceptedCase = false; }
            Need(acceptedCase == entry.Accept, "whole reference transcript control " + entry.Path + " expected=" + entry.Accept);
        }
        Console.WriteLine("TRANSCRIPT_CONTROLS m04.010 cases=" + manifest.Length);
    }
    [HostTest("m04.010.efi actual default and reference no-stdlib NativeAOT artifacts", HostLane.Native, 4)]
    private static void TestArtifacts()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (bool enabled in new[] { false, true })
        {
            string directory = Scratch(enabled ? "artifact-enabled" : "artifact-default"), artifact = Path.Combine(root, "artifacts/efi/issue537-" + Guid.NewGuid().ToString("N") + ".EFI");
            string[] flags = enabled ? [.. Chain, Flag] : [];
            Result r = Command(ToolArguments("efi build", ["-EfiOutputPath", artifact, "-LogPath", Path.Combine(directory, "publish.log"), .. flags.Select(x => "-" + x)]), Path.Combine(directory, "publish")); Passed(r);
            Need(r.Output.Contains("TEST_PASS efi.shell.publish", StringComparison.Ordinal) && r.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal), "complete actual publication and verifier");
            foreach (string suffix in new[] { "", ".link.json", ".link.map", ".link.rsp", ".serializer.obj" }.Concat(enabled ? new[] { ".references.obj" } : [])) File.Copy(artifact + suffix, Path.Combine(directory, "image.EFI" + suffix));
            foreach (var file in new[] { ("src/OS/bin/Release/net10.0/win-x64/OS.dll", "OS.dll"), ("src/OS/obj/Release/net10.0/win-x64/native/OS.obj", "OS.obj"), ("src/OS/obj/Release/net10.0/win-x64/native/OS.map.xml", "OS.map.xml") }) File.Copy(Path.Combine(root, file.Item1), Path.Combine(directory, file.Item2));
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(artifact + ".link.json")); var e = json.RootElement; var inputs = e.GetProperty("sourceClosureInputs").EnumerateArray().ToArray(); Need(inputs.Length == OS.Tooling.EfiEvidence.SourceClosurePaths(root).Count, "complete raw source inventory");
            foreach (var input in inputs) { string path = input.GetProperty("path").GetString()!; byte[] bytes = File.ReadAllBytes(Path.Combine(root, path)); Need(Hash(bytes).Equals(input.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase), "actual source " + path); string copy = Path.Combine(directory, "source", path); Directory.CreateDirectory(Path.GetDirectoryName(copy)!); File.WriteAllBytes(copy, bytes); }
            var evaluation = e.GetProperty("postRestoreEvaluation"); Need(evaluation.GetProperty("compileCount").GetInt32() == OS.Tooling.EfiContracts.CompileItems.Count, "exact122 Compile"); var actual = evaluation.GetProperty("properties").EnumerateObject().Where(x => x.Name.StartsWith("Enable", StringComparison.Ordinal)).ToArray(); Need(actual.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count, "exact52 flags");
            foreach (var f in actual) Need(f.Value.GetString() == (flags.Contains(f.Name, StringComparer.Ordinal) ? "true" : "false"), "actual profile " + f.Name);
            using var pe = new System.Reflection.PortableExecutable.PEReader(File.OpenRead(Path.Combine(directory, "OS.dll"))); var md = pe.GetMetadataReader(); Need(md.AssemblyReferences.Count == 0, "real no-stdlib zero AssemblyRefs");
            var methods = md.TypeDefinitions.SelectMany(h => md.GetTypeDefinition(h).GetMethods()).Select(h => md.GetMethodDefinition(h)).ToArray();
            foreach (string name in new[] { "ReferenceCallback", "StoreReferenceField", "StoreReferenceUnknown", "CopyReferencePair", "CopyReferenceBlock", "AddressReferencePair", "AddressReferenceBlock", "ReadReferenceFirst", "ReadReferenceLast", "ReadReferenceTag", "ReferenceStackLine" })
            { var matches = methods.Where(m => md.GetString(m.Name) == name).ToArray(); Need(matches.Length == (enabled ? 1 : 0), "actual conditional method " + name); if (enabled) Need(matches[0].RelativeVirtualAddress != 0, "actual IL " + name); }
            Need(methods.Count(m => md.GetString(m.Name) == "TryGetOriginalReferenceStack") == (enabled ? 2 : 0), "original stack getter and forwarder conditional metadata");
            if (enabled)
            {
                Need(Hash(File.ReadAllBytes(artifact + ".references.obj")) == OS.Tooling.EfiContracts.CheckedReferenceObjectSha256.ToUpperInvariant(), "whole generated bridge object");
                Need(r.Output.Contains("EFI_ARTIFACT_CHECKED_REFERENCE_THUNKS bytes=1645 text=1390 exports=3 callbackRel32=3 callbackGraphAccepted=false unwindSupported=false", StringComparison.Ordinal), "actual full COFF-to-PE callback linkage, separate pending graph");
                var callback = methods.Single(m => md.GetString(m.Name) == "ReferenceCallback"); Need(md.GetBlobBytes(callback.Signature).AsSpan().SequenceEqual(new byte[] { 0, 4, 9, 11, 11, 11, 11 }), "ordinary uint callback four UInt64 values");
            }
            File.WriteAllText(Path.Combine(directory, "identities.json"), System.Text.Json.JsonSerializer.Serialize(Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Select(p => new { Path = Path.GetRelativePath(directory, p), Bytes = new FileInfo(p).Length, Sha256 = Hash(File.ReadAllBytes(p)) }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

}
#endif
