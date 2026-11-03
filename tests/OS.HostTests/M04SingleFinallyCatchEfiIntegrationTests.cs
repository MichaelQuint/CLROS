#if SINGLE_FINALLY_CORE_TEST
using System.Reflection;
using OS.Kernel.Runtime;
using OS.Runtime;






























































































#else
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static class M04SingleFinallyCatchEfiIntegrationTests
{
    private const string Flag = "EnableM04SingleFinallyCatchProbe";
    private static readonly string[] Ancestry = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe", "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe", "EnableM04GcStaticRootMaterializationProbe"];
    private static string Root => HostToolProcess.FindRepositoryRoot();

    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }

    [HostTest("m04.021.finally.efi default authority", HostLane.Fast, 0)]
    private static unsafe void Defaults()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/OS/OS.csproj"));
        var defaults = project.Descendants(Flag).ToArray();
        Need(defaults.Length == 1 && defaults[0].Value == "false" && !Probes.EnableM04SingleFinallyCatchProbe && !BootSequence.IsSingleFinallyCatchEnabled(), "new profile defaults off independently of simple catch");
        int vectorCount = System.Text.RegularExpressions.Regex.Matches(project.ToString(), @"\$\((Enable\w+Probe)\)").Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).Count();
        var literalDefaults = project.Descendants().Where(x => x.Name.LocalName.StartsWith("Enable", StringComparison.Ordinal) && x.Attribute("Condition")?.Value == "'$(" + x.Name.LocalName + ")' == ''").ToArray();
        string[] expectedDefaults = OS.Tooling.EfiContracts.ProjectDefaultProbes.Order(StringComparer.Ordinal).ToArray();
        Need(vectorCount == OS.Tooling.EfiContracts.DiagnosticProbes.Count && literalDefaults.Select(x => x.Name.LocalName).Order(StringComparer.Ordinal).SequenceEqual(expectedDefaults) && literalDefaults.All(x => x.Value == "false"), "complete expanded profile vector with all dangerous literal defaults false");
        var probe = Probes.GetM04SingleFinallyCatchProbe();
        Need(probe.Name == Flag && !probe.EnabledByDefault && probe.Risk == ProbeRisk.Destructive, "explicit destructive diagnostic");
        var result = BootSequence.RunSingleFinallyCatchUnwind(default, default, null, 0, 0, null, 0, null, 0, default);
        Need(!result.Succeeded && result.Caller.Rip == 0 && result.Caller.Rsp == 0, "disabled boot authority cannot reach unwind with invalid inputs");
    }

    [HostTest("m04.021.finally.efi project switch admission", HostLane.Native, 1)]
    private static void Switches()
    {
        string[] chain = Ancestry.Select(x => "-p:" + x + "=true").ToArray();
        Check("default", [], 0, null);
        Check("enabled", [..chain, "-p:" + Flag + "=true"], 0, null);
        Check("invalid", [..chain, "-p:" + Flag + "=invalid"], 1, "m04_single_finally_switch_invalid");
        Check("missing-gc", ["-p:" + Flag + "=true"], 1, "m04_single_finally_requires_gc_profile");
        foreach (string collision in new[]
        {
            "EnableM04SimpleThrowCatchProbe",
            "EnableM04SeededCollectorChurnProbe",
            "EnableM04ClassConstructorExecutionProbe",
            "EnableM04InterfaceDispatchProbe",
            "EnableM04X64UnwindMetadataProbe"
        }

        )
        {
            string[] dependencies = collision == "EnableM04X64UnwindMetadataProbe" ? ["-p:EnableM04TypeManagerModuleRegistrationProbe=true"] : [];
            // The unwind profile requires the separate module-registration
            // profile, which the collecting GC ancestor already excludes.
            string reason = collision == "EnableM04X64UnwindMetadataProbe" ? "m04_gc_static_profile_conflict EnableM04TypeManagerModuleRegistrationProbe" : "m04_single_finally_profile_conflict";
            Check(collision, [..chain, ..dependencies, "-p:" + Flag + "=true", "-p:" + collision + "=true"], 1, reason);
        }
    }

    [HostTest("m04.021.finally.efi explicit profile dry run", HostLane.Fast, 2)]
    private static void Profile()
    {
        var result = HostToolProcess.RunTool(["qemu", "smoke", "--build-efi", "--dry-run", "--" + Flag, ..Ancestry.Select(x => "--" + x)]);
        Need(result.ExitCode == 0 && result.Error.Length == 0, result.Output + result.Error);
        string output = result.Output;
        string[] Read(string key) => output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))[key.Length..].TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] ordered = Read("ORDERED_MARKERS "), required = Read("REQUIRED_MARKERS "), unique = Read("UNIQUE_MARKERS ");
        string[] expected = new[]
        {
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS"
        }.Concat(ordered).Distinct(StringComparer.Ordinal).ToArray();
        Need(ordered.Length == OS.Tooling.QemuContracts.M04SingleFinallyRequiredMarkers.Count && required.SequenceEqual(expected, StringComparer.Ordinal) && unique.SequenceEqual(ordered, StringComparer.Ordinal), "complete ancestor and fourteen child markers");
        Need(ordered[51] == "TEST_START m04.021.single-finally" && ordered[64] == "CLROS_M04_SINGLE_FINALLY_READY" && ordered[65] == "TEST_START boot.sequence", "child follows collector and precedes boot");
        Need(Read("FORBIDDEN_MARKERS ").Contains("TEST_FAIL", StringComparer.Ordinal), "failure markers forbidden");
    }

    private static void Check(string name, string[] properties, int expectedExit, string? expectedReason) => RunCommand(name, "dotnet", ["msbuild", "src/OS/OS.csproj", "-nologo", "-t:ValidateM01ProbeSwitches", ..properties], expectedExit, expectedReason);
    [HostTest("m04.021.finally.efi actual admission guard core", HostLane.Native, 3)]
    private static void GuardCore()
    {
        string directory = Path.Combine(Root, "artifacts/finally-admission-core", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string project = Path.Combine(directory, "FinallyAdmission.Core.csproj");
        new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), new XElement("PropertyGroup", new XElement("OutputType", "Exe"), new XElement("TargetFramework", "net11.0"), new XElement("LangVersion", "14.0"), new XElement("Nullable", "enable"), new XElement("ImplicitUsings", "enable"), new XElement("AllowUnsafeBlocks", "true"), new XElement("EnableDefaultCompileItems", "false"), new XElement("DefineConstants", "SINGLE_FINALLY_CORE_TEST")), new XElement("ItemGroup", new[] { new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "src/OS/Runtime/SimpleCatchBase.cs"))), new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "src/OS/Runtime/SimpleCatchDerived.cs"))), new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "src/OS/Runtime/ExceptionCatchProbe.RhpNewFastAllocation.cs"))), new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "src/OS/Runtime/RhpNewFastAllocation.EhStorage.cs"))), new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "src/OS/Runtime/RhpNewFastAllocation.EhTransfer.cs"))), new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "src/OS/Runtime/RhpNewFastAllocation.EhControl.cs"))), new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "src/OS/Runtime/SingleFinallyAdmission.cs"))), new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "src/OS/Runtime/SingleFinallyAdmission.Control.cs"))) }, new[] { new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "tests/OS.HostTests/M04SingleFinallyCatchEfiIntegrationTests.cs"))), new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "tests/OS.HostTests/M04SingleFinallyCatchEfiIntegrationTests.FinallyAdmissionCoreProgram.cs"))) }, new XElement("ProjectReference", new XAttribute("Include", Path.Combine(Root, "src/OS.Kernel/OS.Kernel.csproj")))))).Save(project);
        RunCommand("guard-core-build", "dotnet", ["build", project, "--nologo", "-m:1", "--no-dependencies"], 0, null);
        string output = RunCommand("guard-core-run", "dotnet", ["run", "--project", project, "--no-build"], 0, "FINALLY_ADMISSION_CORE_PASS");
        Need(!output.Contains("TEST_FAIL ", StringComparison.Ordinal), "actual guard rejection cases pass");
    }

    [HostTest("m04.021.finally.efi strict complete finally transcript", HostLane.Fast, 4)]
    private static void Transcript() => EfiExceptionTranscriptControls.Run(finallyProfile: true);
    [HostTest("m04.021.finally.efi exact native clause and unwind guards", HostLane.Fast, 5)]
    private static void NativeMetadata()
    {
        const ulong type = 0x12340, tableRva = 0x45670;
        byte[] table = [0x04, 0x36, 0x32, 0x8e, 0x36, 0xa0, 0xbc, ..BitConverter.GetBytes((uint)type)];
        byte[] rootData = [0x01, 0x06, 0x03, 0x00, 0x06, 0x62, 0x02, 0x30, 0x01, 0x50, 0x04, ..BitConverter.GetBytes((uint)tableRva)];
        byte[] funclet = [0x01, 0x04, 0x01, 0x00, 0x04, 0x42, 0x01];
        Need(OS.Tooling.EfiVerify.TestSingleFinallyEhTable(table, type) && OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(rootData, true, tableRva) && OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(funclet, false, tableRva), "exact measured positive bytes");
        for (int i = 0; i < table.Length; i++)
        {
            byte[] bad = [..table];
            bad[i] ^= 1;
            Need(!OS.Tooling.EfiVerify.TestSingleFinallyEhTable(bad, type), "table byte mutation " + i);
        }

        foreach (bool root in new[]
        {
            true,
            false
        }

        )
        {
            byte[] original = root ? rootData : funclet;
            for (int i = 0; i < original.Length; i++)
            {
                byte[] bad = [..original];
                bad[i] ^= 1;
                Need(!OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(bad, root, tableRva), "unwind byte mutation " + i);
            }

            for (int length = 0; length < original.Length; length++)
                Need(!OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(original[..length], root, tableRva), "truncated unwind " + length);
            Need(!OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes([..original, 0], root, tableRva), "extended unwind");
        }

        for (int length = 0; length < table.Length; length++)
            Need(!OS.Tooling.EfiVerify.TestSingleFinallyEhTable(table[..length], type), "truncated table " + length);
        foreach (ulong badType in new[]
        {
            0UL,
            type + 1,
            (ulong)uint.MaxValue + 1
        }

        )
            Need(!OS.Tooling.EfiVerify.TestSingleFinallyEhTable(table, badType), "wrong type RVA");
        Need(!OS.Tooling.EfiVerify.TestSingleFinallyEhTable([..table, 0], type) && !OS.Tooling.EfiVerify.TestSingleFinallyEhTable(null, type) && !OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(rootData, true, tableRva + 1) && !OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(rootData, true, 0) && !OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(null, false, tableRva), "length null and table association");
    }

    private static string RunCommand(string name, string executable, string[] arguments, int expectedExit, string? expectedReason)
    {
        string folder = Path.Combine(Root, "artifacts/finally-project-admission", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);
        File.WriteAllText(Path.Combine(folder, "arguments.json"), JsonSerializer.Serialize(start.ArgumentList));
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        bool ended = process.WaitForExit(60000);
        if (!ended)
        {
            process.Kill(true);
            process.WaitForExit();
        }

        string output = stdout.GetAwaiter().GetResult(), error = stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(folder, "stdout.log"), output);
        File.WriteAllText(Path.Combine(folder, "stderr.log"), error);
        Console.WriteLine("FINALLY_PROJECT_ADMISSION case=" + name + " exit=" + process.ExitCode + " evidence=" + folder);
        Need(ended && process.ExitCode == expectedExit && error.Length == 0 && (expectedReason is null || output.Contains(expectedReason, StringComparison.Ordinal)), name + ": " + output + error);
        return output;
    }
}
#endif
