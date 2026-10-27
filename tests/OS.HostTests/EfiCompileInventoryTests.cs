using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using OS.Tooling;

internal static class EfiCompileInventoryTests
{
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
            HostCheck.Equal("post_restore_evidence_count_mismatch", actual);
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

    [HostTest("EFI writable timing adds only the separately reviewed diagnostic helper")]
    private static void WritableTimingSource()
    {
        EfiProfile plain = EfiOptions.Parse(["--EnableM16027Probe"], true).Profile();
        EfiProfile timing = EfiOptions.Parse(["--EnableM16027TimingProbe"], true).Profile();
        const string include = @"..\OS.Kernel\Diagnostics\WritableStorageTiming.cs";
        string[] additions = EfiProjectAdmission.ExpectedCompileItems(timing)
            .Except(EfiProjectAdmission.ExpectedCompileItems(plain), StringComparer.Ordinal).ToArray();
        HostCheck.True(additions.SequenceEqual(new[] { include + @"|Diagnostics\WritableStorageTiming.cs" }, StringComparer.Ordinal));
        string source = File.ReadAllText(Path.GetFullPath(include, Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS")));
        HostCheck.True(EfiContracts.ReviewedSourceIncludes.Contains(include, StringComparer.Ordinal));
        HostCheck.True(EfiSourceAdmission.Inspect(source, include).Succeeded);
        HostCheck.True(!EfiSourceAdmission.Inspect(source + "\n// changed timing source\n", include).Succeeded);
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
        EfiOptions.Parse(["--EnableM10018FullProbe"], true).Profile()
    ];

    // The project declarations are an independent oracle for the shared helper.
    // Deliberately interpret only reviewed predicates; a new condition
    // requires an explicit test review instead of being silently treated as true.
    private static string[] DeclaredItems(EfiProfile profile)
    {
        var items = XDocument.Load(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/OS.csproj"))
            .Descendants("Compile").Where(item => item.Attribute("Include") is not null).ToArray();
        string Key(XElement item) => (string)item.Attribute("Include")! + "|" + ((string?)item.Attribute("Link") ?? "");
        HostCheck.True(items.Select(Key).SequenceEqual(EfiContracts.CompileItems, StringComparer.Ordinal));
        int unconditional = EfiProjectAdmission.ExpectedCompileItems(Profiles()[0]).Length;
        HostCheck.Equal(EfiContracts.CompileItems.Count - unconditional, items.Count(item => item.Attribute("Condition") is not null));
        return items.Where(item => (string?)item.Attribute("Condition") switch
        {
            null => true,
            "'$(EnableM09004Probe)' == 'true' or '$(EnableM09005Probe)' == 'true' or '$(EnableM09006Probe)' == 'true' or '$(EnableM09009Probe)' == 'true' or '$(EnableM09010Probe)' == 'true' or '$(EnableM16008Probe)' == 'true' or '$(EnableM07026Probe)' == 'true'" => profile.IsEnabled("EnableM09004Probe") || profile.IsEnabled("EnableM09005Probe") || profile.IsEnabled("EnableM09006Probe") || profile.IsEnabled("EnableM09009Probe") || profile.IsEnabled("EnableM09010Probe") || profile.IsEnabled("EnableM16008Probe") || profile.IsEnabled("EnableM07026Probe"),
            "'$(EnableM09005Probe)' == 'true' or '$(EnableM09006Probe)' == 'true' or '$(EnableM09009Probe)' == 'true' or '$(EnableM09010Probe)' == 'true' or '$(EnableM16008Probe)' == 'true' or '$(EnableM07026Probe)' == 'true'" => profile.IsEnabled("EnableM09005Probe") || profile.IsEnabled("EnableM09006Probe") || profile.IsEnabled("EnableM09009Probe") || profile.IsEnabled("EnableM09010Probe") || profile.IsEnabled("EnableM16008Probe") || profile.IsEnabled("EnableM07026Probe"),
            "'$(EnableM09006Probe)' == 'true' or '$(EnableM09009Probe)' == 'true' or '$(EnableM09010Probe)' == 'true' or '$(EnableM16008Probe)' == 'true' or '$(EnableM07026Probe)' == 'true'" => profile.IsEnabled("EnableM09006Probe") || profile.IsEnabled("EnableM09009Probe") || profile.IsEnabled("EnableM09010Probe") || profile.IsEnabled("EnableM16008Probe") || profile.IsEnabled("EnableM07026Probe"),
            "'$(EnableM09004Probe)' == 'true' or '$(EnableM09005Probe)' == 'true' or '$(EnableM09006Probe)' == 'true' or '$(EnableM09009Probe)' == 'true' or '$(EnableM09010Probe)' == 'true' or '$(EnableM16008Probe)' == 'true'" => profile.IsEnabled("EnableM09004Probe") || profile.IsEnabled("EnableM09005Probe") || profile.IsEnabled("EnableM09006Probe") || profile.IsEnabled("EnableM09009Probe") || profile.IsEnabled("EnableM09010Probe") || profile.IsEnabled("EnableM16008Probe"),
            "'$(EnableM09005Probe)' == 'true' or '$(EnableM09006Probe)' == 'true' or '$(EnableM09009Probe)' == 'true' or '$(EnableM09010Probe)' == 'true' or '$(EnableM16008Probe)' == 'true'" => profile.IsEnabled("EnableM09005Probe") || profile.IsEnabled("EnableM09006Probe") || profile.IsEnabled("EnableM09009Probe") || profile.IsEnabled("EnableM09010Probe") || profile.IsEnabled("EnableM16008Probe"),
            "'$(EnableM09006Probe)' == 'true' or '$(EnableM09009Probe)' == 'true' or '$(EnableM09010Probe)' == 'true'" => profile.IsEnabled("EnableM09006Probe") || profile.IsEnabled("EnableM09009Probe") || profile.IsEnabled("EnableM09010Probe"),
            "'$(EnableM09006Probe)' == 'true' or '$(EnableM09009Probe)' == 'true' or '$(EnableM09010Probe)' == 'true' or '$(EnableM16008Probe)' == 'true'" => profile.IsEnabled("EnableM09006Probe") || profile.IsEnabled("EnableM09009Probe") || profile.IsEnabled("EnableM09010Probe") || profile.IsEnabled("EnableM16008Probe"),
            "'$(EnableM09009Probe)' == 'true' or '$(EnableM09010Probe)' == 'true'" => profile.IsEnabled("EnableM09009Probe") || profile.IsEnabled("EnableM09010Probe"),
            "'$(EnableM09009Probe)' == 'true' or '$(EnableM09010Probe)' == 'true' or '$(EnableM16008Probe)' == 'true'" => profile.IsEnabled("EnableM09009Probe") || profile.IsEnabled("EnableM09010Probe") || profile.IsEnabled("EnableM16008Probe"),
            "'$(EnableM16008Probe)' == 'true'" => profile.IsEnabled("EnableM16008Probe"),
            "'$(EnableM16009Probe)' == 'true'" => profile.IsEnabled("EnableM16009Probe"),
            "'$(EnableM16011Probe)' == 'true'" => profile.IsEnabled("EnableM16011Probe"),
            "'$(EnableM10004Probe)' == 'true'" => profile.IsEnabled("EnableM10004Probe"),
            "'$(EnableM10017Probe)' == 'true'" => profile.IsEnabled("EnableM10017Probe"),
            "'$(EnableM10017Probe)' == 'true' or '$(EnableM10018Probe)' == 'true'" => profile.IsEnabled("EnableM10017Probe") || profile.IsEnabled("EnableM10018Probe"),
            "'$(EnableM16025Probe)' == 'true'" => profile.IsEnabled("EnableM16025Probe"),
            "'$(EnableM16027Probe)' == 'true'" => profile.IsEnabled("EnableM16027Probe"),
            "'$(EnableM16027TimingProbe)' == 'true'" => profile.IsEnabled("EnableM16027TimingProbe"),
            "'$(EnableM07026Probe)' == 'true'" => profile.IsEnabled("EnableM07026Probe"),
            string condition => throw new InvalidOperationException("Unreviewed compile condition: " + condition)
        }).Select(Key).ToArray();
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
