using System.Buffers.Binary;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Std.Hash;
using OS.Tooling;

internal static unsafe class M06FatReadEfiIntegrationTests
{
    internal static readonly string[] Flags = [.. M06GptPartitionEfiIntegrationTests.Flags, "--EnableM06FatReadProbe"];

    [HostTest("m06.fat.efi defaults off and requires GPT fresh intact image and explicit ownership")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM06FatReadProbe && !BootSequence.RunOwnedFatRead(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M06FatRead"));
            foreach (string required in Flags[..^1])
                Reject(() => EfiOptions.Parse(Flags.Where(f => f != required).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
        }
        string[] args = ["--DryRun", "--BuildEfi", .. Flags];
        HostCheck.True(QemuProfiles.Create(QemuSmokeOptions.Parse(args)).Ordered.SequenceEqual(QemuContracts.M06FatReadMarkers()));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([.. args, "--SkipImageBuild"])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([.. args, "--GptImageVariant", "PrimaryHeaderCrcFault"])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(args.Where(s => s != "--BuildEfi").ToArray())));
        foreach (string marker in new[] { "M06_FAT 80", "CLROS_M06_013_READY", "TEST_PASS m06.014" })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. M06GptPartitionEfiIntegrationTests.Flags, "--RequireMarker", marker])));
    }
    private static bool Execute(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    [HostTest("m06.fat.efi allocator ownership rejects altered FAT epochs purpose and caller")]
    private static void Ownership()
    {
        var snapshot = new EfiAllocatorOwnershipSources(EfiOptions.Parse(Flags, false).Profile());
        foreach (string call in new[] { "BootPageAllocatorMutator.AllocateOnePage", "BootPageAllocatorMutator.FreeOnePage" })
        {
            HostCheck.True(EfiProfileTestControls.RemoveOwnedDmaCalls(snapshot.Sources, call, out _, out string reason), reason);
            foreach (var changed in new[] {
                snapshot.ChangeEpoch("ReadFatSector", 70UL, 69UL),
                snapshot.ChangePurpose("ReadFatSector", 5, true, false),
                snapshot.RenameMethod("ReadFatSector") })
                HostCheck.True(!EfiProfileTestControls.RemoveOwnedDmaCalls(changed, call, out _, out _));
        }
    }

    [HostTest("m06.fat.efi exact records reject mutated geometry names chains epochs and cleanup")]
    private static void Records()
    {
        var (lines, _) = Capture();
        QemuTranscripts.AssertM06FatReadRecords(lines);
        Reject(() => QemuTranscripts.AssertM06FatReadRecords(lines[..^1]));
        foreach (int stage in Enumerable.Range(0, 9).Concat(Enumerable.Range(0x80, 16)))
            for (int column = 0; column < 4; column++)
            {
                string[] changed = [.. lines];
                string key = "M06_FAT " + stage.ToString("X2");
                int index = Array.FindIndex(changed, s => s.StartsWith(key + " ", StringComparison.Ordinal));
                string[] words = changed[index].Split(' ');
                words[column + 2] = "0xFFFFFFFFFFFFFFFF";
                changed[index] = string.Join(' ', words);
                Reject(() => QemuTranscripts.AssertM06FatReadRecords(changed));
            }
        string path = Path.Combine(Path.GetTempPath(), "clros-fat-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            void Check() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioBlockQueueSetup: true, virtioBlockRead: true, liveBlockContract: true, gptPartition: true, fatRead: true);
            File.WriteAllText(path, M06VirtioBlockQueueSetupEfiIntegrationTests.BlockFrame(lines));
            Check();
            string[] reordered = [.. lines];
            int start = Array.FindIndex(lines, s => s.StartsWith("M06_FAT 85 ", StringComparison.Ordinal));
            (reordered[start], reordered[start + 1]) = (reordered[start + 1], reordered[start]);
            File.WriteAllText(path, M06VirtioBlockQueueSetupEfiIntegrationTests.BlockFrame(reordered));
            Reject(Check);
        }
        finally { File.Delete(path); }
    }

    [HostTest("m06.fat.efi binds every sector directory chain mirror and known file to image bytes")]
    private static void Disk()
    {
        var (lines, disk) = Capture();
        string dir = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/fat-image-association", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string serial = Path.Combine(dir, "serial.log"), image = Path.Combine(dir, "disk.raw");
            File.WriteAllLines(serial, lines);
            File.WriteAllBytes(image, disk);
            void Check() => QemuTranscripts.AssertM06FatReadImage(serial, image);
            Check();
            foreach (int stage in Enumerable.Range(0x80, 16))
            {
                string[] changed = [.. lines];
                string key = "M06_FAT " + stage.ToString("X2");
                int index = Array.FindIndex(changed, s => s.StartsWith(key + " ", StringComparison.Ordinal));
                string[] words = changed[index].Split(' ');
                words[3] = "0x0000000000000000";
                changed[index] = string.Join(' ', words);
                File.WriteAllLines(serial, changed);
                Reject(Check);
            }
            File.WriteAllLines(serial, lines);
            // Data mutation with a refreshed sector receipt must still fail the
            // independent host name lookup / known-file content check.
            foreach (int stage in new[] { 0x86, 0x8d })
            {
                int index = Array.FindIndex(lines, s => s.StartsWith("M06_FAT " + stage.ToString("X2") + " ", StringComparison.Ordinal));
                string[] words = lines[index].Split(' ');
                int offset = checked((int)Convert.ToUInt64(words[2][2..], 16) * 512);
                disk[offset + 1] ^= 1;
                File.WriteAllBytes(image, disk);
                string[] refreshed = [.. lines];
                words[3] = "0x" + Crc32.Compute(disk.AsSpan(offset, 512)).ToString("X16");
                refreshed[index] = string.Join(' ', words);
                File.WriteAllLines(serial, refreshed);
                Reject(Check);
                disk[offset + 1] ^= 1;
                File.WriteAllBytes(image, disk);
                File.WriteAllLines(serial, lines);
            }
            Check();
        }
        finally { Directory.Delete(dir, true); }
    }

    internal static (string[] Lines, byte[] Disk) Capture()
    {
        var (prefix, disk) = M06GptPartitionEfiIntegrationTests.Capture(false);
        const int first = 2048, last = 131038, fatSectors = 993, fat = first + 32, data = fat + 2 * fatSectors;
        const uint extension = 10, fileFirst = 11, fileSecond = 13, fileThird = 12;
        void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(disk.AsSpan(offset), value);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(disk.AsSpan(offset), value);
        int Cluster(uint c) => checked((data + (int)c - 2) * 512);
        int boot = first * 512;
        U16(boot + 11, 512);
        disk[boot + 13] = 1;
        U16(boot + 14, 32);
        disk[boot + 16] = 2;
        U32(boot + 32, last - first + 1);
        U32(boot + 36, fatSectors);
        U32(boot + 44, 2);
        U16(boot + 510, 0xaa55);
        foreach ((uint c, uint next) in new[] { (2u, extension), (extension, 0x0fffffffu), (fileFirst, fileSecond), (fileSecond, fileThird), (fileThird, 0x0fffffffu) })
            foreach (int copy in new[] { 0, fatSectors })
                U32((fat + copy) * 512 + (int)c * 4, next);
        int root = Cluster(2), tail = Cluster(extension);
        "EFI        "u8.CopyTo(disk.AsSpan(root));
        disk[root + 11] = 0x10;
        U16(root + 26, 3);
        "OS         "u8.CopyTo(disk.AsSpan(root + 32));
        disk[root + 43] = 0x10;
        U16(root + 58, 9);
        for (int i = 2; i < 15; i++)
            disk[root + i * 32] = 0xe5;
        byte[] alias = MultiClusterDirectoryVFATLongNameSupportSeparateTests.ShortEntry();
        BinaryPrimitives.WriteUInt16LittleEndian(alias.AsSpan(26), (ushort)fileFirst);
        var lfn = MultiClusterDirectoryVFATLongNameSupportSeparateTests.Encode(FatImageFixture.LongName, alias);
        lfn[0].CopyTo(disk, root + 480);
        lfn[1].CopyTo(disk, tail);
        lfn[2].CopyTo(disk, tail + 32);
        alias.CopyTo(disk, tail + 64);
        uint[] chain = [fileFirst, fileSecond, fileThird];
        for (int s = 0; s < 3; s++)
        {
            disk.AsSpan(Cluster(chain[s]), 512).Fill(0xa5);
            for (int i = 0; i < 512 && s * 512 + i < 1301; i++)
                disk[Cluster(chain[s]) + i] = unchecked((byte)(((s * 512 + i) * 73 + 19) ^ ((s * 512 + i) >> 3)));
        }
        string[] lines = QemuContracts.M06FatReadMarkers().ToArray();
        prefix.CopyTo(lines, 0);
        void Set(int stage, params ulong[] values)
        {
            string key = "M06_FAT " + stage.ToString("X2");
            int index = Array.IndexOf(lines, key);
            lines[index] = key + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
        }
        Set(0, first, last, fat, data);
        Set(1, 1, last - data + 1, 2, last - first + 1);
        Set(2, 2, 2, extension, 5);
        Set(3, 2, 20, 1, 1);
        Set(4, fileFirst, 1301, 30, Crc32.Compute(Encoding.Unicode.GetBytes(FatImageFixture.LongName)));
        Set(5, 0x317e45524946534c, 0x4e4942, 0x20, 7);
        Set(6, 1301, 0xe194d61d, 0xe194d61d, 3);
        Set(7, fileFirst, fileSecond, fileThird, 16);
        Set(8, 1, 3, 15, 85);
        ulong[] lbas = [first, fat, fat + fatSectors, fat, fat + fatSectors, data, data + extension - 2,
            data + fileFirst - 2, fat, fat + fatSectors, data + fileSecond - 2, fat, fat + fatSectors,
            data + fileThird - 2, fat, fat + fatSectors];
        for (int i = 0; i < 16; i++)
            Set(0x80 + i, lbas[i], Crc32.Compute(disk.AsSpan((int)lbas[i] * 512, 512)), (ulong)i + 70, 1);
        return (lines, disk);
    }

    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected, "invalid FAT profile or evidence must be rejected");
    }
}
