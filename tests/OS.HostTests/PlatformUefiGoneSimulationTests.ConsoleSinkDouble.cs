using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PlatformUefiGoneSimulationTests
{
    private sealed class ConsoleSinkDouble : IPlatformConsoleSink
    {
        public ConsoleSinkDouble(bool available)
        {
            Available = available;
        }

        public bool Available
        {
            get;
        }
        public int LineCount
        {
            get; private set;
        }

        public void WriteLine(string line)
        {
            LineCount++;
        }
    }
}
