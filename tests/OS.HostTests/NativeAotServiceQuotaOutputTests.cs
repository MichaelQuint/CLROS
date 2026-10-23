using OS.Tooling;

internal static class NativeAotServiceQuotaOutputTests
{
    private const string Gc = "limit=16777216 baselineHeap=30920 baselineCommitted=151552 liveBytes=2097152 liveHeap=2200000 liveCommitted=2300000 pressureBytes=8388608 pressureHeap=8500000 pressureCommitted=9000000 pressureCollections=4 oom=1";
    private static string Output(int completed = 2)
    {
        var rows = new List<string> { "CLROS_APP_ENTRY minimal.api.service", "CLROS_APP_CCTOR minimal.api.service",
            "CLROS_APP_CTOR minimal.api.service", "CLROS_APP_BUILT minimal.api.service", "CLROS_SERVICE_GC_BEGIN " + Gc,
            "CLROS_SERVICE_LISTEN contract=v3-quota endpoint=0.0.0.0:8080", "CLROS_M10_011_READY",
            "CLROS_SERVICE_RANDOM_CONTROLS zero=0 invalid=-22 fault=-14",
            "CLROS_SERVICE_QUOTA_BEGIN chunkBytes=1048576 maximumChunks=64" };
        for (int index = 0; index <= completed; index++)
        {
            rows.Add($"CLROS_SERVICE_QUOTA_ATTEMPT index={index} retainedBytes={index * 1048576L} gcCommitted=9000000");
            if (index < completed) rows.Add($"CLROS_SERVICE_QUOTA_RETAINED index={index} retainedBytes={(index + 1) * 1048576L} collectionsBefore={7 + index} collectionsAfter={8 + index} gcCommitted=9000000");
        }
        return string.Join('\n', rows) + "\n";
    }

    [HostTest("quota output preserves bounded native retention forced GC and an unanswered final attempt")]
    private static void Valid()
    {
        foreach (int chunks in new[] { 1, 2, 4, 63 })
        {
            var value = QemuTranscripts.InspectNativeAotServiceQuotaOutputForTests("", Output(chunks));
            HostCheck.Equal((ulong)chunks, value.CompletedChunks);
            HostCheck.Equal((ulong)chunks + 1, value.Attempts);
            HostCheck.Equal((ulong)chunks * 1048576, value.RetainedBytes);
            HostCheck.Equal((ulong)Math.Min(chunks, 4), value.ForcedCollections);
        }
        var crlf = QemuTranscripts.InspectNativeAotServiceQuotaOutputForTests("", Output().Replace("\n", "\r\n", StringComparison.Ordinal));
        HostCheck.Equal(2UL, crlf.CompletedChunks);
    }

    [HostTest("quota output rejects wrong contract channels early death and returned or survived pressure")]
    private static void Failures()
    {
        string valid = Output();
        foreach (string text in new[] { Output(0), Output(64), valid[..^1], valid.Replace("v3-quota", "v2", StringComparison.Ordinal),
            valid + "CLROS_SERVICE_QUOTA_SURVIVED retainedBytes=2097152 released=True\n",
            valid + "CLROS_QUOTA_RETURNED errno=12\n", valid + "CLROS_APP_FAIL minimal.api.service\n",
            valid + "CLROS_SERVICE_END worker=joined\n", valid + "TEST_PASS m16-009\n",
            valid.Replace("CLROS_SERVICE_QUOTA_BEGIN", "CLROS_SERVICE_QUOTA_UNKNOWN", StringComparison.Ordinal) }) Reject("", text);
        Reject(valid, ""); Reject("CLROS_SERVICE_QUOTA_BEGIN\n", valid);
    }

    [HostTest("quota output rejects skipped duplicate reordered accounting and absent GC progress")]
    private static void Accounting()
    {
        string valid = Output();
        foreach (string text in new[] {
            valid.Replace("ATTEMPT index=2", "ATTEMPT index=1", StringComparison.Ordinal),
            valid.Replace("RETAINED index=1", "RETAINED index=0", StringComparison.Ordinal),
            valid.Replace("retainedBytes=2097152", "retainedBytes=1048576", StringComparison.Ordinal),
            valid.Replace("collectionsBefore=7", "collectionsBefore=6", StringComparison.Ordinal),
            valid.Replace("collectionsAfter=8", "collectionsAfter=7", StringComparison.Ordinal),
            valid.Replace("collectionsBefore=8", "collectionsBefore=4", StringComparison.Ordinal),
            valid.Replace("gcCommitted=9000000", "gcCommitted=16777217", StringComparison.Ordinal),
            valid.Replace("CLROS_APP_CTOR minimal.api.service\n", "", StringComparison.Ordinal),
            valid + "CLROS_SERVICE_QUOTA_ATTEMPT index=2 retainedBytes=2097152 gcCommitted=9000000\n" }) Reject("", text);
    }

    private static string Warning(string duration = "00:00:01.0036847", string timestamp = "10/22/2026 02:22:03 +00:00") =>
        "warn: Microsoft.AspNetCore.Server.Kestrel[22]\n      As of \"" + timestamp +
        "\", the heartbeat has been running for \"" + duration +
        "\" which is longer than \"00:00:01\". This could be caused by thread pool starvation.\n";

    [HostTest("quota output admits only exact paired Kestrel22 warnings with canonical UTC and bounded duration")]
    private static void HeartbeatWarnings()
    {
        // Literal first warning from preserved r05 framing; copied text only.
        string actual = Warning(), stderr = Output();
        foreach (string warning in new[] { actual, actual.Replace("\n", "\r\n", StringComparison.Ordinal),
            string.Concat(Enumerable.Repeat(actual, 91)), Warning("00:00:01.0000001"), Warning("00:15:00") })
            HostCheck.Equal(2UL, QemuTranscripts.InspectNativeAotServiceQuotaOutputForTests(warning, stderr).CompletedChunks);
        foreach (string warning in new[] {
            actual.Replace("Kestrel[22]", "Kestrel[21]", StringComparison.Ordinal),
            actual.Replace("warn:", "info:", StringComparison.Ordinal), actual[..^1],
            actual[(actual.IndexOf('\n') + 1)..], actual[..(actual.IndexOf('\n') + 1)], actual + "extra\n",
            actual.Replace("      As of", "     As of", StringComparison.Ordinal),
            actual.Replace("longer than \"00:00:01\"", "longer than \"00:00:02\"", StringComparison.Ordinal),
            actual.Replace("starvation.", "starvation!", StringComparison.Ordinal),
            actual.Replace("As of", "As \u00e9f", StringComparison.Ordinal), actual.Replace("\n", "\r", StringComparison.Ordinal),
            Warning(timestamp: "2026/10/22 02:22:03 +00:00"), Warning(timestamp: "10/22/2026 02:22:03 +01:00"),
            Warning(timestamp: "02/29/2025 02:22:03 +00:00"), Warning(timestamp: "1/22/2026 02:22:03 +00:00"),
            Warning("00:00:00"), Warning("00:00:01"), Warning("-00:00:02"), Warning("00:15:00.0000001"),
            Warning("00:00:02.0000000"), Warning("0:00:02") }) Reject(warning, stderr);
        Reject("", actual + stderr);
    }

    [HostTest("quota output enforces combined65536 byte extent and mandatory ordered random controls")]
    private static void CombinedExtentAndControls()
    {
        string stderr = Output(), warning = Warning();
        string repeated = string.Concat(Enumerable.Repeat(warning, (65536 - stderr.Length) / warning.Length));
        int spare = 65536 - stderr.Length - repeated.Length;
        string CrLf(int count)
        {
            var text = new System.Text.StringBuilder();
            foreach (char c in repeated)
            { if (c == '\n' && count > 0) { text.Append('\r'); count--; } text.Append(c); }
            HostCheck.Equal(0, count); return text.ToString();
        }
        string exact = CrLf(spare), over = CrLf(spare + 1);
        HostCheck.Equal(65536, exact.Length + stderr.Length);
        HostCheck.Equal(2UL, QemuTranscripts.InspectNativeAotServiceQuotaOutputForTests(exact, stderr).CompletedChunks);
        HostCheck.True(over.Length <= 65536 && over.Length + stderr.Length == 65537);
        Reject(over, stderr, "service_quota_output_extent");
        const string controls = "CLROS_SERVICE_RANDOM_CONTROLS zero=0 invalid=-22 fault=-14\n";
        foreach (string changed in new[] {
            stderr.Replace(controls, "", StringComparison.Ordinal), stderr.Replace(controls, controls + controls, StringComparison.Ordinal),
            stderr.Replace("zero=0 invalid=-22 fault=-14", "zero=1 invalid=-22 fault=-14", StringComparison.Ordinal),
            stderr.Replace("CLROS_M10_011_READY\n" + controls, controls + "CLROS_M10_011_READY\n", StringComparison.Ordinal) })
            Reject("", changed, "service_quota_output_order");
    }

    private static void Reject(string stdout, string stderr, string? reason = null)
    {
        bool rejected = false;
        try { _ = QemuTranscripts.InspectNativeAotServiceQuotaOutputForTests(stdout, stderr); }
        catch (ToolFailure error) { if (reason is not null) HostCheck.Equal(reason, error.Reason); rejected = true; }
        HostCheck.True(rejected);
    }
}
