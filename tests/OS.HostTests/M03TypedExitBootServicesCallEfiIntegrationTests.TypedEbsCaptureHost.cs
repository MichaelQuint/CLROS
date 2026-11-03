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
#if TYPED_EBS_CAPTURE_HOST
using OS.Boot;
using Core = OS.Boot.EfiEntry.TypedExitBootServicesCallProbe;
using MapCore = OS.Boot.EfiEntry.FinalMapRetryProbe;
internal static unsafe class TypedEbsCaptureHost
{
    private static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static void Run(string name,Action test){Console.WriteLine("TEST_START m03.typedebs.host "+name);test();Console.WriteLine("TEST_PASS m03.typedebs.host "+name);}
    private static ulong Entries()=>(ulong)typeof(Core).GetField("s_totalEntries",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetValue(null)!;
    private static byte[] Bytes(void* address,int length)=>new ReadOnlySpan<byte>(address,length).ToArray();
    private static ulong mapEntries;
    private static bool mapReentryDenied;
    [UnmanagedCallersOnly]
    private static ulong Map(ulong* size,void* buffer,ulong* key,ulong* stride,uint* version)
    {
        mapEntries++;
        if(*size!=4096)return 0x8000000000000005UL;
        if(mapEntries==1)mapReentryDenied=!MapCore.Execute((void*)1,&Map,(byte*)buffer,*size);
        *size=48;*stride=48;*version=1;*key=mapEntries<=3?0:ulong.MaxValue;
        ((byte*)buffer)[0]=0xA5;
        return 0;
    }
    private static int Main(string[] args)
    {
        if(args.Length==1&&args[0]=="orphan"){
            Run("every callback entry counted before missing active pointer refusal",()=>{
                var method=typeof(Core).GetMethod("Controlled",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
                var callback=(delegate* unmanaged<void*,ulong,ulong>)(nuint)method.MethodHandle.GetFunctionPointer();
                Need(Entries()==0,"fresh process counter");ulong status=callback((void*)0x123456789ABCDEF0UL,ulong.MaxValue);
                Need(status==0x8000000000000002UL&&Entries()==1,"guard still counts actual indirect entry");
            });
            Console.WriteLine("TEST_PASS m03.typedebs.orphan.tests");return 0;
        }
        Core.Storage* storage=(Core.Storage*)NativeMemory.AlignedAlloc(4096,64);
        Core.Storage* fresh=(Core.Storage*)NativeMemory.AlignedAlloc(4096,64);
        if(storage==null||fresh==null)throw new OutOfMemoryException();
        NativeMemory.Clear(storage,4096);NativeMemory.Clear(fresh,4096);
        try{
            nint admitted=(nint)storage,second=(nint)fresh;
            Run("mapped admission preserves every byte and callback count",()=>{
                Core.Storage* s=(Core.Storage*)admitted;byte[] before=Bytes(s,4096);
                Need(sizeof(TypedExitBootServicesState)==40&&sizeof(TypedExitBootServicesResult)==16&&sizeof(Core.Observation)==104&&sizeof(Core.Storage)==600,"actual POD geometry");
                Need(!Core.Execute(null,(void*)1)&&!Core.Execute((Core.Storage*)((byte*)s+1),(void*)1)&&!Core.Execute(s,null),"null or unaligned mapped admission");
                Need(Bytes(s,4096).SequenceEqual(before)&&Entries()==0,"rejected admission never writes/calls");
                for(int i=0;i<sizeof(Core.Storage);i++){((byte*)s)[i]=1;before=Bytes(s,4096);Need(!Core.Execute(s,(void*)1)&&Bytes(s,4096).SequenceEqual(before)&&Entries()==0,"nonzero byte rejection "+i);((byte*)s)[i]=0;}
            });
            Run("four actual typed calls exact opaque keys statuses and complete state bytes",()=>{
                Core.Storage* s=(Core.Storage*)admitted;void* handle=(void*)0x123456789ABCDEF0UL;
                Need(Core.Execute(s,handle)&&Entries()==4&&s->Phase==2&&s->AdmissionPreserved==1&&s->ReadyPreserved==1,"complete genuine coordinator");
                ulong[] keys=[0,0x8000000000000000UL,ulong.MaxValue,0x8000000000000000UL],statuses=[0,0x8000000000000002UL,0xC000000000000123UL,0];
                ulong callback=(ulong)(nuint)typeof(Core).GetMethod("Controlled",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.MethodHandle.GetFunctionPointer();
                int functionOffset=(int)Marshal.OffsetOf<TypedExitBootServicesState>("Function"),handleOffset=(int)Marshal.OffsetOf<TypedExitBootServicesState>("Handle"),keyOffset=(int)Marshal.OffsetOf<TypedExitBootServicesState>("Key"),statusOffset=(int)Marshal.OffsetOf<TypedExitBootServicesState>("ReturnedStatus"),phaseOffset=(int)Marshal.OffsetOf<TypedExitBootServicesState>("PhaseValue");
                for(int i=0;i<4;i++){
                    var state=(TypedExitBootServicesState*)((byte*)s+i*sizeof(TypedExitBootServicesState));var o=(Core.Observation*)((byte*)s+160+i*sizeof(Core.Observation));
                    byte[] expected=new byte[40];System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(functionOffset),callback);System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(handleOffset),(ulong)handle);System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(keyOffset),keys[i]);System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(statusOffset),statuses[i]);System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(phaseOffset),3);
                    Need(Bytes(state,40).SequenceEqual(expected),"full state including retained padding "+i);
                    Need(o->Calls==1&&o->Handle==(ulong)handle&&o->Key==keys[i]&&o->Status==statuses[i]&&o->Phase==2,"actual ABI observation "+i);
                    Need(o->ReuseAttempt==9&&o->ReusePrepare==9&&o->ReusePreserved==1&&o->ReentryAttempt==(i==3?8UL:0)&&o->ReentryPrepare==(i==3?8UL:0)&&o->ReentryPreserved==(i==3?1UL:0),"full denial results "+i);
                }
                Need(new ReadOnlySpan<byte>((byte*)s+600,3496).ToArray().All(x=>x==0),"surrounding admitted allocation unchanged");
                Console.WriteLine("TYPED_EBS_HOST_LAYOUT pointer=8 state=40 result=16 observation=104 storage=600 calls=4");
            });
            Run("consumed coordinator and fresh storage cannot reset the global entry history",()=>{
                Core.Storage* s=(Core.Storage*)admitted;Core.Storage* n=(Core.Storage*)second;byte[] before=Bytes(s,4096),zero=Bytes(n,4096);
                Need(!Core.Execute(s,(void*)1)&&!Core.Execute(n,(void*)1),"no rearm");Need(Bytes(s,4096).SequenceEqual(before)&&Bytes(n,4096).SequenceEqual(zero)&&Entries()==4,"all retained and fresh bytes unchanged");
            });
            Run("actual final-map ordering stale retry exhaustion and consumed storage",()=>{
                byte* buffer=(byte*)second;
                Need(MapCore.Execute((void*)0x123456789ABCDEF0UL,&Map,buffer,4096),"actual three-case coordinator");
                Need(mapEntries==5&&mapReentryDenied&&buffer[0]==0xA5,"five real captures and rejected coordinator reentry");
                var names=new[]{"s_single","s_success","s_exhausted"};
                for(int i=0;i<3;i++){
                    var c=(MapCore.Case)typeof(MapCore).GetField(names[i],System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetValue(null)!;
                    ulong count=i==0?1UL:2UL;
                    Need(c.Captures==count&&c.Exits==count&&c.Sequence==3*count&&c.Error==0&&c.Preserved==1,"actual call counts/order and whole consumed-state preservation");
                    Need(c.Status==(i==2?4UL:1UL)&&c.Windows==count&&c.Reuse==(i==0?3UL:6UL),"exact final statuses and reuse refusals");
                    Need(c.First.EnterOrder==1&&c.First.ReturnOrder==2&&c.First.ExitOrder==3&&c.First.CaptureStatus==0,"first capture precedes exit");
                    Need(c.First.Key==(i==2?ulong.MaxValue:0)&&c.First.MapBytes==48&&c.First.DescriptorBytes==48&&c.First.DescriptorVersion==1,"actual captured opaque key/geometry");
                    if(i!=0)Need(c.Second.EnterOrder==4&&c.Second.ReturnOrder==5&&c.Second.ExitOrder==6&&c.Second.Key==c.First.Key&&c.Second.CaptureStatus==0,"fresh capture even when opaque keys repeat");
                    Need(c.First.ExitStatus==(i==0?0UL:0x8000000000000002UL)&&(i==0||c.Second.ExitStatus==(i==1?0UL:0x8000000000000002UL)),"only precise stale status retries");
                }
                Need(!MapCore.Execute((void*)1,&Map,buffer,4096)&&mapEntries==5,"consumed coordinator cannot capture again");
            });
            Console.WriteLine("TEST_PASS m03.typedebs.host.tests");return 0;
        }finally{NativeMemory.AlignedFree(storage);NativeMemory.AlignedFree(fresh);}
    }
}
#else
#endif
