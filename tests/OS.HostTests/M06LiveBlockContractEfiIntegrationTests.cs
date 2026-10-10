using System.Buffers.Binary;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M06LiveBlockContractEfiIntegrationTests
{
    internal static readonly string[] Flags = [.. M06VirtioBlockReadEfiIntegrationTests.Flags, "--EnableM06LiveBlockContractProbe"];

    [HostTest("m06.bdev.efi requires the live read profile and defaults off")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM06LiveBlockContractProbe && !BootSequence.RunOwnedLiveBlockContract(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M06LiveBlockContract"));
            foreach (string required in Flags[..^1]) Reject(() => EfiOptions.Parse(Flags.Where(f => f != required).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
            foreach (string other in new[] { "M05VirtioNegotiation", "M05ApicSetup", "M05VirtqueuePolling", "M05VirtqueueInterrupt", "M05BarDma" })
                Reject(() => EfiOptions.Parse([.. Flags, "--Enable" + other + "Probe"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. Flags]));
        HostCheck.True(QemuContracts.M06LiveBlockContractMarkers.SequenceEqual(plan.Ordered));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", .. Flags])));
        foreach (string marker in new[] { "M06_BDEV 0", "CLROS_M06_007_READY", "TEST_PASS m06.008" })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. M06VirtioBlockReadEfiIntegrationTests.Flags, "--RequireMarker", marker])));
    }
    private static bool Execute(void* context) { (*(int*)context)++; return true; }

    [HostTest("m06.bdev.efi static admission rejects changed shared Span compiler and block sources")]
    private static void SourceAdmission()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string entry = File.ReadAllText(Path.Combine(root, "src/OS/Boot/EfiEntry.cs"));
        foreach (string call in new[] { "BootPageAllocatorMutator.AllocateOnePage", "BootPageAllocatorMutator.FreeOnePage" })
            foreach (string changed in new[] { entry.Replace("RunBlockReadCore(state, true)", "RunBlockReadCore(state, false)", StringComparison.Ordinal),
                entry.Replace("RunBlockReadCore(state, true)", "UnreviewedRead(state, true)", StringComparison.Ordinal),
                entry.Replace("RunBlockReadCore(state)", "RunBlockReadCore(state, true)", StringComparison.Ordinal) })
            {
                string rejected = changed;
                HostCheck.True(!EfiProfileTestControls.RemoveOwnedDmaCalls(ref rejected, call, out _));
            }
        string copy = Path.Combine(root, "artifacts/bdev-admission", Guid.NewGuid().ToString("N"));
        void Copy(string relative)
        {
            string destination = Path.GetFullPath(Path.Combine(copy, relative));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Path.GetFullPath(Path.Combine(root, relative)), destination);
        }
        try
        {
            foreach (string file in new[] { "src/OS/OS.csproj", "Directory.Build.props", "Directory.Build.targets",
                "tools/Directory.Build.props", "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj",
                "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets" }) Copy(file);
            foreach (string item in EfiContracts.CompileItems) Copy(Path.Combine("src/OS", item.Split('|')[0]));
            var accepted = EfiProjectAdmission.InspectStatic(copy);
            HostCheck.True(accepted.Succeeded, accepted.Reason + " " + accepted.Detail);
            string[] sources = [@"..\..\std\no-runtime\shared\Memory\SystemSpanPointer.cs", @"Runtime\MinimalRuntime.cs",
                .. new[] { "IReadOnlyBlockDevice", "BlockDeviceInfo", "BlockDeviceInfoResult", "BlockDeviceStatus",
                    "BlockReadResult", "BlockDeviceRead", "LiveDeviceExistingIReadOnlyBlockDeviceContract", "LBAN0Guest" }
                    .Select(name => @"..\OS.Kernel\Storage\" + name + ".cs")];
            foreach (string source in sources)
            {
                string path = Path.GetFullPath(Path.Combine(copy, "src/OS", source)), original = File.ReadAllText(path);
                // The actual wrapper admission must bind every byte of the reviewed surface,
                // including the shared/compiler inputs that are outside OS.Kernel.
                File.WriteAllText(path, original + "\n// unreviewed local input\n");
                var rejected = EfiProjectAdmission.InspectStatic(copy);
                HostCheck.True(!rejected.Succeeded && rejected.Reason == "forbidden_active_source" &&
                    rejected.Detail.Contains(source, StringComparison.Ordinal), source + ":" + rejected.Reason);
                File.WriteAllText(path, original);
            }
            HostCheck.True(EfiProjectAdmission.InspectStatic(copy).Succeeded);
        }
        finally { if (Directory.Exists(copy)) Directory.Delete(copy, true); }
    }

    [HostTest("m06.bdev.efi transcript binds original storage receipts and rejects malformed contract evidence")]
    private static void Transcript()
    {
        string[] lines = Capture();
        string path = Path.Combine(Path.GetTempPath(), "clros-live-block-" + Guid.NewGuid().ToString("N") + ".log");
        void Check(string[] rows)
        {
            File.WriteAllText(path, M06VirtioBlockQueueSetupEfiIntegrationTests.BlockFrame(rows));
            QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioBlockQueueSetup: true, virtioBlockRead: true, liveBlockContract: true);
        }
        try
        {
            Check(lines);
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioBlockQueueSetup: true, virtioBlockRead: true));
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioBlockQueueSetup: true, liveBlockContract: true));
            int[] indices = Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith("M06_BDEV ", StringComparison.Ordinal)).ToArray();
            foreach ((int row, int col, ulong wrong) in new (int, int, ulong)[]
            {
                (0,0,65535),(0,0,ulong.MaxValue),(0,1,1),(0,2,511),(0,3,256),
                (1,0,0),(1,0,17),(1,1,1),(1,2,0),(1,3,0),(1,3,1000001),
                (2,0,0),(2,1,0),(2,2,0),(2,3,8192),(3,0,0),(3,1,1),(3,2,512),(3,3,1),
                (4,0,0),(4,1,0),(4,2,3),(4,3,1),(5,0,ulong.MaxValue),(5,3,511),
                (6,0,0),(6,1,0),(6,2,0),(6,3,0),(6,3,1025),
                (7,0,0),(7,1,0),(7,2,999),(7,3,0),(8,0,0x407),(8,1,15),(8,2,0x8000),(8,3,0),
                (9,0,1),(9,1,1),(9,2,1),(9,3,1),(10,0,4096),(10,1,65535),(10,2,512),(10,3,0),
                (11,0,0),(11,1,0),(11,2,65536),(11,3,0),(12,0,511),(12,1,1),(12,2,1),(12,3,0),
                (13,0,1),(13,1,1),(13,2,1),(13,3,1)
            })
            {
                string[] changed = [.. lines]; string[] parts = changed[indices[row]].Split(' ');
                parts[col + 2] = "0x" + wrong.ToString("X16"); changed[indices[row]] = string.Join(' ', parts);
                Reject(() => Check(changed));
            }
            foreach (string malformed in new[] { "M06_BDEV 0 0x1", lines[indices[0]] + " 0x0000000000000000", lines[indices[0]].Replace("0x0000000000010000", "0x10000") })
            { string[] changed = [.. lines]; changed[indices[0]] = malformed; Reject(() => Check(changed)); }
            string[] swapped = [.. lines]; (swapped[indices[0]], swapped[indices[1]]) = (swapped[indices[1]], swapped[indices[0]]); Reject(() => Check(swapped));
            string[] duplicate = [.. lines]; duplicate[indices[1]] = duplicate[indices[0]]; Reject(() => Check(duplicate));
            Reject(() => Check([.. lines, "extra"])); Reject(() => Check(lines[..^1])); Check(lines);
        }
        finally { File.Delete(path); }
    }

    [HostTest("m06.bdev.efi associates LBA0 with actual disk geometry bytes and independent protective MBR parsing")]
    private static void DiskAssociation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "clros-bdev-disk-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string serial = Path.Combine(directory, "serial.log"), image = Path.Combine(directory, "disk.raw");
        byte[] disk = new byte[4096]; disk[0] = 0x31; disk[440] = 0x42;
        disk[450] = 0xee; BinaryPrimitives.WriteUInt32LittleEndian(disk.AsSpan(454), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(disk.AsSpan(458), 7); disk[510] = 0x55; disk[511] = 0xaa;
        string[] lines = Capture();
        lines[Array.FindIndex(lines, s => s.StartsWith("M06_READ 0 ", StringComparison.Ordinal))] =
            "M06_READ 0 0x0000000000000008 0x0000000000000001 0x0000000000000200 0x0000000000000000";
        Set(lines, 0, 8, 0, 512, 0); Set(lines, 10, 512, 8, 4096, 1); Set(lines, 11, 1, 1, 7, 512);
        void Receipt()
        {
            Set(lines, 5, OS.Std.Hash.Crc32.Compute(disk.AsSpan(0, 512)), BinaryPrimitives.ReadUInt64LittleEndian(disk),
                BinaryPrimitives.ReadUInt64LittleEndian(disk.AsSpan(504)), 512);
            File.WriteAllLines(serial, lines); File.WriteAllBytes(image, disk);
        }
        void Check() => QemuTranscripts.AssertM06LiveBlockContractImage(serial, image);
        try
        {
            Receipt(); Check();
            disk[100] ^= 1; File.WriteAllBytes(image, disk); Reject(Check); disk[100] ^= 1;
            foreach (int offset in new[] { 446, 450, 454, 458, 462, 509, 510, 511 })
            { disk[offset] ^= 1; Receipt(); Reject(Check); disk[offset] ^= 1; }
            Receipt(); File.WriteAllBytes(image, disk[..^512]); Reject(Check);
            File.WriteAllBytes(image, disk[..^1]); Reject(Check);
            foreach ((int row, int column) in new[] { (0,0),(0,1),(0,2),(5,1),(5,2),(5,3),(10,0),(10,1),(10,2),(10,3),(11,0),(11,1),(11,2),(11,3) })
            {
                Receipt(); string[] changed = [.. lines]; int index = Array.FindIndex(changed, s => s.StartsWith("M06_BDEV " + row.ToString("X") + " ", StringComparison.Ordinal));
                string[] parts = changed[index].Split(' '); parts[column + 2] = "0xFFFFFFFFFFFFFFFF"; changed[index] = string.Join(' ', parts);
                File.WriteAllLines(serial, changed); Reject(Check);
            }
            Receipt(); Check();
        }
        finally { Directory.Delete(directory, true); }
    }

    private static string[] Capture()
    {
        string[] prefix = M06VirtioBlockReadEfiIntegrationTests.Capture();
        string[] lines = QemuContracts.M06LiveBlockContractMarkers.ToArray(); prefix.CopyTo(lines, 0);
        ulong[] Prior(int stage) => prefix.Single(s => s.StartsWith("M06_READ " + stage + " ", StringComparison.Ordinal)).Split(' ').Skip(2).Select(s => Convert.ToUInt64(s[2..], 16)).ToArray();
        ulong[][] values = [[65536,0,512,0],Prior(1),Prior(2),[1,0,513,0],[1,1,4,2],[123,0,0xaa55000000000000,512],
            [1,1,1,1],Prior(7),Prior(8),[14,14,15,14],[512,65536,33554432,1],[1,1,65535,512],[512,0,0,1],[4,5,6,7]];
        for (int stage = 0; stage < values.Length; stage++) Set(lines, stage, values[stage]);
        return lines;
    }
    private static void Set(string[] lines, int stage, params ulong[] values)
    {
        string key = "M06_BDEV " + stage.ToString("X");
        int index = Array.FindIndex(lines, s => s == key || s.StartsWith(key + " ", StringComparison.Ordinal));
        lines[index] = key + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
    }
    private static void Reject(Action action)
    {
        bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected, "invalid live block profile or evidence must be rejected");
    }
}
