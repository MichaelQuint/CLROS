using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M09004EfiIntegrationTests
{
    private static readonly string[] Parents = ["M01LoadedImageStack", "M01LiveAllocator", "AcpiPhysicalDiscovery",
        "M03FirstRealExitBootServices", "M03OwnedSubsystemSurvival"];

    [HostTest("m09.004 default-off authority and complete explicit prerequisite rejection", HostLane.Fast, 0)]
    private static void DefaultsAndParents()
    {
        HostCheck.True(!Probes.EnableM09004Probe && !Probes.GetM09004Probe().EnabledByDefault);
        HostCheck.True(!BootSequence.PrepareCooperativeSwitch(true, null, null) &&
            !BootSequence.RunOwnedCooperativeSwitch(true, true, null, null));
        foreach (bool build in new[] { false, true })
        {
            var defaults = EfiOptions.Parse([], build).Profile();
            HostCheck.True(!defaults.IsEnabled("M09004"));
            var profile = EfiOptions.Parse(["--EnableM09004Probe"], build).Profile();
            HostCheck.True(profile.BootPolicy == "Diagnostic" && profile.IsEnabled("M09004") && Parents.All(profile.IsEnabled));
            HostCheck.Equal(6, profile.Flags.Count(pair => pair.Value));
            foreach (string parent in Parents)
                Reject(() => EfiOptions.Parse(["--EnableM09004Probe", "--Enable" + parent + "Probe:false"], build).Profile());
            Reject(() => EfiOptions.Parse(["--BootPolicy", "VerifiedExit", "--EnableM09004Probe"], build).Profile());
            foreach (string conflict in EfiContracts.DiagnosticProbes.Where(name => name != "EnableM09004Probe" &&
                !Parents.Any(parent => name == "Enable" + parent + "Probe")))
                Reject(() => EfiOptions.Parse(["--EnableM09004Probe", "--" + conflict], build).Profile());
        }
    }

    [HostTest("m09.004 smoke selects original standalone capture and rejects marker replacement", HostLane.Fast, 1)]
    private static void SmokeSelection()
    {
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--EnableM09004Probe"]));
        HostCheck.True(plan.IsEnabled("EnableM09004Probe") && plan.Required.Contains("CLROS_M09_004_READY") &&
            plan.Required.Contains("M09_004_TRANSFER") && plan.Forbidden.Contains("M09_004_REJECT"));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--EnableM09004Probe", "--RequireMarker", "OS_SMOKE_PASS"])));
        Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript("missing", cooperativeSwitch: true));
    }

    [HostTest("m09.004 allocator firewall rejects redirected receipts ownership gates and callbacks", HostLane.Fast, 5)]
    private static void AllocatorFirewall()
    {
        string source = File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/Boot/EfiEntry.cs"));
        foreach (string call in new[] { "BootPageAllocatorMutator.AllocateOnePage", "BootPageAllocatorMutator.FreeOnePage" })
        {
            string admitted = source;
            HostCheck.True(EfiProfileTestControls.RemoveOwnedDmaCalls(ref admitted, call, out string reason), reason);
            HostCheck.True(!admitted.Contains(call, StringComparison.Ordinal));
            foreach (string corrupted in new[] {
                source.Replace("receipts[count - 1]", "receipts[0]", StringComparison.Ordinal),
                source.Replace("RunOwnedCooperativeSwitch(state->OwnedPhase == 2", "RunOwnedCooperativeSwitch(true", StringComparison.Ordinal),
                source.Replace("state, &RunCooperativeSwitch)", "state, &UnreviewedCooperativeSwitch)", StringComparison.Ordinal) })
            {
                string rejected = corrupted;
                // Receipt corruption affects only release; the ordered edge
                // controls must reject independently for allocation as well.
                if (call.EndsWith(".AllocateOnePage", StringComparison.Ordinal) && corrupted.Contains("receipts[0]", StringComparison.Ordinal)) continue;
                HostCheck.True(!EfiProfileTestControls.RemoveOwnedDmaCalls(ref rejected, call, out _));
            }
        }
    }

    [HostTest("m09.004 project exclusion covers every declared unrelated probe", HostLane.Fast, 6)]
    private static void ProjectExclusion()
    {
        var project = System.Xml.Linq.XDocument.Load(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/OS.csproj"));
        string condition = project.Descendants("Error").Single(element =>
            element.Attribute("Text")?.Value == "m09_004_profile_conflict").Attribute("Condition")!.Value;
        HostCheck.True(condition.StartsWith("'$(EnableM09004Probe)' == 'true' and (", StringComparison.Ordinal));
        string[] declared = project.Descendants("PropertyGroup").SelectMany(group => group.Elements()).Select(element => element.Name.LocalName)
            .Where(name => name.StartsWith("Enable", StringComparison.Ordinal) && name.EndsWith("Probe", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).ToArray();
        foreach (string name in declared)
        {
            bool allowed = name == "EnableM09004Probe" || Parents.Any(parent => name == "Enable" + parent + "Probe");
            HostCheck.Equal(!allowed, condition.Contains("'$(" + name + ")' != 'false'", StringComparison.Ordinal));
        }
        int excluded = declared.Count(name => name != "EnableM09004Probe" &&
            !Parents.Any(parent => name == "Enable" + parent + "Probe"));
        HostCheck.Equal(excluded, System.Text.RegularExpressions.Regex.Matches(condition, @"'\$\(Enable[A-Za-z0-9]+Probe\)' != 'false'").Count);
    }

    [HostTest("m09.004 synthetic complete repeated transfer and restoration tail preserves parent prefix", HostLane.Fast, 2)]
    private static void CompleteTail()
    {
        string[] rows = Tail();
        HostCheck.True(QemuTranscripts.ValidateCooperativeTail(rows).SequenceEqual(new[] { "parent" }));
    }

    [HostTest("m09.004 transcript rejects altered transfer identities continuations and actual stack positions", HostLane.Fast, 3)]
    private static void TransferCorruption()
    {
        string[] rows = Tail();
        for (int row = 3; row < 20; row++)
            for (int field = 1; field <= 4; field++)
            {
                string[] changed = (string[])rows.Clone(); string[] values = changed[row].Split(' ');
                ulong value = Convert.ToUInt64(values[field][2..], 16);
                // The original captured stack is unconstrained apart from ABI alignment;
                // changing only it still disagrees with the later restoration witness.
                values[field] = "0x" + (value + 1).ToString("x16"); changed[row] = string.Join(' ', values);
                Reject(() => QemuTranscripts.ValidateCooperativeTail(changed));
            }
    }

    [HostTest("m09.004 transcript requires restored state complete release and success after cleanup", HostLane.Fast, 4)]
    private static void ResourceAndTerminalCorruption()
    {
        string[] rows = Tail();
        foreach (int row in new[] { 2, 20, 21 })
            for (int field = 1; field <= 4; field++)
            {
                string[] changed = (string[])rows.Clone(); string[] values = changed[row].Split(' ');
                values[field] = "0x0000000000000000"; changed[row] = string.Join(' ', values);
                if (changed[row] != rows[row]) Reject(() => QemuTranscripts.ValidateCooperativeTail(changed));
            }
        for (int row = 1; row < rows.Length; row++)
        {
            string[] removed = rows.Where((_, index) => index != row).ToArray();
            Reject(() => QemuTranscripts.ValidateCooperativeTail(removed));
            string[] duplicated = rows.Take(row).Concat(new[] { rows[row] }).Concat(rows.Skip(row)).ToArray();
            Reject(() => QemuTranscripts.ValidateCooperativeTail(duplicated));
        }
        string[] reordered = (string[])rows.Clone(); (reordered[21], reordered[22]) = (reordered[22], reordered[21]);
        Reject(() => QemuTranscripts.ValidateCooperativeTail(reordered));
        Reject(() => QemuTranscripts.ValidateCooperativeTail(new[] { "CLROS_M09_004_READY" }.Concat(rows).ToArray()));
    }

    // Deliberately synthetic parser input. No emitted/native instructions run here.
    private static string[] Tail()
    {
        const ulong pool = 0x200000, originalRsp = 0x900000;
        var rows = new List<string> { "parent", "TEST_START m09.004", Row("ALLOC", pool, 32, 20, 12) };
        for (uint step = 0; step < 17; step++)
        {
            uint from = step == 0 ? 0U : (step & 1) == 1 ? 1U : 2U;
            uint to = step == 16 ? 0U : from == 1 ? 2U : 1U;
            ulong rip = pool + (from == 0 ? 26UL : from == 1 ? 0x209UL : step == 16 ? 0x324UL : 0x35cUL);
            ulong rsp = from == 0 ? originalRsp : pool + (from == 1 ? 16UL : 19UL) * 4096 - 80;
            rows.Add(Row("TRANSFER", step, (ulong)from << 32 | to, rip, rsp));
        }
        rows.Add(Row("RESTORE", 0xffff, 2, originalRsp, pool + 26));
        rows.Add(Row("CLEANUP", 32, 0, 1, 81920));
        rows.Add("CLROS_M09_004_READY"); rows.Add("TEST_PASS m09.004"); return rows.ToArray();
    }

    private static string Row(string marker, params ulong[] values) => "M09_004_" + marker + " " +
        string.Join(' ', values.Select(value => "0x" + value.ToString("x16")));

    private static void Reject(Action action)
    {
        bool rejected = false;
        try { action(); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
