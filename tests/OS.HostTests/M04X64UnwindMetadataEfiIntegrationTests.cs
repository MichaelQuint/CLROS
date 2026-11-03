using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Tools.QemuRunner;
using OS.Tooling;

internal static unsafe class M04X64UnwindMetadataEfiIntegrationTests
{
    private const string Flag = "EnableM04X64UnwindMetadataProbe";

    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }

    [HostTest("m04.020.efi original module and disabled boot boundary", HostLane.Fast, 0)]
    private static void Source()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        var defaults = project.Descendants(Flag).ToArray();
        Need(defaults.Length == 1 && defaults[0].Value == "false" &&
            (string?)defaults[0].Attribute("Condition") == "'$(" + Flag + ")' == ''", "one disabled default");
        Need(project.Descendants("PropertyGroup").Single(x =>
            (string?)x.Attribute("Condition") == "'$(" + Flag + ")' == 'true'")
            .Element("DefineConstants")!.Value == "$(DefineConstants);M04_X64_UNWIND_METADATA_PROBE", "explicit child symbol");
        string[] errors = project.Descendants("Error").Select(x => (string)x.Attribute("Condition")!).ToArray();
        foreach (string condition in new[] {
            "'$(" + Flag + ")' != '' and '$(" + Flag + ")' != 'true' and '$(" + Flag + ")' != 'false'",
            "'$(" + Flag + ")' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' != 'true'",
            "'$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '$(EnableM01LoadedImageStackProbe)' != 'true'" })
            Need(errors.Count(x => x == condition) == 1, "exact original-module prerequisite or value guard");
        string[] compile = project.Descendants("Compile").Select(x => (string)x.Attribute("Include")!).ToArray();
        foreach (string source in new[] {
            "..\\OS.Kernel\\Runtime\\X64PERuntimeFunctionUnwindMetadataHostileInput.cs",
            "Runtime\\X64UnwindMetadataProbe.cs" })
            Need(compile.Count(x => x == source) == 1, "one explicit metadata source " + source);

        Need(!Probes.EnableM04X64UnwindMetadataProbe &&
            OS.Boot.BootSequence.RunX64UnwindMetadata(null, 0, 0).Status == X64UnwindMetadataStatus.Uninitialized,
            "disabled host boundary does not consume supplied pointers");
        var definition = Probes.GetM04X64UnwindMetadataProbe();
        Need(!definition.EnabledByDefault && definition.Risk == ProbeRisk.Destructive, "dangerous probe remains disabled");
        Need(X64PERuntimeFunctionUnwindMetadataHostileInput.StateBytes == 288,
            "actual x64 retained-state geometry exercised by transcript mutations below");
        string probe = File.ReadAllText(Path.Combine(root, "src/OS/Runtime/X64UnwindMetadataProbe.cs"));
        string normalizedProbe = string.Concat(probe.Where(character => !char.IsWhiteSpace(character)));
        Need(TypeManagerModuleMetadataLiveNativeAOTImage.SlotBytes == 16 &&
            normalizedProbe.Contains("original.Length!=(int)TypeManagerModuleMetadataLiveNativeAOTImage.SlotBytes", StringComparison.Ordinal) &&
            normalizedProbe.Contains("retained.Length!=(int)TypeManagerModuleMetadataLiveNativeAOTImage.SlotBytes", StringComparison.Ordinal),
            "initial and retained section 204 checks consume the current two-word indirection extent");
        Need(probe.StartsWith("#if NOSTDLIB_EFI && M04_X64_UNWIND_METADATA_PROBE", StringComparison.Ordinal) &&
            probe.Contains("[System.Runtime.RuntimeExport(\"__security_cookie\")]", StringComparison.Ordinal) &&
            probe.Split("SecurityCookieStorageAnchor(", StringSplitOptions.None).Length == 2,
            "one profile-only cookie anchor with no calls");
        string owner = File.ReadAllText(Path.Combine(root, "src/OS/Runtime/NativeAotModuleRegistrationProbe.cs"));
        int child = owner.IndexOf("if (!X64UnwindMetadataProbe.Run(capture, mapping, (byte*)buffer->Words,", StringComparison.Ordinal);
        Need(child > owner.IndexOf("Token(console, 17); console.WriteCrLf(); Token(console, 18);", StringComparison.Ordinal) &&
            owner.IndexOf("if (!Preserved((byte*)buffer->Words, o.Required))", child, StringComparison.Ordinal) > child,
            "original registration transcript and retained canaries surround the child");
    }

    [HostTest("m04.020.efi bounded metadata transcript and unselected rejection", HostLane.Fast, 1)]
    private static void Transcript()
    {
        var image = new Dictionary<string, ulong> { ["base"] = 1048576, ["bytes"] = 262144 };
        var module = new Dictionary<string, ulong> { ["storage"] = 1056768 };
        var registration = new Dictionary<string, ulong> { ["handle"] = 1057040, ["slot"] = 1060864 };
        ulong seed = image["base"] ^ registration["handle"], word0 = 0x3D947E21A6C850BF, word1 = 0xC2715AB809E634DF;
        ulong[][] rows = [
            [image["base"], image["bytes"], module["storage"], registration["handle"]],
            [16384, 24, 2, 328], [1114112, 65536, 65504, 288],
            [1064960, 2097152, 2232320, 2232280], [2232000, 2231904, word0, word1],
            [1081344, 0, 32768, 32832], [20480, 20480, 8, 2], [0, 0, 32, 40], [0, 0, 0, 0],
            [0, seed, seed ^ 255UL, 1081407], [1081600, 1, 33024, 33088],
            [20512, 20512, 8, 2], [0, 0, 32, 40], [0, 0, 0, 0], [0, seed, seed ^ 85UL, 1081663],
            [15, 15, 1, 2], [7, 2231808, word0, word1], [328, 328, 2, 4],
            [module["storage"], registration["handle"], registration["slot"], registration["handle"]]];
        static string Row(int stage, ulong[] values) => $"CLROS_M04_020_DATA stage={stage} a={values[0]} b={values[1]} c={values[2]} d={values[3]}";
        string[] good = ["TEST_START m04.020", ..rows.Select((values, stage) => Row(stage, values)),
            "CLROS_M04_020_READY methods=2 lookups=4 version=1 hostConformance=false guestIntegrated=true unwindExecution=false", "TEST_PASS m04.020"];
        int controls = 0;
        void Check(string[] lines, bool expected)
        {
            bool accepted = true;
            try
            {
                QemuTranscripts.AssertM04X64UnwindMetadataTranscript(lines, image, module, registration);
            }
            catch (ToolFailure) when (!expected) { accepted = false; }
            Need(accepted == expected, "unwind admission control " + controls++);
        }
        Check(good, true);
        for (int i = 0; i < good.Length; i++)
        {
            Check(good.Where((_, index) => index != i).ToArray(), false);
            string[] bad = [.. good];
            bad[i] += " ";
            Check(bad, false);
        }
        Check([.. good, good[0]], false);
        string[] swapped = [.. good];
        (swapped[6], swapped[11]) = (swapped[11], swapped[6]);
        Check(swapped, false);
        foreach ((int row, int field, ulong value) in new (int, int, ulong)[] {
            (0,2,1056776),(1,2,2049),(1,3,65505),(2,1,65535),(2,0,1056768),(2,3,8),
            (3,3,2232288),(4,1,2097160),(4,2,0),(5,1,2),(5,0,1081408),
            (6,0,16384),(6,2,65),(6,3,0),(7,0,1),(7,1,1),(7,2,41),
            (8,0,4),(8,3,1),(9,2,0),(9,3,1081408),(10,1,0),
            (15,1,1),(15,3,1),(16,0,6),(17,1,255),(18,3,1057048) })
        {
            string[] bad = [.. good];
            ulong[] values = [.. rows[row]];
            values[field] = value;
            bad[row + 1] = Row(row, values);
            Check(bad, false);
        }
        foreach (string value in new[] { "-1", "01", "18446744073709551616" })
        {
            string[] bad = [.. good];
            bad[1] = bad[1].Replace("a=1048576", "a=" + value, StringComparison.Ordinal);
            Check(bad, false);
        }
        QemuTranscripts.AssertM04X64UnwindMetadataSelection("OS_BOOT_OK", false);
        QemuTranscripts.AssertM04X64UnwindMetadataSelection(string.Join("\r\n", good), true);
        foreach (string claim in new[] { "TEST_START m04.020", "CLROS_M04_020_DATA stage=0", "clros_\tM04_020_READY", "TEST_PASS m04.\0" + "020" })
        {
            bool rejected = false;
            try
            {
                QemuTranscripts.AssertM04X64UnwindMetadataSelection(claim, false);
            }
            catch (ToolFailure) { rejected = true; }
            Need(rejected, "unselected child admitted " + claim);
        }
        Console.WriteLine("TRANSCRIPT_CONTROLS unwind cases=" + controls);
    }
    [HostTest("m04.020.efi complete original module and unwind profile", HostLane.Fast, 2)]
    private static void Profile()
    {
        var result = HostToolProcess.RunTool("qemu", "smoke",
            "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", "-EnableM01LoadedImageStackProbe", "-EnableM04TypeManagerModuleRegistrationProbe", "-" + Flag);
        Need(result.ExitCode == 0 && result.Error.Length == 0, result.Output + result.Error);
        string output = result.Output;
        string[] Read(string key) => output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))
            [key.Length..].TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] required = Read("REQUIRED_MARKERS "), ordered = Read("ORDERED_MARKERS "),
            unique = Read("UNIQUE_MARKERS "), forbidden = Read("FORBIDDEN_MARKERS ");
        Need(ordered.Length == QemuContracts.M04X64UnwindMetadataRequiredMarkers.Count && unique.SequenceEqual(ordered, StringComparer.Ordinal) &&
            required.SequenceEqual(ordered, StringComparer.Ordinal), "complete 45 ancestor plus 22 child markers, including required order");
        int first = Array.IndexOf(ordered, "TEST_START m04.020");
        Need(first == 37 && ordered[first - 1] == "TEST_PASS m04.011" && ordered[first + 22] == "TEST_START boot.sequence",
            "child follows original registration and precedes ordinary boot");
        bool Accept(string[] records)
        {
            var state = new SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            return state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n", records) + "\r\n")).Succeeded && state.Complete().Passed;
        }
        Need(Accept(ordered), "controlled complete marker stream");
        for (int i = first; i < first + 22; i++)
            Need(!Accept(ordered.Where((_, index) => index != i).ToArray()), "missing child marker " + i);
        Need(!Accept(ordered.Append("TEST_FAIL m04.020 reason=1 status=8").ToArray()), "late child failure rejected");
    }

}
