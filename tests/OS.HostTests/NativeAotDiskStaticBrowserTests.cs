using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;
using B = OS.Tooling.NativeAotDiskStaticBrowser;

internal static partial class NativeAotDiskStaticBrowserTests
{
    private const int Port = 18777;
    private static JsonElement Element(JsonObject value)
    {
        using var json = JsonDocument.Parse(value.ToJsonString());
        return json.RootElement.Clone();
    }
    private static (JsonObject Request, JsonObject RequestExtra, JsonObject Response, JsonObject ResponseExtra, JsonObject Finished) Events(string target, byte[] body, string media, string id = "129.1", int status = 200)
    {
        string url = "http://127.0.0.1:" + Port + target;
        return (
            new()
            {
                ["requestId"] = id,
                ["request"] = new JsonObject { ["method"] = "GET", ["url"] = url }
            },
            new()
            {
                ["requestId"] = id,
                ["headers"] = new JsonObject { ["Host"] = "127.0.0.1:" + Port, ["Accept"] = "*/*" }
            },
            new()
            {
                ["requestId"] = id,
                ["hasExtraInfo"] = true,
                ["response"] = new JsonObject { ["url"] = url, ["protocol"] = "http/1.1", ["status"] = status, ["fromDiskCache"] = false, ["fromServiceWorker"] = false }
            },
            new()
            {
                ["requestId"] = id,
                ["statusCode"] = status,
                ["headers"] = new JsonObject { ["Content-Length"] = body.Length.ToString(), ["Content-Type"] = media }
            },
            new()
            {
                ["requestId"] = id,
                ["encodedDataLength"] = body.Length + 128
            });
    }
    private static B.Exchange Exchange(string target, byte[] body, string media, string id = "129.1", int status = 200)
    {
        var e = Events(target, body, media, id, status);
        return B.InspectExchangeValues(Element(e.Request), Element(e.RequestExtra), Element(e.Response), Element(e.ResponseExtra), Element(e.Finished), body, id + ".body", Port);
    }
    private static void Reject(Action action, string reason)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure error) { rejected = error.Reason.StartsWith("disk_browser_", StringComparison.Ordinal) && error.Reason.Contains(reason, StringComparison.Ordinal); }
        HostCheck.True(rejected, reason);
    }

    [HostTest("browser copied CDP chain preserves wire headers binary bodies and independent repeated requests")]
    private static void ExchangeCopies()
    {
        byte[] body = [0, 255, 1, 128, 0];
        var e = Events("/_framework/example.wasm", body, "application/wasm");
        var actual = B.InspectExchangeValues(Element(e.Request), Element(e.RequestExtra), Element(e.Response), Element(e.ResponseExtra), Element(e.Finished), body, "000.body", Port);
        HostCheck.True(actual.Method == "GET" && actual.Target == "/_framework/example.wasm" && actual.Status == 200 && actual.Protocol == "http/1.1" && actual.EncodedDataLength > actual.Bytes);
        HostCheck.True(actual.RequestHeaders.Single(h => h.Name == "Host").Value == "127.0.0.1:" + Port && actual.ResponseHeaders.Single(h => h.Name == "Content-Length").Value == "5");
        body[0] = 99;
        byte[] copy = actual.CopyBody();
        copy[1] = 99;
        HostCheck.True(actual.CopyBody().AsSpan().SequenceEqual(new byte[] { 0, 255, 1, 128, 0 }));
        e.RequestExtra["headers"]!["Host"] = "changed";
        HostCheck.True(actual.RequestHeaders.Single(h => h.Name == "Host").Value == "127.0.0.1:" + Port);
        bool immutable = false;
        try
        {
            ((IList<B.Header>)actual.ResponseHeaders).Clear();
        }
        catch (NotSupportedException) { immutable = true; }
        HostCheck.True(immutable);
        var repeated = Exchange(actual.Target, actual.CopyBody(), "application/wasm", "129.2");
        HostCheck.True(actual.Sha256 == repeated.Sha256 && actual.RequestId != repeated.RequestId);
        var headers = e.ResponseExtra["headers"]!.AsObject();
        headers.Remove("Content-Length");
        headers["Transfer-Encoding"] = "chunked";
        e.RequestExtra["headers"]!["Host"] = "127.0.0.1:" + Port;
        var chunked = B.InspectExchangeValues(Element(e.Request), Element(e.RequestExtra), Element(e.Response), Element(e.ResponseExtra), Element(e.Finished), actual.CopyBody(), "001.body", Port);
        HostCheck.Equal(actual.Sha256, chunked.Sha256);
        var query = Exchange("/_framework/example.wasm?v=exact%2Fidentity", actual.CopyBody(), "application/wasm", "129.3");
        HostCheck.Equal("/_framework/example.wasm?v=exact%2Fidentity", query.Target);
    }

    [HostTest("browser accepts observed false redirect extra info but rejects real redirects and malformed flags")]
    private static void RedirectExtraInfo()
    {
        byte[] body = [1, 2, 3];
        foreach (bool includeFalse in new[] { false, true })
        {
            var e = Events("/", body, "text/html");
            // The failed original's requestWillBeSent has no redirectResponse
            // and explicitly carries redirectHasExtraInfo:false.
            if (includeFalse)
                e.Request["redirectHasExtraInfo"] = false;
            var actual = B.InspectExchangeValues(Element(e.Request), Element(e.RequestExtra), Element(e.Response), Element(e.ResponseExtra), Element(e.Finished), body, "000.body", Port);
            HostCheck.Equal("/", actual.Target);
            HostCheck.Equal(200, actual.Status);
        }
        foreach (string flag in new[] { "true", "null", "0", "\"false\"", "{}", "[]" })
        {
            var e = Events("/", body, "text/html");
            e.Request["redirectHasExtraInfo"] = JsonNode.Parse(flag);
            Reject(() => B.InspectExchangeValues(Element(e.Request), Element(e.RequestExtra), Element(e.Response), Element(e.ResponseExtra), Element(e.Finished), body, "000.body", Port), "redirect");
        }
        var redirect = Events("/", body, "text/html");
        redirect.Request["redirectHasExtraInfo"] = false;
        redirect.Request["redirectResponse"] = new JsonObject { ["status"] = 302 };
        Reject(() => B.InspectExchangeValues(Element(redirect.Request), Element(redirect.RequestExtra), Element(redirect.Response), Element(redirect.ResponseExtra), Element(redirect.Finished), body, "000.body", Port), "redirect");
    }

    [HostTest("browser copied CDP parser rejects cache redirects incomplete identity cross origin and body ambiguity")]
    private static void ExchangeRejections()
    {
        byte[] body = [1, 2, 3];
        foreach (string mode in new[] { "request_identity", "redirect", "request_method", "guest_origin", "cached_response", "response_identity", "response_status", "host_header", "encoded_or_redirected_body", "body_length", "header_bounds", "body_identity" })
        {
            var e = Events("/index.html", body, "text/html");
            string file = "000.body";
            switch (mode)
            {
                case "request_identity":
                    e.Finished["requestId"] = "different";
                    break;
                case "redirect":
                    e.Request["redirectResponse"] = new JsonObject();
                    break;
                case "request_method":
                    e.Request["request"]!["method"] = "POST";
                    break;
                case "guest_origin":
                    e.Request["request"]!["url"] = "http://127.0.0.1:18778/index.html";
                    break;
                case "cached_response":
                    e.Response["response"]!["fromServiceWorker"] = true;
                    break;
                case "response_identity":
                    e.Response["response"]!["protocol"] = "h2";
                    break;
                case "response_status":
                    e.ResponseExtra["statusCode"] = 304;
                    break;
                case "host_header":
                    e.RequestExtra["headers"]!["Host"] = "external.example";
                    break;
                case "encoded_or_redirected_body":
                    e.ResponseExtra["headers"]!["Content-Encoding"] = "br";
                    break;
                case "body_length":
                    e.ResponseExtra["headers"]!["Content-Length"] = "4";
                    break;
                case "header_bounds":
                    e.RequestExtra["headers"]!["Bad Header"] = "value";
                    break;
                case "body_identity":
                    file = "../000.body";
                    break;
            }
            Reject(() => B.InspectExchangeValues(Element(e.Request), Element(e.RequestExtra), Element(e.Response), Element(e.ResponseExtra), Element(e.Finished), body, file, Port), mode);
        }
        foreach (string url in new[] { "http://user@127.0.0.1:18777/index.html", "http://localhost:18777/index.html", "http://127.0.0.1:18777/../index.html", "http://127.0.0.1:18777/index.html#fragment" })
        {
            var e = Events("/index.html", body, "text/html");
            e.Request["request"]!["url"] = url;
            Reject(() => B.InspectExchangeValues(Element(e.Request), Element(e.RequestExtra), Element(e.Response), Element(e.ResponseExtra), Element(e.Finished), body, "000.body", Port), "guest_origin");
        }
    }

    [HostTest("browser wire multiset preserves all indistinguishable candidates and rejects missing extra or changed exchange")]
    private static void WireJoin()
    {
        var a = Exchange("/index.html", "same"u8.ToArray(), "text/html", "200.1");
        var b = Exchange("/index.html", "same"u8.ToArray(), "text/html", "200.2");
        var c = Exchange("/api/status", "other"u8.ToArray(), "application/json", "200.3");
        B.WireExchange Wire(B.Exchange e, byte[]? body = null, string? target = null, bool changeHeader = false) => new(e.Method, target ?? e.Target, e.Status,
            e.RequestHeaders.Select(h => new B.Header(h.Name.ToLowerInvariant(), h.Value)),
            e.ResponseHeaders.Select(h => new B.Header(h.Name.ToUpperInvariant(), changeHeader && h.Name == "Content-Type" ? "changed" : h.Value)), [], body ?? e.CopyBody());
        var wire = new[] { Wire(b), Wire(c), Wire(a) };
        var matches = B.MatchExchangeValues([a, b, c], wire);
        HostCheck.True(matches[0].EquivalentWireCandidates.SequenceEqual(new[] { 0, 2 }) && matches[1].EquivalentWireCandidates.SequenceEqual(new[] { 0, 2 }) && matches[2].EquivalentWireCandidates.Single() == 1);
        HostCheck.True(matches.Select(m => m.AssignedWireIndex).Distinct().Count() == 3);
        Reject(() => B.MatchExchangeValues([a, b, c], wire[..2]), "wire_multiset_count");
        Reject(() => B.MatchExchangeValues([a, b, c], [.. wire, Wire(c)]), "wire_multiset_count");
        Reject(() => B.MatchExchangeValues([a, b, c], [Wire(a), Wire(b), Wire(c, "wrong"u8.ToArray())]), "wire_exchange_missing");
        Reject(() => B.MatchExchangeValues([a, b, c], [Wire(a), Wire(b), Wire(c, changeHeader: true)]), "wire_exchange_missing");
        Reject(() => B.MatchExchangeValues([a, b, c], [Wire(a), Wire(b), Wire(c, target: "/api/items")]), "wire_exchange_missing");
        Reject(() => new B.WireExchange("GET", "/", 200, [], [], [1], []), "wire_body_bounds");
    }

    [HostTest("browser value projection requires independently extracted framework assets API values and variant nonce", HostLane.Native)]
    private static void InventoryJoin() => WebRootImageFixtureTests.WithImage((root, directory, image, source) =>
    {
        string output = Path.Combine(directory, "browser-inventory.raw");
        var receipt = WebRootImageFixture.Apply(root, image, source, WebRootImageFixture.SnapshotPublishedRoot(source), output);
        var inventory = WebRootDiskInventory.Inspect(output, File.ReadAllBytes(output), receipt);
        List<B.Exchange> rows = [];
        foreach (var pair in new[] { ("/", "text/html"), ("/site.css", "text/css"), ("/_framework/blazor.webassembly.js", "text/javascript"), ("/_framework/Example.Project.wasm", "application/wasm"), ("/fixture-nonce.txt", "text/plain") })
        {
            var entry = inventory.Find(WebRootDiskInventory.Root + (pair.Item1 == "/" ? "/index.html" : pair.Item1))!;
            rows.Add(Exchange(pair.Item1, entry.CopyBytes(), pair.Item2, "100." + rows.Count));
        }
        rows.Add(Exchange("/api/status", Encoding.UTF8.GetBytes("{\"profile\":\"v1\",\"ready\":true,\"message\":\"CLROS ready — café\"}"), "application/json; charset=utf-8", "100.5"));
        rows.Add(Exchange("/api/items", "{\"items\":[{\"id\":1,\"name\":\"Blazor\"},{\"id\":2,\"name\":\"FAT32\"}]}"u8.ToArray(), "application/json", "100.6"));
        rows.Add(Exchange("/favicon.ico", [], "text/plain", "100.7", 404));
        string nonce = Encoding.UTF8.GetString(inventory.Find(WebRootDiskInventory.Root + "/fixture-nonce.txt")!.CopyBytes()).TrimEnd('\r', '\n');
        B.Dom dom = new("ready", "CLROS disk-hosted Blazor", "CLROS ready — café", "API items: 2", nonce, "http://127.0.0.1:" + Port);
        B.ValidateValues(inventory, Port, dom, rows);
        var queryRows = rows.ToArray();
        queryRows[3] = Exchange(rows[3].Target + "?v=original%2Fquery", rows[3].CopyBody(), "application/wasm", rows[3].RequestId);
        B.ValidateValues(inventory, Port, dom, queryRows);
        Reject(() => B.ValidateValues(inventory, Port, dom with { Nonce = nonce + "x" }, rows), "rendered_values");
        Reject(() => B.ValidateValues(inventory, Port, dom with { State = "loading" }, rows), "rendered_values");
        Reject(() => B.ValidateValues(inventory, Port, dom, rows.Where(r => !r.Target.EndsWith(".wasm", StringComparison.Ordinal)).ToArray()), "required_asset");
        Reject(() => B.ValidateValues(inventory, Port, dom, [.. rows, rows[0]]), "network_bounds");
        var changed = rows.ToArray();
        changed[3] = Exchange(rows[3].Target, new byte[rows[3].Bytes], "application/wasm", rows[3].RequestId);
        Reject(() => B.ValidateValues(inventory, Port, dom, changed), "asset_bytes");
        changed = rows.ToArray();
        changed[0] = Exchange("/private/sentinel.txt", [1], "text/plain", rows[0].RequestId);
        Reject(() => B.ValidateValues(inventory, Port, dom, changed), "asset_bytes");
        changed = rows.ToArray();
        changed[3] = Exchange(rows[3].Target, rows[3].CopyBody(), "application/octet-stream", rows[3].RequestId);
        Reject(() => B.ValidateValues(inventory, Port, dom, changed), "response_media");
        // Generated host values prove neither a browser execution nor a guest
        // packet exchange. The public completion has no public constructors.
        HostCheck.Equal(0, typeof(B.Completion).GetConstructors().Length);
    });
}
