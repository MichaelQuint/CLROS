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
    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("conditional source and unchanged typed host authority",TestSource);
        Run("actual mapped stable states and controlled typed callback",TestCapture);
        Run("literal raw grammar and lifecycle controls",TestTranscript);
        Run("profile options and actual complete marker streamer",TestProfile);
        Run("default and enabled no-stdlib artifacts",TestArtifacts);
    }
    private const string Flag="EnableM03TypedExitBootServicesCallProbe";
    private static void Run(string name,Action action)
    {Console.WriteLine("TEST_START m03.004.efi "+name);try{action();Console.WriteLine("TEST_PASS m03.004.efi "+name);}catch(Exception e){Console.WriteLine("TEST_FAIL m03.004.efi "+name+" "+e);throw;}}
    private static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static string Root()=>HostToolProcess.FindRepositoryRoot();
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scratch(string tag){string path=Path.Combine(Root(),"artifacts/host-tests/m03-typed-ebs-"+tag+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    private sealed record Result(int Exit,bool Timeout,string Output,string Error);
    private static Result Process(string directory,string file,params string[] args)
    {
        Directory.CreateDirectory(directory);var start=new ProcessStartInfo(file){WorkingDirectory=Root(),UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string key in start.Environment.Keys.Where(x=>string.Equals(x,"PSModulePath",StringComparison.OrdinalIgnoreCase)).ToArray())start.Environment.Remove(key);
        start.Environment["PSModulePath"]=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WindowsPowerShell/Modules")+";"+Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32/WindowsPowerShell/v1.0/Modules");
        foreach(string arg in args)start.ArgumentList.Add(arg);
        DateTime begin=DateTime.UtcNow;File.WriteAllText(Path.Combine(directory,"command.json"),JsonSerializer.Serialize(new{File=file,Arguments=args,WorkingDirectory=Root(),StartedUtc=begin,EffectivePSModulePath=start.Environment["PSModulePath"]}));
        using var process=System.Diagnostics.Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();bool timeout=!process.WaitForExit(900000);if(timeout){process.Kill(true);process.WaitForExit();}
        string output=stdout.GetAwaiter().GetResult(),error=stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory,"stdout.log"),output);File.WriteAllText(Path.Combine(directory,"stderr.log"),error);
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{NativeExit=process.ExitCode,TimedOut=timeout,StartedUtc=begin,EndedUtc=DateTime.UtcNow}));
        return new(process.ExitCode,timeout,output,error);
    }
    private static void Passed(Result r)=>Need(r.Exit==0&&!r.Timeout&&r.Error.Length==0,r.Output+r.Error);
    private static void TestSource()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        var defaults=project.Descendants(Flag).ToArray();Need(defaults.Length==1&&defaults[0].Value=="false"&&(string?)defaults[0].Attribute("Condition")=="'$("+Flag+")' == ''","one false default");
        var define=project.Descendants("DefineConstants").Single(x=>x.Value.Contains("M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE",StringComparison.Ordinal));Need((string?)define.Parent?.Attribute("Condition")=="'$("+Flag+")' == 'true'"&&define.Value=="$(DefineConstants);M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE","exact conditional symbol");
        var compile=project.Descendants("Compile").Select(x=>(string)x.Attribute("Include")!).ToArray();Need(compile.Length==124&&compile.Distinct(StringComparer.Ordinal).Count()==124,"actual ordered full closure");
        Need(compile.Skip(93).Take(11).SequenceEqual(new[]{@"..\OS.Kernel\Boot\TypedNoStdlibExitBootServicesCallAdapter.cs",@"..\OS.Kernel\Boot\NoAllocationOrUnrelatedFirmwareCallOccursBetween.cs",@"..\OS.Kernel\Boot\BoundedRetryStaleKeyStatus.cs",@"..\OS.Kernel\Boot\FirstOptRealExitBootServicesTransition.cs",@"..\OS.Kernel\Platform\PostFirmwarePhysicalMappingExplicitUCWCWB.cs",@"..\OS.Kernel\Boot\OwnedAllocatorDescriptorTablesACPISnapshotPanicOutput.cs",@"..\OS.Kernel\Boot\BootSequence.cs",@"..\OS.Kernel\Diagnostics\Probes.cs",@"..\..\std\no-runtime\shared\Hash\Crc32.cs",@"..\OS.Kernel\Platform\FirmwareServicesMode.cs",@"..\OS.Kernel\Diagnostics\TypedExitBootServicesCallProbeAuthority.cs"},StringComparer.Ordinal),"eleven exact typed, first-exit, mapping and owned source positions");
        Need(Hash(Path.Combine(root,"src/OS.Kernel/Boot/TypedNoStdlibExitBootServicesCallAdapter.cs"))=="7227E860B5E4F97D47C9C269B889C975E30F21C506F9ECB51424F5417E2945DB","unchanged host core");
        Need(Hash(Path.Combine(root,"tests/OS.HostTests/TypedNoStdlibExitBootServicesCallAdapterTests.cs"))=="98F5A225E855D311A54D8792C7619C03365051004A6200F34DD48107D1E3F587","unchanged whole host fixture");
        Need(File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Diagnostics/TypedExitBootServicesCallProbeAuthority.cs")).Contains("public const bool EnableTypedExitBootServicesCallProbe = false;",StringComparison.Ordinal),"default false authority");
        string entry=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));int start=entry.IndexOf("    internal static class TypedExitBootServicesCallProbe",StringComparison.Ordinal),end=entry.IndexOf("#endif",start);string core=entry[start..end];
        foreach(string text in new[]{"UnmanagedCallersOnly","MethodImplOptions.NoInlining","s_totalEntries++;","s_totalEntries != 4","s_totalEntries != entries + 1","s_totalEntries != 0","ref *state, Available, key","ref *state, Available, 0","sizeof(TypedExitBootServicesState) != 40","Snapshot before = default","0x8000000000000000UL","0xFFFFFFFFFFFFFFFFUL","0xC000000000000123UL"})Need(core.Contains(text,StringComparison.Ordinal),"actual finite coordinator "+text);
        foreach(string forbidden in new[]{"->BootServices","->ExitBootServices","s_memoryMapSnapshot","new byte[","throw ","string ","Marshal.","Reflection","s_totalEntries ="})Need(!core.Contains(forbidden,StringComparison.Ordinal),"no original firmware or hidden runtime "+forbidden);
        int callback=core.IndexOf("private static ulong Controlled",StringComparison.Ordinal);Need(core.IndexOf("s_totalEntries++;",callback,StringComparison.Ordinal)<core.IndexOf("if (state == null",callback,StringComparison.Ordinal),"count every ABI entry before guard");
        Need(entry.Contains("TypedExitBootServicesCallProbe.Run(imageHandle, console, serial)",StringComparison.Ordinal)&&entry.Contains("#if EXIT_BOOT_SERVICES_FRESH_MAP_PROBE",StringComparison.Ordinal),"real opaque handle and unchanged separate fresh-map profile");
        string[] flags=Regex.Matches(File.ReadAllText(Path.Combine(root,"src/OS/OS.csproj")),@"\$\((Enable\w+Probe)\)").Select(x=>x.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();Need(flags.Length==54,"actual54 flags");
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
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04RethrowProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "rethrow inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x=>x=="'$(EnableM05PhysicalMappingProbe)' == 'true' and ('$(ClrosBootPolicy)' != 'Diagnostic' or '$(EnableM03FirstRealExitBootServicesProbe)' != 'true' or '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true')")==1,"mapping inherits first-exit conflicts");
        foreach(string other in flags.Where(x=>x!=Flag&&x!="EnableM05PhysicalMappingProbe"&&x!="EnableM04X64UnwindMetadataProbe"&&x!="EnableM04SeededCollectorChurnProbe"&&x!="EnableM04ClassConstructorExecutionProbe"&&x!="EnableM04InterfaceDispatchProbe"&&x!="EnableM04SimpleThrowCatchProbe"&&x!="EnableM04SingleFinallyCatchProbe"&&x!="EnableM04RethrowProbe"&&x!="EnableM03OwnedSubsystemSurvivalProbe"&&x!="EnableM03OwnedSubsystemPanicProbe"&&x!="EnableM03FirmwareAdapterRevocationProbe")){Need((other=="EnableM03FirstRealExitBootServicesProbe"||conditions.Count(x=>x=="'$("+Flag+")' == 'true' and '$("+other+")' == 'true'")==1)&&conditions.Count(x=>x=="'$("+other+")' == 'true' and '$("+Flag+")' == 'true'")==1,"exact reciprocal exclusive row "+other);}
        string build=File.ReadAllText(Path.Combine(root,"scripts/build-efi-shell.ps1"));Need(build.Contains("reviewed_typed_ebs_adapter_source_identity",StringComparison.Ordinal)&&build.Contains("7227e860b5e4f97d47c9c269b889c975e30f21c506f9ecb51424f5417e2945db",StringComparison.Ordinal),"exact full source admission");
    }

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

    private static void TestTranscript()
    {
        string directory=Scratch("transcript"),script=Path.Combine(Root(),"scripts/run-qemu-smoke.ps1"),copy=Path.Combine(directory,"run-qemu-smoke.ps1"),control=Path.Combine(directory,"controls.ps1");
        File.Copy(script,copy);File.WriteAllText(control,TranscriptScript,new UTF8Encoding(false));
        var inputs=new[]{copy,control}.Select(path=>new{Path=path,Bytes=new FileInfo(path).Length,Sha256=Hash(path)}).ToArray();
        File.WriteAllText(Path.Combine(directory,"inputs.json"),JsonSerializer.Serialize(inputs));
        Result run=Process(Path.Combine(directory,"run"),"powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",control,"-Source",copy,"-Directory",directory);Passed(run);
        Need(run.Output.Contains("TEST_PASS m03.typedebs.transcript controls=",StringComparison.Ordinal)&&run.Output.Contains("stderrWhitespaceRejected=true",StringComparison.Ordinal),"complete strict raw/lifecycle controls");
        foreach(var input in inputs)Need(Hash(input.Path)==input.Sha256,"unchanged transcript input "+input.Path);
        Need(Hash(script)==Hash(copy),"original production parser remained unchanged");
    }

    private static readonly string TranscriptScript = """
        param([string]$Source,[string]$Directory)
        $ErrorActionPreference='Stop'
        $tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'script syntax'}
        foreach($name in @('Read-M03MemoryMapCaptureRecord','Assert-M03TypedExitBootServicesTranscript','Assert-M03TypedExitBootServicesRunnerResult','Invoke-NativeCommand')){
            $function=$ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true)
            if($function.Count -ne 1){throw ('function inventory '+$name)}
            Invoke-Expression $function[0].Extent.Text
        }
        $root=$Directory
        $lines=@(
        'TEST_START efi.main',
        'EFI_CONSOLE_READY',
        'UEFI_MEMORY_MAP_READY',
        'UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4',
        'UEFI_MEMORY_MAP_OWNED',
        'BOOT_HANDOFF_READY',
        'UEFI_MEMORY_MAP_NORMALIZED',
        'BOOT_MEMORY_MAP_ACCEPTED',
        'BOOT_MEMORY_REGIONS_READY',
        'BOOT_MEMORY_REGION_COUNT 4',
        'BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0',
        'BOOT_ACPI_RSDP_HANDOFF_READY',
        'UEFI_ACPI_RSDP_READY',
        'TEST_START m03.004',
        'M03_TYPED_EBS_IMAGE handle=1311768467463790320 callback=2097152 storage=3145728 storageBytes=600 stateBytes=40',
        'M03_TYPED_EBS_ADMISSION default=6 missingFunction=3 missingHandle=4 simulated=5 exited=5 readyDenied=5 readyPrepare=7 unchanged=1 calls=0',
        'M03_TYPED_EBS_CASE id=0 state=3145728 handle=1311768467463790320 key=0 status=0 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=0 reentryPrepare=0 reentryUnchanged=0',
        'M03_TYPED_EBS_CASE id=1 state=3145768 handle=1311768467463790320 key=9223372036854775808 status=9223372036854775810 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=0 reentryPrepare=0 reentryUnchanged=0',
        'M03_TYPED_EBS_CASE id=2 state=3145808 handle=1311768467463790320 key=18446744073709551615 status=13835058055282164003 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=0 reentryPrepare=0 reentryUnchanged=0',
        'M03_TYPED_EBS_CASE id=3 state=3145848 handle=1311768467463790320 key=9223372036854775808 status=0 calls=1 phase=2 reuseAttempt=9 reusePrepare=9 unchanged=1 reentryAttempt=8 reentryPrepare=8 reentryUnchanged=1',
        'CLROS_M03_004_READY controlled=true realExitBootServices=false states=4 calls=4 reuse=4 reentry=1 mode=0',
        'TEST_PASS m03.004',
        'TEST_START m03.005',
        'M03_FINAL_MAP_IMAGE handle=1311768467463790320 getMemoryMap=2097408 buffer=4194304 capacity=131072',
        'M03_FINAL_MAP_CALL case=0 index=0 enter=1 returned=2 exit=3 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=0',
        'M03_FINAL_MAP_RESULT case=0 status=1 windows=1 captures=1 exits=1 reuse=3 preserved=1',
        'CLROS_M03_005_READY controlled=true realExitBootServices=false captures=1 exits=1 reuseRejected=true',
        'TEST_PASS m03.005',
        'TEST_START m03.006',
        'M03_FINAL_MAP_CALL case=1 index=0 enter=1 returned=2 exit=3 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=9223372036854775810',
        'M03_FINAL_MAP_CALL case=1 index=1 enter=4 returned=5 exit=6 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=0',
        'M03_FINAL_MAP_RESULT case=1 status=1 windows=2 captures=2 exits=2 reuse=6 preserved=1',
        'M03_FINAL_MAP_CALL case=2 index=0 enter=1 returned=2 exit=3 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=9223372036854775810',
        'M03_FINAL_MAP_CALL case=2 index=1 enter=4 returned=5 exit=6 captureStatus=0 key=0 mapBytes=48 descriptorBytes=48 descriptorVersion=1 exitStatus=9223372036854775810',
        'M03_FINAL_MAP_RESULT case=2 status=4 windows=2 captures=2 exits=2 reuse=6 preserved=1',
        'CLROS_M03_006_READY controlled=true realExitBootServices=false cases=2 maxAttempts=2 captures=4 exits=4 exhausted=1',
        'TEST_PASS m03.006',
        'TEST_START boot.sequence',
        'BOOT_PHASE PhaseA.Audit START',
        'BOOT_PHASE PhaseA.Audit PASS',
        'BOOT_PHASE PhaseA.Diagnostics START',
        'BOOT_PHASE PhaseA.Diagnostics PASS',
        'OS_BOOT_OK',
        'TEST_PASS boot.sequence',
        'OS_SMOKE_PASS')
        $preamble=[Convert]::FromBase64String('G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg==')
        $suffix=[Convert]::FromBase64String('QmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAwICJVaUFwcCIgZnJvbSBGdig3Q0I4QkRDOS1GOEVCLTRGMzQtQUFFQS0zRUU0QUY2NTE2QTEpL0Z2RmlsZSg0NjJDQUEyMS03NjE0LTQ1MDMtODM2RS04QUI2RjQ2NjIzMzEpDQpCZHNEeGU6IHN0YXJ0aW5nIEJvb3QwMDAwICJVaUFwcCIgZnJvbSBGdig3Q0I4QkRDOS1GOEVCLTRGMzQtQUFFQS0zRUU0QUY2NTE2QTEpL0Z2RmlsZSg0NjJDQUEyMS03NjE0LTQ1MDMtODM2RS04QUI2RjQ2NjIzMzEpDQobWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMG0bWzMwbRtbNDBtG1swMTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDI7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzAzOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDU7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA2OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDg7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA5OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTE7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzEyOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTQ7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTc7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE4OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjA7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIxOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjM7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzI0OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syNTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swMTswMUgbWzBtG1szNG0bWzQ3bRtbMDE7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzAyOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swMzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDQ7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA1OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDE7MDFIG1swMjswMkhTdGFuZGFyZCBQQyAoUTM1ICsgSUNIOSwgMjAwOSkbWzAzOzAySHBjLXEzNS0xMS4xG1swMzs1NEgyLjAwIEdIehtbMDQ7MDJIZWRrMi1zdGFibGUyMDI0MDgtcHJlYnVpbHQucWVtdS5vcmcbWzA0OzU0SDI1NiBNQiBSQU0bWzA1OzAySBtbMDU7NTRIG1swNjswMkgbWzA2OzU0SBtbMG0bWzM3bRtbNDBtG1syMTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjI7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIzOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syNDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjE7MDFIG1swbRtbMzBtG1s0MG0bWzI1OzUzSCAgG1syNTs1NEggIBtbMjU7NTVIICAbWzI1OzU2SCAgG1syNTs1N0ggIBtbMjU7NThIICAbWzI1OzU5SCAgG1syNTs2MEggIBtbMjU7NjFIICAbWzI1OzYySCAgG1syNTs2M0ggIBtbMjU7NjRIICAbWzI1OzY1SCAgG1syNTs2NkggIBtbMjU7NjdIICAbWzI1OzY4SCAgG1syNTs2OUggIBtbMjU7NzBIICAbWzI1OzcxSCAgG1syNTs3MkggIBtbMjU7NzNIICAbWzBtG1szMG0bWzQ3bRtbMDc7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA4OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTA7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzExOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTM7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE0OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTY7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxODswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTk7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIwOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNzswMUgbWzA3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzdtG1s0MG0bWzA4OzMxSDxTdGFuZGFyZCBFbmdsaXNoPhtbMG0bWzMwbRtbNDdtG1swODs0OUggICAgICAgICAbWzA4OzAxSCAgIBtbMDg7MDRIU2VsZWN0IExhbmd1YWdlICAgICAgICAgICAgG1swbRtbMzRtG1s0N20bWzA5OzAxSCAgIBtbMDk7MDRIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzBtG1s0N20bWzEwOzAxSCAgIBtbMTA7MDJI4pa6G1sxMDswNEhEZXZpY2UgTWFuYWdlciAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzExOzAxSCAgIBtbMTE7MDJI4pa6G1sxMTswNEhCb290IE1hbmFnZXIgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzEyOzAxSCAgIBtbMTI7MDJI4pa6G1sxMjswNEhCb290IE1haW50ZW5hbmNlIE1hbmFnZXIgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzBtG1szNG0bWzQ3bRtbMTM7MDFIICAgG1sxMzswNEggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzBtG1szMG0bWzQ3bRtbMTQ7MDFIICAgG1sxNDswNEhDb250aW51ZSAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzAxSCAgIBtbMTU7MDRIUmVzZXQgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTg7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIwOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzdtG1s0MG0bWzIzOzAySCAbWzIyOzAySCAbWzIyOzUzSCAgICAgICAgICAgICAgICAgICAgICAgICAbWzIyOzI3SCAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMzs1M0ggICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIzOzAzSOKGkeKGkz1Nb3ZlIEhpZ2hsaWdodCAgICAgICAbWzIyOzAzSCAgICAgICAgICAgICAgICAgICAgICAgIBtbMjM7MjdIPEVudGVyPj1TZWxlY3QgRW50cnkgICAgICAbWzBtG1szNG0bWzQ3bRtbMDg7NThIVGhpcyBpcyB0aGUgb3B0aW9uICAgICAbWzA5OzU4SG9uZSBhZGp1c3RzIHRvIGNoYW5nZSAgG1sxMDs1OEh0aGUgbGFuZ3VhZ2UgZm9yIHRoZSAgIBtbMTE7NThIY3VycmVudCBzeXN0ZW0gICAgICAgICAbWzEyOzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxMzs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTQ7NThIICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxNjs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTc7NThIICAgICAgICAgICAgICAgICAgICAgICAbWzE4OzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxOTs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTk7ODBI')
        $serialPath=Join-Path $Directory 'serial.log';$summaryPath=Join-Path $Directory 'summary.log';$cases=0
        function Bytes([string[]]$value){return [Text.Encoding]::ASCII.GetBytes(($value -join "`r`n")+"`r`n")}
        $good=Bytes $lines
        function Check([byte[]]$protocol,[bool]$expected,[byte[]]$pre=$preamble,[byte[]]$post=$suffix){
            [IO.File]::WriteAllBytes($serialPath,[byte[]]($pre+$protocol+$post));$accepted=$true
            try{Assert-M03TypedExitBootServicesTranscript $serialPath}catch{$accepted=$false}
            if($accepted -ne $expected){throw ('transcript case '+$script:cases+' expected='+$expected)};$script:cases++
        }
        Check $good $true
        for($i=0;$i -lt 45;$i++){
            foreach($replacement in @('',($lines[$i]+' '),(' '+$lines[$i]),($lines[$i]+' extra=1'),($lines[$i]+"`0"))){$copy=@($lines);$copy[$i]=$replacement;Check (Bytes $copy) $false}
            $copy=@($lines);$copy[$i]=$copy[$i].ToLowerInvariant();if($copy[$i] -cne $lines[$i]){Check (Bytes $copy) $false}
            $copy=@($lines[0..$i])+@($lines[$i..44]);Check (Bytes $copy) $false
        }
        foreach($pair in @(@('storageBytes=600','storageBytes=599'),@('stateBytes=40','stateBytes=48'),@('handle=1311768467463790320','handle=0'),@('callback=2097152','callback=0'),@('storage=3145728','storage=18446744073709551615'),@('state=3145768','state=3145728'),@('key=9223372036854775808','key=0'),@('key=18446744073709551615','key=18446744073709551616'),@('status=9223372036854775810','status=2147483650'),@('status=13835058055282164003','status=291'),@('calls=1','calls=2'),@('phase=2','phase=3'),@('reuseAttempt=9','reuseAttempt=2'),@('reusePrepare=9','reusePrepare=1'),@('unchanged=1','unchanged=0'),@('reentryAttempt=8','reentryAttempt=2'),@('reentryPrepare=8','reentryPrepare=1'),@('reentryUnchanged=1','reentryUnchanged=0'),@('realExitBootServices=false','realExitBootServices=true'),@('regions=4','regions=3'),@('freeBytes=81920','freeBytes=81921'))){
            if($pair.Count -ne 2){throw 'raw mutation pair'};$mutated=[Text.Encoding]::ASCII.GetString($good).Replace($pair[0],$pair[1]);if([string]::Equals($mutated,[Text.Encoding]::ASCII.GetString($good),[StringComparison]::Ordinal)){throw 'raw mutation unchanged'};Check ([Text.Encoding]::ASCII.GetBytes($mutated)) $false
        }
        foreach($pair in @(@('getMemoryMap=2097408','getMemoryMap=0'),@('buffer=4194304','buffer=4194305'),@('capacity=131072','capacity=131071'),@('returned=2 exit=3','returned=3 exit=2'),@('captureStatus=0','captureStatus=1'),@('mapBytes=48','mapBytes=49'),@('descriptorBytes=48','descriptorBytes=32'),@('descriptorVersion=1','descriptorVersion=0'),@('exitStatus=9223372036854775810','exitStatus=2147483650'),@('status=4 windows=2','status=1 windows=2'),@('reuse=6 preserved=1','reuse=6 preserved=0'))){
            Check ([Text.Encoding]::ASCII.GetBytes([Text.Encoding]::ASCII.GetString($good).Replace($pair[0],$pair[1]))) $false
        }
        $copy=@($lines);$copy[16]=$lines[17];$copy[17]=$lines[16];Check (Bytes $copy) $false
        Check ([Text.Encoding]::ASCII.GetBytes([Text.Encoding]::ASCII.GetString($good).Replace("`r`n","`n"))) $false
        Check ([byte[]]$good[0..($good.Length-2)]) $false
        Check ([byte[]]($good+0)) $false
        Check ([byte[]]($good+0xFF)) $false
        Check $good $false ([byte[]]@(0xFF))
        Check $good $false ([Text.Encoding]::ASCII.GetBytes('arbitrary'))
        Check $good $false $preamble ([byte[]]($suffix+65))
        Check $good $true
        ; # Expected runner data is built from the independent literal transcript above.
        $ordered=@(
        'TEST_START efi.main',
        'EFI_CONSOLE_READY',
        'UEFI_MEMORY_MAP_READY',
        'UEFI_MEMORY_MAP_DESCRIPTOR_COUNT',
        'UEFI_MEMORY_MAP_OWNED',
        'BOOT_HANDOFF_READY',
        'UEFI_MEMORY_MAP_NORMALIZED',
        'BOOT_MEMORY_MAP_ACCEPTED',
        'BOOT_MEMORY_REGIONS_READY',
        'BOOT_MEMORY_REGION_COUNT',
        'BOOT_PAGE_ALLOCATOR_READY',
        'BOOT_ACPI_RSDP_HANDOFF_READY',
        'UEFI_ACPI_RSDP_READY',
        'TEST_START m03.004',
        'M03_TYPED_EBS_IMAGE',
        'M03_TYPED_EBS_ADMISSION',
        'M03_TYPED_EBS_CASE id=0',
        'M03_TYPED_EBS_CASE id=1',
        'M03_TYPED_EBS_CASE id=2',
        'M03_TYPED_EBS_CASE id=3',
        'CLROS_M03_004_READY',
        'TEST_PASS m03.004',
        'TEST_START m03.005',
        'M03_FINAL_MAP_IMAGE',
        'M03_FINAL_MAP_CALL case=0 index=0',
        'M03_FINAL_MAP_RESULT case=0',
        'CLROS_M03_005_READY',
        'TEST_PASS m03.005',
        'TEST_START m03.006',
        'M03_FINAL_MAP_CALL case=1 index=0',
        'M03_FINAL_MAP_CALL case=1 index=1',
        'M03_FINAL_MAP_RESULT case=1',
        'M03_FINAL_MAP_CALL case=2 index=0',
        'M03_FINAL_MAP_CALL case=2 index=1',
        'M03_FINAL_MAP_RESULT case=2',
        'CLROS_M03_006_READY',
        'TEST_PASS m03.006',
        'TEST_START boot.sequence',
        'BOOT_PHASE PhaseA.Audit START',
        'BOOT_PHASE PhaseA.Audit PASS',
        'BOOT_PHASE PhaseA.Diagnostics START',
        'BOOT_PHASE PhaseA.Diagnostics PASS',
        'OS_BOOT_OK',
        'TEST_PASS boot.sequence',
        'OS_SMOKE_PASS')
        $required=New-Object 'System.Collections.Generic.List[string]'
        foreach($value in @('OS_BOOT_OK','TEST_PASS boot.sequence','OS_SMOKE_PASS','UEFI_MEMORY_MAP_OWNED','BOOT_HANDOFF_READY','UEFI_MEMORY_MAP_NORMALIZED','BOOT_MEMORY_MAP_ACCEPTED','BOOT_MEMORY_REGIONS_READY','BOOT_MEMORY_REGION_COUNT','BOOT_PAGE_ALLOCATOR_READY','BOOT_ACPI_RSDP_HANDOFF_READY','UEFI_ACPI_RSDP_READY')+$ordered){if(-not $required.Contains($value)){$required.Add($value)}}
        $forbidden=@('OS_SMOKE_FAIL','TEST_FAIL','UEFI_EBS_CALLED','UEFI_BOOT_SERVICES_EXITED')
        $runner=@('QEMU_DISCOVERY result=qemu.exe checked=qemu.exe','OVMF_DISCOVERY result=ovmf.fd checked=ovmf.fd','TEST_START qemu.smoke','IMAGE_ARTIFACT image.raw',('SERIAL_LOG '+$serialPath),'OVMF_CODE ovmf.fd','WORKLOAD_PROFILE SingleMinimalApi','MARKER_PRESETS none',('REQUIRED_MARKERS '+[string]::Join(';',$required)),('ORDERED_MARKERS '+[string]::Join(';',$ordered)),('UNIQUE_MARKERS '+[string]::Join(';',$ordered)),('FORBIDDEN_MARKERS '+[string]::Join(';',$forbidden)),('SERIAL_MARKER_STREAM status=Passed terminal=Success bytes='+($preamble.Length+$good.Length+$suffix.Length)+' lines=50 invalidBytes=207 invalidLines=2 orderedSeen=45 outOfOrder=0 uniqueSeen=45 duplicateUnique=0'),'SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1',('SUMMARY_LOG '+$summaryPath),'TEST_PASS qemu.smoke')
        $summary=@('QEMU_SMOKE_STATUS pass')+@($runner[3],$runner[4],$runner[6],$runner[7],$runner[8],$runner[9],$runner[10],$runner[11])+@($lines|ForEach-Object{'OBSERVED_MARKER '+$_})+@('APP_EXIT_RESULT none','APP_EXIT_MALFORMED none','MISSING_MARKERS none','OUT_OF_ORDER_MARKERS none','DUPLICATE_UNIQUE_MARKERS none','FORBIDDEN_MARKER_HIT none','FAIL_REASON none','SERIAL_TAIL_LIMIT lines=8 chars=240')
        $serial=[Text.Encoding]::UTF8.GetString([byte[]]($preamble+$good+$suffix))
        foreach($line in @($serial.Split([string[]]@("`r`n"),[StringSplitOptions]::None)|Where-Object {$_.Trim().Length -gt 0}|Select-Object -Last 8)){$value=$line.Trim();if($value.Length -gt 240){$value=$value.Substring(0,237)+'...'};$summary+='SERIAL_TAIL_LINE '+$value}
        $summaryText=($summary -join "`r`n")+"`r`n";[IO.File]::WriteAllText($summaryPath,$summaryText,[Text.Encoding]::UTF8)
        function Runner([string[]]$value,[bool]$expected){
            $accepted=$true;try{Assert-M03TypedExitBootServicesRunnerResult -Lines $value -SummaryLogPath $summaryPath -SerialLogPath $serialPath -RequiredMarkers $required.ToArray() -OrderedMarkers $ordered -UniqueMarkers $ordered -ForbiddenMarkers $forbidden -ImagePath 'image.raw' -Profile 'SingleMinimalApi'}catch{$accepted=$false}
            if($accepted -ne $expected){throw ('runner case '+$script:cases+' expected='+$expected)};$script:cases++
        }
        Runner $runner $true
        foreach($exit in @('0','-2147483648','2147483647')){$copy=@($runner);$copy[14]='QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exit;Runner $copy $true}
        foreach($exit in @('none','-0','01','+1','2147483648','-2147483649')){$copy=@($runner);$copy[14]='QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exit;Runner $copy $false}
        for($i=0;$i -lt $runner.Count;$i++){$copy=@($runner);$copy[$i]+="`0";Runner $copy $false}
        foreach($pair in @(@(12,'invalidBytes=207','invalidBytes=208'),@(12,'lines=50','lines=49'),@(12,'orderedSeen=45','orderedSeen=44'),@(12,'status=Passed','status=Timeout'),@(13,'milliseconds=750','milliseconds=749'),@(13,'milliseconds','Milliseconds'),@(14,'runner-terminated','exited'),@(14,'runner-terminated','cleanup-failed'))){$copy=@($runner);$copy[$pair[0]]=$copy[$pair[0]].Replace($pair[1],$pair[2]);Runner $copy $false}
        foreach($extra in @('TEST_FAIL x','OS_SMOKE_FAIL','QEMU_PROCESS_CLEANUP_FAILED','TEST_START qemu.smoke')){Runner @($runner+$extra) $false}
        foreach($pair in @(@('QEMU_SMOKE_STATUS pass','QEMU_SMOKE_STATUS fail'),@('FAIL_REASON none','FAIL_REASON timeout'),@('MISSING_MARKERS none','MISSING_MARKERS x'),@('DUPLICATE_UNIQUE_MARKERS none','DUPLICATE_UNIQUE_MARKERS x'),@(('SERIAL_LOG '+$serialPath),'SERIAL_LOG different'))){if($pair.Count -ne 2){throw 'summary mutation pair'};$mutated=$summaryText.Replace($pair[0],$pair[1]);if([string]::Equals($mutated,$summaryText,[StringComparison]::Ordinal)){throw 'summary mutation unchanged'};[IO.File]::WriteAllText($summaryPath,$mutated,[Text.Encoding]::UTF8);Runner $runner $false}
        [IO.File]::WriteAllText($summaryPath,$summaryText+"OS_SMOKE_FAIL`r`n",[Text.Encoding]::UTF8);Runner $runner $false
        [IO.File]::WriteAllText($summaryPath,$summaryText,[Text.Encoding]::UTF8);Runner $runner $true
        ; # Empty stderr is enforced by the same native-command boundary used by this profile.
        Invoke-NativeCommand -RequireEmptyError -Command {& $env:ComSpec /d /c 'exit 0'}
        $rejected=$false;$retained=$null
        try{Invoke-NativeCommand -RequireEmptyError -Command {& $env:ComSpec /d /c 'echo. 1>&2'}}catch{$rejected=$true;$retained=$_.Exception.Message}
        if(-not $rejected -or -not $retained.Contains('path=')){throw 'stderr admission'}
        $errorPath=$retained.Substring($retained.IndexOf('path=')+5);if((Get-Item -LiteralPath $errorPath).Length -eq 0){throw 'stderr evidence lost'}
        Copy-Item -LiteralPath $errorPath -Destination (Join-Path $Directory 'rejected-stderr.log')
        $shortSuffix=Bytes @('BdsDxe: loading Boot0000 "UiApp" from Fv(7CB8BDC9-F8EB-4F34-AAEA-3EE4AF6516A1)/FvFile(462CAA21-7614-4503-836E-8AB6F4662331)',
            'BdsDxe: starting Boot0000 "UiApp" from Fv(7CB8BDC9-F8EB-4F34-AAEA-3EE4AF6516A1)/FvFile(462CAA21-7614-4503-836E-8AB6F4662331)')
        Check $good $true $preamble $shortSuffix
        Check $good $false $preamble ([byte[]]($shortSuffix+65))
        Check $good $false $preamble ([byte[]]@())
        Check $good $true $preamble $shortSuffix
        $shortRunner=@($runner);$shortRunner[12]='SERIAL_MARKER_STREAM status=Passed terminal=Success bytes='+($preamble.Length+$good.Length+$shortSuffix.Length)+' lines=49 invalidBytes=15 invalidLines=1 orderedSeen=45 outOfOrder=0 uniqueSeen=45 duplicateUnique=0'
        $shortSerial=[Text.Encoding]::UTF8.GetString([byte[]]($preamble+$good+$shortSuffix))
        $shortSummary=@($summary|Where-Object{-not $_.StartsWith('SERIAL_TAIL_LINE ',[StringComparison]::Ordinal)})
        foreach($line in @($shortSerial.Split([string[]]@("`r`n"),[StringSplitOptions]::None)|Where-Object{$_.Trim().Length -gt 0}|Select-Object -Last 8)){$value=$line.Trim();if($value.Length -gt 240){$value=$value.Substring(0,237)+'...'};$shortSummary+='SERIAL_TAIL_LINE '+$value}
        [IO.File]::WriteAllText($summaryPath,($shortSummary -join "`r`n")+"`r`n",[Text.Encoding]::UTF8);Runner $shortRunner $true
        Runner $runner $false
        [IO.File]::WriteAllBytes($serialPath,[byte[]]($preamble+$good+$suffix));Runner $shortRunner $false
        [IO.File]::WriteAllText($summaryPath,$summaryText,[Text.Encoding]::UTF8);Runner $runner $true
        Write-Output ('TEST_PASS m03.typedebs.transcript controls='+$cases+' stderrEmpty=true stderrWhitespaceRejected=true')
        """;
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
    private static void TestProfile()
    {
        string root=Root(),directory=Scratch("profile"),script=Path.Combine(root,"scripts/run-qemu-smoke.ps1"),before=Hash(script);int n=0;
        Result Dry(params string[] args)=>Process(Path.Combine(directory,(n++).ToString("D3")),"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-BootPolicy","Diagnostic","-DryRun",..args]);
        string[] positive=["-BuildEfi","-"+Flag];
        Result result=Dry(positive);Passed(result);Need(result.Output.Contains("TEST_PASS qemu.runner.dryrun",StringComparison.Ordinal),"actual dry-run terminal");
        Result preflight=Process(Path.Combine(directory,"enabled-preflight"),"powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/build-efi-shell.ps1"),"-PreflightOnly","-"+Flag,"-LogPath",Path.Combine(directory,"enabled-preflight.log"));
        Passed(preflight);Need(preflight.Output.Split('\n').Count(x=>x.TrimEnd('\r')=="EFI_PREFLIGHT_OK")==1,"actual enabled producer preflight succeeds");
        string[] Vector(string prefix)=>result.Output.Split('\n').Single(x=>x.StartsWith(prefix+" ",StringComparison.Ordinal))[(prefix.Length+1)..].TrimEnd('\r').Split(';').Select(x=>x.Trim()).ToArray();
        var required=Vector("REQUIRED_MARKERS");var ordered=Vector("ORDERED_MARKERS");var unique=Vector("UNIQUE_MARKERS");var forbidden=Vector("FORBIDDEN_MARKERS");
        foreach(var vector in new[]{required,ordered,unique})Need(vector.Length==45&&vector.Distinct(StringComparer.Ordinal).Count()==45,"actual45 canonical unique vectors");
        Need(required.Order(StringComparer.Ordinal).SequenceEqual(ordered.Order(StringComparer.Ordinal),StringComparer.Ordinal)&&ordered.SequenceEqual(unique,StringComparer.Ordinal),"actual complete set and ordered/unique sequence");
        OS.Tools.QemuRunner.SerialMarkerCompletionResult Stream(string[] lines){var state=new OS.Tools.QemuRunner.SerialMarkerStateMachine(required,forbidden,orderedMarkers:ordered,uniqueMarkers:unique);Need(state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n",lines)+"\r\n")).Succeeded,"actual streamer Feed");return state.Complete();}
        var accepted=Stream(ProtocolLines);Need(accepted.Passed&&accepted.LinesProcessed==45&&accepted.OrderedSeenCount==45&&accepted.UniqueSeenCount==45,"genuine full-token streamer success");
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
        string project=File.ReadAllText(Path.Combine(root,"src/OS/OS.csproj"));string[] flags=Regex.Matches(project,@"\$\((Enable\w+Probe)\)").Select(x=>x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();Need(flags.Length==54,"actual54 profile flags");
        foreach(string other in flags.Where(x=>x!=Flag))Reject([..positive,"-"+other],other=="EnableM05PhysicalMappingProbe"?"m05_mapping_requires_owned_first_exit":other=="EnableM03OwnedSubsystemSurvivalProbe"?"m03_owned_requires_first_exit":other=="EnableM03OwnedSubsystemPanicProbe"?"m03_owned_panic_requires_survival":other=="EnableM04SimpleThrowCatchProbe"?"m04_simple_catch_requires_gc_profile":other=="EnableM04SingleFinallyCatchProbe"?"m04_single_finally_requires_gc_profile":other=="EnableM04RethrowProbe"?"m04_rethrow_requires_gc_profile":other=="EnableM04X64UnwindMetadataProbe"?"m04_x64_unwind_metadata_requires_original_module":other=="EnableM03FirmwareAdapterRevocationProbe"?"m03_revocation_requires_first_exit":other=="EnableM04InterfaceDispatchProbe"?"m04_dispatch_requires_cctor_profile":other=="EnableM04ClassConstructorExecutionProbe"?"m04_cctor_requires_gc_profile":other=="EnableM04SeededCollectorChurnProbe"?"m04_churn_requires_gc_profile":other=="EnableM03FirstRealExitBootServicesProbe"?"m03_first_exit_requires property=EnableM01LoadedImageStackProbe":other=="EnableM04GcStaticRootMaterializationProbe"?"m04_gc_static_profile_invalid property=EnableM01LoadedImageStackProbe":"m03_typed_ebs_profile_conflict property="+other);
        Reject(["-"+Flag],"m03_typed_ebs_requires_build_and_original_capture");
        Reject([..positive,"-SkipImageBuild"],"m03_typed_ebs_requires_build_and_original_capture");
        Reject([..positive,"-M01RawObservationLog",""],"m03_typed_ebs_requires_build_and_original_capture");
        Reject([..positive,"-RequireNativeAppMarkers"],"m03_typed_ebs_requires_build_and_original_capture");
        foreach(string option in new[]{"RequireMarker","RequireOrderedMarker","RequireUniqueMarker","ForbidMarker","MarkerPreset"})Reject([..positive,"-"+option,"CLROS_M03_004_READY"],"m03_typed_ebs_forbids_override");
        Reject(["-RequireMarker","CLROS_M03_004_READY"],"m03_typed_ebs_requires_explicit_profile");
        foreach(string name in new[]{"build-efi-shell.ps1","test-efi-artifact.ps1"}){
            string path=Path.Combine(root,"scripts",name);string[] mode=name=="build-efi-shell.ps1"?["-PreflightOnly"]:["-EfiArtifact","missing.EFI"];
            Result failure=Process(Path.Combine(directory,(n++).ToString("D3")),"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",path,..mode,"-"+Flag,"-EnableM01LoadedImageStackProbe"]);
            Need(failure.Exit!=0&&!failure.Timeout&&(failure.Output+failure.Error).Contains("m03_typed_ebs_profile_conflict property=EnableM01LoadedImageStackProbe",StringComparison.Ordinal),"closed producer/verifier "+name);
        }
        Passed(Dry());Need(Hash(script)==before,"unchanged profile input");
    }
    private static void TestArtifacts()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));int compile=project.Descendants("Compile").Count();
        foreach(bool enabled in new[]{false,true})
        {
            string directory=Scratch(enabled?"enabled":"default"),artifact=Path.Combine(root,"artifacts/efi/issue546-"+Guid.NewGuid().ToString("N")+".EFI");
            Result result=Process(directory,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/build-efi-shell.ps1"),"-EfiOutputPath",artifact,"-LogPath",Path.Combine(directory,"publish.log"),..(enabled?new[]{"-EnableM03TypedExitBootServicesCallProbe"}:Array.Empty<string>())]);Passed(result);
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
