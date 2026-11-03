namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private readonly record struct WebRaceFrame(ulong Identity, uint Signal, ulong Frame, int Index, bool Returned);
}
