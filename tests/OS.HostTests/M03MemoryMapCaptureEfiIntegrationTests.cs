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
internal static class M03MemoryMapCaptureEfiIntegrationTests
{
    private const string Flag="EnableM03MemoryMapCaptureProbe";
    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("source profile closure and original capture",TestSource);
        Run("actual original capture controlled callbacks and guarded copy",TestCapture);
        Run("ordinary source admission and unchanged generic firewall",TestFirewall);
        Run("strict whole transcript and ordinary lifecycle",TestTranscript);
        Run("all reciprocal profile collisions and ordinary dry runs",TestProfiles);
        Run("default and enabled native artifacts and managed ABI",TestArtifacts);
    }
    private static void Run(string name,Action action)
    {Console.WriteLine("TEST_START m03.001.efi "+name);try{action();Console.WriteLine("TEST_PASS m03.001.efi "+name);}catch(Exception e){Console.WriteLine("TEST_FAIL m03.001.efi "+name+" "+e.Message);throw;}}
    private static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static string Root()=>HostToolProcess.FindRepositoryRoot();
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Scratch(string role){string p=Path.Combine(Root(),"artifacts","issue532-tests",role+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
    private static string[] Flags()=>XDocument.Load(Path.Combine(Root(),"src/OS/OS.csproj")).Descendants("Error")
        .Select(x=>x.Attribute("Condition")!.Value).Where(x=>x.StartsWith("'$("+Flag+")' == 'true' and '",StringComparison.Ordinal))
        .Select(x=>Regex.Matches(x,@"\$\(([^)]+)\)")[1].Groups[1].Value).ToArray();
    private sealed record Result(int Code,string Output,string Error,bool TimedOut);
    private static Result Process(string directory,string program,params string[] args)
    {
        Directory.CreateDirectory(directory);var start=new ProcessStartInfo(program){WorkingDirectory=Root(),RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        foreach(string arg in args)start.ArgumentList.Add(arg);DateTime begin=DateTime.UtcNow;
        File.WriteAllText(Path.Combine(directory,"command.json"),JsonSerializer.Serialize(new{Program=program,Arguments=args,WorkingDirectory=Root(),StartedUtc=begin}));
        using var p=System.Diagnostics.Process.Start(start)!;var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
        bool timeout=!p.WaitForExit(900000);if(timeout){p.Kill(true);p.WaitForExit();}string stdout=output.GetAwaiter().GetResult(),stderr=error.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory,"stdout.log"),stdout);File.WriteAllText(Path.Combine(directory,"stderr.log"),stderr);
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{NativeExit=p.ExitCode,TimedOut=timeout,StartedUtc=begin,EndedUtc=DateTime.UtcNow}));
        return new(p.ExitCode,stdout,stderr,timeout);
    }
    private static void Passed(Result r)=>Need(!r.TimedOut&&r.Code==0&&r.Error.Length==0,r.Output+r.Error);
    private static void TestSource()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));var compile=project.Descendants("Compile").ToArray();
        Need(compile.Length==95&&compile[84].Attribute("Include")!.Value=="..\\OS.Kernel\\Diagnostics\\MemoryMapCaptureProbeAuthority.cs"&&compile[84].Attribute("Link")!.Value=="Diagnostics\\MemoryMapCaptureProbeAuthority.cs","exact one appended source");
        var flag=project.Descendants(Flag).Single();Need(flag.Value=="false"&&flag.Attribute("Condition")!.Value=="'$("+Flag+")' == ''","explicit false default");
        Need(project.Descendants("PropertyGroup").Single(x=>x.Attribute("Condition")?.Value=="'$("+Flag+")' == 'true'").Element("DefineConstants")!.Value=="$(DefineConstants);M03_MEMORY_MAP_CAPTURE_PROBE","exact compiler symbol");
        string[] flags=Flags();Need(flags.Length==40&&flags.Distinct(StringComparer.Ordinal).Count()==40,"all40 conflicts");
        Need(flags.Contains("EnableM04TypeManagerModuleRegistrationProbe",StringComparer.Ordinal),"explicit TypeManager conflict member");
        Need(flags.Contains("EnableM03TypedExitBootServicesCallProbe",StringComparer.Ordinal),"explicit typed EBS conflict member");
        var conditions=project.Descendants("Error").Select(x=>x.Attribute("Condition")!.Value).ToHashSet(StringComparer.Ordinal);
        Need(conditions.Contains("'$(EnableM03MemoryMapCaptureProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'")&&conditions.Contains("'$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'"),"exact TypeManager reciprocal conflict rows");
        foreach(string f in flags)Need(conditions.Contains("'$("+f+")' == 'true' and '$("+Flag+")' == 'true'"),"reciprocal "+f);
        string entry=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));
        Need(Regex.Matches(entry,@"captureResult = UefiMemoryMapCapture\.Capture\(").Count==1&&entry.Contains("if (!M03MemoryMapCaptureObserver.ReadBack(captureResult))",StringComparison.Ordinal),"sole actual original Capture and immediate readback");
        string observer=entry[entry.IndexOf("    private static class M03MemoryMapCaptureObserver",StringComparison.Ordinal)..entry.IndexOf("    private static bool TryCaptureUefiMemoryMap",StringComparison.Ordinal)];
        foreach(string forbidden in new[]{"FreePool(","new byte[","new object","throw ","RuntimeExport","Serialize("})Need(!observer.Contains(forbidden,StringComparison.Ordinal),"bounded observer "+forbidden);
        const string tableAssignment=@"services->(?:GetMemoryMap|AllocatePool)\s*=(?!=)";
        Need(!Regex.IsMatch(observer,tableAssignment,RegexOptions.CultureInvariant),"no firmware-table assignment");
        foreach(string field in new[]{"GetMemoryMap","AllocatePool"})
            Need(!Regex.IsMatch("services->"+field+" == null",tableAssignment,RegexOptions.CultureInvariant)&&
                Regex.IsMatch("services->"+field+" = replacement",tableAssignment,RegexOptions.CultureInvariant),"assignment versus equality control "+field);
        // Only these assignments record addresses; no firmware-table mutation exists.
        Need(observer.Contains("o->GetMemoryMap = (ulong)services->GetMemoryMap",StringComparison.Ordinal)&&observer.Contains("o->AllocatePool = (ulong)services->AllocatePool",StringComparison.Ordinal),"retained originals");
    }
    private static void TestCapture()
    {
        string root=Root(),directory=Scratch("actual-capture");
        string includes=string.Join('\n',new[]{"src/OS/Boot/EfiEntry.cs","src/OS/Boot/EfiConsoleAdapter.cs","tests/OS.HostTests/M03MemoryMapCaptureEfiIntegrationTests.cs"}.Select(x=>"<Compile Include=\""+SecurityElement.Escape(Path.Combine(root,x))+"\"/>"));
        string shim=File.ReadAllText(Path.Combine(root,"src/OS/Runtime/MinimalRuntime.cs"));string attribute="        internal sealed class RuntimeExportAttribute : Attribute\n        {\n            public RuntimeExportAttribute(string entry)\n            {\n            }\n        }";
        Need(shim.Replace("\r\n","\n",StringComparison.Ordinal).Contains(attribute,StringComparison.Ordinal),"exact unchanged ordinary-host attribute projection");
        File.WriteAllText(Path.Combine(directory,"Attribute.cs"),"namespace System.Runtime {\n"+attribute+"\n}");
        File.WriteAllText(Path.Combine(directory,"Capture.csproj"),"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion>
            <OutputType>Exe</OutputType><StartupObject>MemoryMapCaptureHost</StartupObject><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>MEMORY_MAP_CAPTURE_HOST;M03_MEMORY_MAP_CAPTURE_PROBE</DefineConstants>
            </PropertyGroup><ItemGroup>
            """+includes+"<Compile Include=\"Attribute.cs\"/><ProjectReference Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj"))+"\"/></ItemGroup></Project>");
        string[] directInputs=[Path.Combine(root,"src/OS/Boot/EfiEntry.cs"),Path.Combine(root,"src/OS/Boot/EfiConsoleAdapter.cs"),Path.Combine(root,"tests/OS.HostTests/M03MemoryMapCaptureEfiIntegrationTests.cs"),Path.Combine(directory,"Attribute.cs"),Path.Combine(directory,"Capture.csproj"),Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj")];
        File.WriteAllText(Path.Combine(directory,"direct-inputs.json"),JsonSerializer.Serialize(directInputs.Select(path=>new{Path=path,Bytes=new FileInfo(path).Length,Sha256=Hash(File.ReadAllBytes(path))}).ToArray()));
        Result r=Process(directory,"dotnet","run","--project",Path.Combine(directory,"Capture.csproj"),"-c","Release","--nologo");Passed(r);
        Need(r.Output.Contains("TEST_PASS m03.capture.host.tests",StringComparison.Ordinal)&&r.Output.Contains("distinctCaptureSizeSlots=true",StringComparison.Ordinal),"complete actual Capture child");
    }
    private static void TestTranscript()=>TranscriptControls();
    private static void TestFirewall()
    {
        string directory=Scratch("firewall"),script=Path.Combine(directory,"Test.ps1");
        File.WriteAllText(script,"""
            param([string]$Root)
            $ErrorActionPreference='Stop';$t=$null;$e=$null
            $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $Root 'scripts/build-efi-shell.ps1'),[ref]$t,[ref]$e)
            if($e.Count){throw 'parse'}
            foreach($name in @('Get-M01Utf8Sha256','Get-NostdlibEfiActiveSource','Test-M01ActiveSourceSurface')) {
                $f=$ast.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true);. ([scriptblock]::Create($f.Extent.Text))
            }
            $name='..\OS.Kernel\Diagnostics\MemoryMapCaptureProbeAuthority.cs'
            $source=[IO.File]::ReadAllText((Join-Path $Root 'src/OS.Kernel/Diagnostics/MemoryMapCaptureProbeAuthority.cs'))
            if(-not(Test-M01ActiveSourceSurface $source $name).Succeeded){throw 'ordinary authority rejected'}
            foreach($bad in @('using System.Text;','class Bad { object M()=>new object(); }','class Bad { void M(){throw null;} }','class Bad { [System.Runtime.InteropServices.UnmanagedCallersOnly] static void M(){} }')) {
                if((Test-M01ActiveSourceSurface ($source+"`n"+$bad) $name).Succeeded){throw 'generic source bypass'}
            }
            Write-Output 'TEST_PASS m03.capture.firewall'
            """);
        Result r=Process(directory,"powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-Root",Root());Passed(r);
        Need(r.Output.Contains("TEST_PASS m03.capture.firewall",StringComparison.Ordinal),"ordinary exact authority admission");
    }
    private static void TestProfiles()
    {
        string directory=Scratch("profiles");int index=0;
        foreach(string other in Flags())foreach(string file in new[]{"build-efi-shell.ps1","test-efi-artifact.ps1","run-qemu-smoke.ps1"})
        {
            string[] extra=file.StartsWith("build-",StringComparison.Ordinal)?["-PreflightOnly","-LogPath",Path.Combine(directory,"preflight-"+index+".log")]:file.StartsWith("test-",StringComparison.Ordinal)?["-EfiArtifact","missing.EFI"]:["-DryRun","-BuildEfi"];
            // The new reference profile validates first; admit its prerequisites to test the actual reciprocal conflict.
            string[] companion=other=="EnableM04CheckedReferenceBarrierProbe"?
                ["-EnableM01LoadedImageStackProbe","-EnableM01LiveAllocatorProbe","-EnableM04IndependentEndianConformanceProbe",
                 "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","-EnableM04ArithmeticConformanceProbe",
                 "-EnableM04LayoutContractProbe","-EnableM04ManagedHeapPageRangeProbe","-EnableM04RhpNewFastProbe"]:[];
            string expected=other=="EnableM03TypedExitBootServicesCallProbe"?"m03_typed_ebs_profile_conflict property=EnableM03MemoryMapCaptureProbe":other=="EnableM04TypeManagerModuleRegistrationProbe"?"m04_type_manager_requires_original_loaded_image":other=="EnableM03FirmwareEntropySeedProbe"?"m03_firmware_entropy_profile_conflict property=EnableM03MemoryMapCaptureProbe":other=="EnableM04CheckedReferenceBarrierProbe"?
                "m04_checked_reference_profile_conflict property=EnableM03MemoryMapCaptureProbe":"m03_memory_map_capture_profile_conflict";
            Result r=Process(Path.Combine(directory,(index++).ToString()),"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(Root(),"scripts",file),..extra,"-"+Flag,"-"+other,..companion]);
            Need(r.Code!=0&&!r.TimedOut&&(r.Output+r.Error).Contains(expected,StringComparison.Ordinal),"exact early conflict "+other+" "+r.Output+r.Error);
        }
        foreach(bool enabled in new[]{false,true})
        {
            Result r=Process(Path.Combine(directory,(index++).ToString()),"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(Root(),"scripts/run-qemu-smoke.ps1"),"-DryRun","-BuildEfi",..(enabled?new[]{"-"+Flag}:Array.Empty<string>())]);Passed(r);
            string ordered=r.Output.Replace("\r","").Split('\n').Single(x=>x.StartsWith("ORDERED_MARKERS ",StringComparison.Ordinal));
            Need(enabled?ordered.Split(';').Length==31:!ordered.Contains("m03.001.capture",StringComparison.Ordinal),"default/enabled exact ordered marker selection");
            if(enabled)
            {
                string[] outputLines=r.Output.Replace("\r","").Split('\n');
                string[][] vectors=new[]{"REQUIRED_MARKERS ","ORDERED_MARKERS ","UNIQUE_MARKERS "}.Select(prefix=>outputLines.Single(line=>line.StartsWith(prefix,StringComparison.Ordinal))[prefix.Length..].Split(';')).ToArray();
                Need(vectors.All(vector=>vector.Length==31&&vector.Distinct(StringComparer.Ordinal).Count()==31&&vector.All(marker=>marker==marker.Trim()))&&vectors[0].ToHashSet(StringComparer.Ordinal).SetEquals(vectors[1])&&vectors[1].SequenceEqual(vectors[2],StringComparer.Ordinal),"31 canonical unique matching required set and exact ordered unique sequence");
            }
        }
    }
    private static void TestArtifacts()
    {
        foreach(bool enabled in new[]{false,true})
        {
            string directory=Scratch(enabled?"enabled":"default"),artifact=Path.Combine(Root(),"artifacts/efi/issue532-"+Guid.NewGuid().ToString("N")+".EFI");
            Result r=Process(directory,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(Root(),"scripts/build-efi-shell.ps1"),"-EfiOutputPath",artifact,"-LogPath",Path.Combine(directory,"publish.log"),..(enabled?new[]{"-"+Flag}:Array.Empty<string>())]);Passed(r);
            Need(r.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal)&&r.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),"complete artifact producer/verifier");
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(artifact+suffix,Path.Combine(directory,"image.EFI"+suffix));
            foreach(var pair in new[]{("src/OS/bin/Release/net10.0/win-x64/OS.dll","OS.dll"),("src/OS/obj/Release/net10.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net10.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(Root(),pair.Item1),Path.Combine(directory,pair.Item2));
            using var json=JsonDocument.Parse(File.ReadAllText(artifact+".link.json"));var e=json.RootElement;var inputs=e.GetProperty("sourceClosureInputs").EnumerateArray().ToArray();Need(inputs.Length==109,"actual109 raw inputs");
            foreach(var input in inputs)Need(Hash(File.ReadAllBytes(Path.Combine(Root(),input.GetProperty("path").GetString()!)))==input.GetProperty("sha256").GetString(),"actual raw hash");
            var evaluation=e.GetProperty("postRestoreEvaluation");Need(evaluation.GetProperty("compileCount").GetInt32()==95,"actual88 Compile");
            var properties=evaluation.GetProperty("properties").EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)).ToArray();Need(properties.Length==41,"actual41 profile flags");
            foreach(var p in properties)Need(p.Value.GetString()==(enabled&&p.Name==Flag?"true":"false"),"exact requested/evaluated "+p.Name);
            InspectMetadata(Path.Combine(directory,"OS.dll"),enabled);
        }
    }
    private static void InspectMetadata(string path,bool enabled)
    {
        using var stream=File.OpenRead(path);using var pe=new PEReader(stream);var reader=pe.GetMetadataReader();Need(reader.AssemblyReferences.Count==0,"actual no-stdlib zero references");
        var types=reader.TypeDefinitions.Where(h=>reader.GetString(reader.GetTypeDefinition(h).Name)=="M03MemoryMapCaptureObserver").ToArray();Need(types.Length==(enabled?1:0),"profile conditional observer");
        if(!enabled)return;var type=reader.GetTypeDefinition(types[0]);
        foreach(string name in new[]{"Prepare","ForwardGetMemoryMap","ForwardAllocatePool","ReadBack","Handoff","Report"})
        {
            var method=reader.GetMethodDefinition(type.GetMethods().Single(h=>reader.GetString(reader.GetMethodDefinition(h).Name)==name));Need(method.RelativeVirtualAddress!=0&&pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!.Length>0,"real method IL "+name);
        }
        foreach(var tuple in new[]{("ForwardGetMemoryMap",new byte[]{0,5,11,15,11,15,1,15,11,15,11,15,9}),("ForwardAllocatePool",new byte[]{0,3,11,9,11,15,15,1})})
        {
            var method=reader.GetMethodDefinition(type.GetMethods().Single(h=>reader.GetString(reader.GetMethodDefinition(h).Name)==tuple.Item1));
            Need(reader.GetBlobBytes(method.Signature).SequenceEqual(tuple.Item2),"exact unmanaged callback scalar signature "+tuple.Item1);
            Need(method.GetCustomAttributes().Count==1,"sole unmanaged entry attribute "+tuple.Item1);
            var attribute=reader.GetCustomAttribute(method.GetCustomAttributes().Single());
            Need(attribute.Constructor.Kind==System.Reflection.Metadata.HandleKind.MethodDefinition,"local no-stdlib attribute constructor");
            var constructor=reader.GetMethodDefinition((System.Reflection.Metadata.MethodDefinitionHandle)attribute.Constructor);
            var declaring=reader.GetTypeDefinition(constructor.GetDeclaringType());
            Need(reader.GetString(declaring.Name)=="UnmanagedCallersOnlyAttribute"&&reader.GetString(declaring.Namespace)=="System.Runtime.InteropServices","actual unmanaged callback boundary");
        }
        File.WriteAllText(Path.ChangeExtension(path,"metadata.json"),JsonSerializer.Serialize(new{AssemblyReferences=reader.AssemblyReferences.Count,GuestExecuted=false,
            Methods=type.GetMethods().Select(h=>{var m=reader.GetMethodDefinition(h);return new{Name=reader.GetString(m.Name),Rva=m.RelativeVirtualAddress,Signature=Convert.ToHexString(reader.GetBlobBytes(m.Signature))};}).ToArray()}));
    }
    // The finite PowerShell controls below extract only the actual parser functions.
    private static void TranscriptControls()
    {
        string directory=Scratch("transcript"),script=Path.Combine(directory,"Test.ps1");
        File.WriteAllText(script,TranscriptScript);
        Result r=Process(directory,"powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-Root",Root(),"-Directory",directory);Passed(r);
        Need(r.Output.Contains("TEST_PASS m03.capture.transcript",StringComparison.Ordinal),"complete finite transcript controls");
    }
    private const string TranscriptScript="""
        param([string]$Root,[string]$Directory)
        $ErrorActionPreference='Stop';$t=$null;$e=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $Root 'scripts/run-qemu-smoke.ps1'),[ref]$t,[ref]$e)
        if($e.Count){throw 'source parse'}
        foreach($name in @('Read-M03MemoryMapCaptureRecord','Assert-M03MemoryMapCaptureTranscript','Assert-M03MemoryMapCaptureRunnerResult')) {
            $f=$ast.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true)
            if($null -eq $f){throw 'missing function'};. ([scriptblock]::Create($f.Extent.Text))
        }
        $lines=@(
        'TEST_START efi.main','EFI_CONSOLE_READY','TEST_START m03.001.capture',
        'M03_CAPTURE_AUTH observer=0000000000100000 observerBytes=488 guard=0000000000200000 guardSpan=131104 payload=0000000000200010 capacity=131072 services=0000000000300000 get=0000000000400000 allocate=0000000000400100',
        'M03_CAPTURE_QUERY calls=1 order=1 inputBytes=0 buffer=0000000000000000 status=8000000000000005 required=96 key=FEDCBA9876543210 stride=48 version=1',
        'M03_CAPTURE_ALLOC calls=1 order=2 type=2 bytes=480 status=0000000000000000 pool=0000000000500000 admitted=1',
        'M03_CAPTURE_FINAL calls=1 order=3 inputBytes=480 buffer=0000000000500000 status=0000000000000000 bytes=160 key=123456789ABCDEF0 stride=40 version=2',
        'M03_CAPTURE_COPY source=0000000000500000 sourceBytes=480 owned=0000000000200010 capacity=131072 bytes=160 count=4 flag=1 compared=160 mismatches=0 firstMismatch=FFFFFFFFFFFFFFFF',
        'M03_CAPTURE_GUARDS bytes=32 mismatches=0 tailBytes=130912 tailMismatches=0',
        'M03_CAPTURE_HANDOFF snapshotKey=123456789ABCDEF0 handoffKey=123456789ABCDEF0 flag=1 matched=1',
        'CLROS_M03_001_READY firmwareAvailable=true exitBootServices=false','TEST_PASS m03.001.capture',
        'UEFI_MEMORY_MAP_READY','UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4','UEFI_MEMORY_MAP_OWNED','BOOT_HANDOFF_READY',
        'UEFI_MEMORY_MAP_NORMALIZED','BOOT_MEMORY_MAP_ACCEPTED','BOOT_MEMORY_REGIONS_READY','BOOT_MEMORY_REGION_COUNT 4',
        'BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0',
        'BOOT_ACPI_RSDP_HANDOFF_READY','UEFI_ACPI_RSDP_READY','TEST_START boot.sequence','BOOT_PHASE PhaseA.Audit START','BOOT_PHASE PhaseA.Audit PASS',
        'BOOT_PHASE PhaseA.Diagnostics START','BOOT_PHASE PhaseA.Diagnostics PASS','OS_BOOT_OK','TEST_PASS boot.sequence','OS_SMOKE_PASS')
        ; # Accepted firmware framing is a fixed byte fixture, never learned from a run.
        $preamble=[Convert]::FromBase64String('G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg==')
        $suffix=[Convert]::FromBase64String('QmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAwICJVaUFwcCIgZnJvbSBGdig3Q0I4QkRDOS1GOEVCLTRGMzQtQUFFQS0zRUU0QUY2NTE2QTEpL0Z2RmlsZSg0NjJDQUEyMS03NjE0LTQ1MDMtODM2RS04QUI2RjQ2NjIzMzEpDQpCZHNEeGU6IHN0YXJ0aW5nIEJvb3QwMDAwICJVaUFwcCIgZnJvbSBGdig3Q0I4QkRDOS1GOEVCLTRGMzQtQUFFQS0zRUU0QUY2NTE2QTEpL0Z2RmlsZSg0NjJDQUEyMS03NjE0LTQ1MDMtODM2RS04QUI2RjQ2NjIzMzEpDQobWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMG0bWzMwbRtbNDBtG1swMTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDI7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzAzOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDU7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA2OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDg7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA5OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTE7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzEyOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTQ7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTc7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE4OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjA7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIxOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjM7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzI0OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syNTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swMTswMUgbWzBtG1szNG0bWzQ3bRtbMDE7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzAyOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swMzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDQ7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA1OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDE7MDFIG1swMjswMkhTdGFuZGFyZCBQQyAoUTM1ICsgSUNIOSwgMjAwOSkbWzAzOzAySHBjLXEzNS0xMS4xG1swMzs1NEgyLjAwIEdIehtbMDQ7MDJIZWRrMi1zdGFibGUyMDI0MDgtcHJlYnVpbHQucWVtdS5vcmcbWzA0OzU0SDI1NiBNQiBSQU0bWzA1OzAySBtbMDU7NTRIG1swNjswMkgbWzA2OzU0SBtbMG0bWzM3bRtbNDBtG1syMTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjI7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIzOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syNDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjE7MDFIG1swbRtbMzBtG1s0MG0bWzI1OzUzSCAgG1syNTs1NEggIBtbMjU7NTVIICAbWzI1OzU2SCAgG1syNTs1N0ggIBtbMjU7NThIICAbWzI1OzU5SCAgG1syNTs2MEggIBtbMjU7NjFIICAbWzI1OzYySCAgG1syNTs2M0ggIBtbMjU7NjRIICAbWzI1OzY1SCAgG1syNTs2NkggIBtbMjU7NjdIICAbWzI1OzY4SCAgG1syNTs2OUggIBtbMjU7NzBIICAbWzI1OzcxSCAgG1syNTs3MkggIBtbMjU7NzNIICAbWzBtG1szMG0bWzQ3bRtbMDc7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA4OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTA7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzExOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTM7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE0OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTY7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxODswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTk7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIwOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNzswMUgbWzA3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzdtG1s0MG0bWzA4OzMxSDxTdGFuZGFyZCBFbmdsaXNoPhtbMG0bWzMwbRtbNDdtG1swODs0OUggICAgICAgICAbWzA4OzAxSCAgIBtbMDg7MDRIU2VsZWN0IExhbmd1YWdlICAgICAgICAgICAgG1swbRtbMzRtG1s0N20bWzA5OzAxSCAgIBtbMDk7MDRIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzBtG1s0N20bWzEwOzAxSCAgIBtbMTA7MDJI4pa6G1sxMDswNEhEZXZpY2UgTWFuYWdlciAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzExOzAxSCAgIBtbMTE7MDJI4pa6G1sxMTswNEhCb290IE1hbmFnZXIgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzEyOzAxSCAgIBtbMTI7MDJI4pa6G1sxMjswNEhCb290IE1haW50ZW5hbmNlIE1hbmFnZXIgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzBtG1szNG0bWzQ3bRtbMTM7MDFIICAgG1sxMzswNEggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzBtG1szMG0bWzQ3bRtbMTQ7MDFIICAgG1sxNDswNEhDb250aW51ZSAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzAxSCAgIBtbMTU7MDRIUmVzZXQgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTg7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIwOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzdtG1s0MG0bWzIzOzAySCAbWzIyOzAySCAbWzIyOzUzSCAgICAgICAgICAgICAgICAgICAgICAgICAbWzIyOzI3SCAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMzs1M0ggICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIzOzAzSOKGkeKGkz1Nb3ZlIEhpZ2hsaWdodCAgICAgICAbWzIyOzAzSCAgICAgICAgICAgICAgICAgICAgICAgIBtbMjM7MjdIPEVudGVyPj1TZWxlY3QgRW50cnkgICAgICAbWzBtG1szNG0bWzQ3bRtbMDg7NThIVGhpcyBpcyB0aGUgb3B0aW9uICAgICAbWzA5OzU4SG9uZSBhZGp1c3RzIHRvIGNoYW5nZSAgG1sxMDs1OEh0aGUgbGFuZ3VhZ2UgZm9yIHRoZSAgIBtbMTE7NThIY3VycmVudCBzeXN0ZW0gICAgICAgICAbWzEyOzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxMzs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTQ7NThIICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxNjs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTc7NThIICAgICAgICAgICAgICAgICAgICAgICAbWzE4OzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxOTs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTk7ODBI')
        $good=[Text.Encoding]::ASCII.GetBytes(($lines -join "`r`n")+"`r`n")
        $path=Join-Path $Directory 'serial.log';$count=0
        function Check([byte[]]$protocol,[bool]$expected,[byte[]]$pre=$preamble,[byte[]]$post=$suffix) {
            [IO.File]::WriteAllBytes($path,[byte[]]($pre+$protocol+$post));$accepted=$true
            try{Assert-M03MemoryMapCaptureTranscript $path}catch{$accepted=$false}
            if($accepted -ne $expected){throw ('transcript case '+$script:count)};$script:count++
        }
        Check $good $true
        foreach($version in @('0','4294967295')) { Check ([Text.Encoding]::ASCII.GetBytes([Text.Encoding]::ASCII.GetString($good).Replace('stride=48 version=1','stride=48 version='+$version))) $true }
        foreach($version in @('4294967296','18446744073709551615')) { Check ([Text.Encoding]::ASCII.GetBytes([Text.Encoding]::ASCII.GetString($good).Replace('stride=48 version=1','stride=48 version='+$version))) $false }
        for($i=0;$i -lt $lines.Count;$i++) {
            $copy=@($lines);$copy[$i]+=' extra=1';Check ([Text.Encoding]::ASCII.GetBytes(($copy -join "`r`n")+"`r`n")) $false
            $copy=@($lines);$copy[$i]='';Check ([Text.Encoding]::ASCII.GetBytes(($copy -join "`r`n")+"`r`n")) $false
        }
        foreach($pair in @(@('observerBytes=488','observerBytes=487'),@('guardSpan=131104','guardSpan=131103'),@('capacity=131072','capacity=131071'),@('pool=0000000000500000','pool=0000000000200000'),@('inputBytes=480','inputBytes=479'),@('compared=160','compared=159'),@('tailMismatches=0','tailMismatches=1'),@('firstMismatch=FFFFFFFFFFFFFFFF','firstMismatch=0000000000000000'),@('snapshotKey=123456789ABCDEF0','snapshotKey=123456789ABCDEF1'),@('required=96','required=18446744073709551616'),@('status=8000000000000005','status=0000000000000000'),@('order=3','order=2'),@('type=2','type=1'),@('version=2','version=0'),@('stride=40','stride=41'),@('count=4','count=5'),@('freeBytes=81920','freeBytes=81921'))) {
            Check ([Text.Encoding]::ASCII.GetBytes([Text.Encoding]::ASCII.GetString($good).Replace($pair[0],$pair[1]))) $false
        }
        Check ([Text.Encoding]::ASCII.GetBytes([Text.Encoding]::ASCII.GetString($good).Replace("`r`n","`n"))) $false
        Check ([byte[]]($good+0)) $false
        Check $good $false ([byte[]]@(0xFF))
        Check $good $false $preamble ([byte[]]($suffix+65))
        Check $good $false ([Text.Encoding]::ASCII.GetBytes('arbitrary'))
        foreach($exit in @('0','-1','-2147483648','2147483647')) {Assert-M03MemoryMapCaptureRunnerResult @('FORBIDDEN_MARKERS OS_SMOKE_FAIL|TEST_FAIL','SERIAL_MARKER_DRAIN milliseconds=750',('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exit))}
        foreach($drain in @("SERIAL_MARKER_DRAIN milliseconds=750`0",'SERIAL_MARKER_DRAIN milliseconds=750'.Replace('milliseconds','Milliseconds'))) {$bad=$false;try{Assert-M03MemoryMapCaptureRunnerResult @($drain,'QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1')}catch{$bad=$true};if(-not $bad){throw 'drain accepted'}}
        foreach($exit in @('none','-0','01','+1','2147483648','-2147483649')) {$bad=$false;try{Assert-M03MemoryMapCaptureRunnerResult @('SERIAL_MARKER_DRAIN milliseconds=750',('QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exit))}catch{$bad=$true};if(-not $bad){throw 'exit accepted'}}
        foreach($extra in @('TEST_FAIL x','OS_SMOKE_FAIL','QEMU_PROCESS_CLEANUP_FAILED','QEMU_PROCESS_RESULT disposition=spontaneous-exit exitCode=0','SERIAL_MARKER_DRAIN milliseconds=750')) {$bad=$false;try{Assert-M03MemoryMapCaptureRunnerResult @('SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1',$extra)}catch{$bad=$true};if(-not $bad){throw 'lifecycle accepted'}}
        Write-Output ('TEST_PASS m03.capture.transcript cases='+$script:count)
        """;
}
#endif
