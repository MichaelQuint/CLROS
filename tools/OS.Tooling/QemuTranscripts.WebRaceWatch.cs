namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed record WebRaceWatch(WebProbePipe Pipe, SocketDescriptor Port, int Index);
}
