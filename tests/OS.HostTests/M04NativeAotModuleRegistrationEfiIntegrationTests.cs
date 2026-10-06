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
#if TYPE_MANAGER_CAPTURE_HOST
using OS.Boot;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;
using Probe = OS.Runtime.NativeAotModuleRegistrationProbe;
using Status = OS.Kernel.Runtime.TypeManagerModuleStatus;
internal static unsafe class TypeManagerCaptureHost
{
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static void Run(string name,Action test){Console.WriteLine("TEST_START m04.typemanager.host "+name);test();Console.WriteLine("TEST_PASS m04.typemanager.host "+name);}
    private static int Main()
    {
        Run("actual adapter register slot-derived consumer and tail canaries",()=>{
            using Fixture f=new();
            Check(sizeof(NativeAotTypeManagerAbi)==48&&Core.StateBytes==272&&sizeof(Probe.Buffer)==4096,"actual geometry");
            Check(Probe.Execute(f.Capture,f.Mapping,f.Storage,4096,out var o),"genuine adapter execution "+o.Failure);
            Check(o.Image==(ulong)f.Image&&o.Storage==(ulong)f.Storage&&o.Handle==(ulong)f.Storage+272&&o.Slot==f.Address(0x2000),"actual pointer lineage");
            Check(o.Short==Status.StorageRejected&&o.Repeat==Status.AlreadyRegistered&&o.Foreign==Status.RegistrationChanged,"actual rejection statuses");
            Check(Probe.ReadWord(o.Slot)==o.Handle&&Probe.ReadWord(o.Slot+8)==0&&o.Abi.OsModule==o.Image&&o.Abi.ReadyToRunHeader==f.Address(0x1000),"slot ABI readback");
            Check(o.Thread.Section==f.Address(0x2100)&&o.Thread.Length==8&&o.Indirection.Section==o.Slot&&o.Indirection.Length==8&&o.Empty.Succeeded&&o.Empty.Length==0&&o.Absent.Status==Status.SectionAbsent,"actual consumer results");
            byte[] before=Bytes(f.Image,Fixture.ImageSize);Check(!Probe.Execute(f.Capture,f.Mapping,f.Storage,4096,out _)&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"second adapter cannot reset retained bytes");
            Console.WriteLine("TYPE_MANAGER_HOST_LAYOUT abi=48 state="+Core.StateBytes+" result="+sizeof(TypeManagerModuleResult)+" observation="+sizeof(Probe.Observation)+" capacity=4096 guardStart=4064");
        });
        Run("known mapped short buffer rejects without image or slot writes",()=>{
            using Fixture f=new();var valid=Core.Validate(f.Capture,f.Mapping);Check(valid.Succeeded&&valid.RequiredStorage<=4064,"actual required capacity");
            byte[] before=Bytes(f.Image,Fixture.ImageSize);var shortResult=Core.RegisterOnce(f.Capture,f.Mapping,f.Storage,valid.RequiredStorage-1);
            Check(shortResult.Status==Status.StorageRejected&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"controlled full image includes full slot and buffer");
            foreach(int selected in new[]{0,1,2,3,4}){
                byte* storage=selected==0?null:selected==1?f.Storage+1:selected==2?(byte*)(ulong.MaxValue-7):f.Storage;
                ulong capacity=selected==3?4095UL:4096UL;var capture=selected==4?default:f.Capture;
                Check(!Probe.Execute(capture,f.Mapping,storage,capacity,out _)&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"numeric rejection unchanged "+selected);
            }
            f.Storage[4095]=1;before=Bytes(f.Image,Fixture.ImageSize);
            Check(!Probe.Execute(f.Capture,f.Mapping,f.Storage,4096,out _)&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"nonzero known tail rejects before registration");
        });
        Run("both internal tail strips and retained padding are read back",()=>{
            using Fixture f=new();Check(Probe.Execute(f.Capture,f.Mapping,f.Storage,4096,out var o),"register");
            foreach(int index in new[]{(int)o.Required,4063,4064,4079,4080,4095}){
                byte saved=f.Storage[index];f.Storage[index]^=1;Check(!Probe.Preserved(f.Storage,o.Required),"changed tail detected "+index);f.Storage[index]=saved;Check(Probe.Preserved(f.Storage,o.Required),"unchanged restored tail");
            }
            byte[] before=Bytes(f.Image,Fixture.ImageSize);Check(Core.RegisterOnce(f.Capture,f.Mapping,f.Storage,4096).Status==Status.AlreadyRegistered&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"repeat all bytes unchanged");
            Check(Core.TryGetSection(f.Capture,f.Mapping,f.Storage,4096,o.Handle+8,204).Status==Status.RegistrationChanged&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"foreign all bytes unchanged");
        });
        Run("real metadata and published slot tampering cannot yield a section",()=>{
            for(int selected=0;selected<4;selected++){
                using Fixture f=new();Check(Probe.Execute(f.Capture,f.Mapping,f.Storage,4096,out var o),"register");
                if(selected==0)W64(f.Row(0)+8,f.Address(0x2110));
                if(selected==1)W64(f.Image+0x2000,o.Handle+8);
                if(selected==2)f.Storage[272]^=1;
                if(selected==3)f.Image[0x200C]=1;
                byte[] before=Bytes(f.Image,Fixture.ImageSize);Check(!Core.TryGetSection(f.Capture,f.Mapping,f.Storage,4096,o.Handle,204).Succeeded&&Bytes(f.Image,Fixture.ImageSize).SequenceEqual(before),"tamper no writes "+selected);
            }
        });
        Console.WriteLine("TEST_PASS m04.typemanager.host.tests");return 0;
    }
    private static EfiLoadedImageRangeCapturePlan Capture(ulong address, ulong bytes, ulong handle = 0x1000)
    {
        var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, handle, 0x2000, address, bytes);
        return EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
    }

    private sealed class Fixture : IDisposable
    {
        internal const int ImageSize = 0x8000;
        internal const ulong Capacity = 4096;
        internal readonly byte* Image;
        internal byte* Storage => Image + 0x3000;
        internal EfiLoadedImageRangeCapturePlan Capture { get; }
        internal M01LoadedImageVirtualMappingEvidence Mapping { get; }

        internal Fixture()
        {
            Image = (byte*)NativeMemory.AlignedAlloc(ImageSize, 4096);
            if (Image == null) throw new OutOfMemoryException();
            NativeMemory.Clear(Image, ImageSize);
            W16(Image, 0x5A4D); W32(Image + 60, 0x80); W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664); W16(Image + 0x86, 4); W16(Image + 0x94, 0xF0);
            W16(Image + 0x98, 0x20B); W32(Image + 0xD0, ImageSize); W32(Image + 0xD4, 0x400);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x2000, 0xC0000040);
            Section(2, ".modules", 0x5000, 16, 0x40000040);
            Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            W64(Image + 0x5000, Address(0x1000));
            W32(Image + 0x1000, 0x00525452); W16(Image + 0x1004, 16); W16(Image + 0x100C, 5);
            Image[0x100E] = 24; Image[0x100F] = 1;
            Row(0, 202, 1, 0x2100, 0x2108); Row(1, 204, 0, 0x2000, 0);
            Row(2, 206, 1, 0x1100, 0x1108); Row(3, 213, 1, 0x1110, 0x1110); Row(4, 301, 1, 0x1200, 0x1218);
            Capture = TypeManagerCaptureHost.Capture((ulong)Image, ImageSize);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
        }

        internal ulong Address(int offset) => (ulong)Image + (uint)offset;
        internal byte* Row(int i) => Image + 0x1010 + 24 * i;
        internal byte* Section(int i) => Image + 0x188 + 40 * i;
        private void Row(int i, uint id, uint flags, int start, int end)
        {
            byte* r = Row(i); W32(r, id); W32(r + 4, flags); W64(r + 8, Address(start)); W64(r + 16, end == 0 ? 0 : Address(end));
        }
        private void Section(int i, string name, uint start, uint bytes, uint flags)
        {
            byte* s = Section(i); for (int n = 0; n < name.Length; n++) s[n] = (byte)name[n];
            W32(s + 8, bytes); W32(s + 12, start); W32(s + 36, flags);
        }
        internal TypeManagerModuleResult Register() => Core.RegisterOnce(Capture, Mapping, Storage, Capacity);
        internal TypeManagerModuleResult Lookup(ulong handle, int id) => Core.TryGetSection(Capture, Mapping, Storage, Capacity, handle, id);
        public void Dispose() { NativeMemory.AlignedFree(Image); }
    }

    private static byte[] Bytes(byte* p, int length) => new ReadOnlySpan<byte>(p, length).ToArray();
    private static void W16(byte* p, ushort v) { p[0] = (byte)v; p[1] = (byte)(v >> 8); }
    private static void W32(byte* p, uint v) { W16(p, (ushort)v); W16(p + 2, (ushort)(v >> 16)); }
    private static void W64(byte* p, ulong v) { W32(p, (uint)v); W32(p + 4, (uint)(v >> 32)); }

}
#else
internal static class M04NativeAotModuleRegistrationEfiIntegrationTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("conditional source and original M01 image authority",TestSource);
        Run("actual mapped registration and slot-derived consumer",TestCapture);
        Run("literal raw grammar and lifecycle controls",TestTranscript);
        Run("profile options and actual complete marker streamer",TestProfile);
        Run("default and enabled no-stdlib artifacts",TestArtifacts);
    }
    private const string Flag="EnableM04TypeManagerModuleRegistrationProbe";
    private static void Run(string name,Action action)
    {Console.WriteLine("TEST_START m04.011.efi "+name);try{action();Console.WriteLine("TEST_PASS m04.011.efi "+name);}catch(Exception e){Console.WriteLine("TEST_FAIL m04.011.efi "+name+" "+e);throw;}}
    private static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static string Root()=>HostToolProcess.FindRepositoryRoot();
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scratch(string tag){string path=Path.Combine(Root(),"artifacts/host-tests/m04-type-manager-"+tag+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
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
        var define=project.Descendants("DefineConstants").Single(x=>x.Value.Contains("M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE",StringComparison.Ordinal));
        Need((string?)define.Parent?.Attribute("Condition")=="'$("+Flag+")' == 'true'"&&define.Value=="$(DefineConstants);M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE","exact symbol");
        var compile=project.Descendants("Compile").ToArray();Need(compile.Length==108,"actual complete source closure");
        foreach(string name in new[]{"..\\OS.Kernel\\Runtime\\TypeManagerModuleMetadataLiveNativeAOTImage.cs","Runtime\\NativeAotModuleRegistrationProbe.cs","..\\OS.Kernel\\Diagnostics\\TypeManagerModuleRegistrationProbeAuthority.cs"})Need(compile.Count(x=>(string?)x.Attribute("Include")==name)==1,"one real source "+name);
        Need(Hash(Path.Combine(root,"src/OS.Kernel/Runtime/TypeManagerModuleMetadataLiveNativeAOTImage.cs"))=="D71B47498F238BE5241B839666F624BF96CF602DE17C11D4CA056EA19B0CFADA","reviewed registered-range core");
        string entry=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));
        foreach(string text in new[]{"TryGetOriginalTypeManagerImage","originalCapture = s_evidenceScratch.LoadedImageCapture","originalMapping = s_evidenceScratch.LoadedImageVirtualMapping","OS.Runtime.NativeAotModuleRegistrationProbe.Run(typeManagerCapture, typeManagerMapping"})Need(entry.Contains(text,StringComparison.Ordinal),"actual original authority "+text);
        Need(File.ReadAllText(Path.Combine(root,"src/OS.Kernel/Diagnostics/TypeManagerModuleRegistrationProbeAuthority.cs")).Contains("public const bool EnableTypeManagerModuleRegistrationProbe = false;",StringComparison.Ordinal),"disabled compile-time authority");
        string adapter=File.ReadAllText(Path.Combine(root,"src/OS/Runtime/NativeAotModuleRegistrationProbe.cs"));
        foreach(string forbidden in new[]{"new byte[","throw ","string ","Marshal.","Reflection","delegate*","AllocatePool","AllocatePages","RhCreateTypeManager","RhGetModuleSection"})Need(!adapter.Contains(forbidden,StringComparison.Ordinal),"bounded adapter "+forbidden);
        foreach(string text in new[]{"valid.RequiredStorage - 1","storage, capacity","ulong consumerHandle = ReadWord(slot.Section)","consumerHandle, 202","consumerHandle, 204","consumerHandle, 213","consumerHandle, 0","MethodImplOptions.NoInlining"})Need(adapter.Contains(text,StringComparison.Ordinal),"genuine consumer "+text);
        Need(adapter.IndexOf("if (!registered.Succeeded",StringComparison.Ordinal)<adapter.IndexOf("storage[i] = Canary(i)",StringComparison.Ordinal),"only successful registration admits canary writes");
        var errors=project.Descendants("Error").Select(x=>(string)x.Attribute("Condition")!).ToArray();
        string[] flags=Regex.Matches(File.ReadAllText(Path.Combine(root,"src/OS/OS.csproj")),@"\$\((Enable\w+Probe)\)").Select(x=>x.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();Need(flags.Length==44,"forty-four actual flags");
        Need(errors.Count(x=>x=="'$("+Flag+")' != '' and '$("+Flag+")' != 'true' and '$("+Flag+")' != 'false'")==1,"exact invalid-value guard");
        Need(errors.Count(x=>x=="'$("+Flag+")' == 'true' and '$(EnableM01LoadedImageStackProbe)' != 'true'")==1,"exact original M01 prerequisite");
        foreach(string other in flags.Where(x=>x!=Flag&&x!="EnableM01LoadedImageStackProbe")){
            if(other=="EnableM03FirstRealExitBootServicesProbe"){
                Need(errors.Count(x=>x=="'$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '$("+Flag+")' == 'true'")==1,"exact incoming first-exit conflict");
                continue;
            }
            if(other=="EnableM04SeededCollectorChurnProbe"){
                Need(errors.Count(x=>x=="'$(EnableM04SeededCollectorChurnProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'")==1
                    &&errors.Count(x=>x=="'$(EnableM04GcStaticRootMaterializationProbe)' == 'true' and '$("+Flag+")' == 'true'")==1
                    &&errors.Count(x=>x=="'$("+Flag+")' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' == 'true'")==1,"exact inherited churn GC conflicts");
                continue;
            }
            Need(errors.Contains("'$("+Flag+")' == 'true' and '$("+other+")' == 'true'",StringComparer.Ordinal)&&errors.Contains("'$("+other+")' == 'true' and '$("+Flag+")' == 'true'",StringComparer.Ordinal),"reciprocal conflict "+other);
        }
    }
    private static void TestCapture()
    {
        string root=Root(),directory=Scratch("actual-core");
        string[] sources=["src/OS/Boot/EfiEntry.cs","src/OS/Boot/EfiConsoleAdapter.cs","src/OS/Runtime/NativeAotModuleRegistrationProbe.cs","tests/OS.HostTests/M04NativeAotModuleRegistrationEfiIntegrationTests.cs"];
        string attribute="        internal sealed class RuntimeExportAttribute : Attribute\n        {\n            public RuntimeExportAttribute(string entry)\n            {\n            }\n        }";
        Need(File.ReadAllText(Path.Combine(root,"src/OS/Runtime/MinimalRuntime.cs")).Replace("\r\n","\n",StringComparison.Ordinal).Contains(attribute,StringComparison.Ordinal),"unchanged host attribute projection");
        File.WriteAllText(Path.Combine(directory,"Attribute.cs"),"namespace System.Runtime {\n"+attribute+"\n}");
        string items=string.Join('\n',sources.Select(x=>"<Compile Include=\""+SecurityElement.Escape(Path.Combine(root,x))+"\"/>"));
        string project=Path.Combine(directory,"Capture.csproj");
        File.WriteAllText(project,"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion><OutputType>Exe</OutputType><StartupObject>TypeManagerCaptureHost</StartupObject><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>TYPE_MANAGER_CAPTURE_HOST</DefineConstants></PropertyGroup><ItemGroup>"+items+"<Compile Include=\"Attribute.cs\"/><ProjectReference Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj"))+"\"/></ItemGroup></Project>");
        string[] inputs=[..sources.Select(x=>Path.Combine(root,x)),Path.Combine(directory,"Attribute.cs"),project];var identities=inputs.Select(path=>new{Path=path,Bytes=new FileInfo(path).Length,Sha256=Hash(path)}).ToArray();File.WriteAllText(Path.Combine(directory,"inputs.json"),JsonSerializer.Serialize(identities));
        Passed(Process(Path.Combine(directory,"build"),"dotnet","build",project,"-c","Release","--nologo","-m:1","--disable-build-servers","-p:UseSharedCompilation=false"));
        Result run=Process(Path.Combine(directory,"run"),"dotnet",Path.Combine(directory,"bin/Release/net10.0/Capture.dll"));Passed(run);
        Need(run.Output.Contains("TEST_PASS m04.typemanager.host.tests",StringComparison.Ordinal)&&run.Output.Split('\n').Count(x=>x.StartsWith("TEST_START m04.typemanager.host ",StringComparison.Ordinal))==4,"complete four-group actual core child");
        foreach(var input in identities)Need(Hash(input.Path)==input.Sha256,"unchanged child input "+input.Path);
    }
    private static void TestTranscript()
    {
        string directory=Scratch("transcript"),script=Path.Combine(Root(),"scripts/run-qemu-smoke.ps1"),copy=Path.Combine(directory,"run-qemu-smoke.ps1"),control=Path.Combine(directory,"controls.ps1");
        File.Copy(script,copy);File.WriteAllText(control,TranscriptScript,new UTF8Encoding(false));
        var inputs=new[]{copy,control}.Select(path=>new{Path=path,Bytes=new FileInfo(path).Length,Sha256=Hash(path)}).ToArray();
        File.WriteAllText(Path.Combine(directory,"inputs.json"),JsonSerializer.Serialize(inputs));
        Result run=Process(Path.Combine(directory,"run"),"powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",control,"-Source",copy,"-Directory",directory);Passed(run);
        Need(run.Output.Contains("TEST_PASS m04.typemanager.transcript controls=",StringComparison.Ordinal)&&run.Output.Contains("stderrWhitespaceRejected=true",StringComparison.Ordinal),"complete strict raw/lifecycle controls");
        foreach(var input in inputs)Need(Hash(input.Path)==input.Sha256,"unchanged transcript input "+input.Path);
        Need(Hash(script)==Hash(copy),"original production parser remained unchanged");
    }

    private static readonly string TranscriptScript = """
        param([string]$Source,[string]$Directory)
        $ErrorActionPreference='Stop'
        $tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'script syntax'}
        foreach($name in @('Read-M03MemoryMapCaptureRecord','Assert-M04TypeManagerTranscript','Assert-M04TypeManagerRunnerResult','Invoke-NativeCommand')){
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
        'TEST_START m01.stage1',
        'M01_LOADED_IMAGE_CAPTURED',
        'M01_STACK_SWITCH_STUB_READY',
        'M01_STACK_ARENA_ALLOCATED',
        'M01_STACK_GUARD_RESERVED',
        'M01_STAGE1_MAP_RECAPTURED',
        'M01_STAGE1_RESERVATIONS_VALIDATED',
        'M01_OWNED_STACK_READY',
        'M01_OWNED_STACK_SWITCHED',
        'M01_RSP_CONTAINED',
        'TEST_PASS m01.stage1',
        'TEST_START m04.011',
        'CLROS_M04_011_IMAGE base=1048576 bytes=32768',
        'CLROS_M04_011_VALIDATE required=1024 storage=1060864 capacity=4096',
        'CLROS_M04_011_REJECT kind=capacity status=7 unchanged=true',
        'CLROS_M04_011_REGISTER slot=1056768 handle=1061136 osModule=1048576 header=1052672 gc=0 tls=1057024 slotBytes=16 index=0 padding=0 abiBytes=48 classlib=0 classlibCount=0',
        'CLROS_M04_011_SECTION id=202 status=1 start=1057024 length=8',
        'CLROS_M04_011_SECTION id=204 status=1 start=1056768 length=8',
        'CLROS_M04_011_SECTION id=213 status=1 start=1052944 length=0',
        'CLROS_M04_011_SECTION id=0 status=11 start=0 length=0',
        'CLROS_M04_011_REJECT kind=repeat status=9 unchanged=true',
        'CLROS_M04_011_REJECT kind=foreign status=10 unchanged=true',
        'CLROS_M04_011_READY modules=1 abiBytes=48 slotBytes=16 registered=true consumer=true tailCanaries=true',
        'TEST_PASS m04.011',
        'TEST_START boot.sequence',
        'BOOT_PHASE PhaseA.Audit START',
        'BOOT_PHASE PhaseA.Audit PASS',
        'BOOT_PHASE PhaseA.Diagnostics START',
        'BOOT_PHASE PhaseA.Diagnostics PASS',
        'OS_BOOT_OK',
        'TEST_PASS boot.sequence',
        'OS_SMOKE_PASS')
        $preamble=[Convert]::FromBase64String('G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg==')
        $suffix=[byte[]]@()
        $serialPath=Join-Path $Directory 'serial.log';$summaryPath=Join-Path $Directory 'summary.log';$cases=0
        function Bytes([string[]]$value){return [Text.Encoding]::ASCII.GetBytes(($value -join "`r`n")+"`r`n")}
        $good=Bytes $lines
        function Check([byte[]]$protocol,[bool]$expected,[byte[]]$pre=$preamble,[byte[]]$post=$suffix){
            [IO.File]::WriteAllBytes($serialPath,[byte[]]($pre+$protocol+$post));$accepted=$true
            try{Assert-M04TypeManagerTranscript $serialPath}catch{$accepted=$false}
            if($accepted -ne $expected){throw ('transcript case '+$script:cases+' expected='+$expected)};$script:cases++
        }
        Check $good $true
        for($i=0;$i -lt 45;$i++){
            foreach($replacement in @('',($lines[$i]+' '),(' '+$lines[$i]),($lines[$i]+' extra=1'),($lines[$i]+"`0"))){$copy=@($lines);$copy[$i]=$replacement;Check (Bytes $copy) $false}
            $copy=@($lines);$copy[$i]=$copy[$i].ToLowerInvariant();if($copy[$i] -cne $lines[$i]){Check (Bytes $copy) $false}
            $copy=@($lines[0..$i])+@($lines[$i..44]);Check (Bytes $copy) $false
        }
        foreach($pair in @(@('required=1024','required=4065'),@('capacity=4096','capacity=4095'),@('storage=1060864','storage=1060865'),@('handle=1061136','handle=1061137'),@('slotBytes=16','slotBytes=8'),@('index=0','index=1'),@('padding=0','padding=4294967296'),@('abiBytes=48','abiBytes=40'),@('classlib=0','classlib=1'),@('classlibCount=0','classlibCount=1'),@('tailCanaries=true','tailCanaries=false'),@('status=7','status=9'),@('id=213 status=1','id=213 status=11'),@('id=0 status=11 start=0','id=0 status=11 start=1'),@('base=1048576','base=18446744073709551615'),@('bytes=32768','bytes=4294967296'),@('slot=1056768','slot=1060864'),@('tls=1057024','tls=0'),@('regions=4','regions=3'),@('freeBytes=81920','freeBytes=81921'))){
            if($pair.Count -ne 2){throw 'raw mutation pair'};$mutated=[Text.Encoding]::ASCII.GetString($good).Replace($pair[0],$pair[1]);if([string]::Equals($mutated,[Text.Encoding]::ASCII.GetString($good),[StringComparison]::Ordinal)){throw 'raw mutation unchanged'};Check ([Text.Encoding]::ASCII.GetBytes($mutated)) $false
        }
        $copy=@($lines);$copy[25]=$lines[26];$copy[26]=$lines[25];Check (Bytes $copy) $false
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
        'TEST_START m01.stage1',
        'M01_LOADED_IMAGE_CAPTURED',
        'M01_STACK_SWITCH_STUB_READY',
        'M01_STACK_ARENA_ALLOCATED',
        'M01_STACK_GUARD_RESERVED',
        'M01_STAGE1_MAP_RECAPTURED',
        'M01_STAGE1_RESERVATIONS_VALIDATED',
        'M01_OWNED_STACK_READY',
        'M01_OWNED_STACK_SWITCHED',
        'M01_RSP_CONTAINED',
        'TEST_PASS m01.stage1',
        'TEST_START m04.011',
        'CLROS_M04_011_IMAGE',
        'CLROS_M04_011_VALIDATE',
        'CLROS_M04_011_REJECT kind=capacity',
        'CLROS_M04_011_REGISTER',
        'CLROS_M04_011_SECTION id=202',
        'CLROS_M04_011_SECTION id=204',
        'CLROS_M04_011_SECTION id=213',
        'CLROS_M04_011_SECTION id=0',
        'CLROS_M04_011_REJECT kind=repeat',
        'CLROS_M04_011_REJECT kind=foreign',
        'CLROS_M04_011_READY',
        'TEST_PASS m04.011',
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
        $forbidden=@('OS_SMOKE_FAIL','TEST_FAIL')
        $runner=@('QEMU_DISCOVERY result=qemu.exe checked=qemu.exe','OVMF_DISCOVERY result=ovmf.fd checked=ovmf.fd','TEST_START qemu.smoke','IMAGE_ARTIFACT image.raw',('SERIAL_LOG '+$serialPath),'OVMF_CODE ovmf.fd','WORKLOAD_PROFILE SingleMinimalApi','MARKER_PRESETS none',('REQUIRED_MARKERS '+[string]::Join(';',$required)),('ORDERED_MARKERS '+[string]::Join(';',$ordered)),('UNIQUE_MARKERS '+[string]::Join(';',$ordered)),('FORBIDDEN_MARKERS '+[string]::Join(';',$forbidden)),('SERIAL_MARKER_STREAM status=Passed terminal=Success bytes='+($preamble.Length+$good.Length+$suffix.Length)+' lines=47 invalidBytes=15 invalidLines=1 orderedSeen=45 outOfOrder=0 uniqueSeen=45 duplicateUnique=0'),'SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1',('SUMMARY_LOG '+$summaryPath),'TEST_PASS qemu.smoke')
        $summary=@('QEMU_SMOKE_STATUS pass')+@($runner[3],$runner[4],$runner[6],$runner[7],$runner[8],$runner[9],$runner[10],$runner[11])+@($lines|ForEach-Object{'OBSERVED_MARKER '+$_})+@('APP_EXIT_RESULT none','APP_EXIT_MALFORMED none','MISSING_MARKERS none','OUT_OF_ORDER_MARKERS none','DUPLICATE_UNIQUE_MARKERS none','FORBIDDEN_MARKER_HIT none','FAIL_REASON none','SERIAL_TAIL_LIMIT lines=8 chars=240')
        $serial=[Text.Encoding]::UTF8.GetString([byte[]]($preamble+$good+$suffix))
        foreach($line in @($serial.Split([string[]]@("`r`n"),[StringSplitOptions]::None)|Where-Object {$_.Trim().Length -gt 0}|Select-Object -Last 8)){$value=$line.Trim();if($value.Length -gt 240){$value=$value.Substring(0,237)+'...'};$summary+='SERIAL_TAIL_LINE '+$value}
        $summaryText=($summary -join "`r`n")+"`r`n";[IO.File]::WriteAllText($summaryPath,$summaryText,[Text.Encoding]::UTF8)
        function Runner([string[]]$value,[bool]$expected){
            $accepted=$true;try{Assert-M04TypeManagerRunnerResult -Lines $value -SummaryLogPath $summaryPath -SerialLogPath $serialPath -RequiredMarkers $required.ToArray() -OrderedMarkers $ordered -UniqueMarkers $ordered -ForbiddenMarkers $forbidden -ImagePath 'image.raw' -Profile 'SingleMinimalApi'}catch{$accepted=$false}
            if($accepted -ne $expected){throw ('runner case '+$script:cases+' expected='+$expected)};$script:cases++
        }
        Runner $runner $true
        foreach($exit in @('0','-2147483648','2147483647')){$copy=@($runner);$copy[14]='QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exit;Runner $copy $true}
        foreach($exit in @('none','-0','01','+1','2147483648','-2147483649')){$copy=@($runner);$copy[14]='QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exit;Runner $copy $false}
        for($i=0;$i -lt $runner.Count;$i++){$copy=@($runner);$copy[$i]+="`0";Runner $copy $false}
        foreach($pair in @(@(12,'invalidBytes=15','invalidBytes=16'),@(12,'lines=47','lines=46'),@(12,'orderedSeen=45','orderedSeen=44'),@(12,'status=Passed','status=Timeout'),@(13,'milliseconds=750','milliseconds=749'),@(13,'milliseconds','Milliseconds'),@(14,'runner-terminated','exited'),@(14,'runner-terminated','cleanup-failed'))){$copy=@($runner);$copy[$pair[0]]=$copy[$pair[0]].Replace($pair[1],$pair[2]);Runner $copy $false}
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
        Write-Output ('TEST_PASS m04.typemanager.transcript controls='+$cases+' stderrEmpty=true stderrWhitespaceRejected=true')
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
        "TEST_START m01.stage1",
        "M01_LOADED_IMAGE_CAPTURED",
        "M01_STACK_SWITCH_STUB_READY",
        "M01_STACK_ARENA_ALLOCATED",
        "M01_STACK_GUARD_RESERVED",
        "M01_STAGE1_MAP_RECAPTURED",
        "M01_STAGE1_RESERVATIONS_VALIDATED",
        "M01_OWNED_STACK_READY",
        "M01_OWNED_STACK_SWITCHED",
        "M01_RSP_CONTAINED",
        "TEST_PASS m01.stage1",
        "TEST_START m04.011",
        "CLROS_M04_011_IMAGE base=1048576 bytes=32768",
        "CLROS_M04_011_VALIDATE required=1024 storage=1060864 capacity=4096",
        "CLROS_M04_011_REJECT kind=capacity status=7 unchanged=true",
        "CLROS_M04_011_REGISTER slot=1056768 handle=1061136 osModule=1048576 header=1052672 gc=0 tls=1057024 slotBytes=16 index=0 padding=0 abiBytes=48 classlib=0 classlibCount=0",
        "CLROS_M04_011_SECTION id=202 status=1 start=1057024 length=8",
        "CLROS_M04_011_SECTION id=204 status=1 start=1056768 length=8",
        "CLROS_M04_011_SECTION id=213 status=1 start=1052944 length=0",
        "CLROS_M04_011_SECTION id=0 status=11 start=0 length=0",
        "CLROS_M04_011_REJECT kind=repeat status=9 unchanged=true",
        "CLROS_M04_011_REJECT kind=foreign status=10 unchanged=true",
        "CLROS_M04_011_READY modules=1 abiBytes=48 slotBytes=16 registered=true consumer=true tailCanaries=true",
        "TEST_PASS m04.011",
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
        Result Dry(params string[] args)=>Process(Path.Combine(directory,(n++).ToString("D3")),"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-DryRun",..args]);
        string[] positive=["-BuildEfi","-"+Flag,"-EnableM01LoadedImageStackProbe"];
        Result result=Dry(positive);Passed(result);Need(result.Output.Contains("TEST_PASS qemu.runner.dryrun",StringComparison.Ordinal),"actual dry-run terminal");
        string[] Vector(string prefix)=>result.Output.Split('\n').Single(x=>x.StartsWith(prefix+" ",StringComparison.Ordinal))[(prefix.Length+1)..].TrimEnd('\r').Split(';').Select(x=>x.Trim()).ToArray();
        var required=Vector("REQUIRED_MARKERS");var ordered=Vector("ORDERED_MARKERS");var unique=Vector("UNIQUE_MARKERS");var forbidden=Vector("FORBIDDEN_MARKERS");
        foreach(var vector in new[]{required,ordered,unique})Need(vector.Length==45&&vector.Distinct(StringComparer.Ordinal).Count()==45,"actual45 canonical unique vectors");
        Need(required.Order(StringComparer.Ordinal).SequenceEqual(ordered.Order(StringComparer.Ordinal),StringComparer.Ordinal)&&ordered.SequenceEqual(unique,StringComparer.Ordinal),"actual complete set and ordered/unique sequence");
        OS.Tools.QemuRunner.SerialMarkerCompletionResult Stream(string[] lines){var state=new OS.Tools.QemuRunner.SerialMarkerStateMachine(required,forbidden,orderedMarkers:ordered,uniqueMarkers:unique);Need(state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n",lines)+"\r\n")).Succeeded,"actual streamer Feed");return state.Complete();}
        var accepted=Stream(ProtocolLines);Need(accepted.Passed&&accepted.LinesProcessed==45&&accepted.OrderedSeenCount==45&&accepted.UniqueSeenCount==45,"genuine full-token streamer success");
        foreach(int index in Enumerable.Range(24,13)){
            var missing=Stream(ProtocolLines.Where((_,i)=>i!=index).ToArray());Need(!missing.Passed&&missing.MissingRequiredMarkers!.Count>0,"missing registration row");
            var duplicate=Stream(ProtocolLines.Take(index+1).Concat(ProtocolLines.Skip(index)).ToArray());Need(!duplicate.Passed&&duplicate.DuplicateUniqueOccurrenceCount==1,"duplicate registration row");
            string[] swapped=[..ProtocolLines];(swapped[index],swapped[index+1])=(swapped[index+1],swapped[index]);Need(Stream(swapped).OutOfOrderOccurrenceCount>0,"reordered registration row");
        }
        void Reject(string[] args,string reason){Result failure=Dry(args);Need(failure.Exit!=0&&!failure.Timeout&&(failure.Output+failure.Error).Contains(reason,StringComparison.Ordinal),"profile rejection "+reason);}
        string project=File.ReadAllText(Path.Combine(root,"src/OS/OS.csproj"));string[] flags=Regex.Matches(project,@"\$\((Enable\w+Probe)\)").Select(x=>x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();Need(flags.Length==44,"actual44 profile flags");
        foreach(string other in flags.Where(x=>x!=Flag&&x!="EnableM01LoadedImageStackProbe"))Reject([..positive,"-"+other],other=="EnableM04SeededCollectorChurnProbe"?"m04_churn_requires_gc_profile":other=="EnableM03FirstRealExitBootServicesProbe"?"m03_first_exit_requires property=EnableM01LiveAllocatorProbe":other=="EnableM04GcStaticRootMaterializationProbe"?"m04_gc_static_profile_invalid property=EnableM01LiveAllocatorProbe":other=="EnableM03TypedExitBootServicesCallProbe"?"m03_typed_ebs_profile_conflict property=EnableM01LoadedImageStackProbe":"m04_type_manager_profile_conflict property="+other);
        Reject(["-"+Flag,"-EnableM01LoadedImageStackProbe"],"m04_type_manager_requires_build_and_original_capture");
        Reject(["-BuildEfi","-"+Flag],"m04_type_manager_requires_original_loaded_image");
        Reject([..positive,"-SkipImageBuild"],"m04_type_manager_requires_build_and_original_capture");
        Reject([..positive,"-M01RawObservationLog",""],"m04_type_manager_requires_build_and_original_capture");
        Reject([..positive,"-RequireNativeAppMarkers"],"m04_type_manager_requires_build_and_original_capture");
        foreach(string option in new[]{"RequireMarker","RequireOrderedMarker","RequireUniqueMarker","ForbidMarker","MarkerPreset"})Reject([..positive,"-"+option,"CLROS_M04_011_READY"],"m04_type_manager_forbids_override");
        Reject(["-RequireMarker","CLROS_M04_011_READY"],"m04_type_manager_requires_explicit_profile");
        foreach(string name in new[]{"build-efi-shell.ps1","test-efi-artifact.ps1"})foreach(bool missingM01 in new[]{true,false}){
            string path=Path.Combine(root,"scripts",name);string[] mode=name=="build-efi-shell.ps1"?["-PreflightOnly"]:["-EfiArtifact","missing.EFI"];
            string[] args=[..mode,"-"+Flag,..(missingM01?Array.Empty<string>():new[]{"-EnableM01LoadedImageStackProbe","-EnableM01LiveAllocatorProbe"})];
            Result failure=Process(Path.Combine(directory,(n++).ToString("D3")),"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",path,..args]);
            string reason=missingM01?"m04_type_manager_requires_original_loaded_image":"m04_type_manager_profile_conflict property=EnableM01LiveAllocatorProbe";
            Need(failure.Exit!=0&&!failure.Timeout&&(failure.Output+failure.Error).Contains(reason,StringComparison.Ordinal),"closed producer/verifier "+name+" "+reason);
        }
        Passed(Dry());Need(Hash(script)==before,"unchanged profile input");
    }
    private static void TestArtifacts()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));int compile=project.Descendants("Compile").Count();
        foreach(bool enabled in new[]{false,true})
        {
            string directory=Scratch(enabled?"enabled":"default"),artifact=Path.Combine(root,"artifacts/efi/issue544-"+Guid.NewGuid().ToString("N")+".EFI");
            Result result=Process(directory,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/build-efi-shell.ps1"),"-EfiOutputPath",artifact,"-LogPath",Path.Combine(directory,"publish.log"),..(enabled?new[]{"-EnableM04TypeManagerModuleRegistrationProbe","-EnableM01LoadedImageStackProbe"}:Array.Empty<string>())]);Passed(result);
            Need(result.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal)&&result.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),"actual producer/verifier");
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(artifact+suffix,Path.Combine(directory,"image.EFI"+suffix));
            foreach(var pair in new[]{("src/OS/bin/Release/net10.0/win-x64/OS.dll","OS.dll"),("src/OS/obj/Release/net10.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net10.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(root,pair.Item1),Path.Combine(directory,pair.Item2));
            using var json=JsonDocument.Parse(File.ReadAllText(artifact+".link.json"));var evaluation=json.RootElement.GetProperty("postRestoreEvaluation");
            Need(evaluation.GetProperty("compileCount").GetInt32()==compile,"actual evaluated Compile membership");
            foreach(var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray())Need(string.Equals(Hash(Path.Combine(root,input.GetProperty("path").GetString()!)),input.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"actual raw source identity");
            foreach(var property in evaluation.GetProperty("properties").EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)))
                Need(property.Value.GetString()==(enabled&&(property.Name==Flag||property.Name=="EnableM01LoadedImageStackProbe")?"true":"false"),"actual profile "+property.Name);
            using var stream=File.OpenRead(Path.Combine(directory,"OS.dll"));using var pe=new PEReader(stream);var metadata=pe.GetMetadataReader();Need(metadata.AssemblyReferences.Count==0,"zero AssemblyRefs");
            var types=metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Where(t=>metadata.GetString(t.Name)=="NativeAotModuleRegistrationProbe").ToArray();
            Need(types.Length==(enabled?1:0),"conditional registration type absent/default and present/enabled");
            if(enabled)foreach(string name in new[]{"Execute","ReadWord","ReadAbi","Zero","Preserved","Run"}){
                var methods=types[0].GetMethods().Where(h=>metadata.GetString(metadata.GetMethodDefinition(h).Name)==name).ToArray();Need(methods.Length==1&&metadata.GetMethodDefinition(methods[0]).RelativeVirtualAddress!=0,"actual registration method "+name);
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
