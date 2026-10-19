using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static class SocketContainmentProfileTests
{
    private static readonly string[] Leaves = ["EnableM07027Probe", "EnableM07028Probe"];

    [HostTest("containment switches are default off with exact project symbols and diagnostic guards")]
    private static void Defaults()
    {
        HostCheck.True(!Probes.EnableM07027Probe && !Probes.EnableM07028Probe);
        var project = XDocument.Load(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/OS.csproj"));
        foreach (bool build in new[] { false, true })
        {
            var profile = EfiOptions.Parse([], build).Profile();
            HostCheck.Equal("VerifiedExit", profile.BootPolicy);
            HostCheck.True(profile.Flags.All(x => !x.Value));
        }
        foreach (var (flag, symbol, error) in new[] {
            (Leaves[0], "M07_027_PROBE", "m07_027_switch_invalid"),
            (Leaves[1], "M07_028_PROBE", "m07_028_switch_invalid") })
        {
            HostCheck.True(EfiContracts.DiagnosticProbes.Count(x => x == flag) == 1 &&
                EfiContracts.FirstExitDependentProbes.Count(x => x == flag) == 1 &&
                EfiContracts.ProjectDefaultProbes.Count(x => x == flag) == 1);
            HostCheck.Equal(symbol, EfiContracts.ProbeSymbols[flag]);
            var defaults = project.Descendants(flag).ToArray();
            HostCheck.True(defaults.Length == 1 && defaults[0].Value == "false" &&
                defaults[0].Attribute("Condition")?.Value == "'$(" + flag + ")' == ''");
            var define = project.Descendants("DefineConstants").Single(x =>
                x.Parent?.Attribute("Condition")?.Value == "'$(" + flag + ")' == 'true'");
            HostCheck.Equal("$(DefineConstants);" + symbol, define.Value);
            string boolean = Error(project, error);
            HostCheck.Equal("'$(" + flag + ")' != 'true' and '$(" + flag + ")' != 'false'", boolean);
            HostCheck.True(project.Descendants("ClrosBootPolicy").Any(x => x.Value == "Diagnostic" &&
                x.Attribute("Condition")?.Value.Contains("'$(" + flag + ")' == 'true'", StringComparison.Ordinal) == true));
            HostCheck.True(Error(project, "clros_boot_policy_probe_conflict")
                .Contains("'$(" + flag + ")' == 'true'", StringComparison.Ordinal));
        }
    }

    [HostTest("either containment leaf resolves exactly the paired synchronous socket prerequisite union")]
    private static void Prerequisites()
    {
        foreach (bool build in new[] { false, true })
        {
            var expected = SocketUnion(build);
            foreach (string leaf in Leaves)
            {
                var profile = EfiOptions.Parse(["--" + leaf], build).Profile();
                HostCheck.Equal("Diagnostic", profile.BootPolicy);
                HostCheck.True(expected.SetEquals(profile.Flags.Where(x => x.Value).Select(x => x.Key)));
                foreach (string parent in expected.Where(x => x != leaf))
                    Reject(() => EfiOptions.Parse(["--" + leaf, "--" + parent, "false"], build).Profile());
            }
            foreach (string leaf in new[] { "EnableM16011Probe", "EnableM16012Probe" })
            {
                var original = EfiOptions.Parse(["--" + leaf], build).Profile();
                HostCheck.True(Leaves.All(x => !original.IsEnabled(x)));
                HostCheck.True(original.IsEnabled("M16012") == (leaf == "EnableM16012Probe"));
            }
        }
    }

    [HostTest("containment rejects every unrelated diagnostic explicit invalid boolean and VerifiedExit")]
    private static void Conflicts()
    {
        foreach (bool build in new[] { false, true })
        foreach (string leaf in Leaves)
        {
            var allowed = SocketUnion(build);
            foreach (string extra in EfiContracts.DiagnosticProbes.Where(x => !allowed.Contains(x)))
                Reject(() => EfiOptions.Parse(["--" + leaf, "--" + extra], build).Profile());
            Reject(() => EfiOptions.Parse(["--" + leaf, "--BootPolicy", "VerifiedExit"], build).Profile());
            Reject(() => EfiOptions.Parse(["--" + leaf, "invalid"], build).Profile());
        }
    }

    [HostTest("containment project prerequisites and conflicts cover the derived complete diagnostic inventory")]
    private static void ProjectUnion()
    {
        var project = XDocument.Load(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/OS.csproj"));
        var expected = SocketUnion(true);
        string required = Error(project, "socket_containment_requires_socket_union");
        HostCheck.True(required.Contains("'$(ClrosBootPolicy)' != 'Diagnostic'", StringComparison.Ordinal));
        var requiredNames = Regex.Matches(required, "'\\$\\((Enable\\w+Probe)\\)' != 'true'")
            .Select(x => x.Groups[1].Value).ToArray();
        HostCheck.True(expected.SetEquals(requiredNames) && requiredNames.Length == expected.Count);
        string conflict = Error(project, "socket_containment_profile_conflict");
        var forbiddenNames = Regex.Matches(conflict, "'\\$\\((Enable\\w+Probe)\\)' == 'true'")
            .Select(x => x.Groups[1].Value).ToArray();
        HostCheck.True(forbiddenNames.Take(Leaves.Length).SequenceEqual(Leaves));
        HostCheck.Equal("'$(EnableM16014Probe)' == 'true' and '$(EnableM10004Probe)' != 'true'",
            Error(project,"m16_014_requires_web_parent"));
        var forbidden = EfiContracts.DiagnosticProbes.Where(x => !expected.Contains(x))
            .Select(x=>x=="EnableM16014Probe"?"EnableM10004Probe":x).ToHashSet(StringComparer.Ordinal);
        HostCheck.True(forbidden.SetEquals(forbiddenNames.Skip(Leaves.Length)) &&
            forbiddenNames.Length == forbidden.Count + Leaves.Length && forbidden.Contains("EnableM16012Probe"));
    }

    private static HashSet<string> SocketUnion(bool build)
    {
        var parent = EfiOptions.Parse(["--EnableM16011Probe"], build).Profile();
        return parent.Flags.Where(x => x.Value).Select(x => x.Key).Concat(Leaves).ToHashSet(StringComparer.Ordinal);
    }

    private static string Error(XDocument project, string reason) => project.Descendants("Error")
        .Single(x => x.Attribute("Text")?.Value == reason).Attribute("Condition")!.Value;

    private static void Reject(Action action)
    {
        try { action(); } catch (ToolFailure) { return; }
        throw new InvalidOperationException("socket_containment_invalid_profile_accepted");
    }
}
