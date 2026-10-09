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

    private const string Flag="EnableM03FirmwareEntropySeedProbe";

    private static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static string Root()=>HostToolProcess.FindRepositoryRoot();
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scratch(string tag){string path=Path.Combine(Root(),"artifacts/host-tests/m03-entropy-"+tag+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
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
    [HostTest("m03.003.efi conditional source and original firmware authority", HostLane.Fast, 0)]
    private static void TestSource()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        var defaults=project.Descendants(Flag).ToArray();Need(defaults.Length==1&&defaults[0].Value=="false"&&(string?)defaults[0].Attribute("Condition")=="'$("+Flag+")' == ''","one false default");
        var define=project.Descendants("DefineConstants").Single(x=>x.Value.Contains("M03_FIRMWARE_ENTROPY_SEED_PROBE",StringComparison.Ordinal));
        Need((string?)define.Parent?.Attribute("Condition")=="'$("+Flag+")' == 'true'"&&define.Value=="$(DefineConstants);M03_FIRMWARE_ENTROPY_SEED_PROBE","exact enabled symbol");
        var compile=project.Descendants("Compile").ToArray();Need(compile.Length==OS.Tooling.EfiContracts.CompileItems.Count&&(string?)compile[89].Attribute("Include")=="..\\OS.Kernel\\Diagnostics\\FirmwareEntropySeedCaptureProbeAuthority.cs","one appended authority");
        string entry=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));string core=entry[entry.IndexOf("    internal static class FirmwareEntropyCapture",StringComparison.Ordinal)..entry.IndexOf("    private const int OwnedMemoryMapBufferSize",StringComparison.Ordinal)];
        foreach(string forbidden in new[]{"new byte[","stackalloc","throw ","new object","string ","GetHash","Console.","AllocatePool(","AllocatePages("})Need(!core.Contains(forbidden,StringComparison.Ordinal),"finite native core "+forbidden);
        Need(core.Contains("s->Locate(&guid, null, (void**)&s->Rng)",StringComparison.Ordinal)&&core.Contains("get(rng, null, (nuint)RequestedBytes, s->Scratch->Data)",StringComparison.Ordinal),"original pointer call shapes");
        Need(core.Contains("WriteUnsignedDecimal(console, serial, ClearedBytes(s))",StringComparison.Ordinal)&&!core.Contains("Zero(scratch->Data, 0, 32) ?",StringComparison.Ordinal),"no diagnostic over uncleared provider bytes");
        Need(core.Contains("MethodImplOptions.NoInlining",StringComparison.Ordinal)&&core.Contains("s->PublishedBytes = RequestedBytes; s->Phase = 3",StringComparison.Ordinal),"private real loads and publication last");
        Need(entry.Contains("if (!FirmwareEntropyCapture.Run(systemTable, console, serial))",StringComparison.Ordinal)&&entry.Contains("while (true) { }",StringComparison.Ordinal),"failed guest terminal leaves success path intact");
        Need(string.Equals(Hash(Path.Combine(root,"src/OS.Kernel/Boot/AnyFirmwareDerivedEntropySeedNeededFirmwareShutdown.cs")),OS.Tooling.EfiContracts.FirmwareEntropyHostOracleSha256,StringComparison.OrdinalIgnoreCase),"frozen host contract");
    }
    [HostTest("m03.003.efi actual numeric core mapped ABI guards and private retention", HostLane.Native, 1)]
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
    [HostTest("m03.003.efi literal raw grammar and lifecycle controls", HostLane.Fast, 2)]
    private static void TestTranscript()
    {
        string directory=Scratch("transcript"); byte[] good=FirmwareTranscriptControls.Bytes(ProtocolLines);
        var control=new FirmwareTranscriptControls(directory,path=>OS.Tooling.QemuTranscripts.AssertM03FirmwareEntropyTranscript(path));
        control.Check(good,true);
        for(int i=0;i<ProtocolLines.Length;i++)
        {
            foreach(string replacement in new[]{"",ProtocolLines[i]+" "," "+ProtocolLines[i],ProtocolLines[i]+" extra=1",ProtocolLines[i]+"\0"})
            { string[] copy=[..ProtocolLines];copy[i]=replacement;control.Check(FirmwareTranscriptControls.Bytes(copy),false); }
            string[] lower=[..ProtocolLines];lower[i]=lower[i].ToLowerInvariant();if(lower[i]!=ProtocolLines[i])control.Check(FirmwareTranscriptControls.Bytes(lower),false);
            control.Check(FirmwareTranscriptControls.Bytes(ProtocolLines.Take(i+1).Concat(ProtocolLines.Skip(i))),false);
        }
        foreach(var pair in new (string Old,string New)[]{("requested=32","requested=31"), ("bytes=32","bytes=31"), ("scratchCleared=32","scratchCleared=0"), ("guards=64","guards=63"), ("tails=64","tails=63"), ("published=1","published=0"), ("status=0 calls=1 found=1","status=9223372036854775815 calls=1 found=1"), ("regions=4","regions=3"), ("freeBytes=81920","freeBytes=81921")})
        {
            string text=Encoding.ASCII.GetString(good),changed=text.Replace(pair.Old,pair.New);Need(text!=changed,"mutation changed input");control.Check(Encoding.ASCII.GetBytes(changed),false);
        }
        string[] swap=[..ProtocolLines];swap[15]=ProtocolLines[14]; control.Check(FirmwareTranscriptControls.Bytes(swap),false);
        control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace("\r\n","\n")),false);
        control.Check(good[..^1],false);control.Check([..good,0],false);control.Check([..good,255],false);
        control.Check(good,false,[255]);control.Check(good,false,Encoding.ASCII.GetBytes("arbitrary"));control.Check(good,false,post:[..FirmwareTranscriptControls.Suffix,65]);
        control.Check(good,true,post:FirmwareTranscriptControls.ShortSuffix);control.Check(good,false,post:[..FirmwareTranscriptControls.ShortSuffix,65]);control.Check(good,false,post:[]);
        foreach(byte[] suffix in new[]{FirmwareTranscriptControls.Suffix,FirmwareTranscriptControls.ShortSuffix})
        {
            foreach(int index in new[]{0,suffix.Length-1}){byte[] changed=[..suffix];changed[index]^=1;control.Check(good,false,post:changed);}
            control.Check(good,false,post:[..suffix,65]);control.Check(good,false,post:[..suffix,13,10]);control.Check(good,false,post:suffix[..^1]);
        }
        int firstCr=Array.IndexOf(FirmwareTranscriptControls.ShortSuffix,(byte)13);
        foreach(int length in new[]{1,firstCr,firstCr+2,FirmwareTranscriptControls.ShortSuffix.Length-1,FirmwareTranscriptControls.ShortSuffix.Length+1,FirmwareTranscriptControls.Suffix.Length-1})control.Check(good,false,post:FirmwareTranscriptControls.Suffix[..length]);
        string[] missing=[..ProtocolLines[..14],"M03_ENTROPY_DISCOVERY status=9223372036854775822 calls=1 found=0","M03_ENTROPY_ACQUIRE status=0 calls=0 requested=32","M03_ENTROPY_RETAIN bytes=0 scratchCleared=0 guards=64 tails=64 published=0","M03_ENTROPY_UNAVAILABLE retainedUnchanged=1 published=0 rngCalls=0","TEST_FAIL m03.003.efi source_unavailable"];
        var absent=new FirmwareTranscriptControls(directory,path=>OS.Tooling.QemuTranscripts.AssertM03FirmwareEntropyTranscript(path,true));
        void Missing(byte[] bytes,bool expected)=>absent.Check(bytes,expected,post:[]);
        Missing(FirmwareTranscriptControls.Bytes(missing),true);
        for(int i=0;i<missing.Length;i++){string[] changed=[..missing];changed[i]+=" extra=1";Missing(FirmwareTranscriptControls.Bytes(changed),false);changed=[..missing];changed[i]="";Missing(FirmwareTranscriptControls.Bytes(changed),false);}
        foreach(var pair in new[]{("rngCalls=0","rngCalls=1"),("scratchCleared=0","scratchCleared=32"),("retainedUnchanged=1","retainedUnchanged=0"),("9223372036854775822","9223372036854775815"),("published=0","published=1")})Missing(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(FirmwareTranscriptControls.Bytes(missing)).Replace(pair.Item1,pair.Item2)),false);
        Missing([..FirmwareTranscriptControls.Bytes(missing),65],false);Missing([..FirmwareTranscriptControls.Bytes(missing),..FirmwareTranscriptControls.Suffix],false);
        control.Check(good,true);
        string[] ordered=ProtocolLines.Select(line=>line.StartsWith("UEFI_MEMORY_MAP_DESCRIPTOR_COUNT ",StringComparison.Ordinal)||line.StartsWith("BOOT_MEMORY_REGION_COUNT ",StringComparison.Ordinal)||line.StartsWith("BOOT_PAGE_ALLOCATOR_READY ",StringComparison.Ordinal)||line.StartsWith("M03_ENTROPY_",StringComparison.Ordinal)?line.Split(' ')[0]:line).ToArray();
        var runner=new StandardFirmwareRunnerControls(directory,ProtocolLines,ordered,["OS_SMOKE_FAIL","TEST_FAIL"],Flag,true);
        runner.CompleteMatrix();
        Result dry=Tool("qemu","smoke","-BootPolicy","Diagnostic","-BuildEfi","-"+Flag,"-FirmwareRngSource","virtio","-DryRun");Passed(dry);
        string[] Vector(string prefix)=>dry.Output.Split('\n').Single(x=>x.StartsWith(prefix+" ",StringComparison.Ordinal))[(prefix.Length+1)..].TrimEnd('\r').Split(';').Select(x=>x.Trim()).ToArray();
        foreach(bool shortened in new[]{false,true})
        {
            byte[] framed=runner.Frame(shortened);File.WriteAllBytes(Path.Combine(directory,shortened?"short-framed.serial.bin":"full-framed.serial.bin"),framed);
            var state=new OS.Tools.QemuRunner.SerialMarkerStateMachine(Vector("REQUIRED_MARKERS"),Vector("FORBIDDEN_MARKERS"),orderedMarkers:Vector("ORDERED_MARKERS"),uniqueMarkers:Vector("UNIQUE_MARKERS"));
            Need(state.Feed(framed).Succeeded,"complete framed Feed");var actual=state.Complete();
            Need(actual.Passed&&actual.TotalBytes==framed.Length&&actual.LinesProcessed==ProtocolLines.Length+(shortened?4:5)&&actual.InvalidByteCount==(shortened?15:207)&&actual.InvalidNoiseLineCount==(shortened?1:2)&&actual.OrderedSeenCount==ProtocolLines.Length&&actual.UniqueSeenCount==ProtocolLines.Length,"complete framed accounting");
        }
        Console.WriteLine("TRANSCRIPT_CONTROLS m03.entropy cases="+(control.Cases+runner.Cases)+" stderrWhitespaceRejected=true");
    }

    private static readonly string[] ProtocolLines = [
        "TEST_START efi.main","EFI_CONSOLE_READY","UEFI_MEMORY_MAP_READY","UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4","UEFI_MEMORY_MAP_OWNED",
        "BOOT_HANDOFF_READY","UEFI_MEMORY_MAP_NORMALIZED","BOOT_MEMORY_MAP_ACCEPTED","BOOT_MEMORY_REGIONS_READY","BOOT_MEMORY_REGION_COUNT 4",
        "BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0",
        "BOOT_ACPI_RSDP_HANDOFF_READY","UEFI_ACPI_RSDP_READY","TEST_START m03.003.efi",
        "M03_ENTROPY_DISCOVERY status=0 calls=1 found=1","M03_ENTROPY_ACQUIRE status=0 calls=1 requested=32",
        "M03_ENTROPY_RETAIN bytes=32 scratchCleared=32 guards=64 tails=64 published=1","CLROS_M03_003_READY bytes=32","TEST_PASS m03.003.efi",
        "TEST_START boot.sequence","BOOT_PHASE PhaseA.Audit START","BOOT_PHASE PhaseA.Audit PASS","BOOT_PHASE PhaseA.Diagnostics START","BOOT_PHASE PhaseA.Diagnostics PASS","OS_BOOT_OK","TEST_PASS boot.sequence","OS_SMOKE_PASS"];
    [HostTest("m03.003.efi profile options and actual complete marker streamer", HostLane.Fast, 3)]
    private static void TestProfile()
    {
        string root=Root(),directory=Scratch("profile"),script=Path.Combine(root,"tools/OS.Tooling/QemuProfiles.cs"),before=Hash(script);
        Result Dry(params string[] args)=>Tool(["qemu","smoke","-BootPolicy","Diagnostic","-DryRun",..args]);
        string[] positive=["-BuildEfi","-"+Flag,"-FirmwareRngSource","virtio"];
        Result result=Dry(positive);Passed(result);Need(result.Output.Contains("TEST_PASS qemu.runner.dryrun",StringComparison.Ordinal),"actual dry-run terminal");
        string[] Vector(string prefix)=>result.Output.Split('\n').Single(x=>x.StartsWith(prefix+" ",StringComparison.Ordinal))[(prefix.Length+1)..].TrimEnd('\r').Split(';').Select(x=>x.Trim()).ToArray();
        var required=Vector("REQUIRED_MARKERS");var ordered=Vector("ORDERED_MARKERS");var unique=Vector("UNIQUE_MARKERS");var forbidden=Vector("FORBIDDEN_MARKERS");
        foreach(var vector in new[]{required,ordered,unique})Need(vector.Length==ProtocolLines.Length&&vector.Distinct(StringComparer.Ordinal).Count()==ProtocolLines.Length,"complete canonical unique vectors");
        Need(required.Order(StringComparer.Ordinal).SequenceEqual(ordered.Order(StringComparer.Ordinal),StringComparer.Ordinal)&&ordered.SequenceEqual(unique,StringComparer.Ordinal),"actual complete set and ordered/unique sequence");
        OS.Tools.QemuRunner.SerialMarkerCompletionResult Stream(string[] lines){var state=new OS.Tools.QemuRunner.SerialMarkerStateMachine(required,forbidden,orderedMarkers:ordered,uniqueMarkers:unique);Need(state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n",lines)+"\r\n")).Succeeded,"actual streamer Feed");return state.Complete();}
        var accepted=Stream(ProtocolLines);Need(accepted.Passed&&accepted.LinesProcessed==ProtocolLines.Length&&accepted.OrderedSeenCount==ProtocolLines.Length&&accepted.UniqueSeenCount==ProtocolLines.Length,"genuine full-token streamer success");
        foreach(int index in new[]{14,15,16,17}){
            var missing=Stream(ProtocolLines.Where((_,i)=>i!=index).ToArray());Need(!missing.Passed&&missing.MissingRequiredMarkers!.Count>0,"missing entropy row");
            var duplicate=Stream(ProtocolLines.Take(index+1).Concat(ProtocolLines.Skip(index)).ToArray());Need(!duplicate.Passed&&duplicate.DuplicateUniqueOccurrenceCount==1,"duplicate entropy row");
            string[] swapped=[..ProtocolLines];(swapped[index],swapped[index+1])=(swapped[index+1],swapped[index]);Need(Stream(swapped).OutOfOrderOccurrenceCount>0,"reordered entropy row");
        }
        void Reject(string[] args,string reason){Result failure=Dry(args);Need(failure.Exit!=0&&!failure.Timeout&&(failure.Output+failure.Error).Contains(reason,StringComparison.Ordinal),"profile rejection "+reason);}
        string project=File.ReadAllText(Path.Combine(root,"src/OS/OS.csproj"));string[] flags=Regex.Matches(project,@"\$\((Enable\w+Probe)\)").Select(x=>x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();Need(flags.Length==OS.Tooling.EfiContracts.DiagnosticProbes.Count,"complete profile flags");
        foreach(string other in flags.Where(x=>x!=Flag))Reject([..positive,"-"+other],EfiProfileTestControls.MissingParentReason([Flag, other]) ?? (other=="EnableM03FirstRealExitBootServicesProbe"?"m03_first_exit_requires property=EnableM01LoadedImageStackProbe":other=="EnableM04GcStaticRootMaterializationProbe"?"m04_gc_static_profile_invalid property=EnableM01LoadedImageStackProbe":other=="EnableM03TypedExitBootServicesCallProbe"?"m03_typed_ebs_profile_conflict property=EnableM03FirmwareEntropySeedProbe":other=="EnableM04TypeManagerModuleRegistrationProbe"?"m04_type_manager_requires_original_loaded_image":"m03_firmware_entropy_profile_conflict property="+other));
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
    [HostTest("m03.003.efi default and enabled no-stdlib artifacts", HostLane.Native, 4)]
    private static void TestArtifacts()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));int compile=project.Descendants("Compile").Count();
        foreach(bool enabled in new[]{false,true})
        {
            string directory=Scratch(enabled?"enabled":"default"),artifact=Path.Combine(root,"artifacts/efi/issue541-"+Guid.NewGuid().ToString("N")+".EFI");
            Result result=Tool(["efi","build","-EfiOutputPath",artifact,"-LogPath",Path.Combine(directory,"publish.log"),..(enabled?new[]{"-EnableM03FirmwareEntropySeedProbe"}:Array.Empty<string>())]);Passed(result);
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
