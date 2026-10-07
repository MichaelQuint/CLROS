using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class M04SpanMemoryConformanceEfiIntegrationTests
{
    private const string TestName = "m04.003.efi.integration";
    private const string M04EndianReadyMarker =
        "CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5 uint16Series=7 fingerprint=E81E4451E8D999F6 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail";
    private const string ReadyMarker =
        "CLROS_M04_003_READY vectors=16 spanVectors=4 overlapVectors=5 memoryCastVectors=3 unsafeWidthVectors=4 fingerprint=F03FF59D36F39ECD pointerWidth=8 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail";
    private const string FailureMarker =
        "M04_SPAN_MEMORY_CONFORMANCE_FAILED";
    private const string CompileClosureSha256 =
        "f36aa897f314bb169a667353c2d37c70d4767cf57aa95babf44103af85f2817a";
    private const string ProjectFileSha256 =
        "47feb1cc1b4856e844b26847fdef1167aa4f5d7eb3ac2e7aa2dfea58b43a0e14";
    private const string FrozenProductionSha256 =
        "0c818f89ecc761a4e5692ce3970e98d577381b98bbe540f6ec5f154130c12153";
    private const string FrozenHostTestSha256 =
        "6ebbc0f3c1ed86c5e93377f0bd24d9fc7e707d7881d7ca8d483a52ee30867b1f";

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m04.003 frozen leaf and 59-item EFI closure are exact",
            TestFrozenLeafAndProjectClosure,
            ref failures);
        RunCase(
            "m04.003 EFI entry executes and audits every frozen record",
            TestEfiEntryExecutionContract,
            ref failures);
        RunCase(
            "m04.003 build and artifact scripts pin dependency and authority",
            TestBuildAndVerifierContracts,
            ref failures);
        RunCase(
            "m04.003 QEMU profiles and hostile arguments are behavioral",
            TestQemuProfilesAndHostileArguments,
            ref failures);
        RunCase(
            "m04.003 pre-evidence docs preserve the guest evidence boundary",
            TestPreEvidenceDocumentationBoundary,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M04-003 EFI integration host tests failed: " + failures);
        }

        Console.WriteLine("TEST_PASS " + TestName);
    }

    private static void RunCase(
        string name,
        Func<bool> test,
        ref int failures)
    {
        Console.WriteLine("TEST_START " + name);
        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception exception)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL " + name + " " + exception.GetType().Name + ": " +
                exception.Message);
        }
    }

    private static bool TestFrozenLeafAndProjectClosure()
    {
        string root = FindRepositoryRoot();
        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        XDocument project = XDocument.Load(projectPath);
        XElement[] compileItems = project.Descendants("Compile").ToArray();
        string[] compileKeys = compileItems
            .Select(item =>
                (item.Attribute("Include")?.Value ?? string.Empty) + "|" +
                (item.Attribute("Link")?.Value ?? string.Empty))
            .ToArray();
        string compileHash = Sha256Text(string.Join("\n", compileKeys) + "\n");
        const string EndianCatalogKey =
            @"..\..\std\no-runtime\shared\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
        const string SpanMemoryCatalogKey =
            @"..\..\std\no-runtime\shared\SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs|Std\SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs";
        const string LoadedImageKey =
            @"..\OS.Kernel\Boot\M01LoadedImageStackIntegration.cs|Boot\M01LoadedImageStackIntegration.cs";
        int endianIndex = Array.IndexOf(compileKeys, EndianCatalogKey);
        int spanMemoryIndex = Array.IndexOf(compileKeys, SpanMemoryCatalogKey);
        int loadedImageIndex = Array.IndexOf(compileKeys, LoadedImageKey);

        XElement[] defaults = project
            .Descendants(
                "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe")
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToArray();
        XElement[] symbolGroups = project
            .Descendants("PropertyGroup")
            .Where(element =>
                element.Attribute("Condition")?.Value ==
                    "'$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true'")
            .ToArray();
        XElement[] errors = project
            .Descendants("Target")
            .Single(element => element.Attribute("Name")?.Value ==
                "ValidateM01ProbeSwitches")
            .Elements("Error")
            .ToArray();
        string dependencyCondition =
            "'$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '$(EnableM04IndependentEndianConformanceProbe)' != 'true'";
        string terminalExclusionCondition =
            "'$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == 'true' and '$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'";

        string productionPath = Path.Combine(
            root,
            "std",
            "no-runtime",
            "shared",
            "SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs");
        string leafTestPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "SpanOverlapMemoryCastUnsafeWidthConformanceVectorsTests.cs");
        string production = File.ReadAllText(productionPath);

        return compileItems.Length == 120
            && compileHash == CompileClosureSha256
            && Sha256File(projectPath) == ProjectFileSha256
            && endianIndex >= 0
            && spanMemoryIndex == endianIndex + 1
            && loadedImageIndex == spanMemoryIndex + 4
            && compileKeys.Count(key => key == SpanMemoryCatalogKey) == 1
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE"
            && errors.Count(error =>
                error.Attribute("Condition")?.Value == dependencyCondition &&
                error.Attribute("Text")?.Value ==
                    "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe requires EnableM04IndependentEndianConformanceProbe=true.") == 1
            && errors.Count(error =>
                error.Attribute("Condition")?.Value == terminalExclusionCondition &&
                error.Attribute("Text")?.Value ==
                    "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe cannot be combined with EnableM02OwnedGdtTssTerminalProbe=true.") == 1
            && Sha256File(productionPath) == FrozenProductionSha256
            && Sha256File(leafTestPath) == FrozenHostTestSha256
            && production.Contains(
                "ExpectedCatalogFingerprint = 0xF03FF59D36F39ECDUL",
                StringComparison.Ordinal)
            && production.Contains(
                "public static unsafe SpanMemoryExecutionResult ExecuteCatalog(",
                StringComparison.Ordinal)
            && !production.Contains("nuint.MaxValue", StringComparison.Ordinal)
            && !production.Contains("ulong.MaxValue", StringComparison.Ordinal);
    }

    private static bool TestEfiEntryExecutionContract()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string probe = ExtractMethod(
            source,
            "private static bool WriteM04SpanMemoryConformanceProbe(");
        string recordMatcher = ExtractMethod(
            source,
            "private static bool M04SpanMemoryRecordMatches(");
        string ready = ExtractAsciiMarker(
            source,
            "private static void WriteM04SpanMemoryConformanceSuccessLine(");
        string failure = ExtractAsciiMarker(
            source,
            "private static void WriteM04SpanMemoryConformanceFailureLine(");

        int consoleReady = source.IndexOf(
            "WriteEfiConsoleReady(console, serial);",
            StringComparison.Ordinal);
        int endianProbe = IndexOfAfter(
            source,
            "WriteM04IndependentEndianConformanceProbe(console, serial)",
            consoleReady);
        int spanProbe = IndexOfAfter(
            source,
            "WriteM04SpanMemoryConformanceProbe(console, serial)",
            endianProbe);
        int memoryMap = IndexOfAfter(
            source,
            "TryCaptureUefiMemoryMap(systemTable)",
            spanProbe);
        int execute = probe.IndexOf(".ExecuteCatalog(", StringComparison.Ordinal);
        int key16 = probe.IndexOf("records[15], 16", StringComparison.Ordinal);
        int raw16 = probe.IndexOf(
            "0x1122334455667788UL",
            key16,
            StringComparison.Ordinal);
        int truncated16 = probe.IndexOf(
            "0x0000000055667788UL",
            raw16,
            StringComparison.Ordinal);
        int success = probe.IndexOf(
            "WriteM04SpanMemoryConformanceSuccessLine(console, serial);",
            StringComparison.Ordinal);

        string[] exactRecordTokens =
        [
            "records[0], 1,",
            "records[1], 2,",
            "records[2], 3,",
            "records[3], 4,",
            "records[4], 5,",
            "records[5], 6,",
            "records[6], 7,",
            "records[7], 8,",
            "records[8], 9,",
            "records[9], 10,",
            "records[10], 11,",
            "records[11], 12,",
            "records[12], 13,",
            "records[13], 14,",
            "records[14], 15,",
            "records[15], 16,"
        ];
        string[] widthTokens =
        [
            "8, 1, 0xFF, 0xFF, 0x000000000000FF00UL, false",
            "8, 1, 0xFF80, 0xFFFFFFFFFFFFFF80UL,",
            "8, 1, 0x89ABCDEF, 0x0000000089ABCDEFUL,",
            "4, 1, 0x1122334455667788UL, 0x0000000055667788UL,"
        ];
        string[] overlapScratchTokens =
        [
            "0x0807060506050403UL,\n                0x0807060506050403UL, false",
            "0x0807040302010201UL,\n                0x0807040302010201UL, false",
            "0x0403020104030201UL,\n                0x0403020104030201UL, false"
        ];

        return ready == ReadyMarker
            && failure == FailureMarker
            && consoleReady >= 0
            && endianProbe > consoleReady
            && spanProbe > endianProbe
            && memoryMap > spanProbe
            && probe.Contains(
                "stackalloc OS.Std.Memory.SpanMemoryConformanceVector[16]",
                StringComparison.Ordinal)
            && probe.Contains("stackalloc byte[16]", StringComparison.Ordinal)
            && probe.Contains(
                "stackalloc OS.Std.Memory.SpanMemoryVectorExecutionRecord[16]",
                StringComparison.Ordinal)
            && CountOccurrences(probe, ".CopyBuiltInCatalog(") == 1
            && CountOccurrences(probe, ".ExecuteCatalog(") == 1
            && execute >= 0
            && probe.Contains(
                "vectors,\n                    16,\n                    scratch,\n                    16,\n                    records,\n                    16,\n                    0xF03FF59D36F39ECDUL",
                StringComparison.Ordinal)
            && CountOccurrences(
                probe,
                "M04SpanMemoryVectorMatches(\n                vectors[") == 16
            && exactRecordTokens.All(token =>
                CountOccurrences(probe, token) == 1)
            && widthTokens.All(token => probe.Contains(token, StringComparison.Ordinal))
            && overlapScratchTokens.All(token =>
                probe.Contains(token, StringComparison.Ordinal))
            && probe.Contains(
                "records[3], 4,\n                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,\n                OS.Std.Memory.SpanMemoryVectorExecutionStatus.ExpectedRejection,\n                0, 0, 0, 0, Seed, true",
                StringComparison.Ordinal)
            && key16 >= 0
            && raw16 > key16
            && truncated16 > raw16
            && success > truncated16
            && recordMatcher.Contains(
                "record.RawUnsignedValue == rawUnsignedValue &&\n            record.OutputBytes == outputBytes",
                StringComparison.Ordinal)
            && recordMatcher.Contains(
                "record.RejectedInputPreserved == rejectedInputPreserved",
                StringComparison.Ordinal)
            && CountOccurrences(
                probe,
                "WriteM04SpanMemoryConformanceFailureLine(console, serial);") == 3
            && CountOccurrences(
                probe,
                "WriteM04SpanMemoryConformanceSuccessLine(console, serial);") == 1
            && CountOccurrences(source, ReadyMarker) == 0
            && CountOccurrences(source, FailureMarker) == 0;
    }

    private static bool TestBuildAndVerifierContracts()
    {
        string root = FindRepositoryRoot();
        string build = File.ReadAllText(
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));
        string verifier = File.ReadAllText(
            Path.Combine(root, "scripts", "test-efi-artifact.ps1"));
        string verifierTests = File.ReadAllText(
            Path.Combine(
                root,
                "tests",
                "OS.HostTests",
                "EfiArtifactVerificationScriptTests.cs"));
        const string Flag =
            "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe";
        const string CompileKey =
            @"..\..\std\no-runtime\shared\SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs|Std\SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs";

        return build.Contains("[switch] $" + Flag, StringComparison.Ordinal)
            && build.Contains(CompileKey, StringComparison.Ordinal)
            && build.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=136",
                StringComparison.Ordinal)
            && build.Contains(
                "-p:" + Flag + "=true",
                StringComparison.Ordinal)
            && build.Contains(
                "EFI_PROBE M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE",
                StringComparison.Ordinal)
            && build.Contains(
                "span_memory_conformance_requires_independent_endian_conformance",
                StringComparison.Ordinal)
            && build.Contains(
                "span_memory_conformance_forbids_owned_gdt_tss_terminal",
                StringComparison.Ordinal)
            && build.Contains(
                "-M04SpanMemoryConformanceEnabled $EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe.IsPresent",
                StringComparison.Ordinal)
            && build.Contains(
                "\"-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe\",\n            \"true\"",
                StringComparison.Ordinal)
            && verifier.Contains("[switch]$" + Flag, StringComparison.Ordinal)
            && verifier.Contains(
                "$expectedSourceClosureInputCount = 136",
                StringComparison.Ordinal)
            && verifierTests.Contains(
                "ExpectedSourceClosureInputCount = 136",
                StringComparison.Ordinal)
            && verifierTests.Contains(
                "[\"compileCount\"] = 120",
                StringComparison.Ordinal)
            && verifierTests.Contains(
                "[\"EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe\"] =\n                \"false\"",
                StringComparison.Ordinal)
            && verifier.Contains(CompileKey, StringComparison.Ordinal)
            && verifier.Contains(
                "$compileItems.Count -ne 120",
                StringComparison.Ordinal)
            && verifier.Contains(
                "-p:" + Flag + "=true",
                StringComparison.Ordinal)
            && verifier.Contains(
                "m04_span_memory_conformance_requires_independent_endian_conformance",
                StringComparison.Ordinal)
            && verifier.Contains(
                "m04_span_memory_conformance_forbids_owned_gdt_tss_terminal",
                StringComparison.Ordinal);
    }

    private static bool TestQemuProfilesAndHostileArguments()
    {
        string root = FindRepositoryRoot();
        ScriptResult defaultProfile = RunQemuDryRun(root);
        ScriptResult endianProfile = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM04IndependentEndianConformanceProbe");
        ScriptResult spanProfile = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM04IndependentEndianConformanceProbe",
            "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");
        ScriptResult missingDependency = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");
        ScriptResult missingBuild = RunQemuDryRun(
            root,
            "-EnableM04IndependentEndianConformanceProbe",
            "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");
        ScriptResult skipImage = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-SkipImageBuild",
            "-EnableM04IndependentEndianConformanceProbe",
            "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");
        ScriptResult terminalCollision = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02OwnedGdtTssTerminalProbe",
            "-EnableM04IndependentEndianConformanceProbe",
            "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");

        return defaultProfile.ExitCode == 0
            && MarkerCount(defaultProfile.Output, "REQUIRED_MARKERS ") == 12
            && MarkerCount(defaultProfile.Output, "ORDERED_MARKERS ") == 0
            && MarkerCount(defaultProfile.Output, "UNIQUE_MARKERS ") == 0
            && MarkerCount(defaultProfile.Output, "FORBIDDEN_MARKERS ") == 2
            && !defaultProfile.Output.Contains(ReadyMarker, StringComparison.Ordinal)
            && endianProfile.ExitCode == 0
            && MarkerCount(endianProfile.Output, "REQUIRED_MARKERS ") == 15
            && MarkerCount(endianProfile.Output, "ORDERED_MARKERS ") == 3
            && MarkerCount(endianProfile.Output, "UNIQUE_MARKERS ") == 1
            && MarkerCount(endianProfile.Output, "FORBIDDEN_MARKERS ") == 3
            && endianProfile.Output.Contains(M04EndianReadyMarker, StringComparison.Ordinal)
            && !endianProfile.Output.Contains(ReadyMarker, StringComparison.Ordinal)
            && spanProfile.ExitCode == 0
            && MarkerCount(spanProfile.Output, "REQUIRED_MARKERS ") == 16
            && MarkerCount(spanProfile.Output, "ORDERED_MARKERS ") == 4
            && MarkerCount(spanProfile.Output, "UNIQUE_MARKERS ") == 2
            && MarkerCount(spanProfile.Output, "FORBIDDEN_MARKERS ") == 4
            && MarkerLine(spanProfile.Output, "ORDERED_MARKERS ") ==
                "EFI_CONSOLE_READY;" + M04EndianReadyMarker + ";" +
                ReadyMarker + ";UEFI_MEMORY_MAP_READY"
            && MarkerLine(spanProfile.Output, "UNIQUE_MARKERS ") ==
                M04EndianReadyMarker + ";" + ReadyMarker
            && MarkerLine(spanProfile.Output, "FORBIDDEN_MARKERS ")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(
                new[]
                {
                    "M04_ENDIAN_CONFORMANCE_FAILED",
                    "M04_SPAN_MEMORY_CONFORMANCE_FAILED",
                    "OS_SMOKE_FAIL",
                    "TEST_FAIL"
                }.Order(StringComparer.Ordinal))
            && FailsWith(
                missingDependency,
                "m04_span_memory_conformance_probe_requires_independent_endian_conformance")
            && FailsWith(
                missingBuild,
                "m04_span_memory_conformance_probe_requires_build_efi")
            && FailsWith(
                skipImage,
                "m04_span_memory_conformance_probe_forbids_skip_image_build")
            && FailsWith(
                terminalCollision,
                "m04_span_memory_conformance_probe_forbids_owned_gdt_tss_terminal");
    }

    private static bool TestPreEvidenceDocumentationBoundary()
    {
        string root = FindRepositoryRoot();
        string efi = File.ReadAllText(Path.Combine(root, "docs", "efi-build.md"));
        string qemu = File.ReadAllText(Path.Combine(root, "docs", "qemu.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        return efi.Contains(
                "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
                StringComparison.Ordinal)
            && efi.Contains(ReadyMarker, StringComparison.Ordinal)
            && qemu.Contains(
                "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
                StringComparison.Ordinal)
            && qemu.Contains(ReadyMarker, StringComparison.Ordinal)
            && readme.Contains(
                "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
                StringComparison.Ordinal)
            && readme.Contains("CLROS_M04_003_READY", StringComparison.Ordinal)
            && Regex.IsMatch(
                efi,
                @"Gate 4\s+remains `Fail`",
                RegexOptions.CultureInvariant)
            && Regex.IsMatch(
                qemu,
                @"Gate 4\s+remains `Fail`",
                RegexOptions.CultureInvariant)
            && !efi.Contains(
                "M04-003 guest evidence accepted",
                StringComparison.Ordinal)
            && !qemu.Contains(
                "M04-003 guest evidence accepted",
                StringComparison.Ordinal);
    }

    private static ScriptResult RunQemuDryRun(
        string root,
        params string[] extraArguments)
    {
        string script = Path.Combine(root, "scripts", "run-qemu-smoke.ps1");
        ProcessStartInfo start = new ProcessStartInfo("powershell.exe")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("-DryRun");
        foreach (string argument in extraArguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start powershell.exe.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("run-qemu-smoke.ps1 dry-run timed out.");
        }

        Task.WaitAll(stdout, stderr);
        return new ScriptResult(
            process.ExitCode,
            stdout.Result + stderr.Result);
    }

    private static bool FailsWith(ScriptResult result, string reason)
    {
        return result.ExitCode != 0
            && result.Output.Contains(
                "TEST_FAIL qemu.smoke.invalid_args " + reason,
                StringComparison.Ordinal)
            && !result.Output.Contains(
                "TEST_PASS qemu.runner.dryrun",
                StringComparison.Ordinal);
    }

    private static int MarkerCount(string output, string prefix)
    {
        string value = MarkerLine(output, prefix);
        return string.IsNullOrEmpty(value) || value == "none"
            ? 0
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string MarkerLine(string output, string prefix)
    {
        string[] lines = output.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] matches = lines
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            return string.Empty;
        }

        return matches[0][prefix.Length..];
    }

    private static string ExtractMethod(string source, string methodToken)
    {
        int start = source.IndexOf(methodToken, StringComparison.Ordinal);
        int end = start < 0
            ? -1
            : source.IndexOf(
                "\n    private static ",
                start + methodToken.Length,
                StringComparison.Ordinal);
        return start >= 0 && end > start
            ? source[start..end]
            : string.Empty;
    }

    private static string ExtractAsciiMarker(string source, string methodToken)
    {
        string method = ExtractMethod(source, methodToken);
        MatchCollection characters = Regex.Matches(
            method,
            @"\(byte\)'(?<value>.)'",
            RegexOptions.CultureInvariant);
        return string.Concat(
            characters
                .Cast<Match>()
                .Select(match => match.Groups["value"].Value));
    }

    private static int IndexOfAfter(string source, string value, int start)
    {
        return start < 0 || start > source.Length
            ? -1
            : source.IndexOf(value, start, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(
                   value,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string Sha256Text(string value)
    {
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private static string Sha256File(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[]
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory
        })
        {
            DirectoryInfo? current = new DirectoryInfo(start);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CLROS.slnx")) &&
                    File.Exists(Path.Combine(
                        current.FullName,
                        "src",
                        "OS",
                        "OS.csproj")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("CLROS repository root was not found.");
    }

    private readonly record struct ScriptResult(int ExitCode, string Output);
}
