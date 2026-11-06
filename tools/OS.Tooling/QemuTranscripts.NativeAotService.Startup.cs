using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Synchronously borrow bounded raw service bytes, frame fd1/fd2
    /// payloads and join the exact service startup to original cumulative call
    /// generations/returns. Invalid or substituted observations throw. Inputs
    /// stay unchanged; a supplied transcript grants no execution authority.</summary>

    public static ServiceStartupInspection InspectNativeAotServiceStartupForTests(ReadOnlySpan<byte> raw, bool lifecycle = false) => ReadServiceStartup(NativeAotThreadingOutput.ReadService(raw), out _, lifecycle);
    /// <summary>Explicit value-only startup contract selection. This checks no
    /// package, source/index, memory ownership, native image or guest authority.</summary>

    public static ServiceStartupInspection InspectNativeAotServiceStartupForTests(ReadOnlySpan<byte> raw, NativeAotServiceContract contract) => ReadServiceStartup(NativeAotThreadingOutput.ReadService(raw), out _, contract);
    private static unsafe ServiceStartupInspection ReadServiceStartup(NativeAotThreadingOutput.Capture framed, out HashSet<int> metadataPositions, bool lifecycle = false) => ReadServiceStartup(framed, out metadataPositions, lifecycle ? NativeAotServiceContract.V2 : NativeAotServiceContract.V1);
    private static unsafe ServiceStartupInspection ReadServiceStartup(NativeAotThreadingOutput.Capture framed, out HashSet<int> metadataPositions, NativeAotServiceContract contract)
    {
        Need(contract is NativeAotServiceContract.V1 or NativeAotServiceContract.V2 or NativeAotServiceContract.QuotaFailure, "service_startup_contract");
        bool lifecycle = contract != NativeAotServiceContract.V1, quota = contract == NativeAotServiceContract.QuotaFailure;
        string text = framed.NormalizedTail;
        Need(text.EndsWith("\r\n", StringComparison.Ordinal), "service_startup_records");
        string[] rows = text[..^2].Split("\r\n", StringSplitOptions.None);
        Need(rows.Length is > 0 and <= 1_000_000 && rows.All(row => row.Length is > 0 and <= 4096 && row.All(c => c is >= ' ' and <= '~')), "service_startup_records");
        int Marker(string marker, bool prefix = false)
        {
            int[] found = Enumerable.Range(0, rows.Length).Where(i => prefix ? rows[i].StartsWith(marker, StringComparison.Ordinal) : rows[i] == marker).ToArray();
            Need(found.Length == 1, "service_startup_marker");
            return found[0];
        }

        int start = Marker("TEST_START m16.014.startup"), enter = Marker("M16_014_ENTER ", true), config = Marker("M16_014_CONFIG ", true), startup = Marker("M16_014_STARTUP ", true), bind = Marker("M16_014_BIND ", true), listen = Marker("M16_014_LISTEN ", true), live = Marker("M16_014_LIVE ", true), ready = Marker("CLROS_M16_014_LISTENING"), pass = Marker("TEST_PASS m16.014.startup");
        metadataPositions = [start, enter, config, startup, bind, listen, live, ready, pass];
        Need(enter == start + 1 && enter < config && config < startup && startup < bind && bind < listen && listen < live && ready == live + 1 && pass == ready + 1, "service_startup_order");
        string[] otherPrefixes = ["M16_014_POLICY ", "M16_014_PROGRESS ", "M16_014_END ", "M16_014_MEMORY ", "M16_014_LISTENER_CLOSE ", "M16_014_STALLED "];
        for (int i = 0; i < rows.Length; i++)
        {
            string row = rows[i];
            Need(!row.StartsWith("TEST_FAIL", StringComparison.Ordinal) && !row.StartsWith("OS_SMOKE_FAIL", StringComparison.Ordinal) && !row.StartsWith("M10_004", StringComparison.Ordinal) && !row.StartsWith("M10_005", StringComparison.Ordinal) && !row.StartsWith("M10_006", StringComparison.Ordinal), "service_startup_failure_or_finite");
            if (row.StartsWith("M16_014_PROGRESS", StringComparison.Ordinal))
                Need(i > pass, "service_startup_early_progress");
            if (metadataPositions.Contains(i))
                continue;
            bool other = otherPrefixes.Any(prefix => row.StartsWith(prefix, StringComparison.Ordinal));
            Need(!row.StartsWith("M16_014_", StringComparison.Ordinal) || other, "service_startup_unknown_record");
            Need(!row.StartsWith("CLROS_M16_014_", StringComparison.Ordinal) || row == "CLROS_M16_014_READY", "service_startup_unknown_record");
            Need(!row.StartsWith("TEST_START m16.014.startup", StringComparison.Ordinal) && !row.StartsWith("TEST_PASS m16.014.startup", StringComparison.Ordinal), "service_startup_unknown_record");
        }

        uint settingsBytes = (uint)Encoding.UTF8.GetByteCount(quota ? NativeAotServicePackage.QuotaSettingsJson : lifecycle ? NativeAotServicePackage.LifecycleSettingsJson : NativeAotServicePackage.SettingsJson);
        var entry = ReadRecord(rows[enter], "M16_014_ENTER", "identity:dec", "rip:dec", "argc:dec", "envc:dec", "configBytes:dec");
        const string configSuffix = " source=immutable-package copied=user", listenSuffix = " address=0 port=8080 owner=live", liveSuffix = " owner=retained externalHttp=required";
        Need(rows[config].EndsWith(configSuffix, StringComparison.Ordinal) && rows[listen].EndsWith(listenSuffix, StringComparison.Ordinal) && rows[live].EndsWith(liveSuffix, StringComparison.Ordinal), "service_startup_schema");
        var settings = ReadRecord(rows[config][..^configSuffix.Length], "M16_014_CONFIG", "fd:dec", "bytes:dec");
        var prefix = ReadRecord(rows[startup], "M16_014_STARTUP", "writer:dec", "stages:dec");
        var binding = ReadRecord(rows[bind], "M16_014_BIND", "identity:dec", "fd:dec");
        var listener = ReadRecord(rows[listen][..^listenSuffix.Length], "M16_014_LISTEN", "fd:dec");
        var lifetime = ReadRecord(rows[live][..^liveSuffix.Length], "M16_014_LIVE", "argc:dec", "envc:dec", "configBytes:dec", "bindFd:dec", "stdoutBytes:dec", "stderrBytes:dec", "active:dec", "clock:dec");
        ulong environmentCount = quota ? 4UL : lifecycle ? 3UL : 2UL;
        Need(entry["argc"] == 5 && entry["envc"] == environmentCount && entry["configBytes"] == settingsBytes && settings["bytes"] == settingsBytes && settings["fd"] is >= 3 && settings["fd"] < NativeAotServicePackage.Descriptors && listener["fd"] is >= 3 && listener["fd"] < NativeAotServicePackage.Descriptors && prefix["stages"] == 4 && lifetime["argc"] == 5 && lifetime["envc"] == environmentCount && lifetime["configBytes"] == settingsBytes && lifetime["bindFd"] == listener["fd"] && lifetime["active"] > 0 && lifetime["active"] <= NativeAotServicePackage.Threads && lifetime["clock"] is > 0 and <= long.MaxValue, "service_startup_policy");
        var returns = ReadLinuxServiceCallReturns(rows).ToDictionary(call => call.RecordIndex);
        string[] calls = ValidateLinuxServiceCallOrdinals(rows);
        var parsed = returns.ToDictionary(item => item.Key, item => ReadRecord(calls[item.Key], "M16_008_CALL", WebCallFields));
        Need(parsed.Count > 0, "service_startup_calls");
        int first = parsed.Keys.Min();
        int entryCall = enter + 1;
        if (lifecycle && parsed[first]["nr"] is 9 or 10 or 11 or 12 or 25 or 28)
        {
            var memory = ReadRecord(rows[entryCall++], "M16_014_MEMORY", "nr:dec", "regions:dec", "regionLimit:dec", "backed:dec", "pool:dec", "reserved:dec");
            Need(memory["nr"] == parsed[first]["nr"], "service_startup_memory_call");
            if (quota)
            {
                int position = entryCall++;
                var topology = ReadRecord(rows[position], "M16_016_TOPOLOGY", "identity:dec", "ordinal:dec", "nr:dec", "pool:dec", "backed:dec", "free:dec", "freeRuns:dec", "largestFreeRun:dec", "regions:dec", "regionLimit:dec", "tables:dec", "tableLimit:dec");
                Need(topology["identity"] == entry["identity"] && topology["ordinal"] == 1 && topology["nr"] == memory["nr"] && topology["pool"] == memory["pool"] && topology["backed"] == memory["backed"] && topology["regions"] == memory["regions"] && topology["regionLimit"] == memory["regionLimit"], "service_startup_quota_topology");
                metadataPositions.Add(position);
            }
            // Full memory bounds, operation coverage and pressure joins belong
            // to the independent memory replay before complete acceptance.
        }

        Need(first == entryCall && returns[first].Ordinal == 1 && entry["identity"] == parsed[first]["identity"] && entry["rip"] == parsed[first]["rip"], "service_startup_entry");
        Need(parsed.TryGetValue(config + 1, out var read) && read["nr"] is 0 or 17 or 19 && read["a0"] == settings["fd"] && read["action"] == 1 && read["status"] == 1 && read["valueBits"] > 0 && read["valueBits"] <= settingsBytes && (read["nr"] == 19 ? read["a2"] is > 0 and <= LinuxDescriptorServices.MaximumVectors : read["a2"] >= read["valueBits"]) && (read["nr"] != 17 || read["a3"] == settingsBytes - read["valueBits"]), "service_startup_settings_call");
        Need(parsed.TryGetValue(listen + 1, out var listenCall) && listenCall["nr"] == 50 && listenCall["a0"] == listener["fd"] && listenCall["action"] == 1 && listenCall["status"] == 1 && listenCall["valueBits"] == 0, "service_startup_listen_call");
        byte[] stdout = Encoding.UTF8.GetBytes(framed.Stdout), stderr = Encoding.UTF8.GetBytes(framed.Stderr);
        byte[] message = Encoding.ASCII.GetBytes(quota ? "CLROS_SERVICE_LISTEN contract=v3-quota endpoint=0.0.0.0:8080\n" : lifecycle ? "CLROS_SERVICE_LISTEN contract=v2 endpoint=0.0.0.0:8080\n" : "CLROS_SERVICE_LISTEN contract=v1 endpoint=0.0.0.0:8080\n");
        int messageAt = stderr.AsSpan().IndexOf(message);
        Need(messageAt > 0 && stderr[messageAt - 1] == (byte)'\n' && stderr.AsSpan(messageAt + message.Length).IndexOf(message) < 0, "service_startup_app_listen");
        int messageEnd = messageAt + message.Length;
        var observed = LinuxAppStartupObservation.ForService();
        List<(uint Fd, int Offset, int Bytes, int Row)> parts = [];
        int writes = 0, stdoutOffset = 0, stderrOffset = 0, pending = 0, firstWrite = -1, completedWrite = -1, completedCall = -1, boundCall = -1, messageWrite = -1, messageCall = -1;
        uint channel = 0;
        ulong boundFd = 0, previousClock = 0;
        fixed (byte* error = stderr)
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].StartsWith("M16_005_WRITE ", StringComparison.Ordinal))
                {
                    Need(writes < framed.WriteLengths.Count, "service_startup_write");
                    uint fd = framed.WriteChannels[writes];
                    int bytes = framed.WriteLengths[writes++];
                    Need(rows[i] == $"M16_005_WRITE fd={fd} bytes={bytes} data=" && (pending == 0 || channel == fd), "service_startup_write");
                    channel = fd;
                    pending += bytes;
                    int offset = fd == 1 ? stdoutOffset : stderrOffset;
                    Need(fd is 1 or 2 && offset <= (fd == 1 ? stdout.Length : stderr.Length) - bytes, "service_startup_bytes");
                    if (fd == 2)
                    {
                        if (firstWrite < 0)
                            firstWrite = i;
                        if (offset < messageEnd && offset + bytes >= messageEnd)
                            messageWrite = i;
                        stderrOffset += bytes;
                    }
                    else
                        stdoutOffset += bytes;
                    parts.Add((fd, offset, bytes, i));
                }

                if (i == live)
                    Need(observed.SuccessfulBind && lifetime["stdoutBytes"] == (ulong)stdoutOffset && lifetime["stderrBytes"] == (ulong)stderrOffset && live == messageWrite + 1, "service_startup_live_output");
                if (!parsed.TryGetValue(i, out var call))
                    continue;
                var returned = returns[i];
                Need((uint)call["identity"] is >= 1 and <= NativeAotServicePackage.Threads && call["identity"] >> 32 != 0 && returned.Identity == call["identity"] && returned.ValueBits == call["valueBits"] && call["clock"] >= previousClock && call["clock"] <= long.MaxValue, "service_startup_call_binding");
                bool copied = call["nr"] is 1 or 20 && call["a0"] is 1 or 2 && unchecked((long)call["valueBits"]) > 0;
                Need(pending == 0 ? !copied : copied && call["a0"] == channel && call["valueBits"] == (ulong)pending && call["action"] == 1 && call["status"] == 1, "service_startup_write_call");
                if (copied)
                    Need(call["nr"] == 1 ? call["a2"] > 0 && call["a2"] <= LinuxDescriptorServices.MaximumIo && call["valueBits"] <= call["a2"] : call["a2"] is > 0 and <= LinuxDescriptorServices.MaximumVectors, "service_startup_write_request");
                foreach (var part in parts)
                {
                    if (part.Fd != 2)
                        continue;
                    bool complete = observed.StartupComplete;
                    Need(observed.Observe(returned.Identity, error + part.Offset, (uint)part.Bytes), "service_startup_prefix");
                    if (!complete && observed.StartupComplete)
                    {
                        completedWrite = part.Row;
                        completedCall = i;
                    }

                    if (part.Offset < messageEnd && part.Offset + part.Bytes >= messageEnd)
                    {
                        Need(live > part.Row && pass < i && lifetime["clock"] >= previousClock && lifetime["clock"] <= call["clock"], "service_startup_live_call");
                        messageCall = i;
                    }
                }

                parts.Clear();
                pending = 0;
                previousClock = call["clock"];
                if (call["nr"] == 49)
                {
                    Need(observed.RecordBind(returned.Identity, call["action"] == 1 && call["status"] == 1 && call["valueBits"] == 0), "service_startup_bind_call");
                    boundCall = i;
                    boundFd = call["a0"];
                }
            }

        Need(pending == 0 && writes == framed.WriteLengths.Count && stdoutOffset == stdout.Length && stderrOffset == stderr.Length && observed.IsService && observed.SuccessfulBind && start < firstWrite && startup == completedWrite + 1 && completedCall > startup && bind > completedCall && boundCall == bind + 1 && listen > boundCall && messageWrite > listen + 1 && messageCall > pass && prefix["writer"] == observed.WriterIdentity && binding["identity"] == observed.BindIdentity && binding["fd"] == boundFd && listener["fd"] == boundFd, "service_startup_join");
        var settingsCall = returns[config + 1];
        return new(observed.WriterIdentity, (int)boundFd, settingsBytes, (int)settings["fd"], settingsCall.Identity, settingsCall.Ordinal);
    }
}
