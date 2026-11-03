using B = OS.Storage.WritableBlockSession;
using Combined = OS.Tooling.CombinedWebDataImageFixture;
using Data = OS.Tooling.WritableDataImageFixture;
using F = OS.Tooling.LinuxWritableDiskProofFixture;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed class MediaMask
    {
        internal readonly byte[] Bits = new byte[512];
        internal int ZeroTail = 512;
    }
}
