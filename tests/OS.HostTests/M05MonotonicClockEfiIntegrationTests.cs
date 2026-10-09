using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;
using OS.Tooling;

internal static unsafe class M05MonotonicClockEfiIntegrationTests
{
    [HostTest("m05.003.efi disabled boot gate and native access bytes")]
    private static void DisabledAndNative()
    {
        int invocations = 0;
        HostCheck.True(!Probes.EnableM05MonotonicClockProbe &&
            !BootSequence.RunOwnedMonotonicClock(true, &invocations, &Execute) &&
            !BootSequence.RunOwnedMonotonicClock(false, &invocations, &Execute) &&
            !BootSequence.RunOwnedMonotonicClock(true, null, &Execute) &&
            !BootSequence.RunOwnedMonotonicClock(true, &invocations, null) && invocations == 0);
        byte* code = stackalloc byte[96];
        new Span<byte>(code, 96).Fill(0xcc);
        HostCheck.True(!X64OwnedClockAccess.Emit(code, 95));
        HostCheck.True(new ReadOnlySpan<byte>(code, 96).IndexOfAnyExcept((byte)0xcc) < 0);
        HostCheck.True(X64OwnedClockAccess.Emit(code, 96));
        // Independent instruction fixtures: ABI-preserved RBX around CPUID, UC
        // indexed loads/stores, and LFENCE on both sides of RDTSC.
        HostCheck.True(new ReadOnlySpan<byte>(code, 11).SequenceEqual(Convert.FromHexString("F30F1EFA89D0488B0401C3")));
        HostCheck.True(new ReadOnlySpan<byte>(code + 16, 11).SequenceEqual(Convert.FromHexString("F30F1EFA89D24C890411C3")));
        HostCheck.True(new ReadOnlySpan<byte>(code + 32, 28).SequenceEqual(Convert.FromHexString("F30F1EFA5389C889D10FA241890041895804418948084189500C5BC3")));
        HostCheck.True(new ReadOnlySpan<byte>(code + 64, 20).SequenceEqual(Convert.FromHexString("F30F1EFA0FAEE80F3148C1E2204809D00FAEE8C3")));
        for (int i = 0; i < 96; i++) { code[i] ^= 1; HostCheck.True(!X64OwnedClockAccess.Validate(code, 96)); code[i] ^= 1; }
    }
    private static bool Execute(void* context) { (*(int*)context)++; return true; }

    [HostTest("m05.003.efi profile guards and fresh capture")]
    private static void Profile()
    {
        string[] flags = ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe",
            "--EnableAcpiPhysicalDiscoveryProbe", "--EnableM03FirstRealExitBootServicesProbe",
            "--EnableM03OwnedSubsystemSurvivalProbe", "--EnableM05MonotonicClockProbe"];
        foreach (bool build in new[] { true, false })
        {
            HostCheck.True(EfiOptions.Parse(flags, build).Profile().IsEnabled("M05MonotonicClock"));
            foreach (string required in flags[..^1])
                Reject(() => EfiOptions.Parse(flags.Where(f => f != required).ToArray(), build).Profile());
            foreach (string conflict in new[] { "M05PhysicalMapping", "M05OwnedAcpiSelection", "M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation" })
                Reject(() => EfiOptions.Parse([.. flags, "--Enable" + conflict + "Probe"], build).Profile());
            Reject(() => EfiOptions.Parse([.. flags, "--BootPolicy", "VerifiedExit"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. flags]));
        HostCheck.True(QemuContracts.M05MonotonicClockMarkers.All(plan.Required.Contains));
        HostCheck.True(plan.Forbidden.Contains("TEST_FAIL"));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", .. flags])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--SkipImageBuild", .. flags])));
    }

    [HostTest("m05.003.efi transcript rejects frequency progress and recovery contradictions")]
    private static void Transcript()
    {
        string Row(int stage, params ulong[] values) => "M05_CLOCK " + stage + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
        string[] lines = Enumerable.Repeat("fixture", QemuContracts.M05MonotonicClockMarkers.Count).ToArray();
        int start = QemuContracts.M03OwnedSubsystemMarkers.Count;
        lines[start] = "TEST_START m05.003";
        lines[start + 1] = Row(0, 0xfed00000, (100000000UL << 32) | 0x8086a201, 100000000, 64);
        lines[start + 2] = Row(1, 1, 10000000, 1, 1);
        lines[start + 3] = Row(2, 123, 456, 0, 0);
        lines[start + 4] = Row(3, 2, 5, 1, 1);
        lines[^2] = "CLROS_M05_003_READY"; lines[^1] = "TEST_PASS m05.003";
        QemuTranscripts.AssertM05MonotonicClockRecords(lines);
        foreach ((int stage, ulong[] values) in new (int, ulong[])[]
        {
            (0, [0xfed00001, (100000000UL << 32) | 0x8086a201, 100000000, 64]),
            (0, [0xfed00000, (100000000UL << 32) | 0x8086a201, 99999999, 64]),
            (0, [0xfed00000, (100000000UL << 32) | 0x8086a201, 100000000, 32]),
            (1, [1, 10000001, 1, 1]), (1, [1, 10000000, 2, 1]), (1, [2, 10000000, 1, 1]),
            (2, [123, 123, 0, 0]), (2, [0, 456, 0, 0]), (2, [123, 456, 0, 10000000]),
            (2, [123, 456, 1, 0]), (3, [2, 5, 0, 1]), (3, [2, 5, 1, 0])
        })
        {
            string[] changed = [.. lines]; changed[start + 1 + stage] = Row(stage, values);
            Reject(() => QemuTranscripts.AssertM05MonotonicClockRecords(changed));
        }
        Reject(() => QemuTranscripts.AssertM05MonotonicClockRecords([.. lines, "extra"]));
    }
    private static void Reject(Action action)
    {
        bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected, "invalid profile or evidence must be rejected");
    }
}
