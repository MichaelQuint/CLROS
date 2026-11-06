namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed record WebRacePort(SocketDescriptor Port, int Descriptor, uint Generation, int Index);
}
