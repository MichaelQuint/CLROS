using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Threading;
using OS.Tooling;

internal static unsafe class M09006EfiIntegrationTests
{
    private static readonly string[] Parents = ["M01LoadedImageStack", "M01LiveAllocator", "AcpiPhysicalDiscovery",
        "M03FirstRealExitBootServices", "M03OwnedSubsystemSurvival", "M05PciEnumeration", "M05ApicSetup"];

    [HostTest("m09.006 default-off lifecycle profile admits only owned exit and APIC prerequisites", HostLane.Fast, 0)]
    private static void DefaultProfile()
    {
        HostCheck.True(!Probes.EnableM09006Probe && !Probes.GetM09006Probe().EnabledByDefault);
        HostCheck.True(!BootSequence.RunOwnedKernelThreads(true, true, true, null, null));
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(!EfiOptions.Parse([], build).Profile().IsEnabled("M09006"));
            var profile = EfiOptions.Parse(["--EnableM09006Probe"], build).Profile();
            HostCheck.True(profile.BootPolicy == "Diagnostic" && profile.IsEnabled("M09006") && Parents.All(profile.IsEnabled));
            HostCheck.Equal(Parents.Length + 1, profile.Flags.Count(p => p.Value));
            foreach (string parent in Parents)
                Reject(() => EfiOptions.Parse(["--EnableM09006Probe", "--Enable" + parent + "Probe:false"], build).Profile());
            Reject(() => EfiOptions.Parse(["--BootPolicy", "VerifiedExit", "--EnableM09006Probe"], build).Profile());
            foreach (string conflict in EfiContracts.DiagnosticProbes.Where(n => n != "EnableM09006Probe" && !Parents.Any(p => n == "Enable" + p + "Probe")))
                Reject(() => EfiOptions.Parse(["--EnableM09006Probe", "--" + conflict], build).Profile());
            string[] compile = EfiProjectAdmission.ExpectedCompileItems(profile);
            HostCheck.True(compile.Contains(@"Boot\EfiEntry.KernelThread.cs|") &&
                compile.Contains(@"..\OS.Kernel\Threading\KernelThreadProgram.cs|Threading\KernelThreadProgram.cs"));
        }
    }

    // Synthetic protocol values only. This deliberately has no process, native
    // object, APIC, guard or restoration authority.
    private static string[] Fixture()
    {
        const ulong pool = 0x10000000, a = 0x100000001, b = 0x100000002, s = 0x1ffffffff;
        List<string> lines = ["ancestor", "TEST_START m09.006"];
        ulong sequence = 0;
        string Row(string marker, params ulong[] words) => marker + string.Concat(words.Select(n => " 0x" + n.ToString("X16")));
        void Put(string marker, params ulong[] words) => lines.Add(Row("M09_006_" + marker, words));
        void Up(ulong worker) => Put("TRANSFER", sequence++, s, worker, pool + ((uint)worker == 1 ? 26UL : 0x21a));
        void Back(ulong worker, ulong pc) => Put("TRANSFER", sequence++, worker, s, pool + pc);
        Put("PREPARED", pool, 23, pool + 13 * 4096, pool ^ 0x4d30393030364f57UL);
        Put("ROLLBACK", 0, (ulong)KernelThreadTable.Status.Full, 12345, 2);
        Up(a);
        Back(a, 0x61a);
        Up(b);
        Back(b, 0x71a);
        Up(a);
        Put("WAIT", a, 1, 2, 4);
        Back(a, 0x62a);
        Up(b);
        Put("WAIT", b, 1, 3, 4);
        Back(b, 0x72a);
        Put("TICK", 1, 1, 0, 0);
        Put("TICK", 2, 3, 1, a);
        Up(a);
        Back(a, 0x63a);
        Up(a);
        Put("WAIT", a, 2, 4, 4);
        Back(a, 0x64a);
        Put("TICK", 3, 3, 1, b);
        Up(b);
        Back(b, 0x73a);
        Up(b);
        Put("WAIT", b, 2, 5, 4);
        Back(b, 0x74a);
        Put("TICK", 4, 3, 1, a);
        Up(a);
        Back(a, 0x65a);
        Put("TICK", 5, 3, 1, b);
        Up(b);
        Back(b, 0x75a);
        Up(a);
        Put("JOIN", a, b, 23, 1);
        Back(a, 0x4a5);
        ulong old = a, joins = 1;
        foreach (ulong generation in new ulong[] { 2, 3 })
        {
            ulong current = (generation << 32) | 1, cycle = generation - 1;
            Put("REAP", old, cycle, 688, 4096);
            Put("ROLLBACK", cycle, (ulong)KernelThreadTable.Status.InvalidContext, 23456, 0);
            Put("REUSE", old, current, (ulong)KernelThreadTable.Status.StaleIdentity, cycle);
            Up(current);
            Put("JOIN", current, b, 23, ++joins);
            Back(current, 0xa1a);
            Up(current);
            Put("JOIN", current, b, 23, ++joins);
            Back(current, 0xa2a);
            Up(current);
            Put("WAIT", current, 1, 5 + cycle, 4);
            Back(current, 0xa3a);
            if (generation == 2)
            {
                Put("CANCEL", current, 1, 4, 1);
                Put("TICK", 6, 1, 1, 0);
            }
            else
            {
                Put("TICK", 7, 3, 1, current);
                Put("CANCEL", current, 1, 2, 2);
            }
            Up(current);
            Back(current, 0x4a5);
            old = current;
        }
        Put("REAP", old, 3, 688, 4096);
        Put("REAP", b, 4, 688, 4096);
        Put("COMPLETE", 7, sequence, joins, 5);
        Put("RESTORED", 23, 32, 4, 1);
        lines.Add("TEST_PASS m09.006");
        lines.Add("CLROS_M09_006_READY");
        return lines.ToArray();
    }

    [HostTest("m09.006 serial admission rejects stale generation lost race wrong stack labels and early cleanup", HostLane.Fast, 1)]
    private static void TranscriptMutations()
    {
        string[] good = Fixture();
        HostCheck.True(QemuTranscripts.ValidateKernelThreadTail(good).SequenceEqual(new[] { "ancestor" }));
        // Every missing/duplicated transition and every changed scalar rejects.
        for (int index = 2; index < good.Length - 2; index++)
        {
            Reject(() => QemuTranscripts.ValidateKernelThreadTail(good.Where((_, i) => i != index).ToArray()));
            string[] duplicated = good.Take(index).Concat(new[] { good[index] }).Concat(good.Skip(index)).ToArray();
            Reject(() => QemuTranscripts.ValidateKernelThreadTail(duplicated));
            if (good[index].StartsWith("M09_006_ROLLBACK", StringComparison.Ordinal))
                continue; // Digest has its own nonzero admission.
            for (int field = 1; field <= 4; field++)
            {
                string[] changed = (string[])good.Clone(), words = changed[index].Split(' ');
                ulong value = Convert.ToUInt64(words[field][2..], 16);
                words[field] = "0x" + (value ^ 1).ToString("X16");
                changed[index] = string.Join(' ', words);
                Reject(() => QemuTranscripts.ValidateKernelThreadTail(changed));
            }
        }
        Reject(() => QemuTranscripts.ValidateKernelThreadTail(good.Concat(new[] { "unexpected" }).ToArray()));
        Reject(() => QemuTranscripts.ValidateKernelThreadTail(new[] { "M09_006_TRANSFER premature" }.Concat(good).ToArray()));
    }

    [HostTest("m09.006 summary inventory covers every protocol row and only single records are unique", HostLane.Fast, 2)]
    private static void SummaryCoverage()
    {
        string[] serial = Fixture().Skip(1).ToArray();
        var options = QemuSmokeOptions.Parse("--EnableM09006Probe", "--BuildEfi", "--RequireMarker", "CLROS_M09_006_READY");
        var plan = QemuProfiles.Create(options);
        foreach (string row in serial)
            HostCheck.True(plan.Required.Any(marker => row == marker || row.StartsWith(marker + " ", StringComparison.Ordinal)), "summary omitted: " + row);
        foreach (string marker in EfiContracts.M09006Markers)
        {
            int occurrences = serial.Count(row => row == marker || row.StartsWith(marker + " ", StringComparison.Ordinal));
            HostCheck.True(occurrences > 0);
            HostCheck.Equal(occurrences == 1, plan.Unique.Contains(marker));
        }
    }
    private static void Reject(Action operation)
    {
        bool rejected = false;
        try
        {
            operation();
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
