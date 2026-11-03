using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static partial class PlatformConsoleGuardTests
{
    private sealed class ConsoleSinkDouble : IPlatformConsoleSink
    {
        private readonly bool _available;
        public ConsoleSinkDouble(bool available)
        {
            _available = available;
        }

        public bool Available
        {
            get
            {
                AvailabilityReadCount++;
                return _available;
            }
        }

        public int AvailabilityReadCount
        {
            get; private set;
        }
        public int LineCount
        {
            get; private set;
        }
        public string LastLine { get; private set; } = string.Empty;

        public void WriteLine(string line)
        {
            LineCount++;
            LastLine = line;
        }
    }
}
