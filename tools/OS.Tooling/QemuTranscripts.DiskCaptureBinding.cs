using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed record DiskCaptureBinding(Dictionary<string, ulong> Disk, Dictionary<string, ulong> Budget, Dictionary<string, ulong> BudgetEnd, Dictionary<string, ulong> End);
}
