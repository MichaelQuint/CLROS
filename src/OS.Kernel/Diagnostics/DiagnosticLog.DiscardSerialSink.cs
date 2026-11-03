namespace OS.Kernel.Diagnostics;

public sealed partial class DiagnosticLog
{
    private sealed class DiscardSerialSink : ISerialSink
    {
        public void WriteLine(string line)
        {
        }
    }
}
