using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

internal static class M03FirmwareAdapterRevocationEfiIntegrationTests
{
    private const string Flag = "EnableM03FirmwareAdapterRevocationProbe";
    private static string Root => HostToolProcess.FindRepositoryRoot();

    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }

    [HostTest("m03.008.efi retained original authority and default-off child profile", HostLane.Fast, 0)]
    private static void Source()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/OS/OS.csproj"));
        Need(project.Descendants(Flag).Single().Value == "false", "disabled default");
        Need(project.Descendants("Error").Count(x => (string?)x.Attribute("Condition") ==
            "'$(EnableM03FirmwareAdapterRevocationProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'") == 1, "exact first-exit dependency");
        var source = HostSourceCatalog.Efi(OS.Tooling.EfiOptions.Parse(
            ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe",
                "--EnableAcpiPhysicalDiscoveryProbe", "--EnableM03FirstRealExitBootServicesProbe", "--" + Flag], true).Profile());
        const string owner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
        string Body(string name)
        {
            var method = source.Method(owner, name, name == "DenyCopiedFirmwareAdapter" ? null : OriginalDiagnosticSignature);
            _ = source.MethodInvocations(method);
            return HostSourceText.Active(method);
        }
        string prepare = Body("PrepareAdapters");
        Need(HostSourceText.Contains(prepare, "&state->Arena, state->Memory.References, backends)"), "original owner binding");
        Need(HostSourceText.Contains(prepare, "state->CopiedHandles = state->OriginalHandles;"), "retained handle copies");
        Need(HostSourceText.Contains(prepare, "state->OriginalHandles.Console.Write(&empty, 1)"), "actual admitted console positive");
        _ = Body("ReportRevokedAdapters");
        string denied = Body("DenyCopiedFirmwareAdapter");
        foreach (string method in new[] { "Console.Write", "MemoryMap.Capture", "FileSystem.Read", "Timer.Read", "Random.Fill", "ConfigurationTable.Find", "PhysicalMemory.Read", "Framebuffer.Snapshot" })
            Need(HostSourceText.Contains(denied, "state->CopiedHandles." + method), "typed copied capability " + method);
        Need(HostSourceText.Contains(denied, "FirmwareAdapterStatus.ServicesUnavailable")
            && HostSourceText.Contains(denied, "AdapterEntryCount(state) == 1")
            && HostSourceText.Contains(denied, "output[j] != 0xa5"), "denial before backend or output mutation");
        Need(!UsesForbiddenDiagnosticTransport(source.Method(owner, "ReportRevokedAdapters"))
            && !UsesForbiddenDiagnosticTransport(source.Method(owner, "DenyCopiedFirmwareAdapter"))
            && HostSourceText.Contains(Body("WriteAdapterMarker"), "state->Arena.Fault->Backend"), "owned diagnostic transport");
    }

    private static bool OriginalDiagnosticSignature(Microsoft.CodeAnalysis.IMethodSymbol method) =>
        method.IsStatic && method.Arity == 0 && !method.ReturnsByRef && !method.ReturnsByRefReadonly
        && method.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Private
        && method.ReturnType.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_Boolean;

    private static bool UsesForbiddenDiagnosticTransport(Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax method) =>
        method.ToString().Contains("Write(console", StringComparison.Ordinal);

    [HostTest("m03.008.efi method signature and inactive diagnostic exclusions remain strict")]
    private static void SourceGuardControls()
    {
        const string text = """
            internal class Controls
            {
                private static bool value;
                private static bool Original() => value;
                private static bool Generic<T>() => value;
                private static ref bool Reference() => ref value;
                private static ref readonly bool ReadOnlyReference() => ref value;
                private bool Instance() => value;
                public static bool Public() => value;
                private static int NonBoolean() => 0;
                private static bool InactiveTransport()
                {
            #if REVIEW_DISABLED
                    Write(console, serial, 1);
            #endif
                    return true;
                }
            }
            """;
        var source = new OS.Tooling.CSharpSourceCatalog(
            [OS.Tooling.CSharpSourceDocument.FromBytes("arbitrary-diagnostics.cs", Encoding.UTF8.GetBytes(text),
                new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp14))],
            [Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic =>
            diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error), "all controls compile");
        foreach (string name in new[] { "Original", "Generic", "Reference", "ReadOnlyReference", "Instance", "Public", "NonBoolean" })
        {
            var method = source.Type("Controls").GetMembers(name).OfType<Microsoft.CodeAnalysis.IMethodSymbol>().Single();
            HostCheck.True(OriginalDiagnosticSignature(method) == (name == "Original"), name);
        }
        HostCheck.True(!UsesForbiddenDiagnosticTransport(source.Method("Controls", "Original")), "ordinary method");
        var inactive = source.Method("Controls", "InactiveTransport");
        HostCheck.True(!HostSourceText.Active(inactive).Contains("Write(console", StringComparison.Ordinal),
            "the control is disabled in the selected profile");
        HostCheck.True(UsesForbiddenDiagnosticTransport(inactive), "disabled diagnostic text remains excluded");
    }

    private static string Invoke(params string[] arguments)
    {
        var plan = OS.Tooling.QemuProfiles.Create(OS.Tooling.QemuSmokeOptions.Parse(["-DryRun", "-BuildEfi", .. arguments]));
        return "REQUIRED_MARKERS " + string.Join(';', plan.Required) + "\nORDERED_MARKERS " + string.Join(';', plan.Ordered) +
            "\nUNIQUE_MARKERS " + string.Join(';', plan.Unique) + "\nFORBIDDEN_MARKERS " + string.Join(';', plan.Forbidden) + "\n";
    }

    [HostTest("m03.008.efi complete marker admission and missing copied-capability refusals", HostLane.Native, 1)]
    private static void Profile()
    {
        foreach (bool revocation in new[] { false, true })
            CheckProfile(revocation);
    }

    private static void CheckProfile(bool revocation)
    {
        string[] arguments = ["-EnableM01LoadedImageStackProbe", "-EnableM01LiveAllocatorProbe", "-EnableAcpiPhysicalDiscoveryProbe", "-EnableM03FirstRealExitBootServicesProbe"];
        string output = Invoke(revocation ? [.. arguments, "-" + Flag] : arguments);
        string[] Read(string key) => output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))[key.Length..].TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] required = Read("REQUIRED_MARKERS "), ordered = Read("ORDERED_MARKERS "), unique = Read("UNIQUE_MARKERS "), forbidden = Read("FORBIDDEN_MARKERS ");
        Need(ordered.Length == (revocation ? 78 : 66) && ordered.SequenceEqual(unique, StringComparer.Ordinal), "exact closed first-exit/child profile");
        Need(ordered[55] == "TEST_START m03.007", "first-exit admission order");
        int tail = revocation ? 62 : 61;
        string[] ownedTail = ["TEST_START m03.009", "UEFI_EBS_SUCCESS", "POST_FIRMWARE_DIAGNOSTICS_READY", "CLROS_M03_009_READY", "TEST_PASS m03.009"];
        Need(ordered.Skip(tail).Take(5).SequenceEqual(ownedTail, StringComparer.Ordinal), "complete owned diagnostic tail");
        Need(ordered[^1] == (revocation ? "TEST_PASS m03.008" : "TEST_PASS m03.009"), "exact terminal record");
        bool AcceptText(string text)
        {
            var state = new SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            return state.Feed(Encoding.ASCII.GetBytes(text)).Succeeded && state.Complete().Passed;
        }
        bool Accept(string[] records) => AcceptText(string.Join("\r\n", records) + "\r\n");
        Need(Accept(ordered), "controlled complete marker stream");
        foreach (int index in Enumerable.Range(tail, 5))
        {
            Need(!Accept(ordered.Where((_, i) => i != index).ToArray()), "missing owned diagnostic record " + index);
            Need(!Accept(ordered.Take(index).Concat([ordered[index]]).Concat(ordered.Skip(index)).ToArray()), "duplicate owned diagnostic record " + index);
            string partial = string.Join("\r\n", ordered.Take(index)) + "\r\n" + ordered[index][..^1];
            Need(!AcceptText(partial), "truncated owned diagnostic record " + index);
        }
        for (int index = tail; index < tail + 4; index++)
        {
            string[] reordered = (string[])ordered.Clone();
            (reordered[index], reordered[index + 1]) = (reordered[index + 1], reordered[index]);
            Need(!Accept(reordered), "reordered owned diagnostic records " + index);
        }
        if (!revocation)
            return;
        Need(ordered[56] == "M03_ADAPTER_ADMITTED 1 0" && ordered[67] == "TEST_START m03.008", "admission and owned diagnostic order");
        for (int i = 0; i < 8; i++)
            Need(ordered[68 + i] == $"M03_ADAPTER_DENIED {i} 4 0 1", "exact denial status/count/preservation");
        foreach (int index in new[] { 56 }.Concat(Enumerable.Range(67, 11)))
        {
            Need(!Accept(ordered.Where((_, i) => i != index).ToArray()), "missing copied-capability record " + index);
            Need(!Accept(ordered.Take(index).Concat([ordered[index]]).Concat(ordered.Skip(index)).ToArray()), "duplicate copied-capability record " + index);
        }
        string[] wrong = (string[])ordered.Clone();
        wrong[68] = "M03_ADAPTER_DENIED 0 1 0 1";
        Need(!Accept(wrong), "successful post-exit call cannot be accepted");
    }
}
