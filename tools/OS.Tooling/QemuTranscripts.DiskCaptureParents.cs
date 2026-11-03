using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed record DiskCaptureParents(WebParentReport Parent, SocketNetworkProof Network, Dictionary<string, ulong> Clock, ulong[] OldPages, ulong[] NicBinding);
}
