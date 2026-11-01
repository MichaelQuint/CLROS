namespace OS.Kernel.Compatibility.LinuxX64;
public unsafe partial struct LinuxAppLogObservation
{
    private struct StreamState
    {
        internal ulong Writer;
        internal uint Position, Worker, Record;
        internal bool Ignored, CrossedCall;
    }
}
