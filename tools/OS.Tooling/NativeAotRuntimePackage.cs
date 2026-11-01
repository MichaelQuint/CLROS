using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Host-only packaging of the original step1225 runtime smoke and the
/// three step1276 apps. Reads exact evidence and current relevant sources; never
/// compiles, maps or executes an app. Returned bytes remain caller-owned.
/// Missing, stale, linked, oversized or substituted inputs fail before output.</summary>
public static class NativeAotRuntimePackage
{
    private const string Qualification="artifacts/runtime-payload-r02/";
    private const string ProjectHash="D8AFB29D1F4465AD7D3EF3F4A292255DBC5E6975FFD4AD02948E912B1D88564B";
    private static readonly UTF8Encoding Utf8=new(false,true);
    private static readonly (string Path,string Hash)[] SharedSources=[
        ("global.json","BE8663081FD82196AC86E65E9021D58D7C03A13574CE8CD4DC803937FC3BA30E"),
        ("Directory.Build.props","DF8A0D794DB73060124F5134EB930DCDBF6EE526C2410227F0C668AB6D10C5E5"),
        ("Directory.Build.targets","85AFD90F9DD0F550B6A3C6AD71D28BF714A8C930158DB872F8DA5D316889931A"),
        ("tools/Directory.Build.props","44B8268EA6934335DBC79FEF47C1CE1FDEE49393C929C92E322417B14AA321AE"),
        ("tools/OS.Tooling/NativeAotPayload.cs","E3EC6BCE13C70C99F3D7306541D68104876FAF73593325107319719811694D2F"),
        ("tools/OS.Tooling/NativeAotRuntimePayload.cs","4E04A469F05111A9A54180D845601C5C04F29D5ED55624225AADD649A3AA1AC7")];

    internal readonly record struct Payload(string Name,int Bytes,string Hash,string Source,string Guest,
        int DllBytes,string DllHash,int ObjectBytes,string ObjectHash,int StdoutBytes,string StdoutHash,int Traces);
    internal static Payload Specification(string app)=>app switch
    {
        "NativeAotRuntimeSmoke"=>new(app,1511248,"9C72E386641925BB2F7EE04B78FE12F3A76FD1A1BF09D00FEF71FBA34A2B5D03",
            "B448C158C8ED551BD8A89096BF9B2565B1F81FE4372C7063FFC85D797471988C","",
            8704,"BF106F8F109928CD33D7C00AA9F90ED33C12AD7D73D58757C5A932DE8D1813C0",5473003,"716857F68888AE0BDEBF3D8D941B0CDE346773FBAD9492AE9F9993DC3AE5C25C",
            149,"9AEADEC8DAAE9768ADB2953CDBF8B8E95A70520AA3846986F23095B0D584D6F1",9),
        "NativeAotSuspensionStress"=>new(app,1576912,"E794C943782498E50BD5D6C388815573C4F43E67383C7CF8AF63CA6E31D7A8EE",
            "E7878D6036F00EFE3E11318033F17A8862A38330335C1C64CFD569D56DA5CB3D","52ACFBDE5BB104DFF29A8BD0F57F4849B2A1100D5303A44599DC202B117E9402",
            11776,"F2B3BE398D3ED0E6365336BE03A120B24DC763EDEE3E9B6C233899B8F8D82CAD",6036485,"BCD1B694569EF436AEFC57089BDBD117396B4011E168C33776540416C53ACE4F",
            1720,"F07823C6B31AD67409A83C6F0EB65CD022822124E041C5C3DE519A1BCAA2EBD4",18),
        "NativeAotThreadPoolStress"=>new(app,1627408,"201D4A524EF30679ABDA6D42B533E52324663FFBA89215CF3E56B353CF9A6ECD",
            "4C6D6CF9D3EB8388168FD5B9CDD54B9A5DDA8E2A4ABBDCD958AA3F9F57B19838","7B01732B05219BCD18DC6FBD74B70A16A189CD37C7EA80668207F8804013C686",
            25088,"EC15BD2BC989A3F28EFBC86FB8A64E4AE337A6B22110E611AFE2916360F59BF7",6132802,"D1FA83BF074F78146AF42B8C803E63D54B431267FF585FC8DA9BCC60EB09CD6D",
            1601,"7C4739C24BE091121DFC82949D7300F6F4A355B83A1468FB1853AA6EE4A04BD0",8),
        "NativeAotRuntimeMatrix"=>new(app,1577616,"7D33742EDC130FDDBFA96918A5B78C2B0CC1199FA85F11C7ED70BC4F96F64F7E",
            "360673D96E5706DFC15949F597BED32C9C0C7D642F6F20B03BC2EC1A99FC5BE6","15264115F5D1826A172258D8A18A16DB3FDA9300652F85760C444AC9E0D3451E",
            22528,"B28BD2E03CC0E7F91D6026663BDE2D51BB1D46A34184C67CADAF066F29EA1822",5888268,"7A056197424C8F9F06F4A8D060116EA57C9C21540080A56D7E807FCCD9D53042",
            2446,"AE4CF62199312713B6A43A2B6E9BABBCD1B53EF4A1DEBE66346B64B46E16489D",14),
        _=>throw new ArgumentException("runtime_package_app",nameof(app))
    };

    /// <summary>Canonical no-argument/no-runtime-override manifest with explicit
    /// user IRQs and the thread service's existing sixteen-slot limit. The
    /// matrix needs at least nine live threads. The native owner now admits
    /// sixteen slots; actual runtime capacity remains to qualify in guests.
    /// The required endpoint field grants no socket or network service.</summary>
    public static string Manifest(string app)
    {
        var p=Specification(app);
        return "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\napp.path=apps/"+p.Name+
            "\nargs=\ndigest="+p.Hash.ToLowerInvariant()+"\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=16\nlisten=127.0.0.1:8080\nenv.CLROS_USER_IRQS=1\n";
    }
    private static IEnumerable<(string Path,string Hash)> Sources(Payload p)
    {
        bool original=p.Name=="NativeAotRuntimeSmoke";
        return (original?SharedSources[..^1]:SharedSources).Concat([
            ($"apps/{p.Name}/Program.cs",p.Source),($"apps/{p.Name}/{p.Name}.csproj",ProjectHash)])
            .Concat(original?[]:new[]{($"tools/OS.Tooling/{p.Name}Guest.cs",p.Guest)});
    }

    /// <summary>Checks only the selected app and its relevant build/producer/
    /// output-validator sources against original raw identities. Build caches
    /// are excluded; new source/restore overrides reject. Unrelated kernel or
    /// adapter edits are not claimed as whole-repository identity.</summary>
    public static void RequireCurrentSources(string root,string app)
    {
        var p=Specification(app);root=Path.GetFullPath(root);
        foreach(string relative in new[]{"NuGet.Config","nuget.config","apps/Directory.Build.props","apps/Directory.Build.targets"})
            Need(!Path.Exists(Path.Combine(root,relative)),"source_override "+relative);
        string directory=Path.Combine(root,"apps",app);AuthorityFiles.RequirePlainPath(root,directory,"runtime_source_path");
        Stack<string> pending=new();pending.Push(directory);List<string> files=[];int entries=0;
        while(pending.TryPop(out string? next))foreach(string path in Directory.EnumerateFileSystemEntries(next))
        {
            Need(++entries<=128,"source_inventory");
            if(Path.GetFileName(path) is "bin" or "obj" or "artifacts" && Directory.Exists(path))continue;
            FileAttributes attributes=File.GetAttributes(path);Need((attributes&FileAttributes.ReparsePoint)==0,"source_link");
            if((attributes&FileAttributes.Directory)!=0)pending.Push(path);else files.Add(Path.GetRelativePath(directory,path).Replace('\\','/'));
        }
        Need(files.Order(StringComparer.Ordinal).SequenceEqual(new[]{app+".csproj","Program.cs"}.Order(StringComparer.Ordinal)),"source_inventory");
        foreach(var source in Sources(p))Need(Hash(Read(root,source.Path,1024*1024))==source.Hash,"source_identity "+source.Path);
    }

    /// <summary>Creates fresh bytes after the original fixed receipt, selected
    /// ELF/DLL/object/stdout and complete per-thread trace inventory match.
    /// The pinned receipt binds Linux/producer inputs; unavailable Linux files
    /// are not claimed to have been rehashed here. Evidence is read-only and
    /// inputs are borrowed synchronously. RC3 Smoke uses its extracted payload-qualification-r01 original;
    /// the three broader apps use the new runtime-payload-r02 receipt.
    /// Success grants no guest admission.</summary>
    public static unsafe byte[] Create(string root,string evidenceRoot,string app)
    {
        var p=Specification(app);RequireCurrentSources(root,app);
        byte[] elf=app=="NativeAotRuntimeSmoke"?OriginalSmokeEvidence(evidenceRoot,p):BroaderEvidence(evidenceRoot,p);
        _=NativeAotPayload.Inspect(elf);
        fixed(byte* image=elf)Need(LinuxElfLoader.Validate(image,(uint)elf.Length,64*1024*1024,1572864,out _)==LinuxElfLoadStatus.Ready,"loader");
        byte[] manifest=Encoding.ASCII.GetBytes(Manifest(app)),package=new byte[64+manifest.Length+elf.Length];
        Header(manifest.Length,elf.Length).CopyTo(package,0);manifest.CopyTo(package,64);elf.CopyTo(package,64+manifest.Length);
        RequirePackage(app,package);RequireCurrentSources(root,app);return package;
    }

    private static byte[] BroaderEvidence(string evidenceRoot,Payload p)
    {
        string app=p.Name;
        byte[] receipt=Pinned(evidenceRoot,Qualification+"receipt.json",1315324,"BF4AEF43449CB7150E110EEC4E75068D414E77210729AEB63451684637947423");
        using JsonDocument document=JsonDocument.Parse(receipt);var data=document.RootElement;
        Need(data.GetProperty("schema").GetInt32()==1 && data.GetProperty("outcome").GetString()=="passed" &&
            data.GetProperty("sdk").GetString()==NativeAotPayload.SdkVersion && data.GetProperty("runtime").GetString()==NativeAotPayload.RuntimeVersion,"producer_policy");
        foreach(var source in Sources(p))Need(data.GetProperty("sources").GetProperty(source.Path).GetString()==source.Hash,"producer_source");
        var commands=data.GetProperty("commands");
        Need(commands.GetArrayLength()==29 && commands.EnumerateArray().All(c=>c.GetProperty("exitCode").GetInt32()==0),"producer_commands");
        string intermediate=$"apps/{app}/obj/Release/net11.0/linux-musl-x64/";
        _=Pinned(evidenceRoot,intermediate+app+".dll",p.DllBytes,p.DllHash);
        _=Pinned(evidenceRoot,intermediate+"native/"+app+".o",p.ObjectBytes,p.ObjectHash);
        Need(data.GetProperty("nativeInputs").GetProperty("/root/clros-rc3-remaining-r05/"+intermediate+app+".dll").GetString()==p.DllHash &&
            data.GetProperty("nativeInputs").GetProperty("/root/clros-rc3-remaining-r05/"+intermediate+"native/"+app+".o").GetString()==p.ObjectHash,"producer_outputs");
        NativeAotRuntimePayload.ValidateOutput(app,Utf8.GetString(Pinned(evidenceRoot,Qualification+app+"-run.stdout.log",p.StdoutBytes,p.StdoutHash)));
        string traces=Path.Combine(Path.GetFullPath(evidenceRoot),Qualification);AuthorityFiles.RequirePlainPath(Path.GetFullPath(evidenceRoot),traces,"runtime_evidence_path");
        var observed=NativeAotRuntimePayload.ReadTraceInventory(traces,app);
        var recorded=data.GetProperty("applications").EnumerateArray().Single(a=>a.GetProperty("name").GetString()==app);
        Need(recorded.GetProperty("sha256").GetString()==p.Hash && observed.Files.Count==p.Traces &&
            recorded.GetProperty("traceFiles").EnumerateObject().Count()==p.Traces &&
            observed.Files.All(f=>recorded.GetProperty("traceFiles").GetProperty(f.Key).GetString()==f.Value) &&
            observed.Syscalls.SequenceEqual(recorded.GetProperty("observedSyscalls").EnumerateArray().Select(s=>s.GetString()!)),"producer_traces");
        return Pinned(evidenceRoot,Qualification+app+"/"+app,p.Bytes,p.Hash);
    }

    // Fresh RC3 original Smoke keeps its own complete producer inputs and traces.
    private static byte[] OriginalSmokeEvidence(string evidenceRoot,Payload p)
    {
        const string q="artifacts/payload-qualification-r01/";
        const string produced="apps/NativeAotRuntimeSmoke/obj/Release/net11.0/linux-musl-x64/";
        using var sources=JsonDocument.Parse(Pinned(evidenceRoot,q+"inputs.json",119781,"BC146C016C3807026B088BA9EEE1354062DE14A2D15AE484266B5EB02ED613D3"));
        using var native=JsonDocument.Parse(Pinned(evidenceRoot,q+"native-inputs.json",226492,"B18C26D43B18353A6EE7B1E54800D727FE1767F575AC787C0FC7C190EB31237A"));
        _=Pinned(evidenceRoot,q+"inspection.json",2054,"852AC089419F443BA5D0EA7283EE0C1614E5ED603E1FB06DCAA276401DE6336C");
        _=Pinned(evidenceRoot,q+"commands.json",7696,"84103D126C99FAF612CDD85A2C71E684F589914766EF2AE44E4FC2B19A4A431C");
        _=Pinned(evidenceRoot,q+"sdk-version.stdout.log",24,"8F311FDA85131EB472C3F117C2C4E441C86AAEB72740D76F44D89FF1E75AE7AE");
        _=Pinned(evidenceRoot,q+"NativeAotRuntimeSmoke-publish.stdout.log",270,"FA9A4F0DC90618CD989DF6D3466BA1994F146AC73D258043A467A9A6D5C47FA2");
        Need(sources.RootElement.EnumerateObject().Count()==998 && native.RootElement.EnumerateObject().Count()==1042,"original_inventory");
        foreach(var source in Sources(p))Need(sources.RootElement.GetProperty(source.Path).GetString()==source.Hash,"producer_source");
        const string intermediate="/root/clros-rc3-r01/apps/NativeAotRuntimeSmoke/obj/Release/net11.0/linux-musl-x64/";
        Need(native.RootElement.GetProperty(intermediate+p.Name+".dll").GetString()==p.DllHash &&
            native.RootElement.GetProperty(intermediate+"native/"+p.Name+".o").GetString()==p.ObjectHash,"producer_outputs");
        _=Pinned(evidenceRoot,produced+p.Name+".dll",p.DllBytes,p.DllHash);
        _=Pinned(evidenceRoot,produced+"native/"+p.Name+".o",p.ObjectBytes,p.ObjectHash);
        NativeAotThreadingOutput.ValidateRuntimeSmokeOutput(Utf8.GetString(Pinned(evidenceRoot,q+p.Name+"-run.stdout.log",p.StdoutBytes,p.StdoutHash)));
        (string Name,int Bytes,string Hash)[] traces=[
            ("2621",43047,"3EA3CB9C5B2DF2D239C4D5E66B605AFC291E1856F5DC63BA85683BADEF215B9E"),
            ("2622",1679,"E7AC56A7C2C220B2A07299EBD21E2B95A2039CCA23744BDB726444B8E6F242BB"),
            ("2623",156,"35ECF941323E9713821AAAB6DEA965F69DB7D11FAC186FC2682A06C4AB96861F"),
            ("2624",1497,"234F2D5DEDB94FD2142655D7EA3A9F70588EB998A068B304CB4ED3B3AD0CD2E5"),
            ("2625",2367,"9741D46B5E708195CE0AD78CC08762335A98C43CDDCC5B382018FBFE83FA4B3A"),
            ("2626",1885,"3B3D6C4E9C472DC195C5A7E87561F5B1718DEA455CCC0C04B0032DACE1163B5D"),
            ("2627",442,"388151D20E70FADE5733EBC1F64214174F60AE38D2BFDE5FF966E7E5DE358284"),
            ("2628",831,"4810E3B5AFCDFABF0675DEC1F29B38B30133E7663421CF24DD6E15BE4EC9DF3D"),
            ("2629",438,"84BC2E33F392B48100139458EC4B48F3C91CCF9A2745DB0190DD68BC14482EEC")];
        string directory=Path.Combine(Path.GetFullPath(evidenceRoot),q);
        Need(Directory.EnumerateFileSystemEntries(directory,p.Name+"-trace.*").Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .SequenceEqual(traces.Select(t=>p.Name+"-trace."+t.Name).Order(StringComparer.Ordinal)),"producer_traces");
        foreach(var trace in traces)_=Pinned(evidenceRoot,q+p.Name+"-trace."+trace.Name,trace.Bytes,trace.Hash);
        return Pinned(evidenceRoot,q+p.Name+"/"+p.Name,p.Bytes,p.Hash);
    }

    /// <summary>Requires canonical header/policy and exact qualified ELF bytes.
    /// Borrowed bytes grant no instruction lookup or execution authority.
    /// Unknown names, malformed or substituted packages throw.</summary>
    public static void RequirePackage(string app,ReadOnlySpan<byte> package)
    {
        var p=Specification(app);byte[] manifest=Encoding.ASCII.GetBytes(Manifest(app));
        Need(package.Length==64+manifest.Length+p.Bytes && package[..64].SequenceEqual(Header(manifest.Length,p.Bytes)),"identity");
        Need(LinuxElfPackage.Validate(package).Succeeded && package.Slice(64,manifest.Length).SequenceEqual(manifest) &&
            Hash(package[(64+manifest.Length)..])==p.Hash,"identity");
    }

    /// <summary>Validates input before flushing a fresh owned sibling temporary
    /// and publishing by no-overwrite rename beneath this repo's artifacts.
    /// Linked parents, existing output and escaping paths reject; only the owned
    /// temporary is removed on failure. Returns a digest, never execution proof.</summary>
    public static string Write(string root,string evidenceRoot,string app,string destination)
    {
        _=Specification(app);root=Path.GetFullPath(root);destination=Path.GetFullPath(destination);string artifacts=Path.Combine(root,"artifacts");
        void Output()
        {
            Need(destination.StartsWith(artifacts+Path.DirectorySeparatorChar,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal),"output_path");
            AuthorityFiles.RequirePlainPath(root,Path.GetDirectoryName(destination)!,"runtime_output_path");
        }
        Output();Need(!Path.Exists(destination),"output_exists");byte[] bytes=Create(root,evidenceRoot,app);
        string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";bool owns=false;
        try
        {
            Output();using(FileStream file=new(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){owns=true;file.Write(bytes);file.Flush(flushToDisk:true);}
            RequireCurrentSources(root,app);Output();File.Move(temporary,destination,overwrite:false);
        }
        finally{if(owns)File.Delete(temporary);}
        return Hash(bytes);
    }
    private static byte[] Header(int manifest,int elf)
    {
        byte[] header=new byte[64];BinaryPrimitives.WriteUInt64LittleEndian(header,LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8),1);BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12),1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16),64);BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20),(uint)manifest);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(24),(ulong)elf);return header;
    }
    private static byte[] Pinned(string root,string path,int bytes,string hash)
    {byte[] value=Read(root,path,bytes);Need(value.Length==bytes && Hash(value)==hash,"evidence_identity "+path);return value;}
    private static byte[] Read(string root,string relative,int maximum)
    {
        root=Path.GetFullPath(root);string path=Path.Combine(root,relative);AuthorityFiles.RequirePlainPath(root,path,"runtime_evidence_path");
        using FileStream file=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);Need(file.Length is >0 && file.Length<=maximum,"input_size "+relative);
        byte[] value=new byte[(int)file.Length];file.ReadExactly(value);Need(file.ReadByte()==-1,"input_changed");return value;
    }
    private static string Hash(ReadOnlySpan<byte> value)=>Convert.ToHexString(SHA256.HashData(value));
    private static void Need(bool condition,string reason){if(!condition)throw new InvalidDataException("runtime_package_"+reason);}
}
