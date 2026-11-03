using System.Xml;
using OS.Tooling;

internal static partial class CSharpCompilationReceiptTests
{
    [HostTest("compiler source order follows canonical linked paths without sorting or deduplication")]
    private static void LinkedSourceOrder()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string first = Path.Combine(root, "std", "first.cs");
        string second = Path.Combine(root, "std", "second.cs");
        string linked = Path.Combine(root, "src", "Shared", "..", "..", "std", "first.cs");
        CSharpProjectEvaluation.VerifyCompilerSourceOrder([first, second], [linked, second]);
        foreach (string[] changed in new[] { new[] { second, linked }, new[] { linked }, new[] { linked, linked, second },
            new[] { first, Path.Combine(root, "another", "second.cs") }, new[] { "std/first.cs", second } })
            Reject(() => CSharpProjectEvaluation.VerifyCompilerSourceOrder([first, second], changed), "source_catalog_compiler_source_order");
    }

    [HostTest("compiler references retain authenticated images aliases and reject changed bytes")]
    private static void ImmutableReference()
    {
        using var fixture = new ReceiptFixture(realReference: true);
        var receipt = fixture.Read();
        string path = ((XmlElement)fixture.Inputs.SelectSingleNode("/Inputs/Declared/File[@Kind='Reference']")!).GetAttribute("Path");
        var reference = receipt.ReadReference(path, Microsoft.CodeAnalysis.MetadataReferenceProperties.Assembly.WithAliases(["control"]));
        HostCheck.True(reference.Properties.Aliases.SequenceEqual(new[] { "control" }));
        File.WriteAllText(path, "changed after snapshot");
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("ReferenceSnapshot", references: [reference.WithAliases([])]);
        HostCheck.Equal(Microsoft.CodeAnalysis.TypeKind.Class, compilation.GetSpecialType(Microsoft.CodeAnalysis.SpecialType.System_Object).TypeKind);
        Reject(() => receipt.ReadReference(path, reference.Properties), "source_catalog_reference_changed");
        Reject(() => receipt.ReadReference(Path.Combine(fixture.Root, "absent.dll"), reference.Properties), "source_catalog_reference_not_recorded");
    }

    [HostTest("compiler receipt joins ordered sources compiler arguments and output bytes")]
    private static void CompleteJoin()
    {
        using var fixture = new ReceiptFixture();
        var receipt = fixture.Read();
        receipt.VerifyArtifacts(fixture.Artifacts);
        receipt.VerifySources(fixture.Sources);
        receipt.VerifyFilesUnchanged();
        Reject(() => receipt.VerifySources(fixture.Sources.Reverse().ToArray()), "source_catalog_receipt_source_snapshot");
        Reject(() => receipt.VerifySources(fixture.Sources[..1]), "source_catalog_receipt_source_snapshot");
        Reject(() => receipt.VerifyArtifacts(fixture.Artifacts with { AssemblySha256 = new string('0', 64) }), "source_catalog_receipt_artifact_mismatch");
        Reject(() => receipt.VerifyArtifacts(fixture.Artifacts with { SymbolsSha256 = new string('0', 64) }), "source_catalog_receipt_artifact_mismatch");
    }

    [HostTest("compiler receipt rejects changed options and every declared input category")]
    private static void ChangedContext()
    {
        using var fixture = new ReceiptFixture();
        foreach (string option in new[] { "/define:OTHER", "/optimize+", "/checked+", "/nullable:disable" })
            Reject(() => fixture.Read(arguments: [.. fixture.Arguments, option]), "source_catalog_receipt_arguments");
        foreach (XmlElement file in fixture.Inputs.SelectNodes("/Inputs/*/File")!)
        {
            string before = file.GetAttribute("Sha256");
            file.SetAttribute("Sha256", new string('0', 64));
            Reject(() => fixture.Read(), "source_catalog_receipt_inputs");
            file.SetAttribute("Sha256", before);
        }
        var receipt = fixture.Read();
        foreach (XmlElement file in fixture.Inputs.SelectNodes("/Inputs/*/File")!)
        {
            string path = file.GetAttribute("Path");
            byte[] before = File.ReadAllBytes(path);
            DateTime timestamp = File.GetLastWriteTimeUtc(path);
            byte[] changed = before.ToArray();
            changed[0] ^= 1;
            File.WriteAllBytes(path, changed);
            File.SetLastWriteTimeUtc(path, timestamp);
            Reject(receipt.VerifyFilesUnchanged, "source_catalog_receipt_input_changed");
            File.WriteAllBytes(path, before);
            File.SetLastWriteTimeUtc(path, timestamp);
        }
        receipt.VerifyFilesUnchanged();
    }

    [HostTest("compiler receipt rejects missing malformed and substituted receipt structure")]
    private static void InvalidReceipt()
    {
        using var fixture = new ReceiptFixture();
        byte[] original = File.ReadAllBytes(fixture.Path);
        foreach ((string attribute, string value, string reason) in new[]
        {
            ("Version", "2", "source_catalog_receipt_schema"),
            ("Producer", "UnreviewedProducer", "source_catalog_receipt_schema"),
            ("InputSha256", new string('0', 64), "source_catalog_receipt_inputs")
        })
        {
            var modified = new XmlDocument { XmlResolver = null };
            modified.Load(fixture.Path);
            modified.DocumentElement!.SetAttribute(attribute, value);
            modified.Save(fixture.Path);
            Reject(() => fixture.Read(), reason);
            File.WriteAllBytes(fixture.Path, original);
        }
        File.WriteAllText(fixture.Path, "<!DOCTYPE HostCompilationReceipt [<!ENTITY external SYSTEM 'file:///unread'>]><HostCompilationReceipt>&external;</HostCompilationReceipt>");
        try
        {
            fixture.Read();
            throw new InvalidOperationException("expected prohibited DTD");
        }
        catch (XmlException) { }
        File.Delete(fixture.Path);
        Reject(() => fixture.Read(), "source_catalog_receipt_missing");
        Reject(() => CSharpCompilationReceipt.Read(fixture.Root, "", "", fixture.Arguments), "source_catalog_receipt_discovery_missing");
    }

    private static void Reject(Action action, string reason)
    {
        try { action(); }
        catch (InvalidOperationException error) when (error.Message.StartsWith(reason, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException("expected rejection " + reason);
    }
}
