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
    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("conditional source and unchanged profile",TestSource);
#if !ACPI_GRAMMAR_HOST
        Run("actual five-copy EFI methods and independent host parity",TestCapture);
#endif
        Run("literal transcript and complete finite fields",TestLiterals);
#if !ACPI_GRAMMAR_HOST
        Run("default and enabled no-stdlib artifacts",TestArtifacts);
#endif
    }
    private static void Run(string name,Action action)
    {Console.WriteLine("TEST_START m03.002.efi "+name);try{action();Console.WriteLine("TEST_PASS m03.002.efi "+name);}catch(Exception e){Console.WriteLine("TEST_FAIL m03.002.efi "+name+" "+e);throw;}}
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
    private static void Passed(Result result)=>Need(result.Exit==0&&!result.Timeout&&result.Error.Length==0,result.Output+result.Error);
    private static void TestSource()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));
        Need(!project.Descendants("EnableAcpiPhysicalDiscoveryProbe").Any(),"existing property unset by default");
        var symbol=project.Descendants("DefineConstants").Single(x=>x.Value.Contains("ACPI_PHYSICAL_DISCOVERY_PROBE",StringComparison.Ordinal));
        Need(string.Equals((string?)symbol.Parent?.Attribute("Condition"),"'$(EnableAcpiPhysicalDiscoveryProbe)' == 'true'",StringComparison.Ordinal),"existing symbol requires explicit true");
        Need(project.Descendants("DefineConstants").Count(x=>x.Value.Contains("ACPI_PHYSICAL_DISCOVERY_PROBE",StringComparison.Ordinal))==1,"one existing symbol");
        Need(Hash(Path.Combine(root,"src/OS.Kernel/Boot/RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap.cs"))=="2AFB2B08C511AC8A25CE3F8818E7730961AB3F53C032CA6B846E0BB634624AE8","frozen host oracle");
        string source=File.ReadAllText(Path.Combine(root,"src/OS/Boot/EfiEntry.cs"));
        string core=source[source.IndexOf("    private static bool WriteAcpiPhysicalDiscoveryProbe(",StringComparison.Ordinal)..source.IndexOf("    private static bool TryParseMcfgSegments(",StringComparison.Ordinal)];
        foreach(string forbidden in new[]{"new byte[","stackalloc","throw ","AllocatePool(","AllocatePages(","ExitBootServices(","ReadOnlySpan", "Console."})Need(!core.Contains(forbidden,StringComparison.Ordinal),"finite EFI core "+forbidden);
        Need(core.IndexOf("if (!AcpiFinalAdmission(",StringComparison.Ordinal)<core.IndexOf("owned[i] =",StringComparison.Ordinal),"all admission before destination writes");
        TestScriptProfile();
    }
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
        string root=Root(),directory=Scratch("script-profile"),script=Path.Combine(root,"scripts/run-qemu-smoke.ps1");
        string before=Hash(script);int ordinal=0;
        Need(File.ReadAllText(script).Replace("\r\n","\n",StringComparison.Ordinal).Contains("        if ($EnableAcpiPhysicalDiscoveryProbe) {\n            $buildArguments += \"-EnableAcpiPhysicalDiscoveryProbe\"\n        }",StringComparison.Ordinal),"conditional build forwarding source");
        Result Dry(params string[] args)=>Process(Path.Combine(directory,(ordinal++).ToString("D3")),"powershell.exe",
            ["-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-DryRun",..args]);
        void Reject(params string[] args){Result r=Dry(args);Need(r.Exit!=0&&!r.Timeout&&r.Output.Contains("TEST_FAIL",StringComparison.Ordinal),"profile refusal "+string.Join(' ',args));}
        const string flag="-EnableAcpiPhysicalDiscoveryProbe";
        string[] positive=["-BuildEfi",flag,"-MarkerPreset","acpi"];
        foreach(string preset in new[]{"acpi"," ACPI "})
        {
            Result run=Dry("-BuildEfi",flag,"-MarkerPreset",preset);Passed(run);
            string[] Vector(string prefix)=>run.Output.Split('\n').Single(x=>x.StartsWith(prefix,StringComparison.Ordinal))[prefix.Length..].TrimEnd('\r').Split(';');
            string[] ordered=Vector("ORDERED_MARKERS "),unique=Vector("UNIQUE_MARKERS "),required=Vector("REQUIRED_MARKERS ");
            Need(ordered.Length==35&&unique.Length==35&&required.Length==35,"all35 records required");
            Need(ordered.SequenceEqual(unique,StringComparer.Ordinal)&&ordered.ToHashSet(StringComparer.Ordinal).SetEquals(required),"ordered/unique sequence and required set");
            Need(ordered.Distinct(StringComparer.Ordinal).Count()==35&&ordered.All(x=>x.Length>0&&x.Trim()==x),"canonical unique markers");
            Need(ordered[13]=="ACPI_RSDP_SNAPSHOT_READY"&&ordered[17]=="ACPI_MCFG_DISCOVERY_READY"&&ordered[18]=="TEST_START m03.002"&&ordered[26]=="TEST_PASS m03.002"&&ordered[^1]=="OS_SMOKE_PASS","old ACPI and complete new sequence");
            Need(run.Output.Split('\n').Count(x=>string.Equals(x.TrimEnd('\r'),"TEST_PASS qemu.runner.dryrun",StringComparison.Ordinal))==1,"exact dryrun terminal");
        }
        Reject(flag,"-MarkerPreset","acpi");Reject("-BuildEfi",flag);Reject("-BuildEfi",flag,"-MarkerPreset","baseline");
        Reject("-BuildEfi",flag,"-MarkerPreset","acpi,baseline");Reject([..positive,"-SkipImageBuild"]);Reject([..positive,"-RequireNativeAppMarkers"]);
        Reject("-RequireMarker","CLROS_M03_002_READY");
        foreach(string option in new[]{"-RequireMarker","-RequireOrderedMarker","-RequireUniqueMarker","-ForbidMarker","-M01RawObservationLog","--http-probe","--extra-arg"})Reject([..positive,option,"extra"]);
        var flags=Regex.Matches(File.ReadAllText(script).Split("$ErrorActionPreference",StringSplitOptions.None)[0],@"\[switch\]\s+\$(Enable\w+)").Select(m=>m.Groups[1].Value).ToArray();
        Need(flags.Length==52,"actual current requested profile vector");
        foreach(string other in flags.Where(x=>x!="EnableAcpiPhysicalDiscoveryProbe"))Reject([..positive,"-"+other]);
        Result markerOnly=Dry("-BootPolicy","Diagnostic","-MarkerPreset","acpi");Passed(markerOnly);
        Need(!markerOnly.Output.Contains("CLROS_M03_002_",StringComparison.Ordinal),"historical marker-only preset remains separate");
        Need(Hash(script)==before,"unchanged actual smoke source");
    }
    private static void TestTranscriptControls()
    {
        string directory=Scratch("transcript"),script=Path.Combine(Root(),"scripts/run-qemu-smoke.ps1"),copy=Path.Combine(directory,"run-qemu-smoke.ps1"),control=Path.Combine(directory,"controls.ps1");
        File.Copy(script,copy);File.WriteAllText(control,TranscriptScript,new UTF8Encoding(false));
        var inputs=new[]{copy,control}.Select(path=>new{Path=path,Bytes=new FileInfo(path).Length,Sha256=Hash(path)}).ToArray();
        File.WriteAllText(Path.Combine(directory,"inputs.json"),JsonSerializer.Serialize(inputs));
        Result run=Process(Path.Combine(directory,"run"),"powershell.exe","-NoProfile","-ExecutionPolicy","Bypass","-File",control,"-Source",copy,"-Directory",directory);Passed(run);
        Need(run.Output.Contains("TEST_PASS m03.acpi.transcript controls=",StringComparison.Ordinal)&&run.Output.Contains("stderrWhitespaceRejected=true",StringComparison.Ordinal),"complete strict raw/lifecycle controls");
        foreach(var input in inputs)Need(Hash(input.Path)==input.Sha256,"unchanged transcript input "+input.Path);
        Need(Hash(script)==Hash(copy),"original production parser remained unchanged");
    }

    private static readonly string TranscriptScript = """
        param([string]$Source,[string]$Directory)
        $ErrorActionPreference='Stop'
        $tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'script syntax'}
        foreach($name in @('Read-M03MemoryMapCaptureRecord','Assert-M03AcpiBootstrapTranscript','Assert-M03AcpiBootstrapRunnerResult','Invoke-NativeCommand')){
            $function=$ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true)
            if($function.Count -ne 1){throw ('function inventory '+$name)}
            Invoke-Expression $function[0].Extent.Text
        }
        $root=$Directory
        $lines=@(
        'TEST_START efi.main','EFI_CONSOLE_READY','UEFI_MEMORY_MAP_READY','UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4','UEFI_MEMORY_MAP_OWNED',
        'BOOT_HANDOFF_READY','UEFI_MEMORY_MAP_NORMALIZED','BOOT_MEMORY_MAP_ACCEPTED','BOOT_MEMORY_REGIONS_READY','BOOT_MEMORY_REGION_COUNT 4',
        'BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0',
        'BOOT_ACPI_RSDP_HANDOFF_READY','UEFI_ACPI_RSDP_READY','ACPI_RSDP_SNAPSHOT_READY','ACPI_RSDP_VALIDATED','ACPI_ROOT_SDT_SNAPSHOT_READY','ACPI_ROOT_SDT_VALIDATED','ACPI_MCFG_DISCOVERY_READY',
        'TEST_START m03.002',
        'CLROS_M03_002_TABLE role=rsdp physical=805306368 owned=1073741840 offset=0 length=36 signature=2329016660419433298 rootIndex=18446744073709551615 compared=36 mismatches=0',
        'CLROS_M03_002_TABLE role=root physical=805310464 owned=1073741876 offset=36 length=60 signature=1413763928 rootIndex=18446744073709551615 compared=60 mismatches=0',
        'CLROS_M03_002_TABLE role=mcfg physical=805314560 owned=1073741936 offset=96 length=60 signature=1195787085 rootIndex=2 compared=60 mismatches=0',
        'CLROS_M03_002_TABLE role=apic physical=805318656 owned=1073741996 offset=156 length=44 signature=1128878145 rootIndex=0 compared=44 mismatches=0',
        'CLROS_M03_002_TABLE role=hpet physical=805322752 owned=1073742040 offset=200 length=56 signature=1413828680 rootIndex=1 compared=56 mismatches=0',
        'CLROS_M03_002_RETAIN total=256 descriptors=5 guards=32 tail=265984 mismatches=0',
        'CLROS_M03_002_READY firmwareAvailable=true exitBootServices=false','TEST_PASS m03.002',
        'TEST_START boot.sequence','BOOT_PHASE PhaseA.Audit START','BOOT_PHASE PhaseA.Audit PASS','BOOT_PHASE PhaseA.Diagnostics START','BOOT_PHASE PhaseA.Diagnostics PASS','OS_BOOT_OK','TEST_PASS boot.sequence','OS_SMOKE_PASS')
        $preamble=[Convert]::FromBase64String('G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg==')
        $suffix=[Convert]::FromBase64String('QmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAwICJVaUFwcCIgZnJvbSBGdig3Q0I4QkRDOS1GOEVCLTRGMzQtQUFFQS0zRUU0QUY2NTE2QTEpL0Z2RmlsZSg0NjJDQUEyMS03NjE0LTQ1MDMtODM2RS04QUI2RjQ2NjIzMzEpDQpCZHNEeGU6IHN0YXJ0aW5nIEJvb3QwMDAwICJVaUFwcCIgZnJvbSBGdig3Q0I4QkRDOS1GOEVCLTRGMzQtQUFFQS0zRUU0QUY2NTE2QTEpL0Z2RmlsZSg0NjJDQUEyMS03NjE0LTQ1MDMtODM2RS04QUI2RjQ2NjIzMzEpDQobWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMG0bWzMwbRtbNDBtG1swMTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDI7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzAzOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDU7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA2OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDg7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA5OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTE7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzEyOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTQ7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTc7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE4OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjA7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIxOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjM7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzI0OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syNTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swMTswMUgbWzBtG1szNG0bWzQ3bRtbMDE7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzAyOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swMzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDQ7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA1OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDE7MDFIG1swMjswMkhTdGFuZGFyZCBQQyAoUTM1ICsgSUNIOSwgMjAwOSkbWzAzOzAySHBjLXEzNS0xMS4xG1swMzs1NEgyLjAwIEdIehtbMDQ7MDJIZWRrMi1zdGFibGUyMDI0MDgtcHJlYnVpbHQucWVtdS5vcmcbWzA0OzU0SDI1NiBNQiBSQU0bWzA1OzAySBtbMDU7NTRIG1swNjswMkgbWzA2OzU0SBtbMG0bWzM3bRtbNDBtG1syMTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjI7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIzOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syNDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjE7MDFIG1swbRtbMzBtG1s0MG0bWzI1OzUzSCAgG1syNTs1NEggIBtbMjU7NTVIICAbWzI1OzU2SCAgG1syNTs1N0ggIBtbMjU7NThIICAbWzI1OzU5SCAgG1syNTs2MEggIBtbMjU7NjFIICAbWzI1OzYySCAgG1syNTs2M0ggIBtbMjU7NjRIICAbWzI1OzY1SCAgG1syNTs2NkggIBtbMjU7NjdIICAbWzI1OzY4SCAgG1syNTs2OUggIBtbMjU7NzBIICAbWzI1OzcxSCAgG1syNTs3MkggIBtbMjU7NzNIICAbWzBtG1szMG0bWzQ3bRtbMDc7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA4OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTA7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzExOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTM7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE0OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTY7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxODswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTk7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIwOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNzswMUgbWzA3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzdtG1s0MG0bWzA4OzMxSDxTdGFuZGFyZCBFbmdsaXNoPhtbMG0bWzMwbRtbNDdtG1swODs0OUggICAgICAgICAbWzA4OzAxSCAgIBtbMDg7MDRIU2VsZWN0IExhbmd1YWdlICAgICAgICAgICAgG1swbRtbMzRtG1s0N20bWzA5OzAxSCAgIBtbMDk7MDRIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzBtG1s0N20bWzEwOzAxSCAgIBtbMTA7MDJI4pa6G1sxMDswNEhEZXZpY2UgTWFuYWdlciAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzExOzAxSCAgIBtbMTE7MDJI4pa6G1sxMTswNEhCb290IE1hbmFnZXIgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzEyOzAxSCAgIBtbMTI7MDJI4pa6G1sxMjswNEhCb290IE1haW50ZW5hbmNlIE1hbmFnZXIgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzBtG1szNG0bWzQ3bRtbMTM7MDFIICAgG1sxMzswNEggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzBtG1szMG0bWzQ3bRtbMTQ7MDFIICAgG1sxNDswNEhDb250aW51ZSAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzAxSCAgIBtbMTU7MDRIUmVzZXQgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNjswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTg7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIwOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzdtG1s0MG0bWzIzOzAySCAbWzIyOzAySCAbWzIyOzUzSCAgICAgICAgICAgICAgICAgICAgICAgICAbWzIyOzI3SCAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMzs1M0ggICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIzOzAzSOKGkeKGkz1Nb3ZlIEhpZ2hsaWdodCAgICAgICAbWzIyOzAzSCAgICAgICAgICAgICAgICAgICAgICAgIBtbMjM7MjdIPEVudGVyPj1TZWxlY3QgRW50cnkgICAgICAbWzBtG1szNG0bWzQ3bRtbMDg7NThIVGhpcyBpcyB0aGUgb3B0aW9uICAgICAbWzA5OzU4SG9uZSBhZGp1c3RzIHRvIGNoYW5nZSAgG1sxMDs1OEh0aGUgbGFuZ3VhZ2UgZm9yIHRoZSAgIBtbMTE7NThIY3VycmVudCBzeXN0ZW0gICAgICAgICAbWzEyOzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxMzs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTQ7NThIICAgICAgICAgICAgICAgICAgICAgICAbWzE1OzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxNjs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTc7NThIICAgICAgICAgICAgICAgICAgICAgICAbWzE4OzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxOTs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTk7ODBI')
        $serialPath=Join-Path $Directory 'serial.log';$summaryPath=Join-Path $Directory 'summary.log';$cases=0
        function Bytes([string[]]$value){return [Text.Encoding]::ASCII.GetBytes(($value -join "`r`n")+"`r`n")}
        $good=Bytes $lines
        function Check([byte[]]$protocol,[bool]$expected,[byte[]]$pre=$preamble,[byte[]]$post=$suffix){
            [IO.File]::WriteAllBytes($serialPath,[byte[]]($pre+$protocol+$post));$accepted=$true
            try{Assert-M03AcpiBootstrapTranscript $serialPath}catch{$accepted=$false}
            if($accepted -ne $expected){throw ('transcript case '+$script:cases+' expected='+$expected)};$script:cases++
        }
        Check $good $true
        ; # A coherent ACPI1/RSDT fixture changes lengths, offsets and counts together.
        $rsdt=@($lines)
        $rsdt[19]=$rsdt[19].Replace('length=36','length=20').Replace('compared=36','compared=20')
        $rsdt[20]=$rsdt[20].Replace('owned=1073741876 offset=36 length=60 signature=1413763928','owned=1073741860 offset=20 length=48 signature=1413763922').Replace('compared=60','compared=48')
        $rsdt[21]=$rsdt[21].Replace('owned=1073741936 offset=96','owned=1073741908 offset=68')
        $rsdt[22]=$rsdt[22].Replace('owned=1073741996 offset=156','owned=1073741968 offset=128')
        $rsdt[23]=$rsdt[23].Replace('owned=1073742040 offset=200','owned=1073742012 offset=172')
        $rsdt[24]=$rsdt[24].Replace('total=256','total=228').Replace('tail=265984','tail=266012');Check (Bytes $rsdt) $true
        $touch=@($rsdt);$touch[20]=$touch[20].Replace('physical=805310464','physical=805306388');Check (Bytes $touch) $true
        for($i=0;$i -lt 35;$i++){
            $copy=@($lines);$copy[$i]+=' extra=1';Check (Bytes $copy) $false
            $copy=@($lines);$copy[$i]='';Check (Bytes $copy) $false
            $copy=@($lines);$copy[$i]=$copy[$i].ToLowerInvariant();if($copy[$i] -cne $lines[$i]){Check (Bytes $copy) $false}
            $copy=@($lines[0..$i]) + @($lines[$i..34]);Check (Bytes $copy) $false
        }
        for($i=19;$i -le 23;$i++){
            foreach($field in @('physical','owned','length')){
                foreach($value in @('0','18446744073709551615','18446744073709551616','01','+1','-1')){
                    $copy=@($lines);$copy[$i]=[regex]::Replace($copy[$i],(' '+$field+'=[0-9]+'),(' '+$field+'='+$value));Check (Bytes $copy) $false
                }
            }
            foreach($field in @('offset','signature','compared','mismatches')){
                $copy=@($lines);$copy[$i]=[regex]::Replace($copy[$i],(' '+$field+'=[0-9]+'),(' '+$field+'=1'));Check (Bytes $copy) $false
            }
            $copy=@($lines);$copy[$i]=[regex]::Replace($copy[$i],' physical=[0-9]+',' physical=1073741840');Check (Bytes $copy) $false
            $copy=@($lines);$copy[$i]=[regex]::Replace($copy[$i],' rootIndex=[0-9]+',(' rootIndex='+$(if($i -lt 21){'0'}else{'3'})));Check (Bytes $copy) $false
        }
        foreach($pair in @(@('physical=805310464','physical=805306369'),@('rootIndex=2','rootIndex=0'),@('signature=1413763928','signature=1413763927'),@('descriptors=5','descriptors=4'),@('guards=32','guards=31'),@('tail=265984','tail=265983'),@('total=256','total=255'),@('regions=4','regions=3'),@('freeBytes=81920','freeBytes=81921'),@('firmwareAvailable=true','firmwareAvailable=false'),@('exitBootServices=false','exitBootServices=true'))){
            Check ([Text.Encoding]::ASCII.GetBytes([Text.Encoding]::ASCII.GetString($good).Replace($pair[0],$pair[1]))) $false
        }
        $copy=@($lines);$copy[21]=$lines[22];$copy[22]=$lines[21];Check (Bytes $copy) $false
        Check ([Text.Encoding]::ASCII.GetBytes([Text.Encoding]::ASCII.GetString($good).Replace("`r`n","`n"))) $false
        Check ([byte[]]$good[0..($good.Length-2)]) $false
        Check ([byte[]]($good+0)) $false
        Check ([byte[]]($good+0xFF)) $false
        Check $good $false ([byte[]]@(0xFF))
        Check $good $false ([Text.Encoding]::ASCII.GetBytes('arbitrary'))
        Check $good $false $preamble ([byte[]]($suffix+65))
        Check $good $true
        ; # Expected runner data is built from the independent literal transcript above.
        $ordered=@($lines | ForEach-Object {
            if($_.StartsWith('UEFI_MEMORY_MAP_DESCRIPTOR_COUNT ')){ 'UEFI_MEMORY_MAP_DESCRIPTOR_COUNT' }
            elseif($_.StartsWith('BOOT_MEMORY_REGION_COUNT ')){ 'BOOT_MEMORY_REGION_COUNT' }
            elseif($_.StartsWith('BOOT_PAGE_ALLOCATOR_READY ')){ 'BOOT_PAGE_ALLOCATOR_READY' }
            elseif($_.StartsWith('CLROS_M03_002_TABLE ')){ $_.Split(' ')[0..1] -join ' ' }
            elseif($_.StartsWith('CLROS_M03_002_RETAIN ')){ 'CLROS_M03_002_RETAIN' }
            else {$_}
        })
        $required=New-Object 'System.Collections.Generic.List[string]'
        foreach($value in @('OS_BOOT_OK','TEST_PASS boot.sequence','OS_SMOKE_PASS','UEFI_MEMORY_MAP_OWNED','BOOT_HANDOFF_READY','UEFI_MEMORY_MAP_NORMALIZED','BOOT_MEMORY_MAP_ACCEPTED','BOOT_MEMORY_REGIONS_READY','BOOT_MEMORY_REGION_COUNT','BOOT_PAGE_ALLOCATOR_READY','BOOT_ACPI_RSDP_HANDOFF_READY','UEFI_ACPI_RSDP_READY')+$ordered){if(-not $required.Contains($value)){$required.Add($value)}}
        $forbidden=@('OS_SMOKE_FAIL','TEST_FAIL','ACPI_DISCOVERY_FAIL')
        $runner=@('QEMU_DISCOVERY result=qemu.exe checked=qemu.exe','OVMF_DISCOVERY result=ovmf.fd checked=ovmf.fd','TEST_START qemu.smoke','IMAGE_ARTIFACT image.raw',('SERIAL_LOG '+$serialPath),'OVMF_CODE ovmf.fd','WORKLOAD_PROFILE SingleMinimalApi','MARKER_PRESETS acpi',('REQUIRED_MARKERS '+[string]::Join(';',$required)),('ORDERED_MARKERS '+[string]::Join(';',$ordered)),('UNIQUE_MARKERS '+[string]::Join(';',$ordered)),('FORBIDDEN_MARKERS '+[string]::Join(';',$forbidden)),('SERIAL_MARKER_STREAM status=Passed terminal=Success bytes='+($preamble.Length+$good.Length+$suffix.Length)+' lines=40 invalidBytes=207 invalidLines=2 orderedSeen=35 outOfOrder=0 uniqueSeen=35 duplicateUnique=0'),'SERIAL_MARKER_DRAIN milliseconds=750','QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1',('SUMMARY_LOG '+$summaryPath),'TEST_PASS qemu.smoke')
        $summary=@('QEMU_SMOKE_STATUS pass')+@($runner[3],$runner[4],$runner[6],$runner[7],$runner[8],$runner[9],$runner[10],$runner[11])+@($lines|ForEach-Object{'OBSERVED_MARKER '+$_})+@('APP_EXIT_RESULT none','APP_EXIT_MALFORMED none','MISSING_MARKERS none','OUT_OF_ORDER_MARKERS none','DUPLICATE_UNIQUE_MARKERS none','FORBIDDEN_MARKER_HIT none','FAIL_REASON none','SERIAL_TAIL_LIMIT lines=8 chars=240')
        $serial=[Text.Encoding]::UTF8.GetString([byte[]]($preamble+$good+$suffix))
        foreach($line in @($serial.Split([string[]]@("`r`n"),[StringSplitOptions]::None)|Where-Object {$_.Trim().Length -gt 0}|Select-Object -Last 8)){$value=$line.Trim();if($value.Length -gt 240){$value=$value.Substring(0,237)+'...'};$summary+='SERIAL_TAIL_LINE '+$value}
        $summaryText=($summary -join "`r`n")+"`r`n";[IO.File]::WriteAllText($summaryPath,$summaryText,[Text.Encoding]::UTF8)
        function Runner([string[]]$value,[bool]$expected){
            $accepted=$true;try{Assert-M03AcpiBootstrapRunnerResult -Lines $value -SummaryLogPath $summaryPath -SerialLogPath $serialPath -RequiredMarkers $required.ToArray() -OrderedMarkers $ordered -UniqueMarkers $ordered -ForbiddenMarkers $forbidden -ImagePath 'image.raw' -Profile 'SingleMinimalApi'}catch{$accepted=$false}
            if($accepted -ne $expected){throw ('runner case '+$script:cases+' expected='+$expected)};$script:cases++
        }
        Runner $runner $true
        foreach($exit in @('0','-2147483648','2147483647')){$copy=@($runner);$copy[14]='QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exit;Runner $copy $true}
        foreach($exit in @('none','-0','01','+1','2147483648','-2147483649')){$copy=@($runner);$copy[14]='QEMU_PROCESS_RESULT disposition=runner-terminated exitCode='+$exit;Runner $copy $false}
        for($i=0;$i -lt $runner.Count;$i++){$copy=@($runner);$copy[$i]+="`0";Runner $copy $false}
        foreach($pair in @(@(12,'invalidBytes=207','invalidBytes=208'),@(12,'lines=40','lines=39'),@(12,'orderedSeen=35','orderedSeen=34'),@(12,'status=Passed','status=Timeout'),@(13,'milliseconds=750','milliseconds=749'),@(13,'milliseconds','Milliseconds'),@(14,'runner-terminated','exited'),@(14,'runner-terminated','cleanup-failed'))){$copy=@($runner);$copy[$pair[0]]=$copy[$pair[0]].Replace($pair[1],$pair[2]);Runner $copy $false}
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
        Write-Output ('TEST_PASS m03.acpi.transcript controls='+$cases+' stderrEmpty=true stderrWhitespaceRejected=true')
        """;
    private static void TestArtifacts()
    {
        string root=Root();var project=XDocument.Load(Path.Combine(root,"src/OS/OS.csproj"));int compile=project.Descendants("Compile").Count();
        foreach(bool enabled in new[]{false,true})
        {
            string directory=Scratch(enabled?"enabled":"default"),artifact=Path.Combine(root,"artifacts/efi/issue539-"+Guid.NewGuid().ToString("N")+".EFI");
            Result result=Process(directory,"powershell.exe",["-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"scripts/build-efi-shell.ps1"),"-BootPolicy","Diagnostic","-EfiOutputPath",artifact,"-LogPath",Path.Combine(directory,"publish.log"),..(enabled?new[]{"-EnableAcpiPhysicalDiscoveryProbe"}:Array.Empty<string>())]);Passed(result);
            Need(result.Output.Contains("TEST_PASS efi.shell.publish",StringComparison.Ordinal)&&result.Output.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),"actual producer/verifier");
            foreach(string suffix in new[]{"",".link.json",".link.map",".link.rsp",".serializer.obj"})File.Copy(artifact+suffix,Path.Combine(directory,"image.EFI"+suffix));
            foreach(var pair in new[]{("src/OS/bin/Release/net10.0/win-x64/OS.dll","OS.dll"),("src/OS/obj/Release/net10.0/win-x64/native/OS.obj","OS.obj"),("src/OS/obj/Release/net10.0/win-x64/native/OS.map.xml","OS.map.xml")})File.Copy(Path.Combine(root,pair.Item1),Path.Combine(directory,pair.Item2));
            using var json=JsonDocument.Parse(File.ReadAllText(artifact+".link.json"));var evaluation=json.RootElement.GetProperty("postRestoreEvaluation");
            Need(evaluation.GetProperty("compileCount").GetInt32()==compile,"actual evaluated Compile membership");
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
