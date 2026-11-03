using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using OS.Tooling;

internal static class EfiCompileInventoryTests
{
    [HostTest("EFI serializer admission checks actual ordered generator inputs and complete source closure")]
    private static void SerializerInventory()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string dotnet = HostTools.FindExecutable(null, "", "dotnet")
            ?? throw new InvalidOperationException("dotnet host missing");
        var version = ProcessRunner.RunAsync(new ProcessRequest(dotnet, ["--version"], root)
        {
            Timeout = TimeSpan.FromSeconds(30), OutputCharacterLimit = 65536
        }).GetAwaiter().GetResult();
        string[] lines = version.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        HostCheck.True(version.Succeeded && EfiPreflight.SdkVersionFailure("ok", lines) == "ok");
        string sdk = Path.Combine(Path.GetDirectoryName(dotnet)!, "sdk", lines.Single());
        var admission = EfiProjectAdmission.InspectStatic(root);
        HostCheck.True(admission.Succeeded, admission.Reason + ":" + admission.Detail);
        EfiProjectAdmission.Evaluate(root, "Release", EfiProfile.Create("Diagnostic", new Dictionary<string, bool>()), dotnet, sdk);
        var closure = EfiEvidence.SourceClosurePaths(root);
        HostCheck.True(closure.Contains(".editorconfig"));
        foreach (string input in EfiSerializerInputs.BuildInputs)
            HostCheck.True(closure.Contains("tools/OS.X64TextSerializerGenerator/" + input), input);
    }

    [HostTest("EFI evaluated inventory follows declared off cooperative timer and lifecycle profiles")]
    private static void ProfileInventory()
    {
        foreach (EfiProfile profile in Profiles())
        {
            string[] declared = DeclaredItems(profile);
            string[] selected = EfiProjectAdmission.ExpectedCompileItems(profile);
            HostCheck.True(declared.SequenceEqual(selected, StringComparer.Ordinal), profile.BootPolicy);
            HostCheck.Equal(Hash(declared), EfiEvidence.HashLines(selected));
            HostCheck.Equal(selected.Length, selected.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [HostTest("EFI receipts reject missing extra duplicate reordered and redirected compile links")]
    private static void ReceiptMutations()
    {
        foreach (EfiProfile profile in Profiles())
        {
            string[] selected = DeclaredItems(profile);
            JsonObject expected = Snapshot(EfiProjectAdmission.ExpectedCompileItems(profile), profile);
            EfiMsbuild.VerifyEvaluation(Snapshot(selected, profile), expected);
            Reject(selected.Skip(1), "post_restore_evidence_count_mismatch");
            Reject(selected.Append("unreviewed.cs|unreviewed.cs"), "post_restore_evidence_count_mismatch");
            Reject(selected.SkipLast(1).Append(selected[0]), "post_restore_evidence_hash_mismatch");
            Reject(selected.Reverse(), "post_restore_evidence_hash_mismatch");
            Reject(selected.Select((item, index) => index == 0 ? item + ".redirected" : item), "post_restore_evidence_hash_mismatch");

            void Reject(IEnumerable<string> items, string reason)
            {
                string? actual = null;
                try { EfiMsbuild.VerifyEvaluation(Snapshot(items, profile), expected); }
                catch (ToolFailure failure) { actual = failure.Reason; }
                HostCheck.Equal(reason, actual);
            }
        }
    }

    [HostTest("EFI receipts cannot substitute another profile compile inventory")]
    private static void CrossProfileReceipts()
    {
        EfiProfile[] profiles = Profiles();
        foreach (EfiProfile requested in profiles)
        foreach (EfiProfile other in profiles.Where(profile => !ReferenceEquals(profile, requested)))
        {
            string? actual = null;
            if (DeclaredItems(other).SequenceEqual(DeclaredItems(requested), StringComparer.Ordinal))
            {
                bool waitPair = requested.IsEnabled("M09009") && other.IsEnabled("M09010") ||
                    requested.IsEnabled("M09010") && other.IsEnabled("M09009");
                bool signalPair = requested.IsEnabled("M16009") && other.IsEnabled("M16009");
                bool diskPair=requested.IsEnabled("M10018") && other.IsEnabled("M10018");
                HostCheck.True(waitPair || signalPair || diskPair, "only reviewed wait/TLS or signal/runtime leaves share an inventory");
                try { EfiMsbuild.VerifyEvaluation(Snapshot(DeclaredItems(other), other), Snapshot(DeclaredItems(requested), requested)); }
                catch (ToolFailure failure) { actual = failure.Reason; }
                HostCheck.Equal("post_restore_evidence_property_mismatch", actual);
                continue;
            }
            // Keep requested properties fixed: the compile inventory alone must
            // reject another profile, even when its metadata claims this one.
            try { EfiMsbuild.VerifyEvaluation(Snapshot(DeclaredItems(other), requested), Snapshot(DeclaredItems(requested), requested)); }
            catch (ToolFailure failure) { actual = failure.Reason; }
            HostCheck.Equal(DeclaredItems(other).Length == DeclaredItems(requested).Length
                ? "post_restore_evidence_hash_mismatch" : "post_restore_evidence_count_mismatch", actual);
        }
    }

    [HostTest("EFI writable leaf admits only its independently declared reviewed source bytes")]
    private static void WritableSources()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        EfiProfile parent = EfiOptions.Parse(["--EnableM16025Probe"], true).Profile();
        EfiProfile writable = EfiOptions.Parse(["--EnableM16027Probe"], true).Profile();
        string[] additions = EfiProjectAdmission.ExpectedCompileItems(writable)
            .Except(EfiProjectAdmission.ExpectedCompileItems(parent), StringComparer.Ordinal).ToArray();
        var declared = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj")).Descendants("Compile")
            .Where(item => (string?)item.Attribute("Condition") == "'$(EnableM16027Probe)' == 'true'")
            .Select(item => (string)item.Attribute("Include")! + "|" + ((string?)item.Attribute("Link") ?? "")).ToArray();
        HostCheck.True(declared.Length > 0 && declared.SequenceEqual(additions, StringComparer.Ordinal));
        foreach (string item in additions)
        {
            string include = item[..item.IndexOf('|')];
            HostCheck.True(EfiContracts.ReviewedSourceIncludes.Contains(include, StringComparer.Ordinal));
            HostCheck.True(EfiContracts.SourceAdmissionReasons.ContainsKey(include) && EfiContracts.SourceIdentities.ContainsKey(include));
            string source = File.ReadAllText(Path.GetFullPath(include, Path.Combine(root, "src/OS")));
            foreach (string valid in new[] { source, source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal) })
                HostCheck.True(EfiSourceAdmission.Inspect(valid, include).Succeeded, include);
            var changed = EfiSourceAdmission.Inspect(source + "\n// unreviewed writable source mutation\n", include);
            HostCheck.True(!changed.Succeeded && changed.Reason == "forbidden_active_source", include);
        }
    }

    [HostTest("EFI writable timing adds only its complete reviewed diagnostic type family")]
    private static void WritableTimingSource()
        => TimingSources("EnableM16027Probe", "EnableM16027TimingProbe", "OS.Kernel.Diagnostics.WritableStorageTiming");

    [HostTest("EFI read timing includes its complete verification model only in the timing profile")]
    private static void ReadTimingSource()
        => TimingSources("EnableM10018FullProbe", "EnableM10018TimingProbe",
            "OS.Kernel.Compatibility.LinuxX64.LinuxThreadServices+DiskVerificationMeasurements");

    private static void TimingSources(string parentFlag, string timingFlag, string modelName)
    {
        var parent = EfiOptions.Parse(["--" + parentFlag], true).Profile();
        var timing = EfiOptions.Parse(["--" + timingFlag], true).Profile();
        string[] additions = DeclaredItems(timing).Except(DeclaredItems(parent), StringComparer.Ordinal).ToArray();
        var catalog = HostSourceCatalog.Efi(timing);
        var family = catalog.TypeSources(modelName);
        string root = HostToolProcess.FindRepositoryRoot();
        string projectDirectory = Path.Combine(root, "src", "OS");
        string Include(string item) => item[..item.IndexOf('|')];
        string PhysicalPath(string item) => Path.GetFullPath(Include(item), projectDirectory);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        HostCheck.True(family.Count > 0 && additions.Select(PhysicalPath).SequenceEqual(family.Select(document => document.Path), comparer),
            "the timing-only Compile delta must be exactly the complete diagnostic type family");
        HostCheck.True(HostSourceCatalog.Efi(parent).Compilation.GetTypeByMetadataName(modelName) is null);
        var declared = XDocument.Load(Path.Combine(projectDirectory, "OS.csproj")).Descendants("Compile").ToArray();
        foreach (string item in additions)
        {
            string include = Include(item);
            var document = family.Single(source => comparer.Equals(source.Path, PhysicalPath(item)));
            var declaration = declared.Single(element => (string?)element.Attribute("Include") == include);
            HostCheck.Equal("'$(" + timingFlag + ")' == 'true'", (string)declaration.Attribute("Condition")!);
            HostCheck.True(EfiContracts.ReviewedSourceIncludes.Contains(include, StringComparer.Ordinal));
            HostCheck.True(EfiContracts.SourceAdmissionReasons.ContainsKey(include) && EfiContracts.SourceIdentities.ContainsKey(include));
            HostCheck.True(EfiSourceAdmission.Inspect(document.Text, include).Succeeded, include);
            HostCheck.True(!EfiSourceAdmission.Inspect(document.Text + "\n// changed timing source\n", include).Succeeded, include);
            foreach (EfiProfile profile in Profiles())
                HostCheck.Equal(profile.IsEnabled(timingFlag), DeclaredItems(profile).Contains(item, StringComparer.Ordinal));
        }
    }
    private static EfiProfile[] Profiles() =>
    [
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool>()),
        EfiOptions.Parse(["--EnableM07026Probe"], true).Profile(),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM09004Probe"] = true }),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM09005Probe"] = true }),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM09006Probe"] = true }),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM09009Probe"] = true }),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM09010Probe"] = true }),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM16008Probe"] = true }),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM16009Probe"] = true }),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM09011Probe"] = true }),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM09012Probe"] = true }),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM16010Probe"] = true }),
        EfiOptions.Parse(["--EnableM16011Probe"], true).Profile(),
        EfiOptions.Parse(["--EnableM16012Probe"], true).Profile(),
        EfiOptions.Parse(["--EnableM10004Probe"], true).Profile(),
        EfiOptions.Parse(["--EnableM10017Probe"], true).Profile(),
        EfiOptions.Parse(["--EnableM16025Probe"], true).Profile(),
        EfiOptions.Parse(["--EnableM16027Probe"], true).Profile(),
        EfiOptions.Parse(["--EnableM16027TimingProbe"], true).Profile(),
        EfiOptions.Parse(["--EnableM10018Probe"], true).Profile(),
        EfiOptions.Parse(["--EnableM10018FullProbe"], true).Profile(),
        EfiOptions.Parse(["--EnableM10018TimingProbe"], true).Profile()
    ];

    // Actual MSBuild evaluation supplies ordered items and Link metadata. The
    // native admission inventory remains an independent reviewed expectation.
    // This avoids maintaining a second interpreter for project conditions here.
    private static string[] DeclaredItems(EfiProfile profile)
    {
        var evaluation = JsonNode.Parse(HostSourceCatalog.EfiEvaluation(profile).EvaluationJson)!;
        return evaluation["Items"]!["Compile"]!.AsArray().Select(item =>
            item!["Identity"]!.GetValue<string>() + "|" + (item["Link"]?.GetValue<string>() ?? "")).ToArray();
    }
    private static string Hash(IEnumerable<string> items) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(items.Select(item => item + "\n")))));

    // Minimal representative evaluation receipt for the production verifier.
    // Real MSBuild and native artifact admission remain separate native checks.
    private static JsonObject Snapshot(IEnumerable<string> inventory, EfiProfile profile)
    {
        string[] items = inventory.ToArray();
        JsonObject properties = new() { ["ClrosBootPolicy"] = profile.BootPolicy };
        foreach (var flag in profile.Flags) properties[flag.Key] = flag.Value.ToString();
        return new()
        {
            ["compileCount"] = items.Length, ["compileSha256"] = Hash(items),
            ["projectReferenceCount"] = 0, ["projectReferenceSha256"] = Hash([]),
            ["referenceCount"] = 0, ["referenceSha256"] = Hash([]),
            ["analyzerCount"] = 2, ["analyzerSha256"] = Hash(["analyzer-a", "analyzer-b"]),
            ["properties"] = properties
        };
    }
}
