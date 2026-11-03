using System.Security.Cryptography;
using System.Text;
using OS.Tooling;

internal static class NativeAotWebObservationTests
{
    private const string Output = "log\nTEST_PASS m10.004\nM16_008_CALL bogus\n";
    private static byte[] Frame(uint fd, byte[] payload) => Join(Encoding.ASCII.GetBytes($"M16_005_WRITE fd={fd} bytes={payload.Length} data="), payload, "\r\n"u8.ToArray());
    private static byte[] Join(params byte[][] parts)
    {
        using MemoryStream stream = new();
        foreach (byte[] part in parts)
            stream.Write(part);
        return stream.ToArray();
    }
    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (Exception e) when (e is ToolFailure or DecoderFallbackException)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
    private static string Call(int ordinal, int number, int fd, int result) =>
        $"M16_008_CALL identity=4294967297 nr={number} a0={fd} a1=4096 a2=1024 a3=0 a4=0 a5=0 rip=4194818 rsp=1879048176 flags=514 fs=0 gs=0 clock=10 valueBits={result} action=1 status=1 ordinal={ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={result}\r\n";
    private static byte[] Prefix(string fragment = "M16_008_CALL identity=4294967297 nr=202 a0=4")
    {
        int bytes = Encoding.UTF8.GetByteCount(Output);
        string start = "TEST_START m10.004\r\nTEST_START m16-009\r\n" +
            "M10_004_ENTER identity=4294967297 rip=4194818 argc=5 envc=3 configBytes=51\r\n" + Call(1, 9, 0, 4096) +
            "M10_004_CONFIG fd=3 bytes=51 source=immutable-package copied=user\r\n" + Call(2, 17, 3, 51) +
            "M10_004_LISTEN fd=6 address=0 port=8080 owner=live\r\n" + Call(3, 50, 6, 0);
        string end = $"M10_004_LIVE argc=5 envc=3 configBytes=51 bindFd=6 stdoutBytes={bytes} stderrBytes=0 active=1 clock=10 owner=retained externalHttp=required\r\n" +
            "CLROS_M10_004_READY\r\nTEST_PASS m10.004\r\n" + Call(4, 1, 1, bytes) + fragment;
        return Join(Encoding.ASCII.GetBytes(start), Frame(1, Encoding.UTF8.GetBytes(Output)), Encoding.ASCII.GetBytes(end));
    }
    private static QemuTranscripts.WebObservationReport Inspect(byte[] raw) => QemuTranscripts.InspectNativeAotWebObservationsForTests(raw, 5, 3, 51);

    [HostTest("nativeaot.web output preserves segmented channel UTF8 and removes all payload from structural rows")]
    private static void Channels()
    {
        byte[] raw = Join("TEST_START m10.004\r\n"u8.ToArray(), Frame(1, [0xc2]), Frame(2, "error\nTEST_FAIL fake\n"u8.ToArray()),
            Frame(1, [0xb5, 10]), "M16_008_CALL identity="u8.ToArray());
        byte[] before = (byte[])raw.Clone();
        var output = NativeAotThreadingOutput.ReadWeb(raw);
        HostCheck.Equal("µ\n", output.Stdout);
        HostCheck.Equal("error\nTEST_FAIL fake\n", output.Stderr);
        HostCheck.True(output.WriteChannels.SequenceEqual(new uint[] { 1, 2, 1 }) && output.WriteLengths.SequenceEqual(new[] { 1, 21, 2 }));
        HostCheck.True(!output.NormalizedTail.Contains("TEST_FAIL", StringComparison.Ordinal) && !output.NormalizedTail.Contains('µ') &&
            output.NormalizedTail.EndsWith("M16_008_CALL identity=", StringComparison.Ordinal) && raw.AsSpan().SequenceEqual(before));
    }

    [HostTest("nativeaot.web output rejects malformed channels lengths framing invalid UTF8 and combined quota overflow")]
    private static void Bounds()
    {
        byte[] maximum = Join(Enumerable.Range(0, 8).Select(i => Frame((uint)(i % 2 + 1), new byte[8192])).ToArray());
        var output = NativeAotThreadingOutput.ReadWeb(maximum);
        HostCheck.Equal(32768, output.Stdout.Length);
        HostCheck.Equal(32768, output.Stderr.Length);
        Reject(() => NativeAotThreadingOutput.ReadWeb(Join(maximum, Frame(1, [1]))));
        foreach (byte[] raw in new[]{Frame(3,[1]),Frame(1,new byte[8193]),Frame(1,[0xc0]),
            "M16_005_WRITE fd=1 bytes=01 data=x\r\n"u8.ToArray(),"M16_005_WRITE fd=1 bytes=0 data=\r\n"u8.ToArray(),
            "M16_005_WRITE fd=1 bytes=2 data=x\r\n"u8.ToArray(),"M16_005_WRITE fd=1 bytes=1 data=x\n"u8.ToArray(),
            "prefix M16_005_WRITE fd=1 bytes=1 data=x\r\n"u8.ToArray()})
            Reject(() => NativeAotThreadingOutput.ReadWeb(raw));
        Reject(() => NativeAotThreadingOutput.Read("NativeAotSocketSmoke", Frame(2, [1])));
    }

    [HostTest("nativeaot.web observations retain complete-prefix hashes and incomplete row without inventing a completed call")]
    private static void CompletePrefix()
    {
        byte[] raw = Prefix();
        byte[] before = (byte[])raw.Clone();
        var report = Inspect(raw);
        HostCheck.Equal(4, report.Calls);
        HostCheck.Equal(1, report.Writes);
        HostCheck.Equal(Encoding.UTF8.GetByteCount(Output), report.StdoutBytes);
        HostCheck.Equal(0, report.StderrBytes);
        HostCheck.Equal(0, report.PendingEpollWaits);
        HostCheck.Equal("M16_008_CALL identity=4294967297 nr=202 a0=4", report.TrailingFragment);
        HostCheck.Equal(Convert.ToHexString(SHA256.HashData(raw)), report.RawSha256);
        HostCheck.Equal(Convert.ToHexString(SHA256.HashData(raw.AsSpan(0, raw.Length - report.TrailingFragment.Length))), report.CompletePrefixSha256);
        HostCheck.True(raw.AsSpan().SequenceEqual(before));
        HostCheck.Equal("", Inspect(Prefix("")).TrailingFragment);
    }

    [HostTest("nativeaot.web observations reject forged payload markers orphan writes mismatched counters and missing call suffixes")]
    private static void Binding()
    {
        string raw = Encoding.UTF8.GetString(Prefix(""));
        foreach (string changed in new[]{
            raw.Replace("CLROS_M10_004_READY\r\nTEST_PASS m10.004\r\n", "",StringComparison.Ordinal),
            raw.Replace("stdoutBytes="+Encoding.UTF8.GetByteCount(Output),"stdoutBytes=1",StringComparison.Ordinal),
            raw.Replace("M10_004_CONFIG fd=3", "M10_004_CONFIG fd=4",StringComparison.Ordinal),
            raw.Replace("M10_004_LISTEN fd=6", "M10_004_LISTEN fd=7",StringComparison.Ordinal),
            raw.Replace("ordinal=2 ","ordinal=3 ",StringComparison.Ordinal),
            raw.Replace(" returnValueBits=51", " returnValueBits=52",StringComparison.Ordinal),
            raw.Replace(Call(4,1,1,Encoding.UTF8.GetByteCount(Output)),"",StringComparison.Ordinal),
            raw+"M16_012_WAKE identity=4294967297 sequence=1 completion=1 state=1 clock=10\r\n",
            raw+"TEST_FAIL m10.004 failure\r\n",raw+"M16_008_OWNER_END fabricated\r\n"})
            Reject(() => Inspect(Encoding.UTF8.GetBytes(changed)));
        string scalar = Call(4, 1, 1, Encoding.UTF8.GetByteCount(Output));
        foreach (int requested in new[] { 0, 1, 65537 })
            Reject(() => Inspect(Encoding.UTF8.GetBytes(raw.Replace(scalar, scalar.Replace("a2=1024 ", $"a2={requested} ", StringComparison.Ordinal), StringComparison.Ordinal))));
        string vector = Call(4, 20, 1, Encoding.UTF8.GetByteCount(Output));
        HostCheck.Equal(1, Inspect(Encoding.UTF8.GetBytes(raw.Replace(scalar, vector.Replace("a2=1024 ", "a2=1 ", StringComparison.Ordinal), StringComparison.Ordinal))).Writes);
        foreach (int count in new[] { 0, 17 })
            Reject(() => Inspect(Encoding.UTF8.GetBytes(raw.Replace(scalar, vector.Replace("a2=1024 ", $"a2={count} ", StringComparison.Ordinal), StringComparison.Ordinal))));
    }

    [HostTest("nativeaot.web observations reject malformed trailing fragments and retain only canonical kernel record prefixes")]
    private static void Fragments()
    {
        foreach (string fragment in new[]{"TEST_FAIL m10.004","M16_008_CALL identity=01","M16_008_CALL identity=1 nr=-2",
            "M16_008_CALL nr=1","M16_008_CALL identity=18446744073709551616","M16_005_WRITE fd=1","M10_004_LIVE x=1", "M16_008_CALL\n"})
            Reject(() => Inspect(Prefix(fragment)));
        foreach (string fragment in new[]{"M16_008_CALL","M16_008_CALL ","M16_008_CALL id","M16_008_CALL identity=",
            "M16_012_POLL identity=0 clock=10 ready=1 idle=0","M16_008_SYSCALL_TIME identity=4294967297 ordinal=4 nr="})
            HostCheck.Equal(fragment, Inspect(Prefix(fragment)).TrailingFragment);
    }
}
