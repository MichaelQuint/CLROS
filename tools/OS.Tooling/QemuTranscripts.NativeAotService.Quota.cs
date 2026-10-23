using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied denial/trailer values only. These authenticate no package,
    /// scheduler, mapping, application output, peer, native image or guest run.</summary>
    public readonly record struct ServiceQuotaInspection(ulong Identity, ulong Ordinal,
        ulong BackedPages, ulong PoolPages, ulong Reaped);

    /// <summary>Inspect normalized borrowed rows for the explicit quota fixture's
    /// final failed 1MiB anonymous mmap and ordered teardown. Require the matching
    /// operation-local topology, physical insufficiency, exit99 and checked
    /// negative terminal instead of an ordinary service pass. Malformed or
    /// incomplete input throws. This value-only seam neither frames raw bytes nor
    /// replays complete worker/mapping/transport ownership. Complete original
    /// parent/program/syscall replay, application/peer/packet overlap, native ABI,
    /// process ownership and quiet-terminal drain remain mandatory.</summary>
    public static ServiceQuotaInspection InspectNativeAotServiceQuotaForTests(string[] rows)
    {
        Need(rows is not null && rows.Length is > 0 and <= 1_000_000 &&
            rows.All(r => r is not null && r.Length is > 0 and <= 4096 && r.All(c => c is >= ' ' and <= '~')) &&
            rows.Sum(r => (long)r.Length + 2) <= 128L * 1024 * 1024, "service_quota_rows");
        string[] quotaMarkers = ["M16_016_QUOTA_ABORT", "M16_016_QUOTA_SOCKETS", "M16_016_QUOTA_PROCESS_FAILURE",
            "M16_016_QUOTA_NATIVE", "M16_016_QUOTA_RESTORE", "M16_016_QUOTA_CLEANUP"];
        Need(rows!.Where(r => r.StartsWith("M16_016_QUOTA_", StringComparison.Ordinal))
            .All(r => quotaMarkers.Contains(r.Split(' ', 2)[0], StringComparer.Ordinal)), "service_quota_unknown_record");
        int One(string prefix)
        {
            int at = -1;
            for (int i = 0; i < rows!.Length; i++) if (rows[i].StartsWith(prefix, StringComparison.Ordinal))
            { Need(at == -1, "service_quota_duplicate"); at = i; }
            Need(at >= 0, "service_quota_missing"); return at;
        }
        string[] normalized = ValidateLinuxServiceCallOrdinals(rows!);
        var returns = ReadLinuxServiceCallReturns(rows!);
        Need(returns.Count > 0, "service_quota_call_missing");
        var final = returns[^1];
        var call = ReadRecord(normalized[final.RecordIndex], "M16_008_CALL",
            "identity:dec", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec",
            "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "clock:dec", "valueBits:dec", "action:dec", "status:dec");
        ulong denied = unchecked((ulong)-12L);
        Need(call["identity"] == final.Identity && call["nr"] == 9 && call["a0"] == 0 && call["a1"] == 1_048_576 &&
            call["a2"] == 3 && call["a3"] == 0x22 && call["a4"] == ulong.MaxValue && call["a5"] == 0 &&
            call["valueBits"] == denied && final.ValueBits == denied && call["action"] == 1 && call["status"] == 1,
            "service_quota_denial_call");
        int topologyAt = -1; Dictionary<string, ulong>? matchedTopology = null;
        var topologyKeys = new HashSet<(ulong Identity, ulong Ordinal)>();
        for (int i = 0; i < rows!.Length; i++) if (rows[i].StartsWith("M16_016_TOPOLOGY", StringComparison.Ordinal))
        {
            var observed = ReadRecord(rows[i], "M16_016_TOPOLOGY", "identity:dec", "ordinal:dec", "nr:dec",
                "pool:dec", "backed:dec", "free:dec", "freeRuns:dec", "largestFreeRun:dec", "regions:dec", "regionLimit:dec", "tables:dec", "tableLimit:dec");
            Need(topologyKeys.Add((observed["identity"], observed["ordinal"])), "service_quota_duplicate_topology");
            if (observed["identity"] == final.Identity && observed["ordinal"] == final.Ordinal)
            { topologyAt = i; matchedTopology = observed; }
        }
        Need(topologyAt >= 0 && topologyAt < final.RecordIndex, "service_quota_topology_order");
        var topology = matchedTopology ?? throw new ToolFailure("service_quota_topology_missing");
        Need(topology["identity"] == final.Identity && topology["ordinal"] == final.Ordinal && topology["nr"] == 9 &&
            topology["pool"] == LinuxAppConfiguration.ConfiguredServicePoolPages && topology["backed"] <= topology["pool"] &&
            topology["free"] == topology["pool"] - topology["backed"] && topology["free"] < call["a1"] / 4096 &&
            topology["largestFreeRun"] <= topology["free"] && topology["freeRuns"] <= topology["free"] &&
            topology["freeRuns"] <= topology["regions"] + 1 &&
            (topology["free"] == 0 ? topology["freeRuns"] == 0 && topology["largestFreeRun"] == 0 :
                topology["freeRuns"] > 0 && topology["largestFreeRun"] > 0 &&
                topology["free"] <= topology["freeRuns"] * topology["largestFreeRun"] &&
                topology["largestFreeRun"] + topology["freeRuns"] - 1 <= topology["free"]) &&
            topology["regions"] <= topology["regionLimit"] && topology["regionLimit"] == OS.Kernel.Compatibility.LinuxX64.LinuxMemoryServices.WebMaximumRegions &&
            (topology["backed"] == 0 || topology["regions"] > 0) && topology["tableLimit"] is >= 4 and <= OS.Kernel.Memory.X64UserAddressSpace.MaximumTables &&
            topology["tables"] >= 4 && topology["tables"] <= topology["tableLimit"], "service_quota_topology");
        int abortAt = One("M16_016_QUOTA_ABORT ");
        const string abortSuffix = " exit=99 policy=terminate cause=physical-backing";
        Need(rows[abortAt].EndsWith(abortSuffix, StringComparison.Ordinal) && final.RecordIndex < abortAt &&
            !rows.Skip(abortAt).Any(r => r.StartsWith("M16_008_CALL", StringComparison.Ordinal)), "service_quota_abort_order");
        var abort = ReadRecord(rows[abortAt][..^abortSuffix.Length], "M16_016_QUOTA_ABORT",
            "identity:dec", "ordinal:dec", "nr:dec", "backed:dec", "pool:dec");
        Need(abort["identity"] == final.Identity && abort["ordinal"] == final.Ordinal && abort["nr"] == 9 &&
            abort["backed"] == topology["backed"] && abort["pool"] == topology["pool"], "service_quota_abort_binding");
        int socketsAt = One("M16_016_QUOTA_SOCKETS "), serviceAt = One("M16_014_END ");
        int failureAt = One("M16_016_QUOTA_PROCESS_FAILURE"), nativeAt = One("M16_016_QUOTA_NATIVE"), restoreAt = One("M16_016_QUOTA_RESTORE");
        int wireAt = One("M16_011_WIRE "), endAt = One("M16_011_END "), cleanupAt = One("M16_016_QUOTA_CLEANUP "), terminalAt = One("CLROS_M16_016_QUOTA_CLEANUP");
        Need(abortAt < socketsAt && socketsAt < serviceAt && failureAt == serviceAt + 1 && nativeAt == failureAt + 1 &&
            restoreAt == nativeAt + 1 && wireAt == restoreAt + 1 && endAt == wireAt + 1 && cleanupAt == endAt + 4 &&
            terminalAt == cleanupAt + 1 && terminalAt == rows.Length - 1, "service_quota_teardown_order");
        Need(rows[failureAt] == "M16_016_QUOTA_PROCESS_FAILURE exit=99 cause=physical-backing" &&
            rows[nativeAt] == "M16_016_QUOTA_NATIVE process=drained timer=restored descriptors=closed guard=detached mappings=restored pool=scrubbed initialStack=zero random=zero reservation=retained" &&
            rows[restoreAt] == "M16_016_QUOTA_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true" &&
            rows[cleanupAt] == "M16_016_QUOTA_CLEANUP exit=99 process=failed workers=reaped memory=zero cpu=restored nic=retired dma=zero allocator=unchanged pci=restored canaries=valid reservation=retained" &&
            rows[terminalAt] == "CLROS_M16_016_QUOTA_CLEANUP", "service_quota_cleanup");
        const string socketSuffix = " retained=0 transport=aborted timewait=discarded buffers=scrubbed descriptors=closed table=retired";
        Need(rows[socketsAt].EndsWith(socketSuffix, StringComparison.Ordinal), "service_quota_socket_cleanup");
        var sockets = ReadRecord(rows[socketsAt][..^socketSuffix.Length], "M16_016_QUOTA_SOCKETS",
            "bulkAborted:dec", "compactDiscarded:dec", "clock:dec");
        Need(sockets["bulkAborted"] <= 8 && sockets["compactDiscarded"] <= OS.Net.TcpTimeWaitTable.MaximumEntries, "service_quota_socket_bound");
        _ = ReadRecord(rows[wireAt], "M16_011_WIRE", "received:dec", "arp:dec", "accepted:dec", "ignored:dec", "capacity:dec", "arpCopied:dec", "tcpCopied:dec");
        const string socketEndSuffix = " retained=0 buffers=scrubbed descriptors=closed workers=reaped";
        Need(rows[endAt].EndsWith(socketEndSuffix, StringComparison.Ordinal), "service_quota_socket_end");
        var socketEnd = ReadRecord(rows[endAt][..^socketEndSuffix.Length], "M16_011_END",
            "polls:dec", "received:dec", "copied:dec", "fresh:dec", "retired:dec", "start:dec", "end:dec", "maxDeadline:dec");
        Need(socketEnd["end"] == sockets["clock"] && socketEnd["end"] >= socketEnd["start"] &&
            socketEnd["retired"] <= socketEnd["fresh"], "service_quota_socket_clock");
        const string serviceSuffix = " process=drained mappings=restored pool=scrubbed random=zero reservation=retained";
        Need(rows[serviceAt].EndsWith(serviceSuffix, StringComparison.Ordinal), "service_quota_service_end");
        var service = ReadRecord(rows[serviceAt][..^serviceSuffix.Length], "M16_014_END", "identity:dec", "calls:dec", "started:dec", "nanos:dec", "reaped:dec");
        Need(service["nanos"] >= service["started"] && service["reaped"] > 0, "service_quota_service_lifetime");
        int ownerAt = One("M16_008_OWNER_END "), budgetAt = One("M16_016_MEMORY "), clockAt = One("M16_008_BUDGET_END ");
        Need(socketsAt < budgetAt && budgetAt < clockAt && clockAt < ownerAt && ownerAt + 1 == serviceAt, "service_quota_owner_order");
        const string ownerSuffix = " process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
        Need(rows[ownerAt].EndsWith(ownerSuffix, StringComparison.Ordinal), "service_quota_owner_cleanup");
        var owner = ReadRecord(rows[ownerAt][..^ownerSuffix.Length], "M16_008_OWNER_END",
            "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec", "userReserved:dec", "userBacked:dec");
        var budget = ReadRecord(rows[budgetAt], "M16_016_MEMORY", "poolPages:dec", "peakPages:dec", "endPages:dec");
        var clock = ReadRecord(rows[clockAt], "M16_008_BUDGET_END", "nanos:dec");
        Need(owner["exit"] == 99 && owner["userReserved"] == 0 && owner["userBacked"] == 0 &&
            owner["reaped"] == service["reaped"] && budget["poolPages"] == topology["pool"] &&
            budget["peakPages"] >= topology["backed"] && budget["peakPages"] <= budget["poolPages"] && budget["endPages"] == 0 &&
            clock["nanos"] >= service["started"] && clock["nanos"] <= service["nanos"],
            "service_quota_owner_end");
        var reaped = new HashSet<ulong>(); bool deniedReaped = false;
        var exits = new Dictionary<ulong, int>();
        for (int i = 0; i < rows.Length; i++) if (rows[i].StartsWith("M16_008_EXIT ", StringComparison.Ordinal))
        {
            Need(i < socketsAt, "service_quota_late_exit");
            var exit = ReadRecord(rows[i], "M16_008_EXIT", "identity:dec", "clearTidResult:dec");
            Need(exits.TryAdd(exit["identity"], i), "service_quota_duplicate_exit");
        }
        for (int i = 0; i < rows.Length; i++) if (rows[i].StartsWith("M16_008_REAP ", StringComparison.Ordinal))
        {
            const string suffix = " context=zero stack=zero slot=zero";
            Need(i < socketsAt && rows[i].EndsWith(suffix, StringComparison.Ordinal), "service_quota_reap_order");
            var reap = ReadRecord(rows[i][..^suffix.Length], "M16_008_REAP", "old:dec", "next:dec");
            ulong old = reap["old"];
            Need(exits.TryGetValue(old, out int exitAt), "service_quota_exit_missing");
            Need(old >> 32 != 0 && (uint)old is >= 1 and <= 16 && old <= ulong.MaxValue - (1UL << 32) &&
                reap["next"] == old + (1UL << 32) && reaped.Add(old) && exitAt < i, "service_quota_reap");
            if (old == final.Identity) { Need(exitAt > abortAt && i > abortAt, "service_quota_denied_reap_order"); deniedReaped = true; }
        }
        Need(deniedReaped && (ulong)reaped.Count == service["reaped"] && reaped.SetEquals(exits.Keys), "service_quota_reap_count");
        for (uint stage = 4; stage <= 6; stage++)
        {
            var row = ReadRecord(rows[endAt + (int)stage - 3], "M07_026_ROW", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec");
            Need(row["stage"] == stage, "service_quota_network_stage");
            if (stage == 5) Need(row["a"] == 8 && row["b"] == 0 && row["c"] == 8 && row["d"] == 1, "service_quota_dma_end");
            if (stage == 6) Need(row["a"] == row["b"] && row["d"] <= ushort.MaxValue, "service_quota_allocator_end");
        }
        Need(!rows.Any(r => r.StartsWith("TEST_FAIL", StringComparison.Ordinal) || r.StartsWith("OS_SMOKE", StringComparison.Ordinal) ||
            r is "TEST_PASS m16.014" or "TEST_PASS m16-012" or "CLROS_M16_012_READY") &&
            !rows.Skip(abortAt).Any(r => r.StartsWith("TEST_PASS", StringComparison.Ordinal)), "service_quota_ordinary_terminal");
        return new(final.Identity, final.Ordinal, topology["backed"], topology["pool"], service["reaped"]);
    }
}
