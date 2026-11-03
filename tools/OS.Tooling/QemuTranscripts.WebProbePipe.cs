namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed class WebProbePipe(int read, int write, uint readGeneration, uint writeGeneration)
    {
        internal int Read = read, Write = write, Buffered;
        internal readonly uint ReadGeneration = readGeneration, WriteGeneration = writeGeneration;
    }
}
