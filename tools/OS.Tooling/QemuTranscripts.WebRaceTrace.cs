namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed class WebRaceTrace(WebSignalRaceSelection selection, int[] originalRows, bool eventOwned)
    {
        internal readonly WebSignalRaceSelection Selection = selection;
        internal readonly int[] OriginalRows = originalRows;
        internal readonly bool EventOwned = eventOwned;
        internal readonly List<WebRacePipe> Pipes = [];
        internal readonly List<WebRacePort> Ports = [];
        internal readonly List<WebRaceWatch> Watches = [];
        internal readonly List<WebRaceFrame> Frames = [];
        internal readonly List<WebSignalRaceSocketCall> SocketCalls = [];
    }
}
