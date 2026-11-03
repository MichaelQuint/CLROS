using System.Buffers.Binary;
using System.Globalization;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;
internal sealed partial class EfiVerification(EfiOptions options, Action<string> emit) : IDisposable
{
    private readonly AuthorityFiles locks = new();
    private readonly List<AuthorityFile> outputs = [];
    private string Root => options.Root;

    private EfiProfile profile = null!;
    private JsonObject evidence = null!;
    private EfiPeImage image = null!;
    private string artifactPath = "", mapPath = "", responsePath = "", evidencePath = "", map = "", response = "", linkedSerializer = "", configuration = "", extensions = "", packages = "", runtimePack = "", sdk = "", dotnet = "";
    private string[] responseTokens = [];
    private AuthorityFile artifact = null!, mapFile = null!, responseFile = null!, evidenceFile = null!, serializer = null!, serializerLinked = null!;
    private string Text(string name) => String(evidence[name], "link_evidence_property_invalid");
    private long Number(string name) => Integer(evidence[name], name);
    private string Absolute(string value, string reason) => FullPath(Root, value, reason);
    private string EvidencePath(string name, string reason) => Absolute(String(evidence[name], reason), reason);
    private AuthorityFile Open(string path, string prefix, long maximum = MaximumFileBytes, bool output = false, bool allowEmpty = false, string? root = null)
    {
        Require(File.Exists(path), prefix + "_missing", "path=" + path);
        var file = locks.Open(root ?? Path.GetPathRoot(path)!, path, prefix, prefix, maximum, allowEmpty, output);
        if (output)
            outputs.Add(file);
        return file;
    }

    public void Dispose() => locks.Dispose();
    // A continuing runner may retain this verified context across execution.
    // Re-enumerate default compile inputs as well as checking held file identity:
    // a new tooling source must not evade the original source-closure snapshot.
    internal void VerifyHeldInputs()
    {
        if (evidence is null)
            throw new ToolFailure("artifact_not_verified");
        VerifySnapshot(locks, Root, SourceClosurePaths(Root), evidence["sourceClosureInputs"], "source_closure", Text("sourceClosureSha256"), MaximumTextBytes, singleLink: true);
        locks.VerifyUnchanged();
    }

    public void Verify()
    {
        profile = options.Profile(line => emit(line.Replace("EFI_PROFILE_", "EFI_ARTIFACT_", StringComparison.Ordinal)));
        artifactPath = Absolute(options.Get("EfiArtifact", "artifacts/efi/BOOTX64.EFI"), "artifact_path_invalid");
        string project = PinnedPath(options.Get("ProjectPath", "src/OS/OS.csproj"), "src/OS/OS.csproj", "project_path_not_canonical_repository_contract");
        string entry = PinnedPath(options.Get("EntrySourcePath", "src/OS/Boot/EfiEntry.cs"), "src/OS/Boot/EfiEntry.cs", "entry_source_path_not_canonical_repository_contract");
        mapPath = Absolute(options.Get("LinkerMapPath", artifactPath + ".link.map"), "linker_map_path_invalid");
        responsePath = Absolute(options.Get("LinkResponsePath", artifactPath + ".link.rsp"), "link_response_path_invalid");
        evidencePath = Absolute(options.Get("LinkEvidencePath", artifactPath + ".link.json"), "link_evidence_path_invalid");
        foreach ((string path, string reason)in new[]
        {
            (artifactPath, "missing_artifact"),
            (project, "missing_build_contract"),
            (entry, "missing_entry_source_contract"),
            (mapPath, "missing_linker_map"),
            (responsePath, "missing_link_response"),
            (evidencePath, "missing_link_evidence")
        }

        )
            Require(File.Exists(path), reason, "path=" + path);
        artifact = Open(artifactPath, "artifact", 536870912, output: true, allowEmpty: true);
        mapFile = Open(mapPath, "linker_map", MaximumTextBytes, output: true);
        responseFile = Open(responsePath, "link_response", 1048576, output: true);
        evidenceFile = Open(evidencePath, "link_evidence", 1048576, output: true);
        AuthorityFile projectFile = Open(project, "source_project", MaximumTextBytes, root: Root), entryFile = Open(entry, "entry_source", MaximumTextBytes, root: Root);
        AuthorityFiles.RequireDistinct(projectFile, entryFile);
        image = new(artifact.ReadBytes(536870912, "artifact_read_failed"));
        string projectText = projectFile.ReadUtf8(MaximumTextBytes, "entry_contract_read_failed"), entryText = entryFile.ReadUtf8(MaximumTextBytes, "entry_contract_read_failed");
        foreach (string pattern in new[]
        {
            @"<EntryPointSymbol>\s*EfiMain\s*</EntryPointSymbol>",
            @"<LinkerSubsystem>\s*EFI_APPLICATION\s*</LinkerSubsystem>",
            @"/ENTRY:\$\(EntryPointSymbol\)",
            @"/NODEFAULTLIB"
        }

        )
            Require(Regex.IsMatch(projectText, pattern), "efimain_project_contract_missing");
        Require(Regex.IsMatch(entryText, "RuntimeExport\\s*\\(\\s*\"EfiMain\"\\s*\\)"), "efimain_source_contract_missing");
        map = mapFile.ReadUtf8(MaximumTextBytes, "link_evidence_read_failed");
        response = responseFile.ReadUtf8(1048576, "link_evidence_read_failed");
        evidence = Parse(evidenceFile.ReadUtf8(1048576, "link_evidence_read_failed"), "link_evidence_read_failed");
        var responseTimer = Stopwatch.StartNew();
        responseTokens = LinkResponseTokens(response);
        VerifyResponseSafety();
        responseTimer.Stop();
        List<string> names = [..EfiContracts.EvidenceProperties];
        foreach (string prefix in NativeObjectPrefixes())
            foreach (string suffix in new[]
            {
                "LinkedObjectPath",
                "ObjectPath",
                "ObjectBytes",
                "ObjectSha256"
            }

            )
                names.Add(prefix + suffix);
        if (profile.IsEnabled("M04EhCatalog") || profile.IsEnabled("M04NormalFinally"))
            foreach (string prefix in new[]
            {
                "ehCatalog",
                "ehCatalogIlc",
                "ehCatalogMap"
            }

            )
                foreach (string suffix in new[]
                {
                    "LinkedObjectPath",
                    "ObjectPath",
                    "ObjectBytes",
                    "ObjectSha256"
                }

                )
                    names.Add(prefix + suffix);
        foreach (string name in names)
            Require(evidence.ContainsKey(name), "link_evidence_property_missing", "property=" + name);
        Shape(evidence, names, "link_evidence_property_set_invalid");
        Require(Number("schemaVersion") == 3 && Text("entryPointSymbol") == "EfiMain" && Text("managedEntrySymbol") == "OS_OS_Boot_EfiEntry__EfiMain" && Text("serializerSymbol") == EfiContracts.SerializerSymbol && Number("serializerExpectedSuccessCookie") == EfiContracts.SerializerSuccessCookie, "link_evidence_contract_invalid");
        foreach (string stage in new[]
        {
            "restore",
            "publish"
        }

        )
            foreach ((string suffix, bool expected)in new[]
            {
                ("ImportProjectExtensionProps", false),
                ("ImportProjectExtensionTargets", false),
                ("ExcludeRestorePackageImports", true)
            }

            )
            {
                string name = stage + suffix;
                Require(evidence[name] is JsonValue value && value.TryGetValue<bool>(out bool boolean) && boolean == expected, "link_evidence_import_boolean_contract_invalid", "property=" + name);
            }

        foreach (var pair in evidence.Where(p => p.Key.EndsWith("Sha256", StringComparison.Ordinal)))
            Require(IsHash(String(pair.Value, "link_evidence_hash_malformed")), "link_evidence_hash_malformed", "property=" + pair.Key);
        var solution = Open(Path.Combine(Root, "CLROS.slnx"), "solution", MaximumTextBytes, root: Root);
        Require(Number("artifactBytes") == artifact.Length && Text("artifactSha256") == artifact.Sha256 && Text("linkerMapSha256") == mapFile.Sha256 && Text("linkResponseSha256") == responseFile.Sha256 && Text("sourceProjectSha256") == projectFile.Sha256 && Text("entrySourceSha256") == entryFile.Sha256 && Text("solutionSha256") == solution.Sha256, "link_evidence_hash_mismatch");
        Require(Text("bootPolicy") == profile.BootPolicy, "boot_policy_evidence_mismatch");
        VerifySnapshot(locks, Root, SourceClosurePaths(Root), evidence["sourceClosureInputs"], "source_closure", Text("sourceClosureSha256"), MaximumTextBytes, singleLink: true);
        configuration = Text("configuration");
        Require(configuration is "Release" or "Debug", "configuration_invalid");
        Require(DateTimeOffset.TryParseExact(Text("generatedUtc"), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var generated), "generated_utc_invalid");
        Require(generated.Offset == TimeSpan.Zero, "generated_utc_not_utc");
        VerifySerializerAndManagedAssembly();
        VerifyLinkResponse();
        var runtimeTimer = Stopwatch.StartNew();
        image.VerifyMapAndRuntime(map, Path.GetFileName(linkedSerializer));
        runtimeTimer.Stop();
        VerifyProfileBindings();
        VerifyNativeBridges();
        // Reject malformed local artifacts before the comparatively expensive package scan and
        // external evaluation. Acceptance still requires every provenance check below.
        VerifyProvenance(project);
        AuthorityFiles.RequireDistinct(outputs.ToArray());
        locks.VerifyUnchanged();
        emit("EFI_ARTIFACT_VERIFY_PATH " + artifactPath);
        emit($"EFI_ARTIFACT_PE format=PE32+ machine=0x8664 subsystem=10 entryRva=0x{image.EntryPointRva:x8} entrySection={image.EntrySection}");
        emit("EFI_ARTIFACT_IMPORTS import=absent bound-import=absent iat=absent delay-import=absent");
        emit($"EFI_ARTIFACT_LINK_RESPONSE_SCAN tokens={responseTokens.Length} elapsedMs={responseTimer.ElapsedMilliseconds} maximumTokens={EfiContracts.MaximumLinkResponseTokens}");
        emit($"EFI_ARTIFACT_ENTRY_CONTRACT symbol=EfiMain managedSymbol=OS_OS_Boot_EfiEntry__EfiMain association=same-build-linker-map rva=0x{image.EntryPointRva:x8}");
        emit($"EFI_ARTIFACT_EXCEPTION_DIRECTORY section={image.Resolve(image.ExceptionRva, image.ExceptionSize, "exception_directory_not_file_backed", "exception_directory_section_ambiguous").Section.Name} rva=0x{image.ExceptionRva:x8} size={image.ExceptionSize} records={image.RuntimeFunctions.Count}");
        emit($"EFI_ARTIFACT_RUNTIME_SCAN records={image.RuntimeFunctions.Count} distinctUnwind={image.RuntimeFunctions.Select(f => f.Unwind).Distinct().Count()} elapsedMs={runtimeTimer.ElapsedMilliseconds} maximumRecords={EfiContracts.MaximumRuntimeFunctionRecords}");
        emit($"EFI_ARTIFACT_SERIALIZER symbol={EfiContracts.SerializerSymbol} section={image.Resolve(image.SerializerRva, (ulong)EfiContracts.SerializerBytes.Length, "serializer_not_file_backed", "serializer_section_ambiguous").Section.Name} rva=0x{image.SerializerRva:x8} size={EfiContracts.SerializerBytes.Length} unwindRva=0x{image.SerializerUnwindRva:x8} sha256={EfiContracts.SerializerBytesSha256} expectedCookie=0x{EfiContracts.SerializerSuccessCookie:x8}");
        emit($"EFI_ARTIFACT_SERIALIZER_OBJECT path={serializer.Path} linkedPath={serializerLinked.Path} bytes={serializer.Length} sha256={serializer.Sha256}");
        emit($"EFI_ARTIFACT_LINK_MAP path={mapPath} sha256={mapFile.Sha256}");
        emit($"EFI_ARTIFACT_LINK_RESPONSE path={responsePath} sha256={responseFile.Sha256}");
        emit("EFI_ARTIFACT_SHA256 " + artifact.Sha256);
    }

    private string PinnedPath(string supplied, string expected, string reason)
    {
        string normalized = supplied.Replace('\\', '/');
        Require(!string.IsNullOrWhiteSpace(supplied) && !normalized.Split('/').Any(s => s is "." or ".."), reason);
        Require(Path.IsPathRooted(supplied) || normalized.Equals(expected, StringComparison.OrdinalIgnoreCase), reason);
        string full = Absolute(supplied, reason), canonical = Path.GetFullPath(Path.Combine(Root, expected));
        Require(SamePath(full, canonical), reason);
        return canonical;
    }

    private IEnumerable<string> NativeObjectPrefixes()
    {
        if (profile.IsEnabled("M04CheckedReferenceBarrier") || profile.IsEnabled("M04GcStaticRootMaterialization"))
            yield return "checkedReference";
        if (profile.IsEnabled("M04InterfaceDispatch"))
            yield return "interfaceDispatch";
        if (profile.IsEnabled("M04SimpleThrowCatch"))
            yield return "simpleCatch";
        if (profile.IsEnabled("M04SingleFinallyCatch"))
            yield return "singleFinally";
        if (profile.IsEnabled("M04Rethrow"))
        {
            yield return "rethrowInner";
            yield return "rethrowOuter";
        }

        if (profile.IsEnabled("M04NormalFinally"))
            yield return "normalFinally";
        if (profile.IsEnabled("M04FrameDispatch"))
            yield return "frameDispatch";
        if (profile.IsEnabled("M04FilteredCatch"))
        {
            yield return "filteredCatch";
            yield return "filterInvoke";
        }

        if (profile.IsEnabled("M04ConstructorFailure"))
            yield return "constructorFailure";
        if (profile.IsEnabled("M04NestedFinallyCatch"))
            yield return "nestedFinally";
        if (profile.IsEnabled("M04UnhandledException"))
            yield return "simpleCatch";
    }

    public static string[] LinkResponseTokens(string text)
    {
        var matches = Regex.Matches(text, "\"([^\"]*)\"|(\\S+)");
        Require(matches.Count <= EfiContracts.MaximumLinkResponseTokens, "link_response_token_count_out_of_range");
        return matches.Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).ToArray();
    }

    private void VerifyResponseSafety()
    {
        foreach (string token in responseTokens)
        {
            Require(!token.StartsWith('@'), "nested_link_response_file_forbidden");
            Require(!token.StartsWith('-'), "noncanonical_link_option_prefix");
            Require(!Regex.IsMatch(token, @"(?i)^/FORCE(?:$|:)"), "force_linkage_forbidden");
        }

        Require(responseTokens.Count(t => t.Equals("/BREPRO", StringComparison.OrdinalIgnoreCase)) == 1 && !responseTokens.Any(t => Regex.IsMatch(t, @"(?i)^/BREPRO[:=]")), "brepro_link_response_contract_invalid");
    }

    private void VerifySerializerAndManagedAssembly()
    {
        string preserved = EvidencePath("serializerObjectPath", "serializer_object_path_invalid");
        Require(SamePath(preserved, artifactPath + ".serializer.obj"), "serializer_object_not_artifact_sibling");
        if (options.Get("SerializerObjectPath").Length != 0)
            Require(SamePath(Absolute(options.Get("SerializerObjectPath"), "serializer_object_path_invalid"), preserved), "serializer_object_path_mismatch");
        serializer = Open(preserved, "serializer_object", EfiContracts.SerializerObjectBytes, output: true);
        Require(Number("serializerObjectBytes") == EfiContracts.SerializerObjectBytes && serializer.Length == EfiContracts.SerializerObjectBytes && serializer.Sha256 == EfiContracts.SerializerObjectSha256 && Text("serializerObjectSha256") == EfiContracts.SerializerObjectSha256, "serializer_object_identity_mismatch");
        linkedSerializer = EvidencePath("serializerLinkedObjectPath", "serializer_linked_object_path_invalid");
        Require(!SamePath(preserved, linkedSerializer), "serializer_linked_and_preserved_object_paths_alias");
        serializerLinked = Open(linkedSerializer, "serializer_linked_object", EfiContracts.SerializerObjectBytes, output: true);
        Require(serializerLinked.Length == EfiContracts.SerializerObjectBytes && serializerLinked.Sha256 == EfiContracts.SerializerObjectSha256, "serializer_linked_object_identity_mismatch");
        foreach ((string property, string path)in EfiContracts.SerializerSourceBindings)
            Require(Open(Path.Combine(Root, path), "serializer_source", MaximumTextBytes, root: Root).Sha256 == Text(property), "serializer_source_hash_mismatch", "property=" + property);
        string managed = EvidencePath("managedAssemblyPath", "managed_assembly_path_invalid"), expected = Path.Combine(Root, EfiContracts.ManagedAssemblyRelativePath(configuration));
        Require(SamePath(managed, expected), "managed_assembly_path_not_canonical");
        var managedFile = Open(managed, "managed_assembly", MaximumFileBytes, output: true, root: Root);
        Require(managedFile.Sha256 == Text("managedAssemblySha256"), "managed_assembly_identity_mismatch");
        VerifyManagedPe(managedFile.ReadBytes(MaximumFileBytes, "managed_assembly_read_failed"));
    }

    private static void VerifyManagedPe(byte[] bytes)
    {
        void Bounds(long offset, long count, string reason) => Range(offset, count, bytes.Length, reason);
        ushort Short(long offset, string reason)
        {
            Bounds(offset, 2, reason);
            return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan((int)offset));
        }

        uint Word(long offset, string reason)
        {
            Bounds(offset, 4, reason);
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan((int)offset));
        }

        Require(bytes.LongLength is> 0 and <= MaximumFileBytes, "managed_assembly_size_out_of_range");
        Bounds(0, 64, "managed_pe_dos_header_truncated");
        Require(bytes[0] == 0x4d && bytes[1] == 0x5a, "managed_pe_dos_signature_invalid");
        long pe = Word(0x3c, "managed_pe_dos_header_truncated");
        Require(pe >= 64, "managed_pe_offset_invalid");
        Bounds(pe, 24, "managed_pe_header_truncated");
        Require(Word(pe, "managed_pe_header_truncated") == 0x4550, "managed_pe_signature_invalid");
        long coff = pe + 4, optional = coff + 20;
        int count = Short(coff + 2, "managed_pe_coff_header_truncated"), optionalSize = Short(coff + 16, "managed_pe_coff_header_truncated");
        Require(count is> 0 and <= 96, "managed_pe_section_count_out_of_range");
        Bounds(optional, optionalSize, "managed_pe_optional_header_truncated");
        ushort magic = Short(optional, "managed_pe_optional_header_truncated");
        Require(magic is 0x10b or 0x20b, "managed_pe_optional_magic_invalid");
        int directoryOffset = magic == 0x10b ? 96 : 112;
        Require(optionalSize >= directoryOffset + 15 * 8, "managed_pe_optional_header_too_small");
        Require(Word(optional + directoryOffset - 4, "managed_pe_optional_header_truncated") > 14, "managed_pe_cli_directory_missing");
        uint cliRva = Word(optional + directoryOffset + 14 * 8, "managed_pe_cli_directory_truncated"), cliSize = Word(optional + directoryOffset + 14 * 8 + 4, "managed_pe_cli_directory_truncated");
        Require(cliRva != 0 && cliSize >= 72, "managed_pe_cli_directory_invalid");
        long table = optional + optionalSize;
        Bounds(table, count * 40, "managed_pe_section_table_truncated");
        List<(uint VirtualSize, uint VirtualAddress, uint RawSize, uint RawOffset)> sections = [];
        for (int i = 0; i < count; i++)
        {
            long row = table + i * 40;
            sections.Add((Word(row + 8, "managed_pe_section_table_truncated"), Word(row + 12, "managed_pe_section_table_truncated"), Word(row + 16, "managed_pe_section_table_truncated"), Word(row + 20, "managed_pe_section_table_truncated")));
        }

        int Resolve(uint rva, uint size, string prefix)
        {
            ulong end = (ulong)rva + size;
            List<int> matches = [];
            foreach (var section in sections)
            {
                ulong virtualEnd = (ulong)section.VirtualAddress + section.VirtualSize, rawEnd = (ulong)section.VirtualAddress + section.RawSize;
                if (section.VirtualSize == 0 || section.RawSize == 0 || virtualEnd > 0x100000000 || rawEnd > 0x100000000 || rva < section.VirtualAddress || end > virtualEnd || end > rawEnd)
                    continue;
                ulong offset = (ulong)section.RawOffset + rva - section.VirtualAddress;
                if (offset <= (ulong)bytes.Length && size <= (ulong)bytes.Length - offset)
                    matches.Add((int)offset);
            }

            Require(matches.Count != 0, prefix + "_not_file_backed");
            Require(matches.Count == 1, prefix + "_ambiguous");
            return matches[0];
        }

        int cli = Resolve(cliRva, 72, "managed_pe_cli_directory");
        Require(Word(cli, "managed_pe_cli_header_truncated") >= 72, "managed_pe_cli_header_invalid");
        uint metadataRva = Word(cli + 8, "managed_pe_cli_header_truncated"), metadataSize = Word(cli + 12, "managed_pe_cli_header_truncated");
        Require(metadataRva != 0 && metadataSize >= 4, "managed_pe_metadata_directory_invalid");
        Require(Word(Resolve(metadataRva, 4, "managed_pe_metadata"), "managed_pe_metadata_not_file_backed") == 0x424a5342, "managed_pe_metadata_signature_invalid");
    }

    private string Option(string name, string reason)
    {
        var options = Regex.Matches(response, "(?im)^\\s*/" + name + ":(?:\"([^\"]+)\"|([^\\s\"]+))\\s*$");
        Require(options.Count == 1, reason);
        return options[0].Groups[1].Success ? options[0].Groups[1].Value : options[0].Groups[2].Value;
    }

    private int ObjectCount(string path) => responseTokens.Count(t => t.Length != 0 && t[0] is not ('/' or '-') && SamePath(FullPath(Path.GetDirectoryName(responsePath)!, t, "link_response_object_path_invalid"), path));
    private void VerifyLinkResponse()
    {
        Require(Option("ENTRY", "efimain_link_response_contract_missing") == "EfiMain", "efimain_link_response_contract_missing");
        Require(Regex.Matches(response, @"(?im)^\s*/NODEFAULTLIB\s*$").Count == 1, "nodefaultlib_link_response_contract_missing");
        Require(!Regex.IsMatch(response, @"(?im)(?:^|\s)/DEFAULTLIB(?::|\s|$)"), "default_library_linkage_present");
        Require(!Regex.IsMatch(response, @"(?im)(?:^|\s)/ALTERNATENAME:"), "serializer_fallback_linkage_present");
        List<string> includes = [EfiContracts.SerializerSymbol];
        if (profile.IsEnabled("M04CheckedReferenceBarrier") || profile.IsEnabled("M04GcStaticRootMaterialization"))
            includes.AddRange(["RhpAssignRef", "RhpCheckedAssignRef", "RhpByRefAssignRef"]);
        if (profile.IsEnabled("M04InterfaceDispatch"))
            includes.AddRange(["RhpInterfaceDispatch", "CLROS_InterfaceDispatch_GetCellSection", "CLROS_InterfaceDispatch_GetInitialStub"]);
        if (profile.IsEnabled("M04SimpleThrowCatch") || profile.IsEnabled("M04SingleFinallyCatch"))
            includes.AddRange(["RhpThrowEx", "CLROS_Eh_GetClauseTable"]);
        if (profile.IsEnabled("M04Rethrow"))
            includes.AddRange(["RhpThrowEx", "RhpRethrow", "CLROS_Rethrow_InnerClauses", "CLROS_Rethrow_OuterClauses"]);
        if (profile.IsEnabled("M04NormalFinally"))
            includes.AddRange(["RhpThrowEx", "CLROS_NormalFinally_Clauses", "RhTypeCast_IsInstanceOfException"]);
        if (profile.IsEnabled("M04FrameDispatch"))
            includes.AddRange(["RhpThrowEx", "CLROS_Frame_CatcherClauses", "CLROS_Frame_MiddleClauses", "RhTypeCast_IsInstanceOfException"]);
        if (profile.IsEnabled("M04EhCatalog") || profile.IsEnabled("M04NormalFinally"))
            includes.AddRange(["CLROS_Eh_CatalogStart", "CLROS_Eh_CatalogEnd", "CLROS_Eh_Contributions", "CLROS_Eh_ContributionsEnd"]);
        if (profile.IsEnabled("M04FilteredCatch"))
            includes.AddRange(["RhpThrowEx", "CLROS_Filter_Clauses", "CLROS_Filter_Invoke", "RhTypeCast_IsInstanceOfClass"]);
        if (profile.IsEnabled("M04ConstructorFailure"))
            includes.AddRange(["RhpThrowEx", "CLROS_CctorFailure_InvokeClauses", "CLROS_CctorFailure_OuterClauses", "RhTypeCast_IsInstanceOfException"]);
        if (profile.IsEnabled("M04NestedFinallyCatch"))
            includes.AddRange(["RhpThrowEx", "CLROS_Nested_Clauses"]);
        if (profile.IsEnabled("M04UnhandledException"))
            includes.AddRange(["RhpThrowEx", "CLROS_Eh_GetClauseTable", "RhTypeCast_IsInstanceOfException"]);
        var actual = Regex.Matches(response, "(?im)^\\s*/INCLUDE:(?:\"([^\"]+)\"|([^\\s\"]+))\\s*$").Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).ToArray();
        Require(actual.Length == includes.Count && includes.All(n => actual.Count(a => a == n) == 1), "serializer_force_include_contract_missing");
        string mapValue = Option("MAP", "serializer_link_map_response_contract_missing"), expectedMap = Path.Combine(Path.GetDirectoryName(linkedSerializer)!, "OS.link.map");
        string resolvedMap = FullPath(Path.GetDirectoryName(responsePath)!, mapValue, "serializer_link_map_response_path_invalid");
        Require(SamePath(resolvedMap, expectedMap), "serializer_link_map_path_mismatch");
        Require(Open(expectedMap, "serializer_link_map", MaximumTextBytes, output: true, root: Root).Sha256 == mapFile.Sha256, "serializer_link_map_identity_mismatch");
        Require(Option("MAPINFO", "serializer_link_mapinfo_contract_missing") == "EXPORTS", "serializer_link_mapinfo_contract_missing");
        Require(ObjectCount(linkedSerializer) == 1, "serializer_object_link_response_contract_missing");
    }
}
