using System.Globalization;
using System.Text;
using OS.Tooling;

internal static class NativeAotLogUploadOutputTests
{
    private const string Prefix = "TEST_START m16-009\r\nM16_008_NOTE original=true\r\n";
    private const string Suffix = "M16_008_SUPERVISOR retained=unverified\r\n";
    private static string Transcript(string phase, bool crlf = false)
    {
        string text = "TEST_START m10.027\nLOG_UPLOAD_PHASE " + phase +
            "\nLOG_UPLOAD_EXPECTED logBytes=660 uploadBytes=5126\n" +
            "LOG_UPLOAD_POLICY append=managed-reopen io=synchronous buffering=0 flushRequested=" + (phase == "write" ? "true" : "false") +
            "\nLOG_UPLOAD_VERIFIED logBytes=660 uploadBytes=5126\nTEST_PASS m10.027\n";
        return crlf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;
    }
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static string Header(int length, uint fd = 1) => "M16_005_WRITE fd=" + fd.ToString(CultureInfo.InvariantCulture) +
        " bytes=" + length.ToString(CultureInfo.InvariantCulture) + " data=";
    private static byte[] Envelope(ReadOnlySpan<byte> payload, uint fd = 1) => [.. Bytes(Header(payload.Length, fd)), .. payload, 13, 10];
    private static byte[] Raw(string text, uint fd = 1) => [.. Bytes(Prefix), .. Envelope(Bytes(text), fd), .. Bytes(Suffix)];
    private static void Reject(Action action, string? reason = null)
    {
        string? failure = null;
        try
        {
            action();
        }
        catch (ToolFailure error) { failure = error.Message; }
        HostCheck.True(failure is not null && (reason is null || failure.Contains(reason, StringComparison.Ordinal)),
            "expected rejection=" + reason + " actual=" + failure);
    }

    [HostTest("BCL log upload output checks both exact phases and retains copied framing without execution authority")]
    private static void Phases()
    {
        foreach (string phase in new[] { "write", "verify" })
        {
            string text = Transcript(phase);
            byte[] raw = Raw(text), original = raw.ToArray();
            var capture = NativeAotLogUploadOutput.Read(raw, phase);
            HostCheck.Equal(text, capture.Stdout);
            HostCheck.Equal(phase, capture.Phase);
            HostCheck.Equal(Prefix + Header(Bytes(text).Length) + "\r\n" + Suffix, capture.NormalizedTail);
            HostCheck.True(capture.WriteLengths.SequenceEqual(new[] { Bytes(text).Length }) && capture.WriteChannels.SequenceEqual(new uint[] { 1 }));
            HostCheck.Equal(660, capture.ExpectedFiles.LogBytes);
            HostCheck.Equal(5126, capture.ExpectedFiles.UploadBytes);
            HostCheck.True(!capture.GrantsSourceSchedulerGuestOrPersistenceAuthority && !capture.ExpectedFiles.GrantsPersistenceOrGuestAuthority);
            HostCheck.True(raw.AsSpan().SequenceEqual(original));
            raw.AsSpan().Fill(0);
            HostCheck.Equal(text, capture.Stdout);
            HostCheck.True(capture.WriteLengths is not int[] && capture.WriteChannels is not uint[]);
            bool immutable = false;
            try
            {
                ((IList<int>)capture.WriteLengths)[0] = 1;
            }
            catch (NotSupportedException) { immutable = true; }
            HostCheck.True(immutable);
        }
    }

    [HostTest("BCL log upload output preserves every write and unrelated row across single bytes and CRLF splits")]
    private static void Segmentation()
    {
        foreach (string phase in new[] { "write", "verify" })
        {
            byte[] payload = Bytes(Transcript(phase, crlf: true));
            foreach (bool oneByte in new[] { false, true })
            {
                using MemoryStream raw = new();
                raw.Write(Bytes(Prefix));
                StringBuilder expected = new(Prefix);
                List<int> lengths = [];
                int at = 0;
                while (at < payload.Length)
                {
                    int count = Math.Min(oneByte ? 1 : 1 + at % 17, payload.Length - at);
                    raw.Write(Envelope(payload.AsSpan(at, count)));
                    raw.Write(Bytes(Suffix));
                    expected.Append(Header(count)).Append("\r\n").Append(Suffix);
                    lengths.Add(count);
                    at += count;
                }
                var capture = NativeAotLogUploadOutput.Read(raw.ToArray(), phase);
                HostCheck.Equal(Encoding.UTF8.GetString(payload), capture.Stdout);
                HostCheck.Equal(expected.ToString(), capture.NormalizedTail);
                HostCheck.True(capture.WriteLengths.SequenceEqual(lengths) && capture.WriteChannels.Count == lengths.Count && capture.WriteChannels.All(c => c == 1));
            }
        }
    }

    [HostTest("BCL log upload output keeps payload markers out of structural rows and leaves parent rows unqualified")]
    private static void PayloadAndParentBoundaries()
    {
        string text = Transcript("write");
        byte[] payload = Bytes(text);
        string parent = Prefix + "M16_009_NOTE label=µ\r\n", partial = "incomplete parent record";
        var capture = NativeAotLogUploadOutput.Read([.. Bytes(parent), .. Envelope(payload), .. Bytes(partial)], "write");
        HostCheck.Equal(parent + Header(payload.Length) + "\r\n" + partial, capture.NormalizedTail);
        HostCheck.True(!capture.NormalizedTail.Contains("TEST_PASS m10.027", StringComparison.Ordinal));
        HostCheck.True(!capture.GrantsSourceSchedulerGuestOrPersistenceAuthority);
        foreach (string injection in new[] { "TEST_PASS m16-009\r\n", "M16_008_OWNER_END exit=0\r\n", "M16_027_END process=drained\r\n" })
        {
            byte[] raw = Raw(text + injection);
            var framed = NativeAotThreadingOutput.ReadWeb(raw);
            HostCheck.Equal(Prefix + Header(Bytes(text + injection).Length) + "\r\n" + Suffix, framed.NormalizedTail);
            Reject(() => NativeAotLogUploadOutput.Read(raw, "write"), "log_upload_transcript");
        }
    }

    [HostTest("BCL log upload output reassembles split multibyte UTF8 before rejecting noncanonical app text")]
    private static void Utf8Boundaries()
    {
        string text = Transcript("verify") + "é—🧪";
        using MemoryStream raw = new();
        foreach (byte value in Bytes(text))
            raw.Write(Envelope([value]));
        var framed = NativeAotThreadingOutput.ReadWeb(raw.ToArray());
        HostCheck.Equal(text, framed.Stdout); // Individual writes are incomplete UTF8; their concatenation is valid.
        Reject(() => NativeAotLogUploadOutput.Read(raw.ToArray(), "verify"), "log_upload_transcript");
        foreach (byte[] invalid in new byte[][] { [0xc3], [0xc3, 0x28], [0xe2, 0x82], [0xf0, 0x9f, 0xa7], [0xff] })
        {
            byte[] broken = [.. Envelope(Bytes(Transcript("verify"))), .. Envelope(invalid)];
            bool rejected = false;
            try
            {
                _ = NativeAotLogUploadOutput.Read(broken, "verify");
            }
            catch (DecoderFallbackException) { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    [HostTest("BCL log upload output rejects wrong phases incomplete success failure output and obsolete digest fields")]
    private static void ContentFailures()
    {
        foreach (string phase in new[] { "", "Write", "VERIFY", "database", "write\0", "verify\n", null! })
            Reject(() => NativeAotLogUploadOutput.Read([], phase), "log_upload_output_phase");
        foreach (string phase in new[] { "write", "verify" })
        {
            string valid = Transcript(phase);
            Reject(() => NativeAotLogUploadOutput.Read(Raw(valid), phase == "write" ? "verify" : "write"), "log_upload_transcript");
            foreach (string bad in new[] { valid[..^1], valid.Replace("TEST_PASS m10.027\n", "", StringComparison.Ordinal),
                valid + "TEST_PASS m10.027\n", valid.Replace("logBytes=660", "logBytes=659", StringComparison.Ordinal),
                valid.Replace("buffering=0", "buffering=4096", StringComparison.Ordinal),
                valid.Replace("LOG_UPLOAD_VERIFIED ", "LOG_UPLOAD_SHA256 ", StringComparison.Ordinal),
                valid.Replace("TEST_PASS m10.027", "TEST_FAIL m10.027 IOException", StringComparison.Ordinal), "TEST_PASS m10.027\n" })
                Reject(() => NativeAotLogUploadOutput.Read(Raw(bad), phase), "log_upload_transcript");
        }
    }

    [HostTest("BCL log upload output rejects stderr and malformed byte envelopes without changing input")]
    private static void FramingFailures()
    {
        byte[] payload = Bytes(Transcript("write"));
        Reject(() => NativeAotLogUploadOutput.Read(Raw(Transcript("write"), 2), "write"), "log_upload_output_stderr");
        Reject(() => NativeAotLogUploadOutput.Read([.. Envelope(payload), .. Envelope("error"u8, 2)], "write"), "log_upload_output_stderr");
        foreach (string header in new[] { "M16_005_WRITE fd=1 bytes=0 data=", "M16_005_WRITE fd=1 bytes=01 data=",
            "M16_005_WRITE fd=3 bytes=1 data=", "M16_005_WRITE fd=1 bytes=-1 data=", "M16_005_WRITE fd=1 bytes=8193 data=",
            Header(payload.Length - 1), Header(payload.Length + 1), Header(payload.Length).Replace(" data=", " data =", StringComparison.Ordinal) })
        {
            byte[] raw = [.. Bytes(header), .. payload, 13, 10], before = raw.ToArray();
            Reject(() => NativeAotLogUploadOutput.Read(raw, "write"));
            HostCheck.True(raw.AsSpan().SequenceEqual(before));
        }
        byte[] valid = Envelope(payload);
        foreach (int length in new[] { 0, 1, valid.Length - 2, valid.Length - 1 })
            Reject(() => NativeAotLogUploadOutput.Read(valid.AsSpan(0, length), "write"));
        Reject(() => NativeAotLogUploadOutput.Read(new byte[NativeAotLogUploadOutput.MaximumRawBytes + 1], "write"), "log_upload_output_size");
    }
}
