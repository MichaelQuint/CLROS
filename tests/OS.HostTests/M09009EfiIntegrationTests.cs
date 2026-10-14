using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Threading;
using OS.Tooling;

internal static class M09009EfiIntegrationTests
{
    [HostTest("m09.009 default off profile requires exact owned APIC parents", HostLane.Fast, 0)]
    private static void Profile() => CheckProfile(false);
    [HostTest("m09.009 protocol rejects every dropped duplicate reordered and corrupted record", HostLane.Fast, 1)]
    private static void Protocol() => CheckProtocol(false);
    [HostTest("m09.009 summary retains all repeated transitions and complete single records", HostLane.Fast, 2)]
    private static void Summary() => CheckSummary(false);

    [HostTest("m09.009 wake before park resumes in place and explicit yield creates the supervisor handoff")]
    private static unsafe void InPlaceHandoff()
    {
        using KernelThreadTableTests.Model m = new();
        var a = m.Create(0).Identity; var b = m.Create(1).Identity; HostCheck.Equal(a.Value, m.Dispatch().Value);
        var wait = m.Table->BeginWait(KernelThreadTableTests.Key, 2, a, KernelThreadTable.WaitKind.Park, 0);
        HostCheck.True(wait.Succeeded && m.Table->Wake(KernelThreadTableTests.Key, 2, wait.Wait).Succeeded);
        var captured = m.Capture(a); var park = m.Table->Park(KernelThreadTableTests.Key, 2, wait.Wait, captured);
        HostCheck.True(park.Succeeded && park.State == KernelThreadTable.State.Running && !m.Table->HandoffPending && m.Table->Current.Value == a.Value);
        HostCheck.True(m.Table->Yield(KernelThreadTableTests.Key, 2, a, captured).Succeeded && m.Table->HandoffPending);
        m.Confirm(); HostCheck.Equal(b.Value, m.Dispatch().Value); HostCheck.Equal(KernelThreadTable.State.Ready, m.Inspect(a).State);
    }

    internal static unsafe void CheckProfile(bool tls)
    {
        string flag = tls ? "EnableM09010Probe" : "EnableM09009Probe";
        string[] parents = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableAcpiPhysicalDiscoveryProbe",
            "EnableM03FirstRealExitBootServicesProbe", "EnableM03OwnedSubsystemSurvivalProbe", "EnableM05PciEnumerationProbe", "EnableM05ApicSetupProbe"];
        HostCheck.True(!Probes.EnableM09009Probe && !Probes.EnableM09010Probe && !Probes.GetM09009Probe().EnabledByDefault && !Probes.GetM09010Probe().EnabledByDefault);
        HostCheck.True(!BootSequence.RunOwnedKernelThreads(true, true, true, null, null));
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(!EfiOptions.Parse([], build).Profile().IsEnabled(flag));
            var profile = EfiOptions.Parse(["--" + flag], build).Profile();
            HostCheck.True(profile.BootPolicy == "Diagnostic" && profile.IsEnabled(flag) && parents.All(profile.IsEnabled));
            HostCheck.Equal(parents.Length + 1, profile.Flags.Count(p => p.Value));
            foreach (string parent in parents) Reject(() => EfiOptions.Parse(["--" + flag, "--" + parent + ":false"], build).Profile());
            foreach (string conflict in EfiContracts.DiagnosticProbes.Where(n => n != flag && !parents.Contains(n)))
                Reject(() => EfiOptions.Parse(["--" + flag, "--" + conflict], build).Profile());
            Reject(() => EfiOptions.Parse(["--BootPolicy", "VerifiedExit", "--" + flag], build).Profile());
            string[] compile = EfiProjectAdmission.ExpectedCompileItems(profile);
            foreach (string include in new[] { @"Boot\EfiEntry.ThreadWait.cs|", @"Boot\EfiEntry.KernelThread.cs|",
                @"..\OS.Kernel\Threading\ThreadWaitProgram.cs|Threading\ThreadWaitProgram.cs", @"..\OS.Kernel\Threading\WaitAddress.cs|Threading\WaitAddress.cs",
                @"..\OS.Kernel\Threading\ThreadLocalStorage.cs|Threading\ThreadLocalStorage.cs" }) HostCheck.True(compile.Contains(include));
        }
    }

    // Independent synthetic protocol controls; no execution or provenance claim.
    internal static string[] Fixture(bool tls)
    {
        string id = tls ? "010" : "009", prefix = "M09_" + id + "_";
        const ulong pool = 0x10000000, supervisor = 0x1ffffffff; ulong transfers = 0, epoch = 0;
        List<string> rows = ["ancestor", "TEST_START m09." + id];
        void Put(string suffix, params ulong[] values) => rows.Add(prefix + suffix + string.Concat(values.Select(v => " 0x" + v.ToString("X16"))));
        Put("PREPARED", pool, 24, pool + 0xd000, pool ^ (tls ? 0x4d30393031304f57UL : 0x4d30393030394f57UL));
        Put("REJECT", 0, (ulong)KernelThreadTable.Status.Full, 12345, 2);
        for (ulong generation = 1; generation <= 2; generation++)
        {
            ulong a = (generation << 32) | 1, b = (generation << 32) | 2, addressA = a, addressB = 0x100000002;
            void Wake(bool second, ulong max, ulong count) => Put("WAKE", second ? addressB : addressA, max, count, epoch);
            void State(ulong thread, ulong state, ulong completion, ulong sequence) => Put("OBSERVE", thread, state, completion, sequence);
            void Run(ulong thread, uint action)
            {
                bool first = (uint)thread == 1;
                Put("TRANSFER", transfers++, supervisor, thread, pool + (first ? 0x1aUL : 0x21a));
                if (action < 4)
                {
                    if (action == 0 && first) { Put("REJECT", 1, (ulong)WaitAddress.Status.ValueChanged, thread, generation - 1); Wake(false, 1, 1); }
                    Put("WAIT", thread, action + 1UL, (action == 0 || action == 2) && !first ? addressB : addressA, action);
                }
                ulong fs = pool + (first ? 0x3f00UL : 0x3f80);
                Put("TLS", thread, fs, fs + 64, 0);
                ulong continuation = action == 4 ? first ? 0x4a5UL : 0x5a5UL : (first ? 0x600UL : 0x700UL) + 0x2c + action * 0x30;
                Put("TRANSFER", transfers++, thread, supervisor, pool + continuation);
            }
            Run(a, 0); Run(b, 0); State(a, 1, 1, 1); State(b, 4, 0, 1); Wake(false, uint.MaxValue, 0); Wake(true, 1, 1);
            Run(a, 1); Run(b, 1); Wake(false, 1, 1); State(a, 1, 1, 2); State(b, 4, 0, 2); Wake(false, uint.MaxValue, 1);
            Run(a, 2); Run(b, 2); Wake(false, 1, 1); Put("TICK", ++epoch, 3, 2, b); Wake(true, uint.MaxValue, 0); State(a, 1, 1, 3); State(b, 1, 2, 3);
            Run(a, 3); Run(b, 3); Put("CANCEL", a, 4, 4, 1); Put("TICK", ++epoch, 3, 2, b); Put("CANCEL", b, 4, 2, 2);
            Wake(false, uint.MaxValue, 0); Run(a, 4); Run(b, 4);
            Put("REAP", a, generation * 2 - 1, 688, 128); Put("REAP", b, generation * 2, 688, 128);
            if (generation == 1) Put("REUSE", a, 0x200000001, addressA, 0x200000001);
        }
        Put("COMPLETE", 4, transfers, 4, 4); Put("RESTORED", 24, 32, 4, 1);
        rows.Add("TEST_PASS m09." + id); rows.Add("CLROS_" + prefix + "READY"); return rows.ToArray();
    }
    internal static void CheckProtocol(bool tls)
    {
        string[] good = Fixture(tls); HostCheck.True(QemuTranscripts.ValidateThreadWaitTail(good, tls).SequenceEqual(new[] { "ancestor" }));
        for (int row = 2; row < good.Length - 2; row++)
        {
            Reject(() => QemuTranscripts.ValidateThreadWaitTail(good.Where((_, i) => i != row).ToArray(), tls));
            Reject(() => QemuTranscripts.ValidateThreadWaitTail(good.Take(row).Concat(new[] { good[row] }).Concat(good.Skip(row)).ToArray(), tls));
            for (int column = 1; column <= 4; column++)
            {
                // Capacity digest is admitted nonzero; the first kernel-GS value
                // establishes an observation baseline, checked at every later row.
                if (row == 3 && column == 3) continue;
                if (good[row].Contains("_TLS ", StringComparison.Ordinal) && column == 4) continue;
                string[] changed = (string[])good.Clone(), words = changed[row].Split(' ');
                words[column] = "0x" + (Convert.ToUInt64(words[column][2..], 16) ^ 1).ToString("X16"); changed[row] = string.Join(' ', words);
                Reject(() => QemuTranscripts.ValidateThreadWaitTail(changed, tls));
            }
        }
        var ownerChanged = (string[])good.Clone(); int secondTls = Array.FindLastIndex(ownerChanged, r => r.Contains("_TLS ", StringComparison.Ordinal));
        ownerChanged[secondTls] = ownerChanged[secondTls][..^16] + "0000000000000001";
        Reject(() => QemuTranscripts.ValidateThreadWaitTail(ownerChanged, tls));
        Reject(() => QemuTranscripts.ValidateThreadWaitTail(good, !tls));
        Reject(() => QemuTranscripts.ValidateThreadWaitTail(good.Concat(new[] { "trailing" }).ToArray(), tls));
        Reject(() => QemuTranscripts.ValidateThreadWaitTail(new[] { good[2] }.Concat(good).ToArray(), tls));
    }
    internal static void CheckSummary(bool tls)
    {
        string id = tls ? "010" : "009"; string[] serial = Fixture(tls).Skip(1).ToArray();
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse("--EnableM09" + id + "Probe", "--BuildEfi", "--RequireMarker", "CLROS_M09_" + id + "_READY"));
        foreach (string row in serial) HostCheck.True(plan.Required.Any(m => row == m || row.StartsWith(m + " ", StringComparison.Ordinal)), row);
        foreach (string marker in EfiContracts.ThreadWaitMarkers(id))
        {
            int count = serial.Count(r => r == marker || r.StartsWith(marker + " ", StringComparison.Ordinal));
            HostCheck.True(count > 0); HostCheck.Equal(count == 1, plan.Unique.Contains(marker));
        }
        foreach (string option in new[] { "--RequireOrderedMarker", "--RequireUniqueMarker", "--ForbidMarker" })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse("--EnableM09" + id + "Probe", option, "anything")));
    }
    private static void Reject(Action work)
    { bool failed = false; try { work(); } catch (ToolFailure) { failed = true; } HostCheck.True(failed); }
}
