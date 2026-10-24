using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static class NativeAotSqliteContractTests
{
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

    [HostTest("sqlite contract owned managed fixture creates commits rolls back reopens and preserves exact inputs",HostLane.Native)]
    private static async Task ManagedReference()
    {
        string root=HostToolProcess.FindRepositoryRoot(),parent=Path.Combine(root,"artifacts/sqlite-contract-host-tests");Directory.CreateDirectory(parent);
        string output=Path.Combine(parent,Guid.NewGuid().ToString("N"));
        var reference=await NativeAotSqliteContract.RunManagedReferenceAsync(root,output);
        HostCheck.True(!reference.GrantsLinuxNativeAotGuestOrDurabilityAuthority && reference.Values.Engine=="3.53.4" &&
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(output,"contract.db")))).Equals(reference.DatabaseSha256,StringComparison.OrdinalIgnoreCase) &&
            File.ReadAllBytes(Path.Combine(output,"contract.db")).AsSpan(0,16).SequenceEqual("SQLite format 3\0"u8));
        HostCheck.True(File.Exists(Path.Combine(output,"assets.json")) && File.Exists(Path.Combine(output,"inputs.json")) &&
            !File.Exists(Path.Combine(output,"contract.db-journal")) && !File.Exists(Path.Combine(output,"contract.db-wal")));
        bool rejected=false;try{await NativeAotSqliteContract.RunManagedReferenceAsync(root,output);}catch(ToolFailure failure){rejected=failure.Reason=="sqlite_contract_fresh_output_required";}
        HostCheck.True(rejected);Console.WriteLine("SQLITE_MANAGED_REFERENCE "+output);
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
}
