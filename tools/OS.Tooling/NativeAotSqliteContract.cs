using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.PortableExecutable;
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
    public const string SourceArchiveUrl="https://www.sqlite.org/2026/sqlite-amalgamation-3530400.zip";
    public const string SourceArchiveSha256="1E71DDF93849C6A6ECF58B827C0692073D2DD7EE40196158068F7B29F422E87D";
    public const string SourceArchiveSha3="628A44CFE82C66AED1CCBBE85A562D2E33EBE64B3288981ED76285612227934E";
    public const string SourceCsha256="B1DD5D74EC7F29055A6684FA06FB3C2F6821C87DD38F9A458DFD2E8A1DB28189";
    public const string SourceHeaderSha256="919E7F2E8ED1D8F56AC17B412B8971C76AA5D1A879752CC6058F75E7D5910E1D";
    public const string SourceId="2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc";
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
    public readonly record struct WeakUndefined(string Name,char Binding);
    public sealed class UpstreamSource
    {
        private readonly byte[] source,header;
        internal UpstreamSource(byte[] source,byte[] header){this.source=source;this.header=header;}
        public string ArchiveSha256=>SourceArchiveSha256;
        public string Csha256=>SourceCsha256;
        public string HeaderSha256=>SourceHeaderSha256;
        public string EngineSourceId=>SourceId;
        public byte[] CopyC()=>source.ToArray();
        public byte[] CopyHeader()=>header.ToArray();
        public bool GrantsNativeBuildOrExecutionAuthority=>false;
    }
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

    /// <summary>Borrow the exact official release ZIP. Require its independently
    /// downloaded SHA256 and SQLite-published SHA3-256, then exact C/header
    /// identities and upstream source ID. Copy only those two named bounded
    /// members; no archive pathname is used as a destination or executed.
    /// Callers may write copies only to ignored external-build artifacts.</summary>
    public static UpstreamSource InspectSourceArchiveValues(ReadOnlySpan<byte> bytes)
    {
        Need(bytes.Length==2946650 && Convert.ToHexString(SHA256.HashData(bytes))==SourceArchiveSha256,"source_archive_hash");
        Need(SHA3_256.IsSupported && Convert.ToHexString(SHA3_256.HashData(bytes))==SourceArchiveSha3,"source_archive_sha3");
        using var stream=new MemoryStream(bytes.ToArray(),false);using var archive=new ZipArchive(stream,ZipArchiveMode.Read);
        Need(archive.Entries.Count==5,"source_archive_inventory");
        byte[] Read(string name,int size,string expected)
        {
            var matches=archive.Entries.Where(e=>e.FullName=="sqlite-amalgamation-3530400/"+name).ToArray();
            Need(matches.Length==1 && matches[0].Length==size,"source_member");
            byte[] result=new byte[size];using var input=matches[0].Open();input.ReadExactly(result);
            Need(input.ReadByte()==-1 && Convert.ToHexString(SHA256.HashData(result))==expected,"source_member_hash");return result;
        }
        byte[] source=Read("sqlite3.c",9515341,SourceCsha256),header=Read("sqlite3.h",690838,SourceHeaderSha256);
        string text=Encoding.UTF8.GetString(header);Need(text.Contains("#define SQLITE_VERSION        \"3.53.4\"",StringComparison.Ordinal) &&
            text.Contains("#define SQLITE_SOURCE_ID      \""+SourceId+"\"",StringComparison.Ordinal),"source_version");return new(source,header);
    }

    /// <summary>Read P/Invoke metadata from the exact signed net8 provider ZIP,
    /// without loading or executing its assembly. Require all imported native
    /// modules to be e_sqlite3 and return copied distinct sqlite3 entry points.
    /// This measures the whole provider import surface, including methods the
    /// finite fixture may not invoke; linked code and symbol proof remain separate.</summary>
    public static IReadOnlyList<string> InspectProviderImports(ReadOnlySpan<byte> package)
    {
        Need(package.Length is >0 and <=1024*1024 && Convert.ToHexString(SHA256.HashData(package))==PackageArchiveSha256["SQLitePCLRaw.provider.e_sqlite3"],"provider_package_hash");
        using var zipStream=new MemoryStream(package.ToArray(),false);using var zip=new ZipArchive(zipStream,ZipArchiveMode.Read);
        var entries=zip.Entries.Where(e=>e.FullName=="lib/net8.0/SQLitePCLRaw.provider.e_sqlite3.dll").ToArray();
        Need(entries.Length==1 && entries[0].Length is >0 and <=1024*1024,"provider_assembly");
        using var stream=new MemoryStream();using(var input=entries[0].Open())input.CopyTo(stream);stream.Position=0;
        using var pe=new PEReader(stream);Need(pe.HasMetadata,"provider_metadata");var metadata=System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
        SortedSet<string> imports=new(StringComparer.Ordinal);
        foreach(var handle in metadata.MethodDefinitions)
        {
            var method=metadata.GetMethodDefinition(handle);if((method.Attributes&MethodAttributes.PinvokeImpl)==0)continue;
            var import=method.GetImport();string module=metadata.GetString(metadata.GetModuleReference(import.Module).Name),name=metadata.GetString(import.Name);
            Need(module=="e_sqlite3" && name.Length is >8 and <=128 && name.StartsWith("sqlite3",StringComparison.Ordinal) &&
                name.All(c=>char.IsAsciiLetterOrDigit(c) || c=='_') && imports.Count<512,"provider_import");imports.Add(name);
        }
        Need(imports.Contains("sqlite3_open_v2") && imports.Contains("sqlite3_step") && imports.Contains("sqlite3_close_v2"),"provider_import_missing");
        return Array.AsReadOnly(imports.ToArray());
    }

    /// <summary>Plan one deterministic musl/x86-64-v2 object build. Preserve
    /// serialized SQLite and default upstream Unix VFS. Enable optional exports
    /// only when required by the measured provider imports; never replace locks,
    /// sync or VFS behavior with host-only stubs. Output is a value plan.</summary>
    public static IReadOnlyList<string> StaticCompileArguments(string source,string output,IReadOnlyList<string> imports)
    {
        Need(Path.IsPathFullyQualified(source) && Path.IsPathFullyQualified(output) && source!=output && imports is {Count:>0 and <=512},"compile_input");
        List<string> args=["-c",source,"-o",output,"-O2","-fPIC","-ffunction-sections","-fdata-sections","-march=x86-64-v2","-mtune=generic","-DSQLITE_THREADSAFE=1"];
        void Feature(string define,params string[] names){if(names.Any(imports.Contains))args.Add("-D"+define+"=1");}
        Feature("SQLITE_ENABLE_COLUMN_METADATA","sqlite3_column_database_name","sqlite3_column_table_name","sqlite3_column_origin_name");
        Feature("SQLITE_ENABLE_PREUPDATE_HOOK","sqlite3_preupdate_hook","sqlite3_preupdate_count","sqlite3_preupdate_depth","sqlite3_preupdate_old","sqlite3_preupdate_new");
        Feature("SQLITE_ENABLE_SNAPSHOT","sqlite3_snapshot_get","sqlite3_snapshot_open","sqlite3_snapshot_free","sqlite3_snapshot_cmp","sqlite3_snapshot_recover");
        Feature("SQLITE_ENABLE_STMT_SCANSTATUS","sqlite3_stmt_scanstatus","sqlite3_stmt_scanstatus_v2","sqlite3_stmt_scanstatus_reset");
        Feature("SQLITE_ENABLE_UNLOCK_NOTIFY","sqlite3_unlock_notify");
        if(imports.Any(n=>n.StartsWith("sqlite3session_",StringComparison.Ordinal) || n.StartsWith("sqlite3changeset_",StringComparison.Ordinal)))
        {args.Add("-DSQLITE_ENABLE_SESSION=1");if(!args.Contains("-DSQLITE_ENABLE_PREUPDATE_HOOK=1"))args.Add("-DSQLITE_ENABLE_PREUPDATE_HOOK=1");}
        return Array.AsReadOnly(args.ToArray());
    }

    /// <summary>Require every supplied import to be defined by the admitted
    /// archive. Metadata may include unused SQLCipher methods; callers must not
    /// mistake that superset for the retained NativeAOT object's actual imports.
    /// This does not replace final ELF/link or execution checks.</summary>
    public static void RequireProviderSymbols(Archive archive,IReadOnlyList<string> imports)
    {
        Need(archive is not null && imports is {Count:>0 and <=512},"provider_symbols_input");
        string[] missing=imports.Where(n=>!archive!.DefinedSqliteSymbols.Contains(n,StringComparer.Ordinal)).Distinct().ToArray();
        if(missing.Length!=0)throw new ToolFailure("sqlite_contract_provider_symbols_missing",string.Join(',',missing.Take(16)));
    }

    /// <summary>Copy actual undefined sqlite3 symbols from one original nm -u
    /// NativeAOT managed object. Require known provider entries and the finite
    /// fixture's core imports. Metadata-only optional APIs are not invented as
    /// retained imports. Supplied text grants no object or linker authority.</summary>
    public static IReadOnlyList<string> InspectRetainedImportValues(string nm,IReadOnlyList<string> providerImports)
    {
        Need(nm.Length is >0 and <=16*1024*1024 && providerImports is {Count:>0 and <=512},"retained_import_input");
        SortedSet<string> result=new(StringComparer.Ordinal);int rows=0;
        foreach(string row in nm.Replace("\r\n","\n").Split('\n'))
        {
            Need(++rows<=200000 && row.Length<=8192,"retained_import_capacity");
            var match=Regex.Match(row,@"^\s*U (sqlite3[A-Za-z0-9_]+)\s*$",RegexOptions.CultureInvariant);
            if(!match.Success)continue;string name=match.Groups[1].Value;
            Need(providerImports.Contains(name,StringComparer.Ordinal),"retained_import_unknown");result.Add(name);
        }
        Need(new[]{"sqlite3_open_v2","sqlite3_close_v2","sqlite3_prepare_v2","sqlite3_step"}.All(result.Contains),"retained_import_missing");
        return Array.AsReadOnly(result.ToArray());
    }

    /// <summary>Classify final nm -u -P output. Allow explicitly reported weak
    /// toolchain references only; reject every strong undefined symbol and any
    /// unresolved SQLite reference, including weak ones. ELF loader/dynamic and
    /// relocation checks remain independent; supplied text is value-only.</summary>
    public static IReadOnlyList<WeakUndefined> InspectFinalUndefinedValues(string nm)
    {
        Need(nm.Length<=1024*1024,"final_undefined_size");List<WeakUndefined> weak=[];
        foreach(string line in nm.Replace("\r\n","\n").Split('\n'))
        {
            if(string.IsNullOrWhiteSpace(line))continue;string[] fields=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            Need(fields.Length==2 && fields[0].Length is >0 and <=255 && fields[1] is "w" or "v" && weak.Count<256,"final_undefined_strong");
            Need(!fields[0].StartsWith("sqlite3",StringComparison.Ordinal),"final_undefined_sqlite");weak.Add(new(fields[0],fields[1][0]));
        }
        return Array.AsReadOnly(weak.OrderBy(w=>w.Name,StringComparer.Ordinal).ToArray());
    }

    /// <summary>Validate the unchanged original socket publication recipe through
    /// the existing publisher, changing only this app/output and adding the
    /// audited archive and no-restore after owned RID restore. Retain unstripped
    /// symbols and the ILC map explicitly for linkage review. DirectPInvoke is
    /// declared in the candidate project. No process is started.</summary>
    public static IReadOnlyList<string> StaticPublishArguments(string originalCommands,string destination,string archive)
    {
        Need(Path.IsPathFullyQualified(archive) && archive.EndsWith(".a",StringComparison.Ordinal),"publish_archive");
        string[] args=NativeAotStaticFilePublisher.PublishArguments(originalCommands,destination);
        args[1]="apps/"+Application+"/"+Application+".csproj";
        return Array.AsReadOnly([..args,"--no-restore","-p:SqliteNativeArchive="+archive,"-p:StripSymbols=false","-p:IlcGenerateMapFile=true"]);
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
        ulong sectionAt=U64(elf,40);uint count=U16(elf,60);Need(count is >0 and <=16384 && sectionAt<=(ulong)elf.Length &&
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
                if(name.StartsWith("sqlite3",StringComparison.Ordinal))
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

    /// <summary>Own one fresh Linux source/cache namespace, original recipe
    /// lease, pinned upstream object/archive build, static NativeAOT publication
    /// and finite traced reference process. Preserve every attempted command and
    /// failed output. The original socket recipe and accepted application bytes
    /// remain read-only. This supplies Linux-reference requirements only; guest
    /// execution, fault/recovery and durable transactions require their own gates.
    /// Caller must serialize the external builder/transport with other owners.</summary>
    public static async Task<int> RunLinuxReferenceAsync(string root,string recipeRoot,string sourceArchive,string destination,TextWriter output)
    {
        const string marker="nativeaot.sqlite-reference";output.WriteLine("TEST_START "+marker);
        Need(OperatingSystem.IsLinux(),"linux_builder_required");root=Path.GetFullPath(root);recipeRoot=Path.GetFullPath(recipeRoot);
        sourceArchive=Path.GetFullPath(sourceArchive,root);destination=Path.GetFullPath(destination,root);
        Need(root!=recipeRoot && sourceArchive.StartsWith(Path.Combine(root,"artifacts")+"/",StringComparison.Ordinal) &&
            destination.StartsWith(Path.Combine(root,"artifacts")+"/",StringComparison.Ordinal) && !Path.Exists(destination),"linux_fresh_output");
        using var owned=NativeAotPayload.OpenQualificationLease(root);using var recipeLease=NativeAotPayload.OpenQualificationLease(recipeRoot);
        using AuthorityFiles held=new();Need(ToolingBuildInputs.Validate(root).Count==0,"tooling_inputs_changed");
        _=NativeAotSocketPackage.Create(root,recipeRoot,asynchronous:true);
        string app=Path.Combine(root,"apps",Application),project=Path.Combine(app,Application+".csproj");
        Need(!Path.Exists(Path.Combine(app,"bin")) && !Path.Exists(Path.Combine(app,"obj")) &&
            Directory.GetFiles(app).Select(Path.GetFileName).Order(StringComparer.Ordinal).SequenceEqual(new[]{Application+".csproj","Program.cs"}.Order(StringComparer.Ordinal)),"linux_fresh_cache");
        string[] SourcePaths()=>ToolingBuildInputs.SourceInputs(root).Concat(new[]{project,Path.Combine(app,"Program.cs")}).Distinct().Order(StringComparer.Ordinal).ToArray();
        var sources=SourcePaths().ToDictionary(p=>p,NativeAotWebPayload.HashFile,StringComparer.Ordinal);
        var original=held.Open(recipeRoot,Path.Combine(recipeRoot,"artifacts/socket-qualification-r01/commands.json"),"original static recipe","sqlite_original_recipe",2*1024*1024,singleLink:true);
        string recipeCommands=Encoding.UTF8.GetString(original.ReadBytes(2*1024*1024,"sqlite_original_recipe"));
        var sourceInput=held.Open(root,sourceArchive,"upstream source ZIP","sqlite_upstream_source",4*1024*1024,singleLink:true);
        var upstream=InspectSourceArchiveValues(sourceInput.ReadBytes(4*1024*1024,"sqlite_upstream_source"));
        Directory.CreateDirectory(destination);AuthorityFiles.RequirePlainPath(root,destination,"sqlite_native_output");
        using var ownership=new FileStream(Path.Combine(destination,"qualification.lock"),FileMode.CreateNew,FileAccess.Write,FileShare.None);
        List<object> commands=[];Dictionary<string,string> native=new(StringComparer.Ordinal);bool succeeded=false;string failure="";
        NativeAotPayload.Inspection? image=null;Archive? archive=null;Transcript? transcript=null;Trace? trace=null;
        IReadOnlyList<string>? imports=null,retainedImports=null;string[] metadataWithoutArchive=[];IReadOnlyList<WeakUndefined>? weakUndefined=null;
        async Task<string> Run(string name,string executable,IReadOnlyList<string> arguments,TimeSpan? timeout=null)
        {
            commands.Add(new{executable,arguments,workingDirectory=root,log=name});
            await NativeAotWebPayload.ExecuteAsync(executable,arguments.ToArray(),root,Path.Combine(destination,name),timeout??TimeSpan.FromMinutes(2));
            return await File.ReadAllTextAsync(Path.Combine(destination,name+".stdout.log"));
        }
        void Native(string path)
        {
            path=File.ResolveLinkTarget(path,true)?.FullName??Path.GetFullPath(path);string hash=NativeAotWebPayload.HashFile(path);
            Need(!native.TryGetValue(path,out string? prior) || prior==hash,"native_input_changed");native[path]=hash;
        }
        try
        {
            Need((await Run("sdk","dotnet",["--version"])).Trim()==NativeAotPayload.SdkVersion,"sdk_pin");
            var originalNative=JsonSerializer.Deserialize<Dictionary<string,string>>(await File.ReadAllTextAsync(Path.Combine(recipeRoot,"artifacts/socket-qualification-r01/native-inputs.json")))!;
            foreach(string name in new[]{"dotnet","gcc","clang","ld","ar","readelf","nm","strace","setsid"})
            {
                string? path=(Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator).Select(p=>Path.Combine(p,name)).FirstOrDefault(File.Exists);
                Need(path is not null,"native_tool_missing");path=File.ResolveLinkTarget(path!,true)?.FullName??Path.GetFullPath(path!);Native(path);
                if(name!="ar")Need(originalNative.TryGetValue(path,out var hash) && hash==native[path],"original_tool_changed");
                if(name is "gcc" or "ar")await Run("version-"+name,name,["--version"]);
            }
            foreach(string library in new[]{"libc.a","libm.a","libz.a","libstdc++.a","libgcc.a","libgcc_eh.a","crt1.o","crti.o","crtn.o"})
            {string path=(await Run("library-"+library,"gcc",["-print-file-name="+library])).Trim();Need(Path.IsPathFullyQualified(path) && File.Exists(path),"static_library_missing");Native(path);}
            string sourceDirectory=Path.Combine(destination,"upstream");Directory.CreateDirectory(sourceDirectory);
            string c=Path.Combine(sourceDirectory,"sqlite3.c"),header=Path.Combine(sourceDirectory,"sqlite3.h"),a=Path.Combine(destination,"libe_sqlite3.a");
            await File.WriteAllBytesAsync(c,upstream.CopyC());await File.WriteAllBytesAsync(header,upstream.CopyHeader());
            File.Copy(sourceArchive,Path.Combine(sourceDirectory,"sqlite-amalgamation-3530400.zip"));Native(c);Native(header);
            await Run("restore","dotnet",["restore",project,"-r","linux-musl-x64","--nologo","-p:PublishAot=true","-p:SqliteNativeArchive="+a],TimeSpan.FromMinutes(10));
            string assetsPath=Path.Combine(app,"obj/project.assets.json");
            var assets=held.Open(root,assetsPath,"native candidate assets","sqlite_native_assets",32*1024*1024,singleLink:true);
            byte[] assetBytes=assets.ReadBytes(32*1024*1024,"sqlite_native_assets");var dependencies=InspectDependencyValues(assetBytes);
            await File.WriteAllBytesAsync(Path.Combine(destination,"assets.json"),assetBytes);
            using(var document=JsonDocument.Parse(assetBytes))
            {
                foreach(var pin in PackagePins)
                {
                    string relative=pin.Key.ToLowerInvariant()+"/"+pin.Value+"/"+pin.Key.ToLowerInvariant()+"."+pin.Value+".nupkg";
                    string? package=document.RootElement.GetProperty("packageFolders").EnumerateObject().Select(p=>Path.Combine(p.Name,relative)).FirstOrDefault(File.Exists);
                    Need(package is not null,"package_archive_missing");var file=held.Open(Path.GetPathRoot(package!)!,package!,"signed package","sqlite_native_package",64*1024*1024,singleLink:true);
                    Need(file.Sha256.Equals(PackageArchiveSha256[pin.Key],StringComparison.OrdinalIgnoreCase),"package_archive_hash");Native(package!);
                    string retained=Path.Combine(destination,"packages",Path.GetFileName(package!));Directory.CreateDirectory(Path.GetDirectoryName(retained)!);File.Copy(package!,retained);
                    if(pin.Key=="SQLitePCLRaw.provider.e_sqlite3")imports=InspectProviderImports(file.ReadBytes(1024*1024,"sqlite_native_provider"));
                }
                foreach(var input in NativeAotPayload.CapturePackageInputs(document.RootElement))
                {Need(!native.TryGetValue(input.Key,out var old) || old==input.Value,"native_input_changed");native[input.Key]=input.Value;}
            }
            Need(imports is not null,"provider_import_missing");
            for(int round=0;round<2;round++)
            {
                string obj=Path.Combine(destination,"sqlite3-r"+round+".o"),lib=round==0?a:Path.Combine(destination,"libe_sqlite3-repeat.a");
                await Run("compile-r"+round,"gcc",StaticCompileArguments(c,obj,imports!),TimeSpan.FromMinutes(10));
                await Run("archive-r"+round,"ar",["crD",lib,obj]);Native(obj);Native(lib);
            }
            Need(NativeAotWebPayload.HashFile(a)==NativeAotWebPayload.HashFile(Path.Combine(destination,"libe_sqlite3-repeat.a")),"archive_reproducibility");
            archive=InspectStaticArchiveValues(await File.ReadAllBytesAsync(a));
            metadataWithoutArchive=imports!.Where(n=>!archive.DefinedSqliteSymbols.Contains(n,StringComparer.Ordinal)).ToArray();
            await Run("archive-defined","nm",["-g","--defined-only",a]);await Run("archive-undefined","nm",["-u",a]);
            string publish=Path.Combine(destination,Application);
            await Run("publish","dotnet",StaticPublishArguments(recipeCommands,publish,a),TimeSpan.FromMinutes(45));
            string executable=Path.Combine(publish,Application);var executableInput=held.Open(root,executable,"static native executable","sqlite_native_executable",NativeAotPayload.MaximumImageBytes,singleLink:true);
            image=NativeAotPayload.Inspect(executableInput.ReadBytes(NativeAotPayload.MaximumImageBytes,"sqlite_native_executable"));Native(executable);
            await Run("elf","readelf",["-h","-l","-d","-n","-W",executable]);await Run("symbols","nm",["-a",executable]);
            weakUndefined=InspectFinalUndefinedValues(await Run("final-undefined","nm",["-u","-P",executable]));
            string relocations=await Run("final-relocations","readelf",["-W","-r",executable]);Need(!relocations.Contains("sqlite3",StringComparison.Ordinal),"final_sqlite_relocation");
            string intermediate=Path.Combine(app,"obj/Release/net11.0/linux-musl-x64");
            retainedImports=InspectRetainedImportValues(await Run("managed-object-imports","nm",["-u",Path.Combine(intermediate,"native",Application+".o")]),imports!);
            RequireProviderSymbols(archive,retainedImports);
            await Run("managed-object-symbols","readelf",["-W","-s",Path.Combine(intermediate,"native",Application+".o")]);
            foreach(string input in new[]{Path.Combine(intermediate,Application+".dll"),Path.Combine(intermediate,"native",Application+".o")})
            {Native(input);string copy=Path.Combine(destination,"originals",Path.GetFileName(input));Directory.CreateDirectory(Path.GetDirectoryName(copy)!);File.Copy(input,copy);}
            bool direct=false;
            foreach(string rsp in Directory.GetFiles(Path.Combine(app,"obj"),"*.rsp",SearchOption.AllDirectories))
            {
                if((await File.ReadAllTextAsync(rsp)).Contains("--directpinvoke:e_sqlite3",StringComparison.Ordinal))direct=true;
                Native(rsp);string copy=Path.Combine(destination,"responses",Path.GetRelativePath(Path.Combine(app,"obj"),rsp));Directory.CreateDirectory(Path.GetDirectoryName(copy)!);File.Copy(rsp,copy);
            }
            Need(direct,"direct_pinvoke_setting");
            string map=Path.Combine(intermediate,"native",Application+".map.xml");Need(File.Exists(map),"ilc_map_missing");Native(map);
            File.Copy(map,Path.Combine(destination,"originals",Application+".map.xml"));
            string database=Path.Combine(destination,"contract.db");
            string result=await Run("reference","strace",["-ff","-yy","-ttt","-s","256","-o",Path.Combine(destination,"sqlite-trace"),executable,"--database",database],TimeSpan.FromMinutes(2));
            transcript=InspectTranscriptValues(result);
            Need(transcript.SourceSha256==Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SourceId))),"engine_source_id");
            string[] traces=Directory.GetFiles(destination,"sqlite-trace.*");Need(traces.Length is >0 and <=64,"trace_inventory");
            Dictionary<string,string> traceValues=new(StringComparer.Ordinal);
            foreach(string file in traces)
            {
                string suffix=file[(file.LastIndexOf('.')+1)..];Need(int.TryParse(suffix,out int pid) && pid>0 && new FileInfo(file).Length is >0 and <=4*1024*1024,"trace_identity");
                string text=await File.ReadAllTextAsync(file);Need(text.TrimEnd('\n','\r').EndsWith("+++ exited with 0 +++",StringComparison.Ordinal) &&
                    !text.Contains("+++ killed by",StringComparison.Ordinal),"trace_process_exit");traceValues.Add(Path.GetFileName(file),text);Native(file);
            }
            trace=InspectTraceValues(traceValues,database);
            Need(trace.Calls.Any(c=>c.Syscall=="execve") && trace.Calls.Any(c=>c.Syscall=="exit_group") &&
                trace.Calls.Any(c=>c.MentionsDatabase && c.Syscall is "fsync" or "fdatasync") &&
                trace.Calls.Any(c=>c.MentionsDatabase && c.Syscall=="fcntl") && trace.Calls.Any(c=>c.MentionsDatabase && c.Syscall is "unlink" or "unlinkat"),"trace_storage_closure");
            Native(database);Need(SourcePaths().SequenceEqual(sources.Keys) && sources.All(p=>NativeAotWebPayload.HashFile(p.Key)==p.Value) &&
                native.All(p=>NativeAotWebPayload.HashFile(p.Key)==p.Value) && ToolingBuildInputs.Validate(root).Count==0,"linux_inputs_changed");held.VerifyUnchanged();
            await File.WriteAllTextAsync(Path.Combine(destination,"dependencies.json"),JsonSerializer.Serialize(dependencies));
            await File.WriteAllTextAsync(Path.Combine(destination,"trace-values.json"),JsonSerializer.Serialize(trace));succeeded=true;
        }
        catch(Exception error)
        {failure=error is ToolFailure tool?tool.Reason:error.Message.ReplaceLineEndings(" ");output.WriteLine("TEST_FAIL "+marker+" "+failure);throw;}
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(destination,"commands.json"),JsonSerializer.Serialize(commands));
            await File.WriteAllTextAsync(Path.Combine(destination,"sources.json"),JsonSerializer.Serialize(sources));
            await File.WriteAllTextAsync(Path.Combine(destination,"native-inputs.json"),JsonSerializer.Serialize(native));
            var files=Directory.GetFiles(destination,"*",SearchOption.AllDirectories).Where(p=>Path.GetFileName(p)!="qualification.lock").Order(StringComparer.Ordinal)
                .ToDictionary(p=>Path.GetRelativePath(destination,p),NativeAotWebPayload.HashFile);
            await File.WriteAllTextAsync(Path.Combine(destination,"receipt.json"),JsonSerializer.Serialize(new
            {Schema="clros-sqlite-linux-reference-v1",Succeeded=succeeded,Failure=failure,Archive=archive,Imports=imports,RetainedImports=retainedImports,MetadataWithoutArchive=metadataWithoutArchive,WeakUndefined=weakUndefined,Image=image,Transcript=transcript,
                TraceUnjoinedLines=trace?.UnjoinedLines,SourceArchiveSha256,SourceArchiveSha3,SourceId,Sources=sources,Native=native,Outputs=files,
                GuestQualified=false,DurabilityQualified=false,RecoveryQualified=false,CompleteTraceProjection=trace?.UnjoinedLines==0}));
        }
        output.WriteLine("TEST_PASS "+marker);return 0;
    }

    private static bool Hex(string value)=>value.Length==64 && value.All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static ushort U16(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
    private static uint U32(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static ulong U64(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt64LittleEndian(bytes[at..]);
    private static void Need(bool condition,string reason){if(!condition)throw new ToolFailure("sqlite_contract_"+reason);}
}
