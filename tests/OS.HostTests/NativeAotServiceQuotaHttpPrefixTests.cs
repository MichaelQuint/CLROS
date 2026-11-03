using System.Text;
using OS.Tooling;

internal static class NativeAotServiceQuotaHttpPrefixTests
{
    private const string Get = "GET /healthz HTTP/1.1\r\nHost: 127.0.0.1:8080\r\n\r\n",
        Post = "POST /__clros/service/start HTTP/1.1\r\nHost: 127.0.0.1:8080\r\nContent-Length: 0\r\n\r\n",
        Length = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nabc",
        Chunked = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nabc\r\n2\r\nde\r\n0\r\n\r\n",
        ChunkHead = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n";

    [HostTest("quota HTTP prefixes consume sequential persistent messages at every real byte split")]
    private static void Splits()
    {
        foreach (var vector in new[] { (Parts: new[] { Get, Post, Get }, Responses: false),
            (Parts: new[] { Length, Chunked, Length }, Responses: true) })
        {
            byte[] bytes = Encoding.ASCII.GetBytes(string.Concat(vector.Parts));
            int[] ends = vector.Parts.Select(part => Encoding.ASCII.GetByteCount(part)).ToArray();
            for (int i = 1; i < ends.Length; i++)
                ends[i] += ends[i - 1];
            for (int cut = 0; cut <= bytes.Length; cut++)
            {
                var report = QemuTranscripts.InspectNativeAotServiceQuotaHttpPrefixesForTests(bytes.AsSpan(0, cut), vector.Responses);
                HostCheck.Equal(cut, report.InputBytes);
                HostCheck.Equal(ends.Count(end => end <= cut), report.Messages.Count(m => m.State == "complete"));
                HostCheck.True(report.Messages.Count(m => m.State != "complete") <= 1);
                int start = 0;
                foreach (var message in report.Messages)
                {
                    HostCheck.Equal(start, message.Start);
                    HostCheck.True(message.End > start && message.End <= cut);
                    if (message.State == "complete")
                        HostCheck.True(ends.Contains(message.End));
                    start = message.End;
                }
                HostCheck.Equal(cut, start);
            }
            HostCheck.Equal(3, QemuTranscripts.InspectNativeAotServiceQuotaHttpPrefixesForTests(bytes, vector.Responses).Messages.Count);
        }
        var full = Read(Chunked + Length, true);
        HostCheck.Equal("abcde", Encoding.ASCII.GetString(Convert.FromBase64String(full.Messages[0].BodyBase64)));
        HostCheck.Equal("abc", Encoding.ASCII.GetString(Convert.FromBase64String(full.Messages[1].BodyBase64)));
        HostCheck.Equal(Get.Length, Read(Get + Post, false).Messages[1].Start);
    }

    [HostTest("quota HTTP prefixes preserve held cancel headers partial echo and exact chunk suffix states")]
    private static void Incomplete()
    {
        var cancel = Read(ChunkHead, true).Messages.Single();
        HostCheck.True(cancel.State == "chunk-size" && cancel.Framing == "chunked" && cancel.BodyBase64 == "");
        byte[] pattern = new byte[64];
        for (int i = 0; i < pattern.Length; i++)
            pattern[i] = unchecked((byte)(31 + i * 17));
        byte[] head = Encoding.ASCII.GetBytes("POST /__clros/service/lifecycle/echo HTTP/1.1\r\nHost: clros.service\r\nContent-Length: 32768\r\n\r\n");
        var echo = QemuTranscripts.InspectNativeAotServiceQuotaHttpPrefixesForTests(head.Concat(pattern).ToArray(), false).Messages.Single();
        HostCheck.True(echo.State == "body" && echo.DeclaredLength == 32768 && echo.HeaderEnd == head.Length &&
            Convert.FromBase64String(echo.BodyBase64).AsSpan().SequenceEqual(pattern));
        foreach (var vector in new[] { ("", "chunk-size"), ("3\r", "chunk-size"), ("3\r\na", "chunk-data"),
            ("3\r\nabc\r", "chunk-delimiter"), ("3\r\nabc\r\n0\r\n", "chunk-end"), ("0\r\n\r", "chunk-end") })
            HostCheck.Equal(vector.Item2, Read(ChunkHead + vector.Item1, true).Messages.Single().State);
        HostCheck.Equal("header", Read("GET /healthz HTTP/1.1\r", false).Messages.Single().State);
        HostCheck.Equal("header", Read("GET /healthz HTTP/1.1\r\nHost: clros\r", false).Messages.Single().State);
        HostCheck.Equal("header", Read(Get[..^1], false).Messages.Single().State);
        HostCheck.True(Read(Get, false).Messages[0].Framing == "none" && Read(Post, false).Messages[0].DeclaredLength == 0);
    }

    [HostTest("quota HTTP prefixes reject malformed duplicate conflicting ambiguous and unsupported framing")]
    private static void Malformed()
    {
        foreach (string bad in new[] { "GET\r", "GET / HTTP/1.1\n", "GET / HTTP/1.1\rX", "BAD / HTTP/1.1\r\n",
            "GET / HTTP/1.0\r\nHost: x\r\n\r\n", "GET / HTTP/1.1\r\nHost: x\r\nhost: y\r\n\r\n",
            "GET / HTTP/1.1\r\nBad Name: x\r\n", "GET / HTTP/1.1\r\nHost\r", "GET / HTTP/1.1\r\nHost:\tx\r\n",
            "POST / HTTP/1.1\r\nHost: x\r\nContent-Length: 01\r\n\r\n",
            "POST / HTTP/1.1\r\nHost: x\r\nContent-Length: +1\r\n\r\n",
            "POST / HTTP/1.1\r\nHost: x\r\nContent-Length: 4194305\r\n\r\n",
            "POST / HTTP/1.1\r\nHost: x\r\nContent-Length: 0\r\nTransfer-Encoding: chunked\r\n\r\n" })
            Reject(() => Read(bad, false));
        foreach (string bad in new[] { "HTTP/1.1 200 OK\r\nServer: Kestrel\r\n\r\n",
            "HTTP/1.1 100 Continue\r\nContent-Length: 0\r\n\r\n",
            "HTTP/1.1 204 No Content\r\nContent-Length: 0\r\n\r\n",
            "HTTP/1.1 304 Not Modified\r\nContent-Length: 0\r\n\r\n",
            ChunkHead.Replace("chunked", "gzip", StringComparison.Ordinal),
            ChunkHead.Replace("Transfer-Encoding: chunked", "Transfer-Encoding: chunked\r\nTrailer: X-End", StringComparison.Ordinal),
            ChunkHead + "3;ext=x\r\nabc\r\n0\r\n\r\n", ChunkHead + "3\n", ChunkHead + "\r",
            ChunkHead + "3\r\nabcX", ChunkHead + "0\r\nX-End: value\r\n\r\n",
            ChunkHead + "400001\r\n", ChunkHead + "000000000", ChunkHead + "z" })
            Reject(() => Read(bad, true));
        Reject(() => QemuTranscripts.InspectNativeAotServiceQuotaHttpPrefixesForTests(new byte[4 * 1024 * 1024 + 1], false));
        Reject(() => Read("GET / HTTP/1.1\r\nHost: x\r\nX: " + new string('a', 8192), false));
        Reject(() => Read(string.Concat(Enumerable.Repeat(Get, 8193)), false));
        const string start = "GET / HTTP/1.1\r\nHost: x\r\nX: ";
        string boundary = start + new string('a', 8192 - start.Length - 4) + "\r\n\r\n";
        HostCheck.True(boundary.Length == 8192 && Read(boundary, false).Messages.Single().State == "complete");
        HostCheck.Equal("header", Read(boundary[..^1], false).Messages.Single().State);
        HostCheck.True(Read("HTTP/1.1 200 OK\r\nContent-Length: 4194304\r\n\r\n", true).Messages.Single().State == "body");
    }

    [HostTest("quota HTTP prefix reports copy header and body bytes into immutable output")]
    private static void Copies()
    {
        byte[] bytes = Encoding.ASCII.GetBytes(Length);
        var report = QemuTranscripts.InspectNativeAotServiceQuotaHttpPrefixesForTests(bytes, true);
        bytes.AsSpan().Clear();
        var message = report.Messages.Single();
        HostCheck.True(message.Headers["Content-Length"] == "3" && message.BodyBase64 == "YWJj");
        bool list = false, headers = false;
        try
        {
            ((IList<QemuTranscripts.ServiceQuotaHttpPrefix>)report.Messages)[0] = message with
            {
                State = "forged"
            };
        }
        catch (NotSupportedException) { list = true; }
        try
        {
            ((IDictionary<string, string>)message.Headers)["Content-Length"] = "9";
        }
        catch (NotSupportedException) { headers = true; }
        HostCheck.True(list && headers);
    }

    private static QemuTranscripts.ServiceQuotaHttpPrefixes Read(string value, bool responses) =>
        QemuTranscripts.InspectNativeAotServiceQuotaHttpPrefixesForTests(Encoding.ASCII.GetBytes(value), responses);
    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
