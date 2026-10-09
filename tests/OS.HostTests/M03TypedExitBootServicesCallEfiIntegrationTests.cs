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
internal static class M03TypedExitBootServicesCallEfiIntegrationTests
{

    private const string Flag="EnableM03TypedExitBootServicesCallProbe";

    private static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static string Root()=>HostToolProcess.FindRepositoryRoot();
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scratch(string tag){string path=Path.Combine(Root(),"artifacts/host-tests/m03-typed-ebs-"+tag+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    private sealed record Result(int Exit,bool Timeout,string Output,string Error);
    private static Result Process(string directory,string file,params string[] args)
    {
        Directory.CreateDirectory(directory);var start=new ProcessStartInfo(file){WorkingDirectory=Root(),UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in args)start.ArgumentList.Add(arg);
        DateTime begin=DateTime.UtcNow;File.WriteAllText(Path.Combine(directory,"command.json"),JsonSerializer.Serialize(new{File=file,Arguments=args,WorkingDirectory=Root(),StartedUtc=begin}));
        using var process=System.Diagnostics.Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();bool timeout=!process.WaitForExit(900000);if(timeout){process.Kill(true);process.WaitForExit();}
        string output=stdout.GetAwaiter().GetResult(),error=stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory,"stdout.log"),output);File.WriteAllText(Path.Combine(directory,"stderr.log"),error);
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{NativeExit=process.ExitCode,TimedOut=timeout,StartedUtc=begin,EndedUtc=DateTime.UtcNow}));
        return new(process.ExitCode,timeout,output,error);
    }
    private static Result Tool(params string[] args)
    {
        var result=HostToolProcess.RunTool(args);return new(result.ExitCode,false,result.Output,result.Error);
    }
    private static void Passed(Result r)=>Need(r.Exit==0&&!r.Timeout&&r.Error.Length==0,r.Output+r.Error);
    [HostTest("m03.004.efi conditional source and unchanged typed host authority", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        var defaults=project.Descendants(Flag).ToArray();Need(defaults.Length==1&&defaults[0].Value=="false"&&(string?)defaults[0].Attribute("Condition")=="'$("+Flag+")' == ''","one false default");
        var define=project.Descendants("DefineConstants").Single(x=>x.Value.Contains("M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE",StringComparison.Ordinal));Need((string?)define.Parent?.Attribute("Condition")=="'$("+Flag+")' == 'true'"&&define.Value=="$(DefineConstants);M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE","exact conditional symbol");
        var compile=project.Descendants("Compile").Select(x=>(string)x.Attribute("Include")!).ToArray();Need(compile.Length==OS.Tooling.EfiContracts.CompileItems.Count&&compile.Distinct(StringComparer.Ordinal).Count()==OS.Tooling.EfiContracts.CompileItems.Count,"actual ordered full closure");
        Need(compile.SequenceEqual(OS.Tooling.EfiContracts.CompileItems.Select(item=>item.Split('|')[0]),StringComparer.Ordinal),"exact reviewed compile source order including typed, first-exit and owned sources");
        Need(string.Equals(Hash(Path.Combine(root,"src/OS.Kernel/Boot/TypedNoStdlibExitBootServicesCallAdapter.cs")),OS.Tooling.EfiContracts.SourceIdentities[@"..\OS.Kernel\Boot\TypedNoStdlibExitBootServicesCallAdapter.cs"],StringComparison.OrdinalIgnoreCase),"unchanged host core");
        HostCheck.Registered(typeof(TypedNoStdlibExitBootServicesCallAdapterTests));
        Need(File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Diagnostics/TypedExitBootServicesCallProbeAuthority.cs")).Contains("public const bool EnableTypedExitBootServicesCallProbe = false;",StringComparison.Ordinal),"default false authority");
        string entry=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));int start=entry.IndexOf("    internal static class TypedExitBootServicesCallProbe",StringComparison.Ordinal),end=entry.IndexOf("#endif",start);string core=entry[start..end];
        foreach(string text in new[]{"UnmanagedCallersOnly","MethodImplOptions.NoInlining","s_totalEntries++;","s_totalEntries != 4","s_totalEntries != entries + 1","s_totalEntries != 0","ref *state, Available, key","ref *state, Available, 0","sizeof(TypedExitBootServicesState) != 40","Snapshot before = default","0x8000000000000000UL","0xFFFFFFFFFFFFFFFFUL","0xC000000000000123UL"})Need(core.Contains(text,StringComparison.Ordinal),"actual finite coordinator "+text);
        foreach(string forbidden in new[]{"->BootServices","->ExitBootServices","s_memoryMapSnapshot","new byte[","throw ","string ","Marshal.","Reflection","s_totalEntries ="})Need(!core.Contains(forbidden,StringComparison.Ordinal),"no original firmware or hidden runtime "+forbidden);
        int callback=core.IndexOf("private static ulong Controlled",StringComparison.Ordinal);Need(core.IndexOf("s_totalEntries++;",callback,StringComparison.Ordinal)<core.IndexOf("if (state == null",callback,StringComparison.Ordinal),"count every ABI entry before guard");
        Need(entry.Contains("TypedExitBootServicesCallProbe.Run(imageHandle, console, serial)",StringComparison.Ordinal)&&entry.Contains("#if EXIT_BOOT_SERVICES_FRESH_MAP_PROBE",StringComparison.Ordinal),"real opaque handle and unchanged separate fresh-map profile");
        string[] flags=Regex.Matches(File.ReadAllText(Path.Combine(root,"src/OS/OS.csproj")),@"\$\((Enable\w+Probe)\)").Select(x=>x.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();Need(flags.Length==OS.Tooling.EfiContracts.DiagnosticProbes.Count,"complete profile flags");
        var conditions=project.Descendants("Error").Select(x=>(string)x.Attribute("Condition")!).ToArray();
        Need(conditions.Count(x=>x=="'$("+Flag+")' != '' and '$("+Flag+")' != 'true' and '$("+Flag+")' != 'false'")==1,"one value guard");
        Need(conditions.Contains("'$(EnableM04SeededCollectorChurnProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"),"churn inherits GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"),"constructors inherit GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04InterfaceDispatchProbe)' == 'true' and '$(EnableM04ClassConstructorExecutionProbe)' != 'true'") == 1, "dispatch inherits constructor and GC prerequisites and conflicts");
        Need(conditions.Contains("'$(EnableM03FirmwareAdapterRevocationProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'"),"revocation inherits first-exit prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04X64UnwindMetadataProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' != 'true'") == 1, "unwind inherits original TypeManager prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM03OwnedSubsystemSurvivalProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'") == 1, "exact owned inherited prerequisite EnableM03OwnedSubsystemSurvivalProbe");
        Need(conditions.Count(x => x == "'$(EnableM03OwnedSubsystemPanicProbe)' == 'true' and '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true'") == 1, "exact owned inherited prerequisite EnableM03OwnedSubsystemPanicProbe");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04SimpleThrowCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "simple catch inherits exact GC prerequisites and conflicts");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04SingleFinallyCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "single finally inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04RethrowProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "rethrow inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM05PhysicalMappingProbe)' == 'true' and ('$(ClrosBootPolicy)' != 'Diagnostic' or '$(EnableM03FirstRealExitBootServicesProbe)' != 'true' or '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true')") == 1, "mapping inherits owned first-exit prerequisites");
        foreach(string other in flags.Where(x=>x!=Flag&&x!="EnableM04X64UnwindMetadataProbe"&&x!="EnableM04SeededCollectorChurnProbe"&&x!="EnableM04ClassConstructorExecutionProbe"&&x!="EnableM04InterfaceDispatchProbe"&&x!="EnableM04SimpleThrowCatchProbe"&&x!="EnableM04SingleFinallyCatchProbe"&&x!="EnableM04RethrowProbe"&&x!="EnableM05PhysicalMappingProbe"&&x!="EnableM05OwnedAcpiSelectionProbe"&&x!="EnableM05MonotonicClockProbe"&&x!="EnableM05PciEnumerationProbe"&&x!="EnableM05BarDmaProbe"&&x!="EnableM03OwnedSubsystemSurvivalProbe"&&x!="EnableM03OwnedSubsystemPanicProbe"&&x!="EnableM03FirmwareAdapterRevocationProbe")){Need((other=="EnableM03FirstRealExitBootServicesProbe"||conditions.Count(x=>x=="'$("+Flag+")' == 'true' and '$("+other+")' == 'true'")==1)&&conditions.Count(x=>x=="'$("+other+")' == 'true' and '$("+Flag+")' == 'true'")==1,"exact reciprocal exclusive row "+other);}
        var sourceAdmission=OS.Tooling.EfiSourceAdmission.Inspect(File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Boot/TypedNoStdlibExitBootServicesCallAdapter.cs")),@"..\OS.Kernel\Boot\TypedNoStdlibExitBootServicesCallAdapter.cs");Need(sourceAdmission.Succeeded,"exact full source admission");
    }

    [HostTest("m03.004.efi actual mapped stable states and controlled typed callback", HostLane.Native, 1)]
    private static void TestCapture()
    {
        string root=Root(),directory=Scratch("actual-core");
        string[] sources=["src/OS/Boot/EfiEntry.cs","src/OS/Boot/EfiConsoleAdapter.cs","tests/OS.HostTests/M03TypedExitBootServicesCallEfiIntegrationTests.cs"];
        string attribute="        internal sealed class RuntimeExportAttribute : Attribute\n        {\n            public RuntimeExportAttribute(string entry)\n            {\n            }\n        }";
        Need(File.ReadAllText(Path.Combine(root,"src/OS/Runtime/MinimalRuntime.cs")).Replace("\r\n","\n",StringComparison.Ordinal).Contains(attribute,StringComparison.Ordinal),"unchanged host attribute projection");
        File.WriteAllText(Path.Combine(directory,"Attribute.cs"),"namespace System.Runtime {\n"+attribute+"\n}");
        string items=string.Join('\n',sources.Select(x=>"<Compile Include=\""+SecurityElement.Escape(Path.Combine(root,x))+"\"/>"));
        string project=Path.Combine(directory,"Capture.csproj");
        File.WriteAllText(project,"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion><OutputType>Exe</OutputType><StartupObject>TypedEbsCaptureHost</StartupObject><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>TYPED_EBS_CAPTURE_HOST;M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE</DefineConstants></PropertyGroup><ItemGroup>"+items+"<Compile Include=\"Attribute.cs\"/><ProjectReference Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj"))+"\"/></ItemGroup></Project>");
        string[] inputs=[..sources.Select(x=>Path.Combine(root,x)),Path.Combine(directory,"Attribute.cs"),project];var identities=inputs.Select(path=>new{Path=path,Bytes=new FileInfo(path).Length,Sha256=Hash(path)}).ToArray();File.WriteAllText(Path.Combine(directory,"inputs.json"),JsonSerializer.Serialize(identities));
        Passed(Process(Path.Combine(directory,"build"),"dotnet","build",project,"-c","Release","--nologo","-m:1","--disable-build-servers","-p:UseSharedCompilation=false"));
        Result run=Process(Path.Combine(directory,"run"),"dotnet",Path.Combine(directory,"bin/Release/net10.0/Capture.dll"));Passed(run);
        Need(run.Output.Contains("TEST_PASS m03.typedebs.host.tests",StringComparison.Ordinal)&&run.Output.Split('\n').Count(x=>x.StartsWith("TEST_START m03.typedebs.host ",StringComparison.Ordinal))==4,"complete four-group actual core child");
        Result orphan=Process(Path.Combine(directory,"orphan"),"dotnet",Path.Combine(directory,"bin/Release/net10.0/Capture.dll"),"orphan");Passed(orphan);Need(orphan.Output.Contains("TEST_PASS m03.typedebs.orphan.tests",StringComparison.Ordinal),"separate actual null-active callback entry control");
        foreach(var input in identities)Need(Hash(input.Path)==input.Sha256,"unchanged child input "+input.Path);
    }

    [HostTest("m03.004.efi literal raw grammar and lifecycle controls", HostLane.Fast, 2)]
    private static void TestTranscript()
    {
        string directory=Scratch("transcript"); byte[] good=FirmwareTranscriptControls.Bytes(ProtocolLines);
        var control=new FirmwareTranscriptControls(directory,path=>OS.Tooling.QemuTranscripts.AssertM03TypedExitBootServicesTranscript(path));
        control.Check(good,true);
        for(int i=0;i<ProtocolLines.Length;i++)
        {
            foreach(string replacement in new[]{"",ProtocolLines[i]+" "," "+ProtocolLines[i],ProtocolLines[i]+" extra=1",ProtocolLines[i]+"\0"})
            { string[] copy=[..ProtocolLines];copy[i]=replacement;control.Check(FirmwareTranscriptControls.Bytes(copy),false); }
            string[] lower=[..ProtocolLines];lower[i]=lower[i].ToLowerInvariant();if(lower[i]!=ProtocolLines[i])control.Check(FirmwareTranscriptControls.Bytes(lower),false);
            control.Check(FirmwareTranscriptControls.Bytes(ProtocolLines.Take(i+1).Concat(ProtocolLines.Skip(i))),false);
        }
        foreach(var pair in new (string Old,string New)[]{("storageBytes=600","storageBytes=599"), ("stateBytes=40","stateBytes=48"), ("handle=1311768467463790320","handle=0"), ("callback=2097152","callback=0"), ("storage=3145728","storage=18446744073709551615"), ("state=3145768","state=3145728"), ("key=9223372036854775808","key=0"), ("key=18446744073709551615","key=18446744073709551616"), ("status=9223372036854775810","status=2147483650"), ("status=13835058055282164003","status=291"), ("calls=1","calls=2"), ("phase=2","phase=3"), ("reuseAttempt=9","reuseAttempt=2"), ("reusePrepare=9","reusePrepare=1"), ("unchanged=1","unchanged=0"), ("reentryAttempt=8","reentryAttempt=2"), ("reentryPrepare=8","reentryPrepare=1"), ("reentryUnchanged=1","reentryUnchanged=0"), ("realExitBootServices=false","realExitBootServices=true"), ("regions=4","regions=3"), ("freeBytes=81920","freeBytes=81921"), ("getMemoryMap=2097408","getMemoryMap=0"), ("buffer=4194304","buffer=4194305"), ("capacity=131072","capacity=131071"), ("returned=2 exit=3","returned=3 exit=2"), ("captureStatus=0","captureStatus=1"), ("mapBytes=48","mapBytes=49"), ("descriptorBytes=48","descriptorBytes=32"), ("descriptorVersion=1","descriptorVersion=0"), ("exitStatus=9223372036854775810","exitStatus=2147483650"), ("status=4 windows=2","status=1 windows=2"), ("reuse=6 preserved=1","reuse=6 preserved=0")})
        {
            string text=Encoding.ASCII.GetString(good),changed=text.Replace(pair.Old,pair.New);Need(text!=changed,"mutation changed input");control.Check(Encoding.ASCII.GetBytes(changed),false);
        }
        string[] swap=[..ProtocolLines];(swap[16],swap[17])=(swap[17],swap[16]); control.Check(FirmwareTranscriptControls.Bytes(swap),false);
        control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace("\r\n","\n")),false);
        control.Check(good[..^1],false);control.Check([..good,0],false);control.Check([..good,255],false);
        control.Check(good,false,[255]);control.Check(good,false,Encoding.ASCII.GetBytes("arbitrary"));control.Check(good,false,post:[..FirmwareTranscriptControls.Suffix,65]);
        control.Check(good,true,post:FirmwareTranscriptControls.ShortSuffix);control.Check(good,false,post:[..FirmwareTranscriptControls.ShortSuffix,65]);control.Check(good,false,post:[]);

        control.Check(good,true);
        string[] ordered=[
            "TEST_START efi.main",
            "EFI_CONSOLE_READY",
            "UEFI_MEMORY_MAP_READY",
            "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT",
            "UEFI_MEMORY_MAP_OWNED",
            "BOOT_HANDOFF_READY",
            "UEFI_MEMORY_MAP_NORMALIZED",
            "BOOT_MEMORY_MAP_ACCEPTED",
            "BOOT_MEMORY_REGIONS_READY",
            "BOOT_MEMORY_REGION_COUNT",
            "BOOT_PAGE_ALLOCATOR_READY",
            "BOOT_ACPI_RSDP_HANDOFF_READY",
            "UEFI_ACPI_RSDP_READY",
            "TEST_START m03.004",
            "M03_TYPED_EBS_IMAGE",
            "M03_TYPED_EBS_ADMISSION",
            "M03_TYPED_EBS_CASE id=0",
            "M03_TYPED_EBS_CASE id=1",
            "M03_TYPED_EBS_CASE id=2",
            "M03_TYPED_EBS_CASE id=3",
            "CLROS_M03_004_READY",
            "TEST_PASS m03.004",
            "TEST_START m03.005",
            "M03_FINAL_MAP_IMAGE",
            "M03_FINAL_MAP_CALL case=0 index=0",
            "M03_FINAL_MAP_RESULT case=0",
            "CLROS_M03_005_READY",
            "TEST_PASS m03.005",
            "TEST_START m03.006",
            "M03_FINAL_MAP_CALL case=1 index=0",
            "M03_FINAL_MAP_CALL case=1 index=1",
            "M03_FINAL_MAP_RESULT case=1",
            "M03_FINAL_MAP_CALL case=2 index=0",
            "M03_FINAL_MAP_CALL case=2 index=1",
            "M03_FINAL_MAP_RESULT case=2",
            "CLROS_M03_006_READY",
            "TEST_PASS m03.006",
            "TEST_START boot.sequence",
            "BOOT_PHASE PhaseA.Audit START",
            "BOOT_PHASE PhaseA.Audit PASS",
            "BOOT_PHASE PhaseA.Diagnostics START",
            "BOOT_PHASE PhaseA.Diagnostics PASS",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS"
        ];
        var runner=new StandardFirmwareRunnerControls(directory,ProtocolLines,ordered,["OS_SMOKE_FAIL","TEST_FAIL","UEFI_EBS_CALLED","UEFI_BOOT_SERVICES_EXITED"],Flag,false);
        runner.CompleteMatrix();

        Console.WriteLine("TRANSCRIPT_CONTROLS m03.typedebs cases="+(control.Cases+runner.Cases)+" stderrWhitespaceRejected=true");
    }

    private static readonly string[] ProtocolLines = [
        "TEST_START efi.main",
        "EFI_CONSOLE_READY",
        "UEFI_MEMORY_MAP_READY",
        "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4",
        "UEFI_MEMORY_MAP_OWNED",
        "BOOT_HANDOFF_READY",
        "UEFI_MEMORY_MAP_NORMALIZED",
        "BOOT_MEMORY_MAP_ACCEPTED",
        "BOOT_MEMORY_REGIONS_READY",
        "BOOT_MEMORY_REGION_COUNT 4",
        "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0",
        "BOOT_ACPI_RSDP_HANDOFF_READY",
        "UEFI_ACPI_RSDP_READY",
        "TEST_START m03.004",
        "M03_TYPED_EBS_IMAGE handle=1311768467463790320 callback=2097152 storage=3145728 storageBytes=600 stateBytes=40",
        "M03_TYPED_EBS_ADMISSION default=6 missingFunction=3 missingHandle=4 simulated=5 exited=5 readyDenied=5 readyPrepare=7 unchanged=1 calls=0",
        "M03_TYPED_EBS_CASE id=0 state=3145728 handle=1311768467463790320 key=0 status=0 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=0 reentryPrepare=0 reentryUnchanged=0",
        "M03_TYPED_EBS_CASE id=1 state=3145768 handle=1311768467463790320 key=9223372036854775808 status=9223372036854775810 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=0 reentryPrepare=0 reentryUnchanged=0",
        "M03_TYPED_EBS_CASE id=2 state=3145808 handle=1311768467463790320 key=18446744073709551615 status=13835058055282164003 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=0 reentryPrepare=0 reentryUnchanged=0",
        "M03_TYPED_EBS_CASE id=3 state=3145848 handle=1311768467463790320 key=9223372036854775808 status=0 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=8 reentryPrepare=8 reentryUnchanged=1",
        "CLROS_M03_004_READY controlled=true realExitBootServices=false states=4 calls=4 reuse=4 reentry=1 mode=0",
        "TEST_PASS m03.004",
        "TEST_START m03.005",
        "M03_FINAL_MAP_IMAGE handle=1311768467463790320 getMemoryMap=2097408 buffer=4194304 capacity=131072",
        "M03_FINAL_MAP_CALL case=0 index=0 enter=1 returned=2 exit=3 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=0",
        "M03_FINAL_MAP_RESULT case=0 status=1 windows=1 captures=1 exits=1 reuse=3 preserved=1",
        "CLROS_M03_005_READY controlled=true realExitBootServices=false captures=1 exits=1 reuseRejected=true",
        "TEST_PASS m03.005",
        "TEST_START m03.006",
        "M03_FINAL_MAP_CALL case=1 index=0 enter=1 returned=2 exit=3 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=9223372036854775810",
        "M03_FINAL_MAP_CALL case=1 index=1 enter=4 returned=5 exit=6 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=0",
        "M03_FINAL_MAP_RESULT case=1 status=1 windows=2 captures=2 exits=2 reuse=6 preserved=1",
        "M03_FINAL_MAP_CALL case=2 index=0 enter=1 returned=2 exit=3 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=9223372036854775810",
        "M03_FINAL_MAP_CALL case=2 index=1 enter=4 returned=5 exit=6 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=9223372036854775810",
        "M03_FINAL_MAP_RESULT case=2 status=4 windows=2 captures=2 exits=2 reuse=6 preserved=1",
        "CLROS_M03_006_READY controlled=true realExitBootServices=false cases=2 maxAttempts=2 captures=4 exits=4 exhausted=1",
        "TEST_PASS m03.006",
        "TEST_START boot.sequence",
        "BOOT_PHASE PhaseA.Audit START",
        "BOOT_PHASE PhaseA.Audit PASS",
        "BOOT_PHASE PhaseA.Diagnostics START",
        "BOOT_PHASE PhaseA.Diagnostics PASS",
        "OS_BOOT_OK",
        "TEST_PASS boot.sequence",
        "OS_SMOKE_PASS"];
    [HostTest("m03.004.efi profile options and actual complete marker streamer", HostLane.Fast, 3)]
    private static void TestProfile()
    {
        string root=Root(),directory=Scratch("profile"),script=Path.Combine(root,"tools/OS.Tooling/QemuProfiles.cs"),before=Hash(script);
        Result Dry(params string[] args)=>Tool(["qemu","smoke","-BootPolicy","Diagnostic","-DryRun",..args]);
        string[] positive=["-BuildEfi","-"+Flag];
        Result result=Dry(positive);Passed(result);Need(result.Output.Contains("TEST_PASS qemu.runner.dryrun",StringComparison.Ordinal),"actual dry-run terminal");
        Result preflight=Tool("efi","build","-PreflightOnly","-"+Flag,"-LogPath",Path.Combine(directory,"enabled-preflight.log"));
        Passed(preflight);Need(preflight.Output.Split('\n').Count(x=>x.TrimEnd('\r')=="EFI_PREFLIGHT_OK")==1,"actual enabled producer preflight succeeds");
        string[] Vector(string prefix)=>result.Output.Split('\n').Single(x=>x.StartsWith(prefix+" ",StringComparison.Ordinal))[(prefix.Length+1)..].TrimEnd('\r').Split(';').Select(x=>x.Trim()).ToArray();
        var required=Vector("REQUIRED_MARKERS");var ordered=Vector("ORDERED_MARKERS");var unique=Vector("UNIQUE_MARKERS");var forbidden=Vector("FORBIDDEN_MARKERS");
        foreach(var vector in new[]{required,ordered,unique})Need(vector.Length==ProtocolLines.Length&&vector.Distinct(StringComparer.Ordinal).Count()==ProtocolLines.Length,"complete canonical unique vectors");
        Need(required.Order(StringComparer.Ordinal).SequenceEqual(ordered.Order(StringComparer.Ordinal),StringComparer.Ordinal)&&ordered.SequenceEqual(unique,StringComparer.Ordinal),"actual complete set and ordered/unique sequence");
        OS.Tools.QemuRunner.SerialMarkerCompletionResult Stream(string[] lines){var state=new OS.Tools.QemuRunner.SerialMarkerStateMachine(required,forbidden,orderedMarkers:ordered,uniqueMarkers:unique);Need(state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n",lines)+"\r\n")).Succeeded,"actual streamer Feed");return state.Complete();}
        var accepted=Stream(ProtocolLines);Need(accepted.Passed&&accepted.LinesProcessed==ProtocolLines.Length&&accepted.OrderedSeenCount==ProtocolLines.Length&&accepted.UniqueSeenCount==ProtocolLines.Length,"genuine full-token streamer success");
        foreach(string forbiddenTransition in new[]{"UEFI_EBS_CALLED","UEFI_BOOT_SERVICES_EXITED"}){
            var state=new OS.Tools.QemuRunner.SerialMarkerStateMachine(required,forbidden,orderedMarkers:ordered,uniqueMarkers:unique);
            var feed=state.Feed(Encoding.ASCII.GetBytes(forbiddenTransition+"\r\n"));Need(!feed.Succeeded||!state.Complete().Passed,"real transition marker is forbidden");
        }
        foreach(int index in Enumerable.Range(13,24)){
            var missing=Stream(ProtocolLines.Where((_,i)=>i!=index).ToArray());Need(!missing.Passed&&missing.MissingRequiredMarkers!.Count>0,"missing controlled row");
            var duplicate=Stream(ProtocolLines.Take(index+1).Concat(ProtocolLines.Skip(index)).ToArray());Need(!duplicate.Passed&&duplicate.DuplicateUniqueOccurrenceCount==1,"duplicate controlled row");
            string[] swapped=[..ProtocolLines];(swapped[index],swapped[index+1])=(swapped[index+1],swapped[index]);Need(Stream(swapped).OutOfOrderOccurrenceCount>0,"reordered controlled row");
        }
        void Reject(string[] args,string reason){Result failure=Dry(args);Need(failure.Exit!=0&&!failure.Timeout&&(failure.Output+failure.Error).Contains(reason,StringComparison.Ordinal),"profile rejection "+reason);}
        string project=File.ReadAllText(Path.Combine(root,"src/OS/OS.csproj"));string[] flags=Regex.Matches(project,@"\$\((Enable\w+Probe)\)").Select(x=>x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();Need(flags.Length==OS.Tooling.EfiContracts.DiagnosticProbes.Count,"complete profile flags");
        foreach(string other in flags.Where(x=>x!=Flag))Reject([..positive,"-"+other],EfiProfileTestControls.MissingParentReason([Flag, other]) ?? (other=="EnableM03FirstRealExitBootServicesProbe"?"m03_first_exit_requires property=EnableM01LoadedImageStackProbe":other=="EnableM04GcStaticRootMaterializationProbe"?"m04_gc_static_profile_invalid property=EnableM01LoadedImageStackProbe":"m03_typed_ebs_profile_conflict property="+other));
        Reject(["-"+Flag],"m03_typed_ebs_requires_build_and_original_capture");
        Reject([..positive,"-SkipImageBuild"],"m03_typed_ebs_requires_build_and_original_capture");
        Reject([..positive,"-M01RawObservationLog",""],"m03_typed_ebs_requires_build_and_original_capture");
        Reject([..positive,"-RequireNativeAppMarkers"],"m03_typed_ebs_requires_build_and_original_capture");
        foreach(string option in new[]{"RequireMarker","RequireOrderedMarker","RequireUniqueMarker","ForbidMarker","MarkerPreset"})Reject([..positive,"-"+option,"CLROS_M03_004_READY"],"m03_typed_ebs_forbids_override");
        Reject(["-RequireMarker","CLROS_M03_004_READY"],"m03_typed_ebs_requires_explicit_profile");
        foreach(bool build in new[]{true,false}){
            var admitted=HostToolProcess.ValidateEfiProfile(build,"-"+Flag,"-EnableM01LoadedImageStackProbe");
            Result failure=new(admitted.ExitCode,false,admitted.Output,admitted.Error);
            Need(failure.Exit!=0&&!failure.Timeout&&(failure.Output+failure.Error).Contains("m03_typed_ebs_profile_conflict property=EnableM01LoadedImageStackProbe",StringComparison.Ordinal),"closed producer/verifier build="+build);
        }
        Passed(Dry());Need(Hash(script)==before,"unchanged profile input");
    }
    [HostTest("m03.004.efi default and enabled no-stdlib artifacts", HostLane.Native, 4)]
    private static void TestArtifacts()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));int compile=project.Descendants("Compile").Count();
        foreach(bool enabled in new[]{false,true})
        {
            string directory=Scratch(enabled?"enabled":"default"),artifact=Path.Combine(root,"artifacts/efi/issue546-"+Guid.NewGuid().ToString("N")+".EFI");
            Result result=Tool(["efi","build","-EfiOutputPath",artifact,"-LogPath",Path.Combine(directory,"publish.log"),..(enabled?new[]{"-EnableM03TypedExitBootServicesCallProbe"}:Array.Empty<string>())]);Passed(result);
            Need(result.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal)&&result.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),"actual producer/verifier");
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(artifact+suffix,Path.Combine(directory,"image.EFI"+suffix));
            foreach(var pair in new[]{("src/OS/bin/Release/net10.0/win-x64/OS.dll","OS.dll"),("src/OS/obj/Release/net10.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net10.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(root,pair.Item1),Path.Combine(directory,pair.Item2));
            using var json=JsonDocument.Parse(File.ReadAllText(artifact+".link.json"));var evaluation=json.RootElement.GetProperty("postRestoreEvaluation");
            Need(evaluation.GetProperty("compileCount").GetInt32()==compile,"actual evaluated Compile membership");
            foreach(var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray())Need(string.Equals(Hash(Path.Combine(root,input.GetProperty("path").GetString()!)),input.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"actual raw source identity");
            foreach(var property in evaluation.GetProperty("properties").EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)))
                Need(property.Value.GetString()==(enabled&&property.Name==Flag?"true":"false"),"actual profile "+property.Name);
            using var stream=File.OpenRead(Path.Combine(directory,"OS.dll"));using var pe=new PEReader(stream);var metadata=pe.GetMetadataReader();Need(metadata.AssemblyReferences.Count==0,"zero AssemblyRefs");
            var types=metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Where(t=>metadata.GetString(t.Name)=="TypedExitBootServicesCallProbe").ToArray();
            Need(types.Length==(enabled?1:0),"conditional controlled type absent/default and present/enabled");
            if(enabled)foreach(string name in new[]{"Execute","Controlled","Call","Same","Save","Run"}){
                var methods=types[0].GetMethods().Where(h=>metadata.GetString(metadata.GetMethodDefinition(h).Name)==name).ToArray();Need(methods.Length==1&&metadata.GetMethodDefinition(methods[0]).RelativeVirtualAddress!=0,"actual controlled method "+name);
            }
            string sourceDirectory=Path.Combine(directory,"source-inputs");Directory.CreateDirectory(sourceDirectory);
            foreach(var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray()){
                string relative=input.GetProperty("path").GetString()!,destination=Path.Combine(sourceDirectory,relative);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(Path.Combine(root,relative),destination);
            }
            File.WriteAllText(Path.Combine(directory,"artifact-identities.json"),JsonSerializer.Serialize(Directory.GetFiles(directory,"*",SearchOption.AllDirectories).Select(path=>new{Path=path,Bytes=new FileInfo(path).Length,Sha256=Hash(path)}).ToArray()));
        }
    }

}
#endif
