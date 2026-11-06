using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static partial class M04SpanMemoryConformanceEfiIntegrationTests
{
    private const string TestName = "m04.003.efi.integration";
    private const string M04EndianReadyMarker = OS.Tooling.QemuContracts.Record278;
    private const string ReadyMarker = OS.Tooling.QemuContracts.Record279;
    private const string FailureMarker = OS.Tooling.QemuContracts.Record275;
    private const string ProjectFileSha256 = OS.Tooling.EfiContracts.ProjectSourceSha256;
    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;
        RunCase("m04.003 frozen leaf and reviewed EFI closure are exact", TestFrozenLeafAndProjectClosure, ref failures);
        RunCase("m04.003 EFI entry executes and audits every frozen record", TestEfiEntryExecutionContract, ref failures);
        RunCase("m04.003 build and artifact commands pin dependency and authority", TestBuildAndVerifierContracts, ref failures);
        RunCase("m04.003 QEMU profiles and hostile arguments are behavioral", TestQemuProfilesAndHostileArguments, ref failures);
        RunCase("m04.003 pre-evidence docs preserve the guest evidence boundary", TestPreEvidenceDocumentationBoundary, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException("M04-003 EFI integration host tests failed: " + failures);
        }

        Console.WriteLine("TEST_PASS " + TestName);
    }

    private static void RunCase(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestFrozenLeafAndProjectClosure()
    {
        string root = FindRepositoryRoot();
        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        XDocument project = XDocument.Load(projectPath);
        var profile = OS.Tooling.EfiOptions.Parse(["--EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
            "--EnableM04IndependentEndianConformanceProbe"], true).Profile();
        var source = HostSourceCatalog.Efi(profile);
        _ = source.Type("OS.Std.Binary.IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa");
        _ = source.Type("OS.Std.Memory.SpanOverlapMemoryCastUnsafeWidthConformanceVectors");
        _ = source.Type("OS.Boot.M01LoadedImageStackIntegration");
        HostCheck.Equal(0, OS.Tooling.ContractAudit.Inspect(root).Count);
        XElement[] defaults = project.Descendants("EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe").Where(element => element.Parent?.Name.LocalName == "PropertyGroup").ToArray();
        XElement[] symbolGroups = project.Descendants("PropertyGroup").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true'").ToArray();
        XElement[] errors = project.Descendants("Target").Single(element => element.Attribute("Name")?.Value == "ValidateM01ProbeSwitches").Elements("Error").ToArray();
        string dependencyCondition = "'$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '$(EnableM04IndependentEndianConformanceProbe)' != 'true'";
        string terminalExclusionCondition = "'$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'";
        const string catalogType = "OS.Std.Memory.SpanOverlapMemoryCastUnsafeWidthConformanceVectors";
        var documents = source.TypeSources(catalogType);
        foreach (var document in documents)
        {
            string include = Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, document.Path).Replace('/', '\\');
            HostCheck.True(OS.Tooling.EfiContracts.SourceIdentities.TryGetValue(include, out string? expected),
                "reviewed source identity for " + include);
            HostCheck.Equal(expected, OS.Tooling.ContractAudit.HashSource(document.Text));
        }
        string production = string.Join("\n", documents.Select(document => document.Text));
        HostCheck.Registered(typeof(SpanOverlapMemoryCastUnsafeWidthConformanceVectorsTests));
        return OS.Tooling.ContractAudit.HashSource(File.ReadAllText(projectPath)) == ProjectFileSha256 && defaults.Length == 1 && defaults[0].Value == "false" && defaults[0].Attribute("Condition")?.Value == "'$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == ''" && symbolGroups.Length == 1 && symbolGroups[0].Elements("DefineConstants").Single().Value == "$(DefineConstants);M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE" && errors.Count(error => error.Attribute("Condition")?.Value == dependencyCondition && error.Attribute("Text")?.Value == "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe requires EnableM04IndependentEndianConformanceProbe=true.") == 1 && errors.Count(error => error.Attribute("Condition")?.Value == terminalExclusionCondition && error.Attribute("Text")?.Value == "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe cannot be combined with EnableM02OwnedGdtTssTerminalProbe=true.") == 1 && production.Contains("ExpectedCatalogFingerprint = 0xF03FF59D36F39ECDUL", StringComparison.Ordinal) && production.Contains("public static unsafe SpanMemoryExecutionResult ExecuteCatalog(", StringComparison.Ordinal) && !production.Contains("nuint.MaxValue", StringComparison.Ordinal) && !production.Contains("ulong.MaxValue", StringComparison.Ordinal);
    }

    private static bool TestEfiEntryExecutionContract()
    {
        const string owner = "OS.Boot.EfiEntry";
        var catalog = HostSourceCatalog.Efi(OS.Tooling.EfiOptions.Parse(
            ["--EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "--EnableM04IndependentEndianConformanceProbe"], true).Profile());
        string source = HostSourceText.Active(catalog.Method(owner, "WriteBootMarkers"));
        string probe = HostSourceText.Active(catalog.Method(owner, "WriteM04SpanMemoryConformanceProbe"));
        string recordMatcher = HostSourceText.Active(catalog.Method(owner, "M04SpanMemoryRecordMatches"));
        string ownerText = HostSourceText.Documents(catalog, owner);
        string ready = ExtractAsciiMarker(catalog, "WriteM04SpanMemoryConformanceSuccessLine");
        string failure = ExtractAsciiMarker(catalog, "WriteM04SpanMemoryConformanceFailureLine");
        int consoleReady = IndexOfAfter(source, "WriteEfiConsoleReady(console, serial);", 0);
        int endianProbe = IndexOfAfter(source, "WriteM04IndependentEndianConformanceProbe(console, serial)", consoleReady);
        int spanProbe = IndexOfAfter(source, "WriteM04SpanMemoryConformanceProbe(console, serial)", endianProbe);
        int memoryMap = IndexOfAfter(source, "TryCaptureUefiMemoryMap(systemTable)", spanProbe);
        int execute = IndexOfAfter(probe, ".ExecuteCatalog(", 0);
        int key16 = IndexOfAfter(probe, "records[15], 16", 0);
        int raw16 = IndexOfAfter(probe, "0x1122334455667788UL", key16);
        int truncated16 = IndexOfAfter(probe, "0x0000000055667788UL", raw16);
        int success = IndexOfAfter(probe, "WriteM04SpanMemoryConformanceSuccessLine(console, serial);", 0);
        string[] exactRecordTokens = ["records[0], 1,", "records[1], 2,", "records[2], 3,", "records[3], 4,", "records[4], 5,", "records[5], 6,", "records[6], 7,", "records[7], 8,", "records[8], 9,", "records[9], 10,", "records[10], 11,", "records[11], 12,", "records[12], 13,", "records[13], 14,", "records[14], 15,", "records[15], 16,"];
        string[] widthTokens = ["8, 1, 0xFF, 0xFF, 0x000000000000FF00UL, false", "8, 1, 0xFF80, 0xFFFFFFFFFFFFFF80UL,", "8, 1, 0x89ABCDEF, 0x0000000089ABCDEFUL,", "4, 1, 0x1122334455667788UL, 0x0000000055667788UL,"];
        string[] overlapScratchTokens = ["0x0807060506050403UL,\n                0x0807060506050403UL, false", "0x0807040302010201UL,\n                0x0807040302010201UL, false", "0x0403020104030201UL,\n                0x0403020104030201UL, false"];
        return ready == ReadyMarker && failure == FailureMarker && consoleReady >= 0 && endianProbe > consoleReady && spanProbe > endianProbe && memoryMap > spanProbe && ContainsCode(probe, "stackalloc OS.Std.Memory.SpanMemoryConformanceVector[16]", StringComparison.Ordinal) && ContainsCode(probe, "stackalloc byte[16]", StringComparison.Ordinal) && ContainsCode(probe, "stackalloc OS.Std.Memory.SpanMemoryVectorExecutionRecord[16]", StringComparison.Ordinal) && CountCodeOccurrences(probe, ".CopyBuiltInCatalog(") == 1 && CountCodeOccurrences(probe, ".ExecuteCatalog(") == 1 && execute >= 0 && ContainsCode(probe, "vectors,\n                    16,\n                    scratch,\n                    16,\n                    records,\n                    16,\n                    0xF03FF59D36F39ECDUL", StringComparison.Ordinal) && CountCodeOccurrences(probe, "M04SpanMemoryVectorMatches(\n                vectors[") == 16 && exactRecordTokens.All(token => CountCodeOccurrences(probe, token) == 1) && widthTokens.All(token => ContainsCode(probe, token, StringComparison.Ordinal)) && overlapScratchTokens.All(token => ContainsCode(probe, token, StringComparison.Ordinal)) && ContainsCode(probe, "records[3], 4,\n                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,\n                OS.Std.Memory.SpanMemoryVectorExecutionStatus.ExpectedRejection,\n                0, 0, 0, 0, Seed, true", StringComparison.Ordinal) && key16 >= 0 && raw16 > key16 && truncated16 > raw16 && success > truncated16 && ContainsCode(recordMatcher, "record.RawUnsignedValue == rawUnsignedValue &&\n            record.OutputBytes == outputBytes", StringComparison.Ordinal) && ContainsCode(recordMatcher, "record.RejectedInputPreserved == rejectedInputPreserved", StringComparison.Ordinal) && CountCodeOccurrences(probe, "WriteM04SpanMemoryConformanceFailureLine(console, serial);") == 3 && CountCodeOccurrences(probe, "WriteM04SpanMemoryConformanceSuccessLine(console, serial);") == 1 && CountOccurrences(ownerText, ReadyMarker) == 0 && CountOccurrences(ownerText, FailureMarker) == 0;
    }

    private static bool TestBuildAndVerifierContracts()
    {
        const string flag = "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe";
        const string prerequisite = "EnableM04IndependentEndianConformanceProbe";
        HostCheck.Registered(typeof(EfiArtifactVerificationScriptTests));
        HostCheck.Equal(0, OS.Tooling.ContractAudit.Inspect(FindRepositoryRoot()).Count);
        foreach (bool build in new[]
        {
            true,
            false
        }

        )
        {
            var profile = OS.Tooling.EfiOptions.Parse(["--" + flag, "--" + prerequisite], build).Profile();
            if (!profile.IsEnabled(flag) || !profile.IsEnabled(prerequisite))
                return false;
            var missing = HostToolProcess.ValidateEfiProfile(build, "--" + flag);
            if (missing.ExitCode == 0 || !missing.Output.Contains("m04_span_memory_conformance_requires_independent_endian_conformance", StringComparison.Ordinal))
                return false;
            var conflict = HostToolProcess.ValidateEfiProfile(build, "--" + flag, "--" + prerequisite, "--EnableM02OwnedGdtTssTerminalProbe");
            if (conflict.ExitCode == 0 || !conflict.Output.Contains("m04_span_memory_conformance_forbids_owned_gdt_tss_terminal", StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool TestQemuProfilesAndHostileArguments()
    {
        string root = FindRepositoryRoot();
        ScriptResult defaultProfile = RunQemuDryRun(root);
        ScriptResult endianProfile = RunQemuDryRun(root, "-BuildEfi", "-EnableM04IndependentEndianConformanceProbe");
        ScriptResult spanProfile = RunQemuDryRun(root, "-BuildEfi", "-EnableM04IndependentEndianConformanceProbe", "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");
        ScriptResult missingDependency = RunQemuDryRun(root, "-BuildEfi", "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");
        ScriptResult missingBuild = RunQemuDryRun(root, "-EnableM04IndependentEndianConformanceProbe", "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");
        ScriptResult skipImage = RunQemuDryRun(root, "-BuildEfi", "-SkipImageBuild", "-EnableM04IndependentEndianConformanceProbe", "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");
        ScriptResult terminalCollision = RunQemuDryRun(root, "-BuildEfi", "-EnableM02OwnedGdtTssTerminalProbe", "-EnableM04IndependentEndianConformanceProbe", "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");
        return defaultProfile.ExitCode == 0 && MarkerCount(defaultProfile.Output, "REQUIRED_MARKERS ") == 12 && MarkerCount(defaultProfile.Output, "ORDERED_MARKERS ") == 0 && MarkerCount(defaultProfile.Output, "UNIQUE_MARKERS ") == 0 && MarkerCount(defaultProfile.Output, "FORBIDDEN_MARKERS ") == 2 && !defaultProfile.Output.Contains(ReadyMarker, StringComparison.Ordinal) && endianProfile.ExitCode == 0 && MarkerCount(endianProfile.Output, "REQUIRED_MARKERS ") == 15 && MarkerCount(endianProfile.Output, "ORDERED_MARKERS ") == 3 && MarkerCount(endianProfile.Output, "UNIQUE_MARKERS ") == 1 && MarkerCount(endianProfile.Output, "FORBIDDEN_MARKERS ") == 3 && endianProfile.Output.Contains(M04EndianReadyMarker, StringComparison.Ordinal) && !endianProfile.Output.Contains(ReadyMarker, StringComparison.Ordinal) && spanProfile.ExitCode == 0 && MarkerCount(spanProfile.Output, "REQUIRED_MARKERS ") == 16 && MarkerCount(spanProfile.Output, "ORDERED_MARKERS ") == 4 && MarkerCount(spanProfile.Output, "UNIQUE_MARKERS ") == 2 && MarkerCount(spanProfile.Output, "FORBIDDEN_MARKERS ") == 4 && MarkerLine(spanProfile.Output, "ORDERED_MARKERS ") == "EFI_CONSOLE_READY;" + M04EndianReadyMarker + ";" + ReadyMarker + ";UEFI_MEMORY_MAP_READY" && MarkerLine(spanProfile.Output, "UNIQUE_MARKERS ") == M04EndianReadyMarker + ";" + ReadyMarker && MarkerLine(spanProfile.Output, "FORBIDDEN_MARKERS ").Split(';', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).SequenceEqual(new[] { "M04_ENDIAN_CONFORMANCE_FAILED", "M04_SPAN_MEMORY_CONFORMANCE_FAILED", "OS_SMOKE_FAIL", "TEST_FAIL" }.Order(StringComparer.Ordinal)) && FailsWith(missingDependency, "m04_span_memory_conformance_requires_independent_endian_conformance") && FailsWith(missingBuild, "qemu_profile_requires_build_efi") && FailsWith(skipImage, "qemu_profile_forbids_skip_image_build") && FailsWith(terminalCollision, "m04_span_memory_conformance_forbids_owned_gdt_tss_terminal");
    }

    private static bool TestPreEvidenceDocumentationBoundary()
    {
        string root = FindRepositoryRoot();
        string efi = File.ReadAllText(Path.Combine(root, "docs", "efi-build.md"));
        string qemu = File.ReadAllText(Path.Combine(root, "docs", "qemu.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        return efi.Contains("EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", StringComparison.Ordinal) && efi.Contains(ReadyMarker, StringComparison.Ordinal) && qemu.Contains("EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", StringComparison.Ordinal) && qemu.Contains(ReadyMarker, StringComparison.Ordinal) && readme.Contains("(docs/efi-build.md)", StringComparison.Ordinal) && readme.Contains("(docs/qemu.md)", StringComparison.Ordinal) && Regex.IsMatch(efi, @"Gate 4\s+remains `Fail`", RegexOptions.CultureInvariant) && Regex.IsMatch(qemu, @"Gate 4\s+remains `Fail`", RegexOptions.CultureInvariant) && !efi.Contains("M04-003 guest evidence accepted", StringComparison.Ordinal) && !qemu.Contains("M04-003 guest evidence accepted", StringComparison.Ordinal);
    }

    private static ScriptResult RunQemuDryRun(string root, params string[] extraArguments)
    {
        var result = HostToolProcess.RunTool(["qemu", "smoke", "--boot-policy", "Diagnostic", "--dry-run", .. extraArguments]);
        return new(result.ExitCode, result.Output + result.Error);
    }

    private static bool FailsWith(ScriptResult result, string reason)
    {
        return result.ExitCode != 0 && result.Output.Contains("TEST_FAIL qemu.smoke " + reason, StringComparison.Ordinal) && !result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal);
    }

    private static int MarkerCount(string output, string prefix)
    {
        string value = MarkerLine(output, prefix);
        return string.IsNullOrEmpty(value) || value == "none" ? 0 : value.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string MarkerLine(string output, string prefix)
    {
        string[] lines = output.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] matches = lines.Where(line => line.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
        {
            return string.Empty;
        }

        return matches[0][prefix.Length..];
    }

    private static string ExtractAsciiMarker(OS.Tooling.CSharpSourceCatalog source, string methodName) =>
        HostSourceText.ByteCharacters(source, "OS.Boot.EfiEntry", methodName);

    private static bool ContainsCode(string source, string fragment, StringComparison comparison) =>
        comparison == StringComparison.Ordinal && HostSourceText.Contains(source, fragment);

    private static int CountCodeOccurrences(string source, string fragment) => HostSourceText.Count(source, fragment);

    private static int IndexOfAfter(string source, string value, int start) => HostSourceText.IndexOf(source, value, start);

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[]
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory
        }

        )
        {
            DirectoryInfo? current = new DirectoryInfo(start);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CLROS.slnx")) && File.Exists(Path.Combine(current.FullName, "src", "OS", "OS.csproj")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("CLROS repository root was not found.");
    }
}
