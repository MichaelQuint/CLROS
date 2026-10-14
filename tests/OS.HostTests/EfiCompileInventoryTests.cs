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
            // Keep requested properties fixed: the compile inventory alone must
            // reject another profile, even when its metadata claims this one.
            try { EfiMsbuild.VerifyEvaluation(Snapshot(DeclaredItems(other), requested), Snapshot(DeclaredItems(requested), requested)); }
            catch (ToolFailure failure) { actual = failure.Reason; }
            HostCheck.Equal("post_restore_evidence_count_mismatch", actual);
        }
    }

    private static EfiProfile[] Profiles() =>
    [
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool>()),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM09004Probe"] = true }),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM09005Probe"] = true }),
        EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM09006Probe"] = true })
    ];

    // The project declarations are an independent oracle for the shared helper.
    // Deliberately interpret only its three reviewed predicates; a new condition
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
            "'$(EnableM09004Probe)' == 'true' or '$(EnableM09005Probe)' == 'true' or '$(EnableM09006Probe)' == 'true'" => profile.IsEnabled("EnableM09004Probe") || profile.IsEnabled("EnableM09005Probe") || profile.IsEnabled("EnableM09006Probe"),
            "'$(EnableM09005Probe)' == 'true' or '$(EnableM09006Probe)' == 'true'" => profile.IsEnabled("EnableM09005Probe") || profile.IsEnabled("EnableM09006Probe"),
            "'$(EnableM09006Probe)' == 'true'" => profile.IsEnabled("EnableM09006Probe"),
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
        return new()
        {
            ["compileCount"] = items.Length, ["compileSha256"] = Hash(items),
            ["projectReferenceCount"] = 0, ["projectReferenceSha256"] = Hash([]),
            ["referenceCount"] = 0, ["referenceSha256"] = Hash([]),
            ["analyzerCount"] = 2, ["analyzerSha256"] = Hash(["analyzer-a", "analyzer-b"]),
            ["properties"] = new JsonObject
            {
                ["ClrosBootPolicy"] = profile.BootPolicy,
                ["EnableM09004Probe"] = profile.IsEnabled("EnableM09004Probe").ToString(),
                ["EnableM09005Probe"] = profile.IsEnabled("EnableM09005Probe").ToString(),
                ["EnableM09006Probe"] = profile.IsEnabled("EnableM09006Probe").ToString()
            }
        };
    }
}
