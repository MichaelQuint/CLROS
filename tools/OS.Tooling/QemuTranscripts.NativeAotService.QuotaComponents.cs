using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Inspect borrowed normalized values and independently validate
    /// their complete transport census. The full production scheduler replay
    /// additionally binds every stack generation/Tid to its actual model.
    /// Does not mutate inputs, run anything or construct owned evidence.</summary>

    public static ServiceQuotaComponentInspection InspectNativeAotServiceQuotaComponentsForTests(string[] rows)
    {
        QuotaComponentInput(rows);
        return ReadServiceQuotaComponents(rows, ReadServiceQuotaTransport(rows, out _), out _);
    }

    private static void QuotaComponentInput(string[] rows) => Need(rows is not null && rows.Length is> 0 and <= 1_000_000 && rows.All(r => r is not null && r.Length is> 0 and <= 4096 && r.All(c => c is >= ' ' and <= '~')) && rows.Sum(r => (long)r.Length + 2) <= 128L * 1024 * 1024, "quota_components_input");
    private static ServiceQuotaComponentInspection ReadServiceQuotaComponents(string[] rows, ServiceQuotaTransportInspection transport, out HashSet<int> metadata)
    {
        QuotaComponentInput(rows);
        metadata = [];
        const ulong page = 4096, usable = 16 * page, rx = 8192, tx = 32768;
        string[] normalized = ValidateLinuxServiceCallOrdinals(rows);
        var calls = ReadLinuxServiceCallReturns(rows).ToDictionary(r => r.RecordIndex, r => ReadRecord(normalized[r.RecordIndex], "M16_008_CALL", WebCallFields));
        int One(string prefix)
        {
            int[] found = Enumerable.Range(0, rows.Length).Where(i => rows[i].StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Need(found.Length == 1, "quota_components_unique");
            return found[0];
        }

        int boundAt = One("M16_008_BOUND "), abortAt = One("M16_016_QUOTA_ABORT "), socketsAt = One("M16_016_QUOTA_SOCKETS "), ownerAt = One("M16_008_OWNER_END ");
        int firstClose = Array.FindIndex(rows, r => r.StartsWith("M16_011_CLOSE ", StringComparison.Ordinal)), lastClose = Array.FindLastIndex(rows, r => r.StartsWith("M16_011_CLOSE ", StringComparison.Ordinal));
        const string boundSuffix = " timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack";
        Need(rows[boundAt].EndsWith(boundSuffix, StringComparison.Ordinal), "quota_components_bound");
        var bound = ReadRecord(rows[boundAt][..^boundSuffix.Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
        ulong pool = bound["pool"], workerPage = LinuxThreadProgram.CodePages + LinuxThreadProgram.MetadataPages + LinuxThreadProgram.SwitchPages;
        Need(pool >= page && pool % page == 0 && pool <= 0x100000000UL - WebThreadPoolPages * page && bound["workers"] == 16 && bound["code"] == LinuxThreadProgram.Bytes && bound["metadata"] == LinuxThreadProgram.MetadataPages * page && boundAt < abortAt && abortAt < socketsAt && socketsAt < ownerAt, "quota_components_bound");
        ulong metadataStart = pool + LinuxThreadProgram.MetadataPage * page, metadataEnd = pool + (LinuxThreadProgram.MetadataPage + LinuxThreadProgram.MetadataPages) * page;
        ServiceQuotaStackBacking? backing = null;
        List<ServiceQuotaStackLease> stackLeases = [];
        List<ServiceQuotaStackLeaseSnapshot> stacks = [];
        List<ServiceQuotaSocketLease> socketLeases = [];
        List<ServiceQuotaSocketLeaseSnapshot> sockets = [];
        Dictionary<ulong, int> liveStacks = [];
        HashSet<ulong> exited = [];
        ulong[] generations = Enumerable.Repeat(1UL, 16).ToArray();
        var census = transport.FreshLifetimes.ToArray();
        Dictionary<(ulong Slot, ulong Owner, ulong Epoch, ulong Generation, ulong Index), ServiceQuotaTransportLifetime> fresh = [];
        foreach (var life in census)
            Need(fresh.TryAdd((life.Slot, life.Owner, life.Epoch, life.Generation, life.Index), life), "quota_components_census");
        HashSet<ServiceQuotaTransportLifetime> liveSockets = [], everSockets = [], retired = [];
        ulong acquired = 0, returned = 0, peakStack = 0, socketAcquired = 0, socketReturned = 0, peakSocket = 0, contextBase = 0;
        int lastCall = -1, stackEnd = -1, socketEnd = -1;
        bool pendingEnd = false;
        for (int at = 0; at < rows.Length; at++)
        {
            string line = rows[at];
            if (pendingEnd && socketEnd < 0)
                Need(line.StartsWith("M16_016_SOCKET_LEASE event=1 ", StringComparison.Ordinal) || line.StartsWith("M16_016_SOCKET_LEASES ", StringComparison.Ordinal), "quota_components_socket_end_order");
            if (calls.ContainsKey(at))
                lastCall = at;
            if (line.StartsWith("M16_008_EXIT ", StringComparison.Ordinal))
            {
                var e = ReadRecord(line, "M16_008_EXIT", "identity:dec", "clearTidResult:dec");
                Need(liveStacks.ContainsKey(e["identity"]) && exited.Add(e["identity"]), "quota_components_exit");
            }

            if (line.StartsWith("M16_008_REAP ", StringComparison.Ordinal))
            {
                const string suffix = " context=zero stack=zero slot=zero";
                Need(line.EndsWith(suffix, StringComparison.Ordinal), "quota_components_reap");
                var r = ReadRecord(line[..^suffix.Length], "M16_008_REAP", "old:dec", "next:dec");
                int index = -1;
                Need(exited.Remove(r["old"]) && liveStacks.Remove(r["old"], out index) && r["old"] <= ulong.MaxValue - (1UL << 32) && r["next"] == r["old"] + (1UL << 32), "quota_components_reap");
                var old = stackLeases[index];
                generations[old.Index]++;
                stackLeases[index] = old with
                {
                    ReapRecordIndex = at
                };
                returned++;
            }

            if (line.StartsWith("M16_011_SLOT ", StringComparison.Ordinal))
            {
                var s = SocketTransport(line);
                if (s["event"] == 1)
                {
                    ServiceQuotaTransportLifetime life = default;
                    Need(fresh.TryGetValue((s["slot"], s["owner"], s["epoch"], s["generation"], s["index"]), out life) && !liveSockets.Contains(life) && retired.Add(life), "quota_components_socket_retire");
                }
            }

            bool stackRow = line.StartsWith("M16_016_STACK", StringComparison.Ordinal), socketRow = line.StartsWith("M16_016_SOCKET_LEASE", StringComparison.Ordinal);
            if (!stackRow && !socketRow)
                continue;
            metadata.Add(at);
            Need(boundAt < at && at < ownerAt, "quota_components_order");
            if (line.StartsWith("M16_016_STACK_BACKING ", StringComparison.Ordinal))
            {
                const string suffix = " reservation=retained";
                Need(backing is null && stackLeases.Count == 0 && lastCall >= 0 && line.EndsWith(suffix, StringComparison.Ordinal), "quota_components_backing");
                Need(at > 0 && (calls.ContainsKey(at - 1) || rows[at - 1].StartsWith("M16_016_SOCKET ", StringComparison.Ordinal)), "quota_components_backing_order");
                var b = ReadRecord(line[..^suffix.Length], "M16_016_STACK_BACKING", "cooperativePages:dec", "workerMappedBytes:dec", "switchMappedBytes:dec", "irqMappedBytes:dec", "mainSupervisorUsableBound:dec");
                Need(b["cooperativePages"] == WebThreadPoolPages && b["workerMappedBytes"] == 16 * usable && b["switchMappedBytes"] == (LinuxThreadProgram.SwitchPages - 2UL) * page && b["irqMappedBytes"] == (LinuxThreadProgram.IrqPages - 2UL) * page && b["mainSupervisorUsableBound"] == 64 * page, "quota_components_backing");
                backing = new(at, b["cooperativePages"], b["workerMappedBytes"], b["switchMappedBytes"], b["irqMappedBytes"], b["mainSupervisorUsableBound"]);
            }
            else if (line.StartsWith("M16_016_STACK_LEASE ", StringComparison.Ordinal))
            {
                var s = ReadRecord(line, "M16_016_STACK_LEASE", "identity:dec", "index:dec", "stack:dec", "stackBytes:dec", "context:dec", "contextBytes:dec");
                Need(backing is not null && stackEnd < 0 && lastCall >= 0 && lastCall < at && at < abortAt && s["index"] < 16, "quota_components_stack");
                int index = (int)s["index"];
                ulong id = s["identity"];
                var call = calls[lastCall];
                bool main = stackLeases.Count == 0;
                Need(id == (generations[index] << 32 | (uint)(index + 1)) && !stackLeases.Any(l => l.Identity == id) && !liveStacks.Keys.Any(identity => (uint)identity == (uint)id) && s["stack"] == pool + (workerPage + (ulong)index * 18 + 1) * page && s["stackBytes"] == usable && s["contextBytes"] == X64ThreadContext.Bytes && s["context"] % X64ThreadContext.Alignment == 0 && s["context"] >= metadataStart + (ulong)index * X64ThreadContext.Bytes && s["context"] <= metadataEnd - X64ThreadContext.Bytes && (main ? id == 0x100000001 && lastCall == calls.Keys.Min() : call["nr"] == 56 && call["action"] == 1 && call["status"] == 1 && call["valueBits"] is >= 2 and <= int.MaxValue && !stackLeases.Any(l => l.CreateCallRecordIndex == lastCall)), "quota_components_stack");
                ulong baseValue = s["context"] - (ulong)index * X64ThreadContext.Bytes;
                if (contextBase == 0)
                    contextBase = baseValue;
                Need(contextBase == baseValue && contextBase >= metadataStart && contextBase <= metadataEnd - 16UL * X64ThreadContext.Bytes, "quota_components_context_alias");
                Need(at > 0 && (calls.ContainsKey(at - 1) || rows[at - 1].StartsWith("M16_016_SOCKET ", StringComparison.Ordinal) || rows[at - 1].StartsWith("M16_016_PTHREAD ", StringComparison.Ordinal) || rows[at - 1].StartsWith("M16_016_STACK_BACKING ", StringComparison.Ordinal)), "quota_components_stack_order");
                Need(at + 1 < rows.Length && rows[at + 1].StartsWith("M16_016_STACK_LEASES ", StringComparison.Ordinal), "quota_components_stack_snapshot_missing");
                stackLeases.Add(new(at, id, index, s["stack"], s["stackBytes"], s["context"], s["contextBytes"], main ? 1 : (int)call["valueBits"], lastCall, -1));
                liveStacks.Add(id, stackLeases.Count - 1);
                acquired++;
                peakStack = Math.Max(peakStack, (ulong)liveStacks.Count);
            }
            else if (line.StartsWith("M16_016_STACK_LEASES ", StringComparison.Ordinal))
            {
                var s = ReadRecord(line, "M16_016_STACK_LEASES", "acquired:dec", "returned:dec", "live:dec", "stackBytes:dec", "contextBytes:dec", "peakLive:dec", "peakStackBytes:dec", "peakContextBytes:dec", "end:dec");
                ulong live = (ulong)liveStacks.Count;
                bool end = s["end"] == 1;
                Need(backing is not null && acquired > 0 && s["end"] <= 1 && stackEnd < 0 && s["acquired"] == acquired && s["returned"] == returned && s["live"] == live && s["stackBytes"] == live * usable && s["contextBytes"] == live * X64ThreadContext.Bytes && s["peakLive"] == peakStack && s["peakStackBytes"] == peakStack * usable && s["peakContextBytes"] == peakStack * X64ThreadContext.Bytes && (end ? at > abortAt && firstClose > at && live == 0 && exited.Count == 0 : at < abortAt && lastCall >= 0 && (stacks.Count == 0 || s["acquired"] != stacks[^1].Acquired || s["returned"] != stacks[^1].Returned)), "quota_components_stack_snapshot");
                if (end)
                    stackEnd = at;
                else
                    Need(at > 0 && (rows[at - 1].StartsWith("M16_016_STACK_LEASE ", StringComparison.Ordinal) || calls.ContainsKey(at - 1) || rows[at - 1].StartsWith("M16_016_SOCKET ", StringComparison.Ordinal)), "quota_components_stack_snapshot_order");
                stacks.Add(new(at, acquired, returned, live, s["stackBytes"], s["contextBytes"], peakStack, s["peakStackBytes"], s["peakContextBytes"], end));
            }
            else if (line.StartsWith("M16_016_SOCKET_LEASE ", StringComparison.Ordinal))
            {
                var s = ReadRecord(line, "M16_016_SOCKET_LEASE", "event:dec", "slot:dec", "owner:dec", "epoch:dec", "generation:dec", "index:dec", "rxBytes:dec", "txBytes:dec", "end:dec");
                ServiceQuotaTransportLifetime life = default;
                Need(s["event"] <= 1 && s["end"] <= 1 && s["rxBytes"] == rx && s["txBytes"] == tx && socketEnd < 0 && fresh.TryGetValue((s["slot"], s["owner"], s["epoch"], s["generation"], s["index"]), out life) && life.RowIndex < at && !retired.Contains(life), "quota_components_socket_lifetime");
                bool release = s["event"] == 1, end = s["end"] == 1;
                if (!release)
                {
                    Need(!end && !pendingEnd && at < abortAt && everSockets.Add(life) && liveSockets.Add(life), "quota_components_socket_acquire");
                    socketAcquired++;
                    peakSocket = Math.Max(peakSocket, (ulong)liveSockets.Count);
                }
                else
                {
                    Need(liveSockets.Remove(life), "quota_components_socket_return");
                    socketReturned++;
                    if (end)
                    {
                        Need(stackEnd >= 0 && at > stackEnd && lastClose < at && at < socketsAt, "quota_components_socket_end");
                        pendingEnd = true;
                    }
                    else
                    {
                        Need(!pendingEnd && at + 1 < rows.Length && rows[at + 1].StartsWith("M16_011_SLOT event=1 ", StringComparison.Ordinal), "quota_components_socket_return_order");
                        var next = SocketTransport(rows[at + 1]);
                        Need(next["slot"] == life.Slot && next["owner"] == life.Owner && next["epoch"] == life.Epoch && next["generation"] == life.Generation && next["index"] == life.Index, "quota_components_socket_return_order");
                    }
                }

                socketLeases.Add(new(at, release, life, rx, tx, end));
            }
            else if (line.StartsWith("M16_016_SOCKET_LEASES ", StringComparison.Ordinal))
            {
                const string suffix = " backing=retained";
                Need(line.EndsWith(suffix, StringComparison.Ordinal), "quota_components_socket_snapshot");
                var s = ReadRecord(line[..^suffix.Length], "M16_016_SOCKET_LEASES", "backingBytes:dec", "acquired:dec", "returned:dec", "live:dec", "rxBytes:dec", "txBytes:dec", "peakLive:dec", "peakRxBytes:dec", "peakTxBytes:dec", "end:dec");
                ulong live = (ulong)liveSockets.Count;
                bool end = s["end"] == 1;
                Need(s["end"] <= 1 && socketEnd < 0 && s["backingBytes"] == 8 * (rx + tx) && s["acquired"] == socketAcquired && s["returned"] == socketReturned && s["live"] == live && live <= 8 && s["rxBytes"] == live * rx && s["txBytes"] == live * tx && s["peakLive"] == peakSocket && s["peakRxBytes"] == peakSocket * rx && s["peakTxBytes"] == peakSocket * tx && (end ? stackEnd >= 0 && at > stackEnd && lastClose < at && at < socketsAt && live == 0 && socketAcquired > 0 : !pendingEnd && at < socketsAt && (sockets.Count == 0 || socketAcquired != sockets[^1].Acquired || socketReturned != sockets[^1].Returned)), "quota_components_socket_snapshot");
                if (end)
                    socketEnd = at;
                sockets.Add(new(at, s["backingBytes"], socketAcquired, socketReturned, live, s["rxBytes"], s["txBytes"], peakSocket, s["peakRxBytes"], s["peakTxBytes"], end));
            }
            else
                throw new ToolFailure("quota_components_unknown");
        }

        Need(backing is not null && stackEnd >= 0 && socketEnd > stackEnd && stacks.Count >= 2 && sockets.Count >= 2 && liveStacks.Count == 0 && stackLeases.All(l => l.ReapRecordIndex > l.CreateCallRecordIndex) && socketReturned == socketAcquired && socketAcquired <= (ulong)census.Length && stackLeases.Count <= rows.Length, "quota_components_complete");
        const string ownerSuffix = " process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
        Need(rows[ownerAt].EndsWith(ownerSuffix, StringComparison.Ordinal), "quota_components_owner");
        var owner = ReadRecord(rows[ownerAt][..^ownerSuffix.Length], "M16_008_OWNER_END", "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec", "userReserved:dec", "userBacked:dec");
        Need(owner["reaped"] == acquired && owner["exit"] == 99 && owner["userReserved"] == 0 && owner["userBacked"] == 0, "quota_components_owner");
        return new(backing!.Value, Array.AsReadOnly(stackLeases.ToArray()), Array.AsReadOnly(stacks.ToArray()), Array.AsReadOnly(socketLeases.ToArray()), Array.AsReadOnly(sockets.ToArray()));
    }
}
