using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;
using OS.Std.Hash;
using OS.Storage;
using OS.Tooling;

internal static class PackageImageFixtureTests
{
    [HostTest("package.image_fixture preserves ancestors and applies exact finite variants", HostLane.Native)]
    private static void Variants() => WithFreshImage((path, efi, ancestor) =>
    {
        byte[] before = File.ReadAllBytes(path);
        string originalHash = Hash(before), efiHash = FileHash(efi);
        int boot = checked((int)U64(before, 2 * 512 + 32) * 512);
        int fat = boot + U16(before, boot + 14) * 512, fatBytes = checked((int)U32(before, boot + 36) * 512);
        int data = fat + 2 * fatBytes, info = boot + 512;
        int Cluster(uint cluster) => checked(data + (int)(cluster - 2) * 512);
        uint osCluster = EntryCluster(before, data + 32);
        int os = Cluster(osCluster), empty = os;
        while (before[empty] != 0) empty += 32;

        foreach (string variant in new[] { "Intact", "ManifestVersion", "PackageChecksum", "PackageHeader", "PackageTruncated", "PackageLoop" })
        {
            File.WriteAllBytes(path, before);
            var receipt = PackageImageFixture.Apply(path, efi, variant);
            byte[] after = File.ReadAllBytes(path);
            int manifestData = Cluster(receipt.ManifestCluster), packageData = Cluster(receipt.PackageCluster);
            uint Next(uint cluster) => U32(after, checked(fat + (int)cluster * 4)) & 0x0fffffff;
            HostCheck.True(receipt.Variant == variant && receipt.OriginalSha256 == originalHash &&
                receipt.FinalSha256 == Hash(after) && receipt.FinalSha256 != originalHash && receipt.EfiSha256 == efiHash);
            HostCheck.True(receipt.PartitionFirstLba == ancestor.PartitionFirstLba && receipt.PartitionLastLba == ancestor.PartitionLastLba &&
                receipt.RootCluster == 2 && receipt.RootExtension == ancestor.RootExtension && receipt.OsDirectoryCluster == osCluster);
            HostCheck.True(receipt.ManifestCluster != receipt.PackageCluster &&
                (U32(before, checked(fat + (int)receipt.ManifestCluster * 4)) & 0x0fffffff) == 0 &&
                (U32(before, checked(fat + (int)receipt.PackageCluster * 4)) & 0x0fffffff) == 0);
            HostCheck.True(after.AsSpan(empty, 11).SequenceEqual("NATIVE  MAN"u8) && after[empty + 11] == 0x20 &&
                EntryCluster(after, empty) == receipt.ManifestCluster && U32(after, empty + 28) == 104 &&
                after.AsSpan(empty + 32, 11).SequenceEqual("APP     PKG"u8) && after[empty + 43] == 0x20 &&
                EntryCluster(after, empty + 32) == receipt.PackageCluster && after[empty + 64] == 0);
            uint declaredBytes = variant == "PackageTruncated" ? 700u : 132u;
            HostCheck.True(receipt.ManifestBytes == 104 && receipt.PackageBytes == declaredBytes && receipt.PackageStoredBytes == 132 &&
                U32(after, empty + 60) == declaredBytes && Next(receipt.ManifestCluster) >= 0x0ffffff8 &&
                (variant == "PackageLoop" ? Next(receipt.PackageCluster) == receipt.PackageCluster : Next(receipt.PackageCluster) >= 0x0ffffff8));

            byte[] manifest = Encoding.ASCII.GetBytes(variant == "ManifestVersion" ?
                NativeManifestDiskTests.Valid.Replace("version=0", "version=1", StringComparison.Ordinal) : NativeManifestDiskTests.Valid);
            byte[] package = APPPKGDiskTests.Package();
            if (variant == "PackageChecksum") package[131] ^= 1;
            if (variant == "PackageHeader") package[0] = (byte)'X';
            HostCheck.True(after.AsSpan(manifestData, 104).SequenceEqual(manifest) &&
                after.AsSpan(packageData, 132).SequenceEqual(package) &&
                after.AsSpan(manifestData + 104, 408).IndexOfAnyExcept((byte)0) < 0 &&
                after.AsSpan(packageData + 132, 380).IndexOfAnyExcept((byte)0) < 0);
            HostCheck.True(receipt.ManifestCrc32 == Crc32.Compute(manifest) && receipt.PackageCrc32 == Crc32.Compute(package) &&
                receipt.PayloadCrc32 == Crc32.Compute(package.AsSpan(96, 36)));
            var parsedManifest = NativeManifestDisk.Validate(after.AsSpan(manifestData, 104));
            HostCheck.Equal(variant == "ManifestVersion" ? LiveManifestStatus.InvalidValue : LiveManifestStatus.Success, parsedManifest.Status);
            HostCheck.Equal(variant != "ManifestVersion", NativeManifestParser.Parse(manifest).Succeeded);
            var parsedPackage = APPPKGDisk.Validate(after.AsSpan(packageData, 132), "bin/hello.app"u8.ToArray(), 0x8680772e);
            HostCheck.Equal(variant == "PackageHeader" ? LivePackageStatus.InvalidHeader :
                variant == "PackageChecksum" ? LivePackageStatus.ChecksumMismatch : LivePackageStatus.Success, parsedPackage.Status);
            HostCheck.Equal(variant != "PackageHeader", OsPackageParser.Parse(package).Succeeded);
            var payload = NativeAppPayloadHeader.Parse(package.AsSpan(96, 36));
            HostCheck.True(payload.Succeeded && payload.Header.EntryOffset == 32);
            HostCheck.True(after.AsSpan(fat, fatBytes).SequenceEqual(after.AsSpan(fat + fatBytes, fatBytes)) &&
                after.AsSpan(info, 512).SequenceEqual(after.AsSpan(boot + 7 * 512, 512)) &&
                U32(after, info + 488) == U32(before, info + 488) - 2 && Next(U32(after, info + 492)) == 0);

            // Independently derive every allowed write extent. Root/VFAT, GPT,
            // old OS entries, original manifest and EFI are outside these spans.
            List<(int Start, int Bytes)> writes = [(empty, 64), (manifestData, 512), (packageData, 512),
                (info + 488, 8), (boot + 7 * 512 + 488, 8)];
            foreach (uint cluster in new[] { receipt.ManifestCluster, receipt.PackageCluster })
                foreach (int copy in new[] { 0, fatBytes }) writes.Add((checked(fat + copy + (int)cluster * 4), 4));
            int cursor = 0;
            foreach (var write in writes.OrderBy(write => write.Start))
            {
                HostCheck.True(write.Start >= cursor && before.AsSpan(cursor, write.Start - cursor).SequenceEqual(after.AsSpan(cursor, write.Start - cursor)));
                cursor = write.Start + write.Bytes;
            }
            HostCheck.True(before.Length == after.Length && before.AsSpan(cursor).SequenceEqual(after.AsSpan(cursor)));
            RejectUnchanged(path, efi, variant);
        }
    });

    [HostTest("package.image_fixture rejects identity geometry copies duplicates and full directories before writes", HostLane.Native)]
    private static void Rejections() => WithFreshImage((path, efi, ancestor) =>
    {
        byte[] original = File.ReadAllBytes(path);
        int boot = checked((int)U64(original, 2 * 512 + 32) * 512);
        int fat = boot + U16(original, boot + 14) * 512, fatBytes = checked((int)U32(original, boot + 36) * 512);
        int data = fat + 2 * fatBytes, backupHeader = original.Length - 512;
        uint osCluster = EntryCluster(original, data + 32);
        int os = checked(data + (int)(osCluster - 2) * 512), empty = os;
        while (original[empty] != 0) empty += 32;
        foreach (int offset in new[] { 512 + 16, backupHeader + 16, boot + 13, fat + fatBytes + 8,
            boot + 512 + 488, checked(data + (int)(ancestor.FileFirst - 2) * 512) })
        {
            byte[] changed = (byte[])original.Clone(); changed[offset] ^= 1;
            File.WriteAllBytes(path, changed); RejectUnchanged(path, efi, "Intact");
        }
        byte[] guidMismatch = (byte[])original.Clone(); guidMismatch[backupHeader + 56] ^= 1;
        BinaryPrimitives.WriteUInt32LittleEndian(guidMismatch.AsSpan(backupHeader + 16), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(guidMismatch.AsSpan(backupHeader + 16), Crc32.Compute(guidMismatch.AsSpan(backupHeader, 92)));
        File.WriteAllBytes(path, guidMismatch); RejectUnchanged(path, efi, "Intact");
        byte[] chainedDirectory = (byte[])original.Clone();
        foreach (int copy in new[] { 0, fatBytes })
            BinaryPrimitives.WriteUInt32LittleEndian(chainedDirectory.AsSpan(checked(fat + copy + (int)osCluster * 4)), osCluster);
        File.WriteAllBytes(path, chainedDirectory); RejectUnchanged(path, efi, "Intact");
        byte[] fullDirectory = (byte[])original.Clone();
        for (int entry = empty; entry < os + 512; entry += 32) fullDirectory[entry] = 0xe5;
        File.WriteAllBytes(path, fullDirectory); RejectUnchanged(path, efi, "Intact");
        foreach (string alias in new[] { "APP     PKG", "NATIVE  MAN", "app     pkg" })
        {
            byte[] duplicate = (byte[])original.Clone(); Encoding.ASCII.GetBytes(alias).CopyTo(duplicate, empty); duplicate[empty + 11] = 0x20;
            File.WriteAllBytes(path, duplicate); RejectUnchanged(path, efi, "Intact");
        }
        File.WriteAllBytes(path, original); RejectUnchanged(path, efi, "Unknown");
        byte[] wrongEfi = File.ReadAllBytes(efi); wrongEfi[0] ^= 1; File.WriteAllBytes(efi, wrongEfi);
        RejectUnchanged(path, efi, "Intact");
        wrongEfi[0] ^= 1; File.WriteAllBytes(efi, wrongEfi);
        byte[] withoutFatFixture = (byte[])original.Clone();
        foreach (int copy in new[] { 0, fatBytes }) BinaryPrimitives.WriteUInt32LittleEndian(withoutFatFixture.AsSpan(fat + copy + 8), 0x0fffffff);
        File.WriteAllBytes(path, withoutFatFixture); RejectUnchanged(path, efi, "Intact");
    });

    [HostTest("package.image_fixture independent byte vectors match reviewed finite metadata")]
    private static void ReviewedVectors()
    {
        byte[] package = APPPKGDiskTests.Package();
        byte[] manifest = Encoding.ASCII.GetBytes(NativeManifestDiskTests.Valid);
        HostCheck.True(manifest.Length == 104 && package.Length == 132 && package.AsSpan(96).Length == 36 &&
            Crc32.Compute(package) == 0x8680772e && Crc32.Compute(package.AsSpan(96)) == 0x8ad44af4 &&
            BinaryPrimitives.ReadUInt64LittleEndian(package.AsSpan(112)) == 32 &&
            package.AsSpan(128).SequenceEqual(new byte[] { 0x31, 0xc0, 0xc3, 0x90 }));
        HostCheck.True(M06DiskPackageFixture.ManifestBytes == 104 && M06DiskPackageFixture.PackageBytes == 132 &&
            M06DiskPackageFixture.PayloadBytes == 36 && M06DiskPackageFixture.EntryOffset == 32 &&
            M06DiskPackageFixture.PackageCrc32 == 0x8680772e && M06DiskPackageFixture.PayloadCrc32 == 0x8ad44af4);
    }

    private static void RejectUnchanged(string path, string efi, string variant)
    {
        string before = FileHash(path); bool rejected = false;
        try { PackageImageFixture.Apply(path, efi, variant); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected && FileHash(path) == before);
    }
    private static void WithFreshImage(Action<string, string, FatImageFixtureReceipt> action)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts/package-image-fixture-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "disk.raw"), efi = Path.Combine(directory, "BOOTX64.EFI");
        try
        {
            File.WriteAllBytes(efi, Enumerable.Range(0, 1537).Select(i => (byte)(i % 251)).ToArray());
            var start = HostToolProcess.CreateDotnetToolStartInfo(root, "OS.ImageBuilder");
            foreach (string arg in new[] { "create-raw", "--efi", efi, "--output", path, "--image-size-mib", "64" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            { process.Kill(true); process.WaitForExit(5000); throw new InvalidOperationException("image builder timed out"); }
            string text = output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();
            HostCheck.True(process.ExitCode == 0 && text.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal), text);
            action(path, efi, FatImageFixture.Apply(path, efi));
        }
        finally { Directory.Delete(directory, true); }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string FileHash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static ulong U64(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset));
    private static uint EntryCluster(byte[] bytes, int offset) => ((uint)U16(bytes, offset + 20) << 16) | U16(bytes, offset + 26);
}
