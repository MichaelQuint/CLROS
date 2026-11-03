using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;
public static partial class LinuxSignalProofFixture
{
    /// <summary>Identity role and handler provenance for one emitted/reissued call.</summary>

    public readonly record struct WaitInvocation(uint Number, ulong ReturnRip, bool Child, bool Handler);
}
