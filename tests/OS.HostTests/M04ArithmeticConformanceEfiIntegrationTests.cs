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

internal static partial class M04ArithmeticConformanceEfiIntegrationTests
{
    private const string Flag = "EnableM04ArithmeticConformanceProbe";
    private const string SpanFlag = "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe";
    private const string EndianFlag = "EnableM04IndependentEndianConformanceProbe";
    private const string Symbol = "M04_ARITHMETIC_CONFORMANCE_PROBE";
    private const string Ready = OS.Tooling.QemuContracts.Record277;
    private const string Failed = OS.Tooling.QemuContracts.Record276;
    private const string Leaf = "std/no-runtime/shared/Numerics/IndependentArithmeticShiftCastN64BitDivisionRemainder.cs";
    private static readonly string CompileHash = OS.Tooling.EfiContracts.CompileInventorySha256;
    // Reviewed source identity uses UTF-8 text with only CRLF normalized to LF.
    // EFI artifact and same-build source-authority hashes remain raw byte hashes.
    private static readonly string LeafSourceHash = OS.Tooling.EfiContracts.SourceIdentities[@"..\..\std\no-runtime\shared\Numerics\IndependentArithmeticShiftCastN64BitDivisionRemainder.cs"];
    [HostTest("m04.004.efi frozen leaf and explicit default-off closure", HostLane.Fast, 0)]
    private static void TestProject()
    {
        string root = Root();
        XDocument project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        string[] keys = project.Descendants("Compile").Select(item => item.Attribute("Include")!.Value + "|" + (item.Attribute("Link")?.Value ?? "")).ToArray();
        Require(keys.Length == OS.Tooling.EfiContracts.CompileItems.Count && Hash(Encoding.UTF8.GetBytes(string.Join('\n', keys) + "\n")) == CompileHash, "reviewed explicit compile inventory");
        Require(SourceIdentityHash(File.ReadAllText(Path.Combine(root, Leaf))) == LeafSourceHash, "reviewed production source identity");
        HostCheck.Registered(typeof(IndependentArithmeticShiftCastN64BitDivisionRemainderTests));
        XElement[] defaults = project.Descendants(Flag).ToArray();
        Require(defaults.Length == 1 && defaults[0].Value == "false" && defaults[0].Attribute("Condition")?.Value == "'$(" + Flag + ")' == ''", "one false default");
        XElement symbol = project.Descendants("PropertyGroup").Single(group => group.Attribute("Condition")?.Value == "'$(" + Flag + ")' == 'true'");
        Require(symbol.Elements().Single().Value == "$(DefineConstants);" + Symbol, "exact symbol");
        foreach ((string dependency, string relation)in new[]
        {
            (SpanFlag, "!="),
            ("EnableM02OwnedGdtTssTerminalProbe", "=="),
            ("EnableM02Ud2VectorStateProbe", "==")
        }

        )
        {
            string condition = "'$(" + Flag + ")' == 'true' and '$(" + dependency + ")' " + relation + " 'true'";
            Require(project.Descendants("Error").Count(error => error.Attribute("Condition")?.Value == condition) == 1, "project dependency " + dependency);
        }

        foreach (bool build in new[]
        {
            true,
            false
        }

        )
        {
            var profile = OS.Tooling.EfiOptions.Parse(["--" + EndianFlag, "--" + SpanFlag, "--" + Flag], build).Profile();
            foreach (string flag in new[]
            {
                EndianFlag,
                SpanFlag,
                Flag
            }

            )
                Require(profile.IsEnabled(flag), "explicit selected profile " + flag);
        }
    }

    [HostTest("m04.004.efi source identity accepts line endings and rejects content changes", HostLane.Fast, 1)]
    private static void TestSourceIdentity()
    {
        foreach ((string relative, string expected)in new[]
        {
            (Leaf, LeafSourceHash)
        }

        )
        {
            string lf = File.ReadAllText(Path.Combine(Root(), relative)).Replace("\r\n", "\n", StringComparison.Ordinal);
            string crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
            Require(SourceIdentityHash(lf) == expected && SourceIdentityHash(crlf) == expected, "LF/CRLF preserve reviewed source identity: " + relative);
            Require(SourceIdentityHash(lf + " ") != expected && SourceIdentityHash(lf + "// changed content\n") != expected && SourceIdentityHash(lf + "\r") != expected, "spaces, content and lone carriage returns remain significant: " + relative);
        }
    }

    [HostTest("m04.004.efi no-stdlib metadata preserves NoInlining", HostLane.Native, 2)]
    private static void TestNoStdLibMetadata()
    {
        string root = Root();
        string directory = Scratch("metadata");
        string project = Path.Combine(directory, "Metadata.csproj");
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
                <NoStdLib>true</NoStdLib><NoConfig>true</NoConfig>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>disable</ImplicitUsings>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
              </PropertyGroup>
              <ItemGroup>
            """ + Compile(root, Leaf) + Compile(root, "src/OS/Runtime/MinimalRuntime.cs") + Compile(root, "src/OS/Runtime/MethodImplAttribute.cs") + "</ItemGroup></Project>");
        ScriptResult build = Process(root, "dotnet", "build", project, "-c", "Release", "--nologo", "-m:1");
        Require(build.ExitCode == 0, build.Output);
        using FileStream file = File.OpenRead(Path.Combine(directory, "bin/Release/net11.0/Metadata.dll"));
        using PEReader pe = new(file);
        MetadataReader metadata = pe.GetMetadataReader();
        Require(metadata.AssemblyReferences.Count == 0, "no managed assembly references");
        MethodDefinition evaluate = metadata.MethodDefinitions.Select(metadata.GetMethodDefinition).Single(method => metadata.GetString(method.Name) == "Evaluate");
        Require((evaluate.ImplAttributes & MethodImplAttributes.NoInlining) != 0, "NoInlining retained in IL metadata");
    }

    [HostTest("m04.004.efi compiled adapter accepts results and rejects each mismatch", HostLane.Native, 3)]
    private static void TestAdapter()
    {
        string root = Root();
        string source = File.ReadAllText(Path.Combine(root, "src/OS/Boot/EfiEntry.cs")).Replace("\r", "");
        string call = "WriteM04ArithmeticConformanceProbe(console, serial)";
        int previous = source.IndexOf("WriteM04SpanMemoryConformanceProbe(console, serial)", StringComparison.Ordinal);
        int invocation = source.IndexOf(call, previous, StringComparison.Ordinal);
        int map = source.IndexOf("TryCaptureUefiMemoryMap(systemTable)", invocation, StringComparison.Ordinal);
        Require(previous >= 0 && invocation > previous && map > invocation, "pre-map call order");
        Require(source.Contains("#if " + Symbol + "\n        if (!" + call + ")\n        {\n            return;\n        }\n#endif", StringComparison.Ordinal), "failure suppresses subsequent boot markers");
        int start = source.IndexOf("    private static bool WriteM04ArithmeticConformanceProbe(", StringComparison.Ordinal);
        int end = source.IndexOf("#endif", start, StringComparison.Ordinal);
        string adapter = source[start..end];
        string catalog = "OS.Std.Numerics.IndependentArithmeticShiftCastN64BitDivisionRemainder\n                .RunCatalog()";
        Require(adapter.Split(catalog, StringSplitOptions.None).Length == 2, "one actual catalog call");
        string directory = Scratch("adapter");
        File.WriteAllText(Path.Combine(directory, "Adapter.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework>
                <LangVersion>14.0</LangVersion><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                <ImplicitUsings>enable</ImplicitUsings><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup><ItemGroup><Compile Include="Program.cs"/>
            """ + Compile(root, Leaf) + "</ItemGroup></Project>");
        // Compile the actual adapter body and marker writers. Only the catalog
        // call is injectable, so each result gate is exercised without firmware.
        File.WriteAllText(Path.Combine(directory, "Program.cs"), """
            using OS.Std.Numerics;
            unsafe static class Program
            {
                private struct EfiSimpleTextOutputProtocol { }
                private struct EfiSerialIoProtocol { }
                private static readonly System.Text.StringBuilder Output = new();
                private static ArithmeticConformanceResult Result;
                private static ArithmeticConformanceResult ReadResult() => Result;
                private static void Write(EfiSimpleTextOutputProtocol* c, EfiSerialIoProtocol* s, byte value) => Output.Append((char)value);
                private static void WriteCrLf(EfiSimpleTextOutputProtocol* c, EfiSerialIoProtocol* s) => Output.Append("\r\n");
                private static int Main()
                {
                    Result = IndependentArithmeticShiftCastN64BitDivisionRemainder.RunCatalog();
                    if (!WriteM04ArithmeticConformanceProbe(null, null) || Output.ToString() !=
            """ + "\n                        \"" + Ready + "\\r\\n\") return 1;\n" + """
                    ArithmeticConformanceResult[] invalid = [default,
                        new(ArithmeticConformanceStatus.UnexpectedValue, 57, -1, 0x45FDFA24DD72EA28UL),
                        new(ArithmeticConformanceStatus.Passed, 56, -1, 0x45FDFA24DD72EA28UL),
                        new(ArithmeticConformanceStatus.Passed, 57, 0, 0x45FDFA24DD72EA28UL),
                        new(ArithmeticConformanceStatus.Passed, 57, -1, 0x45FDFA24DD72EA29UL)];
                    foreach (var invalidResult in invalid)
                    {
                        Result = invalidResult; Output.Clear();
                        if (WriteM04ArithmeticConformanceProbe(null, null) ||
                            Output.ToString() != "M04_ARITHMETIC_CONFORMANCE_FAILED\r\n") return 2;
                    }
                    System.Console.WriteLine("TEST_PASS m04.004.efi.adapter.controlled");
                    return 0;
                }
            """ + "\n" + adapter.Replace(catalog, "ReadResult()", StringComparison.Ordinal) + "\n}");
        ScriptResult result = Process(root, "dotnet", "run", "--project", Path.Combine(directory, "Adapter.csproj"), "-c", "Release");
        Require(result.ExitCode == 0 && result.Output.Contains("TEST_PASS m04.004.efi.adapter.controlled", StringComparison.Ordinal), result.Output);
    }

    [HostTest("m04.004.efi profile propagation and controlled serial rejection", HostLane.Fast, 5)]
    private static void TestProfilesAndSerial()
    {
        string root = Root();
        string[] enabled = ["-BuildEfi", "-" + EndianFlag, "-" + SpanFlag, "-" + Flag];
        ScriptResult profile = Qemu(root, enabled);
        Require(profile.ExitCode == 0, profile.Output);
        string[] required = Markers(profile.Output, "REQUIRED_MARKERS ");
        string[] ordered = Markers(profile.Output, "ORDERED_MARKERS ");
        string[] unique = Markers(profile.Output, "UNIQUE_MARKERS ");
        string[] forbidden = Markers(profile.Output, "FORBIDDEN_MARKERS ");
        Require(required.Length == 17 && ordered.Length == 5 && unique.Length == 3 && forbidden.Length == 5, "17/5/3/5 enabled profile");
        Require(ordered[0] == "EFI_CONSOLE_READY" && ordered[1].StartsWith("CLROS_M04_002_READY ", StringComparison.Ordinal) && ordered[2].StartsWith("CLROS_M04_003_READY ", StringComparison.Ordinal) && ordered[3] == Ready && ordered[4] == "UEFI_MEMORY_MAP_READY" && unique.SequenceEqual(ordered[1..4]) && forbidden.Contains(Failed), "exact order and uniqueness");
        foreach ((string[] arguments, int count)in new[]
        {
            (Array.Empty<string>(), 12),
            (new[] { "-BuildEfi", "-" + EndianFlag }, 15),
            (new[] { "-BuildEfi", "-" + EndianFlag, "-" + SpanFlag }, 16)
        }

        )
        {
            ScriptResult baseline = Qemu(root, arguments);
            Require(baseline.ExitCode == 0 && Markers(baseline.Output, "REQUIRED_MARKERS ").Length == count && !Markers(baseline.Output, "REQUIRED_MARKERS ").Contains(Ready), "unchanged older profile " + count);
        }

        foreach ((string[] arguments, string reason)in new[]
        {
            (new[] { "-BuildEfi", "-" + Flag }, "m04_arithmetic_conformance_requires_span_memory_conformance"),
            (new[] { "-BuildEfi", "-" + SpanFlag, "-" + Flag }, "m04_span_memory_conformance_requires_independent_endian_conformance"),
            (enabled[1..], "qemu_profile_requires_build_efi"),
            (enabled.Concat(new[] { "-SkipImageBuild" }).ToArray(), "qemu_profile_forbids_skip_image_build"),
            (enabled.Concat(new[] { "-EnableM02OwnedGdtTssTerminalProbe" }).ToArray(), "m04_arithmetic_conformance_forbids_owned_gdt_tss_terminal"),
            (enabled.Concat(new[] { "-EnableM02Ud2VectorStateProbe" }).ToArray(), "m04_arithmetic_conformance_forbids_m02_ud2_vector_state")
        }

        )
        {
            ScriptResult rejected = Qemu(root, arguments);
            Require(rejected.ExitCode != 0 && rejected.Output.Contains("TEST_FAIL qemu.smoke " + reason, StringComparison.Ordinal), rejected.Output);
        }

        ScriptResult combined = Qemu(root, [..enabled, "-EnableM02NormalizedInterruptFramePageProbe"]);
        Require(combined.ExitCode == 0 && Markers(combined.Output, "ORDERED_MARKERS ").Contains(Ready), "returning M02 combination");
        string stream = string.Join('\n', ordered.Concat(required.Where(marker => !ordered.Contains(marker) && marker != "OS_SMOKE_PASS"))) + "\nOS_SMOKE_PASS\n";
        SerialMarkerCompletionResult Evaluate(string input)
        {
            SerialMarkerStateMachine machine = new(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            byte[] bytes = Encoding.ASCII.GetBytes(input);
            for (int offset = 0; offset < bytes.Length; offset += 7)
                machine.Feed(bytes.AsSpan(offset, Math.Min(7, bytes.Length - offset)));
            return machine.Complete();
        }

        Require(Evaluate(stream).Passed, "controlled success");
        foreach (string replacement in new[]
        {
            "",
            Ready.Replace("vectors=57", "vectors=56", StringComparison.Ordinal),
            Ready.Replace("45FDFA24DD72EA28", "45FDFA24DD72EA29", StringComparison.Ordinal),
            Ready.Replace("hostConformance=false guestIntegrated=true", "hostConformance=true guestIntegrated=false", StringComparison.Ordinal),
            "prefix" + Ready,
            Ready + "suffix",
            Ready.Insert(30, "\0"),
            Ready[..^1]
        }

        )
            Require(!Evaluate(stream.Replace(Ready, replacement, StringComparison.Ordinal)).Passed, "reject altered/partial/noisy guest record");
        Require(!Evaluate(stream.Replace(Ready, Ready + "\n" + Ready, StringComparison.Ordinal)).Passed, "duplicate");
        Require(!Evaluate(stream.Replace(ordered[2] + "\n" + Ready, Ready + "\n" + ordered[2], StringComparison.Ordinal)).Passed, "wrong order");
        Require(!Evaluate(stream + Failed + "\n").Passed, "late failure sentinel");
        Require(!Evaluate(stream + "CLROS_M04_004_REA").Passed, "partial terminal-drain record");
    }

    [HostTest("m04.004.efi build and verifier reject missing and conflicting prerequisites", HostLane.Fast, 4)]
    private static void TestBuildAndVerifierArguments()
    {
        foreach (bool build in new[]
        {
            true,
            false
        }

        )
            foreach ((string[] arguments, string reason)in new[]
            {
                (new[] { "--" + Flag }, "m04_arithmetic_conformance_requires_span_memory_conformance"),
                (new[] { "--" + Flag, "--" + SpanFlag, "--" + EndianFlag, "--EnableM02OwnedGdtTssTerminalProbe" }, "m04_arithmetic_conformance_forbids_owned_gdt_tss_terminal"),
                (new[] { "--" + Flag, "--" + SpanFlag, "--" + EndianFlag, "--EnableM02Ud2VectorStateProbe" }, "m04_arithmetic_conformance_forbids_m02_ud2_vector_state")
            }

            )
            {
                var result = HostToolProcess.ValidateEfiProfile(build, arguments);
                Require(result.ExitCode != 0 && result.Output.Contains(reason, StringComparison.Ordinal), result.Output);
            }
    }

    private static string Compile(string root, string relative) => "<Compile Include=\"" + SecurityElement.Escape(Path.Combine(root, relative)) + "\"/>";
    private static ScriptResult Qemu(string root, params string[] arguments)
    {
        var result = HostToolProcess.RunTool(["qemu", "smoke", "--boot-policy", "Diagnostic", "--dry-run", ..arguments]);
        return new(result.ExitCode, result.Output + result.Error);
    }

    private static ScriptResult Process(string root, string executable, params string[] arguments)
    {
        ProcessStartInfo start = new(executable)
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);
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
        string directory = Path.Combine(Root(), "artifacts/issue507-tests", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string Root()
    {
        for (DirectoryInfo? current = new(Environment.CurrentDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "CLROS.slnx")))
                return current.FullName;
        throw new DirectoryNotFoundException("CLROS repository root");
    }

    private static string SourceIdentityHash(string text) => Hash(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal)));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool accepted, string reason)
    {
        if (!accepted)
            throw new InvalidOperationException(reason);
    }
}
