using System.Xml.Linq;
using System.Text.Json.Nodes;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Tooling;

internal static class M04UnhandledExceptionEfiIntegrationTests
{
    private const string Flag = "EnableM04UnhandledExceptionProbe";
    private const ulong SiblingRva = 0x54150, TableRva = 0x560C0;

    // Independent captured vectors from unhandled-native-geometry-r01.log.
    [HostTest("m04.021.unhandled.efi exact clause table controls", HostLane.Fast, 0)]
    private static void ClauseTable()
    {
        byte[] bytes = Convert.FromHexString("0214483A50410500");
        ExactBytes(bytes, b => EfiVerify.TestUnhandledEhTable(b, SiblingRva));
        foreach (ulong bad in new ulong[] { 0, SiblingRva - 1, SiblingRva + 1, uint.MaxValue, 0x100000000, ulong.MaxValue })
            HostCheck.True(!EfiVerify.TestUnhandledEhTable(bytes, bad), "wrong sibling RVA " + bad);
    }

    [HostTest("m04.021.unhandled.efi exact unwind controls", HostLane.Fast, 1)]
    private static void Unwind()
    {
        string[] vectors = ["01040100044200", "010502000552013000", "010502000552015004C0600500", "01040100044201"];
        for (int part = 0; part < vectors.Length; part++)
        {
            byte[] bytes = Convert.FromHexString(vectors[part]);
            int selected = part;
            ExactBytes(bytes, b => EfiVerify.TestUnhandledUnwindBytes(b, selected, TableRva));
            foreach (int bad in new[] { -1, 4, int.MinValue, int.MaxValue })
                HostCheck.True(!EfiVerify.TestUnhandledUnwindBytes(bytes, bad, TableRva), "invalid unwind part " + bad);
            for (int other = 0; other < vectors.Length; other++)
                if (other != part)
                    HostCheck.True(!EfiVerify.TestUnhandledUnwindBytes(bytes, other, TableRva), "wrong unwind part");
        }
        byte[] root = Convert.FromHexString(vectors[2]);
        foreach (ulong bad in new ulong[] { 0, TableRva - 1, TableRva + 1, uint.MaxValue, 0x100000000, ulong.MaxValue })
            HostCheck.True(!EfiVerify.TestUnhandledUnwindBytes(root, 2, bad), "wrong EH table RVA " + bad);
    }

    [HostTest("m04.021.unhandled.efi normalized code controls", HostLane.Fast, 2)]
    private static void Code()
    {
        string[] vectors = [
            "4883EC28E8876BFFFFCC",
            "534883EC30488BD9488B4B0848894C2428488D4C2428E815000000488BCBE8CDFFFFFF488B4424284883C4305BC3",
            "554883EC30488D6C2430E851FFFFFF488945F8488B45F84883C4305DC34883EC28E81A00000033C0488945F8488D05E0FFFFFF4883C428C3"];
        int[][] relocations = [[5], [23, 31], [11, 34]];
        int[] lengths = [10, 46, 56];
        for (int part = 0; part < vectors.Length; part++)
        {
            byte[] bytes = Convert.FromHexString(vectors[part]), original = [.. bytes];
            HostCheck.True(bytes.Length == lengths[part] && EfiVerify.TestUnhandledCode(bytes, part), "captured method shape");
            HostCheck.True(!EfiVerify.TestUnhandledCode(null, part) && !EfiVerify.TestUnhandledCode([.. bytes, 0], part), "null/extended code");
            for (int length = 0; length < bytes.Length; length++)
                HostCheck.True(!EfiVerify.TestUnhandledCode(bytes[..length], part), "truncated code " + length);
            for (int offset = 0; offset < bytes.Length; offset++)
            {
                bool rel32 = relocations[part].Any(field => offset >= field && offset < field + 4);
                byte[] changed = [.. bytes];
                changed[offset] ^= 1;
                // The production verifier binds each rel32 to its actual linked target.
                HostCheck.True(EfiVerify.TestUnhandledCode(changed, part) == rel32, "code mutation " + part + ":" + offset);
            }
            foreach (int bad in new[] { -1, 3, int.MinValue, int.MaxValue })
                HostCheck.True(!EfiVerify.TestUnhandledCode(bytes, bad), "invalid code part " + bad);
            for (int other = 0; other < vectors.Length; other++)
                if (other != part)
                    HostCheck.True(!EfiVerify.TestUnhandledCode(bytes, other), "wrong code part");
            HostCheck.True(bytes.AsSpan().SequenceEqual(original), "normalization preserves caller bytes");
        }
    }

    [HostTest("m04.021.unhandled.efi default off catalog and unwind boundary", HostLane.Fast, 3)]
    private static unsafe void Defaults()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var defaults = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj")).Descendants(Flag).ToArray();
        HostCheck.True(defaults.Length == 1 && defaults[0].Value == "false" &&
            defaults[0].Attribute("Condition")?.Value == "'$(EnableM04UnhandledExceptionProbe)' == ''", "explicit false default");
        HostCheck.True(!Probes.EnableM04UnhandledExceptionProbe && !BootSequence.IsUnhandledExceptionEnabled(), "host default off");
        var probe = Probes.GetM04UnhandledExceptionProbe();
        HostCheck.True(probe.Name == Flag && probe.Group == "runtime.exception" && !probe.EnabledByDefault && probe.Risk == ProbeRisk.Destructive);
        HostCheck.True(Probes.TryGetProbe(41, out var catalog) && catalog.Name == Flag && !catalog.EnabledByDefault &&
            catalog.Risk == ProbeRisk.Destructive, "catalog index 41");
        HostCheck.True(EfiContracts.DiagnosticProbes.Contains(Flag) &&
            EfiContracts.ProbeSymbols.TryGetValue(Flag, out var symbol) && symbol == "M04_UNHANDLED_EXCEPTION_PROBE", "shared diagnostic declaration");
        X64UnwindContext input = new()
        {
            Rip = 0x1234,
            Rsp = 0x10000,
            Rbx = 99
        };
        var result = BootSequence.RunUnhandledExceptionUnwind(default, default, null, 0, 0, null, 0, null, 0, input);
        HostCheck.True(!result.Succeeded && result.Status == X64FrameUnwindStatus.Uninitialized &&
            result.Caller.Equals(default(X64UnwindContext)), "disabled path publishes no unwind context");
    }

    [HostTest("m04.021.unhandled.efi fresh native geometry and export controls", HostLane.Native, 4)]
    private static void Native()
    {
        string root = HostToolProcess.FindRepositoryRoot(), name = "unhandled-controls-" + Guid.NewGuid().ToString("N"),
            artifact = Path.Combine(root, "artifacts/efi", name + ".EFI"), log = Path.Combine(root, "artifacts/unhandled-native", name + ".log");
        using StringWriter output = new();
        int code = EfiBuild.Run(["--EfiOutputPath", artifact, "--LogPath", log, .. UnhandledExceptionTranscriptTests.Arguments], output);
        string text = output.ToString();
        HostCheck.True(code == 0, text.Length > 4000 ? text[^4000..] : text);
        byte[] original = File.ReadAllBytes(artifact);
        string evidencePath = artifact + ".link.json", originalEvidence = File.ReadAllText(evidencePath), map = File.ReadAllText(artifact + ".link.map");
        EfiPeImage image = new(original);
        int Offset(string symbol, int length)
        {
            var symbols = EfiPeImage.Symbols(map, symbol);
            HostCheck.True(symbols.Count == 1);
            ulong rva = EfiPeImage.Hex(symbols[0].Groups[3].Value, "fixture address") - image.ImageBase;
            return image.Resolve(rva, (ulong)length, "fixture backing", "fixture ambiguity").Offset;
        }
        void Verify(string? expected = null)
        {
            string? failure = null;
            try
            {
                EfiVerify.Verify(EfiOptions.Parse(["--EfiArtifact", artifact, .. UnhandledExceptionTranscriptTests.Arguments], false));
            }
            catch (ToolFailure e)
            {
                failure = e.Reason;
            }
            HostCheck.True(failure == expected, "native expected=" + expected + " actual=" + failure);
        }
        Verify();
        int table = Offset("__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__Catcher", 8), catcher = Offset("OS_OS_Runtime_RhpNewFastAllocation__Catcher", 56),
            middle = Offset("OS_OS_Runtime_RhpNewFastAllocation__UnhandledMiddle", 46), thrower = Offset("OS_OS_Runtime_RhpNewFastAllocation__Thrower", 10),
            bridge = Offset("RhpThrowEx", 1032), predicate = Offset("RhTypeCast_IsInstanceOfException", 5),
            unwind = Offset("_unwind0OS_OS_Runtime_RhpNewFastAllocation__Catcher", 13), run = Offset("OS_OS_Runtime_RhpNewFastAllocation__RunUnhandledException", 996);
        foreach (var (at, reason) in new[]{(table,"unhandled_clause_table_mismatch"),(table+4,"unhandled_clause_table_mismatch"),
            (catcher+3,"unhandled_method_bytes_mismatch"),(catcher+11,"unhandled_method_call_mismatch"),
            (middle+4,"unhandled_method_bytes_mismatch"),(thrower+5,"unhandled_method_call_mismatch"),
            (bridge+330,"unhandled_selector_relocation"),(unwind+8,"unhandled_unwind_mismatch"),
            (predicate+1,"unhandled_predicate_export_call"),(run+944,"unhandled_predicate_import_call"),(run+937,"unhandled_predicate_argument_shape")})
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
            finally
            {
                File.WriteAllBytes(artifact, original);
                File.WriteAllText(evidencePath, originalEvidence);
            }
        }
        string sidecar = artifact + ".catch.obj";
        byte[] objectBytes = File.ReadAllBytes(sidecar);
        try
        {
            byte[] changed = [.. objectBytes];
            changed[0] ^= 1;
            File.WriteAllBytes(sidecar, changed);
            Verify("unhandled_object_identity_mismatch");
        }
        finally
        {
            File.WriteAllBytes(sidecar, objectBytes);
        }
        Verify();
        File.WriteAllText(log + ".controls", "fresh publication; eleven rehashed PE mutations and one object mutation rejected; originals restored and reverified\n");
    }

    private static void ExactBytes(byte[] bytes, Func<byte[]?, bool> admits)
    {
        HostCheck.True(admits(bytes) && !admits(null) && !admits([.. bytes, 0]), "exact/null/extended bytes");
        for (int length = 0; length < bytes.Length; length++)
            HostCheck.True(!admits(bytes[..length]), "truncated bytes " + length);
        for (int offset = 0; offset < bytes.Length; offset++)
        {
            byte[] changed = [.. bytes];
            changed[offset] ^= 1;
            HostCheck.True(!admits(changed), "byte mutation " + offset);
        }
    }
}
