using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed class VectorConnection(int index)
    {
        internal readonly int Index = index;
        internal uint Received, Sent;
        internal int Eof, Reset, Shutdown;
        internal bool Pending, Watched;
    }
}
