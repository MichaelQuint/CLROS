using System.Globalization;
using System.Text;
using OS.Tooling;

internal static class NativeAotThreadingOutputTests
{
    // Independent synthetic observations; these vectors are not guest captures.
    private const string Suspension = "TEST_START nativeaot.suspension\n" +
        "NATIVEAOT_SUSPENSION_GC epoch=1 before=1 after=2 progressBefore=10 progressReturned=12 progressAfter=13\n" +
        "NATIVEAOT_SUSPENSION_GC epoch=2 before=2 after=3 progressBefore=13 progressReturned=15 progressAfter=16\n" +
        "NATIVEAOT_SUSPENSION_JOINED workers=1 collections=2 roots=true interior=true tls=true\n" +
        "TEST_PASS nativeaot.suspension\n";
    private const string ThreadPool = "TEST_START nativeaot.threadpool\n" +
        "NATIVEAOT_THREADPOOL_WORK tasks=4 completed=4 checksum=30 workers=3 threadPool=true\n" +
        "NATIVEAOT_THREADPOOL_CONTINUATIONS before=1 race=1 after=1 count=3 value=43 publicationInline=0\n" +
        "NATIVEAOT_THREADPOOL_CANCELLATION before=2 during=1 callbacks=0\n" +
        "NATIVEAOT_THREADPOOL_DELAY requestedMs=50 before=1000 after=1050 frequency=1000 minimumMs=25\n" +
        "NATIVEAOT_THREADPOOL_TIMER dueMs=50 before=1100 fired=1150 frequency=1000 callbacks=1 stale=0 disposed=true\n" +
        "NATIVEAOT_THREADPOOL_DRAINED tasks=4 continuations=3 callbacks=1 stale=0\nTEST_PASS nativeaot.threadpool\n";
    private const string Prefix = "TEST_START m16-009\r\nM16_009_NOTE label=µ\r\n";
    private const string Suffix = "M16_008_CALL retained=exact\r\nM16_008_SUPERVISOR retained=exact\r\n";

    [HostTest("nativeaot.threading-output admits complete coalesced observations and preserves unrelated raw bytes")]
    private static void FullOutputs()
    {
        foreach (var app in Applications())
        {
            byte[] payload = Encoding.UTF8.GetBytes(app.Text);
            byte[] raw = Join(Encoding.UTF8.GetBytes(Prefix), Envelope(payload), Encoding.UTF8.GetBytes(Suffix));
            byte[] before = (byte[])raw.Clone();
            var capture = NativeAotThreadingOutput.Read(app.Name, raw);
            HostCheck.Equal(app.Text, capture.Stdout);
            HostCheck.Equal(Prefix + Header(payload.Length) + WithoutNewlines(app.Text) + "\r\n" + Suffix, capture.NormalizedTail);
            HostCheck.True(capture.WriteLengths.SequenceEqual(new[] { payload.Length }) && raw.AsSpan().SequenceEqual(before));
        }
    }

    [HostTest("nativeaot.threading-output retains arbitrary segmentation including CRLF splits and one-byte writes")]
    private static void Segmentation()
    {
        foreach (var app in Applications())
        {
            string text = app.Text.Replace("\n", "\r\n", StringComparison.Ordinal);
            byte[] payload = Encoding.UTF8.GetBytes(text);
            int carriage = Array.IndexOf(payload, (byte)'\r');
            int[] lengths = [1, carriage, 1, payload.Length - carriage - 2]; // Split both a row and its CRLF pair.
            using MemoryStream raw = new();
            StringBuilder expected = new();
            int cursor = 0;
            foreach (int length in lengths)
            {
                byte[] part = payload.AsSpan(cursor, length).ToArray();
                raw.Write(Envelope(part));
                raw.Write(Encoding.UTF8.GetBytes(Suffix));
                expected.Append(Header(length)).Append(WithoutNewlines(Encoding.UTF8.GetString(part))).Append("\r\n").Append(Suffix);
                cursor += length;
            }
            var capture = NativeAotThreadingOutput.Read(app.Name, raw.ToArray());
            HostCheck.Equal(text, capture.Stdout);
            HostCheck.Equal(expected.ToString(), capture.NormalizedTail);
            HostCheck.True(capture.WriteLengths.SequenceEqual(lengths));
        }
        using MemoryStream singles = new();
        foreach (byte value in Encoding.UTF8.GetBytes(Suspension))
            singles.Write(Envelope([value]));
        var one = NativeAotThreadingOutput.Read("NativeAotSuspensionSmoke", singles.ToArray());
        HostCheck.Equal(Suspension, one.Stdout);
        HostCheck.True(one.WriteLengths.Count == Encoding.UTF8.GetByteCount(Suspension) && one.WriteLengths.All(n => n == 1) &&
            one.NormalizedTail.Contains("bytes=1 data=\r\n", StringComparison.Ordinal));
    }

    [HostTest("nativeaot.threading-output rejects malformed counts framing embedded markers encoding and bounded sizes")]
    private static void RawFailures()
    {
        byte[] payload = Encoding.UTF8.GetBytes(Suspension), valid = Envelope(payload);
        string header = Header(payload.Length), serial = Encoding.UTF8.GetString(valid);
        foreach (string changed in new[]
        {
            header.Replace("fd=1", "fd=2", StringComparison.Ordinal),
            Header(0), Header(payload.Length - 1), Header(payload.Length + 1), Header(4097),
            "M16_005_WRITE fd=1 bytes=0" + payload.Length.ToString(CultureInfo.InvariantCulture) + " data=",
            "M16_005_WRITE fd=1 bytes=-1 data=", "M16_005_WRITE fd=1 bytes=+1 data=",
            "M16_005_WRITE fd=1 bytes=999999999999999999999 data=", header.Replace("data=", "data =", StringComparison.Ordinal)
        })
            Reject("NativeAotSuspensionSmoke", Encoding.UTF8.GetBytes(changed + Suspension + "\r\n"));
        Reject("NativeAotSuspensionSmoke", valid[..^1]);
        Reject("NativeAotSuspensionSmoke", Encoding.UTF8.GetBytes(serial[..^2] + "\n"));
        Reject("NativeAotSuspensionSmoke", Encoding.UTF8.GetBytes("embedded " + serial));
        Reject("NativeAotSuspensionSmoke", Encoding.UTF8.GetBytes("M16_009_NOTE embedded=M16_005_WRITE\r\n" + serial));
        Reject("NativeAotSuspensionSmoke", Join(valid, Envelope("extra"u8.ToArray())));
        Reject("NativeAotSuspensionSmoke", Join(valid, "M16_005_WRITE"u8.ToArray()));
        byte[] badPayload = (byte[])payload.Clone();
        badPayload[0] = 0xc0;
        badPayload[1] = 0xaf;
        Reject("NativeAotSuspensionSmoke", Envelope(badPayload));
        Reject("NativeAotSuspensionSmoke", Join(new byte[] { 0xc0, 0xaf, 13, 10 }, valid));
        Reject("NativeAotSuspensionSmoke", Join(valid, Envelope(new byte[4096])));
        Reject("NativeAotSuspensionSmoke", new byte[8 * 1024 * 1024 + 1]);
        foreach (string application in new[] { "", "nativeaotsuspensionsmoke", "NativeAotHello" })
            Reject(application, valid);
    }

    [HostTest("nativeaot.threading-output requires complete successful app output and returns immutable independent captures")]
    private static void CompleteAndImmutable()
    {
        foreach (var app in Applications())
        {
            foreach (string invalid in new[] {"", app.Text + "\n", app.Text.Replace("TEST_PASS", "TEST_FAIL", StringComparison.Ordinal),
                app.Text[..app.Text.LastIndexOf("TEST_PASS", StringComparison.Ordinal)], app.Text + "extra\n"})
                Reject(app.Name, invalid.Length == 0 ? Array.Empty<byte>() : Envelope(Encoding.UTF8.GetBytes(invalid)));
            byte[] raw = Envelope(Encoding.UTF8.GetBytes(app.Text));
            var capture = NativeAotThreadingOutput.Read(app.Name, raw);
            string normalized = capture.NormalizedTail;
            int length = capture.WriteLengths[0];
            HostCheck.True(capture.WriteLengths is not int[]);
            if (capture.WriteLengths is IList<int> list)
            {
                bool rejected = false;
                try
                {
                    list[0] = 1;
                }
                catch (NotSupportedException)
                {
                    rejected = true;
                }
                HostCheck.True(rejected);
            }
            Array.Fill(raw, (byte)0);
            HostCheck.Equal(app.Text, capture.Stdout);
            HostCheck.Equal(normalized, capture.NormalizedTail);
            HostCheck.Equal(length, capture.WriteLengths[0]);
        }
        Reject("NativeAotSuspensionSmoke", Envelope(Encoding.UTF8.GetBytes(ThreadPool)));
        Reject("NativeAotThreadPoolSmoke", Envelope(Encoding.UTF8.GetBytes(ThreadPool.Replace("stale=0", "stale=1", StringComparison.Ordinal))));
        Reject("NativeAotSuspensionSmoke", Envelope(Encoding.UTF8.GetBytes(Suspension.Replace("progressAfter=16", "progressAfter=15", StringComparison.Ordinal))));
    }

    [HostTest("nativeaot.threading-output size rejection retains exact reason and bounded scalar diagnostics")]
    private static void SizeFailureDetails()
    {
        void Rejected(byte[] raw, bool full, string condition, int declared, int remaining, int stdout, int stderr, int writes, int limit)
        {
            try
            {
                _ = NativeAotThreadingOutput.ReadWeb(raw, full);
                throw new InvalidOperationException("accepted size overflow");
            }
            catch (ToolFailure error)
            {
                HostCheck.Equal("nativeaot_threading_raw_size", error.Message);
                HostCheck.Equal(9, error.Data.Count);
                HostCheck.Equal(condition, (string)error.Data["condition"]!);
                HostCheck.Equal(full ? "disk-static-full" : "web", (string)error.Data["selectedMode"]!);
                HostCheck.Equal(declared, (int)error.Data["declaredPayloadLength"]!);
                HostCheck.Equal(remaining, (int)error.Data["bytesRemaining"]!);
                HostCheck.Equal(stdout, (int)error.Data["stdoutBytes"]!);
                HostCheck.Equal(stderr, (int)error.Data["stderrBytes"]!);
                HostCheck.Equal(writes, (int)error.Data["writeCount"]!);
                HostCheck.Equal(limit, (int)error.Data["applicableLimit"]!);
                HostCheck.True(error.Data["inputOffset"] is int offset && offset >= 0 && offset <= raw.Length);
                HostCheck.True(error.Data.Values.Cast<object>().All(value => value is int or string));
            }
        }
        byte[] oversized = new byte[8 * 1024 * 1024 + 1];
        Rejected(oversized, false, "raw extent", 0, oversized.Length, 0, 0, 0, 8 * 1024 * 1024);
        foreach (var policy in new[] { (Full: false, CombinedBytes: 65536), (Full: true, CombinedBytes: 131072) })
        {
            bool full = policy.Full;
            Rejected(Envelope([(byte)'x'])[..^1], full, "payload exceeds remaining", 1, 0, 0, 0, 0, 0);
            using MemoryStream output = new();
            byte[] chunk = Enumerable.Repeat((byte)'x', 8192).ToArray();
            int chunks = policy.CombinedBytes / 8192;
            for (int i = 0; i < chunks; i++)
            {
                string header = Header(chunk.Length);
                if ((i & 1) != 0)
                    header = header.Replace("fd=1", "fd=2", StringComparison.Ordinal);
                output.Write(Join(Encoding.UTF8.GetBytes(header), chunk, [13, 10]));
            }
            var boundary = NativeAotThreadingOutput.ReadWeb(output.ToArray(), full);
            HostCheck.Equal(policy.CombinedBytes / 2, boundary.Stdout.Length);
            HostCheck.Equal(policy.CombinedBytes / 2, boundary.Stderr.Length);
            HostCheck.Equal(chunks, boundary.WriteLengths.Count);
            output.Write(Envelope([(byte)'y']));
            Rejected(output.ToArray(), full, "combined stdout+stderr", 1, 1, policy.CombinedBytes / 2, policy.CombinedBytes / 2, chunks, policy.CombinedBytes);
            if (!full)
            {
                var expanded = NativeAotThreadingOutput.ReadWeb(output.ToArray(), true);
                HostCheck.Equal(65537, expanded.Stdout.Length + expanded.Stderr.Length);
            }
            using MemoryStream writes = new();
            byte[] one = Envelope([(byte)'x']);
            for (int i = 0; i < 4096; i++)
                writes.Write(one);
            HostCheck.Equal(4096, NativeAotThreadingOutput.ReadWeb(writes.ToArray(), full).WriteLengths.Count);
            writes.Write(one);
            Rejected(writes.ToArray(), full, "write count", 1, 1, 4096, 0, 4096, 4096);
        }
    }

    private static (string Name, string Text)[] Applications() => [("NativeAotSuspensionSmoke", Suspension), ("NativeAotThreadPoolSmoke", ThreadPool)];
    private static string Header(int bytes) => "M16_005_WRITE fd=1 bytes=" + bytes.ToString(CultureInfo.InvariantCulture) + " data=";
    private static byte[] Envelope(byte[] bytes) => Join(Encoding.UTF8.GetBytes(Header(bytes.Length)), bytes, new byte[] { 13, 10 });
    private static byte[] Join(params byte[][] parts)
    {
        using MemoryStream result = new();
        foreach (byte[] part in parts)
            result.Write(part);
        return result.ToArray();
    }
    private static string WithoutNewlines(string text) => text.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
    private static void Reject(string application, byte[] raw)
    {
        bool rejected = false;
        try
        {
            _ = NativeAotThreadingOutput.Read(application, raw);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        catch (DecoderFallbackException)
        {
            rejected = true;
        }
        HostCheck.True(rejected, "strict raw threading output admission");
    }
}
