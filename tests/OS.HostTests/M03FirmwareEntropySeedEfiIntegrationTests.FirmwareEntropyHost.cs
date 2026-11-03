using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
#if ENTROPY_CAPTURE_HOST
using OS.Boot;
using Core = OS.Boot.EfiEntry.FirmwareEntropyCapture;
using Reservoir = OS.Boot.EfiEntry.FirmwareEntropyReservoir;

// Genuine numeric core on admitted host storage and controlled ABI callbacks.
// No EfiMain, firmware output, target native image or device executes here.
internal static unsafe class FirmwareEntropyHost
{
    private static byte* memory;
    private static Core.State* S => (Core.State*)memory;
    private static Core.Buffer* Scratch => (Core.Buffer*)(memory + 512);
    private static Core.Buffer* Retained => (Core.Buffer*)(memory + 768);
    private static EfiSystemTable* Table => (EfiSystemTable*)(memory + 1024);
    private static EfiBootServices* Services => (EfiBootServices*)(memory + 1536);
    private static Core.Protocol* Rng => (Core.Protocol*)(memory + 2048);
    private static int mode, locateCount, rngCount, infoCount;
    private static byte fill;
    private static bool callbackValid;
    private static Reservoir.State* Pool => (Reservoir.State*)(memory + 2304);
    private static Core.State* poolCapture;
    private static int poolFault, poolFaultBlock;
    private static void Need(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static byte[] Snapshot() => new ReadOnlySpan<byte>(memory, 4096).ToArray();
    private static void Reset(int selected = 0, byte value = 0xA5)
    {
        new Span<byte>(memory, 4096).Clear();mode=selected;fill=value;
        locateCount=rngCount=infoCount=0;callbackValid=true;
        Table->BootServices=Services;Services->LocateProtocol=&Locate;Rng->GetRng=&Get;
        Rng->GetInfo=(void*)(delegate* unmanaged<ulong>)&Info;
    }
    [UnmanagedCallersOnly]
    private static ulong Info() { infoCount++;return 0x8000000000000007UL; }
    [UnmanagedCallersOnly]
    private static ulong Locate(EfiGuid* guid, void* registration, void** output)
    {
        locateCount++;
        callbackValid &= locateCount==1 && rngCount==0 && S->Phase==2 && S->LocateCalls==1 &&
            registration==null && output==(void**)&S->Rng && guid!=null &&
            guid->Data1==0x3152BCA5 && guid->Data2==0xEADE && guid->Data3==0x433D &&
            guid->Data4_0==0x86 && guid->Data4_1==0x2E && guid->Data4_2==0xC0 && guid->Data4_3==0x1C &&
            guid->Data4_4==0xDC && guid->Data4_5==0x29 && guid->Data4_6==0x1F && guid->Data4_7==0x44;
        if (output!=(void**)&S->Rng) return 0x8000000000000002UL;
        if (mode==1) return Core.NotFound;
        if (mode==2) return 0x8000000000000007UL;
        *output=mode switch {3=>null,4=>Scratch,5=>(void*)(ulong.MaxValue-7),6=>guid,7=>Table,8=>Services,9=>S,_=>Rng};
        if(mode==10) Rng->GetRng=null;
        if(mode==11) Rng->GetRng=(delegate* unmanaged<Core.Protocol*,EfiGuid*,nuint,byte*,ulong>)Retained;
        if(mode==12) callbackValid &= !Core.Begin(S,Scratch,Retained,Table) && !Core.Acquire(S);
        return 0;
    }
    [UnmanagedCallersOnly]
    private static ulong Get(Core.Protocol* instance, EfiGuid* algorithm, nuint count, byte* bytes)
    {
        rngCount++;
        callbackValid &= locateCount==1 && rngCount==1 && instance==Rng && algorithm==null &&
            count==32 && bytes==Scratch->Data && S->Phase==2 && S->RngCalls==1 && S->PublishedBytes==0;
        if (count!=32 || bytes!=Scratch->Data) return 0x8000000000000002UL;
        for(int i=0;i<32;i++)bytes[i]=fill;
        if(mode==13) return 0x8000000000000007UL;
        if(mode==14) Scratch->After0 ^= 1;
        if(mode==15) Scratch->Data[32]=1;
        if(mode==16) Retained->Data[0]=1;
        if(mode==17) callbackValid &= !Core.Begin(S,Scratch,Retained,Table) && !Core.Acquire(S);
        return 0;
    }
    private static void Begin() => Need(Core.Begin(S,Scratch,Retained,Table), "actual begin");

    private static void ResetPool(int fault = 0, int block = 0)
    {
        Reset(); poolFault = fault; poolFaultBlock = block; poolCapture = null;
        Services->LocateProtocol = &PoolLocate; Rng->GetRng = &PoolGet;
    }
    [UnmanagedCallersOnly]
    private static ulong PoolLocate(EfiGuid* guid, void* registration, void** output)
    {
        locateCount++;
        poolCapture = (Core.State*)((byte*)output - ((byte*)&S->Rng - (byte*)S));
        callbackValid &= registration == null && guid != null && guid->Data1 == 0x3152BCA5 &&
            poolCapture->Phase == 2 && poolCapture->LocateCalls == 1 && poolCapture->RngCalls == 0 &&
            poolCapture->PublishedBytes == 0 && Pool->Phase == 1 && Pool->PublishedBytes == 0;
        if (poolFault == 1 && locateCount == poolFaultBlock) return Core.NotFound;
        *output = Rng; return 0;
    }
    [UnmanagedCallersOnly]
    private static ulong PoolGet(Core.Protocol* instance, EfiGuid* algorithm, nuint count, byte* bytes)
    {
        rngCount++;
        callbackValid &= instance == Rng && algorithm == null && count == 32 && bytes == poolCapture->Scratch->Data &&
            poolCapture->Phase == 2 && poolCapture->RngCalls == 1 && poolCapture->PublishedBytes == 0 &&
            Core.Guards(poolCapture->Scratch) && Core.Guards(poolCapture->Retained) &&
            Core.Zero(poolCapture->Retained->Data, 0, 64) && !Reservoir.Acquire(Pool, Table);
        if (count != 32) return 0x8000000000000002UL;
        for (int i = 0; i < 32; i++) bytes[i] = (byte)(rngCount * 32 + i);
        if (locateCount == poolFaultBlock)
        {
            if (poolFault == 2) return 0x8000000000000007UL;
            if (poolFault == 3) poolCapture->Scratch->After0 ^= 1;
            if (poolFault == 4) bytes[32] = 1;
        }
        return 0;
    }
    private static void Run(string name, Action test)
    { Console.WriteLine("TEST_START m03.entropy.host "+name);test();Console.WriteLine("TEST_PASS m03.entropy.host "+name); }
    private static int Main()
    {
        memory=(byte*)NativeMemory.AlignedAlloc(4096,64);Need(memory!=null,"mapped storage");
        try
        {
            Run("layout and genuine32 byte zero repeat retention",()=>{
                Need(sizeof(Core.State)==112&&sizeof(Core.Buffer)==96&&sizeof(Core.Protocol)==16,"actual POD sizeof");
                Need((byte*)&Rng->GetRng-(byte*)Rng==8 && (byte*)Scratch->Data-(byte*)Scratch==16,"actual field offsets");
                foreach(byte value in new byte[]{0,1,0xA5,0xFF}){
                    Reset(value:value);Begin();Need(Core.Acquire(S)&&S->Phase==3&&S->PublishedBytes==32&&Core.ClearedBytes(S)==32,"complete publication");
                    Need(callbackValid&&locateCount==1&&rngCount==1&&infoCount==0&&S->LocateStatus==0&&S->RngStatus==0,"original ABI exact arguments and status");
                    Need(new ReadOnlySpan<byte>(Retained->Data,32).IndexOfAnyExcept(value)==-1&&Core.Zero(Scratch->Data,0,64)&&Core.Zero(Retained->Data,32,32)&&Core.Guards(Scratch)&&Core.Guards(Retained),"retention clear guards tails");
                    byte[] before=Snapshot();Need(!Core.Begin(S,Scratch,Retained,Table)&&!Core.Acquire(S)&&!Core.Retain(S)&&Snapshot().SequenceEqual(before),"published retry unchanged");
                }
                Console.WriteLine("ENTROPY_HOST_LAYOUT state=112 buffer=96 protocol=16 payloadOffset=16 getRngOffset=8 requested=32");
            });
            Run("full span alignment overflow and aliases reject before writes",()=>{
                Reset();void* wrong=(void*)0x1234;delegate* unmanaged<EfiGuid*,void*,void**,ulong> callback=&Locate;
                Need(callback(null,null,&wrong)==0x8000000000000002UL&&wrong==(void*)0x1234&&!callbackValid,"wrong callback output slot is not written");
                for(int i=0;i<24;i++){
                    Reset();Core.State* state=S;Core.Buffer* scratch=Scratch;Core.Buffer* retained=Retained;EfiSystemTable* table=Table;
                    switch(i){
                        case 0:state=null;break;case 1:state=(Core.State*)((ulong)S+1);break;case 2:state=(Core.State*)(ulong.MaxValue-7);break;
                        case 3:scratch=null;break;case 4:scratch=(Core.Buffer*)((ulong)Scratch+1);break;case 5:scratch=(Core.Buffer*)(ulong.MaxValue-7);break;
                        case 6:retained=null;break;case 7:retained=(Core.Buffer*)((ulong)Retained+1);break;case 8:retained=(Core.Buffer*)(ulong.MaxValue-7);break;
                        case 9:table=null;break;case 10:table=(EfiSystemTable*)((ulong)Table+1);break;case 11:table=(EfiSystemTable*)(ulong.MaxValue-7);break;
                        case 12:scratch=(Core.Buffer*)S;break;case 13:retained=(Core.Buffer*)((byte*)S+8);break;case 14:retained=(Core.Buffer*)((byte*)Scratch+88);break;
                        case 15:table=(EfiSystemTable*)Scratch;break;case 16:Table->BootServices=(EfiBootServices*)Retained;break;
                        case 17:Table->BootServices=(EfiBootServices*)Table;break;case 18:Table->BootServices=null;break;
                        case 19:Services->LocateProtocol=null;break;case 20:Services->LocateProtocol=(delegate* unmanaged<EfiGuid*,void*,void**,ulong>)S;break;
                        case 21:S->Phase=4;break;case 22:Scratch->Data[63]=1;break;case 23:Retained->Data[0]=1;break;
                    }
                    byte[] before=Snapshot();Need(!Core.Begin(state,scratch,retained,table)&&Snapshot().SequenceEqual(before)&&locateCount==0&&rngCount==0,"entire page unchanged "+i);
                }
            });
            Run("real discovery failures no GetInfo or GetRNG and unchanged retained",()=>{
                foreach(int selected in new[]{1,2,3,4,5,6,7,8,9,10,11}){
                    Reset(selected);Begin();byte[] before=new ReadOnlySpan<byte>(Retained,96).ToArray();
                    Need(!Core.Acquire(S)&&S->Phase==4&&S->PublishedBytes==0&&Core.ClearedBytes(S)==0&&rngCount==0&&infoCount==0&&callbackValid,"discovery rejected "+selected);
                    Need(new ReadOnlySpan<byte>(Retained,96).SequenceEqual(before),"full retained span preserved");
                    Need(S->LocateStatus==(selected==1?Core.NotFound:selected==2?0x8000000000000007UL:0),"original status preserved");
                    Need(S->Error==(selected==1?2UL:selected==2?3UL:4UL),"distinct status classification");
                    byte[] after=Snapshot();Need(!Core.Acquire(S)&&!Core.Begin(S,Scratch,Retained,Table)&&Snapshot().SequenceEqual(after),"terminal failure refuses retry");
                }
            });
            Run("provider writes then failure exposes only clear completion status",()=>{
                foreach(byte value in new byte[]{0,0xA5,0xFF}){
                    Reset(13,value);Begin();byte[] before=new ReadOnlySpan<byte>(Retained,96).ToArray();
                    Need(!Core.Acquire(S)&&S->RngStatus==0x8000000000000007UL&&S->Error==5&&S->PublishedBytes==0&&Core.ClearedBytes(S)==0,"public failure result independent of provider value");
                    Need(new ReadOnlySpan<byte>(Retained,96).SequenceEqual(before)&&new ReadOnlySpan<byte>(Scratch->Data,32).IndexOfAnyExcept(value)==-1,"failure retains original scratch privately and retained unchanged");
                }
            });
            Run("precopy guard tail and retained corruption cannot publish",()=>{
                foreach(int selected in new[]{14,15,16})foreach(byte value in new byte[]{0,0xA5}){
                    Reset(selected,value);Begin();Need(!Core.Acquire(S)&&S->RngStatus==0&&S->Error==6&&S->PublishedBytes==0&&Core.ClearedBytes(S)==0,"guard failure diagnostic independent of scratch data");
                    Need(Core.Zero(Retained->Data,selected==16?1:0,selected==16?63:64),"no destination writes after rejected admission");
                }
            });
            Run("one shot callback reentry and invalid retention phase",()=>{
                foreach(int selected in new[]{12,17}){Reset(selected);Begin();Need(Core.Acquire(S)&&callbackValid&&locateCount==1&&rngCount==1,"reentry cannot reacquire");}
                for(ulong phase=0;phase<=4;phase++){
                    Reset();Begin();S->Phase=phase;byte[] before=Snapshot();Need(!Core.Retain(S)&&Snapshot().SequenceEqual(before),"no retention before complete acquisition "+phase);
                }
            });
            Run("runtime reservoir uses eight original guarded acquisitions then scrubs without refill",()=>{
                ResetPool(); Need(Reservoir.Acquire(Pool, Table), "complete reservoir");
                Need(callbackValid && locateCount == 8 && rngCount == 8 && infoCount == 0 &&
                    Pool->Phase == 2 && Pool->PublishedBytes == 256, "eight fresh acquisitions and single publication");
                for (int i = 0; i < 256; i++) Need(Pool->Data[i] == (byte)(32 + i), "distinct original bytes " + i);
                byte[] complete = Snapshot(); Need(!Reservoir.Acquire(Pool, Table) && Snapshot().SequenceEqual(complete), "no second lifetime");
                Need(Reservoir.Clear(Pool) && Pool->Phase == 3 && Pool->PublishedBytes == 0 && Core.Zero(Pool->Data, 0, 256), "complete scrub");
                Need(!Reservoir.Acquire(Pool, Table) && Reservoir.Clear(Pool) && locateCount == 8 && rngCount == 8, "clear never authorizes refill");
            });
            Run("runtime reservoir failures scrub earlier blocks and malformed aliases mutate nothing",()=>{
                foreach (int fault in new[] { 1, 2, 3, 4 }) foreach (int block in new[] { 1, 4, 8 })
                {
                    ResetPool(fault, block);
                    Need(!Reservoir.Acquire(Pool, Table) && Pool->Phase == 3 && Pool->PublishedBytes == 0 &&
                        Core.Zero(Pool->Data, 0, 256), "failed acquisition clears prefix and tail");
                    Need(callbackValid && locateCount == block && rngCount == block - (fault == 1 ? 1 : 0), "no acquisition after failed block");
                    byte[] failed = Snapshot(); Need(!Reservoir.Acquire(Pool, Table) && Snapshot().SequenceEqual(failed), "failed lifetime cannot retry");
                }
                for (int i = 0; i < 8; i++)
                {
                    ResetPool(); Reservoir.State* candidate = Pool; EfiSystemTable* table = Table;
                    switch (i)
                    {
                        case 0: candidate = null; break;
                        case 1: candidate = (Reservoir.State*)((byte*)Pool + 1); break;
                        case 2: candidate = (Reservoir.State*)(ulong.MaxValue - 7); break;
                        case 3: candidate = (Reservoir.State*)((byte*)Table - 128); break;
                        case 4: candidate = (Reservoir.State*)((byte*)Services - 128); break;
                        case 5: table = null; break;
                        case 6: Pool->Phase = 2; break;
                        case 7: Pool->Data[255] = 1; break;
                    }
                    byte[] before = Snapshot(); Need(!Reservoir.Acquire(candidate, table) && Snapshot().SequenceEqual(before) &&
                        locateCount == 0 && rngCount == 0, "malformed or stale reservoir unchanged " + i);
                }
            });
            Console.WriteLine("TEST_PASS m03.entropy.host.tests");return 0;
        }
        finally { NativeMemory.AlignedFree(memory); }
    }
}
#else
#endif
