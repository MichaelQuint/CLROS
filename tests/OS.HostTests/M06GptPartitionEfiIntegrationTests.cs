using System.Buffers.Binary;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Std.Hash;
using OS.Tooling;

internal static unsafe class M06GptPartitionEfiIntegrationTests
{
    internal static readonly string[] Flags = [.. M06LiveBlockContractEfiIntegrationTests.Flags, "--EnableM06GptPartitionProbe"];

    [HostTest("m06.gpt.efi defaults off and requires the complete live storage profile and fresh image")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM06GptPartitionProbe && !BootSequence.RunOwnedGptPartition(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M06GptPartition"));
            foreach (string required in Flags[..^1]) Reject(() => EfiOptions.Parse(Flags.Where(f => f != required).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
        }
        foreach (bool fault in new[] { false, true })
        {
            string[] args = ["--DryRun", "--BuildEfi", .. Flags, "--GptImageVariant", fault ? "PrimaryHeaderCrcFault" : "Intact"];
            HostCheck.True(QemuProfiles.Create(QemuSmokeOptions.Parse(args)).Ordered.SequenceEqual(QemuContracts.M06GptPartitionMarkers(fault)));
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([.. args, "--SkipImageBuild"])));
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(args.Where(s => s != "--BuildEfi").ToArray())));
        }
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. Flags, "--GptImageVariant", "Unknown"])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. M06LiveBlockContractEfiIntegrationTests.Flags, "--GptImageVariant", "PrimaryHeaderCrcFault"])));
        foreach (string marker in new[] { "M06_GPT 80", "CLROS_M06_010_READY", "TEST_PASS m06.011" })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. M06LiveBlockContractEfiIntegrationTests.Flags, "--RequireMarker", marker])));
    }
    private static bool Execute(void* context) { (*(int*)context)++; return true; }

    [HostTest("m06.gpt.efi allocator admission rejects altered sector core arguments and callers")]
    private static void Ownership()
    {
        string source = File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/Boot/EfiEntry.cs"));
        foreach (string call in new[] { "BootPageAllocatorMutator.AllocateOnePage", "BootPageAllocatorMutator.FreeOnePage" })
        {
            string accepted = source;
            HostCheck.True(EfiProfileTestControls.RemoveOwnedDmaCalls(ref accepted, call, out string reason), reason);
            foreach (string changed in new[] { source.Replace("reads + 4UL)", "reads + 3UL)", StringComparison.Ordinal),
                source.Replace("RunBlockReadCore(state, false, destination", "UnreviewedRead(state, false, destination", StringComparison.Ordinal),
                source.Replace("RunBlockReadCore(state, false, destination", "RunBlockReadCore(state, true, destination", StringComparison.Ordinal) })
            {
                string rejected = changed; HostCheck.True(!EfiProfileTestControls.RemoveOwnedDmaCalls(ref rejected, call, out _));
            }
        }
    }

    [HostTest("m06.gpt.efi exact transcripts reject modified counts order CRC selection and cleanup")]
    private static void Records()
    {
        foreach (bool fault in new[] { false, true })
        {
            var (lines, _) = Capture(fault);
            QemuTranscripts.AssertM06GptPartitionRecords(lines, fault);
            Reject(() => QemuTranscripts.AssertM06GptPartitionRecords(lines[..^1], fault));
            Reject(() => QemuTranscripts.AssertM06GptPartitionRecords(lines, !fault));
            foreach (int stage in Enumerable.Range(0, 7).Concat(Enumerable.Range(0x80, fault ? 34 : 33)))
                for (int column = 0; column < 4; column++)
                {
                    // Opaque attributes and bounded payload CRCs have their semantic
                    // association checked independently against the actual disk.
                    if (stage == 6 && column == 1) continue;
                    string[] changed = [.. lines]; string key = "M06_GPT " + stage.ToString("X2");
                    int index = Array.FindIndex(changed, s => s.StartsWith(key + " ", StringComparison.Ordinal));
                    string[] words = changed[index].Split(' '); words[column + 2] = "0xFFFFFFFFFFFFFFFF"; changed[index] = string.Join(' ', words);
                    Reject(() => QemuTranscripts.AssertM06GptPartitionRecords(changed, fault));
                }
            string[] reordered = [.. lines]; int first = Array.FindIndex(lines, s => s.StartsWith("M06_GPT 80 ", StringComparison.Ordinal));
            (reordered[first], reordered[first + 1]) = (reordered[first + 1], reordered[first]);
            string path = Path.Combine(Path.GetTempPath(), "clros-gpt-" + Guid.NewGuid().ToString("N") + ".log");
            try
            {
                void Check() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                    pciEnumeration: true, virtioBlockQueueSetup: true, virtioBlockRead: true, liveBlockContract: true, gptPartition: true, primaryHeaderFault: fault);
                File.WriteAllText(path, fault ? M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(lines) : M06VirtioBlockQueueSetupEfiIntegrationTests.BlockFrame(lines)); Check();
                File.WriteAllText(path, fault ? M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(reordered) : M06VirtioBlockQueueSetupEfiIntegrationTests.BlockFrame(reordered)); Reject(Check);
            }
            finally { File.Delete(path); }
        }
    }

    [HostTest("m06.gpt.efi binds every sector and selected partition to intact and damaged primary images")]
    private static void Disk()
    {
        string dir = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/gpt-image-association", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            foreach (bool fault in new[] { false, true })
            {
                var (lines, disk) = Capture(fault);
                string serial = Path.Combine(dir, "serial.log"), image = Path.Combine(dir, "disk.raw");
                File.WriteAllLines(serial, lines); File.WriteAllBytes(image, disk);
                if (fault)
                {
                    byte[] intact = disk.ToArray(); intact[528] ^= 1;
                    File.WriteAllBytes(image + ".boot.raw", intact);
                }
                void Check() => QemuTranscripts.AssertM06GptPartitionImage(serial, image, fault);
                Check();
                disk[(fault ? (int)GptTestData.Capacity - 33 : 2) * 512 + 56] ^= 1;
                File.WriteAllBytes(image, disk); Reject(Check);
                disk[(fault ? (int)GptTestData.Capacity - 33 : 2) * 512 + 56] ^= 1;
                File.WriteAllBytes(image, disk);
                for (int i = 0; i < (fault ? 34 : 33); i++)
                {
                    string[] changed = [.. lines]; string key = "M06_GPT " + (0x80 + i).ToString("X2");
                    int index = Array.FindIndex(changed, s => s.StartsWith(key + " ", StringComparison.Ordinal));
                    string[] words = changed[index].Split(' '); words[3] = "0x0000000000000000"; changed[index] = string.Join(' ', words);
                    File.WriteAllLines(serial, changed); Reject(Check);
                }
                File.WriteAllLines(serial, lines); Check();
                if (fault)
                {
                    using (var boot = File.OpenWrite(image + ".boot.raw")) { boot.Position = 0; boot.WriteByte(1); }
                    Reject(Check);
                }
            }
        }
        finally { Directory.Delete(dir, true); }
    }

    private static (string[] Lines, byte[] Disk) Capture(bool fault)
    {
        var (primary, entries) = GptTestData.Create(); var (backup, _) = GptTestData.Create(true);
        uint headerCrc = BinaryPrimitives.ReadUInt32LittleEndian(primary.AsSpan(16)), arrayCrc = Crc32.Compute(entries), backupCrc = BinaryPrimitives.ReadUInt32LittleEndian(backup.AsSpan(16));
        if (fault) primary[16] ^= 1;
        byte[] disk = new byte[checked((int)GptTestData.Capacity * 512)];
        primary.CopyTo(disk, 512); entries.CopyTo(disk, 1024); backup.CopyTo(disk, disk.Length - 512); entries.CopyTo(disk, disk.Length - 33 * 512);
        string[] lines = QemuContracts.M06GptPartitionMarkers(fault).ToArray();
        string[] prefix = M06LiveBlockContractEfiIntegrationTests.Capture();
        for (int i = 0; i < prefix.Length; i++)
            lines[i] = !(prefix[i].StartsWith("M06_READ 0 ", StringComparison.Ordinal) || prefix[i].StartsWith("M06_BDEV 0 ", StringComparison.Ordinal) ||
                prefix[i].StartsWith("M06_BDEV A ", StringComparison.Ordinal) || prefix[i].StartsWith("M06_BDEV B ", StringComparison.Ordinal)) ? prefix[i] : prefix[i].Replace("0x0000000000010000", "0x0000000000020000", StringComparison.Ordinal)
                .Replace("0x000000000000FFFF", "0x000000000001FFFF", StringComparison.Ordinal)
                .Replace("0x0000000002000000", "0x0000000004000000", StringComparison.Ordinal);
        void Set(int stage, params ulong[] values)
        {
            string key = "M06_GPT " + stage.ToString("X2"); int index = Array.IndexOf(lines, key);
            lines[index] = key + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
        }
        ulong count = fault ? 34UL : 33, capacity = GptTestData.Capacity, entryLba = fault ? capacity - 33 : 2;
        Set(0, fault ? 14UL : 1, fault ? headerCrc ^ 1U : headerCrc, headerCrc, arrayCrc);
        Set(1, fault ? 0UL : 1, fault ? 0UL : arrayCrc, 2, fault ? 1UL : 33);
        Set(2, fault ? 1UL : 0, fault ? backupCrc : 0, fault ? backupCrc : 0, fault ? arrayCrc : 0);
        Set(3, fault ? 1UL : 0, fault ? 14UL : 1, fault ? 1UL : 0, count);
        Set(4, fault ? capacity - 1 : 1, entryLba, 34, capacity - 34);
        Set(5, 0, 2048, 131038, 128991); Set(6, 1048576, 0, count, 1);
        for (int i = 0; i < (int)count; i++)
        {
            ulong lba = i == 0 ? 1UL : fault && i == 1 ? capacity - 1 : entryLba + (ulong)(i - (fault ? 2 : 1));
            Set(0x80 + i, lba, Crc32.Compute(disk.AsSpan((int)lba * 512, 512)), (ulong)i + 4, 1);
        }
        return (lines, disk);
    }

    private static void Reject(Action action)
    {
        bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected, "invalid GPT profile or evidence must be rejected");
    }
}
