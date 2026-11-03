using System.Security.Cryptography;
using System.Text;
using OS.Tooling;
using Observation = OS.Tooling.NativeAotStaticFileContract.Observation;

internal static class NativeAotStaticFileEvidenceTests
{
    [HostTest("static HTTP evidence validates independent JSON assets ranges and metadata")]
    internal static void Positive() => WithEvidence((directory, assets, rows) => NativeAotStaticFileContract.ValidateHttp(directory, assets, rows));

    [HostTest("static HTTP evidence rejects absent stale swapped and malformed responses")]
    internal static void Rejections() => WithEvidence((directory, assets, rows) =>
    {
        Reject(() => NativeAotStaticFileContract.ValidateHttp(directory, assets, rows.Skip(1).ToArray()));
        foreach (var changed in new[]
        {
            rows[0] with { Status = 500 }, rows[0] with { Sha256 = new string('0', 64) },
            rows[0] with { BodyFile = "../outside.body" }, rows[0] with { BodyBytes = rows[0].BodyBytes + 1 }
        })
        {
            var copy = rows.ToArray();
            copy[0] = changed;
            Reject(() => NativeAotStaticFileContract.ValidateHttp(directory, assets, copy));
        }
        var wrongRange = rows.ToArray();
        int index = Array.FindIndex(wrongRange, x => x.Status == 206);
        wrongRange[index] = wrongRange[index] with
        {
            ContentRange = "bytes 511-1032/2048"
        };
        Reject(() => NativeAotStaticFileContract.ValidateHttp(directory, assets, wrongRange));
        var wrongType = rows.ToArray();
        index = Array.FindIndex(wrongType, x => x.Path == "/app.wasm" && x.Status == 200);
        wrongType[index] = wrongType[index] with
        {
            ContentType = "text/plain"
        };
        Reject(() => NativeAotStaticFileContract.ValidateHttp(directory, assets, wrongType));
        var serialized = rows.ToArray();
        index = Array.FindIndex(serialized, x => x.Concurrent);
        serialized[index] = serialized[index] with
        {
            Started = 100,
            Finished = 110
        };
        Reject(() => NativeAotStaticFileContract.ValidateHttp(directory, assets, serialized));
        var duplicate = rows.ToArray();
        duplicate[1] = duplicate[1] with
        {
            BodyFile = duplicate[0].BodyFile
        };
        Reject(() => NativeAotStaticFileContract.ValidateHttp(directory, assets, duplicate));
        File.WriteAllText(Path.Combine(directory, rows[0].BodyFile), "stale body");
        Reject(() => NativeAotStaticFileContract.ValidateHttp(directory, assets, rows));
    });

    [HostTest("static reference replay rejects changed original native inputs")]
    internal static void NativeInputs()
    {
        string directory = Path.Combine(Path.GetTempPath(), "clros-static-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string input = Path.Combine(directory, "original.o");
            File.WriteAllBytes(input, [1, 2, 3, 4]);
            var inventory = new Dictionary<string, string> { [input] = Hash(File.ReadAllBytes(input)) };
            NativeAotStaticFileContract.ValidateNativeInputs(inventory);
            File.WriteAllBytes(input, [1, 2, 3, 5]);
            Reject(() => NativeAotStaticFileContract.ValidateNativeInputs(inventory));
            File.Delete(input);
            Reject(() => NativeAotStaticFileContract.ValidateNativeInputs(inventory));
            Reject(() => NativeAotStaticFileContract.ValidateNativeInputs(new Dictionary<string, string>()));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [HostTest("static reference shutdown requires signal exit marker and endpoint refusal")]
    internal static void Shutdown()
    {
        string stderr = "CLROS_APP_ENTRY minimal.api.static-files\nCLROS_STATIC_FILES_APP_END v1\n";
        string[] trace = ["1720000000.000 --- SIGTERM {si_signo=SIGTERM, si_code=SI_USER} ---", "1720000000.001 exit_group(0) = ?"];
        var receipt = new NativeAotStaticFileContract.Shutdown(15, 0, true);
        NativeAotStaticFileContract.ValidateShutdown(stderr, trace, receipt);
        Reject(() => NativeAotStaticFileContract.ValidateShutdown(stderr, trace, receipt with { EndpointRefused = false }));
        Reject(() => NativeAotStaticFileContract.ValidateShutdown(stderr, trace, receipt with { ExitCode = 1 }));
        Reject(() => NativeAotStaticFileContract.ValidateShutdown(stderr, trace.Skip(1).ToArray(), receipt));
        Reject(() => NativeAotStaticFileContract.ValidateShutdown(stderr + "CLROS_STATIC_FILES_APP_END v1\n", trace, receipt));
    }

    [HostTest("Blazor preparation retains raw SDK bytes and refuses unsafe publication")]
    internal static void PreparedAssets()
    {
        string root = Path.Combine(Path.GetTempPath(), "clros-blazor-assets-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "artifacts", "publish", "raw");
        Directory.CreateDirectory(Path.Combine(source, "_framework"));
        try
        {
            File.WriteAllText(Path.Combine(source, "_framework", "blazor.webassembly.js"), "immutable SDK bootstrap");
            byte[] wasm = [0, 97, 115, 109, 1, 0, 0, 0];
            File.WriteAllBytes(Path.Combine(source, "_framework", "runtime.wasm"), wasm);
            File.WriteAllText(Path.Combine(source, "_framework", "runtime.wasm.br"), "compression sidecar");
            File.WriteAllText(Path.Combine(source, "_framework", "runtime.js.map"), "debug sidecar");
            string[] originalTree = Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
            foreach (string overlap in new[] { source, source + Path.DirectorySeparatorChar,
                Path.Combine(source, "_framework", ".."), Path.Combine(source, "prepared"), Path.GetDirectoryName(source)! })
            {
                bool overlapRejected = false;
                try
                {
                    BlazorStaticFileAssets.Prepare(root, source, overlap);
                }
                catch (InvalidDataException error)
                {
                    overlapRejected = error.Message == "blazor_source_destination_overlap";
                }
                HostCheck.True(overlapRejected);
                HostCheck.True(Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).SequenceEqual(originalTree));
            }
            // A sibling sharing only the source's filename prefix is safe.
            string output = Path.Combine(root, "artifacts", "publish", "raw-prepared");
            BlazorStaticFileAssets.Prepare(root, source, output);
            HostCheck.True(File.ReadAllBytes(Path.Combine(output, "_framework", "runtime.wasm")).AsSpan().SequenceEqual(wasm));
            HostCheck.True(File.ReadAllText(Path.Combine(output, "index.html")) == BlazorStaticFileAssets.HostPage);
            HostCheck.True(!File.Exists(Path.Combine(output, "_framework", "runtime.wasm.br")) &&
                !File.Exists(Path.Combine(output, "_framework", "runtime.js.map")));
            Reject(() => BlazorStaticFileAssets.Prepare(root, source, output));
            Reject(() => BlazorStaticFileAssets.Prepare(root, source, Path.Combine(root, "outside")));
            HostCheck.True(!Directory.Exists(Path.Combine(root, "outside")));
            HostCheck.True(File.ReadAllBytes(Path.Combine(source, "_framework", "runtime.wasm")).AsSpan().SequenceEqual(wasm));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void WithEvidence(Action<string, WebRootImageFixture.Snapshot, Observation[]> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "clros-static-http-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            List<Observation> rows = [];
            byte[] index = "<html>independent static fixture</html>"u8.ToArray();
            byte[] wasm = Enumerable.Range(0, 2048).Select(i => (byte)(i ^ (i >> 8))).ToArray();
            var assets = new WebRootImageFixture.Snapshot([new("index.html", index.Length, Hash(index)), new("app.wasm", wasm.Length, Hash(wasm))],
                index.Length + wasm.Length, wasm.Length, "independent fixture inventory");
            void Row(string method, string path, int status, byte[] body, string? type = null, long? length = null, string? range = null)
            {
                string file = $"http-{rows.Count:D4}.body";
                File.WriteAllBytes(Path.Combine(directory, file), body);
                rows.Add(new(method, path, status, type, length ?? body.Length, body.Length, Hash(body), file,
                    "\"fixture-etag\"", "Tue, 01 Jan 1980 00:00:00 GMT", range));
            }
            Row("GET", "/healthz", 200, "OS_MINIMAL_API_OK"u8.ToArray());
            Row("GET", "/config", 200, "{\"appId\":\"minimal.api.static-files\"}"u8.ToArray());
            Row("GET", "/api/status", 200, Encoding.UTF8.GetBytes("{\"profile\":\"v1\",\"ready\":true,\"message\":\"CLROS ready — café\"}"));
            Row("GET", "/api/items", 200, "{\"items\":[{\"id\":1},{\"id\":2,\"detail\":null}]}"u8.ToArray());
            Row("GET", "/api/items?empty=true", 200, "{\"items\":[]}"u8.ToArray());
            Row("GET", "/fixture-nonce.txt", 200, "CLROS-DISK-A-0001\n"u8.ToArray());
            Row("GET", "/", 200, index, "text/html");
            Row("HEAD", "/", 200, [], "text/html", index.Length);
            Row("GET", "/index.html", 200, index, "text/html");
            Row("GET", "/app.wasm", 200, wasm, "application/wasm");
            Row("GET", "/app.wasm", 206, wasm.AsSpan(510, 522).ToArray(), "application/wasm", 522, "bytes 510-1031/2048");
            Row("GET", "/app.wasm", 416, [], range: "bytes */2048");
            Row("GET", "/", 304, []);
            Row("GET", "/", 304, []);
            Row("GET", "/.clros-missing", 404, []);
            Row("POST", "/index.html", 404, []);
            Row("GET", "/app.wasm", 200, wasm, "application/wasm");
            rows[^1] = rows[^1] with
            {
                Concurrent = true,
                Started = 10,
                Finished = 30
            };
            Row("GET", "/app.wasm", 200, wasm, "application/wasm");
            rows[^1] = rows[^1] with
            {
                Concurrent = true,
                Started = 20,
                Finished = 40
            };
            action(directory, assets, rows.ToArray());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        catch (FileNotFoundException)
        {
            rejected = true;
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
