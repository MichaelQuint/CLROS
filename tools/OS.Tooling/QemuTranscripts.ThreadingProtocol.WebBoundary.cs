using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        private sealed class WebBoundary(string expected) : Exception
        {
            internal string Expected => expected;
        }
    }
}
