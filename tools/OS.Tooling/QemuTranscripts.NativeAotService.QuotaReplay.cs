namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Joined quota leaf values from original framed rows. Memory
    /// arithmetic may report a bound above the pool or entitlement; no fit is
    /// implied. Original native, independent peer/packet, process and exact issue
    /// acceptance remain separate requirements.</summary>
    public readonly record struct ServiceQuotaReplayReport(ThreadingProtocolReport Protocol, ServiceStartupInspection Startup,
        ServiceRandomInspection Random, ServiceQuotaTransportInspection Transport, ServiceQuotaMemoryInspection Memory,
        ServiceQuotaInspection Denial, int SocketAccepts, ulong SocketSent, ulong SocketReceived)
    {
        /// <summary>Copied original post-CALL socket byte positions, checked
        /// against transport census and complete descriptor/syscall replay.</summary>
        public IReadOnlyList<ServiceQuotaSocketIo> SocketIo { get; init; } = Array.Empty<ServiceQuotaSocketIo>();
        /// <summary>Present only after strict component records and complete
        /// scheduler generation/Tid/peak joins. Retained backing and borrower
        /// peaks remain separate; guest pthread bounds are not supplied.</summary>
        public ServiceQuotaComponentInspection? Components { get; init; }
        /// <summary>Default pthread logical extents and committed allocation
        /// history, joined to original topology and full clone/reap replay.
        /// TLS/tail sizing and failed-before-clone allocations stay separate.</summary>
        public ServiceQuotaPthreadInspection? Pthreads { get; init; }
    }

    private static ServiceQuotaReplayReport ReplayServiceQuota(NativeAotThreadingOutput.Capture framed, byte[] elf,
        Func<ulong,bool> instruction, Func<ulong,bool> syscallReturn, ulong userRoot, ulong kernelRoot, ulong entitlementBytes)
    {
        Need(framed.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "service_quota_replay_terminal");
        string[] original = framed.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        var denial = InspectNativeAotServiceQuotaForTests(original);
        var startup = ReadServiceStartup(framed, out var metadata, NativeAotServiceContract.QuotaFailure);
        var memory = ReadServiceQuotaMemory(framed, entitlementBytes, out var memoryPositions); metadata.UnionWith(memoryPositions);
        var random = ReadServiceRandom(original);
        var transport = ReadServiceQuotaTransport(original, out var transportPositions); metadata.UnionWith(transportPositions);
        Need(!framed.Stdout.Contains("M16_016_PTHREAD", StringComparison.Ordinal) &&
            !framed.Stderr.Contains("M16_016_PTHREAD", StringComparison.Ordinal), "pthread_channel");
        var pthreads = ReadServiceQuotaPthreads(original, out var pthreadPositions); metadata.UnionWith(pthreadPositions);
        var components = ReadServiceQuotaComponents(original, transport, out var componentPositions); metadata.UnionWith(componentPositions);
        var socketIo = ReadServiceQuotaSocketIo(original, transport.FreshLifetimes);
        for (int i = 0; i < original.Length; i++)
            if (original[i].StartsWith("M11_012_", StringComparison.Ordinal) ||
                original[i] == "M07_033_SEED source=firmware-rng acquisitions=1 secretBytes=32 domain=tcp-isn state=ready firmwareCallbacks=none") metadata.Add(i);
        int[] starts = Enumerable.Range(0, original.Length).Where(i => original[i] == "TEST_START m16-009").ToArray(),
            wires = Enumerable.Range(0, original.Length).Where(i => original[i].StartsWith("M16_011_WIRE ", StringComparison.Ordinal)).ToArray();
        Need(starts.Length == 1 && wires.Length == 1 && starts[0] < wires[0] &&
            original[wires[0]-1] == "M16_016_QUOTA_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true",
            "service_quota_replay_leaf_extent");
        // Only validated metadata is removed. The real denied CALL, abort,
        // descriptor closes, final memory and owner teardown stay in sequence.
        string[] rows = original.Where((_,i) => i >= starts[0] && i < wires[0] && !metadata.Contains(i)).ToArray();
        var capture = NativeAotSocketGuest.ReadServiceReadiness(new(framed.Stdout, string.Join("\r\n", rows)+"\r\n",
            framed.WriteLengths.ToArray(), framed.WriteChannels.ToArray(), framed.Stderr));
        var replay = new ThreadingProtocol(NativeAotServicePackage.Application, rows, elf, instruction, syscallReturn,
            framed.WriteLengths, userRoot, kernelRoot, socketCapture:capture, webSettingsBytes:startup.SettingsBytes,
            webSettingsFd:startup.SettingsDescriptor, webSettingsCall:(startup.SettingsIdentity,startup.SettingsOrdinal),
            configuredServiceMemory:true, serviceQuotaFailure:true);
        var protocol = replay.Validate(); replay.RequireQuotaComponents(components); replay.RequireQuotaPthreads(pthreads, memory);
        var counts = replay.ServiceSocketCounts();
        Need(socketIo.Count(c => c.Accept) == counts.Accepts &&
            socketIo.Where(c => !c.Accept && c.Response).Aggregate(0UL, (n, c) => n + c.Bytes) == counts.Sent &&
            socketIo.Where(c => !c.Accept && !c.Response && !c.Peek).Aggregate(0UL, (n, c) => n + c.Bytes) == counts.Received,
            "service_quota_socket_totals");
        Need(protocol.Calls >0 && (ulong)protocol.Calls >= random.Requests && protocol.Reaped == protocol.Clones+1 &&
            denial.Reaped == (ulong)protocol.Reaped && denial.Identity == memory.Denial.Identity && denial.Ordinal == memory.Denial.Ordinal &&
            denial.BackedPages == memory.Denial.BackedPages && denial.PoolPages *4096 == memory.PoolBytes &&
            counts.Accepts >=4 && transport.History.Connections >=4, "service_quota_replay_joins");
        return new(protocol, startup, random, transport, memory, denial, counts.Accepts, counts.Sent, counts.Received)
            { SocketIo = socketIo, Components = components, Pthreads = pthreads };
    }
}
