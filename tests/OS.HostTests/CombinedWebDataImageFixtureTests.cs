using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Std.Hash;
using OS.Storage;
using OS.Tooling;
using Image = OS.Tooling.CombinedWebDataImageFixture;
using Data = OS.Tooling.WritableDataImageFixture;
using B = OS.Storage.WritableBlockSession;
using K = OS.Storage.ContinuingFat32Bootstrap;

// Pure host image and fake-media controls; no native/guest/persistence authority.
internal static unsafe class CombinedWebDataImageFixtureTests
{
    [HostTest("combined r13-sized synthetic web boot package and writable seeds repeat deterministically and independently extract from a cold copy", HostLane.Fast)]
    private static void LargeFootprintColdCopy() => WebRootImageFixtureTests.WithImage((root, folder, _, source) =>
    {
        // Clean-checkout size control, not an executable package or Blazor app.
        // The retained actual r13 bytes are a separately held original check.
        HostCheck.True(source.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Directory.Delete(source, true); Directory.CreateDirectory(source);
        void Asset(string relative, int bytes, byte salt)
        {
            string path = Path.Combine(source, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, SizedBytes(bytes, salt));
        }
        Asset("index.html", 357, 1); Asset("site.css", 159, 2);
        Asset("_framework/System.Private.CoreLib.wasm", 2009369, 3); Asset("_framework/dotnet.native.wasm", 1585294, 4);
        for (int i = 0; i < 41; i++) Asset($"_framework/footprint-{i:D2}.wasm", 56764 + (i < 4 ? 1 : 0), (byte)(5 + i));
        var published = WebRootImageFixture.SnapshotPublishedRoot(source);
        HostCheck.True(published.Files.Length == 45 && published.TotalBytes == 5922507 && published.MaximumFileBytes == 2009369);
        string efi = Path.Combine(folder, "sized-efi.bin"), package = Path.Combine(folder, "sized-app.pkg"), basePath = Path.Combine(folder, "sized-base.raw");
        File.WriteAllBytes(efi, SizedBytes(1106944, 0x51)); File.WriteAllBytes(package, SizedBytes(8642088, 0xc3));
        string efiHash = FileHash(efi), packageHash = FileHash(package);
        var start = HostToolProcess.CreateDotnetToolStartInfo(root, "OS.ImageBuilder");
        foreach (string arg in new[] { "create-raw", "--efi", efi, "--package", package, "--output", basePath, "--image-size-mib", "64" }) start.ArgumentList.Add(arg);
        using (var process = System.Diagnostics.Process.Start(start)!)
        {
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(true); process.WaitForExit(5000); throw new InvalidOperationException("sized image builder timed out"); }
            string text = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
            HostCheck.True(process.ExitCode == 0 && text.Contains("TEST_PASS imagebuilder.raw", StringComparison.Ordinal), text);
        }
        string web = Path.Combine(folder, "sized-web.raw"), data = Path.Combine(folder, "sized-data.raw");
        var webReceipt = WebRootImageFixture.Apply(root, basePath, source, published, web); var seed = Data.Create(root, data);
        string first = Path.Combine(folder, "sized-combined-a.raw"), second = Path.Combine(folder, "sized-combined-b.raw");
        var a = Image.Create(root, web, data, first); var b = Image.Create(root, web, data, second);
        HostCheck.True(a.CopyJson().AsSpan().SequenceEqual(b.CopyJson()) && FileHash(first) == FileHash(second) &&
            a.Image.CapacitySectors == 262144 && a.Image.DataFirst == 131072 && a.Image.DataLast == 260062 &&
            a.Image.Data.FirstFatSector == 131104 && a.Image.Data.FirstDataSector == 133090 && a.Image.Data.FreeClusters == 126965);
        foreach (string absent in new[] { "m16-write.bin", "contract.db", "contract.db-journal", "contract.db-wal", "contract.db-shm" }) HostCheck.True(a.Image.Data.Find(absent) is null);
        foreach (string empty in new[] { "LOG.TXT", "UPLOAD.BIN", "SQLITE.DB" }) HostCheck.True(a.Image.Data.Find(empty) is { Bytes: 0 });
        byte[] originalWeb = File.ReadAllBytes(web), combined = File.ReadAllBytes(first);
        HostCheck.True(combined.AsSpan(2048 * 512, (131038 - 2048 + 1) * 512).SequenceEqual(originalWeb.AsSpan(2048 * 512, (131038 - 2048 + 1) * 512)));
        HostCheck.True(a.Image.WebFiles.Single(x => x.Path == "/EFI/BOOT/BOOTX64.EFI").Sha256 == efiHash &&
            a.Image.WebFiles.Single(x => x.Path == "/OS/APP.PKG").Sha256 == packageHash &&
            a.Image.WebFiles.Single(x => x.Path == "/OS/APP.PKG").Bytes == 8642088 &&
            a.Image.WebFiles.Single(x => x.Path == "/private/sentinel.txt").Sha256 == webReceipt.Sentinel.Sha256);
        string coldPath = Path.Combine(folder, "sized-cold-copy.raw"); File.WriteAllBytes(coldPath, combined); combined.AsSpan().Clear();
        Image.Verify(coldPath, a); var cold = Image.Inspect(coldPath);
        HostCheck.True(cold.RawSha256 == a.Image.RawSha256 && cold.WebPartitionSha256 == a.Image.WebPartitionSha256 &&
            cold.DataPartitionSha256 == a.Image.DataPartitionSha256 && cold.WebFiles.Count == 54 &&
            cold.Data.Find("GUARD.TXT")!.CopyBytes().AsSpan().SequenceEqual(seed.Image.Find("GUARD.TXT")!.CopyBytes()));
        foreach (var file in a.Image.WebFiles)
        { var now = cold.WebFiles.Single(x => x.Path == file.Path); HostCheck.True(now.Sha256 == file.Sha256 && now.CopyBytes().AsSpan().SequenceEqual(file.CopyBytes()) && now.Extents.SequenceEqual(file.Extents)); }
        HostCheck.True(FileHash(web) == a.OriginalWebSha256 && FileHash(data) == a.OriginalDataSha256 && FileHash(efi) == efiHash && FileHash(package) == packageHash &&
            !Directory.GetFiles(folder, "*.tmp-*").Any());
        Console.WriteLine($"COMBINED_LARGE_FOOTPRINT syntheticInputs=true executableValidated=false publishedFiles=45 publishedBytes=5922507 serverPackageBytes=8642088 efiBytes=1106944 capacity=262144 rawSha256={a.Image.RawSha256} receiptSha256={Hash(a.CopyJson())} coldSha256={cold.RawSha256}");
    });

    private static byte[] SizedBytes(int length, byte salt)
    { byte[] bytes = new byte[length]; for (int i = 0; i < bytes.Length; i++) bytes[i] = unchecked((byte)(salt ^ i * 59 ^ i >> 8)); return bytes; }

    [HostTest("combined web data two fresh images and canonical receipts are deterministic with original sources unchanged", HostLane.Native)]
    private static void Deterministic() => WithImage((root, folder, web, data, path, receipt) =>
    {
        string second = Path.Combine(folder, "combined-second.raw"); var repeated = Image.Create(root, web, data, second);
        HostCheck.True(receipt.CopyJson().AsSpan().SequenceEqual(repeated.CopyJson()) && FileHash(path) == FileHash(second) &&
            FileHash(web) == receipt.OriginalWebSha256 && FileHash(data) == receipt.OriginalDataSha256);
        HostCheck.True(new FileInfo(path).Length == 134217728 && receipt.Image.CapacitySectors == 262144 &&
            receipt.Image.WebFirst == 2048 && receipt.Image.WebLast == 131038 && receipt.Image.DataFirst == 131072 && receipt.Image.DataLast == 260062);
        using var json = JsonDocument.Parse(receipt.CopyJson());
        HostCheck.True(json.RootElement.GetProperty("schema").GetString() == "CLROS_COMBINED_WEB_DATA_IMAGE_V1" &&
            !System.Text.Encoding.UTF8.GetString(receipt.CopyJson()).Contains(folder, StringComparison.Ordinal));
        Image.Verify(path, receipt);
        Console.WriteLine($"COMBINED_WEB_DATA rawSha256={receipt.Image.RawSha256} receiptSha256={Hash(receipt.CopyJson())} bytes=134217728 webFiles={receipt.Image.WebFiles.Count} dataFiles={receipt.Image.Data.Files.Count}");
    });

    [HostTest("combined actual GPT selects disjoint ESP and data with exact preserved boot package web bytes and two relocated BPB edits", HostLane.Native)]
    private static void Preservation() => WithImage((_, _, webPath, dataPath, path, receipt) =>
    {
        byte[] web = File.ReadAllBytes(webPath), data = File.ReadAllBytes(dataPath), raw = File.ReadAllBytes(path);
        var original = WebRootImageFixture.ReadBack(webPath); var seed = Data.Inspect(data);
        HostCheck.True(raw.AsSpan(2048 * 512, (131038 - 2048 + 1) * 512).SequenceEqual(web.AsSpan(2048 * 512, (131038 - 2048 + 1) * 512)) &&
            raw.AsSpan(1024, 128).SequenceEqual(web.AsSpan(1024, 128)) && new Guid(raw.AsSpan(512 + 56, 16)) == new Guid(web.AsSpan(512 + 56, 16)));
        HostCheck.True(U64(raw, 512 + 32) == 262143 && U64(raw, 512 + 48) == 262110 && U64(raw, raw.Length - 512 + 72) == 262111 &&
            raw.AsSpan(1024, 16384).SequenceEqual(raw.AsSpan(raw.Length - 33 * 512, 16384)) &&
            new Guid(raw.AsSpan(1024, 16)) == new Guid("C12A7328-F81F-11D2-BA4B-00A0C93EC93B") &&
            new Guid(raw.AsSpan(1024 + 128, 16)) == Data.PartitionType);
        var oldBody = data.AsSpan(2048 * 512, (131038 - 2048 + 1) * 512); var body = raw.AsSpan(131072 * 512, oldBody.Length);
        byte[] expectedBody = oldBody.ToArray(); W32(expectedBody, 28, 131072); W32(expectedBody, 3100, 131072);
        HostCheck.True(body.SequenceEqual(expectedBody));
        HostCheck.True(U32(body, 28) == 131072 && U32(body, 3100) == 131072 && !receipt.Image.Data.CheckedGpt &&
            receipt.Image.Data.FirstLba == 131072 && receipt.Image.Data.LastLba == 260062 && receipt.Image.Data.FirstFatSector == 131104 &&
            receipt.Image.Data.FirstDataSector == 133090 && receipt.Image.Data.RootClusters.SequenceEqual(new uint[] { 2, 19, 37 }));
        foreach (var file in original.Files)
        {
            var actual = receipt.Image.WebFiles.Single(x => x.Path == file.Path);
            HostCheck.True(actual.Sha256 == file.Sha256 && actual.Bytes == file.Bytes && actual.Extents.SequenceEqual(file.Extents) && Hash(actual.CopyBytes()) == file.Sha256);
        }
        HostCheck.True(receipt.Image.WebFiles.Any(x => x.Path == "/EFI/BOOT/BOOTX64.EFI") &&
            receipt.Image.WebFiles.Any(x => x.Path.StartsWith("/OS/", StringComparison.Ordinal)) &&
            receipt.Image.WebFiles.Any(x => x.Path == "/app/wwwroot/index.html"));
        foreach (var file in seed.Files)
        {
            var actual = receipt.Image.Data.Find(file.Name)!;
            HostCheck.True(actual.ShortEntryOffset == file.ShortEntryOffset + 129024UL * 512 &&
                actual.Extents.SequenceEqual(file.Extents.Select(x => x with { FirstLba = x.FirstLba + 129024 })) && actual.CopyBytes().AsSpan().SequenceEqual(file.CopyBytes()));
        }
        HostCheck.True(receipt.Image.Data.Find("m16-write.bin") is null &&
            new[] { "LOG.TXT", "UPLOAD.BIN", "SQLITE.DB" }.All(x => receipt.Image.Data.Find(x) is { Bytes: 0 }));
    });

    [HostTest("combined extraction and receipt return immutable owned bytes and independent output substitution rejects", HostLane.Native)]
    private static void CopiedValues() => WithImage((_, folder, _, _, path, receipt) =>
    {
        var file = receipt.Image.WebFiles.Single(x => x.Path == "/app/wwwroot/index.html"); string hash = file.Sha256;
        byte[] content = file.CopyBytes(); content.AsSpan().Fill(0); byte[] json = receipt.CopyJson(); json.AsSpan().Fill(0);
        HostCheck.True(Hash(file.CopyBytes()) == hash && receipt.CopyJson()[0] == (byte)'{');
        byte[] raw = File.ReadAllBytes(path); raw[checked((int)file.Extents[0].FirstLba * 512)] ^= 1;
        string changed = Path.Combine(folder, "changed-web.raw"); File.WriteAllBytes(changed, raw);
        var independent = Image.Inspect(changed); HostCheck.True(independent.RawSha256 != receipt.Image.RawSha256);
        Reject(() => Image.Verify(changed, receipt), "combined_image_receipt_identity");
        HostCheck.True(Hash(file.CopyBytes()) == hash && receipt.Image.RawSha256 == FileHash(path));
    });

    [HostTest("combined GPT rejects corrupt or CRC valid divergent bounds overlaps GUIDs attributes and extra selections", HostLane.Native)]
    private static void GptRejection() => WithImage((_, folder, _, _, path, _) =>
    {
        byte[] raw = File.ReadAllBytes(path);
        Mutant(folder, raw, x => x[512 + 16] ^= 1, "combined_image_gpt_header");
        Mutant(folder, raw, x => { x[x.Length - 512 + 56] ^= 1; HeaderCrc(x, x.Length - 512); }, "combined_image_gpt_header");
        Mutant(folder, raw, x => { W64(x, 1024 + 128 + 32, 131038); ArraysCrc(x); }, "combined_image_gpt_array");
        Mutant(folder, raw, x => { x.AsSpan(1024 + 16, 16).CopyTo(x.AsSpan(1024 + 128 + 16, 16)); ArraysCrc(x); }, "combined_image_gpt_array");
        Mutant(folder, raw, x => { W64(x, 1024 + 128 + 48, 1UL << 60); ArraysCrc(x); }, "combined_image_partitions");
        Mutant(folder, raw, x => { W64(x, 1024 + 128 + 40, 260061); ArraysCrc(x); }, "combined_image_partitions");
        Mutant(folder, raw, x =>
        {
            x.AsSpan(1024 + 128, 128).CopyTo(x.AsSpan(1024 + 256, 128));
            new Guid("11A90E4D-E121-42F5-B33F-000000000099").TryWriteBytes(x.AsSpan(1024 + 256 + 16, 16));
            W64(x, 1024 + 256 + 32, 260063); W64(x, 1024 + 256 + 40, 260064); ArraysCrc(x);
        }, "combined_image_gpt_selection");
    });

    [HostTest("combined independently checks actual relocated data mirrors reserved copies hidden sectors and allocated guard bytes", HostLane.Native)]
    private static void DataRejection() => WithImage((_, folder, _, _, path, receipt) =>
    {
        byte[] raw = File.ReadAllBytes(path);
        Mutant(folder, raw, x => { W32(x, 131072 * 512 + 28, 2048); W32(x, (131072 + 6) * 512 + 28, 2048); }, "writable_image_bpb");
        Mutant(folder, raw, x => x[(131072 + 6) * 512 + 71] ^= 1, "writable_image_boot_backup");
        Mutant(folder, raw, x => x[(131104 + 993) * 512 + 5 * 4] ^= 1, "writable_image_fat_mirror");
        Mutant(folder, raw, x => x[(131072 + 7) * 512 + 488] ^= 1, "writable_image_fsinfo_backup");
        var guard = receipt.Image.Data.Find("GUARD.TXT")!; raw[checked((int)guard.Extents[0].FirstLba * 512)] ^= 1;
        string changed = Path.Combine(folder, "changed-guard.raw"); File.WriteAllBytes(changed, raw);
        HostCheck.True(Image.Inspect(changed).Data.Find("GUARD.TXT")!.Sha256 != guard.Sha256);
        Reject(() => Image.Verify(changed, receipt), "combined_image_receipt_identity");
    });

    [HostTest("combined publisher rejects existing input or output paths and invalid initial data without staging or source changes", HostLane.Native)]
    private static void PublicationSafety() => WithImage((root, folder, web, data, path, receipt) =>
    {
        Reject(() => Image.Create(root, web, data, path), "combined_image_output_path");
        Reject(() => Image.Create(root, web, data, web), "combined_image_output_path");
        Reject(() => Image.Create(root, web, data, data), "combined_image_output_path");
        Reject(() => Image.Create(root, web, data, folder), "combined_image_output_path");
        byte[] wrong = File.ReadAllBytes(data); "OTHER   TXT"u8.CopyTo(wrong.AsSpan(4066 * 512, 11));
        string changed = Path.Combine(folder, "bad-seed.raw"), absent = Path.Combine(folder, "never-published.raw"); File.WriteAllBytes(changed, wrong);
        HostCheck.True(Data.Inspect(wrong).Find("LOG.TXT") is null);
        Reject(() => Image.Create(root, web, changed, absent), "combined_image_data_seed");
        HostCheck.True(!File.Exists(absent) && !Directory.GetFiles(folder, "*.tmp-*").Any() &&
            FileHash(web) == receipt.OriginalWebSha256 && FileHash(data) == receipt.OriginalDataSha256 && FileHash(path) == receipt.Image.RawSha256);
    });

    [HostTest("combined real queued bootstrap selects the BasicData partition beside ESP through the same exclusive writable block owner", HostLane.Native)]
    private static void ActualBootstrap() => WithImage((_, _, _, _, path, receipt) =>
    {
        byte[] raw = File.ReadAllBytes(path); using var device = new WritableBlockSessionTests.Fixture((ulong)raw.Length / 512);
        device.UseMedium(raw); HostCheck.Equal(B.Status.Success, device.Start());
        K.State* state = stackalloc K.State[1]; *state = default; byte* scratch = (byte*)NativeMemory.AlignedAlloc(5 * 4096, 4096);
        try
        {
            K.Admission input = new() { Device = device.State, Epoch = 99, DeviceEpoch = device.Input.Epoch,
                Sector = scratch, Entries = scratch + 512, SectorBudget = K.MaximumReads };
            HostCheck.Equal(K.Status.Pending, K.Start(state, input)); K.Status status = K.Status.Pending;
            for (int turn = 0; turn < 1000 && status == K.Status.Pending; turn++)
            { status = K.Pump(state); if (device.Hardware->Pending) device.Complete(); }
            HostCheck.Equal(K.Status.Ready, status); K.Result result = default; HostCheck.Equal(K.Status.Success, K.CopyResult(state, input.Epoch, &result));
            HostCheck.True(result.EntryIndex == 1 && result.Reads == 35 && result.Volume.Capacity == 262144 &&
                result.Volume.PartitionFirstLba == receipt.Image.DataFirst && result.Volume.PartitionLastLba == receipt.Image.DataLast &&
                result.Volume.FirstFatLba == receipt.Image.Data.FirstFatSector && result.Volume.FirstDataLba == receipt.Image.Data.FirstDataSector &&
                device.State->Submitted == 35 && device.State->Completed == 35 && device.State->SubmittedBytes == 0 && !device.State->Dirty);
        }
        finally { HostCheck.Equal(K.Status.Success, K.Stop(state)); HostCheck.Equal(B.Status.Success, B.Stop(device.State)); NativeMemory.AlignedFree(scratch); }
    });

    internal static void WithImage(Action<string, string, string, string, string, Image.Receipt> action) => WebRootImageFixtureTests.WithImage((root, folder, baseImage, source) =>
    {
        string web = Path.Combine(folder, "web.raw"), data = Path.Combine(folder, "data.raw"), output = Path.Combine(folder, "combined.raw");
        WebRootImageFixture.Apply(root, baseImage, source, WebRootImageFixture.SnapshotPublishedRoot(source), web);
        Data.Create(root, data); var receipt = Image.Create(root, web, data, output); action(root, folder, web, data, output, receipt);
    });
    private static void Mutant(string folder, byte[] original, Action<byte[]> change, string reason)
    { byte[] copy = (byte[])original.Clone(); change(copy); string path = Path.Combine(folder, "mutant.raw"); File.WriteAllBytes(path, copy); Reject(() => Image.Inspect(path), reason); }
    private static void Reject(Action action, string reason)
    { try { action(); throw new InvalidOperationException("expected " + reason); } catch (ToolFailure error) { HostCheck.Equal(reason, error.Reason); } }
    private static void ArraysCrc(byte[] raw)
    {
        raw.AsSpan(1024, 16384).CopyTo(raw.AsSpan(raw.Length - 33 * 512, 16384)); uint crc = Crc32.Compute(raw.AsSpan(1024, 16384));
        W32(raw, 512 + 88, crc); W32(raw, raw.Length - 512 + 88, crc); HeaderCrc(raw, 512); HeaderCrc(raw, raw.Length - 512);
    }
    private static void HeaderCrc(byte[] raw, int offset) { W32(raw, offset + 16, 0); W32(raw, offset + 16, Crc32.Compute(raw.AsSpan(offset, 92))); }
    private static string Hash(ReadOnlySpan<byte> raw) => Convert.ToHexString(SHA256.HashData(raw));
    private static string FileHash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static uint U32(ReadOnlySpan<byte> raw, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(offset, 4));
    private static ulong U64(ReadOnlySpan<byte> raw, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(raw.Slice(offset, 8));
    private static void W32(Span<byte> raw, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(raw.Slice(offset, 4), value);
    private static void W64(Span<byte> raw, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(raw.Slice(offset, 8), value);
}
