using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        private readonly bool serviceQuotaFailure;
        private readonly int quotaImageBytes;
        private bool quotaAborted;
        private ulong quotaBacked;
        private readonly Dictionary<SocketDescriptor, (ServiceQuotaSocketIdentity Connection, uint Description, ulong Inode)> quotaSockets = [];
        private readonly HashSet<ServiceQuotaSocketIdentity> quotaSocketConnections = [];
        // Called only after the complete ordinary CALL/descriptor model applied
        // its real result, before timing/wake records. Do not remove these rows
        // as unchecked metadata. Unsupported descriptor operations still fail
        // in the existing model before this observation can grant coverage.
        private void QuotaSocketObservation(Dictionary<string, ulong> call, LinuxCallReturnObservation returned)
        {
            ulong nr = call["nr"], v = call["valueBits"];
            bool accept = nr is 43 or 288 && unchecked((long)v) >= 0;
            ulong fd = accept ? v : call["a0"];
            socketDescriptors.TryGetValue(fd < 16 ? (int)fd : -1, out var descriptor);
            bool io = descriptor is { Port: false, Phase: 3 } && unchecked((long)v) > 0 && nr is 0 or 1 or 20 or 44 or 45 or 46 or 47;
            bool required = call["action"] == 1 && call["status"] == 1 && (accept || io);
            Need(required == Starts("M16_016_SOCKET "), "service_quota_socket_replay_presence");
            if (!required)
                return;
            var value = QuotaSocketRow(Take(), position - 1);
            QuotaSocketCallBinding(value, call, returned);
            Need(descriptor is { Port: false, Phase: 3 } && value.DescriptorGeneration == descriptorGenerations[value.Descriptor] && value.Sent == descriptor.Sent && value.Received == descriptor.Received, "service_quota_socket_replay_descriptor");
            if (accept)
            {
                Need(value.Offset == 0 && value.Sent == 0 && value.Received == 0 && quotaSocketConnections.Add(value.Connection) && quotaSockets.TryAdd(descriptor!, (value.Connection, value.Description, value.Inode)), "service_quota_socket_replay_accept");
            }

            Need(quotaSockets.TryGetValue(descriptor!, out var owner) && owner.Connection == value.Connection && owner.Description == value.Description && owner.Inode == value.Inode && value.Offset == (accept ? 0 : value.Response ? value.Sent - value.Bytes : value.Peek ? value.Received : value.Received - value.Bytes), "service_quota_socket_replay_owner");
        }

        private void QuotaAbort(RuntimeThread current, Dictionary<string, ulong> call, LinuxCallWaitObservation witness)
        {
            const string suffix = " exit=99 policy=terminate cause=physical-backing";
            string line = Take();
            Need(line.EndsWith(suffix, StringComparison.Ordinal), "service_quota_abort_policy");
            var row = ReadRecord(line[..^suffix.Length], "M16_016_QUOTA_ABORT", "identity:dec", "ordinal:dec", "nr:dec", "backed:dec", "pool:dec");
            Need(service && serviceQuotaFailure && !quotaAborted && !stopping && !current.Exiting && !current.Parking && call["nr"] == 9 && call["a0"] == 0 && call["a1"] == 1048576 && call["a2"] == 3 && call["a3"] == 0x22 && (uint)call["a4"] == uint.MaxValue && call["a5"] == 0 && call["valueBits"] == Bits(-12) && call["action"] == 1 && call["status"] == 1 && witness.Source == 0 && row["identity"] == current.Identity && row["ordinal"] == current.TotalCalls && row["ordinal"] == witness.Ordinal && row["nr"] == 9 && row["pool"] == memoryPoolPages && row["backed"] <= row["pool"] && row["pool"] - row["backed"] < 256 && budget.RequestCancellation(), "service_quota_abort_binding");
            quotaBacked = row["backed"];
            quotaAborted = stopping = true;
            // NativeAbort invokes the real process Shutdown99 after the CALL
            // was logged Return/Active. Kernel cancellation preserves prior
            // winners, queues waiting workers by slot, and does not resume user.
            foreach (var other in slots.Where(thread => thread is not null).Select(thread => thread!))
            {
                other.Exiting = other.GroupRetired = true;
                if (other.WaitSource != 0 && other.Completion is null)
                    other.Completion = -125;
                if (other != current && !ready.Contains(other))
                    Enqueue(other);
            }
        }

        private void QuotaCloseDescriptors()
        {
            Need(quotaAborted && budget.CancellationRequested && history.All(thread => thread.Reaped), "service_quota_native_close_phase");
            // DrainLinuxSocketNetwork closes all captured native descriptors in
            // ascending order, even on abort. Consume each actual close/result;
            // never clear live modeled descriptors from a summary assertion.
            foreach (int fd in Enumerable.Range(0, 16).Where(fd => fd < 3 || files.Contains(fd) || fd == pipeRead || fd == pipeWrite).ToArray())
            {
                SocketTransportObservations();
                Need(Starts("M16_011_CLOSE "), "service_quota_native_descriptor_observation_missing");
                var close = Row("M16_011_CLOSE", "fd:dec", "valueBits:dec");
                Need(close["fd"] == (ulong)fd && close["valueBits"] == 0, "service_quota_native_close");
                if (socketDescriptors.ContainsKey(fd))
                    SocketClose(fd);
                else
                    files.Remove(fd);
                if (fd == pipeRead)
                    pipeRead = 0;
                if (fd == pipeWrite)
                    pipeWrite = 0;
            }

            SocketTransportObservations();
            Need(socketDescriptors.Count == 0 && pipeRead == 0 && pipeWrite == 0 && files.Count == 0, "service_quota_native_descriptors");
        }

        private void QuotaMemoryEnd()
        {
            var row = Row("M16_016_MEMORY", "poolPages:dec", "peakPages:dec", "endPages:dec");
            Need(row["poolPages"] == memoryPoolPages && row["peakPages"] >= quotaBacked && row["peakPages"] <= memoryPoolPages && row["peakPages"] > 0 && row["endPages"] == 0, "service_quota_memory_end");
        }

        private void QuotaFinish()
        {
            const string suffix = " process=drained mappings=restored pool=scrubbed random=zero reservation=retained";
            string line = Take();
            Need(quotaAborted && budget.CancellationRequested && line.EndsWith(suffix, StringComparison.Ordinal), "service_quota_owner_cleanup");
            var row = ReadRecord(line[..^suffix.Length], "M16_014_END", "identity:dec", "calls:dec", "started:dec", "nanos:dec", "reaped:dec");
            Need(row["identity"] == servicePeakIdentity && row["calls"] == servicePeakCalls && servicePeakCalls > 0 && row["started"] == budget.StartedNanoseconds && row["nanos"] >= Math.Max(clock, serviceBudgetFinished) && row["nanos"] <= long.MaxValue && row["nanos"] >= row["started"] && row["reaped"] == (ulong)reaped && history.All(thread => thread.Reaped), "service_quota_owner_accounting");
            Exact("M16_016_QUOTA_PROCESS_FAILURE exit=99 cause=physical-backing");
            Exact("M16_016_QUOTA_NATIVE process=drained timer=restored descriptors=closed guard=detached mappings=restored pool=scrubbed initialStack=zero random=zero reservation=retained");
            Exact("M16_016_QUOTA_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");
        }
    }
}
