using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;
public static partial class LinuxSignalProofFixture
{
    /// <summary>Actual user SYSCALL instruction/continuation in execution order.
    /// Handler includes its restorer; synthetic kernel exits and exit99 assertion
    /// failures are excluded. The complete transcript must additionally bind
    /// dynamic PID, RSP and independently checked arguments/results.</summary>

    public readonly record struct Invocation(uint Number, ulong ReturnRip, bool Handler);
}
