using System.Diagnostics;
using System.Reflection;
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
#if MEMORY_MAP_CAPTURE_HOST
using OS.Boot;

// This ordinary host child links the whole actual EfiEntry and original Capture.
// Reflection reaches private observation members only; no EfiMain, output,
// architectural instruction, real firmware or replacement Capture is executed.
internal static unsafe class MemoryMapCaptureHost
{
    private const ulong TooSmall=0x8000000000000005, Error=0x8000000100000007;
    private static readonly Type Observer=typeof(EfiEntry).GetNestedType("M03MemoryMapCaptureObserver",BindingFlags.NonPublic)!;
    private static readonly FieldInfo State=Observer.GetField("s_observation",BindingFlags.Static|BindingFlags.NonPublic)!;
    private static byte* s_pool;
    private static EfiBootServices* s_services;
    private static int s_case,s_maps,s_allocations;
    private static bool s_bad;
    private static ulong s_querySize,s_finalSize,s_queryKey,s_finalKey,s_requested;
    private static delegate* unmanaged<ulong*,void*,ulong*,ulong*,uint*,ulong> ForwardMap=>
        (delegate* unmanaged<ulong*,void*,ulong*,ulong*,uint*,ulong>)(void*)Observer.GetMethod("ForwardGetMemoryMap")!.MethodHandle.GetFunctionPointer();
    private static delegate* unmanaged<uint,ulong,void**,ulong> ForwardPool=>
        (delegate* unmanaged<uint,ulong,void**,ulong>)(void*)Observer.GetMethod("ForwardAllocatePool")!.MethodHandle.GetFunctionPointer();
    private static ulong Value(string name)=>(ulong)State.FieldType.GetField(name)!.GetValue(State.GetValue(null))!;
    private static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static void Reset(int scenario=0)
    {
        State.SetValue(null,Activator.CreateInstance(State.FieldType));s_case=scenario;s_maps=s_allocations=0;s_bad=false;
        s_querySize=s_finalSize=s_queryKey=s_finalKey=s_requested=0;
        new Span<byte>(s_pool,4096).Fill(0xBC);*s_services=default;s_services->GetMemoryMap=&GetMap;s_services->AllocatePool=&Allocate;
    }
    [UnmanagedCallersOnly]
    private static ulong GetMap(ulong* size,void* map,ulong* key,ulong* stride,uint* version)
    {
        s_maps++;
        if(s_maps==1)
        {
            s_querySize=(ulong)size;s_queryKey=(ulong)key;s_bad|=*size!=0||map!=null;
            *size=s_case==4?ulong.MaxValue-15:96;*key=0xFEDCBA9876543210;*stride=48;*version=1;
            if(s_case==8) { ulong n=0,k=0,d=0;uint v=0;s_bad|=ForwardMap(&n,null,&k,&d,&v)!=0x8000000000000015; }
            return s_case==1?Error:TooSmall;
        }
        s_finalSize=(ulong)size;s_finalKey=(ulong)key;s_bad|=*size!=480||map!=s_pool;
        *size=s_case==5?481UL:160UL;*key=0x123456789ABCDEF0;*stride=s_case==6?0UL:40UL;*version=2;
        if(s_case==3)return TooSmall;
        if(s_case==10)return Error;
        for(int i=0;i<160;i++)s_pool[i]=(byte)(i*7+3);
        if(s_case>=100 && s_case<132)
        { ulong guard=Value("GuardAddress"),payload=Value("Destination");int index=s_case-100;((byte*)(index<16?guard+(ulong)index:payload+131072+(ulong)(index-16)))[0]^=1; }
        if(s_case==132)((byte*)Value("Destination"))[131071]^=1;
        return 0;
    }
    [UnmanagedCallersOnly]
    private static ulong Allocate(uint type,ulong bytes,void** buffer)
    {
        s_allocations++;s_requested=bytes;s_bad|=type!=2||bytes!=480;
        *buffer=s_case==7?null:s_case==9?(void*)Value("Destination"):s_case==11?(void*)(ulong.MaxValue-100):s_pool;
        return s_case==2?Error:0;
    }
    private static bool TryCapture()
    {
        EfiSystemTable table=default;table.BootServices=s_services;
        return (bool)typeof(EfiEntry).GetMethod("TryCaptureUefiMemoryMap",BindingFlags.NonPublic|BindingFlags.Static)!
            .Invoke(null,[Pointer.Box(&table,typeof(EfiSystemTable*))])!;
    }
    private static void Prepare(byte* guard)
    {
        Need((bool)Observer.GetMethod("Prepare")!.Invoke(null,[Pointer.Box(s_services,typeof(EfiBootServices*)),
            Pointer.Box(guard,typeof(OwnedMemoryMapBuffer*)),Pointer.Box(guard+16,typeof(byte*))])!,"real Prepare");
    }
    private static UefiMemoryMapCaptureResult Capture(byte* guard)
        =>UefiMemoryMapCapture.Capture(ForwardMap,ForwardPool,guard+16,131072);
    private static bool Read(UefiMemoryMapCaptureResult result)=>(bool)Observer.GetMethod("ReadBack")!.Invoke(null,[result])!;
    private static void Run(string name,Action body)
    {Console.WriteLine("TEST_START m03.capture.host "+name);try{body();Console.WriteLine("TEST_PASS m03.capture.host "+name);}catch(Exception e){Console.WriteLine("TEST_FAIL m03.capture.host "+name+" "+e);throw;}}
    private static int Main()
    {
        s_pool=(byte*)NativeMemory.AlignedAlloc(4096,64);s_services=(EfiBootServices*)NativeMemory.AllocZeroed((nuint)sizeof(EfiBootServices));
        try
        {
            Run("original capture distinct size slots full owned copy and handoff",()=>
            {
                Reset();Need(TryCapture(),"whole original TryCapture");
                Need(!s_bad&&s_maps==2&&s_allocations==1&&s_requested==480,"exact original forwards");
                Need(s_querySize!=s_finalSize&&Value("SizeSlot")==s_querySize&&Value("FinalSizeSlot")==s_finalSize&&s_queryKey==s_finalKey,"actual distinct Capture locals and retained key slot");
                Need(Value("QueryStride")==48&&Value("FinalStride")==40&&Value("QueryKey")!=Value("FinalKey"),"legitimate changed stride/key");
                Need(Value("QueryEvent")==1&&Value("AllocationEvent")==2&&Value("FinalEvent")==3&&Value("EventCount")==3,"actual call order");
                Need(Value("Fault")==0&&Value("Phase")==8&&Value("HandoffMatched")==1&&Value("SnapshotKey")==Value("FinalKey")&&Value("HandoffKey")==Value("FinalKey"),"same final snapshot/handoff");
                Need(Value("ComparedBytes")==160&&Value("MismatchBytes")==0&&Value("FirstMismatch")==ulong.MaxValue&&Value("GuardBytes")==32&&Value("TailBytes")==130912,"all byte counts");
                byte* destination=(byte*)Value("Destination");Need(new ReadOnlySpan<byte>(destination,160).SequenceEqual(new ReadOnlySpan<byte>(s_pool,160)),"independent full payload");
                for(int i=160;i<131072;i++)Need(destination[i]==0xCD,"independent tail");
                for(int i=0;i<16;i++)Need(destination[-16+i]==0xA7&&destination[131072+i]==0xA7,"independent guards");
                int maps=s_maps,allocations=s_allocations;Need(!TryCapture()&&s_maps==maps&&s_allocations==allocations&&Value("Fault")==1,"one-shot before firmware");
            });
            Run("original failures full width statuses no retry and local admission",()=>
            {
                foreach(int scenario in new[]{1,2,3,4,5,6,7,8,9,10,11})
                {
                    Reset(scenario);Need(!TryCapture(),"failure scenario "+scenario);
                    Need(s_maps<=2&&s_allocations<=1,"no retry/reallocation");
                    if(scenario==1)Need(Value("QueryStatus")==Error&&Value("FirmwareStatus")==Error&&s_allocations==0,"original query failure");
                    if(scenario==2)Need(Value("AllocationStatus")==Error&&Value("FirmwareStatus")==Error&&s_maps==1,"original pool failure");
                    if(scenario==3)Need(Value("FinalStatus")==TooSmall&&Value("FirmwareStatus")==TooSmall,"second BUFFER_TOO_SMALL unchanged");
                    if(scenario==4)Need(s_allocations==0&&s_maps==1,"overflow before AllocatePool");
                    if(scenario==8)Need(Value("QueryStatus")==TooSmall&&Value("Fault")==3&&s_allocations==0,"reentry preserves original firmware result");
                    if(scenario==9||scenario==11)Need(Value("AllocationStatus")==0&&Value("Fault")==5&&s_maps==1,"local fault separate from real success");
                    if(scenario==10)Need(Value("FinalStatus")==Error&&Value("FirmwareStatus")==Error,"full width final error");
                }
            });
            Run("all guards full tail copy mismatch and handoff failures",()=>
            {
                for(int scenario=100;scenario<=132;scenario++){Reset(scenario);Need(!TryCapture()&&Value("Fault")==7,"guard/tail index "+scenario);}
                byte* guard=(byte*)NativeMemory.Alloc(131104);
                try
                {
                    foreach(int index in new[]{0,79,159})
                    {
                        Reset();Prepare(guard);var result=Capture(guard);Need(result.Succeeded,"original Capture prior to independent corruption");
                        guard[16+index]^=1;Need(!Read(result)&&Value("MismatchBytes")==1&&Value("ComparedBytes")==160&&Value("FirstMismatch")==(ulong)index,"actual readback detects byte "+index);
                    }
                }
                finally{NativeMemory.Free(guard);}
                Reset();Need(TryCapture(),"fresh handoff");
                var snapshot=(EfiMemoryMapSnapshot)typeof(EfiEntry).GetField("s_memoryMapSnapshot",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
                var handoff=typeof(EfiEntry).GetField("s_bootHandoff",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
                snapshot.MapKey^=1;Need(!(bool)Observer.GetMethod("Handoff")!.Invoke(null,[snapshot,handoff])!&&Value("Fault")==8,"mismatched actual snapshot");
            });
            Run("pre-read numeric rejection final-size aliases and actual geometry",()=>
            {
                Need(sizeof(EfiBootServices)==328&&sizeof(OwnedMemoryMapBuffer)==131104&&Marshal.SizeOf(State.FieldType)==488,"actual complete geometry");
                MethodInfo storage=Observer.GetMethod("Storage",BindingFlags.NonPublic|BindingFlags.Static)!;
                foreach(ulong[] args in new ulong[][]{[0,0x100000,0x100010],[ulong.MaxValue-100,0x100000,0x100010],[0x100008,0x100000,0x100010],[0x200000,ulong.MaxValue-16,ulong.MaxValue]})
                    Need(!(bool)storage.Invoke(null,args.Cast<object>().ToArray())!,"forged storage rejected without dereference");
                byte* guard=(byte*)NativeMemory.Alloc(131104);ulong* slots=(ulong*)NativeMemory.AllocZeroed(64);
                try
                {
                    for(int variant=0;variant<7;variant++)
                    {
                        Reset();Prepare(guard);ulong* size=slots;ulong* key=slots+1;ulong* stride=slots+2;uint* version=(uint*)(slots+3);*size=0;
                        Need(ForwardMap(size,null,key,stride,version)==TooSmall,"query");void* pool=null;Need(ForwardPool(2,480,&pool)==0,"allocation");
                        ulong* final=variant==0?slots+4:variant==1?size:variant==2?(ulong*)((byte*)size+1):variant==3?(ulong*)pool:variant==4?(ulong*)guard:variant==5?(ulong*)Value("ObservationAddress"):(ulong*)(ulong.MaxValue-3);
                        if(variant<2)*final=480;
                        ulong status=ForwardMap(final,pool,key,stride,version);
                        Need(variant<2?status==0&&Value("FinalSizeSlot")== (ulong)final:status==0x8000000000000015&&s_maps==1,"bounded final slot case "+variant);
                    }
                }
                finally{NativeMemory.Free(guard);NativeMemory.Free(slots);}
                Console.WriteLine("M03_CAPTURE_POD observation=488 guarded=131104 payloadOffset=16 capacity=131072 bootServices=328 distinctCaptureSizeSlots=true");
            });
            Console.WriteLine("TEST_PASS m03.capture.host.tests");return 0;
        }
        finally{NativeMemory.Free(s_services);NativeMemory.AlignedFree(s_pool);}
    }
}
#else
#endif
