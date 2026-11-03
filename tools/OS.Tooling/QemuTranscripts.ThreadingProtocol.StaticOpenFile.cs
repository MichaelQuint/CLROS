using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        private sealed record StaticOpenFile(uint Generation, ulong Kind, ulong View, ulong Inode, ulong Bytes)
        {
            internal ulong Position;
        }
    }
}
