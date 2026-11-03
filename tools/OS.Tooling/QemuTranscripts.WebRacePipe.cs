namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed record WebRacePipe(WebProbePipe Pipe, int Read, int Write, uint ReadGeneration, uint WriteGeneration, int Index);
}
