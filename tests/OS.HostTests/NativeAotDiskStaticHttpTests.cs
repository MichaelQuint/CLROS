using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static partial class NativeAotDiskStaticHttpTests
{
    private static void WithFixture(Action<Func<string, Fixture>> action) => WebRootImageFixtureTests.WithImage((root, dir, image, source) =>
    {
        string path = Path.Combine(dir, "disk-http.raw");
        var receipt = WebRootImageFixture.Apply(root, image, source, WebRootImageFixture.SnapshotPublishedRoot(source), path);
        var inventory = WebRootDiskInventory.Inspect(path, File.ReadAllBytes(path), receipt);
        action(mode => Vector(inventory, mode));
    });
    private static Fixture Vector(WebRootDiskInventory.Inspection inventory, string mode = "none")
    {
        var original = NativeAotSocketIntegrationTests.Packets();
        var frames = original.Frames.Take(8).Select(f => f.ToArray()).ToList();
        uint client = 1000, server = 2000;
        const ushort remote = 43000;
        void Add(bool outgoing, uint seq, uint ack, byte flags, ReadOnlySpan<byte> bytes = default) => frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing, remote, seq, ack, flags, bytes));
        Add(false, client++, 0, 2);
        Add(true, server++, client, 0x12);
        Add(false, client, server, 0x10);
        JsonArray rows = [];
        Dictionary<string, byte[]> bodies = [];
        List<QemuTranscripts.DiskStaticSocketWrite> writes = [];
        List<NativeDiskStaticReadWindows.ReadWindow> reads = [];
        ulong stream = 0;
        for (int ordinal = 0; ordinal < 5; ordinal++)
        {
            string path = DiskStaticHttpContract.Paths[ordinal], name = ordinal.ToString("D3", CultureInfo.InvariantCulture) + ".body";
            var file = ordinal < 3 ? null : inventory.Find(WebRootDiskInventory.Root + path)!;
            byte[] entity = ordinal == 0 ? "OS_MINIMAL_API_OK"u8.ToArray() : ordinal == 1 ? Encoding.UTF8.GetBytes("{\"profile\":\"v1\",\"ready\":true,\"message\":\"CLROS ready \u2014 caf\u00e9\"}") : ordinal == 2 ? "{\"items\":[{\"id\":1,\"name\":\"Blazor\",\"detail\":\"Standalone WebAssembly\"},{\"id\":2,\"name\":\"FAT32\",\"detail\":null}]}"u8.ToArray() : file!.CopyBytes();
            string type = ordinal is 1 or 2 ? "application/json; charset=utf-8" : ordinal == 3 ? "text/html" : "text/plain";
            List<string> headers = ["Date: Fri, 23 Oct 2026 00:00:00 GMT", "Server: Kestrel", "Content-Type: " + type];
            bool chunked = ordinal is 1 or 2;
            headers.Add(chunked ? "Transfer-Encoding: chunked" : "Content-Length: " + entity.Length.ToString(CultureInfo.InvariantCulture));
            if (file is not null)
            {
                headers.AddRange(["Accept-Ranges: bytes", "Cache-Control: no-store", "Last-Modified: Tue, 01 Jan 1980 00:00:00 GMT", "ETag: \"" + (0x1a8e79fe1d58000L ^ file.Bytes).ToString("x", CultureInfo.InvariantCulture) + "\""]);
                reads.Add(new(0x100000001, (uint)ordinal, 17, (uint)ordinal, (uint)ordinal + 10, 95 + ordinal * 10, 99 + ordinal * 10, file.Inode, 0, 8192, 8192, file.Bytes));
            }

            bodies.Add(name, entity.ToArray());
            rows.Add(new JsonObject { ["Ordinal"] = ordinal, ["Method"] = "GET", ["Path"] = path, ["Status"] = 200, ["RequestHeaders"] = new JsonArray(), ["ResponseHeaders"] = new JsonArray(headers.Select(h => (JsonNode? )JsonValue.Create(h)).ToArray()), ["Bytes"] = entity.Length, ["Sha256"] = Convert.ToHexString(SHA256.HashData(entity)), ["BodyFile"] = name, ["Started"] = 100 + ordinal * 100, ["Finished"] = 150 + ordinal * 100 });
            byte[] request = Encoding.ASCII.GetBytes("GET " + (ordinal == 3 && mode == "wrong-target" ? "/other.html" : path) + " HTTP/1.1\r\nHost: " + (mode == "wrong-host" ? "elsewhere:5555" : "127.0.0.1:5555") + "\r\n\r\n");
            Add(false, client, server, 0x18, request);
            client += (uint)request.Length;
            Add(true, server, client, 0x10);
            byte[] actual = entity.ToArray();
            if (mode == "changed-body" && ordinal == 3)
                actual[0] ^= 1;
            if (chunked)
                actual = [..Encoding.ASCII.GetBytes(actual.Length.ToString("x", CultureInfo.InvariantCulture) + "\r\n"), ..actual, .."\r\n0\r\n\r\n"u8.ToArray()];
            byte[] response = [..Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" + string.Concat(headers.Select(h => h + "\r\n")) + "\r\n"), ..actual];
            writes.Add(new(0, 100 + ordinal * 10, stream, stream + (uint)response.Length));
            stream += (uint)response.Length;
            Add(true, server, client, 0x18, response);
            if (mode == "retry")
                Add(true, server, client, 0x18, response);
            server += (uint)response.Length;
            Add(false, client, server, 0x10);
        }

        if (mode != "missing-fin")
        {
            Add(false, client++, server, 0x11);
            Add(true, server, client, 0x10);
            Add(true, server++, client, 0x11);
            Add(false, client, server, 0x10);
        }

        var root = new JsonObject
        {
            ["Schema"] = DiskStaticHttpContract.Schema,
            ["Scope"] = DiskStaticHttpContract.Scope,
            ["Port"] = 5555,
            ["Complete"] = true,
            ["ClockFrequency"] = 1000,
            ["Responses"] = rows
        };
        var stat = new QemuTranscripts.StaticFileReplayReport(default, 1000, 2000, 3000, 2, 2, 1, Array.AsReadOnly(new[] { new QemuTranscripts.StaticFileSocketIo(0, stream, (ulong)DiskStaticHttpContract.Paths.Sum(p => Encoding.ASCII.GetByteCount("GET " + p + " HTTP/1.1\r\nHost: 127.0.0.1:5555\r\n\r\n")), 0, 1, true, true) }));
        var replay = new QemuTranscripts.DiskStaticReplayReport(stat, Array.AsReadOnly(reads.ToArray()), [], Array.AsReadOnly(writes.ToArray()), [], 1, 1, 37, 37, 50);
        return new(NativeAotSocketIntegrationTests.Capture(frames), original.Anchor, original.Arp, root, bodies, inventory, replay);
    }

    private static QemuTranscripts.DiskStaticHttpInspection Read(Fixture f) => QemuTranscripts.InspectNativeAotDiskStaticHttpValues(f.Packets, f.Anchor, f.Arp, [43000], Encoding.UTF8.GetBytes(f.Receipt.ToJsonString()), f.Bodies, f.Inventory, f.Replay);
    private static void Reject(Fixture f, string? reason = null)
    {
        string? actual = null;
        try
        {
            Read(f);
        }
        catch (Exception e)when (e is ToolFailure or InvalidDataException or System.Text.Json.JsonException or FormatException)
        {
            actual = e is ToolFailure t ? t.Reason : e.Message;
        }

        HostCheck.True(actual is not null && (reason is null || actual.Contains(reason, StringComparison.Ordinal)), "disk HTTP expected " + reason + " got " + actual);
    }

    [HostTest("disk HTTP development joins five actual wire entities JSON file metadata and cold read-before-send", HostLane.Native)]
    private static void Complete() => WithFixture(make =>
    {
        foreach (string mode in new[]
        {
            "none",
            "retry"
        }

        )
        {
            var f = make(mode);
            var value = Read(f);
            HostCheck.True(value.Connections == 1 && value.Responses == 5 && value.Files.Count == 2 && value.Scope == "five-response-development" && !value.GrantsOriginalNativeProcessOrBrowserAuthority);
            HostCheck.True(value.Files[0].Path.EndsWith("/index.html", StringComparison.Ordinal) && value.Files[0].ReadCompleteRecord < value.Files[0].FirstBodySendRecord);
            bool immutable = false;
            try
            {
                ((IList<QemuTranscripts.DiskStaticHttpDataJoin>)value.Files)[0] = default;
            }
            catch (NotSupportedException)
            {
                immutable = true;
            }

            HostCheck.True(immutable);
        }
    });
    [HostTest("disk HTTP rejects receipt substitution wrong hashes headers paths scope and incomplete inventories", HostLane.Native)]
    private static void Receipt() => WithFixture(make =>
    {
        foreach (string mode in new[]
        {
            "scope",
            "complete",
            "extra",
            "ordinal",
            "hash",
            "file",
            "headers",
            "time",
            "metadata"
        }

        )
        {
            var f = make("none");
            var rows = f.Receipt["Responses"]!.AsArray();
            switch (mode)
            {
                case "scope":
                    f.Receipt["Scope"] = "blazor-qualified";
                    break;
                case "complete":
                    f.Receipt["Complete"] = false;
                    break;
                case "extra":
                    rows.Add(rows[0]!.DeepClone());
                    break;
                case "ordinal":
                    rows[3]!["Ordinal"] = 2;
                    break;
                case "hash":
                    rows[3]!["Sha256"] = new string ('0', 64);
                    break;
                case "file":
                    rows[3]!["BodyFile"] = "../003.body";
                    break;
                case "headers":
                    rows[3]!["RequestHeaders"] = new JsonArray("X: forged");
                    break;
                case "time":
                    rows[3]!["Started"] = 1;
                    break;
                case "metadata":
                    rows[3]!["ResponseHeaders"]!.AsArray().Add("ETag: \"wrong\"");
                    break;
            }

            Reject(f);
        }
    });
    [HostTest("disk HTTP independently rejects altered wire target host bytes and incomplete close", HostLane.Native)]
    private static void Wire() => WithFixture(make =>
    {
        foreach (string mode in new[]
        {
            "wrong-target",
            "wrong-host",
            "changed-body",
            "missing-fin"
        }

        )
            Reject(make(mode));
    });
    [HostTest("disk HTTP requires original read identity mapping complete byte coverage and send order", HostLane.Native)]
    private static void Causality() => WithFixture(make =>
    {
        foreach (string mode in new[]
        {
            "late",
            "early",
            "inode",
            "extra",
            "gap"
        }

        )
        {
            var f = make("none");
            var reads = f.Replay.Reads.ToArray();
            var writes = f.Replay.SocketWrites.ToArray();
            if (mode == "late")
                reads[0] = reads[0] with
                {
                    CompleteRecordIndex = 130
                };
            if (mode == "early")
                reads[0] = reads[0] with
                {
                    BeginRecordIndex = 120
                };
            if (mode == "inode")
                reads[0] = reads[0] with
                {
                    Inode = 17
                };
            if (mode == "extra")
                reads = [..reads, reads[0]];
            if (mode == "gap")
                writes[2] = writes[2] with
                {
                    Before = writes[2].Before + 1
                };
            Reject(f with { Replay = f.Replay with { Reads = reads, SocketWrites = writes } });
        }
    });
    private static void WithAssetInventory(Action<WebRootDiskInventory.Inspection, WebRootImageFixture.Receipt, WebRootImageFixture.Snapshot> action, Action<string>? prepare = null) => WebRootImageFixtureTests.WithImage((root, dir, image, source) =>
    {
        prepare?.Invoke(source);
        var snapshot = WebRootImageFixture.SnapshotPublishedRoot(source);
        string path = Path.Combine(dir, "asset-matrix.raw");
        var receipt = WebRootImageFixture.Apply(root, image, source, snapshot, path);
        action(WebRootDiskInventory.Inspect(path, File.ReadAllBytes(path), receipt), receipt, snapshot);
    });
    [HostTest("disk HTTP asset plan matches independent complete published inventory and confined escaped routes", HostLane.Native)]
    private static void AssetInventory() => WithAssetInventory((inventory, receipt, snapshot) =>
    {
        var plan = DiskStaticHttpContract.CreateAssetPlan(inventory);
        HostCheck.True(plan.ImageSha256 == inventory.ImageSha256 && plan.PublishedInventorySha256 == snapshot.Sha256 && plan.PublishedInventorySha256 == receipt.PublishedInventorySha256 && plan.PublishedFiles == snapshot.Files.Length && plan.PublishedBytes == snapshot.TotalBytes && !plan.GrantsOriginalNativeProcessOrBrowserAuthority);
        var expected = inventory.Entries.Where(e => !e.IsDirectory).ToArray();
        HostCheck.True(plan.Assets.Select(a => a.Path).SequenceEqual(expected.Select(e => e.Path)) && plan.Assets.Where(a => a.Published).Select(a => a.Path).SequenceEqual(snapshot.Files.Select(f => "/app/wwwroot/" + f.Path)) && plan.Requests.Select(r => r.Ordinal).SequenceEqual(Enumerable.Range(0, plan.Requests.Count)));
        foreach (var asset in plan.Assets)
        {
            var physical = inventory.Find(asset.Path)!;
            HostCheck.True(asset.Bytes == physical.Bytes && asset.Sha256 == physical.Sha256 && asset.Inode == physical.Inode && asset.Path.StartsWith("/app/wwwroot/", StringComparison.Ordinal) && !physical.IsDirectory);
            HostCheck.True(plan.Requests.Count(r => r.Method == "GET" && r.Target == asset.Target && r.Status == 200 && r.Headers.Count == 0) == 1 && plan.Requests.Count(r => r.Method == "HEAD" && r.Target == asset.Target && r.ContentLength == asset.Bytes && r.BodyBytes == 0) == 1);
        }

        HostCheck.True(plan.Assets.Single(a => a.Path.EndsWith("/hash#tag%.txt", StringComparison.Ordinal)).Target == "/hash%23tag%25.txt" && plan.Assets.Single(a => a.Path.EndsWith("/extra.mjs", StringComparison.Ordinal)).MediaType == "text/javascript" && plan.Assets.Single(a => a.Path.EndsWith("/extra.json", StringComparison.Ordinal)).MediaType == "application/json" && plan.Assets.Single(a => a.Path.EndsWith("/extra.dat", StringComparison.Ordinal)).MediaType == "application/octet-stream");
        HostCheck.True(!plan.Assets.Any(a => a.Path.Contains("/EFI/", StringComparison.Ordinal) || a.Path.Contains("/OS/", StringComparison.Ordinal) || a.Path.Contains("/private/", StringComparison.Ordinal)) && plan.Requests.Where(r => r.Status == 404).All(r => r.Asset is null));
        bool assets = false, requests = false, headers = false;
        try
        {
            ((IList<DiskStaticHttpContract.Asset>)plan.Assets).RemoveAt(0);
        }
        catch (NotSupportedException)
        {
            assets = true;
        }

        try
        {
            ((IList<DiskStaticHttpContract.Request>)plan.Requests).RemoveAt(0);
        }
        catch (NotSupportedException)
        {
            requests = true;
        }

        try
        {
            ((IList<DiskStaticHttpContract.Header>)plan.Requests.First(r => r.Headers.Count != 0).Headers).Clear();
        }
        catch (NotSupportedException)
        {
            headers = true;
        }

        HostCheck.True(assets && requests && headers && DiskStaticHttpContract.Paths.Count == 5 && DiskStaticHttpContract.Scope == "five-response-development");
    }, source =>
    {
        File.WriteAllText(Path.Combine(source, "hash#tag%.txt"), "escaped route");
        File.WriteAllText(Path.Combine(source, "extra.mjs"), "export const value=1;");
        File.WriteAllText(Path.Combine(source, "extra.json"), "{}");
        File.WriteAllBytes(Path.Combine(source, "extra.dat"), [1, 2, 3]);
    });
    [HostTest("disk HTTP asset plan defines exact range conditional HEAD and fallback body semantics", HostLane.Native)]
    private static void AssetSemantics() => WithAssetInventory((inventory, _, snapshot) =>
    {
        var plan = DiskStaticHttpContract.CreateAssetPlan(inventory);
        var largest = snapshot.Files.OrderByDescending(f => f.Bytes).ThenBy(f => f.Path, StringComparer.Ordinal).First();
        string path = "/app/wwwroot/" + largest.Path;
        byte[] bytes = inventory.Find(path)!.CopyBytes();
        var rows = plan.Requests.Where(r => r.Asset?.Path == path).ToArray();
        string Header(DiskStaticHttpContract.Request row, string name) => row.Headers.Single(h => h.Name == name).Value;
        var cross = rows.Single(r => r.Status == 206 && r.Headers.Count == 1 && Header(r, "Range") == "bytes=510-1031");
        HostCheck.True(cross.BodyOffset == 510 && cross.BodyBytes == 522 && cross.ContentLength == 522 && cross.ContentRange == "bytes 510-1031/" + bytes.Length && cross.BodySha256 == Convert.ToHexString(SHA256.HashData(bytes.AsSpan(510, 522))));
        var suffix = rows.Single(r => r.Status == 206 && r.Headers.Count == 1 && Header(r, "Range") == "bytes=-513");
        HostCheck.True(suffix.BodyOffset == bytes.Length - 513 && suffix.BodyBytes == 513 && suffix.BodySha256 == Convert.ToHexString(SHA256.HashData(bytes.AsSpan(bytes.Length - 513))));
        var tail = rows.Single(r => r.Status == 206 && r.Headers.Count == 1 && Header(r, "Range").EndsWith("-", StringComparison.Ordinal));
        HostCheck.True(tail.BodyOffset == bytes.Length - 32 && tail.BodyBytes == 32 && tail.ContentRange == $"bytes {bytes.Length - 32}-{bytes.Length - 1}/{bytes.Length}");
        var unsatisfied = rows.Single(r => r.Status == 416);
        HostCheck.True(unsatisfied.BodyBytes == 0 && unsatisfied.ContentLength == 0 && unsatisfied.ContentRange == "bytes */" + bytes.Length && Header(unsatisfied, "Range") == "bytes=" + bytes.Length + "-");
        var etag = rows.Single(r => r.Headers.Any(h => h.Name == "If-None-Match"));
        var modified = rows.Single(r => r.Headers.Any(h => h.Name == "If-Modified-Since"));
        HostCheck.True(etag.Status == 304 && modified.Status == 304 && etag.BodyBytes == 0 && modified.BodyBytes == 0 && etag.ContentLength is null && modified.ContentLength is null && Header(etag, "If-None-Match") == etag.Asset!.ETag && Header(modified, "If-Modified-Since") == "Tue, 01 Jan 1980 00:00:00 GMT");
        var ifRange = rows.Single(r => r.Status == 206 && r.Headers.Any(h => h.Name == "If-Range"));
        var fallback = rows.Single(r => r.Status == 200 && r.Headers.Any(h => h.Name == "If-Range"));
        HostCheck.True(Header(ifRange, "If-Range") == ifRange.Asset!.ETag && ifRange.BodyBytes == 522 && fallback.BodyOffset == 0 && fallback.BodyBytes == bytes.Length && fallback.BodySha256 == largest.Sha256 && Header(fallback, "If-Range") != fallback.Asset!.ETag);
        HostCheck.True(plan.Requests.Single(r => r.Target == "/" && r.Method == "GET").Asset!.Path == "/app/wwwroot/index.html" && plan.Requests.Single(r => r.Method == "POST").Status == 404 && plan.Requests.Where(r => r.Method == "HEAD").All(r => r.MinimumReadCompletions == 0));
    });
    [HostTest("disk HTTP asset plan bounds complete large wasm bodies while requiring multiple read completions", HostLane.Native)]
    private static void AssetCoverage() => WithAssetInventory((inventory, _, snapshot) =>
    {
        var plan = DiskStaticHttpContract.CreateAssetPlan(inventory);
        var wasm = plan.Assets.Single(a => a.Path.EndsWith("/maximum.wasm", StringComparison.Ordinal));
        HostCheck.True(wasm.Bytes == 2 * 1024 * 1024 && wasm.MediaType == "application/wasm" && snapshot.MaximumFileBytes == wasm.Bytes);
        long observed = 0;
        int lowerBound = 0;
        foreach (var row in plan.Requests)
        {
            observed += row.BodyBytes;
            if (row.Asset is null || row.BodyBytes == 0)
            {
                HostCheck.True(row.MinimumReadCompletions == 0);
                continue;
            }

            var file = inventory.Find(row.Asset.Path)!;
            byte[] data = file.CopyBytes();
            HostCheck.True(row.BodyOffset + row.BodyBytes <= data.Length && row.BodySha256 == Convert.ToHexString(SHA256.HashData(data.AsSpan((int)row.BodyOffset, (int)row.BodyBytes))));
            int completions = 0;
            uint remaining = row.BodyBytes;
            while (remaining != 0)
            {
                remaining -= Math.Min(remaining, 8192U);
                completions++;
            }

            HostCheck.True(row.MinimumReadCompletions == completions);
            lowerBound += completions;
        }

        var full = plan.Requests.Single(r => r.Asset == wasm && r.Method == "GET" && r.Headers.Count == 0);
        var fragmented = plan.Requests.Single(r => r.Target == "/fixture/fragmented.bin" && r.Method == "GET");
        HostCheck.True(full.MinimumReadCompletions == 256 && fragmented.MinimumReadCompletions > 16 && inventory.Find(fragmented.Asset!.Path)!.Extents.Count > 2 && observed == plan.ExpectedBodyBytes && lowerBound == plan.MinimumReadCompletions && observed > snapshot.TotalBytes && observed <= DiskStaticHttpContract.MaximumExpectedBodyBytes && plan.Requests.Count <= DiskStaticHttpContract.MaximumAssetRequests);
    }, source => File.WriteAllBytes(Path.Combine(source, "_framework/maximum.wasm"), Enumerable.Range(0, 2 * 1024 * 1024).Select(i => (byte)(i ^ (i >> 9))).ToArray()));
    [HostTest("disk HTTP asset plan rejects unadmitted media route collisions and exhausted inventory capacities", HostLane.Native)]
    private static void AssetRejections()
    {
        foreach (string mode in new[]
        {
            "media",
            "count",
            "total",
            "collision"
        }

        )
            WithAssetInventory((inventory, _, _) =>
            {
                string? reason = null;
                try
                {
                    DiskStaticHttpContract.CreateAssetPlan(inventory);
                }
                catch (ToolFailure failure)
                {
                    reason = failure.Reason;
                }

                HostCheck.True(reason is not null && reason.StartsWith("disk_http_plan_", StringComparison.Ordinal) && (mode != "media" || reason == "disk_http_plan_media") && (mode != "collision" || reason == "disk_http_plan_control_collision"), mode + ": " + reason);
            }, source =>
            {
                if (mode == "media")
                    File.WriteAllBytes(Path.Combine(source, "debug.pdb"), [1]);
                if (mode == "count")
                    for (int i = 0; i < 100; i++)
                        File.WriteAllText(Path.Combine(source, $"extra-{i:D3}.txt"), "x");
                if (mode == "total")
                    for (int i = 0; i < 5; i++)
                        File.WriteAllBytes(Path.Combine(source, $"large-{i}.wasm"), new byte[2 * 1024 * 1024]);
                if (mode == "collision")
                {
                    Directory.CreateDirectory(Path.Combine(source, "private"));
                    File.WriteAllText(Path.Combine(source, "private/sentinel.txt"), "conflicting served route");
                }
            });
    }
}
