using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
/// <summary>Own a fresh external Chromium process, private profile and original
/// bounded CDP transcript. The guest origin must supply the actual Blazor assets
/// and API values. Copied value validation grants no process or guest authority;
/// the enclosing consumer must join these exchanges to its original packets and
/// independent disk inventory. No existing browser profile or host webroot is used.</summary>

public static partial class NativeAotDiskStaticBrowser
{
    public const string Schema = "clros-disk-browser-v1";
    public const int MaximumRequests = 256, MaximumBodyBytes = 4 * 1024 * 1024, MaximumTotalBodyBytes = 32 * 1024 * 1024, MaximumProtocolBytes = 64 * 1024 * 1024;
    /// <summary>Compare copied complete exchange multisets. Retain all byte-
    /// equivalent candidates for every CDP request; a deterministic one-to-one
    /// assignment consumes every wire row exactly once without inventing a NAT
    /// mapping. This helper alone grants no original-capture authority.</summary>

    public static IReadOnlyList<Match> MatchExchangeValues(IReadOnlyList<Exchange> browser, IReadOnlyList<WireExchange> wire)
    {
        Need(browser.Count is > 0 and <= MaximumRequests && browser.Count == wire.Count && browser.Select(e => e.RequestId).Distinct(StringComparer.Ordinal).Count() == browser.Count && browser.Sum(e => (long)e.Bytes) <= MaximumTotalBodyBytes && wire.Sum(e => (long)e.Bytes) <= MaximumTotalBodyBytes, "wire_multiset_count");
        bool[] consumed = new bool[wire.Count];
        List<Match> matches = [];
        foreach (var exchange in browser)
        {
            int[] candidates = Enumerable.Range(0, wire.Count).Where(i => wire[i].Equals(exchange)).ToArray();
            int assigned = candidates.FirstOrDefault(i => !consumed[i], -1);
            Need(assigned >= 0, "wire_exchange_missing " + exchange.RequestId);
            consumed[assigned] = true;
            matches.Add(new(exchange.RequestId, assigned, Array.AsReadOnly(candidates)));
        }

        Need(consumed.All(v => v), "wire_exchange_extra");
        return Array.AsReadOnly(matches.ToArray());
    }

    /// <summary>Recheck the original joined browser completion, every retained
    /// body/protocol/DOM/PNG/log/receipt identity and independent inventory before
    /// matching the caller's complete wire multiset. Packet and guest authority
    /// remain with the original enclosing consumer.</summary>

    public static IReadOnlyList<Match> ValidateOriginal(Completion original, WebRootDiskInventory.Inspection inventory, IReadOnlyList<WireExchange> wire)
    {
        original.VerifyUnchanged();
        Need(original.InventorySha256 == inventory.ImageSha256, "original_inventory_changed");
        ValidateValues(inventory, original.Port, original.Document, original.Exchanges);
        return MatchExchangeValues(original.Exchanges, wire);
    }

    /// <summary>Synchronously inspect five copied CDP params objects and a
    /// decoded body. Requires the complete extra-info/finish chain, matching ids,
    /// plain guest HTTP/1.1, no redirects/cache/service worker and bounded headers
    /// and content. This is a value parser, not an original-capture constructor.</summary>

    public static Exchange InspectExchangeValues(JsonElement request, JsonElement requestExtra, JsonElement response, JsonElement responseExtra, JsonElement finished, byte[] decodedBody, string bodyFile, int port)
    {
        Need(port is > 0 and < 65536 && decodedBody.Length <= MaximumBodyBytes, "exchange_bounds");
        string id = Text(request, "requestId");
        Need(id.Length is > 0 and <= 128 && new[] { requestExtra, response, responseExtra, finished }.All(e => Text(e, "requestId") == id), "request_identity");
        // CDP emits redirectHasExtraInfo:false on ordinary requests. Its
        // presence alone is not a redirect; a redirect response always is.
        Need(!request.TryGetProperty("redirectResponse", out _) && (!request.TryGetProperty("redirectHasExtraInfo", out var redirectInfo) || redirectInfo.ValueKind == JsonValueKind.False), "redirect");
        var req = request.GetProperty("request");
        string method = Text(req, "method"), url = Text(req, "url");
        Need(method == "GET" && !req.TryGetProperty("postData", out _), "request_method");
        string origin = "http://127.0.0.1:" + port;
        Need(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "http" && uri.Host == "127.0.0.1" && uri.Port == port && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 && url == origin + uri.PathAndQuery && uri.PathAndQuery.Length <= 2048 && !uri.AbsolutePath.Contains('\\') && !uri.AbsolutePath.Contains("//", StringComparison.Ordinal), "guest_origin");
        string target = url[origin.Length..];
        var res = response.GetProperty("response");
        Need(Text(res, "url") == url && Text(res, "protocol") == "http/1.1" && response.TryGetProperty("hasExtraInfo", out var extra) && extra.GetBoolean(), "response_identity");
        foreach (string flag in new[]
        {
            "fromDiskCache",
            "fromServiceWorker",
            "fromPrefetchCache"
        }

        )
            Need(!res.TryGetProperty(flag, out var value) || !value.GetBoolean(), "cached_response");
        int status = res.GetProperty("status").GetInt32();
        Need(status == responseExtra.GetProperty("statusCode").GetInt32() && status is 200 or 404, "response_status");
        long encoded = finished.GetProperty("encodedDataLength").GetInt64();
        Need(encoded is >= 0 and <= MaximumBodyBytes + 65536 && bodyFile.Length is > 0 and <= 32 && bodyFile.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-') && bodyFile.EndsWith(".body", StringComparison.Ordinal), "body_identity");
        var requestHeaders = Headers(requestExtra.GetProperty("headers"));
        var responseHeaders = Headers(responseExtra.GetProperty("headers"));
        Need(requestHeaders.Count(h => h.Name.Equals("Host", StringComparison.OrdinalIgnoreCase) && h.Value == "127.0.0.1:" + port) == 1, "host_header");
        Need(!responseHeaders.Any(h => h.Name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) || h.Name.Equals("Location", StringComparison.OrdinalIgnoreCase)), "encoded_or_redirected_body");
        var length = responseHeaders.Where(h => h.Name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)).ToArray();
        var transfer = responseHeaders.Where(h => h.Name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)).ToArray();
        Need(length.Length == 1 && transfer.Length == 0 && int.TryParse(length[0].Value, out int bytes) && bytes == decodedBody.Length || length.Length == 0 && transfer.Length == 1 && transfer[0].Value == "chunked", "body_length");
        return new(id, method, target, "http/1.1", status, requestHeaders, responseHeaders, encoded, decodedBody, bodyFile);
    }

    /// <summary>Validate copied exchange bytes against the independently
    /// extracted inventory and rendered component values. Every request must be
    /// an admitted asset, the two fixed APIs, or the browser's empty favicon404.
    /// A root document, JS/CSS/WASM, both APIs and the variant nonce must actually
    /// have completed. No device, packet, browser or guest authority is granted.</summary>

    public static void ValidateValues(WebRootDiskInventory.Inspection inventory, int port, Dom dom, IReadOnlyList<Exchange> exchanges)
    {
        Need(port is > 0 and < 65536 && exchanges.Count is > 0 and <= MaximumRequests && exchanges.Select(e => e.RequestId).Distinct(StringComparer.Ordinal).Count() == exchanges.Count && exchanges.Sum(e => (long)e.Bytes) <= MaximumTotalBodyBytes, "network_bounds");
        var nonce = inventory.Find(WebRootDiskInventory.Root + "/fixture-nonce.txt");
        Need(nonce is { IsDirectory: false, Bytes: > 0 and <= 130 }, "nonce_inventory");
        string expectedNonce = new UTF8Encoding(false, true).GetString(nonce!.CopyBytes()).TrimEnd('\r', '\n');
        Need(expectedNonce.Length is > 0 and <= 128 && dom == new Dom("ready", "CLROS disk-hosted Blazor", "CLROS ready — café", "API items: 2", expectedNonce, "http://127.0.0.1:" + port), "rendered_values");
        foreach (var exchange in exchanges)
        {
            string physical = Uri.UnescapeDataString(exchange.Target.Split('?')[0]);
            Need(exchange.Method == "GET" && exchange.Protocol == "http/1.1" && !exchange.FromDiskCache && !exchange.FromServiceWorker && !exchange.FromPrefetchCache, "exchange_policy");
            if (physical == "/favicon.ico")
            {
                Need(exchange.Status == 404 && exchange.Bytes == 0, "favicon_response");
                continue;
            }

            Need(exchange.Status == 200, "asset_status");
            string media;
            if (physical is "/api/status" or "/api/items")
            {
                Need(exchange.Target == physical, "api_query");
                using var json = JsonDocument.Parse(exchange.CopyBody());
                var value = json.RootElement;
                if (physical == "/api/status")
                    Need(value.GetProperty("profile").GetString() == "v1" && value.GetProperty("ready").GetBoolean() && value.GetProperty("message").GetString() == dom.Status, "status_json");
                else
                {
                    var items = value.GetProperty("items");
                    Need(items.GetArrayLength() == 2 && items[0].GetProperty("id").GetInt32() == 1 && items[0].GetProperty("name").GetString() == "Blazor" && items[1].GetProperty("id").GetInt32() == 2 && items[1].GetProperty("name").GetString() == "FAT32", "items_json");
                }

                media = "application/json";
            }
            else
            {
                string path = physical == "/" ? "/index.html" : physical;
                var entry = inventory.Find(WebRootDiskInventory.Root + path);
                Need(entry is { IsDirectory: false } && entry.Bytes == exchange.Bytes && entry.Sha256 == exchange.Sha256, "asset_bytes");
                media = Path.GetExtension(path).ToLowerInvariant() switch
                {
                    ".html" => "text/html",
                    ".css" => "text/css",
                    ".js" or ".mjs" => "text/javascript",
                    ".json" => "application/json",
                    ".txt" => "text/plain",
                    ".wasm" => "application/wasm",
                    ".dat" or ".bin" => "application/octet-stream",
                    _ => throw new ToolFailure("disk_browser_asset_media")
                };
            }

            var contentType = exchange.ResponseHeaders.Where(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).ToArray();
            Need(contentType.Length == 1 && contentType[0].Value.Split(';')[0].Trim() == media, "response_media");
        }

        foreach (string required in new[]
        {
            "/",
            "/api/status",
            "/api/items",
            "/fixture-nonce.txt",
            "/_framework/blazor.webassembly.js"
        }

        )
            Need(exchanges.Any(e => Uri.UnescapeDataString(e.Target.Split('?')[0]) == required), "required_request " + required);
        foreach (string extension in new[]
        {
            ".wasm",
            ".js",
            ".css"
        }

        )
            Need(exchanges.Any(e => Uri.UnescapeDataString(e.Target.Split('?')[0]).EndsWith(extension, StringComparison.Ordinal) && e.Bytes > 0), "required_asset " + extension);
    }

    /// <summary>Run one Windows job-owned Chrome/Edge process for at most one
    /// hour. All artifacts must be fresh under root/artifacts. Cache and
    /// service-worker bypass precede navigation; bodies, raw events, DOM and PNG
    /// are retained before graceful browser close. Cancellation/failure joins
    /// the owned job and preserves partial logs/receipt before throwing.</summary>

    public static async Task<Completion> RunAsync(string root, int guestForwardPort, string freshEvidenceDir, WebRootDiskInventory.Inspection inventory, CancellationToken cancellation = default)
    {
        Need(OperatingSystem.IsWindows(), "windows_process_owner_required");
        root = Path.GetFullPath(root);
        string destination = Path.GetFullPath(freshEvidenceDir, root);
        Need(guestForwardPort is > 0 and < 65536 && destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !Path.Exists(destination), "fresh_output");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(destination)!, "disk_browser_output");
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        string browser = FindBrowser();
        string browserHash = NativeAotWebPayload.HashFile(browser);
        Directory.CreateDirectory(destination);
        using var ownership = new FileStream(Path.Combine(destination, "ownership.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        string profile = Path.Combine(destination, "profile");
        Directory.CreateDirectory(profile);
        string[] arguments = ["--headless=new", "--remote-debugging-address=127.0.0.1", "--remote-debugging-port=0", "--user-data-dir=" + profile, "--no-first-run", "--no-default-browser-check", "--disable-background-networking", "--disable-component-update", "--disable-default-apps", "--disable-sync", "--disable-extensions", "--disable-quic", "--disable-http2", "--disable-features=MediaRouter,OptimizationHints", "--window-size=1280,720", "about:blank"];
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(DiskStaticCapturePolicy.BrowserTimeoutSeconds));
        using var processStop = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        Task<ProcessResult> process = ProcessRunner.RunAsync(new(browser, arguments, root)
        {
            Timeout = TimeSpan.FromSeconds(DiskStaticCapturePolicy.BrowserTimeoutSeconds),
            OutputCharacterLimit = 4 * 1024 * 1024
        }, processStop.Token);
        ProcessResult? joined = null;
        Cdp? cdp = null;
        Dom? dom = null;
        Exchange[] exchanges = [];
        string failure = "";
        Exception? observationFailure = null;
        bool succeeded = false;
        using var capture = new Capture();
        try
        {
            string active = Path.Combine(profile, "DevToolsActivePort");
            while (!File.Exists(active))
            {
                Need(!process.IsCompleted, "browser_exited_before_debugger");
                await Task.Delay(100, deadline.Token);
            }

            string[] endpoint = await File.ReadAllLinesAsync(active, deadline.Token);
            int debugPort = 0;
            Need(endpoint.Length == 2 && int.TryParse(endpoint[0], out debugPort) && debugPort is > 0 and < 65536 && endpoint[1].StartsWith("/devtools/browser/", StringComparison.Ordinal), "debugger_endpoint");
            using HttpClient http = new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            string listing = await http.GetStringAsync("http://127.0.0.1:" + debugPort + "/json/list", deadline.Token);
            Need(listing.Length <= 65536, "debugger_listing");
            using var targets = JsonDocument.Parse(listing);
            var page = targets.RootElement.EnumerateArray().Single(e => Text(e, "type") == "page" && Text(e, "url") == "about:blank");
            string websocket = Text(page, "webSocketDebuggerUrl");
            Need(Uri.TryCreate(websocket, UriKind.Absolute, out var ws) && ws.Scheme == "ws" && ws.Host == "127.0.0.1" && ws.Port == debugPort && ws.AbsolutePath.StartsWith("/devtools/page/", StringComparison.Ordinal), "debugger_websocket");
            cdp = new Cdp(Path.Combine(destination, "protocol.jsonl"), capture.Event);
            await cdp.Connect(ws!, deadline.Token);
            await cdp.Call("Network.enable", new
            {
                maxTotalBufferSize = MaximumTotalBodyBytes,
                maxResourceBufferSize = MaximumBodyBytes
            }, deadline.Token);
            await cdp.Call("Network.setCacheDisabled", new
            {
                cacheDisabled = true
            }, deadline.Token);
            await cdp.Call("Network.setBypassServiceWorker", new
            {
                bypass = true
            }, deadline.Token);
            await cdp.Call("Page.enable", new
            {
            }, deadline.Token);
            await cdp.Call("Runtime.enable", new
            {
            }, deadline.Token);
            await cdp.Call("Log.enable", new
            {
            }, deadline.Token);
            await cdp.Call("Page.navigate", new
            {
                url = "http://127.0.0.1:" + guestForwardPort + "/"
            }, deadline.Token);
            while (true)
            {
                var value = await cdp.Call("Runtime.evaluate", new
                {
                    expression = DocumentExpression,
                    returnByValue = true
                }, deadline.Token);
                Need(!value.TryGetProperty("exceptionDetails", out _), "document_evaluation");
                if (value.GetProperty("result").TryGetProperty("value", out var document))
                {
                    dom = JsonSerializer.Deserialize<Dom>(document.GetRawText())!;
                    Need(dom.State != "failed", "blazor_failed");
                    if (dom.State == "ready" && capture.Quiet)
                        break;
                }

                Need(!process.IsCompleted, "browser_exited_before_ready");
                await Task.Delay(250, deadline.Token);
            }

            await cdp.Call("Page.stopLoading", new
            {
            }, deadline.Token);
            var rows = capture.Snapshot();
            List<Exchange> completed = [];
            int total = 0;
            foreach (var row in rows)
            {
                var body = await cdp.Call("Network.getResponseBody", new
                {
                    requestId = row.Id
                }, deadline.Token);
                string encoded = Text(body, "body");
                Need(encoded.Length <= MaximumBodyBytes * 2, "body_capacity");
                byte[] bytes = body.GetProperty("base64Encoded").GetBoolean() ? Convert.FromBase64String(encoded) : Encoding.UTF8.GetBytes(encoded);
                Need((total = checked(total + bytes.Length)) <= MaximumTotalBodyBytes, "total_body_capacity");
                string name = completed.Count.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + ".body";
                var exchange = InspectExchangeValues(row.Request, row.RequestExtra, row.Response, row.ResponseExtra, row.Finished, bytes, name, guestForwardPort);
                await File.WriteAllBytesAsync(Path.Combine(destination, name), bytes, deadline.Token);
                completed.Add(exchange);
                exchanges = completed.ToArray();
            }

            exchanges = completed.ToArray();
            ValidateValues(inventory, guestForwardPort, dom!, exchanges);
            await File.WriteAllTextAsync(Path.Combine(destination, "document.json"), JsonSerializer.Serialize(dom), deadline.Token);
            var rendered = await cdp.Call("Runtime.evaluate", new
            {
                expression = "document.documentElement.outerHTML",
                returnByValue = true
            }, deadline.Token);
            string html = Text(rendered.GetProperty("result"), "value");
            Need(html.Length <= 1024 * 1024, "document_capacity");
            await File.WriteAllTextAsync(Path.Combine(destination, "document.html"), html, deadline.Token);
            var screenshot = await cdp.Call("Page.captureScreenshot", new
            {
                format = "png",
                captureBeyondViewport = false
            }, deadline.Token);
            byte[] png = Convert.FromBase64String(Text(screenshot, "data"));
            Need(png.Length is > 24 and <= 8 * 1024 * 1024 && png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "screenshot_png");
            await File.WriteAllBytesAsync(Path.Combine(destination, "screenshot.png"), png, deadline.Token);
            await cdp.Call("Browser.close", new
            {
            }, deadline.Token);
            joined = await process;
            QemuTranscripts.RequireCompleteProcess(joined, "disk_browser_process_incomplete");
            Need(joined.Succeeded, "browser_exit");
            await cdp.Join(joined, deadline.Token);
            capture.RequireComplete(rows.Length);
            Need(NativeAotWebPayload.HashFile(browser) == browserHash && ToolingBuildInputs.Validate(root).Count == 0, "inputs_changed");
            succeeded = true;
        }
        catch (Exception error)
        {
            failure = error is ToolFailure tool ? tool.Reason : error.GetType().Name + ": " + error.Message.ReplaceLineEndings(" ");
            observationFailure = error;
            throw;
        }
        finally
        {
            await RunCleanupPreservingFailureAsync(observationFailure, async () =>
            {
                if (!process.IsCompleted)
                    processStop.Cancel();
                try
                {
                    joined ??= await process;
                }
                catch (Exception cleanup)
                {
                    succeeded = false;
                    failure += " process-owner: " + cleanup.GetType().Name + ": " + cleanup.Message.ReplaceLineEndings(" ");
                }
                finally
                {
                    if (cdp is not null)
                        await cdp.DisposeAsync();
                }

                await File.WriteAllTextAsync(Path.Combine(destination, "browser.stdout.log"), joined?.StandardOutput ?? "", CancellationToken.None);
                await File.WriteAllTextAsync(Path.Combine(destination, "browser.stderr.log"), joined?.StandardError ?? "", CancellationToken.None);
                await File.WriteAllTextAsync(Path.Combine(destination, "errors.json"), JsonSerializer.Serialize(capture.Errors()), CancellationToken.None);
                await File.WriteAllTextAsync(Path.Combine(destination, "receipt.json"), JsonSerializer.Serialize(new
                {
                    Schema,
                    Succeeded = succeeded,
                    Failure = failure,
                    Port = guestForwardPort,
                    InventorySha256 = inventory.ImageSha256,
                    Browser = browser,
                    BrowserSha256 = browserHash,
                    Arguments = arguments,
                    Process = joined,
                    DebuggerShutdown = cdp?.Shutdown,
                    Document = dom,
                    Exchanges = exchanges,
                    ScreenshotSha256 = File.Exists(Path.Combine(destination, "screenshot.png")) ? NativeAotWebPayload.HashFile(Path.Combine(destination, "screenshot.png")) : "",
                    ProtocolSha256 = File.Exists(Path.Combine(destination, "protocol.jsonl")) ? NativeAotWebPayload.HashFile(Path.Combine(destination, "protocol.jsonl")) : "",
                    PacketJoinQualified = false,
                    GuestQualified = false
                }), CancellationToken.None);
            });
        }

        var retained = System.IO.Directory.GetFiles(destination, "*", SearchOption.TopDirectoryOnly).Where(p => Path.GetFileName(p) != "ownership.lock").ToDictionary(p => p, NativeAotWebPayload.HashFile, StringComparer.Ordinal);
        return new(root, destination, guestForwardPort, NativeAotWebPayload.HashFile(Path.Combine(destination, "receipt.json")), inventory.ImageSha256, joined!, dom!, exchanges, retained);
    }

    private const string DocumentExpression = "(()=>({State:document.querySelector('main')?.dataset.clrosState??'',Heading:document.querySelector('h1')?.textContent??'',Status:document.querySelector('#api-status')?.textContent??'',Items:document.querySelector('#api-items')?.textContent??'',Nonce:document.querySelector('#disk-nonce')?.textContent??'',Origin:location.origin}))()";
    private static string FindBrowser()
    {
        foreach (string directory in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        }

        )
            foreach (string path in new[]
            {
                "Google/Chrome/Application/chrome.exe",
                "Microsoft/Edge/Application/msedge.exe"
            }

            )
                if (File.Exists(Path.Combine(directory, path)))
                    return Path.Combine(directory, path);
        throw new ToolFailure("disk_browser_chromium_missing");
    }

    private static IReadOnlyList<Header> Headers(JsonElement value)
    {
        Need(value.ValueKind == JsonValueKind.Object, "headers");
        List<Header> headers = [];
        int total = 0;
        foreach (var property in value.EnumerateObject())
        {
            string text = property.Value.GetString()!;
            Need(headers.Count < 64 && property.Name.Length is > 0 and <= 128 && property.Name.All(c => char.IsAsciiLetterOrDigit(c) || "!#$%&'*+-.^_`|~".Contains(c)) && text.Length <= 8192 && text.All(c => c is >= ' ' and <= '~') && (total += property.Name.Length + text.Length) <= 32768 && !headers.Any(h => h.Name.Equals(property.Name, StringComparison.OrdinalIgnoreCase)), "header_bounds");
            headers.Add(new(property.Name, text));
        }

        return Array.AsReadOnly(headers.ToArray());
    }

    private static string Text(JsonElement element, string property) => element.GetProperty(property).GetString() ?? throw new ToolFailure("disk_browser_string " + property);
    private static bool SameHeaders(IReadOnlyList<Header> a, IReadOnlyList<Header> b) => a.Count == b.Count && a.OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase).ThenBy(h => h.Value, StringComparer.Ordinal).Zip(b.OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase).ThenBy(h => h.Value, StringComparer.Ordinal), (x, y) => x.Name.Equals(y.Name, StringComparison.OrdinalIgnoreCase) && x.Value == y.Value).All(v => v);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("disk_browser_" + reason);
    }
}
