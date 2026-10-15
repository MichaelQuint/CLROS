using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Host-only packaging of the three step1276 Linux-qualified runtime
/// apps. Reads exact retained evidence and current relevant raw sources; never
/// compiles, maps or executes an app. Returned bytes remain caller-owned.
/// Missing, stale, linked, oversized or substituted inputs fail before output.</summary>
public static class NativeAotRuntimePackage
{
    private const string Qualification="artifacts/runtime-payload-r02/";
    private const string ProjectHash="D8AFB29D1F4465AD7D3EF3F4A292255DBC5E6975FFD4AD02948E912B1D88564B";
    private static readonly UTF8Encoding Utf8=new(false,true);
    private static readonly (string Path,string Hash)[] SharedSources=[
        ("global.json","FD3FDAAE1B349B01D6E6835EC111903F107A2643F3BCE313FAD4DC20180F5FB2"),
        ("Directory.Build.props","DF8A0D794DB73060124F5134EB930DCDBF6EE526C2410227F0C668AB6D10C5E5"),
        ("Directory.Build.targets","85AFD90F9DD0F550B6A3C6AD71D28BF714A8C930158DB872F8DA5D316889931A"),
        ("tools/Directory.Build.props","44B8268EA6934335DBC79FEF47C1CE1FDEE49393C929C92E322417B14AA321AE"),
        ("tools/OS.Tooling/NativeAotPayload.cs","44F16ABB380FE7F3CD42BA3C7E54710172F0320998CF2E85E296F8A7CE184AA5"),
        ("tools/OS.Tooling/NativeAotRuntimePayload.cs","4E04A469F05111A9A54180D845601C5C04F29D5ED55624225AADD649A3AA1AC7")];

    internal readonly record struct Payload(string Name,int Bytes,string Hash,string Source,string Guest,
        int DllBytes,string DllHash,int ObjectBytes,string ObjectHash,int StdoutBytes,string StdoutHash,int Traces);
    internal static Payload Specification(string app)=>app switch
    {
        "NativeAotSuspensionStress"=>new(app,1576912,"8581EE49DF39E639BAA4BDE83E724706BE7B923C708707BC6609BB7DA3C49C2E",
            "E7878D6036F00EFE3E11318033F17A8862A38330335C1C64CFD569D56DA5CB3D","52ACFBDE5BB104DFF29A8BD0F57F4849B2A1100D5303A44599DC202B117E9402",
            11776,"B50A28A446AB60150D10E53B99BF3CE951CDA796220D07EDAF5009CD6BA0F463",6036485,"C5BF994153128E54502D98F12F72A31CBB04D02CF1226DEDC2DE894F0018B57B",
            1720,"483C0BE0316175A01000A7FAAB262857447A86156958B26B19D14D4CDC31E01A",18),
        "NativeAotThreadPoolStress"=>new(app,1627408,"B5644BD17F505DC722849F232042916FEF60C2637FF112D03ACCD9ED61368444",
            "4C6D6CF9D3EB8388168FD5B9CDD54B9A5DDA8E2A4ABBDCD958AA3F9F57B19838","7B01732B05219BCD18DC6FBD74B70A16A189CD37C7EA80668207F8804013C686",
            25088,"D9BE9925249FE36DB3962278D21ACCE2F57B1DF0FAE4AD5EFFF67285835A67CC",6132786,"D9D81C5E7E6E424C7672130637C03741BDD549D7DB4933E77A6C099A58FCF9D8",
            1612,"F55B844FD6C7F1CF36F15511F937C6F6C37ECC5BFAC926ACEFAC3A2045CB987C",8),
        "NativeAotRuntimeMatrix"=>new(app,1577616,"8510C37D6DF0686F8FD1CC5972C5D6A0421C3DE82CE13E5671731C144CCDBFE0",
            "360673D96E5706DFC15949F597BED32C9C0C7D642F6F20B03BC2EC1A99FC5BE6","15264115F5D1826A172258D8A18A16DB3FDA9300652F85760C444AC9E0D3451E",
            22528,"9BCC667ADA36380F3FE5B0BCC8EDF25E20883EEC531E38328E79818DE2505C6D",5888252,"EFC53B4354D4B2D07D3EB4D50A7C76CA0998E42CCA92184E0DB64CD70FF5C619",
            2466,"B64FAE964476EBF3C7DD2A8C9B9559CEECBB85FA520EA93BE764BF7CC15E2280",13),
        _=>throw new ArgumentException("runtime_package_app",nameof(app))
    };

    /// <summary>Canonical no-argument/no-runtime-override manifest with explicit
    /// user IRQs and the thread service's existing sixteen-slot limit. The
    /// matrix needs at least nine live threads; the eight-slot native owner
    /// cannot run this policy yet. Actual native capacity remains to qualify.
    /// The required endpoint field grants no socket or network service.</summary>
    public static string Manifest(string app)
    {
        var p=Specification(app);
        return "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\napp.path=apps/"+p.Name+
            "\nargs=\ndigest="+p.Hash.ToLowerInvariant()+"\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=16\nlisten=127.0.0.1:8080\nenv.CLROS_USER_IRQS=1\n";
    }
    private static IEnumerable<(string Path,string Hash)> Sources(Payload p)=>SharedSources.Concat([
        ($"apps/{p.Name}/Program.cs",p.Source),($"apps/{p.Name}/{p.Name}.csproj",ProjectHash),($"tools/OS.Tooling/{p.Name}Guest.cs",p.Guest)]);

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
    /// inputs are borrowed synchronously. Success grants no guest admission.</summary>
    public static unsafe byte[] Create(string root,string evidenceRoot,string app)
    {
        var p=Specification(app);RequireCurrentSources(root,app);
        byte[] receipt=Pinned(evidenceRoot,Qualification+"receipt.json",1267256,"E514B0BCF197176EC80289C9A48567EC43F012431D9318E04EC3C0554E16D526");
        using JsonDocument document=JsonDocument.Parse(receipt);var data=document.RootElement;
        Need(data.GetProperty("schema").GetInt32()==1 && data.GetProperty("outcome").GetString()=="passed" &&
            data.GetProperty("sdk").GetString()==NativeAotPayload.SdkVersion && data.GetProperty("runtime").GetString()==NativeAotPayload.RuntimeVersion,"producer_policy");
        foreach(var source in Sources(p))Need(data.GetProperty("sources").GetProperty(source.Path).GetString()==source.Hash,"producer_source");
        var commands=data.GetProperty("commands");
        Need(commands.GetArrayLength()==29 && commands.EnumerateArray().All(c=>c.GetProperty("exitCode").GetInt32()==0),"producer_commands");
        string intermediate=$"apps/{app}/obj/Release/net11.0/linux-musl-x64/";
        _=Pinned(evidenceRoot,intermediate+app+".dll",p.DllBytes,p.DllHash);
        _=Pinned(evidenceRoot,intermediate+"native/"+app+".o",p.ObjectBytes,p.ObjectHash);
        Need(data.GetProperty("nativeInputs").GetProperty("/clros-runtime-r02/"+intermediate+app+".dll").GetString()==p.DllHash &&
            data.GetProperty("nativeInputs").GetProperty("/clros-runtime-r02/"+intermediate+"native/"+app+".o").GetString()==p.ObjectHash,"producer_outputs");
        NativeAotRuntimePayload.ValidateOutput(app,Utf8.GetString(Pinned(evidenceRoot,Qualification+app+"-run.stdout.log",p.StdoutBytes,p.StdoutHash)));
        string traces=Path.Combine(Path.GetFullPath(evidenceRoot),Qualification);AuthorityFiles.RequirePlainPath(Path.GetFullPath(evidenceRoot),traces,"runtime_evidence_path");
        var observed=NativeAotRuntimePayload.ReadTraceInventory(traces,app);
        var recorded=data.GetProperty("applications").EnumerateArray().Single(a=>a.GetProperty("name").GetString()==app);
        Need(recorded.GetProperty("sha256").GetString()==p.Hash && observed.Files.Count==p.Traces &&
            recorded.GetProperty("traceFiles").EnumerateObject().Count()==p.Traces &&
            observed.Files.All(f=>recorded.GetProperty("traceFiles").GetProperty(f.Key).GetString()==f.Value) &&
            observed.Syscalls.SequenceEqual(recorded.GetProperty("observedSyscalls").EnumerateArray().Select(s=>s.GetString()!)),"producer_traces");
        byte[] elf=Pinned(evidenceRoot,Qualification+app+"/"+app,p.Bytes,p.Hash);_=NativeAotPayload.Inspect(elf);
        fixed(byte* image=elf)Need(LinuxElfLoader.Validate(image,(uint)elf.Length,64*1024*1024,1572864,out _)==LinuxElfLoadStatus.Ready,"loader");
        byte[] manifest=Encoding.ASCII.GetBytes(Manifest(app)),package=new byte[64+manifest.Length+elf.Length];
        Header(manifest.Length,elf.Length).CopyTo(package,0);manifest.CopyTo(package,64);elf.CopyTo(package,64+manifest.Length);
        RequirePackage(app,package);RequireCurrentSources(root,app);return package;
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
