using System.Globalization;
using System.Text;
using OS.Tooling;

internal static class NativeAotRuntimeOutputTests
{
    private const string Retained = "M16_009_NOTE label=µ\r\n";

    [HostTest("nativeaot.runtime-output decodes three complete independent synthetic outputs without changing raw input")]
    private static void Complete()
    {
        foreach (var app in Vectors())
        {
            byte[] payload = Encoding.UTF8.GetBytes(app.Text);
            HostCheck.True(payload.Length < 4096);
            byte[] raw = Join(Encoding.UTF8.GetBytes(Retained), Envelope(payload), Encoding.UTF8.GetBytes(Retained));
            byte[] before = (byte[])raw.Clone();
            var capture = NativeAotThreadingOutput.Read(app.Name, raw);
            HostCheck.Equal(app.Text, capture.Stdout);
            HostCheck.Equal(Retained + Header(payload.Length) + WithoutNewlines(app.Text) + "\r\n" + Retained, capture.NormalizedTail);
            HostCheck.True(capture.WriteLengths.SequenceEqual(new[] { payload.Length }) && raw.AsSpan().SequenceEqual(before));
            HostCheck.True(capture.WriteLengths is not int[]);
            if (capture.WriteLengths is IList<int> list)
            {
                bool rejected = false;
                try
                {
                    list[0] = 1;
                }
                catch (NotSupportedException) { rejected = true; }
                HostCheck.True(rejected);
            }
            Array.Fill(raw, (byte)0);
            HostCheck.Equal(app.Text, capture.Stdout);
            HostCheck.Equal(payload.Length, capture.WriteLengths[0]);
        }
    }

    [HostTest("nativeaot.runtime-output retains segmented rows CRLF splits coalesced rows and unrelated UTF8 records")]
    private static void Segmentation()
    {
        foreach (var app in Vectors())
        {
            string text = app.Text.Replace("\n", "\r\n", StringComparison.Ordinal);
            byte[] payload = Encoding.UTF8.GetBytes(text);
            int carriage = Array.IndexOf(payload, (byte)'\r');
            int[] lengths = [1, carriage, 1, payload.Length - carriage - 2];
            using MemoryStream raw = new();
            StringBuilder normalized = new();
            int cursor = 0;
            foreach (int length in lengths)
            {
                byte[] part = payload.AsSpan(cursor, length).ToArray();
                cursor += length;
                raw.Write(Envelope(part));
                raw.Write(Encoding.UTF8.GetBytes(Retained));
                normalized.Append(Header(length)).Append(WithoutNewlines(Encoding.UTF8.GetString(part))).Append("\r\n").Append(Retained);
            }
            byte[] bytes = raw.ToArray(), before = (byte[])bytes.Clone();
            var capture = NativeAotThreadingOutput.Read(app.Name, bytes);
            HostCheck.Equal(text, capture.Stdout);
            HostCheck.Equal(normalized.ToString(), capture.NormalizedTail);
            HostCheck.True(capture.WriteLengths.SequenceEqual(lengths) && bytes.AsSpan().SequenceEqual(before));
        }
    }

    [HostTest("nativeaot.runtime-output rejects wrong app incomplete failed extra malformed and invalid UTF8 captures")]
    private static void Failures()
    {
        var apps = Vectors();
        for (int index = 0; index < apps.Length; index++)
        {
            var app = apps[index];
            byte[] payload = Encoding.UTF8.GetBytes(app.Text), valid = Envelope(payload);
            foreach (string invalid in new[] { "", app.Text[(app.Text.IndexOf('\n') + 1)..],
                app.Text[..app.Text.LastIndexOf("TEST_PASS", StringComparison.Ordinal)], app.Text + "extra\n",
                app.Text + "\n", app.Text.Replace("TEST_PASS", "TEST_FAIL", StringComparison.Ordinal),
                app.Text.Replace("joined=5", "joined=4", StringComparison.Ordinal).Replace("stale=0", "stale=1", StringComparison.Ordinal) })
                Reject(app.Name, Envelope(Encoding.UTF8.GetBytes(invalid)));
            Reject(apps[(index + 1) % apps.Length].Name, valid);
            Reject(app.Name.ToLowerInvariant(), valid);
            foreach (string header in new[] { Header(payload.Length).Replace("fd=1", "fd=2", StringComparison.Ordinal),
                Header(payload.Length - 1), Header(payload.Length + 1), Header(4097),
                "M16_005_WRITE fd=1 bytes=0" + payload.Length.ToString(CultureInfo.InvariantCulture) + " data=" })
                Reject(app.Name, Join(Encoding.UTF8.GetBytes(header), payload, new byte[] { 13, 10 }));
            Reject(app.Name, valid[..^1]);
            Reject(app.Name, Join(Encoding.UTF8.GetBytes(Header(payload.Length)), payload, new byte[] { 10 }));
            Reject(app.Name, Join("embedded "u8.ToArray(), valid));
            Reject(app.Name, Join("M16_009_NOTE embedded=M16_005_WRITE\r\n"u8.ToArray(), valid));
            Reject(app.Name, Join(valid, Envelope("extra\n"u8.ToArray())));
            Reject(app.Name, Join(valid, Envelope(new byte[4096])));
            byte[] bad = (byte[])payload.Clone();
            bad[0] = 0xc0;
            bad[1] = 0xaf;
            Reject(app.Name, Envelope(bad));
            Reject(app.Name, Join(new byte[] { 0xc0, 0xaf, 13, 10 }, valid));
        }
        Reject("NativeAotRuntimeSmoke", Envelope(Encoding.UTF8.GetBytes(apps[0].Text)));
    }

    // Independent complete host observations, matching the existing validator
    // controls. These are synthetic inputs, with no artifact or execution claim.
    private static (string Name, string Text)[] Vectors()
    {
        StringBuilder gc = new("TEST_START nativeaot.gc-stress\n"), tp = new("TEST_START nativeaot.tp-stress\n"), matrix = new("TEST_START nativeaot.runtime-matrix\n");
        for (int round = 1; round <= 3; round++)
        {
            for (int epoch = 1; epoch <= 3; epoch++)
            {
                int before = (round - 1) * 3 + epoch - 1, progress = epoch * 10;
                gc.Append(FormattableString.Invariant($"NATIVEAOT_GC_STRESS_COLLECT lifetime={round} epoch={epoch} before={before} after={before + 1} progressBefore={progress} progressReturned={progress + 1} progressAfter={progress + 2} waiting=3\n"));
            }
            gc.Append(FormattableString.Invariant($"NATIVEAOT_GC_STRESS_JOIN lifetime={round} started=5 joined=5 completed=5 roots=5 interior=5 tls=5 sleepStarts=1 exitRequests=1\n"));
            int timer = round * 100;
            tp.Append(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_WORK lifetime={round} tasks=8 completed=8 checksum=204 activeMax=2 inFlight=0 mask=255 workers=2 threadPool=true\n"));
            tp.Append(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_CANCEL lifetime={round} before=2 during=2 workCallbacks=0 recovered=1\n"));
            tp.Append(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_RACE lifetime={round} outcome=result winners=1 duplicates=2 continuations=3 inline=0 value=43\n"));
            tp.Append(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_TIMER lifetime={round} before={timer} fired={timer + 25} drained={timer + 45} after={timer + 70} frequency=1000 callbacks=1 completed=1 stale=0 never=0 pendingDrain=true\n"));
            int collection = round * 3 - 2, running = round * 100, clock = round * 1000;
            matrix.Append(FormattableString.Invariant($"NATIVEAOT_MATRIX_EH round={round} filters=1 catches=2 cleanups=2 order=12 identity=true\n"));
            matrix.Append(FormattableString.Invariant($"NATIVEAOT_MATRIX_CCTOR round={round} attempts=2 total={round * 2} runs=1 typed=true innerIdentity=true\n"));
            matrix.Append(FormattableString.Invariant($"NATIVEAOT_MATRIX_DELEGATES round={round} closed={round + 50} closure={round + 47} calls=2 actions=3 order=122 sum=19\n"));
            matrix.Append(FormattableString.Invariant($"NATIVEAOT_MATRIX_GC round={round} collections=2 before={collection} after={collection + 2} workers=2 joined=2 freshTls=2 roots=2 interior=2 tls=2 progressBefore={running} progressReturned={running + 10} progressAfter={running + 20} finalizedMask={(1 << round) - 1} duplicateFinalizers=0\n"));
            matrix.Append(FormattableString.Invariant($"NATIVEAOT_MATRIX_ASYNC round={round} tasks=4 completed=4 checksum=30 workers=2 canceledBefore=1 canceledDuring=1 workCallbacks=0 delayBefore={clock} delayAfter={clock + 50} timerBefore={clock + 100} fired={clock + 150} drained={clock + 160} after={clock + 210} frequency=1000 callbacks=1 stale=0 disposed=true\n"));
        }
        return [("NativeAotSuspensionStress", gc.Append("TEST_PASS nativeaot.gc-stress\n").ToString()),
            ("NativeAotThreadPoolStress", tp.Append("TEST_PASS nativeaot.tp-stress\n").ToString()),
            ("NativeAotRuntimeMatrix", matrix.Append("TEST_PASS nativeaot.runtime-matrix\n").ToString())];
    }

    private static string Header(int bytes) => "M16_005_WRITE fd=1 bytes=" + bytes.ToString(CultureInfo.InvariantCulture) + " data=";
    private static string WithoutNewlines(string text) => text.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
    private static byte[] Envelope(byte[] bytes) => Join(Encoding.UTF8.GetBytes(Header(bytes.Length)), bytes, new byte[] { 13, 10 });
    private static byte[] Join(params byte[][] parts)
    {
        using MemoryStream result = new();
        foreach (byte[] part in parts)
            result.Write(part);
        return result.ToArray();
    }
    private static void Reject(string application, byte[] raw)
    {
        bool rejected = false;
        try
        {
            _ = NativeAotThreadingOutput.Read(application, raw);
        }
        catch (ToolFailure) { rejected = true; }
        catch (DecoderFallbackException) { rejected = true; }
        HostCheck.True(rejected, "strict raw runtime output admission");
    }
}
