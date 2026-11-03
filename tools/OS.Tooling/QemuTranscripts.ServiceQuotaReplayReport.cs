namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Joined quota leaf values from original framed rows. Memory
    /// arithmetic may report a bound above the pool or entitlement; no fit is
    /// implied. Original native, independent peer/packet, process and exact issue
    /// acceptance remain separate requirements.</summary>

    public readonly record struct ServiceQuotaReplayReport(ThreadingProtocolReport Protocol, ServiceStartupInspection Startup, ServiceRandomInspection Random, ServiceQuotaTransportInspection Transport, ServiceQuotaMemoryInspection Memory, ServiceQuotaInspection Denial, int SocketAccepts, ulong SocketSent, ulong SocketReceived)
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
}
