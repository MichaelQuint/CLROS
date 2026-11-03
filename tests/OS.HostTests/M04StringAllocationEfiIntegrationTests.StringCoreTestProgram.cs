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

internal static unsafe partial class StringCoreTestProgram
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




















































































}
#else
#endif
