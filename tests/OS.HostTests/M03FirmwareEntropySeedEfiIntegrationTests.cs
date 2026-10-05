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
            Console.WriteLine("TEST_PASS m03.entropy.host.tests");return 0;
        }
        finally { NativeMemory.AlignedFree(memory); }
    }
}
#else
internal static class M03FirmwareEntropySeedEfiIntegrationTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("conditional source and original firmware authority",TestSource);
        Run("actual numeric core mapped ABI guards and private retention",TestCapture);
        Run("literal raw grammar and lifecycle controls",TestTranscript);
        Run("profile options and actual complete marker streamer",TestProfile);
        Run("default and enabled no-stdlib artifacts",TestArtifacts);
    }
    private const string Flag="EnableM03FirmwareEntropySeedProbe";
    private static void Run(string name,Action action)
    {Console.WriteLine("TEST_START m03.003.efi "+name);try{action();Console.WriteLine("TEST_PASS m03.003.efi "+name);}catch(Exception e){Console.WriteLine("TEST_FAIL m03.003.efi "+name+" "+e);throw;}}
    private static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static string Root()=>HostToolProcess.FindRepositoryRoot();
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scratch(string tag){string path=Path.Combine(Root(),"artifacts/host-tests/m03-entropy-"+tag+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
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
        var define=project.Descendants("DefineConstants").Single(x=>x.Value.Contains("M03_FIRMWARE_ENTROPY_SEED_PROBE",StringComparison.Ordinal));
        Need((string?)define.Parent?.Attribute("Condition")=="'$("+Flag+")' == 'true'"&&define.Value=="$(DefineConstants);M03_FIRMWARE_ENTROPY_SEED_PROBE","exact enabled symbol");
        var compile=project.Descendants("Compile").ToArray();Need(compile.Length==95&&(string?)compile[88].Attribute("Include")=="..\\OS.Kernel\\Diagnostics\\FirmwareEntropySeedCaptureProbeAuthority.cs","one appended authority");
        string entry=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));string core=entry[entry.IndexOf("    internal static class FirmwareEntropyCapture",StringComparison.Ordinal)..entry.IndexOf("    private const int OwnedMemoryMapBufferSize",StringComparison.Ordinal)];
        foreach(string forbidden in new[]{"new byte[","stackalloc","throw ","new object","string ","GetHash","Console.","AllocatePool(","AllocatePages("})Need(!core.Contains(forbidden,StringComparison.Ordinal),"finite native core "+forbidden);
        Need(core.Contains("s->Locate(&guid, null, (void**)&s->Rng)",StringComparison.Ordinal)&&core.Contains("get(rng, null, (nuint)RequestedBytes, s->Scratch->Data)",StringComparison.Ordinal),"original pointer call shapes");
        Need(core.Contains("WriteUnsignedDecimal(console, serial, ClearedBytes(s))",StringComparison.Ordinal)&&!core.Contains("Zero(scratch->Data, 0, 32) ?",StringComparison.Ordinal),"no diagnostic over uncleared provider bytes");
        Need(core.Contains("MethodImplOptions.NoInlining",StringComparison.Ordinal)&&core.Contains("s->PublishedBytes = RequestedBytes; s->Phase = 3",StringComparison.Ordinal),"private real loads and publication last");
        Need(entry.Contains("if (!FirmwareEntropyCapture.Run(systemTable, console, serial))",StringComparison.Ordinal)&&entry.Contains("while (true) { }",StringComparison.Ordinal),"failed guest terminal leaves success path intact");
        Need(Hash(Path.Combine(root,"src/OS.Kernel/Boot/AnyFirmwareDerivedEntropySeedNeededFirmwareShutdown.cs"))=="B76FEF791119978524F7E159F99D26087D48BD33F292ABCB5422B6FE7C4000BC","frozen host contract");
    }
    private static void TestCapture()
    {
        string root=Root(),directory=Scratch("actual-core");
        string[] sources=["src/OS/Boot/EfiEntry.cs","src/OS/Boot/EfiConsoleAdapter.cs","tests/OS.HostTests/M03FirmwareEntropySeedEfiIntegrationTests.cs"];
        string attribute="        internal sealed class RuntimeExportAttribute : Attribute\n        {\n            public RuntimeExportAttribute(string entry)\n            {\n            }\n        }";
        Need(File.ReadAllText(Path.Combine(root,"src/OS/Runtime/MinimalRuntime.cs")).Replace("\r\n","\n",StringComparison.Ordinal).Contains(attribute,StringComparison.Ordinal),"unchanged host attribute projection");
        File.WriteAllText(Path.Combine(directory,"Attribute.cs"),"namespace System.Runtime {\n"+attribute+"\n}");
        string items=string.Join('\n',sources.Select(x=>"<Compile Include=\""+SecurityElement.Escape(Path.Combine(root,x))+"\"/>"));
        string project=Path.Combine(directory,"Capture.csproj");
        File.WriteAllText(project,"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion><OutputType>Exe</OutputType><StartupObject>FirmwareEntropyHost</StartupObject><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>ENTROPY_CAPTURE_HOST;M03_FIRMWARE_ENTROPY_SEED_PROBE</DefineConstants></PropertyGroup><ItemGroup>"+items+"<Compile Include=\"Attribute.cs\"/><ProjectReference Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj"))+"\"/></ItemGroup></Project>");
        string[] inputs=[..sources.Select(x=>Path.Combine(root,x)),Path.Combine(directory,"Attribute.cs"),project];var identities=inputs.Select(path=>new{Path=path,Bytes=new FileInfo(path).Length,Sha256=Hash(path)}).ToArray();File.WriteAllText(Path.Combine(directory,"inputs.json"),JsonSerializer.Serialize(identities));
        Passed(Process(Path.Combine(directory,"build"),"dotnet","build",project,"-c","Release","--nologo","-m:1","--disable-build-servers","-p:UseSharedCompilation=false"));
        Result run=Process(Path.Combine(directory,"run"),"dotnet",Path.Combine(directory,"bin/Release/net10.0/Capture.dll"));Passed(run);
        Need(run.Output.Contains("TEST_PASS m03.entropy.host.tests",StringComparison.Ordinal)&&run.Output.Split('\n').Count(x=>x.StartsWith("TEST_START m03.entropy.host ",StringComparison.Ordinal))==6,"complete six-group actual core child");
        foreach(var input in identities)Need(Hash(input.Path)==input.Sha256,"unchanged child input "+input.Path);
    }
    private static void TestTranscript()
    {
        string directory=Scratch("transcript"),script=Path.Combine(Root(),"scripts/run-qemu-smoke.ps1"),copy=Path.Combine(directory,"run-qemu-smoke.ps1"),control=Path.Combine(directory,"controls.ps1");
        File.Copy(script,copy);File.WriteAllText(control,TranscriptScript,new UTF8Encoding(false));
        var inputs=new[]{copy,control}.Select(path=>new{Path=path,Bytes=new FileInfo(path).Length,Sha256=Hash(path)}).ToArray();
        File.WriteAllText(Path.Combine(directory,"inputs.json"),JsonSerializer.Serialize(inputs));
        Result run=Process(Path.Combine(directory,"run"),"powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",control,"-Source",copy,"-Directory",directory);Passed(run);
        Need(run.Output.Contains("TEST_PASS m03.entropy.transcript controls=",StringComparison.Ordinal)&&run.Output.Contains("stderrWhitespaceRejected=true",StringComparison.Ordinal),"complete strict raw/lifecycle controls");
        foreach(var input in inputs)Need(Hash(input.Path)==input.Sha256,"unchanged transcript input "+input.Path);
        Result dry=Process(Path.Combine(directory,"streamer-dryrun"),"powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-BuildEfi","-"+Flag,"-FirmwareRngSource","virtio","-DryRun");Passed(dry);
        string[] Vector(string prefix)=>dry.Output.Split('\n').Single(x=>x.StartsWith(prefix+" ",StringComparison.Ordinal))[(prefix.Length+1)..].TrimEnd('\r').Split(';').Select(x=>x.Trim()).ToArray();
        foreach(var shape in new[]{(Name:"full",Lines:32,InvalidBytes:207,InvalidLines:2),(Name:"short",Lines:31,InvalidBytes:15,InvalidLines:1)})
        {
            byte[] framed=File.ReadAllBytes(Path.Combine(directory,shape.Name+"-framed.serial.bin"));
            var state=new OS.Tools.QemuRunner.SerialMarkerStateMachine(Vector("REQUIRED_MARKERS"),Vector("FORBIDDEN_MARKERS"),orderedMarkers:Vector("ORDERED_MARKERS"),uniqueMarkers:Vector("UNIQUE_MARKERS"));
            Need(state.Feed(framed).Succeeded,"complete framed "+shape.Name+" Feed");var actual=state.Complete();
            Need(actual.Passed&&actual.TotalBytes==framed.Length&&actual.LinesProcessed==shape.Lines&&actual.InvalidByteCount==shape.InvalidBytes&&actual.InvalidNoiseLineCount==shape.InvalidLines&&actual.OrderedSeenCount==27&&actual.UniqueSeenCount==27,"complete framed "+shape.Name+" actual streamer accounting");
        }
        Need(Hash(script)==Hash(copy),"original production parser remained unchanged");
    }

    private static readonly string TranscriptScript = """
        param([string]$Source,[string]$Directory)
        $ErrorActionPreference='Stop'
        $tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'script syntax'}
        foreach($name in @('Read-M03MemoryMapCaptureRecord','Assert-M03FirmwareEntropyTranscript','Assert-M03FirmwareEntropyRunnerResult','Invoke-NativeCommand')){
            $function=$ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true)
            if($function.Count -ne 1){throw ('function inventory '+$name)}
            Invoke-Expression $function[0].Extent.Text
        }
        $root=$Directory
        $lines=@(
        'TEST_START efi.main','EFI_CONSOLE_READY','UEFI_MEMORY_MAP_READY','UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4','UEFI_MEMORY_MAP_OWNED',
        'BOOT_HANDOFF_READY','UEFI_MEMORY_MAP_NORMALIZED','BOOT_MEMORY_MAP_ACCEPTED','BOOT_MEMORY_REGIONS_READY','BOOT_MEMORY_REGION_COUNT 4',
        'BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0',
        'BOOT_ACPI_RSDP_HANDOFF_READY','UEFI_ACPI_RSDP_READY','TEST_START m03.003.efi',
        'M03_ENTROPY_DISCOVERY status=0 calls=1 found=1',
        'M03_ENTROPY_ACQUIRE status=0 calls=1 requested=32',
        'M03_ENTROPY_RETAIN bytes=32 scratchCleared=32 guards=64 tails=64 published=1',
        'CLROS_M03_003_READY bytes=32','TEST_PASS m03.003.efi',
        'TEST_START boot.sequence','BOOT_PHASE PhaseA.Audit START','BOOT_PHASE PhaseA.Audit PASS','BOOT_PHASE PhaseA.Diagnostics START','BOOT_PHASE PhaseA.Diagnostics PASS','OS_BOOT_OK','TEST_PASS boot.sequence','OS_SMOKE_PASS')
        $preamble=[Convert]::FromBase64String('G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg==')
        $suffix=[Convert]::FromBase64String('QmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAwICJVaUFwcCIgZnJvbSBGdig3Q0I4QkRDOS1GOEVCLTRGMzQtQUFFQS0zRUU0QUY2NTE2QTEpL0Z2RmlsZSg0NjJDQUEyMS03NjE0LTQ1MDMtODM2RS04QUI2RjQ2NjIzMzEpDQpCZHNEeGU6IHN0YXJ0aW5nIEJvb3QwMDAwICJVaUFwcCIgZnJvbSBGdig3Q0I4QkRDOS1GOEVCLTRGMzQtQUFFQS0zRUU0QUY2NTE2QTEpL0Z2RmlsZSg0NjJDQUEyMS03NjE0LTQ1MDMtODM2RS04QUI2RjQ2NjIzMzEpDQobWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMG0bWzMwbRtbNDBtG1swMTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDI7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzAzOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDU7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA2OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDg7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA5OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTE7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzEyOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTQ7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTc7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE4OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjA7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIxOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjM7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzI0OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syNTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swMTswMUgbWzBtG1szNG0bWzQ3bRtbMDE7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzAyOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swMzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDQ7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA1OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDE7MDFIG1swMjswMkhTdGFuZGFyZCBQQyAoUTM1ICsgSUNIOSwgMjAwOSkbWzAzOzAySHBjLXEzNS0xMS4xG1swMzs1NEgyLjAwIEdIehtbMDQ7MDJIZWRrMi1zdGFibGUyMDI0MDgtcHJlYnVpbHQucWVtdS5vcmcbWzA0OzU0SDI1NiBNQiBSQU0bWzA1OzAySBtbMDU7NTRIG1swNjswMkgbWzA2OzU0SBtbMG0bWzM3bRtbNDBtG1syMTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjI7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIzOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syNDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjE7MDFIG1swbRtbMzBtG1s0MG0bWzI1OzUzSCAgG1syNTs1NEggIBtbMjU7NTVIICAbWzI1OzU2SCAgG1syNTs1N0ggIBtbMjU7NThIICAbWzI1OzU5SCAgG1syNTs2MEggIBtbMjU7NjFIICAbWzI1OzYySCAgG1syNTs2M0ggIBtbMjU7NjRIICAbWzI1OzY1SCAgG1syNTs2NkggIBtbMjU7NjdIICAbWzI1OzY4SCAgG1syNTs2OUggIBtbMjU7NzBIICAbWzI1OzcxSCAgG1syNTs3MkggIBtbMjU7NzNIICAbWzBtG1szMG0bWzQ3bRtbMDc7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA4OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTA7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzExOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTM7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE0OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTY7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxODswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTk7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIwOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNzswMUgbWzA3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzdtG1s0MG0bWzA4OzMxSDxTdGFuZGFyZCBFbmdsaXNoPhtbMG0bWzMwbRtbNDdtG1swODs0OUggICAgICAgICAbWzA4OzAxSCAgIBtbMDg7MDRIU2VsZWN0IExhbmd1YWdlICAgICAgICAgICAgG1swbRtbMzRtG1s0N20bWzA5OzAxSCAgIBtbMDk7MDRIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzBtG1s0N20bWzEwOzAxSCAgIBtbMTA7MDJI4pa6G1sxMDswNEhEZXZpY2UgTWFuYWdlciAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzExOzAxSCAgIBtbMTE7MDJI4pa6G1sxMTswNEhCb290IE1hbmFnZXIgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzEyOzAxSCAgIBtbMTI7MDJI4pa6G1sxMjswNEhCb290IE1haW50ZW5hbmNlIE1hbmFnZXIgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzBtG1szNG0bWzQ3bRtbMTM7MDFIICAgG1sxMzswNEggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzBtG1szMG0bWzQ3bRtbMTQ7MDFIICAgG1sxNDswNEhDb250aW51ZSAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzAxSCAgIBtbMTU7MDRIUmVzZXQgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTg7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIwOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzdtG1s0MG0bWzIzOzAySCAbWzIyOzAySCAbWzIyOzUzSCAgICAgICAgICAgICAgICAgICAgICAgICAbWzIyOzI3SCAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMzs1M0ggICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIzOzAzSOKGkeKGkz1Nb3ZlIEhpZ2hsaWdodCAgICAgICAbWzIyOzAzSCAgICAgICAgICAgICAgICAgICAgICAgIBtbMjM7MjdIPEVudGVyPj1TZWxlY3QgRW50cnkgICAgICAbWzBtG1szNG0bWzQ3bRtbMDg7NThIVGhpcyBpcyB0aGUgb3B0aW9uICAgICAbWzA5OzU4SG9uZSBhZGp1c3RzIHRvIGNoYW5nZSAgG1sxMDs1OEh0aGUgbGFuZ3VhZ2UgZm9yIHRoZSAgIBtbMTE7NThIY3VycmVudCBzeXN0ZW0gICAgICAgICAbWzEyOzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxMzs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTQ7NThIICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxNjs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTc7NThIICAgICAgICAgICAgICAgICAgICAgICAbWzE4OzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxOTs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTk7ODBI')
        $serialPath=Join-Path $Directory 'serial.log';$summaryPath=Join-Path $Directory 'summary.log';$cases=0
        function Bytes([string[]]$value){return [Text.Encoding]::ASCII.GetBytes(($value -join "`r`n")+"`r`n")}
        $good=Bytes $lines
        $shortSuffix=Bytes @('BdsDxe: loading Boot0000 "UiApp" from Fv(7CB8BDC9-F8EB-4F34-AAEA-3EE4AF6516A1)/FvFile(462CAA21-7614-4503-836E-8AB6F4662331)',
            'BdsDxe: starting Boot0000 "UiApp" from Fv(7CB8BDC9-F8EB-4F34-AAEA-3EE4AF6516A1)/FvFile(462CAA21-7614-4503-836E-8AB6F4662331)')
        if($shortSuffix.Length -ne 251 -or $suffix.Length -ne 6945){throw 'literal suffix extents'}
        [IO.File]::WriteAllBytes((Join-Path $Directory 'full-framed.serial.bin'),[byte[]]($preamble+$good+$suffix))
        [IO.File]::WriteAllBytes((Join-Path $Directory 'short-framed.serial.bin'),[byte[]]($preamble+$good+$shortSuffix))
        function Check([byte[]]$protocol,[bool]$expected,[byte[]]$pre=$preamble,[byte[]]$post=$suffix){
            [IO.File]::WriteAllBytes($serialPath,[byte[]]($pre+$protocol+$post));$accepted=$true
            try{Assert-M03FirmwareEntropyTranscript $serialPath}catch{$accepted=$false}
            if($accepted -ne $expected){throw ('transcript case '+$script:cases+' expected='+$expected)};$script:cases++
        }
        Check $good $true
        for($i=0;$i -lt 27;$i++){
            foreach($replacement in @('',($lines[$i]+' '),(' '+$lines[$i]),($lines[$i]+' extra=1'),($lines[$i]+"`0"))){$copy=@($lines);$copy[$i]=$replacement;Check (Bytes $copy) $false}
            $copy=@($lines);$copy[$i]=$copy[$i].ToLowerInvariant();if($copy[$i] -cne $lines[$i]){Check (Bytes $copy) $false}
            $copy=@($lines[0..$i])+@($lines[$i..26]);Check (Bytes $copy) $false
        }
        foreach($pair in @(@('requested=32','requested=31'),@('bytes=32','bytes=31'),@('scratchCleared=32','scratchCleared=0'),@('guards=64','guards=63'),@('tails=64','tails=63'),@('published=1','published=0'),@('status=0 calls=1 found=1','status=9223372036854775815 calls=1 found=1'),@('regions=4','regions=3'),@('freeBytes=81920','freeBytes=81921'))){
            $mutated=[Text.Encoding]::ASCII.GetString($good).Replace($pair[0],$pair[1]);if($mutated -ceq [Text.Encoding]::ASCII.GetString($good)){throw 'raw mutation unchanged'};Check ([Text.Encoding]::ASCII.GetBytes($mutated)) $false
        }
        $copy=@($lines);$copy[15]=$lines[15];$copy[15]=$lines[14];Check (Bytes $copy) $false
        Check ([Text.Encoding]::ASCII.GetBytes([Text.Encoding]::ASCII.GetString($good).Replace("`r`n","`n"))) $false
        Check ([byte[]]$good[0..($good.Length-2)]) $false
        Check ([byte[]]($good+0)) $false
        Check ([byte[]]($good+0xFF)) $false
        Check $good $false ([byte[]]@(0xFF))
        Check $good $false ([Text.Encoding]::ASCII.GetBytes('arbitrary'))
        Check $good $false $preamble ([byte[]]($suffix+65))
        Check $good $true $preamble $shortSuffix
        foreach($shape in @('full','short')){
            $post=if($shape -ceq 'full'){$suffix}else{$shortSuffix}
            foreach($index in @(0,($post.Length-1))){$changed=[byte[]]$post.Clone();$changed[$index]=$changed[$index] -bxor 1;Check $good $false $preamble $changed}
            Check $good $false $preamble ([byte[]]($post+65))
            Check $good $false $preamble ([byte[]]($post+13+10))
            Check $good $false $preamble ([byte[]]$post[0..($post.Length-2)])
        }
        Check $good $false $preamble ([byte[]]@())
        $firstCr=[Array]::IndexOf($shortSuffix,[byte]13)
        foreach($length in @(1,$firstCr,($firstCr+2),($shortSuffix.Length-1),($shortSuffix.Length+1),($suffix.Length-1))){Check $good $false $preamble ([byte[]]$suffix[0..($length-1)])}
        Check $good $true
        $missing=@($lines[0..13])+@('M03_ENTROPY_DISCOVERY status=9223372036854775822 calls=1 found=0','M03_ENTROPY_ACQUIRE status=0 calls=0 requested=32','M03_ENTROPY_RETAIN bytes=0 scratchCleared=0 guards=64 tails=64 published=0','M03_ENTROPY_UNAVAILABLE retainedUnchanged=1 published=0 rngCalls=0','TEST_FAIL m03.003.efi source_unavailable')
        function Missing([byte[]]$protocol,[bool]$expected){[IO.File]::WriteAllBytes($serialPath,[byte[]]($preamble+$protocol));$accepted=$true;try{Assert-M03FirmwareEntropyTranscript $serialPath -Missing}catch{$accepted=$false};if($accepted -ne $expected){throw ('missing-provider grammar case '+$script:cases)};$script:cases++}
        Missing (Bytes $missing) $true
        for($i=0;$i -lt 19;$i++){$copy=@($missing);$copy[$i]+=' extra=1';Missing (Bytes $copy) $false;$copy=@($missing);$copy[$i]='';Missing (Bytes $copy) $false}
        foreach($pair in @(@('rngCalls=0','rngCalls=1'),@('scratchCleared=0','scratchCleared=32'),@('retainedUnchanged=1','retainedUnchanged=0'),@('9223372036854775822','9223372036854775815'),@('published=0','published=1'))){Missing ([Text.Encoding]::ASCII.GetBytes(([Text.Encoding]::ASCII.GetString((Bytes $missing))).Replace($pair[0],$pair[1]))) $false}
        Missing ([byte[]]((Bytes $missing)+65)) $false
        Missing ([byte[]]((Bytes $missing)+$suffix)) $false
        Check $good $true
        ; # Expected runner data is built from the independent literal transcript above.
        $ordered=@($lines | ForEach-Object {
            if($_.StartsWith('UEFI_MEMORY_MAP_DESCRIPTOR_COUNT ')){ 'UEFI_MEMORY_MAP_DESCRIPTOR_COUNT' }
            elseif($_.StartsWith('BOOT_MEMORY_REGION_COUNT ')){ 'BOOT_MEMORY_REGION_COUNT' }
            elseif($_.StartsWith('BOOT_PAGE_ALLOCATOR_READY ')){ 'BOOT_PAGE_ALLOCATOR_READY' }
            elseif($_.StartsWith('M03_ENTROPY_')){ $_.Split(' ')[0] }
            else {$_}
        })
        $required=New-Object 'System.Collections.Generic.List[string]'
        foreach($value in @('OS_BOOT_OK','TEST_PASS boot.sequence','OS_SMOKE_PASS','UEFI_MEMORY_MAP_OWNED','BOOT_HANDOFF_READY','UEFI_MEMORY_MAP_NORMALIZED','BOOT_MEMORY_MAP_ACCEPTED','BOOT_MEMORY_REGIONS_READY','BOOT_MEMORY_REGION_COUNT','BOOT_PAGE_ALLOCATOR_READY','BOOT_ACPI_RSDP_HANDOFF_READY','UEFI_ACPI_RSDP_READY')+$ordered){if(-not $required.Contains($value)){$required.Add($value)}}
        $forbidden=@('OS_SMOKE_FAIL','TEST_FAIL')
        $runner=@('QEMU_DISCOVERY result=qemu.exe checked=qemu.exe','OVMF_DISCOVERY result=ovmf.fd checked=ovmf.fd','TEST_START qemu.smoke','IMAGE_ARTIFACT image.raw',('SERIAL_LOG '+$serialPath),'OVMF_CODE ovmf.fd','WORKLOAD_PROFILE SingleMinimalApi','FIRMWARE_RNG_SOURCE virtio','MARKER_PRESETS none',('REQUIRED_MARKERS '+[string]::Join(';',$required)),('ORDERED_MARKERS '+[string]::Join(';',$ordered)),('UNIQUE_MARKERS '+[string]::Join(';',$ordered)),('FORBIDDEN_MARKERS '+[string]::Join(';',$forbidden)),('SERIAL_MARKER_STREAM status=Passed terminal=Success bytes='+($preamble.Length+$good.Length+$suffix.Length)+' lines=32 invalidBytes=207 invalidLines=2 orderedSeen=27 outOfOrder=0 uniqueSeen=27 duplicateUnique=0'),'SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1',('SUMMARY_LOG '+$summaryPath),'TEST_PASS qemu.smoke')
        $summary=@('QEMU_SMOKE_STATUS pass')+@($runner[3],$runner[4],$runner[6],$runner[7],$runner[8],$runner[9],$runner[10],$runner[11],$runner[12])+@($lines|ForEach-Object{'OBSERVED_MARKER '+$_})+@('APP_EXIT_RESULT none','APP_EXIT_MALFORMED none','MISSING_MARKERS none','OUT_OF_ORDER_MARKERS none','DUPLICATE_UNIQUE_MARKERS none','FORBIDDEN_MARKER_HIT none','FAIL_REASON none','SERIAL_TAIL_LIMIT lines=8 chars=240')
        $serial=[Text.Encoding]::UTF8.GetString([byte[]]($preamble+$good+$suffix))
        foreach($line in @($serial.Split([string[]]@("`r`n"),[StringSplitOptions]::None)|Where-Object {$_.Trim().Length -gt 0}|Select-Object -Last 8)){$value=$line.Trim();if($value.Length -gt 240){$value=$value.Substring(0,237)+'...'};$summary+='SERIAL_TAIL_LINE '+$value}
        $summaryText=($summary -join "`r`n")+"`r`n";[IO.File]::WriteAllText($summaryPath,$summaryText,[Text.Encoding]::UTF8)
        function Runner([string[]]$value,[bool]$expected){
            $accepted=$true;try{Assert-M03FirmwareEntropyRunnerResult -Lines $value -SummaryLogPath $summaryPath -SerialLogPath $serialPath -RequiredMarkers $required.ToArray() -OrderedMarkers $ordered -UniqueMarkers $ordered -ForbiddenMarkers $forbidden -ImagePath 'image.raw' -Profile 'SingleMinimalApi'}catch{$accepted=$false}
            if($accepted -ne $expected){throw ('runner case '+$script:cases+' expected='+$expected)};$script:cases++
        }
        Runner $runner $true
        foreach($exit in @('0','-2147483648','2147483647')){$copy=@($runner);$copy[15]='QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exit;Runner $copy $true}
        foreach($exit in @('none','-0','01','+1','2147483648','-2147483649')){$copy=@($runner);$copy[15]='QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exit;Runner $copy $false}
        for($i=0;$i -lt $runner.Count;$i++){$copy=@($runner);$copy[$i]+="`0";Runner $copy $false}
        foreach($pair in @(@(13,'invalidBytes=207','invalidBytes=208'),@(13,'lines=32','lines=31'),@(13,'orderedSeen=27','orderedSeen=26'),@(13,'status=Passed','status=Timeout'),@(14,'milliseconds=750','milliseconds=749'),@(14,'milliseconds','Milliseconds'),@(15,'runner-terminated','exited'),@(15,'runner-terminated','cleanup-failed'))){$copy=@($runner);$copy[$pair[0]]=$copy[$pair[0]].Replace($pair[1],$pair[2]);Runner $copy $false}
        foreach($extra in @('TEST_FAIL x','OS_SMOKE_FAIL','QEMU_PROCESS_CLEANUP_FAILED','TEST_START qemu.smoke')){Runner @($runner+$extra) $false}
        foreach($pair in @(@('QEMU_SMOKE_STATUS pass','QEMU_SMOKE_STATUS fail'),@('FAIL_REASON none','FAIL_REASON timeout'),@('MISSING_MARKERS none','MISSING_MARKERS x'),@('DUPLICATE_UNIQUE_MARKERS none','DUPLICATE_UNIQUE_MARKERS x'),@(('SERIAL_LOG '+$serialPath),'SERIAL_LOG different'))){if($pair.Count -ne 2){throw 'summary mutation pair'};$mutated=$summaryText.Replace($pair[0],$pair[1]);if([string]::Equals($mutated,$summaryText,[StringComparison]::Ordinal)){throw 'summary mutation unchanged'};[IO.File]::WriteAllText($summaryPath,$mutated,[Text.Encoding]::UTF8);Runner $runner $false}
        [IO.File]::WriteAllText($summaryPath,$summaryText+"OS_SMOKE_FAIL`r`n",[Text.Encoding]::UTF8);Runner $runner $false
        [IO.File]::WriteAllText($summaryPath,$summaryText,[Text.Encoding]::UTF8);Runner $runner $true
        foreach($pair in @(@('invalidLines=2','invalidLines=1'),@('invalidBytes=207','invalidBytes=15'))){$copy=@($runner);$copy[13]=$copy[13].Replace($pair[0],$pair[1]);Runner $copy $false}
        $shortRunner=@($runner);$shortRunner[13]='SERIAL_MARKER_STREAM status=Passed terminal=Success bytes='+($preamble.Length+$good.Length+$shortSuffix.Length)+' lines=31 invalidBytes=15 invalidLines=1 orderedSeen=27 outOfOrder=0 uniqueSeen=27 duplicateUnique=0'
        $shortSummary=@($summary|Where-Object{-not $_.StartsWith('SERIAL_TAIL_LINE ',[StringComparison]::Ordinal)})
        $shortSerial=[Text.Encoding]::UTF8.GetString([byte[]]($preamble+$good+$shortSuffix))
        foreach($line in @($shortSerial.Split([string[]]@("`r`n"),[StringSplitOptions]::None)|Where-Object {$_.Trim().Length -gt 0}|Select-Object -Last 8)){$value=$line.Trim();if($value.Length -gt 240){$value=$value.Substring(0,237)+'...'};$shortSummary+='SERIAL_TAIL_LINE '+$value}
        $shortSummaryText=($shortSummary -join "`r`n")+"`r`n"
        [IO.File]::WriteAllBytes($serialPath,[byte[]]($preamble+$good+$shortSuffix))
        Runner $shortRunner $false # Correct short stream paired with the full firmware summary.
        [IO.File]::WriteAllText($summaryPath,$shortSummaryText,[Text.Encoding]::UTF8);Runner $shortRunner $true
        foreach($pair in @(@('lines=31','lines=32'),@('invalidLines=1','invalidLines=2'),@('invalidBytes=15','invalidBytes=207'))){$copy=@($shortRunner);$copy[13]=$copy[13].Replace($pair[0],$pair[1]);Runner $copy $false}
        [IO.File]::WriteAllBytes($serialPath,[byte[]]($preamble+$good+$suffix));Runner $runner $false # Correct full stream paired with the short firmware summary.
        [IO.File]::WriteAllText($summaryPath,$summaryText,[Text.Encoding]::UTF8);Runner $runner $true
        ; # Empty stderr is enforced by the same native-command boundary used by this profile.
        Invoke-NativeCommand -RequireEmptyError -Command {& $env:ComSpec /d /c 'exit 0'}
        $rejected=$false;$retained=$null
        try{Invoke-NativeCommand -RequireEmptyError -Command {& $env:ComSpec /d /c 'echo. 1>&2'}}catch{$rejected=$true;$retained=$_.Exception.Message}
        if(-not $rejected -or -not $retained.Contains('path=')){throw 'stderr admission'}
        $errorPath=$retained.Substring($retained.IndexOf('path=')+5);if((Get-Item -LiteralPath $errorPath).Length -eq 0){throw 'stderr evidence lost'}
        Copy-Item -LiteralPath $errorPath -Destination (Join-Path $Directory 'rejected-stderr.log')
        Write-Output ('TEST_PASS m03.entropy.transcript controls='+$cases+' stderrEmpty=true stderrWhitespaceRejected=true')
        """;
    private static readonly string[] ProtocolLines = [
        "TEST_START efi.main","EFI_CONSOLE_READY","UEFI_MEMORY_MAP_READY","UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4","UEFI_MEMORY_MAP_OWNED",
        "BOOT_HANDOFF_READY","UEFI_MEMORY_MAP_NORMALIZED","BOOT_MEMORY_MAP_ACCEPTED","BOOT_MEMORY_REGIONS_READY","BOOT_MEMORY_REGION_COUNT 4",
        "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0",
        "BOOT_ACPI_RSDP_HANDOFF_READY","UEFI_ACPI_RSDP_READY","TEST_START m03.003.efi",
        "M03_ENTROPY_DISCOVERY status=0 calls=1 found=1","M03_ENTROPY_ACQUIRE status=0 calls=1 requested=32",
        "M03_ENTROPY_RETAIN bytes=32 scratchCleared=32 guards=64 tails=64 published=1","CLROS_M03_003_READY bytes=32","TEST_PASS m03.003.efi",
        "TEST_START boot.sequence","BOOT_PHASE PhaseA.Audit START","BOOT_PHASE PhaseA.Audit PASS","BOOT_PHASE PhaseA.Diagnostics START","BOOT_PHASE PhaseA.Diagnostics PASS","OS_BOOT_OK","TEST_PASS boot.sequence","OS_SMOKE_PASS"];
    private static void TestProfile()
    {
        string root=Root(),directory=Scratch("profile"),script=Path.Combine(root,"scripts/run-qemu-smoke.ps1"),before=Hash(script);int n=0;
        Result Dry(params string[] args)=>Process(Path.Combine(directory,(n++).ToString("D3")),"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-DryRun",..args]);
        string[] positive=["-BuildEfi","-"+Flag,"-FirmwareRngSource","virtio"];
        Result result=Dry(positive);Passed(result);Need(result.Output.Contains("TEST_PASS qemu.runner.dryrun",StringComparison.Ordinal),"actual dry-run terminal");
        string[] Vector(string prefix)=>result.Output.Split('\n').Single(x=>x.StartsWith(prefix+" ",StringComparison.Ordinal))[(prefix.Length+1)..].TrimEnd('\r').Split(';').Select(x=>x.Trim()).ToArray();
        var required=Vector("REQUIRED_MARKERS");var ordered=Vector("ORDERED_MARKERS");var unique=Vector("UNIQUE_MARKERS");var forbidden=Vector("FORBIDDEN_MARKERS");
        foreach(var vector in new[]{required,ordered,unique})Need(vector.Length==27&&vector.Distinct(StringComparer.Ordinal).Count()==27,"actual27 canonical unique vectors");
        Need(required.Order(StringComparer.Ordinal).SequenceEqual(ordered.Order(StringComparer.Ordinal),StringComparer.Ordinal)&&ordered.SequenceEqual(unique,StringComparer.Ordinal),"actual complete set and ordered/unique sequence");
        OS.Tools.QemuRunner.SerialMarkerCompletionResult Stream(string[] lines){var state=new OS.Tools.QemuRunner.SerialMarkerStateMachine(required,forbidden,orderedMarkers:ordered,uniqueMarkers:unique);Need(state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n",lines)+"\r\n")).Succeeded,"actual streamer Feed");return state.Complete();}
        var accepted=Stream(ProtocolLines);Need(accepted.Passed&&accepted.LinesProcessed==27&&accepted.OrderedSeenCount==27&&accepted.UniqueSeenCount==27,"genuine full-token streamer success");
        foreach(int index in new[]{14,15,16,17}){
            var missing=Stream(ProtocolLines.Where((_,i)=>i!=index).ToArray());Need(!missing.Passed&&missing.MissingRequiredMarkers!.Count>0,"missing entropy row");
            var duplicate=Stream(ProtocolLines.Take(index+1).Concat(ProtocolLines.Skip(index)).ToArray());Need(!duplicate.Passed&&duplicate.DuplicateUniqueOccurrenceCount==1,"duplicate entropy row");
            string[] swapped=[..ProtocolLines];(swapped[index],swapped[index+1])=(swapped[index+1],swapped[index]);Need(Stream(swapped).OutOfOrderOccurrenceCount>0,"reordered entropy row");
        }
        void Reject(string[] args,string reason){Result failure=Dry(args);Need(failure.Exit!=0&&!failure.Timeout&&(failure.Output+failure.Error).Contains(reason,StringComparison.Ordinal),"profile rejection "+reason);}
        string project=File.ReadAllText(Path.Combine(root,"src/OS/OS.csproj"));string[] flags=Regex.Matches(project,@"\$\((Enable\w+Probe)\)").Select(x=>x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();Need(flags.Length==41,"actual41 profile flags");
        foreach(string other in flags.Where(x=>x!=Flag))Reject([..positive,"-"+other],other=="EnableM03TypedExitBootServicesCallProbe"?"m03_typed_ebs_profile_conflict property=EnableM03FirmwareEntropySeedProbe":other=="EnableM04TypeManagerModuleRegistrationProbe"?"m04_type_manager_requires_original_loaded_image":"m03_firmware_entropy_profile_conflict property="+other);
        foreach(string value in new[]{"VIRTIO","Missing","unknown"})Reject(["-BuildEfi","-"+Flag,"-FirmwareRngSource",value],"m03_firmware_entropy_requires_exact_source");
        Reject(["-FirmwareRngSource","virtio"],"firmware_rng_source_requires_entropy_profile");
        Reject(["-FirmwareRngSource",""],"firmware_rng_source_requires_entropy_profile");
        Reject(["-BuildEfi","-"+Flag],"m03_firmware_entropy_requires_exact_source");
        Reject(["-"+Flag,"-FirmwareRngSource","virtio"],"m03_firmware_entropy_requires_build_and_original_capture");
        Reject([..positive,"-SkipImageBuild"],"m03_firmware_entropy_requires_build_and_original_capture");
        Reject([..positive,"-MarkerPreset","acpi"],"m03_firmware_entropy_forbids_override");
        Passed(Dry("-BuildEfi","-"+Flag,"-FirmwareRngSource","missing"));
        Need(Hash(script)==before,"unchanged profile input");
    }
    private static void TestArtifacts()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));int compile=project.Descendants("Compile").Count();
        foreach(bool enabled in new[]{false,true})
        {
            string directory=Scratch(enabled?"enabled":"default"),artifact=Path.Combine(root,"artifacts/efi/issue541-"+Guid.NewGuid().ToString("N")+".EFI");
            Result result=Process(directory,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/build-efi-shell.ps1"),"-EfiOutputPath",artifact,"-LogPath",Path.Combine(directory,"publish.log"),..(enabled?new[]{"-EnableM03FirmwareEntropySeedProbe"}:Array.Empty<string>())]);Passed(result);
            Need(result.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal)&&result.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),"actual producer/verifier");
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(artifact+suffix,Path.Combine(directory,"image.EFI"+suffix));
            foreach(var pair in new[]{("src/OS/bin/Release/net10.0/win-x64/OS.dll","OS.dll"),("src/OS/obj/Release/net10.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net10.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(root,pair.Item1),Path.Combine(directory,pair.Item2));
            using var json=JsonDocument.Parse(File.ReadAllText(artifact+".link.json"));var evaluation=json.RootElement.GetProperty("postRestoreEvaluation");
            Need(evaluation.GetProperty("compileCount").GetInt32()==compile,"actual evaluated Compile membership");
            foreach(var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray())Need(string.Equals(Hash(Path.Combine(root,input.GetProperty("path").GetString()!)),input.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"actual raw source identity");
            foreach(var property in evaluation.GetProperty("properties").EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)))
                Need(property.Value.GetString()==(enabled&&property.Name==Flag?"true":"false"),"actual profile "+property.Name);
            using var stream=File.OpenRead(Path.Combine(directory,"OS.dll"));using var pe=new PEReader(stream);var metadata=pe.GetMetadataReader();Need(metadata.AssemblyReferences.Count==0,"zero AssemblyRefs");
            var types=metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Where(t=>metadata.GetString(t.Name)=="FirmwareEntropyCapture").ToArray();
            Need(types.Length==(enabled?1:0),"conditional entropy type absent/default and present/enabled");
            if(enabled)foreach(string name in new[]{"Begin","Acquire","Retain","ReadByte","ClearedBytes","Run"}){
                var methods=types[0].GetMethods().Where(h=>metadata.GetString(metadata.GetMethodDefinition(h).Name)==name).ToArray();Need(methods.Length==1&&metadata.GetMethodDefinition(methods[0]).RelativeVirtualAddress!=0,"actual entropy method "+name);
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
