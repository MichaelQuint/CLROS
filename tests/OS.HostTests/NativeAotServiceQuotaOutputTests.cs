using OS.Tooling;

internal static class NativeAotServiceQuotaOutputTests
{
    private const string Gc = "limit=16777216 baselineHeap=30920 baselineCommitted=151552 liveBytes=2097152 liveHeap=2200000 liveCommitted=2300000 pressureBytes=8388608 pressureHeap=8500000 pressureCommitted=9000000 pressureCollections=4 oom=1";
    private static string Output(int completed = 2)
    {
        var rows = new List<string> { "CLROS_APP_ENTRY minimal.api.service", "CLROS_APP_CCTOR minimal.api.service",
            "CLROS_APP_CTOR minimal.api.service", "CLROS_APP_BUILT minimal.api.service", "CLROS_SERVICE_GC_BEGIN " + Gc,
            "CLROS_SERVICE_LISTEN contract=v3-quota endpoint=0.0.0.0:8080", "CLROS_M10_011_READY",
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

    private static void Reject(string stdout, string stderr)
    {
        bool rejected = false;
        try { _ = QemuTranscripts.InspectNativeAotServiceQuotaOutputForTests(stdout, stderr); }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
