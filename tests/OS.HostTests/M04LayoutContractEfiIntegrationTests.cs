using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

internal static class M04LayoutContractEfiIntegrationTests
{
    private const string Flag = "EnableM04LayoutContractProbe";
    private const string ArithmeticFlag = "EnableM04ArithmeticConformanceProbe";
    private const string SpanFlag = "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe";
    private const string EndianFlag = "EnableM04IndependentEndianConformanceProbe";
    private const string Symbol = "M04_LAYOUT_CONTRACT_PROBE";
    private const string Ready = "CLROS_M04_005_READY profile=ilc10.0.8-win-x64-le checks=19 shapes=3 prefixBytes=24 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail";
    private const string Failed = "M04_LAYOUT_CONTRACT_FAILED";
    private const string Leaf = "src/OS.Kernel/Runtime/ObjectArrayStringEETypeMethodTableLayoutContracts.cs";
    private const string LeafTests = "tests/OS.HostTests/ObjectArrayStringEETypeMethodTableLayoutContractsTests.cs";
    private const string CompileHash = "54bfa32fa851eb9a5f96ee8f3b8d9bd3f916b01dc629d5497cb42d97f6d79993";
    // Reviewed source identity uses UTF-8 text with only CRLF normalized to LF.
    // EFI artifact and same-build source-authority hashes remain raw byte hashes.
    private const string LeafSourceHash = "44f8a34e2f1911aae4fd5b028adb88bf1d8bf06ce915625e9f13d11964f579aa";
    private const string LeafTestSourceHash = "d22ab6680e73713844c62e2fc66c4dc474f96955fd66d94740618de16dfb249b";

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m04.005.efi.integration");
        Run("frozen leaf and explicit default-off closure", TestProject);
        Run("source identity accepts line endings and rejects content changes", TestSourceIdentity);
        Run("no-stdlib metadata preserves NoInlining", TestNoStdLibMetadata);
        Run("compiled adapter accepts results and rejects each mismatch", TestAdapter);
        Run("build and verifier reject missing and conflicting prerequisites", TestBuildAndVerifierArguments);
        Run("profile propagation and sibling preservation", TestProfiles);
        Run("ordinal whole-line transcript and OVMF return", TestStrictTranscript);
        Run("frozen kernel source admission rejects mutations", TestSourceFirewall);
        Console.WriteLine("TEST_PASS m04.005.efi.integration");
    }

    private static void Run(string name, Action test)
    {
        Console.WriteLine("TEST_START m04.005.efi " + name);
        try
        {
            test();
            Console.WriteLine("TEST_PASS m04.005.efi " + name);
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL m04.005.efi " + name + " " + error.Message);
            throw;
        }
    }

    private static void TestProject()
    {
        string root = Root();
        XDocument project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        string[] keys = project.Descendants("Compile").Select(item =>
            item.Attribute("Include")!.Value + "|" + (item.Attribute("Link")?.Value ?? "")).ToArray();
        Require(keys.Length == 89 && Hash(Encoding.UTF8.GetBytes(string.Join('\n', keys) + "\n")) == CompileHash,
            "exact 88-item closure");
        Require(SourceIdentityHash(File.ReadAllText(Path.Combine(root, Leaf))) == LeafSourceHash &&
            SourceIdentityHash(File.ReadAllText(Path.Combine(root, LeafTests))) == LeafTestSourceHash, "reviewed source identities");
        XElement[] defaults = project.Descendants(Flag).ToArray();
        Require(defaults.Length == 1 && defaults[0].Value == "false" &&
            defaults[0].Attribute("Condition")?.Value == "'$(" + Flag + ")' == ''", "one false default");
        XElement symbol = project.Descendants("PropertyGroup").Single(group =>
            group.Attribute("Condition")?.Value == "'$(" + Flag + ")' == 'true'");
        Require(symbol.Elements().Single().Value == "$(DefineConstants);" + Symbol, "exact symbol");
        foreach ((string dependency, string relation) in new[]
        {
            (ArithmeticFlag, "!="), (SpanFlag, "!="), (EndianFlag, "!="), ("EnableM02OwnedGdtTssTerminalProbe", "=="), ("EnableM02Ud2VectorStateProbe", "==")
        })
        {
            string condition = "'$(" + Flag + ")' == 'true' and '$(" + dependency + ")' " + relation + " 'true'";
            Require(project.Descendants("Error").Count(error => error.Attribute("Condition")?.Value == condition) == 1,
                "project dependency " + dependency);
        }
        foreach (string file in new[] { "scripts/build-efi-shell.ps1", "scripts/test-efi-artifact.ps1" })
        {
            string source = File.ReadAllText(Path.Combine(root, file)).Replace("\r", "");
            Require(source.Contains("-p:" + Flag + "=true", StringComparison.Ordinal) &&
                source.Contains(Flag + ",\" +", StringComparison.Ordinal) &&
                source.Contains("ObjectArrayStringEETypeMethodTableLayoutContracts.cs|Runtime", StringComparison.Ordinal),
                "requested/evaluated/source profile: " + file);
        }
    }

    private static void TestSourceIdentity()
    {
        foreach ((string relative, string expected) in new[] { (Leaf, LeafSourceHash), (LeafTests, LeafTestSourceHash) })
        {
            string lf = File.ReadAllText(Path.Combine(Root(), relative)).Replace("\r\n", "\n", StringComparison.Ordinal);
            string crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
            Require(SourceIdentityHash(lf) == expected && SourceIdentityHash(crlf) == expected,
                "LF/CRLF preserve reviewed source identity: " + relative);
            Require(SourceIdentityHash(lf + " ") != expected &&
                SourceIdentityHash(lf + "// changed content\n") != expected &&
                SourceIdentityHash(lf + "\r") != expected,
                "spaces, content and lone carriage returns remain significant: " + relative);
        }
    }

    private static void TestNoStdLibMetadata()
    {
        string root = Root();
        string directory = Scratch("metadata");
        string project = Path.Combine(directory, "Metadata.csproj");
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion>
                <NoStdLib>true</NoStdLib><NoConfig>true</NoConfig>
                <DefineConstants>NOSTDLIB_EFI</DefineConstants>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>disable</ImplicitUsings>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
              </PropertyGroup>
              <ItemGroup>
            """ + Compile(root, Leaf) + Compile(root, "src/OS/Runtime/MinimalRuntime.cs") +
            Compile(root, "src/OS/Runtime/MethodImplAttribute.cs") + "</ItemGroup></Project>");
        ScriptResult build = Process(root, "dotnet", "build", project, "-c", "Release", "--nologo", "-m:1");
        Require(build.ExitCode == 0, build.Output);
        using FileStream file = File.OpenRead(Path.Combine(directory, "bin/Release/net10.0/Metadata.dll"));
        using PEReader pe = new(file);
        MetadataReader metadata = pe.GetMetadataReader();
        Require(metadata.AssemblyReferences.Count == 0, "no managed assembly references");
        MethodDefinition evaluate = metadata.MethodDefinitions.Select(metadata.GetMethodDefinition)
            .Single(method => metadata.GetString(method.Name) == "Evaluate");
        Require((evaluate.ImplAttributes & MethodImplAttributes.NoInlining) != 0, "NoInlining retained in IL metadata");
    }


    private static void TestAdapter()
    {
        string root = Root();
        string source = File.ReadAllText(Path.Combine(root, "src/OS/Boot/EfiEntry.cs")).Replace("\r", "");
        string call = "WriteM04LayoutContractProbe(console, serial)";
        int previous = source.IndexOf("WriteM04ArithmeticConformanceProbe(console, serial)", StringComparison.Ordinal);
        int invocation = source.IndexOf(call, previous, StringComparison.Ordinal);
        int map = source.IndexOf("TryCaptureUefiMemoryMap(systemTable)", invocation, StringComparison.Ordinal);
        Require(previous >= 0 && invocation > previous && map > invocation, "pre-map call order");
        string guard = "        if (!" + call + ")\n        {\n            return;\n        }\n";
        Require(source.Contains("#if " + Symbol + "\n" + guard + "#endif", StringComparison.Ordinal), "failure returns before later markers");
        int start = source.IndexOf("    private static bool WriteM04LayoutContractProbe(", StringComparison.Ordinal);
        string adapter = source[start..source.IndexOf("#endif", start, StringComparison.Ordinal)];
        Require(adapter.Split("!CheckM04LayoutResult(", StringSplitOptions.None).Length == 20, "19 actual checks");
        const string evaluator = "ObjectArrayStringEETypeMethodTableLayoutContracts.Evaluate(";
        Require(adapter.Split(evaluator, StringSplitOptions.None).Length == 2, "one actual evaluator seam");
        string directory = Scratch("adapter");
        File.WriteAllText(Path.Combine(directory, "Adapter.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
            <ImplicitUsings>enable</ImplicitUsings><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
            </PropertyGroup><ItemGroup><Compile Include="Program.cs"/>
            """ + Compile(root, Leaf) + "</ItemGroup></Project>");
        string fields = """
                private struct EfiSimpleTextOutputProtocol { }
                private struct EfiSerialIoProtocol { }
                private static readonly System.Text.StringBuilder Output = new();
                private static void Write(EfiSimpleTextOutputProtocol* c, EfiSerialIoProtocol* s, byte value) => Output.Append((char)value);
                private static void WriteCrLf(EfiSimpleTextOutputProtocol* c, EfiSerialIoProtocol* s) => Output.Append("\r\n");
            """;
        string program = "using OS.Kernel.Runtime;\nunsafe static class Actual {\n" + fields + adapter +
            "public static bool Run() => WriteM04LayoutContractProbe(null,null) && Output.ToString() == \"" + Ready + "\\r\\n\";\n}\n";
        program += "unsafe static class Controlled {\n" + fields + """
                private static int Calls, FaultAt, Field;
                private static NativeAotLayoutResult ReadResult(NativeAotLayoutProfile profile,
                    NativeAotMethodTablePrefix prefix, int count, ulong capacity)
                {
                    Calls++;
                    NativeAotLayoutResult r = ObjectArrayStringEETypeMethodTableLayoutContracts.Evaluate(profile, prefix, count, capacity);
                    if (Calls != FaultAt) return r;
                    return new NativeAotLayoutResult(
                        Field == 0 ? (r.Succeeded ? NativeAotLayoutStatus.Uninitialized : NativeAotLayoutStatus.Success) : r.Status,
                        Field == 1 ? (NativeAotLayoutShape)((byte)r.Shape + 1) : r.Shape,
                        Field == 2 ? (ushort)(r.ComponentSize ^ 1) : r.ComponentSize,
                        Field == 3 ? !r.ContainsGcPointers : r.ContainsGcPointers,
                        Field == 4 ? r.LengthOffsetFromReference + 1 : r.LengthOffsetFromReference,
                        Field == 5 ? r.DataOffsetFromReference + 1 : r.DataOffsetFromReference,
                        Field == 6 ? r.RawSizeBytes + 1 : r.RawSizeBytes,
                        Field == 7 ? r.AlignedSizeBytes + 1 : r.AlignedSizeBytes);
                }
                public static bool Run()
                {
                    FaultAt = 0; Calls = 0;
                    if (!WriteM04LayoutContractProbe(null,null) || Calls != 19 || Output.ToString() !=
            """ + "\n                        \"" + Ready + "\\r\\n\") return false;\n" + """
                    for (FaultAt = 1; FaultAt <= 19; FaultAt++)
                    for (Field = 0; Field < 8; Field++)
                    {
                        Calls = 0; Output.Clear(); Boot();
                        if (Calls != FaultAt || Output.ToString() != "M04_LAYOUT_CONTRACT_FAILED\r\n") return false;
                    }
                    return true;
                }
                private static void Boot()
                {
                    EfiSimpleTextOutputProtocol* console = null;
                    EfiSerialIoProtocol* serial = null;
            """ + "\n" + guard + "\nOutput.Append(\"UEFI_MEMORY_MAP_READY\\r\\nOS_BOOT_OK\\r\\nOS_SMOKE_PASS\\r\\n\");\n}\n" +
            adapter.Replace(evaluator, "ReadResult(", StringComparison.Ordinal) + "\n}\n" + """
            static class Program
            {
                static int Main()
                {
                    if (!Actual.Run() || !Controlled.Run()) return 1;
                    System.Console.WriteLine("TEST_PASS m04.005.efi.adapter.controlled mutations=152 calls=19");
                    return 0;
                }
            }
            """;
        File.WriteAllText(Path.Combine(directory, "Program.cs"), program);
        ScriptResult result = Process(root, "dotnet", "run", "--project", Path.Combine(directory, "Adapter.csproj"), "-c", "Release");
        File.WriteAllText(Path.Combine(directory, "run.log"), result.Output);
        Require(result.ExitCode == 0 && result.Output.Contains("mutations=152 calls=19", StringComparison.Ordinal), result.Output);
    }

    private static string[] EnabledArguments() => ["-BuildEfi", "-" + EndianFlag, "-" + SpanFlag, "-" + ArithmeticFlag, "-" + Flag];

    private static void TestProfiles()
    {
        string root = Root();
        ScriptResult profile = Qemu(root, EnabledArguments());
        Require(profile.ExitCode == 0, profile.Output);
        string[] required = Markers(profile.Output, "REQUIRED_MARKERS ");
        string[] ordered = Markers(profile.Output, "ORDERED_MARKERS ");
        string[] unique = Markers(profile.Output, "UNIQUE_MARKERS ");
        string[] forbidden = Markers(profile.Output, "FORBIDDEN_MARKERS ");
        Require(required.Length == 18 && ordered.Length == 6 && unique.Length == 4 && forbidden.Length == 6,
            "18/6/4/6 profile " + profile.Output);
        Require(ordered[0] == "EFI_CONSOLE_READY" && ordered[1].StartsWith("CLROS_M04_002_READY ", StringComparison.Ordinal) &&
            ordered[2].StartsWith("CLROS_M04_003_READY ", StringComparison.Ordinal) && ordered[3].StartsWith("CLROS_M04_004_READY ", StringComparison.Ordinal) &&
            ordered[4] == Ready && ordered[5] == "UEFI_MEMORY_MAP_READY" && unique.SequenceEqual(ordered[1..5]) &&
            forbidden.Contains(Failed) && forbidden.Contains("TEST_FAIL") && forbidden.Contains("OS_SMOKE_FAIL"), "exact profile lists");
        foreach ((string[] arguments, int count) in new[]
        {
            (Array.Empty<string>(), 12), (new[] { "-BuildEfi", "-" + EndianFlag }, 15),
            (new[] { "-BuildEfi", "-" + EndianFlag, "-" + SpanFlag }, 16),
            (new[] { "-BuildEfi", "-" + EndianFlag, "-" + SpanFlag, "-" + ArithmeticFlag }, 17)
        })
        {
            ScriptResult baseline = Qemu(root, arguments);
            Require(baseline.ExitCode == 0 && Markers(baseline.Output, "REQUIRED_MARKERS ").Length == count &&
                !Markers(baseline.Output, "REQUIRED_MARKERS ").Contains(Ready), "unchanged sibling profile " + count);
        }
        ScriptResult returning = Qemu(root, [.. EnabledArguments(), "-EnableM02NormalizedInterruptFramePageProbe"]);
        Require(returning.ExitCode == 0 && Markers(returning.Output, "REQUIRED_MARKERS ").Contains(Ready) &&
            Markers(returning.Output, "REQUIRED_MARKERS ").Any(m => m.StartsWith("CLROS_M02_010_READY ", StringComparison.Ordinal)), "returning M02 combination " + returning.Output);
        foreach (string missing in new[] { "-BuildEfi", "-" + EndianFlag, "-" + SpanFlag, "-" + ArithmeticFlag })
            Require(Qemu(root, EnabledArguments().Where(x => x != missing).ToArray()).ExitCode != 0, "reject missing " + missing);
        foreach (string disabled in new[] { "-" + EndianFlag, "-" + SpanFlag, "-" + ArithmeticFlag })
        {
            string command = "& '" + Path.Combine(root, "scripts/run-qemu-smoke.ps1").Replace("'", "''") + "' -DryRun " +
                string.Join(' ', EnabledArguments().Select(x => x == disabled ? x + ":$false" : x));
            ScriptResult explicitFalse = Process(root, "powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", command);
            Require(explicitFalse.ExitCode != 0 && explicitFalse.Output.Contains("m04_layout_contract_probe_requires_explicit_m04_chain", StringComparison.Ordinal), explicitFalse.Output);
        }
        foreach (string extra in new[] { "-SkipImageBuild", "-EnableM02OwnedGdtTssTerminalProbe", "-EnableM02Ud2VectorStateProbe" })
            Require(Qemu(root, [.. EnabledArguments(), extra]).ExitCode != 0, "reject conflicting " + extra);
        foreach (string argument in new[] { "-RequireMarker", "-RequireOrderedMarker", "-RequireUniqueMarker" })
            Require(Qemu(root, argument, Ready).ExitCode != 0, "marker requires typed profile " + argument);
    }

    private static void TestBuildAndVerifierArguments()
    {
        string root = Root();
        string[] switches = EnabledArguments()[1..];
        foreach (string script in new[] { "scripts/build-efi-shell.ps1", "scripts/test-efi-artifact.ps1" })
        {
            string[] preflight = script.Contains("build-efi", StringComparison.Ordinal)
                ? ["-PreflightOnly", "-LogPath", Path.Combine(Scratch("invalid-profile"), "preflight.log")] : [];
            foreach (string missing in new[] { "-" + EndianFlag, "-" + SpanFlag, "-" + ArithmeticFlag })
            {
                ScriptResult result = Process(root, "powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                    Path.Combine(root, script), .. preflight, .. switches.Where(x => x != missing)]);
                Require(result.ExitCode != 0 && result.Output.Contains("m04_layout_contract_requires_explicit_m04_chain", StringComparison.Ordinal), result.Output);
                // PowerShell -Command preserves explicit false SwitchParameter values on 5.1.
                string command = "& '" + Path.Combine(root, script).Replace("'", "''") + "' " +
                    string.Join(' ', preflight.Select(x => x.StartsWith("-", StringComparison.Ordinal) ? x : "'" + x.Replace("'", "''") + "'")) + " " +
                    string.Join(' ', switches.Select(x => x == missing ? x + ":$false" : x));
                ScriptResult explicitFalse = Process(root, "powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", command);
                Require(explicitFalse.ExitCode != 0 && explicitFalse.Output.Contains("m04_layout_contract_requires_explicit_m04_chain", StringComparison.Ordinal), explicitFalse.Output);
            }
            foreach (string terminal in new[] { "-EnableM02OwnedGdtTssTerminalProbe", "-EnableM02Ud2VectorStateProbe" })
            {
                ScriptResult result = Process(root, "powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                    Path.Combine(root, script), .. preflight, .. switches, terminal]);
                Require(result.ExitCode != 0 && result.Output.Contains("m04_layout_contract_forbids_", StringComparison.Ordinal), result.Output);
            }
        }
    }

    private static void TestStrictTranscript()
    {
        string root = Root();
        string source = File.ReadAllText(Path.Combine(root, "scripts/run-qemu-smoke.ps1"));
        string directory = Scratch("serial");
        string markerDefinitions = Between(source, "$script:M04IndependentEndianConformanceReadyMarker =", "function Invoke-NativeCommand {");
        string function = Between(source, "function Assert-M04LayoutContractTranscript {", "function Merge-QemuSmokeMarkerList {");
        Require(function.Contains("[System.StringComparison]::Ordinal)", StringComparison.Ordinal) &&
            !function.Contains("-ceq", StringComparison.Ordinal) && !function.Contains("-cne", StringComparison.Ordinal), "ordinal whole-line comparison");
        Require(source.Contains("if ($EnableM04LayoutContractProbe -and -not $DryRun -and $runnerExitCode -eq 0)", StringComparison.Ordinal) &&
            source.Contains("Assert-M04LayoutContractTranscript -SerialLogPath $SerialLog", StringComparison.Ordinal), "strict post-capture gate enabled only for layout");
        string[] chain = Markers(Qemu(root, EnabledArguments()).Output, "ORDERED_MARKERS ");
        string good = string.Join('\n', chain) + "\nOS_BOOT_OK\nTEST_PASS boot.sequence\nOS_SMOKE_PASS\n";
        List<(string Text, bool Accept)> cases = [(good, true), (good.Replace("\n", "\r\n", StringComparison.Ordinal), true)];
        // Exact accepted #507 serial bytes, including OVMF preamble/firmware return.
        // SHA256 0BC55D0331B587FEBC333283C4EC0F7F8F211B4747675C705511CF59D713AFFC.
        byte[] priorBytes = Convert.FromBase64String(PriorOvmfTranscript);
        Require(Hash(priorBytes) == "0bc55d0331b587febc333283c4ec0f7f8f211b4747675c705511cf59d713affc", "preserved OVMF fixture identity");
        string firmware = Encoding.UTF8.GetString(priorBytes).Replace("UEFI_MEMORY_MAP_READY\r\n", Ready + "\r\nUEFI_MEMORY_MAP_READY\r\n", StringComparison.Ordinal);
        cases.Add((firmware, true));
        foreach (string record in chain[1..5])
        {
            foreach (string changed in new[] { "", "prefix" + record, " " + record, record + " ", record + " diagnostic",
                record + "suffix", record + "\t", record[..^1], record.ToLowerInvariant(),
                record.Replace("hostConformance=false guestIntegrated=true", "hostConformance=true guestIntegrated=false", StringComparison.Ordinal) })
                cases.Add((good.Replace(record, changed, StringComparison.Ordinal), false));
            for (int control = 0; control <= 32; control++)
                cases.Add((good.Replace(record, record.Insert(7, ((char)control).ToString()), StringComparison.Ordinal), false));
            cases.Add((good.Replace(record, record.Insert(7, "\u007f"), StringComparison.Ordinal), false));
            cases.Add((good.Replace(record, record.Insert(7, "\u0085"), StringComparison.Ordinal), false));
            cases.Add((good + record + "\n", false));
            cases.Add((record + "\n" + good, false));
        }
        foreach (string token in new[] { "checks=19", "profile=ilc10.0.8-win-x64-le", "shapes=3", "prefixBytes=24", "gate4=Fail" })
            cases.Add((good.Replace(token, token + "x", StringComparison.Ordinal), false));
        cases.Add((good.Replace(chain[2] + "\n" + chain[3], chain[3] + "\n" + chain[2], StringComparison.Ordinal), false));
        cases.Add((good.Replace("OS_SMOKE_PASS\n", "OS_SMOKE_PASS", StringComparison.Ordinal), false));
        foreach (string bad in new[] { Failed, "TEST_FAIL m04.005 late", "OS_SMOKE_FAIL", "CLROS_M04_005_REA",
            "CLROS_M04_999_READY", "prefix" + Ready + " suffix", Ready.Insert(7, "\0"), "M04_UNKNOWN_RECORD" })
        {
            cases.Add((bad + "\n" + good, false));
            cases.Add((good + bad + "\n", false));
            cases.Add((firmware + "\n" + bad + "\n", false));
        }
        cases.Add((good + "OS_SMOKE_PASS\n", false));
        cases.Add((good + "BOOT_PHASE\tcorrupt\n", false));
        cases.Add(("\uFEFF" + good, false));
        cases.Add((good.Replace(Ready, Ready.Insert(7, "\uFEFF"), StringComparison.Ordinal), false));
        var files = cases.Select((c, i) => new { Path = Path.Combine(directory, i + ".log"), c.Accept }).ToArray();
        for (int i = 0; i < cases.Count; i++) File.WriteAllText(files[i].Path, cases[i].Text, new UTF8Encoding(false));
        string manifest = Path.Combine(directory, "cases.json");
        File.WriteAllText(manifest, System.Text.Json.JsonSerializer.Serialize(files));
        string harness = Path.Combine(directory, "Check.ps1");
        File.WriteAllText(harness, "$ErrorActionPreference='Stop'\n" + markerDefinitions + function +
            "\n$cases = Get-Content -Raw -LiteralPath '" + manifest.Replace("'", "''") + "' | ConvertFrom-Json\n" + """
            foreach ($case in $cases) {
                $accepted = $true
                try { Assert-M04LayoutContractTranscript -SerialLogPath $case.Path }
                catch { $accepted = $false }
                if ($accepted -ne $case.Accept) { throw ('serial case: ' + $case.Path) }
            }
            Write-Output ('TEST_PASS m04.005.efi.serial cases=' + $cases.Count)
            """);
        ScriptResult result = Process(root, "powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", harness);
        File.WriteAllText(Path.Combine(directory, "run.log"), result.Output);
        Require(result.ExitCode == 0 && result.Output.Contains("TEST_PASS m04.005.efi.serial", StringComparison.Ordinal), result.Output);
    }

    private static void TestSourceFirewall()
    {
        string root = Root();
        string directory = Scratch("firewall");
        string harness = Path.Combine(directory, "Check.ps1");
        File.WriteAllText(harness, """
            param([string] $Root)
            $ErrorActionPreference='Stop'
            $source = Join-Path $Root 'scripts/build-efi-shell.ps1'
            $tokens=$null; $errors=$null
            $ast=[System.Management.Automation.Language.Parser]::ParseFile($source,[ref]$tokens,[ref]$errors)
            if ($errors.Count -ne 0) { throw 'script parse' }
            foreach ($name in @('Get-M01Utf8Sha256','Get-NostdlibEfiActiveSource','Test-M01ActiveSourceSurface')) {
                $definition=$ast.Find({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name},$true)
                . ([scriptblock]::Create($definition.Extent.Text))
            }
            $name='..\OS.Kernel\Runtime\ObjectArrayStringEETypeMethodTableLayoutContracts.cs'
            $text=[IO.File]::ReadAllText((Join-Path $Root 'src/OS.Kernel/Runtime/ObjectArrayStringEETypeMethodTableLayoutContracts.cs'))
            $lf=$text.Replace("`r`n","`n")
            foreach ($valid in @($lf,$lf.Replace("`n","`r`n"))) {
                if (-not (Test-M01ActiveSourceSurface -SourceText $valid -SourceName $name).Succeeded) { throw 'line endings' }
            }
            foreach ($changed in @($lf+' ',$lf+"`r",$lf.Replace('10.0.8','10.0.9'),$lf+"`n// changed")) {
                if ((Test-M01ActiveSourceSurface -SourceText $changed -SourceName $name).Succeeded) { throw 'content identity' }
            }
            foreach ($other in @('..\OS.Kernel\Runtime\Other.cs',($name+' '),$name.ToLowerInvariant(),
                $name.Replace('\','/'),$name.Replace('Runtime\','Runtime\.\'),$name.Insert(10,[string][char]0))) {
                if ((Test-M01ActiveSourceSurface -SourceText $lf -SourceName $other).Succeeded) { throw 'path identity' }
            }
            Write-Output 'TEST_PASS m04.005.efi.firewall'
            """);
        ScriptResult result = Process(root, "powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", harness, "-Root", root);
        Require(result.ExitCode == 0 && result.Output.Contains("TEST_PASS m04.005.efi.firewall", StringComparison.Ordinal), result.Output);
    }

    private static string Between(string text, string first, string next)
    {
        int start = text.IndexOf(first, StringComparison.Ordinal);
        Require(start >= 0, "source start " + first);
        return text[start..text.IndexOf(next, start, StringComparison.Ordinal)];
    }

    private const string PriorOvmfTranscript =
        "G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNo" +
        "G1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNp" +
        "Um9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVG" +
        "SSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4" +
        "MCkNClRFU1RfU1RBUlQgZWZpLm1haW4NCkVGSV9DT05TT0xFX1JFQURZDQpDTFJPU19NMDRfMDAyX1JFQURZIHJlYWRWZWN0b3Jz" +
        "PTI1IHJldmVyc2VWZWN0b3JzPTcgYnl0ZVNlcmllcz01IHVpbnQxNlNlcmllcz03IGZpbmdlcnByaW50PUU4MUU0NDUxRThEOTk5" +
        "RjYgcmVmZXJlbmNlRmlsZXNSZXF1aXJlZD1mYWxzZSBob3N0Q29uZm9ybWFuY2U9ZmFsc2UgZ3Vlc3RJbnRlZ3JhdGVkPXRydWUg" +
        "Z2F0ZTQ9RmFpbA0KQ0xST1NfTTA0XzAwM19SRUFEWSB2ZWN0b3JzPTE2IHNwYW5WZWN0b3JzPTQgb3ZlcmxhcFZlY3RvcnM9NSBt" +
        "ZW1vcnlDYXN0VmVjdG9ycz0zIHVuc2FmZVdpZHRoVmVjdG9ycz00IGZpbmdlcnByaW50PUYwM0ZGNTlEMzZGMzlFQ0QgcG9pbnRl" +
        "cldpZHRoPTggcmVmZXJlbmNlRmlsZXNSZXF1aXJlZD1mYWxzZSBob3N0Q29uZm9ybWFuY2U9ZmFsc2UgZ3Vlc3RJbnRlZ3JhdGVk" +
        "PXRydWUgZ2F0ZTQ9RmFpbA0KQ0xST1NfTTA0XzAwNF9SRUFEWSB2ZWN0b3JzPTU3IGZpbmdlcnByaW50PTQ1RkRGQTI0REQ3MkVB" +
        "MjggcmVmZXJlbmNlRmlsZXNSZXF1aXJlZD1mYWxzZSBob3N0Q29uZm9ybWFuY2U9ZmFsc2UgZ3Vlc3RJbnRlZ3JhdGVkPXRydWUg" +
        "Z2F0ZTQ9RmFpbA0KVUVGSV9NRU1PUllfTUFQX1JFQURZDQpVRUZJX01FTU9SWV9NQVBfREVTQ1JJUFRPUl9DT1VOVCAxMDINClVF" +
        "RklfTUVNT1JZX01BUF9PV05FRA0KQk9PVF9IQU5ET0ZGX1JFQURZDQpVRUZJX01FTU9SWV9NQVBfTk9STUFMSVpFRA0KQk9PVF9N" +
        "RU1PUllfTUFQX0FDQ0VQVEVEDQpCT09UX01FTU9SWV9SRUdJT05TX1JFQURZDQpCT09UX01FTU9SWV9SRUdJT05fQ09VTlQgMTAy" +
        "DQpCT09UX1BBR0VfQUxMT0NBVE9SX1JFQURZIHN0YXR1cz1SZWFkeSByZWdpb25zPTEwMiBjb252ZW50aW9uYWxQYWdlcz01MjIz" +
        "NiBjb252ZW50aW9uYWxCeXRlcz0yMTM5NTg2NTYgZnJlZU5vZGVzPTkgZnJlZVBhZ2VzPTUyMjM2IGZyZWVCeXRlcz0yMTM5NTg2" +
        "NTYgcGFnZVNpemU9NDA5NiBzdGF0dXNDb2RlPTANCkJPT1RfQUNQSV9SU0RQX0hBTkRPRkZfUkVBRFkNClVFRklfQUNQSV9SU0RQ" +
        "X1JFQURZDQpURVNUX1NUQVJUIGJvb3Quc2VxdWVuY2UNCkJPT1RfUEhBU0UgUGhhc2VBLkF1ZGl0IFNUQVJUDQpCT09UX1BIQVNF" +
        "IFBoYXNlQS5BdWRpdCBQQVNTDQpCT09UX1BIQVNFIFBoYXNlQS5EaWFnbm9zdGljcyBTVEFSVA0KQk9PVF9QSEFTRSBQaGFzZUEu" +
        "RGlhZ25vc3RpY3MgUEFTUw0KT1NfQk9PVF9PSw0KVEVTVF9QQVNTIGJvb3Quc2VxdWVuY2UNCk9TX1NNT0tFX1BBU1MNCkJkc0R4" +
        "ZTogbG9hZGluZyBCb290MDAwMCAiVWlBcHAiIGZyb20gRnYoN0NCOEJEQzktRjhFQi00RjM0LUFBRUEtM0VFNEFGNjUxNkExKS9G" +
        "dkZpbGUoNDYyQ0FBMjEtNzYxNC00NTAzLTgzNkUtOEFCNkY0NjYyMzMxKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMCAiVWlB" +
        "cHAiIGZyb20gRnYoN0NCOEJEQzktRjhFQi00RjM0LUFBRUEtM0VFNEFGNjUxNkExKS9GdkZpbGUoNDYyQ0FBMjEtNzYxNC00NTAz" +
        "LTgzNkUtOEFCNkY0NjYyMzMxKQ0KG1syShtbMDE7MDFIG1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzBtG1szMG0bWzQw" +
        "bRtbMDE7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAbWzAyOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swMzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDQ7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzA1OzAxSCAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNjswMUgg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgIBtbMDc7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAbWzA4OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swOTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTA7MDFIICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzExOzAxSCAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMjsw" +
        "MUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgIBtbMTM7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAbWzE0OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTY7MDFIICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE3OzAxSCAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sx" +
        "ODswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgIBtbMTk7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAbWzIwOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjI7MDFIICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIzOzAxSCAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "G1syNDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgIBtbMjU7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDE7MDFIG1swbRtbMzRtG1s0N20bWzAxOzAxSCAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swMjswMUgg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgIBtbMDM7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAbWzA0OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swNTswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDY7MDFIICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzAxOzAxSBtbMDI7MDJIU3Rh" +
        "bmRhcmQgUEMgKFEzNSArIElDSDksIDIwMDkpG1swMzswMkhwYy1xMzUtMTEuMRtbMDM7NTRIMi4wMCBHSHobWzA0OzAySGVkazIt" +
        "c3RhYmxlMjAyNDA4LXByZWJ1aWx0LnFlbXUub3JnG1swNDs1NEgyNTYgTUIgUkFNG1swNTswMkgbWzA1OzU0SBtbMDY7MDJIG1sw" +
        "Njs1NEgbWzBtG1szN20bWzQwbRtbMjE7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIyOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMzswMUggICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjQ7MDFIICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzIx" +
        "OzAxSBtbMG0bWzMwbRtbNDBtG1syNTs1M0ggIBtbMjU7NTRIICAbWzI1OzU1SCAgG1syNTs1NkggIBtbMjU7NTdIICAbWzI1OzU4" +
        "SCAgG1syNTs1OUggIBtbMjU7NjBIICAbWzI1OzYxSCAgG1syNTs2MkggIBtbMjU7NjNIICAbWzI1OzY0SCAgG1syNTs2NUggIBtb" +
        "MjU7NjZIICAbWzI1OzY3SCAgG1syNTs2OEggIBtbMjU7NjlIICAbWzI1OzcwSCAgG1syNTs3MUggIBtbMjU7NzJIICAbWzI1Ozcz" +
        "SCAgG1swbRtbMzBtG1s0N20bWzA3OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swODswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDk7MDFIICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzEwOzAxSCAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxMTsw" +
        "MUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgIBtbMTI7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAbWzEzOzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTU7MDFIICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAbWzE2OzAxSCAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sx" +
        "NzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgIBtbMTg7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAbWzE5OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMDc7MDFIG1swNzswMUggICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMG0b" +
        "WzM3bRtbNDBtG1swODszMUg8U3RhbmRhcmQgRW5nbGlzaD4bWzBtG1szMG0bWzQ3bRtbMDg7NDlIICAgICAgICAgG1swODswMUgg" +
        "ICAbWzA4OzA0SFNlbGVjdCBMYW5ndWFnZSAgICAgICAgICAgIBtbMG0bWzM0bRtbNDdtG1swOTswMUggICAbWzA5OzA0SCAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMG0bWzMwbRtbNDdtG1sxMDswMUggICAb" +
        "WzEwOzAySOKWuhtbMTA7MDRIRGV2aWNlIE1hbmFnZXIgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sx" +
        "MTswMUggICAbWzExOzAySOKWuhtbMTE7MDRIQm9vdCBNYW5hZ2VyICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgG1sxMjswMUggICAbWzEyOzAySOKWuhtbMTI7MDRIQm9vdCBNYWludGVuYW5jZSBNYW5hZ2VyICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgG1swbRtbMzRtG1s0N20bWzEzOzAxSCAgIBtbMTM7MDRIICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgG1swbRtbMzBtG1s0N20bWzE0OzAxSCAgIBtbMTQ7MDRIQ29udGludWUgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNTswMUggICAbWzE1OzA0SFJlc2V0ICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIBtbMTY7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sxNzswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgICAgICAgICAbWzE4OzAxSCAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgIBtbMTk7MDFIICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgG1sy" +
        "MDswMUggICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
        "ICAgICAgICAgIBtbMG0bWzM3bRtbNDBtG1syMzswMkggG1syMjswMkggG1syMjs1M0ggICAgICAgICAgICAgICAgICAgICAgICAg" +
        "G1syMjsyN0ggICAgICAgICAgICAgICAgICAgICAgICAgIBtbMjM7NTNIICAgICAgICAgICAgICAgICAgICAgICAgICAgG1syMzsw" +
        "M0jihpHihpM9TW92ZSBIaWdobGlnaHQgICAgICAgG1syMjswM0ggICAgICAgICAgICAgICAgICAgICAgICAbWzIzOzI3SDxFbnRl" +
        "cj49U2VsZWN0IEVudHJ5ICAgICAgG1swbRtbMzRtG1s0N20bWzA4OzU4SFRoaXMgaXMgdGhlIG9wdGlvbiAgICAgG1swOTs1OEhv" +
        "bmUgYWRqdXN0cyB0byBjaGFuZ2UgIBtbMTA7NThIdGhlIGxhbmd1YWdlIGZvciB0aGUgICAbWzExOzU4SGN1cnJlbnQgc3lzdGVt" +
        "ICAgICAgICAgG1sxMjs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTM7NThIICAgICAgICAgICAgICAgICAgICAgICAbWzE0" +
        "OzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxNTs1OEggICAgICAgICAgICAgICAgICAgICAgIBtbMTY7NThIICAgICAgICAg" +
        "ICAgICAgICAgICAgICAbWzE3OzU4SCAgICAgICAgICAgICAgICAgICAgICAgG1sxODs1OEggICAgICAgICAgICAgICAgICAgICAg" +
        "IBtbMTk7NThIICAgICAgICAgICAgICAgICAgICAgICAbWzE5OzgwSA==";

    private static string Compile(string root, string relative) =>
        "<Compile Include=\"" + SecurityElement.Escape(Path.Combine(root, relative)) + "\"/>";

    private static ScriptResult Qemu(string root, params string[] arguments) =>
        Process(root, "powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            Path.Combine(root, "scripts/run-qemu-smoke.ps1"), "-DryRun", .. arguments]);

    private static ScriptResult Process(string root, string executable, params string[] arguments)
    {
        ProcessStartInfo start = new(executable)
        {
            WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(executable + " exceeded 120 seconds");
        }
        Task.WaitAll(output, error);
        return new(process.ExitCode, output.Result + error.Result);
    }

    private static string[] Markers(string output, string prefix)
    {
        string value = output.Replace("\r", "").Split('\n').Single(line => line.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..];
        return value == "none" ? [] : value.Split(';');
    }

    private static string Scratch(string name)
    {
        string directory = Path.Combine(Root(), "artifacts/issue512-tests", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string Root()
    {
        for (DirectoryInfo? current = new(Environment.CurrentDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "CLROS.slnx"))) return current.FullName;
        throw new DirectoryNotFoundException("CLROS repository root");
    }

    private static string SourceIdentityHash(string text) =>
        Hash(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal)));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool accepted, string reason) { if (!accepted) throw new InvalidOperationException(reason); }
    private readonly record struct ScriptResult(int ExitCode, string Output);
}
