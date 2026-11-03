using System.Buffers.Binary;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Std.Hash;
using OS.Storage;
using OS.Tooling;

internal static unsafe class M06PackageReadEfiIntegrationTests
{
    internal static readonly string[] Flags = [.. M06FatReadEfiIntegrationTests.Flags, "--EnableM06PackageReadProbe"];
    private static readonly string[] Variants = ["Intact", "ManifestVersion", "PackageChecksum", "PackageHeader", "PackageTruncated", "PackageLoop"];

    [HostTest("m06.package.efi hands the complete long marker contract to the real runner")]
    private static void RunnerHandoff()
    {
        string[] args = ["qemu", "smoke", "--DryRun", "--BuildEfi", .. Flags];
        var result = HostToolProcess.RunTool(args);
        HostCheck.True(result.ExitCode == 0, result.Error + result.Output);
        string markers = string.Join(";", QemuContracts.M06PackageReadMarkers("Intact"));
        foreach (string prefix in new[] { "REQUIRED_MARKERS ", "ORDERED_MARKERS ", "UNIQUE_MARKERS " })
            HostCheck.True(result.Output.Contains(prefix + markers, StringComparison.Ordinal), prefix);
        HostCheck.True(result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal));
    }

    [HostTest("m06.package.efi defaults off and requires FAT fresh image and exact fault ownership")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM06PackageReadProbe && !BootSequence.RunOwnedPackageRead(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M06PackageRead"));
            foreach (string parent in Flags[..^1])
                Reject(() => EfiOptions.Parse(Flags.Where(f => f != parent).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
        }
        string[] args = ["--DryRun", "--BuildEfi", .. Flags];
        foreach (string variant in Variants)
            HostCheck.True(QemuProfiles.Create(QemuSmokeOptions.Parse([.. args, "--PackageImageVariant", variant])).Ordered
                .SequenceEqual(QemuContracts.M06PackageReadMarkers(variant)));
        foreach (string[] bad in new string[][] { [.. args, "--SkipImageBuild"], [.. args, "--GptImageVariant", "PrimaryHeaderCrcFault"],
            [.. args, "--PackageImageVariant", "Unknown"], args.Where(a => a != "--BuildEfi").ToArray(),
            ["--DryRun", "--BuildEfi", .. M06FatReadEfiIntegrationTests.Flags, "--PackageImageVariant", "PackageLoop"] })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(bad)));
        foreach (string marker in new[] { "M06_PKG 80", "CLROS_M06_018_READY", "TEST_PASS m06.017" })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. M06FatReadEfiIntegrationTests.Flags, "--RequireMarker", marker])));
    }
    private static bool Execute(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    [HostTest("m06.package.efi restricts both allocator owners and exact transport purpose epochs")]
    private static void Ownership()
    {
        var snapshot = new EfiAllocatorOwnershipSources(EfiOptions.Parse(Flags, false).Profile());
        foreach (string call in new[] { "BootPageAllocatorMutator.AllocateOnePage", "BootPageAllocatorMutator.FreeOnePage" })
        {
            HostCheck.True(EfiProfileTestControls.RemoveOwnedDmaCalls(snapshot.Sources, call, out _, out string reason), reason);
            foreach (var changed in new[] {
                snapshot.ChangeEpoch("ReadPackageSector", 134UL, 133UL),
                snapshot.SwapArguments("ReadPackageSector", "RunBlockReadCore", 5, 6),
                snapshot.RenameMethod("ReadPackageSector"),
                snapshot.SwapArguments("RunPackageReadCore", "StagePackagePayload", 1, 2) })
                HostCheck.True(!EfiProfileTestControls.RemoveOwnedDmaCalls(changed, call, out _, out _));
        }
    }

    [HostTest("m06.package.efi binds finite records failures byte ownership and cleanup to the disk")]
    private static void Records()
    {
        string dir = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/package-transcript-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string serial = Path.Combine(dir, "serial.log"), image = Path.Combine(dir, "disk.raw");
        try
        {
            foreach (string variant in Variants)
            {
                var (lines, disk) = Capture(variant);
                QemuTranscripts.AssertM06PackageReadRecords(lines, variant);
                File.WriteAllText(serial, M06VirtioBlockQueueSetupEfiIntegrationTests.BlockFrame(lines));
                QemuTranscripts.AssertM03AcpiBootstrapTranscript(serial, firstRealExit: true, ownedSurvival: true,
                    pciEnumeration: true, virtioBlockQueueSetup: true, virtioBlockRead: true, liveBlockContract: true,
                    gptPartition: true, fatRead: true, packageRead: true, packageVariant: variant);
                File.WriteAllBytes(image, disk);
                QemuTranscripts.AssertM06PackageReadImage(serial, image, variant);
                Reject(() => QemuTranscripts.AssertM06PackageReadRecords(lines[..^1], variant));
                foreach (string other in Variants.Where(v => v != variant))
                    Reject(() => QemuTranscripts.AssertM06PackageReadRecords(lines, other));
                int last = Array.FindIndex(lines, s => s.StartsWith("M06_PKG 09 ", StringComparison.Ordinal));
                string[] changed = [.. lines];
                changed[last] = changed[last].Replace("0x0000000000000001", "0x0000000000000000", StringComparison.Ordinal);
                Reject(() => QemuTranscripts.AssertM06PackageReadRecords(changed, variant));
                int read = Array.FindIndex(lines, s => s.StartsWith("M06_PKG 8A ", StringComparison.Ordinal));
                string[] words = lines[read].Split(' ');
                ulong lba = Convert.ToUInt64(words[2][2..], 16);
                using (var file = File.Open(image, FileMode.Open, FileAccess.ReadWrite))
                {
                    file.Position = checked((long)lba * 512);
                    int value = file.ReadByte();
                    file.Position--;
                    file.WriteByte((byte)(value ^ 1));
                }
                Reject(() => QemuTranscripts.AssertM06PackageReadImage(serial, image, variant));
            }
        }
        finally { Directory.Delete(dir, true); }
    }

    // Independently assembled bounded host fixture; this is no guest evidence.
    internal static (string[] Lines, byte[] Disk) Capture(string variant)
    {
        var (prefix, disk) = M06FatReadEfiIntegrationTests.Capture();
        const uint os = 9, manifest = 14, package = 15;
        const int fat = 2080, fatSectors = 993, data = fat + 2 * fatSectors;
        int Cluster(uint c) => checked((data + (int)c - 2) * 512);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(disk.AsSpan(offset), value);
        foreach (uint c in new[] { os, manifest, package })
            foreach (int copy in new[] { 0, fatSectors })
                U32((fat + copy) * 512 + (int)c * 4, c == package && variant == "PackageLoop" ? package : 0x0fffffff);
        void Entry(int offset, ReadOnlySpan<byte> alias, uint first, uint bytes)
        {
            disk.AsSpan(offset, 32).Clear();
            alias.CopyTo(disk.AsSpan(offset));
            disk[offset + 11] = 0x20;
            BinaryPrimitives.WriteUInt16LittleEndian(disk.AsSpan(offset + 26), (ushort)first);
            U32(offset + 28, bytes);
        }
        disk.AsSpan(Cluster(os), 512).Clear();
        Entry(Cluster(os), "NATIVE  MAN"u8, manifest, 104);
        Entry(Cluster(os) + 32, "APP     PKG"u8, package, variant == "PackageTruncated" ? 700u : 132u);
        byte[] manifestBytes = Encoding.ASCII.GetBytes(NativeManifestDiskTests.Valid), packageBytes = APPPKGDiskTests.Package();
        if (variant == "ManifestVersion")
            manifestBytes[34] = (byte)'1';
        if (variant == "PackageHeader")
            packageBytes[0] = (byte)'X';
        if (variant == "PackageChecksum")
            packageBytes[^1] ^= 1;
        manifestBytes.CopyTo(disk, Cluster(manifest));
        packageBytes.CopyTo(disk, Cluster(package));
        // FAT-sector contents changed; refresh the ancestor's existing receipt
        // CRCs while preserving its reviewed LBA, purpose and epoch sequence.
        foreach (string row in prefix.Where(s => s.StartsWith("M06_FAT 8", StringComparison.Ordinal)))
        {
            string[] fields = row.Split(' ');
            ulong lba = Convert.ToUInt64(fields[2][2..], 16);
            fields[3] = "0x" + ((ulong)Crc32.Compute(disk.AsSpan((int)lba * 512, 512))).ToString("X16");
            prefix[Array.IndexOf(prefix, row)] = string.Join(' ', fields);
        }
        string[] lines = QemuContracts.M06PackageReadMarkers(variant).ToArray();
        prefix.CopyTo(lines, 0);
        void Set(int stage, params ulong[] values)
        {
            string key = "M06_PKG " + stage.ToString("X2");
            int index = Array.IndexOf(lines, key);
            if (index >= 0)
                lines[index] = key + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
        }
        Set(0, 2048, 131038, fat, data);
        Set(1, 2, 2, 10, os);
        Set(2, os, manifest, package, 10);
        Set(3, 104, 60, 13, 0x67a2f958);
        Set(4, 132, 96, 36, 0x8680772e);
        ulong[] Row(string key) => prefix.Single(s => s.StartsWith(key + " ", StringComparison.Ordinal)).Split(' ').Skip(2)
            .Select(v => Convert.ToUInt64(v[2..], 16)).ToArray();
        Set(5, Row("M06_BLK 5")[0], 36, 32, 0x8ad44af4);
        Set(6, Row("M06_BLK B"));
        Set(7, 16, 149, 17, 1);
        if (variant != "Intact")
        {
            ulong kind = variant == "ManifestVersion" ? 1UL : variant is "PackageTruncated" or "PackageLoop" ? 4UL : 2UL;
            ulong status = variant switch
            {
                "ManifestVersion" => 8,
                "PackageHeader" => 3,
                "PackageChecksum" => 11,
                "PackageTruncated" => 16,
                "PackageLoop" => 14,
                _ => 0
            };
            Set(8, kind, status, variant == "ManifestVersion" ? 15UL : 16UL, variant == "ManifestVersion" ? 13UL : 16UL);
        }
        Set(9, 1, 0, 1, 1);
        ulong[] lbas = [2048, data, fat, fat + fatSectors, data + 8, fat, fat + fatSectors, fat, fat + fatSectors,
            data + os - 2, data + manifest - 2, fat, fat + fatSectors, data + package - 2, fat, fat + fatSectors];
        for (int i = 0; i < (variant == "ManifestVersion" ? 13 : 16); i++)
            Set(0x80 + i, lbas[i], Crc32.Compute(disk.AsSpan((int)lbas[i] * 512, 512)), (ulong)i + 134, 1);
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
        HostCheck.True(rejected, "invalid package profile or evidence must be rejected");
    }
}
