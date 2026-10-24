using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using OS.Std.Hash;
using OS.Storage;
using OS.Tooling;

internal static class WebRootImageFixtureTests
{
    [HostTest("webroot.image deterministic recursive VFAT import preserves payloads", HostLane.Native)]
    private static void DeterministicImport() => WithImage((root, directory, image, source) =>
    {
        string baseHash = FileHash(image);
        var snapshot = WebRootImageFixture.SnapshotPublishedRoot(source);
        string a = Path.Combine(directory, "DiskA.raw"), again = Path.Combine(directory, "DiskA-again.raw"), b = Path.Combine(directory, "DiskB.raw");
        var first = WebRootImageFixture.Apply(root, image, source, snapshot, a);
        var repeated = WebRootImageFixture.Apply(root, image, source, snapshot, again);
        var second = WebRootImageFixture.Apply(root, image, source, snapshot, b, "DiskB");
        HostCheck.Equal(baseHash, FileHash(image));
        HostCheck.True(first.FinalSha256 == repeated.FinalSha256 && first.FinalSha256 != second.FinalSha256 &&
            first.PublishedInventorySha256 == snapshot.Sha256 && second.PublishedInventorySha256 == snapshot.Sha256);
        HostCheck.True(first.PublishedFileCount == snapshot.Files.Length && first.PublishedBytes == snapshot.TotalBytes &&
            first.MaximumPublishedFileBytes == snapshot.MaximumFileBytes && first.WebRootFiles.Length == snapshot.Files.Length + 5);
        foreach (var asset in snapshot.Files)
        {
            var diskFile = first.WebRootFiles.Single(x => x.Path == WebRootImageFixture.GuestRoot + "/" + asset.Path);
            HostCheck.True(diskFile.Bytes == asset.Bytes && diskFile.Sha256 == asset.Sha256 && diskFile.Attributes == 0x21 &&
                diskFile.FatDate == 0x21 && diskFile.FatTime == 0);
        }
        HostCheck.True(first.PreservedFiles.Any(x => x.Path == "/EFI/BOOT/BOOTX64.EFI") &&
            first.PreservedFiles.Any(x => x.Path == "/OS/APP.PKG") && first.PreservedFiles.Any(x => x.Path == "/OS/MANIFEST.JSON"));
        HostCheck.True(first.Sentinel.Path == "/private/sentinel.txt" &&
            !first.WebRootFiles.Any(x => x.Path.Contains("sentinel", StringComparison.Ordinal)));
        byte[] diskA = File.ReadAllBytes(a), diskB = File.ReadAllBytes(b);
        int differences = 0, differenceOffset = -1;
        for (int index = 0; index < diskA.Length; index++)
            if (diskA[index] != diskB[index]) { differences++; differenceOffset = index; }
        var nonce = first.WebRootFiles.Single(x => x.Path.EndsWith("/fixture-nonce.txt", StringComparison.Ordinal));
        HostCheck.True(differences == 1 && differenceOffset == checked((int)nonce.Extents[0].FirstLba * 512 + 11));
        foreach (var file in first.WebRootFiles.Concat(first.PreservedFiles).Append(first.Sentinel))
        {
            byte[] extracted = Extract(diskA, file);
            HostCheck.True(extracted.Length == file.Bytes && Hash(extracted) == file.Sha256);
        }
        WebRootImageFixture.Verify(a, first);
        WebRootImageFixture.Verify(b, second);
        Console.WriteLine($"WEBROOT_IMAGE_RECEIPT publishedFiles={snapshot.Files.Length} publishedBytes={snapshot.TotalBytes} maxFileBytes={snapshot.MaximumFileBytes} imageBytes={diskA.Length}");
    });

    [HostTest("webroot.image empty large and fragmented files have independent exact bytes", HostLane.Native)]
    private static void EdgeFiles() => WithImage((root, directory, image, source) =>
    {
        string output = Path.Combine(directory, "edges.raw");
        var receipt = WebRootImageFixture.Apply(root, image, source, WebRootImageFixture.SnapshotPublishedRoot(source), output);
        byte[] disk = File.ReadAllBytes(output);
        var empty = receipt.WebRootFiles.Single(x => x.Path.EndsWith("/fixture/empty.bin", StringComparison.Ordinal));
        var large = receipt.WebRootFiles.Single(x => x.Path.EndsWith("/fixture/large.bin", StringComparison.Ordinal));
        var fragmented = receipt.WebRootFiles.Single(x => x.Path.EndsWith("/fixture/fragmented.bin", StringComparison.Ordinal));
        HostCheck.True(empty.Bytes == 0 && empty.Extents.Length == 0 && Extract(disk, empty).Length == 0);
        HostCheck.True(large.Bytes == 65553 && fragmented.Bytes == 131109 && fragmented.Extents.Length > 2);
        foreach (var file in new[] { large, fragmented })
        {
            byte[] actual = Extract(disk, file);
            bool exact = true;
            for (int i = 0; i < actual.Length; i++) exact &= actual[i] == unchecked((byte)((i * 73 + 19) ^ (i / 512) ^ (i / 131072)));
            HostCheck.True(exact);
        }
        HostCheck.True(receipt.WebRootFiles.Any(x => x.Path.EndsWith("/fixture/nested/info.txt", StringComparison.Ordinal)));
        // This directory needs several clusters; long names cross both sector
        // and cluster boundaries and must return exactly once after parsing.
        HostCheck.Equal(24, receipt.WebRootFiles.Count(x => x.Path.Contains("/many/", StringComparison.Ordinal)));
    });

    [HostTest("webroot.image controlled fault variants retain positive identity and fail explicitly", HostLane.Native)]
    private static void FaultVariants() => WithImage((root, directory, image, source) =>
    {
        string positive = Path.Combine(directory, "positive.raw");
        var receipt = WebRootImageFixture.Apply(root, image, source, WebRootImageFixture.SnapshotPublishedRoot(source), positive);
        foreach (string variant in new[] { "Missing", "Truncated", "Loop", "MirrorMismatch" })
        {
            string output = Path.Combine(directory, variant + ".raw");
            var fault = WebRootImageFixture.CreateFaultVariant(root, positive, receipt, output, variant);
            HostCheck.True(fault.SourceSha256 == receipt.FinalSha256 && fault.FinalSha256 == FileHash(output) &&
                fault.FinalSha256 != receipt.FinalSha256 && FileHash(positive) == receipt.FinalSha256);
            if (variant == "Missing")
            {
                var actual = WebRootImageFixture.ReadBack(output);
                HostCheck.True(!actual.Files.Any(x => x.Path == fault.TargetPath) && actual.Files.Length ==
                    receipt.WebRootFiles.Length + receipt.PreservedFiles.Length);
            }
            else Reject(() => WebRootImageFixture.ReadBack(output), fault.ExpectedReadback);
        }
        WebRootImageFixture.Verify(positive, receipt);
    });

    [HostTest("webroot.image rejects changed inventory unsafe output and duplicate publication", HostLane.Native)]
    private static void PublicationRejections() => WithImage((root, directory, image, source) =>
    {
        var snapshot = WebRootImageFixture.SnapshotPublishedRoot(source);
        string original = FileHash(image), output = Path.Combine(directory, "rejected.raw");
        File.AppendAllText(Path.Combine(source, "index.html"), "changed");
        Reject(() => WebRootImageFixture.Apply(root, image, source, snapshot, output), "webroot_image_source_inventory_changed");
        HostCheck.True(!File.Exists(output) && FileHash(image) == original);
        snapshot = WebRootImageFixture.SnapshotPublishedRoot(source);
        string outside = Path.Combine(root, "webroot-outside-artifacts.raw");
        Reject(() => WebRootImageFixture.Apply(root, image, source, snapshot, outside), "webroot_image_output_path");
        HostCheck.True(!File.Exists(outside));
        var receipt = WebRootImageFixture.Apply(root, image, source, snapshot, output);
        Reject(() => WebRootImageFixture.Apply(root, image, source, snapshot, output), "webroot_image_output_path");
        HostCheck.True(FileHash(output) == receipt.FinalSha256 && FileHash(image) == original && !Directory.GetFiles(directory, "*.tmp-*").Any());
    });

    [HostTest("webroot.image rejects malformed geometry FAT copies and namespace collision", HostLane.Native)]
    private static void ImageRejections() => WithImage((root, directory, image, source) =>
    {
        var snapshot = WebRootImageFixture.SnapshotPublishedRoot(source);
        byte[] original = File.ReadAllBytes(image);
        int boot = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(original.AsSpan(2 * 512 + 32)) * 512);
        int fat = boot + BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(boot + 14)) * 512;
        int fatBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(boot + 36)) * 512);
        foreach (int offset in new[] { boot + 40, boot + 42, fat + fatBytes + 8 })
        {
            byte[] changed = (byte[])original.Clone(); changed[offset] ^= 1; File.WriteAllBytes(image, changed);
            string output = Path.Combine(directory, "bad-" + offset + ".raw");
            Reject(() => WebRootImageFixture.Apply(root, image, source, snapshot, output));
            HostCheck.True(!File.Exists(output) && FileHash(image) == Hash(changed));
        }
        byte[] differingUsableRange = (byte[])original.Clone();
        int backup = differingUsableRange.Length - 512;
        ulong firstUsable = BinaryPrimitives.ReadUInt64LittleEndian(differingUsableRange.AsSpan(backup + 40));
        BinaryPrimitives.WriteUInt64LittleEndian(differingUsableRange.AsSpan(backup + 40), firstUsable + 1);
        BinaryPrimitives.WriteUInt32LittleEndian(differingUsableRange.AsSpan(backup + 16), 0);
        int headerBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(differingUsableRange.AsSpan(backup + 12)));
        BinaryPrimitives.WriteUInt32LittleEndian(differingUsableRange.AsSpan(backup + 16), Crc32.Compute(differingUsableRange.AsSpan(backup, headerBytes)));
        HostCheck.True(PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(differingUsableRange.AsSpan(backup, 512),
            (ulong)differingUsableRange.Length / 512, BlockReadResult.Success(1, 512), backup: true).Succeeded);
        File.WriteAllBytes(image, differingUsableRange);
        string inconsistent = Path.Combine(directory, "gpt-inconsistent.raw");
        Reject(() => WebRootImageFixture.Apply(root, image, source, snapshot, inconsistent), "webroot_image_gpt_identity");
        HostCheck.True(!File.Exists(inconsistent) && FileHash(image) == Hash(differingUsableRange));
        File.WriteAllBytes(image, original);
        string valid = Path.Combine(directory, "valid.raw");
        WebRootImageFixture.Apply(root, image, source, snapshot, valid);
        string repeated = Path.Combine(directory, "duplicate.raw");
        Reject(() => WebRootImageFixture.Apply(root, valid, source, snapshot, repeated), "webroot_image_root_collision");
        HostCheck.True(!File.Exists(repeated));
    });

    [HostTest("webroot.image source paths depth reserved names and size are bounded", HostLane.Native)]
    private static void SourceRejections() => WithImage((root, directory, image, source) =>
    {
        string deep = source;
        for (int i = 0; i < 9; i++) { deep = Path.Combine(deep, "d"); Directory.CreateDirectory(deep); }
        File.WriteAllText(Path.Combine(deep, "file.txt"), "deep");
        Reject(() => WebRootImageFixture.SnapshotPublishedRoot(source), "webroot_image_path_bounds");
        Directory.Delete(Path.Combine(source, "d"), true);
        string longPath = Path.Combine(source, new string('a', 244));
        File.WriteAllText(longPath, "path");
        Reject(() => WebRootImageFixture.SnapshotPublishedRoot(source), "webroot_image_path_bounds");
        File.Delete(longPath);
        string oversized = Path.Combine(source, "oversized.bin");
        using (var file = File.Create(oversized)) file.SetLength((long)WebRootImageFixture.MaximumImageBytes + 1);
        Reject(() => WebRootImageFixture.SnapshotPublishedRoot(source), "webroot_asset_size_out_of_range");
        File.Delete(oversized);
        File.WriteAllText(Path.Combine(source, "fixture-nonce.txt"), "reserved");
        var snapshot = WebRootImageFixture.SnapshotPublishedRoot(source);
        string output = Path.Combine(directory, "reserved.raw");
        Reject(() => WebRootImageFixture.Apply(root, image, source, snapshot, output), "webroot_image_reserved_fixture_path");
        HostCheck.True(!File.Exists(output));
    });

    private static byte[] Extract(byte[] image, WebRootImageFixture.DiskFile file)
    {
        byte[] bytes = new byte[file.Bytes]; int position = 0;
        foreach (var extent in file.Extents)
        {
            int count = Math.Min(checked((int)extent.Clusters * 512), bytes.Length - position);
            image.AsSpan(checked((int)extent.FirstLba * 512), count).CopyTo(bytes.AsSpan(position)); position += count;
        }
        HostCheck.Equal(bytes.Length, position);
        return bytes;
    }
    private static void Reject(Action action, string? expected = null)
    {
        bool rejected = false;
        try { action(); }
        catch (ToolFailure failure) { rejected = expected is null || failure.Reason == expected; }
        HostCheck.True(rejected, expected ?? "expected explicit image rejection");
    }
    internal static void WithImage(Action<string, string, string, string> action)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts/webroot-image-fixture-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string image = Path.Combine(directory, "base.raw"), efi = Path.Combine(directory, "BOOTX64.EFI"), package = Path.Combine(directory, "APP.PKG");
            File.WriteAllBytes(efi, Enumerable.Range(0, 1537).Select(i => (byte)(i % 251)).ToArray());
            File.WriteAllBytes(package, Enumerable.Range(0, 2305).Select(i => (byte)((i * 17) ^ (i >> 8))).ToArray());
            string source = Path.Combine(directory, "published"); Directory.CreateDirectory(source);
            Write("index.html", "<html>Published webroot</html>"u8.ToArray());
            Write("site.css", "body{color:#123}"u8.ToArray());
            Write("_framework/blazor.webassembly.js", "/* immutable bootstrap */"u8.ToArray());
            Write("_framework/Example.Project.wasm", Enumerable.Range(0, 4097).Select(i => (byte)((i * 7) ^ (i >> 9))).ToArray());
            Write("_content/Example.Library/styles/site.css", "/* nested asset */"u8.ToArray());
            for (int i = 0; i < 24; i++) Write($"many/long-component-name-crossing-vfat-slots-{i:D2}.txt", [(byte)i]);
            var start = HostToolProcess.CreateDotnetToolStartInfo(root, "OS.ImageBuilder");
            foreach (string arg in new[] { "create-raw", "--efi", efi, "--package", package, "--output", image, "--image-size-mib", "64" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            { process.Kill(true); process.WaitForExit(5000); throw new InvalidOperationException("image builder timed out"); }
            string text = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
            HostCheck.True(process.ExitCode == 0 && text.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal), text);
            action(root, directory, image, source);

            void Write(string relative, byte[] bytes)
            {
                string path = Path.Combine(source, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes);
            }
        }
        finally { Directory.Delete(directory, true); }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string FileHash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
}
