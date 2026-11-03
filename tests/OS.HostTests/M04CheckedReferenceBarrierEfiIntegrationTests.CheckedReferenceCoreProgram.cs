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

internal static unsafe partial class CheckedReferenceCoreProgram
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

































































































}
#else
#endif
