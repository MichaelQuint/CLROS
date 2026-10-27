using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static class NativeAotSqliteContractTests
{
    private delegate byte[] ClrosPackageBuilder(ReadOnlySpan<byte> elf,string mode,string hash);
    private delegate NativeAotSqliteContract.ClrosPackageValues ClrosPackageInspector(ReadOnlySpan<byte> package,string mode,string hash);
    private static readonly ClrosPackageBuilder CreateCopiedPackage=typeof(NativeAotSqliteContract).GetMethod("CreateClrosPackageCore",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<ClrosPackageBuilder>();
    private static readonly ClrosPackageInspector InspectCopiedPackage=typeof(NativeAotSqliteContract).GetMethod("InspectClrosPackageCore",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<ClrosPackageInspector>();
    private static readonly string[] ClrosModes=["database","verify-database","recovery-seed","recovery-spill","recovery-verify"];

    [HostTest("sqlite copied CLROS packages bind five exact database file modes and a selected finite envelope")]
    private static void ClrosPackageModes()
    {
        byte[] elf=ClrosElf();string hash=Convert.ToHexString(SHA256.HashData(elf));
        foreach(string mode in ClrosModes)
        {
            string path=mode.StartsWith("recovery-",StringComparison.Ordinal)?"/data/recovery.db":"/data/contract.db";
            byte[] package=CreateCopiedPackage(elf,mode,hash);var values=InspectCopiedPackage(package,mode,hash);
            string manifest=ClrosManifest(mode,path,hash);var view=OS.Manifest.LinuxElfPackage.Validate(package);
            HostCheck.True(view.Succeeded && view.DataFileCount==0 && view.PayloadBytes==NativeAotSqliteContract.ClrosElfBytes &&
                view.Manifest.MemoryBytes==67108864 && view.Manifest.StackBytes==1572864 && view.Manifest.Threads==16 && view.Manifest.Descriptors==32 &&
                values.Mode==mode && values.DatabasePath==path && values.Manifest==manifest && values.PackageBytes==package.Length &&
                values.PackageSha256==Convert.ToHexString(SHA256.HashData(package)) && values.Arguments.SequenceEqual(new[]{"--"+mode,path}) &&
                values.Image.Entry==4231863 && values.Image.ProgramHeaderCount==11 && values.Image.Loads.Count==4 &&
                values.Image.Tls is {FileSize:24,MemorySize:264,Alignment:8} && !values.ResourceEnvelopeMeasured && !values.InstructionIndexAdmitted &&
                !values.ExecutionAuthority && !values.GrantsOriginalPublicationGuestRecoveryOrPersistenceAuthority);
            HostCheck.True(ClrosEnvelope(elf,manifest).AsSpan().SequenceEqual(package) &&
                package.AsSpan((int)view.PayloadOffset,(int)view.PayloadBytes).SequenceEqual(elf) && !manifest.Contains("--root",StringComparison.Ordinal) &&
                !manifest.Contains("CLROS_USER_IRQS",StringComparison.Ordinal));
            foreach(string other in ClrosModes.Where(other=>other!=mode))Reject(()=>InspectCopiedPackage(package,other,hash),"clros_package");
        }
    }

    [HostTest("sqlite public CLROS admission fixes the original ELF identity and rejects synthetic default unknown and option-spelled modes")]
    private static void ClrosPublicIdentity()
    {
        byte[] elf=ClrosElf();string hash=Convert.ToHexString(SHA256.HashData(elf));HostCheck.True(hash!=NativeAotSqliteContract.ClrosElfSha256);
        byte[] package=CreateCopiedPackage(elf,"database",hash);
        Reject(()=>NativeAotSqliteContract.CreateClrosPackage(elf,"database"),"clros_elf_identity");
        Reject(()=>NativeAotSqliteContract.InspectClrosPackage(package,"database"),"clros_package");
        foreach(string mode in new[]{"","default","Database","VERIFY-DATABASE","--database","recovery","recovery-spill\n",null!})
        {
            Reject(()=>NativeAotSqliteContract.CreateClrosPackage(elf,mode),"clros_mode");
            Reject(()=>NativeAotSqliteContract.InspectClrosPackage(package,mode),"clros_mode");
        }
        Reject(()=>NativeAotSqliteContract.CreateClrosPackage([],"database"),"clros_elf_length");
        Reject(()=>NativeAotSqliteContract.InspectClrosPackage([],"verify-database"),"clros_package");
    }

    [HostTest("sqlite copied CLROS geometry rejects changed entry loads TLS dynamic and truncated identities")]
    private static void ClrosGeometry()
    {
        foreach(Action<byte[]> change in new Action<byte[]>[]{
            elf=>BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(24),4231864),
            elf=>BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(120+4),7),
            elf=>BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(176+32),1338199),
            elf=>BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(288+40),265),
            elf=>BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(344),2)})
        {
            byte[] elf=ClrosElf();change(elf);string hash=Convert.ToHexString(SHA256.HashData(elf));
            Reject(()=>CreateCopiedPackage(elf,"database",hash),"clros_elf_geometry");
        }
        byte[] original=ClrosElf();Reject(()=>CreateCopiedPackage(original.AsSpan(0,original.Length-1),"database",Convert.ToHexString(SHA256.HashData(original))),"clros_elf_length");
    }

    [HostTest("sqlite canonical package framing rejects every reserved header area altered payload truncation and trailing bytes")]
    private static void ClrosFraming()
    {
        byte[] elf=ClrosElf();string hash=Convert.ToHexString(SHA256.HashData(elf));byte[] package=CreateCopiedPackage(elf,"recovery-seed",hash);
        foreach(int at in new[]{0,8,12,16,20,24,32,40,48,56,63})
        {byte[] changed=package.ToArray();changed[at]^=1;Reject(()=>InspectCopiedPackage(changed,"recovery-seed",hash),"clros_package");}
        Reject(()=>InspectCopiedPackage(package.AsSpan(0,package.Length-1),"recovery-seed",hash),"clros_package");
        Reject(()=>InspectCopiedPackage([..package,(byte)0],"recovery-seed",hash),"clros_package");
        int payload=package.Length-elf.Length;package[payload+4096]^=1;
        Reject(()=>InspectCopiedPackage(package,"recovery-seed",hash),"clros_elf_identity");
    }

    [HostTest("sqlite copied package policy rejects unconfined paths alternate arguments resource changes and user IRQ opt-in")]
    private static void ClrosPolicy()
    {
        byte[] elf=ClrosElf();string hash=Convert.ToHexString(SHA256.HashData(elf)),manifest=ClrosManifest("database","/data/contract.db",hash);
        foreach(string changed in new[]{
            manifest.Replace("/data/contract.db","/tmp/contract.db",StringComparison.Ordinal),
            manifest.Replace("/data/contract.db","/data/../private/contract.db",StringComparison.Ordinal),
            manifest.Replace("args=--database /data/contract.db","args=--database --root /data",StringComparison.Ordinal),
            manifest.Replace("memory=67108864","memory=134217728",StringComparison.Ordinal),
            manifest.Replace("stack=1572864","stack=65536",StringComparison.Ordinal),
            manifest.Replace("threads=16","threads=2",StringComparison.Ordinal),
            manifest.Replace("descriptors=32","descriptors=64",StringComparison.Ordinal),
            manifest+"env.CLROS_USER_IRQS=1\n",manifest.Replace(hash.ToLowerInvariant(),hash,StringComparison.Ordinal)})
            Reject(()=>InspectCopiedPackage(ClrosEnvelope(elf,changed),"database",hash),"clros_package");
    }

    [HostTest("sqlite copied package metadata and arguments remain immutable after borrowed ELF and package buffers change")]
    private static void ClrosCopiedValues()
    {
        byte[] elf=ClrosElf();string hash=Convert.ToHexString(SHA256.HashData(elf));byte[] package=CreateCopiedPackage(elf,"recovery-verify",hash);
        var values=InspectCopiedPackage(package,"recovery-verify",hash);string packageHash=values.PackageSha256;
        elf.AsSpan().Fill(0xcc);package.AsSpan().Fill(0xaa);
        HostCheck.True(values.Mode=="recovery-verify" && values.DatabasePath=="/data/recovery.db" && values.Image.Sha256==hash &&
            values.PackageSha256==packageHash && values.Arguments.SequenceEqual(new[]{"--recovery-verify","/data/recovery.db"}));
        bool args=false,loads=false;try{((IList<string>)values.Arguments).Clear();}catch(NotSupportedException){args=true;}
        try{((IList<NativeAotPayload.Segment>)values.Image.Loads).Clear();}catch(NotSupportedException){loads=true;}HostCheck.True(args && loads);
    }

    private static string ClrosManifest(string mode,string path,string hash)=>
        "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\napp.path=apps/NativeAotSqliteContract\nargs=--"+mode+" "+path+
        "\ndigest="+hash.ToLowerInvariant()+"\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=32\nlisten=0.0.0.0:8080\n";
    private static byte[] ClrosEnvelope(byte[] elf,string manifest)
    {
        byte[] text=Encoding.ASCII.GetBytes(manifest),result=new byte[64+text.Length+elf.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(result,OS.Manifest.LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8),1);BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12),1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16),64);BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(20),(uint)text.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(24),(ulong)elf.Length);text.CopyTo(result,64);elf.CopyTo(result,64+text.Length);return result;
    }
    private static byte[] ClrosElf()
    {
        byte[] elf=new byte[NativeAotSqliteContract.ClrosElfBytes];new byte[]{0x7f,69,76,70,2,1,1}.CopyTo(elf,0);
        void W16(int at,ushort value)=>BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at),value);
        void W32(int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at),value);
        void W64(int at,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at),value);
        W16(16,2);W16(18,62);W32(20,1);W64(24,4231863);W64(32,64);W16(52,64);W16(54,56);W16(56,11);
        void Segment(int at,uint type,uint flags,ulong offset,ulong address,ulong file,ulong memory,ulong alignment)
        {W32(at,type);W32(at+4,flags);W64(at+8,offset);W64(at+16,address);W64(at+32,file);W64(at+40,memory);W64(at+48,alignment);}
        Segment(64,1,4,0,4194304,748,748,4096);Segment(120,1,5,4096,4198400,2633584,2633584,4096);
        Segment(176,1,4,2637824,6832128,1338200,1338200,4096);Segment(232,1,6,3977448,8171752,49504,603016,4096);
        Segment(288,7,4,3977448,8171752,24,264,8);return elf;
    }
    private static JsonObject Assets()
    {
        JsonObject libraries=[];string hash=Convert.ToBase64String(Enumerable.Range(0,64).Select(i=>(byte)i).ToArray());
        foreach(var pin in NativeAotSqliteContract.PackagePins)
            libraries.Add(pin.Key+"/"+pin.Value,new JsonObject{["type"]="package",["sha512"]=hash});
        return new JsonObject{["libraries"]=libraries,["targets"]=new JsonObject{["net11.0"]=new JsonObject
            {["SQLite/3.53.4"]=new JsonObject{["runtimeTargets"]=new JsonObject
                {["runtimes/linux-musl-x64/native/libe_sqlite3.so"]=new JsonObject{["assetType"]="native",["rid"]="linux-musl-x64"}}}}}};
    }
    private static byte[] Json(JsonObject value)=>Encoding.UTF8.GetBytes(value.ToJsonString());
    private static void Reject(Action action,string reason)
    {
        string? actual=null;try{action();}catch(ToolFailure failure){actual=failure.Reason;}
        HostCheck.True(actual is not null && actual.StartsWith("sqlite_contract_",StringComparison.Ordinal) && actual.Contains(reason,StringComparison.Ordinal),reason+": "+actual);
    }

    [HostTest("sqlite contract pins direct provider and native dependencies without EF or static closure inference")]
    private static void DependencyClosure()
    {
        var result=NativeAotSqliteContract.InspectDependencyValues(Json(Assets()));
        HostCheck.True(result.Packages.Count==7 && result.Packages.Single(p=>p.Name=="SQLite").Version=="3.53.4" &&
            result.Packages.Single(p=>p.Name=="Microsoft.Data.Sqlite.Core").Version==NativeAotPayload.RuntimeVersion &&
            result.MuslNativeAssets.Single()=="runtimes/linux-musl-x64/native/libe_sqlite3.so" && !result.HasStaticArchive && !result.GrantsNativeLinkageOrGuestAuthority);
        foreach(string mode in new[]{"version","missing","ef","unknown","duplicate","hash","path"})
        {
            var value=Assets();var libraries=value["libraries"]!.AsObject();string key="Microsoft.Data.Sqlite.Core/"+NativeAotPayload.RuntimeVersion;
            if(mode=="version"){var entry=libraries[key]!.DeepClone();libraries.Remove(key);libraries.Add("Microsoft.Data.Sqlite.Core/10.0.0",entry);}
            if(mode=="missing")libraries.Remove(key);
            if(mode=="ef")libraries.Add("Microsoft.EntityFrameworkCore/11.0.0",new JsonObject{["type"]="package"});
            if(mode=="unknown")libraries.Add("SQLitePCLRaw.provider.dynamic_cdecl/3.0.5",new JsonObject{["type"]="package"});
            if(mode=="duplicate")libraries.Add("SQLITE/3.53.4",libraries["SQLite/3.53.4"]!.DeepClone());
            if(mode=="hash")libraries[key]!["sha512"]="not-a-hash";
            if(mode=="path")value["targets"]!["net11.0"]!["SQLite/3.53.4"]!["runtimeTargets"]!.AsObject().Add("../native.so",new JsonObject());
            Reject(()=>NativeAotSqliteContract.InspectDependencyValues(Json(value)),mode switch
                {"missing"=>"package_missing","ef"=>"ef_core_forbidden","hash"=>"package_hash","path"=>"native_path",_=>"package_pin"});
        }
        bool copied=false;try{((IList<NativeAotSqliteContract.Package>)result.Packages).Clear();}catch(NotSupportedException){copied=true;}HostCheck.True(copied);
    }

    private static byte[] ArchiveVector()
    {
        string[] names=["sqlite3_open_v2","sqlite3_close_v2","sqlite3_prepare_v2","sqlite3_step","sqlite3_finalize",
            "sqlite3_bind_text","sqlite3_bind_blob","sqlite3_libversion","sqlite3_sourceid"];
        byte[] strings=Encoding.ASCII.GetBytes("\0"+string.Join('\0',names)+"\0");int stringAt=336,symbolAt=(stringAt+strings.Length+7)&~7;
        byte[] elf=new byte[symbolAt+(names.Length+1)*24];new byte[]{0x7f,69,76,70,2,1,1}.CopyTo(elf,0);
        void U16(int at,ushort value)=>BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at),value);
        void U32(int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at),value);
        void U64(int at,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at),value);
        U16(16,1);U16(18,62);U32(20,1);U64(40,64);U16(52,64);U16(58,64);U16(60,4);
        U32(128+4,1);U64(128+8,6);U64(128+24,320);U64(128+32,16);elf.AsSpan(320,16).Fill(0xc3);
        U32(192+4,2);U64(192+24,(ulong)symbolAt);U64(192+32,(ulong)((names.Length+1)*24));U32(192+40,3);U64(192+56,24);
        U32(256+4,3);U64(256+24,(ulong)stringAt);U64(256+32,(ulong)strings.Length);strings.CopyTo(elf,stringAt);
        int nameAt=1;
        for(int i=0;i<names.Length;i++)
        {
            int row=symbolAt+(i+1)*24;U32(row,(uint)nameAt);elf[row+4]=0x12;U16(row+6,1);U64(row+8,(ulong)i);U64(row+16,1);nameAt+=names[i].Length+1;
        }
        string header="sqlite.o/".PadRight(16)+"0".PadRight(12)+"0".PadRight(6)+"0".PadRight(6)+"100644".PadRight(8)+
            elf.Length.ToString(CultureInfo.InvariantCulture).PadRight(10)+"`\n";
        return [.."!<arch>\n"u8.ToArray(),..Encoding.ASCII.GetBytes(header),..elf,..(elf.Length%2==0?Array.Empty<byte>():new byte[]{10})];
    }

    private sealed class DependencyVector
    {
        internal const string Directory="/fixture/artifacts/sqlite-reference";
        internal readonly Dictionary<string,byte[]> Artifacts=new(StringComparer.Ordinal);
        internal readonly Dictionary<string,NativeAotSqliteContract.CompilerInput> Compiler=new(StringComparer.Ordinal);
        internal string[] Imports=["sqlite3_open_v2","sqlite3_close_v2","sqlite3_prepare_v2","sqlite3_step"];
        internal readonly JsonObject Receipt;
        internal JsonArray Commands;
        internal static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
        internal DependencyVector()
        {
            byte[] archive=ArchiveVector();int bytes=int.Parse(Encoding.ASCII.GetString(archive,56,10).Trim(),CultureInfo.InvariantCulture);
            byte[] obj=archive.AsSpan(68,bytes).ToArray();
            Artifacts.Add("libe_sqlite3.a",archive);Artifacts.Add("libe_sqlite3-repeat.a",archive.ToArray());
            Artifacts.Add("round0/sqlite3.o",obj);Artifacts.Add("round1/sqlite3.o",obj.ToArray());
            JsonObject native=[];foreach(string role in new[]{"dotnet","gcc","clang","ld","ar","readelf","nm","strace","setsid",
                "libc.a","libm.a","libz.a","libstdc++.a","libgcc.a","libgcc_eh.a","crt1.o","crti.o","crtn.o"})
            {string path="/compiler/"+role,hash=Hash(Encoding.ASCII.GetBytes("independent compiler "+role));Compiler.Add(role,new(path,hash));native[path]=hash;}
            native[Directory+"/upstream/sqlite3.c"]=NativeAotSqliteContract.SourceCsha256;
            native[Directory+"/upstream/sqlite3.h"]=NativeAotSqliteContract.SourceHeaderSha256;
            Commands=[];
            for(int round=0;round<2;round++)
            {
                string objectPath=Directory+"/round"+round+"/sqlite3.o",lib=Directory+(round==0?"/libe_sqlite3.a":"/libe_sqlite3-repeat.a");
                Commands.Add(new JsonObject{["executable"]="gcc",["workingDirectory"]="/fixture",["log"]="compile-r"+round,
                    ["arguments"]=new JsonArray("-c",Directory+"/upstream/sqlite3.c","-o",objectPath,"-O2","-fPIC","-ffunction-sections","-fdata-sections",
                        "-march=x86-64-v2","-mtune=generic","-DSQLITE_THREADSAFE=1")});
                Commands.Add(new JsonObject{["executable"]="ar",["workingDirectory"]="/fixture",["log"]="archive-r"+round,["arguments"]=new JsonArray("crD",lib,objectPath)});
                native[objectPath]=Hash(obj);native[lib]=Hash(archive);
            }
            var inspected=NativeAotSqliteContract.InspectStaticArchiveValues(archive);JsonObject outputs=[];
            foreach(var artifact in Artifacts)outputs[artifact.Key]=Hash(artifact.Value);
            outputs["upstream/sqlite3.c"]=NativeAotSqliteContract.SourceCsha256;outputs["upstream/sqlite3.h"]=NativeAotSqliteContract.SourceHeaderSha256;
            outputs["upstream/sqlite-amalgamation-3530400.zip"]=NativeAotSqliteContract.SourceArchiveSha256;
            Receipt=new JsonObject{["Schema"]="clros-sqlite-linux-reference-v1",["Succeeded"]=true,["Failure"]="",
                ["SourceArchiveSha256"]=NativeAotSqliteContract.SourceArchiveSha256,["SourceArchiveSha3"]=NativeAotSqliteContract.SourceArchiveSha3,
                ["SourceId"]=NativeAotSqliteContract.SourceId,["Imports"]=new JsonArray(Imports.Select(p=>(JsonNode?)JsonValue.Create(p)).ToArray()),
                ["Archive"]=JsonSerializer.SerializeToNode(inspected),["Native"]=native,["Outputs"]=outputs};RehashCommands();
        }
        internal void RehashCommands(){Artifacts["commands.json"]=Encoding.UTF8.GetBytes(Commands.ToJsonString());Rehash("commands.json");}
        internal void Rehash(string name){Receipt["Outputs"]![name]=Hash(Artifacts[name]);}
        internal NativeAotSqliteContract.ArchiveDependency Inspect()
        {byte[] bytes=Json(Receipt);return NativeAotSqliteContract.InspectArchiveDependencyValues(bytes,Hash(bytes),Artifacts,Imports,Compiler);}
    }

    [HostTest("sqlite archive reuse validates independent two-build objects members recipes and copied immutable provenance without native authority")]
    private static void ArchiveDependencyValues()
    {
        var vector=new DependencyVector();var actual=vector.Inspect();
        HostCheck.True(actual.Archive.ObjectFiles==1 && actual.Archive.Bytes==vector.Artifacts["libe_sqlite3.a"].Length &&
            actual.ReceiptSha256==DependencyVector.Hash(Json(vector.Receipt)) && actual.ObjectSha256==DependencyVector.Hash(vector.Artifacts["round0/sqlite3.o"]) &&
            actual.CompilerInputs.Count==18 && !actual.GrantsNativeOrExecutionAuthority);
        string gcc=actual.CompilerInputs["gcc"].Sha256;vector.Compiler["gcc"]=new("/changed/gcc",new string('A',64));
        vector.Artifacts["round0/sqlite3.o"].AsSpan().Clear();vector.Receipt.Clear();
        HostCheck.Equal(gcc,actual.CompilerInputs["gcc"].Sha256);
        bool immutable=false;try{((IDictionary<string,NativeAotSqliteContract.CompilerInput>)actual.CompilerInputs).Clear();}catch(NotSupportedException){immutable=true;}
        HostCheck.True(immutable && actual.Archive.DefinedSqliteSymbols.Contains("sqlite3_step"));
    }

    [HostTest("sqlite archive reuse refuses mismatched expected receipt failed unknown malformed and duplicated original declarations")]
    private static void ArchiveDependencyReceipt()
    {
        var original=new DependencyVector();byte[] exact=Json(original.Receipt);
        Reject(()=>NativeAotSqliteContract.InspectArchiveDependencyValues(exact,new string('A',64),original.Artifacts,original.Imports,original.Compiler),"dependency_receipt_hash");
        foreach(string mode in new[]{"failed","failure","schema","missing","duplicate","malformed","capacity"})
        {
            var f=new DependencyVector();
            if(mode=="failed")f.Receipt["Succeeded"]=false;if(mode=="failure")f.Receipt["Failure"]="compile timed out";
            if(mode=="schema")f.Receipt["Schema"]="unreviewed-original";if(mode=="missing")f.Receipt.Remove("Archive");
            byte[] bytes=mode switch{
                "duplicate"=>Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Json(f.Receipt)).Replace("\"Succeeded\":true","\"Succeeded\":true,\"Succeeded\":false",StringComparison.Ordinal)),
                "malformed"=>"{"u8.ToArray(),"capacity"=>new byte[2*1024*1024+1],_=>Json(f.Receipt)};
            Reject(()=>NativeAotSqliteContract.InspectArchiveDependencyValues(bytes,DependencyVector.Hash(bytes),f.Artifacts,f.Imports,f.Compiler),
                mode is "failed" or "failure" or "schema"?"dependency_original_failed":mode=="capacity"?"dependency_receipt_hash":"dependency_json");
        }
    }

    [HostTest("sqlite archive reuse rejects source provider compiler and static library substitution rather than accepting matching archive hash alone")]
    private static void ArchiveDependencyInputs()
    {
        foreach(string mode in new[]{"zip","c","header","id","imports","gcc","library","missing-tool","extra-tool","source-native"})
        {
            var f=new DependencyVector();
            if(mode=="zip")f.Receipt["SourceArchiveSha256"]=new string('A',64);
            if(mode=="c")f.Receipt["Outputs"]!["upstream/sqlite3.c"]=new string('A',64);
            if(mode=="header")f.Receipt["Outputs"]!["upstream/sqlite3.h"]=new string('A',64);
            if(mode=="id")f.Receipt["SourceId"]="different engine";
            if(mode=="imports")f.Receipt["Imports"]![0]="sqlite3_unknown_provider";
            if(mode=="gcc")f.Compiler["gcc"]=f.Compiler["gcc"] with{Sha256=new string('A',64)};
            if(mode=="library")f.Receipt["Native"]![f.Compiler["libc.a"].Path]=new string('A',64);
            if(mode=="missing-tool")f.Compiler.Remove("ar");if(mode=="extra-tool")f.Compiler.Add("unreviewed",new("/compiler/unreviewed",new string('A',64)));
            if(mode=="source-native")f.Receipt["Native"]![DependencyVector.Directory+"/upstream/sqlite3.c"]=new string('A',64);
            Reject(()=>f.Inspect(),mode switch{"imports"=>"dependency_imports","gcc" or "library"=>"dependency_compiler_changed",
                "missing-tool" or "extra-tool"=>"dependency_compiler_inventory",_=>"dependency_source"});
        }
    }

    [HostTest("sqlite archive reuse compares actual output bytes identical repeats and sole archive object lineage")]
    private static void ArchiveDependencyArtifacts()
    {
        foreach(string mode in new[]{"hash","repeat","member","native","declared","extra","size"})
        {
            var f=new DependencyVector();
            if(mode=="hash")f.Artifacts["round0/sqlite3.o"][320]^=1;
            if(mode=="repeat"){f.Artifacts["round1/sqlite3.o"][320]^=1;f.Rehash("round1/sqlite3.o");}
            if(mode=="member")foreach(string name in new[]{"round0/sqlite3.o","round1/sqlite3.o"}){f.Artifacts[name][320]^=1;f.Rehash(name);f.Receipt["Native"]![DependencyVector.Directory+"/"+name]=DependencyVector.Hash(f.Artifacts[name]);}
            if(mode=="native")f.Receipt["Native"]![DependencyVector.Directory+"/round0/sqlite3.o"]=new string('A',64);
            if(mode=="declared")f.Receipt["Archive"]!["Bytes"]=1;
            if(mode=="extra")f.Artifacts.Add("unverified.o",new byte[]{1});
            if(mode=="size")f.Artifacts["round0/sqlite3.o"]=new byte[16*1024*1024+1];
            Reject(()=>f.Inspect(),mode switch{"hash"=>"dependency_output_hash","repeat"=>"dependency_repeat","member"=>"dependency_object_member",
                "native"=>"dependency_native_output","declared"=>"dependency_archive","extra"=>"dependency_inventory",_=>"dependency_artifact_size"});
        }
    }

    [HostTest("sqlite archive reuse rejects altered defines optimization source archive ordering and extra or duplicate compile attempts")]
    private static void ArchiveDependencyRecipes()
    {
        foreach(string mode in new[]{"optimization","define","optional","source","archive","order","duplicate","extra","workdir"})
        {
            var f=new DependencyVector();
            if(mode=="optimization")f.Commands[0]!["arguments"]![4]="-O0";
            if(mode=="define")f.Commands[0]!["arguments"]!.AsArray().Add("-DSQLITE_OMIT_WAL=1");
            if(mode=="optional"){f.Imports[0]="sqlite3_column_table_name";f.Receipt["Imports"]![0]="sqlite3_column_table_name";}
            if(mode=="source")f.Commands[0]!["arguments"]![1]="/fixture/artifacts/substitute.c";
            if(mode=="archive")f.Commands[1]!["arguments"]![0]="cr";
            if(mode=="order"){var command=f.Commands[1]!.DeepClone();f.Commands.RemoveAt(1);f.Commands.Add(command);}
            if(mode=="duplicate")f.Commands.Add(f.Commands[0]!.DeepClone());
            if(mode=="extra"){var command=f.Commands[0]!.DeepClone();command["log"]="compile-r2";f.Commands.Add(command);}
            if(mode=="workdir")f.Commands[2]!["workingDirectory"]="/other";
            f.RehashCommands();Reject(()=>f.Inspect(),mode is "order" or "duplicate" or "extra"?"dependency_commands":"dependency_recipe");
        }
    }

    [HostTest("sqlite archive dependency command help makes reuse explicit and leaves the ordinary fresh reference route")]
    private static void ArchiveDependencyHelp()
    {
        TextWriter previous=Console.Out;using StringWriter text=new(CultureInfo.InvariantCulture);
        try {Console.SetOut(text);HostCheck.Equal(0,ToolCommand.Run(["--help"]));}finally{Console.SetOut(previous);}
        HostCheck.True(text.ToString().Contains("linux sqlite-reference|sqlite-recovery-reference",StringComparison.Ordinal) &&
            text.ToString().Contains("linux sqlite-reference-with-archive",StringComparison.Ordinal) &&
            text.ToString().Contains("linux sqlite-recovery-reference-with-archive",StringComparison.Ordinal) &&
            NativeAotSqliteContract.ArchiveDependencyReceiptSha256=="77B2A06C28E919CDE82383839AA37CDC3A9AB3FC131FD781CA26559B07C4D21B");
    }

    private sealed class HeldDependencyVector : IDisposable
    {
        internal static readonly string[] Names=["receipt.json","commands.json","round0/sqlite3.o","round1/sqlite3.o","libe_sqlite3.a",
            "libe_sqlite3-repeat.a","upstream/sqlite3.c","upstream/sqlite3.h","upstream/sqlite-amalgamation-3530400.zip"];
        internal readonly string Root=Path.Combine(HostToolProcess.FindRepositoryRoot(),"artifacts/sqlite-archive-held-tests",Guid.NewGuid().ToString("N"));
        internal readonly DependencyVector Values=new();
        internal string ReceiptHash=>DependencyVector.Hash(File.ReadAllBytes(PathOf("receipt.json")));
        internal string PathOf(string relative)=>Path.Combine(Root,relative);
        internal HeldDependencyVector()
        {
            Directory.CreateDirectory(Root);
            foreach(var pair in Values.Artifacts)Write(pair.Key,pair.Value);
            // Deliberately not pinned SQLite source: this helper grants only
            // local held-file identity. Full recipe/source admission must fail.
            foreach(string name in Names.Where(p=>p.StartsWith("upstream/",StringComparison.Ordinal)))
            {
                byte[] bytes=Encoding.UTF8.GetBytes("independent held source "+name);Write(name,bytes);
                Values.Receipt["Outputs"]![name]=DependencyVector.Hash(bytes);
            }
            WriteReceipt();
        }
        internal void Write(string relative,byte[] bytes)
        {string path=PathOf(relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,bytes);}
        internal void WriteReceipt()=>Write("receipt.json",Json(Values.Receipt));
        internal IReadOnlyDictionary<string,AuthorityFile> Hold(AuthorityFiles files)=>
            NativeAotSqliteContract.HoldArchiveDependencyInputs(files,Root,ReceiptHash);
        public void Dispose()=>Directory.Delete(Root,true);
    }
    private static void ToolReject(Action action,string reason)
    {
        try{action();}catch(ToolFailure failure){HostCheck.Equal(reason,failure.Reason);return;}
        throw new InvalidOperationException("expected "+reason);
    }

    [HostTest("sqlite recovery outer nine-file hold survives nested publisher disposal without granting pinned source authority")]
    private static void RecoveryDependencyLifetime()
    {
        using var vector=new HeldDependencyVector();using AuthorityFiles outer=new();var files=vector.Hold(outer);
        HostCheck.True(files.Count==9 && files.Keys.Order(StringComparer.Ordinal).SequenceEqual(HeldDependencyVector.Names.Order(StringComparer.Ordinal)));
        using(AuthorityFiles nested=new())
        {
            var inside=vector.Hold(nested);HostCheck.True(files.All(p=>!ReferenceEquals(p.Value,inside[p.Key])));
            nested.VerifyUnchanged();
        }
        foreach(var file in files)HostCheck.True(file.Value.ReadBytes(16*1024*1024,"fixture").SequenceEqual(File.ReadAllBytes(vector.PathOf(file.Key))));
        outer.VerifyUnchanged();bool readOnly=false;
        try{((IDictionary<string,AuthorityFile>)files).Clear();}catch(NotSupportedException){readOnly=true;}HostCheck.True(readOnly);
        Reject(()=>vector.Values.Inspect(),"dependency_source");
    }

    [HostTest("sqlite recovery dependency hold rejects unknown receipt failed schema missing associations and actual substitutions")]
    private static void RecoveryDependencyAdmission()
    {
        using(var unknown=new HeldDependencyVector())using(AuthorityFiles files=new())
            Reject(()=>NativeAotSqliteContract.HoldArchiveDependencyInputs(files,unknown.Root,NativeAotSqliteContract.ArchiveDependencyReceiptSha256),"dependency_receipt_hash");
        foreach(string mode in new[]{"failed","failure","schema","missing","substitution","duplicate","malformed","capacity"})
        {
            using var vector=new HeldDependencyVector();using AuthorityFiles files=new();
            if(mode=="failed")vector.Values.Receipt["Succeeded"]=false;
            if(mode=="failure")vector.Values.Receipt["Failure"]="prior producer failed";
            if(mode=="schema")vector.Values.Receipt["Schema"]="unreviewed-recovery-receipt";
            if(mode=="missing")vector.Values.Receipt["Outputs"]!.AsObject().Remove("round1/sqlite3.o");
            vector.WriteReceipt();
            if(mode=="substitution")File.AppendAllText(vector.PathOf("round1/sqlite3.o"),"changed actual object");
            if(mode=="duplicate")File.WriteAllText(vector.PathOf("receipt.json"),Encoding.UTF8.GetString(Json(vector.Values.Receipt)).Replace("\"Succeeded\":true","\"Succeeded\":true,\"Succeeded\":false",StringComparison.Ordinal));
            if(mode=="malformed")File.WriteAllText(vector.PathOf("receipt.json"),"{");
            if(mode=="capacity")File.WriteAllBytes(vector.PathOf("commands.json"),new byte[2*1024*1024+1]);
            string reason=mode switch{
                "failed" or "failure" or "schema"=>"sqlite_contract_dependency_original_failed",
                "duplicate" or "malformed"=>"sqlite_contract_dependency_json",
                "capacity"=>"sqlite_archive_dependency_size_out_of_range",_=>"sqlite_contract_dependency_output_hash"};
            ToolReject(()=>vector.Hold(files),reason);
        }
    }

    [HostTest("sqlite recovery each original remains protected against mutation after the inner publisher closes")]
    private static void RecoveryDependencyMutation()
    {
        foreach(string name in HeldDependencyVector.Names)
        {
            using var vector=new HeldDependencyVector();string path=vector.PathOf(name);byte[] original=File.ReadAllBytes(path);
            using(AuthorityFiles outer=new())
            {
                _=vector.Hold(outer);using(AuthorityFiles nested=new())_=vector.Hold(nested);
                bool denied=false;try{File.WriteAllBytes(path,[..original,0x7f]);}catch(IOException){denied=true;}
                if(denied){HostCheck.True(OperatingSystem.IsWindows());outer.VerifyUnchanged();}
                else ToolReject(outer.VerifyUnchanged,"authority_input_changed");
            }
            File.WriteAllBytes(path,original); // disposal released the outer handles
        }
    }

    [HostTest("sqlite recovery each original name replacement is blocked or detected despite identical bytes after nested dispose")]
    private static void RecoveryDependencyReplacement()
    {
        foreach(string name in HeldDependencyVector.Names)
        {
            using var vector=new HeldDependencyVector();string path=vector.PathOf(name),old=path+".old",replacement=path+".new";
            byte[] bytes=File.ReadAllBytes(path);File.WriteAllBytes(replacement,bytes);
            using(AuthorityFiles outer=new())
            {
                _=vector.Hold(outer);using(AuthorityFiles nested=new())_=vector.Hold(nested);
                bool denied=false;try{File.Move(path,old);}catch(IOException){denied=true;}
                if(denied){HostCheck.True(OperatingSystem.IsWindows());outer.VerifyUnchanged();}
                else {File.Move(replacement,path);ToolReject(outer.VerifyUnchanged,"authority_input_changed");}
            }
            File.WriteAllBytes(path,bytes);
        }
    }

    [HostTest("sqlite recovery partial dependency rejection retains caller ownership until outer disposal")]
    private static void RecoveryDependencyFailureLifetime()
    {
        using var vector=new HeldDependencyVector();vector.Values.Receipt["Outputs"]!["upstream/sqlite-amalgamation-3530400.zip"]=new string('A',64);vector.WriteReceipt();
        string path=vector.PathOf("receipt.json");byte[] bytes=File.ReadAllBytes(path);
        using(AuthorityFiles outer=new())
        {
            Reject(()=>vector.Hold(outer),"dependency_output_hash");
            bool denied=false;try{File.WriteAllBytes(path,[..bytes,0]);}catch(IOException){denied=true;}
            if(denied){HostCheck.True(OperatingSystem.IsWindows());outer.VerifyUnchanged();}
            else ToolReject(outer.VerifyUnchanged,"authority_input_changed");
        }
        File.WriteAllBytes(path,bytes);
    }

    [HostTest("sqlite recovery archive CLI refuses missing extra and unknown routes before native execution")]
    private static void RecoveryDependencyRoute()
    {
        TextWriter previous=Console.Error,previousOutput=Console.Out;using StringWriter errors=new(CultureInfo.InvariantCulture);using StringWriter output=new(CultureInfo.InvariantCulture);
        try
        {
            Console.SetError(errors);Console.SetOut(output);
            foreach(string[] args in new[]{new[]{"linux","sqlite-recovery-reference-with-archive","recipe","zip","old"},
                new[]{"linux","sqlite-recovery-reference-with-archive","recipe","zip","old","fresh","extra"},
                new[]{"linux","sqlite-recovery-reference-with-unknown","recipe","zip","old","fresh"}})
                HostCheck.Equal(1,ToolCommand.Run(args));
            HostCheck.Equal(3,errors.ToString().Split("unknown_command",StringSplitOptions.None).Length-1);
            if(!OperatingSystem.IsLinux())
            {
                errors.GetStringBuilder().Clear();HostCheck.Equal(1,ToolCommand.Run(["linux","sqlite-recovery-reference-with-archive","recipe","zip","old","fresh-recovery"]));
                HostCheck.True(errors.ToString().Contains("linux_recovery_platform",StringComparison.Ordinal));
            }
        }
        finally{Console.SetError(previous);Console.SetOut(previousOutput);}
    }

    [HostTest("sqlite contract static archive requires real x64 relocatable objects and defined executable symbols")]
    private static void NativeClosure()
    {
        byte[] original=ArchiveVector();var report=NativeAotSqliteContract.InspectStaticArchiveValues(original);
        HostCheck.True(report.ObjectFiles==1 && report.Bytes==original.Length && report.DefinedSqliteSymbols.Contains("sqlite3_step") &&
            report.Sha256==Convert.ToHexString(SHA256.HashData(original)) && !report.GrantsCompleteNativeLinkageAuthority);
        foreach(string mode in new[]{"shared","thin","truncated","machine","missing","noncode"})
        {
            byte[] value=original.ToArray();
            if(mode=="shared")BinaryPrimitives.WriteUInt16LittleEndian(value.AsSpan(68+16),3);
            if(mode=="thin")"!<thin>\n"u8.CopyTo(value);
            if(mode=="truncated")value=value[..^1];
            if(mode=="machine")BinaryPrimitives.WriteUInt16LittleEndian(value.AsSpan(68+18),183);
            if(mode=="missing")value[68+336+1]=(byte)'z';
            if(mode=="noncode")BinaryPrimitives.WriteUInt64LittleEndian(value.AsSpan(68+128+8),2);
            Reject(()=>NativeAotSqliteContract.InspectStaticArchiveValues(value),mode switch
                {"shared" or "machine"=>"archive_elf","thin"=>"static_archive_required","truncated"=>"archive_member","missing"=>"archive_symbols",_=>"archive_symbol_body"});
        }
        Reject(()=>NativeAotSqliteContract.InspectStaticArchiveValues(new byte[]{0x7f,69,76,70,2,1,1}),"static_archive_required");
    }

    private static string Transcript()
    {
        string source=Convert.ToBase64String(Encoding.UTF8.GetBytes("2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc"));
        return "TEST_START m10.020\nSQLITE_ENGINE version=3.53.4 sourceBase64="+source+
            "\nSQLITE_POLICY journal=delete synchronous=2 locking=normal pageSize=4096\nSQLITE_COMMIT rows=1 textBytes=22 blobBytes=34003"+
            " journal=true\nSQLITE_ROLLBACK rows=1 journal=true\nSQLITE_LOCK busy=5 rows=1\nSQLITE_REOPEN rows=1 integrity=ok\n"+
            "SQLITE_FAILURE readonly=8 missingParent=14\nSQLITE_CLEANUP journal=false wal=false shm=false\nTEST_PASS m10.020\n";
    }

    [HostTest("sqlite contract finite values require rollback commit lock reopen and explicit failures")]
    private static void TranscriptValues()
    {
        string original=Transcript();var value=NativeAotSqliteContract.InspectTranscriptValues(original);
        HostCheck.True(value.Engine=="3.53.4" && !value.GrantsNativeAotGuestOrDurabilityAuthority);
        foreach(var change in new[]{("synchronous=2","synchronous=0"),("busy=5","busy=0"),("readonly=8","readonly=0"),
            ("journal=false","journal=true"),("rows=1 integrity=ok","rows=2 integrity=ok"),("sourceBase64=","sourceBase64=unknown")})
            Reject(()=>NativeAotSqliteContract.InspectTranscriptValues(original.Replace(change.Item1,change.Item2,StringComparison.Ordinal)),"transcript_");
        Reject(()=>NativeAotSqliteContract.InspectTranscriptValues(original+"TEST_PASS m10.020\n"),"transcript_inventory");
    }

    [HostTest("sqlite independent readonly verifier protocol requires exact source row blob integrity and closed sidecars")]
    private static void VerificationTranscriptValues()
    {
        string original="TEST_START m10.020.verify\nSQLITE_VERIFY_ENGINE version=3.53.4 sourceBase64="+
            Convert.ToBase64String(Encoding.UTF8.GetBytes(NativeAotSqliteContract.SourceId))+"\n"+
            "SQLITE_VERIFY rows=1 textBytes=22 blobBytes=34003 integrity=ok readonly=true\n"+
            "SQLITE_VERIFY_CLEANUP journal=false wal=false shm=false\nTEST_PASS m10.020.verify\n";
        var result=NativeAotSqliteContract.InspectVerificationTranscriptValues(original);
        HostCheck.True(result==NativeAotSqliteContract.InspectTranscriptValues(Transcript()) && !result.GrantsNativeAotGuestOrDurabilityAuthority);
        foreach(var mutation in new[]{("version=3.53.4","version=3.53.3"),("rows=1","rows=2"),("blobBytes=34003","blobBytes=34002"),
            ("integrity=ok","integrity=failed"),("readonly=true","readonly=false"),("journal=false","journal=true"),("SQLITE_VERIFY_CLEANUP","SQLITE_CLEANUP")})
            Reject(()=>NativeAotSqliteContract.InspectVerificationTranscriptValues(original.Replace(mutation.Item1,mutation.Item2,StringComparison.Ordinal)),"verify_transcript_");
        foreach(string malformed in new[]{Transcript(),original+"TEST_PASS m10.020.verify\n",original.Replace("TEST_PASS m10.020.verify\n","",StringComparison.Ordinal),
            original.Replace("SQLITE_VERIFY_ENGINE","SQLITE_VERIFY_ENGINE extra=untrusted",StringComparison.Ordinal)})
            Reject(()=>NativeAotSqliteContract.InspectVerificationTranscriptValues(malformed),"verify_transcript_");
    }

    [HostTest("sqlite ordinary reference plans distinct writer and readonly verifier arguments without executing Linux")]
    private static void LinuxReferencePlan()
    {
        string[] expected=["-ff","-yy","-ttt","-s","256","-o","/fresh/verify-trace","/fresh/sqlite","--verify-database","/fresh/contract.db"];
        var verify=NativeAotSqliteContract.LinuxReferenceTraceArguments("/fresh/sqlite","/fresh/contract.db",true,"/fresh/verify-trace");
        HostCheck.True(verify.SequenceEqual(expected));
        var writer=NativeAotSqliteContract.LinuxReferenceTraceArguments("/fresh/sqlite","/fresh/contract.db",false,"/fresh/write-trace");
        HostCheck.True(writer[^2]=="--database" && writer[6]!="/fresh/verify-trace" && writer[^1]==verify[^1]);
        foreach(string path in new[]{"relative","/fresh/../db","/fresh/./db","/fresh//db","/fresh/db\n","/fresh/db\0","/"})
            Reject(()=>NativeAotSqliteContract.LinuxReferenceTraceArguments("/fresh/sqlite",path,true,"/fresh/trace"),"reference_linux_path");
        Reject(()=>NativeAotSqliteContract.LinuxReferenceTraceArguments("/fresh/sqlite","/fresh/sqlite",true,"/fresh/trace"),"reference_linux_path");
    }

    [HostTest("sqlite trace keeps nonreturning terminal exit distinct from missing futex continuation and preserves every copied raw row")]
    private static void TraceReturnAccounting()
    {
        const string database="/fresh/contract.db";
        NativeAotSqliteContract.Trace Parse(string main,string? other=null)
        {
            Dictionary<string,string> files=new(){["trace.1"]=main};if(other is not null)files.Add("trace.2",other);
            return NativeAotSqliteContract.InspectTraceValues(files,database);
        }
        const string terminal="1.000003 exit_group(0) = ?\n1.000004 +++ exited with 0 +++\n";
        var complete=Parse("1.000001 getpid() = 1\n"+terminal);
        HostCheck.True(complete.CompleteTraceProjection && complete.UnreturnedCalls==0 && complete.TerminalUnreturnedCalls==1 &&
            complete.Rows.Count==3 && complete.Rows[1].Raw=="1.000003 exit_group(0) = ?" && !complete.GrantsCompleteTraceOrExecutionAuthority);
        var wait=Parse("1.000001 getpid() = 1\n"+terminal,"1.000001 futex(0x1234, FUTEX_WAIT_PRIVATE, 2, NULL) = ?\n1.000004 +++ exited with 0 +++\n");
        HostCheck.True(wait.UnjoinedLines==0 && wait.UnreturnedCalls==1 && wait.TerminalUnreturnedCalls==1 && !wait.CompleteTraceProjection &&
            wait.Calls.Single(c=>c.Syscall=="futex").Result=="?" && wait.Rows.Count==5);
        var unfinished=Parse(terminal,"1.000001 read(3<pipe:[7]> <unfinished ...>) = ?\n1.000004 +++ exited with 0 +++\n");
        HostCheck.True(unfinished.UnjoinedLines==1 && !unfinished.CompleteTraceProjection && unfinished.Rows.Count==4 &&
            unfinished.Rows.Single(r=>r.Kind==NativeAotSqliteContract.TraceRowKind.Unfinished).Raw.Contains("pipe:[7]",StringComparison.Ordinal));
        foreach(string bad in new[]{terminal.Replace("exited with 0","killed by SIGKILL",StringComparison.Ordinal),
            terminal.Replace("exit_group(0)","exit_group(1)",StringComparison.Ordinal),terminal.Replace(" = ?"," = 0",StringComparison.Ordinal),
            "1.000003 exit_group(0) = ?\n",terminal+"1.000005 getpid() = 1\n",terminal+"1.000005 +++ exited with 0 +++\n"})
            HostCheck.True(!Parse(bad).CompleteTraceProjection);
    }

    [HostTest("sqlite contract trace values preserve arguments errno and unjoined calls while naming storage successors")]
    private static void TraceValues()
    {
        var files=new Dictionary<string,string>{["trace.17"]="""
            123.000001 openat(AT_FDCWD, "/app/data/contract.db", O_RDWR|O_CREAT, 0600) = 3</app/data/contract.db>
            123.000002 fcntl(3</app/data/contract.db>, F_SETLK, {l_type=F_WRLCK, l_start=1073741824, l_len=1}) = -1 EACCES (Permission denied)
            123.000003 pwrite64(3</app/data/contract.db>, "bytes", 5, 4096) = 5
            123.000004 fdatasync(3</app/data/contract.db>) = 0
            123.000005 unlink("/app/data/contract.db-journal") = 0
            123.000006 fsync(4</app/data>) = 0
            123.000007 read(3</app/data/contract.db>, <unfinished ...>
            123.000008 <... read resumed> "bytes", 5) = 5
            123.000009 +++ exited with 0 +++
            """};
        var value=NativeAotSqliteContract.InspectTraceValues(files,"/app/data/contract.db");
        HostCheck.True(value.Calls.Count==6 && value.UnjoinedLines==2 && value.Successors.SequenceEqual(new[]{"M06-020/#798","M06-021/#799","M16-027/#800"}) &&
            value.Calls[1].Result.StartsWith("-1 EACCES",StringComparison.Ordinal) && value.Calls[1].Arguments.Contains("1073741824",StringComparison.Ordinal) &&
            value.Calls[4].MentionsDatabase && !value.Calls[5].MentionsDatabase && !value.GrantsCompleteTraceOrExecutionAuthority);
        Reject(()=>NativeAotSqliteContract.InspectTraceValues(new Dictionary<string,string>{["trace.1"]="made up success"},"/app/data/contract.db"),"trace_call");
        Reject(()=>NativeAotSqliteContract.InspectTraceValues(files,"../contract.db"),"trace_input");
    }

    [HostTest("sqlite trace classifies measured musl vectored writes ownership and lstat without inventing signal pipe completion")]
    private static void MuslTraceValues()
    {
        // Original r03 uses pwritev2/RWF_NOAPPEND beneath libc pwrite. These
        // supplied rows test projection only; they do not replay its authority.
        var files=new Dictionary<string,string>{["trace.5255"]="""
            1792836276.967378 pwritev2(6</app/data/contract.db-journal>, [{iov_base="bytes", iov_len=5}], 1, 4096, RWF_NOAPPEND) = 5
            1792836276.959929 fchown(6</app/data/contract.db-journal>, 0, 0) = 0
            1792836278.530675 lstat("/app/data/contract.db-journal", 0x1234) = -1 ENOENT (No such file or directory)
            1792836278.536145 exit_group(0) = ?
            1792836278.540566 +++ exited with 0 +++
            """,["trace.5257"]="""
            1792836276.792870 read(3<pipe:[6103]> <unfinished ...>) = ?
            1792836278.537834 +++ exited with 0 +++
            """};
        var value=NativeAotSqliteContract.InspectTraceValues(files,"/app/data/contract.db");
        HostCheck.True(value.UnjoinedLines==1 && value.Calls.Count==4 && value.Calls.Take(3).All(c=>c.Successor=="M16-027/#800" && c.MentionsDatabase) &&
            value.Calls[0].Arguments.EndsWith("1, 4096, RWF_NOAPPEND",StringComparison.Ordinal) && value.Calls[0].Result=="5" &&
            value.Calls[2].Result.StartsWith("-1 ENOENT",StringComparison.Ordinal) && value.Calls.All(c=>!c.Arguments.Contains("pipe:",StringComparison.Ordinal)) &&
            !value.GrantsCompleteTraceOrExecutionAuthority);
        foreach(string syscall in new[]{"readv","writev","preadv","pwritev","preadv2","pwritev2"})
        {
            var mapped=NativeAotSqliteContract.InspectTraceValues(new Dictionary<string,string>{["trace.1"]="1.000001 "+syscall+"(3, [], 0, 0, 0) = 0\n"},"/app/data/contract.db");
            HostCheck.Equal("M16-027/#800",mapped.Calls.Single().Successor);
        }
    }

    private static Dictionary<string,string> StorageRows(string? rows=null)=>new()
    {
        ["trace.17"]=rows??"""
        1.000001 open("/app/data/contract.db", O_RDWR|O_CREAT|O_LARGEFILE|O_NOFOLLOW|O_CLOEXEC, 0644) = 5</app/data/contract.db>
        1.000002 fcntl(5</app/data/contract.db>, F_SETFD, FD_CLOEXEC) = 0
        1.000003 pread64(5</app/data/contract.db>, "", 100, 0) = 0
        1.000004 fcntl(5</app/data/contract.db>, F_SETLK, {l_type=F_WRLCK, l_whence=SEEK_SET, l_start=1073741825, l_len=1}) = 0
        1.000005 open("/app/data/contract.db-journal", O_RDWR|O_CREAT|O_LARGEFILE|O_NOFOLLOW|O_CLOEXEC, 0600) = 6</app/data/contract.db-journal>
        1.000006 fchown(6</app/data/contract.db-journal>, 0, 0) = 0
        1.000007 pwritev2(6</app/data/contract.db-journal>, [{iov_base="a, }], escaped\"", iov_len=512}], 1, 0, RWF_NOAPPEND) = 512
        1.000008 fsync(6</app/data/contract.db-journal>) = 0
        1.000009 open("/app/data", O_RDONLY|O_LARGEFILE|O_CLOEXEC) = 7</app/data>
        1.000010 fcntl(7</app/data>, F_SETFD, FD_CLOEXEC) = 0
        1.000011 fsync(7</app/data>) = 0
        1.000012 close(7</app/data>) = 0
        1.000013 pwritev2(6</app/data/contract.db-journal>, [{iov_base="header", iov_len=12}], 1, 0, RWF_NOAPPEND) = 12
        1.000014 fsync(6</app/data/contract.db-journal>) = 0
        1.000015 pwritev2(5</app/data/contract.db>, [{iov_base="page"..., iov_len=4096}], 1, 4096, RWF_NOAPPEND) = 4096
        1.000016 fsync(5</app/data/contract.db>) = 0
        1.000017 close(6</app/data/contract.db-journal>) = 0
        1.000018 unlink("/app/data/contract.db-journal") = 0
        1.000019 open("/app/data/contract.db", O_RDWR|O_LARGEFILE|O_NOFOLLOW|O_CLOEXEC) = 7</app/data/contract.db>
        1.000020 fcntl(7</app/data/contract.db>, F_SETLK, {l_type=F_WRLCK, l_whence=SEEK_SET, l_start=1073741825, l_len=1}) = -1 EAGAIN (Resource temporarily unavailable)
        1.000021 close(7</app/data/contract.db>) = 0
        1.000022 close(5</app/data/contract.db>) = 0
        1.000023 lstat("/app/data/contract.db-journal", 0x1234) = -1 ENOENT (No such file or directory)
        1.000024 +++ exited with 0 +++
        """,
        ["trace.18"]="1.000001 read(3<pipe:[6103]> <unfinished ...>) = ?\n1.000025 +++ exited with 0 +++\n"
    };

    [HostTest("sqlite storage trace follows descriptor generations and exact positioned lock sync values without upgrading projection")]
    private static void StorageTraceValues()
    {
        var input=StorageRows();var value=NativeAotSqliteContract.InspectStorageTraceValues(input,"/app/data/contract.db");input.Clear();
        HostCheck.True(value.Source=="trace.17" && value.UnjoinedLines==1 && !value.GrantsPayloadByteOrCompleteTraceOrDurabilityAuthority &&
            value.Lifetimes.Count==4 && value.Lifetimes.All(l=>l.CloseLine.HasValue) &&
            value.Lifetimes.Where(l=>l.Descriptor==7).Select(l=>l.Kind).SequenceEqual(new[]{"directory","database"}) &&
            value.Lifetimes[0].OpenFlags.Contains("O_NOFOLLOW",StringComparison.Ordinal) && value.Lifetimes[0].Mode=="0644" && value.Lifetimes[1].Mode=="0600");
        var write=value.Events.Single(e=>e.Kind=="database" && e.Call.Syscall=="pwritev2");
        HostCheck.True(write.Offset==4096 && write.Count==4096 && write.Returned==4096 && write.Flags=="RWF_NOAPPEND" &&
            value.Events.Single(e=>e.Call.Syscall=="pread64").Returned==0);
        var busy=value.Events.Single(e=>e.Returned<0 && e.Lock is not null);
        HostCheck.True(busy.LifetimeId==4 && busy.Lock==new NativeAotSqliteContract.StorageLock("F_WRLCK","SEEK_SET",1073741825,1) &&
            busy.Call.Result.StartsWith("-1 EAGAIN",StringComparison.Ordinal));
        var order=value.Journals.Single();
        HostCheck.True(order.DatabaseWrites==1 && order.CompleteSyncOrder && order.ClosedBeforeUnlink &&
            order.FirstJournalSync<order.ParentSync && order.ParentSync<order.HeaderWrite && order.HeaderWrite<order.LastJournalSync &&
            order.LastJournalSync<order.DatabaseSync && order.DatabaseSync<order.Unlink);
        bool immutable=false;try{((IList<NativeAotSqliteContract.StorageLifetime>)value.Lifetimes).Clear();}catch(NotSupportedException){immutable=true;}HostCheck.True(immutable);
    }

    [HostTest("sqlite copied storage trace preserves LP64 offsets and locks and rejects arithmetic overflow")]
    private static void StorageTraceLp64()
    {
        // Synthetic argument boundaries only; no measured engine usage or lock ownership.
        const long high=4294967301,wideLength=4294967296;
        string original=StorageRows()["trace.17"];
        const string write="}], 1, 4096, RWF_NOAPPEND) = 4096";
        const string highWrite="}], 1, 4294967301, RWF_NOAPPEND) = 4096";
        const string region="l_start=1073741825, l_len=1";
        const string wideRegion="l_start=4294967301, l_len=4294967296";
        string large=original.Replace(write,highWrite,StringComparison.Ordinal).Replace(region,wideRegion,StringComparison.Ordinal);
        var value=NativeAotSqliteContract.InspectStorageTraceValues(StorageRows(large),"/app/data/contract.db");
        var positioned=value.Events.Single(e=>e.Kind=="database" && e.Call.Syscall=="pwritev2");
        var locks=value.Events.Where(e=>e.Lock is not null).ToArray();
        HostCheck.True(positioned.Offset==high && positioned.Count==4096 && positioned.Returned==4096 &&
            positioned.Flags=="RWF_NOAPPEND" && locks.Length==2 &&
            locks.All(e=>e.Lock==new NativeAotSqliteContract.StorageLock("F_WRLCK","SEEK_SET",high,wideLength)) &&
            !value.GrantsPayloadByteOrCompleteTraceOrDurabilityAuthority);
        var zero=NativeAotSqliteContract.InspectStorageTraceValues(
            StorageRows(large.Replace(wideRegion,"l_start=4294967301, l_len=0",StringComparison.Ordinal)),"/app/data/contract.db");
        var toEnd=zero.Events.Where(e=>e.Lock is not null).ToArray();
        HostCheck.True(toEnd.Length==2 && toEnd.All(e=>e.Lock==new NativeAotSqliteContract.StorageLock("F_WRLCK","SEEK_SET",high,0)) &&
            !zero.GrantsPayloadByteOrCompleteTraceOrDurabilityAuthority);
        foreach(var control in new[]{
            (large.Replace(highWrite,"}], 1, 9223372036854775807, RWF_NOAPPEND) = 4096",StringComparison.Ordinal),"storage_io_result"),
            (large.Replace(wideRegion,"l_start=9223372036854775807, l_len=1",StringComparison.Ordinal),"storage_lock"),
            (large.Replace(highWrite,"}], 1, 9223372036854775808, RWF_NOAPPEND) = 4096",StringComparison.Ordinal),"storage_number")})
            Reject(()=>NativeAotSqliteContract.InspectStorageTraceValues(StorageRows(control.Item1),"/app/data/contract.db"),control.Item2);
    }

    private const string GetLockDatabase="/root/clros-sqlite-contract-recovery-native-r03/artifacts/sqlite-native-recovery-reference-r03/recovery.db";
    // Exact verify-trace.9365:269; only the surrounding open/close rows below
    // are synthetic. A returned F_UNLCK does not expose its original query.
    private const string GetLockRow="1792901404.391374 fcntl(5</root/clros-sqlite-contract-recovery-native-r03/artifacts/sqlite-native-recovery-reference-r03/recovery.db>, F_GETLK, {l_type=F_UNLCK, l_whence=SEEK_SET, l_start=1073741825, l_len=1, l_pid=1792901399}) = 0";
    private static Dictionary<string,string> GetLockRows(string? row=null)=>StorageRows(
        $"1792901404.390000 open(\"{GetLockDatabase}\", O_RDWR|O_CLOEXEC) = 5<{GetLockDatabase}>\n"+
        (row??GetLockRow)+$"\n1792901404.392000 close(5<{GetLockDatabase}>) = 0\n1792901404.393000 +++ exited with 0 +++\n");

    [HostTest("sqlite storage trace preserves measured F_GETLK returned fields separately from input locks and ownership")]
    private static void StorageTraceGetLockValues()
    {
        var input=GetLockRows();var trace=NativeAotSqliteContract.InspectTraceValues(input,GetLockDatabase);
        HostCheck.True(trace.Rows.Any(r=>r.Raw==GetLockRow) && !trace.GrantsCompleteTraceOrExecutionAuthority);
        var value=NativeAotSqliteContract.InspectStorageTraceValues(input,GetLockDatabase);input.Clear();
        var observed=value.Events.Single(e=>e.Flags=="F_GETLK");
        HostCheck.True(observed.Call.Syscall=="fcntl" && observed.Call.Result=="0" && observed.LifetimeId==1 && observed.Descriptor==5 &&
            observed.Returned==0 && observed.Count is null && observed.Offset is null && observed.Lock is null &&
            observed.ReturnedLock==new NativeAotSqliteContract.StorageReturnedLock("F_UNLCK","SEEK_SET",1073741825,1,1792901399) &&
            !value.GrantsPayloadByteOrCompleteTraceOrDurabilityAuthority);
        var zero=NativeAotSqliteContract.InspectStorageTraceValues(GetLockRows(GetLockRow.Replace("l_pid=1792901399","l_pid=0",StringComparison.Ordinal)),GetLockDatabase);
        HostCheck.Equal(0,zero.Events.Single(e=>e.Flags=="F_GETLK").ReturnedLock!.ReportedPid);
        var wide=NativeAotSqliteContract.InspectStorageTraceValues(GetLockRows(GetLockRow.Replace("l_start=1073741825, l_len=1","l_start=4294967301, l_len=4294967296",StringComparison.Ordinal)),GetLockDatabase);
        HostCheck.True(wide.Events.Single(e=>e.Flags=="F_GETLK").ReturnedLock==
            new NativeAotSqliteContract.StorageReturnedLock("F_UNLCK","SEEK_SET",4294967301,4294967296,1792901399));
        var legacy=NativeAotSqliteContract.InspectStorageTraceValues(StorageRows(),"/app/data/contract.db");
        HostCheck.True(legacy.Events.All(e=>e.ReturnedLock is null) && legacy.Events.Count(e=>e.Flags=="F_SETLK" && e.Lock is not null)==2);
    }

    [HostTest("sqlite storage F_GETLK rejects unmeasured malformed returned locks and LP64 or pid overflow")]
    private static void StorageTraceGetLockRejections()
    {
        foreach(var change in new[]{
            ("F_GETLK","F_OFD_GETLK","storage_fcntl"),
            ("l_type=F_UNLCK","l_type=F_WRLCK","storage_lock"),
            ("l_whence=SEEK_SET","l_whence=SEEK_CUR","storage_lock"),
            (", l_pid=1792901399","","storage_lock"),
            ("l_pid=1792901399","l_pid=1792901399, l_pid=0","storage_lock"),
            ("l_start=1073741825","l_start=-1","storage_lock"),
            ("l_start=1073741825","l_start=9223372036854775808","storage_number"),
            ("l_start=1073741825, l_len=1","l_start=9223372036854775807, l_len=1","storage_lock"),
            ("l_len=1","l_len=9223372036854775808","storage_number"),
            ("l_pid=1792901399","l_pid=2147483648","storage_lock"),
            ("l_pid=1792901399","l_pid=-2147483649","storage_lock"),
            (") = 0",") = -1 EIO (I/O error)","storage_fcntl")})
            Reject(()=>NativeAotSqliteContract.InspectStorageTraceValues(GetLockRows(GetLockRow.Replace(change.Item1,change.Item2,StringComparison.Ordinal)),GetLockDatabase),change.Item3);
    }

    [HostTest("sqlite storage F_GETLK joins exact live descriptor generations and rejects stale or mismatched annotations")]
    private static void StorageTraceGetLockLifetime()
    {
        string earlyClose=$"1792901404.391000 close(5<{GetLockDatabase}>) = 0\n";
        string reopen=$"1792901404.391100 open(\"{GetLockDatabase}\", O_RDWR|O_CLOEXEC) = 5<{GetLockDatabase}>\n";
        var reused=NativeAotSqliteContract.InspectStorageTraceValues(GetLockRows(earlyClose+reopen+GetLockRow),GetLockDatabase);
        HostCheck.True(reused.Lifetimes.Count==2 && reused.Lifetimes.All(l=>l.CloseLine.HasValue) &&
            reused.Events.Single(e=>e.Flags=="F_GETLK").LifetimeId==2 && !reused.GrantsPayloadByteOrCompleteTraceOrDurabilityAuthority);
        Reject(()=>NativeAotSqliteContract.InspectStorageTraceValues(GetLockRows(earlyClose+GetLockRow),GetLockDatabase),"storage_stale_descriptor");
        Reject(()=>NativeAotSqliteContract.InspectStorageTraceValues(GetLockRows(GetLockRow.Replace("fcntl(5<","fcntl(6<",StringComparison.Ordinal)),GetLockDatabase),"storage_stale_descriptor");
        Reject(()=>NativeAotSqliteContract.InspectStorageTraceValues(GetLockRows(GetLockRow.Replace(GetLockDatabase+">",GetLockDatabase+"-journal>",StringComparison.Ordinal)),GetLockDatabase),"storage_annotation");
    }

    [HostTest("sqlite storage trace rejects stale descriptor attribution ambiguous owners and malformed measured operations")]
    private static void StorageTraceRejections()
    {
        string original=StorageRows()["trace.17"];
        foreach(var change in new[]{
            ("1.000022 close(5</app/data/contract.db>) = 0", "1.000022 close(5</app/data/contract.db>) = 0\n1.000023 fsync(5</app/data/contract.db>) = 0", "stale_descriptor"),
            ("5</app/data/contract.db>, F_SETFD", "5</app/data/contract.db-journal>, F_SETFD", "annotation"),
            ("1.000012 close(7</app/data>) = 0", "", "descriptor_reused"),
            ("}], 1, 4096, RWF_NOAPPEND", "}, {iov_base=\"extra\", iov_len=1}], 2, 4096, RWF_NOAPPEND", "iovec"),
            ("4096, RWF_NOAPPEND) = 4096", "4096, RWF_NOAPPEND) = 4097", "io_result"),
            ("4096, RWF_NOAPPEND) = 4096", "-1, RWF_NOAPPEND) = 4096", "number"),
            ("l_whence=SEEK_SET", "l_whence=SEEK_CUR", "lock"),
            ("1.000016 fsync(5</app/data/contract.db>) = 0", "1.000016 ioctl(5</app/data/contract.db>, 0, 0) = 0", "operation_unsupported"),
            ("1.000016 fsync(5</app/data/contract.db>) = 0", "1.000016 mmap(NULL, 4096, PROT_READ, MAP_PRIVATE, 5</app/data/contract.db>, 0) = 0x1234", "path_operation"),
            ("1.000022 close(5</app/data/contract.db>) = 0", "", "lifetime_unclosed")})
            Reject(()=>NativeAotSqliteContract.InspectStorageTraceValues(StorageRows(original.Replace(change.Item1,change.Item2,StringComparison.Ordinal)),"/app/data/contract.db"),"storage_"+change.Item3);
        var ambiguous=StorageRows();ambiguous["trace.18"]+="1.000026 lstat(\"/app/data/contract.db\", 0x1234) = 0\n";
        Reject(()=>NativeAotSqliteContract.InspectStorageTraceValues(ambiguous,"/app/data/contract.db"),"storage_single_owner");
        var unfinished=StorageRows();unfinished["trace.17"]+="1.000026 read(3<pipe:[7]> <unfinished ...>) = ?\n";
        Reject(()=>NativeAotSqliteContract.InspectStorageTraceValues(unfinished,"/app/data/contract.db"),"storage_owner_unjoined");
        Reject(()=>NativeAotSqliteContract.InspectStorageTraceValues(StorageRows(),"/app/../contract.db"),"storage_path");
    }

    [HostTest("sqlite storage trace distinguishes ordered commit sync from failed missing reordered and rollback journal observations")]
    private static void StorageTraceOrdering()
    {
        string original=StorageRows()["trace.17"];
        foreach(var change in new[]{
            ("1.000011 fsync(7</app/data>) = 0",""),
            ("1.000011 fsync(7</app/data>) = 0","1.000011 fsync(7</app/data>) = -1 EIO (I/O error)"),
            ("1.000014 fsync(6</app/data/contract.db-journal>) = 0","1.000014 fdatasync(6</app/data/contract.db-journal>) = 0"),
            ("1.000015 pwritev2(5</app/data/contract.db>, [{iov_base=\"page\"..., iov_len=4096}], 1, 4096, RWF_NOAPPEND) = 4096",""),
            ("1.000016 fsync(5</app/data/contract.db>) = 0","1.000016 fsync(5</app/data/contract.db>) = -1 EIO (I/O error)"),
            ("1.000017 close(6</app/data/contract.db-journal>) = 0\n1.000018 unlink(\"/app/data/contract.db-journal\") = 0",
             "1.000017 unlink(\"/app/data/contract.db-journal\") = 0\n1.000018 close(6</app/data/contract.db-journal>) = 0")})
        {
            var value=NativeAotSqliteContract.InspectStorageTraceValues(StorageRows(original.Replace(change.Item1,change.Item2,StringComparison.Ordinal)),"/app/data/contract.db");
            HostCheck.True(!value.Journals.Single().CompleteSyncOrder);
        }
        // A later journal generation must not borrow a prior transaction's
        // flushes or database writes, even when the numeric fd is reused.
        string suffix="""
        1.000025 open("/app/data/contract.db-journal", O_RDWR|O_CREAT, 0600) = 6</app/data/contract.db-journal>
        1.000026 pwritev2(6</app/data/contract.db-journal>, [{iov_base="rollback", iov_len=512}], 1, 0, RWF_NOAPPEND) = 512
        1.000027 close(6</app/data/contract.db-journal>) = 0
        1.000028 unlink("/app/data/contract.db-journal") = 0
        """;
        var separate=NativeAotSqliteContract.InspectStorageTraceValues(StorageRows(original.Replace("1.000024 +++ exited with 0 +++","",StringComparison.Ordinal)+"\n"+suffix),"/app/data/contract.db");
        HostCheck.True(separate.Journals.Count==2 && separate.Journals[0].CompleteSyncOrder && separate.Journals[1].DatabaseWrites==0 &&
            separate.Journals[1].ClosedBeforeUnlink && !separate.Journals[1].CompleteSyncOrder && separate.Journals[1].ParentSync is null);
    }

    [HostTest("sqlite contract owned managed fixture creates commits rolls back reopens and preserves exact inputs",HostLane.Native)]
    private static async Task ManagedReference()
    {
        string root=HostToolProcess.FindRepositoryRoot(),parent=Path.Combine(root,"artifacts/sqlite-contract-host-tests");Directory.CreateDirectory(parent);
        string output=Path.Combine(parent,Guid.NewGuid().ToString("N"));
        var reference=await NativeAotSqliteContract.RunManagedReferenceAsync(root,output);
        HostCheck.True(!reference.GrantsLinuxNativeAotGuestOrDurabilityAuthority && reference.Values.Engine=="3.53.4" &&
            reference.Verification==reference.Values &&
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(output,"contract.db")))).Equals(reference.DatabaseSha256,StringComparison.OrdinalIgnoreCase) &&
            File.ReadAllBytes(Path.Combine(output,"contract.db")).AsSpan(0,16).SequenceEqual("SQLite format 3\0"u8));
        HostCheck.True(File.Exists(Path.Combine(output,"assets.json")) && File.Exists(Path.Combine(output,"inputs.json")) &&
            File.Exists(Path.Combine(output,"verify.stdout.log")) && !File.Exists(Path.Combine(output,"contract.db-journal")) && !File.Exists(Path.Combine(output,"contract.db-wal")));
        NativeAotSqliteContract.InspectVerificationTranscriptValues(File.ReadAllText(Path.Combine(output,"verify.stdout.log")));
        var receipt=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"receipt.json")))!;
        HostCheck.True(receipt["Schema"]!.GetValue<string>()=="clros-sqlite-managed-reference-v3" && receipt["Writer"]!["ExitCode"]!.GetValue<int>()==0 &&
            receipt["Verifier"]!["ExitCode"]!.GetValue<int>()==0 && receipt["Writer"]!["TerminationConfirmed"]!.GetValue<bool>() && receipt["Verifier"]!["TerminationConfirmed"]!.GetValue<bool>());
        byte[] originalDatabase=File.ReadAllBytes(Path.Combine(output,"contract.db"));
        string negatives=Path.Combine(parent,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(negatives);
        string app=Path.Combine(root,"apps/NativeAotSqliteContract/bin/Debug/net11.0/NativeAotSqliteContract.dll");
        foreach(string control in new[]{"missing","changed","sidecar"})
        {
            string target=Path.Combine(negatives,control+".db");
            if(control!="missing")
            {
                byte[] bytes=originalDatabase.ToArray();
                if(control=="changed")
                {
                    byte[] text=Encoding.UTF8.GetBytes("CLROS SQLite — café");int at=bytes.AsSpan().IndexOf(text);
                    HostCheck.True(at>=0 && bytes.AsSpan(at+text.Length).IndexOf(text)<0);bytes[at]=(byte)'Z';
                }
                await File.WriteAllBytesAsync(target,bytes);
                if(control=="sidecar")await File.WriteAllBytesAsync(target+"-journal",new byte[]{1});
            }
            var rejectedProcess=await ProcessRunner.RunAsync(new("dotnet",new[]{app,"--verify-database",target},root)
                {Timeout=TimeSpan.FromSeconds(30),OutputCharacterLimit=65536});
            await File.WriteAllTextAsync(Path.Combine(negatives,control+".stdout.log"),rejectedProcess.StandardOutput);
            await File.WriteAllTextAsync(Path.Combine(negatives,control+".stderr.log"),rejectedProcess.StandardError);
            QemuTranscripts.RequireCompleteProcess(rejectedProcess,"sqlite_verify_negative_incomplete");
            HostCheck.True(rejectedProcess.ExitCode==1 && rejectedProcess.StandardError.Length==0 && rejectedProcess.StandardOutput.Contains("TEST_FAIL m10.020.verify",StringComparison.Ordinal) &&
                !rejectedProcess.StandardOutput.Contains("TEST_PASS m10.020.verify",StringComparison.Ordinal));
            if(control=="missing")HostCheck.True(!File.Exists(target));
        }
        HostCheck.True(File.ReadAllBytes(Path.Combine(output,"contract.db")).SequenceEqual(originalDatabase));
        bool rejected=false;try{await NativeAotSqliteContract.RunManagedReferenceAsync(root,output);}catch(ToolFailure failure){rejected=failure.Reason=="sqlite_contract_fresh_output_required";}
        HostCheck.True(rejected);Console.WriteLine("SQLITE_MANAGED_REFERENCE "+output);
    }

    [HostTest("sqlite recovery journal bytes require original checksummed pages and an actual protected database change")]
    private static void RecoveryJournalValues()
    {
        byte[] baseline=new byte[8192];"SQLite format 3\0"u8.CopyTo(baseline);BinaryPrimitives.WriteUInt16BigEndian(baseline.AsSpan(16),4096);
        baseline.AsSpan(4096).Fill(0x41);byte[] changed=baseline.ToArray();changed.AsSpan(4096).Fill(0x5a);
        byte[] journal=new byte[512+4104];new byte[]{0xd9,0xd5,5,0xf9,0x20,0xa1,0x63,0xd7}.CopyTo(journal,0);
        foreach(var pair in new[]{(8,1u),(12,0x10203040u),(16,2u),(20,512u),(24,4096u),(512,2u),(512+4100,0x10203554u)})
            BinaryPrimitives.WriteUInt32BigEndian(journal.AsSpan(pair.Item1),pair.Item2);
        baseline.AsSpan(4096).CopyTo(journal.AsSpan(516));var value=NativeAotSqliteContract.InspectRecoveryJournalValues(baseline,changed,journal);
        HostCheck.True(value.FirstSegmentRecords==1 && value.ChangedProtectedPages==1 && value.OriginalPages==2 && value.SectorBytes==512 &&
            value.BaselineSha256==Convert.ToHexString(SHA256.HashData(baseline)) && value.JournalSha256==Convert.ToHexString(SHA256.HashData(journal)) &&
            !value.GrantsLockFlushHotJournalOrDurabilityAuthority);
        foreach(string mode in new[]{"zero","count","unbounded","truncated","page","checksum","baseline","unchanged"})
        {
            byte[] candidate=journal.ToArray(),after=changed.ToArray();string reason;
            switch(mode)
            {
                case "zero":candidate.AsSpan(0,8).Clear();reason="journal_header";break;
                case "count":candidate.AsSpan(8,4).Clear();reason="journal_segment";break;
                case "unbounded":candidate.AsSpan(8,4).Fill(255);reason="journal_segment";break;
                case "truncated":candidate=candidate[..^1];reason="journal_segment";break;
                case "page":BinaryPrimitives.WriteUInt32BigEndian(candidate.AsSpan(512),3);reason="journal_page";break;
                case "checksum":candidate[^1]^=1;reason="journal_checksum";break;
                case "baseline":candidate[516+100]^=1;reason="journal_baseline";break;
                default:after=baseline.ToArray();reason="spill_not_observed";break;
            }
            Reject(()=>NativeAotSqliteContract.InspectRecoveryJournalValues(baseline,after,candidate),"recovery_"+reason);
        }
    }

    private static (byte[] Baseline,byte[] Changed,byte[] Journal) JournalSegments()
    {
        byte[] baseline=new byte[5*4096];"SQLite format 3\0"u8.CopyTo(baseline);
        BinaryPrimitives.WriteUInt16BigEndian(baseline.AsSpan(16),4096);baseline[18]=baseline[19]=1;BinaryPrimitives.WriteUInt32BigEndian(baseline.AsSpan(28),5);
        for(int page=2;page<=5;page++)baseline.AsSpan((page-1)*4096,4096).Fill((byte)(0x3f+page));
        byte[] changed=baseline.ToArray();changed.AsSpan(4096,8192).Fill(0x5a);byte[] journal=new byte[10240+512+4104];
        uint[] nonces=[0x10203040,0x11223344,0x33445566];
        for(int part=0;part<3;part++)
        {
            int at=part*5120,record=at+512;uint page=(uint)(part+2),nonce=nonces[part];
            if(part<2)new byte[]{0xd9,0xd5,5,0xf9,0x20,0xa1,0x63,0xd7}.CopyTo(journal,at);
            foreach(var field in new[]{(8,part<2?1u:0u),(12,nonce),(16,5u),(20,512u),(24,4096u)})
                BinaryPrimitives.WriteUInt32BigEndian(journal.AsSpan(at+field.Item1),field.Item2);
            BinaryPrimitives.WriteUInt32BigEndian(journal.AsSpan(record),page);baseline.AsSpan(((int)page-1)*4096,4096).CopyTo(journal.AsSpan(record+4));
            // Each constant page has twenty sampled bytes; expected checksums
            // are independent of the consumer's page-walk implementation.
            BinaryPrimitives.WriteUInt32BigEndian(journal.AsSpan(record+4100),nonce+20*(0x3f+page));
        }
        return(baseline,changed,journal);
    }

    [HostTest("sqlite copied rollback segments walk aligned headers and separately retain the unsynced zero header tail shape")]
    private static void RollbackSegmentValues()
    {
        var input=JournalSegments();var value=NativeAotSqliteContract.InspectRollbackJournalSegmentValues(input.Baseline,input.Changed,input.Journal);
        HostCheck.True(value.OriginalPages==5 && value.SectorBytes==512 && value.ChangedDatabasePages==2 && value.ValidSegments.Count==2 &&
            value.ValidSegments.Select(s=>s.HeaderOffset).SequenceEqual(new[]{0,5120}) && value.ValidSegments.SelectMany(s=>s.Pages).Select(p=>p.Number).SequenceEqual(new uint[]{2,3}) &&
            value.ValidSegments.SelectMany(s=>s.Pages).All(p=>p.DiffersInDatabase) && value.UncommittedTail is{HeaderOffset:10240} &&
            value.UncommittedTail.Pages.Single() is{Number:4,DiffersInDatabase:false} && !value.GrantsOriginalCapturePlaybackFlushOrDurabilityAuthority &&
            value.JournalSha256==Convert.ToHexString(SHA256.HashData(input.Journal)));
        string original=value.ValidSegments[1].Pages.Single().OriginalSha256;input.Journal.AsSpan().Clear();
        HostCheck.True(value.ValidSegments[1].Pages.Single().OriginalSha256==original);
        bool immutable=false;try{((IList<NativeAotSqliteContract.RollbackPage>)value.UncommittedTail!.Pages).Clear();}catch(NotSupportedException){immutable=true;}HostCheck.True(immutable);
    }

    [HostTest("sqlite copied rollback segments reject partial inconsistent checksummed conflicting and duplicate page structures")]
    private static void RollbackSegmentRejections()
    {
        var input=JournalSegments();
        foreach(string mode in new[]{"tail_record","record_count","second_header","sector","original_pages","page_size","magic","tail_count", "count_zero",
            "no_sync","checksum","original_page","nonce","duplicate_conflict","duplicate_same","gap","tail_header","tail_extra","tail_checksum","tail_identity"})
        {
            byte[] journal=input.Journal.ToArray();string reason;
            void Put(int at,uint value)=>BinaryPrimitives.WriteUInt32BigEndian(journal.AsSpan(at),value);
            switch(mode)
            {
                case "tail_record":journal=journal[..^1];reason="tail_truncated";break;
                case "record_count":Put(8,4);reason="records_truncated";break;
                case "second_header":journal=journal[..(5120+27)];reason="header_truncated";break;
                case "sector":Put(5120+20,1024);reason="header_consistency";break;
                case "original_pages":Put(5120+16,6);reason="header_consistency";break;
                case "page_size":Put(5120+24,2048);reason="header_consistency";break;
                case "magic":journal[5120]^=1;reason="header_magic";break;
                case "tail_count":Put(10240+8,1);reason="header_magic";break;
                case "count_zero":Put(8,0);reason="record_count";break;
                case "no_sync":Put(8,uint.MaxValue);reason="record_count";break;
                case "checksum":journal[5120+512+4100]^=1;reason="checksum";break;
                case "original_page":journal[5120+512+4+100]^=1;reason="original_page";break;
                case "nonce":journal[5120+12]^=1;reason="checksum";break;
                case "duplicate_conflict":Put(5120+512,2);reason="duplicate_conflict";break;
                case "duplicate_same":Put(5120+512,2);input.Baseline.AsSpan(4096,4096).CopyTo(journal.AsSpan(5120+512+4));Put(5120+512+4100,0x11223858);reason="duplicate_page";break;
                case "gap":journal[512+4104]=1;reason="alignment_padding";break;
                case "tail_header":journal=journal[..(10240+28)];reason="header_truncated";break;
                case "tail_extra":journal=[..journal,1];reason="tail_truncated";break;
                case "tail_checksum":journal[^1]^=1;reason="checksum";break;
                default:journal[10240+512+4+100]^=1;reason="original_page";break;
            }
            Reject(()=>NativeAotSqliteContract.InspectRollbackJournalSegmentValues(input.Baseline,input.Changed,journal),"segments_"+reason);
        }
    }

    [HostTest("sqlite copied rollback segments expose valid shorter prefixes without claiming capture identity or flush history")]
    private static void RollbackSegmentBoundaryValues()
    {
        var input=JournalSegments();var whole=NativeAotSqliteContract.InspectRollbackJournalSegmentValues(input.Baseline,input.Changed,input.Journal);
        var prefix=NativeAotSqliteContract.InspectRollbackJournalSegmentValues(input.Baseline,input.Changed,input.Journal.AsSpan(0,512+4104));
        HostCheck.True(prefix.ValidSegments.Count==1 && prefix.UncommittedTail is null && prefix.JournalSha256!=whole.JournalSha256 &&
            prefix.ChangedDatabasePages==2 && prefix.ValidSegments.SelectMany(s=>s.Pages).Count(p=>p.DiffersInDatabase)==1 &&
            !prefix.GrantsOriginalCapturePlaybackFlushOrDurabilityAuthority);
    }

    [HostTest("sqlite managed process kill leaves original hot journal and independently reopens to committed bytes",HostLane.Native)]
    private static async Task ManagedRecoveryReference()
    {
        string root=HostToolProcess.FindRepositoryRoot(),parent=Path.Combine(root,"artifacts/sqlite-recovery-host-tests");Directory.CreateDirectory(parent);
        string output=Path.Combine(parent,Guid.NewGuid().ToString("N"));var value=await NativeAotSqliteContract.RunManagedRecoveryReferenceAsync(root,output);
        HostCheck.True(!value.GrantsLinuxNativeAotGuestOrPowerLossDurabilityAuthority && value.Writer.Cancelled && value.Writer.TerminationConfirmed &&
            !value.Writer.Succeeded && value.Journal.ChangedProtectedPages>0 && value.RecoveredDatabaseSha256==value.Journal.BaselineSha256);
        byte[] baseline=File.ReadAllBytes(Path.Combine(output,"baseline.db")),before=File.ReadAllBytes(Path.Combine(output,"before-reopen.db")),
            journal=File.ReadAllBytes(Path.Combine(output,"before-reopen.journal"));
        HostCheck.True(!baseline.AsSpan().SequenceEqual(before) && baseline.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(output,"recovery.db"))) &&
            NativeAotSqliteContract.InspectRecoveryJournalValues(baseline,before,journal)==value.Journal && !File.Exists(Path.Combine(output,"recovery.db-journal")));
        var receipt=JsonNode.Parse(File.ReadAllBytes(Path.Combine(output,"receipt.json")))!;
        HostCheck.True(receipt["Succeeded"]!.GetValue<bool>() && !receipt["SqliteFlushCallsObserved"]!.GetValue<bool>() &&
            !receipt["HostAssistedFlush"]!.GetValue<bool>() && receipt["RequiresDistinctLinuxPublication"]!.GetValue<bool>() &&
            !receipt["PowerLossQualified"]!.GetValue<bool>() && !receipt["GuestQualified"]!.GetValue<bool>());
        Console.WriteLine("SQLITE_MANAGED_RECOVERY "+output);
    }

    [HostTest("sqlite Linux recovery argv keeps direct modes and distinct bounded Linux paths without execution")]
    private static void LinuxRecoveryPlan()
    {
        var plan=NativeAotSqliteContract.LinuxRecoveryTraceArguments("/fresh/sqlite", "/fresh/recovery.db","spill","/fresh/spill-trace");
        HostCheck.True(plan[^3]=="/fresh/sqlite" && plan[^2]=="--recovery-spill" && plan[^1]=="/fresh/recovery.db" &&
            plan[Array.IndexOf(plan.ToArray(),"-o")+1]=="/fresh/spill-trace" && !plan.Any(p=>p is "sh" or "-c" or "--database"));
        foreach(string mode in new[]{"seed","verify"})HostCheck.Equal("--recovery-"+mode,NativeAotSqliteContract.LinuxRecoveryTraceArguments("/fresh/sqlite","/fresh/db",mode,"/fresh/trace")[^2]);
        foreach(string path in new[]{"relative","/fresh/../old","/fresh//db","/fresh/db\n","/fresh/sqlite"})
            Reject(()=>NativeAotSqliteContract.LinuxRecoveryTraceArguments("/fresh/sqlite",path,"spill","/fresh/trace"),"recovery_linux_path");
        Reject(()=>NativeAotSqliteContract.LinuxRecoveryTraceArguments("/fresh/sqlite","/fresh/db","ordinary","/fresh/trace"),"recovery_mode");
        bool immutable=false;try{((IList<string>)plan).Clear();}catch(NotSupportedException){immutable=true;}HostCheck.True(immutable);
        if(!OperatingSystem.IsLinux())Reject(()=>NativeAotSqliteContract.RunLinuxRecoveryReferenceAsync("missing-root","missing-recipe","missing-source","missing-output",TextWriter.Null).GetAwaiter().GetResult(),"linux_recovery_platform");
    }

    [HostTest("sqlite Linux recovery copied proc selectors require the sole live traced child and exact start identities")]
    private static void LinuxRecoveryIdentity()
    {
        string Stat(int pid,int parent,int group,int session,ulong start,string state="S")
        {
            string[] fields=Enumerable.Repeat("0",24).ToArray();fields[0]=state;fields[1]=parent.ToString(CultureInfo.InvariantCulture);
            fields[2]=group.ToString(CultureInfo.InvariantCulture);fields[3]=session.ToString(CultureInfo.InvariantCulture);fields[19]=start.ToString(CultureInfo.InvariantCulture);
            return pid.ToString(CultureInfo.InvariantCulture)+" (SQLite (main)) "+string.Join(' ',fields);
        }
        string tracer=Stat(100,1,100,100,10),writer=Stat(201,100,100,100,20),status="Name:\tsqlite\nTgid:\t201\nTracerPid:\t100\n";
        var value=NativeAotSqliteContract.InspectLinuxRecoveryIdentityValues(100,tracer,"201\n",writer,status,"/fresh/sqlite","/fresh/sqlite");
        HostCheck.True(value.Tracer==100 && value.Writer==201 && value.TracerStart==10 && value.WriterStart==20 && !value.GrantsProcessOrSignalAuthority);
        HostCheck.True(!NativeAotSqliteContract.InspectRecoveryPidfdPollValues(0,0) && NativeAotSqliteContract.InspectRecoveryPidfdPollValues(1,1) &&
            NativeAotSqliteContract.InspectRecoveryPidfdPollValues(1,16) && NativeAotSqliteContract.InspectRecoveryPidfdPollValues(1,17));
        Reject(()=>NativeAotSqliteContract.InspectRecoveryPidfdPollValues(1,32),"recovery_pidfd_poll");
        Reject(()=>NativeAotSqliteContract.InspectRecoveryPidfdPollValues(0,1),"recovery_pidfd_poll");
        foreach(string mode in new[]{"children","parent","session","dead","tracer","executable","start","duplicated"})
        {
            string child=mode=="children"?"201 202":"201",actual=mode=="executable"?"/old/sqlite":"/fresh/sqlite";
            string supplied=mode switch{"parent"=>Stat(201,101,100,100,20),"session"=>Stat(201,100,100,99,20),"dead"=>Stat(201,100,100,100,20,"Z"),"start"=>Stat(201,100,100,100,0),_=>writer};
            string suppliedStatus=mode=="tracer"?status.Replace("TracerPid:\t100","TracerPid:\t0",StringComparison.Ordinal):mode=="duplicated"?status+"Tgid:\t201\n":status;
            Reject(()=>NativeAotSqliteContract.InspectLinuxRecoveryIdentityValues(100,tracer,child,supplied,suppliedStatus,actual,"/fresh/sqlite"),"recovery_proc_");
        }
    }

    [HostTest("sqlite killed trace preserves live descriptors and runtime waits without inventing closes or per page flush proof")]
    private static void LinuxRecoveryKilledTrace()
    {
        string text=RecoverySpillRows();
        var value=NativeAotSqliteContract.InspectKilledRecoveryTraceValues(StorageRows(text),"/app/data/contract.db");
        HostCheck.True(value.DatabaseWrites==1 && value.WritesAfterJournalAndParentSync==1 && value.JournalSyncs==2 && value.ParentSyncs==1 &&
            value.Storage.Lifetimes.Count(l=>l.CloseLine is null)==2 && value.Storage.UnjoinedLines==2 &&
            value.Storage.Lifetimes.Single(l=>l.Id==value.ObserverLifetimeId).Descriptor==9 && value.JournalBytes==8192 &&
            value.Storage.Lifetimes.Single(l=>l.Id==value.ObserverLifetimeId).CloseLine<value.SpillLine &&
            value.Storage.Journals.All(j=>!j.CompleteSyncOrder) && !value.GrantsOriginalTraceOrDurabilityAuthority);
        var flocks=value.Storage.Events.Where(e=>e.Call.Syscall=="flock").ToArray();
        HostCheck.True(flocks.Select(e=>e.Flags).SequenceEqual(new[]{"LOCK_SH|LOCK_NB","LOCK_UN"}) && flocks.All(e=>e.Lock is null) &&
            value.Storage.Events.Any(e=>e.Call.Syscall=="fcntl" && e.Lock is not null));
        Reject(()=>NativeAotSqliteContract.InspectStorageTraceValues(StorageRows(text),"/app/data/contract.db"),"storage_owner_unjoined");
        foreach(string missing in new[]{"1.000011 fsync(7</app/data>) = 0","1.000014 fsync(6</app/data/contract.db-journal>) = 0"})
            Reject(()=>NativeAotSqliteContract.InspectKilledRecoveryTraceValues(StorageRows(text.Replace(missing,"",StringComparison.Ordinal)),"/app/data/contract.db"),"recovery_trace_sync_order");
        Reject(()=>NativeAotSqliteContract.InspectKilledRecoveryTraceValues(StorageRows(text.Replace("futex(0x1234, FUTEX_WAIT_PRIVATE, 2, NULL", "pread64(5</app/data/contract.db>, 0x1234, 4096, 0",StringComparison.Ordinal)),"/app/data/contract.db"),"storage_owner_unjoined");
    }

    private static string RecoverySpillRows()
    {
        string prefix=StorageRows()["trace.17"].Split("1.000017",StringSplitOptions.None)[0];
        // A synced previous segment may be followed by an unsynced empty header.
        prefix=prefix.Replace("1.000015 pwritev2", "1.0000145 pwritev2(6</app/data/contract.db-journal>, [{iov_base=\"empty next header\", iov_len=512}], 1, 5120, RWF_NOAPPEND) = 512\n1.000015 pwritev2",StringComparison.Ordinal);
        byte[] header=new byte[28];new byte[]{0xd9,0xd5,5,0xf9,0x20,0xa1,0x63,0xd7}.CopyTo(header,0);header[11]=1;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16),2);BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(20),512);BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(24),4096);
        string quoted=string.Concat(header.Select(b=>"\\"+Convert.ToString(b,8).PadLeft(3,'0')));
        string marker="SQLITE_RECOVERY_SPILL rows=64 journalBytes=8192 headerBase64="+Convert.ToBase64String(header);
        return prefix+"\n"+$$"""
        1.000017 open("/app/data/contract.db-journal", O_RDONLY|O_NOCTTY|O_LARGEFILE|O_CLOEXEC) = 9</app/data/contract.db-journal>
        1.000018 fcntl(9</app/data/contract.db-journal>, F_SETFD, FD_CLOEXEC) = 0
        1.000019 fstat(9</app/data/contract.db-journal>, {st_mode=S_IFREG|0600, st_size=8192, ...}) = 0
        1.000020 flock(9</app/data/contract.db-journal>, LOCK_SH|LOCK_NB) = 0
        1.000021 pread64(9</app/data/contract.db-journal>, "{{quoted}}"..., 4096, 0) = 4096
        1.000022 fstat(9</app/data/contract.db-journal>, {st_mode=S_IFREG|0600, st_size=8192, ...}) = 0
        1.000023 flock(9</app/data/contract.db-journal>, LOCK_UN) = 0
        1.000024 close(9</app/data/contract.db-journal>) = 0
        1.000025 write(1<pipe:[42]>, "{{marker}}\n", {{marker.Length+1}}) = {{marker.Length+1}}
        1.000026 futex(0x1234, FUTEX_WAIT_PRIVATE, 2, NULL <unfinished ...>) = ?
        1.000027 +++ killed by SIGKILL +++
        """+"\n";
    }

    [HostTest("sqlite recovery observer metadata is scoped independently of fd number ordering and historical journal opens")]
    private static void RecoveryObserverMetadataAndHistory()
    {
        string text=RecoverySpillRows();
        // The constructor's earlier metadata is sufficient; do not require
        // FileStream.Length to issue a second fstat after its buffered read.
        text=text.Replace("1.000022 fstat(9</app/data/contract.db-journal>, {st_mode=S_IFREG|0600, st_size=8192, ...}) = 0\n","",StringComparison.Ordinal);
        text="0.999997 open(\"/app/data/contract.db-journal\", O_RDONLY|O_LARGEFILE) = 12</app/data/contract.db-journal>\n"+
            "0.999998 close(12</app/data/contract.db-journal>) = 0\n"+text.Replace("9</app/data/contract.db-journal>","23</app/data/contract.db-journal>",StringComparison.Ordinal);
        text=text.Replace("\\040"," ",StringComparison.Ordinal).Replace("\\143","c",StringComparison.Ordinal);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\\0([0-7]{2})",m=>"\\"+Convert.ToString(Convert.ToInt32(m.Groups[1].Value,8),8));
        var value=NativeAotSqliteContract.InspectKilledRecoveryTraceValues(StorageRows(text),"/app/data/contract.db");
        HostCheck.True(value.Storage.Lifetimes.Count==5 && value.Storage.Lifetimes[0].Descriptor==12 && value.Storage.Lifetimes[0].CloseLine is not null &&
            value.Storage.Lifetimes.Single(l=>l.Id==value.ObserverLifetimeId).Descriptor==23 && value.JournalSyncs==2 && value.ParentSyncs==1);
    }

    [HostTest("sqlite recovery rejects the original still live journal observer late close and reused stale or foreign observer lifetime")]
    private static void RecoveryObserverLifetimeRejections()
    {
        string text=RecoverySpillRows(),unlock="1.000023 flock(9</app/data/contract.db-journal>, LOCK_UN) = 0\n",close="1.000024 close(9</app/data/contract.db-journal>) = 0\n";
        foreach(var change in new[]{
            (unlock+close,"","recovery_trace_live_files"),
            (close,"","recovery_trace_live_files"),
            (close,"1.000024 open(\"/app/data/contract.db-journal\", O_RDONLY|O_NOCTTY|O_LARGEFILE|O_CLOEXEC) = 9</app/data/contract.db-journal>\n","storage_descriptor_reused"),
            ("1.000023 flock",close+"1.000023 flock","storage_stale_descriptor"),
            ("1.000026 futex",close+"1.000026 futex","storage_stale_descriptor"),
            ("O_RDONLY|O_NOCTTY|O_LARGEFILE|O_CLOEXEC","O_RDONLY|O_CREAT|O_NOCTTY|O_LARGEFILE|O_CLOEXEC","recovery_trace_observer_lifetime"),
            ("O_RDONLY|O_NOCTTY|O_LARGEFILE|O_CLOEXEC","O_RDONLY|O_TRUNC|O_LARGEFILE|O_CLOEXEC","recovery_trace_observer_lifetime"),
            ("O_RDWR|O_CREAT|O_LARGEFILE|O_NOFOLLOW|O_CLOEXEC","O_RDONLY|O_CREAT|O_LARGEFILE|O_NOFOLLOW|O_CLOEXEC","recovery_trace_live_flags")})
            Reject(()=>NativeAotSqliteContract.InspectKilledRecoveryTraceValues(StorageRows(text.Replace(change.Item1,change.Item2,StringComparison.Ordinal)),"/app/data/contract.db"),change.Item3);
        string late=text.Replace(close,"",StringComparison.Ordinal).Replace("1.000026 futex",close+"1.000026 futex",StringComparison.Ordinal);
        Reject(()=>NativeAotSqliteContract.InspectKilledRecoveryTraceValues(StorageRows(late),"/app/data/contract.db"),"recovery_trace_observer_lifetime");
        string outside=text.Replace("9</app/data/contract.db-journal>","9</app/data/wrong-journal>",StringComparison.Ordinal)
            .Replace("open(\"/app/data/contract.db-journal\", O_RDONLY","open(\"/app/data/wrong-journal\", O_RDONLY",StringComparison.Ordinal);
        Reject(()=>NativeAotSqliteContract.InspectKilledRecoveryTraceValues(StorageRows(outside),"/app/data/contract.db"),"recovery_trace_observer");
    }

    [HostTest("sqlite recovery observer accepts only successful matched shared flock header read metadata and unlock without writer sync credit")]
    private static void RecoveryObserverOperationRejections()
    {
        string text=RecoverySpillRows();
        foreach(var change in new[]{
            ("LOCK_SH|LOCK_NB","LOCK_EX|LOCK_NB","storage_flock"),
            ("LOCK_SH|LOCK_NB) = 0","LOCK_SH|LOCK_NB) = -1 EWOULDBLOCK (Resource temporarily unavailable)","storage_flock"),
            ("LOCK_UN) = 0","LOCK_UN) = -1 EIO (I/O error)","storage_flock"),
            ("1.000023 flock(9</app/data/contract.db-journal>, LOCK_UN) = 0\n","","recovery_trace_observer_lock_read"),
            ("1.000022 fstat","1.000022 flock(9</app/data/contract.db-journal>, LOCK_SH|LOCK_NB) = 0\n1.000022 fstat","recovery_trace_observer_lock_read"),
            ("1.000021 pread64","1.000021 flock(9</app/data/contract.db-journal>, LOCK_UN) = 0\n1.000021 pread64","recovery_trace_observer_lock_read"),
            ("1.000022 fstat","1.000022 fsync(9</app/data/contract.db-journal>) = 0\n1.000022 fstat","recovery_trace_observer_operation"),
            ("1.000022 fstat","1.000022 pwrite64(9</app/data/contract.db-journal>, \"evil\", 4, 0) = 4\n1.000022 fstat","recovery_trace_observer_operation"),
            ("1.000022 fstat","1.000022 fcntl(9</app/data/contract.db-journal>, F_SETLK, {l_type=F_WRLCK, l_whence=SEEK_SET, l_start=0, l_len=1}) = 0\n1.000022 fstat","recovery_trace_observer_operation"),
            ("1.000022 fstat","1.000022 dup(9</app/data/contract.db-journal>) = 10</app/data/contract.db-journal>\n1.000022 fstat","storage_operation_unsupported_dup"),
            ("..., 4096, 0) = 4096","..., 8192, 0) = 4096","recovery_trace_observer_lock_read"),
            ("..., 4096, 0) = 4096","..., 4096, 0) = 27","recovery_trace_observer_lock_read"),
            ("..., 4096, 0) = 4096","..., 4096, 1) = 4096","recovery_trace_observer_lock_read"),
            ("st_size=8192","st_size=8193","recovery_trace_observer_metadata"),
            ("\\331\\325","\\330\\325","recovery_trace_observer_header"),
            ("\\331\\325","\\xD9\\325","recovery_trace_observer_header"),
            ("1.000004 fcntl(5</app/data/contract.db>, F_SETLK, {l_type=F_WRLCK, l_whence=SEEK_SET, l_start=1073741825, l_len=1}) = 0\n","","recovery_trace_reserved_lock")})
            Reject(()=>NativeAotSqliteContract.InspectKilledRecoveryTraceValues(StorageRows(text.Replace(change.Item1,change.Item2,StringComparison.Ordinal)),"/app/data/contract.db"),change.Item3);
        string noSync=text.Replace("1.000014 fsync(6</app/data/contract.db-journal>) = 0\n","",StringComparison.Ordinal);
        Reject(()=>NativeAotSqliteContract.InspectKilledRecoveryTraceValues(StorageRows(noSync),"/app/data/contract.db"),"recovery_trace_sync_order");
    }

    [HostTest("sqlite recovery spill barrier requires one complete same writer stdout write with matching header length and causal position")]
    private static void RecoveryObserverSpillRejections()
    {
        string text=RecoverySpillRows();string spill=text.Split('\n').Single(l=>l.Contains(" write(1<pipe",StringComparison.Ordinal))+"\n";
        foreach(string changed in new[]{text.Replace(spill,"",StringComparison.Ordinal),text+spill,
            text.Replace("write(1<pipe","write(2<pipe",StringComparison.Ordinal),text.Replace(") = 102\n",") = 101\n",StringComparison.Ordinal),
            text.Replace("rows=64 journalBytes","rows=63 journalBytes",StringComparison.Ordinal),
            text.Replace("journalBytes=8192 headerBase64","journalBytes=9999999 headerBase64",StringComparison.Ordinal)})
            Reject(()=>NativeAotSqliteContract.InspectKilledRecoveryTraceValues(StorageRows(changed),"/app/data/contract.db"),"recovery_trace_spill");
        var foreign=StorageRows(text.Replace(spill,"",StringComparison.Ordinal));foreign["trace.18"]+=spill;
        Reject(()=>NativeAotSqliteContract.InspectKilledRecoveryTraceValues(foreign,"/app/data/contract.db"),"recovery_trace_spill_marker");
        string before=text.Replace(spill,"",StringComparison.Ordinal).Replace("1.000015 pwritev2",spill+"1.000015 pwritev2",StringComparison.Ordinal);
        Reject(()=>NativeAotSqliteContract.InspectKilledRecoveryTraceValues(StorageRows(before),"/app/data/contract.db"),"recovery_trace_spill_marker");
    }

    [HostTest("sqlite contract source and provider admission reject altered inputs before native compilation")]
    private static void UpstreamAdmission()
    {
        Reject(()=>NativeAotSqliteContract.InspectSourceArchiveValues(new byte[2946650]),"source_archive_hash");
        Reject(()=>NativeAotSqliteContract.InspectSourceArchiveValues("untrusted upstream replacement"u8),"source_archive_hash");
        Reject(()=>NativeAotSqliteContract.InspectProviderImports("shared library is not a signed provider package"u8),"provider_package_hash");
        var archive=NativeAotSqliteContract.InspectStaticArchiveValues(ArchiveVector());
        NativeAotSqliteContract.RequireProviderSymbols(archive,Array.AsReadOnly(new[]{"sqlite3_open_v2","sqlite3_step"}));
        Reject(()=>NativeAotSqliteContract.RequireProviderSymbols(archive,Array.AsReadOnly(new[]{"sqlite3_unlock_notify"})),"provider_symbols_missing");
    }

    [HostTest("sqlite contract static recipe preserves original musl policy and enables only measured optional exports")]
    private static void StaticRecipe()
    {
        string directory=Path.GetFullPath(Path.Combine("artifacts","sqlite-recipe-values")),source=Path.Combine(directory,"sqlite3.c"),
            obj=Path.Combine(directory,"sqlite3.o"),archive=Path.Combine(directory,"libe_sqlite3.a");
        var imports=Array.AsReadOnly(new[]{"sqlite3_open_v2","sqlite3_column_table_name","sqlite3_preupdate_hook"});
        var compile=NativeAotSqliteContract.StaticCompileArguments(source,obj,imports);
        HostCheck.True(compile.Take(4).SequenceEqual(new[]{"-c",source,"-o",obj}) && compile.Contains("-march=x86-64-v2") &&
            compile.Contains("-DSQLITE_THREADSAFE=1") && compile.Contains("-DSQLITE_ENABLE_COLUMN_METADATA=1") &&
            compile.Contains("-DSQLITE_ENABLE_PREUPDATE_HOOK=1") && !compile.Any(a=>a.Contains("SNAPSHOT",StringComparison.Ordinal) || a.Contains("OMIT",StringComparison.Ordinal)));
        string[] old=NativeAotThreadingPayload.PublishArguments("NativeAotSuspensionSmoke",directory).ToArray();
        old[1]="apps/NativeAotSocketSmoke/NativeAotSocketSmoke.csproj";
        string recipe=System.Text.Json.JsonSerializer.Serialize(new[]{new{executable="dotnet",arguments=old}});
        var publish=NativeAotSqliteContract.StaticPublishArguments(recipe,directory,archive);
        HostCheck.True(publish[1]=="apps/NativeAotSqliteContract/NativeAotSqliteContract.csproj" &&
            publish.Contains("-p:StaticExecutable=true") && publish.Contains("-p:PositionIndependentExecutable=false") &&
            publish.Contains("-p:SqliteNativeArchive="+archive) && publish.Contains("--no-restore") &&
            publish.Where(a=>a.StartsWith("-p:RuntimeFrameworkVersion=",StringComparison.Ordinal)).Single()=="-p:RuntimeFrameworkVersion="+NativeAotPayload.RuntimeVersion);
        Reject(()=>NativeAotSqliteContract.StaticPublishArguments(recipe,directory,Path.Combine(directory,"libe_sqlite3.so")),"publish_archive");
        bool rejected=false;
        try{NativeAotSqliteContract.StaticPublishArguments(recipe.Replace("StaticExecutable=true","StaticExecutable=false",StringComparison.Ordinal),directory,archive);}
        catch(ToolFailure failure){rejected=failure.Reason.Contains("recipe_changed",StringComparison.Ordinal);}
        HostCheck.True(rejected);
    }

    [HostTest("sqlite contract distinguishes provider metadata from actual retained object imports")]
    private static void RetainedImports()
    {
        string[] metadata=["sqlite3_open_v2","sqlite3_close_v2","sqlite3_prepare_v2","sqlite3_step","sqlite3_key"];
        const string actual="                 U sqlite3_open_v2\n                 U sqlite3_close_v2\n                 U sqlite3_prepare_v2\n                 U sqlite3_step\n                 U malloc\n";
        var result=NativeAotSqliteContract.InspectRetainedImportValues(actual,metadata);
        HostCheck.True(result.Count==4 && !result.Contains("sqlite3_key"));
        Reject(()=>NativeAotSqliteContract.InspectRetainedImportValues(actual+" U sqlite3_not_in_provider\n",metadata),"retained_import_unknown");
        Reject(()=>NativeAotSqliteContract.InspectRetainedImportValues(actual.Replace(" U sqlite3_step"," U malloc",StringComparison.Ordinal),metadata),"retained_import_missing");
        var weak=NativeAotSqliteContract.InspectFinalUndefinedValues("__gmon_start__ w\n_ITM_registerTMCloneTable w\n");
        HostCheck.True(weak.Count==2 && weak.All(w=>w.Binding=='w') && NativeAotSqliteContract.InspectFinalUndefinedValues("").Count==0);
        Reject(()=>NativeAotSqliteContract.InspectFinalUndefinedValues("sqlite3_key w\n"),"final_undefined_sqlite");
        Reject(()=>NativeAotSqliteContract.InspectFinalUndefinedValues("malloc U\n"),"final_undefined_strong");
    }

    [HostTest("sqlite instruction values bind executable bytes in a separate domain without granting prepared program authority")]
    private static void InstructionValues()
    {
        byte[] elf=LinuxThreadProofFixture.CreateElf("Threads");
        new byte[]{0xb8,0x0f,0x05,0,0,0x0f,0x05,0xc3}.CopyTo(elf,512);
        const string dump="400200: b8 0f 05 00 00\tmovl $0x50f, %eax\n400205: 0f 05\tsyscall\n400207: c3\tretq\n";
        var actual=NativeAotSqliteContract.InspectInstructionValues(elf,dump);
        using MemoryStream bytes=new();bytes.Write(Encoding.ASCII.GetBytes("CLROS_SQLITE_INSTRUCTIONS_V1\n"));
        using(var writer=new BinaryWriter(bytes,Encoding.ASCII,true))
        {
            writer.Write(0x400200UL);writer.Write((byte)5);writer.Write(new byte[]{0xb8,0x0f,0x05,0,0});writer.Write((byte)0);
            writer.Write(0x400205UL);writer.Write((byte)2);writer.Write(new byte[]{0x0f,0x05});writer.Write((byte)1);
            writer.Write(0x400207UL);writer.Write((byte)1);writer.Write((byte)0xc3);writer.Write((byte)0);
        }
        HostCheck.True(actual.InstructionCount==3 && actual.SyscallCount==1 && actual.SyscallContinuationsValid &&
            actual.Sha256==Convert.ToHexString(SHA256.HashData(bytes.ToArray())) && !actual.GrantsPreparedProgramOrExecutionAuthority &&
            actual.Sha256!=NativeAotHelloProgram.Inspect(elf,dump).Sha256);
        var partial=NativeAotSqliteContract.InspectInstructionValues(elf,"400205: 0f 05\tsyscall\n");
        HostCheck.True(partial.InstructionCount==1 && !partial.SyscallContinuationsValid && partial.Sha256!=actual.Sha256 && !partial.GrantsPreparedProgramOrExecutionAuthority);
        bool rejected=false;try{NativeAotSqliteContract.InspectInstructionValues(elf,dump.Replace("0f 05\tsyscall","90 90\tnop",StringComparison.Ordinal));}
        catch(InvalidDataException error){rejected=error.Message=="sqlite_instruction_backing";}HostCheck.True(rejected);
    }
}
