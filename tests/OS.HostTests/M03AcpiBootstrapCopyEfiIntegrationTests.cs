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
#if ACPI_BOOTSTRAP_HOST
using OS.Boot;

// The whole actual EFI source is linked below. Only private numeric methods are
// invoked on admitted host storage; EfiMain, firmware output and devices never run.
internal static unsafe class AcpiBootstrapHost
{
    private const int Capacity = 266240, SourceCapacity = 1048576;
    private static byte* s_source;
    private static AcpiBootstrapWorkspace* s_work;
    private static byte* s_owned;
    private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void* VirtualAlloc(void* address, nuint size, uint allocation, uint protect);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(void* address, nuint size, uint kind);
    private static void Need(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static object P(void* pointer, Type type) => Pointer.Box(pointer, type);
    private static object W => P(s_work, typeof(AcpiBootstrapWorkspace*));
    private static object O => P(s_owned, typeof(byte*));
    private static object? Call(string name, params object?[] args) => typeof(EfiEntry).GetMethod(name, PrivateStatic)!.Invoke(null, args);
    private static bool Capture(int capacity = Capacity)
    {
        object?[] args = [W, O, capacity, (ulong)s_source, 0UL];
        bool result = (bool)Call("TryCaptureAcpiBootstrap", args)!;
        if (!result) Console.WriteLine("ACPI_CAPTURE_REJECT status=" + args[4]);
        return result;
    }
    private static bool ReadBack(int capacity = Capacity) => (bool)Call("ReadBackAcpiBootstrap", W, O, capacity)!;
    private static void Put32(byte* p, uint value) { for (int i = 0; i < 4; i++) p[i] = (byte)(value >> (i * 8)); }
    private static void Put64(byte* p, ulong value) { for (int i = 0; i < 8; i++) p[i] = (byte)(value >> (i * 8)); }
    private static void Checksum(byte* p, int length, int position)
    { p[position] = 0; int sum = 0; for (int i = 0; i < length; i++) sum += p[i]; p[position] = unchecked((byte)-sum); }
    private static void Sdt(byte* p, string signature, int length)
    {
        new Span<byte>(p, length).Clear();
        for (int i = 0; i < 4; i++) p[i] = (byte)signature[i];
        Put32(p + 4, (uint)length); p[8] = 1;
        for (int i = 10; i < length; i++) p[i] = (byte)(i * 7 + 3);
        Checksum(p, length, 9);
    }
    private static void Map(uint type = 9, ulong count = 1, ulong length = SourceCapacity)
    {
        object buffer = Activator.CreateInstance(typeof(OwnedMemoryRegionBuffer))!;
        GCHandle pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var records = (BootMemoryRegionRecord*)pin.AddrOfPinnedObject();
            records[0] = new(type, (ulong)s_source, 0, length / 4096, 0, length, (ulong)s_source + length);
            if (count == 2) records[1] = new(type, (ulong)s_source + length, 0, 1, 0, 4096, (ulong)s_source + length + 4096);
            typeof(EfiEntry).GetField("s_ownedMemoryRegions", PrivateStatic)!.SetValue(null, buffer);
        }
        finally { pin.Free(); }
        typeof(EfiEntry).GetField("s_memoryRegionMaterialization", PrivateStatic)!.SetValue(null,
            BootMemoryRegionMaterializationResult.Success(count, 4096, 256, SourceCapacity, 0, 0));
    }
    private static void Reset(int revision = 2, bool xsdt = true)
    {
        new Span<byte>(s_source, SourceCapacity).Clear();
        new Span<byte>(s_work, sizeof(AcpiBootstrapWorkspace)).Clear();
        new Span<byte>(s_owned, Capacity + 32).Fill(0xBC);
        Map();
        byte* root = s_source + 4096; byte* mcfg = s_source + 8192; byte* madt = s_source + 16384; byte* hpet = s_source + 24576;
        Sdt(mcfg, "MCFG", 60); new Span<byte>(mcfg + 36, 8).Clear(); Put64(mcfg + 44, 0xE0000000); mcfg[54] = 0; mcfg[55] = 255; Checksum(mcfg, 60, 9);
        Sdt(madt, "APIC", 52); Sdt(hpet, "HPET", 56);
        int width = revision >= 2 && xsdt ? 8 : 4;
        Sdt(root, width == 8 ? "XSDT" : "RSDT", 36 + 3 * width);
        for (int i = 0; i < 3; i++)
        {
            ulong value = (ulong)(i == 0 ? madt : i == 1 ? mcfg : hpet);
            if (width == 8) Put64(root + 36 + i * width, value); else Put32(root + 36 + i * width, (uint)value);
        }
        Checksum(root, 36 + 3 * width, 9);
        byte[] signature = "RSD PTR "u8.ToArray(); signature.AsSpan().CopyTo(new Span<byte>(s_source, 8));
        "CLROS "u8.CopyTo(new Span<byte>(s_source + 9, 6)); s_source[15] = (byte)revision;
        Put32(s_source + 16, (uint)(ulong)root);
        if (revision >= 2) { Put32(s_source + 20, 48); Put64(s_source + 24, xsdt ? (ulong)root : 0); }
        Checksum(s_source, 20, 8); if (revision >= 2) Checksum(s_source, 48, 32);
    }
    private static void UnchangedDestination() => Need(new ReadOnlySpan<byte>(s_owned, Capacity + 32).IndexOfAnyExcept((byte)0xBC) == -1, "entire destination unchanged");
    private static void Reject(Action mutate)
    { Reset(); mutate(); Need(!Capture() && s_work->Phase != 3, "rejected before publication"); UnchangedDestination(); }
    private static byte[] Bytes(byte* p, int length) => new ReadOnlySpan<byte>(p, length).ToArray();
    private static void Parity(int revision, bool xsdt)
    {
        Reset(revision, xsdt);
        int rootLength = revision >= 2 && xsdt ? 60 : 48;
        byte[] rsdp = Bytes(s_source, revision >= 2 ? 48 : 20), root = Bytes(s_source + 4096, rootLength);
        AcpiTableSource[] sources = [new((ulong)(s_source + 8192), Bytes(s_source + 8192, 60)),
            new((ulong)(s_source + 24576), Bytes(s_source + 24576, 56)), new((ulong)(s_source + 16384), Bytes(s_source + 16384, 52))];
        byte[] expected = new byte[Capacity];
        var oracle = RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap.Copy(rsdp, (ulong)s_source, root,
            (ulong)(s_source + 4096), new ulong[256], sources, expected);
        Need(oracle.Succeeded && Capture() && s_work->Phase == 3 && ReadBack(), "actual capture and independent host oracle");
        Need(s_work->TotalBytes == oracle.RequiredBytes && new ReadOnlySpan<byte>(s_owned + 16, oracle.CopiedBytes).SequenceEqual(expected.AsSpan(0, oracle.CopiedBytes)), "all five full bytes");
        AcpiOwnedTableDescriptor[] descriptors = [oracle.Rsdp, oracle.Root, oracle.Mcfg, oracle.Madt, oracle.Hpet];
        for (int i = 0; i < 5; i++)
        {
            var d = descriptors[i]; ulong* row = s_work->Descriptors + i * 6;
            Need(row[0] == d.PhysicalAddress && row[1] == d.Signature && row[2] == (ulong)d.Length &&
                row[3] == unchecked((ulong)d.RootPointerIndex) && row[4] == (ulong)d.Offset && row[5] == (ulong)d.Length, "descriptor parity " + i);
        }
        byte[] retained = Bytes(s_owned, Capacity + 32);
        new Span<byte>(s_source, SourceCapacity).Fill(0xEE);
        Need(ReadBack() && new ReadOnlySpan<byte>(s_owned, Capacity + 32).SequenceEqual(retained), "all sources mutated; owned bytes retained");
        Need(!Capture() && s_work->Phase == 3 && new ReadOnlySpan<byte>(s_owned, Capacity + 32).SequenceEqual(retained), "one shot preserves original publication");
    }
    private static void Run(string name, Action action)
    { Console.WriteLine("TEST_START m03.acpi.host " + name); action(); Console.WriteLine("TEST_PASS m03.acpi.host " + name); }
    private static void Stage()
    {
        Need((bool)Call("TryBeginAcpiBootstrap", W, O, Capacity)!, "actual begin");
        s_work->SourceRsdp = (ulong)s_source;
        object?[] rsdp = [W, O, Capacity, (ulong)s_source, 0UL];
        Need((bool)Call("TryReadAndValidateAcpiRsdp", rsdp)!, "actual RSDP");
        object?[] args = [W, O, Capacity, 0UL];
        Need((bool)Call("TryReadAndValidateRootSdt", args)! && (bool)Call("TryDiscoverSelectedAcpiTables", args)!, "actual root/children");
        s_work->Phase = 2; // The real orchestration's one scalar handoff, to exercise prewrite negatives.
    }
    private static int Main()
    {
        for (uint hint = 0x30000000; hint < 0x60000000 && s_source == null; hint += 0x01000000)
            s_source = (byte*)VirtualAlloc((void*)(nuint)hint, SourceCapacity, 0x3000, 4);
        Need(s_source != null && (ulong)s_source + SourceCapacity < uint.MaxValue, "readable low host allocation for real RSDT pointers");
        s_work = (AcpiBootstrapWorkspace*)NativeMemory.AlignedAlloc((nuint)sizeof(AcpiBootstrapWorkspace), 64);
        s_owned = (byte*)NativeMemory.AlignedAlloc(Capacity + 32, 64);
        try
        {
            Run("five copies ACPI1 XSDT fallback and retained parity", () => { Parity(0, false); Parity(2, true); Parity(2, false); Map(10); });
            Run("envelope selection and missing corrupt earlier table rejection", () =>
            {
                Reject(() => s_source[0] ^= 1);
                Reject(() => { new Span<byte>(s_source + 9, 6).Clear(); Checksum(s_source, 20, 8); Checksum(s_source, 48, 32); });
                Reject(() => s_source[8] ^= 1);
                Reject(() => { s_source[15] = 1; Checksum(s_source, 20, 8); });
                Reject(() => Put32(s_source + 20, 4097));
                Reject(() => s_source[32] ^= 1);
                Reject(() => { s_source[4096] ^= 1; Checksum(s_source + 4096, 60, 9); });
                Reject(() => Put32(s_source + 4100, 65537));
                Reject(() => s_source[4105] ^= 1);
                Reject(() => { Put64(s_source + 4132, 0); Checksum(s_source + 4096, 60, 9); });
                Reject(() => { Put64(s_source + 4148, (ulong)(s_source + 16384)); Checksum(s_source + 4096, 60, 9); });
                Reject(() => { Put64(s_source + 4148, (ulong)(s_source + 16390)); Checksum(s_source + 4096, 60, 9); });
                Reject(() => Sdt(s_source + 24576, "SSDT", 56));
                Reject(() => { Sdt(s_source + 16384, "SSDT", 52); s_source[16393] ^= 1; });
                Reject(() => { s_source[8192 + 36] = 1; Checksum(s_source + 8192, 60, 9); });
                Reject(() => { s_source[8192 + 54] = 2; s_source[8192 + 55] = 1; Checksum(s_source + 8192, 60, 9); });
                Reject(() => Map(7)); Reject(() => Map(count: 0)); Reject(() => Map(count: 4097));
                Reject(() => Map(length: 24576 + 55));
                Reset(); Map(10); Need(Capture(), "NVS type10 permitted");
                Reset(); Sdt(s_source + 32768, "SSDT", 40);
                Put64(s_source + 4148, (ulong)(s_source + 32768)); Put64(s_source + 4156, (ulong)(s_source + 24576));
                Put32(s_source + 4100, 68); Checksum(s_source + 4096, 68, 9);
                Need(Capture(), "earlier valid full nonmatch followed by required HPET");
            });
            Run("actual final envelope helpers reject changed header fields", () =>
            {
                Reset(); byte[] original = Bytes(s_source, 48);
                foreach(int field in new[]{0,9,15,20,32})
                {
                    original.AsSpan().CopyTo(new Span<byte>(s_source,48));
                    if(field==9)new Span<byte>(s_source+9,6).Clear();else s_source[field]^=1;
                    if(field!=32){Checksum(s_source,20,8);Checksum(s_source,48,32);}
                    Need(!(bool)Call("AcpiRsdpEnvelope",P(s_source,typeof(byte*)),48,(byte)2)!,"RSDP final field "+field);
                }
                Reset(); byte* root=s_source+4096;byte[] before=Bytes(root,60);
                foreach(int field in new[]{0,4,9})
                {before.AsSpan().CopyTo(new Span<byte>(root,60));root[field]^=1;if(field!=9)Checksum(root,60,9);Need(!(bool)Call("AcpiSdtEnvelope",P(root,typeof(byte*)),60,0x54445358U)!,"SDT final field "+field);}
                Need(!(bool)Call("AcpiSdtEnvelope",P(root,typeof(byte*)),35,0x54445358U)!,"short envelope before field reads");
            });
            Run("full capacity aliases range guards and cross-region denial", () =>
            {
                Reset();ulong w=(ulong)s_work,o=(ulong)s_owned;
                foreach(ulong[] pair in new ulong[][]{[0,o],[ulong.MaxValue-100,o],[w+1,o],[w,w],[w,w+(ulong)sizeof(AcpiBootstrapWorkspace)-1],[w,ulong.MaxValue-31]})
                    Need(!(bool)Call("AcpiBootstrapStorage",pair[0],pair[1],Capacity)!,"numeric storage admission");
                foreach(int capacity in new[]{-1,0,Capacity+1}) Need(!(bool)Call("AcpiBootstrapStorage",w,o,capacity)!,"capacity");
                foreach(ulong address in new[]{w,w+(ulong)sizeof(AcpiBootstrapWorkspace)-1,o,o+16,o+Capacity+31,ulong.MaxValue-10})
                    Need(!(bool)Call("AcpiSourceAdmission",W,O,Capacity,address,36)!,"source overlaps full capacities");
                Need(!(bool)Call("AcpiOverlap",w,16UL,w+16,16UL)!,"touching boundaries");
                object header=P(s_work->Header,typeof(byte*));
                Need((bool)Call("TryReadAcpiPhysicalBytes",W,O,Capacity,(ulong)s_source+SourceCapacity-36,36,header,36)!,"exact range end");
                Need(!(bool)Call("TryReadAcpiPhysicalBytes",W,O,Capacity,(ulong)s_source+SourceCapacity-35,36,header,36)!,"one byte outside");
                Need(!(bool)Call("TryReadAcpiPhysicalBytes",W,O,Capacity,(ulong)s_source,36,P(s_owned,typeof(byte*)),36)!,"not a finite read target");
                Map(count:2,length:4096);
                Need(!(bool)Call("TryReadAcpiPhysicalBytes",W,O,Capacity,(ulong)s_source+4080,36,header,36)!,"contiguous but cross-region denied");
            });
            Run("prewrite metadata rejection exact capacity and full readback", () =>
            {
                Reset();Need(Capture(),"measure bounded payload");int total=(int)s_work->TotalBytes;
                Reset();Need(!Capture(total-1),"short destination");UnchangedDestination();
                Reset();Need(Capture(total)&&ReadBack(total),"exact capacity");
                Need(new ReadOnlySpan<byte>(s_owned+total+32,Capacity-total).IndexOfAnyExcept((byte)0xBC)==-1,"beyond declared capacity unchanged");
                foreach(int slot in new[]{0,1,2,3,4,5,6,7,8,9,12,13,14,15,18,19,20,21,24,25,26,27})
                {
                    Reset();Stage();s_work->Descriptors[slot]^=1;
                    object?[] args=[W,O,Capacity,0UL];Need(!(bool)Call("TryPublishAcpiBootstrap",args)!,"forged descriptor "+slot);UnchangedDestination();
                }
                foreach(int index in Enumerable.Range(0,16).Concat(Enumerable.Range(Capacity+16,16)).Concat(new[]{16,16+total/2,15+total,16+total,Capacity+15}))
                {Reset();Need(Capture(),"capture before corruption");s_owned[index]^=1;Need(!ReadBack(),"guard/payload/tail mismatch "+index);}
                Reset();Need(Capture(),"snapshot corruption baseline");s_work->Madt[40]^=1;Need(!ReadBack(),"snapshot versus retained comparison");
                Need(sizeof(AcpiBootstrapWorkspace)==335696&&sizeof(AcpiBootstrapOwnedBuffer)==266272,"actual whole struct layout");
                Console.WriteLine("M03_ACPI_POD workspace="+sizeof(AcpiBootstrapWorkspace)+" owned="+sizeof(AcpiBootstrapOwnedBuffer)+" payloadOffset=16 capacity=266240 descriptorSlots=30");
            });
            Console.WriteLine("TEST_PASS m03.acpi.host.tests");return 0;
        }
        finally{NativeMemory.AlignedFree(s_work);NativeMemory.AlignedFree(s_owned);Need(VirtualFree(s_source,0,0x8000),"release host source mapping");}
    }
}
#else
internal static class M03AcpiBootstrapCopyEfiIntegrationTests
{

    private static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static string Root()
    {DirectoryInfo? d=new(AppContext.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"CLROS.slnx")))d=d.Parent;return d?.FullName??throw new InvalidOperationException("repo root");}
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Scratch(string tag){string path=Path.Combine(Root(),"artifacts/host-tests/m03-acpi-"+tag+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    private sealed record Result(int Exit,bool Timeout,string Output,string Error);
    private static Result Process(string directory,string file,params string[] args)
    {
        Directory.CreateDirectory(directory);var start=new ProcessStartInfo(file){WorkingDirectory=Root(),UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in args)start.ArgumentList.Add(arg);
        DateTime begin=DateTime.UtcNow;File.WriteAllText(Path.Combine(directory,"command.json"),JsonSerializer.Serialize(new{File=file,Arguments=args,WorkingDirectory=Root(),StartedUtc=begin}));
        using var p=System.Diagnostics.Process.Start(start)!;var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();bool timeout=!p.WaitForExit(900000);if(timeout){p.Kill(true);p.WaitForExit();}
        string output=stdout.GetAwaiter().GetResult(),error=stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory,"stdout.log"),output);File.WriteAllText(Path.Combine(directory,"stderr.log"),error);
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{NativeExit=p.ExitCode,TimedOut=timeout,StartedUtc=begin,EndedUtc=DateTime.UtcNow}));
        return new(p.ExitCode,timeout,output,error);
    }
    private static Result Tool(params string[] args)
    {
        var result=HostToolProcess.RunTool(args);return new(result.ExitCode,false,result.Output,result.Error);
    }
    private static void Passed(Result result)=>Need(result.Exit==0&&!result.Timeout&&result.Error.Length==0,result.Output+result.Error);
    [HostTest("m03.002.efi conditional source and unchanged profile", HostLane.Native, 0)]
    private static void TestSource()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        Need(!project.Descendants("EnableAcpiPhysicalDiscoveryProbe").Any(),"existing property unset by default");
        var symbol=project.Descendants("DefineConstants").Single(x=>x.Value.Contains("ACPI_PHYSICAL_DISCOVERY_PROBE",StringComparison.Ordinal));
        Need(string.Equals((string?)symbol.Parent?.Attribute("Condition"),"'$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",StringComparison.Ordinal),"existing symbol requires explicit true");
        Need(project.Descendants("DefineConstants").Count(x=>x.Value.Contains("ACPI_PHYSICAL_DISCOVERY_PROBE",StringComparison.Ordinal))==1,"one existing symbol");
        Need(string.Equals(Hash(Path.Combine(root,"src/OS.Kernel/Boot/RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap.cs")),OS.Tooling.EfiContracts.AcpiBootstrapHostOracleSha256,StringComparison.OrdinalIgnoreCase),"frozen host oracle");
        string source=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));
        string core=source[source.IndexOf("    private static bool WriteAcpiPhysicalDiscoveryProbe(",StringComparison.Ordinal)..source.IndexOf("    private static bool TryParseMcfgSegments(",StringComparison.Ordinal)];
        foreach(string forbidden in new[]{"new byte[","stackalloc","throw ","AllocatePool(","AllocatePages(","ExitBootServices(","ReadOnlySpan", "Console."})Need(!core.Contains(forbidden,StringComparison.Ordinal),"finite EFI core "+forbidden);
        Need(core.IndexOf("if (!AcpiFinalAdmission(",StringComparison.Ordinal)<core.IndexOf("owned[i] =",StringComparison.Ordinal),"all admission before destination writes");
        TestScriptProfile();
    }
    [HostTest("m03.002.efi actual five-copy EFI methods and independent host parity", HostLane.Native, 1)]
    private static void TestCapture()
    {
        string root=Root(),directory=Scratch("actual-methods");
        string[] sources=["src/OS/Boot/EfiEntry.cs","src/OS/Boot/EfiConsoleAdapter.cs","tests/OS.HostTests/M03AcpiBootstrapCopyEfiIntegrationTests.cs"];
        string attribute="        internal sealed class RuntimeExportAttribute : Attribute\n        {\n            public RuntimeExportAttribute(string entry)\n            {\n            }\n        }";
        Need(File.ReadAllText(Path.Combine(root,"src/OS/Runtime/MinimalRuntime.cs")).Replace("\r\n","\n",StringComparison.Ordinal).Contains(attribute,StringComparison.Ordinal),"unchanged ordinary-host attribute projection");
        File.WriteAllText(Path.Combine(directory,"Attribute.cs"),"namespace System.Runtime {\n"+attribute+"\n}");
        string items=string.Join('\n',sources.Select(x=>"<Compile Include=\""+SecurityElement.Escape(Path.Combine(root,x))+"\"/>"));
        string project=Path.Combine(directory,"Capture.csproj");
        File.WriteAllText(project,"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion><OutputType>Exe</OutputType><StartupObject>AcpiBootstrapHost</StartupObject><AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>ACPI_BOOTSTRAP_HOST;ACPI_PHYSICAL_DISCOVERY_PROBE</DefineConstants></PropertyGroup><ItemGroup>"+items+"<Compile Include=\"Attribute.cs\"/><ProjectReference Include=\""+SecurityElement.Escape(Path.Combine(root,"src/OS.Kernel/OS.Kernel.csproj"))+"\"/></ItemGroup></Project>");
        string[] inputs=[..sources.Select(x=>Path.Combine(root,x)),Path.Combine(directory,"Attribute.cs"),project];
        var identities=inputs.Select(path=>new{Path=path,Bytes=new FileInfo(path).Length,Sha256=Hash(path)}).ToArray();
        File.WriteAllText(Path.Combine(directory,"inputs.json"),JsonSerializer.Serialize(identities));
        Passed(Process(Path.Combine(directory,"build"),"dotnet","build",project,"-c","Release","--nologo","-m:1","--disable-build-servers","-p:UseSharedCompilation=false"));
        Result run=Process(Path.Combine(directory,"run"),"dotnet",Path.Combine(directory,"bin/Release/net10.0/Capture.dll"));Passed(run);
        Need(run.Output.Contains("TEST_PASS m03.acpi.host.tests",StringComparison.Ordinal)&&run.Output.Split('\n').Count(x=>x.StartsWith("TEST_START m03.acpi.host ",StringComparison.Ordinal))==5,"complete actual child");
        foreach(var input in identities)Need(Hash(input.Path)==input.Sha256,"unchanged input "+input.Path);
    }
    [HostTest("m03.002.efi literal transcript and complete finite fields", HostLane.Fast, 2)]
    private static void TestLiterals()
    {
        string source=File.ReadAllText(Path.Combine(Root(),"src/OS/Boot/EfiEntry.cs"));
        string formatter=source[source.IndexOf("    private static void WriteAcpiBootstrapTranscript(",StringComparison.Ordinal)..source.IndexOf("    private static bool TryParseMcfgSegments(",StringComparison.Ordinal)];
        var literals=Regex.Matches(formatter,@"WriteAcpiBootstrapAscii\(console, serial, 0x([0-9A-F]{16}), ([1-8])\);");
        var decoded=new StringBuilder();foreach(Match m in literals){ulong value=Convert.ToUInt64(m.Groups[1].Value,16);for(int i=0;i<int.Parse(m.Groups[2].Value);i++)decoded.Append((char)(byte)(value>>(8*i)));}
        const string expected="TEST_START m03.002\r\nCLROS_M03_002_TABLE role=rsdprootmcfgapichpet physical= owned= offset= length= signature= rootIndex= compared= mismatches=CLROS_M03_002_RETAIN total= descriptors=5 guards=32 tail= mismatches=0\r\nCLROS_M03_002_READY firmwareAvailable=true exitBootServices=false\r\nTEST_PASS m03.002\r\n";
        Need(decoded.ToString()==expected,"exact source-emitted literal grammar independent of future captures");
        Need(Regex.Matches(formatter,"WriteUnsignedDecimal\\(console, serial,").Count==10&&formatter.Contains("role < 5",StringComparison.Ordinal),"eight row fields plus total/tail");
        TestTranscriptControls();
    }
    private static void TestScriptProfile()
    {
        string root=Root(),directory=Scratch("script-profile"),script=Path.Combine(root,"tools/OS.Tooling/QemuProfiles.cs");
        string before=Hash(script);
        var buildProfile=OS.Tooling.EfiProfile.Create("Diagnostic",new Dictionary<string,bool>{["EnableAcpiPhysicalDiscoveryProbe"]=true});
        Need(buildProfile.CommandArguments().Contains("--EnableAcpiPhysicalDiscoveryProbe"),"conditional build forwarding");
        Result Dry(params string[] args)=>Tool(["qemu","smoke","-DryRun",..args]);
        void Reject(params string[] args){Result r=Dry(args);Need(r.Exit!=0&&!r.Timeout&&(r.Output+r.Error).Contains("TEST_FAIL",StringComparison.Ordinal),"profile refusal "+string.Join(' ',args)+" "+r.Output+r.Error);}
        const string flag="-EnableAcpiPhysicalDiscoveryProbe";
        string[] positive=["-BuildEfi",flag,"-MarkerPreset","acpi"];
        foreach(string preset in new[]{"acpi"," ACPI "})
        {
            Result run=Dry("-BuildEfi",flag,"-MarkerPreset",preset);Passed(run);
            string[] Vector(string prefix)=>run.Output.Split('\n').Single(x=>x.StartsWith(prefix,StringComparison.Ordinal))[prefix.Length..].TrimEnd('\r').Split(';');
            string[] ordered=Vector("ORDERED_MARKERS "),unique=Vector("UNIQUE_MARKERS "),required=Vector("REQUIRED_MARKERS ");
            Need(ordered.Length==OS.Tooling.QemuContracts.M03AcpiBootstrapRequiredMarkers.Count&&unique.Length==ordered.Length&&required.Length==ordered.Length,"complete record inventory required");
            Need(ordered.SequenceEqual(unique,StringComparer.Ordinal)&&ordered.ToHashSet(StringComparer.Ordinal).SetEquals(required),"ordered/unique sequence and required set");
            Need(ordered.Distinct(StringComparer.Ordinal).Count()==ordered.Length&&ordered.All(x=>x.Length>0&&x.Trim()==x),"canonical unique markers");
            Need(ordered[13]=="ACPI_RSDP_SNAPSHOT_READY"&&ordered[17]=="ACPI_MCFG_DISCOVERY_READY"&&ordered[18]=="TEST_START m03.002"&&ordered[26]=="TEST_PASS m03.002"&&ordered[^1]=="OS_SMOKE_PASS","old ACPI and complete new sequence");
            Need(run.Output.Split('\n').Count(x=>string.Equals(x.TrimEnd('\r'),"TEST_PASS qemu.runner.dryrun",StringComparison.Ordinal))==1,"exact dryrun terminal");
        }
        Reject(flag,"-MarkerPreset","acpi");Reject("-BuildEfi",flag);Reject("-BuildEfi",flag,"-MarkerPreset","baseline");
        Reject("-BuildEfi",flag,"-MarkerPreset","acpi,baseline");Reject([..positive,"-SkipImageBuild"]);Reject([..positive,"-RequireNativeAppMarkers"]);
        Reject("-RequireMarker","CLROS_M03_002_READY");
        foreach(string option in new[]{"-RequireMarker","-RequireOrderedMarker","-RequireUniqueMarker","-ForbidMarker","-M01RawObservationLog","--http-probe","--extra-arg"})Reject([..positive,option,"extra"]);
        var flags=OS.Tooling.EfiContracts.DiagnosticProbes.ToArray();
        Need(flags.Length==OS.Tooling.EfiContracts.DiagnosticProbes.Count,"actual current requested profile vector");
        foreach(string other in flags.Where(x=>x!="EnableAcpiPhysicalDiscoveryProbe"))Reject([..positive,"-"+other]);
        Result markerOnly=Dry("-BootPolicy","Diagnostic","-MarkerPreset","acpi");Passed(markerOnly);
        Need(!markerOnly.Output.Contains("CLROS_M03_002_",StringComparison.Ordinal),"historical marker-only preset remains separate");
        Need(Hash(script)==before,"unchanged actual smoke source");
    }
    private static void TestTranscriptControls()
    {
        string directory=Scratch("transcript"); string[] lines=[
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
            "ACPI_RSDP_SNAPSHOT_READY",
            "ACPI_RSDP_VALIDATED",
            "ACPI_ROOT_SDT_SNAPSHOT_READY",
            "ACPI_ROOT_SDT_VALIDATED",
            "ACPI_MCFG_DISCOVERY_READY",
            "TEST_START m03.002",
            "CLROS_M03_002_TABLE role=rsdp physical=805306368 owned=1073741840 offset=0 length=36 signature=2329016660419433298 rootIndex=18446744073709551615 compared=36 mismatches=0",
            "CLROS_M03_002_TABLE role=root physical=805310464 owned=1073741876 offset=36 length=60 signature=1413763928 rootIndex=18446744073709551615 compared=60 mismatches=0",
            "CLROS_M03_002_TABLE role=mcfg physical=805314560 owned=1073741936 offset=96 length=60 signature=1195787085 rootIndex=2 compared=60 mismatches=0",
            "CLROS_M03_002_TABLE role=apic physical=805318656 owned=1073741996 offset=156 length=44 signature=1128878145 rootIndex=0 compared=44 mismatches=0",
            "CLROS_M03_002_TABLE role=hpet physical=805322752 owned=1073742040 offset=200 length=56 signature=1413828680 rootIndex=1 compared=56 mismatches=0",
            "CLROS_M03_002_RETAIN total=256 descriptors=5 guards=32 tail=265984 mismatches=0",
            "CLROS_M03_002_READY firmwareAvailable=true exitBootServices=false",
            "TEST_PASS m03.002",
            "TEST_START boot.sequence",
            "BOOT_PHASE PhaseA.Audit START",
            "BOOT_PHASE PhaseA.Audit PASS",
            "BOOT_PHASE PhaseA.Diagnostics START",
            "BOOT_PHASE PhaseA.Diagnostics PASS",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS"
        ];
        var control=new FirmwareTranscriptControls(directory,path=>OS.Tooling.QemuTranscripts.AssertM03AcpiBootstrapTranscript(path));byte[] good=FirmwareTranscriptControls.Bytes(lines);control.Check(good,true);
        string[] rsdt=[..lines];
        rsdt[19]=rsdt[19].Replace("length=36","length=20").Replace("compared=36","compared=20");
        rsdt[20]=rsdt[20].Replace("owned=1073741876 offset=36 length=60 signature=1413763928","owned=1073741860 offset=20 length=48 signature=1413763922").Replace("compared=60","compared=48");
        rsdt[21]=rsdt[21].Replace("owned=1073741936 offset=96","owned=1073741908 offset=68");rsdt[22]=rsdt[22].Replace("owned=1073741996 offset=156","owned=1073741968 offset=128");rsdt[23]=rsdt[23].Replace("owned=1073742040 offset=200","owned=1073742012 offset=172");rsdt[24]=rsdt[24].Replace("total=256","total=228").Replace("tail=265984","tail=266012");control.Check(FirmwareTranscriptControls.Bytes(rsdt),true);
        string[] touch=[..rsdt];touch[20]=touch[20].Replace("physical=805310464","physical=805306388");control.Check(FirmwareTranscriptControls.Bytes(touch),true);
        for(int i=0;i<lines.Length;i++)
        {
            string[] copy=[..lines];copy[i]+=" extra=1";control.Check(FirmwareTranscriptControls.Bytes(copy),false);copy=[..lines];copy[i]="";control.Check(FirmwareTranscriptControls.Bytes(copy),false);
            copy=[..lines];copy[i]=copy[i].ToLowerInvariant();if(copy[i]!=lines[i])control.Check(FirmwareTranscriptControls.Bytes(copy),false);
            control.Check(FirmwareTranscriptControls.Bytes(lines.Take(i+1).Concat(lines.Skip(i))),false);
        }
        for(int i=19;i<=23;i++)
        {
            foreach(string field in new[]{"physical","owned","length"})foreach(string value in new[]{"0","18446744073709551615","18446744073709551616","01","+1","-1"})
            {string[] copy=[..lines];copy[i]=Regex.Replace(copy[i]," "+field+"=[0-9]+"," "+field+"="+value);control.Check(FirmwareTranscriptControls.Bytes(copy),false);}
            foreach(string field in new[]{"offset","signature","compared","mismatches"}){string[] copy=[..lines];copy[i]=Regex.Replace(copy[i]," "+field+"=[0-9]+"," "+field+"=1");control.Check(FirmwareTranscriptControls.Bytes(copy),false);}
            string[] overlap=[..lines];overlap[i]=Regex.Replace(overlap[i]," physical=[0-9]+"," physical=1073741840");control.Check(FirmwareTranscriptControls.Bytes(overlap),false);
            string[] index=[..lines];index[i]=Regex.Replace(index[i]," rootIndex=[0-9]+"," rootIndex="+(i<21?"0":"3"));control.Check(FirmwareTranscriptControls.Bytes(index),false);
        }
        foreach(var pair in new (string Old,string New)[]{("physical=805310464","physical=805306369"), ("rootIndex=2","rootIndex=0"), ("signature=1413763928","signature=1413763927"), ("descriptors=5","descriptors=4"), ("guards=32","guards=31"), ("tail=265984","tail=265983"), ("total=256","total=255"), ("regions=4","regions=3"), ("freeBytes=81920","freeBytes=81921"), ("firmwareAvailable=true","firmwareAvailable=false"), ("exitBootServices=false","exitBootServices=true")})control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace(pair.Old,pair.New)),false);
        string[] swap=[..lines];(swap[21],swap[22])=(swap[22],swap[21]);control.Check(FirmwareTranscriptControls.Bytes(swap),false);
        control.Check(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(good).Replace("\r\n","\n")),false);control.Check(good[..^1],false);control.Check([..good,0],false);control.Check([..good,255],false);control.Check(good,false,[255]);control.Check(good,false,Encoding.ASCII.GetBytes("arbitrary"));control.Check(good,false,post:[..FirmwareTranscriptControls.Suffix,65]);control.Check(good,true);
        string[] ordered=lines.Select(line=>line.StartsWith("UEFI_MEMORY_MAP_DESCRIPTOR_COUNT ",StringComparison.Ordinal)||line.StartsWith("BOOT_MEMORY_REGION_COUNT ",StringComparison.Ordinal)||line.StartsWith("BOOT_PAGE_ALLOCATOR_READY ",StringComparison.Ordinal)||line.StartsWith("CLROS_M03_002_RETAIN ",StringComparison.Ordinal)?line.Split(' ')[0]:line.StartsWith("CLROS_M03_002_TABLE ",StringComparison.Ordinal)?string.Join(' ',line.Split(' ').Take(2)):line).ToArray();
        var runner=new StandardFirmwareRunnerControls(directory,lines,ordered,["OS_SMOKE_FAIL","TEST_FAIL","ACPI_DISCOVERY_FAIL"],"EnableAcpiPhysicalDiscoveryProbe",preset:"acpi");runner.CompleteMatrix(false);
        Console.WriteLine("TRANSCRIPT_CONTROLS m03.acpi cases="+(control.Cases+runner.Cases)+" stderrWhitespaceRejected=true");
    }
    [HostTest("m03.002.efi default and enabled no-stdlib artifacts", HostLane.Native, 3)]
    private static void TestArtifacts()
    {
        string root=Root();
        foreach(bool enabled in new[]{false,true})
        {
            string directory=Scratch(enabled?"enabled":"default"),artifact=Path.Combine(root,"artifacts/efi/issue539-"+Guid.NewGuid().ToString("N")+".EFI");
            Result result=Tool(["efi","build","-BootPolicy","Diagnostic","-EfiOutputPath",artifact,"-LogPath",Path.Combine(directory,"publish.log"),..(enabled?new[]{"-EnableAcpiPhysicalDiscoveryProbe"}:Array.Empty<string>())]);Passed(result);
            Need(result.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal)&&result.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),"actual producer/verifier");
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(artifact+suffix,Path.Combine(directory,"image.EFI"+suffix));
            foreach(var pair in new[]{("src/OS/bin/Release/net10.0/win-x64/OS.dll","OS.dll"),("src/OS/obj/Release/net10.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net10.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(root,pair.Item1),Path.Combine(directory,pair.Item2));
            using var json=JsonDocument.Parse(File.ReadAllText(artifact+".link.json"));var evaluation=json.RootElement.GetProperty("postRestoreEvaluation");
            string[] expectedCompileItems = OS.Tooling.EfiProjectAdmission.ExpectedCompileItems(
                OS.Tooling.EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableAcpiPhysicalDiscoveryProbe"] = enabled }));
            Need(evaluation.GetProperty("compileCount").GetInt32()==expectedCompileItems.Length,"complete evaluated compile inventory");
            Need(evaluation.GetProperty("compileSha256").GetString()==OS.Tooling.EfiEvidence.HashLines(expectedCompileItems),"ordered evaluated compile inventory");
            foreach(var input in json.RootElement.GetProperty("sourceClosureInputs").EnumerateArray())Need(string.Equals(Hash(Path.Combine(root,input.GetProperty("path").GetString()!)),input.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"actual raw source identity");
            foreach(var property in evaluation.GetProperty("properties").EnumerateObject().Where(x=>x.Name.StartsWith("Enable",StringComparison.Ordinal)))
                Need(property.Value.GetString()==(enabled&&property.Name=="EnableAcpiPhysicalDiscoveryProbe"?"true":"false"),"actual profile "+property.Name);
            using var stream=File.OpenRead(Path.Combine(directory,"OS.dll"));using var pe=new PEReader(stream);var metadata=pe.GetMetadataReader();Need(metadata.AssemblyReferences.Count==0,"zero AssemblyRefs");
            var entry=metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(t=>metadata.GetString(t.Name)=="EfiEntry");
            foreach(string name in new[]{"TryCaptureAcpiBootstrap","TryReadAndValidateAcpiRsdp","TryReadAndValidateRootSdt","TryDiscoverSelectedAcpiTables","AcpiFinalAdmission","TryPublishAcpiBootstrap","ReadBackAcpiBootstrap"})
            {
                var methods=entry.GetMethods().Where(h=>metadata.GetString(metadata.GetMethodDefinition(h).Name)==name).ToArray();Need(methods.Length==(enabled?1:0),"conditional numeric method "+name);
                if(enabled)Need(metadata.GetMethodDefinition(methods[0]).RelativeVirtualAddress!=0,"actual method body "+name);
            }
        }
    }
}
#endif
