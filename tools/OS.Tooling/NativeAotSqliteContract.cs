using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;

/// <summary>Direct Microsoft.Data.Sqlite candidate and host requirement values.
/// No EF Core, native publication, guest storage or durability is admitted here.
/// Linux static linkage and original traced NativeAOT execution remain separate.</summary>
public static class NativeAotSqliteContract
{
    public const string Application="NativeAotSqliteContract",ProviderVersion="11.0.0-rc.2.26504.105",RawVersion="3.0.5",EngineVersion="3.53.4";
    public const string NativePackageSha256="5641EB58E88429FD73D09FC294BDAA610A6E99099F4141DE912A5E4AA6D773CA";
    public static IReadOnlyDictionary<string,string> PackagePins {get;}=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.Data.Sqlite"]=ProviderVersion,["Microsoft.Data.Sqlite.Core"]=ProviderVersion,
        ["SQLitePCLRaw.bundle_e_sqlite3"]=RawVersion,["SQLitePCLRaw.config.e_sqlite3"]=RawVersion,
        ["SQLitePCLRaw.provider.e_sqlite3"]=RawVersion,["SQLitePCLRaw.core"]=RawVersion,["SQLite"]=EngineVersion
    });
    // Exact signed archives independently fetched from NuGet's HTTPS flat
    // container. NuGet's project.assets sha512 is a different content identity;
    // it must not be compared with SHA512 over the complete signed ZIP bytes.
    public static IReadOnlyDictionary<string,string> PackageArchiveSha256 {get;}=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.Data.Sqlite"]="E735E6AB8D9C520DA4A5F2352C9217D99D7B47919BC13C58D7EDB13B22416118",
        ["Microsoft.Data.Sqlite.Core"]="38522A58D3BF13A5901577D1D1A730322AC12192F28F825921FD7397F875AA34",
        ["SQLitePCLRaw.bundle_e_sqlite3"]="E325CE61A86BCA381FBD35487264481E1C6A39663546FEC82035FA661198C2A1",
        ["SQLitePCLRaw.config.e_sqlite3"]="1C3908111A4A14E898839836BEA9C4DA390EDC669B297C404DE92422ABEE3AB1",
        ["SQLitePCLRaw.provider.e_sqlite3"]="10C6D3DAAC432BB382DDA06C35E5F7FFCCE3618792141BD91656527F06EF5B25",
        ["SQLitePCLRaw.core"]="7476B4B8E067D88C4CB598A31893B511037B88E05C28D524F4B4433FE9F69048",
        ["SQLite"]=NativePackageSha256
    });
    public sealed record Package(string Name,string Version,string Sha512);
    public sealed record Dependencies(IReadOnlyList<Package> Packages,IReadOnlyList<string> MuslNativeAssets)
    {
        public bool HasStaticArchive=>MuslNativeAssets.Any(p=>p.EndsWith(".a",StringComparison.Ordinal));
        public bool GrantsNativeLinkageOrGuestAuthority=>false;
    }
    public sealed record Archive(string Sha256,int Bytes,int ObjectFiles,IReadOnlyList<string> DefinedSqliteSymbols)
    {public bool GrantsCompleteNativeLinkageAuthority=>false;}
    public sealed record Transcript(string Engine,string SourceSha256,string TextSha256,string BlobSha256)
    {public bool GrantsNativeAotGuestOrDurabilityAuthority=>false;}
    public sealed record TraceCall(string Source,int Line,string Syscall,string Arguments,string Result,string Successor,bool MentionsDatabase);
    public sealed record Trace(IReadOnlyList<TraceCall> Calls,int UnjoinedLines,IReadOnlyList<string> Successors)
    {public bool GrantsCompleteTraceOrExecutionAuthority=>false;}
    public sealed class ManagedReference
    {
        internal ManagedReference(string directory,Transcript transcript,string databaseHash){Directory=directory;Values=transcript;DatabaseSha256=databaseHash;}
        public string Directory {get;}
        public Transcript Values {get;}
        public string DatabaseSha256 {get;}
        public bool GrantsLinuxNativeAotGuestOrDurabilityAuthority=>false;
    }

    /// <summary>Borrow bounded project.assets.json values and require the exact
    /// seven direct/provider/native package pins. Reject EF or unrecognized
    /// SQLite packages anywhere in the closure. Return copied musl native paths;
    /// paths and package hashes are observations, not held-file authority.</summary>
    public static Dependencies InspectDependencyValues(ReadOnlySpan<byte> assets)
    {
        Need(assets.Length is >0 and <=32*1024*1024,"assets_size");
        using var json=JsonDocument.Parse(assets.ToArray(),new(){MaxDepth=32});var root=json.RootElement;
        List<Package> packages=[];HashSet<string> seen=new(StringComparer.OrdinalIgnoreCase);SortedSet<string> native=new(StringComparer.Ordinal);
        int libraries=0;
        foreach(var property in root.GetProperty("libraries").EnumerateObject())
        {
            Need(++libraries<=1024,"library_count");string[] identity=property.Name.Split('/');Need(identity.Length==2,"package_identity");
            string name=identity[0];Need(!name.Contains("EntityFramework",StringComparison.OrdinalIgnoreCase),"ef_core_forbidden");
            bool sqlite=name.Contains("sqlite",StringComparison.OrdinalIgnoreCase);
            if(!sqlite)continue;
            Need(PackagePins.TryGetValue(name,out string? version) && identity[1]==version && seen.Add(name) &&
                property.Value.GetProperty("type").GetString()=="package","package_pin");
            string hash=property.Value.GetProperty("sha512").GetString()??"";
            Need(Convert.TryFromBase64String(hash,new byte[64],out int written) && written==64,"package_hash");
            packages.Add(new(name,identity[1],hash));
        }
        Need(seen.Count==PackagePins.Count,"package_missing");
        foreach(var target in root.GetProperty("targets").EnumerateObject())
            foreach(var package in target.Value.EnumerateObject())
                foreach(string section in new[]{"native","runtimeTargets"})
                    if(package.Value.TryGetProperty(section,out var values))
                        foreach(var file in values.EnumerateObject())
                        {
                            string path=file.Name;
                            Need(path.Length is >0 and <=512 && !path.StartsWith('/') && !path.Contains('\\') &&
                                path.Split('/').All(s=>s.Length>0 && s is not ("." or "..")),"native_path");
                            if(path.StartsWith("runtimes/linux-musl-x64/native/",StringComparison.Ordinal))
                            {Need(native.Count<64,"native_count");native.Add(path);}
                        }
        Need(native.Contains("runtimes/linux-musl-x64/native/libe_sqlite3.so"),"musl_native_missing");
        return new(Array.AsReadOnly(packages.OrderBy(p=>p.Name,StringComparer.Ordinal).ToArray()),Array.AsReadOnly(native.ToArray()));
    }

    /// <summary>Borrow an explicit ordinary ar archive. Every object must be
    /// ELF64 little-endian x86-64 ET_REL with bounded valid symbol/string tables.
    /// Require defined global SQLite entry points, rejecting shared libraries,
    /// thin archives and incomplete substitutes. This structural check cannot
    /// prove complete provider imports, libc closure, compiler provenance or a
    /// linked static executable; original link/nm/readelf evidence is required.</summary>
    public static Archive InspectStaticArchiveValues(ReadOnlySpan<byte> bytes)
    {
        Need(bytes.Length is >=68 and <=64*1024*1024 && bytes[..8].SequenceEqual("!<arch>\n"u8),"static_archive_required");
        int at=8,objects=0,members=0;SortedSet<string> symbols=new(StringComparer.Ordinal);
        while(at<bytes.Length)
        {
            Need(++members<=2048 && bytes.Length-at>=60 && bytes.Slice(at+58,2).SequenceEqual("`\n"u8),"archive_header");
            string name=Encoding.ASCII.GetString(bytes.Slice(at,16)).Trim();
            Need(int.TryParse(Encoding.ASCII.GetString(bytes.Slice(at+48,10)).Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out int size) &&
                size>=0 && size<=bytes.Length-at-60,"archive_member");
            var member=bytes.Slice(at+60,size);
            if(name is not ("/" or "//" or "/SYM64/"))
            {Need(++objects<=1024,"archive_objects");ReadSymbols(member,symbols);}
            at=checked(at+60+size);if((size&1)!=0){Need(at<bytes.Length && bytes[at]=='\n',"archive_padding");at++;}
        }
        string[] required=["sqlite3_open_v2","sqlite3_close_v2","sqlite3_prepare_v2","sqlite3_step","sqlite3_finalize",
            "sqlite3_bind_text","sqlite3_bind_blob","sqlite3_libversion","sqlite3_sourceid"];
        Need(at==bytes.Length && objects>0 && required.All(symbols.Contains),"archive_symbols");
        return new(Convert.ToHexString(SHA256.HashData(bytes)),bytes.Length,objects,Array.AsReadOnly(symbols.ToArray()));
    }

    private static void ReadSymbols(ReadOnlySpan<byte> elf,SortedSet<string> symbols)
    {
        Need(elf.Length>=64 && elf[..7].SequenceEqual(new byte[]{0x7f,69,76,70,2,1,1}) && U16(elf,16)==1 && U16(elf,18)==62 &&
            U16(elf,52)==64 && U16(elf,58)==64,"archive_elf");
        ulong sectionAt=U64(elf,40);uint count=U16(elf,60);Need(count is >0 and <=4096 && sectionAt<=(ulong)elf.Length &&
            count*64UL<=(ulong)elf.Length-sectionAt,"archive_sections");
        for(uint i=0;i<count;i++)
        {
            int header=checked((int)(sectionAt+i*64UL));if(U32(elf,header+4)!=2)continue;
            ulong offset=U64(elf,header+24),size=U64(elf,header+32);uint link=U32(elf,header+40);
            Need(link<count && U64(elf,header+56)==24 && size%24==0 && size/24<=100000 &&
                offset<=(ulong)elf.Length && size<=(ulong)elf.Length-offset,"archive_symtab");
            int strings=checked((int)(sectionAt+link*64UL));ulong textOffset=U64(elf,strings+24),textBytes=U64(elf,strings+32);
            Need(U32(elf,strings+4)==3 && textOffset<=(ulong)elf.Length && textBytes<=(ulong)elf.Length-textOffset,"archive_strings");
            for(ulong symbol=0;symbol<size;symbol+=24)
            {
                int row=checked((int)(offset+symbol));uint nameAt=U32(elf,row);byte info=elf[row+4];
                Need(nameAt<textBytes,"archive_symbol_name");
                if((info>>4) is not (1 or 2) || (info&15)!=2 || U16(elf,row+6)==0)continue;
                var rest=elf.Slice(checked((int)(textOffset+nameAt)),checked((int)(textBytes-nameAt)));int end=rest.IndexOf((byte)0);
                Need(end is >=0 and <=255,"archive_symbol_text");
                string name=Encoding.ASCII.GetString(rest[..end]);
                if(name.StartsWith("sqlite3_",StringComparison.Ordinal))
                {
                    uint defined=U16(elf,row+6);Need(defined<count,"archive_symbol_section");int body=checked((int)(sectionAt+defined*64UL));
                    ulong value=U64(elf,row+8),length=U64(elf,row+16),sectionBytes=U64(elf,body+32),bodyOffset=U64(elf,body+24);
                    Need(U32(elf,body+4)==1 && (U64(elf,body+8)&4)!=0 && value<sectionBytes && length<=sectionBytes-value &&
                        bodyOffset<=(ulong)elf.Length && sectionBytes<=(ulong)elf.Length-bodyOffset,"archive_symbol_body");
                    Need(symbols.Count<4096,"archive_symbol_count");symbols.Add(name);
                }
            }
        }
    }

    /// <summary>Validate the complete finite fixture protocol, parameterized
    /// UTF8/blob identities, commit/rollback/reopen, SQLITE_BUSY/READONLY/CANTOPEN
    /// controls and closed journal state. Supplied text is value-only evidence.</summary>
    public static Transcript InspectTranscriptValues(string output)
    {
        Need(output.Length is >0 and <=65536,"transcript_size");string[] lines=output.Replace("\r\n","\n").TrimEnd('\n').Split('\n');
        Need(lines.Length==10 && lines[0]=="TEST_START m10.020" && lines[9]=="TEST_PASS m10.020","transcript_inventory");
        const string prefix="SQLITE_ENGINE version=3.53.4 sourceSha256=";
        Need(lines[1].StartsWith(prefix,StringComparison.Ordinal) && Hex(lines[1][prefix.Length..]),"transcript_engine");
        string text=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("CLROS SQLite — café"))),
            blob=Convert.ToHexString(SHA256.HashData(Enumerable.Range(0,34003).Select(i=>(byte)(i^(i>>9))).ToArray()));
        string[] expected=["SQLITE_POLICY journal=delete synchronous=2 locking=normal pageSize=4096",
            "SQLITE_COMMIT rows=1 textSha256="+text+" blobSha256="+blob+" journal=true","SQLITE_ROLLBACK rows=1 journal=true",
            "SQLITE_LOCK busy=5 rows=1","SQLITE_REOPEN rows=1 integrity=ok","SQLITE_FAILURE readonly=8 missingParent=14",
            "SQLITE_CLEANUP journal=false wal=false shm=false"];
        Need(lines.Skip(2).Take(7).SequenceEqual(expected),"transcript_contract");return new(EngineVersion,lines[1][prefix.Length..],text,blob);
    }

    /// <summary>Copy bounded complete strace -ff/-ttt rows, retaining original
    /// argument/result text and mapping observed storage operations to existing
    /// successor keys. Unfinished/resumed rows are counted, never invented into
    /// completed calls. A path mention is lexical, not descriptor/VFS causality.
    /// This does not qualify a trace, native process, syscall closure or durability.</summary>
    public static Trace InspectTraceValues(IReadOnlyDictionary<string,string> files,string databasePath)
    {
        Need(files is {Count:>0 and <=64} && databasePath.Length is >0 and <=255 && databasePath.StartsWith('/') &&
            !databasePath.Any(c=>c is '"' or '\n' or '\r' or '\0'),"trace_input");
        List<TraceCall> calls=[];int unjoined=0,total=0;
        foreach(var file in files.OrderBy(f=>f.Key,StringComparer.Ordinal))
        {
            Need(file.Key.Length is >0 and <=255 && file.Value.Length<=4*1024*1024 &&
                (total+=file.Value.Length)<=16*1024*1024,"trace_capacity");int lineNumber=0;
            foreach(string line in file.Value.Replace("\r\n","\n").Split('\n'))
            {
                lineNumber++;Need(lineNumber<=200000 && line.Length<=8192,"trace_line");if(line.Length==0)continue;
                if(line.Contains("<unfinished ...>",StringComparison.Ordinal) || line.Contains(" resumed>",StringComparison.Ordinal)){unjoined++;continue;}
                if(Regex.IsMatch(line,@"^\d+\.\d+ (?:\+\+\+|---)",RegexOptions.CultureInvariant))continue;
                var match=Regex.Match(line,@"^\d+\.\d+ ([a-zA-Z0-9_]+)\((.*)\) += (.+)$",RegexOptions.CultureInvariant);
                Need(match.Success && calls.Count<200000,"trace_call");string syscall=match.Groups[1].Value;
                string successor=syscall switch
                {
                    "fsync" or "fdatasync" or "syncfs"=>"M06-020/#798",
                    "truncate" or "ftruncate" or "unlink" or "unlinkat" or "rename" or "renameat" or "renameat2" or "mkdir" or "mkdirat"=>"M06-021/#799",
                    "open" or "openat" or "close" or "stat" or "fstat" or "newfstatat" or "statx" or "access" or "faccessat" or
                    "read" or "pread64" or "write" or "pwrite64" or "lseek" or "fcntl" or "flock"=>"M16-027/#800",
                    _=>""
                };
                string arguments=match.Groups[2].Value;
                calls.Add(new(file.Key,lineNumber,syscall,arguments,match.Groups[3].Value,successor,arguments.Contains(databasePath,StringComparison.Ordinal)));
            }
        }
        Need(calls.Count>0,"trace_empty");return new(Array.AsReadOnly(calls.ToArray()),unjoined,
            Array.AsReadOnly(calls.Where(c=>c.Successor.Length!=0).Select(c=>c.Successor).Distinct().Order(StringComparer.Ordinal).ToArray()));
    }

    /// <summary>Own a fresh artifacts directory and serialized managed restore,
    /// nonincremental build and execution. Hold original fixture/config inputs
    /// throughout, then hold exact assets and built outputs through joined child
    /// exit. Preserve all logs/failures and database bytes. This Windows job-owned
    /// host milestone is separate from Linux NativeAOT publication/tracing and
    /// CLROS file/lock/flush/transaction qualification.</summary>
    public static async Task<ManagedReference> RunManagedReferenceAsync(string root,string destination,CancellationToken cancellation=default)
    {
        Need(OperatingSystem.IsWindows(),"managed_owned_windows_required");root=Path.GetFullPath(root);destination=Path.GetFullPath(destination,root);
        Need(destination.StartsWith(Path.Combine(root,"artifacts")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) &&
            !Path.Exists(destination),"fresh_output_required");
        AuthorityFiles.RequirePlainPath(root,Path.GetDirectoryName(destination)!,"sqlite_output_parent");
        using var lease=NativeAotPayload.OpenQualificationLease(root);using AuthorityFiles held=new();
        string app=Path.Combine(root,"apps",Application),project=Path.Combine(app,Application+".csproj");
        var sources=new[]{project,Path.Combine(app,"Program.cs"),Path.Combine(root,"global.json"),Path.Combine(root,"Directory.Build.props"),Path.Combine(root,"Directory.Build.targets")}
            .Select(path=>held.Open(root,path,"sqlite source","sqlite_source",1024*1024,singleLink:true)).ToArray();
        Need(ToolingBuildInputs.Validate(root).Count==0,"tooling_inputs_changed");Directory.CreateDirectory(destination);
        AuthorityFiles.RequirePlainPath(root,destination,"sqlite_output_path");
        using var ownership=new FileStream(Path.Combine(destination,"ownership.lock"),FileMode.CreateNew,FileAccess.Write,FileShare.None);
        bool succeeded=false;string failure="";var commands=new List<object>();Transcript? transcript=null;string databaseHash="";
        async Task<ProcessResult> Run(string name,string[] arguments)
        {
            commands.Add(new{executable="dotnet",arguments,workingDirectory=root});
            var result=await ProcessRunner.RunAsync(new("dotnet",arguments,root){Timeout=TimeSpan.FromMinutes(5),OutputCharacterLimit=4*1024*1024},cancellation);
            await File.WriteAllTextAsync(Path.Combine(destination,name+".stdout.log"),result.StandardOutput,CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(destination,name+".stderr.log"),result.StandardError,CancellationToken.None);
            QemuTranscripts.RequireCompleteProcess(result,"sqlite_"+name+"_incomplete");Need(result.Succeeded,"process_"+name);return result;
        }
        try
        {
            var sdk=await Run("sdk",["--version"]);Need(sdk.StandardOutput.Trim()==NativeAotPayload.SdkVersion,"sdk_pin");
            await Run("restore",["restore",project,"--nologo"]);
            await Run("build",["build",project,"--no-restore","--no-incremental","--nologo","-m:1","-p:UseSharedCompilation=false"]);
            var assets=held.Open(root,Path.Combine(app,"obj/project.assets.json"),"sqlite assets","sqlite_assets",32*1024*1024,singleLink:true);
            byte[] assetBytes=assets.ReadBytes(32*1024*1024,"sqlite_assets");var dependencies=InspectDependencyValues(assetBytes);
            await File.WriteAllBytesAsync(Path.Combine(destination,"assets.json"),assetBytes,CancellationToken.None);
            List<AuthorityFile> archives=[];
            using(var parsed=JsonDocument.Parse(assetBytes))
                foreach(var pin in PackagePins)
                {
                    string relative=pin.Key.ToLowerInvariant()+"/"+pin.Value+"/"+pin.Key.ToLowerInvariant()+"."+pin.Value+".nupkg";
                    string? path=parsed.RootElement.GetProperty("packageFolders").EnumerateObject().Select(f=>Path.Combine(f.Name,relative)).FirstOrDefault(File.Exists);
                    Need(path is not null,"package_archive_missing");var archive=held.Open(Path.GetPathRoot(path!)!,path!,"sqlite signed package","sqlite_package",64*1024*1024,singleLink:true);
                    Need(archive.Sha256.Equals(PackageArchiveSha256[pin.Key],StringComparison.OrdinalIgnoreCase),"package_archive_hash");archives.Add(archive);
                }
            string bin=Path.Combine(app,"bin/Debug/net11.0");var built=System.IO.Directory.GetFiles(bin,"*",SearchOption.AllDirectories);
            Need(built.Length is >0 and <=256,"built_count");
            var outputs=built.Select(path=>held.Open(root,path,"sqlite built output","sqlite_built",64*1024*1024,allowEmpty:true,singleLink:true)).ToArray();
            var result=await Run("fixture",[Path.Combine(bin,Application+".dll"),"--database",Path.Combine(destination,"contract.db")]);
            Need(result.StandardError.Length==0,"fixture_stderr");transcript=InspectTranscriptValues(result.StandardOutput);
            var database=held.Open(root,Path.Combine(destination,"contract.db"),"sqlite database","sqlite_database",1024*1024,singleLink:true);
            databaseHash=database.Sha256;held.VerifyUnchanged();Need(ToolingBuildInputs.Validate(root).Count==0,"tooling_inputs_changed");
            await File.WriteAllTextAsync(Path.Combine(destination,"dependencies.json"),JsonSerializer.Serialize(dependencies),CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(destination,"inputs.json"),JsonSerializer.Serialize(sources.Concat(outputs).Append(assets)
                .ToDictionary(f=>Path.GetRelativePath(root,f.Path).Replace('\\','/'),f=>f.Sha256)),CancellationToken.None);
            foreach(var input in sources.Concat(outputs).Append(assets))
            {
                string relative=Path.GetRelativePath(root,input.Path),copy=Path.Combine(destination,"originals",relative);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);File.Copy(input.Path,copy);
                Need(NativeAotWebPayload.HashFile(copy).Equals(input.Sha256,StringComparison.OrdinalIgnoreCase),"retained_input_hash");
            }
            foreach(var archive in archives)
            {
                string copy=Path.Combine(destination,"packages",Path.GetFileName(archive.Path));Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(archive.Path,copy);Need(NativeAotWebPayload.HashFile(copy).Equals(archive.Sha256,StringComparison.OrdinalIgnoreCase),"retained_package_hash");
            }
            held.VerifyUnchanged();
            succeeded=true;
        }
        catch(Exception error){failure=error is ToolFailure tool?tool.Reason:error.GetType().Name;throw;}
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(destination,"commands.json"),JsonSerializer.Serialize(commands),CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(destination,"receipt.json"),JsonSerializer.Serialize(new
            {Schema="clros-sqlite-managed-reference-v1",Succeeded=succeeded,Failure=failure,Transcript=transcript,DatabaseSha256=databaseHash,
                LinuxNativeAotQualified=false,GuestQualified=false,DurabilityQualified=false}),CancellationToken.None);
        }
        return new(destination,transcript!,databaseHash);
    }

    private static bool Hex(string value)=>value.Length==64 && value.All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static ushort U16(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
    private static uint U32(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static ulong U64(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt64LittleEndian(bytes[at..]);
    private static void Need(bool condition,string reason){if(!condition)throw new ToolFailure("sqlite_contract_"+reason);}
}
