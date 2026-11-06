using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M06BlockMsixEfiIntegrationTests
{
    internal static readonly string[] Flags = [.. M06PackageReadEfiIntegrationTests.Flags,
        "--EnableM05ApicSetupProbe", "--EnableM06BlockMsixProbe"];

    [HostTest("m06.block-msix.efi requires the explicit package and APIC chain and defaults off")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM06BlockMsixProbe && !Probes.EnableM06BlockMsixPollingRecoveryProbe &&
            !BootSequence.RunOwnedBlockMsix(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M06BlockMsix"));
            foreach (string parent in Flags[..^1])
                Reject(() => EfiOptions.Parse(Flags.Where(f => f != parent).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
            foreach (string conflict in new[] { "M05BarDma", "M05VirtioNegotiation", "M05VirtqueuePolling", "M05VirtqueueInterrupt" })
                Reject(() => EfiOptions.Parse([.. Flags, "--Enable" + conflict + "Probe"], build).Profile());
        }
    }

    [HostTest("m06.block-msix.efi masked polling recovery requires the main profile")]
    private static void Recovery()
    {
        foreach (bool build in new[] { false, true })
        {
            string[] recovery = [.. Flags, "--EnableM06BlockMsixPollingRecoveryProbe"];
            HostCheck.True(EfiOptions.Parse(recovery, build).Profile().IsEnabled("M06BlockMsixPollingRecovery"));
            Reject(() => EfiOptions.Parse(recovery.Where(f => f != "--EnableM06BlockMsixProbe").ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. recovery, "--EnableM06BlockMsixProbe", "false"], build).Profile());
            foreach (string flag in new[] { "EnableM06BlockMsixProbe", "EnableM06BlockMsixPollingRecoveryProbe" })
                Reject(() => EfiOptions.Parse([.. Flags, "--" + flag, "invalid"], build).Profile());
        }
    }

    private static bool Execute(void* context)
    {
        (*(int*)context)++;
        return true;
    }
    [HostTest("m06.block-msix.efi hands intact IRQ and recovery marker contracts to the runner")]
    private static void Runner()
    {
        foreach (bool recovery in new[] { false, true })
        {
            string[] args = ["qemu", "smoke", "--DryRun", "--BuildEfi", .. Flags,
                .. recovery ? new[] { "--EnableM06BlockMsixPollingRecoveryProbe" } : []];
            var result = HostToolProcess.RunTool(args);
            HostCheck.True(result.ExitCode == 0, result.Error + result.Output);
            string markers = string.Join(';', QemuContracts.M06BlockMsixMarkers);
            foreach (string prefix in new[] { "REQUIRED_MARKERS ", "ORDERED_MARKERS ", "UNIQUE_MARKERS " })
                HostCheck.True(result.Output.Contains(prefix + markers, StringComparison.Ordinal), prefix);
            HostCheck.True(result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal));
            foreach (string variant in new[] { "ManifestVersion", "PackageChecksum", "PackageHeader", "PackageTruncated", "PackageLoop" })
                Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([.. args[2..], "--PackageImageVariant", variant])));
            foreach (string flag in new[] { "--BuildEfi", "--EnableM06BlockMsixProbe" })
                Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(args[2..].Where(a => a != flag).ToArray())));
        }
    }

    [HostTest("m06.block-msix.efi rejects each corrupt IRQ receipt and binds normalized parent image evidence")]
    private static void Records()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/block-msix-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string serial = Path.Combine(directory, "serial.log"), image = Path.Combine(directory, "disk.raw");
        try
        {
            var (parent, disk) = M06PackageReadEfiIntegrationTests.Capture("Intact");
            ReadOnlySpan<byte> sector = disk.AsSpan(512, 512);
            int payloadIndex = Array.FindIndex(parent, l => l.StartsWith("M06_READ 5 ", StringComparison.Ordinal));
            parent[payloadIndex] = "M06_READ 5" + string.Concat(new ulong[] {
                OS.Std.Hash.Crc32.Compute(sector), System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(sector),
                System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(sector[504..]), 512
            }.Select(v => " 0x" + v.ToString("X16")));
            int pci = QemuContracts.M05PciEnumerationMarkers.Count;
            string[] apic = M05ApicSetupEfiIntegrationTests.Capture()[pci..];
            ulong[] Row(string prefix) => parent.Single(l => l.StartsWith(prefix + " ", StringComparison.Ordinal))
                .Split(' ')[2..].Select(v => Convert.ToUInt64(v[2..], 16)).ToArray();
            foreach (bool recovery in new[] { false, true })
            {
                string[] lines = [.. parent[..pci], .. apic, .. parent[pci..], .. QemuContracts.M06BlockMsixMarkers.TakeLast(12)];
                ulong[][] rows = [
                    [recovery ? 3UL : 2UL, Row("M06_BLK 1")[3], 6, Row("M06_READ 0")[0]],
                    [1,512,0,513], [0xfee00000,0xe0,0x210000,0x210800],
                    recovery ? [0,0,0,0] : [1,0,1,0],
                    recovery ? [0,0,2,2] : [0x481300,0x202,2,2],
                    [4095,0x400000,4095,0x400000], [recovery ? 1000000UL : 1UL,1000000,1,4],
                    [1,1,1,1], [Row("M06_READ 5")[0],1,0,1]
                ];
                M05ApicSetupEfiIntegrationTests.Fill(lines, "M06_IRQ", rows);
                QemuTranscripts.AssertM06BlockMsixRecords(lines, recovery);
                HostCheck.True(QemuTranscripts.NormalizeM06BlockMsixRecords(lines).SequenceEqual(parent));
                File.WriteAllText(serial, M06VirtioBlockQueueSetupEfiIntegrationTests.BlockFrame(lines));
                File.WriteAllBytes(image, disk);
                QemuTranscripts.AssertM03AcpiBootstrapTranscript(serial, firstRealExit: true, ownedSurvival: true,
                    pciEnumeration: true, apicSetup: true, virtioBlockQueueSetup: true, virtioBlockRead: true, liveBlockContract: true,
                    gptPartition: true, fatRead: true, packageRead: true, blockMsix: true, blockMsixPollingRecovery: recovery);
                QemuTranscripts.AssertM06BlockMsixImage(serial, image, recovery);
                Reject(() => QemuTranscripts.AssertM06BlockMsixRecords(lines, !recovery));
                Reject(() => QemuTranscripts.AssertM06BlockMsixRecords(lines[..^1], recovery));
                int[] field = [0, 3, 1, 0, 2, 3, 1, 3, 0];
                for (int stage = 0; stage < 9; stage++)
                {
                    string[] changed = (string[])lines.Clone();
                    int index = Array.FindIndex(changed, s => s.StartsWith("M06_IRQ " + stage + " ", StringComparison.Ordinal));
                    ulong[] values = (ulong[])rows[stage].Clone();
                    values[field[stage]] ^= stage == 4 ? 0x200UL : 1UL;
                    changed[index] = "M06_IRQ " + stage + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
                    Reject(() => QemuTranscripts.AssertM06BlockMsixRecords(changed, recovery));
                }
            }
        }
        finally
        {
            Directory.Delete(directory, true);
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
        HostCheck.True(rejected);
    }
}
