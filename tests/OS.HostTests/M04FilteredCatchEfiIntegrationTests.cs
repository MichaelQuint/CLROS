using System.Xml.Linq;
using System.Text.Json.Nodes;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static class M04FilteredCatchEfiIntegrationTests
{
    private const string Flag = "EnableM04FilteredCatchProbe", Symbol = "M04_FILTERED_CATCH_PROBE";
    private static readonly string[] Flags = [.. RuntimeProfilePortTests.RethrowFlags[..^1], "M04FilteredCatch"];
    private static readonly string[] Competitors = ["M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch",
        "M04X64UnwindMetadata", "M04SimpleThrowCatch", "M04SingleFinallyCatch", "M04Rethrow", "M04NestedFinallyCatch", "M04UnhandledException", "M04FrameDispatch", "M04ConstructorFailure"];

    [HostTest("m04.021.filters.efi exact prerequisite profile", HostLane.Fast, 0)]
    private static void Prerequisites()
    {
        foreach (bool build in new[] { false, true })
        {
            var profile = EfiOptions.Parse(Arguments(Flags), build).Profile();
            HostCheck.True(profile.BootPolicy == "Diagnostic" && Flags.Select(Full).ToHashSet().SetEquals(profile.Flags.Where(p => p.Value).Select(p => p.Key)), "exact explicit profile");
            foreach (string missing in Flags[..^1])
            {
                string reason = missing == "M04GcStaticRootMaterialization" ? "m04_filter_requires_gc_profile" : "m04_gc_static_profile_invalid";
                string[] remaining = Arguments(Flags.Where(name => name != missing));
                Reject(remaining, build, reason);
                Reject([.. remaining, "--" + Full(missing), "false"], build, reason);
            }
            var disabled = EfiOptions.Parse(["--" + Flag, "false"], build).Profile();
            HostCheck.True(disabled.BootPolicy == "VerifiedExit" && !disabled.IsEnabled("M04FilteredCatch"), "explicit false");
            Reject(["--" + Flag + "=invalid"], build, "argument_boolean_invalid");
        }
    }

    [HostTest("m04.021.filters.efi competing profiles reject", HostLane.Fast, 1)]
    private static void Conflicts()
    {
        foreach (bool build in new[] { false, true })
        {
            foreach (string collision in Competitors)
            {
                string[] prerequisite = collision switch
                {
                    "M04InterfaceDispatch" => ["M04ClassConstructorExecution"],
                    "M04X64UnwindMetadata" => ["M04TypeManagerModuleRegistration"],
                    _ => []
                };
                Reject(Arguments([.. Flags, .. prerequisite, collision]), build, "m04_filter_profile_conflict");
            }
            Reject(["--boot-policy", "VerifiedExit", .. Arguments(Flags)], build, "clros_boot_policy_probe_conflict");
        }
    }

    [HostTest("m04.021.filters.efi default off and source integration", HostLane.Fast, 2)]
    private static void Defaults()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        var defaults = project.Descendants(Flag).ToArray();
        HostCheck.True(defaults.Length == 1 && defaults[0].Value == "false" && defaults[0].Attribute("Condition")?.Value == "'$(EnableM04FilteredCatchProbe)' == ''", "explicit default false");
        HostCheck.True(!Probes.EnableM04FilteredCatchProbe && !BootSequence.IsFilteredCatchEnabled(), "host default off");
        var probe = Probes.GetM04FilteredCatchProbe();
        HostCheck.True(probe.Name == Flag && probe.Group == "runtime.exception" && !probe.EnabledByDefault && probe.Risk == ProbeRisk.Destructive, "destructive definition");
        var catalog = Enumerable.Range(0, Probes.ProbeCatalogCount).Select(index => Probes.TryGetProbe(index, out var p) ? p : default).ToArray();
        HostCheck.True(catalog.Count(p => p.Name == Flag) == 1, "one catalog entry");
        HostCheck.True(EfiContracts.DiagnosticProbes.Contains(Flag) && EfiContracts.ProbeSymbols[Flag] == Symbol, "shared flag and symbol");
        var defines = project.Descendants("DefineConstants").Where(node => node.Value.Split(';').Contains(Symbol)).ToArray();
        HostCheck.True(defines.Length == 1 && defines[0].Parent?.Attribute("Condition")?.Value == "'$(EnableM04FilteredCatchProbe)' == 'true'", "explicit compile gate");
        var source = HostSourceCatalog.Efi(EfiOptions.Parse(Arguments(Flags), true).Profile());
        var sources = source.TypeSources("OS.Kernel.Runtime.BoundedExceptionFilterSearch")
            .Append(source.Document(source.Method("OS.Runtime.RhpNewFastAllocation", "RunFilteredCatch")))
            .DistinctBy(document => document.Path);
        foreach (var document in sources)
        {
            string include = Path.GetRelativePath(Path.Combine(root, "src", "OS"), document.Path).Replace('/', '\\');
            HostCheck.True(EfiContracts.ReviewedSourceIncludes.Contains(include)
                && HostEfiSourceAdmission.Accepts(include, document.Text), "reviewed compiler input " + include);
        }
        var admission = EfiProjectAdmission.InspectStatic(root);
        HostCheck.True(admission.Succeeded, admission.Reason + " " + admission.Detail);
    }

    [HostTest("m04.021.filters.efi original EH and unwind byte controls", HostLane.Fast, 3)]
    private static void MetadataControls()
    {
        byte[] table = [4, 62, 52, 254, 120, 62, 52, 213, 3, 201, 2];
        Check(table, EfiVerify.TestFilteredCatchEhTable);
        byte[][] unwind = [[1, 4, 1, 0, 4, 66, 0], [1, 5, 2, 0, 5, 114, 1, 80, 4, 0x60, 0xA6, 5, 0], [1, 4, 1, 0, 4, 66, 2], [1, 4, 1, 0, 4, 66, 1]];
        for (int part = 0; part < unwind.Length; part++)
        {
            int index = part;
            Check(unwind[part], bytes => EfiVerify.TestFilteredCatchUnwind(bytes, index, 0x5A660));
        }
        HostCheck.True(!EfiVerify.TestFilteredCatchUnwind(unwind[1], 1, 0) && !EfiVerify.TestFilteredCatchUnwind(unwind[1], 1, 0x100000000), "invalid table RVAs");
    }

    [HostTest("m04.021.filters.efi native pins and rehashed mutation controls", HostLane.Native, 4)]
    private static void NativeAdmission()
    {
        string root = HostToolProcess.FindRepositoryRoot(), name = "filter-admission-" + Guid.NewGuid().ToString("N"),
            artifact = Path.Combine(root, "artifacts/efi", name + ".EFI"), log = Path.Combine(root, "artifacts/filter-native", name + ".log");
        using StringWriter output = new();
        int code = EfiBuild.Run(["--EfiOutputPath", artifact, "--LogPath", log, .. Arguments(Flags)], output);
        HostCheck.True(code == 0, output.ToString().Length > 4000 ? output.ToString()[^4000..] : output.ToString());
        byte[] original = File.ReadAllBytes(artifact);
        string evidencePath = artifact + ".link.json", originalEvidence = File.ReadAllText(evidencePath), map = File.ReadAllText(artifact + ".link.map");
        EfiPeImage image = new(original);
        image.VerifyMapAndRuntime(map, Path.GetFileName(JsonNode.Parse(originalEvidence)!["serializerLinkedObjectPath"]!.GetValue<string>()));
        int Offset(string symbol, int bytes)
        {
            var rows = EfiPeImage.Symbols(map, symbol);
            HostCheck.True(rows.Count == 1, "unique " + symbol);
            ulong rva = EfiPeImage.Hex(rows[0].Groups[3].Value, "fixture") - image.ImageBase;
            return image.Resolve(rva, (ulong)bytes, "fixture backing", "fixture ambiguity").Offset;
        }
        void Verify(string? expected)
        {
            string? failure = null;
            try
            {
                EfiVerify.Verify(EfiOptions.Parse(["--EfiArtifact", artifact, .. Arguments(Flags)], false));
            }
            catch (ToolFailure rejected) { failure = rejected.Reason; }
            HostCheck.True(failure == expected, "expected=" + expected + " actual=" + failure);
        }
        Verify(null);
        List<(int At, string Reason)> controls = [];
        byte[] catcherCode = original.AsSpan(Offset("OS_OS_Runtime_RhpNewFastAllocation__FilterCatcher", 295), 295).ToArray();
        HostCheck.True(EfiVerify.TestFilteredCatchOffsets(catcherCode, 36, 37) &&
            !EfiVerify.TestFilteredCatchOffsets(catcherCode, 35, 36) && !EfiVerify.TestFilteredCatchOffsets(catcherCode, 36, 36),
            "original call returns at +36 and both actual catch LEAs resume at +37");
        foreach (int at in new[] { 31, 36, 37, 169, 286 })
        {
            byte[] changed = [.. catcherCode];
            changed[at] ^= 1;
            HostCheck.True(!EfiVerify.TestFilteredCatchOffsets(changed, 36, 37), "actual call/NOP/continuation/return-LEA mutation");
        }
        foreach (string method in new[] { "FilterThrower", "FilterCatcher", "FilterDecision", "FilterCatchEntered", "FilterContinued", "MatchRetainedExceptionType", "MatchFilteredExceptionType", "SelectFilteredCatch", "ValidateFilteredContinuation", "FilterContextMatches" })
            controls.Add((Offset("OS_OS_Runtime_RhpNewFastAllocation__" + method, 1), "filters_method_bytes_mismatch"));
        controls.Add((Offset("RhTypeCast_IsInstanceOfClass", 1), "filters_method_bytes_mismatch"));
        // Independent semantic controls for the reviewed .NET 11 callbacks:
        // the predicate selects RC3 profile3 and retains its result in saved EBX;
        // context equality visits all 30 qwords with an eight-byte stride.
        int predicate = Offset("OS_OS_Runtime_RhpNewFastAllocation__MatchFilteredExceptionType", 529),
            context = Offset("OS_OS_Runtime_RhpNewFastAllocation__FilterContextMatches", 144);
        HostCheck.True(original.AsSpan(predicate + 434, 5).SequenceEqual(new byte[] { 0xB9, 3, 0, 0, 0 }) &&
            original.AsSpan(predicate + 454, 6).SequenceEqual(new byte[] { 0x0F, 0xB6, 0x5D, 0xC1, 0x85, 0xDB }) &&
            original.AsSpan(predicate + 471, 2).SequenceEqual(new byte[] { 0x8B, 0xC3 }),
            "RC3 profile3 and preserved predicate result across retained-object validation");
        int selection = Offset("OS_OS_Runtime_RhpNewFastAllocation__SelectFilteredCatch", 3553);
        foreach (int profileAt in new[] { 739, 1023, 1816 })
        {
            HostCheck.True(original.AsSpan(selection + profileAt - 1, 5).SequenceEqual(new byte[] { 0xBA, 3, 0, 0, 0 }),
                "RC3 clause/unwind profile3 remains an exact unmasked operand");
            // XOR1 below changes the active3 back to legacy2; it must reject.
            controls.Add((selection + profileAt, "filters_method_bytes_mismatch"));
        }
        HostCheck.True(original.AsSpan(context + 96, 6).SequenceEqual(new byte[] { 0x41, 0xB8, 30, 0, 0, 0 }) &&
            original.AsSpan(context + 112, 9).SequenceEqual(new byte[] { 0x48, 0x83, 0xC2, 8, 0x41, 0xFF, 0xC8, 0x75, 0xED }),
            "complete 240-byte context comparison with bounded countdown");
        foreach (int at in new[] { predicate + 435, predicate + 455, predicate + 472, context + 98, context + 115, context + 120 })
            controls.Add((at, "filters_method_bytes_mismatch"));
        controls.Add((Offset("OS_OS_Runtime_RhpNewFastAllocation__FilterCatcher", 76) + 75, "filters_method_target_mismatch"));
        controls.Add((Offset("__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FilterCatcher", 11) + 10, "filters_table_mismatch"));
        controls.Add((Offset("_unwind0OS_OS_Runtime_RhpNewFastAllocation__FilterCatcher", 13) + 8, "filters_unwind_mismatch"));
        controls.Add((Offset("_unwind1OS_OS_Runtime_RhpNewFastAllocation__FilterCatcher", 7) + 6, "filters_unwind_mismatch"));
        controls.Add((Offset("CLROS_Filter_Invoke", 669) + 668, "filter_invoke_text_mismatch"));
        foreach (var (at, reason) in controls)
        {
            try
            {
                byte[] changed = [.. original];
                changed[at] ^= 1;
                File.WriteAllBytes(artifact, changed);
                JsonObject evidence = JsonNode.Parse(originalEvidence)!.AsObject();
                evidence["artifactSha256"] = ContractAudit.Hash(changed);
                File.WriteAllText(evidencePath, evidence.ToJsonString());
                Verify(reason);
            }
            finally { File.WriteAllBytes(artifact, original); File.WriteAllText(evidencePath, originalEvidence); }
        }
        foreach (var side in new[] { (".filter-catch.obj", "filters_object_identity_mismatch"), (".filter-invoke.obj", "filter_invoke_object_identity_mismatch") })
        {
            string path = artifact + side.Item1;
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                byte[] changed = [.. bytes];
                changed[0] ^= 1;
                File.WriteAllBytes(path, changed);
                Verify(side.Item2);
            }
            finally { File.WriteAllBytes(path, bytes); }
        }
        Verify(null);
        File.WriteAllText(log + ".controls", controls.Count + " rehashed PE controls and two original object mutations rejected; originals restored. No guest/transfer acceptance.\n");
    }

    private static string Full(string name) => "Enable" + name + "Probe";
    private static string[] Arguments(IEnumerable<string> names) => names.Select(name => "--" + Full(name)).ToArray();
    private static void Reject(string[] args, bool build, string reason)
    {
        string? failure = null;
        try
        {
            _ = EfiOptions.Parse(args, build).Profile();
        }
        catch (ToolFailure rejected) { failure = rejected.Reason; }
        HostCheck.True(failure == reason, "expected=" + reason + " actual=" + failure);
    }
    private static void Check(byte[] bytes, Func<byte[]?, bool> accept)
    {
        HostCheck.True(accept(bytes), "original metadata bytes");
        for (int at = 0; at < bytes.Length; at++)
        {
            byte[] changed = [.. bytes];
            changed[at] ^= 1;
            HostCheck.True(!accept(changed), "changed metadata byte " + at);
        }
        HostCheck.True(!accept(null) && !accept(bytes[..^1]) && !accept([.. bytes, 0]), "null and clipped/extended bytes");
    }
}
