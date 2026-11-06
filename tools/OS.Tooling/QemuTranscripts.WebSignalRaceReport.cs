namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Immutable original-order projection after complete web replay.
    /// Indices address complete normalized original rows before metadata removal.
    /// Pointer contents, callback code roles, native joins, packets and owned
    /// process/guest acceptance remain separate. Legacy profiles permit missing
    /// native exits; the event-owned profile requires both dedicated probe
    /// threads' generation-bound native exits and reaps after their last CALL.</summary>

    public sealed partial class WebSignalRaceReport
    {
        public WebSignalRaceSelection Selection
        {
            get;
        }
        /// <summary>True only when complete replay checked the explicit
        /// event-owned named waits and repark. This is copied profile identity,
        /// not original native, source, process or guest authority.</summary>

        public bool EventOwned
        {
            get;
        }
        public Thread Waiter
        {
            get;
        }
        public Thread Actor
        {
            get;
        }
        public Thread Callback
        {
            get;
        }
        public Thread RecoveryWriter
        {
            get;
        }
        public DescriptorObservation ReadDescriptor
        {
            get;
        }
        public DescriptorObservation WriteDescriptor
        {
            get;
        }
        public DescriptorObservation EpollDescriptor
        {
            get;
        }
        public IReadOnlyList<Step> Steps
        {
            get;
        }
        /// <summary>Copied added-lifetime socket projections, emitted only after
        /// the entire existing replay succeeds. Ordinary sockets and probe-pipe
        /// calls are excluded; no quota-only M16_016_SOCKET row is fabricated.</summary>

        public IReadOnlyList<WebSignalRaceSocketCall> SocketCalls
        {
            get;
        }

        internal WebSignalRaceReport(WebSignalRaceSelection selection, Thread waiter, Thread actor, Thread callback, Thread writer, DescriptorObservation read, DescriptorObservation write, DescriptorObservation epoll, IEnumerable<Step> steps, IEnumerable<WebSignalRaceSocketCall> socketCalls, bool eventOwned = false)
        {
            Selection = selection with
            {
            };
            EventOwned = eventOwned;
            Waiter = waiter;
            Actor = actor;
            Callback = callback;
            RecoveryWriter = writer;
            ReadDescriptor = read;
            WriteDescriptor = write;
            EpollDescriptor = epoll;
            Steps = Array.AsReadOnly(steps.ToArray());
            SocketCalls = Array.AsReadOnly(socketCalls.ToArray());
        }
    }
}
