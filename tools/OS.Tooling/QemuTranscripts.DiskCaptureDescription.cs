using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed class DiskCaptureDescription(WebRootImageFixture.DiskFile file)
    {
        internal readonly WebRootImageFixture.DiskFile File = file;
        internal ulong Position;
    }
}
