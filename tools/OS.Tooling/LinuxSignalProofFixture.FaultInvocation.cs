using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;
public static partial class LinuxSignalProofFixture
{
    /// <summary>Canonical actual fault instruction, saved hardware RIP/RSP and
    /// explicit repair continuation. These addresses bind mechanical package
    /// instructions only; trap/error/code expectations are independently checked
    /// by the transcript validator. Construction implies no guest execution.</summary>

    public readonly record struct FaultInvocation(ulong TriggerRip, ulong SavedRip, ulong ResumeRip, ulong SavedRsp);
}
