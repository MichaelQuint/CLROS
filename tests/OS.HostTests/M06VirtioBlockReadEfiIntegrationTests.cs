using System.Buffers.Binary;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M06VirtioBlockReadEfiIntegrationTests
{
    internal static readonly string[] Flags = [.. M06VirtioBlockQueueSetupEfiIntegrationTests.Flags, "--EnableM06VirtioBlockReadProbe"];

    [HostTest("m06.read.efi requires the owned queue profile and defaults off")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM06VirtioBlockReadProbe && !BootSequence.RunOwnedVirtioBlockRead(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M06VirtioBlockRead"));
            foreach (string required in Flags[..^1])
                Reject(() => EfiOptions.Parse(Flags.Where(f => f != required).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
            foreach (string other in new[] { "M05VirtioNegotiation", "M05ApicSetup", "M05VirtqueuePolling", "M05VirtqueueInterrupt", "M05BarDma" })
                Reject(() => EfiOptions.Parse([.. Flags, "--Enable" + other + "Probe"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. Flags]));
        HostCheck.True(QemuContracts.M06VirtioBlockReadMarkers.SequenceEqual(plan.Ordered));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", .. Flags])));
        foreach (string marker in new[] { "M06_READ 0", "CLROS_M06_005_READY", "TEST_PASS m06.006" })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. M06VirtioBlockQueueSetupEfiIntegrationTests.Flags, "--RequireMarker", marker])));
    }
    private static bool Execute(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    [HostTest("m06.read.efi transcript rejects altered transport ownership completion cleanup and control evidence")]
    private static void Transcript()
    {
        string[] lines = Capture();
        string path = Path.Combine(Path.GetTempPath(), "clros-block-read-" + Guid.NewGuid().ToString("N") + ".log");
        void Check(string[] rows)
        {
            File.WriteAllText(path, M06VirtioBlockQueueSetupEfiIntegrationTests.BlockFrame(rows));
            QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioBlockQueueSetup: true, virtioBlockRead: true);
        }
        try
        {
            Check(lines);
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioBlockQueueSetup: true));
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioBlockRead: true));
            int[] indices = Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith("M06_READ ", StringComparison.Ordinal)).ToArray();
            foreach ((int row, int col, ulong wrong) in new (int, int, ulong)[]
            {
                (0,0,0),(0,0,ulong.MaxValue),(0,1,0),(0,2,511),(0,3,256),
                (1,0,0),(1,0,17),(1,1,1),(1,2,0),(1,3,0),(1,3,1000001),
                (2,0,0),(2,1,0),(2,2,0),(2,3,8192),
                (3,0,0),(3,0,2),(3,1,1),(3,2,512),(3,2,514),(3,3,1),
                (4,0,0),(4,1,0),(4,2,3),(4,3,1),(5,0,ulong.MaxValue),(5,3,511),
                (6,0,0),(6,1,0),(6,2,0),(6,3,0),(6,3,1025),
                (7,0,0),(7,1,0),(7,2,999),(7,3,0),(8,0,0x407),(8,1,15),(8,2,0x8000),(8,3,0),
                (9,0,1),(9,1,1),(9,2,1),(9,3,1)
            })
            {
                string[] changed = [.. lines];
                string[] parts = changed[indices[row]].Split(' ');
                parts[col + 2] = "0x" + wrong.ToString("X16");
                changed[indices[row]] = string.Join(' ', parts);
                Reject(() => Check(changed));
            }
            string[] swapped = [.. lines];
            (swapped[^1], swapped[^2]) = (swapped[^2], swapped[^1]);
            Reject(() => Check(swapped));
            Reject(() => Check([.. lines, "extra"]));
            Reject(() => Check(lines[..^1]));
            Check(lines);
        }
        finally { File.Delete(path); }
    }

    [HostTest("m06.read.efi compares the guest sector receipt with actual disk bytes and capacity")]
    private static void DiskAssociation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "clros-read-disk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string serial = Path.Combine(directory, "serial.log"), image = Path.Combine(directory, "disk.raw");
        byte[] disk = new byte[4096];
        for (int i = 512; i < 1024; i++)
            disk[i] = unchecked((byte)(i * 37 + 5));
        string[] lines = Capture();
        int geometry = Array.FindIndex(lines, s => s.StartsWith("M06_READ 0 ", StringComparison.Ordinal));
        int payload = Array.FindIndex(lines, s => s.StartsWith("M06_READ 5 ", StringComparison.Ordinal));
        lines[geometry] = Row(0, 8, 1, 512, 0);
        lines[payload] = Row(5, OS.Std.Hash.Crc32.Compute(disk.AsSpan(512, 512)),
            BinaryPrimitives.ReadUInt64LittleEndian(disk.AsSpan(512)), BinaryPrimitives.ReadUInt64LittleEndian(disk.AsSpan(1016)), 512);
        try
        {
            File.WriteAllLines(serial, lines);
            File.WriteAllBytes(image, disk);
            QemuTranscripts.AssertM06VirtioBlockReadImage(serial, image);
            disk[800] ^= 1;
            File.WriteAllBytes(image, disk);
            Reject(() => QemuTranscripts.AssertM06VirtioBlockReadImage(serial, image));
            disk[800] ^= 1;
            File.WriteAllBytes(image, disk[..^512]);
            Reject(() => QemuTranscripts.AssertM06VirtioBlockReadImage(serial, image));
            File.WriteAllBytes(image, disk);
            lines[geometry] = Row(0, 8, 2, 512, 0);
            File.WriteAllLines(serial, lines);
            Reject(() => QemuTranscripts.AssertM06VirtioBlockReadImage(serial, image));
        }
        finally { Directory.Delete(directory, true); }
    }

    internal static string[] Capture()
    {
        string[] prefix = M06VirtioBlockQueueSetupEfiIntegrationTests.Capture();
        string[] lines = QemuContracts.M06VirtioBlockReadMarkers.ToArray();
        prefix.CopyTo(lines, 0);
        ulong[] dma = prefix.Single(s => s.StartsWith("M06_BLK 5 ", StringComparison.Ordinal)).Split(' ').Skip(2).Select(s => Convert.ToUInt64(s[2..], 16)).ToArray();
        ulong[] allocator = prefix.Single(s => s.StartsWith("M06_BLK B ", StringComparison.Ordinal)).Split(' ').Skip(2).Select(s => Convert.ToUInt64(s[2..], 16)).ToArray();
        ulong[][] values = [[65536,1,512,0],[1,0,0xc000003000,10],dma,[1,0,513,0],[1,1,4,2],[123,0x5452415020494645,0,512],
            [1,1,1,1],allocator,[0x403,0,2,1],[14,14,15,14]];
        int stage = 0;
        for (int i = prefix.Length; i < lines.Length; i++)
            if (lines[i].StartsWith("M06_READ ", StringComparison.Ordinal))
            {
                lines[i] = Row(stage, values[stage]);
                stage++;
            }
        return lines;
    }
    private static string Row(int stage, params ulong[] values) => "M06_READ " + stage + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected, "invalid block read evidence must be rejected");
    }
}
