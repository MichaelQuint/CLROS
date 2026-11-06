using OS.Tooling;

internal static class RuntimeProfilePortTests
{
    internal static readonly string[] RethrowFlags = ["M01LoadedImageStack", "M01LiveAllocator", "M04IndependentEndianConformance", "M04SpanOverlapMemoryCastUnsafeWidthConformance", "M04ArithmeticConformance", "M04LayoutContract", "M04ManagedHeapPageRange", "M04RhpNewFast", "M04GcStaticRootMaterialization", "M04Rethrow"];
    internal static readonly string[] MappingFlags = ["M01LoadedImageStack", "M01LiveAllocator", "AcpiPhysicalDiscovery", "M03FirstRealExitBootServices", "M03OwnedSubsystemSurvival", "M05PhysicalMapping"];
    private static string Flag(string name) => "Enable" + name + "Probe";
    private static string[] Arguments(IEnumerable<string> names) => names.Select(name => "--" + Flag(name)).ToArray();

    [HostTest("runtime profiles share build verifier and smoke admission")]
    private static void Profiles()
    {
        foreach (var (flags, ready) in new[] { (RethrowFlags, "CLROS_M04_RETHROW_READY"), (MappingFlags, "CLROS_M05_001_READY") })
        {
            foreach (bool build in new[] { false, true })
            {
                var accepted = EfiOptions.Parse(Arguments(flags), build).Profile();
                HostCheck.True(flags.All(name => accepted.IsEnabled(Flag(name))));
                foreach (string missing in flags[..^1])
                    Reject(() => EfiOptions.Parse(Arguments(flags.Where(name => name != missing)), build).Profile());
            }
            var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run", "--build-efi", .. Arguments(flags)]));
            HostCheck.True(plan.Required.Contains(ready));
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run", .. Arguments(flags)])));
            var baseline = QemuProfiles.Create(QemuSmokeOptions.Parse("--boot-policy", "Diagnostic", "--dry-run"));
            foreach (string claim in new[] { ready, ready.Replace("READY", "\nREADY", StringComparison.Ordinal) })
                Reject(() => QemuTranscripts.ValidateUnselectedRecords(claim, baseline));
        }
        foreach (string collision in new[] { "M04SimpleThrowCatch", "M04SingleFinallyCatch", "M04SeededCollectorChurn", "M04ClassConstructorExecution", "M04InterfaceDispatch", "M04X64UnwindMetadata" })
            Reject(() => EfiOptions.Parse(Arguments([.. RethrowFlags, collision]), true).Profile());
        foreach (string collision in new[] { "M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation" })
            Reject(() => EfiOptions.Parse(Arguments([.. MappingFlags, collision]), true).Profile());
        QemuTranscripts.AssertM04SimpleCatchSelection("TEST_PASS m04.021.rethrow", false, false, true);
        var project = EfiProjectAdmission.InspectStatic(HostToolProcess.FindRepositoryRoot());
        HostCheck.True(project.Succeeded, project.Reason + " " + project.Detail);
        HostCheck.True(ContractAudit.Inspect(HostToolProcess.FindRepositoryRoot()).Count == 0);
    }

    [HostTest("rethrow verifier preserves measured clause and unwind bytes")]
    private static void RethrowNativeContracts()
    {
        foreach (bool inner in new[] { true, false })
        {
            byte[] table = inner ? [4, 40, 50, 92, 40, 160, 138, 88, 152, 5, 0] : [2, 42, 48, 102, 88, 152, 5, 0];
            Bytes(table, data => EfiVerify.TestRethrowEhTable(data, inner, 0x59858));
            foreach (ulong bad in new ulong[] { 0, 0x59859, 0x100000000 })
                HostCheck.True(!EfiVerify.TestRethrowEhTable(table, inner, bad));
            foreach (bool root in new[] { true, false })
            {
                byte[] unwind = !root ? [1, 4, 1, 0, 4, 66, 1] : inner ? [1, 5, 2, 0, 5, 82, 1, 80, 4, 0, 176, 5, 0] : [1, 6, 3, 0, 6, 66, 2, 48, 1, 80, 4, 0, 176, 5, 0];
                Bytes(unwind, data => EfiVerify.TestRethrowUnwindBytes(data, inner, root, 0x5b000));
                if (root)
                    foreach (ulong bad in new ulong[] { 0, 0x5b001, 0x100000000 })
                        HostCheck.True(!EfiVerify.TestRethrowUnwindBytes(unwind, inner, root, bad));
            }
        }
        static void Bytes(byte[] original, Func<byte[]?, bool> accepts)
        {
            HostCheck.True(accepts(original) && !accepts(null) && !accepts([.. original, 0]));
            for (int i = 0; i < original.Length; i++)
            {
                HostCheck.True(!accepts(original[..i]));
                byte[] changed = [.. original];
                changed[i] ^= 1;
                HostCheck.True(!accepts(changed));
            }
        }
    }

    [HostTest("rethrow complete transcript preserves frame exception and cleanup associations")]
    private static void RethrowTranscript()
    {
        string[] ancestor = M04GCStaticRootMaterializationEfiIntegrationTests.Lines;
        int gc = Array.FindIndex(ancestor, line => line.StartsWith("M04_GC_ROOT ", StringComparison.Ordinal));
        int insert = Array.IndexOf(ancestor, "TEST_START boot.sequence");
        ulong[] image = EfiTranscriptControls.Fields(ancestor[gc]), module = EfiTranscriptControls.Fields(ancestor[gc + 1]),
            stack = EfiTranscriptControls.Fields(ancestor[gc + 2]), obj = EfiTranscriptControls.Fields(ancestor[gc + 4]),
            canaries = EfiTranscriptControls.Fields(ancestor[gc + 7]);
        ulong heap = obj[0] - 32, seed = (module[3] ^ heap) & 0xffff, sp = stack[1] - 1024, rip = image[0] + 4105;
        ulong inner = image[0] + 8192, outer = image[0] + 12288, parent = sp + 48, outerSp = parent + 64;
        ulong bp = parent + 48, outerBp = outerSp + 48, capture = parent - 624;
        int[] stages = [0, 1, 2, 3, 4, 9, 10, 5, 14, 11, 12, 13, 6, 7, 8];
        Dictionary<int, ulong[]> rows = new()
        {
            [0] = [module[3], image[0] + 65536, 512, 3],
            [1] = [obj[0], obj[1] + 64, 9, 3],
            [2] = [rip, rip ^ 1UL, sp, 0],
            [3] = [rip, sp, inner + 25, parent],
            [4] = [obj[1] + 32, inner + 69, bp, sp + 40],
            [9] = [obj[0], bp - 8, seed + 2, 1],
            [10] = [inner + 46, inner + 69, bp - 8, seed + 2],
            [5] = [obj[0], seed, seed + 2, 1],
            [14] = [inner + 87, capture - 320, capture, obj[0]],
            [11] = [inner + 25, parent, outer + 26, outerSp],
            [12] = [obj[1] + 32, outer + 51, outerBp, parent + 56],
            [13] = [obj[0], seed, outer + 51, 1],
            [6] = [outer + 27, outerSp, outerBp, 1],
            [7] = [obj[0], seed, seed + 1, 1],
            [8] = [10, sp - 4096, canaries[1], canaries[2]]
        };
        string[] child = ["TEST_START m04.021.rethrow", .. stages.Select(stage => EfiTranscriptControls.Row("M04_RETHROW", stage, rows[stage])), "TEST_PASS m04.021.rethrow", "CLROS_M04_RETHROW_READY"];
        string[] lines = [.. ancestor[..insert], .. child, .. ancestor[insert..]];
        var controls = new EfiTranscriptControls(Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/rethrow-transcript", Guid.NewGuid().ToString("N")),
            path => QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(path, includeGcStatics: true, includeRethrow: true));
        controls.Check(lines, true, "complete ancestor and rethrow vector");
        controls.Structure(lines, insert, child.Length);
        foreach (int stage in new[] { 1, 2, 3, 4, 5, 6, 7, 9, 10, 11, 12, 13, 14 })
        {
            string[] changed = [.. lines];
            ulong[] values = [.. rows[stage]];
            values[0] ^= 1;
            changed[insert + 1 + Array.IndexOf(stages, stage)] = EfiTranscriptControls.Row("M04_RETHROW", stage, values);
            controls.Check(changed, false, "changed identity or frame " + stage);
        }
    }

    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected, "unsupported profile or evidence must be rejected");
    }
}
