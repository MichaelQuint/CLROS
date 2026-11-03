namespace OS.Tooling;
public static partial class NativeAotSocketContainmentGuest
{
    /// <summary>Owned immutable decoded text and read-only observations.
    /// Pending waits are observations, not proof that process cleanup drained.</summary>

    public sealed class Capture
    {
        internal Capture(NativeAotSocketGuest.Capture scheduling, NativeAotSocketContainmentOutput.Observation application, QemuTranscripts.SocketContainmentTransport transport)
        {
            Scheduling = scheduling;
            Application = application;
            Transport = transport;
        }

        public NativeAotSocketGuest.Capture Scheduling { get; }
        public NativeAotSocketContainmentOutput.Observation Application { get; }
        public QemuTranscripts.SocketContainmentTransport Transport { get; }
    }
}
