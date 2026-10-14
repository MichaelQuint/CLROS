using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Strings = OS.Kernel.Runtime.GuestTestStringAllocationCharacterStorage;
using Arrays = OS.Kernel.Runtime.GuestTestArrayAllocationLengthOverflowChecks;
using Fixed = OS.Kernel.Runtime.GuestTestRhpNewFast;
using Heap = OS.Kernel.Runtime.ManagedHeapPageRangeLiveAllocator;
using Status = OS.Kernel.Runtime.RhpNewFastStatus;

internal static unsafe class GuestTestStringAllocationCharacterStorageTests
{
    private const ulong Token = 0x1234567800000008;
    private const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian;

    [HostFixture(HostLane.Native)]
    internal static void Run()
    {
        Console.WriteLine("TEST_START m04.009");
        try
        {
            Group("contract", CheckContract);
            Group("bytes", CheckBytes);
            Group("character_storage", CheckCharacterStorage);
            Group("mixed", CheckMixed);
            Group("domain", CheckDomain);
            Group("lengths", CheckLengths);
            Group("storage", CheckStorage);
            Group("state_authority", CheckStateAndAuthority);
            Group("zero_allocation", CheckZeroAllocation);
            Group("nostdlib", CheckNoStdLibCompilation);
            Console.WriteLine("CLROS_M04_009_HOST_READY pageBytes=4096 stateBytes=80 stringBaseBytes=22 componentBytes=2"
                + " sharedCursor=true hostConformance=true guestIntegrated=false compiledString=false"
                + " runtimeExport=false interning=false gc=false gate4=Fail");
            Console.WriteLine("TEST_PASS m04.009");
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL m04.009 " + error.GetType().Name + ": " + error.Message);
            throw;
        }
    }

    private static void Group(string name, Action test)
    {

        HostCheck.Run("m04.009." + name, test);
    }

    private static void CheckContract()
    {
        Require(sizeof(RhpNewFastHeapAccess) == 104 && sizeof(RhpNewFastBumpState) == 80 &&
            sizeof(RhpNewFastResult) == 88 && Fixed.RequiredStateBytes == 80, "unchanged shared POD sizes");
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<RhpNewFastHeapAccess>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<RhpNewFastBumpState>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<RhpNewFastResult>(), "no managed references");
        Require((byte)Status.InvalidLength == 18 && (byte)Status.UnsupportedStringType == 19 &&
            !default(RhpNewFastResult).Succeeded, "append-only status/default contract");
        var methods = typeof(Strings).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Require(methods.Length == 1 && methods[0].Name == "TryAllocate" &&
            methods[0].ReturnType == typeof(RhpNewFastResult) && methods[0].GetParameters()[6].ParameterType == typeof(long),
            "thin signed64 entry without binder/reset/free/native export");
        using Fixture f = new();
        Expect(f, Status.NotInitialized, profile: 0, token: 0, flags: 0, length: long.MinValue);
        Require(Bind(f).Succeeded, "existing binder");
        Expect(f, Status.UnsupportedProfile, profile: 0, token: 0, flags: 0, length: long.MinValue);
        foreach (NativeAotLayoutProfile profile in new[] { NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian, (NativeAotLayoutProfile)255 })
            Expect(f, Status.UnsupportedProfile, profile: profile);
        Expect(f, Status.InvalidMethodTableToken, token: 0, flags: 0, length: long.MinValue);
        byte[] before = f.Snapshot();
        Reject(Bind(f), Status.AlreadyInitialized, operation: RhpNewFastOperation.Bind); f.Unchanged(before);
    }

    private static void CheckBytes()
    {
        // Literal x64 sizes and complete expected bytes do not use the layout evaluator.
        (int Length, uint Size)[] vectors = [(0,24),(1,24),(2,32),(3,32),(5,32),(6,40),(7,40),(9,40)];
        using Fixture f = new();
        foreach (var vector in vectors)
        {
            f.Reset(); Require(Bind(f).Succeeded, "bytes bind");
            new Span<byte>(f.Page, 4096).Fill(0xBC);
            byte[] before = f.Snapshot();
            CheckResult(f, Allocate(f, vector.Length), 0, vector.Size, 1, Token);
            CheckBytesAt(f.Page, vector.Size, Token, vector.Length);
            f.OnlyChanged(before, (0x3000,80), (f.PageOffset,(int)vector.Size));
            Require(Retained(f.Access).IsReady && !Retained(f.Access).ContentsAreZeroNow,
                "used page remains retained, only selected span zeroed");
        }
        f.Reset(secondPage: true); Bind(f);
        CheckResult(f, Allocate(f, 2), 0, 32, 1, Token);
        Require(f.PageOffset == 0x6000 && f.Access.Heap->PhysicalStart == 0x300001000, "actual page mapping offset");
        f.Reset(physical: 0xFFFFFFFFFFFFD000); Bind(f);
        RhpNewFastResult high = Allocate(f, 1, token: 0xFFFFFFFFFFFFFFE0);
        CheckResult(f, high, 0, 24, 1, 0xFFFFFFFFFFFFFFE0);
        Require(high.PhysicalAllocationEndExclusive == 0xFFFFFFFFFFFFD018, "no physical/token truncation");
        CheckBytesAt(f.Page, 24, 0xFFFFFFFFFFFFFFE0, 1);
    }

    private static void CheckCharacterStorage()
    {
        using Fixture f = new(); Bind(f);
        RhpNewFastResult result = Allocate(f, 7); CheckResult(f, result, 0, 40, 1, Token);
        CheckBytesAt(f.Page, 40, Token, 7);
        byte[] before = f.Snapshot();
        // Caller writes raw code units only; this is not a compiled String constructor.
        ushort[] units = [0x0041,0x0000,0x00E9,0xD83D,0xDE00,0xFFFF,0x1234];
        for (int i = 0; i < units.Length; i++)
        {
            f.Page[20 + 2*i] = (byte)units[i]; f.Page[21 + 2*i] = (byte)(units[i] >> 8);
        }
        byte[] literal = [0x41,0x00,0x00,0x00,0xE9,0x00,0x3D,0xD8,0x00,0xDE,0xFF,0xFF,0x34,0x12];
        Require(new ReadOnlySpan<byte>(f.Page + 20,14).SequenceEqual(literal), "UTF16 code units including NUL and surrogates");
        Require(BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(f.Page+16,4)) == 7 &&
            BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(f.Page+8,8)) == Token,
            "length and MT retained after caller initialization");
        Require(new ReadOnlySpan<byte>(f.Page+34,6).SequenceEqual(new byte[6]), "terminator and alignment padding remain zero");
        f.OnlyChanged(before, (f.PageOffset+20,14));
        before = f.Snapshot(); CheckResult(f, Allocate(f, 0, Token+8), 40, 24, 2, Token+8);
        f.OnlyChanged(before, (0x3000,80), (f.PageOffset+40,24));
        Require(new ReadOnlySpan<byte>(f.Page+20,14).SequenceEqual(literal), "later empty allocation preserves characters");
    }

    private static void CheckMixed()
    {
        using Fixture f = new();
        foreach (bool reverse in new[] { false, true })
        {
            f.Reset(); Bind(f);
            uint[] sizes = reverse ? [40,24,40,32,32,32,24] : [24,32,32,32,40,24,40];
            ulong used = 0;
            for (int i = 0; i < 7; i++)
            {
                int kind = reverse ? 6-i : i; ulong token = Token+(ulong)i*0x100;
                byte[] before = f.Snapshot();
                RhpNewFastResult result = kind switch
                {
                    0 => Fixed.TryAllocate(f.Access,f.State,f.StateCapacity,Profile,Prefix(0x50000000,24),token),
                    1 => Arrays.TryAllocate(f.Access,f.State,f.StateCapacity,Profile,Prefix(0xE0020001,24),token,1),
                    2 => Allocate(f,3,token),
                    3 => Fixed.TryAllocate(f.Access,f.State,f.StateCapacity,Profile,Prefix(0x51000000,32),token),
                    4 => Arrays.TryAllocate(f.Access,f.State,f.StateCapacity,Profile,Prefix(0xE1020008,24),token,2),
                    5 => Allocate(f,0,token),
                    _ => Allocate(f,7,token)
                };
                CheckResult(f,result,used,sizes[i],(ulong)i+1,token);
                CheckBytesAt(f.Page+used,sizes[i],token,kind switch { 1=>1,2=>3,4=>2,5=>0,6=>7,_=>null });
                f.OnlyChanged(before,(0x3000,80),(f.PageOffset+(int)used,(int)sizes[i]));
                // Disturb only ordinary scalar/character payload, never pointer slots.
                if (kind == 0 || kind == 1 || kind == 2 || kind == 6)
                    f.Page[used+(kind == 0 ? 16UL : kind == 1 ? 24UL : 20UL)] = (byte)(0xC0+i);
                used += sizes[i];
            }
            Require(used == 224 && f.State->UsedBytes == 224 && f.State->AllocationCount == 7, "one mixed cursor/count");
            byte[] beforeBind = f.Snapshot();
            Reject(Bind(f), Status.AlreadyInitialized, operation: RhpNewFastOperation.Bind); f.Unchanged(beforeBind);
            Expect(f, Status.Exhausted, length: 1926); // 3874 raw -> 3880, but only 3872 remain.
            CheckResult(f,Allocate(f,1925,Token+8),224,3872,8,Token+8);
            CheckBytesAt(f.Page+224,3872,Token+8,1925);
            f.OnlyChanged(beforeBind,(0x3000,80),(f.PageOffset+224,3872));
            Expect(f, Status.Exhausted, length: 0);
        }
    }

    private static void CheckDomain()
    {
        using Fixture f = new(); Bind(f);
        Expect(f,Status.InvalidMethodTableFlags,flags:0xD0200002,length:long.MaxValue);
        Expect(f,Status.InvalidMethodTableFlags,flags:0xD1000002,length:long.MaxValue);
        foreach (uint extra in new uint[] {0x00080000,0x00100000,0x00800000,0x02000000})
            Expect(f,Status.UnsupportedStringType,flags:0xD0000002|extra,length:long.MinValue);
        foreach (uint extra in new uint[] {0,0x00040000,0x00400000,0x00440000})
        {
            f.Reset(); Bind(f);
            CheckResult(f, Strings.TryAllocate(f.Access,f.State,f.StateCapacity,Profile,Prefix(0xD0000002|extra),Token,1),0,24,1,Token);
            CheckBytesAt(f.Page,24,Token,1);
        }
        for (uint element = 0; element < 32; element++)
        for (uint kind = 0; kind < 4; kind++)
        {
            if (element == 20 && kind == 0) continue;
            Status expected = element == 24 && kind == 2 ? Status.InvalidBaseSize : Status.UnsupportedStringType;
            Expect(f,expected,flags:0x80000002|(element<<26)|(kind<<16));
        }
        foreach (uint size in new uint[] {0,16,21,23,24,32,uint.MaxValue})
            Expect(f,Status.InvalidBaseSize,size:size,length:long.MinValue);
        foreach (uint width in new uint[] {0,1,3,4,8,255,256,65535})
            Expect(f,Status.InvalidComponentSize,flags:0xD0000000|width,length:long.MaxValue);
        Expect(f,Status.UnsupportedStringType,flags:0x50000000,size:24);
        Expect(f,Status.InvalidLength,flags:0x50000000,size:24,length:1);
        Expect(f,Status.InvalidBaseSize,flags:0x50000000,size:23,length:long.MaxValue);
        Expect(f,Status.UnsupportedStringType,flags:0xE0020002,size:24,length:1);
        Expect(f,Status.InvalidLength,flags:0xE0020002,size:24,length:0x100000001L);
        Expect(f,Status.InvalidMethodTableFlags,flags:0x60020002,size:24);
        byte[] before = f.Snapshot();
        Reject(Fixed.TryAllocate(f.Access,f.State,f.StateCapacity,Profile,Prefix(),Token),Status.UnsupportedFixedType);
        Reject(Arrays.TryAllocate(f.Access,f.State,f.StateCapacity,Profile,Prefix(),Token,1),Status.UnsupportedArrayType);
        f.Unchanged(before);
    }

    private static void CheckLengths()
    {
        using Fixture f = new(); Bind(f);
        foreach (long length in new long[] {long.MinValue,int.MinValue,-1,0x3FFFFFE0L,int.MaxValue,
            0x80000000L,0xFFFFFFFFL,0x100000000L,0x100000001L,0x1000007F5L,long.MaxValue})
            Expect(f,Status.InvalidLength,length:length);
        foreach (long length in new long[] {2038,0x3FFFFFDEL,0x3FFFFFDFL})
            Expect(f,Status.Exhausted,length:length);
        byte[] before = f.Snapshot();
        CheckResult(f,Allocate(f,2037,Token+8),0,4096,1,Token+8);
        CheckBytesAt(f.Page,4096,Token+8,2037);
        Require(f.Page[4094] == 0 && f.Page[4095] == 0, "last two page bytes are the terminator");
        f.OnlyChanged(before,(0x3000,80),(f.PageOffset,4096));
        for (int i=0;i<3;i++) Expect(f,Status.Exhausted,length:0);
        f.Reset(); Bind(f);
        for (ulong i=0;i<170;i++) CheckResult(f,Allocate(f,0,Token+8),i*24,24,i+1,Token+8);
        Require(f.State->UsedBytes == 4080 && f.State->AllocationCount == 170,"empty storage consumes24; no interned singleton");
        Expect(f,Status.Exhausted,length:0);
    }

    private static void CheckStorage()
    {
        using Fixture f = new();
        for(int scenario=0;scenario<36;scenario++)
        {
            f.Reset();Bind(f);RhpNewFastHeapAccess a=f.Access;RhpNewFastBumpState* state=f.State;
            ulong capacity=f.StateCapacity;Status expected=Status.InvalidStorage;
            switch(scenario)
            {
                case 0: state=null;break;
                case 1: state=(RhpNewFastBumpState*)((byte*)state+1);break;
                case 2: capacity=79;break;
                case 3: capacity=ulong.MaxValue;break;
                case 4: state=(RhpNewFastBumpState*)0xFFFFFFFFFFFFFFF8;break;
                case 5: a.Allocator=null;break;
                case 6: a.Nodes=null;break;
                case 7: a.Reservations=null;break;
                case 8: a.Backing=null;break;
                case 9: a.Heap=null;break;
                case 10: a.AllocatorCapacity=0;break;
                case 11: a.AllocatorCapacity=2;break;
                case 12: a.NodeCount=0;break;
                case 13: a.NodeCount=4097;break;
                case 14: a.NodeCapacity=0;break;
                case 15: a.NodeCapacity=ulong.MaxValue;break;
                case 16: a.ReservationCount=0;break;
                case 17: a.ReservationCount=257;break;
                case 18: a.ReservationCapacity=1;break;
                case 19: a.ReservationCapacity=ulong.MaxValue;break;
                case 20: a.ParentIndex=2;break;
                case 21: a.HeapCapacity=215;break;
                case 22: a.BackingCapacity=4095;break;
                case 23: a.Allocator=(BootPageAllocatorState*)((byte*)a.Allocator+1);break;
                case 24: a.Nodes=(BootPageFreeListNode*)((byte*)a.Nodes+1);break;
                case 25: a.Reservations=(BootReservedRange*)((byte*)a.Reservations+1);break;
                case 26: a.Heap=(ManagedHeapPageRangeState*)((byte*)a.Heap+1);break;
                case 27: a.Backing++;break;
                case 28: a.HeapCapacity=ulong.MaxValue;break;
                case 29: a.BackingCapacity=ulong.MaxValue;break;
                case 30: a.Nodes=(BootPageFreeListNode*)0xFFFFFFFFFFFFFFD0;break;
                case 31: a.Reservations=(BootReservedRange*)0xFFFFFFFFFFFFFFD8;break;
                case 32: a.Allocator=(BootPageAllocatorState*)0xFFFFFFFFFFFFFFF8;break;
                case 33: a.Heap=(ManagedHeapPageRangeState*)0xFFFFFFFFFFFFFFF8;break;
                case 34: a.Backing=(byte*)0xFFFFFFFFFFFFF000;break;
                case 35: a.Nodes=(BootPageFreeListNode*)a.Reservations;expected=Status.StorageOverlap;break;
            }
            byte[] before=f.Snapshot();
            Reject(Strings.TryAllocate(a,state,capacity,0,Prefix(0),0,-1),expected);f.Unchanged(before);
        }
        foreach(int offset in new[]{0x100,0x300,0x330,0x1000,0x1050,0x2000,0x20D8,0x5000,0x7000})
        {
            f.Reset();Bind(f);byte[] before=f.Snapshot();
            Reject(Strings.TryAllocate(f.Access,(RhpNewFastBumpState*)(f.Storage+offset),96,Profile,Prefix(),Token,1),
                Status.StorageOverlap);f.Unchanged(before);
        }
        f.Reset();Bind(f);byte[] snapshot=f.Snapshot();
        Reject(Strings.TryAllocate(f.Access,f.State,0x2001,Profile,Prefix(),Token,1),Status.StorageOverlap);f.Unchanged(snapshot);
        foreach(ulong token in new ulong[]{0,1,Token+1,Token+7,ulong.MaxValue,0xFFFFFFFFFFFFFFE8,
            (ulong)f.Access.Allocator,(ulong)f.Access.Nodes+48,(ulong)f.Access.Reservations+80,
            (ulong)f.Access.Heap+216,(ulong)f.Access.Backing+8192,(ulong)f.State+80,(ulong)f.State-16})
            Expect(f,Status.InvalidMethodTableToken,token:token);
        Require(Allocate(f,0,token:(ulong)f.State-24).AllocationSucceeded,"token exact touching span");
        f.Reset();f.State=(RhpNewFastBumpState*)(f.Storage+0x5000-80);f.StateCapacity=80;
        new Span<byte>(f.State,80).Clear();Bind(f);
        CheckResult(f,Allocate(f,1),0,24,1,Token);
    }

    private static void CheckStateAndAuthority()
    {
        using Fixture f = new();
        for (int slot=0;slot<10;slot++)
        {
            f.Reset(); Bind(f); ((ulong*)f.State)[slot]^=8;
            Status expected=slot<3||slot>=8 ? Status.InvalidState : Status.HeapBindingMismatch;
            Expect(f,expected,heapStatus:expected==Status.HeapBindingMismatch ? ManagedHeapPageRangeStatus.Success : default);
        }
        foreach ((ulong used,ulong count) in new (ulong,ulong)[] {(4097,1),(8,1),(24,0),(0,1),(24,2),(4096,171),(ulong.MaxValue,1),(25,1)})
        {
            f.Reset(); Bind(f); ((ulong*)f.State)[8]=used; ((ulong*)f.State)[9]=count; Expect(f,Status.InvalidState);
        }
        f.Reset(); Bind(f);
        RhpNewFastBumpState* copy=(RhpNewFastBumpState*)(f.Storage+0x4000); *copy=*f.State;
        byte[] before=f.Snapshot();
        Reject(Strings.TryAllocate(f.Access,copy,f.StateCapacity,Profile,Prefix(),Token,0),Status.InvalidState); f.Unchanged(before);
        for (int scenario=0;scenario<9;scenario++)
        {
            f.Reset(); Bind(f); Allocate(f,1); RhpNewFastHeapAccess a=f.Access;
            switch(scenario)
            {
                case 0: a.Allocator=(BootPageAllocatorState*)(f.Storage+0x8000); *a.Allocator=*f.Access.Allocator; break;
                case 1: a.Nodes=(BootPageFreeListNode*)(f.Storage+0x8000); a.Nodes[0]=f.Access.Nodes[0]; break;
                case 2: a.Reservations=(BootReservedRange*)(f.Storage+0x8000); a.Reservations[0]=f.Access.Reservations[0]; a.Reservations[1]=f.Access.Reservations[1]; break;
                case 3: a.ParentIndex=0; break;
                case 4: a.Backing=f.Storage+0x8000; break;
                case 5: a.Heap=(ManagedHeapPageRangeState*)(f.Storage+0x8000); *a.Heap=*f.Access.Heap; break;
                case 6: a.NodeCapacity=1; break;
                case 7: a.BackingCapacity=8192; break;
                case 8: f.Access.Reservations[1]=Range(BootReservationKind.AllocatorArena,0x300010000,2); break;
            }
            before=f.Snapshot();
            Reject(Strings.TryAllocate(a,f.State,f.StateCapacity,0,Prefix(0),0,long.MinValue),Status.HeapAuthorityRejected,
                ManagedHeapPageRangeStatus.IdentityMismatch); f.Unchanged(before);
        }
        using Fixture other=new(); f.Reset(); Bind(f); before=f.Snapshot(); byte[] otherBefore=other.Snapshot();
        Reject(Strings.TryAllocate(other.Access,f.State,f.StateCapacity,Profile,Prefix(),Token,0),Status.HeapBindingMismatch,
            ManagedHeapPageRangeStatus.Success); f.Unchanged(before); other.Unchanged(otherBefore);
        f.Reset(); Bind(f); Allocate(f,1); f.Page[20]=0xE7;
        BootPageAllocatorOnePageResult later=BootPageAllocatorMutator.AllocateOnePage(*f.Access.Allocator,
            f.Access.Nodes,f.Access.NodeCount,f.Access.NodeCapacity,null,0,0,1,4096);
        Require(later.Succeeded,"later original allocator allocation"); *f.Access.Allocator=later.AfterState;
        Require(Allocate(f,3).AllocationSucceeded && f.Page[20]==0xE7,"nonzero retained String survives free-chain exhaustion");
        BootPageAllocatorOnePageFreeResult freed=BootPageAllocatorMutator.FreeOnePage(*f.Access.Allocator,
            f.Access.Nodes,f.Access.NodeCount,f.Access.NodeCapacity,null,0,0,later);
        Require(freed.Succeeded,"free only unrelated later page"); *f.Access.Allocator=freed.AfterState;
        Require(Allocate(f,2).AllocationSucceeded,"retained ownership survives legal unrelated free");
        *f.Access.Allocator=f.Access.Heap->InitialAllocatorState; f.Access.Nodes[0]=Node(0x300000000,2);
        Expect(f,Status.HeapAuthorityRejected,heapStatus:ManagedHeapPageRangeStatus.RetainedRangeIsFree);
        f.Reset(); Bind(f); *f.Access.Heap=default;
        Expect(f,Status.HeapAuthorityRejected,heapStatus:ManagedHeapPageRangeStatus.NotReady);
    }

    private static void CheckZeroAllocation()
    {
        using Fixture f=new(); bool passed=true;
        for(int i=0;i<32;i++) Measure(f,ref passed);
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<256;i++) Measure(f,ref passed);
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Require(passed && allocated==0,"warmed complete mixed success/rejection paths allocate zero CLR bytes");
    }
    private static void Measure(Fixture f,ref bool passed)
    {
        f.Reset(); passed&=Bind(f).Succeeded;
        passed&=Fixed.TryAllocate(f.Access,f.State,f.StateCapacity,Profile,Prefix(0x51000000,24),Token).AllocationSucceeded;
        passed&=Arrays.TryAllocate(f.Access,f.State,f.StateCapacity,Profile,Prefix(0xE1020008,24),Token,1).AllocationSucceeded;
        passed&=Allocate(f,0).AllocationSucceeded; passed&=Allocate(f,7).AllocationSucceeded;
        passed&=Allocate(f,long.MinValue).Status==Status.InvalidLength;
        passed&=Allocate(f,0x100000001L).Status==Status.InvalidLength;
        passed&=Allocate(f,2037).Status==Status.Exhausted;
        passed&=Bind(f).Status==Status.AlreadyInitialized;
    }

    private static NativeAotMethodTablePrefix Prefix(uint flags=0xD0000002,uint size=22) =>
        new(flags,size,0xFEDCBA9876543211,ushort.MaxValue,ushort.MaxValue,0xDEADBEEF);
    private static RhpNewFastResult Bind(Fixture f) => Fixed.InitializeOnce(f.Access,f.State,f.StateCapacity);
    private static RhpNewFastResult Allocate(Fixture f,long length,ulong token=Token) =>
        Strings.TryAllocate(f.Access,f.State,f.StateCapacity,Profile,Prefix(),token,length);
    private static ManagedHeapPageRangeResult Retained(RhpNewFastHeapAccess a) => Heap.ValidateRetained(a.Allocator,
        a.AllocatorCapacity,a.Nodes,a.NodeCount,a.NodeCapacity,a.Reservations,a.ReservationCount,
        a.ReservationCapacity,a.ParentIndex,a.Backing,a.BackingCapacity,a.Heap,a.HeapCapacity);
    private static void Expect(Fixture f,Status expected,uint flags=0xD0000002,uint size=22,long length=0,
        ulong token=Token,NativeAotLayoutProfile profile=Profile,ManagedHeapPageRangeStatus heapStatus=default)
    {
        byte[] before=f.Snapshot(); Reject(Strings.TryAllocate(f.Access,f.State,f.StateCapacity,profile,Prefix(flags,size),token,length),expected,heapStatus);
        f.Unchanged(before);
    }
    private static void Reject(RhpNewFastResult r,Status expected,ManagedHeapPageRangeStatus heapStatus=default,
        RhpNewFastOperation operation=RhpNewFastOperation.Allocation)
    {
        Require(r.Status==expected && !r.Succeeded && !r.AllocationSucceeded && r.Operation==operation && r.HeapStatus==heapStatus,
            "exact rejection expected="+expected+" actual="+r.Status+"/"+r.HeapStatus);
        Require(r.MappedAllocationStart==0 && r.MappedAllocationEndExclusive==0 && r.PhysicalAllocationStart==0 &&
            r.PhysicalAllocationEndExclusive==0 && r.ObjectReference==0 && r.MethodTableToken==0 && r.BytesConsumed==0 &&
            r.UsedBefore==0 && r.UsedAfter==0 && r.AllocationCount==0,"no successful rejection payload");
    }
    private static void CheckResult(Fixture f,RhpNewFastResult r,ulong before,uint size,ulong count,ulong token)
    {
        Require(r.AllocationSucceeded && r.Operation==RhpNewFastOperation.Allocation && r.HeapStatus==ManagedHeapPageRangeStatus.Success &&
            r.MappedAllocationStart==(ulong)f.Page+before && r.MappedAllocationEndExclusive==(ulong)f.Page+before+size &&
            r.PhysicalAllocationStart==f.Access.Heap->PhysicalStart+before && r.PhysicalAllocationEndExclusive==f.Access.Heap->PhysicalStart+before+size &&
            r.ObjectReference==(ulong)f.Page+before+8 && r.MethodTableToken==token && r.BytesConsumed==size &&
            r.UsedBefore==before && r.UsedAfter==before+size && r.AllocationCount==count &&
            f.State->UsedBytes==before+size && f.State->AllocationCount==count,"literal mixed allocation result");
    }
    private static void CheckBytesAt(byte* origin,uint size,ulong token,int? length)
    {
        byte[] expected=new byte[size]; BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8,8),token);
        if(length.HasValue) BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(16,4),length.Value);
        Require(new ReadOnlySpan<byte>(origin,(int)size).SequenceEqual(expected),"independent entire selected interval byte oracle");
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly byte* Storage=(byte*)NativeMemory.AlignedAlloc(65536,4096);
        internal RhpNewFastHeapAccess Access;
        internal RhpNewFastBumpState* State;
        internal ulong StateCapacity;
        internal byte* Page;
        internal int PageOffset=>(int)(Page-Storage);
        internal Fixture(){Require(Storage!=null,"native fixture");Reset();}
        internal void Reset(bool secondPage=false,ulong physical=0x300000000)
        {
            // A test reset creates a fresh authority lifetime; production has no reset.
            new Span<byte>(Storage,65536).Fill(0xA5);
            Access=new RhpNewFastHeapAccess
            {
                Allocator=(BootPageAllocatorState*)(Storage+0x100),AllocatorCapacity=1,
                Nodes=(BootPageFreeListNode*)(Storage+0x300),NodeCount=1,NodeCapacity=2,
                Reservations=(BootReservedRange*)(Storage+0x1000),ReservationCount=2,ReservationCapacity=3,
                ParentIndex=1,Heap=(ManagedHeapPageRangeState*)(Storage+0x2000),HeapCapacity=232,
                Backing=Storage+0x5000,BackingCapacity=8208
            };
            State=(RhpNewFastBumpState*)(Storage+0x3000);StateCapacity=96;
            *Access.Allocator=new BootPageAllocatorState(true,4096,0,1,secondPage?1UL:2UL,
                secondPage?4096UL:8192UL,secondPage?1UL:0UL,secondPage?4096UL:0UL);
            Access.Nodes[0]=Node(physical+(secondPage?4096UL:0UL),secondPage?1UL:2UL);
            Access.Reservations[0]=Range(BootReservationKind.LoadedImage,0x100000,1);
            Access.Reservations[1]=Range(BootReservationKind.AllocatorArena,physical,2);
            new Span<byte>(Access.Heap,216).Clear();new Span<byte>(State,80).Clear();
            ManagedHeapPageRangeResult heap=Heap.ReserveAndInitializeOnce(Access.Allocator,Access.AllocatorCapacity,
                Access.Nodes,Access.NodeCount,Access.NodeCapacity,Access.Reservations,Access.ReservationCount,
                Access.ReservationCapacity,Access.ParentIndex,Access.Backing,Access.BackingCapacity,Access.Heap,Access.HeapCapacity);
            Require(heap.IsReady,"actual retained-page reserve");Page=(byte*)heap.MappedStart;
        }
        internal byte[] Snapshot()=>new ReadOnlySpan<byte>(Storage,65536).ToArray();
        internal void Unchanged(byte[] before)=>Require(new ReadOnlySpan<byte>(Storage,65536).SequenceEqual(before),"all storage/state/authority/tails/canaries unchanged");
        internal void OnlyChanged(byte[] before,params (int Start,int Length)[] intervals)
        {
            for(int i=0;i<before.Length;i++)
            {
                bool inside=false;foreach(var interval in intervals) inside|=i>=interval.Start && i<interval.Start+interval.Length;
                if(!inside && Storage[i]!=before[i]) throw new InvalidOperationException("changed outside allocation at "+i);
            }
        }
        public void Dispose()=>NativeMemory.AlignedFree(Storage);
    }
    private static BootPageFreeListNode Node(ulong start,ulong pages)=>new(0,0,start,start+pages*4096,pages,pages*4096,-1);
    private static BootReservedRange Range(BootReservationKind kind,ulong start,ulong pages)
    {
        PhysicalAddress.TryCreateFromUefiBoundary(start,out PhysicalAddress address);
        Require(BootReservedRange.TryCreate(kind,address,pages,out BootReservedRange range)==BootReservedRangeStatus.Success,"fixture reservation");
        return range;
    }

    private static void CheckNoStdLibCompilation()
    {
        string root=FindRoot();string directory=Path.Combine(root,"artifacts","issue78","nostdlib",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);File.Copy(Path.Combine(root,"global.json"),Path.Combine(directory,"global.json"));
        string[] sources=
        [
            "src/OS/Runtime/MinimalRuntime.cs","src/OS/Runtime/MethodImplAttribute.cs",
            "src/OS.Kernel/Boot/TypedPhysicalVirtualDeviceAddressPODWrappers.cs",
            "src/OS.Kernel/Memory/BootReservedRange.cs","src/OS.Kernel/Memory/BootReservationTableValidator.cs",
            "src/OS.Kernel/Memory/BootPageAllocatorCoreContracts.cs","src/OS.Kernel/Memory/BootPageAllocatorMutation.cs",
            "src/OS.Kernel/Runtime/ManagedHeapPageRangeLiveAllocator.cs",
            "src/OS.Kernel/Runtime/ObjectArrayStringEETypeMethodTableLayoutContracts.cs",
            "src/OS.Kernel/Runtime/GuestTestRhpNewFast.cs","src/OS.Kernel/Runtime/GuestTestStringAllocationCharacterStorage.cs"
        ];
        string includes=string.Join(Environment.NewLine,sources.Select(p=>"<Compile Include=\""+System.Security.SecurityElement.Escape(Path.Combine(root,p))+"\" />"));
        File.WriteAllText(Path.Combine(directory,"Compile.csproj"),$$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType><AssemblyName>CLROS.M04.String.NoStdLib</AssemblyName>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                <NoStdLib>true</NoStdLib><DefineConstants>NOSTDLIB_EFI</DefineConstants><NoConfig>true</NoConfig>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo><GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles><RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly><EnableNETAnalyzers>false</EnableNETAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <DebugType>none</DebugType><RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                <UseSharedCompilation>false</UseSharedCompilation>
              </PropertyGroup>
              <ItemGroup>{{includes}}</ItemGroup>
              <Target Name="RemoveFrameworkReferences" BeforeTargets="BeforeCompile" AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup><ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                  <ReferencePath Remove="@(ReferencePath)" /></ItemGroup>
              </Target>
            </Project>
            """,new UTF8Encoding(false));
        var identities=sources.Select(p=>new{Path=p,Sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root,p))))});
        File.WriteAllText(Path.Combine(directory,"source-identities.json"),System.Text.Json.JsonSerializer.Serialize(identities));
        ProcessStartInfo start=new("dotnet"){WorkingDirectory=directory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        string[] arguments=["build","Compile.csproj","--nologo","-m:1","-c","Release","-nodeReuse:false"];
        foreach(string arg in arguments) start.ArgumentList.Add(arg);
        File.WriteAllText(Path.Combine(directory,"command.json"),System.Text.Json.JsonSerializer.Serialize(new{Executable=start.FileName,Arguments=arguments,WorkingDirectory=directory,StartedUtc=DateTime.UtcNow}));
        using Process process=Process.Start(start)??throw new InvalidOperationException("no-stdlib start");
        Task<string> stdout=process.StandardOutput.ReadToEndAsync(),stderr=process.StandardError.ReadToEndAsync();
        bool exited=process.WaitForExit(90000);if(!exited){process.Kill(entireProcessTree:true);process.WaitForExit();}
        Task.WaitAll(stdout,stderr);
        File.WriteAllText(Path.Combine(directory,"build.stdout.log"),stdout.Result);File.WriteAllText(Path.Combine(directory,"build.stderr.log"),stderr.Result);
        File.WriteAllText(Path.Combine(directory,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Exited=exited,NativeExit=process.ExitCode,EndedUtc=DateTime.UtcNow}));
        Require(exited && process.ExitCode==0,"actual eleven-source String no-stdlib compile: "+directory+" "+stdout.Result+stderr.Result);
        using FileStream stream=File.OpenRead(Path.Combine(directory,"bin","Release","net11.0","CLROS.M04.String.NoStdLib.dll"));
        using PEReader pe=new(stream);MetadataReader metadata=pe.GetMetadataReader();
        Require(metadata.AssemblyReferences.Count==0,"zero AssemblyRefs");
        foreach((string type,string method) in new[]{("GuestTestStringAllocationCharacterStorage","TryAllocate"),("GuestTestRhpNewFast","TryAllocate"),("GuestTestRhpNewFast","TryAllocateArray"),("GuestTestRhpNewFast","TryAllocateString"),("GuestTestRhpNewFast","AllocateCore")})
        {
            TypeDefinition definition=metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(t=>metadata.GetString(t.Name)==type);
            MethodDefinition entry=definition.GetMethods().Select(metadata.GetMethodDefinition).Single(m=>metadata.GetString(m.Name)==method);
            Require(entry.RelativeVirtualAddress!=0 && pe.GetMethodBody(entry.RelativeVirtualAddress).GetILBytes()!.Length>0,"actual production method body "+type+"."+method);
        }
        File.WriteAllText(Path.Combine(directory,"metadata-result.json"),System.Text.Json.JsonSerializer.Serialize(new{SourceCount=sources.Length,AssemblyReferences=metadata.AssemblyReferences.Count,ProductionMethodsVerified=true}));
        Console.WriteLine("M04_STRING_HOST_NOSTDLIB inputs=11 assemblyReferences=0 sharedCore=true");
    }
    private static string FindRoot()
    {
        DirectoryInfo? current=new(AppContext.BaseDirectory);
        while(current is not null){if(File.Exists(Path.Combine(current.FullName,"CLROS.slnx"))) return current.FullName;current=current.Parent;}
        throw new InvalidOperationException("CLROS root not found");
    }
    private static void Require(bool condition,string reason){if(!condition) throw new InvalidOperationException(reason);}
}
