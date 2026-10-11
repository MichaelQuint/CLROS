using System.Xml.Linq;
using System.Text.Json.Nodes;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Tooling;

internal static class M04FrameDispatchEfiIntegrationTests
{
    private const string Flag = "EnableM04FrameDispatchProbe";
    private const ulong SiblingRva = 0x5AA28, BaseRva = 0x5A9E8, MiddleTableRva = 0x5CA30, CatcherTableRva = 0x5CA34;
    private static readonly string[] Flags = [.. RuntimeProfilePortTests.RethrowFlags[..^1], "M04FrameDispatch"];

    // Independent original linked bytes from frame-native-geometry-r01.log.
    [HostTest("m04.021.frames.efi exact clause table controls", HostLane.Fast, 0)]
    private static void ClauseTables()
    {
        byte[] middle = Convert.FromHexString("02283262"), catcher = Convert.FromHexString("044A509628AA05004A50C0E8A90500");
        ExactBytes(middle, b => EfiVerify.TestFrameDispatchEhTable(b, 0, SiblingRva, BaseRva));
        ExactBytes(catcher, b => EfiVerify.TestFrameDispatchEhTable(b, 1, SiblingRva, BaseRva));
        foreach (int bad in new[] { -1, 2, int.MinValue, int.MaxValue })
        {
            HostCheck.True(!EfiVerify.TestFrameDispatchEhTable(middle, bad, SiblingRva, BaseRva) &&
                !EfiVerify.TestFrameDispatchEhTable(catcher, bad, SiblingRva, BaseRva), "invalid EH part " + bad);
        }
        HostCheck.True(!EfiVerify.TestFrameDispatchEhTable(middle, 1, SiblingRva, BaseRva) &&
            !EfiVerify.TestFrameDispatchEhTable(catcher, 0, SiblingRva, BaseRva), "distinct physical tables");
        foreach (ulong bad in new ulong[] { 0, SiblingRva - 1, SiblingRva + 1, BaseRva, uint.MaxValue, 0x100000000, ulong.MaxValue })
            HostCheck.True(!EfiVerify.TestFrameDispatchEhTable(catcher, 1, bad, BaseRva), "wrong sibling RVA " + bad);
        foreach (ulong bad in new ulong[] { 0, BaseRva - 1, BaseRva + 1, SiblingRva, uint.MaxValue, 0x100000000, ulong.MaxValue })
            HostCheck.True(!EfiVerify.TestFrameDispatchEhTable(catcher, 1, SiblingRva, bad), "wrong base RVA " + bad);
    }

    [HostTest("m04.021.frames.efi exact unwind controls", HostLane.Fast, 1)]
    private static void Unwind()
    {
        string[] vectors = ["01040100044200", "01050200055201500430CA0500", "01040100044201",
            "010603000662023001500434CA0500", "01040100044201", "01040100044201"];
        for (int part = 0; part < vectors.Length; part++)
        {
            byte[] bytes = Convert.FromHexString(vectors[part]);
            int selected = part;
            ulong table = part == 1 ? MiddleTableRva : CatcherTableRva;
            ExactBytes(bytes, b => EfiVerify.TestFrameDispatchUnwindBytes(b, selected, table));
            foreach (int bad in new[] { -1, 6, int.MinValue, int.MaxValue })
                HostCheck.True(!EfiVerify.TestFrameDispatchUnwindBytes(bytes, bad, table), "invalid unwind part " + bad);
            for (int other = 0; other < vectors.Length; other++)
                HostCheck.True(EfiVerify.TestFrameDispatchUnwindBytes(bytes, other, table) == (vectors[other] == vectors[part]),
                    "exact unwind contribution/shared leaf " + part + ":" + other);
            // Only root records contain a table RVA; the three identical funclet records do not.
            if (part is 1 or 3)
                foreach (ulong bad in new ulong[] { 0, table - 1, table + 1, uint.MaxValue, 0x100000000, ulong.MaxValue })
                    HostCheck.True(!EfiVerify.TestFrameDispatchUnwindBytes(bytes, part, bad), "wrong EH table RVA " + bad);
        }
    }

    [HostTest("m04.021.frames.efi normalized code controls", HostLane.Fast, 2)]
    private static void Code()
    {
        string[] vectors = [
            "4883EC28E87769FFFFCC",
            "554883EC30488D6C243033C0488945F848894D10E8D7FFFFFF90488D55F8488B4D10E879010000488B45F84883C4305DC34883EC28488D55F8488B4D10E85E010000904883C428C3",
            "55534883EC38488D6C244033C0488945E848894D1033D2488955F0488D55E8E87C00000090488B4D10E882FFFFFF90488B4D10488B55F04C8B45E8E8F0030000488B45F04883C4385B5DC34883EC28E83C000000488D05D4FFFFFF4883C428C34883EC28488BD9488D55E8488BCBE84D020000488B4B0848FFC148894DF0488D05AAFFFFFF4883C428C3"];
        int[] lengths = [10, 72, 138];
        int[][] callRel32 = [[5], [21, 35, 62], [32, 42, 60, 80, 111]];
        for (int part = 0; part < vectors.Length; part++)
        {
            byte[] bytes = Convert.FromHexString(vectors[part]), original = [.. bytes];
            HostCheck.True(bytes.Length == lengths[part] && EfiVerify.TestFrameDispatchCode(bytes, part), "captured method geometry");
            HostCheck.True(!EfiVerify.TestFrameDispatchCode(null, part) && !EfiVerify.TestFrameDispatchCode([.. bytes, 0], part), "null/extended code");
            for (int length = 0; length < bytes.Length; length++)
                HostCheck.True(!EfiVerify.TestFrameDispatchCode(bytes[..length], part), "truncated code " + part + ":" + length);
            foreach (int field in callRel32[part]) HostCheck.True(bytes[field - 1] == 0xE8, "independent call field position");
            for (int offset = 0; offset < bytes.Length; offset++)
            {
                bool exempt = callRel32[part].Any(field => offset >= field && offset < field + 4);
                byte[] changed = [.. bytes]; changed[offset] ^= 1; byte[] before = [.. changed];
                // Production native verification independently binds all call rel32 targets.
                HostCheck.True(EfiVerify.TestFrameDispatchCode(changed, part) == exempt, "code mutation " + part + ":" + offset);
                HostCheck.True(changed.AsSpan().SequenceEqual(before), "mutation normalization preserves caller input");
            }
            foreach (int bad in new[] { -1, 3, int.MinValue, int.MaxValue })
                HostCheck.True(!EfiVerify.TestFrameDispatchCode(bytes, bad), "invalid code part " + bad);
            for (int other = 0; other < vectors.Length; other++) if (other != part)
                HostCheck.True(!EfiVerify.TestFrameDispatchCode(bytes, other), "wrong method part");
            HostCheck.True(bytes.AsSpan().SequenceEqual(original), "original normalization preserves caller input");
        }
    }

    [HostTest("m04.021.frames.efi default off catalog and unwind boundary", HostLane.Fast, 3)]
    private static unsafe void Defaults()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var defaults = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj")).Descendants(Flag).ToArray();
        HostCheck.True(defaults.Length == 1 && defaults[0].Value == "false" &&
            defaults[0].Attribute("Condition")?.Value == "'$(EnableM04FrameDispatchProbe)' == ''", "explicit false default");
        HostCheck.True(!Probes.EnableM04FrameDispatchProbe && !BootSequence.IsFrameDispatchEnabled(), "host default off");
        var probe = Probes.GetM04FrameDispatchProbe();
        HostCheck.True(probe.Name == Flag && probe.Group == "runtime.exception" && !probe.EnabledByDefault && probe.Risk == ProbeRisk.Destructive);
        HostCheck.True(Probes.TryGetProbe(42, out var catalog) && catalog.Name == Flag && !catalog.EnabledByDefault &&
            catalog.Risk == ProbeRisk.Destructive, "catalog index 42");
        HostCheck.True(EfiContracts.DiagnosticProbes.Contains(Flag) &&
            EfiContracts.ProbeSymbols.TryGetValue(Flag, out var symbol) && symbol == "M04_FRAME_DISPATCH_PROBE", "shared flag/symbol declaration");
        X64UnwindContext input = new() { Rip = 0x1234, Rsp = 0x10000, Rbx = 99, Xmm6 = new() { Low = 123, High = 456 } };
        var result = BootSequence.RunFrameDispatchUnwind(default, default, null, 0, 0, null, 0, null, 0, input);
        HostCheck.True(!result.Succeeded && result.Status == X64FrameUnwindStatus.Uninitialized &&
            result.Caller.Equals(default(X64UnwindContext)), "disabled path publishes zero caller context");
    }

    [HostTest("m04.021.frames.efi shared profile prerequisites and conflicts", HostLane.Fast, 4)]
    private static void Profiles()
    {
        foreach (bool build in new[] { false, true })
        {
            var profile = EfiOptions.Parse(Arguments(Flags), build).Profile();
            HostCheck.True(profile.BootPolicy == "Diagnostic" && Flags.All(profile.IsEnabled), "actual GC prerequisite chain admitted");
            foreach (string missing in Flags[..^1])
                Reject(Arguments(Flags.Where(name => name != missing)), build,
                    missing == "M04GcStaticRootMaterialization" ? "m04_frames_requires_gc_profile" : null);
            foreach (string collision in new[] { "M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch",
                "M04X64UnwindMetadata", "M04SimpleThrowCatch", "M04SingleFinallyCatch", "M04Rethrow", "M04NestedFinallyCatch", "M04UnhandledException" })
            {
                string[] prerequisites = collision switch
                {
                    "M04InterfaceDispatch" => ["M04ClassConstructorExecution"],
                    "M04X64UnwindMetadata" => ["M04TypeManagerModuleRegistration"],
                    _ => []
                };
                Reject(Arguments([.. Flags, .. prerequisites, collision]), build, "m04_frames_profile_conflict");
            }
            Reject(["--boot-policy", "VerifiedExit", .. Arguments(Flags)], build, "clros_boot_policy_probe_conflict");
        }
    }

    [HostTest("m04.021.frames.efi fresh native geometry and route controls", HostLane.Native, 5)]
    private static void Native()
    {
        string root = HostToolProcess.FindRepositoryRoot(), name = "frame-dispatch-controls-" + Guid.NewGuid().ToString("N"),
            artifact = Path.Combine(root, "artifacts/efi", name + ".EFI"), log = Path.Combine(root, "artifacts/frame-dispatch-native", name + ".log");
        using StringWriter output = new();
        int code = EfiBuild.Run(["--EfiOutputPath", artifact, "--LogPath", log, .. Arguments(Flags)], output);
        string text = output.ToString();
        HostCheck.True(code == 0, text.Length > 4000 ? text[^4000..] : text);
        byte[] original = File.ReadAllBytes(artifact);
        string evidencePath = artifact + ".link.json", originalEvidence = File.ReadAllText(evidencePath), map = File.ReadAllText(artifact + ".link.map");
        EfiPeImage image = new(original);
        string serializer = JsonNode.Parse(originalEvidence)!["serializerLinkedObjectPath"]!.GetValue<string>();
        image.VerifyMapAndRuntime(map, Path.GetFileName(serializer));
        ulong Address(string symbol)
        {
            var symbols = EfiPeImage.Symbols(map, symbol); HostCheck.True(symbols.Count == 1, "unique fresh symbol " + symbol);
            return EfiPeImage.Hex(symbols[0].Groups[3].Value, "fixture address") - image.ImageBase;
        }
        int Offset(string symbol, int length) => image.Resolve(Address(symbol), (ulong)length, "fixture backing", "fixture ambiguity").Offset;
        void Verify(string? expected = null)
        {
            string? failure = null;
            try { EfiVerify.Verify(EfiOptions.Parse(["--EfiArtifact", artifact, .. Arguments(Flags)], false)); }
            catch (ToolFailure e) { failure = e.Reason; }
            HostCheck.True(failure == expected, "native expected=" + expected + " actual=" + failure);
        }
        Verify();
        List<(int At, string Reason)> controls = [];
        string[] methods = ["FrameThrower", "FrameMiddle", "FrameCatcher"];
        int[] lengths = [10, 72, 138];
        int[][] callFields = [[5], [21, 35, 62], [32, 42, 60, 80, 111]];
        for (int part = 0; part < methods.Length; part++)
        {
            string symbol = "OS_OS_Runtime_RhpNewFastAllocation__" + methods[part];
            int at = Offset(symbol, lengths[part]); ulong rva = Address(symbol);
            controls.Add((at + 3, "frames_method_bytes_mismatch"));
            foreach (int field in callFields[part]) controls.Add((at + field, "frames_method_call_mismatch"));
            var rows = image.RuntimeFunctions.Where(row => row.Begin < rva + (ulong)lengths[part] && row.End > rva).ToArray();
            HostCheck.True(rows.Length == part + 1, "all six physical unwind contributions");
            for (int i = 0; i < rows.Length; i++)
            {
                // Keep the generic unwind header valid; exercise the frame-specific prolog check.
                controls.Add((rows[i].UnwindOffset + 1, "frames_unwind_mismatch"));
                if (part != 0 && i == 0) controls.Add((rows[i].UnwindOffset + (part == 1 ? 9 : 11), "frames_unwind_mismatch"));
            }
        }
        int middle = Offset("__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FrameMiddle", 4),
            catcher = Offset("__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__FrameCatcher", 15),
            predicate = Offset("RhTypeCast_IsInstanceOfException", 5),
            run = Offset("OS_OS_Runtime_RhpNewFastAllocation__RunFrameDispatch", 1062) + 1002;
        foreach (int at in new[] { middle, catcher, catcher + 4, catcher + 11 }) controls.Add((at, "frames_clause_table_mismatch"));
        controls.Add((predicate, "frames_predicate_export_shape"));
        controls.Add((predicate + 1, "frames_predicate_export_call"));
        foreach (int at in new[] { 0, 18, 37 }) controls.Add((run + at, "frames_predicate_argument_shape"));
        controls.Add((run + 51, "frames_predicate_null_shape"));
        foreach (int field in new[] { 7, 25, 44, 56 }) controls.Add((run + field, "frames_predicate_import_call"));
        foreach (var (at, reason) in controls)
        {
            try
            {
                byte[] changed = [.. original]; changed[at] ^= 1; File.WriteAllBytes(artifact, changed);
                JsonObject evidence = JsonNode.Parse(originalEvidence)!.AsObject(); evidence["artifactSha256"] = ContractAudit.Hash(changed);
                File.WriteAllText(evidencePath, evidence.ToJsonString()); Verify(reason);
            }
            finally { File.WriteAllBytes(artifact, original); File.WriteAllText(evidencePath, originalEvidence); }
        }
        string sidecar = artifact + ".frames.obj"; byte[] objectBytes = File.ReadAllBytes(sidecar);
        try { byte[] changed = [.. objectBytes]; changed[0] ^= 1; File.WriteAllBytes(sidecar, changed); Verify("frames_object_identity_mismatch"); }
        finally { File.WriteAllBytes(sidecar, objectBytes); }
        Verify();
        File.WriteAllText(log + ".controls", "fresh publication; " + controls.Count +
            " rehashed PE controls (six logical unwind contributions, shared records allowed) and one object mutation rejected; originals restored and reverified\n");
    }

    private static string[] Arguments(IEnumerable<string> names) => names.Select(name => "--Enable" + name + "Probe").ToArray();
    private static void Reject(string[] arguments, bool build, string? expected = null)
    {
        string? reason = null;
        try { _ = EfiOptions.Parse(arguments, build).Profile(); }
        catch (ToolFailure failure) { reason = failure.Reason; }
        HostCheck.True(reason is not null && (expected is null || reason == expected), "profile rejection expected=" + expected + " actual=" + reason);
    }
    private static void ExactBytes(byte[] bytes, Func<byte[]?, bool> admits)
    {
        byte[] original = [.. bytes];
        HostCheck.True(admits(bytes) && !admits(null) && !admits([.. bytes, 0]), "exact/null/extended bytes");
        for (int length = 0; length < bytes.Length; length++) HostCheck.True(!admits(bytes[..length]), "truncated bytes " + length);
        for (int offset = 0; offset < bytes.Length; offset++)
        {
            byte[] changed = [.. bytes]; changed[offset] ^= 1;
            HostCheck.True(!admits(changed), "byte mutation " + offset);
        }
        HostCheck.True(bytes.AsSpan().SequenceEqual(original), "metadata input preserved");
    }
}
