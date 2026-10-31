using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Tooling;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using I = OS.Tooling.WebRootDiskInventory;

internal static class WebRootDiskInventoryTests
{
    [HostTest("truncated original held receipt join accepts exact bytes across hash casing and rejects wrong images or receipts", HostLane.Native)]
    private static void TruncatedHeldReceipts() => WithInventory((root, directory, image, receipt, _, _) =>
    {
        string path=Path.Combine(directory,"held-truncated.raw"),metadata=Path.Combine(directory,"held-fault.json"),
            wrongMetadata=Path.Combine(directory,"held-wrong-fault.json");
        var fault=WebRootImageFixture.CreateFaultVariant(root,image,receipt,path,"Truncated");
        var verified=I.VerifiedTruncated.Verify(root,image,receipt,path,fault);
        File.WriteAllText(metadata,JsonSerializer.Serialize(fault));
        File.WriteAllText(wrongMetadata,JsonSerializer.Serialize(fault with {TargetPath=I.Root+"/index.html"}));
        using AuthorityFiles held=new();
        var positive=held.Open(root,image,"positive image","held_positive",WebRootImageFixture.MaximumImageBytes,singleLink:true);
        var actual=held.Open(root,path,"fault image","held_fault",WebRootImageFixture.MaximumImageBytes,singleLink:true);
        var expected=held.Open(root,metadata,"fault receipt","held_receipt",4096,singleLink:true);
        var wrong=held.Open(root,wrongMetadata,"wrong fault receipt","held_wrong_receipt",4096,singleLink:true);
        HostCheck.Equal(verified.Baseline.ImageSha256,positive.Sha256.ToUpperInvariant());
        QemuSmoke.RequireTruncatedReceipts(verified,positive,expected);
        Reject(()=>QemuSmoke.RequireTruncatedReceipts(verified,actual,expected),"disk_truncated_receipt_identity");
        Reject(()=>QemuSmoke.RequireTruncatedReceipts(verified,positive,wrong),"disk_truncated_receipt_identity");
        held.VerifyUnchanged();verified.VerifyUnchanged();
    });

    [HostTest("corrupt stat to HTTP join rejects missing late duplicate wrong path and wrong generation observations")]
    private static void FaultResponseJoin()
    {
        QemuTranscripts.DiskStaticOperation op = new(1, 2, 4, 7, 30, 40, 50, 144, -5);
        QemuTranscripts.DiskStaticFault fault = new(I.VerifiedTruncated.Target, op);
        var joined = QemuTranscripts.InspectDiskStaticFaultResponseValues([fault], 10, 20, 60);
        HostCheck.True(joined.Count == 1 && joined[0] == fault);
        var other = fault with { Operation = op with { Ordinal = 3, Request = 8, BeginRecordIndex = 35, WakeRecordIndex = 45, CompleteRecordIndex = 55 } };
        HostCheck.Equal(2, QemuTranscripts.InspectDiskStaticFaultResponseValues([fault, other], 10, 20, 60).Count);
        foreach (var invalid in new[]
        {
            fault with { Path = I.Root + "/index.html" },
            fault with { Operation = op with { Identity = 0 } },
            fault with { Operation = op with { Ordinal = 0 } },
            fault with { Operation = op with { Request = 0 } },
            fault with { Operation = op with { Number = 2 } },
            fault with { Operation = op with { Value = -2 } },
            fault with { Operation = op with { Value = 0 } },
            fault with { Operation = op with { OutputPrefix = 143 } },
            fault with { Operation = op with { BeginRecordIndex = 10 } },
            fault with { Operation = op with { WakeRecordIndex = 30 } },
            fault with { Operation = op with { CompleteRecordIndex = 40 } },
            fault with { Operation = op with { CompleteRecordIndex = 60 } }
        }) Reject(() => QemuTranscripts.InspectDiskStaticFaultResponseValues([invalid], 10, 0, 60));
        Reject(() => QemuTranscripts.InspectDiskStaticFaultResponseValues([], 10, 20, 60));
        Reject(() => QemuTranscripts.InspectDiskStaticFaultResponseValues([fault, fault], 10, 20, 60));
        Reject(() => QemuTranscripts.InspectDiskStaticFaultResponseValues([fault], 10, 50, 60));
        Reject(() => QemuTranscripts.InspectDiskStaticFaultResponseValues([fault], 10, 20, 20));
    }

    [HostTest("truncated HTTP selector requires complete fresh Full018 and preserves healthy defaults")]
    private static void FaultSelection()
    {
        string[] args = ["-EnableM10018FullProbe", "-BuildEfi", "-LinuxElfPackage", "artifacts/full.pkg",
            "-LinuxThreadingDisassembly", "artifacts/full.dump", "-WebRootFixture", "DiskA", "-WebRootAssets", "artifacts/full-root"];
        var positive = QemuSmokeOptions.Parse(args); _ = QemuProfiles.Create(positive);
        HostCheck.Equal("None", positive.WebRootFault);
        var negative = QemuSmokeOptions.Parse([..args, "-WebRootFault", "Truncated"]); _ = QemuProfiles.Create(negative);
        HostCheck.Equal(positive.TimeoutSeconds, negative.TimeoutSeconds);
        foreach (string[] suffix in new[]
        {
            new[]{"-EnableM10018TimingProbe"}, new[]{"-BuildEfi:false"}, new[]{"-SkipImageBuild"},
            new[]{"-GptImageVariant", "PrimaryHeaderCrcFault"}, new[]{"-PackageImageVariant", "ManifestHashFault"},
            new[]{"-EnableM10018FullProbe:false"}, new[]{"-LinuxDiskRaceVariant", "CloseReuse"}
        }) Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([..args, "-WebRootFault", "Truncated", ..suffix])));
        foreach (string[] suffix in new[] { new[]{"-WebRootFault", "Loop"}, new[]{"-WebRootFault", ""},
            new[]{"-WebRootFault", "Truncated", "-WebRootFault", "None"} })
        {
            bool denied = false; try { _ = QemuSmokeOptions.Parse([..args, ..suffix]); } catch (ArgumentException) { denied = true; }
            HostCheck.True(denied);
        }
    }

    [HostTest("verified truncated control retains distinct actual and positive identities and refuses healthy substitution", HostLane.Native)]
    private static void TruncatedAuthority() => WithInventory((root, directory, image, receipt, raw, actual) =>
    {
        string path = Path.Combine(directory, "typed-truncated.raw");
        var fault = WebRootImageFixture.CreateFaultVariant(root, image, receipt, path, "Truncated");
        var verified = I.VerifiedTruncated.Verify(root, image, receipt, path, fault);
        byte[] changed = File.ReadAllBytes(path);
        ulong fat = verified.Fault.Fat0WordOffset / 512;
        HostCheck.Equal(receipt.FinalSha256, verified.Baseline.ImageSha256);
        HostCheck.Equal(fault.FinalSha256, verified.ActualImageSha256);
        HostCheck.True(verified.Baseline.ImageSha256 != verified.ActualImageSha256);
        HostCheck.Equal(I.MetadataKind.Fat0, I.ClassifyMetadata(verified.Baseline, changed, [fat], verified)[0].Kind);
        Reject(() => I.ClassifyMetadata(verified.Baseline, changed, [fat]), "webroot_disk_inventory_metadata_image");
        Reject(() => I.ClassifyMetadata(verified.Baseline, raw, [fat], verified), "webroot_disk_inventory_truncated_values");
        Reject(() => I.ClassifyMetadata(actual, changed, [fat], verified), "webroot_disk_inventory_truncated_values");
        ulong targetData = verified.Baseline.Find(I.VerifiedTruncated.Target)!.Sectors[0].Lba;
        Reject(() => I.ClassifyMetadata(verified.Baseline, changed, [targetData], verified), "webroot_disk_inventory_metadata_asset");
        Reject(() => I.VerifiedTruncated.Verify(root, image, receipt, path, fault with { Variant = "Loop" }), "webroot_disk_inventory_truncated_input");
        Reject(() => I.VerifiedTruncated.Verify(root, image, receipt, image, fault));
        verified.VerifyUnchanged();
        changed[^100] ^= 1; File.WriteAllBytes(path, changed);
        Reject(verified.VerifyUnchanged, "webroot_image_fault_identity");
    });

    [HostTest("verified truncated context snapshots original receipt records and nested extent arrays", HostLane.Native)]
    private static void TruncatedReceiptSnapshot() => WithInventory((root, directory, image, receipt, _, _) =>
    {
        string path = Path.Combine(directory, "snapshot-truncated.raw");
        var fault = WebRootImageFixture.CreateFaultVariant(root, image, receipt, path, "Truncated");
        var verified = I.VerifiedTruncated.Verify(root, image, receipt, path, fault);
        var baseline = verified.Baseline;
        var target = baseline.Find(I.VerifiedTruncated.Target)!;
        byte[] content = target.CopyBytes();
        var extents = target.Extents.ToArray(); var sectors = target.Sectors.ToArray();
        string plan = JsonSerializer.Serialize(DiskStaticHttpContract.CreateTruncatedAssetPlan(verified));
        int at = Array.FindIndex(receipt.WebRootFiles, f => f.Path == I.VerifiedTruncated.Target);
        HostCheck.True(at >= 0 && receipt.WebRootFiles[at].Extents.Length > 0 &&
            receipt.PreservedFiles.Length > 0 && receipt.PreservedFiles[0].Extents.Length > 0 && receipt.Sentinel.Extents.Length > 0);
        var original = receipt.WebRootFiles[at];
        original.Extents[0] = original.Extents[0] with { FirstCluster = 0, FirstLba = 0 };
        receipt.WebRootFiles[at] = original with { Path = I.Root + "/changed.bin", Bytes = 0, Sha256 = Hash([]) };
        var preserved = receipt.PreservedFiles[0];
        preserved.Extents[0] = preserved.Extents[0] with { Clusters = 0 };
        receipt.PreservedFiles[0] = preserved with { Path = "/changed", Bytes = 0 };
        receipt.Sentinel.Extents[0] = receipt.Sentinel.Extents[0] with { FirstLba = 0 };

        // Only the borrowed receipt was changed; the original image pair is
        // still exact, so rechecking must use the context's private snapshot.
        verified.VerifyUnchanged();
        HostCheck.True(ReferenceEquals(baseline, verified.Baseline) && baseline.ImageSha256 == receipt.FinalSha256 &&
            verified.ActualImageSha256 == fault.FinalSha256 && baseline.Find(I.Root + "/changed.bin") is null &&
            target.CopyBytes().SequenceEqual(content) && target.Extents.SequenceEqual(extents) && target.Sectors.SequenceEqual(sectors));
        HostCheck.Equal(plan, JsonSerializer.Serialize(DiskStaticHttpContract.CreateTruncatedAssetPlan(verified)));
        HostCheck.True(!verified.Fault.GrantsNativeDeviceOrProcessAuthority && !verified.Fault.GrantsGuestReadFailureAuthority);
    });

    [HostTest("truncated full asset plan changes only fixed GET HEAD and never advertises healthy scope", HostLane.Native)]
    private static void TruncatedPlan() => WithInventory((root, directory, image, receipt, _, _) =>
    {
        string path = Path.Combine(directory, "plan-truncated.raw");
        var fault = WebRootImageFixture.CreateFaultVariant(root, image, receipt, path, "Truncated");
        var verified = I.VerifiedTruncated.Verify(root, image, receipt, path, fault);
        var positive = DiskStaticHttpContract.CreateAssetPlan(verified.Baseline);
        var negative = DiskStaticHttpContract.CreateTruncatedAssetPlan(verified);
        HostCheck.True(!positive.Truncated && negative.Truncated && positive.Scope != negative.Scope && positive.Schema != negative.Schema);
        HostCheck.Equal(positive.Requests.Count, negative.Requests.Count);
        HostCheck.Equal(fault.FinalSha256, negative.ImageSha256);
        HostCheck.Equal(receipt.FinalSha256, positive.ImageSha256);
        HostCheck.Equal(positive.PublishedInventorySha256, negative.PublishedInventorySha256);
        int changes = 0;
        for (int i = 0; i < positive.Requests.Count; i++)
        {
            var a = positive.Requests[i]; var b = negative.Requests[i];
            if (a.Target == "/fixture/fragmented.bin")
            {
                changes++;
                HostCheck.True(b.Method is "GET" or "HEAD" && b.Status == 404 && b.Asset is null && b.BodyBytes == 0 &&
                    (b.Method == "HEAD" ? b.ContentLength is null : b.ContentLength == 0) && b.ContentRange is null && b.Headers.Count == 0 && b.BodySha256 == Hash([]));
            }
            else HostCheck.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        }
        HostCheck.Equal(2, changes);
        HostCheck.Equal(positive.ExpectedBodyBytes - WebRootImageFixture.FragmentedFixtureBytes, negative.ExpectedBodyBytes);
        verified.VerifyUnchanged();
    });


    [HostTest("verified truncated HEAD404 permits only absent or zero length with exact scope and complete following responses", HostLane.Native)]
    private static void TruncatedHeadFraming() => WithInventory((root, directory, image, receipt, _, _) =>
    {
        string path = Path.Combine(directory, "head-framing-truncated.raw");
        var fault = WebRootImageFixture.CreateFaultVariant(root, image, receipt, path, "Truncated");
        var verified = I.VerifiedTruncated.Verify(root, image, receipt, path, fault);
        var positive = DiskStaticHttpContract.CreateAssetPlan(verified.Baseline);
        var negative = DiskStaticHttpContract.CreateTruncatedAssetPlan(verified);
        var head = negative.Requests.Single(r => r.Method == "HEAD" && r.Target == "/fixture/fragmented.bin");
        var get = negative.Requests.Single(r => r.Method == "GET" && r.Target == head.Target);
        var next = negative.Requests.First(r => r.Method == "GET" && r.Status == 404 && r.Target != head.Target);
        HostCheck.True(head.ContentLength is null && get.ContentLength == 0 && head.BodyBytes == 0 && get.BodyBytes == 0);
        (string Method,string Target)[] pair = [(head.Method, head.Target), (next.Method, next.Target)];
        byte[] following = Reply(404, "0");
        foreach (string? length in new string?[] { null, "0" })
        {
            byte[] first = Reply(404, length);
            var parsed = Read([.. first, .. following]);
            HostCheck.True(parsed.Count == 2 && parsed.All(r => r.Status == 404 && r.BodyBytes == 0) && parsed[0].HeaderBytes == first.Length);
        }
        foreach (byte[] first in new[]
        {
            Reply(404, "1"), Reply(404, "bad"), Reply(404, "18446744073709551616"),
            Reply(404, extra:"Transfer-Encoding: chunked\r\n"),
            Reply(404, "0", "Transfer-Encoding: chunked\r\n"),
            Reply(404, extra:"Trailer: X-Extra\r\n"),
            Reply(404, extra:"Content-Encoding: gzip\r\n"),
            Reply(404, body:"x"), Reply(404, body:"0\r\n\r\n")
        }) Reject(() => Read([.. first, .. following]));
        Reject(() => Read([.. Reply(404), .. following, .. following]));
        Reject(() => Read([.. Reply(404), .. following], full:false));
        Reject(() => Read(Reply(404), requests:[("HEAD", "/not-the-verified-target")]));
        Reject(() => Read(Reply(200), requests:[(head.Method, head.Target)]));
        Reject(() => Read(Reply(404), requests:[(get.Method, get.Target)]));
        foreach (bool full in new[] { false, true })
        {
            var healthy = positive.Requests.Single(r => r.Method == "HEAD" && r.Target == (full ? head.Target : "/index.html"));
            var healthyLength = healthy.ContentLength ?? throw new InvalidOperationException("healthy HEAD length missing");
            var healthyRequests = new (string Method,string Target)[] { (healthy.Method, healthy.Target) };
            Reject(() => Read(Reply(200), positive, healthyRequests, full));
            HostCheck.True(Read(Reply(200, healthyLength.ToString(System.Globalization.CultureInfo.InvariantCulture)), positive, healthyRequests, full).Single().BodyBytes == 0);
        }
        verified.VerifyUnchanged();

        IReadOnlyList<(int Status,int HeaderBytes,int BodyBytes)> Read(byte[] bytes, DiskStaticHttpContract.AssetPlan? plan = null,
            IReadOnlyList<(string Method,string Target)>? requests = null, bool full = true) =>
            QemuTranscripts.InspectNativeAotDiskStaticHttpResponsesForTests(bytes, plan ?? negative, requests ?? pair, full);
        static byte[] Reply(int status, string? length = null, string extra = "", string body = "") =>
            System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 404 ? " Not Found" : " OK") +
                "\r\nDate: Sat, 31 Oct 2026 00:00:00 GMT\r\nServer: Kestrel\r\n" +
                (length is null ? "" : "Content-Length: " + length + "\r\n") + extra + "\r\n" + body);
    });

    [HostTest("disk inventory joins45 published paths plus generated edges with actual short-slot inode and timestamps", HostLane.Native)]
    private static void CompleteInventory() => WebRootImageFixtureTests.WithImage((root, directory, image, source) =>
    {
        // Portable inventory shape; these generated bytes are not a Blazor
        // publication or a native execution witness. Actual published bytes are
        // a separately held qualification input to this same inventory API.
        for (int n = 0; n < 16; n++)
        {
            byte[] bytes = new byte[n == 0 ? 2009369 : 513 + n];
            for (int at = 0; at < bytes.Length; at++) bytes[at] = (byte)((at * 7) ^ n);
            File.WriteAllBytes(Path.Combine(source, "_framework", "Inventory-" + n.ToString("D2") + ".wasm"), bytes);
        }
        var snapshot = WebRootImageFixture.SnapshotPublishedRoot(source); HostCheck.Equal(45, snapshot.Files.Length);
        string output = Path.Combine(directory, "inventory.raw");
        var receipt = WebRootImageFixture.Apply(root, image, source, snapshot, output);
        byte[] raw = File.ReadAllBytes(output); var actual = I.Inspect(output, raw, receipt);
        HostCheck.True(!actual.GrantsNativeDeviceOrProcessAuthority && actual.ImageSha256 == receipt.FinalSha256);
        HostCheck.Equal(50, actual.Entries.Count(e => !e.IsDirectory));
        foreach (var expected in receipt.WebRootFiles)
        {
            var entry = actual.Find(expected.Path)!;
            HostCheck.True(entry is not null && !entry.IsDirectory && entry.Bytes == expected.Bytes && entry.Sha256 == expected.Sha256);
            HostCheck.Equal(entry!.ShortEntryLba * 16 + entry.ShortEntrySlot + 256, entry.Inode);
            int position = checked((int)entry.ShortEntryLba * 512 + (int)entry.ShortEntrySlot * 32);
            HostCheck.True(raw[position + 11] == entry.Attributes && BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(position + 28)) == entry.Bytes);
            HostCheck.True(entry.ModifiedSeconds == 315532800 && entry.FatDate == 0x21 && entry.FatTime == 0);
            HostCheck.True(entry.AllocatedSectors == (entry.Bytes + 511UL) / 512 && entry.Sectors.Count == (int)entry.AllocatedSectors);
            HostCheck.Equal(entry.Sha256, Hash(entry.CopyBytes()));
        }
        var largest = actual.Find(I.Root + "/_framework/Inventory-00.wasm")!;
        HostCheck.Equal(3925UL, largest.AllocatedSectors);
        HostCheck.True(actual.Find(I.Root) is { IsDirectory: true } && actual.Find(I.Root + "/fixture/nested") is { IsDirectory: true });
        HostCheck.True(actual.Entries.Select(e => e.Inode).Distinct().Count() == actual.Entries.Count);
        HostCheck.True(actual.Find("/private/sentinel.txt") is null && actual.Find("/OS/APP.PKG") is null);
    });

    [HostTest("disk inventory fragmentation and EOF produce exact independent range sectors without content aliases", HostLane.Native)]
    private static void Sectors() => WithInventory((_, _, _, _, _, actual) =>
    {
        var fragmented = actual.Find(I.Root + "/fixture/fragmented.bin")!;
        HostCheck.True(fragmented.Extents.Count > 2 && fragmented.Sectors.Count == 257);
        var range = fragmented.Cover(65535, 4);
        HostCheck.True(range.Count == 2 && range[0].FileOffset == 65024 && range[1].FileOffset == 65536);
        HostCheck.True(range[0].Lba != range[1].Lba && fragmented.Sectors[^1].ValidBytes == 37);
        HostCheck.Equal(0, fragmented.Cover(fragmented.Bytes, 8192).Count);
        HostCheck.Equal(0, fragmented.Cover(0, 0).Count);
        var empty = actual.Find(I.Root + "/fixture/empty.bin")!;
        HostCheck.True(empty.Bytes == 0 && empty.Extents.Count == 0 && empty.Sectors.Count == 0 && empty.CopyBytes().Length == 0);
        HostCheck.Equal(0, empty.Cover(0, 65536).Count);
        Reject(() => fragmented.Cover(0, 65537)); Reject(() => fragmented.Cover(ulong.MaxValue, 1));
        Reject(() => actual.Find(I.Root)!.Cover(0, 1));
    });

    [HostTest("disk inventory canonical lookup rejects short aliases traversal case prefixes and alternate roots", HostLane.Native)]
    private static void Confinement() => WithInventory((_, _, _, _, _, actual) =>
    {
        foreach (string path in new[] { "/app/wwwroot/../fixture-nonce.txt", "/app/wwwroot/./index.html", "/app/wwwroot//index.html",
            "/app/wwwroot/", "/app/WWWROOT/index.html", "/app/wwwroot-other/index.html", "app/wwwroot/index.html", "/app/wwwroot\\index.html",
            "/app/wwwroot/%2e%2e/private/sentinel.txt", "/app/wwwroot/index.html.", "/app/wwwroot/index.html ", "/private/sentinel.txt" })
            HostCheck.True(actual.Find(path) is null);
        foreach (var entry in actual.Entries.Where(e => !e.IsDirectory && e.Path.Contains("/many/", StringComparison.Ordinal)))
        {
            HostCheck.True(actual.Find(entry.Path) == entry);
            string alias = entry.Path[..(entry.Path.LastIndexOf('/') + 1)] + entry.ShortAlias;
            HostCheck.True(alias != entry.Path && actual.Find(alias) is null);
        }
    });

    [HostTest("disk inventory all-zero FAT time is unspecified while nonzero Gregorian corruption rejects", HostLane.Native)]
    private static void Timestamps() => WithInventory((_, directory, image, receipt, raw, actual) =>
    {
        var file = actual.Find(I.Root + "/index.html")!;
        int position = checked((int)file.ShortEntryLba * 512 + (int)file.ShortEntrySlot * 32);
        byte[] zero = raw.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(zero.AsSpan(position + 24), 0); BinaryPrimitives.WriteUInt16LittleEndian(zero.AsSpan(position + 22), 0);
        string candidate = Path.Combine(directory, "zero-time.raw"); File.WriteAllBytes(candidate, zero);
        var zeroReceipt = Rebase(receipt, candidate);
        HostCheck.Equal(0L, I.Inspect(candidate, zero, zeroReceipt).Find(file.Path)!.ModifiedSeconds);
        foreach (var (date, time) in new (ushort, ushort)[] { (1, 0), (0x21, 31), (0x21, 0xf800), (0x21, 0x7e0), (0, 1) })
        {
            byte[] invalid = raw.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(invalid.AsSpan(position + 24), date);
            BinaryPrimitives.WriteUInt16LittleEndian(invalid.AsSpan(position + 22), time);
            string path = Path.Combine(directory, "invalid-time-" + date + "-" + time + ".raw"); File.WriteAllBytes(path, invalid);
            var mutated = Rebase(receipt, path); Reject(() => I.Inspect(path, invalid, mutated), "webroot_disk_inventory_timestamp");
        }
        HostCheck.Equal(receipt.FinalSha256, Hash(File.ReadAllBytes(image)));
    });

    [HostTest("disk inventory rejects image receipt FAT chain VFAT and metadata substitutions", HostLane.Native)]
    private static void Corruption() => WithInventory((root, directory, image, receipt, raw, actual) =>
    {
        byte[] changed = raw.ToArray(); changed[^100] ^= 1; Reject(() => I.Inspect(image, changed, receipt), "webroot_disk_inventory_identity");
        Reject(() => I.Inspect(image, raw, receipt with { PublishedFileCount = receipt.PublishedFileCount + 1 }), "webroot_disk_inventory_published_shape");
        Reject(() => I.Inspect(image, raw, receipt with { PartitionFirstLba = receipt.PartitionFirstLba + 1 }), "webroot_disk_inventory_identity");
        var forged = receipt.WebRootFiles.ToArray(); forged[0] = forged[0] with { Sha256 = new string('0', 64) };
        Reject(() => I.Inspect(image, raw, receipt with { WebRootFiles = forged }), "webroot_disk_inventory_receipt");
        foreach (string variant in new[] { "Missing", "Truncated", "Loop", "MirrorMismatch" })
        {
            string path = Path.Combine(directory, variant + ".raw"); WebRootImageFixture.CreateFaultVariant(root, image, receipt, path, variant);
            Reject(() => I.Inspect(path, File.ReadAllBytes(path), receipt));
        }
        var longEntry = actual.Entries.First(e => !e.IsDirectory && e.Path.Contains("/many/", StringComparison.Ordinal));
        int slot = checked((int)longEntry.ShortEntryLba * 512 + (int)longEntry.ShortEntrySlot * 32);
        byte[] vfat = raw.ToArray(); vfat[slot] ^= 1; string malformed = Path.Combine(directory, "bad-alias-checksum.raw"); File.WriteAllBytes(malformed, vfat);
        Reject(() => I.Inspect(malformed, vfat, receipt));
    });

    [HostTest("disk fault verifier authenticates fixed negative images without admitting healthy inventories", HostLane.Native)]
    private static void FaultReceipts() => WithInventory((root, directory, image, receipt, raw, actual) =>
    {
        var target = actual.Find(I.Root + "/fixture/fragmented.bin")!;
        foreach (string variant in new[] { "Missing", "Truncated", "Loop", "MirrorMismatch" })
        {
            string path = Path.Combine(directory, "verified-" + variant + ".raw");
            var fault = WebRootImageFixture.CreateFaultVariant(root, image, receipt, path, variant);
            var inspected = WebRootImageFixture.VerifyFaultVariant(root, image, receipt, path, fault);
            HostCheck.True(inspected.Receipt == fault && inspected.PartitionFirstLba == receipt.PartitionFirstLba &&
                inspected.PartitionLastLba == receipt.PartitionLastLba && inspected.TargetBytes == target.Bytes &&
                inspected.OriginalTargetSha256 == target.Sha256 && inspected.TargetFirstCluster == target.Extents[0].FirstCluster &&
                inspected.Fat0Before == inspected.Fat1Before && inspected.ChangedBytes > 0 &&
                !inspected.GrantsNativeDeviceOrProcessAuthority && !inspected.GrantsGuestReadFailureAuthority);
            HostCheck.Equal(fault.FinalSha256, Hash(File.ReadAllBytes(path)));
            if (variant == "Missing")
                HostCheck.True(inspected.Fat0After == inspected.Fat0Before && inspected.Fat1After == inspected.Fat1Before);
            else
            {
                Reject(() => WebRootImageFixture.ReadBack(path), fault.ExpectedReadback);
                HostCheck.True(variant == "MirrorMismatch" ? inspected.Fat0After != inspected.Fat1After :
                    inspected.Fat0After == inspected.Fat1After);
            }
            Reject(() => I.Inspect(path, File.ReadAllBytes(path), receipt));
        }
        HostCheck.Equal(receipt.FinalSha256, Hash(raw));
        WebRootImageFixture.Verify(image, receipt);
    });

    [HostTest("disk fault verifier rejects forged receipts and every extra protected image mutation even with updated hashes", HostLane.Native)]
    private static void FaultReceiptRejections() => WithInventory((root, directory, image, receipt, raw, actual) =>
    {
        string path = Path.Combine(directory, "truncated.raw");
        var fault = WebRootImageFixture.CreateFaultVariant(root, image, receipt, path, "Truncated");
        var verified = WebRootImageFixture.VerifyFaultVariant(root, image, receipt, path, fault);
        Reject(() => WebRootImageFixture.VerifyFaultVariant(root, image, receipt, path, fault with { Variant = "Unknown" }), "webroot_image_fault_variant");
        Reject(() => WebRootImageFixture.VerifyFaultVariant(root, image, receipt, path, fault with { TargetPath = I.Root + "/index.html" }), "webroot_image_fault_receipt");
        Reject(() => WebRootImageFixture.VerifyFaultVariant(root, image, receipt, path, fault with { ExpectedReadback = "webroot_image_chain_loop_or_crosslink" }), "webroot_image_fault_receipt");
        Reject(() => WebRootImageFixture.VerifyFaultVariant(root, image, receipt, path, fault with { SourceSha256 = new string('0', 64) }), "webroot_image_fault_receipt");
        Reject(() => WebRootImageFixture.VerifyFaultVariant(root, image, receipt, path, fault with { FinalSha256 = new string('0', 64) }), "webroot_image_fault_identity");
        Reject(() => WebRootImageFixture.VerifyFaultVariant(root, image, receipt with { PublishedFileCount = receipt.PublishedFileCount + 1 }, path, fault), "webroot_disk_inventory_published_shape");
        Reject(() => WebRootImageFixture.VerifyFaultVariant(root, image, null!, path, fault), "webroot_image_fault_input");
        Reject(() => WebRootImageFixture.VerifyFaultVariant(root, image, receipt, path, null!), "webroot_image_fault_input");
        Reject(() => WebRootImageFixture.VerifyFaultVariant(root, image, receipt, image, fault));

        byte[] truncated = File.ReadAllBytes(path);
        var target = actual.Find(fault.TargetPath)!;
        var efi = receipt.PreservedFiles.Single(f => f.Path == "/EFI/BOOT/BOOTX64.EFI");
        var package = receipt.PreservedFiles.Single(f => f.Path == "/OS/APP.PKG");
        int[] protectedOffsets = [512 + 16, checked((int)efi.Extents[0].FirstLba * 512),
            checked((int)package.Extents[0].FirstLba * 512), checked((int)target.Extents[0].FirstLba * 512),
            checked((int)verified.Fat0WordOffset + 4), checked((int)verified.Fat1WordOffset + 3)];
        for (int i = 0; i < protectedOffsets.Length; i++)
        {
            byte[] changed = truncated.ToArray(); changed[protectedOffsets[i]] ^= i == protectedOffsets.Length - 1 ? (byte)0x80 : (byte)1;
            string extra = Path.Combine(directory, "extra-mutation-" + i + ".raw"); File.WriteAllBytes(extra, changed);
            // Rehashing the receipt cannot authorize an extra EFI/package,
            // GPT, target-data, unrelated FAT or reserved-bit mutation.
            Reject(() => WebRootImageFixture.VerifyFaultVariant(root, image, receipt, extra,
                fault with { FinalSha256 = Hash(changed) }), "webroot_image_fault_mutation_scope");
        }
        HostCheck.Equal(receipt.FinalSha256, Hash(raw));
        WebRootImageFixture.Verify(image, receipt);
    });

    [HostTest("actual readonly FS observes truncated fixture chain without publishing an open result or returning stale file bytes", HostLane.Native)]
    private static unsafe void FaultFileSystemFailure() => WithInventory((root, directory, image, receipt, raw, actual) =>
    {
        const string targetPath = I.Root + "/fixture/fragmented.bin";
        var target = actual.Find(targetPath)!;
        using (var healthy = MountImage(raw, receipt))
        {
            var file = healthy.Open(targetPath); byte[] bytes = new byte[512];
            healthy.Read(file.File, 0, 512, bytes, 512);
            HostCheck.True(bytes.AsSpan().SequenceEqual(target.CopyBytes().AsSpan(0, 512)));
            HostCheck.Equal(Fs.Status.Success, Fs.Close(healthy.Mount, file.File));
        }
        string path = Path.Combine(directory, "fs-truncated.raw");
        var fault = WebRootImageFixture.CreateFaultVariant(root, image, receipt, path, "Truncated");
        var verified = WebRootImageFixture.VerifyFaultVariant(root, image, receipt, path, fault);
        using var f = MountImage(File.ReadAllBytes(path), receipt);
        Fs.OperationToken token = default;
        HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(f.Mount, System.Text.Encoding.ASCII.GetBytes(targetPath), &token));
        HostCheck.Equal(Fs.Status.Truncated, f.Drive(token));
        Fs.OpenResult result = new() { Info = new() { Length = 73 } };
        ulong submitted = f.Device->Submitted, completed = f.Device->Completed, copied = f.Device->Copied;
        HostCheck.Equal(Fs.Status.Truncated, Fs.CopyOpen(f.Mount, token, &result));
        HostCheck.True(result.Info.Length == 73 && !result.File.IsInitialized && submitted > 0 &&
            f.Device->Submitted == submitted && f.Device->Completed == completed && f.Device->Copied == copied &&
            f.Disk.Reads[checked((int)(verified.Fat0WordOffset / 512))] > 0 &&
            f.Disk.Reads[checked((int)(verified.Fat1WordOffset / 512))] > 0);
        HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(f.Mount, token));
        var other = f.Open(I.Root + "/index.html");
        byte[] wanted = actual.Find(I.Root + "/index.html")!.CopyBytes(), output = new byte[wanted.Length];
        f.Read(other.File, 0, (uint)wanted.Length, output, (uint)wanted.Length);
        HostCheck.True(output.SequenceEqual(wanted));
        HostCheck.Equal(Fs.Status.Success, Fs.Close(f.Mount, other.File));
        Console.WriteLine($"WEBROOT_FAULT_FS variant=Truncated result=Truncated submitted={submitted} completed={completed} copied={copied} syntheticDevice=true guest=false");
    });

    [HostTest("actual managed descriptor stat and lstat report truncated EIO without output fd or stale completion and recover healthy metadata", HostLane.Native)]
    private static unsafe void TruncatedDescriptorErrors() => WithInventory((root, directory, image, receipt, rawImage, actual) =>
    {
        string path = Path.Combine(directory, "descriptor-truncated.raw");
        var fault = WebRootImageFixture.CreateFaultVariant(root, image, receipt, path, "Truncated");
        var verified = I.VerifiedTruncated.Verify(root, image, receipt, path, fault);
        using var owner = new LinuxDiskDescriptorServicesTests.Owner(storage: MountImage(File.ReadAllBytes(path), receipt));
        const ulong pathAddress = LinuxThreadServicesTests.UserBase + 0x1200, output = LinuxThreadServicesTests.UserBase + 0x2000;
        var old = new List<(Fds.DiskToken Token, OS.Kernel.Compatibility.LinuxX64.LinuxSyscallFrame Call)>();
        foreach (ulong number in new[] { 4UL, 6UL })
        {
            owner.PutPath(I.VerifiedTruncated.Target);
            new Span<byte>(owner.Thread.At(output), 144).Fill(0xa5);
            uint writes = owner.Thread.Writes;
            var call = owner.Call(number, pathAddress, output);
            var begun = owner.Begin(call);
            HostCheck.Equal(Fds.DiskProgress.Pending, begun.Progress);
            var ready = owner.Ready(begun.Token);
            HostCheck.True(ready.Progress == Fds.DiskProgress.Ready && ready.Failure == Fs.Status.Truncated);
            var result = Fds.CompleteDisk(owner.Files.State, owner.ThreadOwner, owner.Worker, begun.Token, call);
            HostCheck.True(result.Progress == Fds.DiskProgress.Immediate && result.Value == -5 && result.Failure == Fs.Status.Truncated);
            HostCheck.True(owner.Thread.Writes == writes && new ReadOnlySpan<byte>(owner.Thread.At(output), 144).IndexOfAnyExcept((byte)0xa5) < 0 &&
                !Fds.Capture(owner.Files.State, 3, out _));
            old.Add((begun.Token, call));
        }
        HostCheck.True(owner.Storage.Disk.Reads[checked((int)(verified.Fault.Fat0WordOffset / 512))] > 0 &&
            owner.Storage.Disk.Reads[checked((int)(verified.Fault.Fat1WordOffset / 512))] > 0);
        foreach (var sector in actual.Find(I.VerifiedTruncated.Target)!.Sectors)
            HostCheck.Equal(0U, owner.Storage.Disk.Reads[checked((int)sector.Lba)]);

        // A new real healthy operation is live while both old result tokens
        // are rejected. No guessed descriptor/memory generations are needed.
        owner.PutPath(I.Root + "/index.html");
        var healthyCall = owner.Call(4, pathAddress, output);
        var healthy = owner.Begin(healthyCall);
        HostCheck.Equal(Fds.DiskProgress.Pending, healthy.Progress);
        uint before = owner.Thread.Writes;
        ulong submitted = owner.Storage.Device->Submitted, copied = owner.Storage.Device->Copied;
        foreach (var stale in old)
        {
            HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PollDisk(owner.Files.State, owner.ThreadOwner, stale.Token).Progress);
            HostCheck.Equal(Fds.DiskProgress.Rejected,
                Fds.CompleteDisk(owner.Files.State, owner.ThreadOwner, owner.Worker, stale.Token, stale.Call).Progress);
        }
        HostCheck.True(owner.Thread.Writes == before && owner.Storage.Device->Submitted == submitted && owner.Storage.Device->Copied == copied &&
            new ReadOnlySpan<byte>(owner.Thread.At(output), 144).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(Fds.DiskProgress.Ready, owner.Ready(healthy.Token).Progress);
        var recovered = Fds.CompleteDisk(owner.Files.State, owner.ThreadOwner, owner.Worker, healthy.Token, healthyCall);
        HostCheck.True(recovered.Progress == Fds.DiskProgress.Immediate && recovered.Value == 0 && recovered.Failure == Fs.Status.Success &&
            owner.Thread.Writes == before + 1 && !Fds.Capture(owner.Files.State, 3, out _));
        byte[] wire = new ReadOnlySpan<byte>(owner.Thread.At(output), 144).ToArray();
        var expected = actual.Find(I.Root + "/index.html")!;
        HostCheck.Equal(expected.Inode, BinaryPrimitives.ReadUInt64LittleEndian(wire.AsSpan(8)));
        HostCheck.Equal((ulong)expected.Bytes, BinaryPrimitives.ReadUInt64LittleEndian(wire.AsSpan(48)));
        HostCheck.Equal(0x8124U, BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(24)));
        HostCheck.Equal(0L, owner.Run(owner.Call(6, pathAddress, output)));
        HostCheck.True(owner.Thread.Writes == before + 2 && new ReadOnlySpan<byte>(owner.Thread.At(output), 144).SequenceEqual(wire) &&
            !Fds.Capture(owner.Files.State, 3, out _));
        verified.VerifyUnchanged();
    });

    private static unsafe ReadOnlyWebRootFileSystemTests.Fixture MountImage(byte[] raw, WebRootImageFixture.Receipt receipt)
    {
        uint capacity = checked((uint)(raw.Length / 512));
        // Match the unchanged sparse test-device constructor exactly:
        // Capacity = partition +32reserved +2*FATsectors +clusters +34tail.
        uint overhead = checked((uint)ReadOnlyWebRootFileSystemTests.Disk.Partition + 32 + 34), clusters = capacity - overhead;
        while (clusters >= 65525 && overhead + clusters + 2 * ((clusters + 129) / 128) != capacity) clusters--;
        HostCheck.True(clusters >= 65525);
        var f = new ReadOnlyWebRootFileSystemTests.Fixture(clusters: clusters);
        try
        {
            HostCheck.Equal((ulong)capacity, f.Disk.Capacity);
            f.Disk.Sectors.Clear();
            for (int at = 0; at < raw.Length; at += 512)
            {
                var sector = raw.AsSpan(at, 512);
                if (sector.IndexOfAnyExcept((byte)0) >= 0) f.Disk.Sectors.Add((ulong)at / 512, sector.ToArray());
            }
            var admission = f.Input;
            admission.PartitionFirst = receipt.PartitionFirstLba; admission.PartitionLast = receipt.PartitionLastLba;
            Fs.Status status = Fs.Start(f.Mount, admission);
            for (int i = 0; i < 10000 && status == Fs.Status.Pending; i++) { f.Tick(); status = Fs.PumpMount(f.Mount); }
            HostCheck.Equal(Fs.Status.Ready, status);
            return f;
        }
        catch { f.Dispose(); throw; }
    }

    [HostTest("disk inventory owns immutable path extent sector and file-byte snapshots after borrowed inputs change", HostLane.Native)]
    private static void Immutable() => WithInventory((_, _, _, receipt, raw, actual) =>
    {
        var file = actual.Find(I.Root + "/fixture/fragmented.bin")!;
        byte[] saved = file.CopyBytes(), writable = file.CopyBytes(); string hash = file.Sha256; var extents = file.Extents.ToArray();
        Array.Clear(writable); Array.Clear(raw); Array.Clear(receipt.WebRootFiles); Array.Clear(receipt.PreservedFiles);
        HostCheck.True(file.CopyBytes().SequenceEqual(saved) && file.Sha256 == hash && file.Extents.SequenceEqual(extents));
        bool entries = false, sectors = false, ranges = false;
        try { ((IList<I.Entry>)actual.Entries)[0] = null!; } catch (NotSupportedException) { entries = true; }
        try { ((IList<I.Sector>)file.Sectors)[0] = default; } catch (NotSupportedException) { sectors = true; }
        try { ((IList<I.Extent>)file.Extents)[0] = default; } catch (NotSupportedException) { ranges = true; }
        HostCheck.True(entries && sectors && ranges && actual.Find(file.Path) == file);
    });

    [HostTest("disk metadata classifies exact navigation nested directory and verified FAT sectors without expanding served paths", HostLane.Native)]
    private static void MetadataKinds() => WithInventory((_, _, _, _, raw, actual) =>
    {
        int bpb = checked((int)actual.PartitionFirstLba * 512);
        ulong fat0 = actual.PartitionFirstLba + BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(bpb + 14));
        uint fatSectors = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(bpb + 36));
        ulong fat1 = fat0 + fatSectors, data = fat1 + fatSectors;
        uint root = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(bpb + 44));
        var rootSector = actual.DirectorySectors.Single(s => s.Path == "/");
        var appSector = actual.DirectorySectors.Single(s => s.Path == "/app");
        HostCheck.Equal(data + root - 2, rootSector.Lba);
        HostCheck.Equal(actual.Find(I.Root)!.ShortEntryLba, appSector.Lba);
        ulong nested = actual.Find(I.Root + "/fixture/nested")!.Sectors[0].Lba;
        ulong[] lbas = [rootSector.Lba, appSector.Lba, nested, fat0, fat0 + fatSectors - 1, fat1, fat1 + fatSectors - 1, rootSector.Lba];
        var result = I.ClassifyMetadata(actual, raw, lbas);
        HostCheck.True(result.Select(s => s.Lba).SequenceEqual(lbas));
        HostCheck.True(result.Take(3).All(s => s.Kind == I.MetadataKind.Directory) &&
            result[0].DirectoryPath == "/" && result[1].DirectoryPath == "/app" && result[2].DirectoryPath == I.Root + "/fixture/nested");
        HostCheck.True(result[3].Kind == I.MetadataKind.Fat0 && result[4].Kind == I.MetadataKind.Fat0 &&
            result[5].Kind == I.MetadataKind.Fat1 && result[6].Kind == I.MetadataKind.Fat1 &&
            result.Skip(3).Take(4).All(s => s.DirectoryPath == ""));
        HostCheck.Equal(result[0], result[^1]);
        HostCheck.True(actual.Find("/") is null && actual.Find("/app") is null && actual.Entries.All(e => e.Path == I.Root || e.Path.StartsWith(I.Root + "/", StringComparison.Ordinal)));
        HostCheck.Equal(2 + actual.Entries.Where(e => e.IsDirectory).Sum(e => e.Sectors.Count), actual.DirectorySectors.Count);
        HostCheck.True(actual.DirectorySectors.Select(s => s.Lba).Distinct().Count() == actual.DirectorySectors.Count);
    });

    [HostTest("disk metadata rejects reserved preserved sentinel free data and parseable directory-looking asset tail sectors", HostLane.Native)]
    private static void MetadataExclusions() => WebRootImageFixtureTests.WithImage((root, directory, image, source) =>
    {
        byte[] directoryLooking = new byte[513];
        System.Text.Encoding.ASCII.GetBytes("DIR        ").CopyTo(directoryLooking, 0);
        directoryLooking[11] = 0x10; BinaryPrimitives.WriteUInt16LittleEndian(directoryLooking.AsSpan(26), 2);
        File.WriteAllBytes(Path.Combine(source, "directory-looking.bin"), directoryLooking);
        string output = Path.Combine(directory, "metadata-exclusions.raw");
        var receipt = WebRootImageFixture.Apply(root, image, source, WebRootImageFixture.SnapshotPublishedRoot(source), output);
        byte[] raw = File.ReadAllBytes(output); var actual = I.Inspect(output, raw, receipt);
        var asset = actual.Find(I.Root + "/directory-looking.bin")!;
        OS.Storage.LiveVfatState vfat = default;
        var parsed = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate.Consume(
            raw.AsSpan(checked((int)asset.Sectors[0].Lba * 512), 32), ref vfat, new ushort[260], out var fake);
        HostCheck.True(parsed == OS.Storage.LiveDirectoryStatus.Entry && (fake.Attributes & 0x10) != 0);
        HostCheck.Equal(1U, asset.Sectors[^1].ValidBytes);
        foreach (ulong lba in new[] { asset.Sectors[0].Lba, asset.Sectors[^1].Lba,
            actual.Find(I.Root + "/fixture/fragmented.bin")!.Sectors[^1].Lba })
            Reject(() => I.ClassifyMetadata(actual, raw, [lba]), "webroot_disk_inventory_metadata_asset");
        foreach (var file in receipt.PreservedFiles.Append(receipt.Sentinel))
        {
            ulong lba = file.Extents[0].FirstLba;
            Reject(() => I.ClassifyMetadata(actual, raw, [lba]), "webroot_disk_inventory_metadata_sector");
        }
        int bpb = checked((int)actual.PartitionFirstLba * 512);
        uint reserved = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(bpb + 14));
        ulong firstFat = actual.PartitionFirstLba + reserved;
        uint fatSectors = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(bpb + 36));
        ulong firstData = firstFat + 2 * (ulong)fatSectors;
        uint free = 2;
        while (BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(checked((int)firstFat * 512 + (int)free * 4))) != 0) free++;
        ulong freeLba = firstData + free - 2;
        foreach (ulong lba in new[] { 0UL, 1UL, 2UL, actual.PartitionFirstLba, actual.PartitionFirstLba + 1,
            actual.PartitionFirstLba + 6, actual.PartitionFirstLba + 7, actual.PartitionFirstLba + reserved - 1,
            freeLba, (ulong)raw.Length / 512, ulong.MaxValue })
            Reject(() => I.ClassifyMetadata(actual, raw, [lba]), "webroot_disk_inventory_metadata_sector");
        HostCheck.True(actual.DirectorySectors.All(s => s.Lba != freeLba && !asset.Sectors.Any(a => a.Lba == s.Lba)));
    });

    [HostTest("disk metadata requires complete held image identity and bounded inputs before classifying residual copies", HostLane.Native)]
    private static void MetadataIdentityAndBounds() => WithInventory((_, _, _, _, raw, actual) =>
    {
        ulong lba = actual.DirectorySectors[0].Lba;
        byte[] substitution = raw.ToArray(); substitution[^100] ^= 1;
        Reject(() => I.ClassifyMetadata(actual, substitution, [lba]), "webroot_disk_inventory_metadata_image");
        Reject(() => I.ClassifyMetadata(actual, substitution, []), "webroot_disk_inventory_metadata_image");
        var nonce = actual.Find(I.Root + "/fixture-nonce.txt")!;
        substitution = raw.ToArray(); substitution[checked((int)nonce.Sectors[0].Lba * 512)] ^= 1;
        Reject(() => I.ClassifyMetadata(actual, substitution, [lba]), "webroot_disk_inventory_metadata_image");
        Reject(() => I.ClassifyMetadata(actual, raw, new MetadataCountOnly(I.MaximumMetadataObservations + 1)), "webroot_disk_inventory_metadata_input");
        Reject(() => I.ClassifyMetadata(actual, raw, null!), "webroot_disk_inventory_metadata_input");
        Reject(() => I.ClassifyMetadata(null!, raw, [lba]), "webroot_disk_inventory_metadata_input");
        Reject(() => I.ClassifyMetadata(actual, raw.AsSpan(0, 512), [lba]), "webroot_disk_inventory_metadata_input");
        Reject(() => I.ClassifyMetadata(actual, raw.AsSpan(0, raw.Length - 1), [lba]), "webroot_disk_inventory_metadata_input");
        HostCheck.Equal(0, I.ClassifyMetadata(actual, raw, []).Count);
        var exactBound = I.ClassifyMetadata(actual, raw, Enumerable.Repeat(lba, I.MaximumMetadataObservations).ToArray());
        HostCheck.Equal(I.MaximumMetadataObservations, exactBound.Count);
    });

    [HostTest("disk metadata classification and navigation sector inventories remain immutable after borrowed inputs change", HostLane.Native)]
    private static void MetadataImmutable() => WithInventory((_, _, _, _, raw, actual) =>
    {
        var savedDirectories = actual.DirectorySectors.ToArray(); List<ulong> lbas = [savedDirectories[0].Lba, savedDirectories[^1].Lba];
        var result = I.ClassifyMetadata(actual, raw, lbas); var saved = result.ToArray();
        lbas[0] = ulong.MaxValue; lbas.Clear(); Array.Clear(raw);
        HostCheck.True(result.SequenceEqual(saved) && actual.DirectorySectors.SequenceEqual(savedDirectories));
        bool classified = false, directories = false;
        try { ((IList<I.MetadataSector>)result)[0] = default; } catch (NotSupportedException) { classified = true; }
        try { ((IList<I.DirectorySector>)actual.DirectorySectors)[0] = default; } catch (NotSupportedException) { directories = true; }
        HostCheck.True(classified && directories && !actual.GrantsNativeDeviceOrProcessAuthority);
    });

    private sealed class MetadataCountOnly(int count) : IReadOnlyList<ulong>
    {
        public int Count => count;
        public ulong this[int index] => throw new InvalidOperationException("metadata_bound_must_precede_indexing");
        public IEnumerator<ulong> GetEnumerator() => throw new InvalidOperationException("metadata_bound_must_precede_enumeration");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static void WithInventory(Action<string, string, string, WebRootImageFixture.Receipt, byte[], I.Inspection> action)
        => WebRootImageFixtureTests.WithImage((root, directory, image, source) =>
        {
            string output = Path.Combine(directory, "inventory.raw");
            var receipt = WebRootImageFixture.Apply(root, image, source, WebRootImageFixture.SnapshotPublishedRoot(source), output);
            byte[] raw = File.ReadAllBytes(output); var actual = I.Inspect(output, raw, receipt);
            action(root, directory, output, receipt, raw, actual);
        });
    private static WebRootImageFixture.Receipt Rebase(WebRootImageFixture.Receipt original, string path)
    {
        var parsed = WebRootImageFixture.ReadBack(path);
        return original with { FinalSha256 = parsed.Sha256,
            WebRootFiles = parsed.Files.Where(f => f.Path.StartsWith(I.Root + "/", StringComparison.Ordinal)).ToArray(),
            PreservedFiles = parsed.Files.Where(f => original.PreservedFiles.Any(p => p.Path == f.Path)).ToArray(),
            Sentinel = parsed.Files.Single(f => f.Path == original.Sentinel.Path) };
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Reject(Action action, string? reason = null)
    {
        bool rejected = false;
        try { action(); } catch (ToolFailure failure) { rejected = reason is null || failure.Reason == reason; }
        HostCheck.True(rejected, reason ?? "expected rejected disk inventory");
    }
}
